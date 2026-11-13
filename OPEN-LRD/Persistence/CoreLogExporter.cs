using System.IO;
using System.Text;
using System.Text.Json;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Persistence
{
    internal static class CoreLogExporter
    {
        public static CoreLogExportResult Export(
            LRD_Core core,
            string path,
            bool includeTransient = true,
            DateTimeOffset? exportedAtUtc = null)
        {
            ArgumentNullException.ThrowIfNull(core);

            if (!core.initialized)
                throw new InvalidOperationException("The core must be initialized before exporting logs.");

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A log path is required.", nameof(path));

            string corePath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(corePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string? transientPath = includeTransient ? BuildTransientPath(corePath) : null;
            DateTimeOffset timestamp = exportedAtUtc ?? DateTimeOffset.UtcNow;
            TypeDefinitionLog typeDefinitions = BuildTypeDefinitions();
            IReadOnlyDictionary<Guid, Web_Node> graph = core.GetDictionary()
                ?? throw new InvalidOperationException("The core graph is not available.");

            CoreLogDocument coreLog = new CoreLogDocument
            {
                ExportedAtUtc = timestamp,
                TransientFile = transientPath is null ? null : Path.GetFileName(transientPath),
                TypeDefinitions = typeDefinitions,
                Nodes = graph.Values
                    .OrderBy(node => node.ID.ToString("D"), StringComparer.Ordinal)
                    .Select(ToNodeLog)
                    .ToList(),
                Links = BuildLinkLogs(core, graph),
                Conversations = BuildConversationLogs(core)
            };

            List<(string Path, string Content)> documents = new List<(string Path, string Content)>
            {
                (corePath, JsonSerializer.Serialize(coreLog, LogJson.Options))
            };

            TransientLogDocument? transientLog = null;

            if (transientPath is not null)
            {
                transientLog = new TransientLogDocument
                {
                    ExportedAtUtc = timestamp,
                    CoreFile = Path.GetFileName(corePath),
                    TypeDefinitions = typeDefinitions,
                    QueryChains = BuildChainLogs(core.GetQuery()),
                    ResponseChains = BuildChainLogs(core.GetResponse())
                };

                documents.Add((
                    transientPath,
                    JsonSerializer.Serialize(transientLog, LogJson.Options)));
            }

            WriteDocuments(documents);

            return new CoreLogExportResult
            {
                CorePath = corePath,
                TransientPath = transientPath,
                NodeCount = coreLog.Nodes.Count,
                LinkCount = coreLog.Links.Count,
                ConversationCount = coreLog.Conversations.Count,
                QueryChainCount = transientLog?.QueryChains.Count ?? 0,
                ResponseChainCount = transientLog?.ResponseChains.Count ?? 0
            };
        }

        internal static string BuildTransientPath(string corePath)
        {
            string fullPath = Path.GetFullPath(corePath);
            string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            string extension = Path.GetExtension(fullPath);
            return Path.Combine(directory, fileName + "_trans" + extension);
        }

        private static TypeDefinitionLog BuildTypeDefinitions()
        {
            return new TypeDefinitionLog
            {
                NodeTypes = NodeTypes.GetAll()
                    .OrderBy(type => type.Id)
                    .Select(type => new TypeNameLog { Id = type.Id, Name = type.Name })
                    .ToList(),
                LinkClasses = LinkClass.GetAll()
                    .OrderBy(linkClass => linkClass.Id)
                    .Select(linkClass => new TypeNameLog { Id = linkClass.Id, Name = linkClass.Name })
                    .ToList(),
                ForwardLinkTypes = ForwardLinkTypes.GetAll()
                    .OrderBy(linkType => linkType.Id)
                    .Select(linkType => new TypeNameLog { Id = linkType.Id, Name = linkType.Name })
                    .ToList(),
                InverseLinkTypes = InverseLinkTypes.GetAll()
                    .OrderBy(linkType => linkType.Id)
                    .Select(linkType => new TypeNameLog { Id = linkType.Id, Name = linkType.Name })
                    .ToList(),
                Relations = RelationRegistry.GetAll()
                    .OrderBy(relation => relation.Id)
                    .Select(relation => new RelationTypeLog
                    {
                        Id = relation.Id,
                        Name = relation.Name,
                        InverseName = relation.InverseName
                    })
                    .ToList()
            };
        }

        private static List<LinkLog> BuildLinkLogs(
            LRD_Core core,
            IReadOnlyDictionary<Guid, Web_Node> graph)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            List<LinkLog> links = new List<LinkLog>();

            foreach (Web_Node node in graph.Values.OrderBy(
                value => value.ID.ToString("D"),
                StringComparer.Ordinal))
            {
                foreach (Link link in core.GetLinks(node.ID))
                {
                    string key = BuildLinkKey(link);

                    if (!seen.Add(key))
                        continue;

                    links.Add(ToLinkLog(link));
                }
            }

            return links
                .OrderBy(link => link.SourceId.ToString("D"), StringComparer.Ordinal)
                .ThenBy(link => link.DestinationId.ToString("D"), StringComparer.Ordinal)
                .ThenBy(link => link.ForwardTypeId)
                .ThenBy(link => link.InverseTypeId)
                .ThenBy(link => link.LinkClassId)
                .ToList();
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

        private static List<ConversationLog> BuildConversationLogs(LRD_Core core)
        {
            IReadOnlyDictionary<Guid, ConversationContext>? conversations = core.GetConversations();

            if (conversations is null)
                return new List<ConversationLog>();

            return conversations.Values
                .OrderBy(conversation => conversation.ConversationID.ToString("D"), StringComparer.Ordinal)
                .Select(ToConversationLog)
                .ToList();
        }

        private static ConversationLog ToConversationLog(ConversationContext conversation)
        {
            return new ConversationLog
            {
                Id = conversation.ConversationID,
                Options = NlpOptionsLogMapper.ToLog(conversation.Options),
                TurnIndex = conversation.TurnIndex,
                Sequence = conversation.Sequence,
                FocusNodeId = conversation.FocusNodeID,
                LastSubjectNodeId = conversation.LastSubjectNodeID,
                LastObjectNodeId = conversation.LastObjectNodeID,
                LastQualifierNodeId = conversation.LastQualifierNodeID,
                LastAnswerNodeId = conversation.LastAnswerNodeID,
                Mentions = conversation.Mentions
                    .OrderBy(mention => mention.Sequence)
                    .Select(ToMentionLog)
                    .ToList(),
                Provenance = conversation.Provenance
                    .OrderBy(entry => entry.Sequence)
                    .Select(ToProvenanceLog)
                    .ToList()
            };
        }

        private static MentionLog ToMentionLog(ConversationMention mention)
        {
            return new MentionLog
            {
                NodeId = mention.NodeID,
                Label = mention.Label,
                SurfaceText = mention.SurfaceText,
                NodeTypeId = mention.NodeType.Id,
                NodeTypeName = mention.NodeType.Name,
                Role = (int)mention.Role,
                TurnIndex = mention.TurnIndex,
                Sequence = mention.Sequence,
                FromAnswer = mention.FromAnswer
            };
        }

        private static ProvenanceLog ToProvenanceLog(SessionProvenanceEntry entry)
        {
            return new ProvenanceLog
            {
                Sequence = entry.Sequence,
                TurnIndex = entry.TurnIndex,
                SentenceIndex = entry.SentenceIndex,
                SourceText = entry.SourceText,
                Status = entry.Status.ToString(),
                RelationName = entry.RelationName,
                LinkClassId = entry.LinkClass.Id,
                LinkClassName = entry.LinkClass.Name,
                ForwardLinkTypeId = entry.ForwardLinkTypeId,
                InverseLinkTypeId = entry.InverseLinkTypeId,
                SubjectNodeId = entry.SubjectNodeID,
                ObjectNodeId = entry.ObjectNodeID,
                QualifierNodeId = entry.QualifierNodeID,
                TrustSourceNodeId = entry.TrustSourceNodeID,
                TopicNodeId = entry.TopicNodeID,
                NodeIds = entry.NodeIDs.ToList(),
                TemporaryNodeIds = entry.TemporaryNodeIDs.ToList(),
                GraphMutated = entry.GraphMutated,
                IsUsableForAnswer = entry.IsUsableForAnswer,
                Diagnostics = entry.Diagnostics.ToList(),
                Coordination = (int)entry.Coordination,
                CoordinationGroup = entry.CoordinationGroup,
                Negation = (int)entry.Negation
            };
        }

        private static List<ChainLog> BuildChainLogs(LinkedList<Chain_Node>? chain)
        {
            if (chain is null || chain.Count == 0)
                return new List<ChainLog>();

            ChainLog result = new ChainLog();

            foreach (Chain_Node node in chain)
            {
                result.Nodes.Add(ToNodeLog(node));

                Link? next = node.Get_LinkNext();

                if (next is not null)
                    result.Links.Add(ToLinkLog(next));
            }

            return new List<ChainLog> { result };
        }

        private static NodeLog ToNodeLog(Node node)
        {
            DataPointer? pointer = node.Get_DataPointer();

            return new NodeLog
            {
                Id = node.ID,
                NodeTypeId = node.Get_NodeType().Id,
                NodeTypeName = node.Get_NodeType().Name,
                Lifetime = node.Get_Lifetime(),
                Ttl = node.Get_TTL(),
                WeightUp = node.Get_WeightUp(),
                WeightDown = node.Get_WeightDown(),
                Label = node.Label,
                Aliases = node.Aliases.ToList(),
                DataPointer = pointer is null
                    ? null
                    : new DataPointerLog
                    {
                        Location = pointer.Location.AbsoluteUri,
                        MediaType = pointer.MediaType,
                        ByteLength = pointer.ByteLength,
                        Checksum = pointer.Checksum
                    }
            };
        }

        private static LinkLog ToLinkLog(Link link)
        {
            return new LinkLog
            {
                SourceId = link.SourceID,
                DestinationId = link.DestinationID,
                ForwardTypeId = link.ForwardLink.Id,
                ForwardTypeName = link.ForwardLink.Name,
                InverseTypeId = link.InverseLink.Id,
                InverseTypeName = link.InverseLink.Name,
                LinkClassId = link.Classification.Id,
                LinkClassName = link.Classification.Name
            };
        }

        private static void WriteDocuments(IReadOnlyList<(string Path, string Content)> documents)
        {
            List<(string TempPath, string TargetPath)> pending =
                new List<(string TempPath, string TargetPath)>();

            try
            {
                foreach ((string path, string content) in documents)
                {
                    string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
                    File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    pending.Add((tempPath, path));
                }

                foreach ((string tempPath, string targetPath) in pending)
                    File.Move(tempPath, targetPath, overwrite: true);
            }
            finally
            {
                foreach ((string tempPath, _) in pending)
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }
        }
    }
}
