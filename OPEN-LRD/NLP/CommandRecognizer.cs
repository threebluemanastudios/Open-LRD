using System.Text.RegularExpressions;

namespace OPEN_LRD.NLP
{
    /// <summary>Identifies the graph or conversation target of a recognized command.</summary>
    internal enum CommandTargetKind
    {
        None,
        Facts,
        Graph,
        Conversation,
        Summary,
        Node,
        CountNodes,
        CountFacts,
        CountEvents,
        CountLinks,
        CountConversation,
        Query
    }

    /// <summary>Stores a recognized command's verb, target, and clarification state.</summary>
    internal sealed record NlpCommand(
        string Verb,
        CommandTargetKind TargetKind,
        string TargetText,
        Guid? NodeID,
        bool NeedsClarification,
        string? Clarification,
        IReadOnlyList<string> Diagnostics);

    /// <summary>Recognizes supported natural-language command phrases and their targets.</summary>
    internal static class CommandRecognizer
    {
        private const string SupportedTargets =
            "facts, graph, conversation, summary, events, nodes, links, node <id>, or a question";

        private static readonly HashSet<string> commandVerbs = new HashSet<string>(
            new[] { "output", "print", "respond", "reply", "count" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> separators = new HashSet<string>(
            new[] { "with", "to", "about" },
            StringComparer.OrdinalIgnoreCase);

        public static bool TryRecognize(ParsedText? parsed, out NlpCommand command)
        {
            command = null!;

            if (parsed is null || parsed.Tokens.Count == 0)
                return false;

            List<Token> tokens = parsed.Tokens.ToList();
            int index = SkipPunctuation(tokens, 0);

            if (index >= tokens.Count)
                return false;

            if (string.Equals(tokens[index].Normalized, "please", StringComparison.OrdinalIgnoreCase))
            {
                index = SkipPunctuation(tokens, index + 1);

                if (index >= tokens.Count)
                    return false;
            }

            string verb = tokens[index].Normalized;

            if (!commandVerbs.Contains(verb))
                return false;

            index = SkipPunctuation(tokens, index + 1);
            bool explicitQuerySeparator = false;

            if (index < tokens.Count && separators.Contains(tokens[index].Normalized))
            {
                explicitQuerySeparator = true;
                index = SkipPunctuation(tokens, index + 1);
            }

            List<Token> targetTokens = tokens
                .Skip(index)
                .Where(token => token.PartOfSpeech != PartOfSpeech.Punctuation)
                .ToList();

            if (targetTokens.Count == 0)
            {
                command = CreateClarification(
                    verb,
                    string.Empty,
                    $"What should I {verb}? Try {SupportedTargets}.");
                return true;
            }

            string normalizedTarget = string.Join(
                " ",
                targetTokens.Select(token => token.Normalized));
            string targetText = BuildTargetText(parsed, targetTokens);

            if (string.Equals(verb, "count", StringComparison.OrdinalIgnoreCase))
            {
                CommandTargetKind? countTarget = normalizedTarget switch
                {
                    "nodes" or "node" or "graph" => CommandTargetKind.CountNodes,
                    "facts" or "all facts" or "data" or "all data" => CommandTargetKind.CountFacts,
                    "events" or "event" => CommandTargetKind.CountEvents,
                    "links" or "connections" => CommandTargetKind.CountLinks,
                    "conversation" or "session" or "mentions" => CommandTargetKind.CountConversation,
                    _ => null
                };

                if (countTarget.HasValue)
                {
                    command = CreateSuccess(verb, countTarget.Value, targetText);
                    return true;
                }

                string countQuestion = BuildCountQueryText(targetText, normalizedTarget);
                if (!string.IsNullOrWhiteSpace(countQuestion))
                {
                    command = CreateSuccess(verb, CommandTargetKind.Query, countQuestion);
                    return true;
                }
            }

            if (normalizedTarget is "facts" or "all facts" or "data" or "all data")
            {
                command = CreateSuccess(verb, CommandTargetKind.Facts, targetText);
                return true;
            }

            if (normalizedTarget == "graph")
            {
                command = CreateSuccess(verb, CommandTargetKind.Graph, targetText);
                return true;
            }

            if (normalizedTarget is "conversation" or "session")
            {
                command = CreateSuccess(verb, CommandTargetKind.Conversation, targetText);
                return true;
            }

            if (normalizedTarget == "summary")
            {
                command = CreateSuccess(verb, CommandTargetKind.Summary, targetText);
                return true;
            }

            if (targetTokens[0].Normalized == "node")
            {
                if (targetTokens.Count == 1)
                {
                    command = CreateClarification(
                        verb,
                        targetText,
                        "Which node ID should I show?");
                    return true;
                }

                if (targetTokens.Count == 2
                    && Guid.TryParse(targetTokens[1].Text, out Guid nodeID)
                    && nodeID != Guid.Empty)
                {
                    command = new NlpCommand(
                        verb,
                        CommandTargetKind.Node,
                        targetText,
                        nodeID,
                        false,
                        null,
                        Array.Empty<string>());
                    return true;
                }

                command = CreateClarification(
                    verb,
                    targetText,
                    $"I couldn't find node '{targetTokens[1].Text}'. Which node ID should I show?");
                return true;
            }

            bool queryLike = explicitQuerySeparator
                || Lexicon.IsWhWord(targetTokens[0].Normalized)
                || Lexicon.IsAuxiliary(targetTokens[0].Normalized);

            if (queryLike)
            {
                command = CreateSuccess(
                    verb,
                    CommandTargetKind.Query,
                    BuildQueryText(parsed, targetTokens));
                return true;
            }

            command = CreateClarification(
                verb,
                targetText,
                $"What should I {verb}? Try {SupportedTargets}.");
            return true;
        }

        private static int SkipPunctuation(IReadOnlyList<Token> tokens, int index)
        {
            while (index < tokens.Count
                && tokens[index].PartOfSpeech == PartOfSpeech.Punctuation)
            {
                index++;
            }

            return index;
        }

        private static string BuildTargetText(ParsedText parsed, IReadOnlyList<Token> targetTokens)
        {
            if (targetTokens.Count == 0)
                return string.Empty;

            int start = targetTokens[0].StartOffset;
            int end = targetTokens[^1].EndOffset;

            if (start < 0 || start >= parsed.OriginalText.Length)
                return string.Join(" ", targetTokens.Select(token => token.Text));

            end = Math.Min(end, parsed.OriginalText.Length);
            return parsed.OriginalText.Substring(start, end - start).Trim();
        }

        private static string BuildQueryText(ParsedText parsed, IReadOnlyList<Token> targetTokens)
        {
            string query = BuildTargetText(parsed, targetTokens).Trim();

            while (query.Length > 0
                && query[^1] is '.' or '!' or ';' or ',')
            {
                query = query[..^1].TrimEnd();
            }

            if (query.Length > 0 && !query.EndsWith('?'))
                query += "?";

            return query;
        }

        private static string BuildCountQueryText(string targetText, string normalizedTarget)
        {
            string query = targetText.Trim();
            if (normalizedTarget.StartsWith("how many ", StringComparison.Ordinal)
                || normalizedTarget.StartsWith("how much ", StringComparison.Ordinal))
            {
                return EnsureQuestionMark(query);
            }

            if (query.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
                query = query["the ".Length..].TrimStart();

            query = Regex.Replace(query, @"\s+(?:who|that)\s+", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (query.Length == 0)
                return string.Empty;

            return EnsureQuestionMark("How many " + query);
        }

        private static string EnsureQuestionMark(string query)
        {
            while (query.Length > 0 && query[^1] is '.' or '!' or ';' or ',')
                query = query[..^1].TrimEnd();
            return query.EndsWith('?') ? query : query + "?";
        }

        private static NlpCommand CreateSuccess(
            string verb,
            CommandTargetKind targetKind,
            string targetText)
        {
            return new NlpCommand(
                verb,
                targetKind,
                targetText,
                null,
                false,
                null,
                Array.Empty<string>());
        }

        private static NlpCommand CreateClarification(
            string verb,
            string targetText,
            string clarification)
        {
            return new NlpCommand(
                verb,
                CommandTargetKind.None,
                targetText,
                null,
                true,
                clarification,
                new[] { clarification });
        }
    }
}
