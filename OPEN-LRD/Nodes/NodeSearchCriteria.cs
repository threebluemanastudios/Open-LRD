using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Nodes
{
    /// <summary>Defines optional filters for searching graph nodes.</summary>
    public sealed record NodeSearchCriteria
    {
        /// <summary>Gets or initializes an exact node identifier filter.</summary>
        public Guid? ID { get; init; }
        /// <summary>Gets or initializes a node type filter.</summary>
        public NodeTypes? Type { get; init; }
        /// <summary>Gets or initializes a node lifetime filter.</summary>
        public NodeLifetime? Lifetime { get; init; }
        /// <summary>Gets or initializes the minimum accepted upward weight.</summary>
        public float? MinWeightUp { get; init; }
        /// <summary>Gets or initializes the maximum accepted upward weight.</summary>
        public float? MaxWeightUp { get; init; }
        /// <summary>Gets or initializes the minimum accepted downward weight.</summary>
        public float? MinWeightDown { get; init; }
        /// <summary>Gets or initializes the maximum accepted downward weight.</summary>
        public float? MaxWeightDown { get; init; }
        /// <summary>Gets or initializes a required link-class filter.</summary>
        public LinkClass? LinkClass { get; init; }
    }
}
