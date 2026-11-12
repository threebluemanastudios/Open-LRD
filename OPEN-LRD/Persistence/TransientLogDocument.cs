namespace OPEN_LRD.Persistence
{
    internal sealed class TransientLogDocument
    {
        public string Format { get; set; } = CoreLogFormat.Transient;

        public int Version { get; set; } = CoreLogFormat.Version;

        public DateTimeOffset ExportedAtUtc { get; set; }

        public string? CoreFile { get; set; }

        public TypeDefinitionLog TypeDefinitions { get; set; } = new TypeDefinitionLog();

        public List<ChainLog> QueryChains { get; set; } = new List<ChainLog>();

        public List<ChainLog> ResponseChains { get; set; } = new List<ChainLog>();
    }

    internal sealed class ChainLog
    {
        public List<NodeLog> Nodes { get; set; } = new List<NodeLog>();

        public List<LinkLog> Links { get; set; } = new List<LinkLog>();
    }
}
