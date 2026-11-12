using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Nodes
{
    public sealed record NodeSearchCriteria
    {
        public Guid? ID { get; init; }
        public NodeTypes? Type { get; init; }
        public NodeLifetime? Lifetime { get; init; }
        public float? MinWeightUp { get; init; }
        public float? MaxWeightUp { get; init; }
        public float? MinWeightDown { get; init; }
        public float? MaxWeightDown { get; init; }
        public LinkClass? LinkClass { get; init; }
    }
}
