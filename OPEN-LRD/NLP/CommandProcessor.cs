using OPEN_LRD.Nodes;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    internal static class CommandProcessor
    {
        public static NlpAnswer CreateClarification(NlpCommand command)
        {
            string text = string.IsNullOrWhiteSpace(command.Clarification)
                ? $"What should I {command.Verb}?"
                : command.Clarification!;
            IReadOnlyList<string> diagnostics = command.Diagnostics.Count > 0
                ? command.Diagnostics
                : new[] { text };

            return CreateAnswer(
                AnswerStatus.Ambiguous,
                text,
                null,
                null,
                false,
                false,
                0.0,
                diagnostics);
        }

        public static NlpAnswer ExecuteFixed(
            NlpCommand command,
            LRD_Core core,
            ConversationContext conversation,
            NlpOptions options)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(options);

            return command.TargetKind switch
            {
                CommandTargetKind.Facts => BuildFacts(core, options),
                CommandTargetKind.Graph => BuildGraph(core),
                CommandTargetKind.Conversation => BuildConversation(conversation),
                CommandTargetKind.Summary => BuildSummary(core, conversation),
                CommandTargetKind.Node => BuildNode(command, core, options),
                _ => CreateClarification(command)
            };
        }

        private static NlpAnswer BuildFacts(LRD_Core core, NlpOptions options)
        {
            List<Web_Node> nodes = core
                .FindNodes()
                .OrderBy(GetNodeLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(node => node.ID)
                .ToList();
            List<(string Source, string Relation, string Target, string LinkClass)> rows =
                new List<(string Source, string Relation, string Target, string LinkClass)>();

            foreach (Web_Node node in nodes)
            {
                foreach (Nodes.Links.Link link in core.GetLinks(node.ID))
                {
                    if (link.SourceID != node.ID)
                        continue;

                    rows.Add((
                        GetNodeLabel(core, link.SourceID),
                        link.ForwardLink.Name,
                        GetNodeLabel(core, link.DestinationID),
                        link.Classification.ToString()));
                }
            }

            rows = rows
                .OrderBy(row => row.Source, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Relation, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Target, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string text = rows.Count == 0
                ? "No facts are currently stored."
                : string.Join(
                    Environment.NewLine,
                    rows.Select(row =>
                        $"{row.Source} --{row.Relation}--> {row.Target} ({row.LinkClass})"));
            List<string> diagnostics = new List<string>();
            text = LimitText(text, options, diagnostics);

            return CreateAnswer(
                AnswerStatus.Answered,
                text,
                $"{rows.Count} fact(s)",
                null,
                nodes.Any(node => node.IsTemporary),
                false,
                0.90,
                diagnostics);
        }

        private static NlpAnswer BuildGraph(LRD_Core core)
        {
            IReadOnlyList<Web_Node> nodes = core.FindNodes();
            int linkCount = nodes.Sum(
                node => core.GetLinks(node.ID).Count(link => link.SourceID == node.ID));
            List<string> typeCounts = nodes
                .GroupBy(node => node.Get_NodeType())
                .OrderBy(group => group.Key.ToString(), StringComparer.OrdinalIgnoreCase)
                .Select(group => $"{group.Key}={group.Count()}")
                .ToList();
            string text = $"Graph: {nodes.Count} nodes, {linkCount} directed links.";

            if (typeCounts.Count > 0)
                text += Environment.NewLine + "Types: " + string.Join(", ", typeCounts);

            return CreateAnswer(
                AnswerStatus.Answered,
                text,
                $"{nodes.Count} nodes, {linkCount} links",
                null,
                nodes.Any(node => node.IsTemporary),
                false,
                0.95,
                Array.Empty<string>());
        }

        private static NlpAnswer BuildConversation(ConversationContext conversation)
        {
            string text =
                $"Conversation {conversation.ConversationID}: " +
                $"turns {conversation.TurnIndex}, " +
                $"focus {FormatGuid(conversation.FocusNodeID)}, " +
                $"mentions {conversation.Mentions.Count}, " +
                $"provenance {conversation.Provenance.Count}." +
                Environment.NewLine +
                $"Last subject {FormatGuid(conversation.LastSubjectNodeID)}, " +
                $"object {FormatGuid(conversation.LastObjectNodeID)}, " +
                $"qualifier {FormatGuid(conversation.LastQualifierNodeID)}, " +
                $"answer {FormatGuid(conversation.LastAnswerNodeID)}.";

            return CreateAnswer(
                AnswerStatus.Answered,
                text,
                $"{conversation.TurnIndex} turns",
                null,
                false,
                conversation.Provenance.Count > 0,
                0.95,
                Array.Empty<string>());
        }

        private static NlpAnswer BuildSummary(LRD_Core core, ConversationContext conversation)
        {
            NlpAnswer graph = BuildGraph(core);
            NlpAnswer session = BuildConversation(conversation);
            List<string> diagnostics = new List<string>();
            diagnostics.AddRange(graph.Diagnostics);
            diagnostics.AddRange(session.Diagnostics);

            return CreateAnswer(
                AnswerStatus.Answered,
                graph.Text + Environment.NewLine + session.Text,
                "Graph and conversation summary",
                null,
                graph.UsesTemporaryEvidence,
                session.UsesSessionProvenance,
                0.95,
                diagnostics);
        }

        private static NlpAnswer BuildNode(
            NlpCommand command,
            LRD_Core core,
            NlpOptions options)
        {
            if (!command.NodeID.HasValue
                || !core.TryGetNode(command.NodeID.Value, out Web_Node? node)
                || node is null)
            {
                string id = command.NodeID?.ToString() ?? command.TargetText;
                string message = $"I couldn't find node '{id}'. Which node ID should I show?";

                return CreateAnswer(
                    AnswerStatus.NoMatch,
                    message,
                    null,
                    null,
                    false,
                    false,
                    0.0,
                    new[] { message });
            }

            string label = GetNodeLabel(node);
            List<string> lines = new List<string>
            {
                $"Node {node.ID}: {label}",
                $"Type: {node.Get_NodeType()}",
                $"Lifetime: {node.Get_Lifetime()}",
                $"Aliases: {(node.Aliases.Count == 0 ? "(none)" : string.Join(", ", node.Aliases))}"
            };
            List<Nodes.Links.Link> outgoing = core
                .GetLinks(node.ID)
                .Where(link => link.SourceID == node.ID)
                .OrderBy(link => GetNodeLabel(core, link.DestinationID), StringComparer.OrdinalIgnoreCase)
                .ThenBy(link => link.ForwardLink.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<Nodes.Links.Link> incoming = core
                .GetLinks(node.ID)
                .Where(link => link.DestinationID == node.ID)
                .OrderBy(link => GetNodeLabel(core, link.SourceID), StringComparer.OrdinalIgnoreCase)
                .ThenBy(link => link.ForwardLink.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            lines.Add("Outgoing:");

            if (outgoing.Count == 0)
                lines.Add("  (none)");
            else
                lines.AddRange(outgoing.Select(link =>
                    $"  --{link.ForwardLink.Name}--> {GetNodeLabel(core, link.DestinationID)}"));

            lines.Add("Incoming:");

            if (incoming.Count == 0)
                lines.Add("  (none)");
            else
                lines.AddRange(incoming.Select(link =>
                    $"  <--{link.ForwardLink.Name}-- {GetNodeLabel(core, link.SourceID)}"));

            List<string> diagnostics = new List<string>();
            string text = LimitText(string.Join(Environment.NewLine, lines), options, diagnostics);

            return CreateAnswer(
                AnswerStatus.Answered,
                text,
                label,
                node.ID,
                node.IsTemporary,
                false,
                0.95,
                diagnostics);
        }

        private static NlpAnswer CreateAnswer(
            AnswerStatus status,
            string text,
            string? shortAnswer,
            Guid? answerNodeID,
            bool usesTemporaryEvidence,
            bool usesSessionProvenance,
            double confidence,
            IReadOnlyList<string> diagnostics)
        {
            return new NlpAnswer(
                status,
                AnswerKind.Description,
                text,
                shortAnswer,
                answerNodeID,
                Array.Empty<AnswerCandidate>(),
                Array.Empty<GraphPath>(),
                null,
                null,
                usesTemporaryEvidence,
                confidence,
                diagnostics,
                usesSessionProvenance);
        }

        private static string LimitText(
            string text,
            NlpOptions options,
            List<string> diagnostics)
        {
            int maxLength = Math.Max(1, options.MaxAnswerTextLength);

            if (text.Length <= maxLength)
                return text;

            diagnostics.Add($"Command output was truncated to {maxLength} characters.");

            if (maxLength <= 3)
                return text[..maxLength];

            return text[..(maxLength - 3)] + "...";
        }

        private static string GetNodeLabel(LRD_Core core, Guid nodeID)
        {
            if (!core.TryGetNode(nodeID, out Web_Node? node) || node is null)
                return nodeID.ToString();

            return GetNodeLabel(node);
        }

        private static string GetNodeLabel(Web_Node node)
        {
            if (!string.IsNullOrWhiteSpace(node.Label))
                return node.Label!;

            string? alias = node.Aliases.FirstOrDefault(alias => !string.IsNullOrWhiteSpace(alias));
            return alias ?? node.ID.ToString();
        }

        private static string FormatGuid(Guid? value)
        {
            return value?.ToString() ?? "(none)";
        }
    }
}
