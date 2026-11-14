using OPEN_LRD.Nodes;

namespace OPEN_LRD.Maintenance
{
    internal static class MaintenanceReasoningEngine
    {
        public static void Run(LRD_Core core, MaintenanceOptions options, MaintenanceReport report)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(report);

            if (!options.EnableReasoningMaintenance)
                return;

            if (options.EnableNodeMerging)
                OverlappingNodeConsolidator.Consolidate(core, options, report);

            HashSet<int> generatedRelationIds = new HashSet<int>();
            if (options.EnableNodeMetadataReasoning)
                NodeMetadataReasoner.RunMaintenance(core, options, report, generatedRelationIds);

            if (options.EnableCompositionalInduction || options.EnableFrequentPatternAbstraction)
            {
                LinkTypeGenerator.Generate(core, options, report, generatedRelationIds);
            }

            if (options.EnableConceptRelationElevation && generatedRelationIds.Count > 0)
                LinkTypeGenerator.ElevateRelations(core, generatedRelationIds, report, options);

            if (options.EnableSupersededLinkPruning)
                SupersededLinkPruner.Prune(core, options, report);
        }
    }
}
