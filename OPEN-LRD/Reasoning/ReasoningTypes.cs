using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    /// <summary>Describes the outcome of a graph reasoning request.</summary>
    internal enum ReasoningStatus
    {
        Answered,
        NoMatch,
        Ambiguous,
        RejectedByTrust,
        Contradiction,
        InvalidInput
    }

    /// <summary>Classifies the source trust decision for proposed knowledge.</summary>
    internal enum TrustDecision
    {
        Neutral,
        Trusted,
        Discredited
    }

    /// <summary>Describes one query node and the labels used to find graph candidates.</summary>
    internal sealed record QueryNode(
        Guid QueryNodeID,
        NodeTypes Type,
        string? Label,
        IReadOnlyCollection<string> Aliases);

    /// <summary>Describes a directed or inverse query edge between query nodes.</summary>
    internal sealed record QueryEdge(
        Guid FromQueryNodeID,
        Guid ToQueryNodeID,
        LinkClass Classification,
        int ForwardLinkType,
        int InverseLinkType,
        bool IsInverse);

    /// <summary>Contains the nodes, edges, root, and shape of a validated query.</summary>
    internal sealed record QueryGraph(
        IReadOnlyList<QueryNode> Nodes,
        IReadOnlyList<QueryEdge> Edges,
        bool IsLinear,
        Guid RootQueryNodeID);

    /// <summary>Contains a ranked path through graph nodes and links.</summary>
    internal sealed record GraphPath(
        IReadOnlyList<Guid> NodeIDs,
        IReadOnlyList<Link> Links,
        double Score,
        double TrustScore,
        int Depth);

    /// <summary>Records competing existing and proposed links for a functional relationship.</summary>
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

    /// <summary>Contains a candidate graph mapping, matched links, conflicts, and quality scores.</summary>
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

    /// <summary>Stores confidence and provenance associated with a graph link.</summary>
    internal sealed record LinkEvidence(
        Link Link,
        double Confidence,
        Guid? ProvenanceSourceID,
        Guid? TopicID,
        bool IsInvalid,
        string? InvalidReason,
        DateTimeOffset? LastUpdated);

    /// <summary>Identifies trust evidence by source and optional topic.</summary>
    internal readonly record struct TrustKey(Guid SourceNodeID, Guid? TopicNodeID);

    /// <summary>Accumulates weighted verified and invalid evidence for a trust key.</summary>
    internal sealed class TrustEvidence
    {
        public double Verified { get; set; }
        public double Invalid { get; set; }
        public DateTimeOffset FirstSeen { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }

    /// <summary>Summarizes evidence totals, confidence, score, and source trust decision.</summary>
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

    /// <summary>Contains query matches, answer paths, trust results, graph changes, and diagnostics.</summary>
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
        IReadOnlyList<string> Diagnostics,
        IReadOnlyDictionary<Guid, Guid>? MergedNodeMap = null);

    /// <summary>Identifies a planned action for a node participating in a merge.</summary>
    internal enum NodeActionKind
    {
        ReuseExistingNode,
        CreateTemporaryNode,
        PromoteExistingNode,
        RejectNode
    }

    /// <summary>Describes how one query node maps to or changes a graph node.</summary>
    internal sealed record NodeAction(
        NodeActionKind Kind,
        Guid QueryNodeID,
        Guid GraphNodeID,
        NodeTypes Type,
        string? Label,
        IReadOnlyCollection<string> Aliases,
        string? RejectionReason = null);

    /// <summary>Identifies a planned action for a proposed or existing graph edge.</summary>
    internal enum EdgeActionKind
    {
        AddDirectedLink,
        KeepExistingLink,
        RejectLink,
        MarkExistingLinkInvalid
    }

    /// <summary>Describes how a proposed edge is applied, retained, rejected, or invalidated.</summary>
    internal sealed record EdgeAction(
        EdgeActionKind Kind,
        Guid SourceGraphNodeID,
        Guid DestinationGraphNodeID,
        LinkClass Classification,
        int ForwardLinkType,
        int InverseLinkType,
        Link? ExistingLink = null,
        string? RejectionReason = null);

    /// <summary>Identifies the trust evidence operation in a merge plan.</summary>
    internal enum TrustActionKind
    {
        RecordVerified,
        RecordInvalid
    }

    /// <summary>Describes a source- and topic-scoped trust update.</summary>
    internal sealed record TrustAction(
        TrustActionKind Kind,
        Guid SourceNodeID,
        Guid? TopicNodeID,
        Guid? TargetNodeID,
        double Increment,
        DateTimeOffset Timestamp);

    /// <summary>Collects validated node, edge, and trust actions before graph mutation.</summary>
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

    /// <summary>Reports graph mutations completed when a merge plan is applied.</summary>
    internal sealed record MergeApplyResult(
        IReadOnlyList<Guid> PromotedNodeIDs,
        IReadOnlyList<Link> AddedLinks,
        IReadOnlyList<Link> InvalidatedLinks,
        IReadOnlyList<string> Diagnostics);

    /// <summary>Reports whether a contradiction is accepted and which links are affected.</summary>
    internal sealed record ContradictionResolution(
        bool Accepted,
        IReadOnlyList<LinkConflict> Conflicts,
        IReadOnlyList<Link> InvalidatedLinks);
}
