using OPEN_LRD.Nodes;

namespace OPEN_LRD.NLP
{
    internal sealed record EntityResolution(
        string Label,
        string Lemma,
        NodeTypes NodeType,
        IReadOnlyList<string> Aliases,
        Guid? ResolvedNodeID,
        IReadOnlyList<Guid> CandidateNodeIDs,
        bool IsAmbiguous,
        bool IsPronoun,
        IReadOnlyList<string> Diagnostics);

    internal sealed class EntityResolver
    {
        private readonly LRD_Core core;
        private readonly ConversationContext? conversation;
        private readonly NlpOptions options;

        public EntityResolver(
            LRD_Core core,
            ConversationContext? conversation = null,
            NlpOptions? options = null)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            this.conversation = conversation;
            this.options = options ?? new NlpOptions();
        }

        public EntityResolution Resolve(FrameArgument argument)
        {
            ArgumentNullException.ThrowIfNull(argument);

            string label = BuildLabel(argument.SurfaceText);
            string lemma = string.IsNullOrWhiteSpace(argument.Lemma)
                ? label.ToLowerInvariant()
                : argument.Lemma;
            List<string> aliases = BuildAliases(label, lemma, argument.NodeType);

            if (argument.IsPronoun)
            {
                if (conversation is not null
                    && options.EnableConversationContext
                    && options.EnablePronounResolution)
                {
                    PronounResolution resolution = conversation.ResolvePronoun(
                        argument.SurfaceText,
                        argument.NodeType,
                        argument.Role,
                        core,
                        options);

                    if (resolution.NodeID.HasValue && !resolution.IsAmbiguous)
                    {
                        if (core.TryGetNode(resolution.NodeID.Value, out Web_Node? node) && node is not null)
                        {
                            string resolvedLabel = string.IsNullOrWhiteSpace(node.Label)
                                ? resolution.Label ?? label
                                : node.Label!;
                            List<string> mergedAliases = MergeAliases(resolvedLabel, node.Aliases);

                            foreach (string alias in aliases)
                                AddAlias(mergedAliases, resolvedLabel, alias);

                            return new EntityResolution(
                                resolvedLabel,
                                lemma,
                                node.Get_NodeType(),
                                mergedAliases,
                                node.ID,
                                new[] { node.ID },
                                false,
                                false,
                                resolution.Diagnostics);
                        }
                    }

                    if (resolution.IsAmbiguous)
                    {
                        return new EntityResolution(
                            label,
                            lemma,
                            argument.NodeType,
                            aliases,
                            null,
                            resolution.CandidateNodeIDs,
                            true,
                            false,
                            resolution.Diagnostics);
                    }

                    return new EntityResolution(
                        label,
                        lemma,
                        argument.NodeType,
                        aliases,
                        null,
                        Array.Empty<Guid>(),
                        false,
                        true,
                        resolution.Diagnostics);
                }

                return new EntityResolution(
                    label,
                    lemma,
                    argument.NodeType,
                    aliases,
                    null,
                    Array.Empty<Guid>(),
                    false,
                    true,
                    new[]
                    {
                        $"Pronoun '{argument.SurfaceText}' requires conversation context, but no context is available."
                    });
            }

            List<Web_Node> candidates = FindCandidates(label, argument.NodeType);

            if (candidates.Count == 0)
                candidates = FindCandidates(argument.SurfaceText, argument.NodeType);

            if (candidates.Count == 0)
                candidates = FindCandidates(lemma, argument.NodeType);

            if (candidates.Count == 0)
            {
                candidates = FindCandidates(label, null);

                if (candidates.Count == 0)
                    candidates = FindCandidates(argument.SurfaceText, null);

                if (candidates.Count == 0)
                    candidates = FindCandidates(lemma, null);
            }

            List<Web_Node> distinct = candidates
                .GroupBy(node => node.ID)
                .Select(group => group.First())
                .ToList();

            if (distinct.Count == 1)
            {
                Web_Node match = distinct[0];
                string resolvedLabel = string.IsNullOrWhiteSpace(match.Label) ? label : match.Label!;
                List<string> mergedAliases = MergeAliases(resolvedLabel, match.Aliases);

                foreach (string alias in aliases)
                    AddAlias(mergedAliases, resolvedLabel, alias);

                return new EntityResolution(
                    resolvedLabel,
                    lemma,
                    match.Get_NodeType(),
                    mergedAliases,
                    match.ID,
                    new[] { match.ID },
                    false,
                    false,
                    Array.Empty<string>());
            }

            if (distinct.Count > 1)
            {
                return new EntityResolution(
                    label,
                    lemma,
                    argument.NodeType,
                    aliases,
                    null,
                    distinct.Select(node => node.ID).ToArray(),
                    true,
                    false,
                    new[]
                    {
                        $"Ambiguous mention '{argument.SurfaceText}' matched {distinct.Count} nodes."
                    });
            }

            return new EntityResolution(
                label,
                lemma,
                argument.NodeType,
                aliases,
                null,
                Array.Empty<Guid>(),
                false,
                false,
                Array.Empty<string>());
        }

        public Guid CreateNodeId()
        {
            Guid nodeID;

            do
            {
                nodeID = Guid.NewGuid();
            }
            while (nodeID == Guid.Empty || core.TryGetNode(nodeID, out _));

            return nodeID;
        }

        private List<Web_Node> FindCandidates(string label, NodeTypes? type)
        {
            if (string.IsNullOrWhiteSpace(label))
                return new List<Web_Node>();

            return core.FindByLabel(label, type).ToList();
        }

        private static string BuildLabel(string surfaceText)
        {
            if (string.IsNullOrWhiteSpace(surfaceText))
                return string.Empty;

            string[] parts = surfaceText.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            int start = 0;

            if (parts.Length > 1 && Lexicon.IsDeterminer(parts[0]))
                start = 1;

            return string.Join(" ", parts.Skip(start)).Trim();
        }

        private static List<string> BuildAliases(string label, string lemma, NodeTypes nodeType)
        {
            List<string> aliases = new List<string>();

            if (!string.IsNullOrWhiteSpace(lemma)
                && lemma.Length > 1
                && lemma.All(char.IsLetter)
                && !Lexicon.IsDeterminer(lemma))
            {
                AddAlias(aliases, label, lemma);
            }

            if (nodeType == NodeTypes.Entities
                || nodeType == NodeTypes.Locations
                || nodeType == NodeTypes.Events)
            {
                AddAlias(aliases, label, label.ToLowerInvariant());
            }

            return aliases;
        }

        private static List<string> MergeAliases(
            string label,
            IReadOnlyCollection<string> existingAliases)
        {
            List<string> aliases = new List<string>(existingAliases.Count);

            foreach (string alias in existingAliases)
                AddAlias(aliases, label, alias);

            return aliases;
        }

        private static void AddAlias(List<string> aliases, string label, string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return;

            string trimmed = alias.Trim();

            if (trimmed.Length == 0
                || string.Equals(trimmed, label, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (aliases.Any(existing =>
                string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            aliases.Add(trimmed);
        }
    }
}
