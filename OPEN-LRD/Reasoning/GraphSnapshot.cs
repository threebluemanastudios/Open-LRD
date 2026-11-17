using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    /// <summary>Captures the immutable state of a single graph node at snapshot creation time.</summary>
    internal sealed record GraphNodeSnapshot(
        Guid ID,
        NodeTypes Type,
        string? Label,
        IReadOnlyCollection<string> Aliases,
        NodeLifetime Lifetime,
        float WeightUp);

    /// <summary>Provides an immutable bounded graph view for matching and response extraction.</summary>
    internal sealed class GraphSnapshot
    {
        private readonly Dictionary<Guid, GraphNodeSnapshot> nodesByID;
        private readonly Dictionary<Guid, List<Link>> outgoingLinks;
        private readonly Dictionary<Guid, List<Link>> incidentLinks;

        /// <summary>Initializes an immutable snapshot index over the supplied nodes and links.</summary>
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

            incidentLinks = new Dictionary<Guid, List<Link>>();
            foreach (Link link in links)
            {
                AddIncidentLink(link.SourceID, link);
                if (link.DestinationID != link.SourceID)
                    AddIncidentLink(link.DestinationID, link);
            }
        }

        public IReadOnlyList<GraphNodeSnapshot> Nodes { get; }

        public IReadOnlyList<Link> Links { get; }

        public static GraphSnapshot Empty { get; } = new GraphSnapshot(
            Array.Empty<GraphNodeSnapshot>(),
            Array.Empty<Link>());

        /// <summary>Captures an immutable snapshot of active nodes and links from the core graph.</summary>
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

        /// <summary>Determines whether the snapshot contains a node with the specified ID.</summary>
        public bool ContainsNode(Guid nodeID)
        {
            return nodesByID.ContainsKey(nodeID);
        }

        /// <summary>Attempts to retrieve a node snapshot by its ID.</summary>
        public bool TryGetNode(Guid nodeID, out GraphNodeSnapshot node)
        {
            return nodesByID.TryGetValue(nodeID, out node!);
        }

        /// <summary>Returns all snapshot nodes matching the specified node type.</summary>
        public IReadOnlyList<GraphNodeSnapshot> GetNodesByType(NodeTypes type)
        {
            return Nodes.Where(node => node.Type == type).ToList();
        }

        /// <summary>Finds snapshot nodes whose label or aliases match the specified text and optional type.</summary>
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

        /// <summary>Returns all active outgoing links originating from the specified node.</summary>
        public IReadOnlyList<Link> GetOutgoingLinks(Guid nodeID)
        {
            return outgoingLinks.TryGetValue(nodeID, out List<Link>? links)
                ? links
                : Array.Empty<Link>();
        }

        /// <summary>Returns all active links incident to the specified node.</summary>
        public IReadOnlyList<Link> GetIncidentLinks(Guid nodeID)
        {
            return incidentLinks.TryGetValue(nodeID, out List<Link>? links)
                ? links
                : Array.Empty<Link>();
        }

        /// <summary>Adds a link to the incident-link index for one endpoint.</summary>
        private void AddIncidentLink(Guid nodeID, Link link)
        {
            if (!incidentLinks.TryGetValue(nodeID, out List<Link>? links))
            {
                links = new List<Link>();
                incidentLinks.Add(nodeID, links);
            }
            links.Add(link);
        }

        /// <summary>Performs a case-insensitive comparison between a candidate label and an expected string.</summary>
        private static bool Matches(string? value, string expected)
        {
            return value is not null
                && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Builds a canonical identity key for a graph link.</summary>
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
