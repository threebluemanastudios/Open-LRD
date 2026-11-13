using System.IO;
using System.Text.RegularExpressions;

namespace OPEN_LRD.Ontology.Gcide
{
    // Streams GCIDE paragraphs and turns each headword paragraph into a
    // GcideEntry. Files are read one at a time so the full 61 MB corpus is
    // never held in memory at once.
    internal static class GcideEntryReader
    {
        private static readonly string[] TaxonomyTags =
        {
            "kingdom", "phylum", "subphylum", "class", "subclass",
            "ord", "subord", "suborder", "fam", "subfam", "gen", "spn"
        };

        private static readonly Regex SynonymPrefix =
            new(@"^\s*Syn\.?\s*--\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IEnumerable<GcideEntry> Read(string directory, char? letter = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("A corpus directory is required.", nameof(directory));

            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException($"The corpus directory was not found: {directory}");

            foreach (string path in EnumerateFiles(directory, letter))
            {
                string text = File.ReadAllText(path);

                foreach (GcideEntry entry in ParseDocument(text))
                    yield return entry;
            }
        }

        public static IEnumerable<string> EnumerateFiles(string directory, char? letter = null)
        {
            IEnumerable<string> files = Directory.EnumerateFiles(directory, "CIDE.*");

            if (letter.HasValue)
            {
                string suffix = "." + char.ToUpperInvariant(letter.Value);
                files = files.Where(path => path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            }

            return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        public static IEnumerable<GcideEntry> ParseDocument(string text)
        {
            int index = 0;

            while (index < text.Length)
            {
                int start = text.IndexOf("<p>", index, StringComparison.Ordinal);

                if (start < 0)
                    break;

                int end = text.IndexOf("</p>", start, StringComparison.Ordinal);

                if (end < 0)
                    end = text.Length;

                string block = text.Substring(start + 3, end - start - 3);

                if (block.Contains("<hw>", StringComparison.Ordinal))
                {
                    GcideEntry? entry = ParseEntry(block);

                    if (entry is not null)
                        yield return entry;
                }

                index = end + 4;
            }
        }

        public static GcideEntry? ParseEntry(string block)
        {
            string index = GcideMarkup.CleanText(ExtractFirst(block, "ent") ?? string.Empty);
            string headword = index;

            if (headword.Length == 0)
                headword = GcideMarkup.CleanText(ExtractFirst(block, "hw") ?? string.Empty);

            if (headword.Length == 0)
                return null;

            List<string> partsOfSpeech = new();
            HashSet<string> seenPos = new(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in ExtractAll(block, "pos"))
            {
                foreach (string token in raw.Split(new[] { '&', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string? canonical = OntologyCatalog.PartOfSpeechFromAbbreviation(token);

                    if (canonical is not null && seenPos.Add(canonical))
                        partsOfSpeech.Add(canonical);
                }
            }

            List<GcideSense> senses = ParseSenses(block);
            List<string> fields = ParsePhrases(ExtractAll(block, "fld")).ToList();
            List<string> taxa = new();

            foreach (string tag in TaxonomyTags)
                taxa.AddRange(ParsePhrases(ExtractAll(block, tag)));

            return new GcideEntry(
                Headword: headword,
                Index: index.Length > 0 ? index : headword,
                PartsOfSpeech: partsOfSpeech,
                Senses: senses,
                Synonyms: ParseList(ExtractAll(block, "syn")),
                AlternativeNames: ParsePhrases(ExtractAll(block, "altname")),
                AlternativeSpellings: ParsePhrases(ExtractAll(block, "asp")),
                Antonyms: ParsePhrases(ExtractAll(block, "ant")),
                Subtypes: ParsePhrases(ExtractAll(block, "stype")),
                Supertypes: ParsePhrases(ExtractAll(block, "isa")),
                Fields: fields,
                Taxa: taxa.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }

        private static List<GcideSense> ParseSenses(string block)
        {
            List<GcideSense> senses = new();
            int searchFrom = 0;

            while (true)
            {
                int defStart = block.IndexOf("<def>", searchFrom, StringComparison.Ordinal);

                if (defStart < 0)
                    break;

                int defEnd = block.IndexOf("</def>", defStart, StringComparison.Ordinal);
                string header = block.Substring(searchFrom, defStart - searchFrom);
                string body = defEnd < 0
                    ? block.Substring(defStart + 5)
                    : block.Substring(defStart + 5, defEnd - defStart - 5);

                string number = GcideMarkup.CleanText(ExtractLast(header, "sn") ?? string.Empty);
                string definition = GcideMarkup.CleanText(body);

                if (definition.Length > 0)
                {
                    string? field = ExtractFirst(header, "fld") ?? ExtractFirst(body, "fld");
                    string? source = ExtractFirst(body, "source");

                    senses.Add(new GcideSense(
                        number,
                        definition,
                        field is null ? null : GcideMarkup.CleanText(field),
                        source is null ? null : GcideMarkup.CleanText(source)));
                }

                searchFrom = defEnd < 0 ? block.Length : defEnd + 6;
            }

            return senses;
        }

        private static IReadOnlyList<string> ParseList(IEnumerable<string> rawValues)
        {
            List<string> results = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in rawValues)
            {
                string cleaned = SynonymPrefix.Replace(GcideMarkup.CleanText(raw), string.Empty);

                foreach (string part in cleaned.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string value = part.Trim();

                    if (value.Length > 0 && seen.Add(value))
                        results.Add(value);
                }
            }

            return results;
        }

        private static IReadOnlyList<string> ParsePhrases(IEnumerable<string> rawValues)
        {
            List<string> results = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

            foreach (string raw in rawValues)
            {
                string value = GcideMarkup.CleanText(raw);

                if (value.Length > 0 && seen.Add(value))
                    results.Add(value);
            }

            return results;
        }

        private static IEnumerable<string> ExtractAll(string block, string tag)
        {
            string open = "<" + tag + ">";
            string close = "</" + tag + ">";
            int index = 0;

            while (index < block.Length)
            {
                int start = block.IndexOf(open, index, StringComparison.Ordinal);

                if (start < 0)
                    yield break;

                int contentStart = start + open.Length;
                int end = block.IndexOf(close, contentStart, StringComparison.Ordinal);

                if (end < 0)
                    yield break;

                yield return block.Substring(contentStart, end - contentStart);
                index = end + close.Length;
            }
        }

        private static string? ExtractFirst(string block, string tag)
        {
            foreach (string value in ExtractAll(block, tag))
                return value;

            return null;
        }

        private static string? ExtractLast(string block, string tag)
        {
            string? result = null;

            foreach (string value in ExtractAll(block, tag))
                result = value;

            return result;
        }
    }
}
