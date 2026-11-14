using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    internal sealed record GraphNodeSnapshot(
        Guid ID,
        NodeTypes Type,
        string? Label,
        IReadOnlyCollection<string> Aliases,
        NodeLifetime Lifetime,
        float WeightUp);

    internal sealed class GraphSnapshot
    {
        private readonly Dictionary<Guid, GraphNodeSnapshot> nodesByID;
        private readonly Dictionary<Guid, List<Link>> outgoingLinks;

        private GraphSnapshot(
            IReadOnlyList<GraphNodeSnapshot> nodes,
            IReadOnlyList<Link> links)
        {
            Nodes = nodes;
            Links = links;
            nodesByID = nodes.ToDictionary(node => node.ID);
            outgoingLinks = links
                .GroupBy(link => link.SourceID)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        public IReadOnlyList<GraphNodeSnapshot> Nodes { get; }

        public IReadOnlyList<Link> Links { get; }

        public static GraphSnapshot Empty { get; } = new GraphSnapshot(
            Array.Empty<GraphNodeSnapshot>(),
            Array.Empty<Link>());

        public static GraphSnapshot FromCore(LRD_Core core, IReadOnlySet<string>? invalidLinkKeys = null)
        {
            ArgumentNullException.ThrowIfNull(core);

            IReadOnlyDictionary<Guid, Web_Node>? dictionary = core.GetDictionary();

            if (dictionary is null || dictionary.Count == 0)
                return Empty;

            List<GraphNodeSnapshot> nodes = new List<GraphNodeSnapshot>(dictionary.Count);
            Dictionary<string, Link> uniqueLinks = new Dictionary<string, Link>(StringComparer.Ordinal);

            foreach (Web_Node node in dictionary.Values)
            {
                if (node.IsDisposed)
                    continue;

                nodes.Add(new GraphNodeSnapshot(
                    node.ID,
                    node.Get_NodeType(),
                    node.Label,
                    node.Aliases.ToArray(),
                    node.Get_Lifetime(),
                    node.Get_WeightUp()));

                foreach (Link link in node.Get_ReferenceLinks())
                {
                    if (!link.IsActive)
                        continue;

                    if (!dictionary.ContainsKey(link.SourceID) || !dictionary.ContainsKey(link.DestinationID))
                        continue;

                    string key = GetLinkKey(link);

                    if (invalidLinkKeys is not null && invalidLinkKeys.Contains(key))
                        continue;

                    uniqueLinks[key] = link;
                }
            }

            return new GraphSnapshot(nodes, uniqueLinks.Values.ToArray());
        }

        public bool ContainsNode(Guid nodeID)
        {
            return nodesByID.ContainsKey(nodeID);
        }

        public bool TryGetNode(Guid nodeID, out GraphNodeSnapshot node)
        {
            return nodesByID.TryGetValue(nodeID, out node!);
        }

        public IReadOnlyList<GraphNodeSnapshot> GetNodesByType(NodeTypes type)
        {
            return Nodes.Where(node => node.Type == type).ToList();
        }

        public IReadOnlyList<GraphNodeSnapshot> FindByLabel(string? label, NodeTypes? type = null)
        {
            if (string.IsNullOrWhiteSpace(label))
                return Array.Empty<GraphNodeSnapshot>();

            string trimmed = label.Trim();

            return Nodes
                .Where(node => (type is null || node.Type == type)
                    && (Matches(node.Label, trimmed) || node.Aliases.Any(alias => Matches(alias, trimmed))))
                .ToList();
        }

        public IReadOnlyList<Link> GetOutgoingLinks(Guid nodeID)
        {
            return outgoingLinks.TryGetValue(nodeID, out List<Link>? links)
                ? links
                : Array.Empty<Link>();
        }

        public IReadOnlyList<Link> GetIncidentLinks(Guid nodeID)
        {
            return Links
                .Where(link => link.SourceID == nodeID || link.DestinationID == nodeID)
                .ToList();
        }

        private static bool Matches(string? value, string expected)
        {
            return value is not null
                && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetLinkKey(Link link)
        {
            return string.Join(
                "|",
                link.SourceID,
                link.DestinationID,
                link.Classification.Id,
                link.ForwardLink.Id,
                link.InverseLink.Id);
        }
    }
}
