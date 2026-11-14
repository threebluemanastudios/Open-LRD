using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;

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
            ClauseArgument? predicateSpan = FindArgument(clause, ClauseRole.Predicate);
            List<Token> predicateTokens = predicateSpan is null
                ? new List<Token>()
                : sentence.Tokens
                    .Skip(predicateSpan.StartToken)
                    .Take(predicateSpan.EndToken - predicateSpan.StartToken + 1)
                    .ToList();
            List<Token> clauseTokens = sentence.Tokens
                .Skip(clause.StartToken)
                .Take(clause.EndToken - clause.StartToken + 1)
                .ToList();

            if (subjectSpan is null)
            {
                diagnostics.Add("A clause without a subject was not extracted as a relation.");
                return;
            }

            Token? negatorToken = clauseTokens.LastOrDefault(
                token => negators.Contains(token.Normalized)
                    || token.Normalized.Contains("n't", StringComparison.Ordinal));
            bool negated = negatorToken is not null;

            Token? overallMainVerb = FindMainVerb(predicateTokens);
            bool overallHasBeAuxiliary = predicateTokens.Any(
                token => token.PartOfSpeech == PartOfSpeech.Auxiliary && token.Lemma == "be");
            ClauseArgument? byQualifier = qualifierSpans.FirstOrDefault(
                span => GetQualifierNounPhrase(sentence, clause, span) is QualifierPhrase phrase
                    && string.Equals(phrase.Preposition, "by", StringComparison.OrdinalIgnoreCase));
            bool passive = overallHasBeAuxiliary
                && overallMainVerb is not null
                && overallMainVerb.Lemma != "be"
                && byQualifier is not null;

            List<(int Start, int End)> subjectSegments = SplitCoordinatedSpan(
                sentence,
                subjectSpan.StartToken,
                subjectSpan.EndToken,
                IsNounHeadToken,
                out CoordinationKind subjectCoordination);

            if (subjectSegments.Count == 0)
                subjectSegments.Add((subjectSpan.StartToken, subjectSpan.EndToken));

            if (passive && subjectSegments.Count > 1)
            {
                subjectSegments = new List<(int Start, int End)>
                {
                    (subjectSpan.StartToken, subjectSpan.EndToken)
                };
                subjectCoordination = CoordinationKind.None;
            }

            CoordinationKind predicateCoordination = CoordinationKind.None;
            List<(int Start, int End)> predicateSegments = predicateSpan is null
                ? new List<(int Start, int End)>()
                : SplitCoordinatedSpan(
                    sentence,
                    predicateSpan.StartToken,
                    predicateSpan.EndToken,
                    IsVerbHeadToken,
                    out predicateCoordination);

            if (predicateSegments.Count == 0)
                predicateSegments.Add((clause.StartToken, clause.StartToken - 1));

            CoordinationKind coordination = Combine(subjectCoordination, predicateCoordination);
            int coordinationGroup = (sentence.Index + 1) * 1000 + clause.Index;

            foreach ((int subjectStart, int subjectEnd) in subjectSegments)
            {
                foreach ((int predicateStart, int predicateEnd) in predicateSegments)
                {
                    List<Token> groupTokens = predicateEnd < predicateStart
                        ? new List<Token>()
                        : sentence.Tokens
                            .Skip(predicateStart)
                            .Take(predicateEnd - predicateStart + 1)
                            .ToList();

                    ExtractClauseCore(
                        sentence,
                        clause,
                        parsed,
                        subjectStart,
                        subjectEnd,
                        groupTokens,
                        negated,
                        negatorToken,
                        qualifierSpans,
                        coordination,
                        coordinationGroup,
                        frames,
                        diagnostics);
                }
            }
        }

        private static void ExtractClauseCore(
            Sentence sentence,
            Clause clause,
            ParsedText parsed,
            int subjectStart,
            int subjectEnd,
            List<Token> predicateTokens,
            bool negated,
            Token? negatorToken,
            List<ClauseArgument> qualifierSpans,
            CoordinationKind coordination,
            int coordinationGroup,
            List<RelationFrame> frames,
            List<string> diagnostics)
        {
            FrameArgument? subject = CreateArgument(
                sentence,
                parsed,
                subjectStart,
                subjectEnd,
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
            NegationKind negation = negated ? NegationKind.Predicate : NegationKind.None;
            QualifierPhrase? negatedQualifier = null;
            bool indefiniteCopulaNominal = false;
            List<FrameArgument> copulaProperties = new List<FrameArgument>();

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
                primaryObject = CreateArgument(
                    sentence,
                    parsed,
                    subjectStart,
                    subjectEnd,
                    FrameRole.Object,
                    null)!;

                predicateLemma = mainVerb!.Lemma;
                relationName = RelationRegistry.Normalize(mainVerb.Lemma);
                linkClass = LinkClass.actions;
                voice = Voice.Passive;
                patternConfidence = 0.80;
            }
            else if (mainVerb is null && hasBeAuxiliary && complementSpan is not null)
            {
                int complementStart = complementSpan.StartToken;

                if (negated
                    && negatorToken is not null
                    && negatorToken.TokenIndex == complementSpan.StartToken - 1)
                {
                    complementStart = negatorToken.TokenIndex;
                    negation = NegationKind.Complement;
                }

                FrameArgument? complement = CreateArgument(
                    sentence,
                    parsed,
                    complementStart,
                    complementSpan.EndToken,
                    FrameRole.Object,
                    null);

                if (complement is null)
                {
                    diagnostics.Add("A relation object could not be resolved into a mention.");
                    return;
                }

                if (negation == NegationKind.Complement)
                    complement = complement with { Lemma = complement.SurfaceText };

                if (negation == NegationKind.None
                    && TrySplitIndefinitePredicateNominal(
                        sentence,
                        parsed,
                        complementSpan,
                        out FrameArgument? category,
                        out copulaProperties))
                {
                    primaryObject = category!;
                    indefiniteCopulaNominal = true;
                }
                else
                {
                    primaryObject = complement;
                }
                predicateLemma = "be";
                relationName = RelationRegistry.Normalize("be");
                linkClass = LinkClass.identities;
                patternConfidence = 0.95;
            }
            else if (mainVerb is null && hasBeAuxiliary && qualifierSpans.Count > 0)
            {
                QualifierPhrase? phrase = FindFirstQualifierPhrase(sentence, clause, qualifierSpans);

                if (phrase is null)
                {
                    diagnostics.Add("A copula complement could not be resolved into a mention.");
                    return;
                }

                int complementStart = phrase.PrepositionToken;

                if (negated
                    && negatorToken is not null
                    && negatorToken.TokenIndex == phrase.PrepositionToken - 1)
                {
                    complementStart = negatorToken.TokenIndex;
                    negation = NegationKind.Complement;
                    negatedQualifier = phrase;
                }

                FrameArgument? complement = CreateArgument(
                    sentence,
                    parsed,
                    complementStart,
                    phrase.PrepositionToken,
                    FrameRole.Object,
                    null);

                if (complement is null)
                {
                    diagnostics.Add("A copula complement could not be resolved into a mention.");
                    return;
                }

                if (negation == NegationKind.Complement)
                    complement = complement with { Lemma = complement.SurfaceText };

                primaryObject = complement;
                predicateLemma = "be";
                relationName = RelationRegistry.Normalize("be");
                linkClass = LinkClass.identities;
                patternConfidence = 0.90;
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
                List<(int Start, int End)> segments = SplitCoordinatedSpan(
                    sentence,
                    objectSpan.StartToken,
                    objectSpan.EndToken,
                    IsNounHeadToken,
                    out CoordinationKind objectCoordination);

                if (segments.Count > 1)
                {
                    CoordinationKind frameCoordination = Combine(coordination, objectCoordination);

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
                            null,
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
                            negation,
                            modal,
                            tense,
                            Voice.Active,
                            0.90,
                            frameCoordination,
                            coordinationGroup,
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
                negatedQualifier,
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
                negation,
                modal,
                tense,
                voice,
                patternConfidence,
                coordination,
                coordinationGroup,
                frames,
                diagnostics);

            if (indefiniteCopulaNominal)
            {
                OntologyTypes.EnsureRegistered();
                OntologyRelations.EnsureRegistered();
                AddFrame(
                    sentence,
                    clause,
                    subject,
                    primaryObject,
                    new List<FrameArgument>(),
                    "be",
                    "is_a",
                    OntologyTypes.ontology,
                    negated,
                    negation,
                    modal,
                    tense,
                    voice,
                    patternConfidence,
                    coordination,
                    coordinationGroup,
                    frames,
                    diagnostics);

                foreach (FrameArgument property in copulaProperties)
                {
                    AddFrame(
                        sentence,
                        clause,
                        subject,
                        property,
                        new List<FrameArgument>(),
                        "have",
                        "has_property",
                        OntologyTypes.properties,
                        negated,
                        negation,
                        modal,
                        tense,
                        voice,
                        patternConfidence,
                        coordination,
                        coordinationGroup,
                        frames,
                        diagnostics);
                }
            }
        }

        private static bool TrySplitIndefinitePredicateNominal(
            Sentence sentence,
            ParsedText parsed,
            ClauseArgument complementSpan,
            out FrameArgument? category,
            out List<FrameArgument> properties)
        {
            category = null;
            properties = new List<FrameArgument>();
            int start = complementSpan.StartToken;
            int end = complementSpan.EndToken;

            while (start <= end && sentence.Tokens[start].PartOfSpeech == PartOfSpeech.Punctuation)
                start++;

            if (start > end
                || sentence.Tokens[start].PartOfSpeech != PartOfSpeech.Determiner
                || sentence.Tokens[start].Normalized is not ("a" or "an"))
            {
                return false;
            }

            int categoryIndex = -1;
            for (int index = end; index > start; index--)
            {
                if (sentence.Tokens[index].PartOfSpeech is PartOfSpeech.Noun or PartOfSpeech.ProperNoun)
                {
                    categoryIndex = index;
                    break;
                }
            }

            if (categoryIndex <= start)
                return false;

            List<FrameArgument> modifiers = new List<FrameArgument>();
            for (int index = start + 1; index < categoryIndex; index++)
            {
                PartOfSpeech partOfSpeech = sentence.Tokens[index].PartOfSpeech;
                if (partOfSpeech is PartOfSpeech.Adverb or PartOfSpeech.Punctuation)
                    continue;

                if (partOfSpeech != PartOfSpeech.Adjective)
                    return false;

                FrameArgument? property = CreateArgument(
                    sentence,
                    parsed,
                    index,
                    index,
                    FrameRole.Object,
                    null);
                if (property is not null)
                    modifiers.Add(property);
            }

            category = CreateArgument(
                sentence,
                parsed,
                categoryIndex,
                categoryIndex,
                FrameRole.Object,
                null);
            if (category is null)
                return false;

            properties = modifiers;
            return true;
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
            NegationKind negation,
            string? modal,
            Tense tense,
            Voice voice,
            double patternConfidence,
            CoordinationKind coordination,
            int coordinationGroup,
            List<RelationFrame> frames,
            List<string> diagnostics)
        {
            double confidence = Math.Min(
                Math.Min(subject.Confidence, primaryObject.Confidence),
                patternConfidence);
            bool ingestible = negation != NegationKind.Predicate
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

            if (negation == NegationKind.Predicate)
                diagnostics.Add($"Skipped negated relation '{relationName}'.");
            else if (negation == NegationKind.Complement)
                diagnostics.Add(
                    $"Ingested negated complement '{primaryObject.SurfaceText}' for relation '{relationName}'.");

            if (modal is not null)
                diagnostics.Add($"Skipped modal relation '{relationName}' (modal '{modal}').");

            if (coordination == CoordinationKind.Disjunction)
                diagnostics.Add(
                    $"Relation '{relationName}' was ingested as a disjunction alternative (group {coordinationGroup}).");

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
                frameDiagnostics,
                coordination,
                coordinationGroup,
                negation));
        }

        private static void AppendQualifiers(
            Sentence sentence,
            Clause clause,
            ParsedText parsed,
            List<ClauseArgument> qualifierSpans,
            ClauseArgument? excluded,
            QualifierPhrase? negatedQualifier,
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

                string? preposition = phrase.Preposition;

                if (negatedQualifier is not null
                    && phrase.StartToken == negatedQualifier.StartToken
                    && phrase.EndToken == negatedQualifier.EndToken)
                {
                    preposition = "not_" + preposition;
                }

                FrameArgument? argument = CreateArgument(
                    sentence,
                    parsed,
                    phrase.StartToken,
                    phrase.EndToken,
                    FrameRole.Qualifier,
                    preposition);

                if (argument is null)
                    continue;

                FrameRole role = ClassifyQualifier(argument.NodeType, argument.Kind, preposition);
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

            return new QualifierPhrase(
                preposition,
                prepositionalPhrase.StartToken,
                nounPhrase.StartToken,
                nounPhrase.EndToken);
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

        private static List<(int Start, int End)> SplitCoordinatedSpan(
            Sentence sentence,
            int start,
            int end,
            Func<Token, bool> isHead,
            out CoordinationKind coordination)
        {
            coordination = CoordinationKind.None;
            List<(int Start, int End)> segments = new List<(int Start, int End)>();
            int segmentStart = start;
            bool found = false;

            for (int index = start; index <= end; index++)
            {
                Token token = sentence.Tokens[index];

                if (token.PartOfSpeech != PartOfSpeech.Conjunction
                    || (token.Normalized != "and"
                        && token.Normalized != "or"
                        && token.Normalized != "nor"))
                {
                    continue;
                }

                if (!HasHead(sentence, segmentStart, index - 1, isHead)
                    || !HasHead(sentence, index + 1, end, isHead))
                {
                    continue;
                }

                found = true;
                coordination = Combine(
                    coordination,
                    token.Normalized == "and"
                        ? CoordinationKind.Conjunction
                        : CoordinationKind.Disjunction);
                segments.Add(TrimSpan(sentence, segmentStart, index - 1));
                segmentStart = index + 1;
            }

            segments.Add(TrimSpan(sentence, segmentStart, end));
            List<(int Start, int End)> result = segments
                .Where(segment => segment.Start <= segment.End)
                .ToList();

            if (!found)
                coordination = CoordinationKind.None;

            return result;
        }

        private static CoordinationKind Combine(CoordinationKind left, CoordinationKind right)
        {
            if (left == CoordinationKind.Disjunction || right == CoordinationKind.Disjunction)
                return CoordinationKind.Disjunction;

            if (left == CoordinationKind.Conjunction || right == CoordinationKind.Conjunction)
                return CoordinationKind.Conjunction;

            return CoordinationKind.None;
        }

        private static bool IsNounHeadToken(Token token)
        {
            return token.PartOfSpeech is PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Pronoun
                or PartOfSpeech.Number;
        }

        private static bool IsVerbHeadToken(Token token)
        {
            return token.PartOfSpeech is PartOfSpeech.Verb or PartOfSpeech.Auxiliary;
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

        private static bool HasHead(Sentence sentence, int start, int end, Func<Token, bool> isHead)
        {
            for (int index = start; index <= end; index++)
            {
                if (isHead(sentence.Tokens[index]))
                    return true;
            }

            return false;
        }

        private static ClauseArgument? FindArgument(Clause clause, ClauseRole role)
        {
            return clause.Arguments.FirstOrDefault(argument => argument.Role == role);
        }

        private sealed record QualifierPhrase(
            string Preposition,
            int PrepositionToken,
            int StartToken,
            int EndToken);
    }
}
