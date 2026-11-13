using System.Text;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Ontology.Gcide
{
    // Maps parsed GCIDE entries onto the graph. The loader writes permanent
    // Lexeme and Sense nodes plus the structural links defined by Module 4.
    // Existing permanent nodes are reused, so a repeated load is idempotent.
    internal sealed class GcideCorpusLoader
    {
        private const int DefinitionPreviewLength = 200;
        private const int ProgressInterval = 100;

        private readonly LRD_Core core;
        private readonly GcideLoadOptions options;

        private readonly Dictionary<string, Guid> lexemes =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Guid> senses =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Guid> categories =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Guid> fields =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Guid> taxa =
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        private int entriesRead;
        private int entriesLoaded;
        private int wordNodes;
        private int senseNodes;
        private int linksCreated;
        private int skippedRelations;

        private GcideCorpusLoader(LRD_Core core, GcideLoadOptions options)
        {
            this.core = core;
            this.options = options;
        }

        public static GcideLoadResult Load(LRD_Core core, GcideLoadOptions options)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);

            return new GcideCorpusLoader(core, options).Run();
        }

        public static string NormalizeKey(string value)
        {
            string clean = GcideMarkup.CleanText(value);

            if (clean.Length == 0)
                return string.Empty;

            StringBuilder builder = new StringBuilder(clean.Length);
            bool pendingSpace = false;

            foreach (char character in clean.ToLowerInvariant())
            {
                // Apostrophes join the surrounding letters ("don't" -> "dont").
                if (character is '\'' or '\u2019')
                    continue;

                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSpace && builder.Length > 0)
                        builder.Append(' ');

                    builder.Append(character);
                    pendingSpace = false;
                    continue;
                }

                // Spaces, hyphens, and punctuation all collapse to one space so
                // "ice-box" and "ice box" share a Lexeme.
                pendingSpace = builder.Length > 0;
            }

            return builder.ToString();
        }

        private GcideLoadResult Run()
        {
            if (!core.initialized)
                throw new InvalidOperationException(
                    "The core must be initialized before loading the GCIDE corpus.");

            if (string.IsNullOrWhiteSpace(options.CorpusDirectory))
                throw new ArgumentException("A corpus directory is required.", nameof(options));

            if (options.Subset == GcideSubset.ByLetter && !options.Letter.HasValue)
                throw new ArgumentException("ByLetter requires a letter.", nameof(options));

            OntologyRelations.EnsureRegistered();
            IndexExistingNodes();

            char? letter = options.Subset == GcideSubset.ByLetter ? options.Letter : null;

            foreach (GcideEntry entry in GcideEntryReader.Read(options.CorpusDirectory, letter))
            {
                options.CancellationToken.ThrowIfCancellationRequested();
                entriesRead++;

                if (options.Subset == GcideSubset.SemanticOnly && !entry.HasSemanticRelations)
                {
                    ReportProgress(entry);
                    continue;
                }

                LoadEntry(entry);
                entriesLoaded++;
                ReportProgress(entry);

                if (options.MaxEntries > 0 && entriesLoaded >= options.MaxEntries)
                    break;
            }

            options.Progress?.Invoke(entriesLoaded, "Complete");

            return new GcideLoadResult(
                EntriesRead: entriesRead,
                EntriesLoaded: entriesLoaded,
                WordNodes: wordNodes,
                SenseNodes: senseNodes,
                LinksCreated: linksCreated,
                SkippedRelations: skippedRelations);
        }

        private void ReportProgress(GcideEntry entry)
        {
            if (entriesRead % ProgressInterval == 0)
                options.Progress?.Invoke(entriesLoaded, entry.Index);
        }

        // Builds label indexes once so a reload does not scan the whole graph
        // for every entry. The full corpus is roughly 300k nodes.
        private void IndexExistingNodes()
        {
            foreach (Web_Node node in core.FindNodes())
            {
                NodeTypes type = node.Get_NodeType();

                if (type == OntologyTypes.Lexemes)
                {
                    IndexNode(node, lexemes);
                }
                else if (type == OntologyTypes.Senses)
                {
                    IndexNode(node, senses);
                }
                else if (type == OntologyTypes.Categories)
                {
                    IndexNode(node, categories);
                }
                else if (type == OntologyTypes.Fields)
                {
                    IndexNode(node, fields);
                }
                else if (type == OntologyTypes.Taxa)
                {
                    IndexNode(node, taxa);
                }
            }
        }

        private static void IndexNode(Web_Node node, Dictionary<string, Guid> index)
        {
            if (node.Label is string label)
                index.TryAdd(label, node.Get_ID());

            foreach (string alias in node.Aliases)
                index.TryAdd(alias, node.Get_ID());
        }

        private void LoadEntry(GcideEntry entry)
        {
            string key = NormalizeKey(entry.Index.Length > 0 ? entry.Index : entry.Headword);

            if (key.Length == 0)
                return;

            Guid lexeme = GetOrCreateLexeme(key, entry.Headword);

            if (lexeme == Guid.Empty)
                return;

            AddAlias(lexeme, entry.Headword);

            foreach (string spelling in entry.AlternativeSpellings)
                AddAlias(lexeme, spelling);

            foreach (string name in entry.AlternativeNames)
                AddAlias(lexeme, name);

            foreach (string partOfSpeech in entry.PartsOfSpeech)
            {
                Guid part = GetOrCreateCategory(partOfSpeech);
                AddDirected(lexeme, part, OntologyTypes.lexicon, OntologyRelations.HasPos);
            }

            for (int index = 0; index < entry.Senses.Count; index++)
            {
                GcideSense sense = entry.Senses[index];
                Guid senseNode = GetOrCreateSense(lexeme, key, entry, sense, index + 1);

                AddDirected(lexeme, senseNode, OntologyTypes.lexicon, OntologyRelations.HasSense);
                AddDirected(senseNode, lexeme, OntologyTypes.lexicon, OntologyRelations.IsNamed);

                string? fieldLabel = sense.Field
                    ?? (entry.Fields.Count > 0 ? entry.Fields[0] : null);

                if (fieldLabel is not null)
                {
                    Guid field = GetOrCreateLabeledNode(
                        OntologyTypes.Fields,
                        fieldLabel.ToLowerInvariant(),
                        fields);
                    AddDirected(senseNode, field, OntologyTypes.properties, OntologyRelations.HasField);
                }
            }

            // <isa> names a supertype of the headword, so the headword is the
            // subtype. <stype> names a subtype of the headword, so the target
            // is the subtype. Hyponymy is tracked between Lexemes.
            Guid anchor = lexeme;
            foreach (string supertype in entry.Supertypes)
                AddIsA(anchor, supertype, targetIsSubtype: false);

            foreach (string subtype in entry.Subtypes)
                AddIsA(anchor, subtype, targetIsSubtype: true);

            foreach (string taxon in entry.Taxa)
                AddIsA(anchor, taxon, targetIsSubtype: false, OntologyTypes.taxonomy, taxa);

            foreach (string synonym in entry.Synonyms)
                AddSymmetric(lexeme, synonym, OntologyRelations.Synonym);

            foreach (string antonym in entry.Antonyms)
                AddSymmetric(lexeme, antonym, OntologyRelations.Antonym);

            foreach (string fieldLabel in entry.Fields)
            {
                Guid field = GetOrCreateLabeledNode(
                    OntologyTypes.Fields,
                    fieldLabel.ToLowerInvariant(),
                    fields);
                AddDirected(lexeme, field, OntologyTypes.properties, OntologyRelations.HasField);
            }
        }

        private Guid GetOrCreateLexeme(string key, string displayForm)
        {
            if (lexemes.TryGetValue(key, out Guid existing))
            {
                EnsurePermanent(existing);
                AddAlias(existing, displayForm);
                return existing;
            }

            Web_Node node = new Web_Node(
                Guid.NewGuid(),
                OntologyTypes.Lexemes,
                NodeLifetime.Permanent,
                0);
            node.Set_Label(key);
            node.Add_Alias(displayForm);

            if (!core.AddExistingNode(node))
                return Guid.Empty;

            lexemes[key] = node.Get_ID();
            AddAlias(node.Get_ID(), key);
            wordNodes++;
            return node.Get_ID();
        }

        private Guid GetOrCreateSense(
            Guid lexeme,
            string wordKey,
            GcideEntry entry,
            GcideSense sense,
            int ordinal)
        {
            string senseKey = BuildSenseKey(wordKey, entry, sense, ordinal);

            if (senses.TryGetValue(senseKey, out Guid existing))
            {
                EnsurePermanent(existing);
                AddAlias(existing, senseKey);
                return existing;
            }

            string preview = Preview(sense.Definition);

            Web_Node node = new Web_Node(
                Guid.NewGuid(),
                OntologyTypes.Senses,
                NodeLifetime.Permanent,
                0);
            node.Set_Label(preview.Length > 0 ? preview : senseKey);
            node.Add_Alias(senseKey);

            if (!core.AddExistingNode(node))
                return Guid.Empty;

            senses[senseKey] = node.Get_ID();
            senseNodes++;
            return node.Get_ID();
        }

        private static string BuildSenseKey(
            string wordKey,
            GcideEntry entry,
            GcideSense sense,
            int ordinal)
        {
            string partOfSpeech = entry.PartsOfSpeech.Count > 0
                ? NormalizeKey(entry.PartsOfSpeech[0])
                : "x";

            string number = NormalizeSenseNumber(sense.Number);

            if (number.Length == 0)
                number = ordinal.ToString();

            return wordKey + "#" + partOfSpeech + "." + number;
        }

        private static string NormalizeSenseNumber(string number)
        {
            StringBuilder builder = new StringBuilder(number.Length);

            foreach (char character in number)
            {
                if (char.IsDigit(character))
                    builder.Append(character);
            }

            return builder.ToString();
        }

        private static string Preview(string definition)
        {
            string cleaned = GcideMarkup.CleanText(definition);

            if (cleaned.Length <= DefinitionPreviewLength)
                return cleaned;

            return cleaned.Substring(0, DefinitionPreviewLength).TrimEnd() + "\u2026";
        }

        private void AddAlias(Guid nodeID, string alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return;

            Web_Node? node = core.GetExistingNode(nodeID);
            node?.Add_Alias(alias);
        }

        private void EnsurePermanent(Guid nodeID)
        {
            Web_Node? node = core.GetExistingNode(nodeID);

            if (node is not null && node.IsTemporary)
                node.PromoteToPermanent();
        }

        private Guid GetOrCreateCategory(string label)
        {
            return GetOrCreateLabeledNode(
                OntologyTypes.Categories,
                label,
                categories,
                permanent: true);
        }

        private Guid GetOrCreateLabeledNode(
            NodeTypes type,
            string label,
            Dictionary<string, Guid> index,
            bool permanent = true)
        {
            string key = GcideMarkup.CleanText(label);

            if (key.Length == 0)
                return Guid.Empty;

            if (index.TryGetValue(key, out Guid existing))
            {
                if (permanent)
                    EnsurePermanent(existing);

                return existing;
            }

            Web_Node node = new Web_Node(
                Guid.NewGuid(),
                type,
                permanent ? NodeLifetime.Permanent : NodeLifetime.Temporary,
                permanent ? 0 : Node.DefaultTimeToLive);
            node.Set_Label(key);

            if (!core.AddExistingNode(node))
                return Guid.Empty;

            index[key] = node.Get_ID();
            return node.Get_ID();
        }

        // Resolves a relation target to a Lexeme. Unresolved targets create a
        // temporary placeholder when enabled; otherwise they are skipped and
        // counted as diagnostics.
        private Guid? ResolveLexemeTarget(string phrase, string sourceKey)
        {
            string key = NormalizeKey(phrase);

            if (key.Length == 0 || string.Equals(key, sourceKey, StringComparison.OrdinalIgnoreCase))
                return null;

            if (lexemes.TryGetValue(key, out Guid existing))
                return existing;

            if (!options.CreatePlaceholderTargets)
            {
                skippedRelations++;
                return null;
            }

            Web_Node node = new Web_Node(
                Guid.NewGuid(),
                OntologyTypes.Lexemes,
                NodeLifetime.Temporary,
                Node.DefaultTimeToLive);
            node.Set_Label(key);

            if (!core.AddExistingNode(node))
            {
                skippedRelations++;
                return null;
            }

            lexemes[key] = node.Get_ID();
            wordNodes++;
            return node.Get_ID();
        }

        private void AddIsA(
            Guid anchor,
            string targetPhrase,
            bool targetIsSubtype,
            LinkClass? linkClass = null,
            Dictionary<string, Guid>? targetIndex = null)
        {
            if (targetIndex is null)
            {
                Guid? target = ResolveLexemeTarget(targetPhrase, GetLexemeKey(anchor));

                if (target is null)
                    return;

                if (targetIsSubtype)
                    AddDirected(target.Value, anchor, linkClass ?? OntologyTypes.ontology, OntologyRelations.IsA);
                else
                    AddDirected(anchor, target.Value, linkClass ?? OntologyTypes.ontology, OntologyRelations.IsA);

                return;
            }

            string label = GcideMarkup.CleanText(targetPhrase);

            if (label.Length == 0)
            {
                skippedRelations++;
                return;
            }

            Guid targetNode = GetOrCreateLabeledNode(OntologyTypes.Taxa, label, targetIndex);
            AddDirected(anchor, targetNode, linkClass ?? OntologyTypes.taxonomy, OntologyRelations.IsA);
        }

        private string GetLexemeKey(Guid anchor)
        {
            Web_Node? node = core.GetExistingNode(anchor);
            return node?.Label ?? string.Empty;
        }

        private void AddSymmetric(Guid source, string targetPhrase, int relationId)
        {
            Guid? target = ResolveLexemeTarget(targetPhrase, GetLexemeKey(source));

            if (target is null)
                return;

            AddDirected(source, target.Value, OntologyTypes.lexicon, relationId);
            AddDirected(target.Value, source, OntologyTypes.lexicon, relationId);
        }

        // Adds one directed link unless the exact link already exists. Returns
        // true when the link is present afterwards.
        private bool AddDirected(Guid sourceID, Guid destinationID, LinkClass linkClass, int relationId)
        {
            if (sourceID == Guid.Empty || destinationID == Guid.Empty || sourceID == destinationID)
                return false;

            Web_Node? source = core.GetExistingNode(sourceID);

            if (source is null)
                return false;

            foreach (Link link in source.Get_ReferenceLinks())
            {
                if (link.DestinationID == destinationID && link.ForwardLink.Id == relationId)
                    return true;
            }

            if (!core.AddDirectedConnection(sourceID, destinationID, linkClass, relationId))
                return false;

            linksCreated++;
            return true;
        }
    }
}
