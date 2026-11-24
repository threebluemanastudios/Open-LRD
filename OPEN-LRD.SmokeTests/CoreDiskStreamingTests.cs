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
            Program.Run("Malformed foundation payloads are rejected deterministically", MalformedFoundationPayloadsAreRejected());
            Program.Run("In-memory and disk-paged modes maintain query parity", PagedModeParityMatchesInMemoryMode());
            Program.Run("Index partitions are hashed as written and reject tampering", IndexPartitionsHashAsWrittenAndRejectTampering());
            Program.Run("Compaction rewrites the attached store", CompactRewritesAttachedStore());
            Program.Run("Failed store rewrites preserve the original v3 and legacy stores", FailedRewritePreservesOriginalStores());
            Program.Run("Structural index and data-header corruption is rejected", StructuralPartitionCorruptionIsRejected());
            Program.Run("Provenance-bearing link trailing bytes follow the v3 and legacy read policy", LinkProvenanceTrailingBytesFollowReadPolicy());
            Program.Run("Provenance-bearing FunctionBox trailing bytes follow the v3 and legacy read policy", FunctionProvenanceTrailingBytesFollowReadPolicy());
        }

        private static Action CompactRewritesAttachedStore()
        {
            return () =>
            {
                string directory = UniqueTempDirectory("open-lrd-compact");
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    List<Guid> senseIds = AddSenses(core, 3);
                    core.StreamTrustedDictionariesToDisk(directory);

                    core.OpenDiskStore(directory, 0);
                    core.MaterializeDiskStore();
                    Guid extraSenseId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(extraSenseId)), "Sense added after opening should be trusted.");
                    core.CompactTrustedDiskStore();
                    core.ResetCore();
                    core.InitializeCore();
                    core.StreamTrustedDictionariesFromDisk(directory);
                    Program.Assert(senseIds.Append(extraSenseId).All(id => core.GetTrustedSenseBox(id) != null),
                        "Compaction should persist every sense, including the one added after opening.");
                    Program.Assert(NoSiblingDirectories(directory),
                        "Compaction should leave no staging or backup directories behind: " + string.Join(", ", LeftoverSiblings(directory)));
                }
                finally
                {
                    core.ResetCore();
                    DeleteIfExists(directory);
                }
            };
        }

        // Compaction and legacy upgrade both finish by calling PartitionedBinaryStore.Create. A record source that
        // fails mid-write exercises that shared failure path deterministically. Core-level failure injection is not
        // possible without a test seam: records are validated when a store attaches, so on-disk corruption cannot
        // reach the rewrite through the public API.
        private static Action FailedRewritePreservesOriginalStores()
        {
            return () =>
            {
                string v3Directory = UniqueTempDirectory("open-lrd-failed-v3");
                string legacyDirectory = UniqueTempDirectory("open-lrd-failed-legacy");
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    List<Guid> v3SenseIds = AddSenses(core, 3);
                    core.StreamTrustedDictionariesToDisk(v3Directory);
                    core.ResetCore();
                    core.InitializeCore();
                    AddSenses(core, 2);
                    core.StreamTrustedDictionariesToDisk(legacyDirectory);
                    ConvertCheckpointToLegacy(legacyDirectory);

                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> partitions = BuildFailingRewritePartitions();
                    foreach (string directory in new[] { v3Directory, legacyDirectory })
                    {
                        string before = FileFingerprint(directory);
                        Program.AssertThrows<IOException>(() => PartitionedBinaryStore.Create(directory, partitions),
                            "A failing record source should abort the rewrite.");
                        Program.Assert(FileFingerprint(directory) == before,
                            "A failed rewrite must leave the original store files byte-identical.");
                        Program.Assert(NoSiblingDirectories(directory),
                            "A failed rewrite must not leave staging or backup directories behind.");
                    }

                    core.ResetCore();
                    core.InitializeCore();
                    core.StreamTrustedDictionariesFromDisk(v3Directory);
                    Program.Assert(v3SenseIds.All(id => core.GetTrustedSenseBox(id) != null) && !core.IsLegacyDiskStoreReadOnly,
                        "The v3 store should still open with its original contents.");
                    core.ResetCore();
                    core.InitializeCore();
                    core.StreamTrustedDictionariesFromDisk(legacyDirectory, true, false, 0);
                    Program.Assert(core.IsLegacyDiskStoreReadOnly,
                        "The legacy store should still open read-only after a failed rewrite.");
                }
                finally
                {
                    core.ResetCore();
                    DeleteIfExists(v3Directory);
                    DeleteIfExists(legacyDirectory);
                }
            };
        }

        private static Dictionary<DiskPartition, IEnumerable<DiskRecord>> BuildFailingRewritePartitions()
        {
            byte[] payload = FoundationBinarySerializer.SerializeSense(new SenseBox(Guid.NewGuid()));
            DiskRecord[] records =
            {
                new DiskRecord(Guid.NewGuid(), payload),
                new DiskRecord(Guid.NewGuid(), payload),
                new DiskRecord(Guid.NewGuid(), payload),
            };
            return Enum.GetValues<DiskPartition>().ToDictionary(
                partition => partition,
                partition => partition == DiskPartition.Senses
                    ? FailAfterFirstRecord(records)
                    : (IEnumerable<DiskRecord>)Array.Empty<DiskRecord>());
        }

        private static IEnumerable<DiskRecord> FailAfterFirstRecord(IEnumerable<DiskRecord> source)
        {
            int yielded = 0;
            foreach (DiskRecord record in source)
            {
                if (yielded == 1) throw new IOException("Simulated write failure after the first record.");
                yield return record;
                yielded++;
            }
        }

        // Read policy (CoreMisses gap 6, decision 10.16): v3 readers interpret a recognized provenance block and
        // ignore unknown bytes after the last recognized field. Legacy (v1/v2) readers do not interpret provenance
        // at all. A recognized but truncated block is rejected as InvalidDataException.
        private static Action LinkProvenanceTrailingBytesFollowReadPolicy()
        {
            return () =>
            {
                Guid assertionId = Guid.NewGuid();
                NodeLinkProvenance provenance = new NodeLinkProvenance(assertionId, NodeLinkOrigin.User,
                    DateTimeOffset.UtcNow, "policy-author", confidence: 0.5);
                NodeLink link = new NodeLink(Guid.NewGuid(), Guid.NewGuid(), NodeRelationType.Custom, 0.5, "policy", provenance);
                byte[] payload = FoundationBinarySerializer.SerializeLink(link);

                NodeLink roundTrip = FoundationBinarySerializer.DeserializeLink(payload);
                Program.Assert(roundTrip.Provenance.AssertionId == assertionId && roundTrip.Provenance.Author == "policy-author" &&
                               roundTrip.Provenance.Origin == NodeLinkOrigin.User,
                    "v3 links should round-trip their provenance block.");

                NodeLink legacy = FoundationBinarySerializer.DeserializeLink(payload, legacyFormat: true);
                Program.Assert(legacy.Provenance.Origin == NodeLinkOrigin.LegacyImport && legacy.Provenance.Author == null,
                    "Legacy readers must not interpret provenance bytes; they should synthesize legacy provenance.");
                Program.Assert(legacy.Weight == 0.5 && legacy.Label == "policy" && legacy.SourceNodeId == link.SourceNodeId,
                    "Legacy readers should still read the shared link fields.");

                byte[] unknownTrailing = payload.Concat(new byte[] { 0x5A, 0x5A }).ToArray();
                NodeLink extended = FoundationBinarySerializer.DeserializeLink(unknownTrailing);
                Program.Assert(extended.Provenance.AssertionId == assertionId && extended.Provenance.Author == "policy-author",
                    "Unknown bytes after the final recognized link field should be ignored.");

                byte[] olderWriter = payload.Take(payload.Length - 1).ToArray();
                NodeLink older = FoundationBinarySerializer.DeserializeLink(olderWriter);
                Program.Assert(older.Provenance.AssertionId == assertionId && older.Provenance.SourceGroupId == null,
                    "A payload that omits the optional trailing source-group block should be accepted.");

                byte[] truncatedBlock = payload.Take(payload.Length - 1).Append((byte)1).ToArray();
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeLink(truncatedBlock),
                    "A source-group flag without its GUID is a truncated recognized block and must be rejected.");
            };
        }

        private static Action FunctionProvenanceTrailingBytesFollowReadPolicy()
        {
            return () =>
            {
                FunctionDispatchProvenance active = new FunctionDispatchProvenance("Math_1.AddDouble", "args-a", "result-a",
                    typeof(double).FullName ?? "double", "m", 1L, DateTimeOffset.UtcNow, "policy-dispatcher");
                FunctionDispatchProvenance earlier = new FunctionDispatchProvenance("Math_1.AddDouble", "args-b", "result-b",
                    typeof(double).FullName ?? "double", "m", 1L, DateTimeOffset.UtcNow.AddMinutes(-5), "policy-dispatcher");
                FunctionBox withProvenance = new FunctionBox(Guid.NewGuid(), typeof(double), "Math_1.AddDouble");
                withProvenance.ReplaceDispatchProvenanceHistory(new[] { earlier, active }, active);
                byte[] payload = FoundationBinarySerializer.SerializeFunction(withProvenance);

                FunctionBox roundTrip = FoundationBinarySerializer.DeserializeFunction(payload);
                Program.Assert(roundTrip.DispatchProvenance != null && roundTrip.DispatchProvenance.Equals(active) &&
                               roundTrip.DispatchProvenanceHistory.Count == 2,
                    "v3 function records should round-trip active provenance and its history.");

                FunctionBox legacy = FoundationBinarySerializer.DeserializeFunction(payload, legacyFormat: true);
                Program.Assert(legacy.DispatchProvenance == null && legacy.DispatchProvenanceHistory.Count == 0,
                    "Legacy readers must not interpret function provenance bytes.");

                byte[] unknownTrailing = payload.Concat(new byte[] { 0x5A, 0x5A, 0x5A }).ToArray();
                FunctionBox extended = FoundationBinarySerializer.DeserializeFunction(unknownTrailing);
                Program.Assert(extended.DispatchProvenance != null && extended.DispatchProvenance.Equals(active) &&
                               extended.DispatchProvenanceHistory.Count == 2,
                    "Unknown bytes after the provenance history should be ignored by v3 readers.");

                FunctionBox plain = new FunctionBox(Guid.NewGuid(), typeof(double), "Math_1.AddDouble");
                byte[] plainPayload = FoundationBinarySerializer.SerializeFunction(plain);
                FunctionBox plainExtended = FoundationBinarySerializer.DeserializeFunction(
                    plainPayload.Concat(new byte[] { 0x00, 0x7F }).ToArray());
                Program.Assert(plainExtended.DispatchProvenance == null && plainExtended.GetFunctionID() == plain.GetFunctionID(),
                    "Unknown trailing bytes after a provenance-less function record should be ignored.");

                byte[] truncated = payload.Take(payload.Length - 10).ToArray();
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeFunction(truncated),
                    "A provenance block cut short inside its last entry must be rejected.");
            };
        }

        private static Action StructuralPartitionCorruptionIsRejected()
        {
            // Each case tampers with one structural field, then rehashes the manifest entry so the
            // hash check passes and the structural validators (or lazy record reads) must reject it.
            List<(string Name, Action<byte[], byte[]> Mutate)> cases = new List<(string, Action<byte[], byte[]>)>
            {
                ("index magic", (data, index) => index[0] ^= 0xFF),
                ("index version", (data, index) => WriteInt32(index, 4, 99)),
                ("index count inflated", (data, index) => WriteInt32(index, 8, ReadInt32(index, 8) + 1)),
                ("index entry key cleared", (data, index) => Array.Clear(index, 12, 16)),
                ("index entry offset past end", (data, index) => BitConverter.GetBytes((long)data.Length + 1000).CopyTo(index, 28)),
                ("index entry length mismatch", (data, index) => WriteInt32(index, 36, ReadInt32(index, 36) + 1)),
                ("data magic", (data, index) => data[0] ^= 0xFF),
                ("data version mismatch", (data, index) => WriteInt32(data, 4, 2)),
                ("data partition id", (data, index) => data[8] = (byte)DiskPartition.Symbols),
                ("record key mismatch", (data, index) => data[(int)BitConverter.ToInt64(index, 28)] ^= 0x01),
                ("record CRC mismatch", (data, index) =>
                {
                    long recordOffset = BitConverter.ToInt64(index, 28);
                    int payloadLength = ReadInt32(index, 36);
                    data[recordOffset + 20 + payloadLength] ^= 0x01;
                }),
            };

            return () =>
            {
                string directory = UniqueTempDirectory("open-lrd-structural");
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    AddSenses(core, 3);
                    core.StreamTrustedDictionariesToDisk(directory);

                    foreach ((string name, Action<byte[], byte[]> mutate) in cases)
                    {
                        string caseDirectory = directory + "-" + name.Replace(' ', '-');
                        try
                        {
                            CopyDirectory(directory, caseDirectory);
                            string dataPath = Path.Combine(caseDirectory, "senses.lrdpart");
                            string indexPath = Path.Combine(caseDirectory, "senses.lrdidx");
                            byte[] data = File.ReadAllBytes(dataPath);
                            byte[] index = File.ReadAllBytes(indexPath);
                            mutate(data, index);
                            File.WriteAllBytes(dataPath, data);
                            File.WriteAllBytes(indexPath, index);
                            RehashPartitionInManifest(caseDirectory, DiskPartition.Senses);

                            core.ResetCore();
                            core.InitializeCore();
                            Guid markerId = Guid.NewGuid();
                            Program.Assert(core.AddTrustedSense(new SenseBox(markerId)), "Marker sense should be trusted.");
                            Program.AssertThrows<InvalidDataException>(() => core.StreamTrustedDictionariesFromDisk(caseDirectory),
                                $"Corruption case '{name}' should be rejected.");
                            Program.Assert(core.initialized && core.GetTrustedSenseBox(markerId) != null,
                                $"Corruption case '{name}' must leave the current core state untouched.");
                        }
                        finally
                        {
                            DeleteIfExists(caseDirectory);
                        }
                    }
                }
                finally
                {
                    core.ResetCore();
                    DeleteIfExists(directory);
                }
            };
        }

        private static string UniqueTempDirectory(string prefix)
        {
            return Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"));
        }

        private static void DeleteIfExists(string directory)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        private static List<Guid> AddSenses(LRD_Core core, int count)
        {
            List<Guid> senseIds = new List<Guid>(count);
            for (int i = 0; i < count; i++)
            {
                Guid senseId = Guid.NewGuid();
                Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                senseIds.Add(senseId);
            }
            return senseIds;
        }

        // Concatenated name:SHA-256 pairs for every file in a store directory, so any byte change is detected.
        private static string FileFingerprint(string directory)
        {
            return string.Join("|", Directory.GetFiles(directory).OrderBy(file => file, StringComparer.Ordinal)
                .Select(file => Path.GetFileName(file) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))));
        }

        // True when no staging or backup sibling of the store directory remains.
        private static bool NoSiblingDirectories(string directory) => !LeftoverSiblings(directory).Any();

        // Matches the exact prefixes PartitionedBinaryStore.Create uses. A "name.*" wildcard is avoided because
        // Win32-style matching also returns the directory itself.
        private static IEnumerable<string> LeftoverSiblings(string directory)
        {
            string parent = Path.GetDirectoryName(directory)!;
            string name = Path.GetFileName(directory);
            return Directory.GetDirectories(parent)
                .Where(path => Path.GetFileName(path).StartsWith(name + ".staging-", StringComparison.Ordinal) ||
                               Path.GetFileName(path).StartsWith(name + ".previous-", StringComparison.Ordinal));
        }

        // Recomputes the SHA-256 data and index hashes for one partition in manifest.lrdmeta. Manifest layout:
        // magic(4) version(4) timestamp(8) count(4), then per entry partition(1) recordCount(4) dataHash(32) indexHash(32).
        private static void RehashPartitionInManifest(string directory, DiskPartition partition)
        {
            string manifestPath = Path.Combine(directory, "manifest.lrdmeta");
            byte[] manifest = File.ReadAllBytes(manifestPath);
            int count = ReadInt32(manifest, 16);
            string name = partition.ToString().ToLowerInvariant();
            for (int i = 0; i < count; i++)
            {
                int entry = 20 + i * 69;
                if ((DiskPartition)manifest[entry] != partition) continue;
                SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name + ".lrdpart"))).CopyTo(manifest, entry + 5);
                SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, name + ".lrdidx"))).CopyTo(manifest, entry + 37);
                File.WriteAllBytes(manifestPath, manifest);
                return;
            }
            throw new InvalidOperationException($"Manifest has no entry for {partition}.");
        }

        private static Action IndexPartitionsHashAsWrittenAndRejectTampering()
        {
            return () =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-index-hash-" + Guid.NewGuid().ToString("N"));
                string tamperedDirectory = directory + "-tampered";
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    List<Guid> senseIds = new List<Guid>();
                    for (int i = 0; i < 256; i++)
                    {
                        Guid senseId = Guid.NewGuid();
                        senseIds.Add(senseId);
                        Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    }

                    core.StreamTrustedDictionariesToDisk(directory);
                    Program.Assert(Directory.GetFiles(directory, "*.body").Length == 0,
                        "Temporary index body files must not survive a checkpoint write.");

                    core.ResetCore();
                    core.InitializeCore();
                    core.StreamTrustedDictionariesFromDisk(directory);
                    Program.Assert(senseIds.All(senseId => core.GetTrustedSenseBox(senseId) != null),
                        "Every indexed sense should round-trip through the index partition.");

                    Directory.CreateDirectory(tamperedDirectory);
                    foreach (string file in Directory.GetFiles(directory))
                        File.Copy(file, Path.Combine(tamperedDirectory, Path.GetFileName(file)));
                    string indexPath = Path.Combine(tamperedDirectory, "senses.lrdidx");
                    byte[] bytes = File.ReadAllBytes(indexPath);
                    bytes[^1] ^= 0x01;
                    File.WriteAllBytes(indexPath, bytes);

                    core.ResetCore();
                    core.InitializeCore();
                    Guid markerId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(markerId)), "Marker sense should be trusted before import.");
                    Program.AssertThrows<InvalidDataException>(() => core.StreamTrustedDictionariesFromDisk(tamperedDirectory),
                        "A single tampered index byte must fail the index hash check.");
                    Program.Assert(core.initialized && core.GetTrustedSenseBox(markerId) != null,
                        "A rejected index import must leave the previously loaded core state untouched.");
                }
                finally
                {
                    core.ResetCore();
                    core.InitializeCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                    if (Directory.Exists(tamperedDirectory)) Directory.Delete(tamperedDirectory, true);
                }
            };
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
                    core.ValidateDiskStore();
                    DiskStoreDiagnostics diagnostics = core.GetDiskStoreDiagnostics()!;
                    Program.Assert(diagnostics.FormatVersion == 3 && diagnostics.CacheMisses > 0 &&
                                   diagnostics.Partitions.Any(partition => partition.Partition == DiskPartition.Links &&
                                       partition.RecordCount >= 1 && partition.DataBytes > 0 && partition.IndexBytes > 0),
                        "Disk diagnostics should report checkpoint version, paged cache activity, and partition sizes.");
                    Program.Assert(diagnostics.LastValidationProvenanceSummary != null &&
                                   diagnostics.LastValidationProvenanceSummary.VerifiedProvenanceCount >= 1 &&
                                   diagnostics.LastValidationProvenanceSummary.TamperedProvenanceCount == 0,
                        "Diagnostics should expose provenance validation counts after validation.");
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

        private static Action MalformedFoundationPayloadsAreRejected()
        {
            return () =>
            {
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeSymbol(Array.Empty<byte>()),
                    "Empty payloads must be rejected deterministically.");
                Guid symbolId = Guid.NewGuid();
                SymbolBox symbol = new SymbolBox(symbolId, 'a');
                byte[] symbolPayload = FoundationBinarySerializer.SerializeSymbol(symbol);
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeSymbol(symbolPayload.AsSpan(0, symbolPayload.Length - 1).ToArray()),
                    "Truncated payloads must be rejected deterministically.");

                byte[] badStringLength = FoundationBinarySerializer.SerializeFunction(new FunctionBox(Guid.NewGuid(), typeof(int), "Math_1.Add", null));
                byte[] corrupted = (byte[])badStringLength.Clone();
                // Corrupt a type tag/length byte inside payload to make it malformed, not just trailing.
                corrupted[Math.Min(20, corrupted.Length - 1)] = 0xFF;
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeFunction(corrupted),
                    "Malformed payload content must be rejected deterministically.");

                byte[] invalidUtf8 = (byte[])symbolPayload.Clone();
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeUsage(new byte[] { 0, 1, 2, 3, 4 }),
                    "Malformed payload headers must be rejected deterministically.");
                Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeWord(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }),
                    "Malformed truncated word payloads must be rejected deterministically.");

                // Allocation guard and malformed payload boundary tests (mismatched lengths, invalid UTF-8, boundary dates/numerics)
                using (MemoryStream badUtf8Stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(badUtf8Stream))
                {
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write(true);
                    writer.Write((byte)2);
                    writer.Write(new byte[] { 0xC3, 0x28 }); // invalid UTF-8 sequence
                    writer.Write(0);
                    Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeGroup(badUtf8Stream.ToArray()),
                        "Invalid UTF-8 bytes in foundation strings must throw InvalidDataException.");
                }

                using (MemoryStream oversizedBigIntStream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(oversizedBigIntStream))
                {
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write((byte)0); // object type
                    writer.Write(false);
                    writer.Write(false);
                    writer.Write(1); // 1 variable
                    writer.Write((byte)1);
                    writer.Write((byte)'x');
                    writer.Write((byte)19); // BigInteger tag
                    writer.Write(500_000); // claims 500KB but stream ends immediately
                    Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeFunction(oversizedBigIntStream.ToArray()),
                        "BigInteger length exceeding remaining stream bytes must be rejected before allocation.");
                }

                using (MemoryStream oversizedArrayStream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(oversizedArrayStream))
                {
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write((byte)0);
                    writer.Write(false);
                    writer.Write(false);
                    writer.Write(1);
                    writer.Write((byte)1);
                    writer.Write((byte)'a');
                    writer.Write((byte)24); // double[] tag
                    writer.Write(900_000); // claims 900k doubles but stream has no bytes remaining
                    Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeFunction(oversizedArrayStream.ToArray()),
                        "Numeric array count exceeding remaining stream bytes must be rejected before allocation.");
                }

                using (MemoryStream invalidDateStream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(invalidDateStream))
                {
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write((int)NodeRelationType.Custom);
                    writer.Write(0.5);
                    writer.Write(false);
                    writer.Write(Guid.NewGuid().ToByteArray());
                    writer.Write((byte)NodeLinkOrigin.User);
                    writer.Write(long.MaxValue); // invalid DateTimeOffset ticks
                    writer.Write((short)0);
                    Program.AssertThrows<InvalidDataException>(() => FoundationBinarySerializer.DeserializeLink(invalidDateStream.ToArray()),
                        "Out-of-range DateTimeOffset ticks must be rejected as InvalidDataException.");
                }

                // PartitionedBinaryStore duplicate keys. Failed compact and upgrade preservation is covered by
                // "Compaction rewrites the attached store and preserves it on failure" and
                // "Failed legacy upgrade preserves the original store".
                string dupDir = Path.Combine(Path.GetTempPath(), "open-lrd-dup-" + Guid.NewGuid().ToString("N"));
                try
                {
                    Guid dupKey = Guid.NewGuid();
                    byte[] validSensePayload = FoundationBinarySerializer.SerializeSense(new SenseBox(dupKey));
                    Dictionary<DiskPartition, IEnumerable<DiskRecord>> badPartitions = new Dictionary<DiskPartition, IEnumerable<DiskRecord>>
                    {
                        [DiskPartition.Senses] = new[]
                        {
                            new DiskRecord(dupKey, validSensePayload),
                            new DiskRecord(dupKey, validSensePayload)
                        }
                    };
                    Program.AssertThrows<InvalidDataException>(() => PartitionedBinaryStore.Create(dupDir, badPartitions),
                        "Duplicate record keys within a partition must be rejected deterministically.");
                    Program.Assert(!Directory.Exists(dupDir) &&
                                   Directory.GetDirectories(Path.GetTempPath(), Path.GetFileName(dupDir) + ".staging-*").Length == 0,
                        "Failed checkpoint creation must clean up staging directories and leave no partial store.");
                }
                finally
                {
                    if (Directory.Exists(dupDir)) Directory.Delete(dupDir, true);
                }

                string preserveDir = Path.Combine(Path.GetTempPath(), "open-lrd-preserve-" + Guid.NewGuid().ToString("N"));
                string badIncomingDir = Path.Combine(Path.GetTempPath(), "open-lrd-bad-incoming-" + Guid.NewGuid().ToString("N"));
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();
                    Guid senseId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    FunctionBox verifiedFn = new FunctionBox(Guid.NewGuid(), typeof(int), "Math_1.AddInt");
                    verifiedFn.AddVariable(new FunctionVariable("a", 2, typeof(int)));
                    verifiedFn.AddVariable(new FunctionVariable("b", 3, typeof(int)));
                    MathFunctionDispatcher.Dispatch(verifiedFn);
                    Program.Assert(core.AddTrustedFunction(verifiedFn), "Verified function should be trusted.");
                    core.StreamTrustedDictionariesToDisk(preserveDir);

                    // Open in paged mode and check validation provenance summary diagnostics
                    core.StreamTrustedDictionariesFromDisk(preserveDir, replaceExisting: true, preloadAllIntoMemory: false, maxCachedRecordsPerPartition: 16);
                    DiskStoreDiagnostics? diag = core.GetDiskStoreDiagnostics();
                    Program.Assert(diag != null &&
                                   diag.LastValidationProvenanceSummary != null &&
                                   diag.LastValidationProvenanceSummary.VerifiedProvenanceCount == 1 &&
                                   diag.LastValidationProvenanceSummary.UnverifiedProvenanceCount == 0 &&
                                   diag.LastValidationProvenanceSummary.TamperedProvenanceCount == 0 &&
                                   diag.LastValidationProvenanceSummary.VerifiedFunctionIds != null &&
                                   diag.LastValidationProvenanceSummary.VerifiedFunctionIds.Contains(verifiedFn.GetFunctionID()),
                        "DiskStoreDiagnostics should expose per-status provenance counts and function IDs on open and validate.");

                    // Prepare corrupted incoming directory and verify failed merge preserves original state and cleans staging
                    Directory.CreateDirectory(badIncomingDir);
                    File.WriteAllBytes(Path.Combine(badIncomingDir, "manifest.lrdmeta"), new byte[] { 1, 2, 3, 4 });
                    core.MaterializeDiskStore();
                    Program.AssertThrows<InvalidDataException>(() => core.StreamTrustedDictionariesFromDisk(badIncomingDir, replaceExisting: false),
                        "Merging a corrupted store must throw InvalidDataException.");
                    Program.Assert(core.GetTrustedSenseBox(senseId) != null &&
                                   core.GetTrustedFunctionBox(verifiedFn.GetFunctionID()) != null,
                        "Failed merge must preserve existing in-memory trusted state.");
                }
                finally
                {
                    core.ResetCore();
                    if (Directory.Exists(preserveDir)) Directory.Delete(preserveDir, true);
                    if (Directory.Exists(badIncomingDir)) Directory.Delete(badIncomingDir, true);
                }
            };
        }

        private static Action PagedModeParityMatchesInMemoryMode()
        {
            return () =>
            {
                string directory = Path.Combine(Path.GetTempPath(), "open-lrd-paged-parity-" + Guid.NewGuid().ToString("N"));
                LRD_Core core = LRD_Core.Instance;
                try
                {
                    core.ResetCore();
                    core.InitializeCore();

                    Guid senseId = Guid.NewGuid();
                    Guid usageId = Guid.NewGuid();
                    Program.Assert(core.AddTrustedSense(new SenseBox(senseId)), "Sense should be trusted.");
                    Program.Assert(core.AddTrustedUsage(new UsageBox(usageId)), "Usage should be trusted.");

                    Guid symbolAId = Guid.NewGuid();
                    SymbolBox symbolA = new SymbolBox(symbolAId, 'p');
                    symbolA.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(symbolA), "Symbol P should be trusted.");

                    Guid symbolBId = Guid.NewGuid();
                    SymbolBox symbolB = new SymbolBox(symbolBId, 'q');
                    symbolB.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(symbolB), "Symbol Q should be trusted.");

                    Guid wordId = Guid.NewGuid();
                    WordBox word = new WordBox(wordId, senseId, usageId, new List<Guid> { symbolAId, symbolBId }, PartOfSpeech.Noun);
                    Program.Assert(core.AddTrustedWord(word), "Word PQ should be trusted.");

                    Guid sentenceId = Guid.NewGuid();
                    SentenceBox sentence = new SentenceBox(sentenceId, senseId, new List<Guid> { wordId });
                    Program.Assert(core.AddTrustedSentence(sentence), "Sentence should be trusted.");

                    Guid groupId = Guid.NewGuid();
                    GroupBox group = new GroupBox(groupId, "ParityGroup", new List<Guid> { sentenceId });
                    Program.Assert(core.AddTrustedGroup(group), "Group should be trusted.");

                    Guid sourceId = Guid.NewGuid();
                    SourceBox source = new SourceBox(sourceId, "ParitySource", new List<Guid> { groupId });
                    Program.Assert(core.AddTrustedSource(source), "Source should be trusted.");

                    FunctionBox function = new FunctionBox(Guid.NewGuid(), typeof(double), "Math_2.CalculateCircleArea", null);
                    function.AddVariable(new FunctionVariable("radius", 2.0, typeof(double), "m"));
                    MathFunctionDispatcher.Dispatch(function);
                    Program.Assert(core.AddTrustedFunction(function), "Function should be trusted.");

                    Node wordNode = core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!;
                    Node funcNode = core.GetTrustedNodeByTarget(NodeTargetKind.Function, function.GetFunctionID())!;
                    Guid wordNodeId = wordNode.GetNodeID();
                    Guid funcNodeId = funcNode.GetNodeID();

                    NodeLinkProvenance provenance = NodeLinkProvenance.CreateReasoning(
                        wordNodeId, funcNodeId, NodeRelationType.EvaluatesWithFunction, "eval", 0.95, "parity-tester",
                        sourceTargetId: sourceId, evidenceTargetId: sentenceId, validFrom: null, validUntil: null);
                    Program.Assert(core.AddTrustedLink(wordNodeId, funcNodeId, NodeRelationType.EvaluatesWithFunction, 0.95, "eval", provenance),
                        "Qualified evaluation link should be added.");
                    core.UpdateNodeLayoutOptions(0.25, 0.35);

                    // In-memory baseline captures
                    string inMemorySentenceText = core.GetTextFromSentenceID(sentenceId);
                    string inMemoryWordText = core.GetTextFromWordID(wordId);
                    WordBox? inMemoryWordBox = core.GetWordByGuid(wordId);
                    List<Node> inMemoryAllNodes = core.FindTrustedNodes();
                    List<Node> inMemoryWordNodes = core.FindTrustedNodes(NodeTargetKind.Word);
                    List<NodeLink> inMemoryLinks = core.GetAllTrustedLinks();
                    List<NodeLink> inMemoryOutgoing = core.GetOutgoingLinks(wordNodeId, NodeRelationType.EvaluatesWithFunction);
                    List<NodeLink> inMemoryIncoming = core.GetIncomingLinks(funcNodeId, NodeRelationType.EvaluatesWithFunction);
                    List<NodeLink> inMemoryProvenance = core.GetTrustedLinksByProvenanceTarget(sourceId);
                    List<NodeLink> inMemoryEvidenceProvenance = core.GetTrustedLinksByProvenanceTarget(sentenceId, includeSourceTarget: false);
                    TrustedKnowledgeGraphSnapshot inMemorySnapshot = core.BuildTrustedKnowledgeGraphSnapshot();
                    Graph<Guid> inMemoryCostGraph = core.BuildSkillsCostGraphSnapshot();
                    NodeLayoutOptions inMemoryLayoutOptions = core.GetNodeLayoutOptions();
                    NodeRepresentation? inMemoryWordRep = core.GetNodeRepresentationByNodeId(wordNodeId);
                    Vector3 inMemoryPos = inMemoryWordRep?.GetPosition() ?? Vector3.Zero;
                    uint inMemoryColor = inMemoryWordRep?.GetColor() ?? 0;

                    // Stream to disk and open in read-only paged mode
                    core.StreamTrustedDictionariesToDisk(directory);
                    core.StreamTrustedDictionariesFromDisk(directory, true, false, 0);
                    Program.Assert(core.IsDiskPagedReadOnly, "Core should be in disk-paged read-only mode.");

                    // Parity assertions under paged mode
                    string pagedSentenceText = core.GetTextFromSentenceID(sentenceId);
                    Program.Assert(pagedSentenceText == inMemorySentenceText && pagedSentenceText == "pq",
                        "GetTextFromSentenceID should have exact parity in paged mode.");

                    string pagedWordText = core.GetTextFromWordID(wordId);
                    Program.Assert(pagedWordText == inMemoryWordText && pagedWordText == "pq",
                        "GetTextFromWordID should have exact parity in paged mode.");

                    WordBox? pagedWordBox = core.GetWordByGuid(wordId);
                    Program.Assert(pagedWordBox != null && pagedWordBox.GetWordID() == wordId &&
                                   pagedWordBox.GetSenseID() == senseId && pagedWordBox.GetUsageID() == usageId,
                        "GetWordByGuid should have exact parity in paged mode.");

                    List<Node> pagedAllNodes = core.FindTrustedNodes();
                    Program.Assert(pagedAllNodes.Count == inMemoryAllNodes.Count,
                        "FindTrustedNodes() count should have parity in paged mode.");
                    for (int i = 0; i < inMemoryAllNodes.Count; i++)
                    {
                        Program.Assert(pagedAllNodes[i].GetNodeID() == inMemoryAllNodes[i].GetNodeID() &&
                                       pagedAllNodes[i].GetTargetKind() == inMemoryAllNodes[i].GetTargetKind() &&
                                       pagedAllNodes[i].GetTargetID() == inMemoryAllNodes[i].GetTargetID(),
                            "FindTrustedNodes() deterministic ordering and content must match in paged mode.");
                    }

                    List<Node> pagedWordNodes = core.FindTrustedNodes(NodeTargetKind.Word);
                    Program.Assert(pagedWordNodes.Count == inMemoryWordNodes.Count &&
                                   pagedWordNodes[0].GetNodeID() == wordNodeId,
                        "FindTrustedNodes(kind) should filter correctly in paged mode.");

                    List<NodeLink> pagedLinks = core.GetAllTrustedLinks();
                    Program.Assert(pagedLinks.Count == inMemoryLinks.Count,
                        "GetAllTrustedLinks count should have parity in paged mode.");

                    List<NodeLink> pagedOutgoing = core.GetOutgoingLinks(wordNodeId, NodeRelationType.EvaluatesWithFunction);
                    Program.Assert(pagedOutgoing.Count == inMemoryOutgoing.Count &&
                                   pagedOutgoing[0].TargetNodeId == funcNodeId &&
                                   pagedOutgoing[0].Provenance.AssertionId == provenance.AssertionId,
                        "GetOutgoingLinks should have parity in paged mode.");

                    List<NodeLink> pagedIncoming = core.GetIncomingLinks(funcNodeId, NodeRelationType.EvaluatesWithFunction);
                    Program.Assert(pagedIncoming.Count == inMemoryIncoming.Count &&
                                   pagedIncoming[0].SourceNodeId == wordNodeId,
                        "GetIncomingLinks should have parity in paged mode.");

                    List<NodeLink> pagedProvenance = core.GetTrustedLinksByProvenanceTarget(sourceId);
                    Program.Assert(pagedProvenance.Count == inMemoryProvenance.Count &&
                                   pagedProvenance[0].Provenance.AssertionId == provenance.AssertionId,
                        "GetTrustedLinksByProvenanceTarget should match in paged mode.");

                    List<NodeLink> pagedEvidenceProvenance = core.GetTrustedLinksByProvenanceTarget(sentenceId, includeSourceTarget: false);
                    Program.Assert(pagedEvidenceProvenance.Count == inMemoryEvidenceProvenance.Count &&
                                   pagedEvidenceProvenance[0].Provenance.AssertionId == provenance.AssertionId,
                        "GetTrustedLinksByProvenanceTarget evidence-only lookup should match in paged mode.");

                    TrustedKnowledgeGraphSnapshot pagedSnapshot = core.BuildTrustedKnowledgeGraphSnapshot();
                    Program.Assert(pagedSnapshot.Nodes.Count == inMemorySnapshot.Nodes.Count &&
                                   pagedSnapshot.Links.Count == inMemorySnapshot.Links.Count,
                        "BuildTrustedKnowledgeGraphSnapshot should match in paged mode.");

                    Graph<Guid> pagedCostGraph = core.BuildSkillsCostGraphSnapshot();
                    Program.Assert(pagedCostGraph.NodeCount == inMemoryCostGraph.NodeCount &&
                                   pagedCostGraph.EdgeCount == inMemoryCostGraph.EdgeCount,
                        "BuildSkillsCostGraphSnapshot should match in paged mode.");

                    NodeLayoutOptions pagedLayoutOptions = core.GetNodeLayoutOptions();
                    Program.Assert(pagedLayoutOptions.NodeWeightBias == inMemoryLayoutOptions.NodeWeightBias &&
                                   pagedLayoutOptions.NeighborPositionBias == inMemoryLayoutOptions.NeighborPositionBias,
                        "GetNodeLayoutOptions should match in paged mode.");

                    NodeRepresentation? pagedWordRep = core.GetNodeRepresentationByNodeId(wordNodeId);
                    Program.Assert(pagedWordRep != null &&
                                   pagedWordRep.GetPosition() == inMemoryPos &&
                                   pagedWordRep.GetColor() == inMemoryColor,
                        "NodeRepresentation should match in paged mode.");

                    core.MaterializeDiskStore();
                    Program.Assert(!core.IsDiskPagedReadOnly, "Core should materialize out of paged mode.");
                    core.UpdateNodeLayoutOptions(0.4, 0.2);
                    NodeRepresentation? materializedWordRep = core.GetNodeRepresentationByNodeId(wordNodeId);
                    Program.Assert(materializedWordRep != null && materializedWordRep.GetPosition().IsFinite,
                        "Layout updates after materialization should recalculate finite node positions.");
                }
                finally
                {
                    core.ResetCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            };
        }
    }
}
