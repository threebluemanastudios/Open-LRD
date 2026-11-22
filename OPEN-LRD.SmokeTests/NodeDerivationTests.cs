using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OPEN_LRD.Foundations;
using OPEN_LRD.Persistence;

namespace OPEN_LRD.SmokeTests
{
    internal static class NodeDerivationTests
    {
        internal static void RunAll()
        {
            Program.Run("Derived presentation fields use link graph and reference evidence", DerivedFieldsUseLinkGraphAndReferenceEvidence);
            Program.Run("Derived class uses dominant relation and enum tie-break", DerivedClassUsesDominantRelationAndEnumTieBreak);
            Program.Run("Weight and trust formulas use normalized incident evidence", WeightAndTrustFormulasUseIncidentEvidence);
            Program.Run("Layout options validate biases and change positions", LayoutOptionsValidateAndChangePositions);
            Program.Run("Layout mirrors trust spheres and keeps spheres disjoint", LayoutMirrorsTrustSpheresAndKeepsSpheresDisjoint);
            Program.Run("Layout counts distinct internal assertions and neighbors", LayoutCountsDistinctInternalAssertionsAndNeighbors);
            Program.Run("Function nodes use split trust spokes", FunctionNodesUseSplitTrustSpokes);
            Program.Run("All target kinds have mirrored trust spokes", AllTargetKindsHaveMirroredTrustSpokes);
            Program.Run("Untrusted target summaries refresh transitively", UntrustedTargetSummariesRefreshTransitively);
            Program.Run("Non-lexical untrusted summaries use target data", NonLexicalUntrustedSummariesUseTargetData);
            Program.Run("Container link mutations recalculate derived presentations", ContainerLinkMutationsRecalculatePresentations);
            Program.Run("Link mutations keep stored positions synchronized", LinkMutationsKeepStoredPositionsSynchronized);
            Program.Run("Layout is deterministic and virtual around the origin", LayoutIsDeterministicAndVirtualAroundOrigin);
            Program.Run("Layout spacing and link clearance remain deterministic", LayoutSpacingAndLinkClearanceRemainDeterministic);
            Program.Run("Layout settings payloads version and read legacy records", LayoutSettingsPayloadsVersionAndReadLegacyRecords);
            Program.Run("Untrusted reference changes recalculate derived color", UntrustedReferenceChangesRecalculateColor);
        }

        private static void DerivedFieldsUseLinkGraphAndReferenceEvidence()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            Guid senseId = Guid.NewGuid();
            Guid usageId = Guid.NewGuid();
            AddSense(core, senseId);
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");

            Guid firstSymbolId = Guid.NewGuid();
            AddSymbol(core, firstSymbolId, senseId, 'a');
            Guid firstWordId = AddWord(core, senseId, usageId, firstSymbolId);
            Guid secondSymbolId = Guid.NewGuid();
            AddSymbol(core, secondSymbolId, senseId, 'b');
            Guid secondWordId = AddWord(core, senseId, usageId, secondSymbolId);

            Guid firstNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, firstWordId)!.GetNodeID();
            Guid secondNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, secondWordId)!.GetNodeID();
            Program.Assert(core.AddTrustedLink(firstNodeId, secondNodeId, NodeRelationType.Synonym, 0.4),
                "Weighted user link should be accepted.");

            NodeRepresentation representation = core.GetNodeRepresentationByNodeId(firstNodeId)!;
            Program.Assert(representation.GetTextLabel()!.EndsWith("[HasSense]", StringComparison.Ordinal),
                "Dominant incident relation should be appended to the display label.");
            Program.Assert(!representation.GetTextLabel()!.Contains("Weight", StringComparison.Ordinal) &&
                           !representation.GetTextLabel()!.Contains("Trust", StringComparison.Ordinal),
                "Display labels should not include numeric metric text.");
            Program.Assert(Math.Abs(representation.GetSize() - 1.7655) < 1e-9,
                "Derived size should use link confidence and distinct neighbor count.");
            Program.Assert(representation.GetColor() != 0xFFBAB0AC, "Derived color should not be the fallback color.");
            Program.Assert(representation.GetPosition().IsFinite, "Derived position should be finite.");
        }

        private static void DerivedClassUsesDominantRelationAndEnumTieBreak()
        {
            NodeDerivationLink[] links =
            {
                Link(NodeRelationType.HasSense, 1.0),
                Link(NodeRelationType.HasSense, 1.0),
                Link(NodeRelationType.HasUsage, 1.0),
                Link(NodeRelationType.ContainsSymbol, 1.0),
                Link(NodeRelationType.Synonym, 0.5)
            };
            Program.Assert(NodePresentationCalculator.CalculateClass(links) == NodeRelationType.HasSense,
                "Most frequent relation should determine the derived class.");

            NodeDerivationLink[] tied =
            {
                Link(NodeRelationType.ContainsSymbol, 0.0),
                Link(NodeRelationType.HasUsage, 0.0),
                Link(NodeRelationType.HasSense, 0.0)
            };
            Program.Assert(NodePresentationCalculator.CalculateClass(tied) == NodeRelationType.HasSense,
                "Class ties should break by enum order.");
            Program.Assert(NodePresentationCalculator.CalculateClass(Array.Empty<NodeDerivationLink>()) == null,
                "Isolated nodes should not have a derived class.");
        }

        private static void WeightAndTrustFormulasUseIncidentEvidence()
        {
            NodeDerivationLink[] links = { Link(NodeRelationType.Synonym, 0.2), Link(NodeRelationType.Synonym, 0.8) };
            Program.AssertEqual(NodePresentationCalculator.CalculateWeight(links), 0.5,
                "Weight should average distinct incident link confidences.");
            Program.AssertEqual(NodePresentationCalculator.CalculateWeight(Array.Empty<NodeDerivationLink>()), 0.0,
                "Isolated nodes should have zero weight.");

            Program.AssertEqual(NodePresentationCalculator.CalculateTrustRating(2, 1, 1.0, true), 0.8,
                "Trust should combine verified references and twice the incident confidence.");
            Program.AssertEqual(NodePresentationCalculator.CalculateTrustRating(0, 0, 0.0, true), 1.0,
                "Trusted nodes with no evidence should use the trusted fallback.");
            Program.AssertEqual(NodePresentationCalculator.CalculateTrustRating(0, 0, 0.0, false), 0.0,
                "Untrusted nodes with no evidence should use the untrusted fallback.");
            Program.AssertEqual(NodePresentationCalculator.CalculateTrustRating(int.MaxValue, int.MaxValue, 0.0, true), 0.5,
                "Trust calculation should avoid integer overflow when both reference counters are maximal.");
            Program.AssertEqual(NodePresentationCalculator.CalculateTrustRating(int.MaxValue, 0, 0.0, false), 1.0,
                "A maximal verified-reference count should still produce full trust.");
            Program.AssertThrows<ArgumentOutOfRangeException>(
                () => NodePresentationCalculator.CalculateTrustRating(-1, 0, 0.0, true),
                "Negative verified-reference counts should be rejected.");
        }

        private static void LayoutOptionsValidateAndChangePositions()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            BuildSmallGraph(core);
            NodeLayoutOptions defaults = core.GetNodeLayoutOptions();
            Program.Assert(defaults.NodeWeightBias == NodeLayoutOptions.DefaultBias &&
                           defaults.NeighborPositionBias == NodeLayoutOptions.DefaultBias,
                "Layout biases should default to 0.2.");

            Guid wordNodeId = core.GetTrustedNodeIds(NodeTargetKind.Word).First();
            Vector3 originalPosition = core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition();
            core.UpdateNodeLayoutOptions(0.0, 0.0);
            Vector3 inwardPosition = core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition();
            core.UpdateNodeLayoutOptions(0.5, 0.5);
            Vector3 neighborPosition = core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition();
            Program.Assert(originalPosition != inwardPosition || inwardPosition != neighborPosition,
                "Changing layout biases should recalculate positions.");
            Program.Assert(core.GetNodeLayoutOptions().NodeWeightBias == 0.5 &&
                           core.GetNodeLayoutOptions().NeighborPositionBias == 0.5,
                "Updated layout options should be readable.");

            Program.AssertThrows<ArgumentOutOfRangeException>(() => core.UpdateNodeLayoutOptions(double.NaN, 0.2),
                "NaN layout bias should be rejected.");
            Program.AssertThrows<ArgumentOutOfRangeException>(() => core.UpdateNodeLayoutOptions(-0.1, 0.2),
                "Negative layout bias should be rejected.");
            Program.AssertThrows<ArgumentOutOfRangeException>(() => core.UpdateNodeLayoutOptions(0.2, 0.51),
                "Layout bias above 0.5 should be rejected.");
            Program.AssertThrows<ArgumentOutOfRangeException>(() => core.UpdateNodeLayoutOptions(double.PositiveInfinity, 0.2),
                "Positive infinity should be rejected as a layout bias.");
            Program.AssertThrows<ArgumentOutOfRangeException>(() => core.UpdateNodeLayoutOptions(0.2, double.NegativeInfinity),
                "Negative infinity should be rejected as a layout bias.");
        }

        private static void LayoutMirrorsTrustSpheresAndKeepsSpheresDisjoint()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            BuildSmallGraph(core);
            Program.Assert(core.SetUntrustedNode(new Node(NodeTargetKind.Symbol, Guid.NewGuid())),
                "Untrusted symbol should be registered.");
            Program.Assert(core.SetUntrustedNode(new Node(NodeTargetKind.Word, Guid.NewGuid())),
                "Untrusted word should be registered.");

            int nodeCountBefore = core.GetTrustedNodeCount() + core.GetUntrustedNodeCount();
            int presentationCountBefore = core.GetNodePresentationCount();
            int edgeCountBefore = core.BuildSkillsGraphSnapshot().EdgeCount;
            NodeLayoutResult layout = core.GetNodeLayoutResult();
            NodeLayoutBucket trusted = layout.Buckets.Single(bucket => bucket.Key == NodeLayoutCalculator.BucketKey(NodeTargetKind.Word, true));
            NodeLayoutBucket untrusted = layout.Buckets.Single(bucket => bucket.Key == NodeLayoutCalculator.BucketKey(NodeTargetKind.Word, false));
            Program.Assert(Math.Abs(trusted.Center.X + untrusted.Center.X) < 1e-9 &&
                           Math.Abs(trusted.Center.Y - untrusted.Center.Y) < 1e-9 &&
                           Math.Abs(trusted.Center.Z - untrusted.Center.Z) < 1e-9,
                "Trusted and untrusted sphere centers should mirror across the YZ plane.");
            Program.Assert(trusted.Center.X > 0.0 && untrusted.Center.X < 0.0,
                "Trusted spheres should sit on +X and untrusted spheres on -X.");
            Program.Assert(layout.CoreAnchor == Vector3.Zero,
                "The virtual layout anchor should be the origin.");

            for (int i = 0; i < layout.Buckets.Count; i++)
            {
                for (int j = i + 1; j < layout.Buckets.Count; j++)
                {
                    NodeLayoutBucket first = layout.Buckets[i];
                    NodeLayoutBucket second = layout.Buckets[j];
                    double required = first.Radius + second.Radius;
                    Program.Assert(first.Center.Distance(second.Center) >= required - 1e-9,
                        $"Spheres {first.Key} and {second.Key} should not overlap.");
                }
            }

            foreach (KeyValuePair<Guid, Vector3> entry in layout.Positions)
            {
                NodeLayoutBucket bucket = layout.Buckets.Single(bucket => bucket.Key == layout.NodeBuckets[entry.Key]);
                Program.Assert(entry.Value.IsFinite &&
                               entry.Value.Distance(bucket.Center) <= bucket.Radius + 1e-9,
                    "Node positions should be finite and remain inside their storage sphere.");
            }

            Program.Assert(core.GetTrustedNodeCount() + core.GetUntrustedNodeCount() == nodeCountBefore &&
                           core.GetNodePresentationCount() == presentationCountBefore &&
                           core.BuildSkillsGraphSnapshot().EdgeCount == edgeCountBefore &&
                           layout.Positions.Count == nodeCountBefore,
                "Calculating virtual layout must not create synthetic Nodes or presentations.");
        }

        private static void LayoutCountsDistinctInternalAssertionsAndNeighbors()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            Guid senseId = Guid.NewGuid();
            Guid usageId = Guid.NewGuid();
            AddSense(core, senseId);
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");
            Guid firstSymbolId = Guid.NewGuid();
            Guid secondSymbolId = Guid.NewGuid();
            AddSymbol(core, firstSymbolId, senseId, 'a');
            AddSymbol(core, secondSymbolId, senseId, 'b');
            Guid firstWordId = AddWord(core, senseId, usageId, firstSymbolId);
            Guid secondWordId = AddWord(core, senseId, usageId, secondSymbolId);
            Guid firstNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, firstWordId)!.GetNodeID();
            Guid secondNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, secondWordId)!.GetNodeID();

            int wordNodeCount = core.GetNodeLayoutResult().NodeBuckets.Values.Count(key =>
                key == NodeLayoutCalculator.BucketKey(NodeTargetKind.Word, true));
            Program.Assert(core.AddTrustedLink(firstNodeId, secondNodeId, NodeRelationType.Synonym, 0.4),
                "Symmetric link assertion should be accepted.");
            NodeLayoutBucket afterSymmetric = GetBucket(core.GetNodeLayoutResult(), NodeTargetKind.Word, true);
            Program.Assert(afterSymmetric.Radius == NodeLayoutCalculator.CalculateRadius(wordNodeCount, 1),
                "A symmetric assertion should contribute once to its bucket radius.");

            Program.Assert(core.AddTrustedLink(firstNodeId, secondNodeId, NodeRelationType.Custom, 0.6),
                "A second distinct assertion between the same Nodes should be accepted.");
            NodeLayoutBucket afterSecondAssertion = GetBucket(core.GetNodeLayoutResult(), NodeTargetKind.Word, true);
            Program.Assert(afterSecondAssertion.Radius == NodeLayoutCalculator.CalculateRadius(wordNodeCount, 2),
                "Distinct assertions count separately even when they share endpoints.");

            NodeDerivationSnapshot first = core.GetNodeDerivationSnapshot(firstNodeId)!;
            NodeDerivationSnapshot second = core.GetNodeDerivationSnapshot(secondNodeId)!;
            Program.Assert(first.Links.Count(link => link.RelationType == NodeRelationType.Synonym) == 1 &&
                           second.Links.Count(link => link.RelationType == NodeRelationType.Synonym) == 1,
                "Incoming/outgoing reverse records should produce one incident assertion per endpoint.");
            Program.Assert(first.Neighbors.Count(id => id == secondNodeId) == 1 &&
                           second.Neighbors.Count(id => id == firstNodeId) == 1,
                "Multiple assertions to one adjacent Node should produce one distinct neighbor.");
            ResetCore();
        }

        private static void FunctionNodesUseSplitTrustSpokes()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            FunctionBox trustedFunction = new FunctionBox(Guid.NewGuid(), typeof(double),
                "Math_2.CalculateCircleArea", "m²");
            trustedFunction.AddVariable(new FunctionVariable("x", 2.0, typeof(double), "m"));
            trustedFunction.StoreResult(new FunctionResult(Math.PI * 4.0, typeof(double), "m²"));
            Program.Assert(core.AddTrustedFunction(trustedFunction), "Trusted Function should be registered.");
            FunctionBox untrustedFunction = new FunctionBox(Guid.NewGuid(), typeof(double), "untrusted-test-function");
            Program.Assert(core.SetUntrustedFunction(untrustedFunction), "Untrusted Function should be registered.");

            NodeLayoutResult layout = core.GetNodeLayoutResult();
            NodeLayoutBucket trusted = GetBucket(layout, NodeTargetKind.Function, true);
            NodeLayoutBucket untrusted = GetBucket(layout, NodeTargetKind.Function, false);
            Program.Assert(trusted.Center.X > 0.0 && untrusted.Center.X < 0.0 &&
                           Math.Abs(trusted.Center.X + untrusted.Center.X) < 1e-9 &&
                           Math.Abs(trusted.Center.Y - untrusted.Center.Y) < 1e-9 &&
                           Math.Abs(trusted.Center.Z - untrusted.Center.Z) < 1e-9,
                "Function trust buckets should be mirrored across the YZ plane.");
            ResetCore();
        }

        private static void AllTargetKindsHaveMirroredTrustSpokes()
        {
            NodeTargetKind[] kinds = Enum.GetValues<NodeTargetKind>();
            Dictionary<Guid, NodeDerivationSnapshot> snapshots = new Dictionary<Guid, NodeDerivationSnapshot>();
            foreach (NodeTargetKind kind in kinds)
            {
                foreach (bool isTrusted in new[] { true, false })
                {
                    Guid nodeId = Guid.NewGuid();
                    snapshots.Add(nodeId, new NodeDerivationSnapshot(nodeId, Guid.NewGuid(), kind, isTrusted,
                        null, 0.0, isTrusted ? 1.0 : 0.0, kind.ToString(),
                        Array.Empty<NodeDerivationLink>(), Array.Empty<Guid>()));
                }
            }
            Dictionary<Guid, double> sizes = snapshots.ToDictionary(pair => pair.Key,
                pair => NodePresentationCalculator.CalculateSize(pair.Value));
            NodeLayoutResult layout = NodeLayoutCalculator.Calculate(snapshots, sizes, kinds, new NodeLayoutOptions());
            Dictionary<Guid, NodeDerivationSnapshot> reversedSnapshots = snapshots.Reverse()
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<Guid, double> reversedSizes = reversedSnapshots.ToDictionary(pair => pair.Key,
                pair => NodePresentationCalculator.CalculateSize(pair.Value));
            NodeLayoutResult reversedLayout = NodeLayoutCalculator.Calculate(
                reversedSnapshots, reversedSizes, kinds.Reverse().ToArray(), new NodeLayoutOptions());
            foreach (NodeTargetKind kind in kinds)
            {
                NodeLayoutBucket trusted = GetBucket(layout, kind, true);
                NodeLayoutBucket untrusted = GetBucket(layout, kind, false);
                Program.Assert(trusted.Center.X > 0.0 && untrusted.Center.X < 0.0 &&
                               Math.Abs(trusted.Center.X + untrusted.Center.X) < 1e-9 &&
                               Math.Abs(trusted.Center.Y - untrusted.Center.Y) < 1e-9 &&
                               Math.Abs(trusted.Center.Z - untrusted.Center.Z) < 1e-9,
                    $"Trusted/untrusted {kind} buckets should be mirrored across the YZ plane.");
            }
            Program.Assert(snapshots.Keys.All(id => layout.Positions[id] == reversedLayout.Positions[id]),
                "Spoke positions should not depend on registration or requested-kind order.");
        }

        private static void UntrustedTargetSummariesRefreshTransitively()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            Guid symbolId = Guid.NewGuid();
            Guid wordId = Guid.NewGuid();
            Guid sentenceId = Guid.NewGuid();
            Program.Assert(core.SetUntrustedSymbol(new SymbolBox(symbolId)),
                "Untrusted symbol should be registered.");
            Program.Assert(core.SetUntrustedSymbolChar(symbolId, 'a'), "Untrusted symbol character should be assigned.");
            Program.Assert(core.SetUntrustedWord(new WordBox(wordId, Guid.NewGuid(), Guid.NewGuid())),
                "Untrusted word should be registered.");
            Program.Assert(core.SetUntrustedWordSymbols(wordId, new List<Guid> { symbolId }),
                "Untrusted word symbols should be assignable.");
            Program.Assert(core.SetUntrustedSentence(new SentenceBox(sentenceId, Guid.NewGuid(), new List<Guid> { wordId })),
                "Untrusted sentence should be registered.");

            Node symbolNode = core.GetUntrustedNodeByTarget(NodeTargetKind.Symbol, symbolId)!;
            Node wordNode = core.GetUntrustedNodeByTarget(NodeTargetKind.Word, wordId)!;
            Node sentenceNode = core.GetUntrustedNodeByTarget(NodeTargetKind.Sentence, sentenceId)!;
            Program.Assert(core.GetNodeRepresentationByNodeId(symbolNode.GetNodeID())!.GetTextLabel() == "a" &&
                           core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())!.GetTextLabel() == "a" &&
                           core.GetNodeRepresentationByNodeId(sentenceNode.GetNodeID())!.GetTextLabel() == "a",
                "Untrusted summaries should use symbol, word, and sentence target data.");

            Program.Assert(core.SetUntrustedSymbol(new SymbolBox(symbolId, 'b')),
                "Replacing an untrusted symbol should update its target summary.");
            Program.Assert(core.GetNodeRepresentationByNodeId(symbolNode.GetNodeID())!.GetTextLabel() == "b" &&
                           core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())!.GetTextLabel() == "b" &&
                           core.GetNodeRepresentationByNodeId(sentenceNode.GetNodeID())!.GetTextLabel() == "b",
                "Target-summary changes should refresh dependent untrusted presentations.");

            Program.Assert(core.SetUntrustedSymbol(new SymbolBox(symbolId, 'c')),
                "A second untrusted symbol replacement should succeed.");
            Program.Assert(core.GetNodeRepresentationByNodeId(sentenceNode.GetNodeID())!.GetTextLabel() == "c",
                "Replacing an untrusted target box should recalculate dependent summaries.");

            Guid alternateSymbolId = Guid.NewGuid();
            Program.Assert(core.SetUntrustedSymbol(new SymbolBox(alternateSymbolId, 'z')),
                "Alternate untrusted symbol should be registered.");
            Program.Assert(core.SetUntrustedWord(new WordBox(wordId, Guid.NewGuid(), Guid.NewGuid(),
                new List<Guid> { alternateSymbolId }, PartOfSpeech.Noun)),
                "Replacing an untrusted word should update its target summary.");
            Program.Assert(core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())!.GetTextLabel() == "z" &&
                           core.GetNodeRepresentationByNodeId(sentenceNode.GetNodeID())!.GetTextLabel() == "z",
                "Word composition mutations should refresh the word and dependent sentence summaries.");
            ResetCore();
        }

        private static void NonLexicalUntrustedSummariesUseTargetData()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            Guid senseId = Guid.NewGuid();
            Guid usageId = Guid.NewGuid();
            Guid groupId = Guid.NewGuid();
            Guid sourceId = Guid.NewGuid();
            Guid functionId = Guid.NewGuid();
            Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense target should be trusted.");
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage target should be trusted.");
            Program.Assert(core.AddTrustedGroup(new GroupBox(groupId, "summary group", new List<Guid>())),
                "Group target should be trusted.");
            Program.Assert(core.AddTrustedSource(new SourceBox(sourceId, "summary source", new List<Guid>())),
                "Source target should be trusted.");
            FunctionBox function = new FunctionBox(functionId, typeof(double), "summary.function");
            Program.Assert(core.SetUntrustedFunction(function), "Function target should be staged as untrusted.");

            (NodeTargetKind Kind, Guid TargetId, string Expected)[] targets =
            {
                (NodeTargetKind.Sense, senseId, $"Sense {senseId:N}"),
                (NodeTargetKind.Usage, usageId, $"Usage {usageId:N}"),
                (NodeTargetKind.Group, groupId, "summary group"),
                (NodeTargetKind.Source, sourceId, "summary source")
            };
            foreach ((NodeTargetKind kind, Guid targetId, string expected) in targets)
            {
                Node node = new Node(kind, targetId, "draft");
                Program.Assert(core.SetUntrustedNode(node), $"Untrusted {kind} node should be registered.");
                Program.Assert(core.GetNodeRepresentationByNodeId(node.GetNodeID())!.GetTextLabel() == $"draft: {expected}",
                    $"Untrusted {kind} summary should use available target data.");
            }
            Node functionNode = core.GetUntrustedNodeByTarget(NodeTargetKind.Function, functionId)!;
            Program.Assert(functionNode != null &&
                           core.GetNodeRepresentationByNodeId(functionNode.GetNodeID())!.GetTextLabel() == "summary.function",
                "Untrusted Function summaries should use their staged FunctionKey.");
            ResetCore();
        }

        private static void ContainerLinkMutationsRecalculatePresentations()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            BuildSmallGraph(core);
            Guid sentenceNodeId = core.GetTrustedNodeIds(NodeTargetKind.Sentence).Single();
            Guid sentenceId = core.GetTrustedNode(sentenceNodeId)!.GetTargetID();
            Guid groupId = Guid.NewGuid();
            Guid sourceId = Guid.NewGuid();
            Program.Assert(core.AddTrustedGroup(new GroupBox(groupId, "group", new List<Guid>())),
                "Empty trusted Group should be registered.");
            Program.Assert(core.AddTrustedSource(new SourceBox(sourceId, "source", new List<Guid>())),
                "Empty trusted Source should be registered.");
            Guid groupNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Group, groupId)!.GetNodeID();
            Guid sourceNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Source, sourceId)!.GetNodeID();
            Vector3 groupPosition = core.GetNodeRepresentationByNodeId(groupNodeId)!.GetPosition();
            Program.Assert(core.AddSentenceToTrustedGroup(groupId, sentenceId),
                "Sentence membership should add its structural link.");
            Program.Assert(core.GetNodeDerivationSnapshot(groupNodeId)!.DerivedClass == NodeRelationType.ContainsSentence &&
                           core.GetNodeRepresentationByNodeId(groupNodeId)!.GetPosition() != groupPosition,
                "Group presentation should recalculate after a sentence link is added.");

            Vector3 sourcePosition = core.GetNodeRepresentationByNodeId(sourceNodeId)!.GetPosition();
            Program.Assert(core.AddGroupToTrustedSource(sourceId, groupId),
                "Source membership should add its structural link.");
            Program.Assert(core.GetNodeDerivationSnapshot(sourceNodeId)!.DerivedClass == NodeRelationType.ContainsGroup &&
                           core.GetNodeRepresentationByNodeId(sourceNodeId)!.GetPosition() != sourcePosition,
                "Source presentation should recalculate after a group link is added.");
            ResetCore();
        }

        private static void LinkMutationsKeepStoredPositionsSynchronized()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            Guid senseId = Guid.NewGuid();
            Guid usageId = Guid.NewGuid();
            AddSense(core, senseId);
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");
            Guid firstSymbol = Guid.NewGuid();
            Guid secondSymbol = Guid.NewGuid();
            AddSymbol(core, firstSymbol, senseId, 'a');
            AddSymbol(core, secondSymbol, senseId, 'b');
            Guid firstWord = AddWord(core, senseId, usageId, firstSymbol);
            Guid secondWord = AddWord(core, senseId, usageId, secondSymbol);
            Guid firstNode = core.GetTrustedNodeByTarget(NodeTargetKind.Word, firstWord)!.GetNodeID();
            Guid secondNode = core.GetTrustedNodeByTarget(NodeTargetKind.Word, secondWord)!.GetNodeID();

            AssertStoredPositionsMatchLayout(core, firstNode, secondNode);
            Program.Assert(core.AddTrustedLink(firstNode, secondNode, NodeRelationType.Custom, 0.4),
                "User link should be added.");
            AssertStoredPositionsMatchLayout(core, firstNode, secondNode);
            Program.Assert(core.UpdateTrustedLinkWeight(firstNode, secondNode, NodeRelationType.Custom, 0.8),
                "User link weight should update.");
            AssertStoredPositionsMatchLayout(core, firstNode, secondNode);
            Program.Assert(core.RemoveTrustedLink(firstNode, secondNode, NodeRelationType.Custom),
                "User link should be removed.");
            AssertStoredPositionsMatchLayout(core, firstNode, secondNode);
            ResetCore();
        }

        private static void AssertStoredPositionsMatchLayout(LRD_Core core, params Guid[] nodeIds)
        {
            NodeLayoutResult layout = core.GetNodeLayoutResult();
            foreach (Guid nodeId in nodeIds)
            {
                Program.Assert(core.GetNodeRepresentationByNodeId(nodeId)!.GetPosition() == layout.Positions[nodeId],
                    "Stored presentation positions should be refreshed to match the current graph layout.");
            }
        }

        private static void LayoutIsDeterministicAndVirtualAroundOrigin()
        {
            Guid[] nodeIds = { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            Dictionary<Guid, NodeDerivationSnapshot> firstSnapshots = new Dictionary<Guid, NodeDerivationSnapshot>();
            Dictionary<Guid, NodeDerivationSnapshot> secondSnapshots = new Dictionary<Guid, NodeDerivationSnapshot>();
            foreach (Guid id in nodeIds)
                firstSnapshots.Add(id, CreateSnapshot(id));
            foreach (Guid id in nodeIds.Reverse())
                secondSnapshots.Add(id, CreateSnapshot(id));

            Dictionary<Guid, double> sizes = firstSnapshots.ToDictionary(pair => pair.Key,
                pair => NodePresentationCalculator.CalculateSize(pair.Value));
            NodeTargetKind[] splitKinds = { NodeTargetKind.Symbol, NodeTargetKind.Word, NodeTargetKind.Sentence, NodeTargetKind.Function };
            NodeLayoutResult first = NodeLayoutCalculator.Calculate(firstSnapshots, sizes, splitKinds, new NodeLayoutOptions());
            NodeLayoutResult second = NodeLayoutCalculator.Calculate(secondSnapshots, sizes, splitKinds, new NodeLayoutOptions());
            NodeTargetKind[] expectedSpokes =
            {
                NodeTargetKind.Symbol, NodeTargetKind.Word, NodeTargetKind.Sentence, NodeTargetKind.Sense,
                NodeTargetKind.Usage, NodeTargetKind.Source, NodeTargetKind.Group, NodeTargetKind.Function
            };
            Program.Assert(first.CoreAnchor == Vector3.Zero &&
                           nodeIds.All(id => first.Positions[id] == second.Positions[id]) &&
                           first.Buckets.Select(bucket => bucket.TargetKind).Distinct().SequenceEqual(expectedSpokes),
                "Core anchor and positions should be stable regardless of snapshot registration order.");
            Program.Assert(NodeLayoutCalculator.CalculateRadius(2, 0) > NodeLayoutCalculator.CalculateRadius(1, 0) &&
                           NodeLayoutCalculator.CalculateRadius(2, 1) > NodeLayoutCalculator.CalculateRadius(2, 0),
                "Bucket radius should increase monotonically with Nodes and distinct internal links.");
            Program.Assert(double.IsFinite(NodeLayoutCalculator.CalculateRadius(int.MaxValue, int.MaxValue)) &&
                           NodeLayoutCalculator.CalculateRadius(int.MaxValue, int.MaxValue) >=
                           NodeLayoutCalculator.CalculateRadius(2, 1),
                "Extreme bucket counts should produce a finite saturated layout radius.");
            Program.AssertThrows<ArgumentOutOfRangeException>(() => NodeLayoutCalculator.CalculateRadius(-1, 0),
                "Negative node counts should be rejected when deriving bucket radii.");
            Program.AssertThrows<ArgumentOutOfRangeException>(() => NodeLayoutCalculator.CalculateRadius(0, -1),
                "Negative link counts should be rejected when deriving bucket radii.");
        }

        private static void LayoutSettingsPayloadsVersionAndReadLegacyRecords()
        {
            NodeLayoutOptions options = new NodeLayoutOptions(0.35, 0.4);
            byte[] versioned = FoundationBinarySerializer.SerializeNodeLayoutOptions(options);
            Program.Assert(versioned[0] == 1, "New layout settings payloads should carry format version 1.");
            NodeLayoutOptions roundTrip = FoundationBinarySerializer.DeserializeNodeLayoutOptions(versioned);
            Program.Assert(roundTrip.NodeWeightBias == options.NodeWeightBias &&
                           roundTrip.NeighborPositionBias == options.NeighborPositionBias,
                "Versioned layout settings should round-trip.");

            byte[] unversioned = new byte[32];
            NodeLayoutOptions.RecordKey.ToByteArray().CopyTo(unversioned, 0);
            BitConverter.GetBytes(0.15).CopyTo(unversioned, 16);
            BitConverter.GetBytes(0.25).CopyTo(unversioned, 24);
            NodeLayoutOptions legacy = FoundationBinarySerializer.DeserializeNodeLayoutOptions(unversioned);
            Program.Assert(legacy.NodeWeightBias == 0.15 && legacy.NeighborPositionBias == 0.25,
                "Previously written unversioned layout settings should remain readable.");

            versioned[0] = 2;
            Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeNodeLayoutOptions(versioned),
                "Unsupported layout settings versions should be rejected.");
        }

        private static void LayoutSpacingAndLinkClearanceRemainDeterministic()
        {
            Guid firstId = Guid.NewGuid();
            Guid secondId = Guid.NewGuid();
            Guid thirdId = Guid.NewGuid();
            Guid fourthId = Guid.NewGuid();
            Guid assertionId = Guid.NewGuid();
            NodeDerivationLink firstLink = new NodeDerivationLink(assertionId, NodeRelationType.Custom, 0.6, secondId);
            NodeDerivationLink secondLink = new NodeDerivationLink(assertionId, NodeRelationType.Custom, 0.6, firstId);
            Dictionary<Guid, NodeDerivationSnapshot> snapshots = new Dictionary<Guid, NodeDerivationSnapshot>
            {
                [firstId] = new NodeDerivationSnapshot(firstId, firstId, NodeTargetKind.Symbol, true,
                    NodeRelationType.Custom, 0.6, 1.0, "first", new[] { firstLink }, new[] { secondId }),
                [secondId] = new NodeDerivationSnapshot(secondId, secondId, NodeTargetKind.Symbol, true,
                    NodeRelationType.Custom, 0.6, 1.0, "second", new[] { secondLink }, new[] { firstId }),
                [thirdId] = CreateSnapshot(thirdId),
                [fourthId] = CreateSnapshot(fourthId)
            };
            Dictionary<Guid, double> sizes = snapshots.ToDictionary(pair => pair.Key,
                pair => NodePresentationCalculator.CalculateSize(pair.Value));
            NodeLayoutResult layout = NodeLayoutCalculator.Calculate(snapshots, sizes,
                new[] { NodeTargetKind.Symbol }, new NodeLayoutOptions());
            NodeLayoutResult repeated = NodeLayoutCalculator.Calculate(snapshots, sizes,
                new[] { NodeTargetKind.Symbol }, new NodeLayoutOptions());

            Program.Assert(snapshots.Keys.All(id => layout.Positions[id] == repeated.Positions[id]),
                "Spacing and clearance passes should be repeatable for a fixed graph.");
            Guid[] ids = snapshots.Keys.OrderBy(id => id).ToArray();
            for (int i = 0; i < ids.Length; i++)
            {
                for (int j = i + 1; j < ids.Length; j++)
                {
                    double required = 0.55 * (sizes[ids[i]] + sizes[ids[j]]) + 0.4;
                    Program.Assert(layout.Positions[ids[i]].Distance(layout.Positions[ids[j]]) >= required - 0.05,
                        "Size-aware spacing should separate nodes within a small bucket.");
                }
            }

            Vector3 start = layout.Positions[firstId];
            Vector3 end = layout.Positions[secondId];
            foreach (Guid otherId in new[] { thirdId, fourthId })
            {
                double requiredClearance = 0.55 * sizes[otherId] + 0.35;
                Program.Assert(DistanceToSegment(layout.Positions[otherId], start, end) >= requiredClearance - 0.1,
                    "Best-effort clearance should keep non-incident nodes away from link segments when feasible.");
            }
        }

        private static double DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            double lengthSquared = segment.LengthSquared();
            if (lengthSquared <= 1e-12) return point.Distance(start);
            double projection = (point - start).Dot(segment) / lengthSquared;
            Vector3 closest = start + segment * Math.Clamp(projection, 0.0, 1.0);
            return point.Distance(closest);
        }

        private static NodeLayoutBucket GetBucket(NodeLayoutResult layout, NodeTargetKind kind, bool isTrusted)
        {
            return layout.Buckets.Single(bucket => bucket.Key == NodeLayoutCalculator.BucketKey(kind, isTrusted));
        }

        private static NodeDerivationSnapshot CreateSnapshot(Guid nodeId)
        {
            return new NodeDerivationSnapshot(nodeId, nodeId, NodeTargetKind.Symbol, true, null,
                0.0, 1.0, "symbol", Array.Empty<NodeDerivationLink>(), Array.Empty<Guid>());
        }

        private static void UntrustedReferenceChangesRecalculateColor()
        {
            ResetCore();
            LRD_Core core = LRD_Core.Instance;
            Node node = new Node(NodeTargetKind.Word, Guid.NewGuid(), "pending");
            Program.Assert(core.SetUntrustedNode(node), "Untrusted node should register.");
            NodeRepresentation representation = core.GetNodeRepresentationByNodeId(node.GetNodeID())!;
            uint initialColor = representation.GetColor();
            node.RegisterVerifiedReference();
            uint trustedColor = representation.GetColor();
            node.RegisterInvalidReference();
            uint disputedColor = representation.GetColor();
            Program.Assert(initialColor != trustedColor && trustedColor != disputedColor,
                "Reference-count changes should recalculate derived color.");
        }

        private static void BuildSmallGraph(LRD_Core core)
        {
            Guid senseId = Guid.NewGuid();
            Guid usageId = Guid.NewGuid();
            AddSense(core, senseId);
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");
            Guid symbolId = Guid.NewGuid();
            AddSymbol(core, symbolId, senseId, 'x');
            Guid wordId = AddWord(core, senseId, usageId, symbolId);
            Program.Assert(core.AddTrustedSentence(new SentenceBox(Guid.NewGuid(), senseId, new List<Guid> { wordId })),
                "Sentence should be trusted.");
        }

        private static NodeDerivationLink Link(NodeRelationType relationType, double weight)
        {
            return new NodeDerivationLink(Guid.NewGuid(), relationType, weight, Guid.NewGuid());
        }

        private static void AddSense(LRD_Core core, Guid senseId)
        {
            Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
        }

        private static void AddSymbol(LRD_Core core, Guid symbolId, Guid senseId, char value)
        {
            SymbolBox symbol = new SymbolBox(symbolId, value);
            symbol.SetSenseID(senseId);
            Program.Assert(core.AddTrustedSymbol(symbol), "Symbol should be trusted.");
        }

        private static Guid AddWord(LRD_Core core, Guid senseId, Guid usageId, Guid symbolId)
        {
            Guid wordId = Guid.NewGuid();
            WordBox word = new WordBox(wordId, senseId, usageId, new List<Guid> { symbolId }, PartOfSpeech.Noun);
            Program.Assert(core.AddTrustedWord(word), "Word should be trusted.");
            return wordId;
        }

        private static void ResetCore()
        {
            LRD_Core.Instance.ResetCore();
            LRD_Core.Instance.InitializeCore();
        }
    }
}
