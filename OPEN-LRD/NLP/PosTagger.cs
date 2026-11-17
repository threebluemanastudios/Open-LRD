namespace OPEN_LRD.NLP
{
    /// <summary>Assigns deterministic part-of-speech tags to normalized tokens.</summary>
    internal static class PosTagger
    {
        /// <summary>Assigns part-of-speech tags and lemmas to every token in the parsed text.</summary>
        public static ParsedText Tag(ParsedText parsed)
        {
            ArgumentNullException.ThrowIfNull(parsed);

            List<Sentence> sentences = new List<Sentence>(parsed.Sentences.Count);
            List<Token> allTokens = new List<Token>(parsed.Tokens.Count);

            foreach (Sentence sentence in parsed.Sentences)
            {
                List<Token> taggedTokens = new List<Token>(sentence.Tokens.Count);

                for (int index = 0; index < sentence.Tokens.Count; index++)
                {
                    Token token = sentence.Tokens[index];
                    PartOfSpeech partOfSpeech = TagToken(sentence.Tokens, index, taggedTokens);
                    string lemma = Lemmatizer.Lemmatize(token.Normalized, partOfSpeech);

                    taggedTokens.Add(token with
                    {
                        PartOfSpeech = partOfSpeech,
                        Lemma = lemma
                    });
                }

                Sentence taggedSentence = sentence with { Tokens = taggedTokens };
                sentences.Add(taggedSentence);
                allTokens.AddRange(taggedTokens);
            }

            return parsed with
            {
                Sentences = sentences,
                Tokens = allTokens
            };
        }

        /// <summary>Determines the part of speech for a single token using lexical and contextual rules.</summary>
        private static PartOfSpeech TagToken(
            IReadOnlyList<Token> tokens,
            int index,
            IReadOnlyList<Token> tagged)
        {
            Token token = tokens[index];
            string value = token.Normalized;

            if (string.IsNullOrWhiteSpace(value))
                return PartOfSpeech.Unknown;

            if (IsPunctuation(value))
                return PartOfSpeech.Punctuation;

            Token? previous = tagged.Count > 0 ? tagged[^1] : null;
            Token? next = index + 1 < tokens.Count ? tokens[index + 1] : null;

            if (Lexicon.IsNumberOrDate(value) || Lexicon.IsNumberWord(value))
                return PartOfSpeech.Number;

            if (TryTagWhContraction(value))
                return PartOfSpeech.WhWord;

            if (Lexicon.IsWhWord(value))
                return PartOfSpeech.WhWord;

            if (Lexicon.LooksLikeDateWord(value) && IsDateContext(tokens, index))
                return PartOfSpeech.ProperNoun;

            if (Lexicon.IsAuxiliary(value))
                return PartOfSpeech.Auxiliary;

            if (TryTagContraction(value, token.Text, previous, out PartOfSpeech contractionTag))
                return contractionTag;

            if (Lexicon.IsDeterminer(value))
                return PartOfSpeech.Determiner;

            if (Lexicon.IsPronoun(value))
                return PartOfSpeech.Pronoun;

            if (Lexicon.IsPreposition(value))
                return PartOfSpeech.Preposition;

            if (Lexicon.IsConjunction(value))
                return PartOfSpeech.Conjunction;

            bool indefiniteCommonNoun = previous?.PartOfSpeech == PartOfSpeech.Determiner
                && previous.Normalized is "a" or "an";

            if (IsCapitalized(token.Text) && !indefiniteCommonNoun)
                return PartOfSpeech.ProperNoun;

            if (Lexicon.IsKnownAdjective(value))
                return PartOfSpeech.Adjective;

            if (Lexicon.IsKnownAdverb(value))
                return PartOfSpeech.Adverb;

            if (IsLikelyVerb(value, previous, next))
                return PartOfSpeech.Verb;

            if (value.EndsWith("ed", StringComparison.Ordinal)
                && previous?.PartOfSpeech == PartOfSpeech.Determiner
                && next is not null
                && !Lexicon.IsAuxiliary(next.Normalized)
                && !Lexicon.IsPreposition(next.Normalized)
                && !IsPunctuation(next.Normalized))
            {
                return PartOfSpeech.Adjective;
            }

            if (value.EndsWith("ly", StringComparison.Ordinal) && value.Length > 3)
                return PartOfSpeech.Adverb;

            if (HasAdjectiveSuffix(value) || IsLikelyAdjectiveForm(value, previous))
                return PartOfSpeech.Adjective;

            if (previous?.PartOfSpeech == PartOfSpeech.Auxiliary)
                return PartOfSpeech.Adjective;

            return PartOfSpeech.Noun;
        }

        /// <summary>Attempts to classify an apostrophe contraction or possessive token.</summary>
        private static bool TryTagContraction(
            string value,
            string originalText,
            Token? previous,
            out PartOfSpeech partOfSpeech)
        {
            partOfSpeech = PartOfSpeech.Unknown;

            if (!value.Contains('\''))
                return false;

            if (value.EndsWith("n't", StringComparison.Ordinal)
                || value.EndsWith("'re", StringComparison.Ordinal)
                || value.EndsWith("'ve", StringComparison.Ordinal)
                || value.EndsWith("'ll", StringComparison.Ordinal)
                || value.EndsWith("'d", StringComparison.Ordinal)
                || value.EndsWith("'m", StringComparison.Ordinal))
            {
                partOfSpeech = PartOfSpeech.Auxiliary;
                return true;
            }

            if (value == "let's")
            {
                partOfSpeech = PartOfSpeech.Verb;
                return true;
            }

            if (value.EndsWith("'s", StringComparison.Ordinal) && value.Length > 2)
            {
                string possessor = value[..^2];

                if (PossessorIsPronoun(possessor))
                {
                    partOfSpeech = PartOfSpeech.Auxiliary;
                    return true;
                }

                if (previous?.PartOfSpeech == PartOfSpeech.Determiner)
                {
                    partOfSpeech = PartOfSpeech.Noun;
                    return true;
                }

                partOfSpeech = IsCapitalized(originalText)
                    ? PartOfSpeech.ProperNoun
                    : PartOfSpeech.Noun;
                return true;
            }

            return false;
        }

        /// <summary>Checks whether a month or weekday word occurs in a date-like token context.</summary>
        private static bool IsDateContext(IReadOnlyList<Token> tokens, int index)
        {
            Token? previous = index > 0 ? tokens[index - 1] : null;
            Token? next = index + 1 < tokens.Count ? tokens[index + 1] : null;

            if (next is not null
                && (Lexicon.IsNumberOrDate(next.Normalized)
                    || Lexicon.IsNumberWord(next.Normalized)))
            {
                return true;
            }

            return previous is not null
                && previous.Normalized is "in" or "on" or "during" or "since"
                    or "until" or "by";
        }

        /// <summary>Checks whether a normalized token is a contracted interrogative word.</summary>
        private static bool TryTagWhContraction(string value)
        {
            return value is "what's" or "who's" or "where's" or "when's" or "why's"
                or "how's" or "which's";
        }

        /// <summary>Checks whether a token is likely a verb based on lexicon membership, suffixes, and neighbors.</summary>
        private static bool IsLikelyVerb(string value, Token? previous, Token? next)
        {
            bool afterDeterminer = previous?.PartOfSpeech == PartOfSpeech.Determiner;
            bool afterAuxiliary = previous?.PartOfSpeech == PartOfSpeech.Auxiliary;

            if (Lexicon.IsKnownVerb(value) || Lexicon.IsIrregularVerbForm(value))
                return !afterDeterminer;

            if (value.EndsWith("s", StringComparison.Ordinal)
                && !value.EndsWith("ss", StringComparison.Ordinal)
                && value.Length > 1
                && Lexicon.IsKnownVerb(value[..^1]))
            {
                return !afterDeterminer;
            }

            if (value.EndsWith("ing", StringComparison.Ordinal) && value.Length > 3)
            {
                if (afterAuxiliary)
                    return true;

                if (afterDeterminer)
                    return false;

                return next is not null
                    && (Lexicon.IsDeterminer(next.Normalized)
                        || Lexicon.IsPronoun(next.Normalized)
                        || IsCapitalized(next.Text));
            }

            if (value.EndsWith("ed", StringComparison.Ordinal) && value.Length > 2)
            {
                if (afterDeterminer)
                    return false;

                if (next is not null && next.PartOfSpeech == PartOfSpeech.Unknown)
                {
                    if (Lexicon.IsDeterminer(next.Normalized)
                        || Lexicon.IsPronoun(next.Normalized)
                        || IsCapitalized(next.Text))
                    {
                        return true;
                    }
                }

                return true;
            }

            if (value.EndsWith("s", StringComparison.Ordinal)
                && !value.EndsWith("ss", StringComparison.Ordinal)
                && !value.EndsWith("us", StringComparison.Ordinal)
                && !value.EndsWith("is", StringComparison.Ordinal)
                && value.Length > 1)
            {
                if (afterDeterminer)
                    return false;

                return previous?.PartOfSpeech is PartOfSpeech.Pronoun
                    or PartOfSpeech.ProperNoun
                    or PartOfSpeech.Noun;
            }

            return false;
        }

        /// <summary>Checks whether a token looks like a comparative or superlative adjective form.</summary>
        private static bool IsLikelyAdjectiveForm(string value, Token? previous)
        {
            if (value.EndsWith("ier", StringComparison.Ordinal)
                || value.EndsWith("iest", StringComparison.Ordinal))
            {
                return value.Length > 3;
            }

            if (value.EndsWith("er", StringComparison.Ordinal) && value.Length > 3)
            {
                string stem = value[..^2];
                return Lexicon.IsKnownAdjective(stem)
                    || previous?.PartOfSpeech is PartOfSpeech.Determiner
                        or PartOfSpeech.Auxiliary;
            }

            if (value.EndsWith("est", StringComparison.Ordinal) && value.Length > 4)
            {
                string stem = value[..^3];
                return Lexicon.IsKnownAdjective(stem)
                    || Lexicon.IsKnownAdjective(stem[..^1])
                    || previous?.PartOfSpeech is PartOfSpeech.Determiner
                        or PartOfSpeech.Auxiliary;
            }

            return false;
        }

        /// <summary>Checks whether a word ends with a common derivational adjective suffix.</summary>
        private static bool HasAdjectiveSuffix(string value)
        {
            return value.EndsWith("ous", StringComparison.Ordinal)
                || value.EndsWith("ical", StringComparison.Ordinal)
                || value.EndsWith("ic", StringComparison.Ordinal)
                || value.EndsWith("al", StringComparison.Ordinal)
                || value.EndsWith("ar", StringComparison.Ordinal)
                || value.EndsWith("ful", StringComparison.Ordinal)
                || value.EndsWith("ive", StringComparison.Ordinal)
                || value.EndsWith("able", StringComparison.Ordinal)
                || value.EndsWith("ible", StringComparison.Ordinal)
                || value.EndsWith("less", StringComparison.Ordinal)
                || value.EndsWith("ish", StringComparison.Ordinal)
                || value.EndsWith("ary", StringComparison.Ordinal);
        }

        /// <summary>Checks whether a token consists solely of punctuation characters.</summary>
        private static bool IsPunctuation(string value)
        {
            return value.All(character => !char.IsLetterOrDigit(character) && character != '_');
        }

        /// <summary>Checks whether a token begins with an uppercase character.</summary>
        private static bool IsCapitalized(string value)
        {
            return value.Length > 0 && char.IsUpper(value[0]);
        }

        /// <summary>Checks whether the stem preceding a contraction's 's is a pronoun or demonstrative.</summary>
        private static bool PossessorIsPronoun(string value)
        {
            return value is "i" or "you" or "he" or "she" or "it" or "we" or "they"
                or "that" or "there" or "here" or "what" or "who";
        }
    }
}
