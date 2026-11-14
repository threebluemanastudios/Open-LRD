namespace OPEN_LRD.Nodes.Links
{
    [Flags]
    public enum LinkOrigin
    {
        None = 0,
        Explicit = 1,
        NodeMetadata = 2,
        Reasoning = 4
    }
}
