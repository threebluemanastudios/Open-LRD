using OPEN_LRD.Nodes;

namespace OPEN_LRD.Reasoning
{
    internal sealed class ReasoningEngine
    {
        private readonly LRD_Core core;
        private readonly ReasoningOptions options;
        private readonly TrustScale trust;
        private readonly HashSet<string> invalidLinkKeys = new HashSet<string>(StringComparer.Ordinal);

        public ReasoningEngine(LRD_Core core, ReasoningOptions? options = null)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));

            if (!core.initialized)
                throw new InvalidOperationException("The core graph must be initialized before creating a reasoning engine.");

            this.options = options ?? new ReasoningOptions();
            trust = new TrustScale(this.options);
        }

        public ReasoningResult Ingest(
            LinkedList<Chain_Node> chain,
            Guid? sourceNodeID = null,
            Guid? topicNodeID = null)
        {
            return Execute(chain, sourceNodeID, topicNodeID, merge: true, answer: false);
        }

        public ReasoningResult Answer(LinkedList<Chain_Node> query)
        {
            return Execute(query, null, null, merge: false, answer: true);
        }

        public ReasoningResult IngestAndAnswer(
            LinkedList<Chain_Node> chain,
            Guid? sourceNodeID = null,
            Guid? topicNodeID = null)
        {
            return Execute(chain, sourceNodeID, topicNodeID, merge: true, answer: true);
        }

        public TrustAssessment GetTrust(Guid sourceNodeID, Guid? topicNodeID = null)
        {
            return trust.Assess(sourceNodeID, topicNodeID);
        }

        public void RecordTrustEvidence(
            Guid sourceNodeID,
            Guid? topicNodeID,
            bool verified,
            double weight = 1.0)
        {
            if (sourceNodeID == Guid.Empty)
                throw new ArgumentException("A trust source node ID is required.", nameof(sourceNodeID));

            if (weight <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(weight), "Evidence weight must be greater than zero.");

            trust.RecordEvidenceBatch(
                sourceNodeID,
                topicNodeID,
                verified ? weight : 0.0,
                verified ? 0.0 : weight);
        }

        public IReadOnlyList<GraphPath> ExtractResponse(QueryGraph query, SubgraphMatch match)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(match);

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core, invalidLinkKeys);

            return ResponseExtractor.Extract(
                query,
                match,
                snapshot,
                trust,
                invalidLinkKeys,
                options);
        }

        private ReasoningResult Execute(
            LinkedList<Chain_Node>? chain,
            Guid? sourceNodeID,
            Guid? topicNodeID,
            bool merge,
            bool answer)
        {
            if (chain is null || chain.Count == 0)
            {
                return new ReasoningResult(
                    ReasoningStatus.InvalidInput,
                    null,
                    null,
                    Array.Empty<SubgraphMatch>(),
                    Array.Empty<GraphPath>(),
                    null,
                    null,
                    Array.Empty<Guid>(),
                    Array.Empty<LinkConflict>(),
                    false,
                    new[] { "A non-empty query chain is required." });
            }

            QueryGraph query;

            try
            {
                query = QueryGraphBuilder.Build(chain, options);
            }
            catch (ArgumentException exception)
            {
                return new ReasoningResult(
                    ReasoningStatus.InvalidInput,
                    null,
                    null,
                    Array.Empty<SubgraphMatch>(),
                    Array.Empty<GraphPath>(),
                    null,
                    null,
                    Array.Empty<Guid>(),
                    Array.Empty<LinkConflict>(),
                    false,
                    new[] { exception.Message });
            }

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core, invalidLinkKeys);
            IReadOnlyList<SubgraphMatch> matches = SubgraphMatcher.FindMatches(query, snapshot, options);
            TrustAssessment proposedTrust = GetProposedTrust(sourceNodeID, topicNodeID);
            List<string> diagnostics = new List<string>();

            diagnostics.Add($"Found {matches.Count} candidate match(es).");

            if (matches.Count == 0)
            {
                core.ClearResponse();

                return new ReasoningResult(
                    ReasoningStatus.NoMatch,
                    query,
                    null,
                    matches,
                    Array.Empty<GraphPath>(),
                    null,
                    proposedTrust,
                    Array.Empty<Guid>(),
                    Array.Empty<LinkConflict>(),
                    false,
                    diagnostics);
            }

            if (merge && proposedTrust.Decision == TrustDecision.Discredited)
            {
                diagnostics.Add(
                    $"The source is discredited (score {proposedTrust.Score:F3}, evidence {proposedTrust.TotalEvidence:F1}).");

                return new ReasoningResult(
                    ReasoningStatus.RejectedByTrust,
                    query,
                    matches[0],
                    matches,
                    Array.Empty<GraphPath>(),
                    null,
                    proposedTrust,
                    Array.Empty<Guid>(),
                    Array.Empty<LinkConflict>(),
                    false,
                    diagnostics);
            }

            bool ambiguous = matches.Count > 1
                && matches[0].OverallScore - matches[1].OverallScore <= options.AmbiguityMargin;

            if (ambiguous)
            {
                diagnostics.Add(
                    $"The top two matches are within the ambiguity margin of {options.AmbiguityMargin:F2}.");

                return new ReasoningResult(
                    ReasoningStatus.Ambiguous,
                    query,
                    null,
                    matches,
                    Array.Empty<GraphPath>(),
                    null,
                    proposedTrust,
                    Array.Empty<Guid>(),
                    Array.Empty<LinkConflict>(),
                    false,
                    diagnostics);
            }

            SubgraphMatch selected = matches[0];
            diagnostics.Add($"Selected match with score {selected.OverallScore:F3}.");

            if (merge)
            {
                MergePlan plan = ReasoningMerger.BuildPlan(
                    core,
                    query,
                    selected,
                    snapshot,
                    trust,
                    proposedTrust,
                    options,
                    sourceNodeID,
                    topicNodeID);

                if (!plan.CanApply)
                {
                    diagnostics.AddRange(plan.ValidationErrors);

                    ReasoningStatus failureStatus = proposedTrust.Decision == TrustDecision.Discredited
                        ? ReasoningStatus.RejectedByTrust
                        : ReasoningStatus.InvalidInput;

                    return new ReasoningResult(
                        failureStatus,
                        query,
                        selected,
                        matches,
                        Array.Empty<GraphPath>(),
                        null,
                        proposedTrust,
                        Array.Empty<Guid>(),
                        plan.Conflicts,
                        false,
                        diagnostics);
                }

                MergeApplyResult applyResult = ReasoningMerger.Apply(
                    core,
                    plan,
                    trust,
                    proposedTrust,
                    options);

                invalidLinkKeys.UnionWith(
                    applyResult.InvalidatedLinks.Select(GraphSnapshot.GetLinkKey));

                diagnostics.AddRange(applyResult.Diagnostics);
                diagnostics.Add(
                    $"Applied {applyResult.AddedLinks.Count} link(s), retained {applyResult.InvalidatedLinks.Count} invalid link(s), and promoted {applyResult.PromotedNodeIDs.Count} node(s).");

                bool mutated =
                    applyResult.AddedLinks.Count > 0
                    || applyResult.InvalidatedLinks.Count > 0
                    || applyResult.PromotedNodeIDs.Count > 0;

                if (answer)
                {
                    GraphSnapshot updatedSnapshot = GraphSnapshot.FromCore(core, invalidLinkKeys);
                    IReadOnlyList<GraphPath> mergedPaths = ResponseExtractor.Extract(
                        query,
                        selected,
                        updatedSnapshot,
                        trust,
                        invalidLinkKeys,
                        options);

                    LinkedList<Chain_Node>? mergedChain = BuildResponse(mergedPaths, updatedSnapshot);

                    return new ReasoningResult(
                        plan.Conflicts.Count > 0
                            ? ReasoningStatus.Contradiction
                            : ReasoningStatus.Answered,
                        query,
                        selected,
                        matches,
                        mergedPaths,
                        mergedChain,
                        proposedTrust,
                        applyResult.PromotedNodeIDs,
                        plan.Conflicts,
                        mutated,
                        diagnostics);
                }

                return new ReasoningResult(
                    plan.Conflicts.Count > 0
                        ? ReasoningStatus.Contradiction
                        : ReasoningStatus.Answered,
                    query,
                    selected,
                    matches,
                    Array.Empty<GraphPath>(),
                    null,
                    proposedTrust,
                    applyResult.PromotedNodeIDs,
                    plan.Conflicts,
                    mutated,
                    diagnostics);
            }

            if (!answer)
            {
                return new ReasoningResult(
                    ReasoningStatus.Answered,
                    query,
                    selected,
                    matches,
                    Array.Empty<GraphPath>(),
                    null,
                    proposedTrust,
                    Array.Empty<Guid>(),
                    Array.Empty<LinkConflict>(),
                    false,
                    diagnostics);
            }

            IReadOnlyList<GraphPath> paths = ResponseExtractor.Extract(
                query,
                selected,
                snapshot,
                trust,
                invalidLinkKeys,
                options);

            LinkedList<Chain_Node>? responseChain = BuildResponse(paths, snapshot);

            if (paths.Count == 0)
            {
                diagnostics.Add("No response path could be extracted.");
                core.ClearResponse();
            }

            return new ReasoningResult(
                paths.Count > 0 ? ReasoningStatus.Answered : ReasoningStatus.NoMatch,
                query,
                selected,
                matches,
                paths,
                responseChain,
                proposedTrust,
                Array.Empty<Guid>(),
                Array.Empty<LinkConflict>(),
                false,
                diagnostics);
        }

        private LinkedList<Chain_Node>? BuildResponse(
            IReadOnlyList<GraphPath> paths,
            GraphSnapshot snapshot)
        {
            if (paths.Count == 0)
            {
                core.ClearResponse();
                return null;
            }

            LinkedList<Chain_Node>? chain = ResponseExtractor.ToResponseChain(paths[0], snapshot);

            if (chain is null)
            {
                core.ClearResponse();
                return null;
            }

            core.ClearResponse();
            core.BuildResponse(chain);

            return chain;
        }

        private TrustAssessment GetProposedTrust(Guid? sourceNodeID, Guid? topicNodeID)
        {
            if (sourceNodeID.HasValue && sourceNodeID.Value != Guid.Empty)
                return trust.Assess(sourceNodeID.Value, topicNodeID);

            double ratioDenominator = options.TrustPriorVerified + options.TrustPriorInvalid;
            double ratio = ratioDenominator <= 0.0
                ? 0.5
                : options.TrustPriorVerified / ratioDenominator;

            return new TrustAssessment(
                Guid.Empty,
                topicNodeID,
                0.0,
                0.0,
                0.0,
                ratio,
                0.0,
                0.0,
                TrustDecision.Neutral,
                false,
                false);
        }
    }
}
