using System.Collections.Generic;
using System.Linq;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;

namespace OPEN_LRD.SmokeTests
{
    internal static class CoreNodeGraphTests
    {
        private sealed record LexicalFixture(
            Guid SenseId,
            Guid UsageId,
            Guid SymbolId,
            Guid WordId,
            Guid SentenceId,
            Guid SenseNodeId,
            Guid UsageNodeId,
            Guid SymbolNodeId,
            Guid WordNodeId,
            Guid SentenceNodeId);

        internal static void RunAll()
        {
            Program.Run("Foundation identity identifiers reject Guid.Empty", EmptyIdentityIdsAreRejected());
            Program.Run("GroupBox stores sentence IDs and snapshots", GroupBoxStoresSentenceIdsAndSnapshots());
            Program.Run("Trusted reverse and containment indexes", ReverseIndexesTrackTrustedIdentities());
            Program.Run("Trusted boxes create nodes and structural links", TrustedBoxesCreateNodesAndStructuralLinks());
            Program.Run("Untrusted node promotion preserves node identity", UntrustedNodePromotionPreservesNodeIdentity());
            Program.Run("Sentence subgraph promotion is atomic", SentenceSubgraphPromotionPromotesDependencies());
            Program.Run("Skills graph snapshots support graph algorithms", SkillsGraphSnapshotsSupportGraphAlgorithms());
            Program.Run("Subgraph identity collisions do not partially promote", SubgraphIdentityCollisionsAreAtomic());
            Program.Run("Trusted graph membership and link lifecycle are hardened", TrustedMembershipAndLinkLifecycleAreHardened());
            Program.Run("Conversation and function APIs return validated snapshots", ConversationAndFunctionApisAreHardened());
        }

        private static Action EmptyIdentityIdsAreRejected()
        {
            return () =>
            {
                Guid senseId = Guid.NewGuid();
                Guid usageId = Guid.NewGuid();

                Program.AssertThrows<ArgumentException>(() => new SymbolBox(Guid.Empty, 'a'), "Symbol IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new WordBox(Guid.Empty, senseId, usageId), "Word IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new WordBox(senseId, usageId, new List<Guid> { Guid.Empty }), "Word symbol IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new SentenceBox(Guid.Empty), "Sentence IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new SentenceBox(senseId, Guid.NewGuid(), new List<Guid> { Guid.Empty }), "Sentence word IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new SenseBox(Guid.Empty), "Sense IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new UsageBox(Guid.Empty), "Usage IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new UsageBox(usageId, new List<Guid> { Guid.Empty }), "Usage sentence IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new GroupBox(Guid.Empty), "Group IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new SourceBox(Guid.Empty), "Source IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => new FunctionBox(Guid.Empty, typeof(double)), "Function IDs cannot be empty.");

                SymbolBox symbol = new SymbolBox();
                WordBox word = new WordBox(senseId, usageId);
                SentenceBox sentence = new SentenceBox();
                Program.AssertThrows<ArgumentException>(() => symbol.SetSenseID(Guid.Empty), "Symbol sense IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => word.SetUsageID(Guid.Empty), "Word usage IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => word.SetSymbols(new List<Guid> { Guid.Empty }), "Word symbol IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => sentence.SetWords(new List<Guid> { Guid.Empty }), "Sentence word IDs cannot be empty.");
                Program.AssertThrows<ArgumentException>(() => sentence.SetSenseID(Guid.Empty), "Sentence sense IDs cannot be empty.");
            };
        }

        private static Action GroupBoxStoresSentenceIdsAndSnapshots()
        {
            return () =>
            {
                Guid groupId = Guid.NewGuid();
                Guid senseId = Guid.NewGuid();
                Guid wordId = Guid.NewGuid();
                SentenceBox sentence = new SentenceBox(Guid.NewGuid(), senseId, new List<Guid> { wordId });
                Dictionary<Guid, SentenceBox> sentences = new Dictionary<Guid, SentenceBox>
                {
                    { sentence.GetSentenceID(), sentence }
                };

                GroupBox group = new GroupBox(groupId, sentences);
                List<Guid>? sentenceIds = group.GetSentences();
                Program.Assert(sentenceIds != null && sentenceIds.Count == 1, "Group should expose stored sentence IDs.");
                AssertContains(sentenceIds!, sentence.GetSentenceID(), "Group should store the sentence ID.");
                sentenceIds!.Clear();
                AssertContains(group.GetSentenceIDs() ?? new List<Guid>(), sentence.GetSentenceID(), "Returned group snapshots must be isolated.");

                Program.Assert(!group.AddSentence(sentence.GetSentenceID()), "Duplicate sentence IDs must be ignored.");
                Program.Assert(group.AddSentence(Guid.NewGuid()), "New sentence IDs must be accepted.");
                Program.Assert(group.GetSentenceIDs()!.Count == 2, "Group should contain both sentence IDs.");
            };
        }

        private static Action ReverseIndexesTrackTrustedIdentities()
        {
            return () =>
            {
                LexicalFixture fixture = BuildLexicalChain();
                LRD_Core core = LRD_Core.Instance;

                AssertContains(core.GetTrustedSymbolsBySenseID(fixture.SenseId), fixture.SymbolId, "Symbol sense index should contain the trusted symbol.");
                AssertContains(core.GetTrustedWordsBySenseID(fixture.SenseId), fixture.WordId, "Word sense index should contain the trusted word.");
                AssertContains(core.GetTrustedSentencesBySenseID(fixture.SenseId), fixture.SentenceId, "Sentence sense index should contain the trusted sentence.");
                AssertContains(core.GetTrustedWordsByUsageID(fixture.UsageId), fixture.WordId, "Word usage index should contain the trusted word.");
                AssertContains(core.GetTrustedWordsContainingSymbol(fixture.SymbolId), fixture.WordId, "Symbol containment index should contain the trusted word.");
                AssertContains(core.GetTrustedSentencesContainingWord(fixture.WordId), fixture.SentenceId, "Word containment index should contain the trusted sentence.");

                List<Guid> symbols = core.GetTrustedSymbolsBySenseID(fixture.SenseId);
                symbols.Clear();
                AssertContains(core.GetTrustedSymbolsBySenseID(fixture.SenseId), fixture.SymbolId, "Returned index snapshots must be isolated.");
            };
        }

        private static Action TrustedBoxesCreateNodesAndStructuralLinks()
        {
            return () =>
            {
                LexicalFixture fixture = BuildLexicalChain();
                LRD_Core core = LRD_Core.Instance;

                Program.Assert(fixture.SymbolNodeId != fixture.SymbolId, "Node ID must be distinct from the wrapped box ID.");
                Program.Assert(core.GetTrustedNode(fixture.WordNodeId)?.IsTrusted == true, "Trusted boxes must have trusted nodes.");

                Node? symbolNode = core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, fixture.SymbolId);
                Node? wordNode = core.GetTrustedNodeByTarget(NodeTargetKind.Word, fixture.WordId);
                Node? sentenceNode = core.GetTrustedNodeByTarget(NodeTargetKind.Sentence, fixture.SentenceId);
                Program.Assert(symbolNode != null && wordNode != null && sentenceNode != null, "Every lexical box should have a trusted node.");

                AssertContainsLink(core.GetOutgoingLinks(symbolNode!.GetNodeID(), NodeRelationType.HasSense), fixture.SenseNodeId, "Symbol should link to its sense.");
                AssertContainsLink(core.GetOutgoingLinks(wordNode!.GetNodeID(), NodeRelationType.HasSense), fixture.SenseNodeId, "Word should link to its sense.");
                AssertContainsLink(core.GetOutgoingLinks(wordNode.GetNodeID(), NodeRelationType.HasUsage), fixture.UsageNodeId, "Word should link to its usage.");
                AssertContainsLink(core.GetOutgoingLinks(wordNode.GetNodeID(), NodeRelationType.ContainsSymbol), fixture.SymbolNodeId, "Word should link to its symbol.");
                AssertContainsLink(core.GetOutgoingLinks(sentenceNode!.GetNodeID(), NodeRelationType.ContainsWord), fixture.WordNodeId, "Sentence should link to its word.");
                AssertContainsLink(core.GetOutgoingLinks(sentenceNode.GetNodeID(), NodeRelationType.LeadingWord), fixture.WordNodeId, "Sentence should link to its leading word.");
                AssertContainsLink(core.GetOutgoingLinks(sentenceNode.GetNodeID(), NodeRelationType.TrailingWord), fixture.WordNodeId, "Sentence should link to its trailing word.");

                SenseBox? senseBox = core.GetTrustedSenseBox(fixture.SenseId);
                UsageBox? usageBox = core.GetTrustedUsageBox(fixture.UsageId);
                Program.Assert(senseBox != null && senseBox.GetSentences()!.Contains(fixture.SentenceId), "SenseBox should receive trusted sentence membership.");
                Program.Assert(senseBox!.GetLeadingWords()!.Contains(fixture.WordId), "SenseBox should receive leading word membership.");
                Program.Assert(senseBox.GetTrailingWords()!.Contains(fixture.WordId), "SenseBox should receive trailing word membership.");
                Program.Assert(usageBox != null && usageBox.GetSentences()!.Contains(fixture.SentenceId), "UsageBox should receive trusted sentence membership.");
            };
        }

        private static Action UntrustedNodePromotionPreservesNodeIdentity()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid symbolId = Guid.NewGuid();
                SymbolBox symbol = new SymbolBox(symbolId, 'u');
                symbol.SetSenseID(senseId);

                Program.Assert(core.SetUntrustedSymbol(symbol), "Symbol should be accepted as untrusted.");
                Node? untrustedNode = core.GetUntrustedNodeByTarget(NodeTargetKind.Symbol, symbolId);
                Program.Assert(untrustedNode != null && !untrustedNode.IsTrusted, "Untrusted symbol should have an untrusted node.");

                Program.Assert(core.ConvertUntrustedSymbolToTrusted(symbolId), "Complete symbol should promote.");
                Node? trustedNode = core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId);
                Program.Assert(trustedNode != null && trustedNode.GetNodeID() == untrustedNode!.GetNodeID(), "Promotion should preserve node identity.");
                Program.Assert(trustedNode!.IsTrusted, "Promoted node should be trusted.");
                Program.Assert(core.GetUntrustedNodeByTarget(NodeTargetKind.Symbol, symbolId) == null, "Promoted node should leave the untrusted store.");
            };
        }

        private static Action SentenceSubgraphPromotionPromotesDependencies()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid symbolId = Guid.NewGuid();
                Guid wordId = Guid.NewGuid();
                Guid sentenceId = Guid.NewGuid();

                SymbolBox symbol = new SymbolBox(symbolId, 's');
                symbol.SetSenseID(senseId);
                WordBox word = new WordBox(wordId, senseId, usageId, new List<Guid> { symbolId }, PartOfSpeech.Noun);
                SentenceBox sentence = new SentenceBox(sentenceId, senseId, new List<Guid> { wordId });

                Program.Assert(core.SetUntrustedSymbol(symbol), "Symbol should be staged.");
                Program.Assert(core.SetUntrustedWord(word), "Word should be staged.");
                Program.Assert(core.SetUntrustedSentence(sentence), "Sentence should be staged.");
                Guid symbolNodeId = core.GetUntrustedNodeByTarget(NodeTargetKind.Symbol, symbolId)!.GetNodeID();
                Guid wordNodeId = core.GetUntrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID();
                Guid sentenceNodeId = core.GetUntrustedNodeByTarget(NodeTargetKind.Sentence, sentenceId)!.GetNodeID();

                Program.Assert(!core.ConvertUntrustedSentenceToTrusted(sentenceId), "Single sentence promotion must fail with untrusted dependencies.");
                Program.Assert(core.ConvertUntrustedSentenceSubgraphToTrusted(sentenceId), "Subgraph promotion should promote all dependencies.");

                Program.Assert(core.GetTrustedSymbolBox(symbolId) != null, "Symbol should be trusted after subgraph promotion.");
                Program.Assert(core.GetTrustedWordBox(wordId) != null, "Word should be trusted after subgraph promotion.");
                Program.Assert(core.GetTrustedSentenceBox(sentenceId) != null, "Sentence should be trusted after subgraph promotion.");
                Program.Assert(core.GetUntrustedSymbolBox(symbolId) == null, "Symbol should leave untrusted storage.");
                Program.Assert(core.GetUntrustedWordBox(wordId) == null, "Word should leave untrusted storage.");
                Program.Assert(core.GetUntrustedSentenceBox(sentenceId) == null, "Sentence should leave untrusted storage.");
                Program.Assert(core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId)!.GetNodeID() == symbolNodeId, "Symbol node identity should be preserved.");
                Program.Assert(core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID() == wordNodeId, "Word node identity should be preserved.");
                Program.Assert(core.GetTrustedNodeByTarget(NodeTargetKind.Sentence, sentenceId)!.GetNodeID() == sentenceNodeId, "Sentence node identity should be preserved.");
            };
        }

        private static Action SkillsGraphSnapshotsSupportGraphAlgorithms()
        {
            return () =>
            {
                LexicalFixture fixture = BuildLexicalChain();
                LRD_Core core = LRD_Core.Instance;
                Graph<Guid> graph = core.BuildSkillsGraphSnapshot();

                Program.Assert(graph.ContainsNode(fixture.WordNodeId), "Snapshot should contain trusted node IDs.");
                Program.Assert(graph.EdgeCount > 0, "Snapshot should contain structural links.");

                GraphTraversal<Guid> traversal = GraphAlgorithms.BreadthFirstTraversal(graph, fixture.SentenceNodeId);
                Program.Assert(traversal.Order.Contains(fixture.SenseNodeId), "Traversal should reach the sense node.");
                Program.Assert(traversal.Order.Contains(fixture.WordNodeId), "Traversal should reach the word node.");
                Program.Assert(traversal.Order.Contains(fixture.SymbolNodeId), "Traversal should reach the symbol node.");

                ShortestPathResult<Guid> shortestPath = GraphAlgorithms.Dijkstra(graph, fixture.SentenceNodeId);
                Program.Assert(shortestPath.TryGetDistance(fixture.SymbolNodeId, out double distance), "Dijkstra should reach the symbol node.");
                Program.AssertEqual(distance, 2.0, "Shortest sentence-to-symbol path should pass through the word.");

                IReadOnlyDictionary<Guid, double> ranks = GraphAlgorithms.PageRank(graph);
                Program.Assert(ranks.Count == graph.NodeCount, "PageRank should cover every graph node.");

                Graph<Guid> senseOnly = core.BuildSkillsGraphSnapshot(NodeRelationType.HasSense);
                Program.Assert(senseOnly.EdgeCount > 0 && senseOnly.EdgeCount < graph.EdgeCount, "Filtered snapshots should include only selected relation types.");
            };
        }

        private static Action SubgraphIdentityCollisionsAreAtomic()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid symbolId = AddTrustedSymbol(core, senseId, 'c');
                List<Guid> symbolIds = new List<Guid> { symbolId };
                WordBox first = new WordBox(Guid.NewGuid(), senseId, usageId, symbolIds, PartOfSpeech.Noun);
                WordBox duplicateIdentity = new WordBox(Guid.NewGuid(), senseId, usageId, symbolIds, PartOfSpeech.Noun);
                SentenceBox sentence = new SentenceBox(Guid.NewGuid(), senseId,
                    new List<Guid> { first.GetWordID(), duplicateIdentity.GetWordID() });

                Program.Assert(core.SetUntrustedWord(first), "First word should be staged.");
                Program.Assert(core.SetUntrustedWord(duplicateIdentity), "Identity-colliding word should be staged.");
                Program.Assert(core.SetUntrustedSentence(sentence), "Sentence should be staged.");
                Program.Assert(!core.ConvertUntrustedSentenceSubgraphToTrusted(sentence.GetSentenceID()),
                    "Batch-local duplicate word identities must reject promotion.");
                Program.Assert(core.GetTrustedWordBox(first.GetWordID()) == null &&
                    core.GetTrustedWordBox(duplicateIdentity.GetWordID()) == null,
                    "Rejected batch must not partially promote words.");
                Program.Assert(core.GetTrustedSentenceBox(sentence.GetSentenceID()) == null,
                    "Rejected batch must leave the sentence untrusted.");
                Program.Assert(core.GetUntrustedWordBox(first.GetWordID()) == first &&
                    core.GetUntrustedWordBox(duplicateIdentity.GetWordID()) == duplicateIdentity,
                    "Rejected batch must retain all untrusted words.");

                ResetCore();
                core = LRD_Core.Instance;
                senseId = AddSense(core);
                usageId = AddUsage(core);
                Guid firstSymbolId = Guid.NewGuid();
                Guid duplicateSymbolId = Guid.NewGuid();
                SymbolBox firstSymbol = new SymbolBox(firstSymbolId, 'x');
                firstSymbol.SetSenseID(senseId);
                SymbolBox duplicateSymbol = new SymbolBox(duplicateSymbolId, 'x');
                duplicateSymbol.SetSenseID(senseId);
                WordBox firstSymbolWord = new WordBox(Guid.NewGuid(), senseId, usageId,
                    new List<Guid> { firstSymbolId }, PartOfSpeech.Noun);
                WordBox secondSymbolWord = new WordBox(Guid.NewGuid(), senseId, usageId,
                    new List<Guid> { duplicateSymbolId }, PartOfSpeech.Verb);
                SentenceBox symbolCollisionSentence = new SentenceBox(Guid.NewGuid(), senseId,
                    new List<Guid> { firstSymbolWord.GetWordID(), secondSymbolWord.GetWordID() });
                Program.Assert(core.SetUntrustedSymbol(firstSymbol) && core.SetUntrustedSymbol(duplicateSymbol),
                    "Both symbols should be staged.");
                Program.Assert(core.SetUntrustedWord(firstSymbolWord) && core.SetUntrustedWord(secondSymbolWord),
                    "Both dependent words should be staged.");
                Program.Assert(core.SetUntrustedSentence(symbolCollisionSentence), "Symbol-collision sentence should be staged.");
                Program.Assert(!core.ConvertUntrustedSentenceSubgraphToTrusted(symbolCollisionSentence.GetSentenceID()),
                    "Batch-local duplicate symbol identities must reject promotion.");
                Program.Assert(core.GetTrustedSymbolBox(firstSymbolId) == null &&
                    core.GetTrustedSymbolBox(duplicateSymbolId) == null &&
                    core.GetTrustedWordBox(firstSymbolWord.GetWordID()) == null,
                    "Symbol collision must leave the entire subgraph unpromoted.");

                Node orphanNode = new Node(NodeTargetKind.Symbol, Guid.NewGuid());
                Program.Assert(core.AddUntrustedNode(orphanNode), "Orphan node should be accepted as untrusted.");
                Program.Assert(!core.ConvertUntrustedNodeToTrusted(orphanNode.GetNodeID()),
                    "Node promotion must reject an absent trusted target.");
                Program.Assert(core.GetUntrustedNode(orphanNode.GetNodeID()) == orphanNode,
                    "Rejected node promotion must preserve its untrusted entry.");
            };
        }

        private static Action TrustedMembershipAndLinkLifecycleAreHardened()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = AddSense(core);
                Guid usageId = AddUsage(core);
                Guid symbolId = AddTrustedSymbol(core, senseId, 'g');
                Guid wordId = AddTrustedWord(core, senseId, usageId, symbolId);
                Guid sentenceId = AddTrustedSentence(core, senseId, wordId);
                Guid secondSymbolId = AddTrustedSymbol(core, senseId, 'h');
                Guid secondWordId = AddTrustedWord(core, senseId, usageId, secondSymbolId);
                Guid secondSentenceId = AddTrustedSentence(core, senseId, secondWordId);
                GroupBox group = new GroupBox(Guid.NewGuid(), new List<Guid> { sentenceId });
                Program.Assert(core.AddTrustedGroup(group), "Trusted group should be registered.");
                Program.AssertThrows<InvalidOperationException>(() => group.AddSentence(secondSentenceId),
                    "Trusted group must reject direct membership mutation.");
                Program.Assert(core.AddSentenceToTrustedGroup(group.GetGroupingID(), secondSentenceId),
                    "Core should allow a validated trusted membership update.");
                AssertContains(core.GetTrustedGroupsContainingSentence(secondSentenceId), group.GetGroupingID(),
                    "Group-by-sentence reverse index should be updated.");
                Guid groupNodeId = NodeIdFor(core, NodeTargetKind.Group, group.GetGroupingID());
                AssertContainsLink(core.GetOutgoingLinks(groupNodeId, NodeRelationType.ContainsSentence),
                    NodeIdFor(core, NodeTargetKind.Sentence, secondSentenceId),
                    "Core-mediated group membership should wire a structural link.");

                GroupBox secondGroup = new GroupBox(Guid.NewGuid(), new List<Guid> { sentenceId });
                Program.Assert(core.AddTrustedGroup(secondGroup), "Second group should be trusted.");
                SourceBox source = new SourceBox(Guid.NewGuid(), "source", new List<Guid> { group.GetGroupingID() });
                Program.Assert(core.AddTrustedSource(source), "Source should be trusted.");
                Program.AssertThrows<InvalidOperationException>(() => source.AddGrouping(secondGroup.GetGroupingID()),
                    "Trusted source must reject direct membership mutation.");
                Program.Assert(core.AddGroupToTrustedSource(source.GetSourceID(), secondGroup.GetGroupingID()),
                    "Core should allow a validated source membership update.");
                AssertContains(core.GetTrustedSourcesContainingGroup(secondGroup.GetGroupingID()), source.GetSourceID(),
                    "Source-by-group reverse index should be updated.");
                AssertContainsLink(core.GetOutgoingLinks(NodeIdFor(core, NodeTargetKind.Source, source.GetSourceID()),
                    NodeRelationType.ContainsGroup), NodeIdFor(core, NodeTargetKind.Group, secondGroup.GetGroupingID()),
                    "Core-mediated source membership should wire a structural link.");

                LexicalFixture fixture = BuildLexicalChain();
                core = LRD_Core.Instance;
                Guid from = fixture.WordNodeId;
                Guid to = fixture.SenseNodeId;
                Program.Assert(core.AddTrustedLink(from, to, NodeRelationType.Custom, 2.0, "context-a"),
                    "First qualified custom link should be added.");
                Program.Assert(core.AddTrustedLink(from, to, NodeRelationType.Custom, 4.0, "context-b"),
                    "Different labels should allow parallel qualified links.");
                Program.Assert(!core.AddTrustedLink(from, to, NodeRelationType.Custom, 5.0, " context-a "),
                    "Normalized exact duplicate should be rejected.");
                Program.Assert(core.UpdateTrustedLinkWeight(from, to, NodeRelationType.Custom, 3.5, "context-a"),
                    "Existing qualified link weight should update.");
                Program.AssertEqual(core.GetOutgoingLinks(from, NodeRelationType.Custom)
                    .Single(link => link.Label == "context-a").Weight, 3.5,
                    "Adjacency should expose updated link weight.");
                Graph<Guid> updatedGraph = core.BuildSkillsGraphSnapshot();
                Program.Assert(updatedGraph.GetOutgoingEdges(from).Any(edge => edge.To == to && edge.Weight == 3.5),
                    "Skills graph should reflect updated link weight.");
                Program.Assert(core.RemoveTrustedLink(from, to, NodeRelationType.Custom, "context-b"),
                    "Qualified link should be removable by label.");
                Program.Assert(core.GetOutgoingLinks(from, NodeRelationType.Custom).Count == 1,
                    "Removal should keep adjacency synchronized.");
                Program.Assert(!core.RemoveTrustedLink(from, to, NodeRelationType.Custom, "missing"),
                    "Removing an absent qualified link should return false.");

                Guid alternateSymbolId = AddTrustedSymbol(core, fixture.SenseId, 'p');
                Guid alternateWordId = AddTrustedWord(core, fixture.SenseId, fixture.UsageId, alternateSymbolId);
                Guid alternateWordNodeId = NodeIdFor(core, NodeTargetKind.Word, alternateWordId);
                foreach (NodeRelationType symmetricType in new[] { NodeRelationType.Synonym, NodeRelationType.Antonym })
                {
                    Program.Assert(core.AddTrustedLink(from, alternateWordNodeId, symmetricType, 1.75, "pair"),
                        $"{symmetricType} edge should be accepted.");
                    NodeLink forward = core.GetOutgoingLinks(from, symmetricType).Single(link =>
                        link.TargetNodeId == alternateWordNodeId && link.Label == "pair");
                    NodeLink reverse = core.GetOutgoingLinks(alternateWordNodeId, symmetricType).Single(link =>
                        link.TargetNodeId == from && link.Label == "pair");
                    Program.Assert(!core.AddTrustedLink(alternateWordNodeId, from, symmetricType, 1.75, "pair"),
                        $"Explicitly adding the stored reverse {symmetricType} edge should be a duplicate.");
                    Program.AssertEqual(forward.Weight, reverse.Weight,
                        $"Both directed {symmetricType} links should have the same weight.");
                    Program.Assert(core.UpdateTrustedLinkWeight(from, alternateWordNodeId, symmetricType, 2.5, "pair"),
                        $"Updating {symmetricType} should update both directions.");
                    Program.AssertEqual(core.GetOutgoingLinks(alternateWordNodeId, symmetricType)
                        .Single(link => link.TargetNodeId == from && link.Label == "pair").Weight, 2.5,
                        $"Reverse {symmetricType} weight should stay synchronized.");
                    Program.Assert(core.RemoveTrustedLink(from, alternateWordNodeId, symmetricType, "pair"),
                        $"Removing {symmetricType} should remove both directions.");
                    Program.Assert(!core.GetOutgoingLinks(from, symmetricType).Any(link => link.TargetNodeId == alternateWordNodeId && link.Label == "pair") &&
                        !core.GetOutgoingLinks(alternateWordNodeId, symmetricType).Any(link => link.TargetNodeId == from && link.Label == "pair"),
                        $"Both directed {symmetricType} links should be removed together.");
                }
                Program.AssertThrows<ArgumentOutOfRangeException>(() =>
                    core.AddTrustedLink(from, to, (NodeRelationType)999), "Undefined link relation should be rejected.");
            };
        }

        private static Action ConversationAndFunctionApisAreHardened()
        {
            return () =>
            {
                Guid conversationId = Guid.NewGuid();
                Program.Assert(new ConversationText(conversationId).GetConversationID() == conversationId,
                    "ConversationText should preserve an explicit ID.");
                Program.Assert(new ConversationSymbolic(conversationId).GetConversationID() == conversationId,
                    "ConversationSymbolic should preserve an explicit ID.");
                Program.AssertThrows<ArgumentException>(() => new ConversationText(Guid.Empty),
                    "ConversationText must reject Guid.Empty IDs.");
                Program.AssertThrows<ArgumentException>(() => new ConversationSymbolic(Guid.Empty),
                    "ConversationSymbolic must reject Guid.Empty IDs.");
                Program.AssertThrows<ArgumentException>(() =>
                    new ConversationSymbolic(new Dictionary<UInt128, List<Guid>>
                    {
                        [0] = new List<Guid> { Guid.Empty }
                    }), "ConversationSymbolic must reject empty symbol IDs.");
                ConversationSymbolic symbolic = new ConversationSymbolic();
                Program.AssertThrows<ArgumentException>(() => symbolic.AddSymbolic(new List<Guid> { Guid.Empty }),
                    "Added symbolic sequences must reject empty symbol IDs.");

                FunctionBox function = new FunctionBox(typeof(double));
                Program.Assert(function.FunctionID == function.GetFunctionID(),
                    "FunctionBox.FunctionID should expose its stable identifier.");
                function.AddVariable(new FunctionVariable("x", 1.0, typeof(double)));
                IReadOnlyList<FunctionVariable> snapshot = function.Variables;
                function.AddVariable(new FunctionVariable("y", 2.0, typeof(double)));
                Program.Assert(snapshot.Count == 1 && function.Variables.Count == 2,
                    "Variables should return an isolated snapshot.");
            };
        }

        private static LexicalFixture BuildLexicalChain()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            Guid senseId = AddSense(core);
            Guid usageId = AddUsage(core);
            Guid symbolId = AddTrustedSymbol(core, senseId, 'n');
            Guid wordId = AddTrustedWord(core, senseId, usageId, symbolId);
            Guid sentenceId = AddTrustedSentence(core, senseId, wordId);

            return new LexicalFixture(
                senseId,
                usageId,
                symbolId,
                wordId,
                sentenceId,
                NodeIdFor(core, NodeTargetKind.Sense, senseId),
                NodeIdFor(core, NodeTargetKind.Usage, usageId),
                NodeIdFor(core, NodeTargetKind.Symbol, symbolId),
                NodeIdFor(core, NodeTargetKind.Word, wordId),
                NodeIdFor(core, NodeTargetKind.Sentence, sentenceId));
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
            Guid symbolId = Guid.NewGuid();
            SymbolBox symbol = new SymbolBox(symbolId, value);
            symbol.SetSenseID(senseId);
            Program.Assert(core.AddTrustedSymbol(symbol), "Test symbol should be registered.");
            return symbolId;
        }

        private static Guid AddTrustedWord(LRD_Core core, Guid senseId, Guid usageId, Guid symbolId)
        {
            Guid wordId = Guid.NewGuid();
            WordBox word = new WordBox(wordId, senseId, usageId, new List<Guid> { symbolId }, PartOfSpeech.Noun);
            Program.Assert(core.AddTrustedWord(word), "Test word should be registered.");
            return wordId;
        }

        private static Guid AddTrustedSentence(LRD_Core core, Guid senseId, Guid wordId)
        {
            Guid sentenceId = Guid.NewGuid();
            SentenceBox sentence = new SentenceBox(sentenceId, senseId, new List<Guid> { wordId });
            Program.Assert(core.AddTrustedSentence(sentence), "Test sentence should be registered.");
            return sentenceId;
        }

        private static Guid NodeIdFor(LRD_Core core, NodeTargetKind kind, Guid targetId)
        {
            Node? node = core.GetTrustedNodeByTarget(kind, targetId);
            Program.Assert(node != null, "Expected trusted node was not created.");
            return node!.GetNodeID();
        }

        private static void AssertContains(List<Guid> values, Guid expected, string message)
        {
            Program.Assert(values.Contains(expected), message);
        }

        private static void AssertContainsLink(List<NodeLink> links, Guid expectedTargetNodeId, string message)
        {
            Program.Assert(links.Any(link => link.TargetNodeId == expectedTargetNodeId), message);
        }

        private static void ResetCore()
        {
            LRD_Core.Instance.ResetCore();
        }
    }
}
