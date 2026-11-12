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

            foreach ((int start, int length) in FindSentenceSpans(normalizedText))
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

        private static List<(int Start, int Length)> FindSentenceSpans(string text)
        {
            List<(int Start, int Length)> spans = new List<(int Start, int Length)>();
            int start = 0;
            int index = 0;

            while (index < text.Length)
            {
                if (text[index] == '\n')
                {
                    AddSpan(spans, text, start, index);
                    index++;
                    start = index;
                    continue;
                }

                if (!IsTerminal(text[index]))
                {
                    index++;
                    continue;
                }

                int runEnd = index;

                while (runEnd < text.Length && IsTerminal(text[runEnd]))
                    runEnd++;

                if (text[index] == '.'
                    && (IsDecimalPoint(text, index) || IsAbbreviationPoint(text, index)))
                {
                    index = runEnd;
                    continue;
                }

                int end = runEnd;

                while (end < text.Length && IsClosingCharacter(text[end]))
                    end++;

                if (IsBoundary(text, end))
                {
                    AddSpan(spans, text, start, end);
                    index = end;
                    start = index;
                    continue;
                }

                index = runEnd;
            }

            AddSpan(spans, text, start, text.Length);
            return spans;
        }

        private static void AddSpan(List<(int Start, int Length)> spans, string text, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(text[start]))
                start++;

            while (end > start && char.IsWhiteSpace(text[end - 1]))
                end--;

            if (end > start)
                spans.Add((start, end - start));
        }

        private static bool IsTerminal(char value)
        {
            return value is '.' or '!' or '?';
        }

        private static bool IsClosingCharacter(char value)
        {
            return value is '"' or '\'' or ')' or ']' or '}' or ',';
        }

        private static bool IsBoundary(string text, int index)
        {
            if (index >= text.Length)
                return true;

            char value = text[index];

            if (char.IsWhiteSpace(value) || value == '\n')
                return true;

            return value is ',' or ';' or ':' or ')' or ']' or '}' or '"' or '\'';
        }

        private static bool IsDecimalPoint(string text, int index)
        {
            return index > 0
                && index + 1 < text.Length
                && char.IsDigit(text[index - 1])
                && char.IsDigit(text[index + 1]);
        }

        private static bool IsAbbreviationPoint(string text, int index)
        {
            int wordStart = index;
            int wordEnd = index;

            while (wordStart > 0)
            {
                char value = text[wordStart - 1];

                if (char.IsLetterOrDigit(value) || value == '.')
                    wordStart--;
                else
                    break;
            }

            string word = text.Substring(wordStart, wordEnd - wordStart);

            if (word.Length == 0)
                return false;

            if (Lexicon.IsAbbreviation(word))
                return true;

            if (word.Length == 1 && char.IsUpper(word[0]))
                return true;

            if (word.Contains('.'))
            {
                return word
                    .Split('.', StringSplitOptions.RemoveEmptyEntries)
                    .All(segment => segment.All(char.IsLetter));
            }

            return false;
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
