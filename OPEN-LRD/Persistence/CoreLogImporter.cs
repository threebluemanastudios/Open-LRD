using System.IO;
using System.Text.Json;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.Persistence
{
    internal static class CoreLogImporter
    {
        private const long MaxLogBytes = 64L * 1024L * 1024L;
        private const int MaxNodes = 100000;
        private const int MaxLinks = 500000;
        private const int MaxConversations = 10000;
        private const int MaxChains = 10000;

        public static PersistenceImportResult ImportCore(
            LRD_Core core,
            string path,
            ImportMode mode)
        {
            ArgumentNullException.ThrowIfNull(core);
            PersistenceImportResult result = new PersistenceImportResult { Mode = mode };

            if (!core.initialized)
            {
                result.Errors.Add("The core must be initialized before importing a log.");
                return result;
            }

            // Ontology relations occupy the reserved 1-999 range, so they must
            // be registered before imported IDs can be validated or restored.
            OPEN_LRD.Ontology.OntologyRelations.EnsureRegistered();

            CoreLogDocument? document = ReadDocument<CoreLogDocument>(
                path,
                CoreLogFormat.Core,
                result);

            if (document is null)
                return result;

            Normalize(document);

            if (!ValidateTypeDefinitions(document.TypeDefinitions, result))
                return result;

            if (document.Nodes.Count > MaxNodes
                || document.Links.Count > MaxLinks
                || document.Conversations.Count > MaxConversations)
            {
                result.Errors.Add("The log exceeds the supported item-count limits.");
                return result;
            }

            ApplyTypeDefinitions(document.TypeDefinitions, result);

            NodePlan nodePlan = PlanNodes(core, document.Nodes, mode, result);
            List<Link> acceptedLinks = PlanLinks(
                core,
                document.Links,
                nodePlan,
                mode,
                result);
            Dictionary<Guid, ConversationContext> conversations = PlanConversations(
                core,
                document.Conversations,
                nodePlan,
                mode,
                result);

            if (result.Errors.Count > 0)
                return result;

            if (mode == ImportMode.Replace)
            {
                foreach (Link link in acceptedLinks)
                {
                    if (!nodePlan.NewNodes.TryGetValue(link.SourceID, out Web_Node? source))
                    {
                        result.Errors.Add($"Imported link source '{link.SourceID}' was not found.");
                        return result;
                    }

                    source.Add_Link(link);
                }

                core.ReplaceGraphAndConversations(nodePlan.NewNodes, conversations);
                result.NodesAdded = nodePlan.NewNodes.Count;
                result.LinksAdded = acceptedLinks.Count;
                result.ConversationsAdded = conversations.Count;
            }
            else
            {
                foreach (Web_Node node in nodePlan.NewNodes.Values)
                {
                    if (!core.AddExistingNode(node))
                    {
                        result.Errors.Add($"Node '{node.ID}' could not be added to the core.");
                        return result;
                    }
                }

                foreach (Link link in acceptedLinks)
                {
                    if (!core.TryGetNode(link.SourceID, out Web_Node? source) || source is null)
                    {
                        result.Errors.Add($"Imported link source '{link.SourceID}' was not found.");
                        return result;
                    }

                    source.Add_Link(link);
                }

                foreach (ConversationContext conversation in conversations.Values)
                {
                    if (!core.AddExistingConversation(conversation))
                    {
                        result.Errors.Add(
                            $"Conversation '{conversation.ConversationID}' could not be added to the core.");
                        return result;
                    }
                }

                result.NodesAdded = nodePlan.NewNodes.Count;
                result.LinksAdded = acceptedLinks.Count;
                result.ConversationsAdded = conversations.Count;
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }

        public static PersistenceImportResult ImportTransient(
            LRD_Core core,
            string path,
            ImportMode mode)
        {
            ArgumentNullException.ThrowIfNull(core);
            PersistenceImportResult result = new PersistenceImportResult { Mode = mode };

            if (!core.initialized)
            {
                result.Errors.Add("The core must be initialized before importing a log.");
                return result;
            }

            // Ontology relations occupy the reserved 1-999 range, so they must
            // be registered before imported IDs can be validated or restored.
            OPEN_LRD.Ontology.OntologyRelations.EnsureRegistered();

            TransientLogDocument? document = ReadDocument<TransientLogDocument>(
                path,
                CoreLogFormat.Transient,
                result);

            if (document is null)
                return result;

            Normalize(document);

            if (!ValidateTypeDefinitions(document.TypeDefinitions, result))
                return result;

            if (document.QueryChains.Count > MaxChains || document.ResponseChains.Count > MaxChains)
            {
                result.Errors.Add("The transient log exceeds the supported chain-count limit.");
                return result;
            }

            ApplyTypeDefinitions(document.TypeDefinitions, result);

            List<(ChainLog Log, LinkedList<Chain_Node> Chain)> queryChains =
                BuildChains(document.QueryChains, result);
            List<(ChainLog Log, LinkedList<Chain_Node> Chain)> responseChains =
                BuildChains(document.ResponseChains, result);

            if (result.Errors.Count > 0)
                return result;

            if (mode == ImportMode.Replace)
            {
                LinkedList<Chain_Node> combinedQuery = CombineChains(queryChains);
                LinkedList<Chain_Node> combinedResponse = CombineChains(responseChains);
                core.ReplaceTransientChains(combinedQuery, combinedResponse);
                result.QueryChainsAdded = queryChains.Count;
                result.ResponseChainsAdded = responseChains.Count;
            }
            else
            {
                List<LinkedList<Chain_Node>> existingChains = EnumerateChains(core.GetQuery())
                    .Concat(EnumerateChains(core.GetResponse()))
                    .ToList();
                List<LinkedList<Chain_Node>> addedChains = new List<LinkedList<Chain_Node>>();

                foreach ((ChainLog log, LinkedList<Chain_Node> chain) in queryChains)
                {
                    if (IsDuplicateChain(chain, existingChains, addedChains))
                    {
                        result.ChainsSkipped++;
                        continue;
                    }

                    core.BuildQuery(chain);
                    addedChains.Add(chain);
                    result.QueryChainsAdded++;
                }

                foreach ((ChainLog log, LinkedList<Chain_Node> chain) in responseChains)
                {
                    if (IsDuplicateChain(chain, existingChains, addedChains))
                    {
                        result.ChainsSkipped++;
                        continue;
                    }

                    core.BuildResponse(chain);
                    addedChains.Add(chain);
                    result.ResponseChainsAdded++;
                }
            }

            result.Success = result.Errors.Count == 0;
            return result;
        }

        private static T? ReadDocument<T>(
            string path,
            string expectedFormat,
            PersistenceImportResult result)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                result.Errors.Add("A log path is required.");
                return null;
            }

            string fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                result.Errors.Add($"Log file '{fullPath}' was not found.");
                return null;
            }

            FileInfo info = new FileInfo(fullPath);

            if (info.Length > MaxLogBytes)
            {
                result.Errors.Add($"Log file exceeds the maximum size of {MaxLogBytes} bytes.");
                return null;
            }

            string json;

            try
            {
                json = File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"The log file could not be read: {ex.Message}");
                return null;
            }

            try
            {
                T? document = JsonSerializer.Deserialize<T>(json, LogJson.Options);

                if (document is null)
                {
                    result.Errors.Add("The log file did not contain a document.");
                    return null;
                }

                string? format = document switch
                {
                    CoreLogDocument coreLog => coreLog.Format,
                    TransientLogDocument transientLog => transientLog.Format,
                    _ => null
                };
                int version = document switch
                {
                    CoreLogDocument coreLog => coreLog.Version,
                    TransientLogDocument transientLog => transientLog.Version,
                    _ => 0
                };

                if (!string.Equals(format, expectedFormat, StringComparison.Ordinal))
                {
                    result.Errors.Add($"Expected log format '{expectedFormat}' but found '{format}'.");
                    return null;
                }

                if (version != CoreLogFormat.Version)
                {
                    result.Errors.Add(
                        $"Unsupported log version {version}; expected {CoreLogFormat.Version}.");
                    return null;
                }

                return document;
            }
            catch (JsonException ex)
            {
                result.Errors.Add($"The log file is not valid JSON: {ex.Message}");
                return null;
            }
        }

        private static void Normalize(CoreLogDocument document)
        {
            document.TypeDefinitions ??= new TypeDefinitionLog();
            document.Nodes ??= new List<NodeLog>();
            document.Links ??= new List<LinkLog>();
            document.Conversations ??= new List<ConversationLog>();
            Normalize(document.TypeDefinitions);

            foreach (NodeLog node in document.Nodes)
            {
                node.Aliases ??= new List<string>();
                node.NodeTypeName ??= string.Empty;
            }

            foreach (LinkLog link in document.Links)
            {
                link.ForwardTypeName ??= string.Empty;
                link.InverseTypeName ??= string.Empty;
                link.LinkClassName ??= string.Empty;
            }

            foreach (ConversationLog conversation in document.Conversations)
            {
                conversation.Options ??= new NlpOptionsLog();
                conversation.Mentions ??= new List<MentionLog>();
                conversation.Provenance ??= new List<ProvenanceLog>();

                foreach (MentionLog mention in conversation.Mentions)
                {
                    mention.Label ??= string.Empty;
                    mention.SurfaceText ??= string.Empty;
                    mention.NodeTypeName ??= string.Empty;
                }

                foreach (ProvenanceLog entry in conversation.Provenance)
                {
                    entry.SourceText ??= string.Empty;
                    entry.Status ??= string.Empty;
                    entry.LinkClassName ??= string.Empty;
                    entry.NodeIds ??= new List<Guid>();
                    entry.TemporaryNodeIds ??= new List<Guid>();
                    entry.Diagnostics ??= new List<string>();
                }
            }
        }

        private static void Normalize(TransientLogDocument document)
        {
            document.TypeDefinitions ??= new TypeDefinitionLog();
            document.QueryChains ??= new List<ChainLog>();
            document.ResponseChains ??= new List<ChainLog>();
            Normalize(document.TypeDefinitions);

            foreach (ChainLog chain in document.QueryChains.Concat(document.ResponseChains))
            {
                chain.Nodes ??= new List<NodeLog>();
                chain.Links ??= new List<LinkLog>();

                foreach (NodeLog node in chain.Nodes)
                {
                    node.Aliases ??= new List<string>();
                    node.NodeTypeName ??= string.Empty;
                }

                foreach (LinkLog link in chain.Links)
                {
                    link.ForwardTypeName ??= string.Empty;
                    link.InverseTypeName ??= string.Empty;
                    link.LinkClassName ??= string.Empty;
                }
            }
        }

        private static void Normalize(TypeDefinitionLog definitions)
        {
            definitions.NodeTypes ??= new List<TypeNameLog>();
            definitions.LinkClasses ??= new List<TypeNameLog>();
            definitions.ForwardLinkTypes ??= new List<TypeNameLog>();
            definitions.InverseLinkTypes ??= new List<TypeNameLog>();
            definitions.Relations ??= new List<RelationTypeLog>();

            foreach (TypeNameLog type in definitions.NodeTypes
                .Concat(definitions.LinkClasses)
                .Concat(definitions.ForwardLinkTypes)
                .Concat(definitions.InverseLinkTypes))
            {
                type.Name ??= string.Empty;
            }

            foreach (RelationTypeLog relation in definitions.Relations)
            {
                relation.Name ??= string.Empty;
                relation.InverseName ??= string.Empty;
            }
        }

        private static bool ValidateTypeDefinitions(
            TypeDefinitionLog definitions,
            PersistenceImportResult result)
        {
            ValidateTypeList(definitions.NodeTypes, "node type", allowZero: false, result);
            ValidateTypeList(definitions.LinkClasses, "link class", allowZero: true, result);
            ValidateTypeList(definitions.ForwardLinkTypes, "forward link type", allowZero: true, result);
            ValidateTypeList(definitions.InverseLinkTypes, "inverse link type", allowZero: true, result);

            HashSet<int> relationIds = new HashSet<int>();

            foreach (RelationTypeLog relation in definitions.Relations)
            {
                if (relation.Id <= RelationRegistry.LastReservedOntologyId
                    && !RelationRegistry.IsOntologyRelationRegistered(
                        relation.Id,
                        relation.Name,
                        relation.InverseName))
                {
                    result.Errors.Add(
                        $"Relation ID {relation.Id} falls in the reserved ontology range.");
                }

                if (string.IsNullOrWhiteSpace(relation.Name)
                    || string.IsNullOrWhiteSpace(relation.InverseName))
                {
                    result.Errors.Add("Relation names cannot be empty.");
                }

                if (!relationIds.Add(relation.Id))
                    result.Errors.Add($"Duplicate relation ID {relation.Id}.");
            }

            return result.Errors.Count == 0;
        }

        private static void ValidateTypeList(
            IReadOnlyList<TypeNameLog> types,
            string label,
            bool allowZero,
            PersistenceImportResult result)
        {
            HashSet<int> ids = new HashSet<int>();

            foreach (TypeNameLog type in types)
            {
                if (type.Id < 0 || (!allowZero && type.Id == 0))
                    result.Errors.Add($"Invalid {label} ID {type.Id}.");

                if (string.IsNullOrWhiteSpace(type.Name))
                    result.Errors.Add($"The {label} with ID {type.Id} has an empty name.");

                if (!ids.Add(type.Id))
                    result.Errors.Add($"Duplicate {label} ID {type.Id}.");
            }
        }

        private static void ApplyTypeDefinitions(
            TypeDefinitionLog definitions,
            PersistenceImportResult result)
        {
            ApplyTypeNames(
                definitions.NodeTypes,
                NodeTypes.GetAll().ToList(),
                type => type.Id,
                type => type.Name,
                (id, name) => NodeTypes.Register(id, name),
                "node type",
                result);

            ApplyTypeNames(
                definitions.LinkClasses,
                LinkClass.GetAll().ToList(),
                type => type.Id,
                type => type.Name,
                (id, name) => LinkClass.Register(id, name),
                "link class",
                result);

            ApplyTypeNames(
                definitions.ForwardLinkTypes,
                ForwardLinkTypes.GetAll().ToList(),
                type => type.Id,
                type => type.Name,
                (id, name) => ForwardLinkTypes.Register(id, name),
                "forward link type",
                result);

            ApplyTypeNames(
                definitions.InverseLinkTypes,
                InverseLinkTypes.GetAll().ToList(),
                type => type.Id,
                type => type.Name,
                (id, name) => InverseLinkTypes.Register(id, name),
                "inverse link type",
                result);

            foreach (RelationTypeLog relation in definitions.Relations)
            {
                if (RelationRegistry.TryRestore(
                    relation.Id,
                    relation.Name,
                    relation.InverseName,
                    out string? error))
                {
                    result.TypesRegistered++;
                }
                else
                {
                    result.TypesSkipped++;
                    result.Warnings.Add(
                        $"Relation '{relation.Name}' was kept from existing state: {error}");
                }
            }
        }

        private static void ApplyTypeNames<T>(
            IReadOnlyList<TypeNameLog> imported,
            IList<T> existing,
            Func<T, int> getId,
            Func<T, string> getName,
            Func<int, string, T> register,
            string label,
            PersistenceImportResult result)
        {
            foreach (TypeNameLog type in imported)
            {
                T? existingById = existing.FirstOrDefault(
                    candidate => getId(candidate) == type.Id);

                if (existingById is not null)
                {
                    if (string.Equals(
                        getName(existingById),
                        type.Name,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.TypesSkipped++;
                    result.Warnings.Add(
                        $"{label} ID {type.Id} is already registered as '{getName(existingById)}'.");
                    continue;
                }

                if (existing.Any(candidate => string.Equals(
                    getName(candidate),
                    type.Name,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                existing.Add(register(type.Id, type.Name));
                result.TypesRegistered++;
            }
        }

        private static NodePlan PlanNodes(
            LRD_Core core,
            IReadOnlyList<NodeLog> nodes,
            ImportMode mode,
            PersistenceImportResult result)
        {
            NodePlan plan = new NodePlan();
            IReadOnlyDictionary<Guid, Web_Node>? existingGraph = core.GetDictionary();

            if (mode == ImportMode.Merge && existingGraph is not null)
            {
                foreach (Guid nodeID in existingGraph.Keys)
                    plan.LinkableNodeIds.Add(nodeID);
            }

            HashSet<Guid> fileIds = new HashSet<Guid>();

            foreach (NodeLog log in nodes)
            {
                if (log.Id == Guid.Empty)
                {
                    result.Errors.Add("A node has an empty ID.");
                    continue;
                }

                if (!fileIds.Add(log.Id))
                {
                    result.Errors.Add($"Duplicate node ID '{log.Id}' in the log.");
                    continue;
                }

                NodeTypes? nodeType = ResolveNodeType(log.NodeTypeId, log.NodeTypeName);

                if (nodeType is null)
                {
                    plan.BlockedNodeIds.Add(log.Id);
                    plan.LinkableNodeIds.Remove(log.Id);
                    result.NodesSkipped++;
                    result.Warnings.Add(
                        $"Node '{log.Id}' was skipped because its node type could not be resolved.");
                    continue;
                }

                if (!TryCreateNode(log, nodeType, out Web_Node? importedNode, out string? error))
                {
                    result.Errors.Add($"Node '{log.Id}' is invalid: {error}");
                    continue;
                }

                if (mode == ImportMode.Merge
                    && existingGraph is not null
                    && existingGraph.TryGetValue(log.Id, out Web_Node? existingNode))
                {
                    if (NodeMatches(existingNode, importedNode!))
                    {
                        result.NodesSkipped++;
                        continue;
                    }

                    plan.BlockedNodeIds.Add(log.Id);
                    plan.LinkableNodeIds.Remove(log.Id);
                    result.NodesSkipped++;
                    result.Warnings.Add(
                        $"Node '{log.Id}' was kept from existing state because its data differs.");
                    continue;
                }

                plan.NewNodes[log.Id] = importedNode!;
                plan.LinkableNodeIds.Add(log.Id);
            }

            return plan;
        }

        private static bool TryCreateNode(
            NodeLog log,
            NodeTypes nodeType,
            out Web_Node? node,
            out string? error)
        {
            node = null;
            error = null;

            if (log.Ttl < 0)
            {
                error = "TTL cannot be negative.";
                return false;
            }

            if (!float.IsFinite(log.WeightUp) || log.WeightUp < 0)
            {
                error = "WeightUp must be a finite non-negative value.";
                return false;
            }

            if (!float.IsFinite(log.WeightDown) || log.WeightDown < 0)
            {
                error = "WeightDown must be a finite non-negative value.";
                return false;
            }

            if (!Enum.IsDefined(log.Lifetime))
            {
                error = "Lifetime is not valid.";
                return false;
            }

            if (log.Label is not null && string.IsNullOrWhiteSpace(log.Label))
            {
                error = "Label cannot be whitespace.";
                return false;
            }

            DataPointer? pointer = null;

            if (log.DataPointer is not null)
            {
                if (!TryCreateDataPointer(log.DataPointer, out pointer, out error))
                    return false;
            }

            Web_Node imported = new Web_Node(log.Id, nodeType, log.Lifetime, log.Ttl);
            imported.Set_Label(log.Label);

            foreach (string alias in log.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    error = "Aliases cannot be empty.";
                    return false;
                }

                if (!imported.Add_Alias(alias))
                {
                    error = $"Alias '{alias}' duplicates the node label or another alias.";
                    return false;
                }
            }

            if (log.WeightUp > 0)
                imported.Add_WeightUp(log.WeightUp);

            if (log.WeightDown > 0)
                imported.Add_WeightDown(log.WeightDown);

            if (pointer is not null)
                imported.Set_DataPointer(pointer);

            node = imported;
            return true;
        }

        private static bool TryCreateDataPointer(
            DataPointerLog log,
            out DataPointer? pointer,
            out string? error)
        {
            pointer = null;
            error = null;

            try
            {
                pointer = new DataPointer(
                    new Uri(log.Location, UriKind.Absolute),
                    log.MediaType,
                    log.ByteLength,
                    log.Checksum);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool NodeMatches(Web_Node existing, Web_Node imported)
        {
            DataPointer? existingPointer = existing.Get_DataPointer();
            DataPointer? importedPointer = imported.Get_DataPointer();

            return existing.Get_NodeType().Id == imported.Get_NodeType().Id
                && string.Equals(
                    existing.Get_NodeType().Name,
                    imported.Get_NodeType().Name,
                    StringComparison.Ordinal)
                && existing.Get_Lifetime() == imported.Get_Lifetime()
                && existing.Get_TTL() == imported.Get_TTL()
                && existing.Get_WeightUp().Equals(imported.Get_WeightUp())
                && existing.Get_WeightDown().Equals(imported.Get_WeightDown())
                && string.Equals(existing.Label, imported.Label, StringComparison.Ordinal)
                && existing.Aliases.SequenceEqual(imported.Aliases, StringComparer.Ordinal)
                && Equals(existingPointer, importedPointer);
        }

        private static List<Link> PlanLinks(
            LRD_Core core,
            IReadOnlyList<LinkLog> links,
            NodePlan nodePlan,
            ImportMode mode,
            PersistenceImportResult result)
        {
            List<Link> accepted = new List<Link>();
            HashSet<string> fileKeys = new HashSet<string>(StringComparer.Ordinal);
            IReadOnlyDictionary<Guid, Web_Node>? existingGraph = core.GetDictionary();

            foreach (LinkLog log in links)
            {
                if (log.SourceId == Guid.Empty || log.DestinationId == Guid.Empty)
                {
                    result.Errors.Add("A link has an empty endpoint.");
                    continue;
                }

                if (nodePlan.BlockedNodeIds.Contains(log.SourceId)
                    || nodePlan.BlockedNodeIds.Contains(log.DestinationId))
                {
                    result.LinksSkipped++;
                    result.Warnings.Add(
                        $"Link '{log.SourceId}' -> '{log.DestinationId}' was skipped because an endpoint was kept from existing state.");
                    continue;
                }

                bool sourceAvailable = nodePlan.LinkableNodeIds.Contains(log.SourceId);
                bool destinationAvailable = nodePlan.LinkableNodeIds.Contains(log.DestinationId);

                if (!sourceAvailable || !destinationAvailable)
                {
                    result.Errors.Add(
                        $"Link '{log.SourceId}' -> '{log.DestinationId}' references a missing node.");
                    continue;
                }

                string key = BuildLinkKey(log);

                if (!fileKeys.Add(key))
                {
                    result.LinksSkipped++;
                    result.Warnings.Add(
                        $"Duplicate link '{log.SourceId}' -> '{log.DestinationId}' was skipped.");
                    continue;
                }

                if (mode == ImportMode.Merge
                    && existingGraph is not null
                    && existingGraph.TryGetValue(log.SourceId, out Web_Node? existingSource)
                    && existingSource.Get_ReferenceLinks().Any(link => BuildLinkKey(link) == key))
                {
                    result.LinksSkipped++;
                    continue;
                }

                if (mode == ImportMode.Merge
                    && nodePlan.NewNodes.TryGetValue(log.SourceId, out Web_Node? importedSource)
                    && importedSource.Get_ReferenceLinks().Any(link => BuildLinkKey(link) == key))
                {
                    result.LinksSkipped++;
                    continue;
                }

                if (!TryCreateLink(log, out Link? link, out string? error))
                {
                    result.LinksSkipped++;
                    result.Warnings.Add(
                        $"Link '{log.SourceId}' -> '{log.DestinationId}' was skipped: {error}");
                    continue;
                }

                accepted.Add(link!);
            }

            return accepted;
        }

        private static bool TryCreateLink(
            LinkLog log,
            out Link? link,
            out string? error)
        {
            link = null;
            error = null;

            ForwardLinkTypes? forward = ResolveForwardType(log.ForwardTypeId, log.ForwardTypeName);

            if (forward is null)
            {
                error = "forward link type could not be resolved";
                return false;
            }

            InverseLinkTypes? inverse = ResolveInverseType(log.InverseTypeId, log.InverseTypeName);

            if (inverse is null)
            {
                error = "inverse link type could not be resolved";
                return false;
            }

            LinkClass? classification = ResolveLinkClass(log.LinkClassId, log.LinkClassName);

            if (classification is null)
            {
                error = "link class could not be resolved";
                return false;
            }

            link = new Link(log.SourceId, log.DestinationId, forward, inverse, classification);
            return true;
        }

        private static Dictionary<Guid, ConversationContext> PlanConversations(
            LRD_Core core,
            IReadOnlyList<ConversationLog> conversations,
            NodePlan nodePlan,
            ImportMode mode,
            PersistenceImportResult result)
        {
            Dictionary<Guid, ConversationContext> accepted =
                new Dictionary<Guid, ConversationContext>();
            HashSet<Guid> fileIds = new HashSet<Guid>();
            IReadOnlyDictionary<Guid, ConversationContext>? existing = core.GetConversations();

            foreach (ConversationLog log in conversations)
            {
                if (log.Id == Guid.Empty)
                {
                    result.Errors.Add("A conversation has an empty ID.");
                    continue;
                }

                if (!fileIds.Add(log.Id))
                {
                    result.Errors.Add($"Duplicate conversation ID '{log.Id}' in the log.");
                    continue;
                }

                ConversationContext? conversation = BuildConversation(log, result);

                if (conversation is null)
                    continue;

                if (mode == ImportMode.Merge
                    && existing is not null
                    && existing.ContainsKey(log.Id))
                {
                    result.ConversationsSkipped++;
                    result.Warnings.Add(
                        $"Conversation '{log.Id}' was kept from existing state.");
                    continue;
                }

                WarnMissingConversationNodes(conversation, nodePlan.LinkableNodeIds, result);
                accepted[log.Id] = conversation;
            }

            return accepted;
        }

        private static void WarnMissingConversationNodes(
            ConversationContext conversation,
            IReadOnlySet<Guid> knownNodeIds,
            PersistenceImportResult result)
        {
            HashSet<Guid> referencedNodeIds = new HashSet<Guid>();

            if (conversation.FocusNodeID.HasValue)
                referencedNodeIds.Add(conversation.FocusNodeID.Value);

            if (conversation.LastSubjectNodeID.HasValue)
                referencedNodeIds.Add(conversation.LastSubjectNodeID.Value);

            if (conversation.LastObjectNodeID.HasValue)
                referencedNodeIds.Add(conversation.LastObjectNodeID.Value);

            if (conversation.LastQualifierNodeID.HasValue)
                referencedNodeIds.Add(conversation.LastQualifierNodeID.Value);

            if (conversation.LastAnswerNodeID.HasValue)
                referencedNodeIds.Add(conversation.LastAnswerNodeID.Value);

            foreach (ConversationMention mention in conversation.Mentions)
                referencedNodeIds.Add(mention.NodeID);

            foreach (SessionProvenanceEntry entry in conversation.Provenance)
            {
                AddIfPresent(referencedNodeIds, entry.SubjectNodeID);
                AddIfPresent(referencedNodeIds, entry.ObjectNodeID);
                AddIfPresent(referencedNodeIds, entry.QualifierNodeID);
                AddIfPresent(referencedNodeIds, entry.TrustSourceNodeID);
                AddIfPresent(referencedNodeIds, entry.TopicNodeID);
                referencedNodeIds.UnionWith(entry.NodeIDs);
                referencedNodeIds.UnionWith(entry.TemporaryNodeIDs);
            }

            int missingCount = referencedNodeIds.Count(nodeID => !knownNodeIds.Contains(nodeID));

            if (missingCount > 0)
            {
                result.Warnings.Add(
                    $"Conversation '{conversation.ConversationID}' references {missingCount} "
                    + "node(s) that are not present in the graph; expired temporary nodes are allowed.");
            }
        }

        private static void AddIfPresent(HashSet<Guid> nodeIds, Guid? nodeID)
        {
            if (nodeID.HasValue)
                nodeIds.Add(nodeID.Value);
        }

        private static ConversationContext? BuildConversation(
            ConversationLog log,
            PersistenceImportResult result)
        {
            if (!ValidateNlpOptions(log.Options))
            {
                result.Errors.Add($"Conversation '{log.Id}' has invalid NLP options.");
                return null;
            }

            NlpOptions options = NlpOptionsLogMapper.FromLog(log.Options);
            List<ConversationMention> mentions = new List<ConversationMention>();

            foreach (MentionLog mentionLog in log.Mentions)
            {
                if (mentionLog.NodeId == Guid.Empty)
                {
                    result.Errors.Add($"Conversation '{log.Id}' has a mention with an empty node ID.");
                    return null;
                }

                if (mentionLog.Sequence <= 0 || mentionLog.TurnIndex < 0)
                {
                    result.Errors.Add($"Conversation '{log.Id}' has an invalid mention sequence.");
                    return null;
                }

                if (!Enum.IsDefined((FrameRole)mentionLog.Role))
                {
                    result.Errors.Add($"Conversation '{log.Id}' has an invalid mention role.");
                    return null;
                }

                NodeTypes mentionType = ResolveNodeType(
                    mentionLog.NodeTypeId,
                    mentionLog.NodeTypeName) ?? NodeTypes.Empties;

                if (mentionType == NodeTypes.Empties
                    && !string.Equals(
                        mentionLog.NodeTypeName,
                        NodeTypes.Empties.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Warnings.Add(
                        $"Conversation '{log.Id}' mention type '{mentionLog.NodeTypeName}' was not resolved.");
                }

                mentions.Add(new ConversationMention(
                    mentionLog.NodeId,
                    mentionLog.Label,
                    mentionLog.SurfaceText,
                    mentionType,
                    (FrameRole)mentionLog.Role,
                    mentionLog.TurnIndex,
                    mentionLog.Sequence,
                    mentionLog.FromAnswer));
            }

            List<SessionProvenanceEntry> provenance = new List<SessionProvenanceEntry>();

            foreach (ProvenanceLog entryLog in log.Provenance)
            {
                if (entryLog.Sequence <= 0 || entryLog.TurnIndex < 0)
                {
                    result.Errors.Add($"Conversation '{log.Id}' has an invalid provenance sequence.");
                    return null;
                }

                if (!Enum.TryParse(
                    entryLog.Status,
                    ignoreCase: true,
                    out ReasoningStatus status))
                {
                    result.Errors.Add(
                        $"Conversation '{log.Id}' has an invalid provenance status '{entryLog.Status}'.");
                    return null;
                }

                LinkClass linkClass = ResolveLinkClass(
                    entryLog.LinkClassId,
                    entryLog.LinkClassName) ?? LinkClass.empty;

                provenance.Add(new SessionProvenanceEntry(
                    entryLog.Sequence,
                    entryLog.TurnIndex,
                    entryLog.SentenceIndex,
                    entryLog.SourceText,
                    status,
                    entryLog.RelationName,
                    linkClass,
                    entryLog.ForwardLinkTypeId,
                    entryLog.InverseLinkTypeId,
                    entryLog.SubjectNodeId,
                    entryLog.ObjectNodeId,
                    entryLog.QualifierNodeId,
                    entryLog.TrustSourceNodeId,
                    entryLog.TopicNodeId,
                    entryLog.NodeIds.ToArray(),
                    entryLog.TemporaryNodeIds.ToArray(),
                    entryLog.GraphMutated,
                    entryLog.IsUsableForAnswer,
                    entryLog.Diagnostics.ToArray(),
                    (CoordinationKind)entryLog.Coordination,
                    entryLog.CoordinationGroup,
                    (NegationKind)entryLog.Negation));
            }

            ConversationContext conversation = new ConversationContext(log.Id, options);

            try
            {
                conversation.RestoreState(
                    log.TurnIndex,
                    log.Sequence,
                    log.FocusNodeId,
                    log.LastSubjectNodeId,
                    log.LastObjectNodeId,
                    log.LastQualifierNodeId,
                    log.LastAnswerNodeId,
                    mentions,
                    provenance);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Conversation '{log.Id}' could not be restored: {ex.Message}");
                return null;
            }

            return conversation;
        }

        private static bool ValidateNlpOptions(NlpOptionsLog options)
        {
            return options.MaxCharacters > 0
                && options.MaxSentences > 0
                && options.MaxTokens > 0
                && options.MaxClausesPerSentence > 0
                && options.MaxChainsPerText > 0
                && options.MaxAnswerCandidates > 0
                && options.MaxQuestionVariants > 0
                && options.MaxAnswerTextLength > 0
                && options.TemporaryNodeTimeToLive >= 0
                && options.MaxConversationMentions > 0
                && options.MaxSessionProvenanceEntries > 0
                && options.MaxPronounCandidates > 0
                && options.PronounRecencyWindow > 0
                && double.IsFinite(options.AnswerAmbiguityMargin)
                && double.IsFinite(options.MinimumAnswerScore)
                && double.IsFinite(options.PronounAmbiguityMargin);
        }

        private static List<(ChainLog Log, LinkedList<Chain_Node> Chain)> BuildChains(
            IReadOnlyList<ChainLog> logs,
            PersistenceImportResult result)
        {
            List<(ChainLog Log, LinkedList<Chain_Node> Chain)> chains =
                new List<(ChainLog Log, LinkedList<Chain_Node> Chain)>();

            foreach (ChainLog log in logs)
            {
                LinkedList<Chain_Node>? chain = BuildChain(log, result);

                if (chain is not null)
                    chains.Add((log, chain));
            }

            return chains;
        }

        private static LinkedList<Chain_Node>? BuildChain(
            ChainLog log,
            PersistenceImportResult result)
        {
            if (log.Nodes.Count == 0)
            {
                result.ChainsSkipped++;
                result.Warnings.Add("An empty transient chain was skipped.");
                return null;
            }

            Dictionary<Guid, Chain_Node> nodes = new Dictionary<Guid, Chain_Node>();
            LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();

            foreach (NodeLog nodeLog in log.Nodes)
            {
                if (nodeLog.Id == Guid.Empty)
                {
                    result.Errors.Add("A transient chain node has an empty ID.");
                    return null;
                }

                if (nodes.ContainsKey(nodeLog.Id))
                {
                    result.Errors.Add($"Duplicate transient chain node ID '{nodeLog.Id}'.");
                    return null;
                }

                NodeTypes? nodeType = ResolveNodeType(nodeLog.NodeTypeId, nodeLog.NodeTypeName);

                if (nodeType is null)
                {
                    result.ChainsSkipped++;
                    result.Warnings.Add(
                        $"Transient chain node '{nodeLog.Id}' has an unresolved node type.");
                    return null;
                }

                if (!TryCreateChainNode(nodeLog, nodeType, out Chain_Node? node, out string? error))
                {
                    result.Errors.Add($"Transient chain node '{nodeLog.Id}' is invalid: {error}");
                    return null;
                }

                nodes.Add(nodeLog.Id, node!);
                chain.AddLast(node!);
            }

            HashSet<string> linkKeys = new HashSet<string>(StringComparer.Ordinal);
            HashSet<Guid> sourcesWithNext = new HashSet<Guid>();
            HashSet<Guid> destinationsWithPrevious = new HashSet<Guid>();

            foreach (LinkLog linkLog in log.Links)
            {
                if (!nodes.ContainsKey(linkLog.SourceId)
                    || !nodes.ContainsKey(linkLog.DestinationId))
                {
                    result.Errors.Add(
                        $"Transient chain link '{linkLog.SourceId}' -> '{linkLog.DestinationId}' references a missing node.");
                    return null;
                }

                if (!linkKeys.Add(BuildLinkKey(linkLog)))
                {
                    result.Errors.Add("A transient chain contains a duplicate link.");
                    return null;
                }

                if (!sourcesWithNext.Add(linkLog.SourceId)
                    || !destinationsWithPrevious.Add(linkLog.DestinationId))
                {
                    result.Errors.Add("A transient chain node has multiple incoming or outgoing links.");
                    return null;
                }

                if (!TryCreateLink(linkLog, out Link? link, out string? error) || link is null)
                {
                    result.ChainsSkipped++;
                    result.Warnings.Add($"A transient chain link was skipped: {error}");
                    return null;
                }

                nodes[linkLog.SourceId].Set_LinkNext(link);
                nodes[linkLog.DestinationId].Set_LinkPrevious(link);
            }

            return chain;
        }

        private static bool TryCreateChainNode(
            NodeLog log,
            NodeTypes nodeType,
            out Chain_Node? node,
            out string? error)
        {
            node = null;

            if (!TryCreateNode(log, nodeType, out Web_Node? graphNode, out error)
                || graphNode is null)
            {
                return false;
            }

            Chain_Node chainNode = new Chain_Node(log.Id, nodeType, log.Lifetime, log.Ttl);
            chainNode.Set_Label(log.Label);

            foreach (string alias in log.Aliases)
                chainNode.Add_Alias(alias);

            if (log.WeightUp > 0)
                chainNode.Add_WeightUp(log.WeightUp);

            if (log.WeightDown > 0)
                chainNode.Add_WeightDown(log.WeightDown);

            DataPointer? pointer = graphNode.Get_DataPointer();

            if (pointer is not null)
                chainNode.Set_DataPointer(pointer);

            node = chainNode;
            return true;
        }

        private static LinkedList<Chain_Node> CombineChains(
            IReadOnlyList<(ChainLog Log, LinkedList<Chain_Node> Chain)> chains)
        {
            LinkedList<Chain_Node> combined = new LinkedList<Chain_Node>();

            foreach ((_, LinkedList<Chain_Node> chain) in chains)
            {
                foreach (Chain_Node node in chain)
                    combined.AddLast(node);
            }

            return combined;
        }

        private static IEnumerable<LinkedList<Chain_Node>> EnumerateChains(
            LinkedList<Chain_Node>? chain)
        {
            if (chain is not null && chain.Count > 0)
                yield return chain;
        }

        private static bool IsDuplicateChain(
            LinkedList<Chain_Node> chain,
            IReadOnlyList<LinkedList<Chain_Node>> existingChains,
            IReadOnlyList<LinkedList<Chain_Node>> addedChains)
        {
            return existingChains.Any(existing => ContainsChain(existing, chain))
                || addedChains.Any(added => ContainsChain(added, chain));
        }

        private static bool ContainsChain(
            LinkedList<Chain_Node> existing,
            LinkedList<Chain_Node> candidate)
        {
            if (candidate.Count == 0 || existing.Count < candidate.Count)
                return false;

            Chain_Node[] existingNodes = existing.ToArray();
            Chain_Node[] candidateNodes = candidate.ToArray();

            for (int start = 0; start <= existingNodes.Length - candidateNodes.Length; start++)
            {
                bool matches = true;

                for (int index = 0; index < candidateNodes.Length; index++)
                {
                    Chain_Node existingNode = existingNodes[start + index];
                    Chain_Node candidateNode = candidateNodes[index];

                    if (existingNode.ID != candidateNode.ID
                        || !LinksMatch(existingNode.Get_LinkNext(), candidateNode.Get_LinkNext()))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                    return true;
            }

            return false;
        }

        private static bool LinksMatch(Link? existing, Link? candidate)
        {
            if (existing is null || candidate is null)
                return existing is null && candidate is null;

            return BuildLinkKey(existing) == BuildLinkKey(candidate);
        }

        private static string BuildLinkKey(LinkLog link)
        {
            return string.Join(
                "|",
                link.SourceId.ToString("D"),
                link.DestinationId.ToString("D"),
                link.ForwardTypeId,
                link.InverseTypeId,
                link.LinkClassId);
        }

        private static string BuildLinkKey(Link link)
        {
            return string.Join(
                "|",
                link.SourceID.ToString("D"),
                link.DestinationID.ToString("D"),
                link.ForwardLink.Id,
                link.InverseLink.Id,
                link.Classification.Id);
        }

        private static NodeTypes? ResolveNodeType(int id, string name)
        {
            NodeTypes[] all = NodeTypes.GetAll().ToArray();
            NodeTypes? byId = all.FirstOrDefault(
                type => type.Id == id
                    && string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase));

            if (byId is not null)
                return byId;

            return all.FirstOrDefault(
                type => string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static LinkClass? ResolveLinkClass(int id, string name)
        {
            LinkClass[] all = LinkClass.GetAll().ToArray();
            LinkClass? byId = all.FirstOrDefault(
                linkClass => linkClass.Id == id
                    && string.Equals(linkClass.Name, name, StringComparison.OrdinalIgnoreCase));

            if (byId is not null)
                return byId;

            return all.FirstOrDefault(
                linkClass => string.Equals(linkClass.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static ForwardLinkTypes? ResolveForwardType(int id, string name)
        {
            ForwardLinkTypes[] all = ForwardLinkTypes.GetAll().ToArray();
            ForwardLinkTypes? byId = all.FirstOrDefault(
                linkType => linkType.Id == id
                    && string.Equals(linkType.Name, name, StringComparison.OrdinalIgnoreCase));

            if (byId is not null)
                return byId;

            return all.FirstOrDefault(
                linkType => string.Equals(linkType.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static InverseLinkTypes? ResolveInverseType(int id, string name)
        {
            InverseLinkTypes[] all = InverseLinkTypes.GetAll().ToArray();
            InverseLinkTypes? byId = all.FirstOrDefault(
                linkType => linkType.Id == id
                    && string.Equals(linkType.Name, name, StringComparison.OrdinalIgnoreCase));

            if (byId is not null)
                return byId;

            return all.FirstOrDefault(
                linkType => string.Equals(linkType.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private sealed class NodePlan
        {
            public Dictionary<Guid, Web_Node> NewNodes { get; } = new Dictionary<Guid, Web_Node>();

            public HashSet<Guid> LinkableNodeIds { get; } = new HashSet<Guid>();

            public HashSet<Guid> BlockedNodeIds { get; } = new HashSet<Guid>();
        }
    }
}
