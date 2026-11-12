namespace OPEN_LRD.NLP
{
    internal sealed class NlpPipeline
    {
        private readonly LRD_Core core;
        private readonly NlpOptions options;

        public NlpPipeline(LRD_Core core, NlpOptions? options = null)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));

            if (!core.initialized)
                throw new InvalidOperationException("The core graph must be initialized before creating an NLP pipeline.");

            this.options = options ?? new NlpOptions();
        }

        public NlpResult Analyze(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new NlpResult(NlpStatus.InvalidInput, new[] { "Text is required." });

            if (text.Length > options.MaxCharacters)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { $"Text exceeds the maximum length of {options.MaxCharacters} characters." });
            }

            string normalizedText = TextNormalizer.Normalize(text);
            ParsedText parsed = TextTokenizer.Tokenize(normalizedText, text, options);

            if (parsed.Tokens.Count == 0)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    parsed.Diagnostics.Count > 0
                        ? parsed.Diagnostics
                        : new[] { "No analyzable text was found." },
                    parsed);
            }

            if (parsed.Tokens.All(token => token.PartOfSpeech == PartOfSpeech.Punctuation))
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { "Text does not contain any analyzable words." },
                    parsed);
            }

            if (parsed.Sentences.Count > options.MaxSentences)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { $"Text exceeds the maximum of {options.MaxSentences} sentences." },
                    parsed);
            }

            if (parsed.Tokens.Count > options.MaxTokens)
            {
                return new NlpResult(
                    NlpStatus.InvalidInput,
                    new[] { $"Text exceeds the maximum of {options.MaxTokens} tokens." },
                    parsed);
            }

            ParsedText tagged = PosTagger.Tag(parsed);
            ParsedText shallow = ShallowParser.Parse(tagged);
            ParsedText recognized = NamedEntityRecognizer.Recognize(shallow);
            return new NlpResult(NlpStatus.Parsed, recognized.Diagnostics, recognized);
        }
    }
}
