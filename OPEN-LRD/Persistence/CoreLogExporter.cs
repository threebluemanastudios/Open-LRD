using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;

namespace OPEN_LRD.Persistence
{
    internal static class CoreLogExporter
    {
        internal const long MaxPartBytes = 500L * 1024L * 1024L - 1L;
        private const int MaxPartsPerExport = 10000;
        private static readonly JsonSerializerOptions CompactJsonOptions = new JsonSerializerOptions(LogJson.Options)
        {
            WriteIndented = false
        };

        public static CoreLogExportResult Export(
            LRD_Core core,
            string path,
            bool includeTransient = true,
            DateTimeOffset? exportedAtUtc = null)
        {
            return ExportCore(core, path, includeTransient, exportedAtUtc, MaxPartBytes);
        }

        internal static CoreLogExportResult ExportWithPartLimit(
            LRD_Core core,
            string path,
            long partLimitBytes,
            bool includeTransient = true,
            DateTimeOffset? exportedAtUtc = null)
        {
            if (partLimitBytes < 1024 || partLimitBytes > MaxPartBytes)
                throw new ArgumentOutOfRangeException(nameof(partLimitBytes));
            return ExportCore(core, path, includeTransient, exportedAtUtc, partLimitBytes);
        }

        private static CoreLogExportResult ExportCore(
            LRD_Core core,
            string path,
            bool includeTransient,
            DateTimeOffset? exportedAtUtc,
            long partLimitBytes)
        {
            ArgumentNullException.ThrowIfNull(core);

            if (!core.initialized)
                throw new InvalidOperationException("The core must be initialized before exporting logs.");

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A log path is required.", nameof(path));

            string corePath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(corePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string? transientPath = includeTransient ? BuildTransientPath(corePath) : null;
            HashSet<string> previousCorePartDirectories = ReadManifestPartDirectories(
                corePath,
                CoreLogFormat.CoreManifest);
            HashSet<string> previousTransientPartDirectories = transientPath is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : ReadManifestPartDirectories(transientPath, CoreLogFormat.TransientManifest);
            DateTimeOffset timestamp = exportedAtUtc ?? DateTimeOffset.UtcNow;
            TypeDefinitionLog typeDefinitions = BuildTypeDefinitions();
            IReadOnlyDictionary<Guid, Web_Node> graph = core.GetDictionary()
                ?? throw new InvalidOperationException("The core graph is not available.");

            List<Web_Node> orderedNodes = graph.Values
                .OrderBy(node => node.ID.ToString("D"), StringComparer.Ordinal)
                .ToList();
            IReadOnlyDictionary<Guid, ConversationContext>? conversations = core.GetConversations();
            string coreTempPath = CreateTempPath(corePath);
            string? transientTempPath = transientPath is null ? null : CreateTempPath(transientPath);
            Guid exportId = Guid.NewGuid();
            string baseName = Path.GetFileNameWithoutExtension(corePath);
            string corePartsDirectoryName = baseName + "_parts_" + exportId.ToString("N");
            string transientPartsDirectoryName = baseName + "_trans_parts_" + exportId.ToString("N");
            string parentDirectory = directory ?? string.Empty;
            string corePartsTempDirectory = Path.Combine(parentDirectory, "." + corePartsDirectoryName + ".tmp");
            string transientPartsTempDirectory = Path.Combine(parentDirectory, "." + transientPartsDirectoryName + ".tmp");
            string corePartsFinalDirectory = Path.Combine(parentDirectory, corePartsDirectoryName);
            string transientPartsFinalDirectory = Path.Combine(parentDirectory, transientPartsDirectoryName);
            bool corePartsPublished = false;
            bool transientPartsPublished = false;
            bool coreOutputPublished = false;
            bool transientOutputPublished = false;
            bool coreSplit = false;
            bool transientSplit = false;
            int linkCount = 0;
            int queryChainCount = 0;
            int responseChainCount = 0;
            List<LogPartDefinition> coreParts = new List<LogPartDefinition>();
            List<LogPartDefinition> transientParts = new List<LogPartDefinition>();

            try
            {
                if (!TryWriteLegacyCoreLog(
                    coreTempPath,
                    timestamp,
                    transientPath is null ? null : Path.GetFileName(transientPath),
                    typeDefinitions,
                    orderedNodes,
                    core,
                    conversations,
                    partLimitBytes,
                    out linkCount))
                {
                    coreSplit = true;
                    Directory.CreateDirectory(corePartsTempDirectory);
                    coreParts = WriteCoreParts(
                        corePartsTempDirectory,
                        baseName,
                        exportId,
                        orderedNodes,
                        core,
                        conversations,
                        partLimitBytes,
                        out linkCount);
                    SetPartPaths(coreParts, corePartsDirectoryName);
                    Directory.Move(corePartsTempDirectory, corePartsFinalDirectory);
                    corePartsPublished = true;

                    CoreLogManifest manifest = new CoreLogManifest
                    {
                        ExportId = exportId,
                        ExportedAtUtc = timestamp,
                        TransientFile = transientPath is null ? null : Path.GetFileName(transientPath),
                        TypeDefinitions = typeDefinitions,
                        NodeCount = graph.Count,
                        LinkCount = linkCount,
                        ConversationCount = conversations?.Count ?? 0,
                        Parts = coreParts
                    };
                    WriteManifest(coreTempPath, manifest);
                }

                if (transientPath is not null && transientTempPath is not null)
                {
                    LinkedList<Chain_Node>? query = core.GetQuery();
                    LinkedList<Chain_Node>? response = core.GetResponse();
                    queryChainCount = query is null || query.Count == 0 ? 0 : 1;
                    responseChainCount = response is null || response.Count == 0 ? 0 : 1;
                    if (!TryWriteLegacyTransientLog(
                        transientTempPath,
                        timestamp,
                        Path.GetFileName(corePath),
                        typeDefinitions,
                        query,
                        response,
                        partLimitBytes))
                    {
                        transientSplit = true;
                        Directory.CreateDirectory(transientPartsTempDirectory);
                        transientParts = WriteTransientParts(
                            transientPartsTempDirectory,
                            baseName + "_trans",
                            exportId,
                            query,
                            response,
                            partLimitBytes);
                        SetPartPaths(transientParts, transientPartsDirectoryName);
                        Directory.Move(transientPartsTempDirectory, transientPartsFinalDirectory);
                        transientPartsPublished = true;

                        TransientLogManifest manifest = new TransientLogManifest
                        {
                            ExportId = exportId,
                            ExportedAtUtc = timestamp,
                            CoreFile = Path.GetFileName(corePath),
                            TypeDefinitions = typeDefinitions,
                            QueryChainCount = queryChainCount,
                            ResponseChainCount = responseChainCount,
                            QueryNodeCount = query?.Count ?? 0,
                            ResponseNodeCount = response?.Count ?? 0,
                            Parts = transientParts
                        };
                        WriteManifest(transientTempPath, manifest);
                    }
                }

                if (coreParts.Count + transientParts.Count > MaxPartsPerExport)
                {
                    throw new InvalidOperationException(
                        $"A combined core/transient export cannot exceed {MaxPartsPerExport} parts.");
                }

                if (transientTempPath is not null && transientPath is not null)
                {
                    File.Move(transientTempPath, transientPath, overwrite: true);
                    transientOutputPublished = true;
                }
                File.Move(coreTempPath, corePath, overwrite: true);
                coreOutputPublished = true;
                CleanupPartDirectories(previousCorePartDirectories);
                if (includeTransient)
                    CleanupPartDirectories(previousTransientPartDirectories);
            }
            finally
            {
                DeleteIfExists(coreTempPath);
                if (transientTempPath is not null)
                    DeleteIfExists(transientTempPath);
                DeleteDirectoryIfExists(corePartsTempDirectory);
                DeleteDirectoryIfExists(transientPartsTempDirectory);
                if (!coreOutputPublished && corePartsPublished)
                    DeleteDirectoryIfExists(corePartsFinalDirectory);
                if (!transientOutputPublished && transientPartsPublished)
                    DeleteDirectoryIfExists(transientPartsFinalDirectory);
            }

            return new CoreLogExportResult
            {
                CorePath = corePath,
                TransientPath = transientPath,
                NodeCount = graph.Count,
                LinkCount = linkCount,
                ConversationCount = conversations?.Count ?? 0,
                QueryChainCount = queryChainCount,
                ResponseChainCount = responseChainCount,
                IsSplit = coreSplit || transientSplit,
                CorePartCount = coreParts.Count,
                TransientPartCount = transientParts.Count,
                TotalBytes = new FileInfo(corePath).Length
                    + (transientPath is null ? 0 : new FileInfo(transientPath).Length)
                    + coreParts.Sum(part => part.ByteLength)
                    + transientParts.Sum(part => part.ByteLength)
            };
        }

        private static int WriteCoreLog(
            Stream output,
            DateTimeOffset exportedAtUtc,
            string? transientFile,
            TypeDefinitionLog typeDefinitions,
            IReadOnlyList<Web_Node> orderedNodes,
            LRD_Core core,
            IReadOnlyDictionary<Guid, ConversationContext>? conversations)
        {
            using Utf8JsonWriter writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteString("format", CoreLogFormat.Core);
            writer.WriteNumber("version", CoreLogFormat.Version);
            writer.WritePropertyName("exportedAtUtc");
            JsonSerializer.Serialize(writer, exportedAtUtc, LogJson.Options);

            if (transientFile is not null)
                writer.WriteString("transientFile", transientFile);

            writer.WritePropertyName("typeDefinitions");
            JsonSerializer.Serialize(writer, typeDefinitions, LogJson.Options);

            writer.WritePropertyName("nodes");
            writer.WriteStartArray();
            foreach (Web_Node node in orderedNodes)
                JsonSerializer.Serialize(writer, ToNodeLog(node), LogJson.Options);
            writer.WriteEndArray();

            writer.WritePropertyName("links");
            int linkCount = WriteLinks(writer, orderedNodes, core);

            writer.WritePropertyName("conversations");
            writer.WriteStartArray();
            if (conversations is not null)
            {
                foreach (ConversationContext conversation in conversations.Values
                    .OrderBy(value => value.ConversationID.ToString("D"), StringComparer.Ordinal))
                {
                    JsonSerializer.Serialize(writer, ToConversationLog(conversation), LogJson.Options);
                }
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.Flush();
            return linkCount;
        }

        private static bool TryWriteLegacyCoreLog(
            string path,
            DateTimeOffset exportedAtUtc,
            string? transientFile,
            TypeDefinitionLog typeDefinitions,
            IReadOnlyList<Web_Node> orderedNodes,
            LRD_Core core,
            IReadOnlyDictionary<Guid, ConversationContext>? conversations,
            long partLimitBytes,
            out int linkCount)
        {
            linkCount = 0;
            try
            {
                using FileStream stream = CreateOutputStream(path);
                using CappedWriteStream capped = new CappedWriteStream(stream, partLimitBytes, leaveOpen: true);
                linkCount = WriteCoreLog(
                    capped,
                    exportedAtUtc,
                    transientFile,
                    typeDefinitions,
                    orderedNodes,
                    core,
                    conversations);
                return true;
            }
            catch (OutputLimitExceededException)
            {
                DeleteIfExists(path);
                linkCount = 0;
                return false;
            }
        }

        private static bool TryWriteLegacyTransientLog(
            string path,
            DateTimeOffset exportedAtUtc,
            string coreFile,
            TypeDefinitionLog typeDefinitions,
            LinkedList<Chain_Node>? query,
            LinkedList<Chain_Node>? response,
            long partLimitBytes)
        {
            try
            {
                using FileStream stream = CreateOutputStream(path);
                using CappedWriteStream capped = new CappedWriteStream(stream, partLimitBytes, leaveOpen: true);
                WriteTransientLog(capped, exportedAtUtc, coreFile, typeDefinitions, query, response);
                return true;
            }
            catch (OutputLimitExceededException)
            {
                DeleteIfExists(path);
                return false;
            }
        }

        private static List<LogPartDefinition> WriteCoreParts(
            string directory,
            string fileNamePrefix,
            Guid exportId,
            IReadOnlyList<Web_Node> orderedNodes,
            LRD_Core core,
            IReadOnlyDictionary<Guid, ConversationContext>? conversations,
            long partLimitBytes,
            out int linkCount)
        {
            using SegmentedPartWriter parts = new SegmentedPartWriter(
                directory,
                fileNamePrefix,
                exportId,
                partLimitBytes,
                isTransient: false);

            foreach (Web_Node node in orderedNodes)
            {
                parts.Write(
                    "nodes",
                    new CoreLogPartRecord { Kind = "node", Node = ToNodeLog(node) },
                    typeof(CoreLogPartRecord),
                    $"node '{node.ID}'");
            }

            linkCount = 0;
            foreach (Web_Node source in orderedNodes)
            {
                foreach (Link link in GetOrderedSourceLinks(source, core))
                {
                    parts.Write(
                        "links",
                        new CoreLogPartRecord { Kind = "link", Link = ToLinkLog(link) },
                        typeof(CoreLogPartRecord),
                        $"link '{link.SourceID}' -> '{link.DestinationID}'");
                    linkCount++;
                }
            }

            if (conversations is not null)
            {
                foreach (ConversationContext conversation in conversations.Values
                    .OrderBy(value => value.ConversationID.ToString("D"), StringComparer.Ordinal))
                {
                    parts.Write(
                        "conversations",
                        new CoreLogPartRecord
                        {
                            Kind = "conversation",
                            Conversation = ToConversationLog(conversation)
                        },
                        typeof(CoreLogPartRecord),
                        $"conversation '{conversation.ConversationID}'");
                }
            }

            return parts.Complete();
        }

        private static List<LogPartDefinition> WriteTransientParts(
            string directory,
            string fileNamePrefix,
            Guid exportId,
            LinkedList<Chain_Node>? query,
            LinkedList<Chain_Node>? response,
            long partLimitBytes)
        {
            using SegmentedPartWriter parts = new SegmentedPartWriter(
                directory,
                fileNamePrefix,
                exportId,
                partLimitBytes,
                isTransient: true);

            WriteTransientChainParts(parts, exportId, "queryChains", query);
            WriteTransientChainParts(parts, exportId, "responseChains", response);
            return parts.Complete();
        }

        private static void WriteTransientChainParts(
            SegmentedPartWriter parts,
            Guid exportId,
            string kind,
            LinkedList<Chain_Node>? chain)
        {
            if (chain is null || chain.Count == 0)
                return;

            int position = 0;
            foreach (Chain_Node node in chain)
            {
                Link? next = node.Get_LinkNext();
                TransientLogPartRecord record = new TransientLogPartRecord
                {
                    Kind = "chainNode",
                    ChainIndex = 0,
                    Position = position,
                    Node = ToNodeLog(node),
                    NextLink = next is null ? null : ToLinkLog(next)
                };
                parts.Write(kind, record, typeof(TransientLogPartRecord),
                    $"{kind} node {position}");
                position++;
            }
        }

        private static List<Link> GetOrderedSourceLinks(Web_Node source, LRD_Core core)
        {
            return core.GetStoredLinks(source.ID)
                .Where(link => link.SourceID == source.ID)
                .OrderBy(link => link.DestinationID.ToString("D"), StringComparer.Ordinal)
                .ThenBy(link => link.ForwardLink.Id)
                .ThenBy(link => link.InverseLink.Id)
                .ThenBy(link => link.Classification.Id)
                .ToList();
        }

        private static void WriteManifest<T>(string path, T manifest)
        {
            using FileStream stream = CreateOutputStream(path);
            using CappedWriteStream capped = new CappedWriteStream(stream, MaxPartBytes, leaveOpen: true);
            using Utf8JsonWriter writer = new Utf8JsonWriter(capped, new JsonWriterOptions { Indented = true });
            JsonSerializer.Serialize(writer, manifest, LogJson.Options);
            writer.Flush();
            capped.Flush();
            if (capped.BytesWritten > MaxPartBytes)
                throw new OutputLimitExceededException();
        }

        private static void SetPartPaths(List<LogPartDefinition> parts, string generationDirectoryName)
        {
            foreach (LogPartDefinition part in parts)
            {
                part.File = Path.Combine(generationDirectoryName, part.File)
                    .Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        private static HashSet<string> ReadManifestPartDirectories(string manifestPath, string expectedFormat)
        {
            HashSet<string> directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(manifestPath))
                return directories;

            try
            {
                if (new FileInfo(manifestPath).Length > 16L * 1024L * 1024L)
                    return directories;
                if (!string.Equals(ReadFormatHeader(manifestPath), expectedFormat, StringComparison.Ordinal))
                    return directories;

                using FileStream stream = File.OpenRead(manifestPath);
                JsonDocument document = JsonDocument.Parse(stream);
                using (document)
                {
                    JsonElement root = document.RootElement;
                    if (!root.TryGetProperty("format", out JsonElement formatElement)
                        || !string.Equals(formatElement.GetString(), expectedFormat, StringComparison.Ordinal)
                        || !root.TryGetProperty("parts", out JsonElement partsElement)
                        || partsElement.ValueKind != JsonValueKind.Array)
                    {
                        return directories;
                    }

                    string parent = Path.GetFullPath(Path.GetDirectoryName(manifestPath) ?? ".");
                    foreach (JsonElement part in partsElement.EnumerateArray())
                    {
                        if (!part.TryGetProperty("file", out JsonElement fileElement))
                            continue;
                        string? relativeFile = fileElement.GetString();
                        if (string.IsNullOrWhiteSpace(relativeFile) || Path.IsPathRooted(relativeFile))
                            continue;

                        string relativeDirectory = Path.GetDirectoryName(
                            relativeFile.Replace('/', Path.DirectorySeparatorChar)) ?? string.Empty;
                        if (relativeDirectory.Length == 0)
                            continue;

                        string fullDirectory = Path.GetFullPath(Path.Combine(parent, relativeDirectory));
                        string relative = Path.GetRelativePath(parent, fullDirectory);
                        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar,
                            StringComparison.Ordinal) || Path.IsPathRooted(relative))
                        {
                            continue;
                        }
                        directories.Add(fullDirectory);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or JsonException or InvalidOperationException)
            {
                // A malformed old manifest is not needed to publish a new one;
                // leave its part directories untouched rather than guessing.
            }

            return directories;
        }

        private static string? ReadFormatHeader(string path)
        {
            using FileStream stream = File.OpenRead(path);
            byte[] header = new byte[512];
            int bytesRead = stream.Read(header, 0, header.Length);
            ReadOnlySpan<byte> json = header.AsSpan(0, bytesRead);
            if (json.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
                json = json[3..];

            Utf8JsonReader reader = new Utf8JsonReader(json, stream.Length <= bytesRead,
                new JsonReaderState(new JsonReaderOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                }));
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject
                || !reader.Read() || reader.TokenType != JsonTokenType.PropertyName
                || !reader.ValueTextEquals("format")
                || !reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                return null;
            }

            return reader.GetString();
        }

        private static void CleanupPartDirectories(IEnumerable<string> directories)
        {
            foreach (string directory in directories)
            {
                try
                {
                    DeleteDirectoryIfExists(directory);
                }
                catch (IOException)
                {
                    // Cleanup is best-effort after the new manifest is committed.
                }
                catch (UnauthorizedAccessException)
                {
                    // Cleanup is best-effort after the new manifest is committed.
                }
            }
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        private sealed class SegmentedPartWriter : IDisposable
        {
            private readonly string directory;
            private readonly string fileNamePrefix;
            private readonly Guid exportId;
            private readonly long maxPartBytes;
            private readonly bool isTransient;
            private readonly List<LogPartDefinition> parts = new List<LogPartDefinition>();
            private FileStream? fileStream;
            private CappedWriteStream? cappedStream;
            private Utf8JsonWriter? writer;
            private string? currentKind;
            private string? currentFileName;
            private int currentRecordCount;
            private int nextIndex = 1;
            private bool completed;

            public SegmentedPartWriter(
                string directory,
                string fileNamePrefix,
                Guid exportId,
                long maxPartBytes,
                bool isTransient)
            {
                this.directory = directory;
                this.fileNamePrefix = fileNamePrefix;
                this.exportId = exportId;
                this.maxPartBytes = maxPartBytes;
                this.isTransient = isTransient;
                Directory.CreateDirectory(directory);
            }

            public void Write(string kind, object record, Type recordType, string description)
            {
                if (writer is null || !string.Equals(currentKind, kind, StringComparison.Ordinal))
                {
                    FinishCurrentPart();
                    StartPart(kind);
                }

                long recordBytes;
                try
                {
                    recordBytes = MeasureRecord(record, recordType);
                }
                catch (OutputLimitExceededException)
                {
                    throw new OversizedLogRecordException(
                        $"Record {description} is too large to fit in a log part smaller than 500 MiB.");
                }

                const long closingArrayBytes = 1;
                const long separatorBytes = 1; // each part starts with a header record
                if (cappedStream!.BytesWritten + separatorBytes + recordBytes + closingArrayBytes > maxPartBytes)
                {
                    FinishCurrentPart();
                    StartPart(kind);
                    if (cappedStream!.BytesWritten + 1 + recordBytes + closingArrayBytes > maxPartBytes)
                    {
                        throw new OversizedLogRecordException(
                            $"Record {description} is too large to fit in a log part smaller than 500 MiB.");
                    }
                }

                JsonSerializer.Serialize(writer!, record, recordType, CompactJsonOptions);
                writer!.Flush();
                currentRecordCount++;
            }

            public List<LogPartDefinition> Complete()
            {
                FinishCurrentPart();
                completed = true;
                return parts;
            }

            public void Dispose()
            {
                FinishCurrentPart();
                if (!completed)
                    DeleteDirectoryIfExists(directory);
            }

            private void StartPart(string kind)
            {
                if (nextIndex > MaxPartsPerExport)
                    throw new InvalidOperationException($"A log export cannot exceed {MaxPartsPerExport} parts.");

                currentKind = kind;
                currentRecordCount = 0;
                currentFileName = fileNamePrefix + "_" + nextIndex.ToString("D4") + ".lrdpart";
                string path = Path.Combine(directory, currentFileName);
                fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 64 * 1024, FileOptions.SequentialScan);
                cappedStream = new CappedWriteStream(fileStream, maxPartBytes, leaveOpen: true);
                writer = new Utf8JsonWriter(cappedStream, new JsonWriterOptions { Indented = false });
                writer.WriteStartArray();

                object header = isTransient
                    ? new TransientLogPartRecord
                    {
                        Format = CoreLogFormat.TransientPart,
                        Version = CoreLogFormat.Version,
                        Kind = "header",
                        ExportId = exportId,
                        PartIndex = nextIndex,
                        PartKind = kind
                    }
                    : new CoreLogPartRecord
                    {
                        Format = CoreLogFormat.CorePart,
                        Version = CoreLogFormat.Version,
                        Kind = "header",
                        ExportId = exportId,
                        PartIndex = nextIndex,
                        PartKind = kind
                    };
                JsonSerializer.Serialize(writer, header, header.GetType(), CompactJsonOptions);
                writer.Flush();
            }

            private void FinishCurrentPart()
            {
                if (writer is null || fileStream is null || cappedStream is null)
                    return;

                writer.WriteEndArray();
                writer.Flush();
                writer.Dispose();
                writer = null;
                cappedStream.Dispose();
                cappedStream = null;
                fileStream.Flush();
                fileStream.Dispose();
                fileStream = null;

                string path = Path.Combine(directory, currentFileName!);
                long length = new FileInfo(path).Length;
                if (length >= 500L * 1024L * 1024L)
                    throw new OutputLimitExceededException();

                parts.Add(new LogPartDefinition
                {
                    Index = nextIndex,
                    Kind = currentKind!,
                    File = currentFileName!,
                    ByteLength = length,
                    Sha256 = ComputeSha256(path),
                    RecordCount = currentRecordCount
                });
                nextIndex++;
                currentKind = null;
                currentFileName = null;
                currentRecordCount = 0;
            }

            private long MeasureRecord(object record, Type recordType)
            {
                using CappedWriteStream counting = new CappedWriteStream(Stream.Null, maxPartBytes, leaveOpen: true);
                using Utf8JsonWriter writer = new Utf8JsonWriter(counting, new JsonWriterOptions { Indented = false });
                JsonSerializer.Serialize(writer, record, recordType, CompactJsonOptions);
                writer.Flush();
                return counting.BytesWritten;
            }
        }

        private sealed class CappedWriteStream : Stream
        {
            private readonly Stream inner;
            private readonly long maximumBytes;
            private readonly bool leaveOpen;

            public CappedWriteStream(Stream inner, long maximumBytes, bool leaveOpen)
            {
                this.inner = inner;
                this.maximumBytes = maximumBytes;
                this.leaveOpen = leaveOpen;
            }

            public long BytesWritten { get; private set; }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => BytesWritten;
            public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }
            public override void Flush() => inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                EnsureCapacity(count);
                inner.Write(buffer, offset, count);
                BytesWritten += count;
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                EnsureCapacity(buffer.Length);
                inner.Write(buffer);
                BytesWritten += buffer.Length;
            }

            public override void WriteByte(byte value)
            {
                EnsureCapacity(1);
                inner.WriteByte(value);
                BytesWritten++;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !leaveOpen)
                    inner.Dispose();
                base.Dispose(disposing);
            }

            private void EnsureCapacity(int count)
            {
                if (count < 0 || BytesWritten + count > maximumBytes)
                    throw new OutputLimitExceededException();
            }
        }

        private sealed class OutputLimitExceededException : IOException
        {
        }

        private sealed class OversizedLogRecordException : IOException
        {
            public OversizedLogRecordException(string message) : base(message) { }
        }

        private static int WriteLinks(
            Utf8JsonWriter writer,
            IReadOnlyList<Web_Node> orderedNodes,
            LRD_Core core)
        {
            writer.WriteStartArray();
            int count = 0;

            // Core links are stored on their source node. Sorting one source's
            // outgoing edges at a time preserves deterministic order without
            // retaining LinkLog objects for the whole graph.
            foreach (Web_Node source in orderedNodes)
            {
                foreach (Link link in core.GetStoredLinks(source.ID)
                    .Where(link => link.SourceID == source.ID)
                    .OrderBy(link => link.DestinationID.ToString("D"), StringComparer.Ordinal)
                    .ThenBy(link => link.ForwardLink.Id)
                    .ThenBy(link => link.InverseLink.Id)
                    .ThenBy(link => link.Classification.Id))
                {
                    JsonSerializer.Serialize(writer, ToLinkLog(link), LogJson.Options);
                    count++;
                }
            }

            writer.WriteEndArray();
            return count;
        }

        private static void WriteTransientLog(
            Stream output,
            DateTimeOffset exportedAtUtc,
            string coreFile,
            TypeDefinitionLog typeDefinitions,
            LinkedList<Chain_Node>? query,
            LinkedList<Chain_Node>? response)
        {
            using Utf8JsonWriter writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteString("format", CoreLogFormat.Transient);
            writer.WriteNumber("version", CoreLogFormat.Version);
            writer.WritePropertyName("exportedAtUtc");
            JsonSerializer.Serialize(writer, exportedAtUtc, LogJson.Options);
            writer.WriteString("coreFile", coreFile);
            writer.WritePropertyName("typeDefinitions");
            JsonSerializer.Serialize(writer, typeDefinitions, LogJson.Options);

            writer.WritePropertyName("queryChains");
            WriteChainArray(writer, query);
            writer.WritePropertyName("responseChains");
            WriteChainArray(writer, response);

            writer.WriteEndObject();
            writer.Flush();
        }

        private static void WriteChainArray(Utf8JsonWriter writer, LinkedList<Chain_Node>? chain)
        {
            writer.WriteStartArray();
            if (chain is not null && chain.Count > 0)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("nodes");
                writer.WriteStartArray();
                foreach (Chain_Node node in chain)
                    JsonSerializer.Serialize(writer, ToNodeLog(node), LogJson.Options);
                writer.WriteEndArray();

                writer.WritePropertyName("links");
                writer.WriteStartArray();
                foreach (Chain_Node node in chain)
                {
                    Link? next = node.Get_LinkNext();
                    if (next is not null)
                        JsonSerializer.Serialize(writer, ToLinkLog(next), LogJson.Options);
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static FileStream CreateOutputStream(string path)
        {
            return new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
        }

        private static string CreateTempPath(string targetPath)
        {
            return targetPath + ".tmp-" + Guid.NewGuid().ToString("N");
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        internal static string BuildTransientPath(string corePath)
        {
            string fullPath = Path.GetFullPath(corePath);
            string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            string extension = Path.GetExtension(fullPath);
            return Path.Combine(directory, fileName + "_trans" + extension);
        }

        private static TypeDefinitionLog BuildTypeDefinitions()
        {
            return new TypeDefinitionLog
            {
                NodeTypes = NodeTypes.GetAll()
                    .OrderBy(type => type.Id)
                    .Select(type => new TypeNameLog { Id = type.Id, Name = type.Name })
                    .ToList(),
                LinkClasses = LinkClass.GetAll()
                    .OrderBy(linkClass => linkClass.Id)
                    .Select(linkClass => new TypeNameLog { Id = linkClass.Id, Name = linkClass.Name })
                    .ToList(),
                ForwardLinkTypes = ForwardLinkTypes.GetAll()
                    .OrderBy(linkType => linkType.Id)
                    .Select(linkType => new TypeNameLog { Id = linkType.Id, Name = linkType.Name })
                    .ToList(),
                InverseLinkTypes = InverseLinkTypes.GetAll()
                    .OrderBy(linkType => linkType.Id)
                    .Select(linkType => new TypeNameLog { Id = linkType.Id, Name = linkType.Name })
                    .ToList(),
                Relations = RelationRegistry.GetAll()
                    .OrderBy(relation => relation.Id)
                    .Select(relation => new RelationTypeLog
                    {
                        Id = relation.Id,
                        Name = relation.Name,
                        InverseName = relation.InverseName
                    })
                    .ToList()
            };
        }

        private static ConversationLog ToConversationLog(ConversationContext conversation)
        {
            return new ConversationLog
            {
                Id = conversation.ConversationID,
                Options = NlpOptionsLogMapper.ToLog(conversation.Options),
                TurnIndex = conversation.TurnIndex,
                Sequence = conversation.Sequence,
                FocusNodeId = conversation.FocusNodeID,
                LastSubjectNodeId = conversation.LastSubjectNodeID,
                LastObjectNodeId = conversation.LastObjectNodeID,
                LastQualifierNodeId = conversation.LastQualifierNodeID,
                LastAnswerNodeId = conversation.LastAnswerNodeID,
                Mentions = conversation.Mentions
                    .OrderBy(mention => mention.Sequence)
                    .Select(ToMentionLog)
                    .ToList(),
                Provenance = conversation.Provenance
                    .OrderBy(entry => entry.Sequence)
                    .Select(ToProvenanceLog)
                    .ToList()
            };
        }

        private static MentionLog ToMentionLog(ConversationMention mention)
        {
            return new MentionLog
            {
                NodeId = mention.NodeID,
                Label = mention.Label,
                SurfaceText = mention.SurfaceText,
                NodeTypeId = mention.NodeType.Id,
                NodeTypeName = mention.NodeType.Name,
                Role = (int)mention.Role,
                TurnIndex = mention.TurnIndex,
                Sequence = mention.Sequence,
                FromAnswer = mention.FromAnswer
            };
        }

        private static ProvenanceLog ToProvenanceLog(SessionProvenanceEntry entry)
        {
            return new ProvenanceLog
            {
                Sequence = entry.Sequence,
                TurnIndex = entry.TurnIndex,
                SentenceIndex = entry.SentenceIndex,
                SourceText = entry.SourceText,
                Status = entry.Status.ToString(),
                RelationName = entry.RelationName,
                LinkClassId = entry.LinkClass.Id,
                LinkClassName = entry.LinkClass.Name,
                ForwardLinkTypeId = entry.ForwardLinkTypeId,
                InverseLinkTypeId = entry.InverseLinkTypeId,
                SubjectNodeId = entry.SubjectNodeID,
                ObjectNodeId = entry.ObjectNodeID,
                QualifierNodeId = entry.QualifierNodeID,
                TrustSourceNodeId = entry.TrustSourceNodeID,
                TopicNodeId = entry.TopicNodeID,
                NodeIds = entry.NodeIDs.ToList(),
                TemporaryNodeIds = entry.TemporaryNodeIDs.ToList(),
                GraphMutated = entry.GraphMutated,
                IsUsableForAnswer = entry.IsUsableForAnswer,
                Diagnostics = entry.Diagnostics.ToList(),
                Coordination = (int)entry.Coordination,
                CoordinationGroup = entry.CoordinationGroup,
                Negation = (int)entry.Negation
            };
        }

        private static NodeLog ToNodeLog(Node node)
        {
            DataPointer? pointer = node.Get_DataPointer();

            return new NodeLog
            {
                Id = node.ID,
                NodeTypeId = node.Get_NodeType().Id,
                NodeTypeName = node.Get_NodeType().Name,
                Lifetime = node.Get_Lifetime(),
                Ttl = node.Get_TTL(),
                WeightUp = node.Get_WeightUp(),
                WeightDown = node.Get_WeightDown(),
                Label = node.Label,
                Aliases = node.Aliases.ToList(),
                DataPointer = pointer is null
                    ? null
                    : new DataPointerLog
                    {
                        Location = pointer.Location.AbsoluteUri,
                        MediaType = pointer.MediaType,
                        ByteLength = pointer.ByteLength,
                        Checksum = pointer.Checksum
                    }
            };
        }

        private static LinkLog ToLinkLog(Link link)
        {
            return new LinkLog
            {
                SourceId = link.SourceID,
                DestinationId = link.DestinationID,
                ForwardTypeId = link.ForwardLink.Id,
                ForwardTypeName = link.ForwardLink.Name,
                InverseTypeId = link.InverseLink.Id,
                InverseTypeName = link.InverseLink.Name,
                LinkClassId = link.Classification.Id,
                LinkClassName = link.Classification.Name,
                IsActive = link.IsActive,
                Origins = link.Origins
            };
        }

    }
}
