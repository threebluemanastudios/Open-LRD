using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.NLP
{
    internal static class RelationExtractor
    {
        private static readonly HashSet<string> negators = new HashSet<string>(
            new[] { "not", "never", "no", "none", "nothing", "nobody" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> modals = new HashSet<string>(
            new[]
            {
                "can", "could", "will", "would", "shall", "should", "may",
                "might", "must", "ought"
            },
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> temporalPrepositions = new HashSet<string>(
            new[] { "during", "after", "before", "since", "until", "by" },
            StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> spatialPrepositions = new HashSet<string>(
            new[]
            {
                "at", "in", "on", "near", "inside", "outside", "under", "over",
                "beside", "between", "behind", "above", "below", "to", "from",
                "into", "onto", "toward", "towards", "across", "through", "along",
                "around", "upon"
            },
            StringComparer.OrdinalIgnoreCase);

        public static RelationExtractionResult Extract(ParsedText parsed, NlpOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(parsed);

            options ??= new NlpOptions();

            List<RelationFrame> frames = new List<RelationFrame>();
            List<string> diagnostics = new List<string>();

            foreach (Sentence sentence in parsed.Sentences)
            {
                if (sentence.Type == SentenceType.Imperative)
                {
                    diagnostics.Add("Imperative input is parsed but not ingested.");
                    continue;
                }

                if (sentence.Type is SentenceType.YesNoQuestion or SentenceType.WhQuestion)
                    continue;

                int clauseCount = 0;

                foreach (Clause clause in sentence.Clauses)
                {
                    clauseCount++;

                    if (clauseCount > options.MaxClausesPerSentence)
                    {
                        diagnostics.Add(
                            $"The maximum of {options.MaxClausesPerSentence} clauses per sentence was reached.");
                        break;
                    }

                    ExtractClause(sentence, clause, parsed, frames, diagnostics);
                }
            }

            return new RelationExtractionResult(frames, diagnostics);
        }

        internal static bool IsNegator(string normalized)
        {
            return negators.Contains(normalized)
                || normalized.Contains("n't", StringComparison.Ordinal);
        }

        internal static bool IsModal(string lemma)
        {
            return modals.Contains(lemma);
        }

        internal static bool IsSpatialPreposition(string value)
        {
            return spatialPrepositions.Contains(value);
        }

        internal static bool IsTemporalPreposition(string value)
        {
            return temporalPrepositions.Contains(value);
        }

        private static void ExtractClause(
            Sentence sentence,
            Clause clause,
            ParsedText parsed,
            List<RelationFrame> frames,
            List<string> diagnostics)
        {
            ClauseArgument? subjectSpan = FindArgument(clause, ClauseRole.Subject);
            List<ClauseArgument> qualifierSpans = clause.Arguments
                .Where(argument => argument.Role == ClauseRole.Qualifier)
                .ToList();
            List<Token> clauseTokens = sentence.Tokens
                .Skip(clause.StartToken)
                .Take(clause.EndToken - clause.StartToken + 1)
                .ToList();
            List<Token> predicateTokens = FindArgument(clause, ClauseRole.Predicate) is ClauseArgument predicateSpan
                ? sentence.Tokens
                    .Skip(predicateSpan.StartToken)
                    .Take(predicateSpan.EndToken - predicateSpan.StartToken + 1)
                    .ToList()
                : new List<Token>();

            if (subjectSpan is null)
            {
                diagnostics.Add("A clause without a subject was not extracted as a relation.");
                return;
            }

            FrameArgument? subject = CreateArgument(
                sentence,
                parsed,
                subjectSpan.StartToken,
                subjectSpan.EndToken,
                FrameRole.Subject,
                null);

            if (subject is null)
            {
                diagnostics.Add("A clause subject could not be resolved into a mention.");
                return;
            }

            Token? mainVerb = FindMainVerb(predicateTokens);
            bool hasBeAuxiliary = predicateTokens.Any(
                token => token.PartOfSpeech == PartOfSpeech.Auxiliary && token.Lemma == "be");
            string? modal = predicateTokens
                .Where(token => token.PartOfSpeech == PartOfSpeech.Auxiliary)
                .Select(token => token.Lemma)
                .FirstOrDefault(lemma => modals.Contains(lemma));
            bool negated = clauseTokens.Any(
                token => negators.Contains(token.Normalized)
                    || token.Normalized.Contains("n't", StringComparison.Ordinal));

            ClauseArgument? objectSpan = FindArgument(clause, ClauseRole.Object);
            ClauseArgument? complementSpan = FindArgument(clause, ClauseRole.Complement);
            ClauseArgument? byQualifier = qualifierSpans.FirstOrDefault(
                span => GetQualifierNounPhrase(sentence, clause, span) is QualifierPhrase phrase
                    && string.Equals(phrase.Preposition, "by", StringComparison.OrdinalIgnoreCase));

            bool passive = hasBeAuxiliary
                && mainVerb is not null
                && mainVerb.Lemma != "be"
                && byQualifier is not null;

            string predicateLemma;
            string relationName;
            LinkClass linkClass;
            Voice voice = Voice.Active;
            Tense tense = DetermineTense(predicateTokens, mainVerb, modal);
            FrameArgument primaryObject;
            List<FrameArgument> qualifiers = new List<FrameArgument>();
            double patternConfidence;

            if (passive)
            {
                QualifierPhrase agentPhrase = GetQualifierNounPhrase(sentence, clause, byQualifier!)!;
                FrameArgument? agent = CreateArgument(
                    sentence,
                    parsed,
                    agentPhrase.StartToken,
                    agentPhrase.EndToken,
                    FrameRole.Subject,
                    null);

                if (agent is null)
                {
                    diagnostics.Add("A passive agent could not be resolved into a mention.");
                    return;
                }

                subject = agent;
                primaryObject = subjectSpan is not null
                    ? CreateArgument(
                        sentence,
                        parsed,
                        subjectSpan.StartToken,
                        subjectSpan.EndToken,
                        FrameRole.Object,
                        null)!
                    : subject;

                predicateLemma = mainVerb!.Lemma;
                relationName = RelationRegistry.Normalize(mainVerb.Lemma);
                linkClass = LinkClass.actions;
                voice = Voice.Passive;
                patternConfidence = 0.80;
            }
            else if (mainVerb is null && hasBeAuxiliary && complementSpan is not null)
            {
                primaryObject = CreateArgument(
                    sentence,
                    parsed,
                    complementSpan.StartToken,
                    complementSpan.EndToken,
                    FrameRole.Object,
                    null)!;

                predicateLemma = "be";
                relationName = RelationRegistry.Normalize("be");
                linkClass = LinkClass.identities;
                patternConfidence = 0.95;
            }
            else if (mainVerb is null)
            {
                diagnostics.Add("A clause predicate could not be identified.");
                return;
            }
            else if (mainVerb.Lemma == "have")
            {
                if (objectSpan is null)
                {
                    diagnostics.Add("A possession relation requires an object.");
                    return;
                }

                primaryObject = CreateArgument(
                    sentence,
                    parsed,
                    objectSpan.StartToken,
                    objectSpan.EndToken,
                    FrameRole.Object,
                    null)!;

                predicateLemma = mainVerb.Lemma;
                relationName = RelationRegistry.Normalize(mainVerb.Lemma);
                linkClass = LinkClass.actions;
                patternConfidence = 0.95;
            }
            else if (objectSpan is not null)
            {
                List<(int Start, int End)> segments = SplitObjectSpan(
                    sentence,
                    objectSpan.StartToken,
                    objectSpan.EndToken);

                if (segments.Count > 1)
                {
                    foreach ((int start, int end) in segments)
                    {
                        FrameArgument? coordinatedObject = CreateArgument(
                            sentence,
                            parsed,
                            start,
                            end,
                            FrameRole.Object,
                            null);

                        if (coordinatedObject is null)
                            continue;

                        AppendQualifiers(
                            sentence,
                            clause,
                            parsed,
                            qualifierSpans,
                            passive ? byQualifier : null,
                            qualifiers);

                        AddFrame(
                            sentence,
                            clause,
                            subject,
                            coordinatedObject,
                            qualifiers,
                            mainVerb.Lemma,
                            RelationRegistry.Normalize(mainVerb.Lemma),
                            LinkClass.actions,
                            negated,
                            modal,
                            tense,
                            Voice.Active,
                            0.90,
                            frames,
                            diagnostics);

                        qualifiers = new List<FrameArgument>();
                    }

                    return;
                }

                primaryObject = CreateArgument(
                    sentence,
                    parsed,
                    objectSpan.StartToken,
                    objectSpan.EndToken,
                    FrameRole.Object,
                    null)!;

                predicateLemma = mainVerb.Lemma;
                relationName = RelationRegistry.Normalize(mainVerb.Lemma);
                linkClass = LinkClass.actions;
                patternConfidence = 0.90;
            }
            else
            {
                QualifierPhrase? verbPhrase = FindFirstQualifierPhrase(sentence, clause, qualifierSpans);

                if (verbPhrase is null)
                {
                    diagnostics.Add(
                        $"Intransitive relation '{mainVerb.Lemma}' has no object; ingestion is deferred.");
                    return;
                }

                primaryObject = CreateArgument(
                    sentence,
                    parsed,
                    verbPhrase.StartToken,
                    verbPhrase.EndToken,
                    FrameRole.Object,
                    null)!;

                predicateLemma = mainVerb.Lemma;
                relationName = RelationRegistry.Normalize(mainVerb.Lemma, verbPhrase.Preposition);
                linkClass = LinkClass.actions;
                patternConfidence = 0.85;
            }

            if (primaryObject is null)
            {
                diagnostics.Add("A relation object could not be resolved into a mention.");
                return;
            }

            AppendQualifiers(
                sentence,
                clause,
                parsed,
                qualifierSpans,
                passive ? byQualifier : null,
                qualifiers);

            AddFrame(
                sentence,
                clause,
                subject,
                primaryObject,
                qualifiers,
                predicateLemma,
                relationName,
                linkClass,
                negated,
                modal,
                tense,
                voice,
                patternConfidence,
                frames,
                diagnostics);
        }

        private static void AddFrame(
            Sentence sentence,
            Clause clause,
            FrameArgument subject,
            FrameArgument primaryObject,
            List<FrameArgument> qualifiers,
            string predicateLemma,
            string relationName,
            LinkClass linkClass,
            bool negated,
            string? modal,
            Tense tense,
            Voice voice,
            double patternConfidence,
            List<RelationFrame> frames,
            List<string> diagnostics)
        {
            double confidence = Math.Min(
                Math.Min(subject.Confidence, primaryObject.Confidence),
                patternConfidence);
            bool ingestible = !negated
                && modal is null
                && !subject.IsPronoun
                && !primaryObject.IsPronoun
                && voice != Voice.Unknown;
            List<FrameArgument> arguments = new List<FrameArgument>(qualifiers.Count + 2)
            {
                subject,
                primaryObject
            };
            arguments.AddRange(qualifiers);

            List<string> frameDiagnostics = new List<string>();

            if (negated)
                diagnostics.Add($"Skipped negated relation '{relationName}'.");

            if (modal is not null)
                diagnostics.Add($"Skipped modal relation '{relationName}' (modal '{modal}').");

            frames.Add(new RelationFrame(
                predicateLemma,
                relationName,
                linkClass,
                arguments,
                negated,
                modal,
                tense,
                voice,
                sentence.Index,
                clause.Index,
                clause.StartToken,
                clause.EndToken,
                confidence,
                ingestible,
                frameDiagnostics));
        }

        private static void AppendQualifiers(
            Sentence sentence,
            Clause clause,
            ParsedText parsed,
            List<ClauseArgument> qualifierSpans,
            ClauseArgument? excluded,
            List<FrameArgument> qualifiers)
        {
            foreach (ClauseArgument span in qualifierSpans)
            {
                if (excluded is not null
                    && span.StartToken == excluded.StartToken
                    && span.EndToken == excluded.EndToken)
                {
                    continue;
                }

                QualifierPhrase? phrase = GetQualifierNounPhrase(sentence, clause, span);

                if (phrase is null)
                    continue;

                FrameArgument? argument = CreateArgument(
                    sentence,
                    parsed,
                    phrase.StartToken,
                    phrase.EndToken,
                    FrameRole.Qualifier,
                    phrase.Preposition);

                if (argument is null)
                    continue;

                FrameRole role = ClassifyQualifier(argument.NodeType, argument.Kind, phrase.Preposition);
                qualifiers.Add(argument with { Role = role });
            }
        }

        private static FrameRole ClassifyQualifier(
            NodeTypes nodeType,
            MentionKind kind,
            string? preposition)
        {
            if (nodeType == NodeTypes.Locations)
                return FrameRole.Location;

            if (nodeType == NodeTypes.RawData && kind is MentionKind.Date or MentionKind.Time)
                return FrameRole.Time;

            if (preposition is not null && temporalPrepositions.Contains(preposition))
                return FrameRole.Time;

            if (preposition is not null && spatialPrepositions.Contains(preposition))
                return FrameRole.Location;

            return FrameRole.Qualifier;
        }

        private static QualifierPhrase? FindFirstQualifierPhrase(
            Sentence sentence,
            Clause clause,
            List<ClauseArgument> qualifierSpans)
        {
            foreach (ClauseArgument span in qualifierSpans)
            {
                QualifierPhrase? phrase = GetQualifierNounPhrase(sentence, clause, span);

                if (phrase is not null)
                    return phrase;
            }

            return null;
        }

        private static QualifierPhrase? GetQualifierNounPhrase(
            Sentence sentence,
            Clause clause,
            ClauseArgument qualifier)
        {
            Chunk? prepositionalPhrase = clause.Chunks.FirstOrDefault(
                chunk => chunk.Kind == ChunkKind.PrepositionalPhrase
                    && chunk.StartToken == qualifier.StartToken
                    && chunk.EndToken == qualifier.EndToken);

            prepositionalPhrase ??= clause.Chunks.FirstOrDefault(
                chunk => chunk.Kind == ChunkKind.PrepositionalPhrase
                    && chunk.StartToken >= qualifier.StartToken
                    && chunk.EndToken <= qualifier.EndToken);

            if (prepositionalPhrase is null)
                return null;

            Chunk? nounPhrase = clause.Chunks.FirstOrDefault(
                chunk => chunk.Kind == ChunkKind.NounPhrase
                    && chunk.StartToken > prepositionalPhrase.StartToken
                    && chunk.EndToken <= prepositionalPhrase.EndToken);

            if (nounPhrase is null)
                return null;

            Token prepositionToken = sentence.Tokens[prepositionalPhrase.StartToken];
            string preposition = string.IsNullOrWhiteSpace(prepositionToken.Lemma)
                ? prepositionToken.Normalized
                : prepositionToken.Lemma;

            return new QualifierPhrase(preposition, nounPhrase.StartToken, nounPhrase.EndToken);
        }

        internal static FrameArgument? CreateArgument(
            Sentence sentence,
            ParsedText parsed,
            int start,
            int end,
            FrameRole role,
            string? preposition)
        {
            if (start < 0 || end < start || end >= sentence.Tokens.Count)
                return null;

            Mention? mention = parsed.Mentions.FirstOrDefault(
                candidate => candidate.SentenceIndex == sentence.Index
                    && candidate.StartToken == start
                    && candidate.EndToken == end);

            mention ??= parsed.Mentions.FirstOrDefault(
                candidate => candidate.SentenceIndex == sentence.Index
                    && candidate.StartToken >= start
                    && candidate.EndToken <= end);

            if (mention is not null)
            {
                return new FrameArgument(
                    role,
                    mention.SurfaceText,
                    mention.Lemma,
                    mention.NodeType,
                    mention.Kind,
                    sentence.Index,
                    start,
                    end,
                    false,
                    preposition,
                    mention.Confidence);
            }

            List<Token> tokens = sentence.Tokens
                .Skip(start)
                .Take(end - start + 1)
                .Where(token => token.PartOfSpeech != PartOfSpeech.Punctuation)
                .ToList();

            if (tokens.Count == 0)
                return null;

            bool isPronoun = tokens.All(token => token.PartOfSpeech == PartOfSpeech.Pronoun);
            Token head = tokens.LastOrDefault(
                    token => token.PartOfSpeech is PartOfSpeech.Noun
                        or PartOfSpeech.ProperNoun
                        or PartOfSpeech.Number)
                ?? tokens[^1];
            string lemma = string.IsNullOrWhiteSpace(head.Lemma) ? head.Normalized : head.Lemma;
            NodeTypes nodeType = head.PartOfSpeech switch
            {
                PartOfSpeech.Number => NodeTypes.RawData,
                PartOfSpeech.ProperNoun or PartOfSpeech.Pronoun => NodeTypes.Entities,
                _ => NodeTypes.Concepts
            };
            MentionKind kind = head.PartOfSpeech switch
            {
                PartOfSpeech.Number => MentionKind.Quantity,
                PartOfSpeech.ProperNoun or PartOfSpeech.Pronoun => MentionKind.Person,
                _ => MentionKind.Concept
            };

            return new FrameArgument(
                role,
                string.Join(" ", tokens.Select(token => token.Text)),
                lemma,
                nodeType,
                kind,
                sentence.Index,
                start,
                end,
                isPronoun,
                preposition,
                0.60);
        }

        internal static Token? FindMainVerb(IReadOnlyList<Token> predicateTokens)
        {
            Token? mainVerb = null;

            foreach (Token token in predicateTokens)
            {
                if (token.PartOfSpeech == PartOfSpeech.Verb
                    && !Lexicon.IsAuxiliary(token.Normalized))
                {
                    mainVerb = token;
                }
            }

            if (mainVerb is not null)
                return mainVerb;

            foreach (Token token in predicateTokens)
            {
                if (token.PartOfSpeech == PartOfSpeech.Verb)
                    mainVerb = token;
            }

            if (mainVerb is not null)
                return mainVerb;

            foreach (Token token in predicateTokens)
            {
                if (token.PartOfSpeech == PartOfSpeech.Auxiliary
                    && token.Lemma is "have" or "do")
                {
                    mainVerb = token;
                }
            }

            return mainVerb;
        }

        internal static Tense DetermineTense(
            IReadOnlyList<Token> predicateTokens,
            Token? mainVerb,
            string? modal)
        {
            if (string.Equals(modal, "will", StringComparison.OrdinalIgnoreCase)
                || string.Equals(modal, "shall", StringComparison.OrdinalIgnoreCase))
            {
                return Tense.Future;
            }

            if (predicateTokens.Any(token =>
                token.Normalized is "was" or "were" or "had" or "did"))
            {
                return Tense.Past;
            }

            if (mainVerb is null)
            {
                if (predicateTokens.Any(token => token.Normalized is "am" or "is" or "are"))
                    return Tense.Present;

                return Tense.Unknown;
            }

            if (mainVerb.Normalized.EndsWith("ed", StringComparison.OrdinalIgnoreCase))
                return Tense.Past;

            if (Lexicon.TryGetIrregularLemma(mainVerb.Normalized, out string irregularLemma)
                && !string.Equals(irregularLemma, mainVerb.Normalized, StringComparison.OrdinalIgnoreCase)
                && mainVerb.Normalized is not (
                    "am" or "is" or "are" or "has" or "have" or "do" or "does"
                    or "being" or "having" or "done" or "doing" or "been"))
            {
                return Tense.Past;
            }

            return Tense.Present;
        }

        private static List<(int Start, int End)> SplitObjectSpan(
            Sentence sentence,
            int start,
            int end)
        {
            List<(int Start, int End)> segments = new List<(int Start, int End)>();
            int segmentStart = start;

            for (int index = start; index <= end; index++)
            {
                Token token = sentence.Tokens[index];

                if (token.PartOfSpeech != PartOfSpeech.Conjunction
                    || (token.Normalized != "and" && token.Normalized != "or"))
                {
                    continue;
                }

                if (!HasNounToken(sentence, segmentStart, index - 1)
                    || !HasNounToken(sentence, index + 1, end))
                {
                    continue;
                }

                segments.Add(TrimSpan(sentence, segmentStart, index - 1));
                segmentStart = index + 1;
            }

            segments.Add(TrimSpan(sentence, segmentStart, end));
            return segments.Where(segment => segment.Start <= segment.End).ToList();
        }

        private static (int Start, int End) TrimSpan(Sentence sentence, int start, int end)
        {
            while (start <= end
                && sentence.Tokens[start].PartOfSpeech == PartOfSpeech.Punctuation)
            {
                start++;
            }

            while (end >= start
                && sentence.Tokens[end].PartOfSpeech == PartOfSpeech.Punctuation)
            {
                end--;
            }

            return (start, end);
        }

        private static bool HasNounToken(Sentence sentence, int start, int end)
        {
            for (int index = start; index <= end; index++)
            {
                if (sentence.Tokens[index].PartOfSpeech is PartOfSpeech.Noun
                    or PartOfSpeech.ProperNoun
                    or PartOfSpeech.Pronoun
                    or PartOfSpeech.Number)
                {
                    return true;
                }
            }

            return false;
        }

        private static ClauseArgument? FindArgument(Clause clause, ClauseRole role)
        {
            return clause.Arguments.FirstOrDefault(argument => argument.Role == role);
        }

        private sealed record QualifierPhrase(string Preposition, int StartToken, int EndToken);
    }
}
