using System.IO;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.SmokeTests
{
    internal static class CoreDiskStreamingTests
    {
        internal static void RunAll()
        {
            Program.Run("Trusted dictionaries stream to and page from binary partitions", CheckpointRoundTripsAndPagesTrustedData());
            Program.Run("Disk checkpoint validation preserves current core on corruption", CorruptCheckpointDoesNotReplaceCore());
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

                    GroupBox group = new GroupBox(Guid.NewGuid(), "group", new List<Guid> { sentenceId });
                    Program.Assert(core.AddTrustedGroup(group), "Group should be trusted.");
                    SourceBox source = new SourceBox(Guid.NewGuid(), "source", new List<Guid> { group.GetGroupingID() });
                    Program.Assert(core.AddTrustedSource(source), "Source should be trusted.");

                    FunctionBox function = new FunctionBox(Guid.NewGuid(), typeof(double), "distance", "m");
                    function.AddVariable(new FunctionVariable("x", 2.5, typeof(double), "m"));
                    function.StoreResult(new FunctionResult(5.0, typeof(double), "m"));
                    Program.Assert(core.AddTrustedFunction(function), "Function should be trusted.");
                    Program.Assert(core.AddTrustedLink(
                        core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID(),
                        core.GetTrustedNodeByTarget(NodeTargetKind.Function, function.GetFunctionID())!.GetNodeID(),
                        NodeRelationType.EvaluatesWithFunction, 2.25, "distance"), "Qualified function link should be stored.");

                    List<Guid> extraSymbolIds = new List<Guid>();
                    for (int i = 0; i < 12; i++)
                    {
                        Guid id = Guid.NewGuid();
                        SymbolBox extra = new SymbolBox(id, (char)(0x0100 + i));
                        extra.SetSenseID(senseId);
                        Program.Assert(core.AddTrustedSymbol(extra), "Extra symbol should be trusted.");
                        extraSymbolIds.Add(id);
                    }

                    core.StreamTrustedDictionariesToDisk(directory);
                    Program.Assert(File.Exists(Path.Combine(directory, "manifest.lrdmeta")), "Checkpoint should include a manifest.");
                    foreach (string partition in new[] { "symbols", "words", "sentences", "senses", "usages", "groups", "sources", "functions", "nodes", "links" })
                    {
                        Program.Assert(File.Exists(Path.Combine(directory, partition + ".lrdpart")), $"Missing {partition} data partition.");
                        Program.Assert(File.Exists(Path.Combine(directory, partition + ".lrdidx")), $"Missing {partition} index partition.");
                    }

                    core.StreamTrustedDictionariesFromDisk(directory, true, false, 1);
                    Program.Assert(core.IsDiskPagedReadOnly, "Checkpoint should open in read-only paging mode.");
                    Program.Assert(core.GetTextFromSentenceID(sentenceId) == "d", "Text resolution should page sentence, word, and symbol data.");
                    Program.Assert(core.GetTrustedSymbolBox(extraSymbolIds[0])?.GetSymbolChar() == (char)0x0100,
                        "Paged symbol getter should preserve records after LRU churn.");
                    Program.Assert(core.GetTrustedSymbolBox(extraSymbolIds[^1])?.GetSymbolChar() == (char)0x010B,
                        "Paged symbol getter should load the final symbol.");
                    Program.Assert(core.GetTrustedFunctionBox(function.FunctionID)?.Variables.Single().Value is double value && value == 2.5,
                        "Function variables should round-trip from disk.");
                    Program.Assert(core.GetTrustedFunctionBox(function.FunctionID)?.Result?.Value is double result && result == 5.0,
                        "Function result should round-trip from disk.");
                    Program.Assert(core.GetTrustedGroupsContainingSentence(sentenceId).Contains(group.GetGroupingID()),
                        "Group reverse index should be rebuilt during paging.");
                    Program.Assert(core.GetTrustedSourcesContainingGroup(group.GetGroupingID()).Contains(source.GetSourceID()),
                        "Source reverse index should be rebuilt during paging.");
                    Program.Assert(core.BuildSkillsGraphSnapshot().EdgeCount > 0,
                        "Graph algorithms should receive the trusted graph while boxes are paged.");
                    Program.Assert(core.GetOutgoingLinks(
                        core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId)!.GetNodeID(), NodeRelationType.EvaluatesWithFunction)
                        .Single().Label == "distance", "Weighted labeled links should round-trip.");

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
                    SymbolBox addedSymbol = new SymbolBox(Guid.NewGuid(), 'z');
                    addedSymbol.SetSenseID(senseId);
                    Program.Assert(core.AddTrustedSymbol(addedSymbol), "Mutation should succeed after materialization.");
                    core.FlushTrustedDiskStore();

                    core.OpenDiskStore(directory, 0);
                    Program.Assert(core.GetTrustedSymbolBox(addedSymbol.GetSymbolID())?.GetSymbolChar() == 'z',
                        "Flushed changes should persist and page back from disk.");
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
                    Program.Assert(core.GetTrustedSenseBox(senseId) != null,
                        "Failed disk import must preserve current trusted state.");
                }
                finally
                {
                    core.ResetCore();
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                    if (Directory.Exists(corruptDirectory)) Directory.Delete(corruptDirectory, true);
                }
            };
        }
    }
}
