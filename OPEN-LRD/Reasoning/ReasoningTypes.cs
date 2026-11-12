using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    internal enum ReasoningStatus
    {
        Answered,
        NoMatch,
        Ambiguous,
        RejectedByTrust,
        Contradiction,
        InvalidInput
    }

    internal enum TrustDecision
    {
        Neutral,
        Trusted,
        Discredited
    }

    internal sealed record QueryNode(
        Guid QueryNodeID,
        NodeTypes Type,
        string? Label,
        IReadOnlyCollection<string> Aliases);

    internal sealed record QueryEdge(
        Guid FromQueryNodeID,
        Guid ToQueryNodeID,
        LinkClass Classification,
        int ForwardLinkType,
        int InverseLinkType,
        bool IsInverse);

    internal sealed record QueryGraph(
        IReadOnlyList<QueryNode> Nodes,
        IReadOnlyList<QueryEdge> Edges,
        bool IsLinear,
        Guid RootQueryNodeID);

    internal sealed record GraphPath(
        IReadOnlyList<Guid> NodeIDs,
        IReadOnlyList<Link> Links,
        double Score,
        double TrustScore,
        int Depth);

    internal sealed record LinkConflict(
        Link ExistingLink,
        Link ProposedLink,
        LinkClass Classification,
        Guid SourceGraphNodeID,
        Guid ExistingDestinationGraphNodeID,
        Guid ProposedDestinationGraphNodeID,
        double ExistingTrustScore,
        double ProposedTrustScore,
        string Resolution);

    internal sealed record SubgraphMatch(
        Guid AnchorGraphNodeID,
        IReadOnlyDictionary<Guid, Guid> NodeMap,
        IReadOnlyList<Link> MatchedLinks,
        IReadOnlyList<LinkConflict> Conflicts,
        IReadOnlyList<Guid> UnmatchedQueryNodeIDs,
        double NodeScore,
        double EdgeScore,
        double CoverageScore,
        double OverallScore,
        IReadOnlyList<string> Diagnostics);

    internal sealed record LinkEvidence(
        Link Link,
        double Confidence,
        Guid? ProvenanceSourceID,
        Guid? TopicID,
        bool IsInvalid,
        string? InvalidReason,
        DateTimeOffset? LastUpdated);

    internal readonly record struct TrustKey(Guid SourceNodeID, Guid? TopicNodeID);

    internal sealed class TrustEvidence
    {
        public double Verified { get; set; }
        public double Invalid { get; set; }
        public DateTimeOffset FirstSeen { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }

    internal sealed record TrustAssessment(
        Guid SourceNodeID,
        Guid? TopicNodeID,
        double Verified,
        double Invalid,
        double TotalEvidence,
        double Ratio,
        double Confidence,
        double Score,
        TrustDecision Decision,
        bool UsesTopicEvidence,
        bool UsesGlobalFallback);

    internal sealed record ReasoningResult(
        ReasoningStatus Status,
        QueryGraph? Query,
        SubgraphMatch? SelectedMatch,
        IReadOnlyList<SubgraphMatch> Matches,
        IReadOnlyList<GraphPath> ResponsePaths,
        LinkedList<Chain_Node>? ResponseChain,
        TrustAssessment? Trust,
        IReadOnlyList<Guid> PromotedNodeIDs,
        IReadOnlyList<LinkConflict> Conflicts,
        bool GraphMutated,
        IReadOnlyList<string> Diagnostics);

    internal enum NodeActionKind
    {
        ReuseExistingNode,
        CreateTemporaryNode,
        PromoteExistingNode,
        RejectNode
    }

    internal sealed record NodeAction(
        NodeActionKind Kind,
        Guid QueryNodeID,
        Guid GraphNodeID,
        NodeTypes Type,
        string? Label,
        IReadOnlyCollection<string> Aliases,
        string? RejectionReason = null);

    internal enum EdgeActionKind
    {
        AddDirectedLink,
        KeepExistingLink,
        RejectLink,
        MarkExistingLinkInvalid
    }

    internal sealed record EdgeAction(
        EdgeActionKind Kind,
        Guid SourceGraphNodeID,
        Guid DestinationGraphNodeID,
        LinkClass Classification,
        int ForwardLinkType,
        int InverseLinkType,
        Link? ExistingLink = null,
        string? RejectionReason = null);

    internal enum TrustActionKind
    {
        RecordVerified,
        RecordInvalid
    }

    internal sealed record TrustAction(
        TrustActionKind Kind,
        Guid SourceNodeID,
        Guid? TopicNodeID,
        Guid? TargetNodeID,
        double Increment,
        DateTimeOffset Timestamp);

    internal sealed class MergePlan
    {
        public Dictionary<Guid, Guid> QueryNodeToGraphNode { get; } = new Dictionary<Guid, Guid>();
        public List<NodeAction> NodeActions { get; } = new List<NodeAction>();
        public List<EdgeAction> EdgeActions { get; } = new List<EdgeAction>();
        public List<TrustAction> TrustActions { get; } = new List<TrustAction>();
        public List<Link> InvalidatedLinks { get; } = new List<Link>();
        public List<LinkConflict> Conflicts { get; } = new List<LinkConflict>();
        public List<string> ValidationErrors { get; } = new List<string>();

        public IReadOnlyList<LinkConflict> ConflictResolutions => Conflicts;

        public bool CanApply => ValidationErrors.Count == 0;
    }

    internal sealed record MergeApplyResult(
        IReadOnlyList<Guid> PromotedNodeIDs,
        IReadOnlyList<Link> AddedLinks,
        IReadOnlyList<Link> InvalidatedLinks,
        IReadOnlyList<string> Diagnostics);

    internal sealed record ContradictionResolution(
        bool Accepted,
        IReadOnlyList<LinkConflict> Conflicts,
        IReadOnlyList<Link> InvalidatedLinks);
}
