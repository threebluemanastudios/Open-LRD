using System.Text.RegularExpressions;

namespace OPEN_LRD.NLP
{
    /// <summary>Stores normalized query text and optional entity-exploration intent.</summary>
    internal sealed record ConversationalQuery(
        string QueryText,
        string? ExplorationEntity,
        IReadOnlyList<string> Diagnostics)
    {
        public bool IsExploration => !string.IsNullOrWhiteSpace(ExplorationEntity);
    }

    /// <summary>Normalizes conversational preambles and follow-up question forms before Ask.</summary>
    internal static partial class ConversationalQueryPreprocessor
    {
        private static readonly Regex[] preambles =
        {
            new Regex(@"^(?:can|could|would|will)\s+you\s+(?:please\s+)?tell\s+me\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"^(?:please\s+)?tell\s+me\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"^(?:can|could|would|will)\s+you\s+(?:please\s+)?show\s+me\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"^(?:please\s+)?show\s+me\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"^(?:do\s+you\s+(?:happen\s+to\s+)?know|does\s+anyone\s+know)\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"^(?:i(?:'|’)d\s+like\s+to\s+know|i\s+would\s+like\s+to\s+know|i\s+want\s+to\s+know)\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"^what\s+can\s+you\s+tell\s+me\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            new Regex(@"^what\s+do\s+you\s+know\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        };

        private static readonly Regex whatAbout = new Regex(
            @"^(?:what|how)\s+about\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex greetingPrefix = new Regex(
            @"^(?:hi|hello|hey|howdy)[!.,;:\s]+(?=(?:who|what|where|when|which|why|how|is|are|am|was|were|do|does|did|can|could|will|would|has|have|had)\b)(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex declarativeBooleanAuxiliary = new Regex(
            @"^(?<subject>.+?)\s+(?<aux>am|is|are|was|were|has|have|had|can|could|will|would|should|may|might|must|do|does|did)\s+(?<rest>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Rewrites conversational preambles, follow-ups, and embedded boolean queries into canonical question text.</summary>
        public static ConversationalQuery Process(
            string text,
            bool enablePreambles,
            string? previousQuestionTemplate = null)
        {
            ArgumentNullException.ThrowIfNull(text);

            string query = TextNormalizer.Normalize(text).Trim();
            List<string> diagnostics = new List<string>();
            string? explorationEntity = null;
            bool embeddedBoolean = false;

            if (enablePreambles)
            {
                Match greeting = greetingPrefix.Match(query);
                if (greeting.Success)
                {
                    query = greeting.Groups[1].Value.Trim();
                    diagnostics.Add("Ignored the greeting before the question.");
                }

                Match aboutMatch = whatAbout.Match(StripTrailingPunctuation(query));
                if (aboutMatch.Success)
                {
                    string entity = aboutMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(previousQuestionTemplate)
                        && previousQuestionTemplate.Contains("{entity}", StringComparison.Ordinal))
                    {
                        query = previousQuestionTemplate.Replace(
                            "{entity}",
                            entity,
                            StringComparison.Ordinal);
                        diagnostics.Add("Applied the previous question pattern to the new entity.");
                    }
                    else
                    {
                        explorationEntity = entity;
                        query = string.Empty;
                        diagnostics.Add("Interpreted the follow-up as an exploration of the entity's known facts.");
                    }
                }
                else
                {
                    foreach (Regex preamble in preambles)
                    {
                        Match match = preamble.Match(query);
                        if (!match.Success)
                            continue;

                        query = match.Groups[1].Value.Trim();
                        diagnostics.Add("Unwrapped a conversational preamble to identify the question.");
                        break;
                    }
                }

                query = StripTrailingPunctuation(query);

                if (explorationEntity is null
                    && query.StartsWith("about ", StringComparison.OrdinalIgnoreCase))
                {
                    explorationEntity = query["about ".Length..].Trim();
                    query = string.Empty;
                    if (explorationEntity.Length > 0)
                        diagnostics.Add("Interpreted the request as an exploration of the entity's known facts.");
                }
                else if (query.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
                {
                    query = query["if ".Length..].Trim();
                    embeddedBoolean = true;
                }
                else if (query.StartsWith("whether ", StringComparison.OrdinalIgnoreCase))
                {
                    query = query["whether ".Length..].Trim();
                    embeddedBoolean = true;
                }

                if (embeddedBoolean)
                    query = NormalizeBooleanQuestion(query);

                if (explorationEntity is null
                    && (embeddedBoolean || IsQuestionLike(query))
                    && !query.EndsWith('?'))
                {
                    query += "?";
                    diagnostics.Add("Interpreted the input as a question despite missing punctuation.");
                }
            }

            return new ConversationalQuery(query, explorationEntity, diagnostics);
        }

        /// <summary>Trims trailing sentence punctuation and whitespace from a query string.</summary>
        private static string StripTrailingPunctuation(string text)
        {
            return text.Trim().TrimEnd('.', '!', '?', ',', ';', ':').TrimEnd();
        }

        /// <summary>Rewrites an embedded declarative clause into an inverted yes/no question.</summary>
        private static string NormalizeBooleanQuestion(string text)
        {
            Match match = declarativeBooleanAuxiliary.Match(text.Trim());
            if (match.Success)
            {
                return match.Groups["aux"].Value + " "
                    + match.Groups["subject"].Value + " "
                    + match.Groups["rest"].Value;
            }

            string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            for (int index = 1; index < words.Length; index++)
            {
                string verb = words[index].Trim(',', ';', ':', '"', '\'').ToLowerInvariant();
                if (Lexicon.IsAuxiliary(verb)
                    || !TryLemmatizePredicate(verb, out string lemma, out bool past))
                {
                    continue;
                }

                string subject = string.Join(" ", words.Take(index));
                string rest = string.Join(" ", words.Skip(index + 1));
                bool pluralSubject = subject.Contains(" and ", StringComparison.OrdinalIgnoreCase)
                    || subject.Equals("we", StringComparison.OrdinalIgnoreCase)
                    || subject.Equals("they", StringComparison.OrdinalIgnoreCase);
                string auxiliary = past ? "Did" : pluralSubject ? "Do" : "Does";
                return string.IsNullOrEmpty(rest)
                    ? $"{auxiliary} {subject} {lemma}"
                    : $"{auxiliary} {subject} {lemma} {rest}";
            }

            return text;
        }

        /// <summary>Attempts to lemmatize a finite verb and determine whether it is in past tense.</summary>
        private static bool TryLemmatizePredicate(string value, out string lemma, out bool past)
        {
            lemma = value;
            past = false;

            if (Lexicon.TryGetIrregularLemma(value, out string irregularLemma)
                && !string.Equals(irregularLemma, value, StringComparison.OrdinalIgnoreCase))
            {
                lemma = irregularLemma;
                past = true;
                return true;
            }

            if (value.EndsWith("ed", StringComparison.OrdinalIgnoreCase)
                && value.Length > 2)
            {
                lemma = Lemmatizer.Lemmatize(value, PartOfSpeech.Verb);
                past = true;
                return Lexicon.IsKnownVerb(lemma);
            }

            string presentLemma = Lemmatizer.Lemmatize(value, PartOfSpeech.Verb);
            if (!string.Equals(presentLemma, value, StringComparison.OrdinalIgnoreCase)
                && Lexicon.IsKnownVerb(presentLemma))
            {
                lemma = presentLemma;
                return true;
            }

            if (Lexicon.IsKnownVerb(value))
            {
                lemma = value;
                return true;
            }

            return false;
        }

        /// <summary>Checks whether text begins with a wh-word or inverted auxiliary question pattern.</summary>
        private static bool IsQuestionLike(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            string first = words[0].TrimStart('(', '"', '\'').ToLowerInvariant();

            if (first is "who" or "what" or "where" or "when" or "which" or "why" or "how")
                return true;

            if (words.Length > 1
                && Lexicon.IsPreposition(first)
                && Lexicon.IsWhWord(words[1].Trim().ToLowerInvariant()))
            {
                return true;
            }

            return words.Length >= 2
                && Lexicon.IsAuxiliary(first)
                && !Lexicon.IsDeterminer(words[1].Trim().ToLowerInvariant());
        }
    }
}
