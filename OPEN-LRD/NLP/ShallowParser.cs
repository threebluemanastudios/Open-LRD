namespace OPEN_LRD.NLP
{
    internal static class ShallowParser
    {
        private static readonly HashSet<string> clauseConjunctions = new HashSet<string>(
            new[]
            {
                "and", "but", "or", "nor", "so", "yet", "because", "although",
                "though", "while", "whereas", "if", "unless", "whether"
            },
            StringComparer.OrdinalIgnoreCase);

        public static ParsedText Parse(ParsedText parsed)
        {
            ArgumentNullException.ThrowIfNull(parsed);

            List<Sentence> sentences = new List<Sentence>(parsed.Sentences.Count);
            List<Chunk> allChunks = new List<Chunk>();

            foreach (Sentence sentence in parsed.Sentences)
            {
                List<Clause> clauses = new List<Clause>();

                foreach ((int start, int end) in FindClauseSpans(sentence.Tokens))
                {
                    int index = clauses.Count;
                    List<Chunk> chunks = BuildChunks(sentence.Tokens, start, end, sentence.Index);
                    List<ClauseArgument> arguments = BuildArguments(sentence, chunks);

                    clauses.Add(new Clause(index, start, end, chunks, arguments));
                    allChunks.AddRange(chunks);
                }

                sentences.Add(sentence with { Clauses = clauses });
            }

            return parsed with
            {
                Sentences = sentences,
                Chunks = allChunks
            };
        }

        private static List<(int Start, int End)> FindClauseSpans(IReadOnlyList<Token> tokens)
        {
            List<(int Start, int End)> spans = new List<(int Start, int End)>();
            int start = 0;
            bool hasVerb = false;

            for (int index = 0; index < tokens.Count; index++)
            {
                if (tokens[index].PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary)
                    hasVerb = true;

                if (!hasVerb
                    || index + 1 >= tokens.Count
                    || !IsClauseConjunction(tokens[index].Normalized)
                    || !IsSubjectStart(tokens[index + 1].PartOfSpeech)
                    || !HasVerbAfter(tokens, index + 1))
                {
                    continue;
                }

                AddSpan(spans, tokens, start, index - 1);
                start = index + 1;
                hasVerb = false;
            }

            AddSpan(spans, tokens, start, tokens.Count - 1);
            return spans;
        }

        private static void AddSpan(
            List<(int Start, int End)> spans,
            IReadOnlyList<Token> tokens,
            int start,
            int end)
        {
            while (start <= end && tokens[start].PartOfSpeech == PartOfSpeech.Punctuation)
                start++;

            while (end >= start && tokens[end].PartOfSpeech == PartOfSpeech.Punctuation)
                end--;

            if (start <= end)
                spans.Add((start, end));
        }

        private static List<Chunk> BuildChunks(
            IReadOnlyList<Token> tokens,
            int start,
            int end,
            int sentenceIndex)
        {
            List<Chunk> chunks = new List<Chunk>();
            int index = start;

            while (index <= end)
            {
                if (tokens[index].PartOfSpeech == PartOfSpeech.Punctuation)
                {
                    index++;
                    continue;
                }

                if (Lexicon.IsPreposition(tokens[index].Normalized))
                {
                    int phraseStart = index;
                    index++;

                    if (index <= end && StartsNounPhrase(tokens, index, end))
                    {
                        int nounStart = index;
                        int nounEnd = ConsumeNounPhrase(tokens, index, end, out index);
                        chunks.Add(new Chunk(ChunkKind.NounPhrase, sentenceIndex, nounStart, nounEnd));
                    }

                    chunks.Add(new Chunk(
                        ChunkKind.PrepositionalPhrase,
                        sentenceIndex,
                        phraseStart,
                        index - 1));
                    continue;
                }

                if (StartsNounPhrase(tokens, index, end))
                {
                    int nounStart = index;
                    int nounEnd = ConsumeNounPhrase(tokens, index, end, out index);
                    chunks.Add(new Chunk(ChunkKind.NounPhrase, sentenceIndex, nounStart, nounEnd));
                    continue;
                }

                if (tokens[index].PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary)
                {
                    int verbStart = index;
                    int verbEnd = ConsumeVerbPhrase(tokens, index, end, out index);
                    chunks.Add(new Chunk(ChunkKind.VerbPhrase, sentenceIndex, verbStart, verbEnd));
                    continue;
                }

                index++;
            }

            return chunks;
        }

        private static int ConsumeNounPhrase(
            IReadOnlyList<Token> tokens,
            int start,
            int end,
            out int next)
        {
            int index = start;

            while (index <= end)
            {
                if (IsNounPhraseToken(tokens[index].PartOfSpeech))
                {
                    index++;
                    continue;
                }

                if (tokens[index].PartOfSpeech == PartOfSpeech.Conjunction
                    && index + 1 <= end
                    && StartsNounPhrase(tokens, index + 1, end))
                {
                    index++;
                    continue;
                }

                break;
            }

            next = index;
            return index - 1;
        }

        private static int ConsumeVerbPhrase(
            IReadOnlyList<Token> tokens,
            int start,
            int end,
            out int next)
        {
            int index = start;

            while (index <= end
                && tokens[index].PartOfSpeech is PartOfSpeech.Verb
                    or PartOfSpeech.Auxiliary
                    or PartOfSpeech.Adverb)
            {
                index++;
            }

            next = index;
            return index - 1;
        }

        private static bool StartsNounPhrase(
            IReadOnlyList<Token> tokens,
            int start,
            int end)
        {
            PartOfSpeech partOfSpeech = tokens[start].PartOfSpeech;

            if (partOfSpeech is PartOfSpeech.Determiner
                or PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Pronoun
                or PartOfSpeech.Number)
            {
                return true;
            }

            if (partOfSpeech != PartOfSpeech.Adjective)
                return false;

            for (int index = start + 1; index <= end; index++)
            {
                if (tokens[index].PartOfSpeech == PartOfSpeech.Adjective)
                    continue;

                return tokens[index].PartOfSpeech is PartOfSpeech.Noun
                    or PartOfSpeech.ProperNoun;
            }

            return false;
        }

        private static bool IsNounPhraseToken(PartOfSpeech partOfSpeech)
        {
            return partOfSpeech is PartOfSpeech.Determiner
                or PartOfSpeech.Adjective
                or PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Pronoun
                or PartOfSpeech.Number;
        }

        private static List<ClauseArgument> BuildArguments(
            Sentence sentence,
            IReadOnlyList<Chunk> chunks)
        {
            List<ClauseArgument> arguments = new List<ClauseArgument>();
            Chunk? predicate = chunks.FirstOrDefault(chunk => chunk.Kind == ChunkKind.VerbPhrase);

            if (predicate is null)
                return arguments;

            if (sentence.Type != SentenceType.Imperative)
            {
                Chunk? subject = chunks.FirstOrDefault(
                    chunk => chunk.Kind == ChunkKind.NounPhrase
                        && chunk.EndToken < predicate.StartToken
                        && !IsInsidePrepositionalPhrase(chunk, chunks));

                if (subject is not null)
                {
                    arguments.Add(new ClauseArgument(
                        ClauseRole.Subject,
                        subject.StartToken,
                        subject.EndToken));
                }
            }

            arguments.Add(new ClauseArgument(
                ClauseRole.Predicate,
                predicate.StartToken,
                predicate.EndToken));

            Chunk? complement = chunks.FirstOrDefault(
                chunk => chunk.Kind == ChunkKind.NounPhrase
                    && chunk.StartToken > predicate.EndToken
                    && !IsInsidePrepositionalPhrase(chunk, chunks));

            if (complement is not null)
            {
                bool isCopula = sentence.Tokens
                    .Skip(predicate.StartToken)
                    .Take(predicate.EndToken - predicate.StartToken + 1)
                    .Any(token => token.Lemma == "be");

                arguments.Add(new ClauseArgument(
                    isCopula ? ClauseRole.Complement : ClauseRole.Object,
                    complement.StartToken,
                    complement.EndToken));
            }

            foreach (Chunk prepositionalPhrase in chunks.Where(
                chunk => chunk.Kind == ChunkKind.PrepositionalPhrase))
            {
                arguments.Add(new ClauseArgument(
                    ClauseRole.Qualifier,
                    prepositionalPhrase.StartToken,
                    prepositionalPhrase.EndToken));
            }

            return arguments;
        }

        private static bool IsInsidePrepositionalPhrase(Chunk chunk, IReadOnlyList<Chunk> chunks)
        {
            return chunks.Any(candidate =>
                candidate.Kind == ChunkKind.PrepositionalPhrase
                && candidate.StartToken <= chunk.StartToken
                && chunk.EndToken <= candidate.EndToken);
        }

        private static bool IsClauseConjunction(string value)
        {
            return clauseConjunctions.Contains(value);
        }

        private static bool HasVerbAfter(IReadOnlyList<Token> tokens, int start)
        {
            for (int index = start; index < tokens.Count; index++)
            {
                if (tokens[index].PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary)
                    return true;
            }

            return false;
        }

        private static bool IsSubjectStart(PartOfSpeech partOfSpeech)
        {
            return partOfSpeech is PartOfSpeech.Determiner
                or PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Pronoun;
        }
    }
}
