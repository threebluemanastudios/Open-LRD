using System.Numerics;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Visualization;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    /// <summary>Contains regression checks for visualization snapshots, geometry, projection, and layout.</summary>
    internal static class VisualizerTests
    {
        private static int nextTypeId = 1_500_000;

        /// <summary>Registers and executes the visualizer regression checks.</summary>
        public static void RunAll()
        {
            Run("Visualizer snapshot ordering and labels", TestSnapshotOrderingAndLabels);
            Run("Visualizer snapshot reciprocal deduplication", TestSnapshotReciprocalDeduplication);
            Run("Visualizer snapshot skipped links", TestSnapshotSkippedLinks);
            Run("Visualizer snapshot distinct forward types", TestSnapshotDistinctForwardTypes);
            Run("Visualizer snapshot node cap", TestSnapshotNodeCap);
            Run("Visualizer snapshot link cap", TestSnapshotLinkCap);
            Run("Visualizer palette defaults", TestPaletteDefaults);
            Run("Visualizer color map precedence", TestColorMapPrecedence);
            Run("Visualizer settings round trip", TestSettingsRoundTrip);
            Run("Visualizer settings fallback", TestSettingsFallback);
            Run("Visualizer options validation", TestOptionsValidation);
            Run("Visualizer spatial node index queries", TestSpatialNodeIndex);
            Run("Visualizer projection quadrants", TestProjectionQuadrants);
            Run("Visualizer projection depth rejection", TestProjectionDepthRejection);
            Run("Visualizer projection ray round trip", TestProjectionRayRoundTrip);
            Run("Visualizer frame bounds", TestFrameBounds);
            Run("Visualizer fly camera movement", TestFlyCameraMovement);
            Run("Visualizer ray AABB picking", TestRayAabbPicking);
            Run("Visualizer cube mesh", TestCubeMesh);
            Run("Visualizer merged node meshes", TestMergedNodeMeshes);
            Run("Visualizer link meshes", TestLinkMeshes);
            Run("Visualizer empty snapshot meshes", TestEmptySnapshotMeshes);
            Run("Visualizer layout empty snapshot", TestLayoutEmptySnapshot);
            Run("Visualizer layout determinism", TestLayoutDeterminism);
            Run("Visualizer layout distance fidelity", TestLayoutDistanceFidelity);
            Run("Visualizer layout collision spacing", TestLayoutCollisionSpacing);
            Run("Visualizer layout component packing", TestLayoutComponentPacking);
            Run("Visualizer layout spherical cloud distribution", TestLayoutSphericalCloudDistribution);
            Run("Visualizer layout center and bounds", TestLayoutCenterAndBounds);
            Run("Visualizer layout cancellation", TestLayoutCancellation);
            Run("Visualizer layout progress", TestLayoutProgress);
            Run("Visualizer layout performance", TestLayoutPerformance);
            Run("Visualizer scene geometry batches", TestSceneGeometryBatches);
            Run("Visualizer scene geometry validation", TestSceneGeometryValidation);
        }

        /// <summary>Initializes and returns the singleton graph core.</summary>
        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        /// <summary>Allocates a unique runtime type ID for visualizer tests.</summary>
        private static int NextTypeId()
        {
            return Interlocked.Increment(ref nextTypeId);
        }

        /// <summary>Builds a deterministic GUID from an integer suffix.</summary>
        private static Guid NodeId(int value)
        {
            return Guid.Parse($"00000000-0000-0000-0000-{value:D12}");
        }

        /// <summary>Creates and adds a graph node fixture with optional label and aliases.</summary>
        private static Web_Node AddNode(
            LRD_Core core,
            Guid id,
            NodeTypes type,
            string? label = null,
            params string[] aliases)
        {
            Web_Node node = new Web_Node(id, type, NodeLifetime.Temporary, 100);

            if (label is not null)
                node.Set_Label(label);

            foreach (string alias in aliases)
                node.Add_Alias(alias);

            Assert(core.AddExistingNode(node), $"node '{id}' should be added");
            return node;
        }

        /// <summary>Creates and returns a unique temporary directory for visualizer settings tests.</summary>
        private static string NewTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "open-lrd-visualizer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>Deletes a temporary test directory if it exists.</summary>
        private static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }

        /// <summary>Resolves a required named color from the built-in graph palette.</summary>
        private static ColorChoice Color(string name)
        {
            Assert(GraphColorPalette.TryGet(name, out ColorChoice choice), $"palette color '{name}' should exist");
            return choice;
        }

        /// <summary>Verifies deterministic GUID ordering and display-label fallback in graph snapshots.</summary>
        private static void TestSnapshotOrderingAndLabels()
        {
            LRD_Core core = NewCore();
            Guid firstId = NodeId(2);
            Guid secondId = NodeId(1);
            Guid thirdId = NodeId(3);

            AddNode(core, thirdId, NodeTypes.Names);
            AddNode(core, firstId, NodeTypes.Basics, label: "Zulu");
            AddNode(core, secondId, NodeTypes.Concepts, aliases: new[] { "Alpha Alias" });

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);

            Assert(snapshot.Nodes.Count == 3, "snapshot should contain every node");
            Assert(snapshot.Nodes[0].Id == secondId, "nodes should sort by GUID ascending");
            Assert(snapshot.Nodes[1].Id == firstId, "nodes should sort by GUID ascending");
            Assert(snapshot.Nodes[2].Id == thirdId, "nodes should sort by GUID ascending");
            Assert(snapshot.Nodes[0].DisplayLabel == "Alpha Alias", "display label should fall back to the first alias");
            Assert(snapshot.Nodes[1].DisplayLabel == "Zulu", "display label should prefer the node label");
            Assert(
                snapshot.Nodes[2].DisplayLabel == thirdId.ToString("N")[..8],
                "display label should fall back to the short ID");
            Assert(snapshot.Nodes[0].Aliases.Count == 1, "aliases should be copied into the snapshot");
            Assert(snapshot.Nodes[1].TypeId == NodeTypes.Basics.Id, "node type ID should be captured");
            Assert(snapshot.Nodes[1].TypeName == NodeTypes.Basics.Name, "node type name should be captured");
            Assert(!snapshot.IsTruncated && snapshot.Warnings.Count == 0, "a complete snapshot should have no warnings");
        }

        /// <summary>Verifies that reciprocal link pairs collapse to a single deduplicated snapshot link.</summary>
        private static void TestSnapshotReciprocalDeduplication()
        {
            LRD_Core core = NewCore();
            Guid firstId = NodeId(1);
            Guid secondId = NodeId(2);

            AddNode(core, firstId, NodeTypes.Basics);
            AddNode(core, secondId, NodeTypes.Events);

            Assert(core.AddConnection(firstId, secondId, LinkClass.basic), "two-way connection should be added");

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);

            Assert(snapshot.SourceLinkCount == 2, "the core should store one record per endpoint");
            Assert(snapshot.DuplicateLinkCount == 1, "the reciprocal record should be counted as a duplicate");
            Assert(snapshot.Links.Count == 1, "reciprocal records should collapse to one rendered link");
            Assert(snapshot.Links[0].AIndex == 0 && snapshot.Links[0].BIndex == 1, "the link should use sorted node indices");
            Assert(snapshot.Links[0].Distance == 1.0, "basic link distance should resolve to its class default");
            Assert(snapshot.Nodes[0].Degree == 1 && snapshot.Nodes[1].Degree == 1, "degree should count the deduplicated link");
            Assert(snapshot.Warnings.Count == 1, "deduplication should be reported as a warning");
        }

        /// <summary>Verifies that self-links and dangling links are skipped and reported in snapshot warnings.</summary>
        private static void TestSnapshotSkippedLinks()
        {
            LRD_Core core = NewCore();
            Guid nodeId = NodeId(1);
            Guid missingId = NodeId(99);
            Web_Node node = AddNode(core, nodeId, NodeTypes.Basics);

            node.Add_Link(new Link(nodeId, nodeId, LinkClass.basic));
            node.Add_Link(new Link(nodeId, missingId, LinkClass.basic));

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);

            Assert(snapshot.SourceLinkCount == 2, "both raw links should be enumerated");
            Assert(snapshot.SelfLinkCount == 1, "a self-link should be counted");
            Assert(snapshot.DanglingLinkCount == 1, "a link to a missing node should be counted");
            Assert(snapshot.Links.Count == 0, "skipped links should not be rendered");
            Assert(snapshot.Nodes[0].Degree == 0, "skipped links should not affect degree");
            Assert(snapshot.Warnings.Count == 2, "both skip reasons should be reported");
        }

        /// <summary>Verifies that links with distinct forward types between the same nodes remain separate.</summary>
        private static void TestSnapshotDistinctForwardTypes()
        {
            LRD_Core core = NewCore();
            Guid firstId = NodeId(1);
            Guid secondId = NodeId(2);
            Web_Node first = AddNode(core, firstId, NodeTypes.Basics);
            Web_Node second = AddNode(core, secondId, NodeTypes.Concepts);

            int forwardId = NextTypeId();
            int inverseId = NextTypeId();
            ForwardLinkTypes forward = ForwardLinkTypes.Register(forwardId, "snapshot_forward_" + forwardId);
            InverseLinkTypes inverse = InverseLinkTypes.Register(inverseId, "snapshot_inverse_" + inverseId);

            first.Add_Link(new Link(firstId, secondId, forward, inverse, LinkClass.basic));
            second.Add_Link(new Link(secondId, firstId, forward, inverse, LinkClass.basic));
            Assert(core.AddDirectedConnection(firstId, secondId, LinkClass.basic), "basic link should be added");

            try
            {
                LinkDistanceDefinitions.SetForwardOverride(forward, 0.25);
                GraphSnapshot snapshot = GraphSnapshot.FromCore(core);

                Assert(snapshot.SourceLinkCount == 3, "all raw link records should be enumerated");
                Assert(snapshot.DuplicateLinkCount == 1, "the reciprocal custom record should be deduplicated");
                Assert(snapshot.Links.Count == 2, "different forward types should remain distinct");
                Assert(
                    snapshot.Links.Any(link => link.ForwardTypeId == forwardId && link.Distance == 0.25),
                    "the forward override distance should be captured");
                Assert(
                    snapshot.Links.Any(link => link.ForwardTypeId == ForwardLinkTypes.basic.Id && link.Distance == 1.0),
                    "the basic link distance should be captured");
                Assert(
                    snapshot.ForwardTypeIds.SequenceEqual(snapshot.ForwardTypeIds.OrderBy(id => id)),
                    "forward type IDs should be sorted");
            }
            finally
            {
                LinkDistanceDefinitions.RemoveForwardOverride(forward);
            }
        }

        /// <summary>Verifies degree-prioritized node truncation when the graph exceeds MaxNodes.</summary>
        private static void TestSnapshotNodeCap()
        {
            LRD_Core core = NewCore();
            Guid firstId = NodeId(1);
            Guid secondId = NodeId(2);
            Guid thirdId = NodeId(3);

            AddNode(core, firstId, NodeTypes.Basics);
            AddNode(core, secondId, NodeTypes.Basics);
            AddNode(core, thirdId, NodeTypes.Basics);

            Assert(core.AddConnection(firstId, secondId), "first link should be added");
            Assert(core.AddConnection(secondId, thirdId), "second link should be added");

            GraphSnapshot snapshot = GraphSnapshot.FromCore(
                core,
                new VisualizerOptions { MaxNodes = 2 });

            Assert(snapshot.SourceNodeCount == 3, "the source node count should be captured");
            Assert(snapshot.Nodes.Count == 2, "the node cap should be applied");
            Assert(snapshot.TruncatedNodeCount == 1, "the removed node should be counted");
            Assert(snapshot.Nodes[0].Id == firstId, "the high-degree node should survive");
            Assert(snapshot.Nodes[1].Id == secondId, "tie-breaking should keep the smaller ID");
            Assert(snapshot.Links.Count == 1, "only induced links should survive");
            Assert(snapshot.Nodes[1].Degree == 1, "surviving degree should reflect the induced link set");
            Assert(snapshot.IsTruncated, "the snapshot should report truncation");
        }

        /// <summary>Verifies deterministic link truncation when the induced link count exceeds MaxLinks.</summary>
        private static void TestSnapshotLinkCap()
        {
            LRD_Core core = NewCore();
            Guid firstId = NodeId(1);
            Guid secondId = NodeId(2);
            Guid thirdId = NodeId(3);

            AddNode(core, firstId, NodeTypes.Basics);
            AddNode(core, secondId, NodeTypes.Basics);
            AddNode(core, thirdId, NodeTypes.Basics);

            Assert(core.AddConnection(firstId, secondId), "first link should be added");
            Assert(core.AddConnection(firstId, thirdId), "second link should be added");
            Assert(core.AddConnection(secondId, thirdId), "third link should be added");

            GraphSnapshot snapshot = GraphSnapshot.FromCore(
                core,
                new VisualizerOptions { MaxLinks = 2 });

            Assert(snapshot.Links.Count == 2, "the link cap should be applied");
            Assert(snapshot.TruncatedLinkCount == 1, "the removed link should be counted");
            Assert(snapshot.Links[0].AIndex == 0 && snapshot.Links[0].BIndex == 1, "links should be capped in sorted order");
            Assert(snapshot.Links[1].AIndex == 0 && snapshot.Links[1].BIndex == 2, "links should be capped in sorted order");
            Assert(snapshot.Nodes[0].Degree == 2, "degree should reflect the capped link set");
        }

        /// <summary>Verifies deterministic default color assignments and name lookups in the palette.</summary>
        private static void TestPaletteDefaults()
        {
            Assert(GraphColorPalette.Palette.Count == 24, "the palette should contain 24 named colors");

            HashSet<RgbColor> colors = new HashSet<RgbColor>();

            for (int typeId = 1; typeId <= 20; typeId++)
            {
                ColorChoice first = GraphColorPalette.GetDefaultNodeColor(typeId);
                ColorChoice second = GraphColorPalette.GetDefaultNodeColor(typeId);

                Assert(first == second, "default node colors should be deterministic");
                Assert(
                    GraphColorPalette.GetDefaultLinkColor(typeId).Color == first.Color,
                    "node and link defaults should use the same deterministic mapping");
                colors.Add(first.Color);
            }

            Assert(colors.Count == 20, "the first 20 type IDs should receive distinct colors");
            Assert(GraphColorPalette.TryGet("sky", out ColorChoice sky), "palette lookup should be case-insensitive");
            Assert(sky.Name == "Sky", "palette lookup should return the named choice");
            Assert(GraphColorPalette.TryGetName(sky.Color, out string? name), "colors should map back to names");
            Assert(name == "Sky", "reverse lookup should return the palette name");
            Assert(!GraphColorPalette.TryGet("not-a-color", out _), "unknown color names should be rejected");
        }

        /// <summary>Verifies override precedence and change notifications on the graph color map.</summary>
        private static void TestColorMapPrecedence()
        {
            GraphColorMap map = new GraphColorMap();
            RgbColor nodeColor = Color("Sky").Color;
            RgbColor classColor = Color("Violet").Color;
            RgbColor linkColor = Color("Red").Color;
            int eventCount = 0;
            map.Changed += (_, _) => eventCount++;

            map.SetNodeColor(NodeTypes.Concepts.Id, nodeColor);
            Assert(map.GetNodeColor(NodeTypes.Concepts.Id) == nodeColor, "node override should win");
            Assert(eventCount == 1, "setting a color should raise one change event");

            map.SetNodeColor(NodeTypes.Concepts.Id, nodeColor);
            Assert(eventCount == 1, "setting the same color should not raise an event");

            map.SetClassColor(LinkClass.events.Id, classColor);
            Assert(
                map.GetLinkColor(ForwardLinkTypes.basic.Id, LinkClass.events.Id) == classColor,
                "link-class fallback should apply");

            map.SetLinkColor(ForwardLinkTypes.basic.Id, linkColor);
            Assert(
                map.GetLinkColor(ForwardLinkTypes.basic.Id, LinkClass.events.Id) == linkColor,
                "forward link override should beat the class fallback");

            Assert(map.ClearLinkColor(ForwardLinkTypes.basic.Id), "link override should clear");
            Assert(
                map.GetLinkColor(ForwardLinkTypes.basic.Id, LinkClass.events.Id) == classColor,
                "clearing the forward override should restore the class color");

            Assert(map.ClearClassColor(LinkClass.events.Id), "class override should clear");
            Assert(
                map.GetLinkColor(ForwardLinkTypes.basic.Id, LinkClass.events.Id)
                    == GraphColorPalette.GetDefaultLinkColor(ForwardLinkTypes.basic.Id).Color,
                "clearing all overrides should restore the palette default");

            map.Reset();
            Assert(map.NodeColors.Count == 0 && map.LinkColors.Count == 0 && map.ClassColors.Count == 0, "reset should clear all overrides");
        }

        /// <summary>Verifies JSON serialization and restoration of visualizer colors and view options.</summary>
        private static void TestSettingsRoundTrip()
        {
            string directory = NewTempDirectory();

            try
            {
                string path = Path.Combine(directory, "visualizer-settings.json");
                GraphColorMap colors = new GraphColorMap();
                colors.SetNodeColor(NodeTypes.Concepts.Id, Color("Sky").Color);
                colors.SetLinkColor(ForwardLinkTypes.basic.Id, Color("Cyan").Color);
                colors.SetClassColor(LinkClass.identities.Id, Color("Violet").Color);

                VisualizerOptions options = new VisualizerOptions
                {
                    Spread = 17.0,
                    CubeSize = 1.2,
                    LineWidth = 0.2,
                    ShowLabels = false,
                    MaxLabels = 49,
                    LabelMode = VisualizerLabelMode.AllVisible,
                    MaxNodes = 75000,
                    MaxLinks = 125000,
                    NearPlaneDistance = 0.75
                };

                VisualizerSettings settings = VisualizerSettings.From(colors, options);
                Assert(settings.Version == VisualizerSettings.CurrentVersion, "settings should use the current version");
                Assert(settings.NodeColors[NodeTypes.Concepts.Id] == "Sky", "node color names should be serialized");
                Assert(settings.LinkColors[ForwardLinkTypes.basic.Id] == "Cyan", "link color names should be serialized");
                Assert(settings.ClassColors[LinkClass.identities.Id] == "Violet", "class color names should be serialized");

                settings.Save(path);
                Assert(File.Exists(path), "settings should be written");
                Assert(!File.Exists(path + ".tmp"), "the temporary settings file should be removed");

                VisualizerSettings loaded = VisualizerSettings.Load(path, out string? warning);
                Assert(warning is null, $"a valid settings file should not warn: {warning}");

                GraphColorMap loadedColors = new GraphColorMap();
                VisualizerOptions loadedOptions = new VisualizerOptions();
                loaded.ApplyTo(loadedColors, loadedOptions);

                Assert(loadedColors.GetNodeColor(NodeTypes.Concepts.Id) == Color("Sky").Color, "node color should round trip");
                Assert(
                    loadedColors.GetLinkColor(ForwardLinkTypes.basic.Id, LinkClass.basic.Id) == Color("Cyan").Color,
                    "link color should round trip");
                Assert(
                    loadedColors.GetClassColor(LinkClass.identities.Id) == Color("Violet").Color,
                    "class color should round trip");
                Assert(loadedOptions.Spread == 17.0, "spread should round trip");
                Assert(loadedOptions.CubeSize == 1.2, "cube size should round trip");
                Assert(loadedOptions.LineWidth == 0.2, "line width should round trip");
                Assert(!loadedOptions.ShowLabels, "label visibility should round trip");
                Assert(loadedOptions.MaxLabels == 49, "label cap should round trip");
                Assert(loadedOptions.LabelMode == VisualizerLabelMode.AllVisible, "label mode should round trip");
                Assert(loadedOptions.MaxNodes == 75000, "node cap should round trip");
                Assert(loadedOptions.MaxLinks == 125000, "link cap should round trip");
                Assert(loadedOptions.NearPlaneDistance == 0.75, "near plane distance should round trip");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>Verifies fallback to default settings when files are missing, corrupt, or out of range.</summary>
        private static void TestSettingsFallback()
        {
            string directory = NewTempDirectory();

            try
            {
                string missingPath = Path.Combine(directory, "missing.json");
                VisualizerSettings missing = VisualizerSettings.Load(missingPath, out string? missingWarning);
                Assert(missingWarning is null, "a missing settings file should not produce a warning");
                Assert(missing.Version == VisualizerSettings.CurrentVersion, "missing settings should use defaults");
                Assert(missing.View.Spread == VisualizerOptions.DefaultSpread, "missing settings should use default view options");
                Assert(missing.View.MaxLabels == VisualizerOptions.DefaultMaxLabels, "missing settings should default to the 50-label cap");
                Assert(missing.View.LabelMode == VisualizerLabelMode.Focused, "missing settings should default to Focused label mode");

                string corruptPath = Path.Combine(directory, "corrupt.json");
                File.WriteAllText(corruptPath, "{ this is not json");
                VisualizerSettings corrupt = VisualizerSettings.Load(corruptPath, out string? corruptWarning);
                Assert(corruptWarning is not null, "corrupt settings should report a warning");
                Assert(corrupt.Version == VisualizerSettings.CurrentVersion, "corrupt settings should fall back to defaults");

                string futurePath = Path.Combine(directory, "future.json");
                File.WriteAllText(futurePath, "{\"version\": 99, \"view\": {\"spread\": 1.0}}");
                VisualizerSettings future = VisualizerSettings.Load(futurePath, out string? futureWarning);
                Assert(futureWarning is not null, "unsupported versions should report a warning");
                Assert(future.View.Spread == VisualizerOptions.DefaultSpread, "unsupported versions should use defaults");

                VisualizerOptions migrated = new VisualizerOptions();
                VisualizerSettings legacyLabels = VisualizerSettings.CreateDefault();
                legacyLabels.View.MaxLabels = 1000;
                legacyLabels.ApplyTo(new GraphColorMap(), migrated);
                Assert(migrated.MaxLabels == 50, "legacy label caps above 50 should clamp to 50");
                Assert(migrated.LabelMode == VisualizerLabelMode.Focused, "legacy settings should default to Focused mode");

                VisualizerSettings invalidLabelMode = VisualizerSettings.CreateDefault();
                invalidLabelMode.View.LabelMode = (VisualizerLabelMode)99;
                invalidLabelMode.ApplyTo(new GraphColorMap(), migrated);
                Assert(migrated.LabelMode == VisualizerLabelMode.Focused, "unknown persisted label modes should fall back to Focused");

                VisualizerSettings lowLabelCap = VisualizerSettings.CreateDefault();
                lowLabelCap.View.MaxLabels = 1;
                lowLabelCap.ApplyTo(new GraphColorMap(), migrated);
                Assert(migrated.MaxLabels == VisualizerOptions.MinimumMaxLabels, "persisted label caps below 2 should clamp to 2");

                VisualizerSettings oldLowSpread = VisualizerSettings.CreateDefault();
                oldLowSpread.View.Spread = 7.5;
                oldLowSpread.ApplyTo(new GraphColorMap(), migrated);
                Assert(migrated.Spread == 10.0, "old saved spread values below the slider minimum should clamp to 10");

                VisualizerSettings oldHighSpread = VisualizerSettings.CreateDefault();
                oldHighSpread.View.Spread = 125.0;
                oldHighSpread.ApplyTo(new GraphColorMap(), migrated);
                Assert(migrated.Spread == 100.0, "old saved spread values above the slider maximum should clamp to 100");

                VisualizerSettings fractionalSpread = VisualizerSettings.CreateDefault();
                fractionalSpread.View.Spread = 23.6;
                fractionalSpread.ApplyTo(new GraphColorMap(), migrated);
                Assert(migrated.Spread == 24.0, "fractional saved spread values should round to the nearest whole number");

                VisualizerSettings invalidSpread = VisualizerSettings.CreateDefault();
                invalidSpread.View.Spread = double.NaN;
                invalidSpread.ApplyTo(new GraphColorMap(), migrated);
                Assert(migrated.Spread == VisualizerOptions.DefaultSpread, "invalid saved spread values should reset to 20");

                GraphColorMap colors = new GraphColorMap();
                colors.SetNodeColor(NodeTypes.Basics.Id, Color("Red").Color);
                VisualizerOptions options = new VisualizerOptions { Spread = 42.0 };
                corrupt.ApplyTo(colors, options);
                Assert(colors.NodeColors.Count == 0, "loading corrupt defaults should clear overrides");
                Assert(options.Spread == VisualizerOptions.DefaultSpread, "loading corrupt defaults should reset view options");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        /// <summary>Verifies range and finiteness validation on visualizer options.</summary>
        private static void TestOptionsValidation()
        {
            VisualizerOptions valid = new VisualizerOptions
            {
                Spread = VisualizerOptions.MinimumSpread,
                CubeSize = 1e-9,
                LineWidth = 1e-9,
                MaxIterations = 1,
                ConvergenceTolerance = 1e-9,
                MinSpacingFactor = 1e-9,
                Gravity = 0d,
                MaxNodes = 1,
                MaxLinks = 1,
                MaxLabels = 2,
                LabelCharacterLimit = 1
            };

            valid.Validate();
            Assert(new VisualizerOptions().LabelMode == VisualizerLabelMode.Focused, "Focused label mode should be the default");
            Assert(new VisualizerOptions().MaxLabels == 50, "the default All Visible label cap should be 50");
            Assert(VisualizerOptions.MaxLabelCandidatesPerFrame == 250, "background label projection budget should be 250 per frame");
            Assert(valid.MinSpacing == VisualizerOptions.MinimumSpacing, "minimum spacing should clamp tiny values");
            Assert(VisualizerOptions.NormalizeSpread(9.9) == 10.0, "spread below the slider range should clamp to 10");
            Assert(VisualizerOptions.NormalizeSpread(100.1) == 100.0, "spread above the slider range should clamp to 100");
            Assert(VisualizerOptions.NormalizeSpread(34.4) == 34.0, "spread should round down to the nearest whole number");
            Assert(VisualizerOptions.NormalizeSpread(34.6) == 35.0, "spread should round up to the nearest whole number");
            Assert(VisualizerOptions.NormalizeSpread(double.PositiveInfinity) == VisualizerOptions.DefaultSpread,
                "non-finite spread should reset to the default");
            Assert(new VisualizerOptions().MinSpacing == 1.75, "default spacing should scale from the default cube size");

            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { Spread = double.NaN }.Validate(),
                "non-finite spread should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { Spread = 9.0 }.Validate(),
                "spread below 10 should be rejected by options validation");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { Spread = 100.5 }.Validate(),
                "fractional spread should be rejected by options validation");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { CubeSize = 0d }.Validate(),
                "zero cube size should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { LineWidth = double.PositiveInfinity }.Validate(),
                "infinite line width should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { MaxIterations = 0 }.Validate(),
                "zero iterations should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { MaxNodes = 0 }.Validate(),
                "zero node cap should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { MaxNodes = VisualizerOptions.MaximumMaxNodes + 1 }.Validate(),
                "node cap above the UI-supported maximum should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { MaxLinks = 0 }.Validate(),
                "zero link cap should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { MaxLinks = VisualizerOptions.MaximumMaxLinks + 1 }.Validate(),
                "link cap above the UI-supported maximum should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { MaxLabels = 1 }.Validate(),
                "label caps below 2 should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { MaxLabels = 51 }.Validate(),
                "label caps above 50 should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { LabelMode = (VisualizerLabelMode)99 }.Validate(),
                "unknown label modes should be rejected by options validation");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { LabelCharacterLimit = 0 }.Validate(),
                "zero label length should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { NearPlaneDistance = 0.005 }.Validate(),
                "near plane distance below 0.01 should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { NearPlaneDistance = 150.0 }.Validate(),
                "near plane distance above 100.0 should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new VisualizerOptions { Gravity = -0.1 }.Validate(),
                "negative gravity should be rejected");
        }

        /// <summary>Verifies screen-quadrant projection of camera-relative 3D points.</summary>
        private static void TestProjectionQuadrants()
        {
            CameraView camera = CreateTestCamera();

            Assert(camera.TryProject(camera.LookDirection * 10f, out Vector2 center, out double centerDepth), "center point should project");
            Assert(Near(center.X, 400f) && Near(center.Y, 300f), "center point should project to the viewport center");
            Assert(Near((float)centerDepth, 10f), "center point depth should equal its forward distance");

            Vector3 rightPoint = camera.LookDirection * 10f + camera.Right * 2f;
            Assert(camera.TryProject(rightPoint, out Vector2 right, out _), "right point should project");
            Assert(right.X > center.X && Near(right.Y, center.Y), "camera-right points should project to the right half");

            Vector3 upPoint = camera.LookDirection * 10f + camera.Up * 2f;
            Assert(camera.TryProject(upPoint, out Vector2 up, out _), "up point should project");
            Assert(up.Y < center.Y && Near(up.X, center.X), "camera-up points should project above the center");

            Vector3 leftPoint = camera.LookDirection * 10f - camera.Right * 2f;
            Assert(camera.TryProject(leftPoint, out Vector2 left, out _), "left point should project");
            Assert(left.X < center.X, "camera-left points should project to the left half");
        }

        /// <summary>Verifies that points behind the camera or outside the clip planes fail projection.</summary>
        private static void TestProjectionDepthRejection()
        {
            CameraView camera = CreateTestCamera();

            Assert(!camera.TryProject(-camera.LookDirection * 5f, out _, out _), "points behind the camera should be rejected");
            Assert(!camera.TryProject(camera.LookDirection * 0.05f, out _, out _), "points closer than the near plane should be rejected");
            Assert(!camera.TryProject(camera.LookDirection * 1000f, out _, out _), "points beyond the far plane should be rejected");
        }

        /// <summary>Verifies that a projected screen coordinate reconstructs a ray passing through the world point.</summary>
        private static void TestProjectionRayRoundTrip()
        {
            CameraView camera = CreateTestCamera();
            Vector3 world = camera.LookDirection * 12f + camera.Right * 1.5f - camera.Up * 2f;

            Assert(camera.TryProject(world, out Vector2 screen, out _), "sample point should project");
            camera.CreateRay(screen, out Vector3 origin, out Vector3 direction);

            Assert(Vector3.Distance(origin, camera.Position) < 1e-5f, "rays should start at the camera position");
            Assert(Math.Abs(direction.Length() - 1f) < 1e-4f, "ray directions should be normalized");

            Vector3 toWorld = world - origin;
            float alongRay = Vector3.Dot(toWorld, direction);
            Vector3 closest = origin + direction * alongRay;
            Assert(Vector3.Distance(world, closest) < 1e-3f, "the projected point should lie on the reconstructed ray");
        }

        /// <summary>Verifies that FrameBounds centers the target sphere within the camera frustum.</summary>
        private static void TestFrameBounds()
        {
            CameraView camera = CreateTestCamera();
            Vector3 center = new Vector3(10f, 5f, -4f);
            CameraView framed = ProjectionMath.FrameBounds(camera, center, 3f);

            Assert(
                Vector3.Distance(framed.Position, center) > 3f,
                "framing should place the camera outside the target sphere");
            Assert(Vector3.Distance(framed.LookDirection, camera.LookDirection) < 1e-5f, "framing should preserve orientation");
            Assert(framed.TryProject(center, out Vector2 screen, out double depth), "framed camera should see the center");
            Assert(Near(screen.X, 400f, 0.5f) && Near(screen.Y, 300f, 0.5f), "framed camera should center the target");
            Assert(depth > framed.NearPlaneDistance && depth < framed.FarPlaneDistance, "framed depth should be within the clip planes");
        }

        /// <summary>Verifies fly-camera translation, pitch clamping, yaw wrapping, orbiting, and roll.</summary>
        private static void TestFlyCameraMovement()
        {
            FlyCamera camera = new FlyCamera(Vector3.Zero, 0d, 0d);

            Assert(Vector3.Distance(camera.LookDirection, Vector3.UnitZ) < 1e-5f, "zero yaw and pitch should look along +Z");
            Assert(Vector3.Distance(camera.Right, -Vector3.UnitX) < 1e-5f, "the right vector should follow the right-handed frame");
            Assert(Vector3.Distance(camera.Up, Vector3.UnitY) < 1e-5f, "the up vector should be world up at level pitch");

            camera.Move(forward: 1d, strafe: 0d, vertical: 0d, speed: 2d, deltaSeconds: 0.5d);
            Assert(Vector3.Distance(camera.Position, new Vector3(0f, 0f, 1f)) < 1e-5f, "forward movement should follow the look direction");

            camera.Move(forward: 0d, strafe: 1d, vertical: 0d, speed: 1d, deltaSeconds: 1d);
            Assert(Vector3.Distance(camera.Position, new Vector3(-1f, 0f, 1f)) < 1e-5f, "strafe should follow the right vector");

            camera.Move(forward: 0d, strafe: 0d, vertical: 1d, speed: 1d, deltaSeconds: 1d);
            Assert(Vector3.Distance(camera.Position, new Vector3(-1f, 1f, 1f)) < 1e-5f, "vertical movement should use world up");

            camera.Look(deltaX: 0d, deltaY: 100000d);
            Assert(
                Math.Abs(camera.Pitch + FlyCamera.MaximumPitchRadians) < 1e-6,
                "pitch should clamp at the downward limit");
            Assert(IsFinite(camera.LookDirection) && IsFinite(camera.Right) && IsFinite(camera.Up), "clamped camera vectors should stay finite");

            camera.SetPose(Vector3.Zero, 0d, 10d);
            Assert(Math.Abs(camera.Pitch - FlyCamera.MaximumPitchRadians) < 1e-6, "set pose should clamp pitch");

            FlyCamera yawWrap = new FlyCamera(Vector3.Zero, Math.PI - 0.01, 0d);
            Vector3 lookBeforeYawWrap = yawWrap.LookDirection;
            yawWrap.Look(deltaX: 10d, deltaY: 0d);
            Assert(yawWrap.Yaw < -3.0, "reported yaw should wrap to the equivalent signed angle");
            Assert(Vector3.Distance(yawWrap.LookDirection, lookBeforeYawWrap) < 0.03f,
                "yaw wrapping should not introduce a visible direction discontinuity");

            FlyCamera framed = new FlyCamera(Vector3.Zero, 0d, 0d);
            framed.FrameSphere(new Vector3(0f, 0f, 10f), 2f, 60d, 1.5d);
            float distance = Vector3.Distance(framed.Position, new Vector3(0f, 0f, 10f));
            Assert(distance > 2f, "framing should move the camera outside the sphere");
            Assert(Vector3.Distance(framed.LookDirection, Vector3.UnitZ) < 1e-5f, "framing should preserve the view axis");

            FlyCamera orbitCam = new FlyCamera(new Vector3(0f, 0f, -10f), Math.PI / 6d, 0d);
            Vector3 target = Vector3.Zero;
            Quaternion orientationBeforeOrbit = orbitCam.Orientation;
            Vector3 targetDirectionBeforeOrbit = Vector3.Normalize(target - orbitCam.Position);
            double targetFacingAngleBeforeOrbit = Math.Acos(Math.Clamp(
                Vector3.Dot(orbitCam.LookDirection, targetDirectionBeforeOrbit),
                -1f,
                1f));
            float yawDelta = MathF.PI / 2f;
            Quaternion positionYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDelta);
            Quaternion expectedOrientation = Quaternion.Normalize(positionYaw * orientationBeforeOrbit);
            orbitCam.OrbitAroundTarget(target, deltaYawRadians: yawDelta, deltaPitchRadians: 0.0);
            float orbitDistance = Vector3.Distance(orbitCam.Position, target);
            Assert(Near(orbitDistance, 10f, 1e-2f), "orbiting should preserve radial distance to target");
            Assert(QuaternionEquivalent(orbitCam.Orientation, expectedOrientation), "orbit orientation should use the same position rotation");
            double targetFacingAngleAfterOrbit = Math.Acos(Math.Clamp(
                Vector3.Dot(orbitCam.LookDirection, Vector3.Normalize(target - orbitCam.Position)),
                -1f,
                1f));
            Assert(Math.Abs(targetFacingAngleAfterOrbit - targetFacingAngleBeforeOrbit) < 1e-4,
                "orbiting should preserve the camera's target-relative facing angle without re-aiming");

            FlyCamera pitchOrbit = new FlyCamera(new Vector3(0f, 0f, -10f), 0.3, -0.2);
            Quaternion beforeCombinedOrbit = pitchOrbit.Orientation;
            const double combinedYaw = 0.4;
            const double combinedPitch = 0.25;
            Quaternion yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)combinedYaw);
            double azimuth = Math.Atan2(pitchOrbit.Position.X, pitchOrbit.Position.Z) + combinedYaw;
            Vector3 elevationAxis = Vector3.Normalize(new Vector3((float)-Math.Cos(azimuth), 0f, (float)Math.Sin(azimuth)));
            Quaternion elevationRotation = Quaternion.CreateFromAxisAngle(elevationAxis, (float)combinedPitch);
            Quaternion combinedPositionRotation = Quaternion.Normalize(elevationRotation * yawRotation);
            Vector3 expectedOffset = Vector3.Transform(pitchOrbit.Position, combinedPositionRotation);
            Quaternion expectedCombinedOrientation = Quaternion.Normalize(combinedPositionRotation * beforeCombinedOrbit);
            pitchOrbit.OrbitAroundTarget(Vector3.Zero, combinedYaw, combinedPitch);
            Assert(Vector3.Distance(pitchOrbit.Position, expectedOffset) < 1e-4f,
                "combined yaw/elevation orbit should apply the composed position transform");
            Assert(QuaternionEquivalent(pitchOrbit.Orientation, expectedCombinedOrientation),
                "combined orbit should apply the same composed transform to camera orientation");

            FlyCamera boundedOrbit = new FlyCamera(
                new Vector3(0f, 0f, -10f),
                yaw: 0d,
                pitch: -FlyCamera.MaximumPitchRadians + 0.05d);
            boundedOrbit.OrbitAroundTarget(Vector3.Zero, deltaYawRadians: 0d, deltaPitchRadians: 1d);
            Assert(boundedOrbit.Pitch >= -FlyCamera.MaximumPitchRadians - 1e-5,
                "orbit rotation should respect the camera pitch limit");
            Assert(boundedOrbit.Position.Y < 10f,
                "orbit elevation should be reduced when camera pitch reaches its limit");

            FlyCamera rollCamera = new FlyCamera(Vector3.Zero, 0d, 0d);
            Vector3 originalRight = rollCamera.Right;
            rollCamera.Roll(Math.PI / 2.0);
            Assert(Vector3.Distance(rollCamera.Up, originalRight) < 1e-5f,
                "positive forward-axis roll should rotate screen up clockwise to screen right");
            FlyCamera restoredRoll = new FlyCamera();
            restoredRoll.SetOrientation(rollCamera.Position, rollCamera.LookDirection, rollCamera.Up);
            Assert(Vector3.Distance(restoredRoll.Right, rollCamera.Right) < 1e-5f
                && Vector3.Distance(restoredRoll.Up, rollCamera.Up) < 1e-5f,
                "camera synchronization should preserve the complete rolled orientation");
            rollCamera.Roll(-Math.PI / 2.0);
            Assert(Vector3.Distance(rollCamera.Up, Vector3.UnitY) < 1e-5f,
                "opposite forward-axis roll should restore the original camera up direction");
        }

        /// <summary>Determines whether two quaternions represent the same 3D rotation.</summary>
        private static bool QuaternionEquivalent(Quaternion left, Quaternion right)
        {
            return Math.Abs(Quaternion.Dot(Quaternion.Normalize(left), Quaternion.Normalize(right))) > 0.99999f;
        }

        /// <summary>Verifies ray-AABB hit detection, entry distance ordering, and bounds validation.</summary>
        private static void TestRayAabbPicking()
        {
            Vector3 origin = new Vector3(0f, 0f, -10f);
            Vector3 direction = Vector3.UnitZ;
            Vector3 nearMinimum = new Vector3(-1f, -1f, 0f);
            Vector3 nearMaximum = new Vector3(1f, 1f, 2f);
            Vector3 farMinimum = new Vector3(-1f, -1f, 5f);
            Vector3 farMaximum = new Vector3(1f, 1f, 7f);

            Assert(
                ProjectionMath.TryIntersectRayAabb(origin, direction, nearMinimum, nearMaximum, out float nearDistance),
                "near box should be hit");
            Assert(Near(nearDistance, 10f), "near box distance should be correct");
            Assert(
                ProjectionMath.TryIntersectRayAabb(origin, direction, farMinimum, farMaximum, out float farDistance),
                "far box should be hit");
            Assert(Near(farDistance, 15f), "far box distance should be correct");
            Assert(nearDistance < farDistance, "the near box should be selected first");
            Assert(
                !ProjectionMath.TryIntersectRayAabb(origin, direction, new Vector3(3f, 3f, 0f), new Vector3(4f, 4f, 2f), out _),
                "a missed box should not report a hit");
            Assert(
                ProjectionMath.TryIntersectRayAabb(new Vector3(0f, 0f, 1f), direction, nearMinimum, nearMaximum, out float insideDistance),
                "a ray starting inside a box should hit");
            Assert(Near(insideDistance, 0f), "inside hits should report distance zero");

            AssertThrows<ArgumentException>(
                () => ProjectionMath.TryIntersectRayAabb(origin, direction, Vector3.One, Vector3.Zero, out _),
                "invalid bounds should be rejected");
        }

        /// <summary>Verifies BVH frustum candidate collection, visibility filtering, and ray picking.</summary>
        private static void TestSpatialNodeIndex()
        {
            Vector3[] positions = new Vector3[20000];
            for (int index = 0; index < positions.Length; index++)
                positions[index] = new Vector3(0f, 0f, index + 1f);

            SpatialNodeIndex spatialIndex = SpatialNodeIndex.Build(positions, leafCapacity: 8);
            CameraView camera = new CameraView(
                Vector3.Zero,
                Vector3.UnitZ,
                Vector3.UnitY,
                fieldOfViewDegrees: 90d,
                viewportWidth: 1200d,
                viewportHeight: 800d,
                nearPlaneDistance: 0.1d,
                farPlaneDistance: 25000d);
            List<int> candidates = new List<int>();

            spatialIndex.CollectFrustumCandidates(
                camera,
                maximumDepth: 20000d,
                maximumCandidates: 250,
                isNodeVisible: null,
                priorityIndexA: 0,
                priorityIndexB: 1,
                candidates);
            Assert(candidates.Count == 250, "the BVH query should respect the background candidate budget");
            Assert(!candidates.Contains(0) && !candidates.Contains(1), "priority nodes should be excluded from the background budget");
            Assert(candidates[0] < candidates[^1], "near-to-far traversal should visit near graph positions first");

            spatialIndex.CollectFrustumCandidates(
                camera,
                maximumDepth: 20000d,
                maximumCandidates: 250,
                isNodeVisible: nodeIndex => nodeIndex % 2 == 0,
                priorityIndexA: null,
                priorityIndexB: null,
                candidates);
            Assert(candidates.Count == 250, "node-type filtering should continue until the projection budget is filled");
            Assert(candidates.All(candidate => candidate % 2 == 0), "the spatial index should apply node visibility filters");

            Vector3[] pickPositions =
            {
                new Vector3(0f, 0f, 5f),
                new Vector3(0f, 0f, 10f),
                new Vector3(5f, 0f, 4f)
            };
            SpatialNodeIndex pickIndex = SpatialNodeIndex.Build(pickPositions, leafCapacity: 1);
            int? nearest = pickIndex.FindNearestRayHit(Vector3.Zero, Vector3.UnitZ, 0.5f);
            Assert(nearest == 0, "indexed ray picking should return the nearest AABB hit");
            int? visibleNearest = pickIndex.FindNearestRayHit(
                Vector3.Zero,
                Vector3.UnitZ,
                0.5f,
                nodeIndex => nodeIndex != 0);
            Assert(visibleNearest == 1, "indexed ray picking should skip hidden nodes and find the next visible hit");
            Assert(pickIndex.FindNearestRayHit(Vector3.Zero, Vector3.UnitY, 0.5f) is null,
                "indexed ray picking should report misses");
        }

        /// <summary>Verifies vertex, normal, and triangle counts for cube and highlight meshes.</summary>
        private static void TestCubeMesh()
        {
            MeshData cube = GraphMeshBuilder.CreateCube(new Vector3(2f, 3f, 4f), 2d);

            Assert(cube.VertexCount == 24, "a cube should contain 24 vertices");
            Assert(cube.IndexCount == 36, "a cube should contain 36 indices");
            Assert(cube.TriangleCount == 12, "a cube should contain 12 triangles");

            HashSet<Vector3> normals = new HashSet<Vector3>();

            for (int index = 0; index < cube.VertexCount; index++)
            {
                int offset = index * 3;
                Vector3 normal = new Vector3(
                    cube.Normals[offset + 0],
                    cube.Normals[offset + 1],
                    cube.Normals[offset + 2]);
                normals.Add(normal);
                Assert(Math.Abs(normal.Length() - 1f) < 1e-4f, "cube normals should be unit length");

                Vector3 position = new Vector3(
                    cube.Positions[offset + 0],
                    cube.Positions[offset + 1],
                    cube.Positions[offset + 2]);
                Assert(
                    Math.Abs(position.X - 2f) <= 1f
                        && Math.Abs(position.Y - 3f) <= 1f
                        && Math.Abs(position.Z - 4f) <= 1f,
                    "cube positions should respect the center and size");
            }

            Assert(normals.Count == 6, "a cube should expose six face normals");
            MeshData highlight = GraphMeshBuilder.BuildHighlightCube(Vector3.Zero, 1d);
            Assert(highlight.VertexCount == 24, "the highlight cube should use the same mesh layout");
        }

        /// <summary>Verifies combined and per-type node cube mesh construction.</summary>
        private static void TestMergedNodeMeshes()
        {
            LRD_Core core = NewCore();
            AddNode(core, NodeId(1), NodeTypes.Basics);
            AddNode(core, NodeId(2), NodeTypes.Basics);
            AddNode(core, NodeId(3), NodeTypes.Concepts);

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);
            List<Vector3> positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(0f, 2f, 0f)
            };

            MeshData all = GraphMeshBuilder.BuildNodeCubes(snapshot, positions, 1d);
            Assert(all.VertexCount == 72, "merged node geometry should scale with node count");
            Assert(all.IndexCount == 108, "merged node indices should scale with node count");

            MeshData basics = GraphMeshBuilder.BuildNodeCubesForType(snapshot, positions, 1d, NodeTypes.Basics.Id);
            Assert(basics.VertexCount == 48, "type filtering should keep only matching cubes");
            Assert(basics.IndexCount == 72, "type filtering should keep only matching indices");

            MeshData concepts = GraphMeshBuilder.BuildNodeCubesForType(snapshot, positions, 1d, NodeTypes.Concepts.Id);
            Assert(concepts.VertexCount == 24, "a single matching node should produce one cube");

            MeshData missing = GraphMeshBuilder.BuildNodeCubesForType(snapshot, positions, 1d, 999999);
            Assert(missing.IsEmpty, "an absent type should produce an empty mesh");
        }

        /// <summary>Verifies crossed-ribbon link mesh construction and parallel-edge offsets.</summary>
        private static void TestLinkMeshes()
        {
            LRD_Core core = NewCore();
            Guid firstId = NodeId(1);
            Guid secondId = NodeId(2);
            Web_Node first = AddNode(core, firstId, NodeTypes.Basics);
            AddNode(core, secondId, NodeTypes.Concepts);

            int forwardId = NextTypeId();
            int inverseId = NextTypeId();
            ForwardLinkTypes forward = ForwardLinkTypes.Register(forwardId, "mesh_forward_" + forwardId);
            InverseLinkTypes inverse = InverseLinkTypes.Register(inverseId, "mesh_inverse_" + inverseId);

            Assert(core.AddDirectedConnection(firstId, secondId, LinkClass.basic), "basic link should be added");
            first.Add_Link(new Link(firstId, secondId, forward, inverse, LinkClass.basic));

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);
            List<Vector3> positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f)
            };

            MeshData all = GraphMeshBuilder.BuildLinkLines(snapshot, positions, 0.2d, 1d);
            Assert(snapshot.Links.Count == 2, "the snapshot should keep both forward types");
            Assert(all.VertexCount == 16, "each link should emit eight vertices");
            Assert(all.IndexCount == 24, "each link should emit twelve indices");

            Vector3 firstLinkVertex = ReadVertex(all, 0);
            Vector3 secondLinkVertex = ReadVertex(all, 8);
            Assert(
                Vector3.Distance(firstLinkVertex, secondLinkVertex) > 0.05f,
                "parallel links should be offset from one another");

            MeshData basic = GraphMeshBuilder.BuildLinkLinesForType(
                snapshot,
                positions,
                0.2d,
                1d,
                ForwardLinkTypes.basic.Id);
            Assert(basic.VertexCount == 8 && basic.IndexCount == 12, "type filtering should keep one link mesh");

            MeshData custom = GraphMeshBuilder.BuildLinkLinesForType(snapshot, positions, 0.2d, 1d, forwardId);
            Assert(custom.VertexCount == 8 && custom.IndexCount == 12, "the custom link type should produce its own mesh");
        }

        /// <summary>Verifies that an empty graph snapshot produces empty node and link meshes.</summary>
        private static void TestEmptySnapshotMeshes()
        {
            LRD_Core core = NewCore();
            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);

            Assert(snapshot.Nodes.Count == 0, "an empty core should produce no nodes");
            Assert(snapshot.Links.Count == 0, "an empty core should produce no links");
            Assert(snapshot.NodeTypeIds.Count == 0 && snapshot.ForwardTypeIds.Count == 0, "an empty core should produce no type IDs");
            Assert(!snapshot.IsTruncated && snapshot.Warnings.Count == 0, "an empty snapshot should be clean");

            List<Vector3> positions = new List<Vector3>();
            Assert(GraphMeshBuilder.BuildNodeCubes(snapshot, positions, 1d).IsEmpty, "empty node mesh should be empty");
            Assert(GraphMeshBuilder.BuildLinkLines(snapshot, positions, 0.2d, 1d).IsEmpty, "empty link mesh should be empty");
        }

        /// <summary>Verifies that laying out an empty graph snapshot completes with zero bounds and no warnings.</summary>
        private static void TestLayoutEmptySnapshot()
        {
            GraphSnapshot snapshot = GraphSnapshot.FromCore(NewCore());
            LayoutResult result = DistanceLayout3D.Compute(snapshot);

            Assert(result.IsEmpty, "an empty snapshot should produce an empty layout");
            Assert(result.NodeCount == 0 && result.LinkCount == 0, "empty layout counts should be zero");
            Assert(result.Iterations == 0 && result.Converged && !result.Cancelled, "empty layout should complete cleanly");
            Assert(result.Radius == 0f, "empty layout radius should be zero");
            Assert(result.BoundsMinimum == Vector3.Zero && result.BoundsMaximum == Vector3.Zero, "empty bounds should be zero");
            Assert(result.Centroid == Vector3.Zero, "empty centroid should be zero");
            Assert(result.Warnings.Count == 0, "empty layout should not warn");
        }

        /// <summary>Verifies that repeated layouts of the same snapshot produce identical node coordinates.</summary>
        private static void TestLayoutDeterminism()
        {
            LRD_Core core = BuildChainCore(5);
            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);

            LayoutResult first = DistanceLayout3D.Compute(snapshot);
            LayoutResult second = DistanceLayout3D.Compute(snapshot);

            Assert(first.Positions.Count == second.Positions.Count, "repeated layouts should have the same node count");
            Assert(first.Iterations == second.Iterations, "repeated layouts should take the same iteration count");
            Assert(first.Converged == second.Converged, "repeated layouts should agree on convergence");
            Assert(first.Radius == second.Radius, "repeated layouts should produce the same radius");
            Assert(first.BoundsMinimum == second.BoundsMinimum, "repeated layouts should produce the same minimum bounds");
            Assert(first.BoundsMaximum == second.BoundsMaximum, "repeated layouts should produce the same maximum bounds");
            Assert(first.Warnings.Count == second.Warnings.Count, "repeated layouts should produce the same warnings");

            for (int index = 0; index < first.Positions.Count; index++)
            {
                Assert(
                    first.Positions[index] == second.Positions[index],
                    $"node {index} should land at exactly the same position");
            }
        }

        /// <summary>Verifies that relaxed link lengths converge near their target semantic rest distances.</summary>
        private static void TestLayoutDistanceFidelity()
        {
            LRD_Core core = BuildChainCore(5);
            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);
            VisualizerOptions options = new VisualizerOptions();
            LayoutResult result = DistanceLayout3D.Compute(snapshot, options);

            Assert(result.Converged, "a short chain should converge");
            Assert(snapshot.Links.Count == 4, "the chain should have four deduplicated links");

            foreach (LinkRecord link in snapshot.Links)
            {
                float actual = Vector3.Distance(
                    result.Positions[link.AIndex],
                    result.Positions[link.BIndex]);
                float restLength = (float)(link.Distance * options.Spread);

                Assert(
                    Math.Abs(actual - restLength) <= restLength * 0.10f,
                    $"link length {actual} should stay within ten percent of the rest length {restLength}");
            }
        }

        /// <summary>Verifies that isolated nodes are separated by at least the minimum spacing distance.</summary>
        private static void TestLayoutCollisionSpacing()
        {
            LRD_Core core = NewCore();

            for (int index = 1; index <= 9; index++)
                AddNode(core, NodeId(index), NodeTypes.Basics);

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);
            VisualizerOptions options = new VisualizerOptions();
            LayoutResult result = DistanceLayout3D.Compute(snapshot, options);

            Assert(result.NodeCount == 9, "every isolated node should be placed");

            for (int a = 0; a < result.Positions.Count; a++)
            {
                for (int b = a + 1; b < result.Positions.Count; b++)
                {
                    float distance = Vector3.Distance(result.Positions[a], result.Positions[b]);

                    Assert(
                        distance >= options.MinSpacing,
                        $"isolated nodes {a} and {b} should respect minimum spacing (was {distance})");
                }
            }
        }

        /// <summary>Verifies that disconnected graph components pack apart without stretching internal links.</summary>
        private static void TestLayoutComponentPacking()
        {
            LRD_Core core = NewCore();
            AddNode(core, NodeId(1), NodeTypes.Basics);
            AddNode(core, NodeId(2), NodeTypes.Basics);
            AddNode(core, NodeId(3), NodeTypes.Basics);
            AddNode(core, NodeId(4), NodeTypes.Basics);
            Assert(core.AddConnection(NodeId(1), NodeId(2)), "first pair should connect");
            Assert(core.AddConnection(NodeId(3), NodeId(4)), "second pair should connect");

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);
            VisualizerOptions options = new VisualizerOptions();
            LayoutResult result = DistanceLayout3D.Compute(snapshot, options);

            foreach (Vector3 position in result.Positions)
                Assert(IsFinite(position), "packed positions should stay finite");

            Vector3 firstCentroid = (result.Positions[0] + result.Positions[1]) * 0.5f;
            Vector3 secondCentroid = (result.Positions[2] + result.Positions[3]) * 0.5f;

            Assert(
                Vector3.Distance(firstCentroid, secondCentroid) >= 7.4f,
                "disconnected components should be packed apart by the pack margin");
            Assert(
                Vector3.Distance(result.Positions[0], result.Positions[1]) <= options.Spread * 1.1f,
                "packing should translate whole components without stretching their links");
        }

        /// <summary>Verifies 3D spherical distribution of disconnected components and isolated node clouds.</summary>
        private static void TestLayoutSphericalCloudDistribution()
        {
            LRD_Core core = NewCore();

            // 5 disconnected pairs
            for (int i = 1; i <= 10; i++)
                AddNode(core, NodeId(i), NodeTypes.Basics);

            for (int i = 1; i <= 5; i++)
                core.AddConnection(NodeId(i * 2 - 1), NodeId(i * 2));

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);
            LayoutResult result = DistanceLayout3D.Compute(snapshot);

            // Verify 3D non-collinear distribution across components
            List<Vector3> centroids = new List<Vector3>();
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = (result.Positions[i * 2] + result.Positions[i * 2 + 1]) * 0.5f;
                centroids.Add(c);
            }

            // Check that components span all 3 axes (X, Y, Z)
            float minX = centroids.Min(c => c.X), maxX = centroids.Max(c => c.X);
            float minY = centroids.Min(c => c.Y), maxY = centroids.Max(c => c.Y);
            float minZ = centroids.Min(c => c.Z), maxZ = centroids.Max(c => c.Z);

            float spanX = maxX - minX;
            float spanY = maxY - minY;
            float spanZ = maxZ - minZ;

            Assert(spanX > 2f, "component centroids should distribute across X axis");
            Assert(spanY > 2f, "component centroids should distribute across Y axis");
            Assert(spanZ > 2f, "component centroids should distribute across Z axis");

            // Isolated nodes test
            LRD_Core isoCore = NewCore();
            for (int i = 1; i <= 40; i++)
                AddNode(isoCore, NodeId(i), NodeTypes.Concepts);

            GraphSnapshot isoSnapshot = GraphSnapshot.FromCore(isoCore);
            VisualizerOptions options = new VisualizerOptions();
            LayoutResult isoResult = DistanceLayout3D.Compute(isoSnapshot, options);

            Assert(isoResult.NodeCount == 40, "all isolated nodes should be placed");

            float isoSpanX = isoResult.BoundsMaximum.X - isoResult.BoundsMinimum.X;
            float isoSpanY = isoResult.BoundsMaximum.Y - isoResult.BoundsMinimum.Y;
            float isoSpanZ = isoResult.BoundsMaximum.Z - isoResult.BoundsMinimum.Z;

            Assert(isoSpanX > 2f && isoSpanY > 2f && isoSpanZ > 2f, "isolated cloud should span all 3 dimensions");

            // Verify minimum spacing between all isolated nodes
            for (int a = 0; a < isoResult.Positions.Count; a++)
            {
                for (int b = a + 1; b < isoResult.Positions.Count; b++)
                {
                    float dist = Vector3.Distance(isoResult.Positions[a], isoResult.Positions[b]);
                    Assert(dist >= options.MinSpacing * 0.99f, $"isolated nodes {a} and {b} should respect min spacing (was {dist})");
                }
            }
        }

        /// <summary>Verifies that completed layouts are centered at the origin and report accurate bounds.</summary>
        private static void TestLayoutCenterAndBounds()
        {
            GraphSnapshot snapshot = GraphSnapshot.FromCore(BuildChainCore(6));
            LayoutResult result = DistanceLayout3D.Compute(snapshot);
            Vector3 total = Vector3.Zero;
            float maximumRadius = 0f;

            foreach (Vector3 position in result.Positions)
            {
                total += position;
                maximumRadius = Math.Max(maximumRadius, position.Length());
                Assert(
                    position.X >= result.BoundsMinimum.X && position.X <= result.BoundsMaximum.X
                        && position.Y >= result.BoundsMinimum.Y && position.Y <= result.BoundsMaximum.Y
                        && position.Z >= result.BoundsMinimum.Z && position.Z <= result.BoundsMaximum.Z,
                    "every position should be inside the reported bounds");
            }

            Assert((total / result.Positions.Count).Length() < 1e-3f, "the layout should be centered on the origin");
            Assert(Math.Abs(result.Radius - maximumRadius) < 1e-4f, "radius should match the farthest node");
        }

        /// <summary>Verifies that a pre-cancelled token stops layout computation before running iterations.</summary>
        private static void TestLayoutCancellation()
        {
            GraphSnapshot snapshot = GraphSnapshot.FromCore(BuildChainCore(3));
            LayoutResult result = DistanceLayout3D.Compute(
                snapshot,
                cancellationToken: new CancellationToken(canceled: true));

            Assert(result.Cancelled, "a pre-cancelled token should cancel the layout");
            Assert(!result.Converged, "a cancelled layout should not report convergence");
            Assert(result.Iterations == 0, "a pre-cancelled layout should not run iterations");
            Assert(result.Positions.Count == 3, "a cancelled layout should still return one entry per node");
            Assert(result.Warnings.Count == 1, "cancellation should be reported once");

            foreach (Vector3 position in result.Positions)
                Assert(position == Vector3.Zero, "a pre-cancelled layout should leave zeroed positions");
        }

        /// <summary>Verifies monotonic progress reporting across initialization, relaxation, packing, and completion.</summary>
        private static void TestLayoutProgress()
        {
            GraphSnapshot snapshot = GraphSnapshot.FromCore(BuildChainCore(6));
            RecordingProgress progress = new RecordingProgress();
            LayoutResult result = DistanceLayout3D.Compute(
                snapshot,
                progress: progress);

            Assert(progress.Reports.Count > 2, "layout should report several progress stages");
            Assert(progress.Reports[0].Stage == LayoutStage.Initializing, "layout should start by initializing");
            Assert(progress.Reports[^1].Stage == LayoutStage.Completed, "layout should finish with a completed report");
            Assert(progress.Reports[^1].Fraction == 1d, "the final report should be complete");
            Assert(progress.Reports[^1].MaxIterations == result.Iterations
                || progress.Reports[^1].MaxIterations >= result.Iterations,
                "progress should carry the iteration budget");

            double previousFraction = -1d;
            bool sawRelaxing = false;
            bool sawPacking = false;

            foreach (LayoutProgress report in progress.Reports)
            {
                if (report.Stage == LayoutStage.Relaxing)
                {
                    sawRelaxing = true;
                    Assert(
                        report.Fraction >= previousFraction,
                        "relaxation progress should be monotonic");
                    previousFraction = report.Fraction;
                }
                else if (report.Stage == LayoutStage.Packing)
                {
                    sawPacking = true;
                }
            }

            Assert(sawRelaxing, "layout should report relaxation progress");
            Assert(sawPacking, "layout should report packing progress");
        }

        /// <summary>Verifies that a 400-node chain lays out with finite coordinates within the time budget.</summary>
        private static void TestLayoutPerformance()
        {
            const int nodeCount = 400;
            GraphSnapshot snapshot = GraphSnapshot.FromCore(BuildChainCore(nodeCount));
            LayoutResult result = DistanceLayout3D.Compute(snapshot);

            Assert(result.NodeCount == nodeCount, "the large layout should place every node");
            Assert(result.LinkCount == nodeCount - 1, "the large layout should keep every link");
            Assert(result.ElapsedSeconds < 10d, "a 400-node chain should lay out in well under ten seconds");

            foreach (Vector3 position in result.Positions)
                Assert(IsFinite(position), "large-layout positions should stay finite");
        }

        /// <summary>Verifies that scene geometry batches group meshes by node type and forward link type.</summary>
        private static void TestSceneGeometryBatches()
        {
            LRD_Core core = NewCore();
            Guid firstId = NodeId(1);
            Guid secondId = NodeId(2);
            Guid thirdId = NodeId(3);

            AddNode(core, firstId, NodeTypes.Basics);
            AddNode(core, secondId, NodeTypes.Basics);
            AddNode(core, thirdId, NodeTypes.Concepts);

            int forwardId = NextTypeId();
            ForwardLinkTypes.Register(forwardId, "scene_forward_" + forwardId);
            InverseLinkTypes.Register(forwardId, "scene_inverse_" + forwardId);

            Assert(core.AddConnection(firstId, secondId), "the basic link should be added");
            Assert(
                core.AddDirectedConnection(secondId, thirdId, LinkClass.identities, forwardId),
                "the first custom link should be added");
            Assert(
                core.AddDirectedConnection(firstId, thirdId, LinkClass.events, forwardId),
                "the second custom link should be added");

            GraphSnapshot snapshot = GraphSnapshot.FromCore(core);
            LayoutResult layout = DistanceLayout3D.Compute(snapshot);
            VisualizerOptions options = new VisualizerOptions();
            SceneGeometry geometry = VisualizerSceneBuilder.Build(snapshot, layout, options);

            Assert(ReferenceEquals(geometry.Snapshot, snapshot), "the scene should keep the snapshot");
            Assert(ReferenceEquals(geometry.Layout, layout), "the scene should keep the layout");
            Assert(geometry.NodeCount == 3 && geometry.LinkCount == 3, "scene counts should mirror the snapshot");
            Assert(
                geometry.NodeBatches.Count == snapshot.NodeTypeIds.Count,
                "each node type should produce exactly one batch");
            Assert(
                geometry.NodeBatches.Sum(batch => batch.Mesh.VertexCount) == 3 * 24,
                "node batches should contain one cube per node");

            foreach (NodeMeshBatch batch in geometry.NodeBatches)
            {
                Assert(!batch.Mesh.IsEmpty, "node batches should not be empty");
                Assert(batch.Mesh.VertexCount % 24 == 0, "node batches should be cube aligned");
                Assert(!string.IsNullOrWhiteSpace(batch.NodeTypeName), "node batches should carry a type name");
            }

            Assert(
                geometry.LinkBatches.Count == snapshot.ForwardTypeIds.Count,
                "each forward link type should produce exactly one batch");
            Assert(
                geometry.LinkBatches.Sum(batch => batch.Mesh.VertexCount) == 3 * 8,
                "link batches should contain one quad set per link");

            LinkMeshBatch custom = geometry.LinkBatches.Single(batch => batch.ForwardTypeId == forwardId);
            Assert(
                custom.ClassId == LinkClass.events.Id,
                "a forward type batch should use the minimum class ID as its representative");
            Assert(!string.IsNullOrWhiteSpace(custom.ForwardTypeName), "link batches should carry a type name");
            Assert(
                geometry.NodeBatches.Any(batch => batch.NodeTypeId == NodeTypes.Concepts.Id),
                "node type IDs should be preserved on the batches");
            Assert(geometry.Center == layout.Centroid, "the scene center should follow the layout centroid");
            Assert(geometry.Radius >= layout.Radius && geometry.Radius > 0f, "the scene radius should cover the layout");
            Assert(geometry.HasNodeGeometry, "a populated graph should report node geometry");
        }

        /// <summary>Verifies argument validation on SceneGeometry and VisualizerSceneBuilder.</summary>
        private static void TestSceneGeometryValidation()
        {
            GraphSnapshot snapshot = GraphSnapshot.FromCore(BuildChainCore(3));
            LayoutResult layout = DistanceLayout3D.Compute(snapshot);
            VisualizerOptions options = new VisualizerOptions();
            SceneGeometry geometry = VisualizerSceneBuilder.Build(snapshot, layout, options);

            AssertThrows<ArgumentNullException>(
                () => new SceneGeometry(null!, layout, geometry.NodeBatches, geometry.LinkBatches, 1f),
                "a null snapshot should be rejected");
            AssertThrows<ArgumentNullException>(
                () => new SceneGeometry(snapshot, null!, geometry.NodeBatches, geometry.LinkBatches, 1f),
                "a null layout should be rejected");
            AssertThrows<ArgumentNullException>(
                () => new SceneGeometry(snapshot, layout, null!, geometry.LinkBatches, 1f),
                "null node batches should be rejected");
            AssertThrows<ArgumentNullException>(
                () => new SceneGeometry(snapshot, layout, geometry.NodeBatches, null!, 1f),
                "null link batches should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new SceneGeometry(snapshot, layout, geometry.NodeBatches, geometry.LinkBatches, float.NaN),
                "a non-finite radius should be rejected");
            AssertThrows<ArgumentOutOfRangeException>(
                () => new SceneGeometry(snapshot, layout, geometry.NodeBatches, geometry.LinkBatches, -1f),
                "a negative radius should be rejected");

            GraphSnapshot empty = GraphSnapshot.FromCore(NewCore());
            LayoutResult emptyLayout = DistanceLayout3D.Compute(empty);

            AssertThrows<ArgumentException>(
                () => new SceneGeometry(snapshot, emptyLayout, geometry.NodeBatches, geometry.LinkBatches, 1f),
                "a layout with a mismatched node count should be rejected");
            AssertThrows<ArgumentException>(
                () => VisualizerSceneBuilder.Build(snapshot, emptyLayout, options),
                "the builder should reject a mismatched layout");
            AssertThrows<ArgumentOutOfRangeException>(
                () => VisualizerSceneBuilder.Build(snapshot, layout, new VisualizerOptions { CubeSize = 0d }),
                "the builder should validate options");

            SceneGeometry emptyGeometry = VisualizerSceneBuilder.Build(empty, emptyLayout, options);

            Assert(
                emptyGeometry.NodeBatches.Count == 0 && emptyGeometry.LinkBatches.Count == 0,
                "an empty graph should produce no batches");
            Assert(!emptyGeometry.HasNodeGeometry, "an empty graph should not report node geometry");
            Assert(
                emptyGeometry.Radius == (float)(options.CubeSize * 1.5d),
                "an empty scene radius should fall back to the cube size");
        }

        /// <summary>Builds a linear chain graph with the specified number of connected nodes.</summary>
        private static LRD_Core BuildChainCore(int count)
        {
            LRD_Core core = NewCore();

            for (int index = 1; index <= count; index++)
                AddNode(core, NodeId(index), NodeTypes.Basics);

            for (int index = 1; index < count; index++)
            {
                Assert(
                    core.AddConnection(NodeId(index), NodeId(index + 1)),
                    $"chain link {index} should be added");
            }

            return core;
        }

        /// <summary>Creates a standard perspective camera view fixture for projection tests.</summary>
        private static CameraView CreateTestCamera()
        {
            return new CameraView(
                Vector3.Zero,
                Vector3.UnitZ,
                Vector3.UnitY,
                60d,
                800d,
                600d,
                0.1d,
                1000d);
        }

        /// <summary>Reads a 3D vertex coordinate from a mesh position array by vertex index.</summary>
        private static Vector3 ReadVertex(MeshData mesh, int vertexIndex)
        {
            int offset = vertexIndex * 3;
            return new Vector3(
                mesh.Positions[offset + 0],
                mesh.Positions[offset + 1],
                mesh.Positions[offset + 2]);
        }

        /// <summary>Determines whether two floating-point values are equal within a tolerance.</summary>
        private static bool Near(float actual, float expected, float tolerance = 1e-3f)
        {
            return Math.Abs(actual - expected) <= tolerance;
        }

        /// <summary>Determines whether all components of a 3D vector are finite numbers.</summary>
        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }

        /// <summary>Captures layout progress reports in order for test assertions.</summary>
        private sealed class RecordingProgress : IProgress<LayoutProgress>
        {
            public List<LayoutProgress> Reports { get; } = new List<LayoutProgress>();

            /// <summary>Appends a layout progress update to the recorded list.</summary>
            public void Report(LayoutProgress value)
            {
                Reports.Add(value);
            }
        }
    }
}
