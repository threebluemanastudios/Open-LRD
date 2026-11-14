namespace OPEN_LRD.NLP
{
    internal static class SocialDialogueRecognizer
    {
        private static readonly Dictionary<string, (string Text, string ShortAnswer)> replies =
            new Dictionary<string, (string Text, string ShortAnswer)>(StringComparer.OrdinalIgnoreCase)
            {
                ["hello"] = ("Hello! What would you like to know?", "Hello!"),
                ["hello there"] = ("Hello! What would you like to know?", "Hello!"),
                ["hi"] = ("Hi! What would you like to know?", "Hi!"),
                ["hi there"] = ("Hi! What would you like to know?", "Hi!"),
                ["hey"] = ("Hey! What can I help you find?", "Hey!"),
                ["hey there"] = ("Hey! What can I help you find?", "Hey!"),
                ["howdy"] = ("Howdy! What would you like to know?", "Howdy!"),
                ["good morning"] = ("Good morning! What would you like to know?", "Good morning!"),
                ["good afternoon"] = ("Good afternoon! What would you like to know?", "Good afternoon!"),
                ["good evening"] = ("Good evening! What would you like to know?", "Good evening!"),
                ["how are you"] = ("I'm doing well, thanks. What can I help you find?", "I'm doing well, thanks."),
                ["how are you doing"] = ("I'm doing well, thanks. What can I help you find?", "I'm doing well, thanks."),
                ["how are you today"] = ("I'm doing well, thanks. What can I help you find?", "I'm doing well, thanks."),
                ["how are you doing today"] = ("I'm doing well, thanks. What can I help you find?", "I'm doing well, thanks."),
                ["how's it going"] = ("It's going well, thanks. What can I help you find?", "It's going well, thanks."),
                ["thanks"] = ("You're welcome!", "You're welcome!"),
                ["thank you"] = ("You're welcome!", "You're welcome!"),
                ["thanks a lot"] = ("You're very welcome!", "You're welcome!"),
                ["thanks so much"] = ("You're very welcome!", "You're welcome!"),
                ["thank you very much"] = ("You're very welcome!", "You're welcome!"),
                ["thank you so much"] = ("You're very welcome!", "You're welcome!"),
                ["goodbye"] = ("Goodbye! I'll be here when you need me.", "Goodbye!"),
                ["good bye"] = ("Goodbye! I'll be here when you need me.", "Goodbye!"),
                ["goodbye for now"] = ("Goodbye for now! I'll be here when you need me.", "Goodbye for now!"),
                ["bye"] = ("Goodbye! I'll be here when you need me.", "Goodbye!"),
                ["bye bye"] = ("Bye for now!", "Bye!"),
                ["bye-bye"] = ("Bye for now!", "Bye!"),
                ["bye for now"] = ("Bye for now!", "Bye!"),
                ["bye now"] = ("Goodbye!", "Goodbye!"),
                ["ok bye"] = ("Goodbye!", "Goodbye!"),
                ["okay bye"] = ("Goodbye!", "Goodbye!"),
                ["see you"] = ("See you next time!", "See you!"),
                ["see ya"] = ("See you next time!", "See you!"),
                ["see you next time"] = ("See you next time!", "See you!"),
                ["farewell"] = ("Farewell!", "Farewell!"),
                ["good night"] = ("Good night!", "Good night!"),
                ["who are you"] = ("I'm OPEN-LRD, a knowledge-graph assistant. I can answer questions using facts in the graph.", "I'm OPEN-LRD."),
                ["what are you"] = ("I'm OPEN-LRD, a knowledge-graph assistant. I can answer questions using facts in the graph.", "I'm OPEN-LRD."),
                ["what is your name"] = ("I'm OPEN-LRD, a knowledge-graph assistant.", "I'm OPEN-LRD."),
                ["what's your name"] = ("I'm OPEN-LRD, a knowledge-graph assistant.", "I'm OPEN-LRD."),
                ["what can you do"] = ("I can answer questions about entities and relationships represented in the knowledge graph, and keep track of context during this conversation.", "I answer questions about the knowledge graph."),
                ["what do you know"] = ("I know the facts, entities, and relationships stored in the knowledge graph. You can ask about them or explore a specific entity.", "I answer questions using the knowledge graph."),
                ["can you help me"] = ("Yes! I can answer questions about entities and relationships in the knowledge graph. What would you like to know?", "Yes, I can help with the knowledge graph."),
                ["help"] = ("Ask me about a person, place, thing, or relationship in the knowledge graph. You can also phrase a request naturally, such as ‘Can you tell me where Alice works?’", "Ask me about the knowledge graph.")
            };

        public static bool TryRecognize(string? text, out NlpAnswer answer)
        {
            answer = null!;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = TextNormalizer.Normalize(text)
                .Trim()
                .TrimEnd('.', '!', '?', ',', ';', ':')
                .Trim()
                .ToLowerInvariant();

            if (!replies.TryGetValue(normalized, out (string Text, string ShortAnswer) reply))
                return false;

            answer = new NlpAnswer(
                AnswerStatus.Answered,
                AnswerKind.Description,
                reply.Text,
                reply.ShortAnswer,
                null,
                Array.Empty<AnswerCandidate>(),
                Array.Empty<OPEN_LRD.Reasoning.GraphPath>(),
                null,
                null,
                false,
                1.0,
                new[] { "Handled as social dialogue; no graph query was performed." });
            return true;
        }
    }
}
