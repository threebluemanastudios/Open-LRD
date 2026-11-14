using OPEN_LRD;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.SmokeTests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static void Main()
        {
            Run("Guid node IDs", TestGuidNodeIds);
            Run("Link value semantics", TestLinkValueSemantics);
            Run("DataPointer validation", TestDataPointer);
            Run("Node DataPointer storage", TestNodeDataPointer);
            Run("Connection semantics", TestConnectionSemantics);
            Run("Duplicate link prevention", TestDuplicateLinkPrevention);
            Run("Temporary TTL purge", TestTemporaryTtlPurge);
            Run("Verified reference promotion", TestVerifiedReferencePromotion);
            Run("Permanent node survival", TestPermanentNodeSurvival);
            Run("Invalid reference disposal", TestInvalidReferenceDisposal);
            Run("Search APIs", TestSearchApis);
            Run("Maintenance link cleanup", TestMaintenanceLinkCleanup);
            Run("Query and response chains", TestQueryAndResponseChains);
            Run("NodeTypes name lookup", TestNodeTypesNameLookup);
            Run("Runtime link type registries", TestRuntimeLinkTypeRegistries);
            Run("Link type registry concurrency", TestLinkTypeRegistryConcurrency);
            ReasoningTests.RunAll();
            NlpTests.RunAll();
            PersistenceTests.RunAll();
            MaintenanceTests.RunAll();
            OntologyTests.RunAll();
            TextImportTests.RunAll();
            VisualizerTests.RunAll();

            Console.WriteLine($"{_passed} passed, {_failed} failed");
            Environment.ExitCode = _failed == 0 ? 0 : 1;
        }

        internal static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        internal static void AssertThrows<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{message}: wrong exception {ex.GetType().Name}");
            }

            throw new InvalidOperationException($"{message}: no exception thrown");
        }

        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        private static void TestGuidNodeIds()
        {
            LRD_Core core = NewCore();
            Guid first = core.GenerateNewNode();
            Guid second = core.GenerateNewNode();

            Assert(first != Guid.Empty && second != Guid.Empty, "generated IDs must not be empty");
            Assert(first != second, "generated IDs must be unique");
            Assert(core.TryGetNode(first, out Web_Node? node), "first node should be found");
            Assert(node is not null && node.ID == first, "lookup should return the expected node");
            Assert(core.GetDictionary() is not null && core.GetDictionary()!.ContainsKey(second), "dictionary should contain the second node");
        }

        private static void TestLinkValueSemantics()
        {
            Guid source = Guid.NewGuid();
            Guid destination = Guid.NewGuid();

            Link first = new Link(source, destination, LinkClass.basic);
            Link second = new Link(source, destination, LinkClass.basic);
            Link differentClass = new Link(source, destination, LinkClass.events);

            Assert(first == second, "identical links should be value-equal");
            Assert(first != differentClass, "links with different classes should not be equal");
            Assert(first.SourceID == source && first.DestinationID == destination, "link endpoints must be assigned");
            Assert(ReferenceEquals(first.GetLink(), first), "GetLink should return the same immutable instance");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new Link(source, destination, 99, 0, LinkClass.basic),
                "unknown link type should be rejected");
        }

        private static void TestDataPointer()
        {
            DataPointer httpPointer = new DataPointer(
                new Uri("https://example.com/data.bin"),
                "application/octet-stream",
                42,
                "abc123");

            Assert(httpPointer.Location.Scheme == "https", "HTTP pointer should preserve its scheme");
            Assert(httpPointer.ByteLength == 42, "byte length should be preserved");

            DataPointer filePointer = DataPointer.FromFilePath(Path.Combine(Path.GetTempPath(), "open-lrd.bin"));
            Assert(filePointer.Location.IsAbsoluteUri && filePointer.Location.Scheme == "file", "file pointer should be absolute");

            AssertThrows<ArgumentException>(
                () => new DataPointer(new Uri("relative/path.bin", UriKind.Relative)),
                "relative pointers should be rejected");
            AssertThrows<ArgumentException>(
                () => new DataPointer(new Uri("ftp://example.com/data.bin")),
                "unsupported schemes should be rejected");
            AssertThrows<ArgumentException>(
                () => new DataPointer(new Uri("https://user:password@example.com/data.bin")),
                "credentials should be rejected");
        }

        private static void TestNodeDataPointer()
        {
            Web_Node node = new Web_Node(Guid.NewGuid(), NodeTypes.Basics, NodeLifetime.Temporary, 10);
            DataPointer pointer = new DataPointer(new Uri("https://example.com/image.png"), "image/png");

            node.Set_DataPointer(pointer);
            Assert(node.Get_DataPointer() == pointer, "node should hold the pointer value");

            node.Clear_DataPointer();
            Assert(node.Get_DataPointer() is null, "node pointer should clear");
        }

        private static void TestConnectionSemantics()
        {
            LRD_Core core = NewCore();
            Guid first = core.GenerateNewNode();
            Guid second = core.GenerateNewNode();

            Assert(core.AddConnection(first, second, LinkClass.basic), "two-way connection should be added");
            Assert(core.GetLinks(first).Any(link => link.SourceID == first && link.DestinationID == second && link.Classification == LinkClass.basic), "source-to-destination link should exist");
            Assert(core.GetLinks(second).Any(link => link.SourceID == second && link.DestinationID == first && link.Classification == LinkClass.basic), "destination-to-source link should exist");

            Assert(core.AddDirectedConnection(first, second, LinkClass.events), "directed connection should be added");
            Assert(core.GetLinks(first).Any(link => link.SourceID == first && link.DestinationID == second && link.Classification == LinkClass.events), "directed forward link should exist");
            Assert(!core.GetLinks(second).Any(link => link.SourceID == second && link.DestinationID == first && link.Classification == LinkClass.events), "directed link should not create a reverse edge");

            Assert(core.RemoveConnection(first, second), "two-way connection should be removed");
            Assert(!core.GetLinks(first).Any(link => link.SourceID == first && link.DestinationID == second), "forward links should be removed");
            Assert(!core.GetLinks(second).Any(link => link.SourceID == second && link.DestinationID == first), "reverse links should be removed");

            Guid missing = Guid.NewGuid();
            Assert(!core.AddConnection(first, missing), "missing destination should return false");
            Assert(!core.AddConnection(missing, second), "missing source should return false");
            Assert(!core.RemoveConnection(missing, second), "missing removal should return false");
        }

        private static void TestDuplicateLinkPrevention()
        {
            LRD_Core core = NewCore();
            Guid first = core.GenerateNewNode();
            Guid second = core.GenerateNewNode();

            Assert(core.AddDirectedConnection(first, second, LinkClass.basic), "first link should be added");
            Assert(core.AddDirectedConnection(first, second, LinkClass.basic), "duplicate call should still succeed");

            int count = core.GetLinks(first).Count(
                link => link.SourceID == first && link.DestinationID == second && link.Classification == LinkClass.basic);

            Assert(count == 1, "exact duplicate links should not be stored twice");
        }

        private static void TestTemporaryTtlPurge()
        {
            LRD_Core core = NewCore();
            Guid nodeID = Guid.NewGuid();
            Web_Node node = new Web_Node(nodeID, NodeTypes.Basics, NodeLifetime.Temporary, 2);

            Assert(core.AddExistingNode(node), "temporary node should be added");
            core.RunMaintenanceCycle();
            Assert(core.GetExistingNode(nodeID) is not null, "node should survive its first maintenance cycle");
            core.RunMaintenanceCycle();
            Assert(core.GetExistingNode(nodeID) is null, "node should be purged when TTL expires");
        }

        private static void TestVerifiedReferencePromotion()
        {
            LRD_Core core = NewCore();
            Guid nodeID = Guid.NewGuid();
            Web_Node node = new Web_Node(nodeID, NodeTypes.Concepts, NodeLifetime.Temporary, 1);

            Assert(core.AddExistingNode(node), "node should be added");
            Assert(node.RegisterVerifiedReference(Node.PromotionWeightThreshold), "verified references should promote the node");
            Assert(node.IsPermanent, "promoted node should be permanent");

            for (int i = 0; i < 5; i++)
                core.RunMaintenanceCycle();

            Assert(core.GetExistingNode(nodeID) is not null, "promoted node should never expire");
        }

        private static void TestPermanentNodeSurvival()
        {
            LRD_Core core = NewCore();
            Guid nodeID = Guid.NewGuid();
            Web_Node node = new Web_Node(nodeID, NodeTypes.Events, NodeLifetime.Permanent, 0);

            Assert(core.AddExistingNode(node), "permanent node should be added");

            for (int i = 0; i < 5; i++)
                core.RunMaintenanceCycle();

            Assert(core.GetExistingNode(nodeID) is not null, "permanent node should survive maintenance");
            Assert(core.GetExistingNode(nodeID)!.Get_TTL() == 0, "permanent node TTL should not decrement");
        }

        private static void TestInvalidReferenceDisposal()
        {
            Web_Node node = new Web_Node(Guid.NewGuid(), NodeTypes.Basics, NodeLifetime.Permanent, 100);

            Assert(node.RegisterInvalidReference(Node.DemotionWeightThreshold), "invalid references should trigger disposal");
            Assert(node.IsDisposed, "node should be disposed after the invalid-reference threshold");
        }

        private static void TestSearchApis()
        {
            LRD_Core core = NewCore();
            Guid conceptID = Guid.NewGuid();
            Guid eventID = Guid.NewGuid();
            Web_Node concept = new Web_Node(conceptID, NodeTypes.Concepts, NodeLifetime.Temporary, 100);
            Web_Node eventNode = new Web_Node(eventID, NodeTypes.Events, NodeLifetime.Permanent, 100);

            Assert(core.AddExistingNode(concept), "concept should be added");
            Assert(core.AddExistingNode(eventNode), "event should be added");

            Assert(core.FindByType(NodeTypes.Concepts).Count == 1, "type search should find one concept");
            Assert(core.FindByLifetime(NodeLifetime.Permanent).Any(node => node.ID == eventID), "lifetime search should find the permanent event");
            Assert(core.FindNodes(new NodeSearchCriteria { MinWeightUp = 0f }).Count >= 2, "criteria search should return both nodes");

            Assert(core.AddDirectedConnection(conceptID, eventID, LinkClass.events), "directed event link should be added");
            Assert(core.FindByLinkClass(LinkClass.events).Any(node => node.ID == conceptID), "link-class search should find the concept");
            Assert(core.FindLinkedTo(conceptID).Any(node => node.ID == eventID), "linked-node search should find the event");
            Assert(core.GetLinks(conceptID).Count == 1, "link retrieval should return the directed edge");
        }

        private static void TestMaintenanceLinkCleanup()
        {
            LRD_Core core = NewCore();
            Guid permanentID = Guid.NewGuid();
            Guid temporaryID = Guid.NewGuid();
            Web_Node permanent = new Web_Node(permanentID, NodeTypes.Basics, NodeLifetime.Permanent, 100);
            Web_Node temporary = new Web_Node(temporaryID, NodeTypes.Basics, NodeLifetime.Temporary, 1);

            Assert(core.AddExistingNode(permanent), "permanent node should be added");
            Assert(core.AddExistingNode(temporary), "temporary node should be added");
            Assert(core.AddConnection(permanentID, temporaryID, LinkClass.basic), "connection should be added");

            core.RunMaintenanceCycle();

            Assert(core.GetExistingNode(temporaryID) is null, "temporary node should be removed");
            Assert(!core.GetLinks(permanentID).Any(link => link.SourceID == temporaryID || link.DestinationID == temporaryID), "surviving node should not keep stale links");
        }

        private static void TestQueryAndResponseChains()
        {
            LRD_Core core = NewCore();
            Chain_Node first = new Chain_Node(Guid.NewGuid(), NodeTypes.Basics, 10);
            Chain_Node second = new Chain_Node(Guid.NewGuid(), NodeTypes.Basics, 10);

            core.BuildQuery(new LinkedList<Chain_Node>(new[] { first }));
            core.BuildQuery(second);

            Assert(core.GetQuery()?.Count == 2, "query chain should contain both nodes");

            core.ClearQuery();
            Assert(core.GetQuery()?.Count == 0, "query chain should clear without becoming null");

            core.BuildResponse(new LinkedList<Chain_Node>(new[] { second }));
            Assert(core.GetResponse()?.Count == 1, "response chain should be initialized and populated");
        }

        private static void TestNodeTypesNameLookup()
        {
            Assert(ReferenceEquals(NodeTypes.FromName("Concept"), NodeTypes.Concepts), "name lookup should be case-insensitive");
            Assert(ReferenceEquals(NodeTypes.FromName("not-a-real-type"), NodeTypes.Empties), "unknown names should return Empties");
        }

        private static void TestRuntimeLinkTypeRegistries()
        {
            Assert(ForwardLinkTypes.basic.Id == 0, "basic forward link ID should remain zero");
            Assert(InverseLinkTypes.basic.Id == 0, "basic inverse link ID should remain zero");
            Assert(ReferenceEquals(ForwardLinkTypes.FromId(0), ForwardLinkTypes.basic), "forward ID lookup should return the seeded instance");
            Assert(ReferenceEquals(InverseLinkTypes.FromId(0), InverseLinkTypes.basic), "inverse ID lookup should return the seeded instance");
            Assert(ReferenceEquals(ForwardLinkTypes.FromName("BASIC"), ForwardLinkTypes.basic), "forward name lookup should be case-insensitive");
            Assert(ReferenceEquals(InverseLinkTypes.FromName("BASIC"), InverseLinkTypes.basic), "inverse name lookup should be case-insensitive");
            Assert(ForwardLinkTypes.GetAll().Any(linkType => ReferenceEquals(linkType, ForwardLinkTypes.basic)), "forward registry should expose seeded values");
            Assert(InverseLinkTypes.GetAll().Any(linkType => ReferenceEquals(linkType, InverseLinkTypes.basic)), "inverse registry should expose seeded values");

            Assert(!ForwardLinkTypes.TryFromId(99999, out _), "unknown forward IDs should not resolve");
            Assert(!InverseLinkTypes.TryFromId(99999, out _), "unknown inverse IDs should not resolve");
            Assert(ReferenceEquals(ForwardLinkTypes.FromId(99999), ForwardLinkTypes.basic), "unknown forward IDs should fall back to basic");
            Assert(ReferenceEquals(InverseLinkTypes.FromId(99999), InverseLinkTypes.basic), "unknown inverse IDs should fall back to basic");

            Assert(LinkClass.empty.Id == 0, "empty link class ID should remain zero");
            Assert(LinkClass.basic.Id == 1, "basic link class ID should remain one");
            Assert(LinkClass.map_locations.Id == 2, "map_locations link class ID should remain two");
            Assert(LinkClass.events.Id == 3, "events link class ID should remain three");
            Assert(LinkClass.actions.Id == 4, "actions link class ID should remain four");
            Assert(LinkClass.spatial.Id == 5, "spatial link class ID should remain five");
            Assert(LinkClass.temporal.Id == 6, "temporal link class ID should remain six");
            Assert(LinkClass.sources.Id == 7, "sources link class ID should remain seven");
            Assert(LinkClass.identities.Id == 8, "identities link class ID should remain eight");

            Assert(ReferenceEquals(LinkClass.FromId(5), LinkClass.spatial), "spatial ID lookup should return the seeded instance");
            Assert(ReferenceEquals(LinkClass.FromName("SPATIAL"), LinkClass.spatial), "spatial name lookup should be case-insensitive");
            Assert(ReferenceEquals(LinkClass.FromId(99999), LinkClass.empty), "unknown link classes should fall back to empty");
            Assert(!LinkClass.TryFromId(99999, out _), "unknown link class IDs should not resolve");
            Assert(!LinkClass.GetAll().Any(linkClass => string.Equals(linkClass.Name, "spacial", StringComparison.OrdinalIgnoreCase)), "the spacial typo should not be registered");

            const int customForwardID = 10001;
            const int customInverseID = 10002;
            const int customClassID = 10003;

            ForwardLinkTypes customForward = ForwardLinkTypes.Register(customForwardID, "custom_forward");
            InverseLinkTypes customInverse = InverseLinkTypes.Register(customInverseID, "custom_inverse");
            LinkClass customClass = LinkClass.Register(customClassID, "custom_class");

            Assert(ReferenceEquals(ForwardLinkTypes.FromId(customForwardID), customForward), "registered forward type should resolve by ID");
            Assert(ReferenceEquals(InverseLinkTypes.FromId(customInverseID), customInverse), "registered inverse type should resolve by ID");
            Assert(ReferenceEquals(LinkClass.FromId(customClassID), customClass), "registered link class should resolve by ID");
            Assert(ReferenceEquals(ForwardLinkTypes.FromName("CUSTOM_FORWARD"), customForward), "registered forward type should resolve by name");
            Assert(ReferenceEquals(InverseLinkTypes.FromName("CUSTOM_INVERSE"), customInverse), "registered inverse type should resolve by name");
            Assert(ReferenceEquals(LinkClass.FromName("CUSTOM_CLASS"), customClass), "registered link class should resolve by name");
            Assert(customForward.ToString() == "custom_forward", "forward registry should preserve its name");
            Assert(customInverse.ToString() == "custom_inverse", "inverse registry should preserve its name");
            Assert(customClass.ToString() == "custom_class", "link class registry should preserve its name");

            Guid source = Guid.NewGuid();
            Guid destination = Guid.NewGuid();
            Link customLink = new Link(source, destination, customForwardID, customInverseID, customClass);

            Assert(ReferenceEquals(customLink.ForwardLink, customForward), "links should resolve registered forward IDs");
            Assert(ReferenceEquals(customLink.InverseLink, customInverse), "links should resolve registered inverse IDs");
            Assert(ReferenceEquals(customLink.Classification, customClass), "links should retain registered link classes");

            AssertThrows<ArgumentOutOfRangeException>(
                () => new Link(source, destination, 99999, 0, LinkClass.basic),
                "unknown forward link IDs should still be rejected");

            LRD_Core core = NewCore();
            Guid first = core.GenerateNewNode();
            Guid second = core.GenerateNewNode();

            Assert(core.AddDirectedConnection(first, second, customClass), "registered link classes should work with the core");
            Assert(core.FindByLinkClass(customClass).Any(node => node.ID == first), "registered link classes should be searchable");
        }

        private static void TestLinkTypeRegistryConcurrency()
        {
            const int iterations = 200;
            const int forwardBase = 100000;
            const int inverseBase = 200000;
            const int classBase = 300000;

            Parallel.For(0, iterations, index =>
            {
                int forwardID = forwardBase + index;
                int inverseID = inverseBase + index;
                int classID = classBase + index;

                ForwardLinkTypes forward = ForwardLinkTypes.Register(forwardID, $"forward_{index}");
                InverseLinkTypes inverse = InverseLinkTypes.Register(inverseID, $"inverse_{index}");
                LinkClass linkClass = LinkClass.Register(classID, $"class_{index}");

                Assert(ReferenceEquals(ForwardLinkTypes.FromId(forwardID), forward), "concurrent forward lookup should return the registered instance");
                Assert(ReferenceEquals(InverseLinkTypes.FromId(inverseID), inverse), "concurrent inverse lookup should return the registered instance");
                Assert(ReferenceEquals(LinkClass.FromId(classID), linkClass), "concurrent link class lookup should return the registered instance");
                Assert(ReferenceEquals(ForwardLinkTypes.FromName($"FORWARD_{index}"), forward), "concurrent forward name lookup should succeed");
                Assert(ReferenceEquals(InverseLinkTypes.FromName($"INVERSE_{index}"), inverse), "concurrent inverse name lookup should succeed");
                Assert(ReferenceEquals(LinkClass.FromName($"CLASS_{index}"), linkClass), "concurrent link class name lookup should succeed");

                _ = ForwardLinkTypes.GetAll().Count();
                _ = InverseLinkTypes.GetAll().Count();
                _ = LinkClass.GetAll().Count();
            });
        }
    }
}
