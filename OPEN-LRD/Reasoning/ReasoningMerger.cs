using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    internal static class ReasoningMerger
    {
        public static MergePlan BuildPlan(
            LRD_Core core,
            QueryGraph query,
            SubgraphMatch match,
            GraphSnapshot snapshot,
            TrustScale trust,
            TrustAssessment proposedTrust,
            ReasoningOptions options,
            Guid? provenanceSourceID,
            Guid? topicNodeID)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(match);
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(trust);
            ArgumentNullException.ThrowIfNull(proposedTrust);
            ArgumentNullException.ThrowIfNull(options);

            MergePlan plan = new MergePlan();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (proposedTrust.Decision == TrustDecision.Discredited)
                plan.ValidationErrors.Add("A discredited source cannot create or promote nodes or links.");

            foreach (QueryNode queryNode in query.Nodes)
            {
                if (match.NodeMap.TryGetValue(queryNode.QueryNodeID, out Guid graphNodeID))
                {
                    if (!core.TryGetNode(graphNodeID, out Web_Node? existingNode) || existingNode is null)
                    {
                        plan.ValidationErrors.Add($"Mapped graph node '{graphNodeID}' does not exist.");
                        continue;
                    }

                    plan.QueryNodeToGraphNode[queryNode.QueryNodeID] = graphNodeID;

                    NodeActionKind kind = proposedTrust.Decision == TrustDecision.Trusted
                        && existingNode.IsTemporary
                        ? NodeActionKind.PromoteExistingNode
                        : NodeActionKind.ReuseExistingNode;

                    plan.NodeActions.Add(new NodeAction(
                        kind,
                        queryNode.QueryNodeID,
                        graphNodeID,
                        existingNode.Get_NodeType(),
                        queryNode.Label,
                        queryNode.Aliases.ToArray()));

                    Guid trustSourceID = proposedTrust.SourceNodeID != Guid.Empty
                        ? proposedTrust.SourceNodeID
                        : provenanceSourceID ?? Guid.Empty;

                    if (trustSourceID != Guid.Empty)
                    {
                        plan.TrustActions.Add(new TrustAction(
                            TrustActionKind.RecordVerified,
                            trustSourceID,
                            proposedTrust.TopicNodeID,
                            graphNodeID,
                            1.0,
                            now));
                    }

                    continue;
                }

                if (proposedTrust.Decision == TrustDecision.Discredited)
                {
                    plan.NodeActions.Add(new NodeAction(
                        NodeActionKind.RejectNode,
                        queryNode.QueryNodeID,
                        Guid.Empty,
                        queryNode.Type,
                        queryNode.Label,
                        queryNode.Aliases.ToArray(),
                        "The source is discredited."));

                    continue;
                }

                Guid newGraphNodeID;

                do
                {
                    newGraphNodeID = Guid.NewGuid();
                }
                while (newGraphNodeID == Guid.Empty
                    || core.GetExistingNode(newGraphNodeID) is not null
                    || plan.QueryNodeToGraphNode.ContainsValue(newGraphNodeID));

                plan.QueryNodeToGraphNode[queryNode.QueryNodeID] = newGraphNodeID;
                plan.NodeActions.Add(new NodeAction(
                    NodeActionKind.CreateTemporaryNode,
                    queryNode.QueryNodeID,
                    newGraphNodeID,
                    queryNode.Type,
                    queryNode.Label,
                    queryNode.Aliases.ToArray()));
            }

            foreach (QueryEdge queryEdge in query.Edges)
            {
                if (!plan.QueryNodeToGraphNode.TryGetValue(queryEdge.FromQueryNodeID, out Guid fromGraphNodeID)
                    || !plan.QueryNodeToGraphNode.TryGetValue(queryEdge.ToQueryNodeID, out Guid toGraphNodeID))
                {
                    continue;
                }

                Link? existingLink = FindExistingLink(snapshot, queryEdge, fromGraphNodeID, toGraphNodeID);

                if (existingLink is not null)
                {
                    plan.EdgeActions.Add(new EdgeAction(
                        EdgeActionKind.KeepExistingLink,
                        fromGraphNodeID,
                        toGraphNodeID,
                        queryEdge.Classification,
                        queryEdge.ForwardLinkType,
                        queryEdge.InverseLinkType,
                        existingLink));

                    continue;
                }

                Link proposedLink = CreateProposedLink(queryEdge, fromGraphNodeID, toGraphNodeID);
                IReadOnlyList<Link> outgoing = snapshot.GetOutgoingLinks(proposedLink.SourceID);

                ContradictionResolution contradiction = ContradictionResolver.Resolve(
                    proposedLink,
                    queryEdge.Classification,
                    outgoing,
                    proposedTrust,
                    link => CreateExistingLinkTrust(link, topicNodeID, options),
                    options);

                plan.Conflicts.AddRange(contradiction.Conflicts);

                if (!contradiction.Accepted)
                {
                    plan.EdgeActions.Add(new EdgeAction(
                        EdgeActionKind.RejectLink,
                        fromGraphNodeID,
                        toGraphNodeID,
                        queryEdge.Classification,
                        queryEdge.ForwardLinkType,
                        queryEdge.InverseLinkType,
                        proposedLink,
                        "The proposed functional link lost trust resolution."));

                    continue;
                }

                foreach (Link invalidatedLink in contradiction.InvalidatedLinks)
                {
                    if (plan.InvalidatedLinks.Contains(invalidatedLink))
                        continue;

                    plan.InvalidatedLinks.Add(invalidatedLink);
                    plan.EdgeActions.Add(new EdgeAction(
                        EdgeActionKind.MarkExistingLinkInvalid,
                        invalidatedLink.SourceID,
                        invalidatedLink.DestinationID,
                        invalidatedLink.Classification,
                        invalidatedLink.ForwardLink.Id,
                        invalidatedLink.InverseLink.Id,
                        invalidatedLink));
                }

                plan.EdgeActions.Add(new EdgeAction(
                    EdgeActionKind.AddDirectedLink,
                    proposedLink.SourceID,
                    proposedLink.DestinationID,
                    proposedLink.Classification,
                    proposedLink.ForwardLink.Id,
                    proposedLink.InverseLink.Id));
            }

            ValidatePlan(core, query, plan, snapshot, proposedTrust, options);

            return plan;
        }

        public static MergeApplyResult Apply(
            LRD_Core core,
            MergePlan plan,
            TrustScale trust,
            TrustAssessment proposedTrust,
            ReasoningOptions options)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(trust);
            ArgumentNullException.ThrowIfNull(proposedTrust);
            ArgumentNullException.ThrowIfNull(options);

            if (!plan.CanApply)
                return new MergeApplyResult(
                    Array.Empty<Guid>(),
                    Array.Empty<Link>(),
                    Array.Empty<Link>(),
                    plan.ValidationErrors.ToArray());

            List<Guid> promotedNodeIDs = new List<Guid>();
            List<Link> addedLinks = new List<Link>();
            List<Link> invalidatedLinks = new List<Link>();
            List<string> diagnostics = new List<string>();

            foreach (NodeAction action in plan.NodeActions)
            {
                switch (action.Kind)
                {
                    case NodeActionKind.ReuseExistingNode:
                        MergeExistingNode(core, action, proposedTrust);
                        break;

                    case NodeActionKind.PromoteExistingNode:
                        if (core.TryGetNode(action.GraphNodeID, out Web_Node? promoteNode) && promoteNode is not null)
                        {
                            MergeExistingNode(core, action, proposedTrust);

                            if (proposedTrust.Decision == TrustDecision.Trusted)
                            {
                                promoteNode.PromoteToPermanent();
                                promotedNodeIDs.Add(promoteNode.ID);
                            }
                        }
                        break;

                    case NodeActionKind.CreateTemporaryNode:
                        if (core.GetExistingNode(action.GraphNodeID) is not null)
                        {
                            diagnostics.Add($"Node '{action.GraphNodeID}' already exists and was not created.");
                            continue;
                        }

                        Web_Node newNode = new Web_Node(
                            action.GraphNodeID,
                            action.Type,
                            NodeLifetime.Temporary,
                            options.TemporaryNodeTimeToLive);

                        newNode.Set_Label(action.Label);

                        foreach (string alias in action.Aliases)
                            newNode.Add_Alias(alias);

                        if (core.AddExistingNode(newNode) && proposedTrust.Decision == TrustDecision.Trusted)
                        {
                            newNode.PromoteToPermanent();
                            promotedNodeIDs.Add(newNode.ID);
                        }

                        break;

                    case NodeActionKind.RejectNode:
                        diagnostics.Add(action.RejectionReason ?? $"Query node '{action.QueryNodeID}' was rejected.");
                        break;
                }
            }

            foreach (EdgeAction action in plan.EdgeActions)
            {
                switch (action.Kind)
                {
                    case EdgeActionKind.AddDirectedLink:
                        if (core.TryGetNode(action.SourceGraphNodeID, out Web_Node? sourceNode)
                            && sourceNode is not null
                            && core.TryGetNode(action.DestinationGraphNodeID, out Web_Node? destinationNode)
                            && destinationNode is not null)
                        {
                            Link link = new Link(
                                action.SourceGraphNodeID,
                                action.DestinationGraphNodeID,
                                action.ForwardLinkType,
                                action.InverseLinkType,
                                action.Classification);

                            if (!sourceNode.Get_ReferenceLinks().Contains(link))
                            {
                                sourceNode.Add_Link(link);
                                addedLinks.Add(link);
                            }
                        }
                        else
                        {
                            diagnostics.Add(
                                $"Could not add link '{action.SourceGraphNodeID}' -> '{action.DestinationGraphNodeID}' because an endpoint is missing.");
                        }
                        break;

                    case EdgeActionKind.KeepExistingLink:
                        break;

                    case EdgeActionKind.MarkExistingLinkInvalid:
                        if (action.ExistingLink is not null && !invalidatedLinks.Contains(action.ExistingLink))
                        {
                            invalidatedLinks.Add(action.ExistingLink);

                            if (proposalCanInvalidateTarget(proposedTrust)
                                && core.TryGetNode(action.DestinationGraphNodeID, out Web_Node? losingTarget)
                                && losingTarget is not null)
                            {
                                losingTarget.RegisterInvalidReference();
                            }
                        }
                        break;

                    case EdgeActionKind.RejectLink:
                        break;
                }
            }

            foreach (TrustAction action in plan.TrustActions)
            {
                trust.RecordEvidence(
                    action.SourceNodeID,
                    action.TopicNodeID,
                    action.Kind == TrustActionKind.RecordVerified,
                    action.Timestamp);
            }

            return new MergeApplyResult(
                promotedNodeIDs,
                addedLinks,
                invalidatedLinks,
                diagnostics);
        }

        private static void MergeExistingNode(
            LRD_Core core,
            NodeAction action,
            TrustAssessment proposedTrust)
        {
            if (!core.TryGetNode(action.GraphNodeID, out Web_Node? existingNode) || existingNode is null)
                return;

            if (existingNode.Label is null && !string.IsNullOrWhiteSpace(action.Label))
                existingNode.Set_Label(action.Label);

            if (existingNode.Aliases.Count == 0)
            {
                foreach (string alias in action.Aliases)
                    existingNode.Add_Alias(alias);
            }

            if (proposedTrust.Decision == TrustDecision.Trusted)
                existingNode.RegisterVerifiedReference();
        }

        private static bool proposalCanInvalidateTarget(TrustAssessment proposedTrust)
        {
            return proposedTrust.Decision == TrustDecision.Trusted;
        }

        private static TrustAssessment CreateExistingLinkTrust(
            Link link,
            Guid? topicNodeID,
            ReasoningOptions options)
        {
            double ratioDenominator = options.TrustPriorVerified + options.TrustPriorInvalid;
            double ratio = ratioDenominator <= 0.0
                ? 0.5
                : options.TrustPriorVerified / ratioDenominator;

            return new TrustAssessment(
                link.SourceID,
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

        private static Link? FindExistingLink(
            GraphSnapshot snapshot,
            QueryEdge queryEdge,
            Guid fromGraphNodeID,
            Guid toGraphNodeID)
        {
            foreach (Link link in snapshot.Links)
            {
                bool direct = link.SourceID == fromGraphNodeID
                    && link.DestinationID == toGraphNodeID;

                bool inverse = link.SourceID == toGraphNodeID
                    && link.DestinationID == fromGraphNodeID;

                if (!direct && !inverse)
                    continue;

                if (link.Classification != queryEdge.Classification)
                    continue;

                int expectedType = direct
                    ? queryEdge.ForwardLinkType
                    : queryEdge.InverseLinkType;

                if (link.ForwardLink.Id == expectedType)
                    return link;
            }

            return null;
        }

        private static Link CreateProposedLink(
            QueryEdge queryEdge,
            Guid fromGraphNodeID,
            Guid toGraphNodeID)
        {
            if (queryEdge.IsInverse)
            {
                return new Link(
                    toGraphNodeID,
                    fromGraphNodeID,
                    queryEdge.InverseLinkType,
                    queryEdge.ForwardLinkType,
                    queryEdge.Classification);
            }

            return new Link(
                fromGraphNodeID,
                toGraphNodeID,
                queryEdge.ForwardLinkType,
                queryEdge.InverseLinkType,
                queryEdge.Classification);
        }

        private static void ValidatePlan(
            LRD_Core core,
            QueryGraph query,
            MergePlan plan,
            GraphSnapshot snapshot,
            TrustAssessment proposedTrust,
            ReasoningOptions options)
        {
            HashSet<Guid> knownNodeIDs = new HashSet<Guid>(snapshot.Nodes.Select(node => node.ID));
            HashSet<Guid> newQueryNodeIDs = new HashSet<Guid>();

            foreach (NodeAction action in plan.NodeActions)
            {
                if (action.Kind == NodeActionKind.CreateTemporaryNode)
                {
                    if (action.GraphNodeID == Guid.Empty)
                        plan.ValidationErrors.Add($"New node for query node '{action.QueryNodeID}' has an empty ID.");

                    if (!newQueryNodeIDs.Add(action.GraphNodeID) || knownNodeIDs.Contains(action.GraphNodeID))
                        plan.ValidationErrors.Add($"New node ID '{action.GraphNodeID}' is not unique.");
                    else
                        knownNodeIDs.Add(action.GraphNodeID);

                    if (proposedTrust.Decision == TrustDecision.Discredited)
                        plan.ValidationErrors.Add("A discredited source cannot create temporary nodes.");
                }
                else if (action.Kind == NodeActionKind.ReuseExistingNode
                    || action.Kind == NodeActionKind.PromoteExistingNode)
                {
                    if (!knownNodeIDs.Contains(action.GraphNodeID))
                        plan.ValidationErrors.Add($"Mapped node ID '{action.GraphNodeID}' does not exist.");
                }

                if (action.Kind != NodeActionKind.RejectNode
                    && action.GraphNodeID != Guid.Empty
                    && !plan.QueryNodeToGraphNode.ContainsKey(action.QueryNodeID))
                {
                    plan.ValidationErrors.Add($"Query node '{action.QueryNodeID}' is not mapped to a graph node.");
                }
            }

            HashSet<string> linkKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (Link link in snapshot.Links)
            {
                linkKeys.Add(GetLinkKey(link));
            }

            foreach (EdgeAction action in plan.EdgeActions)
            {
                if (action.Kind != EdgeActionKind.AddDirectedLink)
                    continue;

                if (!knownNodeIDs.Contains(action.SourceGraphNodeID)
                    || !knownNodeIDs.Contains(action.DestinationGraphNodeID))
                {
                    plan.ValidationErrors.Add(
                        $"Proposed link endpoints '{action.SourceGraphNodeID}' -> '{action.DestinationGraphNodeID}' do not exist.");
                    continue;
                }

                Link proposed = new Link(
                    action.SourceGraphNodeID,
                    action.DestinationGraphNodeID,
                    action.ForwardLinkType,
                    action.InverseLinkType,
                    action.Classification);

                if (!linkKeys.Add(GetLinkKey(proposed)))
                    plan.ValidationErrors.Add($"Duplicate link would be created: {GetLinkKey(proposed)}.");
            }

            if (query.Nodes.Count != plan.QueryNodeToGraphNode.Count)
            {
                bool hasRejectedNodes = plan.NodeActions.Any(action => action.Kind == NodeActionKind.RejectNode);

                if (!hasRejectedNodes)
                    plan.ValidationErrors.Add("Not all query nodes are mapped or explicitly rejected.");
            }

            if (options.MaxExpandedNodes < plan.NodeActions.Count)
                plan.ValidationErrors.Add("The merge plan exceeds the configured expanded-node limit.");

            if (core is null)
                plan.ValidationErrors.Add("The core graph is unavailable.");
        }

        private static string GetLinkKey(Link link)
        {
            return string.Join(
                "|",
                link.SourceID,
                link.DestinationID,
                link.Classification.Id,
                link.ForwardLink.Id,
                link.InverseLink.Id);
        }
    }
}
