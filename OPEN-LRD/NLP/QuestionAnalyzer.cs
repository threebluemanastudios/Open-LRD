using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.NLP
{
    internal static class QuestionAnalyzer
    {
        private static readonly string[] spatialPrepositions =
        {
            "at", "in", "on", "near", "inside", "outside", "under", "over",
            "beside", "between", "behind", "above", "below", "to", "from",
            "into", "onto", "toward", "towards", "across", "through", "along",
            "around", "upon"
        };

        private static readonly string[] temporalPrepositions =
        {
            "at", "in", "on", "during", "after", "before", "since", "until", "by"
        };

        private static readonly NodeTypes[] broadWhatTypes =
        {
            NodeTypes.Entities, NodeTypes.Concepts, NodeTypes.Events,
            NodeTypes.Objects, NodeTypes.Locations, NodeTypes.RawData,
            NodeTypes.Names, NodeTypes.Basics
        };

        private static readonly Dictionary<string, WhKind> whKinds =
            new Dictionary<string, WhKind>(StringComparer.OrdinalIgnoreCase)
            {
                ["who"] = WhKind.Who,
                ["whom"] = WhKind.Whom,
                ["whose"] = WhKind.Whose,
                ["what"] = WhKind.What,
                ["which"] = WhKind.Which,
                ["when"] = WhKind.When,
                ["where"] = WhKind.Where,
                ["why"] = WhKind.Why,
                ["how"] = WhKind.How
            };

        public static QuestionAnalysis Analyze(
            ParsedText parsed,
            EntityResolver resolver,
            NlpOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(parsed);
            ArgumentNullException.ThrowIfNull(resolver);

            options ??= new NlpOptions();

            List<string> diagnostics = new List<string>();
            List<Sentence> questions = parsed.Sentences
                .Where(sentence => sentence.Type is SentenceType.WhQuestion or SentenceType.YesNoQuestion)
                .ToList();

            if (questions.Count == 0)
            {
                diagnostics.Add("Only questions are supported by Ask.");
                return CreateUnsupportedQuestion(diagnostics);
            }

            if (questions.Count > 1)
                diagnostics.Add("Additional question sentences were ignored.");

            Sentence sentence = questions[0];
            List<Token> tokens = sentence.Tokens.ToList();
            int end = TrimTrailingPunctuation(tokens);

            if (end < 0)
            {
                diagnostics.Add("The question does not contain any analyzable words.");
                return CreateUnsupportedQuestion(diagnostics, sentence.Index);
            }

            int whIndex = -1;
            string? leadingPreposition = null;

            if (sentence.Type == SentenceType.WhQuestion)
            {
                if (end >= 1
                    && tokens[0].PartOfSpeech == PartOfSpeech.Preposition
                    && tokens[1].PartOfSpeech == PartOfSpeech.WhWord)
                {
                    leadingPreposition = LemmaOrNormalized(tokens[0]);
                    whIndex = 1;
                }
                else
                {
                    int searchEnd = Math.Min(end, 2);

                    for (int index = 0; index <= searchEnd; index++)
                    {
                        if (tokens[index].PartOfSpeech == PartOfSpeech.WhWord)
                        {
                            whIndex = index;
                            break;
                        }
                    }
                }

                if (whIndex < 0)
                {
                    diagnostics.Add("The question word could not be identified.");
                    return CreateUnsupportedQuestion(diagnostics, sentence.Index);
                }
            }

            WhKind wh = whIndex >= 0
                ? MapWhKind(tokens[whIndex].Normalized)
                : WhKind.None;
            bool quantityQuestion = wh == WhKind.How
                && whIndex + 1 <= end
                && tokens[whIndex + 1].Normalized is "many" or "much";
            int whEnd = ExtendWhSpan(tokens, whIndex, end);

            bool negated = tokens
                .Take(end + 1)
                .Any(token => RelationExtractor.IsNegator(token.Normalized));
            string? modal = tokens
                .Take(end + 1)
                .Where(token => token.PartOfSpeech == PartOfSpeech.Auxiliary
                    && RelationExtractor.IsModal(token.Lemma))
                .Select(token => token.Lemma)
                .FirstOrDefault();

            int auxIndex = -1;
            int auxSearchStart = whIndex >= 0 ? whIndex + 1 : 0;

            for (int index = auxSearchStart; index <= end; index++)
            {
                if (tokens[index].PartOfSpeech == PartOfSpeech.Auxiliary)
                {
                    auxIndex = index;
                    break;
                }
            }

            int verbSearchStart = auxIndex >= 0
                ? auxIndex + 1
                : (whIndex >= 0 ? whIndex + 1 : 0);
            Token? mainVerb = FindMainVerbToken(
                tokens,
                parsed,
                sentence,
                verbSearchStart,
                end,
                auxIndex);
            Token? predicateToken = mainVerb ?? FindAuxiliaryPredicate(tokens, auxIndex, end);

            if (predicateToken is null)
            {
                diagnostics.Add("The question predicate could not be identified.");
                return CreateUnsupportedQuestion(
                    diagnostics,
                    sentence.Index,
                    wh,
                    WhSurface(tokens, whIndex, whEnd),
                    whIndex,
                    whEnd);
            }

            bool copula = mainVerb is null && predicateToken.Lemma == "be";
            bool possession = mainVerb is null && predicateToken.Lemma == "have";
            string predicateLemma = predicateToken.Lemma;
            bool hasBeAuxiliary = tokens
                .Take(end + 1)
                .Any(token => token.PartOfSpeech == PartOfSpeech.Auxiliary
                    && token.Lemma == "be");
            Tense tense = RelationExtractor.DetermineTense(
                tokens.Take(end + 1).ToList(),
                mainVerb,
                modal);

            List<Chunk> prepositionalPhrases = GetPrepositionalPhrases(sentence);
            int relationAnchorIndex = mainVerb?.TokenIndex ?? predicateToken.TokenIndex;
            List<QualifierPhrase> qualifiers = FindQualifiersAfter(
                parsed,
                sentence,
                relationAnchorIndex,
                end,
                prepositionalPhrases);
            QualifierPhrase? byQualifier = qualifiers.FirstOrDefault(
                qualifier => qualifier.Preposition == "by");
            string? strandedPreposition = FindStrandedPreposition(
                tokens,
                relationAnchorIndex,
                end);

            bool passive = hasBeAuxiliary
                && mainVerb is not null
                && mainVerb.Lemma != "be"
                && (byQualifier is not null
                    || string.Equals(leadingPreposition, "by", StringComparison.OrdinalIgnoreCase));

            FrameArgument? invertedSubject = FindInvertedSubject(
                parsed,
                sentence,
                tokens,
                auxIndex,
                mainVerb,
                predicateToken,
                end,
                copula);
            FrameArgument? directObject = FindDirectObject(
                parsed,
                sentence,
                tokens,
                relationAnchorIndex,
                end,
                prepositionalPhrases);
            FrameArgument? firstQualifier = qualifiers.Count > 0
                ? CreateArgument(
                    sentence,
                    parsed,
                    qualifiers[0].StartToken,
                    qualifiers[0].EndToken,
                    FrameRole.Qualifier,
                    qualifiers[0].Preposition)
                : null;

            QuestionType questionType = DetermineQuestionType(
                sentence.Type,
                wh,
                quantityQuestion,
                leadingPreposition,
                passive,
                byQualifier,
                invertedSubject,
                auxIndex,
                whIndex,
                whEnd,
                tokens,
                end);

            AnswerKind expectedAnswer = DetermineAnswerKind(questionType, wh);
            List<NodeTypes> expectedTypes = DetermineExpectedTypes(
                questionType,
                wh,
                tokens,
                whIndex,
                whEnd);

            FrameArgument? whArgument = whIndex >= 0
                ? CreateArgument(
                    sentence,
                    parsed,
                    whIndex,
                    whEnd,
                    WhQuestionRole(questionType),
                    GetWhPreposition(questionType, leadingPreposition, strandedPreposition))
                : null;

            FrameArgument? subject = null;
            FrameArgument? objectArgument = null;
            FrameArgument? qualifier = firstQualifier;
            FrameArgument? anchorArgument = null;
            FrameRole anchorRole = FrameRole.Subject;

            if (questionType == QuestionType.YesNo)
            {
                subject = invertedSubject
                    ?? FindFirstArgumentBefore(parsed, sentence, tokens, relationAnchorIndex, true);
                objectArgument = copula
                    ? FindFirstMentionAfter(
                        parsed,
                        sentence,
                        subject?.EndToken ?? predicateToken.TokenIndex,
                        end,
                        prepositionalPhrases,
                        false)
                        ?? FindTokenArgumentSpan(
                            parsed,
                            sentence,
                            tokens,
                            (subject?.EndToken ?? predicateToken.TokenIndex) + 1,
                            end,
                            FrameRole.Object,
                            prepositionalPhrases)
                    : directObject ?? firstQualifier;
                anchorArgument = subject;
                anchorRole = FrameRole.Subject;
            }
            else if (questionType == QuestionType.WhSubject)
            {
                subject = whArgument;

                if (passive && byQualifier is not null)
                {
                    anchorArgument = CreateArgument(
                        sentence,
                        parsed,
                        byQualifier.StartToken,
                        byQualifier.EndToken,
                        FrameRole.Qualifier,
                        byQualifier.Preposition);
                    anchorRole = FrameRole.Qualifier;
                    qualifier = anchorArgument;
                    subject = anchorArgument;
                }
                else if (passive && string.Equals(leadingPreposition, "by", StringComparison.OrdinalIgnoreCase))
                {
                    anchorArgument = invertedSubject;
                    anchorRole = FrameRole.Object;
                    objectArgument = anchorArgument;
                }
                else if (directObject is not null)
                {
                    anchorArgument = directObject;
                    anchorRole = FrameRole.Object;
                    objectArgument = anchorArgument;
                }
                else if (firstQualifier is not null)
                {
                    anchorArgument = firstQualifier;
                    anchorRole = FrameRole.Qualifier;
                    qualifier = firstQualifier;

                    if (passive)
                        subject = firstQualifier;
                }
                else if (copula && invertedSubject is not null)
                {
                    anchorArgument = invertedSubject;
                    anchorRole = FrameRole.Subject;
                }
            }
            else
            {
                if (passive && byQualifier is not null)
                {
                    anchorArgument = CreateArgument(
                        sentence,
                        parsed,
                        byQualifier.StartToken,
                        byQualifier.EndToken,
                        FrameRole.Subject,
                        byQualifier.Preposition);
                    anchorRole = FrameRole.Subject;
                    subject = anchorArgument;
                }
                else
                {
                    subject = invertedSubject
                        ?? FindFirstArgumentBefore(parsed, sentence, tokens, relationAnchorIndex, false);
                    anchorArgument = questionType is QuestionType.WhQualifier or QuestionType.WhQuantity
                        ? subject ?? directObject
                        : subject;

                    if (anchorArgument is not null && directObject is not null
                        && ReferenceEquals(anchorArgument, directObject))
                    {
                        anchorRole = FrameRole.Object;
                    }
                    else
                    {
                        anchorRole = FrameRole.Subject;
                    }
                }

                objectArgument = questionType is QuestionType.WhQualifier or QuestionType.WhQuantity
                    ? qualifier ?? whArgument
                    : whArgument;
            }

            List<string> explicitPrepositions = new List<string>();

            if (leadingPreposition is not null
                && !string.Equals(leadingPreposition, "by", StringComparison.OrdinalIgnoreCase))
            {
                explicitPrepositions.Add(leadingPreposition);
            }

            if (questionType is QuestionType.WhSubject or QuestionType.YesNo
                && anchorArgument is not null
                && !string.IsNullOrWhiteSpace(anchorArgument.Preposition))
            {
                explicitPrepositions.Add(anchorArgument.Preposition);
            }
            else if (objectArgument is not null
                && !string.IsNullOrWhiteSpace(objectArgument.Preposition)
                && questionType is QuestionType.YesNo)
            {
                explicitPrepositions.Add(objectArgument.Preposition);
            }

            foreach (QualifierPhrase qualifierPhrase in qualifiers)
            {
                if (passive && qualifierPhrase.Preposition == "by")
                    continue;

                if (!explicitPrepositions.Contains(
                    qualifierPhrase.Preposition,
                    StringComparer.OrdinalIgnoreCase))
                {
                    explicitPrepositions.Add(qualifierPhrase.Preposition);
                }
            }

            if (strandedPreposition is not null
                && !explicitPrepositions.Contains(strandedPreposition, StringComparer.OrdinalIgnoreCase))
            {
                explicitPrepositions.Add(strandedPreposition);
            }

            List<RelationCandidate> relationCandidates = BuildRelationCandidates(
                predicateLemma,
                copula,
                possession,
                passive,
                questionType,
                leadingPreposition,
                explicitPrepositions);

            bool supported = true;

            if (modal is not null)
            {
                diagnostics.Add("Modal questions are not supported in Phase 5.");
                supported = false;
            }

            if (negated)
            {
                diagnostics.Add("Negated questions are not supported in Phase 5.");
                supported = false;
            }

            if (wh is WhKind.Why || (wh is WhKind.How && !quantityQuestion))
            {
                diagnostics.Add("Why and manner questions are not supported in Phase 5.");
                supported = false;
            }

            if (wh is WhKind.Whose)
            {
                diagnostics.Add("Possessive questions are not supported in Phase 5.");
                supported = false;
            }

            if (copula
                && wh is WhKind.Where or WhKind.When
                && qualifiers.Count == 0
                && leadingPreposition is null
                && strandedPreposition is null)
            {
                diagnostics.Add(
                    "Locative copula questions require Phase 4 ingestion support that is not available.");
                supported = false;
            }

            if (anchorArgument is null)
            {
                diagnostics.Add("The question anchor could not be recovered.");
                return CreateUnsupportedQuestion(
                    diagnostics,
                    sentence.Index,
                    wh,
                    WhSurface(tokens, whIndex, whEnd),
                    whIndex,
                    whEnd,
                    questionType,
                    expectedAnswer,
                    tense,
                    passive ? Voice.Passive : Voice.Active);
            }

            EntityResolution anchor = resolver.Resolve(anchorArgument);
            diagnostics.AddRange(anchor.Diagnostics);

            if (anchor.IsPronoun)
            {
                supported = false;

                if (anchor.Diagnostics.Count == 0)
                    diagnostics.Add("Pronoun questions require conversation context, but no antecedent is available.");
            }

            if (anchor.IsAmbiguous)
            {
                diagnostics.Add("The question anchor matches multiple nodes.");
                supported = false;
            }

            if (relationCandidates.Count == 0)
            {
                diagnostics.Add("No relation candidate could be registered.");
                supported = false;
            }

            List<QuestionVariant> variants = new List<QuestionVariant>();
            RelayLink? primary = null;

            if (anchorArgument is not null && !anchor.IsPronoun && !anchor.IsAmbiguous)
            {
                Guid anchorNodeID = anchor.ResolvedNodeID ?? resolver.CreateNodeId();
                Chain_Node anchorNode = CreateNode(anchorNodeID, anchor, options);

                if (questionType == QuestionType.YesNo)
                {
                    FrameArgument? yesNoObject = objectArgument;

                    if (subject is not null && yesNoObject is not null && relationCandidates.Count > 0)
                    {
                        EntityResolution target = resolver.Resolve(yesNoObject);
                        diagnostics.AddRange(target.Diagnostics);

                        if (target.IsPronoun)
                        {
                            supported = false;

                            if (target.Diagnostics.Count == 0)
                                diagnostics.Add("Pronoun questions require conversation context, but no antecedent is available.");
                        }

                        if (target.IsAmbiguous)
                        {
                            supported = false;
                            diagnostics.Add("The yes/no object matches multiple nodes.");
                        }

                        if (!target.IsPronoun && !target.IsAmbiguous)
                        {
                            Guid targetID = target.ResolvedNodeID ?? resolver.CreateNodeId();

                            if (targetID == anchorNodeID)
                            {
                                diagnostics.Add(
                                    $"The yes/no object '{target.Label}' resolves to the question anchor.");
                            }
                            else
                            {
                                Chain_Node subjectNode = CreateNode(anchorNodeID, anchor, options);
                                Chain_Node targetNode = CreateNode(targetID, target, options);
                                RelationLinkType relation = RelationRegistry.GetOrRegister(
                                    relationCandidates[0].Name);
                                Link link = new Link(
                                    anchorNodeID,
                                    targetID,
                                    relation.Forward.Id,
                                    relation.Inverse.Id,
                                    relationCandidates[0].LinkClass);
                                subjectNode.Set_LinkNext(link);
                                targetNode.Set_LinkPrevious(link);

                                LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();
                                chain.AddLast(subjectNode);
                                chain.AddLast(targetNode);
                                variants.Add(new QuestionVariant(
                                    "yesno+" + relation.Name,
                                    chain,
                                    target.NodeType,
                                    relation.Name,
                                    relation.Forward.Id,
                                    relation.Inverse.Id,
                                    relationCandidates[0].LinkClass,
                                    FrameRole.Object,
                                    false,
                                    true));
                            }
                        }
                    }

                    if (variants.Count == 0 && supported)
                    {
                        diagnostics.Add("The question does not contain a complete fact.");
                        supported = false;
                    }
                }
                else
                {
                    primary = relationCandidates.Count > 0
                        ? new RelayLink(
                            relationCandidates[0].Name,
                            relationCandidates[0].LinkClass)
                        : null;

                    for (int relationIndex = 0;
                        relationIndex < relationCandidates.Count
                            && variants.Count < Math.Max(1, options.MaxQuestionVariants);
                        relationIndex++)
                    {
                        RelationCandidate candidate = relationCandidates[relationIndex];
                        RelationLinkType relation = RelationRegistry.GetOrRegister(candidate.Name);

                        foreach (NodeTypes expectedType in expectedTypes)
                        {
                            if (variants.Count >= Math.Max(1, options.MaxQuestionVariants))
                                break;

                            Guid whNodeID = resolver.CreateNodeId();

                            if (whNodeID == anchorNodeID)
                                whNodeID = resolver.CreateNodeId();

                            Chain_Node whNode = new Chain_Node(
                                whNodeID,
                                expectedType,
                                options.TemporaryNodeTimeToLive);
                            Chain_Node variantAnchor = CreateNode(anchorNodeID, anchor, options);

                            Link link = questionType is QuestionType.WhSubject
                                ? new Link(
                                    whNodeID,
                                    anchorNodeID,
                                    relation.Forward.Id,
                                    relation.Inverse.Id,
                                    candidate.LinkClass)
                                : new Link(
                                    whNodeID,
                                    anchorNodeID,
                                    relation.Inverse.Id,
                                    relation.Forward.Id,
                                    candidate.LinkClass);

                            whNode.Set_LinkNext(link);
                            variantAnchor.Set_LinkPrevious(link);

                            LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();
                            chain.AddLast(whNode);
                            chain.AddLast(variantAnchor);

                            variants.Add(new QuestionVariant(
                                BuildVariantName(wh, expectedType, relation.Name),
                                chain,
                                expectedType,
                                relation.Name,
                                link.ForwardLink.Id,
                                link.InverseLink.Id,
                                candidate.LinkClass,
                                AnswerRoleFor(questionType),
                                false,
                                false));
                        }
                    }

                    if (options.EnableAnchorFallback
                        && questionType is QuestionType.WhQualifier or QuestionType.WhQuantity
                        && variants.Count < Math.Max(1, options.MaxQuestionVariants)
                        && relationCandidates.Count > 0)
                    {
                        RelationCandidate fallbackCandidate = relationCandidates[0];
                        RelationLinkType fallbackRelation =
                            RelationRegistry.GetOrRegister(fallbackCandidate.Name);
                        LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();
                        chain.AddLast(CreateNode(anchorNodeID, anchor, options));

                        variants.Add(new QuestionVariant(
                            "anchor+" + fallbackRelation.Name,
                            chain,
                            expectedTypes[0],
                            fallbackRelation.Name,
                            fallbackRelation.Forward.Id,
                            fallbackRelation.Inverse.Id,
                            fallbackCandidate.LinkClass,
                            AnswerRoleFor(questionType),
                            true,
                            false));
                    }
                }

                if (anchorNode.ID != Guid.Empty
                    && anchorNode.Label is null
                    && string.IsNullOrEmpty(anchorArgument?.SurfaceText))
                    diagnostics.Add("The question anchor label is empty.");
            }

            if (variants.Count == 0 && supported)
            {
                diagnostics.Add("No valid query variant could be built.");
                supported = false;
            }

            return new QuestionAnalysis(
                questionType,
                wh,
                expectedAnswer,
                WhSurface(tokens, whIndex, whEnd),
                whArgument,
                subject,
                objectArgument,
                qualifier,
                predicateLemma,
                primary?.Name ?? relationCandidates.FirstOrDefault()?.Name,
                primary?.LinkClass ?? relationCandidates.FirstOrDefault()?.LinkClass ?? LinkClass.basic,
                tense,
                passive ? Voice.Passive : Voice.Active,
                negated,
                passive,
                supported,
                anchor.IsAmbiguous,
                anchor.ResolvedNodeID,
                anchorRole,
                variants,
                diagnostics,
                sentence.Index,
                whIndex,
                whEnd);
        }

        private static QuestionType DetermineQuestionType(
            SentenceType sentenceType,
            WhKind wh,
            bool quantityQuestion,
            string? leadingPreposition,
            bool passive,
            QualifierPhrase? byQualifier,
            FrameArgument? invertedSubject,
            int auxIndex,
            int whIndex,
            int whEnd,
            IReadOnlyList<Token> tokens,
            int end)
        {
            if (sentenceType == SentenceType.YesNoQuestion)
                return QuestionType.YesNo;

            if (quantityQuestion)
                return QuestionType.WhQuantity;

            if (wh is WhKind.Where or WhKind.When)
                return QuestionType.WhQualifier;

            if (wh is WhKind.Who or WhKind.Whom or WhKind.What or WhKind.Which)
            {
                if (passive && string.Equals(leadingPreposition, "by", StringComparison.OrdinalIgnoreCase))
                    return QuestionType.WhSubject;

                if (passive && byQualifier is not null)
                    return QuestionType.WhObject;

                bool inverted = auxIndex > whIndex && invertedSubject is not null;
                return inverted ? QuestionType.WhObject : QuestionType.WhSubject;
            }

            if (wh is WhKind.How && whEnd > whIndex)
            {
                string next = tokens[Math.Min(whIndex + 1, end)].Normalized;
                return next is "many" or "much"
                    ? QuestionType.WhQuantity
                    : QuestionType.Unknown;
            }

            return QuestionType.Unknown;
        }

        private static AnswerKind DetermineAnswerKind(QuestionType questionType, WhKind wh)
        {
            if (questionType == QuestionType.YesNo)
                return AnswerKind.Boolean;

            return questionType switch
            {
                QuestionType.WhSubject when wh is WhKind.Who or WhKind.Whom or WhKind.Whose => AnswerKind.Entity,
                QuestionType.WhObject when wh is WhKind.Who or WhKind.Whom or WhKind.Whose => AnswerKind.Entity,
                QuestionType.WhQualifier when wh is WhKind.Where => AnswerKind.Location,
                QuestionType.WhQualifier when wh is WhKind.When => AnswerKind.Time,
                QuestionType.WhQuantity => AnswerKind.Quantity,
                QuestionType.WhSubject or QuestionType.WhObject => AnswerKind.Description,
                _ => AnswerKind.None
            };
        }

        private static List<NodeTypes> DetermineExpectedTypes(
            QuestionType questionType,
            WhKind wh,
            IReadOnlyList<Token> tokens,
            int whIndex,
            int whEnd)
        {
            if (questionType == QuestionType.WhQualifier && wh == WhKind.Where)
                return new List<NodeTypes>
                {
                    NodeTypes.Locations,
                    NodeTypes.Entities,
                    NodeTypes.RawData,
                    NodeTypes.Basics
                };

            if (questionType == QuestionType.WhQualifier && wh == WhKind.When)
                return new List<NodeTypes> { NodeTypes.RawData, NodeTypes.Events };

            if (questionType == QuestionType.WhQuantity)
                return new List<NodeTypes> { NodeTypes.RawData };

            if (wh is WhKind.Who or WhKind.Whom or WhKind.Whose)
                return new List<NodeTypes> { NodeTypes.Entities };

            if (wh is WhKind.Which && whEnd > whIndex)
            {
                string noun = tokens[Math.Min(whIndex + 1, whEnd)].Normalized;

                if (noun is "city" or "country" or "place")
                    return new List<NodeTypes> { NodeTypes.Locations };

                if (noun is "day" or "date" or "time")
                    return new List<NodeTypes> { NodeTypes.RawData };

                if (noun is "person" or "company")
                    return new List<NodeTypes> { NodeTypes.Entities };
            }

            List<NodeTypes> types = new List<NodeTypes>();

            foreach (NodeTypes type in broadWhatTypes)
            {
                if (type != NodeTypes.Empties && !types.Contains(type))
                    types.Add(type);
            }

            return types;
        }

        private static FrameRole WhQuestionRole(QuestionType questionType)
        {
            return questionType switch
            {
                QuestionType.WhSubject => FrameRole.Subject,
                QuestionType.WhQualifier => FrameRole.Qualifier,
                _ => FrameRole.Object
            };
        }

        private static FrameRole AnswerRoleFor(QuestionType questionType)
        {
            return questionType switch
            {
                QuestionType.WhSubject => FrameRole.Subject,
                QuestionType.WhQualifier => FrameRole.Qualifier,
                QuestionType.WhQuantity => FrameRole.Qualifier,
                _ => FrameRole.Object
            };
        }

        private static string? GetWhPreposition(
            QuestionType questionType,
            string? leadingPreposition,
            string? strandedPreposition)
        {
            if (questionType == QuestionType.WhQualifier)
                return leadingPreposition ?? strandedPreposition;

            return leadingPreposition;
        }

        private static string BuildVariantName(WhKind wh, NodeTypes type, string relationName)
        {
            string whName = wh == WhKind.None ? "yesno" : wh.ToString().ToLowerInvariant();
            return whName + "+" + type.Name.Replace(" ", string.Empty) + "+" + relationName;
        }

        private static WhKind MapWhKind(string normalized)
        {
            return whKinds.TryGetValue(normalized, out WhKind kind)
                ? kind
                : WhKind.None;
        }

        private static int ExtendWhSpan(
            IReadOnlyList<Token> tokens,
            int whIndex,
            int end)
        {
            if (whIndex < 0 || whIndex >= end)
                return whIndex;

            string wh = tokens[whIndex].Normalized;

            if (wh == "how"
                && whIndex + 1 <= end
                && tokens[whIndex + 1].Normalized is "many" or "much")
            {
                return whIndex + 1;
            }

            if (wh is "which" or "what"
                && whIndex + 1 <= end
                && tokens[whIndex + 1].PartOfSpeech is PartOfSpeech.Noun
                    or PartOfSpeech.ProperNoun
                    or PartOfSpeech.Number)
            {
                return whIndex + 1;
            }

            return whIndex;
        }

        private static string? WhSurface(IReadOnlyList<Token> tokens, int start, int end)
        {
            if (start < 0 || start > end || start >= tokens.Count)
                return null;

            int safeEnd = Math.Min(end, tokens.Count - 1);
            return string.Join(" ", tokens.Skip(start).Take(safeEnd - start + 1).Select(token => token.Text));
        }

        private static int TrimTrailingPunctuation(IReadOnlyList<Token> tokens)
        {
            int end = tokens.Count - 1;

            while (end >= 0 && tokens[end].PartOfSpeech == PartOfSpeech.Punctuation)
                end--;

            return end;
        }

        private static List<Chunk> GetPrepositionalPhrases(Sentence sentence)
        {
            return sentence.Clauses
                .SelectMany(clause => clause.Chunks)
                .Where(chunk => chunk.Kind == ChunkKind.PrepositionalPhrase)
                .GroupBy(chunk => (chunk.StartToken, chunk.EndToken))
                .Select(group => group.First())
                .OrderBy(chunk => chunk.StartToken)
                .ToList();
        }

        private static List<QualifierPhrase> FindQualifiersAfter(
            ParsedText parsed,
            Sentence sentence,
            int afterToken,
            int end,
            IReadOnlyList<Chunk> prepositionalPhrases)
        {
            List<QualifierPhrase> qualifiers = new List<QualifierPhrase>();

            foreach (Chunk phrase in prepositionalPhrases)
            {
                if (phrase.StartToken <= afterToken || phrase.StartToken > end)
                    continue;

                Token prepositionToken = sentence.Tokens[phrase.StartToken];
                string preposition = LemmaOrNormalized(prepositionToken);
                Mention? noun = parsed.Mentions
                    .Where(mention => mention.SentenceIndex == sentence.Index
                        && mention.StartToken > phrase.StartToken
                        && mention.EndToken <= phrase.EndToken)
                    .OrderBy(mention => mention.StartToken)
                    .ThenByDescending(mention => mention.EndToken)
                    .FirstOrDefault();

                if (noun is not null)
                {
                    qualifiers.Add(new QualifierPhrase(
                        preposition,
                        noun.StartToken,
                        noun.EndToken));
                }
                else if (phrase.EndToken > phrase.StartToken)
                {
                    qualifiers.Add(new QualifierPhrase(
                        preposition,
                        phrase.StartToken + 1,
                        phrase.EndToken));
                }
            }

            return qualifiers;
        }

        private static string? FindStrandedPreposition(
            IReadOnlyList<Token> tokens,
            int afterToken,
            int end)
        {
            if (end <= afterToken)
                return null;

            for (int index = end; index > afterToken; index--)
            {
                if (tokens[index].PartOfSpeech == PartOfSpeech.Preposition)
                    return LemmaOrNormalized(tokens[index]);
            }

            return null;
        }

        private static FrameArgument? FindInvertedSubject(
            ParsedText parsed,
            Sentence sentence,
            IReadOnlyList<Token> tokens,
            int auxIndex,
            Token? mainVerb,
            Token predicateToken,
            int end,
            bool copula)
        {
            if (auxIndex < 0)
                return null;

            if (copula)
            {
                return FindCopulaSubject(
                    parsed,
                    sentence,
                    tokens,
                    predicateToken.TokenIndex + 1,
                    end)
                    ?? FindFirstMentionAfter(
                        parsed,
                        sentence,
                        predicateToken.TokenIndex,
                        end,
                        GetPrepositionalPhrases(sentence),
                        false,
                        true);
            }

            int predicateIndex = mainVerb?.TokenIndex ?? predicateToken.TokenIndex;

            if (predicateIndex <= auxIndex + 1)
                return null;

            FrameArgument? subject = FindFirstMentionInRange(
                parsed,
                sentence,
                auxIndex + 1,
                predicateIndex - 1,
                FrameRole.Subject,
                GetPrepositionalPhrases(sentence),
                false);

            return subject ?? FindTokenArgumentSpan(
                parsed,
                sentence,
                tokens,
                auxIndex + 1,
                predicateIndex - 1,
                FrameRole.Subject);
        }

        private static FrameArgument? FindCopulaSubject(
            ParsedText parsed,
            Sentence sentence,
            IReadOnlyList<Token> tokens,
            int start,
            int end)
        {
            if (start > end)
                return null;

            Token first = tokens[start];

            if (first.PartOfSpeech == PartOfSpeech.Pronoun)
                return CreateArgument(sentence, parsed, start, start, FrameRole.Subject, null);

            if (first.PartOfSpeech is not (
                PartOfSpeech.Determiner
                or PartOfSpeech.Adjective
                or PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Number))
            {
                return null;
            }

            int spanEnd = start;
            bool hasNominal = first.PartOfSpeech is PartOfSpeech.Noun
                or PartOfSpeech.ProperNoun
                or PartOfSpeech.Number;

            for (int index = start + 1; index <= end; index++)
            {
                PartOfSpeech partOfSpeech = tokens[index].PartOfSpeech;

                if (partOfSpeech == PartOfSpeech.Determiner)
                {
                    if (hasNominal)
                        break;

                    spanEnd = index;
                    continue;
                }

                if (partOfSpeech == PartOfSpeech.Adjective)
                {
                    spanEnd = index;
                    continue;
                }

                if (partOfSpeech is PartOfSpeech.Noun
                    or PartOfSpeech.ProperNoun
                    or PartOfSpeech.Number)
                {
                    spanEnd = index;
                    hasNominal = true;
                    continue;
                }

                break;
            }

            if (!hasNominal)
                return null;

            return CreateArgument(
                sentence,
                parsed,
                start,
                spanEnd,
                FrameRole.Subject,
                null);
        }

        private static FrameArgument? FindDirectObject(
            ParsedText parsed,
            Sentence sentence,
            IReadOnlyList<Token> tokens,
            int afterToken,
            int end,
            IReadOnlyList<Chunk> prepositionalPhrases)
        {
            return FindFirstMentionInRange(
                parsed,
                sentence,
                afterToken + 1,
                end,
                FrameRole.Object,
                prepositionalPhrases,
                true)
                ?? FindTokenArgumentSpan(
                    parsed,
                    sentence,
                    tokens,
                    afterToken + 1,
                    end,
                    FrameRole.Object,
                    prepositionalPhrases);
        }

        private static FrameArgument? FindFirstArgumentBefore(
            ParsedText parsed,
            Sentence sentence,
            IReadOnlyList<Token> tokens,
            int beforeToken,
            bool allowPronoun)
        {
            FrameArgument? mention = FindFirstMentionInRange(
                parsed,
                sentence,
                0,
                beforeToken - 1,
                FrameRole.Subject,
                GetPrepositionalPhrases(sentence),
                false);

            if (mention is not null)
                return mention;

            if (!allowPronoun)
                return null;

            return FindTokenArgumentSpan(
                parsed,
                sentence,
                tokens,
                0,
                beforeToken - 1,
                FrameRole.Subject);
        }

        private static FrameArgument? FindFirstMentionAfter(
            ParsedText parsed,
            Sentence sentence,
            int afterToken,
            int end,
            IReadOnlyList<Chunk> prepositionalPhrases,
            bool excludeInsidePreposition,
            bool preferShortest = false)
        {
            return FindFirstMentionInRange(
                parsed,
                sentence,
                afterToken + 1,
                end,
                FrameRole.Object,
                prepositionalPhrases,
                excludeInsidePreposition,
                preferShortest);
        }

        private static FrameArgument? FindFirstMentionInRange(
            ParsedText parsed,
            Sentence sentence,
            int start,
            int end,
            FrameRole role,
            IReadOnlyList<Chunk> prepositionalPhrases,
            bool excludeInsidePreposition,
            bool preferShortest = false)
        {
            if (start > end)
                return null;

            Mention? mention = parsed.Mentions
                .Where(candidate => candidate.SentenceIndex == sentence.Index
                    && candidate.StartToken >= start
                    && candidate.EndToken <= end)
                .Where(candidate => !excludeInsidePreposition
                    || !IsInsidePrepositionalPhrase(candidate, prepositionalPhrases))
                .OrderBy(candidate => candidate.StartToken)
                .ThenBy(candidate => preferShortest ? candidate.EndToken : -candidate.EndToken)
                .FirstOrDefault();

            if (mention is null)
                return null;

            return CreateArgument(
                sentence,
                parsed,
                mention.StartToken,
                mention.EndToken,
                role,
                null);
        }

        private static FrameArgument? FindTokenArgumentSpan(
            ParsedText parsed,
            Sentence sentence,
            IReadOnlyList<Token> tokens,
            int start,
            int end,
            FrameRole role,
            IReadOnlyList<Chunk>? prepositionalPhrases = null)
        {
            if (start > end)
                return null;

            for (int index = start; index <= end; index++)
            {
                Token token = tokens[index];

                if (token.PartOfSpeech == PartOfSpeech.Pronoun)
                    return CreateArgument(sentence, parsed, index, index, role, null);

                if (token.PartOfSpeech is not (
                    PartOfSpeech.Determiner
                    or PartOfSpeech.Adjective
                    or PartOfSpeech.Noun
                    or PartOfSpeech.ProperNoun
                    or PartOfSpeech.Number))
                {
                    continue;
                }

                int spanEnd = index;

                while (spanEnd + 1 <= end
                    && tokens[spanEnd + 1].PartOfSpeech is PartOfSpeech.Determiner
                        or PartOfSpeech.Adjective
                        or PartOfSpeech.Noun
                        or PartOfSpeech.ProperNoun
                        or PartOfSpeech.Number)
                {
                    spanEnd++;
                }

                if (prepositionalPhrases is not null
                    && IsInsidePrepositionalPhrase(index, spanEnd, prepositionalPhrases))
                {
                    index = spanEnd;
                    continue;
                }

                return CreateArgument(sentence, parsed, index, spanEnd, role, null);
            }

            return null;
        }

        private static FrameArgument? CreateArgument(
            Sentence sentence,
            ParsedText parsed,
            int start,
            int end,
            FrameRole role,
            string? preposition)
        {
            return RelationExtractor.CreateArgument(
                sentence,
                parsed,
                start,
                end,
                role,
                preposition);
        }

        private static bool IsInsidePrepositionalPhrase(
            Mention mention,
            IReadOnlyList<Chunk> prepositionalPhrases)
        {
            return IsInsidePrepositionalPhrase(
                mention.StartToken,
                mention.EndToken,
                prepositionalPhrases);
        }

        private static bool IsInsidePrepositionalPhrase(
            int start,
            int end,
            IReadOnlyList<Chunk> prepositionalPhrases)
        {
            return prepositionalPhrases.Any(phrase =>
                phrase.StartToken <= start && end <= phrase.EndToken);
        }

        private static Token? FindMainVerbToken(
            IReadOnlyList<Token> tokens,
            ParsedText parsed,
            Sentence sentence,
            int start,
            int end,
            int auxIndex)
        {
            if (start > end)
                return null;

            List<Token> range = tokens
                .Skip(start)
                .Take(end - start + 1)
                .ToList();
            Token? mainVerb = RelationExtractor.FindMainVerb(range);

            if (mainVerb is not null)
                return mainVerb;

            for (int index = start; index <= end; index++)
            {
                Token token = tokens[index];

                if (token.PartOfSpeech == PartOfSpeech.Auxiliary)
                    continue;

                if (Lexicon.IsKnownVerb(token.Normalized)
                    || Lexicon.IsIrregularVerbForm(token.Normalized)
                    || IsThirdPersonForm(token.Normalized))
                {
                    return token;
                }
            }

            if (auxIndex < 0 || tokens[auxIndex].Lemma != "do")
                return null;

            Mention? subject = parsed.Mentions
                .Where(mention => mention.SentenceIndex == sentence.Index
                    && mention.StartToken > auxIndex
                    && mention.EndToken <= end)
                .OrderBy(mention => mention.StartToken)
                .FirstOrDefault();
            int scanStart = subject is null ? auxIndex + 1 : subject.EndToken + 1;

            for (int index = scanStart; index <= end; index++)
            {
                Token token = tokens[index];

                if (token.PartOfSpeech is PartOfSpeech.Auxiliary
                    or PartOfSpeech.Determiner
                    or PartOfSpeech.Preposition
                    or PartOfSpeech.Conjunction
                    or PartOfSpeech.Punctuation
                    or PartOfSpeech.Pronoun
                    or PartOfSpeech.WhWord
                    or PartOfSpeech.Number
                    or PartOfSpeech.Adjective
                    or PartOfSpeech.Adverb)
                {
                    continue;
                }

                return token;
            }

            return null;
        }

        private static bool IsThirdPersonForm(string normalized)
        {
            if (!normalized.EndsWith("s", StringComparison.Ordinal)
                || normalized.Length <= 2
                || normalized.EndsWith("ss", StringComparison.Ordinal)
                || Lexicon.IsAuxiliary(normalized))
            {
                return false;
            }

            string stem = normalized[..^1];

            if (Lexicon.IsKnownVerb(stem))
                return true;

            return stem.EndsWith("e", StringComparison.Ordinal)
                && stem.Length > 1
                && Lexicon.IsKnownVerb(stem[..^1]);
        }

        private static Token? FindAuxiliaryPredicate(
            IReadOnlyList<Token> tokens,
            int auxIndex,
            int end)
        {
            if (auxIndex < 0)
                return null;

            Token? have = null;
            Token? be = null;

            for (int index = auxIndex; index <= end; index++)
            {
                if (tokens[index].PartOfSpeech != PartOfSpeech.Auxiliary)
                    continue;

                if (tokens[index].Lemma == "have")
                    have = tokens[index];
                else if (tokens[index].Lemma == "be")
                    be = tokens[index];
            }

            return have ?? be;
        }

        private static List<RelationCandidate> BuildRelationCandidates(
            string predicateLemma,
            bool copula,
            bool possession,
            bool passive,
            QuestionType questionType,
            string? leadingPreposition,
            IReadOnlyList<string> explicitPrepositions)
        {
            List<RelationCandidate> candidates = new List<RelationCandidate>();

            if (copula)
            {
                AddRelation(candidates, "is", LinkClass.identities);
                return candidates;
            }

            if (possession)
            {
                AddRelation(candidates, "has", LinkClass.actions);
                return candidates;
            }

            if (passive)
            {
                AddRelation(candidates, RelationRegistry.Normalize(predicateLemma), LinkClass.actions);
                return candidates;
            }

            foreach (string preposition in explicitPrepositions)
            {
                AddRelation(
                    candidates,
                    RelationRegistry.Normalize(predicateLemma, preposition),
                    LinkClass.actions);
            }

            AddRelation(candidates, RelationRegistry.Normalize(predicateLemma), LinkClass.actions);

            bool qualifierQuestion = questionType is QuestionType.WhQualifier or QuestionType.WhQuantity;

            if (!qualifierQuestion)
            {
                foreach (string preposition in explicitPrepositions)
                {
                    AddRelation(
                        candidates,
                        RelationRegistry.Normalize(preposition),
                        ClassifyPrepositionLinkClass(preposition, questionType));
                }

                return candidates;
            }

            string[] standardPrepositions = questionType == QuestionType.WhQualifier
                && leadingPreposition is not null
                && RelationExtractor.IsTemporalPreposition(leadingPreposition)
                ? temporalPrepositions
                : spatialPrepositions;

            foreach (string preposition in standardPrepositions)
            {
                AddRelation(
                    candidates,
                    RelationRegistry.Normalize(predicateLemma, preposition),
                    LinkClass.actions);

                AddRelation(
                    candidates,
                    RelationRegistry.Normalize(preposition),
                    ClassifyPrepositionLinkClass(preposition, questionType));
            }

            return candidates;
        }

        private static void AddRelation(
            List<RelationCandidate> candidates,
            string name,
            LinkClass linkClass)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            if (candidates.Any(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            candidates.Add(new RelationCandidate(name, linkClass));
        }

        private static LinkClass ClassifyPrepositionLinkClass(
            string preposition,
            QuestionType questionType)
        {
            if (questionType == QuestionType.WhQualifier || questionType == QuestionType.WhQuantity)
            {
                if (RelationExtractor.IsTemporalPreposition(preposition))
                    return LinkClass.temporal;

                if (RelationExtractor.IsSpatialPreposition(preposition))
                    return LinkClass.spatial;

                return LinkClass.basic;
            }

            return LinkClass.basic;
        }

        private static Chain_Node CreateNode(
            Guid nodeID,
            EntityResolution resolution,
            NlpOptions options)
        {
            Chain_Node node = new Chain_Node(
                nodeID,
                resolution.NodeType,
                options.TemporaryNodeTimeToLive);
            node.Set_Label(resolution.Label);

            foreach (string alias in resolution.Aliases)
                node.Add_Alias(alias);

            return node;
        }

        private static string LemmaOrNormalized(Token token)
        {
            return string.IsNullOrWhiteSpace(token.Lemma)
                ? token.Normalized
                : token.Lemma;
        }

        private static QuestionAnalysis CreateUnsupportedQuestion(
            IReadOnlyList<string> diagnostics,
            int sentenceIndex = -1,
            WhKind wh = WhKind.None,
            string? whSurface = null,
            int whStart = -1,
            int whEnd = -1,
            QuestionType type = QuestionType.Unknown,
            AnswerKind expectedAnswer = AnswerKind.None,
            Tense tense = Tense.Unknown,
            Voice voice = Voice.Unknown)
        {
            return new QuestionAnalysis(
                type,
                wh,
                expectedAnswer,
                whSurface,
                null,
                null,
                null,
                null,
                null,
                null,
                LinkClass.basic,
                tense,
                voice,
                false,
                false,
                false,
                false,
                null,
                FrameRole.Subject,
                Array.Empty<QuestionVariant>(),
                diagnostics,
                sentenceIndex,
                whStart,
                whEnd);
        }

        private sealed record QualifierPhrase(
            string Preposition,
            int StartToken,
            int EndToken);

        private sealed record RelationCandidate(
            string Name,
            LinkClass LinkClass);

        private sealed record RelayLink(
            string Name,
            LinkClass LinkClass);
    }
}
