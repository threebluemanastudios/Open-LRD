namespace OPEN_LRD.NLP
{
    internal static class Lemmatizer
    {
        private static readonly Dictionary<string, string> pronounLemmas =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["i"] = "i",
                ["me"] = "i",
                ["you"] = "you",
                ["he"] = "he",
                ["him"] = "he",
                ["she"] = "she",
                ["her"] = "she",
                ["it"] = "it",
                ["we"] = "we",
                ["us"] = "we",
                ["they"] = "they",
                ["them"] = "they",
                ["myself"] = "i",
                ["yourself"] = "you",
                ["himself"] = "he",
                ["herself"] = "she",
                ["itself"] = "it",
                ["ourselves"] = "we",
                ["yourselves"] = "you",
                ["themselves"] = "they",
                ["his"] = "he",
                ["hers"] = "she",
                ["its"] = "it",
                ["ours"] = "we",
                ["theirs"] = "they"
            };

        private static readonly HashSet<string> adverbExceptions = new HashSet<string>(
            new[]
            {
                "daily", "early", "family", "friendly", "holy", "likely", "lonely",
                "lovely", "monthly", "only", "reply", "silly", "supply", "weekly",
                "yearly"
            },
            StringComparer.OrdinalIgnoreCase);

        public static string Lemmatize(string normalized, PartOfSpeech partOfSpeech)
        {
            if (string.IsNullOrWhiteSpace(normalized))
                return normalized;

            if (partOfSpeech is PartOfSpeech.Punctuation or PartOfSpeech.Number)
                return normalized;

            if (partOfSpeech == PartOfSpeech.Pronoun
                && pronounLemmas.TryGetValue(normalized, out string? pronounLemma))
            {
                return pronounLemma;
            }

            if (Lexicon.TryGetIrregularLemma(normalized, out string irregular))
                return irregular;

            string? contraction = GetContractionLemma(normalized, partOfSpeech);

            if (contraction is not null)
                return contraction;

            return partOfSpeech switch
            {
                PartOfSpeech.Noun or PartOfSpeech.ProperNoun => LemmatizeNoun(normalized),
                PartOfSpeech.Verb or PartOfSpeech.Auxiliary => LemmatizeVerb(normalized),
                PartOfSpeech.Adjective => LemmatizeAdjective(normalized),
                PartOfSpeech.Adverb => LemmatizeAdverb(normalized),
                _ => normalized
            };
        }

        private static string LemmatizeNoun(string value)
        {
            if (value.EndsWith("ies", StringComparison.Ordinal) && value.Length > 3)
                return value[..^3] + "y";

            if (value.EndsWith("ches", StringComparison.Ordinal)
                || value.EndsWith("shes", StringComparison.Ordinal)
                || value.EndsWith("ses", StringComparison.Ordinal)
                || value.EndsWith("xes", StringComparison.Ordinal)
                || value.EndsWith("zes", StringComparison.Ordinal))
            {
                return value[..^2];
            }

            if (value.EndsWith("s", StringComparison.Ordinal)
                && !value.EndsWith("ss", StringComparison.Ordinal)
                && !value.EndsWith("us", StringComparison.Ordinal)
                && !value.EndsWith("is", StringComparison.Ordinal)
                && value.Length > 1)
            {
                return value[..^1];
            }

            return value;
        }

        private static string LemmatizeVerb(string value)
        {
            if (value.EndsWith("ies", StringComparison.Ordinal) && value.Length > 3)
            {
                string singular = value[..^1];

                if (Lexicon.IsKnownVerb(singular))
                    return singular;

                return value[..^3] + "y";
            }

            if (value.EndsWith("ied", StringComparison.Ordinal) && value.Length > 3)
                return value[..^3] + "y";

            if (value.EndsWith("es", StringComparison.Ordinal) && value.Length > 2)
                return ChooseVerbStem(value[..^2]);

            if (value.EndsWith("ed", StringComparison.Ordinal) && value.Length > 2)
                return ChooseVerbStem(value[..^2]);

            if (value.EndsWith("ing", StringComparison.Ordinal) && value.Length > 3)
                return ChooseVerbStem(value[..^3]);

            if (value.EndsWith("s", StringComparison.Ordinal)
                && !value.EndsWith("ss", StringComparison.Ordinal)
                && value.Length > 1)
            {
                return ChooseVerbStem(value[..^1]);
            }

            return value;
        }

        private static string ChooseVerbStem(string stem)
        {
            if (stem.Length == 0)
                return stem;

            if (Lexicon.IsKnownVerb(stem))
                return stem;

            if (Lexicon.IsKnownVerb(stem + "e"))
                return stem + "e";

            if (stem.Length >= 3
                && stem[^1] == stem[^2]
                && IsConsonant(stem[^1]))
            {
                string single = stem[..^1];

                if (Lexicon.IsKnownVerb(single))
                    return single;

                if (Lexicon.IsKnownVerb(single + "e"))
                    return single + "e";

                return single;
            }

            return stem;
        }

        private static string LemmatizeAdjective(string value)
        {
            return value switch
            {
                "better" or "best" => "good",
                "worse" or "worst" => "bad",
                _ => LemmatizeAdjectiveSuffix(value)
            };
        }

        private static string LemmatizeAdjectiveSuffix(string value)
        {
            if (value.EndsWith("iest", StringComparison.Ordinal) && value.Length > 4)
                return value[..^4] + "y";

            if (value.EndsWith("ier", StringComparison.Ordinal) && value.Length > 3)
                return value[..^3] + "y";

            if (value.EndsWith("est", StringComparison.Ordinal) && value.Length > 4)
                return ChooseAdjectiveStem(value[..^3]);

            if (value.EndsWith("er", StringComparison.Ordinal) && value.Length > 3)
                return ChooseAdjectiveStem(value[..^2]);

            return value;
        }

        private static string ChooseAdjectiveStem(string stem)
        {
            if (stem.Length == 0)
                return stem;

            if (Lexicon.IsKnownAdjective(stem))
                return stem;

            if (Lexicon.IsKnownAdjective(stem + "e"))
                return stem + "e";

            if (stem.Length >= 3
                && stem[^1] == stem[^2]
                && IsConsonant(stem[^1]))
            {
                return stem[..^1];
            }

            return stem;
        }

        private static string LemmatizeAdverb(string value)
        {
            if (value.Length > 5
                && value.EndsWith("ly", StringComparison.Ordinal)
                && !adverbExceptions.Contains(value))
            {
                return value[..^2];
            }

            return value;
        }

        private static string? GetContractionLemma(string value, PartOfSpeech partOfSpeech)
        {
            switch (value)
            {
                case "can't":
                    return "can";
                case "won't":
                    return "will";
                case "shan't":
                    return "shall";
                case "let's":
                    return "let";
            }

            if (value.EndsWith("n't", StringComparison.Ordinal) && value.Length > 3)
            {
                string baseWord = value[..^3];

                if (Lexicon.TryGetIrregularLemma(baseWord, out string irregular))
                    return irregular;

                return baseWord;
            }

            if (value.EndsWith("'re", StringComparison.Ordinal))
                return "be";

            if (value.EndsWith("'ve", StringComparison.Ordinal))
                return "have";

            if (value.EndsWith("'ll", StringComparison.Ordinal))
                return "will";

            if (value.EndsWith("'d", StringComparison.Ordinal))
                return "would";

            if (value.EndsWith("'m", StringComparison.Ordinal))
                return "be";

            if (value.EndsWith("'s", StringComparison.Ordinal) && value.Length > 2)
            {
                string possessor = value[..^2];

                if (partOfSpeech == PartOfSpeech.Verb
                    || partOfSpeech == PartOfSpeech.Auxiliary
                    || PossessorIsPronoun(possessor))
                {
                    return "be";
                }

                return Lemmatize(possessor, partOfSpeech);
            }

            if (value.EndsWith("'", StringComparison.Ordinal) && value.Length > 1)
                return Lemmatize(value[..^1], partOfSpeech);

            return null;
        }

        private static bool PossessorIsPronoun(string value)
        {
            return value is "i" or "you" or "he" or "she" or "it" or "we" or "they"
                or "that" or "there" or "here" or "what" or "who";
        }

        private static bool IsConsonant(char value)
        {
            return char.IsLetter(value)
                && value is not ('a' or 'e' or 'i' or 'o' or 'u');
        }
    }
}
