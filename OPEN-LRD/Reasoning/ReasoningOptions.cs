using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    internal sealed record ReasoningOptions
    {
        public double MinimumMatchScore { get; init; } = 0.65;
        public double MinimumPartialMatchScore { get; init; } = 0.30;
        public double MinimumPartialMatchCoverage { get; init; } = 0.50;
        public double AmbiguityMargin { get; init; } = 0.05;
        public int MaxMatches { get; init; } = 10;
        public int MaxCandidateNodesPerQueryNode { get; init; } = 50;
        public int MaxSearchSteps { get; init; } = 10000;
        public int MaxTraversalDepth { get; init; } = 4;
        public int MaxResponsePaths { get; init; } = 20;
        public int MaxExpandedNodes { get; init; } = 500;

        public bool AllowTypeOnlyCandidates { get; init; }
        public bool AllowClassOnlyEdgeMatching { get; init; }

        public double ExactNodeScore { get; init; } = 1.00;
        public double AliasNodeScore { get; init; } = 0.75;
        public double TypeOnlyNodeScore { get; init; } = 0.35;
        public double ExactEdgeScore { get; init; } = 1.00;
        public double InverseEdgeScore { get; init; } = 0.90;
        public double ClassOnlyEdgeScore { get; init; } = 0.50;

        public double NodeWeight { get; init; } = 0.35;
        public double EdgeWeight { get; init; } = 0.45;
        public double CoverageWeight { get; init; } = 0.20;
        public double TrustBonusWeight { get; init; } = 0.10;
        public double WeightBonusWeight { get; init; } = 0.10;

        public double TrustPriorVerified { get; init; } = 1.0;
        public double TrustPriorInvalid { get; init; } = 1.0;
        public double TrustVolumeSmoothing { get; init; } = 5.0;
        public double MinTrustEvidence { get; init; } = 5.0;
        public double TrustedThreshold { get; init; } = 0.70;
        public double DiscreditedThreshold { get; init; } = 0.30;
        public double TrustMargin { get; init; } = 0.10;

        public IReadOnlyCollection<LinkClass> FunctionalLinkClasses { get; init; } =
            new[] { LinkClass.identities };

        public IReadOnlyDictionary<LinkClass, double> LinkClassPriority { get; init; } =
            new Dictionary<LinkClass, double>
            {
                [LinkClass.identities] = 1.00,
                [LinkClass.events] = 0.90,
                [LinkClass.temporal] = 0.80,
                [LinkClass.spatial] = 0.80,
                [LinkClass.actions] = 0.70,
                [LinkClass.sources] = 0.60,
                [LinkClass.map_locations] = 0.55,
                [LinkClass.basic] = 0.50,
                [LinkClass.empty] = 0.00
            };

        public int TemporaryNodeTimeToLive { get; init; } = Node.DefaultTimeToLive;

        public bool IsFunctional(LinkClass classification)
        {
            return FunctionalLinkClasses.Contains(classification);
        }
    }
}
