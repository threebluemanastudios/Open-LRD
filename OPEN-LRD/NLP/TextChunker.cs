using System.Text;

namespace OPEN_LRD.NLP
{
    internal sealed record TextChunkOptions
    {
        public int MaxChunkCharacters { get; init; } = 15000;
        public int MaxSentencesPerChunk { get; init; } = 40;
        public int MaxChunks { get; init; } = 10000;
    }

    internal sealed record TextChunk(
        int Index,
        string Text,
        int ParagraphIndex,
        int SentenceCount)
    {
        public int CharacterCount => Text.Length;
    }

    internal sealed record TextChunkingResult(
        IReadOnlyList<TextChunk> Chunks,
        int TotalCharacters,
        int ParagraphCount,
        int SentenceCount,
        bool Truncated);

    /// <summary>
    /// Splits raw text into bounded paragraphs and sentences so that each piece
    /// can be handed to a single NLP ingestion call.
    /// </summary>
    internal static class TextChunker
    {
        public static TextChunkingResult Chunk(string? rawText, TextChunkOptions? options = null)
        {
            options ??= new TextChunkOptions();
            Validate(options);

            ChunkCollector collector = new ChunkCollector(options);
            int totalCharacters = 0;
            int paragraphCount = 0;
            int sentenceCount = 0;

            if (!string.IsNullOrWhiteSpace(rawText))
            {
                foreach (string rawParagraph in SplitParagraphs(rawText))
                {
                    string paragraph = TextNormalizer.Normalize(rawParagraph);

                    if (paragraph.Length == 0)
                        continue;

                    paragraphCount++;
                    totalCharacters += paragraph.Length;

                    List<(int Start, int Length)> spans = FindSentenceSpans(paragraph);
                    sentenceCount += Math.Max(spans.Count, 1);

                    AddParagraph(collector, paragraph, spans, paragraphCount - 1, options);
                }
            }

            return new TextChunkingResult(
                collector.Chunks,
                totalCharacters,
                paragraphCount,
                sentenceCount,
                collector.Truncated);
        }

        /// <summary>
        /// Returns the tokenizer's sentence spans without tokenizing.
        /// Shared so the chunker and the tokenizer agree on boundaries.
        /// </summary>
        internal static List<(int Start, int Length)> FindSentenceSpans(string text)
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

        /// <summary>
        /// Splits a chunk in half at the sentence boundary closest to the midpoint.
        /// Returns false when the text holds fewer than two sentences.
        /// </summary>
        internal static bool TrySplitAtSentenceBoundary(string text, out string first, out string second)
        {
            first = string.Empty;
            second = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            List<(int Start, int Length)> spans = FindSentenceSpans(text);

            if (spans.Count < 2)
                return false;

            int middle = text.Length / 2;
            int split = -1;

            for (int index = 1; index < spans.Count; index++)
            {
                int position = spans[index].Start;

                if (position <= 0 || position >= text.Length)
                    continue;

                if (split < 0 || Math.Abs(position - middle) < Math.Abs(split - middle))
                    split = position;
            }

            if (split <= 0)
                return false;

            string left = text.Substring(0, split).Trim();
            string right = text.Substring(split).Trim();

            if (left.Length == 0 || right.Length == 0)
                return false;

            first = left;
            second = right;
            return true;
        }

        private static void Validate(TextChunkOptions options)
        {
            if (options.MaxChunkCharacters <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxChunkCharacters must be greater than zero.");
            }

            if (options.MaxSentencesPerChunk <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxSentencesPerChunk must be greater than zero.");
            }

            if (options.MaxChunks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxChunks must be greater than zero.");
            }
        }

        private static void AddParagraph(
            ChunkCollector collector,
            string paragraph,
            List<(int Start, int Length)> spans,
            int paragraphIndex,
            TextChunkOptions options)
        {
            if (collector.Truncated)
                return;

            if (spans.Count == 0)
            {
                collector.Add(paragraph, paragraphIndex, 1);
                return;
            }

            if (paragraph.Length <= options.MaxChunkCharacters
                && spans.Count <= options.MaxSentencesPerChunk)
            {
                collector.Add(paragraph, paragraphIndex, spans.Count);
                return;
            }

            int chunkStart = -1;
            int chunkEnd = -1;
            int chunkSentences = 0;

            void Flush()
            {
                if (chunkStart >= 0 && chunkEnd > chunkStart)
                {
                    collector.Add(
                        paragraph.Substring(chunkStart, chunkEnd - chunkStart),
                        paragraphIndex,
                        chunkSentences);
                }

                chunkStart = -1;
                chunkEnd = -1;
                chunkSentences = 0;
            }

            foreach ((int start, int length) in spans)
            {
                if (collector.Truncated)
                    return;

                if (length > options.MaxChunkCharacters)
                {
                    Flush();

                    foreach (string piece in HardSplit(paragraph.Substring(start, length), options.MaxChunkCharacters))
                        collector.Add(piece, paragraphIndex, 1);

                    continue;
                }

                int candidateStart = chunkStart < 0 ? start : chunkStart;
                int candidateEnd = start + length;
                int candidateSentences = chunkSentences + 1;

                if (chunkStart >= 0
                    && (candidateEnd - candidateStart > options.MaxChunkCharacters
                        || candidateSentences > options.MaxSentencesPerChunk))
                {
                    Flush();
                    candidateStart = start;
                    candidateEnd = start + length;
                    candidateSentences = 1;
                }

                chunkStart = candidateStart;
                chunkEnd = candidateEnd;
                chunkSentences = candidateSentences;
            }

            Flush();
        }

        private static IEnumerable<string> HardSplit(string text, int maxCharacters)
        {
            int offset = 0;

            while (offset < text.Length)
            {
                while (offset < text.Length && char.IsWhiteSpace(text[offset]))
                    offset++;

                if (offset >= text.Length)
                    yield break;

                int remaining = text.Length - offset;

                if (remaining <= maxCharacters)
                {
                    string tail = text.Substring(offset).Trim();

                    if (tail.Length > 0)
                        yield return tail;

                    yield break;
                }

                int cut = FindHardSplit(text, offset, maxCharacters);

                if (cut <= offset)
                    cut = offset + maxCharacters;

                string piece = text.Substring(offset, cut - offset).Trim();

                if (piece.Length > 0)
                    yield return piece;

                offset = cut;
            }
        }

        private static int FindHardSplit(string text, int offset, int maxCharacters)
        {
            int limit = Math.Min(text.Length, offset + maxCharacters);

            for (int index = limit; index > offset; index--)
            {
                char value = text[index - 1];

                if (value is ',' or ';' or ' ' or '\n' or '\t')
                    return index;
            }

            return limit;
        }

        private static List<string> SplitParagraphs(string text)
        {
            List<string> paragraphs = new List<string>();
            StringBuilder current = new StringBuilder();
            int index = 0;

            while (index < text.Length)
            {
                int lineStart = index;

                while (index < text.Length && text[index] != '\n')
                    index++;

                int lineEnd = index;

                if (index < text.Length)
                    index++;

                int contentEnd = lineEnd;

                if (contentEnd > lineStart && text[contentEnd - 1] == '\r')
                    contentEnd--;

                bool blank = true;

                for (int position = lineStart; position < contentEnd; position++)
                {
                    if (!char.IsWhiteSpace(text[position]))
                    {
                        blank = false;
                        break;
                    }
                }

                if (blank)
                {
                    if (current.Length > 0)
                    {
                        paragraphs.Add(current.ToString());
                        current.Clear();
                    }

                    continue;
                }

                if (current.Length > 0)
                    current.Append('\n');

                current.Append(text, lineStart, contentEnd - lineStart);
            }

            if (current.Length > 0)
                paragraphs.Add(current.ToString());

            return paragraphs;
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

        private sealed class ChunkCollector
        {
            private readonly TextChunkOptions options;
            private readonly List<TextChunk> chunks = new List<TextChunk>();

            public ChunkCollector(TextChunkOptions options)
            {
                this.options = options;
            }

            public IReadOnlyList<TextChunk> Chunks => chunks;

            public bool Truncated { get; private set; }

            public void Add(string text, int paragraphIndex, int sentenceCount)
            {
                if (Truncated)
                    return;

                string trimmed = text.Trim();

                if (trimmed.Length == 0)
                    return;

                if (chunks.Count >= options.MaxChunks)
                {
                    Truncated = true;
                    return;
                }

                chunks.Add(new TextChunk(
                    chunks.Count,
                    trimmed,
                    paragraphIndex,
                    Math.Max(sentenceCount, 1)));
            }
        }
    }
}
