using OPEN_LRD.Nodes;

namespace OPEN_LRD.Maintenance
{
    internal sealed record MaintenanceOptions
    {
        public bool EnableReasoningMaintenance { get; init; }
        public bool EnableNodeMerging { get; init; } = true;
        public bool EnableNodeMetadataReasoning { get; init; } = true;
        public bool EnableCompositionalInduction { get; init; } = true;
        public bool EnableFrequentPatternAbstraction { get; init; } = true;
        public bool EnableConceptRelationElevation { get; init; } = true;
        public bool EnableSupersededLinkPruning { get; init; } = true;

        public int MinPatternFrequency { get; init; } = 5;
        public int MaxNodesToExaminePerCycle { get; init; } = 5000;
        public int MaxLinksToExaminePerCycle { get; init; } = 25000;
        public int MaxPathsToExaminePerCycle { get; init; } = 50000;
        public int MaxMergesPerCycle { get; init; } = 100;
        public int MaxLinksGeneratedPerCycle { get; init; } = 500;
        public int MaxRelationTypesGeneratedPerCycle { get; init; } = 100;
        public int MaxLinksPrunedPerCycle { get; init; } = 500;
        public int MaxTransitivePruneDepth { get; init; } = 2;
        public int MaxMetadataFactsPerNode { get; init; } = 256;
        public int MaxMetadataFactsExaminedPerCycle { get; init; } = 1280000;
        public int MaxMetadataValuesCreatedPerCycle { get; init; } = 1000;
        public int MetadataValueNodeTimeToLive { get; init; } = Node.DefaultTimeToLive;

        public void Validate()
        {
            if (MinPatternFrequency < 2)
                throw new ArgumentOutOfRangeException(nameof(MinPatternFrequency));
            if (MaxNodesToExaminePerCycle < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxNodesToExaminePerCycle));
            if (MaxLinksToExaminePerCycle < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxLinksToExaminePerCycle));
            if (MaxPathsToExaminePerCycle < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxPathsToExaminePerCycle));
            if (MaxMergesPerCycle < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxMergesPerCycle));
            if (MaxLinksGeneratedPerCycle < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxLinksGeneratedPerCycle));
            if (MaxRelationTypesGeneratedPerCycle < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxRelationTypesGeneratedPerCycle));
            if (MaxLinksPrunedPerCycle < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxLinksPrunedPerCycle));
            if (MaxTransitivePruneDepth is < 1 or > 6)
                throw new ArgumentOutOfRangeException(nameof(MaxTransitivePruneDepth));
            if (MaxMetadataFactsPerNode < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxMetadataFactsPerNode));
            if (MaxMetadataFactsExaminedPerCycle < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxMetadataFactsExaminedPerCycle));
            if (MaxMetadataValuesCreatedPerCycle < 0)
                throw new ArgumentOutOfRangeException(nameof(MaxMetadataValuesCreatedPerCycle));
            if (MetadataValueNodeTimeToLive < 1)
                throw new ArgumentOutOfRangeException(nameof(MetadataValueNodeTimeToLive));
        }
    }
}
