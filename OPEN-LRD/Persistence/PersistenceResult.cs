namespace OPEN_LRD.Persistence
{
    /// <summary>Chooses whether imported persistence records merge with or replace graph state.</summary>
    internal enum ImportMode
    {
        Merge,
        Replace
    }

    /// <summary>Summarizes output paths, graph counts, and warnings from a log export.</summary>
    internal sealed class CoreLogExportResult
    {
        public string CorePath { get; init; } = string.Empty;

        public string? TransientPath { get; init; }

        public int NodeCount { get; init; }

        public int LinkCount { get; init; }

        public int ConversationCount { get; init; }

        public int QueryChainCount { get; init; }

        public int ResponseChainCount { get; init; }

        public bool IsSplit { get; init; }

        public int CorePartCount { get; init; }

        public int TransientPartCount { get; init; }

        public long TotalBytes { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }

    /// <summary>Summarizes graph changes and diagnostics from a persistence import.</summary>
    internal sealed class PersistenceImportResult
    {
        public bool Success { get; set; }

        public ImportMode Mode { get; set; }

        public int NodesAdded { get; set; }

        public int NodesSkipped { get; set; }

        public int LinksAdded { get; set; }

        public int LinksSkipped { get; set; }

        public int ConversationsAdded { get; set; }

        public int ConversationsSkipped { get; set; }

        public int TypesRegistered { get; set; }

        public int TypesSkipped { get; set; }

        public int QueryChainsAdded { get; set; }

        public int ResponseChainsAdded { get; set; }

        public int ChainsSkipped { get; set; }

        public List<string> Warnings { get; } = new List<string>();

        public List<string> Errors { get; } = new List<string>();
    }
}
