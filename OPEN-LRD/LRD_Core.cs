using OPEN_LRD.NLP;
using OPEN_LRD.Maintenance;
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
        private Dictionary<Guid, ConversationContext>? conversations;
        private Dictionary<Guid, Guid>? pipelineConversations;

        static LRD_Core() { }

        private LRD_Core() { }

        public static LRD_Core Instance => _instance;

        // Initializes the graph dictionary and the temporary query/response chains.
        public void InitializeCore()
        {
            nodes = new Dictionary<Guid, Web_Node>();
            queryChain = new LinkedList<Chain_Node>();
            responseChain = new LinkedList<Chain_Node>();
            conversations = new Dictionary<Guid, ConversationContext>();
            pipelineConversations = new Dictionary<Guid, Guid>();
            initialized = true;
        }

        public Guid CreateConversation(NlpOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!initialized || conversations is null)
                throw new InvalidOperationException("The core must be initialized before creating a conversation.");

            Guid conversationID;

            do
            {
                conversationID = Guid.NewGuid();
            }
            while (conversationID == Guid.Empty || conversations.ContainsKey(conversationID));

            conversations.Add(conversationID, new ConversationContext(conversationID, options));
            return conversationID;
        }

        public ConversationContext GetConversation(Guid conversationID)
        {
            if (!initialized || conversations is null)
                throw new InvalidOperationException("The core must be initialized before accessing conversations.");

            if (conversations.TryGetValue(conversationID, out ConversationContext? context))
                return context;

            throw new InvalidOperationException($"Conversation '{conversationID}' was not found.");
        }

        public bool TryGetConversation(Guid conversationID, out ConversationContext? context)
        {
            context = null;

            if (!initialized || conversations is null)
                return false;

            return conversations.TryGetValue(conversationID, out context);
        }

        public ConversationContext ResolveConversation(Guid? conversationID, NlpOptions options)
        {
            if (!initialized)
                throw new InvalidOperationException("The core must be initialized before resolving conversations.");

            if (!conversationID.HasValue)
                return GetConversation(CreateConversation(options));

            return GetConversation(conversationID.Value);
        }

        public bool RemoveConversation(Guid conversationID)
        {
            if (!initialized || conversations is null || pipelineConversations is null)
                return false;

            List<Guid> attachedPipelines = pipelineConversations
                .Where(pair => pair.Value == conversationID)
                .Select(pair => pair.Key)
                .ToList();

            foreach (Guid pipelineID in attachedPipelines)
                pipelineConversations.Remove(pipelineID);

            return conversations.Remove(conversationID);
        }

        public IReadOnlyDictionary<Guid, ConversationContext>? GetConversations()
        {
            if (!initialized || conversations is null)
                return null;

            return new ReadOnlyDictionary<Guid, ConversationContext>(conversations);
        }

        internal bool AddExistingConversation(ConversationContext conversation)
        {
            ArgumentNullException.ThrowIfNull(conversation);

            if (!initialized || conversations is null || pipelineConversations is null)
                return false;

            return conversations.TryAdd(conversation.ConversationID, conversation);
        }

        internal void ReplaceGraphAndConversations(
            IReadOnlyDictionary<Guid, Web_Node> replacementNodes,
            IReadOnlyDictionary<Guid, ConversationContext> replacementConversations)
        {
            ArgumentNullException.ThrowIfNull(replacementNodes);
            ArgumentNullException.ThrowIfNull(replacementConversations);

            Dictionary<Guid, Web_Node> newNodes = new Dictionary<Guid, Web_Node>(replacementNodes);
            Dictionary<Guid, ConversationContext> newConversations =
                new Dictionary<Guid, ConversationContext>(replacementConversations);

            foreach ((Guid nodeID, Web_Node node) in newNodes)
            {
                if (node is null || node.ID != nodeID)
                    throw new InvalidOperationException("Replacement node dictionary keys must match node IDs.");
            }

            foreach ((Guid conversationID, ConversationContext conversation) in newConversations)
            {
                if (conversation is null || conversation.ConversationID != conversationID)
                {
                    throw new InvalidOperationException(
                        "Replacement conversation dictionary keys must match conversation IDs.");
                }
            }

            nodes = newNodes;
            conversations = newConversations;
            pipelineConversations = new Dictionary<Guid, Guid>();
            queryChain = new LinkedList<Chain_Node>();
            responseChain = new LinkedList<Chain_Node>();
            initialized = true;
        }

        public void ClearConversations()
        {
            conversations?.Clear();
            pipelineConversations?.Clear();
        }

        public bool RegisterPipelineConversation(Guid pipelineID, Guid conversationID)
        {
            if (!initialized || conversations is null || pipelineConversations is null)
                return false;

            if (!conversations.ContainsKey(conversationID))
                return false;

            pipelineConversations[pipelineID] = conversationID;
            return true;
        }

        public bool TryGetPipelineConversation(Guid pipelineID, out Guid conversationID)
        {
            conversationID = Guid.Empty;

            if (!initialized || pipelineConversations is null)
                return false;

            return pipelineConversations.TryGetValue(pipelineID, out conversationID);
        }

        public bool UnregisterPipelineConversation(Guid pipelineID)
        {
            if (!initialized || pipelineConversations is null)
                return false;

            return pipelineConversations.Remove(pipelineID);
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

        public MaintenanceReport IngestNodeMetadata(Guid nodeID)
        {
            return NodeMetadataReasoner.IngestNode(this, nodeID);
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

        internal bool MergeNodes(Guid survivorID, Guid duplicateID)
        {
            if (!initialized || nodes is null || survivorID == duplicateID
                || !nodes.TryGetValue(survivorID, out Web_Node? survivor)
                || !nodes.TryGetValue(duplicateID, out Web_Node? duplicate)
                || survivor.IsDisposed || duplicate.IsDisposed)
            {
                return false;
            }

            DataPointer? survivorPointer = survivor.Get_DataPointer();
            DataPointer? duplicatePointer = duplicate.Get_DataPointer();
            if (survivorPointer is not null && duplicatePointer is not null
                && !Equals(survivorPointer, duplicatePointer))
            {
                return false;
            }

            Dictionary<string, Link> affectedLinks = new Dictionary<string, Link>(StringComparer.Ordinal);
            foreach (Web_Node node in nodes.Values)
            {
                foreach (Link link in node.Get_ReferenceLinks())
                {
                    if (link.SourceID != duplicateID && link.DestinationID != duplicateID)
                        continue;

                    string key = string.Join("|", link.SourceID, link.DestinationID,
                        link.ForwardLink.Id, link.InverseLink.Id, link.Classification.Id);
                    if (!affectedLinks.TryGetValue(key, out Link? existing)
                        || (!existing.IsActive && link.IsActive))
                    {
                        if (existing is not null)
                            link.AddOrigin(existing.Origins);
                        affectedLinks[key] = link;
                    }
                    else
                    {
                        existing.AddOrigin(link.Origins);
                    }
                }
            }

            if (!survivor.MergeMetadataFrom(duplicate))
                return false;

            foreach (Web_Node node in nodes.Values)
                node.RemoveLinksFor(duplicateID);

            foreach (Link oldLink in affectedLinks.Values)
            {
                Guid sourceID = oldLink.SourceID == duplicateID ? survivorID : oldLink.SourceID;
                Guid destinationID = oldLink.DestinationID == duplicateID ? survivorID : oldLink.DestinationID;

                if (sourceID == destinationID || !nodes.TryGetValue(sourceID, out Web_Node? source))
                    continue;

                Link replacement = new Link(
                    sourceID,
                    destinationID,
                    oldLink.ForwardLink,
                    oldLink.InverseLink,
                    oldLink.Classification,
                    oldLink.Origins);

                if (!oldLink.IsActive)
                    replacement.Invalidate();

                source.Add_Link(replacement);
            }

            return RemoveExistingNode(duplicateID);
        }

        internal bool InvalidateLink(Link target)
        {
            ArgumentNullException.ThrowIfNull(target);

            if (!initialized || nodes is null)
                return false;

            bool changed = false;
            foreach (Web_Node node in nodes.Values)
            {
                foreach (Link link in node.Get_ReferenceLinks())
                {
                    if (link.HasSameIdentity(target) && link.IsActive)
                    {
                        link.Invalidate();
                        changed = true;
                    }
                }
            }

            return changed;
        }

        internal (int FactsRemoved, int LinksInvalidated) ReconcileMetadataLinks(
            Guid sourceID,
            IReadOnlySet<string> expectedLinkKeys)
        {
            ArgumentNullException.ThrowIfNull(expectedLinkKeys);
            if (!initialized || nodes is null || !nodes.TryGetValue(sourceID, out Web_Node? source))
                return (0, 0);

            int factsRemoved = 0;
            int invalidated = 0;
            foreach (Link link in source.Get_ReferenceLinks())
            {
                if (link.SourceID != sourceID
                    || (link.Origins & LinkOrigin.NodeMetadata) == 0
                    || expectedLinkKeys.Contains(Maintenance.NodeMetadataReasoner.GetLinkKey(link)))
                {
                    continue;
                }

                LinkOrigin remaining = link.RemoveOrigin(LinkOrigin.NodeMetadata);
                factsRemoved++;
                if (remaining == LinkOrigin.None && link.IsActive)
                {
                    link.Invalidate();
                    invalidated++;
                }
            }

            return (factsRemoved, invalidated);
        }

        // Adds a two-way connection between the source and destination nodes.
        public bool AddConnection(Guid sourceID, Guid destinationID)
        {
            return AddConnections(sourceID, destinationID, LinkClass.basic, ForwardLinkTypes.basic.Id, true,
                LinkOrigin.Explicit);
        }

        // Adds a two-way connection between the source and destination nodes.
        public bool AddConnection(Guid sourceID, Guid destinationID, LinkClass linkClass)
        {
            return AddConnections(sourceID, destinationID, linkClass, ForwardLinkTypes.basic.Id, true,
                LinkOrigin.Explicit);
        }

        public bool AddConnection(Guid sourceID, Guid destinationID, LinkClass linkClass, int linkType)
        {
            return AddConnections(sourceID, destinationID, linkClass, linkType, true, LinkOrigin.Explicit);
        }

        // Adds a single directed connection from the source node to the destination node.
        public bool AddDirectedConnection(Guid sourceID, Guid destinationID)
        {
            return AddConnections(sourceID, destinationID, LinkClass.basic, ForwardLinkTypes.basic.Id, false,
                LinkOrigin.Explicit);
        }

        public bool AddDirectedConnection(Guid sourceID, Guid destinationID, LinkClass linkClass)
        {
            return AddConnections(sourceID, destinationID, linkClass, ForwardLinkTypes.basic.Id, false,
                LinkOrigin.Explicit);
        }

        public bool AddDirectedConnection(Guid sourceID, Guid destinationID, LinkClass linkClass, int linkType)
        {
            return AddConnections(sourceID, destinationID, linkClass, linkType, false, LinkOrigin.Explicit);
        }

        internal bool AddDirectedConnection(
            Guid sourceID,
            Guid destinationID,
            LinkClass linkClass,
            int linkType,
            LinkOrigin origin)
        {
            return AddConnections(sourceID, destinationID, linkClass, linkType, false, origin);
        }

        private bool AddConnections(
            Guid sourceID,
            Guid destinationID,
            LinkClass linkClass,
            int linkType,
            bool bidirectional,
            LinkOrigin origin)
        {
            if (!initialized || nodes is null)
                return false;

            if (!nodes.TryGetValue(sourceID, out Web_Node? source))
                return false;

            if (!nodes.TryGetValue(destinationID, out Web_Node? destination))
                return false;

            source.Add_Link(new Link(sourceID, destinationID, linkType, linkType, linkClass, origin));

            if (bidirectional && sourceID != destinationID)
                destination.Add_Link(new Link(destinationID, sourceID, linkType, linkType, linkClass, origin));

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

                if (criteria.LinkClass is not null)
                    results = results.Where(node => node.Get_ReferenceLinks().Any(
                        link => link.IsActive && link.Classification == criteria.LinkClass));
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

        public IReadOnlyList<Web_Node> FindByLabel(string label, NodeTypes? type = null)
        {
            if (!initialized || nodes is null || string.IsNullOrWhiteSpace(label))
                return Array.Empty<Web_Node>();

            string trimmedLabel = label.Trim();

            return nodes.Values
                .Where(node => (type is null || node.Get_NodeType() == type)
                    && (node.Has_Label(trimmedLabel) || node.Has_Alias(trimmedLabel)))
                .ToList();
        }

        public bool TryFindByLabel(string label, out IReadOnlyList<Web_Node> foundNodes)
        {
            foundNodes = FindByLabel(label);
            return foundNodes.Count > 0;
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
                    if (!link.IsActive)
                        continue;

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
                    link => link.IsActive && (link.SourceID == nodeID || link.DestinationID == nodeID)))
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
                ? node.Get_ReferenceLinks().Where(link => link.IsActive).ToArray()
                : Array.Empty<Link>();
        }

        internal IReadOnlyList<Link> GetStoredLinks(Guid nodeID)
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

        internal void ReplaceTransientChains(
            LinkedList<Chain_Node>? replacementQuery,
            LinkedList<Chain_Node>? replacementResponse)
        {
            if (!initialized)
                throw new InvalidOperationException("The core must be initialized before restoring transient chains.");

            queryChain = replacementQuery is null
                ? new LinkedList<Chain_Node>()
                : new LinkedList<Chain_Node>(replacementQuery);
            responseChain = replacementResponse is null
                ? new LinkedList<Chain_Node>()
                : new LinkedList<Chain_Node>(replacementResponse);
        }

        public MaintenanceReport RunMaintenanceCycle()
        {
            return RunMaintenanceCycle(new MaintenanceOptions());
        }

        public MaintenanceReport RunMaintenanceCycle(MaintenanceOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();
            MaintenanceReport report = new MaintenanceReport();

            if (!initialized || nodes is null)
            {
                report.Complete();
                return report;
            }

            HashSet<Guid> nodesToRemove = new HashSet<Guid>();

            Web_Node[] maintenanceNodes = nodes.Values.ToArray();
            report.NodesExamined = maintenanceNodes.Length;
            foreach (Web_Node node in maintenanceNodes)
            {
                node.PerformMaintenance();

                if (node.IsDisposed)
                    nodesToRemove.Add(node.ID);
            }

            foreach (Guid nodeID in nodesToRemove)
                RemoveExistingNode(nodeID);

            report.NodesRemoved = nodesToRemove.Count;
            MaintenanceReasoningEngine.Run(this, options, report);
            report.Complete();
            return report;
        }

    }
}
