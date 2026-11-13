using System.IO;
using System.Text;
using OPEN_LRD.Nodes;

namespace OPEN_LRD.NLP
{
    internal sealed record TextImportProgress(int ProcessedChunks, int TotalChunks);

    internal sealed record TextImportOptions
    {
        public string FilePath { get; init; } = string.Empty;
        public Guid? SourceNodeID { get; init; }
        public Guid? TopicNodeID { get; init; }
        public bool DryRun { get; init; }
        public long MaxFileBytes { get; init; } = 50L * 1024 * 1024;
        public int MaxChunks { get; init; } = 10000;
        public int MaxChunkCharacters { get; init; } = 15000;
        public int MaxSentencesPerChunk { get; init; } = 40;
        public int MaxDiagnostics { get; init; } = 100;
        public int MaxRetryDepth { get; init; } = 3;
        public NlpOptions? PipelineOptions { get; init; }
        public Action<TextImportProgress>? Progress { get; init; }
        public CancellationToken CancellationToken { get; init; }
    }

    internal sealed record TextImportResult
    {
        public string FilePath { get; init; } = string.Empty;
        public Guid? ConversationID { get; init; }
        public Guid? SourceNodeID { get; init; }
        public bool SourceNodeCreated { get; init; }
        public bool DryRun { get; init; }
        public int TotalCharacters { get; init; }
        public int ParagraphCount { get; init; }
        public int SentenceCount { get; init; }
        public int ChunkCount { get; init; }
        public int ProcessedChunks { get; init; }
        public int IngestedChunks { get; init; }
        public int UnsupportedChunks { get; init; }
        public int FailedChunks { get; init; }
        public int SkippedChunks { get; init; }
        public bool Truncated { get; init; }
        public bool Cancelled { get; init; }
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Imports a plain-text file by splitting it into bounded chunks and passing
    /// each chunk through a shared <see cref="NlpPipeline"/> and conversation.
    /// </summary>
    internal static class TextFileImporter
    {
        private const int BinaryProbeBytes = 8 * 1024;

        public static TextImportResult Import(LRD_Core core, TextImportOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(core);
            options ??= new TextImportOptions();
            ValidateOptions(options);

            if (!core.initialized)
            {
                throw new InvalidOperationException(
                    "The core graph must be initialized before importing a text file.");
            }

            string filePath = ValidateFilePath(options);
            Guid? sourceNodeID = NormalizeGuid(options.SourceNodeID);
            Guid? topicNodeID = NormalizeGuid(options.TopicNodeID);

            if (sourceNodeID.HasValue && !core.TryGetNode(sourceNodeID.Value, out _))
                throw new InvalidOperationException($"Source node '{sourceNodeID}' was not found.");

            if (topicNodeID.HasValue && !core.TryGetNode(topicNodeID.Value, out _))
                throw new InvalidOperationException($"Topic node '{topicNodeID}' was not found.");

            string rawText = ReadText(filePath, options.MaxFileBytes);
            TextChunkingResult chunking = TextChunker.Chunk(rawText, new TextChunkOptions
            {
                MaxChunkCharacters = options.MaxChunkCharacters,
                MaxSentencesPerChunk = options.MaxSentencesPerChunk,
                MaxChunks = options.MaxChunks
            });

            DiagnosticBag diagnostics = new DiagnosticBag(options.MaxDiagnostics);

            if (chunking.Truncated)
            {
                diagnostics.Add(
                    $"The chunk limit of {options.MaxChunks} was reached; "
                    + "the remainder of the file was not chunked.");
            }

            TextImportResult BuildResult(
                Guid? conversationID,
                Guid? resolvedSourceNodeID,
                bool sourceCreated,
                int processed,
                int ingested,
                int unsupported,
                int failed,
                bool cancelled)
            {
                return new TextImportResult
                {
                    FilePath = filePath,
                    ConversationID = conversationID,
                    SourceNodeID = resolvedSourceNodeID,
                    SourceNodeCreated = sourceCreated,
                    DryRun = options.DryRun,
                    TotalCharacters = chunking.TotalCharacters,
                    ParagraphCount = chunking.ParagraphCount,
                    SentenceCount = chunking.SentenceCount,
                    ChunkCount = chunking.Chunks.Count,
                    ProcessedChunks = processed,
                    IngestedChunks = ingested,
                    UnsupportedChunks = unsupported,
                    FailedChunks = failed,
                    SkippedChunks = cancelled
                        ? chunking.Chunks.Count - processed
                        : 0,
                    Truncated = chunking.Truncated,
                    Cancelled = cancelled,
                    Diagnostics = diagnostics.ToList()
                };
            }

            if (chunking.Chunks.Count == 0)
            {
                diagnostics.Add("No importable text was found in the file.");
                return BuildResult(null, sourceNodeID, false, 0, 0, 0, 0, false);
            }

            if (options.DryRun)
            {
                return BuildResult(null, sourceNodeID, false, 0, 0, 0, 0, false);
            }

            if (options.CancellationToken.IsCancellationRequested)
            {
                return BuildResult(null, sourceNodeID, false, 0, 0, 0, 0, true);
            }

            bool sourceCreated = false;

            if (!sourceNodeID.HasValue)
            {
                sourceNodeID = EnsureSourceNode(core, filePath, out sourceCreated);
            }

            int processedChunks = 0;
            int ingestedChunks = 0;
            int unsupportedChunks = 0;
            int failedChunks = 0;
            bool cancelledRun = false;
            Guid? conversationID = null;
            NlpPipeline? pipeline = null;

            try
            {
                pipeline = new NlpPipeline(core, options.PipelineOptions);
                conversationID = pipeline.ConversationID;

                foreach (TextChunk chunk in chunking.Chunks)
                {
                    if (options.CancellationToken.IsCancellationRequested)
                    {
                        cancelledRun = true;
                        break;
                    }

                    processedChunks++;

                    try
                    {
                        ChunkOutcome outcome = IngestWithRetry(
                            pipeline,
                            chunk,
                            sourceNodeID,
                            topicNodeID,
                            diagnostics,
                            options,
                            depth: 0);

                        switch (outcome)
                        {
                            case ChunkOutcome.Ingested:
                                ingestedChunks++;
                                break;

                            case ChunkOutcome.Unsupported:
                                unsupportedChunks++;
                                break;

                            default:
                                failedChunks++;
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        processedChunks--;
                        cancelledRun = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        failedChunks++;
                        diagnostics.Add($"Chunk {chunk.Index + 1} failed: {ex.Message}");
                    }

                    options.Progress?.Invoke(new TextImportProgress(
                        processedChunks,
                        chunking.Chunks.Count));
                }
            }
            finally
            {
                pipeline?.Dispose();
            }

            if (cancelledRun)
            {
                diagnostics.Add(
                    $"Import cancelled after {processedChunks} "
                    + $"of {chunking.Chunks.Count} chunks.");
            }

            return BuildResult(
                conversationID,
                sourceNodeID,
                sourceCreated,
                processedChunks,
                ingestedChunks,
                unsupportedChunks,
                failedChunks,
                cancelledRun);
        }

        private static ChunkOutcome IngestWithRetry(
            NlpPipeline pipeline,
            TextChunk chunk,
            Guid? sourceNodeID,
            Guid? topicNodeID,
            DiagnosticBag diagnostics,
            TextImportOptions options,
            int depth)
        {
            NlpResult result = pipeline.Ingest(chunk.Text, sourceNodeID, topicNodeID);

            switch (result.Status)
            {
                case NlpStatus.Ingested:
                    return ChunkOutcome.Ingested;

                case NlpStatus.Unsupported:
                    diagnostics.Add(
                        $"Chunk {chunk.Index + 1} contained no ingestible relations.");
                    return ChunkOutcome.Unsupported;

                case NlpStatus.InvalidInput
                    when depth < options.MaxRetryDepth
                        && TextChunker.TrySplitAtSentenceBoundary(chunk.Text, out string first, out string second):
                {
                    ChunkOutcome firstOutcome = IngestWithRetry(
                        pipeline,
                        WithText(chunk, first),
                        sourceNodeID,
                        topicNodeID,
                        diagnostics,
                        options,
                        depth + 1);

                    ChunkOutcome secondOutcome = IngestWithRetry(
                        pipeline,
                        WithText(chunk, second),
                        sourceNodeID,
                        topicNodeID,
                        diagnostics,
                        options,
                        depth + 1);

                    return Combine(firstOutcome, secondOutcome);
                }

                default:
                {
                    string reason = result.Diagnostics.FirstOrDefault()
                        ?? result.Status.ToString();
                    diagnostics.Add(
                        $"Chunk {chunk.Index + 1} failed ({result.Status}): {reason}");
                    return ChunkOutcome.Failed;
                }
            }
        }

        private static TextChunk WithText(TextChunk chunk, string text)
        {
            int sentences = Math.Max(TextChunker.FindSentenceSpans(text).Count, 1);
            return chunk with { Text = text, SentenceCount = sentences };
        }

        private static ChunkOutcome Combine(ChunkOutcome first, ChunkOutcome second)
        {
            if (first == ChunkOutcome.Ingested || second == ChunkOutcome.Ingested)
                return ChunkOutcome.Ingested;

            if (first == ChunkOutcome.Unsupported || second == ChunkOutcome.Unsupported)
                return ChunkOutcome.Unsupported;

            return ChunkOutcome.Failed;
        }

        private static Guid EnsureSourceNode(LRD_Core core, string filePath, out bool created)
        {
            string label = "Source: " + Path.GetFileName(filePath);
            Web_Node? existing = core.FindByLabel(label, NodeTypes.Sources).FirstOrDefault();

            if (existing is not null)
            {
                created = false;
                return existing.ID;
            }

            Web_Node node = new Web_Node(
                Guid.NewGuid(),
                NodeTypes.Sources,
                NodeLifetime.Permanent,
                0);
            node.Set_Label(label);
            node.Set_DataPointer(DataPointer.FromFilePath(filePath));

            if (!core.AddExistingNode(node))
                throw new InvalidOperationException("The provenance source node could not be added.");

            created = true;
            return node.ID;
        }

        private static string ReadText(string filePath, long maxFileBytes)
        {
            FileInfo info = new FileInfo(filePath);

            if (info.Length > maxFileBytes)
            {
                throw new InvalidOperationException(
                    $"The file is {info.Length} bytes, which exceeds the "
                    + $"{maxFileBytes}-byte limit.");
            }

            using FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            byte[] probe = new byte[BinaryProbeBytes];
            int read = stream.Read(probe, 0, probe.Length);

            for (int index = 0; index < read; index++)
            {
                if (probe[index] == 0)
                {
                    throw new InvalidOperationException(
                        "The file appears to be binary and cannot be imported as text.");
                }
            }

            stream.Position = 0;

            using StreamReader reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                detectEncodingFromByteOrderMarks: true);

            return reader.ReadToEnd();
        }

        private static string ValidateFilePath(TextImportOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FilePath))
                throw new ArgumentException("A text file path is required.", nameof(options));

            string fullPath = Path.GetFullPath(options.FilePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    "The text file was not found.",
                    fullPath);
            }

            return fullPath;
        }

        private static void ValidateOptions(TextImportOptions options)
        {
            if (options.MaxFileBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxFileBytes must be greater than zero.");
            }

            if (options.MaxChunks <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxChunks must be greater than zero.");
            }

            if (options.MaxChunkCharacters <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxChunkCharacters must be greater than zero.");
            }

            if (options.MaxSentencesPerChunk <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxSentencesPerChunk must be greater than zero.");
            }

            if (options.MaxDiagnostics <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxDiagnostics must be greater than zero.");
            }

            if (options.MaxRetryDepth < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    "MaxRetryDepth cannot be negative.");
            }
        }

        private static Guid? NormalizeGuid(Guid? value)
        {
            return !value.HasValue || value.Value == Guid.Empty ? null : value;
        }

        private enum ChunkOutcome
        {
            Ingested,
            Unsupported,
            Failed
        }

        private sealed class DiagnosticBag
        {
            private readonly int maxEntries;
            private readonly List<string> entries = new List<string>();
            private bool truncated;

            public DiagnosticBag(int maxEntries)
            {
                this.maxEntries = maxEntries;
            }

            public void Add(string message)
            {
                if (entries.Count < maxEntries)
                    entries.Add(message);
                else
                    truncated = true;
            }

            public IReadOnlyList<string> ToList()
            {
                if (!truncated)
                    return entries;

                List<string> copy = new List<string>(entries)
                {
                    "Additional diagnostics omitted."
                };
                return copy;
            }
        }
    }
}
