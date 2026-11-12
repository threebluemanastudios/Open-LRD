using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using System.Collections.ObjectModel;

namespace OPEN_LRD
{
    internal class LRD_Core
    {
        private static readonly LRD_Core _instance = new LRD_Core();
        public bool initialized = false;

        protected Dictionary<Guid, Web_Node>? nodes;
        protected LinkedList<Chain_Node>? queryChain;
        protected LinkedList<Chain_Node>? responseChain;

        static LRD_Core() { }

        private LRD_Core() { }

        public static LRD_Core Instance => _instance;

        // Initializes the graph dictionary and the temporary query/response chains.
        public void InitializeCore()
        {
            nodes = new Dictionary<Guid, Web_Node>();
            queryChain = new LinkedList<Chain_Node>();
            responseChain = new LinkedList<Chain_Node>();
            initialized = true;
        }

        // Generates a new basic type node with a collision-resistant GUID and adds it to the graph.
        public Guid GenerateNewNode(NodeLifetime lifetime = NodeLifetime.Temporary, int ttl = Node.DefaultTimeToLive)
        {
            if (!initialized || nodes is null)
                return Guid.Empty;

            Guid nodeID;
            do
            {
                nodeID = Guid.NewGuid();
            }
            while (nodes.ContainsKey(nodeID));

            Web_Node newNode = new Web_Node(nodeID, NodeTypes.Basics, lifetime, ttl);
            nodes.Add(nodeID, newNode);
            return nodeID;
        }

        // Adds the specified Node to the nodes dictionary.
        public bool AddExistingNode(Web_Node tempNode)
        {
            if (!initialized || nodes is null || tempNode is null)
                return false;

            return nodes.TryAdd(tempNode.Get_ID(), tempNode);
        }

        // Returns the existing node at index (NodeID) or null when it does not exist.
        public Web_Node? GetExistingNode(Guid nodeID)
        {
            if (!initialized || nodes is null)
                return null;

            return nodes.TryGetValue(nodeID, out Web_Node? node) ? node : null;
        }

        public bool TryGetNode(Guid nodeID, out Web_Node? node)
        {
            node = null;

            if (!initialized || nodes is null)
                return false;

            return nodes.TryGetValue(nodeID, out node);
        }

        public bool RemoveExistingNode(Web_Node nodeToRemove)
        {
            ArgumentNullException.ThrowIfNull(nodeToRemove);
            return RemoveExistingNode(nodeToRemove.Get_ID());
        }

        // Removes the node and every link that references it.
        public bool RemoveExistingNode(Guid nodeID)
        {
            if (!initialized || nodes is null)
                return false;

            if (!nodes.TryGetValue(nodeID, out Web_Node? nodeToRemove))
                return false;

            foreach (Link link in nodeToRemove.Get_ReferenceLinks())
            {
                Guid otherID = link.SourceID == nodeID ? link.DestinationID : link.SourceID;

                if (otherID != nodeID && nodes.TryGetValue(otherID, out Web_Node? otherNode))
                    otherNode.RemoveLinksFor(nodeID);
            }

            nodeToRemove.RemoveLinksFor(nodeID);
            return nodes.Remove(nodeID);
        }

        // Adds a two-way connection between the source and destination nodes.
        public bool AddConnection(Guid sourceID, Guid destinationID)
        {
            return AddConnections(sourceID, destinationID, LinkClass.basic, (int)ForwardLinkTypes.basic, true);
        }

        // Adds a two-way connection between the source and destination nodes.
        public bool AddConnection(Guid sourceID, Guid destinationID, LinkClass linkClass)
        {
            return AddConnections(sourceID, destinationID, linkClass, (int)ForwardLinkTypes.basic, true);
        }

        public bool AddConnection(Guid sourceID, Guid destinationID, LinkClass linkClass, int linkType)
        {
            return AddConnections(sourceID, destinationID, linkClass, linkType, true);
        }

        // Adds a single directed connection from the source node to the destination node.
        public bool AddDirectedConnection(Guid sourceID, Guid destinationID)
        {
            return AddConnections(sourceID, destinationID, LinkClass.basic, (int)ForwardLinkTypes.basic, false);
        }

        public bool AddDirectedConnection(Guid sourceID, Guid destinationID, LinkClass linkClass)
        {
            return AddConnections(sourceID, destinationID, linkClass, (int)ForwardLinkTypes.basic, false);
        }

        public bool AddDirectedConnection(Guid sourceID, Guid destinationID, LinkClass linkClass, int linkType)
        {
            return AddConnections(sourceID, destinationID, linkClass, linkType, false);
        }

        private bool AddConnections(Guid sourceID, Guid destinationID, LinkClass linkClass, int linkType, bool bidirectional)
        {
            if (!initialized || nodes is null)
                return false;

            if (!nodes.TryGetValue(sourceID, out Web_Node? source))
                return false;

            if (!nodes.TryGetValue(destinationID, out Web_Node? destination))
                return false;

            source.Add_Link(new Link(sourceID, destinationID, linkType, linkType, linkClass));

            if (bidirectional && sourceID != destinationID)
                destination.Add_Link(new Link(destinationID, sourceID, linkType, linkType, linkClass));

            return true;
        }

        // Removes both directions of a connection between the source and destination nodes.
        public bool RemoveConnection(Guid sourceID, Guid destinationID)
        {
            if (!initialized || nodes is null)
                return false;

            bool removed = false;

            if (nodes.TryGetValue(sourceID, out Web_Node? source))
                removed |= source.RemoveLinksBetween(sourceID, destinationID);

            if (sourceID != destinationID && nodes.TryGetValue(destinationID, out Web_Node? destination))
                removed |= destination.RemoveLinksBetween(destinationID, sourceID);

            return removed;
        }

        // Removes only the connection from the source node to the destination node.
        public bool RemoveDirectedConnection(Guid sourceID, Guid destinationID)
        {
            if (!initialized || nodes is null)
                return false;

            return nodes.TryGetValue(sourceID, out Web_Node? source)
                && source.RemoveLinksBetween(sourceID, destinationID);
        }

        public void CleanObsoleteLinks(Guid destinationID)
        {
            if (!initialized || nodes is null)
                return;

            foreach (Web_Node node in nodes.Values.ToList())
                node.Remove_Destination_Link(destinationID);
        }

        public IReadOnlyDictionary<Guid, Web_Node>? GetDictionary()
        {
            if (!initialized || nodes is null)
                return null;

            return new ReadOnlyDictionary<Guid, Web_Node>(nodes);
        }

        public IReadOnlyList<Web_Node> FindNodes(NodeSearchCriteria? criteria = null)
        {
            if (!initialized || nodes is null)
                return Array.Empty<Web_Node>();

            IEnumerable<Web_Node> results = nodes.Values;

            if (criteria is not null)
            {
                if (criteria.ID.HasValue)
                    results = results.Where(node => node.ID == criteria.ID.Value);

                if (criteria.Type is not null)
                    results = results.Where(node => node.Get_NodeType() == criteria.Type);

                if (criteria.Lifetime.HasValue)
                    results = results.Where(node => node.Get_Lifetime() == criteria.Lifetime.Value);

                if (criteria.MinWeightUp.HasValue)
                    results = results.Where(node => node.Get_WeightUp() >= criteria.MinWeightUp.Value);

                if (criteria.MaxWeightUp.HasValue)
                    results = results.Where(node => node.Get_WeightUp() <= criteria.MaxWeightUp.Value);

                if (criteria.MinWeightDown.HasValue)
                    results = results.Where(node => node.Get_WeightDown() >= criteria.MinWeightDown.Value);

                if (criteria.MaxWeightDown.HasValue)
                    results = results.Where(node => node.Get_WeightDown() <= criteria.MaxWeightDown.Value);

                if (criteria.LinkClass.HasValue)
                    results = results.Where(node => node.Get_ReferenceLinks().Any(
                        link => link.Classification == criteria.LinkClass.Value));
            }

            return results.ToList();
        }

        public IReadOnlyList<Web_Node> FindNodes(Func<Web_Node, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            if (!initialized || nodes is null)
                return Array.Empty<Web_Node>();

            return nodes.Values.Where(predicate).ToList();
        }

        public IReadOnlyList<Web_Node> FindByType(NodeTypes type)
        {
            return FindNodes(new NodeSearchCriteria { Type = type });
        }

        public IReadOnlyList<Web_Node> FindByLifetime(NodeLifetime lifetime)
        {
            return FindNodes(new NodeSearchCriteria { Lifetime = lifetime });
        }

        public IReadOnlyList<Web_Node> FindByLinkClass(LinkClass linkClass)
        {
            return FindNodes(new NodeSearchCriteria { LinkClass = linkClass });
        }

        public IReadOnlyList<Web_Node> FindLinkedTo(Guid nodeID)
        {
            if (!initialized || nodes is null)
                return Array.Empty<Web_Node>();

            HashSet<Guid> linkedNodeIDs = new HashSet<Guid>();

            if (nodes.TryGetValue(nodeID, out Web_Node? node))
            {
                foreach (Link link in node.Get_ReferenceLinks())
                {
                    if (link.SourceID != nodeID)
                        linkedNodeIDs.Add(link.SourceID);

                    if (link.DestinationID != nodeID)
                        linkedNodeIDs.Add(link.DestinationID);
                }
            }

            foreach (Web_Node candidate in nodes.Values)
            {
                if (candidate.ID == nodeID)
                    continue;

                if (candidate.Get_ReferenceLinks().Any(
                    link => link.SourceID == nodeID || link.DestinationID == nodeID))
                {
                    linkedNodeIDs.Add(candidate.ID);
                }
            }

            return linkedNodeIDs
                .Where(id => nodes.ContainsKey(id))
                .Select(id => nodes[id])
                .ToList();
        }

        public IReadOnlyList<Link> GetLinks(Guid nodeID)
        {
            if (!initialized || nodes is null)
                return Array.Empty<Link>();

            return nodes.TryGetValue(nodeID, out Web_Node? node)
                ? node.Get_ReferenceLinks()
                : Array.Empty<Link>();
        }

        public void BuildQuery(LinkedList<Chain_Node> chainList)
        {
            if (!initialized)
                return;

            if (chainList is null)
                return;

            queryChain ??= new LinkedList<Chain_Node>();

            if (queryChain.Count == 0)
            {
                queryChain = new LinkedList<Chain_Node>(chainList);
            }
            else
            {
                foreach (Chain_Node chainNode in chainList)
                    queryChain.AddLast(chainNode);
            }
        }

        public void BuildQuery(Chain_Node chainNode)
        {
            if (!initialized || chainNode is null)
                return;

            queryChain ??= new LinkedList<Chain_Node>();
            queryChain.AddLast(chainNode);
        }

        public void AddQueryNode(Chain_Node chainNode)
        {
            BuildQuery(chainNode);
        }

        public void ClearQuery()
        {
            if (!initialized)
                return;

            queryChain ??= new LinkedList<Chain_Node>();
            queryChain.Clear();
        }

        public void BuildResponse(LinkedList<Chain_Node> chainList)
        {
            if (!initialized)
                return;

            if (chainList is null)
                return;

            responseChain ??= new LinkedList<Chain_Node>();

            if (responseChain.Count == 0)
            {
                responseChain = new LinkedList<Chain_Node>(chainList);
            }
            else
            {
                foreach (Chain_Node chainNode in chainList)
                    responseChain.AddLast(chainNode);
            }
        }

        public void BuildResponse(Chain_Node chainNode)
        {
            if (!initialized || chainNode is null)
                return;

            responseChain ??= new LinkedList<Chain_Node>();
            responseChain.AddLast(chainNode);
        }

        public void AddResponseNode(Chain_Node chainNode)
        {
            BuildResponse(chainNode);
        }

        public void ClearResponse()
        {
            if (!initialized)
                return;

            responseChain ??= new LinkedList<Chain_Node>();
            responseChain.Clear();
        }

        public LinkedList<Chain_Node>? GetQuery()
        {
            if (!initialized || queryChain is null)
                return null;

            return new LinkedList<Chain_Node>(queryChain);
        }

        public LinkedList<Chain_Node>? GetResponse()
        {
            if (!initialized || responseChain is null)
                return null;

            return new LinkedList<Chain_Node>(responseChain);
        }

        public void RunMaintenanceCycle()
        {
            if (!initialized || nodes is null)
                return;

            HashSet<Guid> nodesToRemove = new HashSet<Guid>();

            foreach (Web_Node node in nodes.Values.ToList())
            {
                node.PerformMaintenance();

                if (node.IsDisposed)
                    nodesToRemove.Add(node.ID);
            }

            foreach (Guid nodeID in nodesToRemove)
                RemoveExistingNode(nodeID);
        }

    }
}
