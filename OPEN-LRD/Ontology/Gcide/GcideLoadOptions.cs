namespace OPEN_LRD.Ontology.Gcide
{
    /// <summary>Selects a portion of the bundled GCIDE corpus to load.</summary>
    internal enum GcideSubset
    {
        All,
        ByLetter,
        SemanticOnly
    }

    /// <summary>Configures corpus path, subset, limits, progress, and cancellation for GCIDE loading.</summary>
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

    /// <summary>Summarizes parsed entries, graph additions, and diagnostics from a GCIDE load.</summary>
    internal sealed record GcideLoadResult(
        int EntriesRead,
        int EntriesLoaded,
        int WordNodes,
        int SenseNodes,
        int LinksCreated,
        int SkippedRelations);
}
