namespace OPEN_LRD.Nodes
{
    /// <summary>Specifies whether a graph node is temporary working memory or permanent knowledge.</summary>
    public enum NodeLifetime
    {
        /// <summary>The node may expire or be disposed by maintenance.</summary>
        Temporary,
        /// <summary>The node is retained across maintenance cycles.</summary>
        Permanent
    }
}
