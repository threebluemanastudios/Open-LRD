using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.SmokeTests
{
    internal static class GraphSynchronizationStressTests
    {
        private const int WriterCount = 8;
        private const int ReaderCount = 8;
        private const int ConsumerCount = 4;
        private const int ItemsPerWriter = 48;
        private const int SeedWordCount = 96;
        private const int ReaderIterations = 150;
        private const int ConsumerMinimumSnapshots = 25;
        private const int MonitorCount = 2;
        private static readonly TimeSpan WorkerJoinTimeout = TimeSpan.FromSeconds(120);

        internal static void RunAll()
        {
            Program.Run("GraphSynchronization parallel readers observe a stable graph", ParallelReadersObserveStableGraph());
            Program.Run("GraphSynchronization mutation bursts preserve exact counts", MutationBurstsPreserveExactCounts());
            Program.Run("GraphSynchronization snapshot consumers see monotonic consistent epochs", SnapshotConsumersSeeConsistentEpochs());
            Program.Run("GraphSynchronization snapshots stay immutable during mutation", SnapshotsStayImmutableDuringMutation());
        }

        private static Action ParallelReadersObserveStableGraph()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                SeedWordRing(core, SeedWordCount);

                int expectedNodes = core.GetTrustedNodeCount();
                int expectedLinks = core.GetTrustedLinkCount();
                long expectedEpoch = core.CurrentGraphEpoch;
                List<Guid> expectedWordNodeIds = core.GetTrustedNodeIds(NodeTargetKind.Word);
                Program.Assert(expectedWordNodeIds.Count == SeedWordCount,
                    "Seed should create one trusted word node per word.");
                Program.Assert(core.GetAllTrustedLinks(NodeRelationType.Custom).Count == SeedWordCount,
                    "Seed ring should create one custom link per word.");

                RunConcurrently(ReaderCount, "parallel readers", reader =>
                {
                    for (int iteration = 0; iteration < ReaderIterations; iteration++)
                    {
                        Program.Assert(core.GetTrustedNodeCount() == expectedNodes,
                            "Node count must not drift under read-only load.");
                        Program.Assert(core.GetTrustedLinkCount() == expectedLinks,
                            "Link count must not drift under read-only load.");
                        Program.Assert(core.CurrentGraphEpoch == expectedEpoch,
                            "Read-only load must not advance the graph epoch.");

                        int offset = (reader * 7 + iteration) % SeedWordCount;
                        List<Guid> page = core.GetTrustedNodeIds(NodeTargetKind.Word, 16, offset);
                        Program.Assert(page.SequenceEqual(expectedWordNodeIds.Skip(offset).Take(16)),
                            "Paged reads must return the same deterministic order under contention.");

                        Node? wordNode = core.GetTrustedNode(expectedWordNodeIds[offset]);
                        Program.Assert(wordNode != null, "Seeded word node should remain visible.");
                        Program.Assert(core.GetOutgoingLinks(wordNode!.GetNodeID(), NodeRelationType.Custom).Count == 1,
                            "Each ring word should keep exactly one outgoing custom link.");

                        if (iteration % 10 == 0)
                        {
                            Program.Assert(core.GetAllTrustedLinks(NodeRelationType.Custom).Count == SeedWordCount,
                                "Relation-filtered link queries must see the full ring.");
                            TrustedKnowledgeGraphSnapshot snapshot = core.BuildTrustedKnowledgeGraphSnapshot();
                            Program.Assert(snapshot.Epoch == expectedEpoch && snapshot.Nodes.Count == expectedNodes,
                                "Epoch snapshots taken from a stable graph must match the stable counts.");
                        }
                    }
                });
            };
        }

        private static Action MutationBurstsPreserveExactCounts()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid[] senseIds = new Guid[WriterCount];
                for (int writer = 0; writer < WriterCount; writer++)
                {
                    senseIds[writer] = AddSense(core);
                }
                AddUsage(core);
                int nodesBefore = core.GetTrustedNodeCount();
                long epochBefore = core.CurrentGraphEpoch;

                Guid[][] symbolIds = new Guid[WriterCount][];
                for (int writer = 0; writer < WriterCount; writer++)
                {
                    symbolIds[writer] = new Guid[ItemsPerWriter];
                }

                RunConcurrently(WriterCount, "symbol bursts", writer =>
                {
                    long lastEpoch = core.CurrentGraphEpoch;
                    for (int item = 0; item < ItemsPerWriter; item++)
                    {
                        SymbolBox symbol = new SymbolBox(Guid.NewGuid(), SymbolCharFor(writer, item));
                        symbol.SetSenseID(senseIds[writer]);
                        Program.Assert(core.AddTrustedSymbol(symbol),
                            "Unique (sense, char) symbols should all be accepted during a burst.");
                        symbolIds[writer][item] = symbol.GetSymbolID();

                        long epoch = core.CurrentGraphEpoch;
                        Program.Assert(epoch >= lastEpoch, "Epoch must never move backwards for a single observer.");
                        lastEpoch = epoch;
                    }
                });

                Program.Assert(core.GetTrustedNodeCount() - nodesBefore == WriterCount * ItemsPerWriter,
                    "Each accepted symbol should add exactly one trusted node.");
                Program.Assert(core.GetTrustedNodeIds(NodeTargetKind.Symbol).Count == WriterCount * ItemsPerWriter,
                    "Every accepted symbol should be visible through the ordered node index.");

                Guid[][] symbolNodeIds = new Guid[WriterCount][];
                for (int writer = 0; writer < WriterCount; writer++)
                {
                    symbolNodeIds[writer] = new Guid[ItemsPerWriter];
                    for (int item = 0; item < ItemsPerWriter; item++)
                    {
                        symbolNodeIds[writer][item] = NodeIdFor(core, NodeTargetKind.Symbol, symbolIds[writer][item]);
                    }
                }

                int linksBefore = core.GetTrustedLinkCount();
                int sharedAccepted = 0;
                int monitorReads = 0;

                RunConcurrently(WriterCount + MonitorCount, "link bursts", worker =>
                {
                    if (worker < WriterCount)
                    {
                        int writer = worker;
                        for (int item = 0; item < ItemsPerWriter; item++)
                        {
                            Guid source = symbolNodeIds[writer][item];
                            Guid target = symbolNodeIds[writer][(item + 1) % ItemsPerWriter];
                            Program.Assert(core.AddTrustedLink(source, target, NodeRelationType.Custom, 0.5,
                                    $"burst-{writer}-{item}"),
                                "Distinct labelled ring links should all be accepted during a burst.");
                        }

                        // Every writer races for the same qualified link; exactly one may win.
                        if (core.AddTrustedLink(symbolNodeIds[0][0], symbolNodeIds[1][0], NodeRelationType.Custom, 0.9, "shared"))
                        {
                            Interlocked.Increment(ref sharedAccepted);
                        }
                        return;
                    }

                    // Monitors read the link count while bursts are in flight: it must only grow.
                    int lastLinks = linksBefore;
                    for (int sample = 0; sample < ReaderIterations; sample++)
                    {
                        int links = core.GetTrustedLinkCount();
                        Program.Assert(links >= lastLinks, "Link count must never decrease during an add-only burst.");
                        Program.Assert(links <= linksBefore + WriterCount * ItemsPerWriter + 1,
                            "Link count must never exceed the number of accepted additions.");
                        lastLinks = links;
                        Interlocked.Increment(ref monitorReads);
                    }
                });

                int expectedNewLinks = WriterCount * ItemsPerWriter + 1;
                Program.Assert(sharedAccepted == 1, "Exactly one writer should win the contended duplicate link.");
                Program.Assert(core.GetTrustedLinkCount() - linksBefore == expectedNewLinks,
                    "Final link count should equal the number of accepted additions.");
                Program.Assert(core.CurrentGraphEpoch > epochBefore,
                    "Graph epoch should advance across the burst.");
                Program.Assert(monitorReads == MonitorCount * ReaderIterations,
                    "Monitors should complete all of their samples.");

                Guid sharedSource = symbolNodeIds[0][0];
                int sharedCount = core.GetOutgoingLinks(sharedSource, NodeRelationType.Custom)
                    .Count(link => link.Label == "shared");
                Program.Assert(sharedCount == 1, "The contended link should be stored exactly once.");
            };
        }

        private static Action SnapshotConsumersSeeConsistentEpochs()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid[] senseIds = new Guid[WriterCount];
                for (int writer = 0; writer < WriterCount; writer++)
                {
                    senseIds[writer] = AddSense(core);
                }
                AddUsage(core);

                int remainingWriters = WriterCount;
                RunConcurrently(WriterCount + ConsumerCount, "snapshot consumers", worker =>
                {
                    if (worker < WriterCount)
                    {
                        try
                        {
                            Guid? previousNode = null;
                            for (int item = 0; item < ItemsPerWriter; item++)
                            {
                                SymbolBox symbol = new SymbolBox(Guid.NewGuid(), SymbolCharFor(worker, item));
                                symbol.SetSenseID(senseIds[worker]);
                                Program.Assert(core.AddTrustedSymbol(symbol), "Writer symbols should be accepted.");
                                Guid currentNode = NodeIdFor(core, NodeTargetKind.Symbol, symbol.GetSymbolID());
                                if (previousNode.HasValue)
                                {
                                    Program.Assert(core.AddTrustedLink(previousNode.Value, currentNode, NodeRelationType.Custom, 0.5,
                                            $"chain-{worker}-{item}"),
                                        "Chain links should be accepted while snapshots are taken.");
                                }
                                previousNode = currentNode;
                            }
                        }
                        finally
                        {
                            Interlocked.Decrement(ref remainingWriters);
                        }
                        return;
                    }

                    long lastEpoch = -1;
                    int lastNodes = -1;
                    int lastLinks = -1;
                    int taken = 0;
                    while (Volatile.Read(ref remainingWriters) > 0 || taken < ConsumerMinimumSnapshots)
                    {
                        TrustedKnowledgeGraphSnapshot snapshot = core.BuildTrustedKnowledgeGraphSnapshot();
                        AssertSnapshotConsistent(snapshot);
                        Program.Assert(snapshot.Epoch >= lastEpoch, "Snapshot epochs must be monotonic for each consumer.");
                        Program.Assert(snapshot.Nodes.Count >= lastNodes, "Snapshot node counts must not shrink during add-only mutation.");
                        Program.Assert(snapshot.Links.Count >= lastLinks, "Snapshot link counts must not shrink during add-only mutation.");
                        lastEpoch = snapshot.Epoch;
                        lastNodes = snapshot.Nodes.Count;
                        lastLinks = snapshot.Links.Count;
                        taken++;
                    }
                });

                TrustedKnowledgeGraphSnapshot finalSnapshot = core.BuildTrustedKnowledgeGraphSnapshot();
                AssertSnapshotConsistent(finalSnapshot);
                Program.Assert(finalSnapshot.Epoch == core.CurrentGraphEpoch,
                    "A snapshot taken after the burst should carry the current epoch.");
                Program.Assert(finalSnapshot.Nodes.Count == core.GetTrustedNodeCount(),
                    "A snapshot taken after the burst should contain every trusted node.");
                Program.Assert(finalSnapshot.Links.Count == core.GetTrustedLinkCount(),
                    "A snapshot taken after the burst should contain every active link.");
            };
        }

        private static Action SnapshotsStayImmutableDuringMutation()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                SeedWordRing(core, SeedWordCount / 3);
                Guid[] senseIds = new Guid[WriterCount];
                for (int writer = 0; writer < WriterCount; writer++)
                {
                    senseIds[writer] = AddSense(core);
                }
                AddUsage(core);

                TrustedKnowledgeGraphSnapshot snapshot = core.BuildTrustedKnowledgeGraphSnapshot();
                AssertSnapshotConsistent(snapshot);
                long frozenEpoch = snapshot.Epoch;
                KnowledgeNodeSnapshot[] frozenNodes = snapshot.Nodes.ToArray();
                NodeLink[] frozenLinks = snapshot.Links.ToArray();
                Program.Assert(snapshot.NodesById.Values.All(node => node.IsReadOnly),
                    "Epoch node views should be read-only.");
                long currentEpochAtCapture = core.CurrentGraphEpoch;
                Program.Assert(currentEpochAtCapture == frozenEpoch, "Snapshot epoch should match the live epoch at capture.");

                RunConcurrently(WriterCount + ReaderCount, "snapshot immutability", worker =>
                {
                    if (worker < WriterCount)
                    {
                        for (int item = 0; item < ItemsPerWriter; item++)
                        {
                            SymbolBox symbol = new SymbolBox(Guid.NewGuid(), SymbolCharFor(worker, item));
                            symbol.SetSenseID(senseIds[worker]);
                            Program.Assert(core.AddTrustedSymbol(symbol), "Writer symbols should be accepted.");
                        }
                        return;
                    }

                    // Readers touch the captured snapshot without taking the lock.
                    for (int iteration = 0; iteration < ReaderIterations; iteration++)
                    {
                        Program.Assert(snapshot.Epoch == frozenEpoch, "A captured snapshot's epoch must not change.");
                        Program.Assert(snapshot.Nodes.SequenceEqual(frozenNodes),
                            "A captured snapshot's node list must not change while the graph mutates.");
                        Program.Assert(snapshot.Links.SequenceEqual(frozenLinks),
                            "A captured snapshot's link list must not change while the graph mutates.");
                        Program.Assert(snapshot.NodesById.Count == frozenNodes.Length,
                            "A captured snapshot's node map must not change while the graph mutates.");
                    }
                });

                Program.Assert(core.CurrentGraphEpoch > frozenEpoch, "The live graph should have advanced past the captured epoch.");
                Program.Assert(core.GetTrustedNodeCount() > frozenNodes.Length,
                    "The live graph should contain nodes that the captured snapshot does not.");
                Program.Assert(snapshot.Nodes.SequenceEqual(frozenNodes) && snapshot.Epoch == frozenEpoch,
                    "The captured snapshot should remain unchanged after the burst.");
            };
        }

        private static void AssertSnapshotConsistent(TrustedKnowledgeGraphSnapshot snapshot)
        {
            Program.Assert(snapshot.Nodes.Count == snapshot.NodesById.Count &&
                           snapshot.Nodes.Count == snapshot.NodeSnapshotsById.Count,
                "Snapshot node views must cover the same nodes as the node list.");
            foreach (KnowledgeNodeSnapshot node in snapshot.Nodes)
            {
                Program.Assert(snapshot.NodeSnapshotsById.TryGetValue(node.NodeId, out KnowledgeNodeSnapshot indexed) && indexed.Equals(node),
                    "Each listed node must match its indexed snapshot.");
                Program.Assert(snapshot.NodesById.ContainsKey(node.NodeId), "Each listed node must have an indexed node view.");
            }
            foreach (NodeLink link in snapshot.Links)
            {
                Program.Assert(snapshot.NodeSnapshotsById.ContainsKey(link.SourceNodeId) &&
                               snapshot.NodeSnapshotsById.ContainsKey(link.TargetNodeId),
                    "Every link endpoint must be a node present in the same epoch.");
            }
            Program.Assert(snapshot.Links.Select(link => (link.SourceNodeId, link.TargetNodeId, link.RelationType, link.Label))
                               .Distinct().Count() == snapshot.Links.Count,
                "A snapshot must not list the same link identity twice.");
            Program.Assert(snapshot.LinksByAssertionId.Count <= snapshot.Links.Count,
                "Assertion index cannot contain more entries than the link list.");
            foreach (KeyValuePair<Guid, NodeLink> entry in snapshot.LinksByAssertionId)
            {
                Program.Assert(entry.Value.Provenance.AssertionId == entry.Key,
                    "Each assertion index entry must be keyed by its link's assertion ID.");
            }
        }

        private static void RunConcurrently(int workerCount, string phase, Action<int> body)
        {
            ConcurrentQueue<Exception> failures = new ConcurrentQueue<Exception>();
            using ManualResetEventSlim startGate = new ManualResetEventSlim(false);
            Thread[] workers = new Thread[workerCount];
            for (int index = 0; index < workerCount; index++)
            {
                int workerIndex = index;
                workers[index] = new Thread(() =>
                {
                    startGate.Wait();
                    try
                    {
                        body(workerIndex);
                    }
                    catch (Exception exception)
                    {
                        failures.Enqueue(exception);
                    }
                })
                {
                    IsBackground = true,
                    Name = $"{phase} worker {workerIndex}"
                };
                workers[index].Start();
            }

            startGate.Set();
            foreach (Thread worker in workers)
            {
                if (!worker.Join(WorkerJoinTimeout))
                {
                    throw new InvalidOperationException(
                        $"{phase}: {worker.Name} did not finish within {WorkerJoinTimeout.TotalSeconds:0} seconds; possible deadlock.");
                }
            }

            if (failures.TryPeek(out Exception? first))
            {
                throw new InvalidOperationException(
                    $"{phase}: {failures.Count} worker failure(s); first: {first.GetType().Name}: {first.Message}");
            }
        }

        private static char SymbolCharFor(int writer, int item)
        {
            return (char)(0x4E00 + writer * ItemsPerWriter + item);
        }

        private static List<Guid> SeedWordRing(LRD_Core core, int wordCount)
        {
            Guid senseId = AddSense(core);
            Guid usageId = AddUsage(core);
            List<Guid> wordIds = new List<Guid>(wordCount);
            for (int item = 0; item < wordCount; item++)
            {
                Guid symbolId = AddTrustedSymbol(core, senseId, (char)(0x6000 + item));
                WordBox word = new WordBox(Guid.NewGuid(), senseId, usageId, new List<Guid> { symbolId }, PartOfSpeech.Noun);
                Program.Assert(core.AddTrustedWord(word), "Seed word should be registered.");
                wordIds.Add(word.GetWordID());
            }

            List<Guid> nodeIds = wordIds.Select(wordId => NodeIdFor(core, NodeTargetKind.Word, wordId)).ToList();
            for (int item = 0; item < wordCount; item++)
            {
                Program.Assert(core.AddTrustedLink(nodeIds[item], nodeIds[(item + 1) % wordCount], NodeRelationType.Custom, 1.0, "ring"),
                    "Seed ring link should be accepted.");
            }
            return wordIds;
        }

        private static Guid AddSense(LRD_Core core)
        {
            Guid senseId = Guid.NewGuid();
            Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Test sense should be registered.");
            return senseId;
        }

        private static Guid AddUsage(LRD_Core core)
        {
            Guid usageId = Guid.NewGuid();
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Test usage should be registered.");
            return usageId;
        }

        private static Guid AddTrustedSymbol(LRD_Core core, Guid senseId, char value)
        {
            SymbolBox symbol = new SymbolBox(Guid.NewGuid(), value);
            symbol.SetSenseID(senseId);
            Program.Assert(core.AddTrustedSymbol(symbol), "Test symbol should be registered.");
            return symbol.GetSymbolID();
        }

        private static Guid NodeIdFor(LRD_Core core, NodeTargetKind kind, Guid targetId)
        {
            Node? node = core.GetTrustedNodeByTarget(kind, targetId);
            Program.Assert(node != null, "Expected trusted node was not created.");
            return node!.GetNodeID();
        }

        private static void ResetCore()
        {
            LRD_Core.Instance.ResetCore();
            LRD_Core.Instance.InitializeCore();
        }
    }
}
