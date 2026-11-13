namespace OPEN_LRD.Ontology.Gcide
{
    internal enum GcideSubset
    {
        All,
        ByLetter,
        SemanticOnly
    }

    internal sealed class GcideLoadOptions
    {
        public string CorpusDirectory { get; set; } = string.Empty;

        public GcideSubset Subset { get; set; } = GcideSubset.SemanticOnly;

        public char? Letter { get; set; }

        // Zero or negative means no cap.
        public int MaxEntries { get; set; } = 2000;

        public bool CreatePlaceholderTargets { get; set; }

        public Action<int, string>? Progress { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }

    internal sealed record GcideLoadResult(
        int EntriesRead,
        int EntriesLoaded,
        int WordNodes,
        int SenseNodes,
        int LinksCreated,
        int SkippedRelations);
}
