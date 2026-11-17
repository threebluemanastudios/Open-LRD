using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Reasoning
{
    /// <summary>Validates chain nodes and converts them into a bounded query graph.</summary>
    internal static class QueryGraphBuilder
    {
        /// <summary>Builds a validated query graph from an ordered chain of query nodes.</summary>
        public static QueryGraph Build(LinkedList<Chain_Node> chain, ReasoningOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(chain);

            if (chain.Count == 0)
                throw new ArgumentException("A query chain must contain at least one node.", nameof(chain));

            options ??= new ReasoningOptions();

            Dictionary<Guid, QueryNode> queryNodes = new Dictionary<Guid, QueryNode>();
            List<Chain_Node> orderedNodes = new List<Chain_Node>(chain.Count);

            foreach (Chain_Node chainNode in chain)
            {
                if (chainNode is null)
                    throw new ArgumentException("A query chain cannot contain null nodes.", nameof(chain));

                Guid id = chainNode.Get_ID();

                if (id == Guid.Empty)
                    throw new ArgumentException("Every query node must have a non-empty ID.", nameof(chain));

                if (queryNodes.ContainsKey(id))
                    throw new ArgumentException($"Duplicate query node ID '{id}' was supplied.", nameof(chain));

                queryNodes[id] = new QueryNode(
                    id,
                    chainNode.Get_NodeType(),
                    chainNode.Label,
                    chainNode.Aliases.ToArray());

                orderedNodes.Add(chainNode);
            }

            HashSet<string> edgeKeys = new HashSet<string>(StringComparer.Ordinal);
            List<QueryEdge> queryEdges = new List<QueryEdge>();

            foreach (Chain_Node chainNode in orderedNodes)
            {
                AddLinkEdge(chainNode.Get_LinkNext(), queryNodes, edgeKeys, queryEdges);
                AddLinkEdge(chainNode.Get_LinkPrevious(), queryNodes, edgeKeys, queryEdges);
            }

            if (orderedNodes.Count > 1)
                AddMissingSequentialEdges(orderedNodes, edgeKeys, queryEdges);

            bool isLinear = IsLinear(queryNodes.Keys, queryEdges);

            return new QueryGraph(
                queryNodes.Values.ToArray(),
                queryEdges,
                isLinear,
                orderedNodes[0].Get_ID());
        }

        /// <summary>Validates a chain link and adds its corresponding query edge if present.</summary>
        private static void AddLinkEdge(
            Link? link,
            Dictionary<Guid, QueryNode> queryNodes,
            HashSet<string> edgeKeys,
            List<QueryEdge> queryEdges)
        {
            if (link is null || link.Classification == LinkClass.empty)
                return;

            if (!queryNodes.ContainsKey(link.SourceID) || !queryNodes.ContainsKey(link.DestinationID))
            {
                throw new ArgumentException(
                    $"Link '{link.SourceID}' -> '{link.DestinationID}' does not connect nodes in the query chain.");
            }

            if (link.SourceID == link.DestinationID)
                throw new ArgumentException("A query chain cannot contain a self-loop.");

            QueryEdge edge = new QueryEdge(
                link.SourceID,
                link.DestinationID,
                link.Classification,
                link.ForwardLink.Id,
                link.InverseLink.Id,
                false);

            AddEdge(edge, edgeKeys, queryEdges);
        }

        /// <summary>Adds default sequential basic edges between adjacent chain nodes that lack explicit links.</summary>
        private static void AddMissingSequentialEdges(
            List<Chain_Node> orderedNodes,
            HashSet<string> edgeKeys,
            List<QueryEdge> queryEdges)
        {
            for (int i = 0; i < orderedNodes.Count - 1; i++)
            {
                Guid fromID = orderedNodes[i].Get_ID();
                Guid toID = orderedNodes[i + 1].Get_ID();

                bool edgeExists = queryEdges.Any(edge =>
                    (edge.FromQueryNodeID == fromID && edge.ToQueryNodeID == toID)
                    || (edge.FromQueryNodeID == toID && edge.ToQueryNodeID == fromID));

                if (edgeExists)
                    continue;

                QueryEdge edge = new QueryEdge(
                    fromID,
                    toID,
                    LinkClass.basic,
                    ForwardLinkTypes.basic.Id,
                    InverseLinkTypes.basic.Id,
                    false);

                AddEdge(edge, edgeKeys, queryEdges);
            }
        }

        /// <summary>Adds a query edge to the collection if an identical edge has not already been recorded.</summary>
        private static void AddEdge(
            QueryEdge edge,
            HashSet<string> edgeKeys,
            List<QueryEdge> queryEdges)
        {
            string key = string.Join(
                "|",
                edge.FromQueryNodeID,
                edge.ToQueryNodeID,
                edge.Classification.Id,
                edge.ForwardLinkType,
                edge.InverseLinkType,
                edge.IsInverse);

            if (edgeKeys.Add(key))
                queryEdges.Add(edge);
        }

        /// <summary>Determines whether the query nodes and edges form a single non-branching connected path.</summary>
        private static bool IsLinear(IEnumerable<Guid> nodeIDs, IReadOnlyList<QueryEdge> edges)
        {
            List<Guid> ids = nodeIDs.ToList();

            if (ids.Count <= 1)
                return true;

            if (!IsConnected(ids, edges))
                return false;

            Dictionary<Guid, int> degrees = ids.ToDictionary(id => id, _ => 0);

            foreach (QueryEdge edge in edges)
            {
                if (edge.FromQueryNodeID == edge.ToQueryNodeID)
                    return false;

                degrees[edge.FromQueryNodeID]++;
                degrees[edge.ToQueryNodeID]++;
            }

            return degrees.Values.All(degree => degree <= 2);
        }

        /// <summary>Determines whether all query nodes belong to a single connected component.</summary>
        private static bool IsConnected(IReadOnlyCollection<Guid> nodeIDs, IReadOnlyList<QueryEdge> edges)
        {
            if (nodeIDs.Count == 0)
                return true;

            Dictionary<Guid, List<Guid>> adjacency = nodeIDs.ToDictionary(id => id, _ => new List<Guid>());

            foreach (QueryEdge edge in edges)
            {
                adjacency[edge.FromQueryNodeID].Add(edge.ToQueryNodeID);
                adjacency[edge.ToQueryNodeID].Add(edge.FromQueryNodeID);
            }

            HashSet<Guid> visited = new HashSet<Guid>();
            Queue<Guid> queue = new Queue<Guid>();
            queue.Enqueue(nodeIDs.First());

            while (queue.Count > 0)
            {
                Guid current = queue.Dequeue();

                if (!visited.Add(current))
                    continue;

                foreach (Guid neighbor in adjacency[current])
                {
                    if (!visited.Contains(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return visited.Count == nodeIDs.Count;
        }
    }
}
