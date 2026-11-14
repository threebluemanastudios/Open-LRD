using System.Diagnostics;

namespace OPEN_LRD.Maintenance
{
    internal sealed class MaintenanceReport
    {
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly List<string> diagnostics = new List<string>();

        public int NodesExamined { get; internal set; }
        public int NodesCreated { get; internal set; }
        public int NodesRemoved { get; internal set; }
        public int NodesMerged { get; internal set; }
        public int MetadataNodesExamined { get; internal set; }
        public int MetadataValuesCreated { get; internal set; }
        public int MetadataFactsExamined { get; internal set; }
        public int MetadataFactsAdded { get; internal set; }
        public int MetadataFactsInvalidated { get; internal set; }
        public int MetadataRelationsRegistered { get; internal set; }
        public int RelationPatternsExamined { get; internal set; }
        public int RelationTypesRegistered { get; internal set; }
        public int RelationNodesCreated { get; internal set; }
        public int LinksGenerated { get; internal set; }
        public int LinksInvalidated { get; internal set; }
        public bool WorkBudgetExhausted { get; internal set; }
        public TimeSpan Elapsed { get; private set; }
        public IReadOnlyList<string> Diagnostics => diagnostics;

        internal void AddDiagnostic(string message) => diagnostics.Add(message);

        internal void Complete() => Elapsed = stopwatch.Elapsed;
    }
}
