using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD.NLP
{
    /// <summary>Identifies the file syntax used by the data-file importer.</summary>
    internal enum DataFileFormat
    {
        Auto = 0,
        Text = 1,
        Csv = 2,
        Json = 3,
        Xml = 4
    }

    /// <summary>Chooses graph-structured ingestion or natural-language extraction.</summary>
    internal enum DataFileIngestionMode
    {
        StructuredData = 0,
        ExtractTextForNlp = 1
    }

    /// <summary>Configures format, provenance, safety, progress, and cancellation for a data-file import.</summary>
    internal sealed record DataFileImportOptions
    {
        public string FilePath { get; init; } = string.Empty;
        public DataFileFormat Format { get; init; } = DataFileFormat.Auto;
        public DataFileIngestionMode Mode { get; init; } = DataFileIngestionMode.StructuredData;
        public Guid? SourceNodeID { get; init; }
        public Guid? TopicNodeID { get; init; }
        public bool UseSourceAsTopic { get; init; }
        public bool DryRun { get; init; }
        public long MaxFileBytes { get; init; } = 500L * 1024 * 1024;
        public int MaxRecords { get; init; } = 10000;
        public int MaxColumns { get; init; } = 1000;
        public int MaxDepth { get; init; } = 32;
        public int MaxValueCharacters { get; init; } = 15000;
        public int MaxRecordCharacters { get; init; } = 16 * 1024 * 1024;
        public int MaxDiagnostics { get; init; } = 100;
        public int MaxCreatedNodes { get; init; } = 100000;
        public int MaxCreatedLinks { get; init; } = 250000;
        public NlpOptions? PipelineOptions { get; init; }
        public Action<DataFileImportProgress>? Progress { get; init; }
        public CancellationToken CancellationToken { get; init; }
    }

    /// <summary>Reports progress while records or chunks are processed.</summary>
    internal sealed record DataFileImportProgress(int ProcessedRecords, int RecordLimit, string Stage);

    /// <summary>Summarizes record outcomes, graph changes, provenance, and diagnostics for an import.</summary>
    internal sealed record DataFileImportResult
    {
        public string FilePath { get; init; } = string.Empty;
        public DataFileFormat Format { get; init; }
        public DataFileIngestionMode Mode { get; init; }
        public Guid? SourceNodeID { get; init; }
        public bool SourceNodeCreated { get; init; }
        public double? SourceTrustRating { get; init; }
        public bool DryRun { get; init; }
        public bool Empty { get; init; }
        public bool SkippedHighTrust { get; init; }
        public bool Cancelled { get; init; }
        public bool Truncated { get; init; }
        public bool FailedFatally { get; init; }
        public int RecordsProcessed { get; init; }
        public int RecordsSucceeded { get; init; }
        public int RecordsUnsupported { get; init; }
        public int RecordsFailed { get; init; }
        public int NodesCreated { get; init; }
        public int LinksCreated { get; init; }
        public int TextCharacters { get; init; }
        public int TextParagraphs { get; init; }
        public int TextSentences { get; init; }
        public int TextChunkCount { get; init; }
        public int TextIngestedChunks { get; init; }
        public int TextSkippedChunks { get; init; }
        public Guid? ConversationID { get; init; }
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    }

    /// <summary>Parses supported file formats and commits bounded records into the graph.</summary>
    internal static class DataFileImporter
    {
        private const string CsvMediaType = "text/csv";
        private const string JsonMediaType = "application/json";
        private const string XmlMediaType = "application/xml";
        private const string TextMediaType = "text/plain";

        private static readonly HashSet<string> proseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "text", "content", "body", "description", "message", "summary"
        };

        /// <summary>Validates an import request, resolves file identity, and dispatches to its format reader.</summary>
        public static DataFileImportResult Import(LRD_Core core, DataFileImportOptions options)
        {
            ArgumentNullException.ThrowIfNull(core);
            ArgumentNullException.ThrowIfNull(options);
            ValidateOptions(options);

            if (!core.initialized)
                throw new InvalidOperationException("The core graph must be initialized before importing a data file.");

            string path = Path.GetFullPath(options.FilePath);
            if (!File.Exists(path))
                throw new FileNotFoundException("The selected data file does not exist.", path);

            FileInfo fileInfo = new FileInfo(path);
            if (fileInfo.Length > options.MaxFileBytes)
            {
                throw new InvalidOperationException(
                    $"The file is {fileInfo.Length} bytes, which exceeds the {options.MaxFileBytes}-byte limit.");
            }

            DataFileFormat format = options.Format == DataFileFormat.Auto
                ? DetectFormat(path)
                : options.Format;
            if (format == DataFileFormat.Auto)
                throw new InvalidOperationException("The file format could not be detected. Choose Text, CSV, JSON, or XML.");
            if (!Enum.IsDefined(format))
                throw new ArgumentOutOfRangeException(nameof(options), "The selected file format is invalid.");
            if (options.Mode == DataFileIngestionMode.StructuredData && format == DataFileFormat.Text)
                throw new InvalidOperationException("Plain-text files use Extract Text for NLP mode.");

            FileUriInfo fileUri = GetFileUri(path);
            string checksum = ComputeSha256(path, options.CancellationToken);
            DataFileImportState state = new DataFileImportState(core, options, path, fileUri.Uri, fileUri.Key, checksum, format);
            state.ResolveExistingSource();

            if (fileInfo.Length == 0)
            {
                state.Empty = true;
                state.AddDiagnostic("The file is empty; no data was imported.");
                return state.ToResult();
            }

            if (!options.DryRun
                && state.Source is not null
                && state.Source.TrustRating > 0.80
                && string.Equals(state.Source.Get_DataPointer()?.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
            {
                state.SkippedHighTrust = true;
                state.AddDiagnostic("This unchanged file was already imported from a source with trust above 80%; import skipped.");
                return state.ToResult();
            }

            try
            {
                OntologyRelations.EnsureRegistered();
                if (format == DataFileFormat.Text)
                    ImportTextFile(state);
                else
                {
                    switch (format)
                    {
                        case DataFileFormat.Csv:
                            ImportCsv(state);
                            break;
                        case DataFileFormat.Json:
                            ImportJson(state);
                            break;
                        case DataFileFormat.Xml:
                            ImportXml(state);
                            break;
                        default:
                            throw new InvalidOperationException("Unsupported data-file format.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                state.Cancelled = true;
                state.AddDiagnostic("Import cancelled; previously committed records remain.");
            }
            catch (Exception ex)
            {
                state.FailedFatally = true;
                state.RecordsFailed++;
                state.AddDiagnostic("Import stopped: " + ex.Message);
            }
            finally
            {
                state.CompleteSourceMetadataIfEligible();
            }

            return state.ToResult();
        }

        /// <summary>Delegates plain-text chunking and NLP ingestion to the existing text importer.</summary>
        private static void ImportTextFile(DataFileImportState state)
        {
            CancellationToken token = state.Options.CancellationToken;
            ReasoningOptions reasoningOptions = new ReasoningOptions
            {
                ExistingNodeWeightPolicy = state.IsExistingSource
                    ? ExistingNodeWeightUpPolicy.Suppress
                    : ExistingNodeWeightUpPolicy.OncePerPipeline
            };

            TextImportOptions textOptions = new TextImportOptions
            {
                FilePath = state.FilePath,
                SourceNodeID = state.Source?.ID ?? state.Options.SourceNodeID,
                TopicNodeID = state.Options.TopicNodeID,
                DryRun = state.Options.DryRun,
                UseSourceAsTopic = state.Options.UseSourceAsTopic,
                MaxFileBytes = state.Options.MaxFileBytes,
                MaxChunks = state.Options.MaxRecords,
                MaxChunkCharacters = state.Options.MaxValueCharacters,
                MaxDiagnostics = state.Options.MaxDiagnostics,
                PipelineOptions = state.Options.PipelineOptions,
                ReasoningOptions = reasoningOptions,
                CancellationToken = token,
                Progress = progress =>
                {
                    state.Options.Progress?.Invoke(new DataFileImportProgress(
                        progress.ProcessedChunks,
                        progress.TotalChunks,
                        "Text chunk"));
                }
            };

            TextImportResult result = TextFileImporter.Import(state.Core, textOptions);
            state.SourceNodeID = result.SourceNodeID;
            state.SourceNodeCreated |= result.SourceNodeCreated;
            state.ConversationID = result.ConversationID;
            state.RecordsProcessed = result.ProcessedChunks;
            state.RecordsSucceeded = result.IngestedChunks + result.UnsupportedChunks;
            state.RecordsUnsupported = result.UnsupportedChunks;
            state.RecordsFailed = result.FailedChunks;
            state.TextCharacters = result.TotalCharacters;
            state.TextParagraphs = result.ParagraphCount;
            state.TextSentences = result.SentenceCount;
            state.TextChunkCount = result.ChunkCount;
            state.TextIngestedChunks = result.IngestedChunks;
            state.TextSkippedChunks = result.SkippedChunks;
            state.Cancelled = result.Cancelled;
            state.Truncated = result.Truncated;
            foreach (Guid nodeID in result.AffectedNodeIDs)
                state.AffectedNodeIDs.Add(nodeID);
            foreach (string diagnostic in result.Diagnostics)
                state.AddDiagnostic(diagnostic);

            if (state.SourceNodeID.HasValue
                && state.Core.TryGetNode(state.SourceNodeID.Value, out Web_Node? source)
                && source is not null)
            {
                state.Source = source;
                state.IsExistingSource |= !result.SourceNodeCreated;
            }
        }

        /// <summary>Reads CSV tables and commits structured rows or extracted NLP text.</summary>
        private static void ImportCsv(DataFileImportState state)
        {
            using StreamReader reader = new StreamReader(
                state.FilePath,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);

            string[]? headers = null;
            Guid tableNodeID = Guid.Empty;
            int tableIndex = -1;
            int rowIndex = 0;
            bool sawNonBlankRecord = false;
            NlpImportSession? nlpSession = state.Options.Mode == DataFileIngestionMode.ExtractTextForNlp
                ? new NlpImportSession(state)
                : null;

            try
            {
                while (TryReadCsvRecord(
                    reader,
                    state.Options.MaxColumns,
                    state.Options.MaxValueCharacters,
                    state.Options.MaxRecordCharacters,
                    state.Options.CancellationToken,
                    out CsvRecord? csvRecord,
                    out bool endOfFile))
                {
                    if (endOfFile || csvRecord is null)
                        break;

                    if (csvRecord.IsBlank)
                    {
                        headers = null;
                        tableNodeID = Guid.Empty;
                        rowIndex = 0;
                        continue;
                    }

                    sawNonBlankRecord = true;
                    if (csvRecord.Error is not null)
                    {
                        if (csvRecord.Fatal)
                            throw new InvalidDataException(csvRecord.Error);

                        if (headers is not null)
                        {
                            state.RecordsProcessed++;
                            state.RecordsFailed++;
                            state.AddDiagnostic($"CSV table {tableIndex}, row {rowIndex}: {csvRecord.Error}");
                            state.ReportProgress("CSV row failed");
                        }
                        else
                        {
                            state.RecordsFailed++;
                            state.AddDiagnostic("CSV header failed: " + csvRecord.Error);
                        }
                        continue;
                    }

                    if (headers is null)
                    {
                        if (state.RecordsProcessed >= state.Options.MaxRecords)
                        {
                            state.Truncated = true;
                            state.AddDiagnostic($"The record limit of {state.Options.MaxRecords} was reached.");
                            break;
                        }

                        headers = csvRecord.Fields;
                        tableIndex++;
                        rowIndex = 1;

                        if (state.Options.Mode == DataFileIngestionMode.StructuredData)
                        {
                            Guid sourceID = state.EnsureSource();
                            tableNodeID = CreateCsvTable(state, sourceID, headers, tableIndex);
                        }
                        continue;
                    }

                    int currentRow = rowIndex++;
                    state.RecordsProcessed++;
                    if (state.RecordsProcessed > state.Options.MaxRecords)
                    {
                        state.Truncated = true;
                        state.RecordsProcessed--;
                        state.AddDiagnostic($"The record limit of {state.Options.MaxRecords} was reached.");
                        break;
                    }

                    if (csvRecord.Fields.Length != headers.Length)
                    {
                        state.RecordsFailed++;
                        state.AddDiagnostic(
                            $"CSV table {tableIndex}, row {currentRow}: expected {headers.Length} cells but found {csvRecord.Fields.Length}.");
                        state.ReportProgress("CSV row failed");
                        continue;
                    }

                    if (state.Options.Mode == DataFileIngestionMode.StructuredData)
                    {
                        try
                        {
                            GraphRecordBuilder record = BuildCsvDataRow(state, tableIndex, currentRow, headers, csvRecord.Fields, tableNodeID);
                            state.Apply(record);
                            state.RecordsSucceeded++;
                        }
                        catch (ImportLimitException ex)
                        {
                            state.Truncated = true;
                            state.AddDiagnostic(ex.Message);
                            state.ReportProgress("CSV import truncated");
                            break;
                        }
                        catch (ImportRecordException ex)
                        {
                            state.RecordsFailed++;
                            state.AddDiagnostic($"CSV table {tableIndex}, row {currentRow}: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            state.RecordsFailed++;
                            state.AddDiagnostic($"CSV table {tableIndex}, row {currentRow}: {ex.Message}");
                        }
                    }
                    else
                    {
                        nlpSession ??= new NlpImportSession(state);
                        string text = BuildCsvNlpText(headers, csvRecord.Fields);
                        nlpSession.ProcessRecord(text, $"CSV row {currentRow}");
                    }

                    state.ReportProgress($"CSV table {tableIndex}, row {currentRow}");
                }
            }
            finally
            {
                nlpSession?.Dispose();
            }

            if (!sawNonBlankRecord)
            {
                state.Empty = true;
                state.AddDiagnostic("The CSV file contains no records; no data was imported.");
            }
        }

        /// <summary>Creates or updates the CSV table node, header row, and header cells.</summary>
        private static Guid CreateCsvTable(DataFileImportState state, Guid sourceID, string[] headers, int tableIndex)
        {
            string tablePath = "table_" + tableIndex.ToString(CultureInfo.InvariantCulture);
            GraphRecordBuilder record = NewRecordBuilder(state);
            int tableNode = record.AddNode(new GraphNodeSpec(
                NodeTypes.Basics,
                tablePath,
                new[] { "Table" },
                CreatePositionPointer(state.FileUri, GetMediaType(state.Format), tablePath)));
            int rowNode = record.AddNode(new GraphNodeSpec(
                NodeTypes.Basics,
                "0",
                new[] { headers.Length.ToString(CultureInfo.InvariantCulture) },
                CreatePositionPointer(state.FileUri, GetMediaType(state.Format), tablePath + "/row_0")));
            record.AddExternalLink(sourceID, tableNode, "has_table");
            record.AddLink(tableNode, rowNode, "has_row");

            int previousCell = -1;
            for (int column = 0; column < headers.Length; column++)
            {
                int cell = record.AddNode(new GraphNodeSpec(
                    NodeTypes.Basics,
                    headers[column],
                    new[] { "HEADER" },
                    CreatePositionPointer(
                        state.FileUri,
                        GetMediaType(state.Format),
                        tablePath + "/row_0/column_" + column.ToString(CultureInfo.InvariantCulture))));
                record.AddLink(previousCell < 0 ? rowNode : previousCell, cell, "has_cell");
                previousCell = cell;
            }

            AppliedGraphRecord applied = state.Apply(record);
            return applied.NodeIDs[tableNode];
        }

        /// <summary>Builds a bounded graph record for one CSV data row and its ordered cells.</summary>
        private static GraphRecordBuilder BuildCsvDataRow(
            DataFileImportState state,
            int tableIndex,
            int rowIndex,
            string[] headers,
            string[] values,
            Guid tableNodeID)
        {
            string tablePath = "table_" + tableIndex.ToString(CultureInfo.InvariantCulture);
            GraphRecordBuilder record = NewRecordBuilder(state);
            int row = record.AddNode(new GraphNodeSpec(
                NodeTypes.Basics,
                rowIndex.ToString(CultureInfo.InvariantCulture),
                new[] { values.Length.ToString(CultureInfo.InvariantCulture) },
                CreatePositionPointer(
                    state.FileUri,
                    GetMediaType(state.Format),
                    tablePath + "/row_" + rowIndex.ToString(CultureInfo.InvariantCulture))));
            record.AddExternalLink(tableNodeID, row, "has_row");

            int previousCell = -1;
            for (int column = 0; column < values.Length; column++)
            {
                int cell = record.AddNode(new GraphNodeSpec(
                    NodeTypes.Basics,
                    values[column],
                    new[] { headers[column] },
                    CreatePositionPointer(
                        state.FileUri,
                        GetMediaType(state.Format),
                        tablePath + "/row_" + rowIndex.ToString(CultureInfo.InvariantCulture)
                            + "/column_" + column.ToString(CultureInfo.InvariantCulture))));

                record.AddLink(previousCell < 0 ? row : previousCell, cell, "has_cell");
                previousCell = cell;
            }

            return record;
        }

        /// <summary>Converts a CSV row to a subject-predicate-object sentence or labeled field text.</summary>
        private static string BuildCsvNlpText(string[] headers, string[] values)
        {
            int subject = FindHeader(headers, "subject");
            int predicate = FindHeader(headers, "predicate");
            if (predicate < 0)
                predicate = FindHeader(headers, "relation");
            int objectIndex = FindHeader(headers, "object");
            if (subject >= 0 && predicate >= 0 && objectIndex >= 0
                && subject != predicate && subject != objectIndex && predicate != objectIndex)
            {
                return string.Join(" ", values[subject], values[predicate], values[objectIndex]).Trim() + ".";
            }

            List<string> fields = new List<string>();
            for (int index = 0; index < Math.Min(headers.Length, values.Length); index++)
            {
                if (values[index].Length > 0)
                    fields.Add(headers[index] + ": " + values[index]);
            }
            return fields.Count == 0 ? string.Empty : string.Join("; ", fields) + ".";
        }

        /// <summary>Finds a CSV header by a case-insensitive trimmed name.</summary>
        private static int FindHeader(IReadOnlyList<string> headers, string name)
        {
            for (int index = 0; index < headers.Count; index++)
            {
                if (string.Equals(headers[index].Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }

        /// <summary>Reads and validates one CSV record without buffering the whole input file.</summary>
        private static bool TryReadCsvRecord(
            TextReader reader,
            int maxColumns,
            int maxValueCharacters,
            int maxRecordCharacters,
            CancellationToken token,
            out CsvRecord? record,
            out bool endOfFile)
        {
            record = null;
            endOfFile = false;
            List<string> fields = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQuotes = false;
            bool afterQuote = false;
            bool sawAny = false;
            bool sawNonWhitespace = false;
            bool valueTooLong = false;
            bool recordTooLong = false;
            bool tooManyColumns = false;
            int recordCharacters = 0;
            string? error = null;

            void AddField()
            {
                if (fields.Count < maxColumns)
                    fields.Add(field.ToString());
                else
                    tooManyColumns = true;
                field.Clear();
                afterQuote = false;
                valueTooLong = false;
            }

            void Append(char value)
            {
                if (recordCharacters < maxRecordCharacters)
                    recordCharacters++;
                else
                    recordTooLong = true;

                if (field.Length < maxValueCharacters)
                    field.Append(value);
                else
                    valueTooLong = true;
            }

            while (true)
            {
                token.ThrowIfCancellationRequested();
                int code = reader.Read();
                if (code < 0)
                {
                    endOfFile = !sawAny && fields.Count == 0 && field.Length == 0;
                    if (endOfFile)
                        return false;
                    if (inQuotes)
                        error = "A quoted CSV field was not closed before end of file.";
                    AddField();
                    break;
                }

                char current = (char)code;
                sawAny = true;
                if (!char.IsWhiteSpace(current) || current == '"' || current == ',')
                    sawNonWhitespace = true;

                if (inQuotes)
                {
                    if (current == '"')
                    {
                        if (reader.Peek() == '"')
                        {
                            reader.Read();
                            Append('"');
                        }
                        else
                        {
                            inQuotes = false;
                            afterQuote = true;
                        }
                    }
                    else
                    {
                        Append(current);
                    }
                    continue;
                }

                if (current == '\r' || current == '\n')
                {
                    if (current == '\r' && reader.Peek() == '\n')
                        reader.Read();
                    AddField();
                    break;
                }

                if (afterQuote)
                {
                    if (current == ',')
                    {
                        AddField();
                        continue;
                    }
                    if (char.IsWhiteSpace(current))
                        continue;
                    error ??= "Unexpected character after a quoted CSV field.";
                    Append(current);
                    afterQuote = false;
                    continue;
                }

                if (current == ',')
                {
                    AddField();
                    continue;
                }
                if (current == '"' && field.Length == 0)
                {
                    inQuotes = true;
                    continue;
                }
                if (current == '"')
                    error ??= "A quote occurred inside an unquoted CSV field.";
                Append(current);
            }

            if (tooManyColumns)
                error ??= $"The CSV record exceeds the {maxColumns}-column limit.";
            if (valueTooLong)
                error ??= $"A CSV value exceeds the {maxValueCharacters}-character limit.";
            if (recordTooLong)
                error ??= $"The CSV record exceeds the {maxRecordCharacters}-character limit.";

            record = new CsvRecord(
                fields.ToArray(),
                error,
                IsBlank: !sawNonWhitespace,
                Fatal: error == "A quoted CSV field was not closed before end of file.");
            return true;
        }

        /// <summary>Streams JSON root values and imports each array item or single root record.</summary>
        private static void ImportJson(DataFileImportState state)
        {
            using StreamReader textReader = new StreamReader(
                state.FilePath,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            JsonCharacterReader reader = new JsonCharacterReader(textReader);
            int first = reader.ReadNonWhitespace();
            if (first < 0)
            {
                state.Empty = true;
                state.AddDiagnostic("The JSON file contains no value; no data was imported.");
                return;
            }

            using NlpImportSession? nlp = state.Options.Mode == DataFileIngestionMode.ExtractTextForNlp
                ? new NlpImportSession(state)
                : null;

            if (first == '[')
            {
                Guid rootID = Guid.Empty;
                int itemIndex = 0;
                bool sawItem = false;

                while (true)
                {
                    state.Options.CancellationToken.ThrowIfCancellationRequested();
                    int next = reader.ReadNonWhitespace();
                    if (next < 0)
                        throw new JsonException("The JSON root array was not closed.");
                    if (next == ']')
                        break;
                    if (next == ',')
                        throw new JsonException("A JSON array contains an empty item.");
                    if (state.RecordsProcessed >= state.Options.MaxRecords)
                    {
                        state.Truncated = true;
                        state.AddDiagnostic($"The record limit of {state.Options.MaxRecords} was reached.");
                        break;
                    }

                    sawItem = true;
                    JsonRawValue raw = ReadJsonValue(reader, next, state.Options.MaxRecordCharacters, state.Options.CancellationToken);
                    int separator = reader.ReadNonWhitespace();
                    if (separator >= 0 && separator != ',' && separator != ']')
                        throw new JsonException("A JSON array item was not followed by a comma or closing bracket.");

                    state.RecordsProcessed++;
                    try
                    {
                        if (raw.TooLong)
                            throw new ImportRecordException($"JSON record {itemIndex} exceeds the {state.Options.MaxRecordCharacters}-character limit.");
                        using JsonDocument document = JsonDocument.Parse(raw.Text, new JsonDocumentOptions
                        {
                            MaxDepth = state.Options.MaxDepth
                        });

                        if (state.Options.Mode == DataFileIngestionMode.StructuredData)
                        {
                            if (rootID == Guid.Empty)
                                rootID = CreateJsonRoot(state);
                            ImportJsonArrayItem(state, rootID, document.RootElement, itemIndex);
                            state.RecordsSucceeded++;
                        }
                        else
                        {
                            string text = ExtractJsonText(document.RootElement);
                            nlp!.ProcessRecord(text, $"JSON record {itemIndex}");
                        }
                    }
                    catch (ImportRecordException ex)
                    {
                        state.RecordsFailed++;
                        state.AddDiagnostic(ex.Message);
                    }
                    catch (JsonException ex)
                    {
                        state.RecordsFailed++;
                        state.AddDiagnostic($"JSON record {itemIndex} is malformed: {ex.Message}");
                    }

                    state.ReportProgress($"JSON record {itemIndex}");
                    itemIndex++;
                    if (separator == ']')
                        break;
                }

                if (!sawItem)
                {
                    state.Empty = true;
                    state.AddDiagnostic("The JSON root array is empty; no data was imported.");
                }

                if (!state.Truncated && reader.ReadNonWhitespace() >= 0)
                    throw new JsonException("Unexpected content follows the JSON root value.");
                return;
            }

            JsonRawValue rootRaw = ReadJsonValue(reader, first, state.Options.MaxRecordCharacters, state.Options.CancellationToken);
            if (reader.ReadNonWhitespace() >= 0)
                throw new JsonException("Unexpected content follows the JSON root value.");

            state.RecordsProcessed++;
            try
            {
                if (rootRaw.TooLong)
                    throw new ImportRecordException($"JSON root exceeds the {state.Options.MaxRecordCharacters}-character limit.");
                using JsonDocument document = JsonDocument.Parse(rootRaw.Text, new JsonDocumentOptions
                {
                    MaxDepth = state.Options.MaxDepth
                });

                if (state.Options.Mode == DataFileIngestionMode.StructuredData)
                {
                    ImportJsonRootRecord(state, document.RootElement);
                    state.RecordsSucceeded++;
                }
                else
                {
                    string text = ExtractJsonText(document.RootElement);
                    nlp!.ProcessRecord(text, "JSON root record");
                }
            }
            catch (ImportRecordException ex)
            {
                state.RecordsFailed++;
                state.AddDiagnostic(ex.Message);
            }
            catch (JsonException ex)
            {
                state.RecordsFailed++;
                state.AddDiagnostic("The JSON root is malformed: " + ex.Message);
                throw;
            }
            state.ReportProgress("JSON root record");
        }

        /// <summary>Creates or updates the structural root node for a JSON array.</summary>
        private static Guid CreateJsonRoot(DataFileImportState state)
        {
            Guid sourceID = state.EnsureSource();
            GraphRecordBuilder root = NewRecordBuilder(state);
            int rootNode = root.AddNode(new GraphNodeSpec(
                NodeTypes.Basics,
                "root",
                new[] { "JSON Element" },
                CreatePositionPointer(state.FileUri, GetMediaType(state.Format), "json/root")));
            root.AddExternalLink(sourceID, rootNode, "has_record");
            return state.Apply(root).NodeIDs[rootNode];
        }

        /// <summary>Builds and applies a structured graph record for a non-array JSON root value.</summary>
        private static void ImportJsonRootRecord(DataFileImportState state, JsonElement value)
        {
            Guid sourceID = state.EnsureSource();
            GraphRecordBuilder record = NewRecordBuilder(state);
            int root = record.AddNode(new GraphNodeSpec(
                NodeTypes.Basics,
                "root",
                new[] { "JSON Element" },
                CreatePositionPointer(state.FileUri, GetMediaType(state.Format), "json/root")));
            record.AddExternalLink(sourceID, root, "has_record");
            AddJsonValueContents(state, record, root, value, "json/root", 1);
            state.Apply(record);
        }

        /// <summary>Builds and applies one structured graph record for a JSON array item.</summary>
        private static void ImportJsonArrayItem(DataFileImportState state, Guid rootID, JsonElement value, int itemIndex)
        {
            GraphRecordBuilder record = NewRecordBuilder(state);
            string itemPath = "json/" + itemIndex.ToString(CultureInfo.InvariantCulture);
            int item = record.AddNode(new GraphNodeSpec(
                NodeTypes.Basics,
                "item",
                new[] { "array_" + itemIndex.ToString(CultureInfo.InvariantCulture) },
                CreatePositionPointer(state.FileUri, GetMediaType(state.Format), itemPath)));
            record.AddExternalLink(rootID, item, "has_item");
            AddJsonValueContents(state, record, item, value, itemPath, 1);
            state.Apply(record);
        }

        /// <summary>Adds typed JSON scalar, object, or array contents to a structured record.</summary>
        private static void AddJsonValueContents(
            DataFileImportState state,
            GraphRecordBuilder record,
            int container,
            JsonElement value,
            string path,
            int depth)
        {
            if (depth > state.Options.MaxDepth)
                throw new ImportRecordException($"JSON nesting exceeds the {state.Options.MaxDepth}-level limit.");

            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    AddJsonObjectProperties(state, record, container, value, path, depth);
                    break;
                case JsonValueKind.Array:
                    AddJsonArrayItems(state, record, container, value, path, depth);
                    break;
                case JsonValueKind.String:
                    AddValueNode(state, record, value.GetString() ?? string.Empty,
                        InferTextValueKind(value.GetString() ?? string.Empty),
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), path + "/value"),
                        container, "has_value");
                    break;
                case JsonValueKind.Number:
                    AddValueNode(state, record, value.GetRawText(), ValueKind.Number,
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), path + "/value"),
                        container, "has_value");
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    AddValueNode(state, record, value.GetRawText(), ValueKind.Boolean,
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), path + "/value"),
                        container, "has_value");
                    break;
                case JsonValueKind.Null:
                    AddValueNode(state, record, "null", ValueKind.Null,
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), path + "/value"),
                        container, "has_value");
                    break;
                default:
                    throw new ImportRecordException("Unsupported JSON value kind.");
            }
        }

        /// <summary>Adds JSON object properties and recursively maps their values.</summary>
        private static void AddJsonObjectProperties(
            DataFileImportState state,
            GraphRecordBuilder record,
            int parent,
            JsonElement value,
            string path,
            int depth)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                state.Options.CancellationToken.ThrowIfCancellationRequested();
                string childPath = path + "/" + EscapeJsonPointer(property.Name);
                int propertyNode = record.AddNode(new GraphNodeSpec(
                    OntologyTypes.Properties,
                    property.Name,
                    new[] { "Property" },
                    CreatePositionPointer(state.FileUri, GetMediaType(state.Format), childPath + "/property")));
                record.AddLink(parent, propertyNode, "has_property");

                JsonElement propertyValue = property.Value;
                if (propertyValue.ValueKind == JsonValueKind.Object)
                {
                    int element = record.AddNode(new GraphNodeSpec(
                        NodeTypes.Basics,
                        property.Name,
                        new[] { "JSON Element" },
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), childPath)));
                    record.AddLink(propertyNode, element, "has_child");
                    AddJsonValueContents(state, record, element, propertyValue, childPath, depth + 1);
                }
                else if (propertyValue.ValueKind == JsonValueKind.Array)
                {
                    AddJsonArrayItems(state, record, propertyNode, propertyValue, childPath, depth + 1);
                }
                else
                {
                    AddJsonValueContents(state, record, propertyNode, propertyValue, childPath, depth + 1);
                }
            }
        }

        /// <summary>Adds JSON array members with stable index-based position pointers.</summary>
        private static void AddJsonArrayItems(
            DataFileImportState state,
            GraphRecordBuilder record,
            int parent,
            JsonElement array,
            string path,
            int depth)
        {
            int index = 0;
            foreach (JsonElement itemValue in array.EnumerateArray())
            {
                state.Options.CancellationToken.ThrowIfCancellationRequested();
                string itemName = "array_" + index.ToString(CultureInfo.InvariantCulture);
                string itemPath = path + "/" + index.ToString(CultureInfo.InvariantCulture);
                int item = record.AddNode(new GraphNodeSpec(
                    NodeTypes.Basics,
                    "item",
                    new[] { itemName },
                    CreatePositionPointer(state.FileUri, GetMediaType(state.Format), itemPath)));
                record.AddLink(parent, item, "has_item");
                AddJsonValueContents(state, record, item, itemValue, itemPath, depth + 1);
                index++;
            }
        }

        /// <summary>Extracts known prose fields or fallback string leaves from a JSON value.</summary>
        private static string ExtractJsonText(JsonElement root)
        {
            List<string> known = new List<string>();
            CollectJsonStrings(root, known, collectKnownFields: true);
            List<string> values = known.Count > 0 ? known : new List<string>();
            if (known.Count == 0)
                CollectJsonStrings(root, values, collectKnownFields: false);
            return string.Join(". ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        /// <summary>Collects prose-key strings or all string leaves in traversal order.</summary>
        private static void CollectJsonStrings(JsonElement element, List<string> values, bool collectKnownFields)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (collectKnownFields
                            && proseKeys.Contains(property.Name)
                            && property.Value.ValueKind == JsonValueKind.String)
                        {
                            values.Add(property.Value.GetString() ?? string.Empty);
                        }
                        else
                        {
                            CollectJsonStrings(property.Value, values, collectKnownFields);
                        }
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                        CollectJsonStrings(child, values, collectKnownFields);
                    break;
                case JsonValueKind.String when !collectKnownFields:
                    values.Add(element.GetString() ?? string.Empty);
                    break;
            }
        }

        /// <summary>Streams XML records into structural graph data or extracted NLP text.</summary>
        private static void ImportXml(DataFileImportState state)
        {
            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = false,
                MaxCharactersInDocument = state.Options.MaxFileBytes,
                MaxCharactersFromEntities = 0
            };

            using XmlReader reader = XmlReader.Create(state.FilePath, settings);
            reader.MoveToContent();
            if (reader.NodeType != XmlNodeType.Element)
            {
                state.Empty = true;
                state.AddDiagnostic("The XML file contains no document element.");
                return;
            }

            string rootPath = "xml/" + EscapeXmlName(ExpandedName(reader));
            if (state.Options.Mode == DataFileIngestionMode.ExtractTextForNlp)
            {
                ImportXmlText(state, reader);
                return;
            }

            GraphRecordBuilder rootRecord = NewRecordBuilder(state);
            int rootNode = AddXmlElementNode(state, rootRecord, reader, rootPath);
            if (!state.Options.DryRun)
                rootRecord.AddExternalLink(state.EnsureSource(), rootNode, "has_record");
            AppliedGraphRecord rootApplied = state.Apply(rootRecord);
            Guid rootID = rootApplied.NodeIDs[rootNode];

            if (reader.IsEmptyElement)
            {
                state.RecordsProcessed++;
                state.RecordsSucceeded++;
                state.ReportProgress("XML root record");
                return;
            }

            int rootDepth = reader.Depth;
            int childIndex = 0;
            int textIndex = 0;
            bool hasChildElements = false;
            GraphRecordBuilder rootText = NewRecordBuilder(state);
            while (reader.Read())
            {
                state.Options.CancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rootDepth)
                    break;

                if (reader.NodeType == XmlNodeType.Element && reader.Depth == rootDepth + 1)
                {
                    hasChildElements = true;
                    if (state.RecordsProcessed >= state.Options.MaxRecords)
                    {
                        state.Truncated = true;
                        state.AddDiagnostic($"The record limit of {state.Options.MaxRecords} was reached.");
                        break;
                    }

                    state.RecordsProcessed++;
                    string childPath = rootPath + "/" + EscapeXmlName(ExpandedName(reader))
                        + "[" + childIndex.ToString(CultureInfo.InvariantCulture) + "]";
                    childIndex++;
                    GraphRecordBuilder record = NewRecordBuilder(state);
                    try
                    {
                        int childNode = ReadXmlElement(state, reader, record, childPath, depth: 2);
                        record.AddExternalLink(rootID, childNode, "has_child");
                        state.Apply(record);
                        state.RecordsSucceeded++;
                    }
                    catch (ImportRecordException ex)
                    {
                        state.RecordsFailed++;
                        state.AddDiagnostic("XML record failed: " + ex.Message);
                        if (reader.ReadState == ReadState.Interactive && reader.NodeType == XmlNodeType.Element)
                            reader.Skip();
                    }
                    state.ReportProgress("XML child record");
                    continue;
                }

                if (reader.Depth == rootDepth + 1
                    && reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA
                    && !string.IsNullOrWhiteSpace(reader.Value))
                {
                    int textValue = AddValueNode(state, rootText, reader.Value, InferTextValueKind(reader.Value),
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), rootPath + "/text_" + textIndex++),
                        parentIndex: -1, relation: "has_value");
                    rootText.AddExternalLink(rootID, textValue, "has_value");
                }
            }

            if (rootText.Nodes.Count > 0)
            {
                state.Apply(rootText);
                if (!hasChildElements)
                {
                    state.RecordsProcessed++;
                    state.RecordsSucceeded++;
                    state.ReportProgress("XML text record");
                }
            }
            else if (!hasChildElements && !reader.IsEmptyElement)
            {
                state.RecordsProcessed++;
                state.RecordsSucceeded++;
                state.ReportProgress("XML empty root record");
            }
        }

        /// <summary>Streams XML text records through one NLP session without creating structural nodes.</summary>
        private static void ImportXmlText(DataFileImportState state, XmlReader reader)
        {
            using NlpImportSession nlp = new NlpImportSession(state);
            int rootDepth = reader.Depth;
            int recordIndex = 0;
            bool hasChildElements = false;
            bool rootTextTooLong = false;
            StringBuilder rootText = new StringBuilder();

            void AppendRootText(string text)
            {
                if (rootText.Length + text.Length > state.Options.MaxRecordCharacters)
                {
                    rootTextTooLong = true;
                    return;
                }
                if (!rootTextTooLong)
                    rootText.Append(text);
            }

            if (reader.IsEmptyElement)
            {
                state.RecordsProcessed++;
                nlp.ProcessRecord(string.Empty, "XML root record");
                state.ReportProgress("XML root record");
                return;
            }

            while (reader.Read())
            {
                state.Options.CancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rootDepth)
                    break;

                if (reader.NodeType == XmlNodeType.Element && reader.Depth == rootDepth + 1)
                {
                    hasChildElements = true;
                    if (state.RecordsProcessed >= state.Options.MaxRecords)
                    {
                        state.Truncated = true;
                        state.AddDiagnostic($"The record limit of {state.Options.MaxRecords} was reached.");
                        break;
                    }

                    state.RecordsProcessed++;
                    int currentRecord = recordIndex++;
                    string text = ReadXmlTextSubtree(
                        reader,
                        state.Options.MaxRecordCharacters,
                        state.Options.CancellationToken,
                        out bool tooLong);
                    if (tooLong)
                    {
                        state.RecordsFailed++;
                        state.AddDiagnostic(
                            $"XML record {currentRecord} exceeds the {state.Options.MaxRecordCharacters}-character limit.");
                    }
                    else
                    {
                        nlp.ProcessRecord(text, $"XML record {currentRecord}");
                    }
                    state.ReportProgress($"XML record {currentRecord}");
                    continue;
                }

                if (reader.Depth == rootDepth + 1
                    && reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA)
                {
                    AppendRootText(reader.Value);
                }
            }

            if (!hasChildElements)
            {
                state.RecordsProcessed++;
                if (rootTextTooLong)
                {
                    state.RecordsFailed++;
                    state.AddDiagnostic(
                        $"The XML root record exceeds the {state.Options.MaxRecordCharacters}-character limit.");
                }
                else
                {
                    nlp.ProcessRecord(rootText.ToString(), "XML root record");
                }
                state.ReportProgress("XML root record");
            }
        }

        /// <summary>Collects text and CDATA from one XML subtree within a bounded character budget.</summary>
        private static string ReadXmlTextSubtree(
            XmlReader reader,
            int maxCharacters,
            CancellationToken cancellationToken,
            out bool tooLong)
        {
            StringBuilder text = new StringBuilder();
            tooLong = false;
            using XmlReader subtree = reader.ReadSubtree();
            while (subtree.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (subtree.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA))
                    continue;

                if (subtree.Value.Length > maxCharacters - text.Length)
                {
                    tooLong = true;
                    continue;
                }
                if (!tooLong)
                    text.Append(subtree.Value);
            }
            return text.ToString();
        }

        /// <summary>Adds one XML element and its attribute properties to a structured record.</summary>
        private static int AddXmlElementNode(
            DataFileImportState state,
            GraphRecordBuilder record,
            XmlReader reader,
            string path)
        {
            string name = ExpandedName(reader);
            int element = record.AddNode(new GraphNodeSpec(
                NodeTypes.Basics,
                name,
                new[] { "XML Element" },
                CreatePositionPointer(state.FileUri, GetMediaType(state.Format), path)));

            if (reader.HasAttributes)
            {
                int attributeIndex = 0;
                while (reader.MoveToNextAttribute())
                {
                    string attributeName = ExpandedName(reader);
                    string attributePath = path + "/@" + EscapeXmlName(attributeName)
                        + "[" + attributeIndex.ToString(CultureInfo.InvariantCulture) + "]";
                    int property = record.AddNode(new GraphNodeSpec(
                        OntologyTypes.Properties,
                        attributeName,
                        new[] { "XML Attribute" },
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), attributePath + "/property")));
                    record.AddLink(element, property, "has_attribute");
                    AddValueNode(state, record, reader.Value, InferTextValueKind(reader.Value),
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), attributePath + "/value"),
                        property, "has_value");
                    attributeIndex++;
                }
                reader.MoveToElement();
            }

            return element;
        }

        /// <summary>Recursively maps an XML element, attributes, child elements, and text values.</summary>
        private static int ReadXmlElement(
            DataFileImportState state,
            XmlReader reader,
            GraphRecordBuilder record,
            string path,
            int depth)
        {
            if (depth > state.Options.MaxDepth)
                throw new ImportRecordException($"XML nesting exceeds the {state.Options.MaxDepth}-level limit.");

            int element = AddXmlElementNode(state, record, reader, path);
            bool isEmpty = reader.IsEmptyElement;
            if (isEmpty)
            {
                AddValueNode(state, record, string.Empty, ValueKind.String,
                    CreatePositionPointer(state.FileUri, GetMediaType(state.Format), path + "/text_0"),
                    element, "has_value");
                return element;
            }

            int elementDepth = reader.Depth;
            int childIndex = 0;
            int textIndex = 0;
            while (reader.Read())
            {
                state.Options.CancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == elementDepth)
                    break;

                if (reader.NodeType == XmlNodeType.Element)
                {
                    string childPath = path + "/" + EscapeXmlName(ExpandedName(reader))
                        + "[" + childIndex.ToString(CultureInfo.InvariantCulture) + "]";
                    childIndex++;
                    int child = ReadXmlElement(state, reader, record, childPath, depth + 1);
                    record.AddLink(element, child, "has_child");
                    continue;
                }

                if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA
                    && !string.IsNullOrWhiteSpace(reader.Value))
                {
                    AddValueNode(state, record, reader.Value, InferTextValueKind(reader.Value),
                        CreatePositionPointer(state.FileUri, GetMediaType(state.Format), path + "/text_" + textIndex++),
                        element, "has_value");
                }
            }

            return element;
        }

        /// <summary>Returns an XML element or attribute name with its namespace URI.</summary>
        private static string ExpandedName(XmlReader reader)
        {
            return string.IsNullOrEmpty(reader.NamespaceURI)
                ? reader.LocalName
                : "{" + reader.NamespaceURI + "}" + reader.LocalName;
        }

        /// <summary>Escapes an XML name for inclusion in a structural position path.</summary>
        private static string EscapeXmlName(string value)
        {
            return Uri.EscapeDataString(value);
        }

        /// <summary>Infers a supported file format from the file extension.</summary>
        private static DataFileFormat DetectFormat(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".txt" or ".md" or ".log" => DataFileFormat.Text,
                ".csv" => DataFileFormat.Csv,
                ".json" => DataFileFormat.Json,
                ".xml" => DataFileFormat.Xml,
                _ => DataFileFormat.Auto
            };
        }

        /// <summary>Creates a normalized absolute file URI and platform-appropriate comparison metadata.</summary>
        private static FileUriInfo GetFileUri(string path)
        {
            Uri uri = new Uri(path);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string key = uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.SafeUnescaped);
            return new FileUriInfo(uri, key, comparison);
        }

        /// <summary>Computes a streaming SHA-256 checksum while honoring cancellation.</summary>
        private static string ComputeSha256(string path, CancellationToken cancellationToken)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.SequentialScan);
            using SHA256 sha = SHA256.Create();
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sha.TransformBlock(buffer, 0, read, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        /// <summary>Rejects invalid file format, mode, or resource-limit options.</summary>
        private static void ValidateOptions(DataFileImportOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.FilePath))
                throw new ArgumentException("A data-file path is required.", nameof(options));
            if (!Enum.IsDefined(options.Format))
                throw new ArgumentOutOfRangeException(nameof(options), "The selected data-file format is invalid.");
            if (!Enum.IsDefined(options.Mode))
                throw new ArgumentOutOfRangeException(nameof(options), "The selected ingestion mode is invalid.");
            if (options.MaxFileBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(options), "MaxFileBytes must be positive.");
            if (options.MaxRecords <= 0 || options.MaxColumns <= 0 || options.MaxDepth <= 0
                || options.MaxValueCharacters <= 0 || options.MaxRecordCharacters <= 0
                || options.MaxDiagnostics <= 0 || options.MaxCreatedNodes <= 0 || options.MaxCreatedLinks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "All data import limits must be positive.");
            }
        }

        /// <summary>Stores the canonical URI and comparison metadata for an import file.</summary>
        private readonly record struct FileUriInfo(Uri Uri, string Key, StringComparison Comparison);

        /// <summary>Tracks a single import's source identity, counters, diagnostics, and graph operations.</summary>
        private sealed class DataFileImportState
        {
            private readonly Dictionary<string, Web_Node> nodesByDataPointer;
            private readonly List<string> diagnostics = new List<string>();
            private bool diagnosticsTruncated;

            /// <summary>Creates import state and indexes current nodes by their data-pointer URI.</summary>
            public DataFileImportState(
                LRD_Core core,
                DataFileImportOptions options,
                string filePath,
                Uri fileUri,
                string fileUriKey,
                string checksum,
                DataFileFormat format)
            {
                Core = core;
                Options = options;
                FilePath = filePath;
                FileUri = fileUri;
                FileUriKey = fileUriKey;
                Checksum = checksum;
                Format = format;
                nodesByDataPointer = new Dictionary<string, Web_Node>(
                    OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

                if (core.GetDictionary() is { } graph)
                {
                    foreach (Web_Node node in graph.Values)
                    {
                        if (node.Get_DataPointer() is { } pointer)
                            nodesByDataPointer[GetPointerKey(pointer.Location)] = node;
                    }
                }
            }

            public LRD_Core Core { get; }
            public DataFileImportOptions Options { get; }
            public string FilePath { get; }
            public Uri FileUri { get; }
            public string FileUriKey { get; }
            public string Checksum { get; }
            public DataFileFormat Format { get; }
            public Web_Node? Source { get; set; }
            public Guid? SourceNodeID { get; set; }
            public bool SourceNodeCreated { get; set; }
            public bool IsExistingSource { get; set; }
            public bool Empty { get; set; }
            public bool SkippedHighTrust { get; set; }
            public bool Cancelled { get; set; }
            public bool Truncated { get; set; }
            public bool FailedFatally { get; set; }
            public int RecordsProcessed { get; set; }
            public int RecordsSucceeded { get; set; }
            public int RecordsUnsupported { get; set; }
            public int RecordsFailed { get; set; }
            public int NodesCreated { get; set; }
            public int LinksCreated { get; set; }
            public int TextCharacters { get; set; }
            public int TextParagraphs { get; set; }
            public int TextSentences { get; set; }
            public int TextChunkCount { get; set; }
            public int TextIngestedChunks { get; set; }
            public int TextSkippedChunks { get; set; }
            public int GraphNodeOperations { get; set; }
            public int GraphLinkOperations { get; set; }
            public Guid? ConversationID { get; set; }
            public HashSet<Guid> AffectedNodeIDs { get; } = new HashSet<Guid>();

            /// <summary>Resolves or validates the source node associated with the normalized file URI.</summary>
            public void ResolveExistingSource()
            {
                if (Options.SourceNodeID.HasValue)
                {
                    if (!Core.TryGetNode(Options.SourceNodeID.Value, out Web_Node? selected) || selected is null)
                        throw new InvalidOperationException($"Source node '{Options.SourceNodeID}' was not found.");
                    if (selected.Get_NodeType().Id != NodeTypes.Sources.Id)
                        throw new InvalidOperationException("The selected Source ID is not a Source node.");
                    if (selected.Get_DataPointer() is { } selectedPointer
                        && !LocationEquals(selectedPointer.Location, FileUri))
                    {
                        throw new InvalidOperationException("The selected Source node points to a different file.");
                    }

                    Source = selected;
                    SourceNodeID = selected.ID;
                    IsExistingSource = true;
                    return;
                }

                if (Core.GetDictionary() is { } graph)
                {
                    Source = graph.Values.FirstOrDefault(node =>
                        node.Get_NodeType().Id == NodeTypes.Sources.Id
                        && node.Get_DataPointer() is { } pointer
                        && LocationEquals(pointer.Location, FileUri));
                }

                if (Source is not null)
                {
                    SourceNodeID = Source.ID;
                    IsExistingSource = true;
                }
            }

            /// <summary>Returns the existing source node or creates its file-backed provenance node.</summary>
            public Guid EnsureSource()
            {
                if (Source is not null)
                    return Source.ID;
                if (Options.DryRun)
                    return Guid.Empty;

                Web_Node source = new Web_Node(Guid.NewGuid(), NodeTypes.Sources, NodeLifetime.Permanent, 0);
                source.Set_Label("Source: " + Path.GetFileName(FilePath));
                source.Set_DataPointer(new DataPointer(FileUri, GetMediaType(Format), new FileInfo(FilePath).Length));
                if (!Core.AddExistingNode(source))
                    throw new InvalidOperationException("The file Source node could not be added.");

                Source = source;
                SourceNodeID = source.ID;
                SourceNodeCreated = true;
                nodesByDataPointer[GetPointerKey(FileUri)] = source;
                return source.ID;
            }

            /// <summary>Finds a graph node indexed by the supplied pointer's location URI.</summary>
            public Guid? FindByPointer(DataPointer pointer)
            {
                return nodesByDataPointer.TryGetValue(GetPointerKey(pointer.Location), out Web_Node? node)
                    ? node.ID
                    : null;
            }

            /// <summary>Validates and applies one structured record, upserting nodes by file position.</summary>
            public AppliedGraphRecord Apply(GraphRecordBuilder record)
            {
                if (record.Error is not null)
                    throw new ImportRecordException(record.Error);

                GraphNodeOperations += record.Nodes.Count;
                GraphLinkOperations += record.Links.Count;
                if (GraphNodeOperations > Options.MaxCreatedNodes || GraphLinkOperations > Options.MaxCreatedLinks)
                {
                    Truncated = true;
                    throw new ImportLimitException("The graph-node or graph-link creation limit was reached.");
                }

                foreach (GraphNodeSpec spec in record.Nodes)
                {
                    Guid? existingID = spec.DataPointer is null ? null : FindByPointer(spec.DataPointer);
                    Web_Node? existing = existingID.HasValue ? Core.GetExistingNode(existingID.Value) : null;
                    if (existing is not null && existing.Get_NodeType().Id != spec.Type.Id)
                        throw new ImportRecordException("A stored file position has an incompatible node type.");
                }

                foreach (GraphLinkSpec link in record.Links)
                {
                    if (link.DestinationIndex < 0 || link.DestinationIndex >= record.Nodes.Count
                        || (link.ExternalSourceID is null
                            && (link.SourceIndex < 0 || link.SourceIndex >= record.Nodes.Count)))
                    {
                        throw new ImportRecordException("A structural link references an invalid record node.");
                    }
                    if (link.ExternalSourceID is Guid externalSourceID
                        && !(Options.DryRun && externalSourceID == Guid.Empty)
                        && !Core.TryGetNode(externalSourceID, out _))
                    {
                        throw new ImportRecordException("A structural link references a missing external source node.");
                    }
                    if (!RelationRegistry.TryGet(link.RelationName, out _))
                        throw new ImportRecordException($"Structural relation '{link.RelationName}' is not registered.");
                }

                if (Options.DryRun)
                    return new AppliedGraphRecord(record.Nodes.Select(_ => Guid.NewGuid()).ToArray());

                Guid[] ids = new Guid[record.Nodes.Count];
                for (int index = 0; index < record.Nodes.Count; index++)
                {
                    GraphNodeSpec spec = record.Nodes[index];
                    Guid? existingID = spec.DataPointer is null ? null : FindByPointer(spec.DataPointer);
                    Web_Node? node = existingID.HasValue ? Core.GetExistingNode(existingID.Value) : null;
                    if (node is null)
                    {
                        node = new Web_Node(Guid.NewGuid(), spec.Type, NodeLifetime.Permanent, 0);
                        node.Set_Label(spec.Label);
                        foreach (string alias in spec.Aliases)
                            node.Add_Alias(alias);
                        if (spec.DataPointer is not null)
                            node.Set_DataPointer(spec.DataPointer);
                        if (Source?.TrustRating is double existingTrust)
                            node.Set_TrustRating(existingTrust);
                        if (!Core.AddExistingNode(node))
                            throw new InvalidOperationException("A structured data node could not be added.");
                        NodesCreated++;
                        if (spec.DataPointer is not null)
                            nodesByDataPointer[GetPointerKey(spec.DataPointer.Location)] = node;
                    }
                    else
                    {
                        if (!string.Equals(node.Label, spec.Label, StringComparison.Ordinal))
                            node.Set_Label(spec.Label);
                        foreach (string alias in node.Aliases.ToArray())
                            node.Remove_Alias(alias);
                        foreach (string alias in spec.Aliases)
                            node.Add_Alias(alias);
                        if (spec.DataPointer is not null)
                            node.Set_DataPointer(spec.DataPointer);
                        if (Source?.TrustRating is double sourceTrust
                            && (!node.TrustRating.HasValue || node.TrustRating.Value < sourceTrust))
                        {
                            node.Set_TrustRating(sourceTrust);
                        }
                    }

                    ids[index] = node.ID;
                    AffectedNodeIDs.Add(node.ID);
                }

                foreach (GraphLinkSpec link in record.Links)
                {
                    Guid sourceID = link.ExternalSourceID ?? ids[link.SourceIndex];
                    Guid destinationID = ids[link.DestinationIndex];
                    if (!RelationRegistry.TryGet(link.RelationName, out RelationLinkType relation))
                        throw new InvalidOperationException($"Structural relation '{link.RelationName}' is not registered.");

                    if (Core.AddDirectedConnection(sourceID, destinationID,
                            OntologyRelations.LinkClassFor(link.RelationName), relation.Forward.Id))
                    {
                        LinksCreated++;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Could not add structural link '{link.RelationName}'.");
                    }
                }

                return new AppliedGraphRecord(ids);
            }

            /// <summary>Adds a diagnostic while enforcing the configured diagnostic count.</summary>
            public void AddDiagnostic(string message)
            {
                if (diagnostics.Count < Options.MaxDiagnostics)
                {
                    diagnostics.Add(message);
                }
                else if (!diagnosticsTruncated)
                {
                    diagnosticsTruncated = true;
                    diagnostics[^1] = "Additional diagnostics were omitted.";
                }
            }

            /// <summary>Publishes the current record progress to the configured callback.</summary>
            public void ReportProgress(string stage)
            {
                Options.Progress?.Invoke(new DataFileImportProgress(RecordsProcessed, Options.MaxRecords, stage));
            }

            /// <summary>Commits source trust and checksum only after a complete import with outcomes.</summary>
            public void CompleteSourceMetadataIfEligible()
            {
                if (Options.DryRun || Cancelled || Truncated || FailedFatally || Source is null)
                    return;

                if (RecordsSucceeded + RecordsFailed > 0)
                {
                    double score = CalculateTrust(RecordsSucceeded, RecordsFailed);
                    if (string.Equals(Source.Label, "dictionary", StringComparison.OrdinalIgnoreCase))
                        score = 1d;
                    else if (string.Equals(Source.Label, "user", StringComparison.OrdinalIgnoreCase))
                        score = 0.5d;

                    double retained = Math.Max(Source.TrustRating ?? 0d, score);
                    Source.Set_TrustRating(retained);
                    SourceTrustRating = retained;

                    foreach (Guid nodeID in AffectedNodeIDs)
                    {
                        if (Core.TryGetNode(nodeID, out Web_Node? node) && node is not null
                            && (!node.TrustRating.HasValue || node.TrustRating.Value < retained))
                        {
                            node.Set_TrustRating(retained);
                        }
                    }

                    Source.Set_DataPointer(new DataPointer(
                        FileUri,
                        GetMediaType(Format),
                        new FileInfo(FilePath).Length,
                        Checksum));
                }
                else
                {
                    SourceTrustRating = Source.TrustRating;
                }
            }

            public double? SourceTrustRating { get; private set; }

            /// <summary>Creates the immutable result snapshot for the current import state.</summary>
            public DataFileImportResult ToResult()
            {
                return new DataFileImportResult
                {
                    FilePath = FilePath,
                    Format = Format,
                    Mode = Options.Mode,
                    SourceNodeID = SourceNodeID,
                    SourceNodeCreated = SourceNodeCreated,
                    SourceTrustRating = SourceTrustRating ?? Source?.TrustRating,
                    DryRun = Options.DryRun,
                    Empty = Empty,
                    SkippedHighTrust = SkippedHighTrust,
                    Cancelled = Cancelled,
                    Truncated = Truncated,
                    FailedFatally = FailedFatally,
                    RecordsProcessed = RecordsProcessed,
                    RecordsSucceeded = RecordsSucceeded,
                    RecordsUnsupported = RecordsUnsupported,
                    RecordsFailed = RecordsFailed,
                    NodesCreated = NodesCreated,
                    LinksCreated = LinksCreated,
                    TextCharacters = TextCharacters,
                    TextParagraphs = TextParagraphs,
                    TextSentences = TextSentences,
                    TextChunkCount = TextChunkCount,
                    TextIngestedChunks = TextIngestedChunks,
                    TextSkippedChunks = TextSkippedChunks,
                    ConversationID = ConversationID,
                    Diagnostics = diagnostics.ToArray()
                };
            }

            /// <summary>Compares two file locations using the platform's path case rules.</summary>
            private bool LocationEquals(Uri left, Uri right)
            {
                string leftKey = left.GetComponents(UriComponents.AbsoluteUri, UriFormat.SafeUnescaped);
                string rightKey = right.GetComponents(UriComponents.AbsoluteUri, UriFormat.SafeUnescaped);
                return string.Equals(leftKey, rightKey, GetPathComparison());
            }

            /// <summary>Normalizes a pointer URI into its dictionary lookup key.</summary>
            private string GetPointerKey(Uri uri) => uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.SafeUnescaped);

            /// <summary>Returns the path comparison rule for the current operating system.</summary>
            private StringComparison GetPathComparison() => OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        /// <summary>Builds a bounded graph representation for one CSV/JSON/XML record.</summary>
        private sealed class GraphRecordBuilder
        {
            private readonly int maxRecordCharacters;
            private readonly int maxNodes;
            private readonly int maxLinks;

            /// <summary>Creates a graph-record builder with explicit node, link, and character budgets.</summary>
            public GraphRecordBuilder(int maxRecordCharacters, int maxNodes, int maxLinks)
            {
                this.maxRecordCharacters = maxRecordCharacters;
                this.maxNodes = maxNodes;
                this.maxLinks = maxLinks;
            }

            public List<GraphNodeSpec> Nodes { get; } = new List<GraphNodeSpec>();
            public List<GraphLinkSpec> Links { get; } = new List<GraphLinkSpec>();
            public int Characters { get; private set; }
            public string? Error { get; private set; }

            /// <summary>Adds a node specification unless the record budget has been exhausted.</summary>
            public int AddNode(GraphNodeSpec spec)
            {
                int additionalCharacters = (spec.Label?.Length ?? 0) + spec.Aliases.Sum(alias => alias.Length);
                if (Nodes.Count >= maxNodes || Characters + additionalCharacters > maxRecordCharacters)
                {
                    MarkError("The record exceeds its node or character budget.");
                    throw new ImportRecordException(Error!);
                }

                Nodes.Add(spec);
                Characters += additionalCharacters;
                return Nodes.Count - 1;
            }

            /// <summary>Attempts to add a node and converts record-budget errors into a false result.</summary>
            public bool TryAddNode(GraphNodeSpec spec, out int index)
            {
                try
                {
                    index = AddNode(spec);
                    return true;
                }
                catch (ImportRecordException)
                {
                    index = -1;
                    return false;
                }
            }

            /// <summary>Adds a link between two nodes in this record.</summary>
            public void AddLink(int sourceIndex, int destinationIndex, string relationName)
            {
                if (Links.Count >= maxLinks)
                {
                    MarkError("The record exceeds its link budget.");
                    throw new ImportRecordException(Error!);
                }
                Links.Add(new GraphLinkSpec(sourceIndex, destinationIndex, relationName, null));
            }

            /// <summary>Adds a link from an existing graph node to a node in this record.</summary>
            public void AddExternalLink(Guid sourceID, int destinationIndex, string relationName)
            {
                if (Links.Count >= maxLinks)
                {
                    MarkError("The record exceeds its link budget.");
                    throw new ImportRecordException(Error!);
                }
                Links.Add(new GraphLinkSpec(-1, destinationIndex, relationName, sourceID));
            }

            /// <summary>Marks the record invalid while preserving its first error.</summary>
            public void MarkError(string message)
            {
                Error ??= message;
            }
        }

        /// <summary>Describes one graph node to create or update from a structured record.</summary>
        private sealed record GraphNodeSpec(NodeTypes Type, string? Label, string[] Aliases, DataPointer? DataPointer);
        /// <summary>Describes one local or source-external graph link in a record.</summary>
        private sealed record GraphLinkSpec(int SourceIndex, int DestinationIndex, string RelationName, Guid? ExternalSourceID);
        /// <summary>Contains the graph identifiers produced when a structured record is applied.</summary>
        private sealed record AppliedGraphRecord(Guid[] NodeIDs);
        /// <summary>Contains one parsed CSV row and its recoverability metadata.</summary>
        private sealed record CsvRecord(string[] Fields, string? Error, bool IsBlank, bool Fatal);

        /// <summary>Signals that a configured file, record, node, or link budget was reached.</summary>
        private sealed class ImportLimitException : Exception
        {
            /// <summary>Creates an exception describing a reached import budget.</summary>
            public ImportLimitException(string message) : base(message) { }
        }

        /// <summary>Signals that the current structured record cannot be applied safely.</summary>
        private sealed class ImportRecordException : Exception
        {
            /// <summary>Creates an exception describing why a record could not be applied.</summary>
            public ImportRecordException(string message) : base(message) { }
        }

        /// <summary>Stores bounded raw JSON input and parser completion flags.</summary>
        private readonly record struct JsonRawValue(string Text, bool TooLong, bool Incomplete);

        /// <summary>Reads JSON characters incrementally while supporting one-character lookahead.</summary>
        private sealed class JsonCharacterReader
        {
            private readonly TextReader reader;
            private int bufferedCharacter = -2;

            /// <summary>Creates a character reader over the supplied text stream.</summary>
            public JsonCharacterReader(TextReader reader) => this.reader = reader;

            /// <summary>Reads one character, returning a buffered lookahead character first.</summary>
            public int Read()
            {
                if (bufferedCharacter != -2)
                {
                    int value = bufferedCharacter;
                    bufferedCharacter = -2;
                    return value;
                }
                return reader.Read();
            }

            /// <summary>Returns the next character without consuming it.</summary>
            public int Peek()
            {
                if (bufferedCharacter == -2)
                    bufferedCharacter = reader.Read();
                return bufferedCharacter;
            }

            /// <summary>Reads the next non-whitespace character or the end-of-stream marker.</summary>
            public int ReadNonWhitespace()
            {
                int value;
                do
                {
                    value = Read();
                } while (value >= 0 && char.IsWhiteSpace((char)value));
                return value;
            }
        }

        /// <summary>Reads one JSON primitive or compound value into a bounded character buffer.</summary>
        private static JsonRawValue ReadJsonValue(
            JsonCharacterReader reader,
            int firstCharacter,
            int maxCharacters,
            CancellationToken cancellationToken)
        {
            StringBuilder text = new StringBuilder(Math.Min(maxCharacters, 4096));
            int totalCharacters = 0;
            bool tooLong = false;
            bool inString = firstCharacter == '"';
            bool escaped = false;
            bool primitive = firstCharacter != '{' && firstCharacter != '[' && firstCharacter != '"';
            int depth = firstCharacter is '{' or '[' ? 1 : 0;

            void Append(int value)
            {
                totalCharacters++;
                if (totalCharacters <= maxCharacters)
                    text.Append((char)value);
                else
                    tooLong = true;
            }

            Append(firstCharacter);
            if (primitive)
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int next = reader.Peek();
                    if (next < 0 || char.IsWhiteSpace((char)next) || next is ',' or ']')
                        break;
                    Append(reader.Read());
                }
                return new JsonRawValue(text.ToString(), tooLong, Incomplete: false);
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int next = reader.Read();
                if (next < 0)
                    return new JsonRawValue(text.ToString(), tooLong, Incomplete: true);

                Append(next);
                char current = (char)next;
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (current == '\\')
                    {
                        escaped = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                        if (depth == 0)
                            return new JsonRawValue(text.ToString(), tooLong, Incomplete: false);
                    }
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }
                if (current is '{' or '[')
                {
                    depth++;
                    continue;
                }
                if (current is '}' or ']')
                {
                    depth--;
                    if (depth <= 0)
                        return new JsonRawValue(text.ToString(), tooLong, Incomplete: false);
                }
            }
        }

        /// <summary>Returns the MIME type associated with a data-file format.</summary>
        private static string GetMediaType(DataFileFormat format) => format switch
        {
            DataFileFormat.Csv => "text/csv",
            DataFileFormat.Json => "application/json",
            DataFileFormat.Xml => "application/xml",
            _ => "text/plain"
        };

        /// <summary>Creates the display label used for a file-backed Source node.</summary>
        private static string CreateSourceNodeLabel(string path) => "Source: " + Path.GetFileName(path);

        /// <summary>Creates a graph-record builder using the import's configured budgets.</summary>
        private static GraphRecordBuilder NewRecordBuilder(DataFileImportState state)
        {
            return new GraphRecordBuilder(
                state.Options.MaxRecordCharacters,
                state.Options.MaxCreatedNodes,
                state.Options.MaxCreatedLinks);
        }

        /// <summary>Calculates the import trust score from successful and failed records.</summary>
        private static double CalculateTrust(int succeeded, int failed)
        {
            int total = succeeded + failed;
            return total == 0 ? 0.90 : 0.30 + 0.60 * succeeded / total;
        }

        /// <summary>Adds a typed metadata value node and its optional parent relation.</summary>
        private static int AddValueNode(
            DataFileImportState state,
            GraphRecordBuilder record,
            string? value,
            ValueKind kind,
            DataPointer pointer,
            int parentIndex,
            string relation)
        {
            if (value is not null && value.Length > state.Options.MaxValueCharacters)
                throw new ImportRecordException($"A value exceeds the {state.Options.MaxValueCharacters}-character limit.");

            string alias = kind switch
            {
                ValueKind.Number => "numberValue",
                ValueKind.Boolean => "booleanValue",
                ValueKind.Null => "nullValue",
                ValueKind.Path => "pathValue",
                _ => "stringValue"
            };
            int valueIndex = record.AddNode(new GraphNodeSpec(
                OntologyTypes.MetadataValues,
                value,
                new[] { alias },
                pointer));
            if (parentIndex >= 0)
                record.AddLink(parentIndex, valueIndex, relation);
            return valueIndex;
        }

        /// <summary>Runs extracted prose records through one NLP pipeline and conversation.</summary>
        private sealed class NlpImportSession : IDisposable
        {
            private readonly DataFileImportState state;
            private readonly NlpPipeline? pipeline;
            private readonly Guid? sourceNodeID;
            private readonly Guid? topicNodeID;

            /// <summary>Creates a shared NLP pipeline unless the import is a dry run.</summary>
            public NlpImportSession(DataFileImportState state)
            {
                this.state = state;
                if (state.Options.DryRun)
                    return;

                sourceNodeID = state.EnsureSource();
                topicNodeID = state.Options.UseSourceAsTopic
                    ? sourceNodeID
                    : state.Options.TopicNodeID;
                ReasoningOptions reasoningOptions = new ReasoningOptions
                {
                    ExistingNodeWeightPolicy = state.IsExistingSource
                        ? ExistingNodeWeightUpPolicy.Suppress
                        : ExistingNodeWeightUpPolicy.OncePerPipeline
                };
                pipeline = new NlpPipeline(
                    state.Core,
                    state.Options.PipelineOptions,
                    reasoningOptions: reasoningOptions);
                state.ConversationID = pipeline.ConversationID;
            }

            /// <summary>Ingests one extracted text record and records its outcome and affected nodes.</summary>
            public void ProcessRecord(string text, string context)
            {
                if (state.Options.CancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(state.Options.CancellationToken);

                if (state.Options.DryRun)
                {
                    state.RecordsSucceeded++;
                    return;
                }

                try
                {
                    NlpResult result = pipeline!.Ingest(text, sourceNodeID, topicNodeID);
                    foreach (ReasoningResult reasoningResult in result.ReasoningResults)
                    {
                        if (reasoningResult.SelectedMatch is not null)
                        {
                            foreach (Guid nodeID in reasoningResult.SelectedMatch.NodeMap.Values)
                                state.AffectedNodeIDs.Add(nodeID);
                        }
                        foreach (Guid promotedNodeID in reasoningResult.PromotedNodeIDs)
                            state.AffectedNodeIDs.Add(promotedNodeID);
                    }

                    if (result.Status == NlpStatus.Ingested)
                    {
                        state.RecordsSucceeded++;
                    }
                    else if (result.Status == NlpStatus.Unsupported)
                    {
                        state.RecordsSucceeded++;
                        state.RecordsUnsupported++;
                    }
                    else
                    {
                        state.RecordsFailed++;
                        state.AddDiagnostic($"{context} failed ({result.Status}): "
                            + (result.Diagnostics.FirstOrDefault() ?? result.Status.ToString()));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    state.RecordsFailed++;
                    state.AddDiagnostic($"{context} failed: {ex.Message}");
                }
            }

            /// <summary>Releases the pipeline's attachment to the shared conversation.</summary>
            public void Dispose() => pipeline?.Dispose();
        }

        /// <summary>Classifies a typed value extracted from JSON, XML, or NLP input.</summary>
        private enum ValueKind
        {
            String,
            Number,
            Boolean,
            Null,
            Path
        }

        /// <summary>Infers the typed value category for a textual scalar.</summary>
        private static ValueKind InferTextValueKind(string value)
        {
            string trimmed = value.Trim();
            if (bool.TryParse(trimmed, out _))
                return ValueKind.Boolean;
            if (decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return ValueKind.Number;
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri)
                && uri.Scheme is "file" or "http" or "https")
            {
                return ValueKind.Path;
            }
            return ValueKind.String;
        }

        /// <summary>Creates a data pointer that identifies a stable position within a source file.</summary>
        private static DataPointer CreatePositionPointer(Uri fileUri, string mediaType, string fragment)
        {
            UriBuilder builder = new UriBuilder(fileUri)
            {
                Fragment = fragment.TrimStart('#')
            };
            return new DataPointer(builder.Uri, mediaType);
        }

        /// <summary>Escapes a property name according to RFC 6901 JSON Pointer rules.</summary>
        private static string EscapeJsonPointer(string value)
        {
            return value.Replace("~", "~0", StringComparison.Ordinal)
                .Replace("/", "~1", StringComparison.Ordinal);
        }
    }
}
