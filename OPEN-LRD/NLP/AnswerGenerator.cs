using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    internal sealed record QuestionEvaluation(
        QuestionVariant Variant,
        ReasoningResult Result);

    internal static class AnswerGenerator
    {
        private static readonly HashSet<string> prepositionRelations =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "at", "in", "on", "by", "for", "with", "of", "from", "to",
                "during", "after", "before", "near", "inside", "outside",
                "under", "over", "beside", "between", "behind", "above",
                "below", "into", "onto", "toward", "towards", "across",
                "through", "along", "around", "upon", "since", "until"
            };

        private static readonly Dictionary<string, string> irregularPast =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["be"] = "was",
                ["become"] = "became",
                ["begin"] = "began",
                ["break"] = "broke",
                ["bring"] = "brought",
                ["build"] = "built",
                ["buy"] = "bought",
                ["catch"] = "caught",
                ["choose"] = "chose",
                ["come"] = "came",
                ["cut"] = "cut",
                ["do"] = "did",
                ["draw"] = "drew",
                ["drink"] = "drank",
                ["drive"] = "drove",
                ["eat"] = "ate",
                ["fall"] = "fell",
                ["feel"] = "felt",
                ["fight"] = "fought",
                ["find"] = "found",
                ["fly"] = "flew",
                ["forget"] = "forgot",
                ["get"] = "got",
                ["give"] = "gave",
                ["go"] = "went",
                ["grow"] = "grew",
                ["have"] = "had",
                ["hear"] = "heard",
                ["hold"] = "held",
                ["keep"] = "kept",
                ["know"] = "knew",
                ["lead"] = "led",
                ["leave"] = "left",
                ["let"] = "let",
                ["lie"] = "lay",
                ["lose"] = "lost",
                ["make"] = "made",
                ["mean"] = "meant",
                ["meet"] = "met",
                ["pay"] = "paid",
                ["put"] = "put",
                ["read"] = "read",
                ["rise"] = "rose",
                ["run"] = "ran",
                ["say"] = "said",
                ["see"] = "saw",
                ["sell"] = "sold",
                ["send"] = "sent",
                ["set"] = "set",
                ["sing"] = "sang",
                ["sit"] = "sat",
                ["sleep"] = "slept",
                ["speak"] = "spoke",
                ["spend"] = "spent",
                ["stand"] = "stood",
                ["swim"] = "swam",
                ["take"] = "took",
                ["teach"] = "taught",
                ["tell"] = "told",
                ["think"] = "thought",
                ["throw"] = "threw",
                ["understand"] = "understood",
                ["wear"] = "wore",
                ["win"] = "won",
                ["write"] = "wrote"
            };

        private const string temporaryCaveat = " (This fact is not yet verified.)";
        private const string sessionCaveat = " (This fact was provided in this session.)";

        public static NlpAnswer Generate(
            QuestionAnalysis? question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions? options = null,
            ConversationContext? conversation = null)
        {
            ArgumentNullException.ThrowIfNull(evaluations);
            ArgumentNullException.ThrowIfNull(core);

            options ??= new NlpOptions();

            if (question is null)
                return Unsupported(null, new[] { "The question could not be analyzed." });

            if (question.AnchorIsAmbiguous)
                return CreateAmbiguousAnchorAnswer(question, core, options);

            if (!question.Supported)
                return Unsupported(question, question.Diagnostics);

            if (evaluations.Count == 0)
                return Unsupported(question, new[] { "No query could be evaluated." });

            return question.Type == QuestionType.YesNo
                ? GenerateBoolean(question, evaluations, core, options, conversation)
                : GenerateWh(question, evaluations, core, options, conversation);
        }

        public static NlpAnswer Unsupported(
            QuestionAnalysis? question,
            IReadOnlyList<string> diagnostics)
        {
            IReadOnlyList<string> messages = diagnostics ?? Array.Empty<string>();
            string reason = messages.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? string.Empty;
            string text = reason.Length == 0
                ? "I can't answer that question yet."
                : "I can't answer that question yet. " + reason;

            return new NlpAnswer(
                AnswerStatus.Unsupported,
                question?.ExpectedAnswer ?? AnswerKind.None,
                text,
                null,
                null,
                Array.Empty<AnswerCandidate>(),
                Array.Empty<GraphPath>(),
                question,
                null,
                false,
                0.0,
                messages);
        }

        private static NlpAnswer CreateAmbiguousAnchorAnswer(
            QuestionAnalysis question,
            LRD_Core core,
            NlpOptions options)
        {
            string text = "The question anchor matches multiple nodes.";
            IReadOnlyList<AnswerCandidate> candidates = SeedAmbiguousAnchorCandidates(question, core, options);

            return new NlpAnswer(
                AnswerStatus.Ambiguous,
                question.ExpectedAnswer,
                text,
                null,
                null,
                candidates,
                Array.Empty<GraphPath>(),
                question,
                null,
                candidates.Any(candidate => candidate.UsesTemporaryEvidence),
                0.50,
                new[] { "The question anchor matches multiple nodes." });
        }

        private static IReadOnlyList<AnswerCandidate> SeedAmbiguousAnchorCandidates(
            QuestionAnalysis question,
            LRD_Core core,
            NlpOptions options)
        {
            HashSet<Guid> seenIDs = new HashSet<Guid>();
            List<Web_Node> nodes = new List<Web_Node>();

            foreach (Guid candidateID in question.AnchorCandidateNodeIDs)
            {
                if (seenIDs.Add(candidateID)
                    && core.TryGetNode(candidateID, out Web_Node? node)
                    && node is not null
                    && !node.IsDisposed)
                {
                    nodes.Add(node);
                }
            }

            if (nodes.Count == 0)
            {
                FrameArgument? anchor = question.AnchorRole switch
                {
                    FrameRole.Object => question.Object,
                    FrameRole.Qualifier => question.Qualifier,
                    _ => question.Subject
                };
                string? lookup = anchor?.SurfaceText;
                if (string.IsNullOrWhiteSpace(lookup))
                    lookup = anchor?.Lemma;

                if (!string.IsNullOrWhiteSpace(lookup))
                {
                    foreach (Web_Node node in core.FindByLabel(lookup.Trim()))
                    {
                        if (seenIDs.Add(node.ID) && !node.IsDisposed)
                            nodes.Add(node);
                    }
                }
            }

            int relationLinkTypeID = 0;
            if (!string.IsNullOrWhiteSpace(question.RelationName)
                && RelationRegistry.TryGet(question.RelationName, out RelationLinkType relation))
            {
                relationLinkTypeID = relation.Id;
            }
            else if (question.Variants.Count > 0)
            {
                relationLinkTypeID = question.Variants[0].ForwardLinkTypeId;
            }

            return nodes
                .OrderByDescending(node => node.IsPermanent)
                .ThenByDescending(node => node.Get_WeightUp())
                .ThenBy(node => node.ID)
                .Take(Math.Max(1, options.MaxAnswerCandidates))
                .Select(node => new AnswerCandidate(
                    node.ID,
                    string.IsNullOrWhiteSpace(ResolveLabel(node))
                        ? node.ID.ToString()
                        : ResolveLabel(node),
                    node.Get_NodeType(),
                    0.50,
                    0,
                    relationLinkTypeID,
                    question.LinkClass,
                    new[] { node.ID },
                    node.IsTemporary))
                .ToArray();
        }

        private static NlpAnswer GenerateBoolean(
            QuestionAnalysis question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions options,
            ConversationContext? conversation)
        {
            bool verified = false;
            bool ambiguous = false;
            bool noMatch = false;
            bool structuralFailure = false;
            double confidence = 0.0;
            GraphPath? evidencePath = null;
            List<string> diagnostics = new List<string>();

            foreach (QuestionEvaluation evaluation in evaluations)
            {
                diagnostics.AddRange(evaluation.Result.Diagnostics);

                switch (evaluation.Result.Status)
                {
                    case ReasoningStatus.Answered:
                        if (IsBooleanVerified(evaluation))
                        {
                            verified = true;
                            confidence = Math.Max(
                                confidence,
                                Math.Clamp(evaluation.Result.SelectedMatch?.OverallScore ?? 0.0, 0.0, 1.0));

                            evidencePath ??= evaluation.Result.ResponsePaths
                                .FirstOrDefault(path => path.NodeIDs.Count >= 2);
                        }

                        break;

                    case ReasoningStatus.Ambiguous:
                        ambiguous = true;
                        diagnostics.Add(
                            $"The query returned {evaluation.Result.Matches.Count} candidate match(es).");
                        break;

                    case ReasoningStatus.NoMatch:
                        noMatch = true;
                        break;

                    case ReasoningStatus.RejectedByTrust:
                    case ReasoningStatus.InvalidInput:
                        structuralFailure = true;
                        break;
                }
            }

            AnswerStatus status;
            string? shortAnswer;
            string text;
            AnswerKind kind = AnswerKind.Boolean;
            bool usesSessionProvenance = false;

            if (verified)
            {
                status = AnswerStatus.Yes;
                shortAnswer = "Yes";
                confidence = confidence <= 0.0 ? 0.40 : confidence;
                string? evidence = options.IncludeEvidenceSentence
                    ? BuildEvidenceSentence(evidencePath, core, question.Tense)
                    : null;
                string baseText = evidence is null ? "Yes." : "Yes. " + evidence;
                usesSessionProvenance = UsesSessionProvenance(
                    evidencePath,
                    conversation,
                    options);

                if (usesSessionProvenance)
                {
                    text = AppendSessionCaveat(baseText, options, diagnostics);
                    diagnostics.Add("Answer evidence was provided in this session.");
                }
                else
                {
                    text = baseText;
                }
            }
            else if (ambiguous)
            {
                status = AnswerStatus.Ambiguous;
                shortAnswer = null;
                confidence = 0.50;
                text = "I found multiple possible answers, so I can't decide between them.";
            }
            else if (noMatch)
            {
                status = AnswerStatus.No;
                shortAnswer = "No";
                confidence = 0.40;
                text = "No, I don't have a fact matching that.";
            }
            else if (structuralFailure)
            {
                return Unsupported(question, new[] { "The query could not be evaluated." });
            }
            else
            {
                status = AnswerStatus.No;
                shortAnswer = "No";
                confidence = 0.40;
                text = "No, I don't have a fact matching that.";
            }

            List<GraphPath> evidencePaths = evidencePath is null
                ? new List<GraphPath>()
                : new List<GraphPath> { evidencePath };

            return new NlpAnswer(
                status,
                kind,
                TrimText(text, options.MaxAnswerTextLength, diagnostics),
                shortAnswer,
                null,
                Array.Empty<AnswerCandidate>(),
                evidencePaths,
                question,
                evaluations.FirstOrDefault()?.Result,
                false,
                Math.Clamp(confidence, 0.0, 1.0),
                Deduplicate(diagnostics),
                usesSessionProvenance);
        }

        private static NlpAnswer GenerateWh(
            QuestionAnalysis question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions options,
            ConversationContext? conversation)
        {
            if (question.AnchorNodeID is null)
            {
                if (question.ExpectedAnswer == AnswerKind.Quantity)
                {
                    return CreateQuantityAnswer(
                        question,
                        Array.Empty<AnswerCandidate>(),
                        evaluations,
                        core,
                        options,
                        conversation,
                        new List<string>());
                }

                List<string> missingDiagnostics = new List<string>();

                foreach (QuestionEvaluation evaluation in evaluations)
                    missingDiagnostics.AddRange(evaluation.Result.Diagnostics);

                missingDiagnostics.Add("The question anchor is not present in the graph.");

                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    question.ExpectedAnswer,
                    BuildNoMatchText(question),
                    null,
                    null,
                    Array.Empty<AnswerCandidate>(),
                    Array.Empty<GraphPath>(),
                    question,
                    evaluations.FirstOrDefault()?.Result,
                    false,
                    0.0,
                    Deduplicate(missingDiagnostics));
            }

            List<string> diagnostics = new List<string>();
            List<AnswerCandidate> immediate = new List<AnswerCandidate>();
            List<AnswerCandidate> deeper = new List<AnswerCandidate>();
            bool ambiguousReasoning = false;
            bool structuralFailure = false;

            foreach (QuestionEvaluation evaluation in evaluations)
            {
                diagnostics.AddRange(evaluation.Result.Diagnostics);

                switch (evaluation.Result.Status)
                {
                    case ReasoningStatus.Answered:
                        ExtractCandidates(
                            question,
                            evaluation,
                            core,
                            options,
                            immediate,
                            deeper,
                            diagnostics);
                        break;

                    case ReasoningStatus.Ambiguous:
                        ambiguousReasoning = true;
                        diagnostics.Add(
                            $"The query returned {evaluation.Result.Matches.Count} candidate match(es).");
                        ExtractCandidates(
                            question,
                            evaluation,
                            core,
                            options,
                            immediate,
                            deeper,
                            diagnostics);
                        break;

                    case ReasoningStatus.NoMatch:
                        break;

                    case ReasoningStatus.RejectedByTrust:
                    case ReasoningStatus.InvalidInput:
                        structuralFailure = true;
                        break;
                }
            }

            List<AnswerCandidate> candidates = immediate.Count > 0 ? immediate : deeper;

            if (candidates.Count == 0)
            {
                if (question.ExpectedAnswer == AnswerKind.Quantity && !structuralFailure)
                {
                    return CreateQuantityAnswer(
                        question,
                        Array.Empty<AnswerCandidate>(),
                        evaluations,
                        core,
                        options,
                        conversation,
                        diagnostics);
                }

                if (ambiguousReasoning)
                {
                    return CreateAmbiguousAnswer(
                        question,
                        evaluations,
                        Array.Empty<AnswerCandidate>(),
                        diagnostics,
                        core,
                        options);
                }

                if (structuralFailure)
                    return Unsupported(question, new[] { "The query could not be evaluated." });

                diagnostics.Add("No answer candidate could be extracted from the matching reasoning result.");

                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    question.ExpectedAnswer,
                    BuildNoMatchText(question),
                    null,
                    null,
                    Array.Empty<AnswerCandidate>(),
                    Array.Empty<GraphPath>(),
                    question,
                    evaluations.FirstOrDefault()?.Result,
                    false,
                    0.0,
                    Deduplicate(diagnostics));
            }

            int answerCandidateLimit = IsDictionarySenseQuestion(question)
                ? int.MaxValue
                : Math.Max(1, options.MaxAnswerCandidates);
            candidates = candidates
                .Where(candidate => candidate.Score >= options.MinimumAnswerScore)
                .GroupBy(
                    candidate => candidate.NodeID != Guid.Empty
                        ? candidate.NodeID.ToString()
                        : candidate.Label,
                    StringComparer.Ordinal)
                .Select(group => group
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.Depth)
                    .First())
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Label, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.NodeID.ToString(), StringComparer.Ordinal)
                .Take(answerCandidateLimit)
                .ToList();

            if (candidates.Count == 0)
            {
                if (question.ExpectedAnswer == AnswerKind.Quantity)
                {
                    return CreateQuantityAnswer(
                        question,
                        Array.Empty<AnswerCandidate>(),
                        evaluations,
                        core,
                        options,
                        conversation,
                        diagnostics);
                }

                diagnostics.Add("No answer candidate passed the relation and type filters.");

                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    question.ExpectedAnswer,
                    BuildNoMatchText(question),
                    null,
                    null,
                    Array.Empty<AnswerCandidate>(),
                    Array.Empty<GraphPath>(),
                    question,
                    evaluations.FirstOrDefault()?.Result,
                    false,
                    0.0,
                    Deduplicate(diagnostics));
            }

            if (options.EnableMultiAnswerSynthesis && candidates.Count > 1)
            {
                double bestScore = candidates[0].Score;
                double synthesisFloor = bestScore - Math.Max(options.AnswerAmbiguityMargin, 0.05);
                List<AnswerCandidate> strongestCandidates = candidates
                    .Where(candidate => candidate.Score >= synthesisFloor)
                    .ToList();
                if (strongestCandidates.Count < candidates.Count)
                {
                    diagnostics.Add("Excluded weaker relation matches from the synthesized answer set.");
                    candidates = strongestCandidates;
                }
            }

            int preferredRelation = candidates[0].RelationLinkTypeId;
            List<AnswerCandidate> relationMatchedCandidates = candidates
                .Where(candidate => candidate.RelationLinkTypeId == preferredRelation)
                .ToList();
            if (relationMatchedCandidates.Count < candidates.Count)
            {
                diagnostics.Add("Kept answer candidates matching the strongest relation interpretation.");
                candidates = relationMatchedCandidates;
            }

            if (IsDictionarySenseQuestion(question))
            {
                return CreateDictionarySenseAnswer(
                    question,
                    candidates,
                    evaluations,
                    core,
                    options,
                    diagnostics);
            }

            if (question.ExpectedAnswer == AnswerKind.Quantity)
            {
                return CreateQuantityAnswer(
                    question,
                    candidates,
                    evaluations,
                    core,
                    options,
                    conversation,
                    diagnostics);
            }

            if (options.EnableMultiAnswerSynthesis && candidates.Count > 1)
            {
                bool hasDuplicateLabels = candidates
                    .GroupBy(candidate => candidate.Label, StringComparer.OrdinalIgnoreCase)
                    .Any(group => group.Count() > 1);

                if (hasDuplicateLabels)
                {
                    return CreateAmbiguousAnswer(
                        question,
                        evaluations,
                        candidates,
                        diagnostics,
                        core,
                        options);
                }

                return CreateSynthesizedAnswer(
                    question,
                    evaluations,
                    candidates,
                    core,
                    options,
                    conversation,
                    diagnostics);
            }

            if (candidates.Count >= 2
                && candidates[0].Depth == candidates[1].Depth
                && Math.Abs(candidates[0].Score - candidates[1].Score) <= options.AnswerAmbiguityMargin)
            {
                return CreateAmbiguousAnswer(
                    question,
                    evaluations,
                    candidates,
                    diagnostics,
                    core,
                    options);
            }

            AnswerCandidate selected = candidates[0];
            diagnostics.Add($"Selected answer '{selected.Label}' with score {selected.Score:F3}.");

            bool usesTemporaryEvidence = selected.UsesTemporaryEvidence;

            if (usesTemporaryEvidence)
                diagnostics.Add("All evidence for this answer is temporary.");

            GraphPath? evidencePath = FindEvidencePath(selected, evaluations, core);

            string? evidence = options.IncludeEvidenceSentence
                ? BuildEvidenceSentence(evidencePath, core, question.Tense)
                : null;
            string baseText = evidence ?? selected.Label;
            bool usesSessionProvenance = UsesSessionProvenance(
                evidencePath,
                conversation,
                options);
            string text;

            if (usesSessionProvenance)
            {
                text = AppendSessionCaveat(baseText, options, diagnostics);
                diagnostics.Add("Answer evidence was provided in this session.");
            }
            else
            {
                text = baseText;

                if (usesTemporaryEvidence)
                    text += temporaryCaveat;
            }

            AnswerKind kind = DetermineAnswerKind(question, selected);

            return new NlpAnswer(
                AnswerStatus.Answered,
                kind,
                TrimText(text, options.MaxAnswerTextLength, diagnostics),
                selected.Label,
                selected.NodeID,
                candidates,
                EvidencePathsFor(candidates, evaluations, options, core),
                question,
                evaluations.FirstOrDefault()?.Result,
                usesTemporaryEvidence,
                Math.Clamp(selected.Score, 0.0, 1.0),
                Deduplicate(diagnostics),
                usesSessionProvenance);
        }

        private static bool IsDictionarySenseQuestion(QuestionAnalysis question)
        {
            return question.Wh == WhKind.What
                && question.Variants.Any(variant => string.Equals(
                    variant.RelationName,
                    "has_sense",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static NlpAnswer CreateDictionarySenseAnswer(
            QuestionAnalysis question,
            IReadOnlyList<AnswerCandidate> candidates,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions options,
            List<string> diagnostics)
        {
            List<AnswerCandidate> senses = candidates
                .Where(candidate => candidate.NodeType == OntologyTypes.Senses)
                .GroupBy(candidate => candidate.NodeID)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.NodeID)
                .ToList();

            string word = GetDictionaryHeadword(question, core);
            string article = GetDictionaryArticle(question.Subject?.SurfaceText);
            string nounPhrase = article.Length == 0 ? word : article + " " + word;

            if (senses.Count == 1)
            {
                AnswerCandidate sense = senses[0];
                string definition = EnsureSentenceEnding(sense.Label);
                string text = $"The definition of {nounPhrase} is {definition}";
                GraphPath? evidencePath = FindEvidencePath(sense, evaluations, core);
                bool usesTemporaryEvidence = sense.UsesTemporaryEvidence;

                if (usesTemporaryEvidence)
                    diagnostics.Add("The dictionary definition is backed by temporary evidence.");

                return new NlpAnswer(
                    AnswerStatus.Answered,
                    AnswerKind.Description,
                    TrimText(text, options.MaxAnswerTextLength, diagnostics),
                    sense.Label,
                    sense.NodeID,
                    senses,
                    EvidencePathsFor(senses, evaluations, options, core, includeAllCandidates: true),
                    question,
                    evaluations.FirstOrDefault()?.Result,
                    usesTemporaryEvidence,
                    Math.Clamp(sense.Score, 0.0, 1.0),
                    Deduplicate(diagnostics));
            }

            diagnostics.Add($"Found {senses.Count} matching GCIDE senses for '{word}'.");
            string multipleText = $"Multiple definitions for {nounPhrase} were found.";

            return new NlpAnswer(
                AnswerStatus.Ambiguous,
                AnswerKind.Description,
                TrimText(multipleText, options.MaxAnswerTextLength, diagnostics),
                null,
                null,
                senses,
                EvidencePathsFor(senses, evaluations, options, core, includeAllCandidates: true),
                question,
                evaluations.FirstOrDefault()?.Result,
                senses.Any(candidate => candidate.UsesTemporaryEvidence),
                0.50,
                Deduplicate(diagnostics));
        }

        private static string GetDictionaryHeadword(QuestionAnalysis question, LRD_Core core)
        {
            if (question.AnchorNodeID.HasValue
                && core.TryGetNode(question.AnchorNodeID.Value, out Web_Node? anchor)
                && anchor is not null
                && !string.IsNullOrWhiteSpace(anchor.Label))
            {
                return anchor.Label!;
            }

            string surface = question.Subject?.SurfaceText
                ?? question.Object?.SurfaceText
                ?? "word";
            string[] parts = surface.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            int start = parts.Length > 1 && IsArticle(parts[0]) ? 1 : 0;
            return string.Join(" ", parts.Skip(start));
        }

        private static string GetDictionaryArticle(string? surfaceText)
        {
            if (string.IsNullOrWhiteSpace(surfaceText))
                return string.Empty;

            string? first = surfaceText
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            return first is not null && IsArticle(first)
                ? first.ToLowerInvariant()
                : string.Empty;
        }

        private static bool IsArticle(string value)
        {
            return value.Equals("a", StringComparison.OrdinalIgnoreCase)
                || value.Equals("an", StringComparison.OrdinalIgnoreCase)
                || value.Equals("the", StringComparison.OrdinalIgnoreCase);
        }

        private static string EnsureSentenceEnding(string text)
        {
            string trimmed = text.Trim();
            return trimmed.Length == 0 || trimmed.EndsWith(".", StringComparison.Ordinal)
                || trimmed.EndsWith("!", StringComparison.Ordinal)
                || trimmed.EndsWith("?", StringComparison.Ordinal)
                ? trimmed
                : trimmed + ".";
        }

        private static NlpAnswer CreateQuantityAnswer(
            QuestionAnalysis question,
            IReadOnlyList<AnswerCandidate> candidates,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions options,
            ConversationContext? conversation,
            List<string> diagnostics)
        {
            int count = candidates.Count;
            string quantifiedNoun = GetQuantifiedNoun(question);
            bool massNoun = question.WhSurface?.Contains("much", StringComparison.OrdinalIgnoreCase) == true;
            string singular = Lemmatizer.Lemmatize(quantifiedNoun, PartOfSpeech.Noun);
            string plural = massNoun
                ? quantifiedNoun
                : EndsInPlural(singular) ? singular : PluralizeNoun(singular);
            bool countsSubject = question.Variants.FirstOrDefault()?.AnswerRole == FrameRole.Subject
                || (question.Type == QuestionType.WhQuantity
                    && question.AnchorRole is FrameRole.Qualifier or FrameRole.Object);
            string labels = FormatConjunction(candidates.Select(candidate => candidate.Label).ToList());
            string text;

            if (countsSubject)
            {
                string anchor = question.Qualifier?.SurfaceText
                    ?? question.Object?.SurfaceText
                    ?? string.Empty;
                string? preposition = question.Qualifier?.Preposition;
                string anchorPhrase = string.IsNullOrWhiteSpace(anchor)
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(preposition)
                        ? " " + anchor
                        : " " + preposition + " " + anchor;
                string verbPhrase = PluralVerbPhrase(question.PredicateLemma, question.Tense);
                text = $"There are {count} {plural} who {verbPhrase}{anchorPhrase}";
                if (count > 0 && !string.IsNullOrWhiteSpace(labels))
                    text += ": " + labels;
                text += ".";
            }
            else
            {
                string subject = question.Subject?.SurfaceText
                    ?? question.Qualifier?.SurfaceText
                    ?? "The subject";
                string verbPhrase = string.IsNullOrWhiteSpace(question.PredicateLemma)
                    ? "has"
                    : BuildVerbPhrase(question.PredicateLemma, question.Tense);
                text = count == 0
                    ? $"{subject} {verbPhrase} no {plural}."
                    : $"{subject} {verbPhrase} {count} {plural}: {labels}.";
            }

            IReadOnlyList<GraphPath> paths = EvidencePathsFor(candidates, evaluations, options, core);
            bool usesTemporaryEvidence = candidates.Any(candidate => candidate.UsesTemporaryEvidence);
            bool usesSessionProvenance = paths.Any(path => UsesSessionProvenance(path, conversation, options));
            if (usesSessionProvenance)
            {
                text = AppendSessionCaveat(text, options, diagnostics);
                diagnostics.Add("Quantity evidence was provided in this session.");
            }
            else if (usesTemporaryEvidence)
            {
                text += temporaryCaveat;
            }

            diagnostics.Add($"Counted {count} matching {plural}.");
            return new NlpAnswer(
                AnswerStatus.Answered,
                AnswerKind.Quantity,
                TrimText(text, options.MaxAnswerTextLength, diagnostics),
                count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                count == 1 ? candidates[0].NodeID : null,
                candidates,
                paths,
                question,
                evaluations.FirstOrDefault()?.Result,
                usesTemporaryEvidence,
                candidates.Count == 0 ? 0.85 : candidates.Max(candidate => candidate.Score),
                Deduplicate(diagnostics),
                usesSessionProvenance);
        }

        private static string GetQuantifiedNoun(QuestionAnalysis question)
        {
            string surface = question.WhSurface ?? question.WhArgument?.SurfaceText ?? string.Empty;
            string[] words = surface.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            int start = words.Length >= 2
                && words[0].Equals("how", StringComparison.OrdinalIgnoreCase)
                && (words[1].Equals("many", StringComparison.OrdinalIgnoreCase)
                    || words[1].Equals("much", StringComparison.OrdinalIgnoreCase))
                ? 2
                : words.Length;
            string noun = string.Join(" ", words.Skip(start)).Trim();
            return string.IsNullOrWhiteSpace(noun) ? "items" : noun;
        }

        private static bool EndsInPlural(string noun)
        {
            return noun.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                || string.Equals(noun, "people", StringComparison.OrdinalIgnoreCase);
        }

        private static string PluralVerbPhrase(string? predicate, Tense tense)
        {
            if (string.IsNullOrWhiteSpace(predicate))
                return "are";

            string verb = predicate.ToLowerInvariant();
            if (verb == "be")
                return tense == Tense.Past ? "were" : tense == Tense.Future ? "will be" : "are";
            if (verb == "have")
                return tense == Tense.Past ? "had" : tense == Tense.Future ? "will have" : "have";
            if (verb == "do")
                return tense == Tense.Past ? "did" : tense == Tense.Future ? "will do" : "do";

            string baseVerb = Lemmatizer.Lemmatize(verb, PartOfSpeech.Verb);
            if (tense == Tense.Past)
                return PastTense(baseVerb);
            if (tense == Tense.Future)
                return "will " + baseVerb;
            return baseVerb;
        }

        public static NlpAnswer GenerateExploration(
            QuestionAnalysis question,
            LRD_Core core,
            NlpOptions options,
            ConversationContext? conversation)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);

            if (!question.AnchorNodeID.HasValue
                || !core.TryGetNode(question.AnchorNodeID.Value, out Web_Node? anchor)
                || anchor is null)
            {
                string label = question.Subject?.SurfaceText
                    ?? question.Object?.SurfaceText
                    ?? "that entity";
                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    AnswerKind.Description,
                    $"I don't know anything about {label} yet.",
                    null,
                    null,
                    Array.Empty<AnswerCandidate>(),
                    Array.Empty<GraphPath>(),
                    question,
                    null,
                    false,
                    0.0,
                    new[] { "The exploration entity is not present in the knowledge graph." });
            }

            Dictionary<string, (Link Link, Web_Node RelatedNode)> connectedFacts =
                new Dictionary<string, (Link Link, Web_Node RelatedNode)>(StringComparer.Ordinal);
            IEnumerable<Web_Node> connectedNodes = core.FindLinkedTo(anchor.ID).Append(anchor);

            foreach (Web_Node owner in connectedNodes)
            {
                foreach (Link link in core.GetLinks(owner.ID))
                {
                    bool outgoing = link.SourceID == anchor.ID && link.DestinationID != anchor.ID;
                    bool incoming = link.DestinationID == anchor.ID && link.SourceID != anchor.ID;
                    if (!outgoing && !incoming)
                        continue;

                    Guid relatedID = outgoing ? link.DestinationID : link.SourceID;
                    if (!core.TryGetNode(relatedID, out Web_Node? relatedNode)
                        || relatedNode is null
                        || string.IsNullOrWhiteSpace(ResolveLabel(relatedNode))
                        || IsBlockedExplorationType(relatedNode.Get_NodeType()))
                    {
                        continue;
                    }

                    string key = string.Join(
                        "|",
                        link.SourceID,
                        link.DestinationID,
                        link.ForwardLink.Id,
                        link.InverseLink.Id,
                        link.Classification.Id);
                    connectedFacts.TryAdd(key, (link, relatedNode));
                }
            }

            List<AnswerCandidate> candidates = new List<AnswerCandidate>();
            List<GraphPath> paths = new List<GraphPath>();
            foreach ((Link link, Web_Node relatedNode) in connectedFacts.Values
                .OrderByDescending(fact => fact.RelatedNode.IsPermanent)
                .ThenByDescending(fact => fact.RelatedNode.Get_WeightUp())
                .ThenBy(fact => fact.Link.ForwardLink.Name, StringComparer.Ordinal)
                .ThenBy(fact => ResolveLabel(fact.RelatedNode), StringComparer.Ordinal)
                .ThenBy(fact => fact.RelatedNode.ID)
                .Take(Math.Max(1, options.MaxAnswerCandidates)))
            {
                double score = relatedNode.IsPermanent ? 0.80 : 0.65;
                GraphPath path = new GraphPath(
                    new[] { anchor.ID, relatedNode.ID },
                    new[] { link },
                    score,
                    score,
                    1);
                paths.Add(path);
                candidates.Add(new AnswerCandidate(
                    relatedNode.ID,
                    ResolveLabel(relatedNode),
                    relatedNode.Get_NodeType(),
                    score,
                    1,
                    link.ForwardLink.Id,
                    link.Classification,
                    path.NodeIDs,
                    PathIsTemporary(path, core)));
            }

            if (candidates.Count == 0)
            {
                string label = ResolveLabel(anchor);
                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    AnswerKind.Description,
                    $"I don't have any recorded facts about {label} yet.",
                    null,
                    null,
                    Array.Empty<AnswerCandidate>(),
                    Array.Empty<GraphPath>(),
                    question,
                    null,
                    false,
                    0.0,
                    new[] { "No active incoming or outgoing facts were found for the exploration entity." });
            }

            List<string> factSentences = paths
                .Select(path => BuildEvidenceSentence(path, core, Tense.Present)!)
                .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            bool usesTemporaryEvidence = candidates.Any(candidate => candidate.UsesTemporaryEvidence);
            bool usesSessionProvenance = paths.Any(path => UsesSessionProvenance(path, conversation, options));
            string text = factSentences.Count > 0
                ? string.Join(" ", factSentences)
                : FormatConjunction(candidates.Select(candidate => candidate.Label).ToList());

            if (usesSessionProvenance)
                text = AppendSessionCaveat(text, options, new List<string>());
            else if (usesTemporaryEvidence)
                text += temporaryCaveat;

            return new NlpAnswer(
                AnswerStatus.Answered,
                AnswerKind.Description,
                TrimText(text, options.MaxAnswerTextLength, new List<string>()),
                FormatConjunction(candidates.Select(candidate => candidate.Label).ToList()),
                candidates.Count == 1 ? candidates[0].NodeID : null,
                candidates,
                paths,
                question,
                null,
                usesTemporaryEvidence,
                candidates.Max(candidate => candidate.Score),
                Array.Empty<string>(),
                usesSessionProvenance);
        }

        public static NlpAnswer GenerateEventTimeAnswer(
            QuestionAnalysis question,
            Web_Node eventNode,
            LRD_Core core,
            NlpOptions options)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentNullException.ThrowIfNull(eventNode);
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);

            string name = EventNodeHelper.GetDisplayName(eventNode);
            if (!EventNodeHelper.TryGetWindow(eventNode, out DateTimeOffset? start, out DateTimeOffset? end))
            {
                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    AnswerKind.Time,
                    $"I don't have a date or time recorded for {name} yet.",
                    null,
                    null,
                    new[] { EventCandidate(eventNode, core) },
                    EventEvidencePaths(eventNode, core),
                    question,
                    null,
                    eventNode.IsTemporary,
                    0.0,
                    new[] { "The event has no encoded start or end time." });
            }

            EventTimeVariance variance = EventTimeVarianceCalculator.Calculate(eventNode, options.ReferenceTime);
            string window = FormatEventWindow(start, end);
            string text = FormatEventTimeStatement(name, window, variance);
            bool temporary = eventNode.IsTemporary;
            if (temporary)
                text += temporaryCaveat;

            return new NlpAnswer(
                AnswerStatus.Answered,
                AnswerKind.Time,
                TrimText(text, options.MaxAnswerTextLength, new List<string>()),
                window,
                eventNode.ID,
                new[] { EventCandidate(eventNode, core) },
                EventEvidencePaths(eventNode, core),
                question,
                null,
                temporary,
                0.95,
                Array.Empty<string>());
        }

        public static NlpAnswer GenerateEventTimeAnswers(
            QuestionAnalysis question,
            IReadOnlyList<Web_Node> eventNodes,
            LRD_Core core,
            NlpOptions options)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentNullException.ThrowIfNull(eventNodes);
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);

            if (eventNodes.Count == 0)
                return Unsupported(question, new[] { "No matching timed Event nodes were found." });
            if (eventNodes.Count == 1)
                return GenerateEventTimeAnswer(question, eventNodes[0], core, options);

            List<AnswerCandidate> candidates = eventNodes
                .Select(eventNode => EventCandidate(eventNode, core))
                .ToList();
            List<GraphPath> paths = eventNodes.SelectMany(eventNode => EventEvidencePaths(eventNode, core)).ToList();
            string text = string.Join(" ", eventNodes.Select(eventNode =>
            {
                EventNodeHelper.TryGetWindow(eventNode, out DateTimeOffset? start, out DateTimeOffset? end);
                EventTimeVariance variance = EventTimeVarianceCalculator.Calculate(eventNode, options.ReferenceTime);
                return FormatEventTimeStatement(
                    EventNodeHelper.GetDisplayName(eventNode),
                    FormatEventWindow(start, end),
                    variance);
            }));
            bool temporary = eventNodes.Any(eventNode => eventNode.IsTemporary);
            if (temporary)
                text += temporaryCaveat;

            return new NlpAnswer(
                AnswerStatus.Answered,
                AnswerKind.Time,
                TrimText(text, options.MaxAnswerTextLength, new List<string>()),
                FormatConjunction(candidates.Select(candidate => candidate.Label).ToList()),
                null,
                candidates,
                paths,
                question,
                null,
                temporary,
                0.95,
                Array.Empty<string>());
        }

        public static NlpAnswer GenerateTemporalRangeAnswer(
            TemporalRangeQuery query,
            LRD_Core core,
            NlpOptions options)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);

            IReadOnlyList<Web_Node> events = core.FindEventsInWindow(query.Start, query.End, includeOverlapping: true);
            if (events.Count == 0)
            {
                string range = FormatRangeHeading(query);
                return new NlpAnswer(
                    query.IsCountQuery ? AnswerStatus.Answered : AnswerStatus.NoMatch,
                    query.IsCountQuery ? AnswerKind.Quantity : AnswerKind.Description,
                    query.IsCountQuery
                        ? $"0 events matched {range}."
                        : $"No events were found {range}.",
                    query.IsCountQuery ? "0" : null,
                    null,
                    Array.Empty<AnswerCandidate>(),
                    Array.Empty<GraphPath>(),
                    null,
                    null,
                    false,
                    query.IsCountQuery ? 0.85 : 0.0,
                    new[] { "No Event nodes overlap the requested time window." });
            }

            List<AnswerCandidate> candidates = events
                .Select(eventNode => EventCandidate(eventNode, core))
                .ToList();
            List<GraphPath> paths = events
                .SelectMany(eventNode => EventEvidencePaths(eventNode, core))
                .ToList();
            string heading = FormatRangeHeading(query);
            string descriptions = string.Join(" ", events.Select(eventNode =>
                BuildEventDescription(eventNode, core, options.ReferenceTime) + " ("
                + (EventNodeHelper.TryGetWindow(eventNode, out DateTimeOffset? eventStart, out DateTimeOffset? eventEnd)
                    ? FormatEventWindow(eventStart, eventEnd)
                    : "time unknown") + ")."));
            bool usesTemporary = events.Any(eventNode => eventNode.IsTemporary);
            string text = heading + ": " + descriptions;
            if (usesTemporary)
                text += temporaryCaveat;

            if (query.IsCountQuery)
            {
                string eventWord = events.Count == 1 ? "event" : "events";
                text = $"{events.Count} {eventWord} matched {heading}: {descriptions}";
                if (usesTemporary)
                    text += temporaryCaveat;
            }

            return new NlpAnswer(
                AnswerStatus.Answered,
                query.IsCountQuery ? AnswerKind.Quantity : AnswerKind.Description,
                TrimText(text, options.MaxAnswerTextLength, new List<string>()),
                query.IsCountQuery
                    ? events.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : FormatConjunction(candidates.Select(candidate => candidate.Label).ToList()),
                events.Count == 1 ? events[0].ID : null,
                candidates,
                paths,
                null,
                null,
                usesTemporary,
                0.90,
                Array.Empty<string>());
        }

        private static AnswerCandidate EventCandidate(Web_Node eventNode, LRD_Core core)
        {
            int temporalRelation = RelationRegistry.GetOrRegister("has_time").Forward.Id;
            IReadOnlyList<Guid> pathIDs = EventEvidencePaths(eventNode, core)
                .FirstOrDefault()?.NodeIDs ?? new[] { eventNode.ID };
            return new AnswerCandidate(
                eventNode.ID,
                EventNodeHelper.GetDisplayName(eventNode),
                NodeTypes.Events,
                0.95,
                pathIDs.Count > 1 ? 1 : 0,
                temporalRelation,
                LinkClass.temporal,
                pathIDs,
                eventNode.IsTemporary);
        }

        private static IReadOnlyList<GraphPath> EventEvidencePaths(Web_Node eventNode, LRD_Core core)
        {
            Link? timeLink = core.GetLinks(eventNode.ID)
                .FirstOrDefault(link => link.SourceID == eventNode.ID
                    && string.Equals(link.ForwardLink.Name, "has_time", StringComparison.OrdinalIgnoreCase));
            if (timeLink is null)
                return Array.Empty<GraphPath>();

            return new[]
            {
                new GraphPath(
                    new[] { eventNode.ID, timeLink.DestinationID },
                    new[] { timeLink },
                    0.95,
                    0.95,
                    1)
            };
        }

        private static string BuildEventDescription(
            Web_Node eventNode,
            LRD_Core core,
            DateTimeOffset? referenceTime)
        {
            string? subject = EventRoleLabel(eventNode, core, "has_subject");
            string? action = EventRoleLabel(eventNode, core, "has_action");
            string? objectLabel = EventRoleLabel(eventNode, core, "has_object");
            string? location = EventRoleLabel(eventNode, core, "has_location");
            string name = EventNodeHelper.GetDisplayName(eventNode);

            if (subject is null || action is null)
                return name;

            string actionPhrase;
            EventTimeVariance variance = EventTimeVarianceCalculator.Calculate(eventNode, referenceTime);
            if (variance.Status == EventTemporalStatus.Past)
            {
                actionPhrase = PastTense(action);
            }
            else if (variance.Status == EventTemporalStatus.Future)
            {
                actionPhrase = "will " + action;
            }
            else
            {
                actionPhrase = ThirdPerson(action);
            }

            string description = subject + " " + actionPhrase
                + (objectLabel is null ? string.Empty : " " + objectLabel)
                + (location is null ? string.Empty : " in " + location);

            if (string.Equals(name, description, StringComparison.OrdinalIgnoreCase))
                return name;
            return string.IsNullOrWhiteSpace(name) ? description : name + ": " + description;
        }

        private static string? EventRoleLabel(Web_Node eventNode, LRD_Core core, string relationName)
        {
            Link? link = core.GetLinks(eventNode.ID).FirstOrDefault(candidate =>
                candidate.SourceID == eventNode.ID
                && string.Equals(candidate.ForwardLink.Name, relationName, StringComparison.OrdinalIgnoreCase));
            if (link is null || !core.TryGetNode(link.DestinationID, out Web_Node? target) || target is null)
                return null;

            return ResolveLabel(target);
        }

        private static string FormatEventWindow(DateTimeOffset? start, DateTimeOffset? end)
        {
            if (start.HasValue && end.HasValue)
            {
                bool fullDay = start.Value.TimeOfDay == TimeSpan.Zero
                    && end.Value.Date == start.Value.Date
                    && end.Value.TimeOfDay >= TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(59));
                if (fullDay)
                    return "on " + start.Value.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);

                if (start.Value.Date == end.Value.Date)
                {
                    return "on " + start.Value.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture)
                        + " from " + start.Value.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
                        + " to " + end.Value.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
                }

                return "from " + start.Value.ToString("MMMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture)
                    + " to " + end.Value.ToString("MMMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (start.HasValue)
                return "starting " + start.Value.ToString("MMMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            if (end.HasValue)
                return "ending " + end.Value.ToString("MMMM d, yyyy h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            return "at an unknown time";
        }

        private static string FormatEventTimeStatement(
            string name,
            string window,
            EventTimeVariance variance)
        {
            return variance.Status switch
            {
                EventTemporalStatus.Future => $"{name} will be {window} ({variance.FriendlyVariance}).",
                EventTemporalStatus.Active => $"{name} is currently ongoing ({window}; {StripOngoingPrefix(variance.FriendlyVariance)}).",
                EventTemporalStatus.Past => $"{name} was {window} ({variance.FriendlyVariance}).",
                _ => $"{name} was {window}."
            };
        }

        private static string StripOngoingPrefix(string text)
        {
            const string prefix = "ongoing ";
            return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? text[prefix.Length..].Trim().Trim('(', ')')
                : text;
        }

        private static string FormatRangeHeading(TemporalRangeQuery query)
        {
            foreach (string preposition in new[] { "on ", "in ", "at " })
            {
                if (query.OriginalExpression.StartsWith(preposition, StringComparison.OrdinalIgnoreCase))
                {
                    return char.ToUpperInvariant(preposition[0])
                        + query.OriginalExpression[1..];
                }
            }

            string expression = query.OriginalExpression
                .Trim()
                .TrimEnd('.', '!', '?', ';', ',')
                .TrimEnd();
            return expression.Length == 0
                ? "In the requested time window"
                : char.ToUpperInvariant(expression[0]) + expression[1..];
        }

        private static bool IsBlockedExplorationType(NodeTypes type)
        {
            return type == NodeTypes.Empties
                || type == NodeTypes.Dictionaries
                || type == NodeTypes.Sources;
        }

        private static string BuildNoMatchText(QuestionAnalysis question)
        {
            string subject = question.Subject?.SurfaceText
                ?? question.Object?.SurfaceText
                ?? question.Qualifier?.SurfaceText
                ?? "that entity";

            if (question.Wh == WhKind.Where)
            {
                string predicate = string.IsNullOrWhiteSpace(question.PredicateLemma)
                    ? "is"
                    : ThirdPerson(question.PredicateLemma);
                return $"I don't know where {subject} {predicate} yet.";
            }

            if (question.Wh == WhKind.When)
            {
                string predicate = string.IsNullOrWhiteSpace(question.PredicateLemma)
                    ? "happens"
                    : ThirdPerson(question.PredicateLemma);
                return $"I don't know when {subject} {predicate} yet.";
            }

            if (!question.AnchorNodeID.HasValue)
                return $"I don't know anything about {subject} yet.";

            return $"I don't have a matching fact about {subject} yet.";
        }

        private static NlpAnswer CreateSynthesizedAnswer(
            QuestionAnalysis question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            IReadOnlyList<AnswerCandidate> candidates,
            LRD_Core core,
            NlpOptions options,
            ConversationContext? conversation,
            List<string> diagnostics)
        {
            List<GraphPath> paths = EvidencePathsFor(candidates, evaluations, options, core).ToList();
            List<string> labels = candidates.Select(candidate => candidate.Label).ToList();
            string shortAnswer = FormatConjunction(labels);
            List<string> evidenceSentences = candidates
                .Select(candidate => FindEvidencePath(candidate, evaluations, core))
                .Where(path => path is not null)
                .Select(path => BuildEvidenceSentence(path!, core, question.Tense)!)
                .Where(sentence => !string.IsNullOrWhiteSpace(sentence))
                .ToList();

            string? synthesizedEvidence = TrySynthesizeEvidence(candidates, evidenceSentences);
            string text = options.IncludeEvidenceSentence
                ? synthesizedEvidence ?? shortAnswer + "."
                : shortAnswer;
            bool usesTemporaryEvidence = candidates.Any(candidate => candidate.UsesTemporaryEvidence);
            bool usesSessionProvenance = paths.Any(path => UsesSessionProvenance(path, conversation, options));

            if (usesSessionProvenance)
            {
                text = AppendSessionCaveat(text, options, diagnostics);
                diagnostics.Add("Answer evidence was provided in this session.");
            }
            else if (usesTemporaryEvidence)
            {
                text += temporaryCaveat;
                diagnostics.Add("The synthesized answer includes temporary evidence.");
            }

            diagnostics.Add($"Synthesized {candidates.Count} distinct answer candidates.");

            return new NlpAnswer(
                AnswerStatus.Answered,
                DetermineAnswerKind(question, candidates[0]),
                TrimText(text, options.MaxAnswerTextLength, diagnostics),
                shortAnswer,
                null,
                candidates,
                paths,
                question,
                evaluations.FirstOrDefault()?.Result,
                usesTemporaryEvidence,
                candidates.Max(candidate => candidate.Score),
                Deduplicate(diagnostics),
                usesSessionProvenance);
        }

        private static string? TrySynthesizeEvidence(
            IReadOnlyList<AnswerCandidate> candidates,
            IReadOnlyList<string> sentences)
        {
            if (sentences.Count != candidates.Count || candidates.Count < 2)
                return null;

            List<string> prefixes = new List<string>();
            List<string> suffixes = new List<string>();

            for (int index = 0; index < candidates.Count; index++)
            {
                string sentence = sentences[index].Trim();
                string label = candidates[index].Label;
                int labelIndex = sentence.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (labelIndex < 0)
                    return null;

                prefixes.Add(sentence[..labelIndex]);
                suffixes.Add(sentence[(labelIndex + label.Length)..]);
            }

            if (prefixes.Skip(1).Any(prefix => !string.Equals(prefix, prefixes[0], StringComparison.OrdinalIgnoreCase))
                || suffixes.Skip(1).Any(suffix => !string.Equals(suffix, suffixes[0], StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            string suffix = suffixes[0];
            if (string.IsNullOrEmpty(prefixes[0]))
                suffix = PluralizePredicate(suffix);

            return prefixes[0] + FormatConjunction(candidates.Select(candidate => candidate.Label).ToList())
                + suffix;
        }

        private static string PluralizePredicate(string suffix)
        {
            foreach (string article in new[] { " is a ", " is an " })
            {
                if (suffix.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                {
                    string nounPhrase = suffix[article.Length..];
                    string trailing = nounPhrase.EndsWith(".", StringComparison.Ordinal) ? "." : string.Empty;
                    string noun = trailing.Length > 0 ? nounPhrase[..^1] : nounPhrase;
                    return " are " + PluralizeNoun(noun) + trailing;
                }
            }

            (string Singular, string Plural)[] pairs =
            {
                (" is ", " are "),
                (" has ", " have "),
                (" does ", " do "),
                (" works ", " work "),
                (" lives ", " live "),
                (" goes ", " go "),
                (" was ", " were ")
            };

            foreach ((string singular, string plural) in pairs)
            {
                if (suffix.StartsWith(singular, StringComparison.OrdinalIgnoreCase))
                    return plural + suffix[singular.Length..];
            }

            int verbStart = 0;
            while (verbStart < suffix.Length && char.IsWhiteSpace(suffix[verbStart]))
                verbStart++;

            int verbEnd = verbStart;
            while (verbEnd < suffix.Length && char.IsLetter(suffix[verbEnd]))
                verbEnd++;

            if (verbEnd > verbStart)
            {
                string verb = suffix[verbStart..verbEnd];
                string lemma = Lemmatizer.Lemmatize(verb.ToLowerInvariant(), PartOfSpeech.Verb);
                if (!string.Equals(lemma, verb, StringComparison.OrdinalIgnoreCase))
                {
                    return suffix[..verbStart]
                        + lemma
                        + suffix[verbEnd..];
                }
            }

            return suffix;
        }

        private static string PluralizeNoun(string noun)
        {
            if (string.Equals(noun, "person", StringComparison.OrdinalIgnoreCase))
                return "people";

            if (noun.EndsWith("y", StringComparison.OrdinalIgnoreCase)
                && noun.Length > 1
                && !"aeiou".Contains(noun[^2], StringComparison.OrdinalIgnoreCase))
            {
                return noun[..^1] + "ies";
            }

            if (noun.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                || noun.EndsWith("x", StringComparison.OrdinalIgnoreCase)
                || noun.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
                || noun.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
            {
                return noun + "es";
            }

            return noun + "s";
        }

        private static string FormatConjunction(IReadOnlyList<string> values)
        {
            string[] distinct = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return distinct.Length switch
            {
                0 => string.Empty,
                1 => distinct[0],
                2 => distinct[0] + " and " + distinct[1],
                _ => string.Join(", ", distinct.Take(distinct.Length - 1)) + ", and " + distinct[^1]
            };
        }

        private static bool UsesSessionProvenance(
            GraphPath? path,
            ConversationContext? conversation,
            NlpOptions options)
        {
            return conversation is not null
                && options.EnableSessionProvenance
                && options.UseSessionProvenanceInAnswers
                && conversation.FindProvenance(path).Count > 0;
        }

        private static string AppendSessionCaveat(
            string baseText,
            NlpOptions options,
            List<string> diagnostics)
        {
            int baseLimit = Math.Max(1, options.MaxAnswerTextLength - sessionCaveat.Length);
            return TrimText(baseText, baseLimit, diagnostics) + sessionCaveat;
        }

        private static GraphPath? FindEvidencePath(
            AnswerCandidate selected,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core)
        {
            foreach (QuestionEvaluation evaluation in evaluations)
            {
                foreach (GraphPath path in evaluation.Result.ResponsePaths)
                {
                    if (path.NodeIDs.Count >= 2
                        && path.NodeIDs[path.NodeIDs.Count - 1] == selected.NodeID
                        && path.NodeIDs.SequenceEqual(selected.PathNodeIDs))
                    {
                        return path;
                    }
                }
            }

            if (selected.PathNodeIDs.Count < 2)
                return null;

            List<Link> links = new List<Link>();
            for (int index = 1; index < selected.PathNodeIDs.Count; index++)
            {
                Guid firstID = selected.PathNodeIDs[index - 1];
                Guid secondID = selected.PathNodeIDs[index];
                Link? link = core.GetStoredLinks(firstID)
                    .Concat(core.GetStoredLinks(secondID))
                    .FirstOrDefault(candidate =>
                        (candidate.SourceID == firstID && candidate.DestinationID == secondID)
                        || (candidate.SourceID == secondID && candidate.DestinationID == firstID));
                if (link is null)
                    return null;
                links.Add(link);
            }

            return new GraphPath(
                selected.PathNodeIDs,
                links,
                selected.Score,
                1.0,
                selected.Depth);
        }

        private static IReadOnlyList<GraphPath> EvidencePathsFor(
            IReadOnlyList<AnswerCandidate> candidates,
            IReadOnlyList<QuestionEvaluation> evaluations,
            NlpOptions options,
            LRD_Core core,
            bool includeAllCandidates = false)
        {
            List<GraphPath> paths = new List<GraphPath>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (AnswerCandidate candidate in candidates)
            {
                if (!includeAllCandidates && paths.Count >= Math.Max(1, options.MaxAnswerCandidates))
                    break;

                GraphPath? match = FindEvidencePath(candidate, evaluations, core);
                if (match is not null && seen.Add(string.Join(">", match.NodeIDs)))
                    paths.Add(match);
            }

            return paths;
        }

        private static void ExtractCandidates(
            QuestionAnalysis question,
            QuestionEvaluation evaluation,
            LRD_Core core,
            NlpOptions options,
            List<AnswerCandidate> immediate,
            List<AnswerCandidate> deeper,
            List<string> diagnostics)
        {
            QuestionVariant variant = evaluation.Variant;
            Chain_Node? anchorNode = variant.Chain.Last?.Value;

            if (anchorNode is null)
                return;

            Guid anchorID = anchorNode.ID;

            foreach (GraphPath path in evaluation.Result.ResponsePaths)
            {
                if (path.NodeIDs.Count < 2 || path.NodeIDs[0] != anchorID)
                    continue;

                Link link = path.Links[0];

                if (!TryMatchRelation(question, variant, link, anchorID, out bool exact, out bool inverse, out bool classOnly))
                    continue;

                for (int index = 1; index < path.NodeIDs.Count; index++)
                {
                    if (!TryCreateCandidate(
                        question,
                        variant,
                        path,
                        index,
                        core,
                        exact,
                        inverse,
                        classOnly,
                        out AnswerCandidate? candidate))
                    {
                        continue;
                    }

                    if (index == 1)
                        immediate.Add(candidate!);
                    else
                        deeper.Add(candidate!);
                }
            }

            if (evaluation.Result.Status == ReasoningStatus.Ambiguous
                && evaluation.Result.ResponsePaths.Count == 0)
            {
                Guid answerQueryID = variant.Chain.First!.Value.ID;
                foreach (SubgraphMatch match in evaluation.Result.Matches)
                {
                    if (!match.NodeMap.TryGetValue(anchorID, out Guid matchedAnchorID)
                        || !match.NodeMap.TryGetValue(answerQueryID, out Guid candidateID)
                        || matchedAnchorID == candidateID)
                    {
                        continue;
                    }

                    Link? matchedLink = match.MatchedLinks.FirstOrDefault(link =>
                        (link.SourceID == matchedAnchorID && link.DestinationID == candidateID)
                        || (link.SourceID == candidateID && link.DestinationID == matchedAnchorID));
                    if (matchedLink is null
                        || !TryMatchRelation(
                            question,
                            variant,
                            matchedLink,
                            matchedAnchorID,
                            out bool exact,
                            out bool inverse,
                            out bool classOnly))
                    {
                        continue;
                    }

                    GraphPath path = new GraphPath(
                        new[] { matchedAnchorID, candidateID },
                        new[] { matchedLink },
                        match.OverallScore,
                        1.0,
                        1);
                    if (TryCreateCandidate(
                        question,
                        variant,
                        path,
                        1,
                        core,
                        exact,
                        inverse,
                        classOnly,
                        out AnswerCandidate? candidate))
                    {
                        immediate.Add(candidate!);
                    }
                }
            }
        }

        private static bool TryMatchRelation(
            QuestionAnalysis question,
            QuestionVariant variant,
            Link link,
            Guid anchorID,
            out bool exact,
            out bool inverse,
            out bool classOnly)
        {
            exact = false;
            inverse = false;
            classOnly = false;

            if (variant.IsAnchorOnly)
            {
                classOnly = link.Classification == variant.LinkClass;
                return classOnly;
            }

            bool idMatches = link.ForwardLink.Id == variant.ForwardLinkTypeId
                || link.InverseLink.Id == variant.InverseLinkTypeId;

            if (idMatches)
            {
                if (link.SourceID == anchorID)
                    inverse = true;
                else
                    exact = true;

                return true;
            }

            if (link.Classification == variant.LinkClass
                && question.Type is QuestionType.WhQualifier or QuestionType.WhQuantity)
            {
                classOnly = true;
                return true;
            }

            return false;
        }

        private static bool TryCreateCandidate(
            QuestionAnalysis question,
            QuestionVariant variant,
            GraphPath path,
            int index,
            LRD_Core core,
            bool exact,
            bool inverse,
            bool classOnly,
            out AnswerCandidate? candidate)
        {
            candidate = null;

            Guid nodeID = path.NodeIDs[index];

            if (nodeID == path.NodeIDs[0])
                return false;

            if (!core.TryGetNode(nodeID, out Web_Node? node) || node is null)
                return false;

            string label = ResolveLabel(node);

            if (string.IsNullOrWhiteSpace(label))
                return false;

            NodeTypes nodeType = node.Get_NodeType();

            if (IsBlockedType(nodeType, variant))
                return false;

            bool exactType = nodeType == variant.ExpectedAnswerType;
            bool relaxedType = false;

            if (!exactType)
            {
                bool relaxedAllowed = variant.IsAnchorOnly
                    || question.Type is QuestionType.WhQualifier or QuestionType.WhQuantity;

                if (!relaxedAllowed)
                    return false;

                relaxedType = true;
            }

            double score = path.Score;

            if (exact)
                score += 0.20;
            else if (inverse)
                score += 0.10;
            else if (classOnly)
                score += 0.05;

            if (exactType)
                score += 0.10;
            else if (relaxedType)
                score -= 0.10;

            int depth = index;

            if (depth == 1)
                score += 0.10;
            else
                score -= 0.05 * (depth - 1);

            if (!string.IsNullOrWhiteSpace(label))
                score += 0.05;

            score = Math.Clamp(score, 0.0, 1.0);

            bool usesTemporaryEvidence = PathIsTemporary(path, core);
            int relationLinkTypeId = inverse ? variant.InverseLinkTypeId : variant.ForwardLinkTypeId;

            candidate = new AnswerCandidate(
                nodeID,
                label,
                nodeType,
                score,
                depth,
                relationLinkTypeId,
                path.Links[0].Classification,
                path.NodeIDs,
                usesTemporaryEvidence);

            return true;
        }

        private static bool IsBlockedType(NodeTypes type, QuestionVariant variant)
        {
            if (type == NodeTypes.Empties || type == NodeTypes.Dictionaries || type == NodeTypes.Sources)
                return true;

            return type == NodeTypes.Basics && variant.ExpectedAnswerType != NodeTypes.Basics;
        }

        private static bool PathIsTemporary(GraphPath path, LRD_Core core)
        {
            if (path.NodeIDs.Count == 0)
                return false;

            foreach (Guid nodeID in path.NodeIDs)
            {
                if (!core.TryGetNode(nodeID, out Web_Node? node) || node is null || !node.IsTemporary)
                    return false;
            }

            return true;
        }

        private static string ResolveLabel(Web_Node node)
        {
            if (node.Get_NodeType() == NodeTypes.Events)
                return EventNodeHelper.GetDisplayName(node);

            if (!string.IsNullOrWhiteSpace(node.Label))
                return node.Label!;

            string? alias = node.Aliases.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            return alias ?? string.Empty;
        }

        private static NlpAnswer CreateAmbiguousAnswer(
            QuestionAnalysis question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            IReadOnlyList<AnswerCandidate> candidates,
            List<string> diagnostics,
            LRD_Core core,
            NlpOptions options)
        {
            diagnostics.Add(
                $"Multiple answers are within the ambiguity margin of {options.AnswerAmbiguityMargin:F2}.");

            List<string> labels = candidates
                .Select(candidate => candidate.Label)
                .Distinct(StringComparer.Ordinal)
                .Take(Math.Max(1, options.MaxAnswerCandidates))
                .ToList();

            string text = labels.Count > 0
                ? "I found multiple possible answers: " + string.Join(", ", labels) + "."
                : "I found multiple possible answers, so I can't decide between them.";

            return new NlpAnswer(
                AnswerStatus.Ambiguous,
                question.ExpectedAnswer,
                TrimText(text, options.MaxAnswerTextLength, diagnostics),
                null,
                null,
                candidates,
                EvidencePathsFor(candidates, evaluations, options, core),
                question,
                evaluations.FirstOrDefault()?.Result,
                false,
                0.50,
                Deduplicate(diagnostics));
        }

        private static AnswerKind DetermineAnswerKind(
            QuestionAnalysis question,
            AnswerCandidate candidate)
        {
            if (question.Type == QuestionType.YesNo)
                return AnswerKind.Boolean;

            return question.Wh switch
            {
                WhKind.Where => AnswerKind.Location,
                WhKind.When => AnswerKind.Time,
                WhKind.How when question.Type == QuestionType.WhQuantity => AnswerKind.Quantity,
                WhKind.Who or WhKind.Whom or WhKind.Whose => AnswerKind.Entity,
                _ => DetermineKindFromType(candidate.NodeType)
            };
        }

        private static AnswerKind DetermineKindFromType(NodeTypes type)
        {
            if (type == NodeTypes.Entities || type == NodeTypes.Names || type == NodeTypes.Objects)
                return AnswerKind.Entity;

            if (type == NodeTypes.Locations)
                return AnswerKind.Location;

            if (type == NodeTypes.Events || type == NodeTypes.Concepts)
                return AnswerKind.Description;

            return AnswerKind.Description;
        }

        private static bool IsBooleanVerified(QuestionEvaluation evaluation)
        {
            if (evaluation.Result.Status != ReasoningStatus.Answered
                || evaluation.Result.SelectedMatch is null)
            {
                return false;
            }

            return evaluation.Result.SelectedMatch.MatchedLinks.Any(link =>
                link.ForwardLink.Id == evaluation.Variant.ForwardLinkTypeId
                || link.InverseLink.Id == evaluation.Variant.InverseLinkTypeId);
        }

        private static string? BuildEvidenceSentence(
            GraphPath? path,
            LRD_Core core,
            Tense tense)
        {
            if (path is null || path.NodeIDs.Count < 2 || path.Links.Count == 0)
                return null;

            Link link = path.Links[0];

            if (!core.TryGetNode(link.SourceID, out Web_Node? sourceNode) || sourceNode is null)
                return null;

            if (!core.TryGetNode(link.DestinationID, out Web_Node? destinationNode) || destinationNode is null)
                return null;

            string source = ResolveLabel(sourceNode);
            string destination = ResolveLabel(destinationNode);

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination))
                return null;

            string relation = link.ForwardLink.Name;

            if (relation.StartsWith("inverse_", StringComparison.OrdinalIgnoreCase))
                relation = relation["inverse_".Length..];

            if (string.Equals(relation, "has_property", StringComparison.OrdinalIgnoreCase))
                return $"{source} is {destination}.";

            if (string.Equals(relation, "is_a", StringComparison.OrdinalIgnoreCase))
                return $"{source} is {IndefiniteArticle(destination)} {destination}.";

            if (string.Equals(relation, "part_of", StringComparison.OrdinalIgnoreCase))
                return $"{source} is part of {destination}.";

            if (string.Equals(relation, "has_location", StringComparison.OrdinalIgnoreCase))
                return $"{source} is located in {destination}.";

            if (string.Equals(relation, "participated_in", StringComparison.OrdinalIgnoreCase))
                return $"{source} participated in {destination}.";

            if (link.Classification == LinkClass.identities)
            {
                if (destinationNode.Get_NodeType() == NodeTypes.Concepts)
                    return $"{source} is {IndefiniteArticle(destination)} {destination}.";

                return $"{source} is {destination}.";
            }

            if (prepositionRelations.Contains(relation))
                return $"{source} is {relation} {destination}.";

            string verbPhrase = BuildVerbPhrase(relation, tense);
            return $"{source} {verbPhrase} {destination}.";
        }

        private static string BuildVerbPhrase(string relation, Tense tense)
        {
            string[] segments = relation.Split(
                '_',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length == 0)
                return "is";

            string baseVerb = segments[0];
            string particles = segments.Length > 1
                ? " " + string.Join(" ", segments.Skip(1))
                : string.Empty;

            if (tense == Tense.Future)
                return "will " + baseVerb + particles;

            if (baseVerb is "is" or "has" or "does")
                return baseVerb + particles;

            if (tense == Tense.Past)
                return PastTense(baseVerb) + particles;

            return ThirdPerson(baseVerb) + particles;
        }

        private static string IndefiniteArticle(string phrase)
        {
            string firstWord = phrase
                .TrimStart()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;
            string lower = firstWord.ToLowerInvariant();

            if (lower.StartsWith("honest", StringComparison.Ordinal)
                || lower.StartsWith("honor", StringComparison.Ordinal)
                || lower.StartsWith("hour", StringComparison.Ordinal)
                || lower.StartsWith("heir", StringComparison.Ordinal))
            {
                return "an";
            }

            if (lower.StartsWith("uni", StringComparison.Ordinal)
                || lower.StartsWith("use", StringComparison.Ordinal)
                || lower.StartsWith("user", StringComparison.Ordinal)
                || lower.StartsWith("euro", StringComparison.Ordinal)
                || lower.StartsWith("one", StringComparison.Ordinal))
            {
                return "a";
            }

            return lower.Length > 0 && "aeiou".Contains(lower[0], StringComparison.Ordinal)
                ? "an"
                : "a";
        }

        private static string ThirdPerson(string verb)
        {
            if (verb == "have")
                return "has";

            if (verb == "do")
                return "does";

            if (verb == "be")
                return "is";

            if (verb.EndsWith("s", StringComparison.Ordinal)
                || verb.EndsWith("x", StringComparison.Ordinal)
                || verb.EndsWith("z", StringComparison.Ordinal)
                || verb.EndsWith("ch", StringComparison.Ordinal)
                || verb.EndsWith("sh", StringComparison.Ordinal))
            {
                return verb + "es";
            }

            if (verb.Length > 1
                && verb.EndsWith("y", StringComparison.Ordinal)
                && !"aeiou".Contains(verb[^2], StringComparison.OrdinalIgnoreCase))
            {
                return verb[..^1] + "ies";
            }

            return verb + "s";
        }

        private static string PastTense(string verb)
        {
            if (irregularPast.TryGetValue(verb, out string? irregular))
                return irregular;

            if (verb.EndsWith("e", StringComparison.Ordinal))
                return verb + "d";

            if (verb.Length > 1
                && verb.EndsWith("y", StringComparison.Ordinal)
                && !"aeiou".Contains(verb[^2], StringComparison.OrdinalIgnoreCase))
            {
                return verb[..^1] + "ied";
            }

            return verb + "ed";
        }

        private static string TrimText(
            string text,
            int maxLength,
            List<string> diagnostics)
        {
            int limit = Math.Max(1, maxLength);

            if (text.Length <= limit)
                return text;

            diagnostics.Add("The answer was truncated to the maximum text length.");

            if (limit <= 3)
                return text[..limit];

            return text[..(limit - 3)] + "...";
        }

        private static IReadOnlyList<string> Deduplicate(List<string> diagnostics)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<string> result = new List<string>(diagnostics.Count);

            foreach (string diagnostic in diagnostics)
            {
                if (!string.IsNullOrWhiteSpace(diagnostic) && seen.Add(diagnostic))
                    result.Add(diagnostic);
            }

            return result;
        }
    }
}
