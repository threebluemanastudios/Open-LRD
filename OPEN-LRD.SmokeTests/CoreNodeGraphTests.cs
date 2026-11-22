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
            Program.Run("Node presentations follow trusted and untrusted Node lifecycles", NodePresentationsFollowNodeLifecycle());
            Program.Run("GroupBox stores sentence IDs and snapshots", GroupBoxStoresSentenceIdsAndSnapshots());
            Program.Run("Trusted reverse and containment indexes", ReverseIndexesTrackTrustedIdentities());
            Program.Run("Trusted boxes create nodes and structural links", TrustedBoxesCreateNodesAndStructuralLinks());
            Program.Run("Untrusted node promotion preserves node identity", UntrustedNodePromotionPreservesNodeIdentity());
            Program.Run("Sentence subgraph promotion is atomic", SentenceSubgraphPromotionPromotesDependencies());
            Program.Run("Skills graph snapshots support graph algorithms", SkillsGraphSnapshotsSupportGraphAlgorithms());
            Program.Run("Subgraph identity collisions do not partially promote", SubgraphIdentityCollisionsAreAtomic());
            Program.Run("Trusted graph membership and link lifecycle are hardened", TrustedMembershipAndLinkLifecycleAreHardened());
            Program.Run("Reasoning links support provenance and validity queries", ReasoningLinksSupportProvenanceAndTimeQueries());
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

        private static Action NodePresentationsFollowNodeLifecycle()
        {
            return () =>
            {
                ResetCore();
                LRD_Core core = LRD_Core.Instance;

                Guid orphanTargetId = Guid.NewGuid();
                Node orphan = new Node(NodeTargetKind.Symbol, orphanTargetId);
                Program.Assert(core.SetUntrustedNode(orphan), "Untrusted node should register.");
                UInt128? firstPresentationId = core.GetNodePresentationId(orphan.GetNodeID());
                NodeRepresentation? orphanRepresentation = firstPresentationId.HasValue
                    ? core.GetNodeRepresentation(firstPresentationId.Value)
                    : null;
                Program.Assert(firstPresentationId == UInt128.Zero &&
                    orphanRepresentation?.GetNodeID() == orphan.GetNodeID(),
                    "First registered node should receive sequential presentation ID zero and link to its Node.");
                Program.Assert(orphanRepresentation!.GetSize() >= 0.6 && orphanRepresentation.GetSize() <= 2.0 &&
                    orphanRepresentation.GetPosition().IsFinite &&
                    orphanRepresentation.GetTextLabel()!.StartsWith("Symbol ", StringComparison.Ordinal),
                    "Derived presentation fields should be initialized for an untrusted node.");
                Program.Assert(core.GetNodePresentationCount() == 1,
                    "Each registered untrusted node should have exactly one presentation.");
                IReadOnlyDictionary<UInt128, NodeRepresentation> presentationSnapshot = core.GetNodePresentations();
                Program.Assert(core.RemoveUntrustedNode(orphan.GetNodeID()), "Untrusted node should be removable.");
                Program.Assert(core.GetNodeRepresentationByNodeId(orphan.GetNodeID()) == null &&
                    core.GetNodePresentationCount() == 0 && presentationSnapshot.Count == 1,
                    "Removing an untrusted node should remove its presentation while returned snapshots remain isolated.");

                Guid senseId = AddSense(core);
                Guid senseNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Sense, senseId)!.GetNodeID();
                Program.Assert(core.GetNodePresentationId(senseNodeId) == (UInt128)1,
                    "Sequential presentation IDs should advance without reusing removed IDs.");
                Guid symbolId = Guid.NewGuid();
                Node promotable = new Node(NodeTargetKind.Symbol, symbolId);
                Program.Assert(core.SetUntrustedNode(promotable), "Promotable node should be staged.");
                NodeRepresentation representation = core.GetNodeRepresentationByNodeId(promotable.GetNodeID())!;
                UInt128 presentationId = core.GetNodePresentationId(promotable.GetNodeID())!.Value;
                Program.Assert(presentationId == (UInt128)2, "Each new Node should receive the next sequential presentation ID.");
                Program.Assert(core.UpdateNodeRepresentation(
                    promotable.GetNodeID(), new Vector3(1.25, -2.5, 3.75), 2.5, 0xFF123456, "custom display"),
                    "Core presentation update API should update a registered NodeRepresentation.");

                SymbolBox symbol = new SymbolBox(symbolId, 'p');
                symbol.SetSenseID(senseId);
                Program.Assert(core.AddTrustedSymbol(symbol), "Trusted symbol should promote its matching untrusted node.");
                Node trustedNode = core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId)!;
                NodeRepresentation promotedRepresentation = core.GetNodeRepresentationByNodeId(trustedNode.GetNodeID())!;
                Program.Assert(trustedNode.GetNodeID() == promotable.GetNodeID() &&
                    core.GetNodePresentationId(trustedNode.GetNodeID()) == presentationId &&
                    ReferenceEquals(promotedRepresentation, representation),
                    "Node promotion should preserve the presentation ID and object.");
                Program.Assert(promotedRepresentation.GetColor() != 0xFF123456 &&
                    promotedRepresentation.GetSize() != 2.5 &&
                    promotedRepresentation.GetTextLabel()!.EndsWith("[HasSense]", StringComparison.Ordinal) &&
                    promotedRepresentation.GetPosition() != new Vector3(1.25, -2.5, 3.75),
                    "Node promotion should recalculate derived presentation fields while preserving the representation object.");
                Program.Assert(core.GetNodePresentationCount() == core.GetTrustedNodeCount() + core.GetUntrustedNodeCount(),
                    "Presentation registry should contain exactly one record for every registered Node.");

                Guid relabelTargetId = Guid.NewGuid();
                Node relabelNode = new Node(NodeTargetKind.Word, relabelTargetId);
                Program.Assert(core.SetUntrustedNode(relabelNode), "Relabel test node should register.");
                NodeRepresentation relabelRepresentation = core.GetNodeRepresentationByNodeId(relabelNode.GetNodeID())!;
                relabelNode.SetLabel("draft label");
                Program.Assert(relabelRepresentation.GetTextLabel() == "draft label",
                    "Derived display labels should follow untrusted Node label changes.");
                relabelRepresentation.SetTextLabel("override");
                relabelNode.SetLabel("second draft");
                Program.Assert(relabelRepresentation.GetTextLabel() == "second draft",
                    "Derived display labels should remain authoritative after label changes.");
                Program.Assert(core.RemoveUntrustedNode(relabelNode.GetNodeID()), "Relabel test node should be removable.");

                Program.AssertThrows<ArgumentException>(
                    () => new NodeRepresentation(Guid.Empty, 0xFFFFFFFF),
                    "NodeRepresentation must reject Guid.Empty Node IDs.");
                Program.AssertThrows<ArgumentOutOfRangeException>(
                    () => new NodeRepresentation(Guid.NewGuid(), 0xFFFFFFFF, 0.0),
                    "NodeRepresentation must reject non-positive sizes.");
                Program.AssertThrows<ArgumentOutOfRangeException>(
                    () => new Vector3(double.NaN, 0.0, 0.0),
                    "NodeRepresentation position components must be finite.");
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
                NodeLink structuralSenseLink = core.GetOutgoingLinks(symbolNode.GetNodeID(), NodeRelationType.HasSense).Single();
                Program.Assert(structuralSenseLink.Provenance.Origin == NodeLinkOrigin.Structural,
                    "Automatically wired links should carry structural provenance.");
                Program.Assert(!core.AddTrustedLink(structuralSenseLink),
                    "External callers cannot spoof an automatically wired structural link.");
                Program.Assert(!core.UpdateTrustedLinkWeight(symbolNode.GetNodeID(), fixture.SenseNodeId,
                    NodeRelationType.HasSense, 0.5), "Structural link weights cannot be externally changed.");
                Program.Assert(!core.RemoveTrustedLink(symbolNode.GetNodeID(), fixture.SenseNodeId,
                    NodeRelationType.HasSense), "Structural links cannot be externally removed.");
                Program.Assert(!core.AddTrustedLink(symbolNode.GetNodeID(), fixture.UsageNodeId,
                    NodeRelationType.HasUsage), "Relation kinds must match the endpoint target kinds.");

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
                Program.Assert(core.AddTrustedLink(from, to, NodeRelationType.Custom, 0.2, "context-a"),
                    "First qualified custom link should be added.");
                Program.Assert(core.AddTrustedLink(from, to, NodeRelationType.Custom, 0.4, "context-b"),
                    "Different labels should allow parallel qualified links.");
                Program.Assert(!core.AddTrustedLink(from, to, NodeRelationType.Custom, 0.5, " context-a "),
                    "Normalized exact duplicate should be rejected.");
                Program.Assert(core.UpdateTrustedLinkWeight(from, to, NodeRelationType.Custom, 0.35, "context-a"),
                    "Existing qualified link weight should update.");
                Program.AssertEqual(core.GetOutgoingLinks(from, NodeRelationType.Custom)
                    .Single(link => link.Label == "context-a").Weight, 0.35,
                    "Adjacency should expose updated link weight.");
                Graph<Guid> updatedGraph = core.BuildSkillsGraphSnapshot();
                Program.Assert(updatedGraph.GetOutgoingEdges(from).Any(edge => edge.To == to && edge.Weight == 0.35),
                    "Skills graph should reflect updated link weight.");
                Program.Assert(core.RemoveTrustedLink(from, to, NodeRelationType.Custom, "context-b"),
                    "Qualified link should be removable by label.");
                Program.Assert(core.GetOutgoingLinks(from, NodeRelationType.Custom).Count == 1,
                    "Removal should keep adjacency synchronized.");
                Program.Assert(!core.RemoveTrustedLink(from, to, NodeRelationType.Custom, "missing"),
                    "Removing an absent qualified link should return false.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() =>
                    new NodeLink(from, to, NodeRelationType.Custom, 1.01),
                    "Link confidence must remain in [0, 1].");

                DateTimeOffset assertionTime = DateTimeOffset.UtcNow;
                NodeLinkProvenance assertionA = new NodeLinkProvenance(Guid.NewGuid(), NodeLinkOrigin.User,
                    assertionTime, "author-a", confidence: 0.6);
                NodeLinkProvenance assertionB = new NodeLinkProvenance(Guid.NewGuid(), NodeLinkOrigin.User,
                    assertionTime, "author-b", confidence: 0.6);
                Program.Assert(core.AddTrustedLink(from, to, NodeRelationType.Custom, 0.6, "shared-evidence", assertionA) &&
                               core.AddTrustedLink(from, to, NodeRelationType.Custom, 0.6, "shared-evidence", assertionB),
                    "Separate provenance assertion IDs should allow parallel evidence-backed links.");
                Program.Assert(!core.AddTrustedLink(from, to, NodeRelationType.Custom, 0.6, "shared-evidence", assertionA),
                    "An exact provenance assertion duplicate should be rejected.");
                Program.Assert(core.UpdateTrustedLinkWeight(assertionA.AssertionId, 0.7) &&
                               core.GetOutgoingLinks(from, NodeRelationType.Custom).Single(link =>
                                   link.Provenance.AssertionId == assertionA.AssertionId).Weight == 0.7,
                    "Weights should update by stable assertion ID without replacing other assertions.");
                Program.Assert(core.RemoveTrustedLink(assertionA.AssertionId) &&
                               core.GetOutgoingLinks(from, NodeRelationType.Custom).Any(link =>
                                   link.Provenance.AssertionId == assertionB.AssertionId),
                    "Removing one evidence assertion should preserve parallel assertions.");

                Guid alternateSymbolId = AddTrustedSymbol(core, fixture.SenseId, 'p');
                Guid alternateWordId = AddTrustedWord(core, fixture.SenseId, fixture.UsageId, alternateSymbolId);
                Guid alternateWordNodeId = NodeIdFor(core, NodeTargetKind.Word, alternateWordId);
                foreach (NodeRelationType symmetricType in new[] { NodeRelationType.Synonym, NodeRelationType.Antonym })
                {
                    Program.Assert(core.AddTrustedLink(from, alternateWordNodeId, symmetricType, 0.75, "pair"),
                        $"{symmetricType} edge should be accepted.");
                    NodeLink forward = core.GetOutgoingLinks(from, symmetricType).Single(link =>
                        link.TargetNodeId == alternateWordNodeId && link.Label == "pair");
                    NodeLink reverse = core.GetOutgoingLinks(alternateWordNodeId, symmetricType).Single(link =>
                        link.TargetNodeId == from && link.Label == "pair");
                    Program.Assert(!core.AddTrustedLink(alternateWordNodeId, from, symmetricType, 0.75, "pair"),
                        $"Explicitly adding the stored reverse {symmetricType} edge should be a duplicate.");
                    Program.AssertEqual(forward.Weight, reverse.Weight,
                        $"Both directed {symmetricType} links should have the same weight.");
                    Program.Assert(core.UpdateTrustedLinkWeight(from, alternateWordNodeId, symmetricType, 0.25, "pair"),
                        $"Updating {symmetricType} should update both directions.");
                    Program.AssertEqual(core.GetOutgoingLinks(alternateWordNodeId, symmetricType)
                        .Single(link => link.TargetNodeId == from && link.Label == "pair").Weight, 0.25,
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

        private static Action ReasoningLinksSupportProvenanceAndTimeQueries()
        {
            return () =>
            {
                LexicalFixture fixture = BuildLexicalChain();
                LRD_Core core = LRD_Core.Instance;
                Guid secondSymbolId = AddTrustedSymbol(core, fixture.SenseId, 'r');
                Guid secondWordId = AddTrustedWord(core, fixture.SenseId, fixture.UsageId, secondSymbolId);
                Guid firstNodeId = fixture.WordNodeId;
                Guid secondNodeId = NodeIdFor(core, NodeTargetKind.Word, secondWordId);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DateTimeOffset validFrom = now.AddMinutes(-5);
                DateTimeOffset validUntil = now.AddMinutes(5);

                Program.Assert(core.AddReasoningLink(firstNodeId, secondNodeId, NodeRelationType.Synonym,
                    0.82, "derived", "researcher", fixture.WordId, fixture.SenseId, validFrom, validUntil),
                    "Reasoning helper should add a trusted assertion with provenance and a validity interval.");
                Program.Assert(!core.AddReasoningLink(secondNodeId, firstNodeId, NodeRelationType.Synonym,
                    0.82, " derived ", " researcher ", fixture.WordId, fixture.SenseId, validFrom, validUntil),
                    "Reasoning assertion IDs should be stable across symmetric endpoint order and normalized text.");

                List<NodeLink> activeReasoningLinks = core.GetAllTrustedLinks(NodeRelationType.Synonym,
                    NodeLinkOrigin.Reasoning, " RESEARCHER ", 0.8, now);
                Program.Assert(activeReasoningLinks.Count == 2 && activeReasoningLinks.All(link =>
                    link.Provenance.IsActiveAt(now) && link.Provenance.IsActiveAt(validFrom) &&
                    link.Provenance.IsActiveAt(validUntil) && link.Provenance.Origin == NodeLinkOrigin.Reasoning),
                    "Provenance filters should return both active directed edges for a symmetric assertion.");
                Guid reasoningAssertionId = activeReasoningLinks[0].Provenance.AssertionId;
                Program.Assert(core.UpdateReasoningLinkWeight(reasoningAssertionId, 0.9) &&
                    core.GetAllTrustedLinks(NodeRelationType.Synonym, NodeLinkOrigin.Reasoning,
                        "researcher", 0.9, now).Count == 2,
                    "Reasoning confidence updates should preserve assertion identity and update both symmetric edges.");
                Program.Assert(core.GetAllTrustedLinks(NodeRelationType.Synonym, NodeLinkOrigin.Reasoning,
                    "researcher", 0.8, now.AddHours(1)).Count == 0,
                    "Validity windows should exclude assertions outside their interval.");
                Program.Assert(core.BuildSkillsGraphSnapshot(NodeRelationType.Synonym, NodeLinkOrigin.Reasoning,
                    "researcher", 0.8, now).EdgeCount == 2,
                    "Filtered graph snapshots should honor provenance and temporal filters.");
                Program.Assert(core.GetTrustedLinksByProvenanceTarget(fixture.SenseId,
                    includeSourceTarget: false).Count == 1,
                    "Reverse provenance lookup should return one assertion rather than both symmetric edges.");

                core.GetTrustedNode(firstNodeId)!.RegisterVerifiedReference(2);
                List<Node> matchingNodes = core.FindTrustedNodes(NodeTargetKind.Word, minimumNetTrustWeight: 1,
                    relationFilter: NodeRelationType.Synonym, minimumIncidentLinkConfidence: 0.8, canonicalOnly: true);
                Program.Assert(matchingNodes.Count == 1 && matchingNodes[0].GetNodeID() == firstNodeId,
                    "Node queries should combine target kind, canonical identity, trust, relation, and confidence filters.");
                Program.AssertThrows<ArgumentOutOfRangeException>(() =>
                    core.GetAllTrustedLinks(minimumConfidence: double.NaN),
                    "Link query confidence thresholds must be finite and in range.");
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
            LRD_Core.Instance.InitializeCore();
        }
    }
}
