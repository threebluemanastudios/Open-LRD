using System.Text.Json;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Persistence;
using OPEN_LRD.Reasoning;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.SmokeTests
{
    internal static class PersistenceTests
    {
        private static int nextTypeId = 600000;

        public static void RunAll()
        {
            Program.Run("Persistence core round trip", TestCoreRoundTrip);
            Program.Run("Persistence replace import", TestReplaceImport);
            Program.Run("Persistence merge conflicts", TestMergeConflicts);
            Program.Run("Persistence idempotent merge", TestIdempotentMerge);
            Program.Run("Persistence invalid input safety", TestInvalidInputSafety);
            Program.Run("Persistence oversized input", TestOversizedInput);
            Program.Run("Persistence import above legacy size cap", TestImportAboveLegacySizeCap);
            Program.Run("Persistence deterministic export", TestDeterministicExport);
            Program.Run("Persistence segmented export and manifest import", TestSegmentedExportRoundTrip);
            Program.Run("Persistence oversized split record preserves prior export", TestOversizedSegmentRecord);
            Program.Run("Persistence large graph streaming export", TestLargeGraphStreamingExport);
            Program.Run("Persistence transient round trip", TestTransientRoundTrip);
            Program.Run("Persistence transient merge and replace", TestTransientMergeAndReplace);
            Program.Run("Persistence malformed transient rejection", TestMalformedTransient);
            Program.Run("Visualization link distance defaults", TestLinkDistanceDefaults);
            Program.Run("Visualization link distance overrides", TestLinkDistanceOverrides);
            Program.Run("Visualization link distance fallback", TestLinkDistanceFallback);
            Program.Run("Visualization link distance validation", TestLinkDistanceValidation);
            Program.Run("Visualization link distance concurrency", TestLinkDistanceConcurrency);
        }

        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        private static int NextTypeId()
        {
            return Interlocked.Increment(ref nextTypeId);
        }

        private static string NewTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "open-lrd-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        private static Web_Node AddNode(
            LRD_Core core,
            Guid id,
            NodeTypes type,
            NodeLifetime lifetime,
            int ttl,
            string? label = null,
            DataPointer? pointer = null)
        {
            Web_Node node = new Web_Node(id, type, lifetime, ttl);

            if (label is not null)
                node.Set_Label(label);

            if (pointer is not null)
                node.Set_DataPointer(pointer);

            Program.Assert(core.AddExistingNode(node), $"node '{id}' should be added");
            return node;
        }

        private static ConversationContext AddConversation(
            LRD_Core core,
            Guid id,
            NlpOptions? options = null)
        {
            ConversationContext conversation = new ConversationContext(id, options);
            Program.Assert(
                core.AddExistingConversation(conversation),
                $"conversation '{id}' should be added");
            return conversation;
        }

        private static void WriteJson<T>(string path, T document)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(document, LogJson.Options));
        }

        private static CoreLogDocument ReadCoreDocument(string path)
        {
            CoreLogDocument? document = JsonSerializer.Deserialize<CoreLogDocument>(
                File.ReadAllText(path),
                LogJson.Options);
            Program.Assert(document is not null, "exported core log should deserialize");
            return document!;
        }

        private static NodeLog BuildNodeLog(Guid id, string label)
        {
            return new NodeLog
            {
                Id = id,
                NodeTypeId = NodeTypes.Basics.Id,
                NodeTypeName = NodeTypes.Basics.Name,
                Lifetime = NodeLifetime.Temporary,
                Ttl = 10,
                Label = label
            };
        }

        private static LinkLog BuildLinkLog(
            Guid source,
            Guid destination,
            string forwardName,
            string inverseName,
            int classId,
            string className)
        {
            return new LinkLog
            {
                SourceId = source,
                DestinationId = destination,
                ForwardTypeId = 0,
                ForwardTypeName = forwardName,
                InverseTypeId = 0,
                InverseTypeName = inverseName,
                LinkClassId = classId,
                LinkClassName = className
            };
        }

        private static void TestCoreRoundTrip()
        {
            string directory = NewTempDirectory();

            try
            {
                LRD_Core core = NewCore();
                string path = Path.Combine(directory, "round-trip.lrdlog");
                Guid sourceID = Guid.NewGuid();
                Guid destinationID = Guid.NewGuid();
                Guid conversationID = Guid.NewGuid();
                NodeTypes customType = NodeTypes.Register(
                    NextTypeId(),
                    "PersistenceType" + Guid.NewGuid().ToString("N"));
                RelationLinkType relation = RelationRegistry.GetOrRegister(
                    "persist_relation_" + Guid.NewGuid().ToString("N"));

                Web_Node source = AddNode(
                    core,
                    sourceID,
                    customType,
                    NodeLifetime.Permanent,
                    0,
                    "Persistence Alpha",
                    new DataPointer(
                        new Uri("https://example.com/data.bin"),
                        "application/octet-stream",
                        42,
                        "checksum-42"));
                Program.Assert(source.Add_Alias("alpha-alias"), "source alias should be added");
                source.Add_WeightUp(3);
                source.Add_WeightDown(2);

                AddNode(
                    core,
                    destinationID,
                    NodeTypes.Events,
                    NodeLifetime.Temporary,
                    321,
                    "Persistence Beta");

                source.Add_Link(new Link(
                    sourceID,
                    destinationID,
                    relation.Forward,
                    relation.Inverse,
                    LinkClass.spatial));

                NlpOptions options = new NlpOptions
                {
                    FirstPersonSpeakerLabel = "Persistence Speaker",
                    MaxConversationMentions = 64,
                    MaxSessionProvenanceEntries = 64
                };
                ConversationContext conversation = AddConversation(core, conversationID, options);
                conversation.BeginTurn();
                conversation.RecordMention(new ConversationMention(
                    sourceID,
                    "Persistence Alpha",
                    "alpha",
                    customType,
                    FrameRole.Subject,
                    1,
                    0,
                    false));
                conversation.RecordMention(new ConversationMention(
                    destinationID,
                    "Persistence Beta",
                    "beta",
                    NodeTypes.Events,
                    FrameRole.Object,
                    1,
                    0,
                    true));
                conversation.SetFocus(sourceID, FrameRole.Subject, false);
                conversation.RecordProvenance(new SessionProvenanceEntry(
                    0,
                    1,
                    0,
                    "Persistence Alpha is linked to Persistence Beta.",
                    ReasoningStatus.Answered,
                    relation.Name,
                    LinkClass.spatial,
                    relation.Forward.Id,
                    relation.Inverse.Id,
                    sourceID,
                    destinationID,
                    null,
                    null,
                    null,
                    new[] { sourceID, destinationID },
                    Array.Empty<Guid>(),
                    true,
                    true,
                    new[] { "round-trip" }));

                CoreLogExportResult export = CoreLogExporter.Export(
                    core,
                    path,
                    includeTransient: false,
                    new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

                Program.Assert(export.NodeCount == 2, "export should contain two nodes");
                Program.Assert(export.LinkCount == 1, "export should contain one link");
                Program.Assert(export.ConversationCount == 1, "export should contain one conversation");
                Program.Assert(export.TransientPath is null, "transient export should be optional");
                Program.Assert(File.Exists(path), "core log should be written");

                CoreLogDocument document = ReadCoreDocument(path);
                Program.Assert(
                    document.TypeDefinitions.NodeTypes.Any(
                        type => type.Id == customType.Id && type.Name == customType.Name),
                    "export should include the runtime node type");
                Program.Assert(
                    document.TypeDefinitions.Relations.Any(
                        entry => entry.Id == relation.Id && entry.Name == relation.Name),
                    "export should include the runtime relation");

                NewCore();
                PersistenceImportResult import = CoreLogImporter.ImportCore(
                    LRD_Core.Instance,
                    path,
                    ImportMode.Replace);

                Program.Assert(import.Success, "replace import should succeed");
                Program.Assert(import.NodesAdded == 2, "replace import should restore both nodes");
                Program.Assert(import.LinksAdded == 1, "replace import should restore the link");
                Program.Assert(import.ConversationsAdded == 1, "replace import should restore the conversation");

                Program.Assert(
                    LRD_Core.Instance.TryGetNode(sourceID, out Web_Node? restoredSource),
                    "restored source node should exist");
                Web_Node restored = restoredSource!;
                Program.Assert(restored.Label == "Persistence Alpha", "node label should round trip");
                Program.Assert(restored.Get_NodeType().Id == customType.Id, "node type should round trip");
                Program.Assert(restored.Get_Lifetime() == NodeLifetime.Permanent, "lifetime should round trip");
                Program.Assert(restored.Get_TTL() == 0, "TTL should round trip");
                Program.Assert(restored.Get_WeightUp() == 3, "weight up should round trip");
                Program.Assert(restored.Get_WeightDown() == 2, "weight down should round trip");
                Program.Assert(restored.Aliases.Contains("alpha-alias"), "aliases should round trip");
                Program.Assert(
                    restored.Get_DataPointer() is not null
                        && restored.Get_DataPointer()!.Checksum == "checksum-42"
                        && restored.Get_DataPointer()!.ByteLength == 42,
                    "DataPointer should round trip");

                Link restoredLink = LRD_Core.Instance.GetLinks(sourceID)
                    .Single(link => link.DestinationID == destinationID);
                Program.Assert(restoredLink.ForwardLink.Id == relation.Id, "forward relation should round trip");
                Program.Assert(restoredLink.InverseLink.Id == relation.Id, "inverse relation should round trip");
                Program.Assert(
                    restoredLink.Classification == LinkClass.spatial,
                    "link class should round trip");

                ConversationContext restoredConversation =
                    LRD_Core.Instance.GetConversation(conversationID);
                Program.Assert(
                    restoredConversation.Options.FirstPersonSpeakerLabel == "Persistence Speaker",
                    "conversation options should round trip");
                Program.Assert(restoredConversation.TurnIndex == 1, "turn index should round trip");
                Program.Assert(restoredConversation.Sequence == 3, "sequence should round trip");
                Program.Assert(restoredConversation.FocusNodeID == sourceID, "focus should round trip");
                Program.Assert(restoredConversation.Mentions.Count == 2, "mentions should round trip");
                Program.Assert(restoredConversation.Provenance.Count == 1, "provenance should round trip");
                Program.Assert(
                    restoredConversation.Provenance[0].LinkClass == LinkClass.spatial,
                    "provenance link class should round trip");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestReplaceImport()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "replace.lrdlog");
                Guid importedID = Guid.NewGuid();
                Guid importedConversationID = Guid.NewGuid();
                LRD_Core sourceCore = NewCore();
                AddNode(
                    sourceCore,
                    importedID,
                    NodeTypes.Concepts,
                    NodeLifetime.Permanent,
                    0,
                    "Imported Replace");
                AddConversation(sourceCore, importedConversationID);
                CoreLogExporter.Export(sourceCore, path, includeTransient: false);

                LRD_Core target = NewCore();
                Guid staleID = Guid.NewGuid();
                AddNode(target, staleID, NodeTypes.Basics, NodeLifetime.Temporary, 100, "Stale");
                AddConversation(target, Guid.NewGuid());
                Chain_Node staleChain = new Chain_Node(Guid.NewGuid(), NodeTypes.Basics, 10);
                target.BuildQuery(staleChain);

                PersistenceImportResult import = CoreLogImporter.ImportCore(
                    target,
                    path,
                    ImportMode.Replace);

                Program.Assert(import.Success, "replace import should succeed");
                Program.Assert(target.GetExistingNode(staleID) is null, "replace should remove stale nodes");
                Program.Assert(
                    target.GetDictionary()!.Count == 1,
                    "replace should leave only imported graph nodes");
                Program.Assert(
                    target.GetConversations()!.Count == 1
                        && target.GetConversations()!.ContainsKey(importedConversationID),
                    "replace should install imported conversations");
                Program.Assert(
                    target.GetQuery() is not null && target.GetQuery()!.Count == 0,
                    "replace should clear transient query chains");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestMergeConflicts()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "merge.lrdlog");
                Guid conflictID = Guid.NewGuid();
                Guid newSourceID = Guid.NewGuid();
                Guid newDestinationID = Guid.NewGuid();
                Guid conflictConversationID = Guid.NewGuid();
                LRD_Core sourceCore = NewCore();

                AddNode(sourceCore, conflictID, NodeTypes.Concepts, NodeLifetime.Permanent, 0, "Imported Conflict");
                AddNode(sourceCore, newSourceID, NodeTypes.Concepts, NodeLifetime.Permanent, 0, "Imported New Source");
                AddNode(
                    sourceCore,
                    newDestinationID,
                    NodeTypes.Concepts,
                    NodeLifetime.Permanent,
                    0,
                    "Imported New Destination");
                sourceCore.GetExistingNode(conflictID)!.Add_Link(
                    new Link(conflictID, newSourceID, LinkClass.basic));
                sourceCore.GetExistingNode(newSourceID)!.Add_Link(
                    new Link(newSourceID, newDestinationID, LinkClass.events));
                AddConversation(sourceCore, conflictConversationID);
                CoreLogExporter.Export(sourceCore, path, includeTransient: false);

                LRD_Core target = NewCore();
                AddNode(target, conflictID, NodeTypes.Concepts, NodeLifetime.Permanent, 0, "Existing Conflict");
                AddConversation(target, conflictConversationID);

                PersistenceImportResult import = CoreLogImporter.ImportCore(
                    target,
                    path,
                    ImportMode.Merge);

                Program.Assert(import.Success, "merge import should succeed");
                Program.Assert(import.NodesSkipped >= 1, "conflicting node should be skipped");
                Program.Assert(import.LinksSkipped >= 1, "link touching a conflict should be skipped");
                Program.Assert(import.ConversationsSkipped >= 1, "conflicting conversation should be skipped");
                Program.Assert(
                    target.GetExistingNode(conflictID)!.Label == "Existing Conflict",
                    "merge should keep the existing conflicting node");
                Program.Assert(
                    target.TryGetNode(newSourceID, out _),
                    "merge should add non-conflicting nodes");
                Program.Assert(
                    target.GetLinks(conflictID).Count == 0,
                    "merge should skip links touching the conflicting node");
                Program.Assert(
                    target.GetLinks(newSourceID).Any(
                        link => link.DestinationID == newDestinationID),
                    "merge should import links between non-conflicting nodes");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestIdempotentMerge()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "idempotent.lrdlog");
                Guid firstID = Guid.NewGuid();
                Guid secondID = Guid.NewGuid();
                Guid conversationID = Guid.NewGuid();
                LRD_Core sourceCore = NewCore();
                AddNode(sourceCore, firstID, NodeTypes.Concepts, NodeLifetime.Permanent, 0, "First");
                AddNode(sourceCore, secondID, NodeTypes.Events, NodeLifetime.Temporary, 50, "Second");
                sourceCore.GetExistingNode(firstID)!.Add_Link(
                    new Link(firstID, secondID, LinkClass.events));
                AddConversation(sourceCore, conversationID);
                CoreLogExporter.Export(sourceCore, path, includeTransient: false);

                LRD_Core target = NewCore();
                PersistenceImportResult first = CoreLogImporter.ImportCore(
                    target,
                    path,
                    ImportMode.Merge);
                PersistenceImportResult second = CoreLogImporter.ImportCore(
                    target,
                    path,
                    ImportMode.Merge);

                Program.Assert(first.Success && second.Success, "both merge imports should succeed");
                Program.Assert(first.NodesAdded == 2, "first merge should add both nodes");
                Program.Assert(second.NodesAdded == 0, "second merge should add no nodes");
                Program.Assert(second.LinksAdded == 0, "second merge should add no links");
                Program.Assert(second.ConversationsAdded == 0, "second merge should add no conversations");
                Program.Assert(target.GetDictionary()!.Count == 2, "merge should not duplicate nodes");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestInvalidInputSafety()
        {
            string directory = NewTempDirectory();

            try
            {
                LRD_Core core = NewCore();
                Guid stableID = Guid.NewGuid();
                Guid stableConversationID = Guid.NewGuid();
                AddNode(core, stableID, NodeTypes.Basics, NodeLifetime.Permanent, 0, "Stable");
                AddConversation(core, stableConversationID);

                string malformedPath = Path.Combine(directory, "malformed.lrdlog");
                File.WriteAllText(malformedPath, "{ not valid json");
                PersistenceImportResult malformed = CoreLogImporter.ImportCore(
                    core,
                    malformedPath,
                    ImportMode.Replace);
                AssertStateUnchanged(core, stableID, stableConversationID, malformed);

                string versionPath = Path.Combine(directory, "version.lrdlog");
                CoreLogDocument versionDocument = new CoreLogDocument { Version = 2 };
                WriteJson(versionPath, versionDocument);
                PersistenceImportResult version = CoreLogImporter.ImportCore(
                    core,
                    versionPath,
                    ImportMode.Replace);
                AssertStateUnchanged(core, stableID, stableConversationID, version);

                string emptyIDPath = Path.Combine(directory, "empty-id.lrdlog");
                CoreLogDocument emptyIDDocument = new CoreLogDocument
                {
                    Nodes = new List<NodeLog> { BuildNodeLog(Guid.Empty, "Empty") }
                };
                WriteJson(emptyIDPath, emptyIDDocument);
                PersistenceImportResult emptyID = CoreLogImporter.ImportCore(
                    core,
                    emptyIDPath,
                    ImportMode.Replace);
                AssertStateUnchanged(core, stableID, stableConversationID, emptyID);

                string duplicateIDPath = Path.Combine(directory, "duplicate-id.lrdlog");
                Guid duplicateNodeID = Guid.NewGuid();
                CoreLogDocument duplicateIDDocument = new CoreLogDocument
                {
                    Nodes = new List<NodeLog>
                    {
                        BuildNodeLog(duplicateNodeID, "Duplicate One"),
                        BuildNodeLog(duplicateNodeID, "Duplicate Two")
                    }
                };
                WriteJson(duplicateIDPath, duplicateIDDocument);
                PersistenceImportResult duplicateID = CoreLogImporter.ImportCore(
                    core,
                    duplicateIDPath,
                    ImportMode.Replace);
                AssertStateUnchanged(core, stableID, stableConversationID, duplicateID);

                string missingLinkPath = Path.Combine(directory, "missing-link.lrdlog");
                Guid linkSourceID = Guid.NewGuid();
                CoreLogDocument missingLinkDocument = new CoreLogDocument
                {
                    Nodes = new List<NodeLog> { BuildNodeLog(linkSourceID, "Link Source") },
                    Links = new List<LinkLog>
                    {
                        BuildLinkLog(
                            linkSourceID,
                            Guid.NewGuid(),
                            ForwardLinkTypes.basic.Name,
                            InverseLinkTypes.basic.Name,
                            LinkClass.basic.Id,
                            LinkClass.basic.Name)
                    }
                };
                WriteJson(missingLinkPath, missingLinkDocument);
                PersistenceImportResult missingLink = CoreLogImporter.ImportCore(
                    core,
                    missingLinkPath,
                    ImportMode.Replace);
                AssertStateUnchanged(core, stableID, stableConversationID, missingLink);

                string duplicateTypePath = Path.Combine(directory, "duplicate-type.lrdlog");
                CoreLogDocument duplicateTypeDocument = new CoreLogDocument();
                duplicateTypeDocument.TypeDefinitions.NodeTypes.Add(
                    new TypeNameLog { Id = 700001, Name = "DuplicateType" });
                duplicateTypeDocument.TypeDefinitions.NodeTypes.Add(
                    new TypeNameLog { Id = 700001, Name = "DuplicateTypeTwo" });
                WriteJson(duplicateTypePath, duplicateTypeDocument);
                PersistenceImportResult duplicateType = CoreLogImporter.ImportCore(
                    core,
                    duplicateTypePath,
                    ImportMode.Replace);
                AssertStateUnchanged(core, stableID, stableConversationID, duplicateType);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void AssertStateUnchanged(
            LRD_Core core,
            Guid nodeID,
            Guid conversationID,
            PersistenceImportResult result)
        {
            Program.Assert(!result.Success, "invalid import should fail");
            Program.Assert(result.Errors.Count > 0, "invalid import should report an error");
            Program.Assert(
                core.GetDictionary()!.Count == 1 && core.GetDictionary()!.ContainsKey(nodeID),
                "failed import should not change graph state");
            Program.Assert(
                core.GetConversations()!.Count == 1
                    && core.GetConversations()!.ContainsKey(conversationID),
                "failed import should not change conversation state");
        }

        private static void TestOversizedInput()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "oversized.lrdlog");

                using (FileStream stream = File.Create(path))
                    stream.SetLength(CoreLogImporter.MaxLogBytes + 1L);

                LRD_Core core = NewCore();
                PersistenceImportResult result = CoreLogImporter.ImportCore(
                    core,
                    path,
                    ImportMode.Merge);

                Program.Assert(!result.Success, "oversized input should fail");
                Program.Assert(
                    result.Errors.Any(error => error.Contains("maximum size", StringComparison.OrdinalIgnoreCase)),
                    "oversized input should report the size limit");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestDeterministicExport()
        {
            string directory = NewTempDirectory();

            try
            {
                LRD_Core core = NewCore();
                Guid firstID = Guid.NewGuid();
                Guid secondID = Guid.NewGuid();
                AddNode(core, firstID, NodeTypes.Concepts, NodeLifetime.Permanent, 0, "Deterministic First");
                AddNode(core, secondID, NodeTypes.Events, NodeLifetime.Temporary, 20, "Deterministic Second");
                core.GetExistingNode(firstID)!.Add_Link(
                    new Link(firstID, secondID, LinkClass.events));
                AddConversation(core, Guid.NewGuid());

                string corePath = Path.Combine(directory, "deterministic.lrdlog");
                string transientPath = CoreLogExporter.BuildTransientPath(corePath);
                DateTimeOffset timestamp = new DateTimeOffset(
                    2026,
                    2,
                    3,
                    4,
                    5,
                    6,
                    TimeSpan.Zero);

                CoreLogExporter.Export(core, corePath, includeTransient: true, timestamp);
                string firstCore = File.ReadAllText(corePath);
                string firstTransient = File.ReadAllText(transientPath);

                CoreLogExporter.Export(core, corePath, includeTransient: true, timestamp);
                string secondCore = File.ReadAllText(corePath);
                string secondTransient = File.ReadAllText(transientPath);

                Program.Assert(firstCore == secondCore, "core export should be deterministic");
                Program.Assert(firstTransient == secondTransient, "transient export should be deterministic");
                Program.Assert(transientPath.EndsWith("_trans.lrdlog", StringComparison.Ordinal), "transient path should use the companion suffix");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestTransientRoundTrip()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "transient.lrdlog");
                Guid firstID = Guid.NewGuid();
                Guid secondID = Guid.NewGuid();
                Guid responseID = Guid.NewGuid();
                DateTimeOffset timestamp = new DateTimeOffset(
                    2026,
                    3,
                    4,
                    5,
                    6,
                    7,
                    TimeSpan.Zero);
                LRD_Core sourceCore = NewCore();
                Chain_Node first = new Chain_Node(
                    firstID,
                    NodeTypes.Concepts,
                    NodeLifetime.Temporary,
                    77);
                first.Set_Label("Transient First");
                Program.Assert(first.Add_Alias("transient-alias"), "chain alias should be added");
                first.Add_WeightUp(4);
                first.Add_WeightDown(1);
                first.Set_DataPointer(new DataPointer(
                    new Uri("https://example.com/transient.bin"),
                    "application/octet-stream",
                    7,
                    "transient-checksum"));

                Chain_Node second = new Chain_Node(
                    secondID,
                    NodeTypes.Events,
                    NodeLifetime.Permanent,
                    0);
                second.Set_Label("Transient Second");

                Link link = new Link(
                    firstID,
                    secondID,
                    ForwardLinkTypes.basic,
                    InverseLinkTypes.basic,
                    LinkClass.temporal);
                first.Set_LinkNext(link);
                second.Set_LinkPrevious(link);

                LinkedList<Chain_Node> queryChain = new LinkedList<Chain_Node>();
                queryChain.AddLast(first);
                queryChain.AddLast(second);
                sourceCore.BuildQuery(queryChain);
                sourceCore.BuildResponse(new Chain_Node(
                    responseID,
                    NodeTypes.Sources,
                    NodeLifetime.Temporary,
                    12));

                CoreLogExportResult export = CoreLogExporter.Export(
                    sourceCore,
                    path,
                    includeTransient: true,
                    timestamp);
                Program.Assert(export.TransientPath is not null, "transient path should be returned");
                Program.Assert(File.Exists(export.TransientPath), "transient log should be written");
                Program.Assert(export.QueryChainCount == 1, "query chain should be exported");
                Program.Assert(export.ResponseChainCount == 1, "response chain should be exported");

                NewCore();
                PersistenceImportResult import = CoreLogImporter.ImportTransient(
                    LRD_Core.Instance,
                    export.TransientPath!,
                    ImportMode.Replace);

                Program.Assert(import.Success, "transient replace import should succeed");
                Program.Assert(import.QueryChainsAdded == 1, "query chain should be restored");
                Program.Assert(import.ResponseChainsAdded == 1, "response chain should be restored");

                LinkedList<Chain_Node>? restoredQuery = LRD_Core.Instance.GetQuery();
                Program.Assert(
                    restoredQuery is not null && restoredQuery.Count == 2,
                    "restored query chain should contain two nodes");
                Chain_Node restoredFirst = restoredQuery!.First!.Value;
                Chain_Node restoredSecond = restoredQuery.Last!.Value;
                Program.Assert(restoredFirst.ID == firstID, "first chain node ID should round trip");
                Program.Assert(restoredFirst.Label == "Transient First", "chain label should round trip");
                Program.Assert(
                    restoredFirst.Get_NodeType().Id == NodeTypes.Concepts.Id,
                    "chain node type should round trip");
                Program.Assert(
                    restoredFirst.Get_Lifetime() == NodeLifetime.Temporary
                        && restoredFirst.Get_TTL() == 77,
                    "chain lifetime and TTL should round trip");
                Program.Assert(restoredFirst.Get_WeightUp() == 4, "chain weight up should round trip");
                Program.Assert(restoredFirst.Get_WeightDown() == 1, "chain weight down should round trip");
                Program.Assert(restoredFirst.Aliases.Contains("transient-alias"), "chain alias should round trip");
                Program.Assert(
                    restoredFirst.Get_DataPointer()?.Checksum == "transient-checksum",
                    "chain DataPointer should round trip");

                Link restoredLink = restoredFirst.Get_LinkNext()!;
                Program.Assert(
                    restoredLink.SourceID == firstID && restoredLink.DestinationID == secondID,
                    "chain link endpoints should round trip");
                Program.Assert(
                    restoredLink.Classification == LinkClass.temporal,
                    "chain link class should round trip");
                Program.Assert(
                    ReferenceEquals(restoredSecond.Get_LinkPrevious(), restoredLink),
                    "chain previous links should share the restored link instance");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestImportAboveLegacySizeCap()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "over-64mb.lrdlog");
                byte[] prefix = JsonSerializer.SerializeToUtf8Bytes(new CoreLogDocument(), LogJson.Options);
                byte[] padding = Enumerable.Repeat((byte)' ', 64 * 1024).ToArray();
                long targetLength = 64L * 1024L * 1024L + 1L;

                using (FileStream stream = File.Create(path))
                {
                    stream.Write(prefix);
                    while (stream.Length < targetLength)
                    {
                        int bytesToWrite = (int)Math.Min(padding.Length, targetLength - stream.Length);
                        stream.Write(padding, 0, bytesToWrite);
                    }
                }

                LRD_Core core = NewCore();
                PersistenceImportResult result = CoreLogImporter.ImportCore(core, path, ImportMode.Merge);
                Program.Assert(result.Success,
                    $"valid logs above the legacy 64 MB limit should import through the stream path: {string.Join("; ", result.Errors)}");
                Program.Assert(new FileInfo(path).Length > 64L * 1024L * 1024L,
                    "streaming import fixture should exceed the legacy limit");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestSegmentedExportRoundTrip()
        {
            string directory = NewTempDirectory();

            try
            {
                LRD_Core source = NewCore();
                const int graphNodeCount = 20;
                Guid[] graphNodeIDs = new Guid[graphNodeCount];
                for (int index = 0; index < graphNodeCount; index++)
                {
                    graphNodeIDs[index] = Guid.NewGuid();
                    AddNode(source, graphNodeIDs[index], NodeTypes.Concepts, NodeLifetime.Permanent, 0,
                        $"Segmented graph node {index:D2} with extra label text");
                }
                for (int index = 1; index < graphNodeIDs.Length; index++)
                {
                    Program.Assert(source.AddDirectedConnection(graphNodeIDs[index - 1], graphNodeIDs[index],
                        LinkClass.basic), "cross-node-part link should be added");
                }

                LinkedList<Chain_Node> query = BuildTestChain("query", 18, LinkClass.actions);
                LinkedList<Chain_Node> response = BuildTestChain("response", 16, LinkClass.events);
                source.BuildQuery(query);
                source.BuildResponse(response);

                string corePath = Path.Combine(directory, "segmented.lrdlog");
                CoreLogExportResult export = CoreLogExporter.ExportWithPartLimit(
                    source,
                    corePath,
                    partLimitBytes: 1024,
                    includeTransient: true,
                    exportedAtUtc: new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero));

                Program.Assert(export.IsSplit, "test-sized limit should select the manifest/parts format");
                Program.Assert(export.CorePartCount > 1, "core data should be divided across numbered parts");
                Program.Assert(export.TransientPartCount > 1, "transient chains should be divided across numbered parts");
                Program.Assert(export.TotalBytes > 0, "split export should report bytes written");
                Program.Assert(export.NodeCount == graphNodeCount && export.LinkCount == graphNodeCount - 1,
                    "segmented export should report aggregate graph counts");

                Program.Assert(new FileInfo(corePath).Length < CoreLogExporter.MaxPartBytes + 1,
                    "core manifest should remain below the hard file-size ceiling");
                CoreLogManifest coreManifest = JsonSerializer.Deserialize<CoreLogManifest>(
                    File.ReadAllText(corePath), LogJson.Options)!;
                Program.Assert(coreManifest.Parts.Count == export.CorePartCount,
                    "core manifest should list every core data part");
                foreach (LogPartDefinition part in coreManifest.Parts)
                {
                    string partPath = Path.Combine(directory, part.File);
                    Program.Assert(new FileInfo(partPath).Length < 1024,
                        "every test core part should honor the configured strict byte cap");
                    Program.Assert(Path.GetFileName(part.File) == $"segmented_{part.Index:D4}.lrdpart",
                        "core parts should use base-name plus incrementing zero-padded indices");
                }

                LRD_Core restored = NewCore();
                PersistenceImportResult coreImport = CoreLogImporter.ImportCore(restored, corePath, ImportMode.Replace);
                Program.Assert(coreImport.Success,
                    $"manifest core import should succeed: {string.Join("; ", coreImport.Errors)}");
                Program.Assert(coreImport.NodesAdded == graphNodeCount && coreImport.LinksAdded == graphNodeCount - 1,
                    "manifest import should restore all nodes and cross-part links");

                string transientPath = CoreLogExporter.BuildTransientPath(corePath);
                Program.Assert(new FileInfo(transientPath).Length < CoreLogExporter.MaxPartBytes + 1,
                    "transient manifest should remain below the hard file-size ceiling");
                TransientLogManifest transientManifest = JsonSerializer.Deserialize<TransientLogManifest>(
                    File.ReadAllText(transientPath), LogJson.Options)!;
                Program.Assert(transientManifest.Parts.Count == export.TransientPartCount,
                    "transient manifest should list every chain part");
                foreach (LogPartDefinition part in transientManifest.Parts)
                {
                    string partPath = Path.Combine(directory, part.File);
                    Program.Assert(new FileInfo(partPath).Length < 1024,
                        "every test transient part should honor the configured strict byte cap");
                    Program.Assert(Path.GetFileName(part.File) == $"segmented_trans_{part.Index:D4}.lrdpart",
                        "transient parts should use the companion base-name plus incrementing indices");
                }

                PersistenceImportResult transientImport = CoreLogImporter.ImportTransient(
                    restored,
                    transientPath,
                    ImportMode.Replace);
                Program.Assert(transientImport.Success,
                    $"manifest transient import should succeed: {string.Join("; ", transientImport.Errors)}");
                Program.Assert(restored.GetQuery()!.Count == query.Count
                    && restored.GetResponse()!.Count == response.Count,
                    "split transient chains should reconstruct in original order");

                LRD_Core partOnlyCore = NewCore();
                PersistenceImportResult partOnly = CoreLogImporter.ImportCore(
                    partOnlyCore,
                    Path.Combine(directory, coreManifest.Parts[0].File),
                    ImportMode.Replace);
                Program.Assert(!partOnly.Success && partOnlyCore.GetDictionary()!.Count == 0,
                    "selecting an individual part should fail without mutating the graph");

                string firstPartPath = Path.Combine(directory, coreManifest.Parts[0].File);
                byte[] originalPart = File.ReadAllBytes(firstPartPath);
                using (FileStream stream = new FileStream(firstPartPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    int firstByte = stream.ReadByte();
                    stream.Position = 0;
                    stream.WriteByte((byte)(firstByte ^ 0x01));
                }
                LRD_Core corruptCore = NewCore();
                PersistenceImportResult corrupt = CoreLogImporter.ImportCore(corruptCore, corePath, ImportMode.Replace);
                Program.Assert(!corrupt.Success && corruptCore.GetDictionary()!.Count == 0,
                    "corrupt part checksum should reject the whole import before graph mutation");

                File.WriteAllBytes(firstPartPath, originalPart);
                string missingPartPath = Path.Combine(directory, coreManifest.Parts[^1].File);
                File.Delete(missingPartPath);
                LRD_Core missingPartCore = NewCore();
                PersistenceImportResult missingPart = CoreLogImporter.ImportCore(
                    missingPartCore,
                    corePath,
                    ImportMode.Replace);
                Program.Assert(!missingPart.Success && missingPartCore.GetDictionary()!.Count == 0,
                    "missing manifest parts should be rejected before mutating the graph");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static LinkedList<Chain_Node> BuildTestChain(string prefix, int count, LinkClass linkClass)
        {
            LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();
            Chain_Node? previous = null;
            for (int index = 0; index < count; index++)
            {
                Chain_Node current = new Chain_Node(Guid.NewGuid(), NodeTypes.Concepts,
                    NodeLifetime.Temporary, 50);
                current.Set_Label($"{prefix} chain node {index:D2}");
                if (previous is not null)
                {
                    Link link = new Link(previous.ID, current.ID, linkClass);
                    previous.Set_LinkNext(link);
                    current.Set_LinkPrevious(link);
                }
                chain.AddLast(current);
                previous = current;
            }
            return chain;
        }

        private static void TestOversizedSegmentRecord()
        {
            string directory = NewTempDirectory();
            try
            {
                LRD_Core core = NewCore();
                AddNode(core, Guid.NewGuid(), NodeTypes.Concepts, NodeLifetime.Permanent, 0,
                    new string('X', 2048));
                string path = Path.Combine(directory, "last-good.lrdlog");
                File.WriteAllText(path, "last known-good export");

                bool rejected = false;
                try
                {
                    CoreLogExporter.ExportWithPartLimit(core, path, partLimitBytes: 1024,
                        includeTransient: false);
                }
                catch (IOException)
                {
                    rejected = true;
                }

                Program.Assert(rejected, "a single record larger than an empty part should be rejected");
                Program.Assert(File.ReadAllText(path) == "last known-good export",
                    "failed segmented export should leave the previous target file unchanged");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestLargeGraphStreamingExport()
        {
            string directory = NewTempDirectory();

            try
            {
                LRD_Core core = NewCore();
                const int nodeCount = 5000;
                Guid[] nodeIDs = new Guid[nodeCount];
                for (int index = 0; index < nodeCount; index++)
                {
                    nodeIDs[index] = Guid.NewGuid();
                    AddNode(core, nodeIDs[index], NodeTypes.Concepts, NodeLifetime.Permanent, 0,
                        "Streaming export node " + index);
                }

                for (int index = 1; index < nodeIDs.Length; index++)
                    core.AddDirectedConnection(nodeIDs[index - 1], nodeIDs[index], LinkClass.basic);

                string path = Path.Combine(directory, "large-streamed-export.lrdlog");
                CoreLogExportResult result = CoreLogExporter.Export(core, path, includeTransient: false);
                Program.Assert(result.NodeCount == nodeCount, "streamed export should count all graph nodes");
                Program.Assert(result.LinkCount == nodeCount - 1, "streamed export should count all graph links");
                Program.Assert(new FileInfo(path).Length > 0, "streamed export should write a non-empty core log");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestTransientMergeAndReplace()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "transient-merge.lrdlog");
                LRD_Core sourceCore = NewCore();
                Guid importedFirstID = Guid.NewGuid();
                Guid importedSecondID = Guid.NewGuid();
                Chain_Node first = new Chain_Node(importedFirstID, NodeTypes.Concepts, 30);
                Chain_Node second = new Chain_Node(importedSecondID, NodeTypes.Events, 30);
                Link link = new Link(importedFirstID, importedSecondID, LinkClass.events);
                first.Set_LinkNext(link);
                second.Set_LinkPrevious(link);
                LinkedList<Chain_Node> query = new LinkedList<Chain_Node>();
                query.AddLast(first);
                query.AddLast(second);
                sourceCore.BuildQuery(query);
                CoreLogExporter.Export(sourceCore, path, includeTransient: true);

                LRD_Core target = NewCore();
                Guid existingID = Guid.NewGuid();
                target.BuildQuery(new Chain_Node(existingID, NodeTypes.Basics, 20));

                PersistenceImportResult merge = CoreLogImporter.ImportTransient(
                    target,
                    CoreLogExporter.BuildTransientPath(path),
                    ImportMode.Merge);
                Program.Assert(merge.Success, "transient merge should succeed");
                Program.Assert(merge.QueryChainsAdded == 1, "transient merge should add the imported chain");
                Program.Assert(target.GetQuery()!.Count == 3, "merge should append to the existing chain");
                Program.Assert(
                    target.GetQuery()!.Any(node => node.ID == existingID),
                    "merge should preserve existing chain nodes");

                PersistenceImportResult duplicate = CoreLogImporter.ImportTransient(
                    target,
                    CoreLogExporter.BuildTransientPath(path),
                    ImportMode.Merge);
                Program.Assert(duplicate.Success, "duplicate transient merge should succeed");
                Program.Assert(duplicate.QueryChainsAdded == 0, "duplicate chain should be skipped");
                Program.Assert(duplicate.ChainsSkipped == 1, "duplicate chain should be counted as skipped");
                Program.Assert(target.GetQuery()!.Count == 3, "duplicate merge should not append again");

                PersistenceImportResult replace = CoreLogImporter.ImportTransient(
                    target,
                    CoreLogExporter.BuildTransientPath(path),
                    ImportMode.Replace);
                Program.Assert(replace.Success, "transient replace should succeed");
                Program.Assert(target.GetQuery()!.Count == 2, "replace should install only imported chain nodes");
                Program.Assert(
                    !target.GetQuery()!.Any(node => node.ID == existingID),
                    "replace should remove existing chain nodes");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestMalformedTransient()
        {
            string directory = NewTempDirectory();

            try
            {
                LRD_Core core = NewCore();
                Guid existingID = Guid.NewGuid();
                core.BuildQuery(new Chain_Node(existingID, NodeTypes.Basics, 20));

                string path = Path.Combine(directory, "malformed-transient.lrdlog");
                File.WriteAllText(path, "{ invalid transient json");

                PersistenceImportResult result = CoreLogImporter.ImportTransient(
                    core,
                    path,
                    ImportMode.Replace);

                Program.Assert(!result.Success, "malformed transient log should fail");
                Program.Assert(
                    core.GetQuery()!.Count == 1 && core.GetQuery()!.First!.Value.ID == existingID,
                    "malformed transient import should not change chains");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        private static void TestLinkDistanceDefaults()
        {
            Dictionary<LinkClass, double> expected = new Dictionary<LinkClass, double>
            {
                [LinkClass.identities] = 0.50,
                [LinkClass.map_locations] = 0.75,
                [LinkClass.basic] = 1.00,
                [LinkClass.events] = 1.00,
                [LinkClass.actions] = 1.25,
                [LinkClass.temporal] = 1.25,
                [LinkClass.spatial] = 1.50,
                [LinkClass.sources] = 1.75,
                [LinkClass.empty] = 2.00
            };

            foreach ((LinkClass linkClass, double distance) in expected)
            {
                Program.Assert(
                    LinkDistanceDefinitions.GetDistance(linkClass) == distance,
                    $"distance for '{linkClass.Name}' should be {distance}");
            }

            Link link = new Link(
                Guid.NewGuid(),
                Guid.NewGuid(),
                ForwardLinkTypes.basic,
                InverseLinkTypes.basic,
                LinkClass.spatial);
            Program.Assert(
                LinkDistanceDefinitions.GetDistance(link) == 1.50,
                "link distance should resolve through its class");
        }

        private static void TestLinkDistanceOverrides()
        {
            int forwardID = NextTypeId();
            ForwardLinkTypes forward = ForwardLinkTypes.Register(
                forwardID,
                "distance_forward_" + forwardID);

            try
            {
                LinkDistanceDefinitions.SetForwardOverride(forward, 0.25);
                Program.Assert(
                    LinkDistanceDefinitions.GetDistance(LinkClass.basic, forward) == 0.25,
                    "forward override should take precedence over the class default");
                Program.Assert(
                    LinkDistanceDefinitions.GetDistance(LinkClass.basic) == 1.00,
                    "class default should remain unchanged");
                Program.Assert(
                    LinkDistanceDefinitions.TryGetForwardOverride(forward, out double distance)
                        && distance == 0.25,
                    "forward override should be readable");
            }
            finally
            {
                LinkDistanceDefinitions.RemoveForwardOverride(forward);
            }
        }

        private static void TestLinkDistanceFallback()
        {
            int classID = NextTypeId();
            LinkClass customClass = LinkClass.Register(
                classID,
                "distance_class_" + classID);

            Program.Assert(
                LinkDistanceDefinitions.GetDistance(customClass) == LinkDistanceDefinitions.FallbackDistance,
                "unregistered classes should use the global fallback");
            Program.Assert(
                LinkDistanceDefinitions.GetDistance(
                    customClass,
                    ForwardLinkTypes.FromId(999999)) == LinkDistanceDefinitions.FallbackDistance,
                "unknown classes should ignore unregistered forward types");
        }

        private static void TestLinkDistanceValidation()
        {
            int classID = NextTypeId();
            int forwardID = NextTypeId();
            LinkClass linkClass = LinkClass.Register(classID, "distance_validation_class_" + classID);
            ForwardLinkTypes forward = ForwardLinkTypes.Register(
                forwardID,
                "distance_validation_forward_" + forwardID);

            Program.Assert(
                !LinkDistanceDefinitions.TrySetClassDefault(linkClass, 0),
                "zero class distance should be rejected");
            Program.Assert(
                !LinkDistanceDefinitions.TrySetClassDefault(linkClass, -1),
                "negative class distance should be rejected");
            Program.Assert(
                !LinkDistanceDefinitions.TrySetClassDefault(linkClass, double.NaN),
                "NaN class distance should be rejected");
            Program.Assert(
                !LinkDistanceDefinitions.TrySetForwardOverride(forward, double.PositiveInfinity),
                "infinite forward distance should be rejected");
            Program.Assert(
                !LinkDistanceDefinitions.TryGetForwardOverride(forward, out _),
                "rejected forward distances should not be stored");
            Program.Assert(
                !LinkDistanceDefinitions.IsValidDistance(0)
                    && LinkDistanceDefinitions.IsValidDistance(0.5),
                "distance validation should require positive finite values");

            Program.AssertThrows<ArgumentOutOfRangeException>(
                () => LinkDistanceDefinitions.SetClassDefault(linkClass, 0),
                "invalid class default should throw");
            Program.AssertThrows<ArgumentOutOfRangeException>(
                () => LinkDistanceDefinitions.SetForwardOverride(forward, double.NaN),
                "invalid forward override should throw");
        }

        private static void TestLinkDistanceConcurrency()
        {
            const int iterations = 100;

            Parallel.For(0, iterations, index =>
            {
                int forwardID = 800000 + index;
                int classID = 900000 + index;
                ForwardLinkTypes forward = ForwardLinkTypes.Register(
                    forwardID,
                    "distance_concurrent_forward_" + index);
                LinkClass linkClass = LinkClass.Register(
                    classID,
                    "distance_concurrent_class_" + index);
                double distance = 0.1 + (index % 10) * 0.1;

                Program.Assert(
                    LinkDistanceDefinitions.TrySetClassDefault(linkClass, distance),
                    "concurrent class default should be accepted");
                Program.Assert(
                    LinkDistanceDefinitions.TrySetForwardOverride(forward, distance),
                    "concurrent forward override should be accepted");
                Program.Assert(
                    LinkDistanceDefinitions.GetDistance(linkClass) == distance,
                    "concurrent class lookup should return the stored distance");
                Program.Assert(
                    LinkDistanceDefinitions.GetDistance(linkClass, forward) == distance,
                    "concurrent forward lookup should return the stored distance");
            });

            _ = LinkDistanceDefinitions.GetClassDefaults();
            _ = LinkDistanceDefinitions.GetForwardOverrides();
        }
    }
}
