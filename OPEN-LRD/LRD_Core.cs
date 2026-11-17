using OPEN_LRD.NLP;
using OPEN_LRD.Maintenance;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;
using System.Collections.ObjectModel;

namespace OPEN_LRD
{
    /// <summary>Owns the graph, runtime registries, conversations, persistence hooks, and lifecycle operations.</summary>
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
        /// <summary>Initializes or resets the singleton graph and its runtime state.</summary>
        public void InitializeCore()
        {
            nodes = new Dictionary<Guid, Web_Node>();
            queryChain = new LinkedList<Chain_Node>();
            responseChain = new LinkedList<Chain_Node>();
            conversations = new Dictionary<Guid, ConversationContext>();
            pipelineConversations = new Dictionary<Guid, Guid>();
            initialized = true;
        }

        /// <summary>Creates and registers a conversation with the supplied NLP options.</summary>
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

        /// <summary>Returns the registered conversation or throws when it does not exist.</summary>
        public ConversationContext GetConversation(Guid conversationID)
        {
            if (!initialized || conversations is null)
                throw new InvalidOperationException("The core must be initialized before accessing conversations.");

            if (conversations.TryGetValue(conversationID, out ConversationContext? context))
                return context;

            throw new InvalidOperationException($"Conversation '{conversationID}' was not found.");
        }

        /// <summary>Attempts to retrieve a registered conversation.</summary>
        public bool TryGetConversation(Guid conversationID, out ConversationContext? context)
        {
            context = null;

            if (!initialized || conversations is null)
                return false;

            return conversations.TryGetValue(conversationID, out context);
        }

        /// <summary>Resolves an existing conversation or creates one when no identifier is supplied.</summary>
        public ConversationContext ResolveConversation(Guid? conversationID, NlpOptions options)
        {
            if (!initialized)
                throw new InvalidOperationException("The core must be initialized before resolving conversations.");

            if (!conversationID.HasValue)
                return GetConversation(CreateConversation(options));

            return GetConversation(conversationID.Value);
        }

        /// <summary>Removes a conversation when no active pipeline depends on it.</summary>
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

        /// <summary>Returns a read-only snapshot of registered conversations.</summary>
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

        /// <summary>Clears registered conversation state and pipeline attachments.</summary>
        public void ClearConversations()
        {
            conversations?.Clear();
            pipelineConversations?.Clear();
        }

        /// <summary>Associates an active pipeline with a registered conversation.</summary>
        public bool RegisterPipelineConversation(Guid pipelineID, Guid conversationID)
        {
            if (!initialized || conversations is null || pipelineConversations is null)
                return false;

            if (!conversations.ContainsKey(conversationID))
                return false;

            pipelineConversations[pipelineID] = conversationID;
            return true;
        }

        /// <summary>Attempts to retrieve the conversation attached to a pipeline.</summary>
        public bool TryGetPipelineConversation(Guid pipelineID, out Guid conversationID)
        {
            conversationID = Guid.Empty;

            if (!initialized || pipelineConversations is null)
                return false;

            return pipelineConversations.TryGetValue(pipelineID, out conversationID);
        }

        /// <summary>Removes an active pipeline-to-conversation association.</summary>
        public bool UnregisterPipelineConversation(Guid pipelineID)
        {
            if (!initialized || pipelineConversations is null)
                return false;

            return pipelineConversations.Remove(pipelineID);
        }

        // Generates a new basic type node with a collision-resistant GUID and adds it to the graph.
        /// <summary>Creates and stores a node using a generated identifier.</summary>
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
        /// <summary>Adds a preconstructed node to the graph.</summary>
        public bool AddExistingNode(Web_Node tempNode)
        {
            if (!initialized || nodes is null || tempNode is null)
                return false;

            return nodes.TryAdd(tempNode.Get_ID(), tempNode);
        }

        /// <summary>Creates an Event node with a time window and links its participants, action, and location.</summary>
        public Web_Node CreateEvent(
            string? eventName,
            DateTimeOffset? startTime,
            DateTimeOffset? endTime,
            Guid? subjectID = null,
            string? verb = null,
            Guid? objectID = null,
            Guid? locationID = null)
        {
            return CreateEvent(
                Guid.NewGuid(),
                eventName,
                startTime,
                endTime,
                subjectID,
                verb,
                objectID,
                locationID);
        }

        /// <summary>Creates an Event node from an existing event window and relationship identifiers.</summary>
        public Web_Node CreateEvent(
            Guid eventID,
            string? eventName,
            DateTimeOffset? startTime,
            DateTimeOffset? endTime,
            Guid? subjectID = null,
            string? verb = null,
            Guid? objectID = null,
            Guid? locationID = null)
        {
            EventNodeHelper.ValidateWindow(startTime, endTime);
            if (!initialized || nodes is null)
                throw new InvalidOperationException("The core must be initialized before creating an event.");
            if (eventID == Guid.Empty || nodes.ContainsKey(eventID))
                throw new ArgumentException("Event ID must be non-empty and unique in the graph.", nameof(eventID));

            ValidateEventEndpoint(subjectID, nameof(subjectID));
            ValidateEventEndpoint(objectID, nameof(objectID));
            ValidateEventEndpoint(locationID, nameof(locationID));

            Web_Node? actionNode = null;
            if (!string.IsNullOrWhiteSpace(verb))
            {
                actionNode = FindByLabel(verb.Trim(), NodeTypes.Concepts).FirstOrDefault();
                if (actionNode is null)
                {
                    actionNode = new Web_Node(
                        Guid.NewGuid(),
                        NodeTypes.Concepts,
                        NodeLifetime.Permanent,
                        0);
                    actionNode.Set_Label(verb.Trim());
                    if (!AddExistingNode(actionNode))
                        throw new InvalidOperationException("The event action concept could not be added.");
                }
            }

            Web_Node eventNode = new Web_Node(eventID, NodeTypes.Events, NodeLifetime.Permanent, 0);
            EventNodeHelper.SetWindow(eventNode, startTime, endTime);
            EventNodeHelper.AddName(eventNode, eventName);
            if (!AddExistingNode(eventNode))
                throw new InvalidOperationException("The Event node could not be added.");

            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            if (subjectID.HasValue)
            {
                AddEventLink(eventNode.ID, subjectID.Value, "has_subject", LinkClass.actions);
                AddEventLink(subjectID.Value, eventNode.ID, "participated_in", OntologyTypes.ontology);
            }
            if (actionNode is not null)
                AddEventLink(eventNode.ID, actionNode.ID, "has_action", LinkClass.actions);
            if (objectID.HasValue)
            {
                AddEventLink(eventNode.ID, objectID.Value, "has_object", LinkClass.actions);
                AddEventLink(objectID.Value, eventNode.ID, "participated_in", OntologyTypes.ontology);
            }
            if (locationID.HasValue)
            {
                AddEventLink(eventNode.ID, locationID.Value, "has_location", OntologyTypes.ontology);
                AddEventLink(locationID.Value, eventNode.ID, "location_of", OntologyTypes.ontology);
            }

            if (startTime.HasValue || endTime.HasValue)
            {
                Web_Node timeNode = new Web_Node(
                    Guid.NewGuid(),
                    OntologyTypes.Times,
                    NodeLifetime.Permanent,
                    0);
                timeNode.Set_Label(FormatEventWindow(startTime, endTime));
                if (AddExistingNode(timeNode))
                    AddEventLink(eventNode.ID, timeNode.ID, "has_time", OntologyTypes.ontology);
            }

            return eventNode;
        }

        /// <summary>Finds active Event nodes whose time windows overlap the requested interval.</summary>
        public IReadOnlyList<Web_Node> FindEventsInWindow(
            DateTimeOffset start,
            DateTimeOffset end,
            bool includeOverlapping = true)
        {
            if (start > end)
                throw new ArgumentException("Search window start cannot be after its end.");
            if (!initialized || nodes is null)
                return Array.Empty<Web_Node>();

            return nodes.Values
                .Where(node => node.Get_NodeType() == NodeTypes.Events)
                .Where(node => EventNodeHelper.TryGetWindow(
                    node,
                    out DateTimeOffset? eventStart,
                    out DateTimeOffset? eventEnd)
                    && EventOverlaps(eventStart, eventEnd, start, end, includeOverlapping))
                .OrderBy(node => EventNodeHelper.TryGetWindow(
                    node,
                    out DateTimeOffset? eventStart,
                    out _) ? eventStart : null)
                .ThenBy(node => node.ID)
                .ToList();
        }

        /// <summary>Finds Event nodes matching optional subject, action, and object constraints.</summary>
        public IReadOnlyList<Web_Node> FindEventsFor(
            Guid subjectID,
            string verb,
            Guid? objectID = null,
            Guid? locationID = null)
        {
            if (subjectID == Guid.Empty || string.IsNullOrWhiteSpace(verb))
                return Array.Empty<Web_Node>();
            if (!initialized || nodes is null)
                return Array.Empty<Web_Node>();

            return nodes.Values
                .Where(node => node.Get_NodeType() == NodeTypes.Events)
                .Where(node => EventNodeHelper.TryGetWindow(node, out _, out _))
                .Where(node => EventLinksMatch(node.ID, subjectID, verb, objectID, locationID))
                .OrderBy(node => EventNodeHelper.TryGetWindow(
                    node,
                    out DateTimeOffset? start,
                    out _) ? start : null)
                .ThenBy(node => node.ID)
                .ToList();
        }

        /// <summary>Calculates an event's temporal status relative to a supplied reference time.</summary>
        public EventTimeVariance CalculateEventTimeVariance(
            Guid eventNodeID,
            DateTimeOffset? referenceTime = null)
        {
            if (!TryGetNode(eventNodeID, out Web_Node? eventNode)
                || eventNode is null
                || eventNode.Get_NodeType() != NodeTypes.Events)
            {
                throw new ArgumentException("The requested node is not an Event node in this graph.", nameof(eventNodeID));
            }

            return EventTimeVarianceCalculator.Calculate(eventNode, referenceTime);
        }

        /// <summary>Calculates an event's temporal status relative to the current time.</summary>
        public EventTimeVariance CalculateEventTimeVariance(
            Web_Node eventNode,
            DateTimeOffset? referenceTime = null)
        {
            ArgumentNullException.ThrowIfNull(eventNode);
            if (eventNode.Get_NodeType() != NodeTypes.Events)
                throw new ArgumentException("The requested node is not an Event node.", nameof(eventNode));

            return EventTimeVarianceCalculator.Calculate(eventNode, referenceTime);
        }

        internal Web_Node? FindMatchingEvent(
            string? eventName,
            DateTimeOffset? startTime,
            DateTimeOffset? endTime,
            Guid? subjectID,
            string? verb,
            Guid? objectID,
            Guid? locationID)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return null;

            return FindByLabel(eventName, NodeTypes.Events)
                .Where(node => EventNodeHelper.TryGetWindow(
                    node,
                    out DateTimeOffset? existingStart,
                    out DateTimeOffset? existingEnd)
                    && existingStart == startTime
                    && existingEnd == endTime)
                .FirstOrDefault(node => EventLinksMatch(
                    node.ID,
                    subjectID,
                    verb,
                    objectID,
                    locationID));
        }

        private bool EventLinksMatch(
            Guid eventID,
            Guid? subjectID,
            string? verb,
            Guid? objectID,
            Guid? locationID)
        {
            IReadOnlyList<Link> links = GetLinks(eventID);
            bool subjectMatches = !subjectID.HasValue
                || links.Any(link => link.DestinationID == subjectID.Value && link.ForwardLink.Name == "has_subject");
            bool objectMatches = !objectID.HasValue
                || links.Any(link => link.DestinationID == objectID.Value && link.ForwardLink.Name == "has_object");
            bool locationMatches = !locationID.HasValue
                || links.Any(link => link.DestinationID == locationID.Value && link.ForwardLink.Name == "has_location");
            bool actionMatches = string.IsNullOrWhiteSpace(verb)
                || links.Any(link => nodes is not null
                    && nodes.TryGetValue(link.DestinationID, out Web_Node? action)
                    && action is not null
                    && link.ForwardLink.Name == "has_action"
                    && action.Has_Label(verb));
            return subjectMatches && objectMatches && locationMatches && actionMatches;
        }

        private void ValidateEventEndpoint(Guid? endpointID, string parameterName)
        {
            if (endpointID.HasValue
                && (nodes is null || !nodes.ContainsKey(endpointID.Value)))
            {
                throw new ArgumentException($"Event endpoint '{endpointID}' was not found in the graph.", parameterName);
            }
        }

        private void AddEventLink(Guid sourceID, Guid destinationID, string relationName, LinkClass classification)
        {
            if (nodes is null
                || !nodes.TryGetValue(sourceID, out Web_Node? source)
                || !nodes.ContainsKey(destinationID))
            {
                throw new InvalidOperationException("Both endpoints must exist before an event link is added.");
            }

            RelationLinkType relation = RelationRegistry.GetOrRegister(relationName);
            source.Add_Link(new Link(
                sourceID,
                destinationID,
                relation.Forward,
                relation.Inverse,
                classification));
        }

        private static string FormatEventWindow(DateTimeOffset? start, DateTimeOffset? end)
        {
            if (start.HasValue && end.HasValue)
                return start.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                    + " – " + end.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            return (start ?? end)?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool EventOverlaps(
            DateTimeOffset? eventStart,
            DateTimeOffset? eventEnd,
            DateTimeOffset searchStart,
            DateTimeOffset searchEnd,
            bool includeOverlapping)
        {
            if (!eventStart.HasValue && !eventEnd.HasValue)
                return false;

            if (!includeOverlapping && (!eventStart.HasValue || !eventEnd.HasValue))
                return false;

            DateTimeOffset actualStart = eventStart ?? DateTimeOffset.MinValue;
            DateTimeOffset actualEnd = eventEnd ?? DateTimeOffset.MaxValue;
            return includeOverlapping
                ? actualStart <= searchEnd && actualEnd >= searchStart
                : actualStart >= searchStart && actualEnd <= searchEnd;
        }

        /// <summary>Materializes the selected node's metadata as typed graph facts.</summary>
        public MaintenanceReport IngestNodeMetadata(Guid nodeID)
        {
            return NodeMetadataReasoner.IngestNode(this, nodeID);
        }

        // Returns the existing node at index (NodeID) or null when it does not exist.
        /// <summary>Returns a graph node by identifier or null when it is absent.</summary>
        public Web_Node? GetExistingNode(Guid nodeID)
        {
            if (!initialized || nodes is null)
                return null;

            return nodes.TryGetValue(nodeID, out Web_Node? node) ? node : null;
        }

        /// <summary>Attempts to retrieve a graph node by identifier.</summary>
        public bool TryGetNode(Guid nodeID, out Web_Node? node)
        {
            node = null;

            if (!initialized || nodes is null)
                return false;

            return nodes.TryGetValue(nodeID, out node);
        }

        /// <summary>Removes a graph node and cleans links that reference it.</summary>
        public bool RemoveExistingNode(Web_Node nodeToRemove)
        {
            ArgumentNullException.ThrowIfNull(nodeToRemove);
            return RemoveExistingNode(nodeToRemove.Get_ID());
        }

        // Removes the node and every link that references it.
        /// <summary>Removes a graph node by identifier and cleans its links.</summary>
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
        /// <summary>Adds a bidirectional basic-class connection between two nodes.</summary>
        public bool AddConnection(Guid sourceID, Guid destinationID)
        {
            return AddConnections(sourceID, destinationID, LinkClass.basic, ForwardLinkTypes.basic.Id, true,
                LinkOrigin.Explicit);
        }

        // Adds a two-way connection between the source and destination nodes.
        /// <summary>Adds a bidirectional connection with the supplied semantic class.</summary>
        public bool AddConnection(Guid sourceID, Guid destinationID, LinkClass linkClass)
        {
            return AddConnections(sourceID, destinationID, linkClass, ForwardLinkTypes.basic.Id, true,
                LinkOrigin.Explicit);
        }

        /// <summary>Adds a bidirectional connection with the supplied class and relationship type.</summary>
        public bool AddConnection(Guid sourceID, Guid destinationID, LinkClass linkClass, int linkType)
        {
            return AddConnections(sourceID, destinationID, linkClass, linkType, true, LinkOrigin.Explicit);
        }

        // Adds a single directed connection from the source node to the destination node.
        /// <summary>Adds a directed basic-class connection between two nodes.</summary>
        public bool AddDirectedConnection(Guid sourceID, Guid destinationID)
        {
            return AddConnections(sourceID, destinationID, LinkClass.basic, ForwardLinkTypes.basic.Id, false,
                LinkOrigin.Explicit);
        }

        /// <summary>Adds a directed connection with the supplied semantic class.</summary>
        public bool AddDirectedConnection(Guid sourceID, Guid destinationID, LinkClass linkClass)
        {
            return AddConnections(sourceID, destinationID, linkClass, ForwardLinkTypes.basic.Id, false,
                LinkOrigin.Explicit);
        }

        /// <summary>Adds a directed connection with the supplied class and relationship type.</summary>
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
        /// <summary>Removes links in both directions between two nodes.</summary>
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
        /// <summary>Removes only the directed link from source to destination.</summary>
        public bool RemoveDirectedConnection(Guid sourceID, Guid destinationID)
        {
            if (!initialized || nodes is null)
                return false;

            return nodes.TryGetValue(sourceID, out Web_Node? source)
                && source.RemoveLinksBetween(sourceID, destinationID);
        }

        /// <summary>Removes links whose destination node no longer exists.</summary>
        public void CleanObsoleteLinks(Guid destinationID)
        {
            if (!initialized || nodes is null)
                return;

            foreach (Web_Node node in nodes.Values.ToList())
                node.Remove_Destination_Link(destinationID);
        }

        /// <summary>Returns a read-only view of the graph node dictionary.</summary>
        public IReadOnlyDictionary<Guid, Web_Node>? GetDictionary()
        {
            if (!initialized || nodes is null)
                return null;

            return new ReadOnlyDictionary<Guid, Web_Node>(nodes);
        }

        /// <summary>Finds nodes that satisfy the supplied optional search criteria.</summary>
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

        /// <summary>Finds nodes accepted by a caller-supplied predicate.</summary>
        public IReadOnlyList<Web_Node> FindNodes(Func<Web_Node, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);

            if (!initialized || nodes is null)
                return Array.Empty<Web_Node>();

            return nodes.Values.Where(predicate).ToList();
        }

        /// <summary>Finds nodes with the specified node type.</summary>
        public IReadOnlyList<Web_Node> FindByType(NodeTypes type)
        {
            return FindNodes(new NodeSearchCriteria { Type = type });
        }

        /// <summary>Finds nodes with the specified lifetime classification.</summary>
        public IReadOnlyList<Web_Node> FindByLifetime(NodeLifetime lifetime)
        {
            return FindNodes(new NodeSearchCriteria { Lifetime = lifetime });
        }

        /// <summary>Finds nodes by case-insensitive label and optional type.</summary>
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

        /// <summary>Attempts to find nodes by label and reports whether any match.</summary>
        public bool TryFindByLabel(string label, out IReadOnlyList<Web_Node> foundNodes)
        {
            foundNodes = FindByLabel(label);
            return foundNodes.Count > 0;
        }

        /// <summary>Finds nodes participating in links of the specified class.</summary>
        public IReadOnlyList<Web_Node> FindByLinkClass(LinkClass linkClass)
        {
            return FindNodes(new NodeSearchCriteria { LinkClass = linkClass });
        }

        /// <summary>Finds nodes connected to the specified node by active links.</summary>
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

        /// <summary>Returns the outgoing links stored by the specified node.</summary>
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

        /// <summary>Replaces the current query chain with a copy of the supplied nodes.</summary>
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

        /// <summary>Replaces the current query chain with a single node.</summary>
        public void BuildQuery(Chain_Node chainNode)
        {
            if (!initialized || chainNode is null)
                return;

            queryChain ??= new LinkedList<Chain_Node>();
            queryChain.AddLast(chainNode);
        }

        /// <summary>Appends a node to the current query chain.</summary>
        public void AddQueryNode(Chain_Node chainNode)
        {
            BuildQuery(chainNode);
        }

        /// <summary>Clears the current query chain.</summary>
        public void ClearQuery()
        {
            if (!initialized)
                return;

            queryChain ??= new LinkedList<Chain_Node>();
            queryChain.Clear();
        }

        /// <summary>Replaces the current response chain with a copy of the supplied nodes.</summary>
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

        /// <summary>Replaces the current response chain with a single node.</summary>
        public void BuildResponse(Chain_Node chainNode)
        {
            if (!initialized || chainNode is null)
                return;

            responseChain ??= new LinkedList<Chain_Node>();
            responseChain.AddLast(chainNode);
        }

        /// <summary>Appends a node to the current response chain.</summary>
        public void AddResponseNode(Chain_Node chainNode)
        {
            BuildResponse(chainNode);
        }

        /// <summary>Clears the current response chain.</summary>
        public void ClearResponse()
        {
            if (!initialized)
                return;

            responseChain ??= new LinkedList<Chain_Node>();
            responseChain.Clear();
        }

        /// <summary>Returns a copy of the current query chain.</summary>
        public LinkedList<Chain_Node>? GetQuery()
        {
            if (!initialized || queryChain is null)
                return null;

            return new LinkedList<Chain_Node>(queryChain);
        }

        /// <summary>Returns a copy of the current response chain.</summary>
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

        /// <summary>Runs lifecycle maintenance with reasoning stages disabled.</summary>
        public MaintenanceReport RunMaintenanceCycle()
        {
            return RunMaintenanceCycle(new MaintenanceOptions());
        }

        /// <summary>Runs lifecycle maintenance and the explicitly selected reasoning stages.</summary>
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
