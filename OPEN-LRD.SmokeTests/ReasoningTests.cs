using OPEN_LRD;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Reasoning;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ReasoningTests
    {
        public static void RunAll()
        {
            Program.Run("Node labels and aliases", TestNodeLabelsAndAliases);
            Program.Run("Trust scale math", TestTrustScaleMath);
            Program.Run("Trust topic fallback", TestTrustTopicFallback);
            Program.Run("Query graph construction", TestQueryGraphConstruction);
            Program.Run("Query graph validation", TestQueryGraphValidation);
            Program.Run("Exact multi-hop matching", TestExactMultiHopMatching);
            Program.Run("No match and ambiguity", TestNoMatchAndAmbiguity);
            Program.Run("Neutral merge creates temporary node", TestNeutralMergeCreatesTemporaryNode);
            Program.Run("Trusted merge promotes temporary node", TestTrustedMergePromotesTemporaryNode);
            Program.Run("Discredited merge is rejected", TestDiscreditedMergeIsRejected);
            Program.Run("Merge is idempotent", TestMergeIsIdempotent);
            Program.Run("Neutral contradiction is rejected", TestNeutralContradictionIsRejected);
            Program.Run("Trusted contradiction replaces link", TestTrustedContradictionReplacesLink);
            Program.Run("Response extraction avoids cycles", TestResponseExtractionAvoidsCycles);
            Program.Run("Contradiction resolver trust policy", TestContradictionResolverTrustPolicy);
        }

        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        private static Guid AddNode(
            LRD_Core core,
            NodeTypes type,
            string? label = null,
            NodeLifetime lifetime = NodeLifetime.Temporary,
            params string[] aliases)
        {
            Guid nodeID = Guid.NewGuid();
            Web_Node node = new Web_Node(nodeID, type, lifetime, 100);
            node.Set_Label(label);

            foreach (string alias in aliases)
                node.Add_Alias(alias);

            Assert(core.AddExistingNode(node), "node should be added");
            return nodeID;
        }

        private static Chain_Node NewQueryNode(
            NodeTypes type,
            string? label = null,
            params string[] aliases)
        {
            Chain_Node node = new Chain_Node(Guid.NewGuid(), type, 100);
            node.Set_Label(label);

            foreach (string alias in aliases)
                node.Add_Alias(alias);

            return node;
        }

        private static LinkedList<Chain_Node> LinkChain(
            LinkClass classification,
            params Chain_Node[] nodes)
        {
            for (int i = 0; i < nodes.Length - 1; i++)
            {
                Link link = new Link(
                    nodes[i].ID,
                    nodes[i + 1].ID,
                    ForwardLinkTypes.basic,
                    InverseLinkTypes.basic,
                    classification);

                nodes[i].Set_LinkNext(link);
                nodes[i + 1].Set_LinkPrevious(link);
            }

            LinkedList<Chain_Node> chain = new LinkedList<Chain_Node>();

            foreach (Chain_Node node in nodes)
                chain.AddLast(node);

            return chain;
        }

        private static void AssertClose(double actual, double expected, string message)
        {
            if (Math.Abs(actual - expected) > 0.000001)
                throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
        }

        private static void TestNodeLabelsAndAliases()
        {
            LRD_Core core = NewCore();
            Web_Node node = new Web_Node(Guid.NewGuid(), NodeTypes.Entities, NodeLifetime.Permanent, 10);

            Assert(node.Label is null && node.Aliases.Count == 0, "new node should have no label or aliases");
            node.Set_Label(" Alpha ");
            Assert(node.Has_Label("alpha"), "label lookup should be case-insensitive");
            Assert(node.Add_Alias("A1"), "first alias should be added");
            Assert(!node.Add_Alias("a1"), "duplicate alias should be rejected");
            Assert(!node.Add_Alias(" ALPHA "), "label text should not be stored as an alias");
            Assert(node.Has_Alias("A1"), "alias lookup should work");
            Assert(node.Remove_Alias("a1"), "alias removal should be case-insensitive");

            Assert(core.AddExistingNode(node), "node should be added");
            Assert(core.FindByLabel("alpha").Any(found => found.ID == node.ID), "label lookup should find node");
            Assert(core.FindByLabel("alpha", NodeTypes.Entities).Any(found => found.ID == node.ID), "matching type filter should find node");
            Assert(core.FindByLabel("alpha", NodeTypes.Concepts).Count == 0, "wrong type filter should return no nodes");
            Assert(core.FindByLabel("missing").Count == 0, "missing label should return no nodes");
            Assert(!core.TryFindByLabel("missing", out IReadOnlyList<Web_Node> missing) || missing.Count == 0, "missing label should not be found");
        }

        private static void TestTrustScaleMath()
        {
            ReasoningOptions options = new ReasoningOptions();
            TrustScale trust = new TrustScale(options);
            Guid source = Guid.NewGuid();

            TrustAssessment empty = trust.Assess(source, null);
            Assert(empty.Decision == TrustDecision.Neutral && empty.TotalEvidence == 0, "no evidence should be neutral");

            trust.RecordEvidenceBatch(source, null, 5, 0);
            TrustAssessment five = trust.Assess(source, null);
            AssertClose(five.Ratio, 6.0 / 7.0, "ratio after five verified");
            AssertClose(five.Confidence, 0.5, "confidence after five verified");
            AssertClose(five.Score, (6.0 / 7.0) * 0.5, "score after five verified");
            Assert(five.Decision == TrustDecision.Neutral, "five verified should not yet be trusted");

            trust.RecordEvidenceBatch(source, null, 45, 0);
            TrustAssessment fifty = trust.Assess(source, null);
            AssertClose(fifty.Ratio, 51.0 / 52.0, "ratio after fifty verified");
            AssertClose(fifty.Confidence, 50.0 / 55.0, "confidence after fifty verified");
            AssertClose(fifty.Score, (51.0 / 52.0) * (50.0 / 55.0), "score after fifty verified");
            Assert(fifty.Decision == TrustDecision.Trusted, "fifty verified should be trusted");

            TrustScale discreditedTrust = new TrustScale(options);
            Guid discreditedSource = Guid.NewGuid();
            discreditedTrust.RecordEvidenceBatch(discreditedSource, null, 0, 100);
            TrustAssessment discredited = discreditedTrust.Assess(discreditedSource, null);
            AssertClose(discredited.Score, (1.0 / 102.0) * (100.0 / 105.0), "discredited score");
            Assert(discredited.Decision == TrustDecision.Discredited, "large invalid evidence should be discredited");
        }

        private static void TestTrustTopicFallback()
        {
            TrustScale trust = new TrustScale(new ReasoningOptions());
            Guid source = Guid.NewGuid();
            Guid topic = Guid.NewGuid();

            trust.RecordEvidenceBatch(source, null, 50, 0);
            TrustAssessment global = trust.Assess(source, topic);
            Assert(global.Decision == TrustDecision.Trusted, "global trust should be trusted");
            Assert(global.UsesGlobalFallback && !global.UsesTopicEvidence, "missing topic evidence should fall back to global");

            trust.RecordEvidenceBatch(source, topic, 1, 0);
            TrustAssessment insufficientTopic = trust.Assess(source, topic);
            Assert(insufficientTopic.UsesGlobalFallback, "insufficient topic evidence should still fall back to global");

            trust.RecordEvidenceBatch(source, topic, 4, 0);
            TrustAssessment topicAssessment = trust.Assess(source, topic);
            Assert(topicAssessment.UsesTopicEvidence && !topicAssessment.UsesGlobalFallback, "sufficient topic evidence should be used");
            Assert(topicAssessment.TotalEvidence == 5, "topic evidence count should be five");
        }

        private static void TestQueryGraphConstruction()
        {
            Chain_Node first = NewQueryNode(NodeTypes.Entities, "First");
            Chain_Node second = NewQueryNode(NodeTypes.Concepts, "Second");
            LinkedList<Chain_Node> chain = LinkChain(LinkClass.identities, first, second);

            QueryGraph graph = QueryGraphBuilder.Build(chain);
            Assert(graph.Nodes.Count == 2, "query graph should contain two nodes");
            Assert(graph.Edges.Count == 1, "query graph should contain one edge");
            Assert(graph.IsLinear, "two-node chain should be linear");
            Assert(graph.RootQueryNodeID == first.ID, "first chain node should be the root");
            Assert(graph.Edges[0].Classification == LinkClass.identities, "edge classification should be preserved");

            Chain_Node third = NewQueryNode(NodeTypes.Events, "Third");
            Chain_Node fourth = NewQueryNode(NodeTypes.Events, "Fourth");
            LinkedList<Chain_Node> unlinked = new LinkedList<Chain_Node>();
            unlinked.AddLast(third);
            unlinked.AddLast(fourth);

            QueryGraph fallback = QueryGraphBuilder.Build(unlinked);
            Assert(fallback.Edges.Count == 1, "missing links should produce a default edge");
            Assert(fallback.Edges[0].Classification == LinkClass.basic, "default edge should be basic");
        }

        private static void TestQueryGraphValidation()
        {
            AssertThrows<ArgumentNullException>(
                () => QueryGraphBuilder.Build(null!),
                "null chain should be rejected");

            AssertThrows<ArgumentException>(
                () => QueryGraphBuilder.Build(new LinkedList<Chain_Node>()),
                "empty chain should be rejected");

            Chain_Node duplicate = NewQueryNode(NodeTypes.Entities, "Duplicate");
            LinkedList<Chain_Node> duplicateChain = new LinkedList<Chain_Node>();
            duplicateChain.AddLast(duplicate);
            duplicateChain.AddLast(duplicate);

            AssertThrows<ArgumentException>(
                () => QueryGraphBuilder.Build(duplicateChain),
                "duplicate query IDs should be rejected");

            Chain_Node first = NewQueryNode(NodeTypes.Entities, "First");
            Chain_Node second = NewQueryNode(NodeTypes.Entities, "Second");
            first.Set_LinkNext(new Link(first.ID, Guid.NewGuid(), LinkClass.basic));
            LinkedList<Chain_Node> inconsistent = new LinkedList<Chain_Node>();
            inconsistent.AddLast(first);
            inconsistent.AddLast(second);

            AssertThrows<ArgumentException>(
                () => QueryGraphBuilder.Build(inconsistent),
                "links with external endpoints should be rejected");
        }

        private static void TestExactMultiHopMatching()
        {
            LRD_Core core = NewCore();
            Guid firstAlpha = AddNode(core, NodeTypes.Entities, "Alpha", NodeLifetime.Permanent);
            Guid beta = AddNode(core, NodeTypes.Entities, "Beta", NodeLifetime.Permanent);
            Guid secondAlpha = AddNode(core, NodeTypes.Entities, "Alpha", NodeLifetime.Permanent);
            Guid gamma = AddNode(core, NodeTypes.Entities, "Gamma", NodeLifetime.Permanent);

            Assert(core.AddDirectedConnection(firstAlpha, beta, LinkClass.identities), "first chain link should be added");
            Assert(core.AddDirectedConnection(secondAlpha, gamma, LinkClass.identities), "second chain link should be added");

            Chain_Node queryAlpha = NewQueryNode(NodeTypes.Entities, "Alpha");
            Chain_Node queryBeta = NewQueryNode(NodeTypes.Entities, "Beta");
            LinkedList<Chain_Node> query = LinkChain(LinkClass.identities, queryAlpha, queryBeta);

            ReasoningEngine engine = new ReasoningEngine(core);
            ReasoningResult result = engine.Answer(query);

            Assert(result.Status == ReasoningStatus.Answered, $"expected Answered, got {result.Status}");
            Assert(result.SelectedMatch is not null, "a match should be selected");
            Assert(result.SelectedMatch!.NodeMap[queryAlpha.ID] == firstAlpha, "the intended Alpha node should be selected");
            Assert(result.SelectedMatch.NodeMap[queryBeta.ID] == beta, "Beta should be mapped");
            Assert(result.ResponsePaths.Count > 0, "a response path should be returned");
            Assert(result.ResponsePaths[0].NodeIDs.Contains(beta), "response should include Beta");
            Assert(result.ResponsePaths[0].NodeIDs.Contains(firstAlpha), "response should include the selected Alpha");
        }

        private static void TestNoMatchAndAmbiguity()
        {
            LRD_Core core = NewCore();
            Guid first = AddNode(core, NodeTypes.Entities, "Same", NodeLifetime.Permanent);
            AddNode(core, NodeTypes.Entities, "Same", NodeLifetime.Permanent);

            ReasoningEngine engine = new ReasoningEngine(core);
            Chain_Node duplicateQuery = NewQueryNode(NodeTypes.Entities, "Same");
            LinkedList<Chain_Node> ambiguousQuery = new LinkedList<Chain_Node>();
            ambiguousQuery.AddLast(duplicateQuery);

            int before = core.GetDictionary()!.Count;
            ReasoningResult ambiguous = engine.Answer(ambiguousQuery);
            Assert(ambiguous.Status == ReasoningStatus.Ambiguous, $"expected Ambiguous, got {ambiguous.Status}");
            Assert(ambiguous.Matches.Count >= 2, "ambiguous result should return multiple matches");
            Assert(core.GetDictionary()!.Count == before, "ambiguous answer should not mutate the graph");
            Assert(core.GetResponse() is null || core.GetResponse()!.Count == 0, "ambiguous answer should not write a response chain");

            Chain_Node missingQuery = NewQueryNode(NodeTypes.Entities, "Missing");
            LinkedList<Chain_Node> missingChain = new LinkedList<Chain_Node>();
            missingChain.AddLast(missingQuery);

            ReasoningResult missing = engine.Answer(missingChain);
            Assert(missing.Status == ReasoningStatus.NoMatch, $"expected NoMatch, got {missing.Status}");
            Assert(core.GetDictionary()!.Count == before, "no-match answer should not mutate the graph");
            Assert(first != Guid.Empty, "first node should exist");
        }

        private static void TestNeutralMergeCreatesTemporaryNode()
        {
            LRD_Core core = NewCore();
            Guid root = AddNode(core, NodeTypes.Entities, "Root", NodeLifetime.Permanent);

            Chain_Node queryRoot = NewQueryNode(NodeTypes.Entities, "Root");
            Chain_Node queryChild = NewQueryNode(NodeTypes.Concepts, "Child");
            LinkedList<Chain_Node> query = LinkChain(LinkClass.identities, queryRoot, queryChild);

            ReasoningEngine engine = new ReasoningEngine(core);
            int before = core.GetDictionary()!.Count;
            ReasoningResult result = engine.Ingest(query);

            Assert(result.Status == ReasoningStatus.Answered, $"expected Answered, got {result.Status}");
            Assert(result.GraphMutated, "ingest should mutate the graph");
            Assert(core.GetDictionary()!.Count == before + 1, "one node should be created");
            Assert(result.SelectedMatch!.NodeMap[queryRoot.ID] == root, "existing root should be reused");

            IReadOnlyList<Web_Node> childNodes = core.FindByLabel("Child", NodeTypes.Concepts);
            Assert(childNodes.Count == 1, "one child node should be created");
            Assert(childNodes[0].IsTemporary, "neutral source should create a temporary node");
            Assert(result.PromotedNodeIDs.Count == 0, "neutral source should not promote nodes");
            Assert(core.GetLinks(root).Any(link =>
                link.DestinationID == childNodes[0].ID
                && link.Classification == LinkClass.identities), "new link should be added");
        }

        private static void TestTrustedMergePromotesTemporaryNode()
        {
            LRD_Core core = NewCore();
            Guid root = AddNode(core, NodeTypes.Entities, "Root", NodeLifetime.Permanent);
            Guid source = AddNode(core, NodeTypes.Sources, "Source", NodeLifetime.Permanent);

            Chain_Node queryRoot = NewQueryNode(NodeTypes.Entities, "Root");
            Chain_Node queryChild = NewQueryNode(NodeTypes.Concepts, "TrustedChild");
            LinkedList<Chain_Node> query = LinkChain(LinkClass.identities, queryRoot, queryChild);

            ReasoningEngine engine = new ReasoningEngine(core);
            engine.RecordTrustEvidence(source, null, true, 50);

            ReasoningResult result = engine.Ingest(query, source);
            Assert(result.Status == ReasoningStatus.Answered, $"expected Answered, got {result.Status}");
            Assert(result.Trust is not null && result.Trust.Decision == TrustDecision.Trusted, "source should be trusted");

            IReadOnlyList<Web_Node> childNodes = core.FindByLabel("TrustedChild", NodeTypes.Concepts);
            Assert(childNodes.Count == 1, "child should be created");
            Assert(childNodes[0].IsPermanent, "trusted source should promote the new node");
            Assert(result.PromotedNodeIDs.Contains(childNodes[0].ID), "promotion should be reported");
            Assert(root != Guid.Empty, "root should be reused");
        }

        private static void TestDiscreditedMergeIsRejected()
        {
            LRD_Core core = NewCore();
            Guid root = AddNode(core, NodeTypes.Entities, "Root", NodeLifetime.Permanent);
            Guid source = AddNode(core, NodeTypes.Sources, "BadSource", NodeLifetime.Permanent);

            Chain_Node queryRoot = NewQueryNode(NodeTypes.Entities, "Root");
            Chain_Node queryChild = NewQueryNode(NodeTypes.Concepts, "RejectedChild");
            LinkedList<Chain_Node> query = LinkChain(LinkClass.identities, queryRoot, queryChild);

            ReasoningEngine engine = new ReasoningEngine(core);
            engine.RecordTrustEvidence(source, null, false, 100);

            int before = core.GetDictionary()!.Count;
            ReasoningResult result = engine.Ingest(query, source);

            Assert(result.Status == ReasoningStatus.RejectedByTrust, $"expected RejectedByTrust, got {result.Status}");
            Assert(!result.GraphMutated, "discredited ingest should not mutate the graph");
            Assert(core.GetDictionary()!.Count == before, "no nodes should be created");
            Assert(core.FindByLabel("RejectedChild").Count == 0, "rejected child should not exist");
            Assert(root != Guid.Empty, "root should exist");
        }

        private static void TestMergeIsIdempotent()
        {
            LRD_Core core = NewCore();
            Guid root = AddNode(core, NodeTypes.Entities, "Root", NodeLifetime.Permanent);

            Chain_Node queryRoot = NewQueryNode(NodeTypes.Entities, "Root");
            Chain_Node queryChild = NewQueryNode(NodeTypes.Concepts, "Child");
            LinkedList<Chain_Node> query = LinkChain(LinkClass.identities, queryRoot, queryChild);

            ReasoningEngine engine = new ReasoningEngine(core);
            ReasoningResult first = engine.Ingest(query);
            Assert(first.GraphMutated, "first ingest should mutate the graph");

            Guid child = core.FindByLabel("Child", NodeTypes.Concepts).Single().ID;
            int linksBefore = core.GetLinks(root).Count(link =>
                link.DestinationID == child && link.Classification == LinkClass.identities);

            LinkedList<Chain_Node> secondQuery = LinkChain(
                LinkClass.identities,
                NewQueryNode(NodeTypes.Entities, "Root"),
                NewQueryNode(NodeTypes.Concepts, "Child"));

            ReasoningResult second = engine.Ingest(secondQuery);
            Assert(second.Status == ReasoningStatus.Answered, $"expected Answered, got {second.Status}");
            Assert(!second.GraphMutated, "second ingest should not add duplicate links");
            Assert(core.GetDictionary()!.Count == 2, "nodes should not be duplicated");

            int linksAfter = core.GetLinks(root).Count(link =>
                link.DestinationID == child && link.Classification == LinkClass.identities);

            Assert(linksBefore == 1 && linksAfter == 1, "the matched edge should be stored exactly once");
        }

        private static void TestNeutralContradictionIsRejected()
        {
            LRD_Core core = NewCore();
            Guid source = AddNode(core, NodeTypes.Entities, "Source", NodeLifetime.Permanent);
            Guid oldTarget = AddNode(core, NodeTypes.Entities, "OldTarget", NodeLifetime.Permanent);
            AddNode(core, NodeTypes.Entities, "NewTarget", NodeLifetime.Permanent);

            Assert(core.AddDirectedConnection(source, oldTarget, LinkClass.identities), "old functional link should be added");

            Chain_Node querySource = NewQueryNode(NodeTypes.Entities, "Source");
            Chain_Node queryNewTarget = NewQueryNode(NodeTypes.Entities, "NewTarget");
            LinkedList<Chain_Node> query = LinkChain(LinkClass.identities, querySource, queryNewTarget);

            ReasoningEngine engine = new ReasoningEngine(core);
            ReasoningResult result = engine.Ingest(query);

            Assert(result.Status == ReasoningStatus.Contradiction, $"expected Contradiction, got {result.Status}");
            Assert(!result.GraphMutated, "neutral contradiction should not mutate the graph");
            Assert(result.Conflicts.Count == 1, "one conflict should be recorded");
            Assert(!core.GetLinks(source).Any(link =>
                link.DestinationID == core.FindByLabel("NewTarget").Single().ID), "proposed link should be rejected");
            Assert(core.GetLinks(source).Any(link =>
                link.DestinationID == oldTarget && link.Classification == LinkClass.identities),
                "old link should remain");
        }

        private static void TestTrustedContradictionReplacesLink()
        {
            LRD_Core core = NewCore();
            Guid source = AddNode(core, NodeTypes.Entities, "Source", NodeLifetime.Permanent);
            Guid oldTarget = AddNode(core, NodeTypes.Entities, "OldTarget", NodeLifetime.Permanent);
            Guid newTarget = AddNode(core, NodeTypes.Entities, "NewTarget", NodeLifetime.Permanent);
            Guid evidenceSource = AddNode(core, NodeTypes.Sources, "EvidenceSource", NodeLifetime.Permanent);

            Assert(core.AddDirectedConnection(source, oldTarget, LinkClass.identities), "old functional link should be added");

            Chain_Node querySource = NewQueryNode(NodeTypes.Entities, "Source");
            Chain_Node queryNewTarget = NewQueryNode(NodeTypes.Entities, "NewTarget");
            LinkedList<Chain_Node> query = LinkChain(LinkClass.identities, querySource, queryNewTarget);

            ReasoningEngine engine = new ReasoningEngine(core);
            engine.RecordTrustEvidence(evidenceSource, null, true, 50);

            ReasoningResult result = engine.IngestAndAnswer(query, evidenceSource);
            Assert(result.Status == ReasoningStatus.Contradiction, $"expected Contradiction, got {result.Status}");
            Assert(result.GraphMutated, "trusted contradiction should mutate reasoning metadata and links");
            Assert(result.Conflicts.Count == 1, "one conflict should be recorded");
            Assert(core.GetLinks(source).Any(link =>
                link.DestinationID == newTarget && link.Classification == LinkClass.identities),
                "new trusted link should be added");
            Assert(core.GetStoredLinks(source).Any(link =>
                link.DestinationID == oldTarget && link.Classification == LinkClass.identities && !link.IsActive),
                "old link should remain inactive in the graph for audit");

            Assert(result.ResponsePaths.Count > 0, "a response path should be extracted");
            Assert(!result.ResponsePaths.Any(path => path.Links.Any(link =>
                link.DestinationID == oldTarget && link.Classification == LinkClass.identities)),
                "invalid old link should be excluded from response paths");
        }

        private static void TestResponseExtractionAvoidsCycles()
        {
            LRD_Core core = NewCore();
            Guid first = AddNode(core, NodeTypes.Entities, "First", NodeLifetime.Permanent);
            Guid second = AddNode(core, NodeTypes.Entities, "Second", NodeLifetime.Permanent);

            Assert(core.AddConnection(first, second, LinkClass.basic), "two-way connection should be added");

            Chain_Node queryFirst = NewQueryNode(NodeTypes.Entities, "First");
            LinkedList<Chain_Node> query = new LinkedList<Chain_Node>();
            query.AddLast(queryFirst);

            ReasoningEngine engine = new ReasoningEngine(core);
            ReasoningResult result = engine.Answer(query);

            Assert(result.Status == ReasoningStatus.Answered, $"expected Answered, got {result.Status}");
            Assert(result.ResponsePaths.Count > 0, "response paths should be returned");

            foreach (GraphPath path in result.ResponsePaths)
            {
                Assert(path.NodeIDs.Count <= 3, "cycle traversal should be bounded");
                Assert(path.NodeIDs.Distinct().Count() == path.NodeIDs.Count, "response path should not repeat nodes");
            }

            Assert(result.ResponseChain is not null, "top response should be converted to a chain");
        }

        private static void TestContradictionResolverTrustPolicy()
        {
            ReasoningOptions options = new ReasoningOptions();
            Link proposed = new Link(Guid.NewGuid(), Guid.NewGuid(), LinkClass.identities);
            Link existing = new Link(proposed.SourceID, Guid.NewGuid(), LinkClass.identities);

            TrustAssessment neutral = new TrustAssessment(
                proposed.SourceID, null, 0, 0, 0, 0.5, 0, 0, TrustDecision.Neutral, false, false);

            TrustAssessment trusted = new TrustAssessment(
                proposed.SourceID, null, 50, 0, 50, 51.0 / 52.0, 50.0 / 55.0, 0.89, TrustDecision.Trusted, false, false);

            TrustAssessment discredited = new TrustAssessment(
                proposed.SourceID, null, 0, 100, 100, 1.0 / 102.0, 100.0 / 105.0, 0.01, TrustDecision.Discredited, false, false);

            ContradictionResolution neutralResult = ContradictionResolver.Resolve(
                proposed,
                LinkClass.identities,
                new[] { existing },
                neutral,
                _ => neutral,
                options);

            Assert(!neutralResult.Accepted && neutralResult.Conflicts.Count == 1, "neutral contradiction should reject proposed link");

            ContradictionResolution trustedResult = ContradictionResolver.Resolve(
                proposed,
                LinkClass.identities,
                new[] { existing },
                trusted,
                _ => neutral,
                options);

            Assert(trustedResult.Accepted, "clearly stronger proposed trust should be accepted");
            Assert(trustedResult.InvalidatedLinks.Count == 1, "existing link should be invalidated");

            ContradictionResolution discreditedResult = ContradictionResolver.Resolve(
                proposed,
                LinkClass.identities,
                new[] { existing },
                discredited,
                _ => neutral,
                options);

            Assert(!discreditedResult.Accepted, "discredited proposed link should be rejected");

            ContradictionResolution trustedExistingResult = ContradictionResolver.Resolve(
                proposed,
                LinkClass.identities,
                new[] { existing },
                neutral,
                _ => trusted,
                options);

            Assert(!trustedExistingResult.Accepted, "trusted existing link should reject neutral proposal");
        }
    }
}
