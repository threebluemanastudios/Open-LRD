using OPEN_LRD.Nodes;

namespace OPEN_LRD.NLP
{
    internal static class NamedEntityRecognizer
    {
        public static ParsedText Recognize(ParsedText parsed)
        {
            ArgumentNullException.ThrowIfNull(parsed);

            List<Mention> mentions = new List<Mention>();

            foreach (Sentence sentence in parsed.Sentences)
            {
                IEnumerable<Chunk> chunks = sentence.Clauses.Count > 0
                    ? sentence.Clauses.SelectMany(clause => clause.Chunks)
                    : Array.Empty<Chunk>();

                foreach (Chunk chunk in chunks.Where(candidate => candidate.Kind == ChunkKind.NounPhrase))
                {
                    Mention? mention = CreateMention(sentence, chunk);

                    if (mention is not null)
                        mentions.Add(mention);
                }
            }

            return parsed with { Mentions = mentions };
        }

        private static Mention? CreateMention(Sentence sentence, Chunk chunk)
        {
            List<Token> tokens = sentence.Tokens
                .Skip(chunk.StartToken)
                .Take(chunk.TokenCount)
                .ToList();

            Token? head = tokens.LastOrDefault(token =>
                token.PartOfSpeech is PartOfSpeech.Noun
                    or PartOfSpeech.ProperNoun
                    or PartOfSpeech.Number);

            if (head is null)
                return null;

            string surfaceText = string.Join(" ", tokens.Select(token => token.Text));
            string normalizedSurface = string.Join(" ", tokens.Select(token => token.Normalized));

            (MentionKind kind, NodeTypes nodeType, double confidence) = Classify(
                tokens,
                normalizedSurface,
                head);

            return new Mention(
                surfaceText,
                head.Lemma,
                nodeType,
                kind,
                sentence.Index,
                chunk.StartToken,
                chunk.EndToken,
                confidence);
        }

        private static (MentionKind Kind, NodeTypes NodeType, double Confidence) Classify(
            IReadOnlyList<Token> tokens,
            string normalizedSurface,
            Token head)
        {
            if (IsDateMention(tokens, normalizedSurface))
                return (MentionKind.Date, NodeTypes.RawData, 0.95);

            if (tokens.Any(token => Lexicon.IsTimeWord(token.Normalized))
                || tokens.Any(token =>
                    token.PartOfSpeech == PartOfSpeech.Number
                    && token.Normalized.Contains(':')))
            {
                return (MentionKind.Time, NodeTypes.RawData, 0.9);
            }

            if (Lexicon.IsEventNoun(head.Normalized) || Lexicon.IsEventNoun(head.Lemma))
                return (MentionKind.Event, NodeTypes.Events, 0.9);

            if (tokens.Any(token => Lexicon.IsLocationWord(token.Normalized))
                || Lexicon.IsLocationWord(normalizedSurface))
            {
                return (MentionKind.Location, NodeTypes.Locations, 0.95);
            }

            if (tokens.Any(token => Lexicon.IsOrganizationWord(token.Normalized))
                || Lexicon.IsOrganizationWord(normalizedSurface))
            {
                return (MentionKind.Organization, NodeTypes.Entities, 0.9);
            }

            if (IsPersonMention(tokens, head))
                return (MentionKind.Person, NodeTypes.Entities, 0.9);

            if (head.PartOfSpeech == PartOfSpeech.Number
                || Lexicon.IsNumberWord(head.Normalized))
            {
                return (MentionKind.Quantity, NodeTypes.RawData, 0.95);
            }

            if (head.PartOfSpeech == PartOfSpeech.ProperNoun)
                return (MentionKind.Person, NodeTypes.Entities, 0.7);

            return (MentionKind.Concept, NodeTypes.Concepts, 0.85);
        }

        private static bool IsDateMention(IReadOnlyList<Token> tokens, string normalizedSurface)
        {
            if (tokens.Any(token => Lexicon.LooksLikeDateWord(token.Normalized)))
                return true;

            if (tokens.Any(token =>
                token.PartOfSpeech == PartOfSpeech.Number
                && (token.Normalized.Contains('-') || token.Normalized.Contains('/'))))
            {
                return true;
            }

            return normalizedSurface.Contains('-')
                && normalizedSurface.Any(char.IsDigit);
        }

        private static bool IsPersonMention(IReadOnlyList<Token> tokens, Token head)
        {
            if (tokens.Any(token => Lexicon.IsPersonTitle(token.Normalized))
                || tokens.Any(token => Lexicon.IsPersonName(token.Normalized)))
            {
                return true;
            }

            int properNounCount = tokens.Count(token => token.PartOfSpeech == PartOfSpeech.ProperNoun);
            return properNounCount >= 2 || head.PartOfSpeech == PartOfSpeech.ProperNoun;
        }
    }
}
