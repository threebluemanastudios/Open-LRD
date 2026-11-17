namespace OPEN_LRD.NLP
{
    /// <summary>Builds shallow phrase chunks, clauses, and semantic arguments from tokens.</summary>
    internal static class ShallowParser
    {
        private static readonly HashSet<string> clauseConjunctions = new HashSet<string>(
            new[]
            {
                "and", "but", "or", "nor", "so", "yet", "because", "although",
                "though", "while", "whereas", "if", "unless", "whether"
            },
            StringComparer.OrdinalIgnoreCase);

        /// <summary>Segments sentences into clauses, phrase chunks, and semantic clause arguments.</summary>
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

        /// <summary>Splits a sentence's tokens into clause spans at clause-connecting conjunctions.</summary>
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

        /// <summary>Adds a punctuation-trimmed token span to a clause span list when non-empty.</summary>
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

        /// <summary>Builds noun, verb, adjective, and prepositional phrase chunks within a clause span.</summary>
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

                if (tokens[index].PartOfSpeech == PartOfSpeech.Adjective)
                {
                    int adjectiveStart = index;
                    int adjectiveEnd = ConsumeAdjectivePhrase(tokens, index, end, out index);
                    chunks.Add(new Chunk(ChunkKind.AdjectivePhrase, sentenceIndex, adjectiveStart, adjectiveEnd));
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

        /// <summary>Consumes a contiguous noun phrase span and advances the token cursor.</summary>
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

                if (IsProperNameOfConnector(tokens, start, index, end))
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

        /// <summary>Checks whether a preposition 'of' connects two proper nouns inside a single compound name.</summary>
        private static bool IsProperNameOfConnector(
            IReadOnlyList<Token> tokens,
            int phraseStart,
            int connectorIndex,
            int phraseEnd)
        {
            return connectorIndex > phraseStart
                && connectorIndex < phraseEnd
                && tokens[connectorIndex].PartOfSpeech == PartOfSpeech.Preposition
                && string.Equals(tokens[connectorIndex].Normalized, "of", StringComparison.OrdinalIgnoreCase)
                && tokens[connectorIndex - 1].PartOfSpeech == PartOfSpeech.ProperNoun
                && tokens[connectorIndex + 1].PartOfSpeech == PartOfSpeech.ProperNoun;
        }

        /// <summary>Consumes a contiguous sequence of adjective tokens and advances the token cursor.</summary>
        private static int ConsumeAdjectivePhrase(
            IReadOnlyList<Token> tokens,
            int start,
            int end,
            out int next)
        {
            int index = start;

            while (index <= end && tokens[index].PartOfSpeech == PartOfSpeech.Adjective)
                index++;

            next = index;
            return index - 1;
        }

        /// <summary>Consumes a contiguous verb phrase including auxiliaries, adverbs, and coordinated verbs.</summary>
        private static int ConsumeVerbPhrase(
            IReadOnlyList<Token> tokens,
            int start,
            int end,
            out int next)
        {
            int index = start;

            while (index <= end)
            {
                if (tokens[index].PartOfSpeech is PartOfSpeech.Verb
                    or PartOfSpeech.Auxiliary
                    or PartOfSpeech.Adverb)
                {
                    index++;
                    continue;
                }

                if (tokens[index].PartOfSpeech == PartOfSpeech.Conjunction
                    && index + 1 <= end
                    && tokens[index + 1].PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary)
                {
                    index++;
                    continue;
                }

                break;
            }

            next = index;
            return index - 1;
        }

        /// <summary>Checks whether a token index begins a noun phrase within the current clause span.</summary>
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

        /// <summary>Checks whether a part of speech can belong to a noun phrase.</summary>
        private static bool IsNounPhraseToken(PartOfSpeech partOfSpeech)
        {
            return partOfSpeech is PartOfSpeech.Determiner
                or PartOfSpeech.Adjective
                or PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Pronoun
                or PartOfSpeech.Number;
        }

        /// <summary>Assigns subject, predicate, object, complement, and qualifier roles to clause chunks.</summary>
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

            bool isCopula = false;

            if (complement is not null)
            {
                isCopula = sentence.Tokens
                    .Skip(predicate.StartToken)
                    .Take(predicate.EndToken - predicate.StartToken + 1)
                    .Any(token => token.Lemma == "be");
            }
            else
            {
                complement = chunks.FirstOrDefault(
                    chunk => chunk.Kind == ChunkKind.AdjectivePhrase
                        && chunk.StartToken > predicate.EndToken
                        && !IsInsidePrepositionalPhrase(chunk, chunks));
                isCopula = complement is not null;
            }

            if (complement is not null)
            {
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

        /// <summary>Checks whether a chunk lies completely inside any prepositional phrase chunk.</summary>
        private static bool IsInsidePrepositionalPhrase(Chunk chunk, IReadOnlyList<Chunk> chunks)
        {
            return chunks.Any(candidate =>
                candidate.Kind == ChunkKind.PrepositionalPhrase
                && candidate.StartToken <= chunk.StartToken
                && chunk.EndToken <= candidate.EndToken);
        }

        /// <summary>Checks whether a normalized token is a clause-boundary conjunction.</summary>
        private static bool IsClauseConjunction(string value)
        {
            return clauseConjunctions.Contains(value);
        }

        /// <summary>Checks whether any verb or auxiliary token occurs at or after the specified token index.</summary>
        private static bool HasVerbAfter(IReadOnlyList<Token> tokens, int start)
        {
            for (int index = start; index < tokens.Count; index++)
            {
                if (tokens[index].PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary)
                    return true;
            }

            return false;
        }

        /// <summary>Checks whether a part of speech can start a new clause subject.</summary>
        private static bool IsSubjectStart(PartOfSpeech partOfSpeech)
        {
            return partOfSpeech is PartOfSpeech.Determiner
                or PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Pronoun;
        }
    }
}
