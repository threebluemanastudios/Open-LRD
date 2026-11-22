using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using BigInteger = System.Numerics.BigInteger;
using OPEN_LRD.Foundations;
using OPEN_LRD.Persistence;
using OPEN_LRD.Skills;

namespace OPEN_LRD.SmokeTests
{
    internal static class CoreDiskStreamingTests
    {
        internal static void RunAll()
        {
            Program.Run("Trusted dictionaries stream to and page from binary partitions", CheckpointRoundTripsAndPagesTrustedData());
            Program.Run("Disk checkpoint validation preserves current core on corruption", CorruptCheckpointDoesNotReplaceCore());
            Program.Run("Empty checkpoints and invalid manifest headers are handled", EmptyCheckpointsAndInvalidManifestHeaders());
            Program.Run("Disk validation rejects invalid layouts and presentation records", InvalidLayoutAndPresentationRecordsAreRejected());
            Program.Run("Legacy checkpoints are read-only until atomic upgrade", LegacyCheckpointUpgradesReadOnly());
            Program.Run("Merge layout options honor configurable precedence", MergeLayoutOptionsHonorPrecedence());
            Program.Run("Merge imports reconcile existing container memberships", MergeReconcilesContainerMemberships());
            Program.Run("Truncated checkpoints are rejected without replacing core", TruncatedCheckpointIsRejected());
        }

        private static Action CheckpointRoundTripsAndPagesTrustedData()
        {
            return () =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-disk-" + Guid.NewGuid().ToString("N"));
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    Guid senseId = Guid.NewGuid();
                    Guid usageId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");

                    Guid symbolId = Guid.NewGuid();
                    SymbolBox symbol = new SymbolBox(symbolId, 'd');
                    symbol.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(symbol), "Symbol should be trusted.");
                    Guid wordId = Guid.NewGuid();
                    WordBox word = new WordBox(wordId, senseId, usageId, new List<Guid> { symbolId }, PartOfSpeech.Noun);
                    Program.Assert(core.AddTrustedWord(word), "Word should be trusted.");
                    Guid sentenceId = Guid.NewGuid();
                    SentenceBox sentence = new SentenceBox(sentenceId, senseId, new List<Guid> { wordId });
                    Program.Assert(core.AddTrustedSentence(sentence), "Sentence should be trusted.");

                    Node wordNode = core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!;
                    UInt128 wordPresentationId = core.GetNodePresentationId(wordNode.GetNodeID())!.Value;

                    GroupBox group = new GroupBox(Guid.NewGuid(), "group", new List<Guid> { sentenceId });
                    Program.Assert(core.AddTrustedGroup(group), "Group should be trusted.");
                    SourceBox source = new SourceBox(Guid.NewGuid(), "source", new List<Guid> { group.GetGroupingID() });
                    Program.Assert(core.AddTrustedSource(source), "Source should be trusted.");

                    FunctionBox function = new FunctionBox(Guid.NewGuid(), typeof(double), "Math_2.CalculateCircleArea", "m²");
                    function.AddVariable(new FunctionVariable("radius", 2.5, typeof(double), "m"));
                    MathFunctionDispatcher.Dispatch(function);
                    Program.Assert(core.AddTrustedFunction(function), "Function should be trusted.");

                    FunctionBox fractionFunction = new FunctionBox(typeof(Fraction), "Math_1.SimplifyFraction");
                    fractionFunction.AddVariable(new FunctionVariable("fraction", new Fraction(6, 8), typeof(Fraction)));
                    MathFunctionDispatcher.Dispatch(fractionFunction);
                    Program.Assert(core.AddTrustedFunction(fractionFunction), "Fraction function should be trusted.");
                    FunctionBox bigIntegerFunction = new FunctionBox(typeof(BigInteger), "Math_1.CalculateGCD");
                    bigIntegerFunction.AddVariable(new FunctionVariable("left", BigInteger.Pow(10, 80), typeof(BigInteger)));
                    bigIntegerFunction.AddVariable(new FunctionVariable("right", BigInteger.Pow(10, 60), typeof(BigInteger)));
                    MathFunctionDispatcher.Dispatch(bigIntegerFunction);
                    Program.Assert(core.AddTrustedFunction(bigIntegerFunction), "BigInteger function should be trusted.");
                    FunctionBox vectorFunction = new FunctionBox(typeof(Vector3), "Math_2.AddVectors");
                    vectorFunction.AddVariable(new FunctionVariable("left", new Vector3(1, 2, 3), typeof(Vector3)));
                    vectorFunction.AddVariable(new FunctionVariable("right", new Vector3(4, 5, 6), typeof(Vector3)));
                    MathFunctionDispatcher.Dispatch(vectorFunction);
                    Program.Assert(core.AddTrustedFunction(vectorFunction), "Vector function should be trusted.");
                    FunctionBox arrayFunction = new FunctionBox(typeof(double), "InformationTheoryAndMLMath.Entropy");
                    arrayFunction.AddVariable(new FunctionVariable("probabilities", new[] { 0.25, 0.75 }, typeof(double[])));
                    MathFunctionDispatcher.Dispatch(arrayFunction);
                    Program.Assert(core.AddTrustedFunction(arrayFunction), "Numeric-array function should be trusted.");
                    FunctionDispatchProvenance? fractionProvenance = fractionFunction.DispatchProvenance;
                    Program.Assert(fractionProvenance != null, "dispatch should record in-memory provenance");
                    DateTimeOffset assertedAt = DateTimeOffset.UtcNow;
                    NodeLinkProvenance provenance = new NodeLinkProvenance(
                        Guid.NewGuid(), NodeLinkOrigin.User, assertedAt, "reviewer",
                        source.GetSourceID(), sentenceId, 0.9, assertedAt.AddDays(-1), assertedAt.AddDays(1));
                    Program.Assert(core.AddTrustedLink(
                        core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID(),
                        core.GetTrustedNodeByTarget(NodeTargetKind.Function, function.GetFunctionID())!.GetNodeID(),
                        NodeRelationType.EvaluatesWithFunction, 0.9, "distance", provenance),
                        "Qualified function link should be stored.");

                    List<Guid> extraSymbolIds = new List<Guid>();
                    for (int i = 0; i < 12; i++)
                    {
                        Guid id = Guid.NewGuid();
                        SymbolBox extra = new SymbolBox(id, (char)(0x0100 + i));
                        extra.SetSenseID(senseId);
                        Program.Assert(core.AddTrustedSymbol(extra), "Extra symbol should be trusted.");
                        extraSymbolIds.Add(id);
                    }

                    NodeRepresentation wordRepresentation = core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())!;
                    uint expectedColor = wordRepresentation.GetColor();
                    double expectedSize = wordRepresentation.GetSize();
                    string? expectedLabel = wordRepresentation.GetTextLabel();
                    Vector3 expectedPosition = wordRepresentation.GetPosition();

                    core.StreamTrustedDictionariesToDisk(directory);
                    Program.Assert(File.Exists(Path.Combine(directory, "manifest.lrdmeta")), "Checkpoint should include a manifest.");
                    foreach (string partition in new[] { "symbols", "words", "sentences", "senses", "usages", "groups", "sources", "functions", "nodes", "links", "nodepresentations", "layoutsettings" })
                    {
                        Program.Assert(File.Exists(Path.Combine(directory, partition + ".lrdpart")), $"Missing {partition} data partition.");
                        Program.Assert(File.Exists(Path.Combine(directory, partition + ".lrdidx")), $"Missing {partition} index partition.");
                    }

                    core.StreamTrustedDictionariesFromDisk(directory, true, false, 1);
                    Program.Assert(core.IsDiskPagedReadOnly, "Checkpoint should open in read-only paging mode.");
                    Program.Assert(core.initialized, "A successful disk import should initialize the core.");
                    Program.Assert(core.GetTextFromSentenceID(sentenceId) == "d", "Text resolution should page sentence, word, and symbol data.");
                    NodeRepresentation pagedWordRepresentation = core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())!;
                    Program.Assert(core.GetNodePresentationId(wordNode.GetNodeID()) == wordPresentationId &&
                        pagedWordRepresentation.GetColor() == expectedColor &&
                        pagedWordRepresentation.GetSize() == expectedSize &&
                        pagedWordRepresentation.GetTextLabel() == expectedLabel &&
                        pagedWordRepresentation.GetPosition() == expectedPosition,
                        "Presentation IDs and deterministic derived display fields should survive disk paging.");
                    Program.AssertThrows<InvalidOperationException>(() => pagedWordRepresentation.SetPosition(Vector3.Zero),
                        "Paged NodeRepresentations should be read-only.");
                    Program.Assert(core.GetTrustedSymbolBox(extraSymbolIds[0])?.GetSymbolChar() == (char)0x0100,
                        "Paged symbol getter should preserve records after LRU churn.");
                    Program.Assert(core.GetTrustedSymbolBox(extraSymbolIds[^1])?.GetSymbolChar() == (char)0x010B,
                        "Paged symbol getter should load the final symbol.");
                    Program.Assert(core.GetTrustedFunctionBox(function.FunctionID)?.Variables.Single().Value is double value && value == 2.5,
                        "Function variables should round-trip from disk.");
                    Program.Assert(core.GetTrustedFunctionBox(function.FunctionID)?.Result?.Value is double result && result == Math.PI * 6.25,
                        "Function result should round-trip from disk.");
                    FunctionBox? restoredFractionFunction = core.GetTrustedFunctionBox(fractionFunction.FunctionID);
                    FunctionDispatchProvenance? restoredProvenance = restoredFractionFunction?.DispatchProvenance;
                    Program.Assert(restoredFractionFunction?.Result?.Value is Fraction restoredFraction &&
                                   restoredFraction.Equals(new Fraction(3, 4)) &&
                                   core.GetTrustedFunctionBox(bigIntegerFunction.FunctionID)?.Result?.Value is BigInteger restoredGcd &&
                                   restoredGcd == BigInteger.Pow(10, 60) &&
                                   core.GetTrustedFunctionBox(vectorFunction.FunctionID)?.Result?.Value is Vector3 restoredVector &&
                                   restoredVector == new Vector3(5, 7, 9) &&
                                   core.GetTrustedFunctionBox(arrayFunction.FunctionID)?.Variables.Single().Value is double[] restoredProbabilities &&
                                   restoredProbabilities.SequenceEqual(new[] { 0.25, 0.75 }),
                        "Extended BigInteger, Fraction, Vector3, and numeric-array function values should round-trip in checkpoints.");
                    Program.Assert(restoredProvenance != null &&
                                   restoredProvenance.FunctionKey == fractionProvenance!.FunctionKey &&
                                   restoredProvenance.ArgumentSnapshotHash == fractionProvenance.ArgumentSnapshotHash &&
                                   restoredProvenance.ResultHash == fractionProvenance.ResultHash &&
                                   restoredProvenance.DefinitionRevision == fractionProvenance.DefinitionRevision,
                        "Function dispatch provenance should survive checkpoint round-trip.");
                    Program.Assert(core.GetTrustedGroupsContainingSentence(sentenceId).Contains(group.GetGroupingID()),
                        "Group reverse index should be rebuilt during paging.");
                    Program.Assert(core.GetTrustedSourcesContainingGroup(group.GetGroupingID()).Contains(source.GetSourceID()),
                        "Source reverse index should be rebuilt during paging.");
                    Program.Assert(core.BuildSkillsGraphSnapshot().EdgeCount > 0,
                        "Graph algorithms should receive the trusted graph while boxes are paged.");
                    NodeLink loadedLink = core.GetOutgoingLinks(
                        core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID(), NodeRelationType.EvaluatesWithFunction)
                        .Single();
                    Program.Assert(loadedLink.Label == "distance" && loadedLink.Provenance == provenance,
                        "Weighted labeled link provenance should round-trip exactly.");
                    DiskStoreDiagnostics diagnostics = core.GetDiskStoreDiagnostics()!;
                    Program.Assert(diagnostics.FormatVersion == 3 && diagnostics.CacheMisses > 0 &&
                                   diagnostics.Partitions.Any(partition => partition.Partition == DiskPartition.Links &&
                                       partition.RecordCount >= 1 && partition.DataBytes > 0 && partition.IndexBytes > 0),
                        "Disk diagnostics should report checkpoint version, paged cache activity, and partition sizes.");
                    Program.Assert(core.GetTrustedLinksByProvenanceTarget(source.GetSourceID()).Count == 1,
                        "Provenance reverse indexes should remain queryable in paged mode.");

                    Program.AssertThrows<InvalidOperationException>(() => core.GetTrustedSenseBox(senseId)!.AddSentence(Guid.NewGuid()),
                        "Paged SenseBox should be read-only.");
                    Program.AssertThrows<InvalidOperationException>(() => core.GetTrustedFunctionBox(function.FunctionID)!
                        .AddVariable(new FunctionVariable("y", 1, typeof(int))), "Paged FunctionBox should be read-only.");
                    Program.AssertThrows<InvalidOperationException>(() => core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!
                        .SetLabel("changed"), "Paged Node should be read-only.");
                    Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedSymbol(new SymbolBox(Guid.NewGuid(), 'z')),
                        "Trusted registry mutations should require materialization.");

                    core.MaterializeDiskStore();
                    Program.Assert(!core.IsDiskPagedReadOnly, "Materialization should restore mutable in-memory mode.");
                    NodeRepresentation materializedWordRepresentation = core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())!;
                    Program.Assert(!materializedWordRepresentation.IsReadOnly,
                        "Materialization should restore writable NodeRepresentations.");
                    core.UpdateNodeLayoutOptions(0.35, 0.35);
                    SymbolBox addedSymbol = new SymbolBox(Guid.NewGuid(), 'z');
                    addedSymbol.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(addedSymbol), "Mutation should succeed after materialization.");
                    Vector3 updatedPosition = core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())!.GetPosition();
                    core.FlushTrustedDiskStore();

                    core.OpenDiskStore(directory, 0);
                    Program.Assert(core.GetTrustedSymbolBox(addedSymbol.GetSymbolID())?.GetSymbolChar() == 'z',
                        "Flushed changes should persist and page back from disk.");
                    Program.Assert(core.GetNodeRepresentationByNodeId(wordNode.GetNodeID())?.GetPosition() == updatedPosition &&
                        core.GetNodeLayoutOptions().NodeWeightBias == 0.35 &&
                        core.GetNodeLayoutOptions().NeighborPositionBias == 0.35,
                        "Derived positions and layout options should persist after materialization and flush.");
                    core.StreamTrustedDictionariesFromDisk(directory, false);
                    Program.Assert(!core.IsDiskPagedReadOnly && core.GetTrustedSymbolIDs('z').Count == 1,
                        "Merge import should materialize existing state and avoid duplicate records.");
                    core.ResetCore();
                }
                finally
                {
                    core.ResetCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            };
        }

        private static Action CorruptCheckpointDoesNotReplaceCore()
        {
            return () =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-corrupt-" + Guid.NewGuid().ToString("N"));
                string corruptDirectory = directory + "-bad";
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    Guid senseId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    core.StreamTrustedDictionariesToDisk(directory);
                    Directory.CreateDirectory(corruptDirectory);
                    foreach (string file in Directory.GetFiles(directory))
                        File.Copy(file, Path.Combine(corruptDirectory, Path.GetFileName(file)));
                    string symbolsPath = Path.Combine(corruptDirectory, "symbols.lrdpart");
                    byte[] bytes = File.ReadAllBytes(symbolsPath);
                    bytes[^1] ^= 0x5A;
                    File.WriteAllBytes(symbolsPath, bytes);

                    Program.AssertThrows<InvalidDataException>(() => core.StreamTrustedDictionariesFromDisk(corruptDirectory),
                        "Corrupted partition should be rejected before core replacement.");
                    Program.Assert(core.initialized && core.GetTrustedSenseBox(senseId) != null,
                        "Failed disk import must preserve current trusted state.");
                }
                finally
                {
                    core.ResetCore();
                    core.InitializeCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                    if (Directory.Exists(corruptDirectory)) Directory.Delete(corruptDirectory, true);
                }
            };
        }

        private static Action EmptyCheckpointsAndInvalidManifestHeaders()
        {
            return () =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-empty-" + Guid.NewGuid().ToString("N"));
                string badMagicDirectory = directory + "-bad-magic";
                string badVersionDirectory = directory + "-bad-version";
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> emptyPartitions =
                        Enum.GetValues<DiskPartition>().ToDictionary(
                            partition => partition,
                            _ => (IEnumerable<DiskRecord>)Array.Empty<DiskRecord>());
                    WriteCheckpoint(directory, emptyPartitions);
                    Program.Assert(core.AddTrustedSense(new SenseBox(Guid.NewGuid())),
                        "A retained entry should be added before opening the empty checkpoint.");
                    core.StreamTrustedDictionariesFromDisk(directory);
                    Program.Assert(core.initialized && core.GetTrustedNodeCount() == 0 && core.GetNodePresentationCount() == 0,
                        "Opening an empty checkpoint should replace state with an initialized empty core.");
                    core.MaterializeDiskStore();

                    foreach (string copy in new[] { badMagicDirectory, badVersionDirectory })
                    {
                        Directory.CreateDirectory(copy);
                        foreach (string file in Directory.GetFiles(directory))
                            File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
                    }
                    byte[] manifest = File.ReadAllBytes(Path.Combine(badMagicDirectory, "manifest.lrdmeta"));
                    manifest[0] ^= 0x7F;
                    File.WriteAllBytes(Path.Combine(badMagicDirectory, "manifest.lrdmeta"), manifest);
                    manifest = File.ReadAllBytes(Path.Combine(badVersionDirectory, "manifest.lrdmeta"));
                    BitConverter.GetBytes(99).CopyTo(manifest, 4);
                    File.WriteAllBytes(Path.Combine(badVersionDirectory, "manifest.lrdmeta"), manifest);

                    Guid retainedSenseId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(retainedSenseId)),
                        "A retained entry should be added before rejecting invalid manifests.");
                    foreach (string invalid in new[] { badMagicDirectory, badVersionDirectory })
                    {
                        Program.AssertThrows<InvalidDataException>(() => core.StreamTrustedDictionariesFromDisk(invalid),
                            "Invalid manifest magic/version should be rejected.");
                        Program.Assert(core.GetTrustedSenseBox(retainedSenseId) != null,
                            "Invalid manifest headers must leave the current core untouched.");
                    }
                }
                finally
                {
                    core.ResetCore();
                    foreach (string path in new[] { directory, badMagicDirectory, badVersionDirectory })
                        if (Directory.Exists(path)) Directory.Delete(path, true);
                }
            };
        }

        private static Action LegacyCheckpointUpgradesReadOnly()
        {
            return () =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-legacy-" + Guid.NewGuid().ToString("N"));
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    Guid senseId = Guid.NewGuid();
                    Guid usageId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");
                    Guid symbolId = Guid.NewGuid();
                    SymbolBox symbol = new SymbolBox(symbolId, 'l');
                    symbol.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(symbol), "Symbol should be trusted.");
                    Guid wordId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedWord(new WordBox(wordId, senseId, usageId,
                        new List<Guid> { symbolId }, PartOfSpeech.Noun)), "Word should be trusted.");
                    Program.Assert(core.AddTrustedSentence(new SentenceBox(Guid.NewGuid(), senseId,
                        new List<Guid> { wordId })), "Sentence should be trusted.");
                    core.StreamTrustedDictionariesToDisk(directory);
                    ConvertCheckpointToLegacy(directory);

                    core.StreamTrustedDictionariesFromDisk(directory, true, false, 1);
                    Program.Assert(core.IsLegacyDiskStoreReadOnly && core.IsDiskPagedReadOnly,
                        "Opening a legacy checkpoint should enter read-only migration mode.");
                    Program.Assert(core.GetNodeLayoutOptions().NodeWeightBias == NodeLayoutOptions.DefaultBias &&
                                   core.GetNodeLayoutOptions().NeighborPositionBias == NodeLayoutOptions.DefaultBias,
                        "Legacy checkpoints without layout settings should use documented defaults.");
                    Program.Assert(core.GetNodePresentationCount() == core.GetTrustedNodeCount(),
                        "Legacy checkpoints without a presentation partition should receive default NodeRepresentations.");
                    Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedSymbol(new SymbolBox(Guid.NewGuid(), 'x')),
                        "Trusted data mutations should be rejected until a legacy checkpoint is upgraded.");
                    Program.AssertThrows<InvalidOperationException>(() => core.FlushTrustedDiskStore(),
                        "Flush should be rejected until a legacy checkpoint is upgraded.");
                    core.MaterializeDiskStore();
                    Program.Assert(core.IsLegacyDiskStoreReadOnly && !core.IsDiskPagedReadOnly,
                        "Materializing a legacy checkpoint must not make it writable.");
                    Program.AssertThrows<InvalidOperationException>(() => core.AddTrustedSymbol(new SymbolBox(Guid.NewGuid(), 'x')),
                        "Materialized legacy data should remain read-only until upgrade.");

                    core.UpgradeLegacyDiskStore();
                    Program.Assert(!core.IsLegacyDiskStoreReadOnly && !core.IsDiskPagedReadOnly,
                        "A successful upgrade should install the current writable format.");
                    core.OpenDiskStore(directory, 1);
                    Program.Assert(core.GetNodePresentationCount() == core.GetTrustedNodeCount(),
                        "Legacy upgrade should persist synthesized NodeRepresentations.");
                    Program.Assert(core.GetNodeLayoutOptions().NodeWeightBias == NodeLayoutOptions.DefaultBias &&
                                   core.GetNodeLayoutOptions().NeighborPositionBias == NodeLayoutOptions.DefaultBias,
                        "Legacy upgrade should persist default layout options.");
                    NodeLink structural = core.GetOutgoingLinks(
                        core.GetTrustedNodeByTarget(NodeTargetKind.Symbol, symbolId)!.GetNodeID(), NodeRelationType.HasSense).Single();
                    Program.Assert(structural.Provenance.Origin == NodeLinkOrigin.LegacyImport && structural.Weight == 1.0,
                        "Legacy links should retain deterministic legacy provenance through upgrade.");
                }
                finally
                {
                    core.ResetCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            };
        }

        private static Action MergeLayoutOptionsHonorPrecedence()
        {
            return () =>
            {
                string baseDirectory = Path.Combine(Path.GetTempPath(), "open-lrd-layout-base-" + Guid.NewGuid().ToString("N"));
                string incomingDirectory = Path.Combine(Path.GetTempPath(), "open-lrd-layout-incoming-" + Guid.NewGuid().ToString("N"));
                string incomingWithoutOptions = Path.Combine(Path.GetTempPath(), "open-lrd-layout-defaults-" + Guid.NewGuid().ToString("N"));
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = Guid.NewGuid();
                Guid usageId = Guid.NewGuid();
                Guid symbolId = Guid.NewGuid();
                Guid wordId = Guid.NewGuid();
                Guid sentenceId = Guid.NewGuid();
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    AddLayoutMergeGraph(core, senseId, usageId, symbolId, wordId, sentenceId);
                    core.UpdateNodeLayoutOptions(0.1, 0.15);
                    core.StreamTrustedDictionariesToDisk(baseDirectory);

                    core.ResetCore();
                    core.InitializeCore();
                    AddLayoutMergeGraph(core, senseId, usageId, symbolId, wordId, sentenceId);
                    core.UpdateNodeLayoutOptions(0.4, 0.45);
                    core.StreamTrustedDictionariesToDisk(incomingDirectory);
                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> noOptions = ReadCheckpointPartitions(incomingDirectory);
                    noOptions[DiskPartition.LayoutSettings] = Array.Empty<DiskRecord>();
                    WriteCheckpoint(incomingWithoutOptions, noOptions);

                    core.ResetCore();
                    core.OpenDiskStore(baseDirectory);
                    core.MaterializeDiskStore();
                    Guid wordNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID();
                    core.StreamTrustedDictionariesFromDisk(incomingDirectory, replaceExisting: false);
                    Program.Assert(core.GetNodeLayoutOptions().NodeWeightBias == 0.1 &&
                                   core.GetNodeLayoutOptions().NeighborPositionBias == 0.15,
                        "Base layout options should win by default during merge.");
                    Program.Assert(core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition() ==
                                   core.GetNodeLayoutResult().Positions[wordNodeId],
                        "Base-preference merge should recalculate positions with the base options.");

                    core.ResetCore();
                    core.OpenDiskStore(baseDirectory);
                    core.MaterializeDiskStore();
                    wordNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID();
                    Vector3 basePosition = core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition();
                    core.StreamTrustedDictionariesFromDisk(incomingDirectory, replaceExisting: false,
                        layoutOptionsMergePreference: LayoutOptionsMergePreference.IncomingWins);
                    Program.Assert(core.GetNodeLayoutOptions().NodeWeightBias == 0.4 &&
                                   core.GetNodeLayoutOptions().NeighborPositionBias == 0.45,
                        "IncomingWins should restore incoming layout options before recalculation.");
                    Program.Assert(core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition() != basePosition &&
                                   core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition() ==
                                   core.GetNodeLayoutResult().Positions[wordNodeId],
                        "Incoming-preference merge should recalculate base Nodes with incoming options before install.");

                    core.ResetCore();
                    core.OpenDiskStore(baseDirectory);
                    core.MaterializeDiskStore();
                    wordNodeId = core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID();
                    core.StreamTrustedDictionariesFromDisk(incomingWithoutOptions, replaceExisting: false,
                        layoutOptionsMergePreference: LayoutOptionsMergePreference.IncomingWins);
                    Program.Assert(core.GetNodeLayoutOptions().NodeWeightBias == NodeLayoutOptions.DefaultBias &&
                                   core.GetNodeLayoutOptions().NeighborPositionBias == NodeLayoutOptions.DefaultBias,
                        "IncomingWins should use defaults when the incoming checkpoint has no settings record.");
                    Program.Assert(core.GetNodeRepresentationByNodeId(wordNodeId)!.GetPosition() ==
                                   core.GetNodeLayoutResult().Positions[wordNodeId],
                        "Incoming default options should be applied before merged positions are recalculated.");
                }
                finally
                {
                    core.ResetCore();
                    foreach (string directory in new[] { baseDirectory, incomingDirectory, incomingWithoutOptions })
                        if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            };
        }

        private static void AddLayoutMergeGraph(
            LRD_Core core,
            Guid senseId,
            Guid usageId,
            Guid symbolId,
            Guid wordId,
            Guid sentenceId)
        {
            Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Layout merge sense should be trusted.");
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Layout merge usage should be trusted.");
            SymbolBox symbol = new SymbolBox(symbolId, 'q');
            symbol.SetSenseID(senseId);
            Program.Assert(core.AddTrustedSymbol(symbol), "Layout merge symbol should be trusted.");
            Program.Assert(core.AddTrustedWord(new WordBox(wordId, senseId, usageId,
                new List<Guid> { symbolId }, PartOfSpeech.Noun)), "Layout merge word should be trusted.");
            Program.Assert(core.AddTrustedSentence(new SentenceBox(sentenceId, senseId, new List<Guid> { wordId })),
                "Layout merge sentence should be trusted.");
        }

        private static Action InvalidLayoutAndPresentationRecordsAreRejected()
        {
            return () =>
            {
                string sourceDirectory = Path.Combine(Path.GetTempPath(), "open-lrd-record-source-" + Guid.NewGuid().ToString("N"));
                string invalidLayoutDirectory = sourceDirectory + "-layout";
                string missingPresentationDirectory = sourceDirectory + "-missing-presentation";
                string malformedPresentationDirectory = sourceDirectory + "-malformed-presentation";
                string duplicatePresentationDirectory = sourceDirectory + "-duplicate-presentation";
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    Program.Assert(core.AddTrustedSense(new SenseBox(Guid.NewGuid())), "Source sense should be trusted.");
                    core.StreamTrustedDictionariesToDisk(sourceDirectory);

                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> invalidLayout = ReadCheckpointPartitions(sourceDirectory);
                    DiskRecord settingsRecord = invalidLayout[DiskPartition.LayoutSettings].Single();
                    byte[] invalidOptions = (byte[])settingsRecord.Payload.Clone();
                    BitConverter.GetBytes(0.75).CopyTo(invalidOptions, 17);
                    invalidLayout[DiskPartition.LayoutSettings] = new[] { new DiskRecord(settingsRecord.Key, invalidOptions) };
                    WriteCheckpoint(invalidLayoutDirectory, invalidLayout);

                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> missingPresentation = ReadCheckpointPartitions(sourceDirectory);
                    List<DiskRecord> presentationRecords = missingPresentation[DiskPartition.NodePresentations].ToList();
                    Program.Assert(presentationRecords.Count > 0, "Source checkpoint should contain a presentation record.");
                    presentationRecords.RemoveAt(0);
                    missingPresentation[DiskPartition.NodePresentations] = presentationRecords;
                    WriteCheckpoint(missingPresentationDirectory, missingPresentation);

                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> duplicatePresentation = ReadCheckpointPartitions(sourceDirectory);
                    presentationRecords = duplicatePresentation[DiskPartition.NodePresentations].ToList();
                    DiskRecord originalPresentation = presentationRecords[0];
                    presentationRecords.Add(new DiskRecord(Guid.NewGuid(), (byte[])originalPresentation.Payload.Clone()));
                    duplicatePresentation[DiskPartition.NodePresentations] = presentationRecords;
                    WriteCheckpoint(duplicatePresentationDirectory, duplicatePresentation);

                    // v3 extensibility (10.16): explicitly versioned/extensible records may carry unknown trailing bytes.
                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> extensiblePresentation = ReadCheckpointPartitions(sourceDirectory);
                    List<DiskRecord> extensibleRecords = extensiblePresentation[DiskPartition.NodePresentations].ToList();
                    DiskRecord extensibleOriginal = extensibleRecords[0];
                    extensibleRecords[0] = new DiskRecord(extensibleOriginal.Key,
                        extensibleOriginal.Payload.Concat(new byte[] { 0x5A }).ToArray());
                    extensiblePresentation[DiskPartition.NodePresentations] = extensibleRecords;
                    WriteCheckpoint(malformedPresentationDirectory, extensiblePresentation);

                    core.ResetCore();
                    core.InitializeCore();
                    Guid retainedSenseId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(retainedSenseId)), "Retained sense should be trusted.");
                    foreach (string invalidDirectory in new[]
                             {
                                 invalidLayoutDirectory,
                                 missingPresentationDirectory,
                                 duplicatePresentationDirectory
                             })
                    {
                        Program.AssertThrows<InvalidDataException>(
                            () => core.StreamTrustedDictionariesFromDisk(invalidDirectory),
                            "Invalid layout/presentation data should fail staged checkpoint validation.");
                        Program.Assert(core.GetTrustedSenseBox(retainedSenseId) != null,
                            "Rejected checkpoint data must not replace the current core.");
                    }
                    core.ResetCore();
                    core.StreamTrustedDictionariesFromDisk(malformedPresentationDirectory);
                    Program.Assert(core.GetTrustedNodeCount() > 0,
                        "Extensible v3 records with unknown trailing bytes should be accepted.");
                }
                finally
                {
                    core.ResetCore();
                    foreach (string directory in new[]
                             {
                                 sourceDirectory,
                                 invalidLayoutDirectory,
                                 missingPresentationDirectory,
                                 malformedPresentationDirectory,
                                 duplicatePresentationDirectory
                             })
                    {
                        if (Directory.Exists(directory)) Directory.Delete(directory, true);
                    }
                }
            };
        }

        private static Dictionary<DiskPartition, IEnumerable<DiskRecord>> ReadCheckpointPartitions(string directory)
        {
            using PartitionedBinaryStore store = PartitionedBinaryStore.Open(directory);
            return Enum.GetValues<DiskPartition>().ToDictionary(
                partition => partition,
                partition => (IEnumerable<DiskRecord>)store.ReadAll(partition).ToList());
        }

        private static void WriteCheckpoint(
            string directory,
            IReadOnlyDictionary<DiskPartition, IEnumerable<DiskRecord>> partitions)
        {
            using PartitionedBinaryStore store = PartitionedBinaryStore.Create(directory, partitions);
            store.Validate();
        }

        private static Action MergeReconcilesContainerMemberships()
        {
            return () =>
            {
                string baseDirectory = Path.Combine(Path.GetTempPath(), "open-lrd-merge-base-" + Guid.NewGuid().ToString("N"));
                string incomingDirectory = Path.Combine(Path.GetTempPath(), "open-lrd-merge-incoming-" + Guid.NewGuid().ToString("N"));
                LRD_Core core = LRD_Core.Instance;
                Guid senseId = Guid.NewGuid();
                Guid usageId = Guid.NewGuid();
                Guid firstSymbolId = Guid.NewGuid();
                Guid firstWordId = Guid.NewGuid();
                Guid firstSentenceId = Guid.NewGuid();
                Guid firstGroupId = Guid.NewGuid();
                Guid secondSymbolId = Guid.NewGuid();
                Guid secondWordId = Guid.NewGuid();
                Guid secondSentenceId = Guid.NewGuid();
                Guid secondGroupId = Guid.NewGuid();
                Guid sourceId = Guid.NewGuid();
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    AddMergeBaseData(core, senseId, usageId, firstSymbolId, firstWordId,
                        firstSentenceId, firstGroupId, sourceId, secondSymbolId, secondWordId,
                        secondSentenceId, secondGroupId, includeSecondSentence: false);
                    core.StreamTrustedDictionariesToDisk(baseDirectory);

                    core.ResetCore();
                    core.InitializeCore();
                    AddMergeBaseData(core, senseId, usageId, firstSymbolId, firstWordId,
                        firstSentenceId, firstGroupId, sourceId, secondSymbolId, secondWordId,
                        secondSentenceId, secondGroupId, includeSecondSentence: true);
                    core.StreamTrustedDictionariesToDisk(incomingDirectory);

                    core.ResetCore();
                    core.OpenDiskStore(baseDirectory, 1);
                    core.MaterializeDiskStore();
                    Node untrustedNode = new Node(NodeTargetKind.Function, Guid.NewGuid());
                    Program.Assert(core.SetUntrustedNode(untrustedNode), "Existing untrusted node should be staged before merge.");
                    core.StreamTrustedDictionariesFromDisk(incomingDirectory, false);
                    Program.Assert(core.GetTrustedGroupBox(firstGroupId)!.GetSentenceCount() == 2,
                        "Incoming sentence memberships should append to an existing trusted group.");
                    Program.Assert(core.GetTrustedGroupBox(secondGroupId)!.ContainsSentence(secondSentenceId),
                        "Incoming new groups should retain their sentence memberships.");
                    Program.Assert(core.GetTrustedSourceBox(sourceId)!.GetGroupingCount() == 2 &&
                                   core.GetTrustedSourcesContainingGroup(secondGroupId).Contains(sourceId),
                        "Incoming group memberships should append to an existing trusted source and reverse index.");
                    Program.Assert(core.GetTrustedSenseBox(senseId)!.ContainsSentence(secondSentenceId) &&
                                    core.GetTrustedUsageBox(usageId)!.ContainsSentence(secondSentenceId),
                         "Incoming SenseBox and UsageBox memberships should be reconciled.");
                    Node mergedSecondWordNode = core.GetTrustedNodeByTarget(NodeTargetKind.Word, secondWordId)!;
                    NodeRepresentation mergedIncomingRepresentation = core.GetNodeRepresentationByNodeId(mergedSecondWordNode.GetNodeID())!;
                    Program.Assert(mergedIncomingRepresentation.GetColor() != 0xFF00ABCD &&
                        mergedIncomingRepresentation.GetSize() >= 0.6 && mergedIncomingRepresentation.GetSize() <= 2.0 &&
                        mergedIncomingRepresentation.GetTextLabel()!.Contains("[", StringComparison.Ordinal) &&
                        mergedIncomingRepresentation.GetPosition().IsFinite,
                        "Merge should recalculate presentation data for newly imported trusted nodes.");
                    NodeRepresentation retainedUntrustedRepresentation = core.GetNodeRepresentationByNodeId(untrustedNode.GetNodeID())!;
                    Program.Assert(core.GetUntrustedNode(untrustedNode.GetNodeID()) != null &&
                        retainedUntrustedRepresentation.GetColor() != 0xFFABCDEF &&
                        retainedUntrustedRepresentation.GetSize() >= 0.6 && retainedUntrustedRepresentation.GetSize() <= 2.0 &&
                        retainedUntrustedRepresentation.GetTextLabel()!.StartsWith("Function ", StringComparison.Ordinal) &&
                        retainedUntrustedRepresentation.GetPosition().IsFinite,
                        "Merge should recalculate pre-existing untrusted NodeRepresentation fields.");
                }
                finally
                {
                    core.ResetCore();
                    if (Directory.Exists(baseDirectory)) Directory.Delete(baseDirectory, true);
                    if (Directory.Exists(incomingDirectory)) Directory.Delete(incomingDirectory, true);
                }
            };
        }

        private static void AddMergeBaseData(
            LRD_Core core,
            Guid senseId,
            Guid usageId,
            Guid firstSymbolId,
            Guid firstWordId,
            Guid firstSentenceId,
            Guid firstGroupId,
            Guid sourceId,
            Guid secondSymbolId,
            Guid secondWordId,
            Guid secondSentenceId,
            Guid secondGroupId,
            bool includeSecondSentence)
        {
            Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Merge SenseBox should be trusted.");
            Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Merge UsageBox should be trusted.");
            SymbolBox firstSymbol = new SymbolBox(firstSymbolId, 'm');
            firstSymbol.SetSenseID(senseId);
            Program.Assert(core.AddTrustedSymbol(firstSymbol), "First merge symbol should be trusted.");
            Program.Assert(core.AddTrustedWord(new WordBox(firstWordId, senseId, usageId,
                new List<Guid> { firstSymbolId }, PartOfSpeech.Noun)), "First merge word should be trusted.");
            Program.Assert(core.AddTrustedSentence(new SentenceBox(firstSentenceId, senseId,
                new List<Guid> { firstWordId })), "First merge sentence should be trusted.");

            List<Guid> firstGroupSentences = new List<Guid> { firstSentenceId };
            List<Guid> sourceGroups = new List<Guid> { firstGroupId };
            if (includeSecondSentence)
            {
                SymbolBox secondSymbol = new SymbolBox(secondSymbolId, 'n');
                secondSymbol.SetSenseID(senseId);
                Program.Assert(core.AddTrustedSymbol(secondSymbol), "Second merge symbol should be trusted.");
                Program.Assert(core.AddTrustedWord(new WordBox(secondWordId, senseId, usageId,
                    new List<Guid> { secondSymbolId }, PartOfSpeech.Noun)), "Second merge word should be trusted.");
                NodeRepresentation secondWordRepresentation = core.GetNodeRepresentationByNodeId(
                    core.GetTrustedNodeByTarget(NodeTargetKind.Word, secondWordId)!.GetNodeID())!;
                secondWordRepresentation.SetColor(0xFF00ABCD);
                secondWordRepresentation.SetSize(2.0);
                secondWordRepresentation.SetTextLabel("incoming word model");
                secondWordRepresentation.SetPosition(1.0, 2.0, 3.0);
                Program.Assert(core.AddTrustedSentence(new SentenceBox(secondSentenceId, senseId,
                    new List<Guid> { secondWordId })), "Second merge sentence should be trusted.");
                firstGroupSentences.Add(secondSentenceId);
                sourceGroups.Add(secondGroupId);
                Program.Assert(core.AddTrustedGroup(new GroupBox(secondGroupId,
                    new List<Guid> { secondSentenceId })), "Second merge group should be trusted.");
            }

            Program.Assert(core.AddTrustedGroup(new GroupBox(firstGroupId, firstGroupSentences)),
                "First merge group should be trusted.");
            Program.Assert(core.AddTrustedSource(new SourceBox(sourceId, sourceGroups)),
                "Merge source should be trusted.");
        }

        private static void ConvertCheckpointToLegacy(string directory)
        {
            Dictionary<DiskPartition, (int Count, byte[] DataHash, byte[] IndexHash)> manifest =
                new Dictionary<DiskPartition, (int Count, byte[] DataHash, byte[] IndexHash)>();
            foreach (DiskPartition partition in Enum.GetValues<DiskPartition>())
            {
                string dataPath = Path.Combine(directory, partition.ToString().ToLowerInvariant() + ".lrdpart");
                string indexPath = Path.Combine(directory, partition.ToString().ToLowerInvariant() + ".lrdidx");
                if (partition is DiskPartition.NodePresentations or DiskPartition.LayoutSettings)
                {
                    File.Delete(dataPath);
                    File.Delete(indexPath);
                    continue;
                }
                byte[] data = File.ReadAllBytes(dataPath);
                byte[] index = File.ReadAllBytes(indexPath);
                WriteInt32(data, 4, 1);
                WriteInt32(index, 4, 1);
                int recordCount = ReadInt32(index, 8);
                if (partition == DiskPartition.Links)
                {
                    (data, index) = ConvertLinkPartition(data, index, recordCount);
                }
                else if (partition == DiskPartition.Symbols)
                {
                    (data, index) = ConvertSymbolPartition(data, recordCount);
                }
                File.WriteAllBytes(dataPath, data);
                File.WriteAllBytes(indexPath, index);
                manifest.Add(partition, (recordCount, SHA256.HashData(data), SHA256.HashData(index)));
            }

            using FileStream stream = new FileStream(Path.Combine(directory, "manifest.lrdmeta"), FileMode.Create, FileAccess.Write);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(0x4D524C4F);
            writer.Write(1);
            writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            writer.Write(manifest.Count);
            foreach (DiskPartition partition in manifest.Keys.OrderBy(partition => partition))
            {
                (int count, byte[] dataHash, byte[] indexHash) = manifest[partition];
                writer.Write((byte)partition);
                writer.Write(count);
                writer.Write(dataHash);
                writer.Write(indexHash);
            }
        }

        private static (byte[] Data, byte[] Index) ConvertLinkPartition(byte[] data, byte[] index, int recordCount)
        {
            using MemoryStream input = new MemoryStream(data, false);
            using BinaryReader reader = new BinaryReader(input);
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();
            _ = reader.ReadByte();
            using MemoryStream dataOutput = new MemoryStream();
            using BinaryWriter dataWriter = new BinaryWriter(dataOutput);
            dataWriter.Write(0x44524C4F);
            dataWriter.Write(1);
            dataWriter.Write((byte)DiskPartition.Links);
            List<(Guid Key, long Offset, int Length)> indexEntries = new List<(Guid Key, long Offset, int Length)>();
            for (int i = 0; i < recordCount; i++)
            {
                Guid key = new Guid(reader.ReadBytes(16));
                int payloadLength = reader.ReadInt32();
                byte[] payload = reader.ReadBytes(payloadLength);
                _ = reader.ReadUInt32();
                int legacyLength = GetLegacyLinkPayloadLength(payload);
                byte[] legacyPayload = payload.AsSpan(0, legacyLength).ToArray();
                long offset = dataOutput.Position;
                dataWriter.Write(key.ToByteArray());
                dataWriter.Write(legacyPayload.Length);
                dataWriter.Write(legacyPayload);
                dataWriter.Write(CalculateCrc32(legacyPayload));
                indexEntries.Add((key, offset, legacyPayload.Length));
            }

            using MemoryStream indexOutput = new MemoryStream();
            using BinaryWriter indexWriter = new BinaryWriter(indexOutput);
            indexWriter.Write(0x49524C4F);
            indexWriter.Write(1);
            indexWriter.Write(recordCount);
            foreach ((Guid key, long offset, int length) in indexEntries)
            {
                indexWriter.Write(key.ToByteArray());
                indexWriter.Write(offset);
                indexWriter.Write(length);
            }
            return (dataOutput.ToArray(), indexOutput.ToArray());
        }

        private static (byte[] Data, byte[] Index) ConvertSymbolPartition(byte[] data, int recordCount)
        {
            using MemoryStream input = new MemoryStream(data, false);
            using BinaryReader reader = new BinaryReader(input);
            _ = reader.ReadInt32();
            _ = reader.ReadInt32();
            _ = reader.ReadByte();
            using MemoryStream dataOutput = new MemoryStream();
            using BinaryWriter dataWriter = new BinaryWriter(dataOutput);
            dataWriter.Write(0x44524C4F);
            dataWriter.Write(1);
            dataWriter.Write((byte)DiskPartition.Symbols);
            List<(Guid Key, long Offset, int Length)> indexEntries = new List<(Guid Key, long Offset, int Length)>();
            for (int i = 0; i < recordCount; i++)
            {
                Guid key = new Guid(reader.ReadBytes(16));
                int payloadLength = reader.ReadInt32();
                byte[] payload = reader.ReadBytes(payloadLength);
                _ = reader.ReadUInt32();
                byte[] legacyPayload = ConvertSymbolPayload(payload);
                long offset = dataOutput.Position;
                dataWriter.Write(key.ToByteArray());
                dataWriter.Write(legacyPayload.Length);
                dataWriter.Write(legacyPayload);
                dataWriter.Write(CalculateCrc32(legacyPayload));
                indexEntries.Add((key, offset, legacyPayload.Length));
            }

            using MemoryStream indexOutput = new MemoryStream();
            using BinaryWriter indexWriter = new BinaryWriter(indexOutput);
            indexWriter.Write(0x49524C4F);
            indexWriter.Write(1);
            indexWriter.Write(recordCount);
            foreach ((Guid key, long offset, int length) in indexEntries)
            {
                indexWriter.Write(key.ToByteArray());
                indexWriter.Write(offset);
                indexWriter.Write(length);
            }
            return (dataOutput.ToArray(), indexOutput.ToArray());
        }

        private static byte[] ConvertSymbolPayload(byte[] payload)
        {
            using MemoryStream input = new MemoryStream(payload, false);
            using BinaryReader reader = new BinaryReader(input);
            byte[] id = reader.ReadBytes(16);
            _ = reader.ReadBoolean();
            char value = (char)reader.ReadUInt16();
            byte[] trailingData = reader.ReadBytes(checked((int)(input.Length - input.Position)));
            using MemoryStream output = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(output);
            writer.Write(id);
            writer.Write(value);
            writer.Write(trailingData);
            return output.ToArray();
        }

        private static int GetLegacyLinkPayloadLength(byte[] payload)
        {
            using MemoryStream stream = new MemoryStream(payload, false);
            using BinaryReader reader = new BinaryReader(stream);
            _ = reader.ReadBytes(36);
            _ = reader.ReadDouble();
            if (reader.ReadBoolean()) _ = reader.ReadString();
            return checked((int)stream.Position);
        }

        private static uint CalculateCrc32(ReadOnlySpan<byte> bytes)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte value in bytes)
            {
                crc ^= value;
                for (int bit = 0; bit < 8; bit++)
                {
                    uint mask = (uint)-(int)(crc & 1);
                    crc = (crc >> 1) ^ (0xEDB88320 & mask);
                }
            }
            return ~crc;
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return BitConverter.ToInt32(data, offset);
        }

        private static void WriteInt32(byte[] data, int offset, int value)
        {
            BitConverter.GetBytes(value).CopyTo(data, offset);
        }

        private static Action TruncatedCheckpointIsRejected()
        {
            return () =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-truncated-" + Guid.NewGuid().ToString("N"));
                string truncatedDirectory = directory + "-truncated";
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    Guid senseId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    core.StreamTrustedDictionariesToDisk(directory);
                    Directory.CreateDirectory(truncatedDirectory);
                    foreach (string file in Directory.GetFiles(directory))
                        File.Copy(file, Path.Combine(truncatedDirectory, Path.GetFileName(file)));
                    string symbolsPath = Path.Combine(truncatedDirectory, "symbols.lrdpart");
                    byte[] bytes = File.ReadAllBytes(symbolsPath);
                    File.WriteAllBytes(symbolsPath, bytes.AsSpan(0, bytes.Length / 2).ToArray());

                    Program.AssertThrows<InvalidDataException>(() => core.StreamTrustedDictionariesFromDisk(truncatedDirectory),
                        "Truncated partition should be rejected before core replacement.");
                    Program.Assert(core.GetTrustedSenseBox(senseId) != null,
                        "Failed truncated import must preserve current trusted state.");
                }
                finally
                {
                    core.ResetCore();
                    core.InitializeCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                    if (Directory.Exists(truncatedDirectory)) Directory.Delete(truncatedDirectory, true);
                }
            };
        }
    }
}
