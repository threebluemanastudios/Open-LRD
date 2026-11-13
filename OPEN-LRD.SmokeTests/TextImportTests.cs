using System.IO;
using System.Text;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class TextImportTests
    {
        public static void RunAll()
        {
            Program.Run("Text import paragraph chunking", TestParagraphChunking);
            Program.Run("Text import sentence fallback", TestSentenceFallback);
            Program.Run("Text import hard split", TestHardSplit);
            Program.Run("Text import chunk budgets", TestChunkBudgets);
            Program.Run("Text import shared conversation", TestSharedConversation);
            Program.Run("Text import continue on failure", TestContinueOnFailure);
            Program.Run("Text import limit retry", TestLimitRetry);
            Program.Run("Text import auto source node", TestAutoSourceNode);
            Program.Run("Text import dry run", TestDryRun);
            Program.Run("Text import safety limits", TestSafetyLimits);
            Program.Run("Text import large file", TestLargeFile);
            Program.Run("Text import encoding", TestEncoding);
            Program.Run("Text import cancellation", TestCancellation);
        }

        private static void TestParagraphChunking()
        {
            string text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.";
            TextChunkingResult result = TextChunker.Chunk(text);

            Assert(result.Chunks.Count == 3, $"expected 3 chunks but got {result.Chunks.Count}");
            Assert(result.ParagraphCount == 3, "each blank-line-separated paragraph should be counted");
            Assert(result.Chunks[0].Text == "First paragraph.", "the first paragraph should be preserved");
            Assert(result.Chunks[1].Text == "Second paragraph.", "the second paragraph should be preserved");
            Assert(result.Chunks[2].Text == "Third paragraph.", "the third paragraph should be preserved");
            Assert(result.Chunks.All(chunk => chunk.CharacterCount <= 15000), "short paragraphs must not be split");
        }

        private static void TestSentenceFallback()
        {
            string paragraph = string.Join(
                " ",
                Enumerable.Repeat("This sentence is long enough to matter.", 8));

            TextChunkingResult result = TextChunker.Chunk(paragraph, new TextChunkOptions
            {
                MaxChunkCharacters = 80
            });

            Assert(result.Chunks.Count > 1, "a long paragraph should split at sentence boundaries");
            Assert(
                result.Chunks.All(chunk => chunk.CharacterCount <= 80),
                "sentence chunks should respect the character budget");
            Assert(
                NonWhitespace(string.Concat(result.Chunks.Select(chunk => chunk.Text)))
                    == NonWhitespace(TextNormalizer.Normalize(paragraph)),
                "concatenated sentence chunks should reproduce the paragraph content");
        }

        private static void TestHardSplit()
        {
            const string text = "alpha beta gamma delta epsilon zeta eta theta";

            TextChunkingResult result = TextChunker.Chunk(text, new TextChunkOptions
            {
                MaxChunkCharacters = 12
            });

            Assert(result.Chunks.Count > 1, "an oversized sentence should be hard-split");
            Assert(
                result.Chunks.All(chunk => chunk.CharacterCount <= 12),
                "hard-split chunks should respect the character budget");
            Assert(
                NonWhitespace(string.Concat(result.Chunks.Select(chunk => chunk.Text)))
                    == NonWhitespace(text),
                "hard splitting must not drop characters");
        }

        private static void TestChunkBudgets()
        {
            string text = string.Join(
                "\n\n",
                Enumerable.Range(0, 4).Select(index =>
                    string.Join(" ", Enumerable.Repeat($"Sentence number {index}.", 3))));

            TextChunkingResult result = TextChunker.Chunk(text, new TextChunkOptions
            {
                MaxChunkCharacters = 60,
                MaxSentencesPerChunk = 2
            });

            Assert(result.Chunks.Count > 0, "budgeted text should produce chunks");
            Assert(
                result.Chunks.All(chunk => chunk.CharacterCount <= 60),
                "every chunk should stay within the character budget");
            Assert(
                result.Chunks.All(chunk => chunk.SentenceCount <= 2),
                "every chunk should stay within the sentence budget");
        }

        private static void TestSharedConversation()
        {
            LRD_Core core = NewCore();
            string path = WriteTempFile("Alice works at Acme.\n\nShe sees Bob.\n");

            try
            {
                TextImportResult result = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path
                });

                Assert(
                    result.IngestedChunks == 2,
                    $"expected both chunks to ingest but got "
                    + $"{result.IngestedChunks} ingested, {result.FailedChunks} failed: "
                    + string.Join("; ", result.Diagnostics));
                Assert(result.ConversationID.HasValue, "the import should report its conversation");
                Assert(
                    core.TryGetConversation(result.ConversationID!.Value, out _),
                    "the reported conversation should remain in the core");
                Assert(core.FindByLabel("She").Count == 0, "a resolved pronoun must not create a node");

                Web_Node alice = core.FindByLabel("Alice", NodeTypes.Entities).Single();
                Web_Node bob = core.FindByLabel("Bob", NodeTypes.Entities).Single();

                Assert(
                    core.GetLinks(alice.ID).Any(link =>
                        link.DestinationID == bob.ID && link.ForwardLink.Name == "see"),
                    "the second chunk should resolve 'She' to Alice and link her to Bob");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static void TestContinueOnFailure()
        {
            LRD_Core core = NewCore();
            string path = WriteTempFile(
                "Alice works at Acme.\n\nQqq zzz.\n\nBob works at Globex.\n");

            try
            {
                TextImportResult result = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path
                });

                Assert(result.ChunkCount == 3, $"expected 3 chunks but got {result.ChunkCount}");
                Assert(result.ProcessedChunks == 3, "every chunk should be visited");
                Assert(
                    result.IngestedChunks == 2,
                    $"expected 2 ingested chunks but got {result.IngestedChunks}: "
                    + string.Join("; ", result.Diagnostics));
                Assert(
                    result.UnsupportedChunks == 1,
                    $"expected 1 unsupported chunk but got {result.UnsupportedChunks}");
                Assert(result.FailedChunks == 0, "unsupported text should not count as a failure");
                Assert(
                    core.FindByLabel("Acme", NodeTypes.Entities).Count == 1
                        && core.FindByLabel("Globex", NodeTypes.Entities).Count == 1,
                    "valid chunks on both sides of the skip should ingest");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static void TestLimitRetry()
        {
            LRD_Core core = NewCore();
            string text = string.Join(" ", Enumerable.Repeat("Alice works at Acme.", 6));
            string path = WriteTempFile(text);

            try
            {
                TextImportResult result = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path,
                    PipelineOptions = new NlpOptions { MaxSentences = 4 }
                });

                Assert(result.ChunkCount == 1, "the fixture should chunk as one paragraph");
                Assert(result.ProcessedChunks == 1, "the retry should stay within one top-level chunk");
                Assert(
                    result.IngestedChunks == 1,
                    $"the split chunk should be ingested but got {result.IngestedChunks} ingested, "
                    + $"{result.FailedChunks} failed: {string.Join("; ", result.Diagnostics)}");
                Assert(result.FailedChunks == 0, "a splittable limit failure should not be reported as failed");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static void TestAutoSourceNode()
        {
            LRD_Core core = NewCore();
            string path = WriteTempFile("Alice works at Acme.\n");
            string label = "Source: " + Path.GetFileName(path);

            try
            {
                TextImportResult first = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path
                });

                Assert(first.SourceNodeID.HasValue, "a blank source should create a source node");
                Assert(first.SourceNodeCreated, "the first blank-source import should report node creation");
                Assert(
                    core.TryGetNode(first.SourceNodeID!.Value, out Web_Node? source) && source is not null,
                    "the source node should be stored in the graph");
                Assert(source!.Get_NodeType() == NodeTypes.Sources, "the source node should be a Source type");
                Assert(source.IsPermanent, "the source node should be permanent");
                Assert(source.Label == label, $"expected label '{label}' but got '{source.Label}'");

                DataPointer? pointer = source.Get_DataPointer();
                Assert(pointer is not null, "the source node should carry a data pointer");
                Assert(
                    pointer!.Location.IsAbsoluteUri
                        && pointer.Location.Scheme == "file"
                        && pointer.Location.LocalPath == Path.GetFullPath(path),
                    "the data pointer should reference the imported file");

                Assert(
                    core.FindByLabel(label, NodeTypes.Sources).Count == 1,
                    "a blank source should create exactly one node");

                TextImportResult second = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path
                });

                Assert(!second.SourceNodeCreated, "a repeated import should reuse the source node");
                Assert(second.SourceNodeID == first.SourceNodeID, "the reused source ID should match");
                Assert(
                    core.FindByLabel(label, NodeTypes.Sources).Count == 1,
                    "a repeated import should not duplicate the source node");

                LRD_Core providedCore = NewCore();
                Guid provided = AddNode(
                    providedCore,
                    NodeTypes.Sources,
                    "KnownSource",
                    NodeLifetime.Permanent);

                TextImportResult providedResult = TextFileImporter.Import(
                    providedCore,
                    new TextImportOptions { FilePath = path, SourceNodeID = provided });

                Assert(providedResult.SourceNodeID == provided, "a provided source GUID should be used as-is");
                Assert(!providedResult.SourceNodeCreated, "a provided source should not be created");
                Assert(
                    providedCore.FindByLabel(label, NodeTypes.Sources).Count == 0,
                    "a provided source should suppress the automatic node");

                LRD_Core rejectedCore = NewCore();
                int before = CountNodes(rejectedCore);

                AssertThrows<InvalidOperationException>(
                    () => TextFileImporter.Import(rejectedCore, new TextImportOptions
                    {
                        FilePath = path,
                        SourceNodeID = Guid.NewGuid()
                    }),
                    "an unknown source GUID should abort the import");

                Assert(CountNodes(rejectedCore) == before, "an unknown source GUID must not mutate the graph");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static void TestDryRun()
        {
            LRD_Core core = NewCore();
            string path = WriteTempFile("Alice works at Acme.\n\nBob works at Globex.\n");

            try
            {
                int nodes = CountNodes(core);
                int links = CountLinks(core);
                int conversations = core.GetConversations()?.Count ?? 0;
                int queryNodes = core.GetQuery()?.Count ?? 0;
                int responseNodes = core.GetResponse()?.Count ?? 0;

                TextImportResult result = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path,
                    DryRun = true
                });

                Assert(result.DryRun, "the result should report a dry run");
                Assert(result.ChunkCount == 2, $"expected 2 chunks but got {result.ChunkCount}");
                Assert(result.ParagraphCount == 2, "the dry run should report paragraphs");
                Assert(result.TotalCharacters > 0, "the dry run should report characters");
                Assert(result.SentenceCount == 2, $"expected 2 sentences but got {result.SentenceCount}");
                Assert(result.ProcessedChunks == 0, "a dry run should not process chunks");
                Assert(result.IngestedChunks == 0, "a dry run should not ingest");
                Assert(result.ConversationID is null, "a dry run should not create a conversation");
                Assert(result.SourceNodeID is null, "a dry run should not create a source node");
                Assert(CountNodes(core) == nodes && CountLinks(core) == links, "a dry run must not mutate the graph");
                Assert(
                    (core.GetConversations()?.Count ?? 0) == conversations,
                    "a dry run must not register a conversation");
                Assert(
                    (core.GetQuery()?.Count ?? 0) == queryNodes
                        && (core.GetResponse()?.Count ?? 0) == responseNodes,
                    "a dry run must not touch the transient chains");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static void TestSafetyLimits()
        {
            LRD_Core core = NewCore();
            string path = WriteTempFile(
                "Alice works at Acme.\n\n"
                + "Bob works at Globex.\n\n"
                + "Carol works at Initech.\n\n"
                + "Dave works at Umbrella.\n");

            try
            {
                AssertThrows<InvalidOperationException>(
                    () => TextFileImporter.Import(core, new TextImportOptions
                    {
                        FilePath = path,
                        MaxFileBytes = 16
                    }),
                    "an oversized file should be rejected");

                TextImportResult result = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path,
                    MaxChunks = 2
                });

                Assert(result.Truncated, "the chunk cap should set Truncated");
                Assert(result.ChunkCount == 2, $"expected 2 chunks but got {result.ChunkCount}");
                Assert(result.ProcessedChunks == 2, "the capped chunks should still be processed");
                Assert(result.IngestedChunks == 2, "the capped chunks should ingest");
                Assert(
                    core.FindByLabel("Carol", NodeTypes.Entities).Count == 0,
                    "the remainder of the file should not be imported");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static void TestLargeFile()
        {
            LRD_Core core = NewCore();
            StringBuilder builder = new StringBuilder();

            for (int index = 0; index < 140; index++)
            {
                builder.Append("Entity");
                builder.Append(index);
                builder.Append(" works at Company");
                builder.Append(index);
                builder.Append(
                    " in the northern regional office with a small project team "
                    + "and a shared annual budget that covers travel and equipment.\n");
            }

            string text = builder.ToString();
            string path = WriteTempFile(text);
            NlpOptions limits = new NlpOptions();
            TextChunkOptions chunkLimits = new TextChunkOptions();

            try
            {
                Assert(
                    text.Length > limits.MaxCharacters,
                    $"the fixture should exceed the {limits.MaxCharacters}-character per-call limit");

                TextChunkingResult chunking = TextChunker.Chunk(text);

                Assert(
                    chunking.TotalCharacters > limits.MaxCharacters,
                    "the normalized fixture should still exceed the per-call limit");
                Assert(
                    chunking.Chunks.Count > 1,
                    $"an oversized file should split into multiple chunks but got {chunking.Chunks.Count}");
                Assert(
                    chunking.Chunks.All(chunk => chunk.CharacterCount <= chunkLimits.MaxChunkCharacters),
                    "every chunk should stay within the chunk character budget");
                Assert(
                    chunking.Chunks.All(chunk => chunk.SentenceCount <= chunkLimits.MaxSentencesPerChunk),
                    "every chunk should stay within the chunk sentence budget");
                Assert(
                    chunking.Chunks.All(chunk => chunk.CharacterCount <= limits.MaxCharacters),
                    "every chunk should stay within the per-call character limit");
                Assert(
                    chunking.Chunks.All(chunk => chunk.SentenceCount <= limits.MaxSentences),
                    "every chunk should stay within the per-call sentence limit");

                TextImportResult result = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path
                });

                Assert(
                    result.ChunkCount == chunking.Chunks.Count,
                    $"expected {chunking.Chunks.Count} chunks but imported {result.ChunkCount}");
                Assert(
                    result.ProcessedChunks == result.ChunkCount,
                    $"expected all chunks to be processed but got {result.ProcessedChunks}");
                Assert(
                    result.IngestedChunks > 0,
                    "the large file should ingest at least one chunk: "
                    + string.Join("; ", result.Diagnostics));
                Assert(result.ConversationID.HasValue, "the import should report its conversation");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static void TestEncoding()
        {
            LRD_Core core = NewCore();
            string content = "Alice works at Acme.\r\n\r\nBob works at Globex.\r\n";
            string bomPath = WriteTempFile(content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            string noBomPath = WriteTempFile(content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            try
            {
                TextImportResult bom = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = bomPath
                });

                Assert(bom.IngestedChunks == 2, "UTF-8 with a BOM and CRLF should import");

                TextImportResult noBom = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = noBomPath
                });

                Assert(noBom.IngestedChunks == 2, "UTF-8 without a BOM and CRLF should import");

                LRD_Core binaryCore = NewCore();
                string binaryPath = WriteTempBytes(new byte[] { 0x41, 0x00, 0x42, 0x43 });

                try
                {
                    AssertThrows<InvalidOperationException>(
                        () => TextFileImporter.Import(binaryCore, new TextImportOptions
                        {
                            FilePath = binaryPath
                        }),
                        "a binary file should be rejected");
                }
                finally
                {
                    DeleteFile(binaryPath);
                }
            }
            finally
            {
                DeleteFile(bomPath);
                DeleteFile(noBomPath);
            }
        }

        private static void TestCancellation()
        {
            LRD_Core core = NewCore();
            string path = WriteTempFile("Alice works at Acme.\n\nBob works at Globex.\n");

            try
            {
                int nodes = CountNodes(core);
                int links = CountLinks(core);
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                TextImportResult result = TextFileImporter.Import(core, new TextImportOptions
                {
                    FilePath = path,
                    CancellationToken = cancellation.Token
                });

                Assert(result.Cancelled, "a pre-cancelled import should report Cancelled");
                Assert(result.ProcessedChunks == 0, "a pre-cancelled import should process no chunks");
                Assert(result.IngestedChunks == 0, "a pre-cancelled import should ingest nothing");
                Assert(result.SkippedChunks == result.ChunkCount, "all chunks should be reported as skipped");
                Assert(
                    CountNodes(core) == nodes && CountLinks(core) == links,
                    "a pre-cancelled import must not mutate the graph");
            }
            finally
            {
                DeleteFile(path);
            }
        }

        private static string NonWhitespace(string value)
        {
            return new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        }

        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }

        private static int CountNodes(LRD_Core core)
        {
            return core.GetDictionary()?.Count ?? 0;
        }

        private static int CountLinks(LRD_Core core)
        {
            IReadOnlyDictionary<Guid, Web_Node>? nodes = core.GetDictionary();

            if (nodes is null)
                return 0;

            return nodes.Values.Sum(node => core.GetLinks(node.ID).Count);
        }

        private static Guid AddNode(
            LRD_Core core,
            NodeTypes type,
            string? label = null,
            NodeLifetime lifetime = NodeLifetime.Temporary)
        {
            Guid nodeID = Guid.NewGuid();
            Web_Node node = new Web_Node(nodeID, type, lifetime, 100);
            node.Set_Label(label);
            Assert(core.AddExistingNode(node), "node should be added");
            return nodeID;
        }

        private static string WriteTempFile(string content, Encoding? encoding = null)
        {
            string directory = NewTempDirectory();
            string path = Path.Combine(directory, "import-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
            return path;
        }

        private static string WriteTempBytes(byte[] bytes)
        {
            string directory = NewTempDirectory();
            string path = Path.Combine(directory, "import-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static string NewTempDirectory()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "open-lrd-text-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteFile(string path)
        {
            try
            {
                string? directory = Path.GetDirectoryName(path);

                if (File.Exists(path))
                    File.Delete(path);

                if (directory is not null && Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
