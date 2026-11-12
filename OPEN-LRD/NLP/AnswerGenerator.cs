using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
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

        public static NlpAnswer Generate(
            QuestionAnalysis question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentNullException.ThrowIfNull(evaluations);
            ArgumentNullException.ThrowIfNull(core);

            options ??= new NlpOptions();

            if (question.AnchorIsAmbiguous)
                return CreateAmbiguousAnchorAnswer(question, options);

            if (!question.Supported)
                return Unsupported(question, question.Diagnostics);

            if (evaluations.Count == 0)
                return Unsupported(question, new[] { "No query could be evaluated." });

            return question.Type == QuestionType.YesNo
                ? GenerateBoolean(question, evaluations, core, options)
                : GenerateWh(question, evaluations, core, options);
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
            NlpOptions options)
        {
            string text = "The question anchor matches multiple nodes.";

            return new NlpAnswer(
                AnswerStatus.Ambiguous,
                question.ExpectedAnswer,
                text,
                null,
                null,
                Array.Empty<AnswerCandidate>(),
                Array.Empty<GraphPath>(),
                question,
                null,
                false,
                0.50,
                new[] { "The question anchor matches multiple nodes." });
        }

        private static NlpAnswer GenerateBoolean(
            QuestionAnalysis question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions options)
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

            if (verified)
            {
                status = AnswerStatus.Yes;
                shortAnswer = "Yes";
                confidence = confidence <= 0.0 ? 0.40 : confidence;
                string? evidence = options.IncludeEvidenceSentence
                    ? BuildEvidenceSentence(evidencePath, core, question.Tense)
                    : null;
                text = evidence is null ? "Yes." : "Yes. " + evidence;
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
                Deduplicate(diagnostics));
        }

        private static NlpAnswer GenerateWh(
            QuestionAnalysis question,
            IReadOnlyList<QuestionEvaluation> evaluations,
            LRD_Core core,
            NlpOptions options)
        {
            if (question.AnchorNodeID is null)
            {
                List<string> missingDiagnostics = new List<string>();

                foreach (QuestionEvaluation evaluation in evaluations)
                    missingDiagnostics.AddRange(evaluation.Result.Diagnostics);

                missingDiagnostics.Add("The question anchor is not present in the graph.");

                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    question.ExpectedAnswer,
                    "I don't know. I couldn't find a matching fact in the graph.",
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
                if (ambiguousReasoning)
                {
                    return CreateAmbiguousAnswer(
                        question,
                        evaluations,
                        Array.Empty<AnswerCandidate>(),
                        diagnostics,
                        options);
                }

                if (structuralFailure)
                    return Unsupported(question, new[] { "The query could not be evaluated." });

                diagnostics.Add("No answer candidate could be extracted from the matching reasoning result.");

                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    question.ExpectedAnswer,
                    "I don't know. I couldn't find a matching fact in the graph.",
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
                .Take(Math.Max(1, options.MaxAnswerCandidates))
                .ToList();

            if (candidates.Count == 0)
            {
                diagnostics.Add("No answer candidate passed the relation and type filters.");

                return new NlpAnswer(
                    AnswerStatus.NoMatch,
                    question.ExpectedAnswer,
                    "I don't know. I couldn't find a matching fact in the graph.",
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

            if (candidates.Count >= 2
                && candidates[0].Depth == candidates[1].Depth
                && Math.Abs(candidates[0].Score - candidates[1].Score) <= options.AnswerAmbiguityMargin)
            {
                return CreateAmbiguousAnswer(
                    question,
                    evaluations,
                    candidates,
                    diagnostics,
                    options);
            }

            AnswerCandidate selected = candidates[0];
            diagnostics.Add($"Selected answer '{selected.Label}' with score {selected.Score:F3}.");

            bool usesTemporaryEvidence = selected.UsesTemporaryEvidence;

            if (usesTemporaryEvidence)
                diagnostics.Add("All evidence for this answer is temporary.");

            GraphPath? evidencePath = FindEvidencePath(selected, evaluations);

            string? evidence = options.IncludeEvidenceSentence
                ? BuildEvidenceSentence(evidencePath, core, question.Tense)
                : null;
            string text = evidence ?? selected.Label;

            if (usesTemporaryEvidence)
                text = text + " (This fact is not yet verified.)";

            AnswerKind kind = DetermineAnswerKind(question, selected);

            return new NlpAnswer(
                AnswerStatus.Answered,
                kind,
                TrimText(text, options.MaxAnswerTextLength, diagnostics),
                selected.Label,
                selected.NodeID,
                candidates,
                EvidencePathsFor(candidates, evaluations, options),
                question,
                evaluations.FirstOrDefault()?.Result,
                usesTemporaryEvidence,
                Math.Clamp(selected.Score, 0.0, 1.0),
                Deduplicate(diagnostics));
        }

        private static GraphPath? FindEvidencePath(
            AnswerCandidate selected,
            IReadOnlyList<QuestionEvaluation> evaluations)
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

            return null;
        }

        private static IReadOnlyList<GraphPath> EvidencePathsFor(
            IReadOnlyList<AnswerCandidate> candidates,
            IReadOnlyList<QuestionEvaluation> evaluations,
            NlpOptions options)
        {
            List<GraphPath> paths = new List<GraphPath>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (AnswerCandidate candidate in candidates)
            {
                if (paths.Count >= Math.Max(1, options.MaxAnswerCandidates))
                    break;

                foreach (QuestionEvaluation evaluation in evaluations)
                {
                    GraphPath? match = evaluation.Result.ResponsePaths.FirstOrDefault(path =>
                        path.NodeIDs.Count >= 2
                        && path.NodeIDs.SequenceEqual(candidate.PathNodeIDs));

                    if (match is not null && seen.Add(string.Join(">", match.NodeIDs)))
                    {
                        paths.Add(match);
                        break;
                    }
                }
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
                EvidencePathsFor(candidates, evaluations, options),
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

            if (link.Classification == LinkClass.identities)
                return $"{source} is {destination}.";

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
