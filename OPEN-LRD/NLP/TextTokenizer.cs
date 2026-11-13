namespace OPEN_LRD.NLP
{
    internal static class TextTokenizer
    {
        public static ParsedText Tokenize(string normalizedText, NlpOptions? options = null)
        {
            return Tokenize(normalizedText, normalizedText, options);
        }

        public static ParsedText Tokenize(
            string normalizedText,
            string originalText,
            NlpOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(normalizedText);

            options ??= new NlpOptions();

            List<Sentence> sentences = new List<Sentence>();
            List<Token> tokens = new List<Token>();
            List<string> diagnostics = new List<string>();

            foreach ((int start, int length) in TextChunker.FindSentenceSpans(normalizedText))
            {
                string sentenceText = normalizedText.Substring(start, length);
                List<Token> sentenceTokens = TokenizeSentence(
                    normalizedText,
                    start,
                    start + length,
                    sentences.Count);

                if (sentenceTokens.Count == 0)
                    continue;

                SentenceType type = ClassifySentence(sentenceTokens);
                Sentence sentence = new Sentence(
                    sentences.Count,
                    sentenceText,
                    start,
                    length,
                    type,
                    sentenceTokens);

                sentences.Add(sentence);
                tokens.AddRange(sentenceTokens);
            }

            if (normalizedText.Length > 0 && sentences.Count == 0)
                diagnostics.Add("No sentence content was found.");

            return new ParsedText(
                originalText ?? normalizedText,
                normalizedText,
                sentences,
                tokens,
                diagnostics);
        }

        private static List<Token> TokenizeSentence(string text, int start, int end, int sentenceIndex)
        {
            List<Token> tokens = new List<Token>();
            int index = start;

            while (index < end)
            {
                if (char.IsWhiteSpace(text[index]))
                {
                    index++;
                    continue;
                }

                bool isWord = IsWordStart(text, index, end);
                int tokenStart = index;

                if (isWord)
                {
                    index++;

                    while (index < end && IsWordPart(text, index, end))
                        index++;
                }
                else
                {
                    index++;

                    while (index < end
                        && !char.IsWhiteSpace(text[index])
                        && !IsWordStart(text, index, end))
                    {
                        index++;
                    }
                }

                string tokenText = text.Substring(tokenStart, index - tokenStart);
                PartOfSpeech partOfSpeech = isWord ? PartOfSpeech.Unknown : PartOfSpeech.Punctuation;

                tokens.Add(new Token(
                    tokenText,
                    tokenText.ToLowerInvariant(),
                    string.Empty,
                    partOfSpeech,
                    tokenStart,
                    index - tokenStart,
                    sentenceIndex,
                    tokens.Count));
            }

            return tokens;
        }

        private static bool IsWordStart(string text, int index, int end)
        {
            char value = text[index];

            if (char.IsLetterOrDigit(value) || value == '_')
                return true;

            return value == '\''
                && index + 1 < end
                && char.IsLetter(text[index + 1]);
        }

        private static bool IsWordPart(string text, int index, int end)
        {
            char value = text[index];

            if (char.IsLetterOrDigit(value) || value == '_')
                return true;

            if (index == 0 || index + 1 >= end)
                return false;

            char previous = text[index - 1];
            char next = text[index + 1];

            if (value == '\'')
                return char.IsLetterOrDigit(previous) && char.IsLetterOrDigit(next);

            if (value == '-')
                return char.IsLetterOrDigit(previous) && char.IsLetterOrDigit(next);

            if (value == '.')
                return char.IsDigit(previous) && char.IsDigit(next);

            return false;
        }

        private static SentenceType ClassifySentence(IReadOnlyList<Token> tokens)
        {
            bool isQuestion = tokens.Any(token => token.Normalized.Contains('?'));
            bool isExclamation = tokens.Any(token => token.Normalized.Contains('!'));

            if (isQuestion)
            {
                List<Token> words = tokens
                    .Where(token => token.PartOfSpeech != PartOfSpeech.Punctuation)
                    .ToList();

                if (words.Count > 0 && Lexicon.IsWhWord(words[0].Normalized))
                    return SentenceType.WhQuestion;

                if (words.Count > 1
                    && Lexicon.IsPreposition(words[0].Normalized)
                    && Lexicon.IsWhWord(words[1].Normalized))
                {
                    return SentenceType.WhQuestion;
                }

                return SentenceType.YesNoQuestion;
            }

            if (isExclamation)
                return SentenceType.Imperative;

            Token? first = tokens.FirstOrDefault(
                token => token.PartOfSpeech != PartOfSpeech.Punctuation);

            if (first is not null
                && (first.Normalized == "please"
                    || (Lexicon.IsKnownVerb(first.Normalized)
                        && !Lexicon.IsAuxiliary(first.Normalized))))
            {
                return SentenceType.Imperative;
            }

            return SentenceType.Declarative;
        }
    }
}
