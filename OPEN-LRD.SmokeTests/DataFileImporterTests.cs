using System.IO;
using System.Text;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;
using OPEN_LRD.Persistence;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    /// <summary>Exercises data-file parsing, graph mapping, provenance, trust, and retry behavior.</summary>
    internal static class DataFileImporterTests
    {
        /// <summary>Registers the data-file importer regression checks with the smoke runner.</summary>
        public static void RunAll()
        {
            Program.Run("Data-file CSV import and checksum skip", TestCsvImportAndChecksumSkip);
            Program.Run("Data-file CSV position upsert", TestCsvPositionUpsert);
            Program.Run("Data-file JSON and XML imports", TestJsonAndXmlImports);
            Program.Run("Data-file dry run and fatal CSV", TestDryRunAndFatalCsv);
            Program.Run("Data-file empty outcome does not commit checksum", TestNoRecordOutcomeDoesNotCommitChecksum);
            Program.Run("Data-file preserves user Source trust baseline", TestUserSourceTrustBaseline);
            Program.Run("Data-file trust counts recoverable row failures", TestRecoverableRowTrustScore);
            Program.Run("Data-file CSV table and cell positions", TestCsvTableAndCellPositions);
            Program.Run("Data-file CSV NLP triple mapping", TestCsvNlpTripleMapping);
            Program.Run("Data-file JSON Pointer escaping", TestJsonPointerEscaping);
            Program.Run("Data-file rejects XML DTD declarations", TestXmlDtdRejected);
            Program.Run("Data-file cancellation preserves only completed records", TestStructuredCancellation);
            Program.Run("Data-file size and JSON depth limits", TestFileAndDepthLimits);
            Program.Run("Data-file JSON scalar and namespaced XML roots", TestJsonScalarAndNamespacedXml);
            Program.Run("Data-file empty files warn and create no Source or data nodes", TestEmptyFilesCreateNoNodes);
            Program.Run("Data-file record, column, value, and diagnostic limits", TestRecordColumnValueAndDiagnosticLimits);
            Program.Run("Data-file JSON and XML schemas, value aliases, and persistence", TestJsonAndXmlSchemasAndPersistence);
            Program.Run("Data-file NLP fallback extraction and semantic duplicate weights", TestNlpFallbacksAndSemanticDuplicates);
            Program.Run("Data-file re-import at or below 80% trust, moved files, and retry", TestLowTrustReimportMovedFileAndRetry);
        }

        /// <summary>Verifies successful CSV ingestion and unchanged high-trust checksum skipping.</summary>
        private static void TestCsvImportAndChecksumSkip()
        {
            string path = CreateTempFile(".csv", "name,age\nAlice,37\n");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult first = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv,
                    Mode = DataFileIngestionMode.StructuredData
                });

                Assert(first.RecordsProcessed == 1 && first.RecordsSucceeded == 1,
                    "the CSV data row should be imported successfully");
                Assert(first.SourceNodeCreated && first.SourceNodeID.HasValue,
                    "the first import should create a Source node");
                Assert(Math.Abs((first.SourceTrustRating ?? 0d) - 0.90d) < 1e-9,
                    "a successful single-record import should receive the expected trust score");
                Assert(first.NodesCreated > 0 && first.LinksCreated > 0,
                    "structured CSV import should create graph nodes and links");

                DataFileImportResult second = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv,
                    Mode = DataFileIngestionMode.StructuredData
                });

                Assert(second.SkippedHighTrust,
                    "an unchanged file from a source above the trust threshold should be skipped");
                Assert(second.RecordsProcessed == 0,
                    "the unchanged high-trust file should not process records again");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies that changed CSV content updates stable structural positions in place.</summary>
        private static void TestCsvPositionUpsert()
        {
            string path = CreateTempFile(".csv", "name,age\nAlice,37\n");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportOptions options = new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv,
                    Mode = DataFileIngestionMode.StructuredData
                };
                DataFileImporter.Import(core, options);
                int nodesBefore = core.GetDictionary()!.Count;
                File.WriteAllText(path, "name,age\nAlice,38\n");

                DataFileImportResult revised = DataFileImporter.Import(core, options);
                Assert(!revised.SkippedHighTrust && revised.RecordsSucceeded == 1,
                    "a changed source revision should be processed");
                Assert(revised.NodesCreated == 0 && core.GetDictionary()!.Count == nodesBefore,
                    "stable CSV positions should update in place rather than duplicate graph nodes");
                Assert(Math.Abs((revised.SourceTrustRating ?? 0d) - 0.90d) < 1e-9,
                    "re-import must retain the maximum existing source trust");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies JSON/XML structured imports and JSON/XML NLP extraction paths.</summary>
        private static void TestJsonAndXmlImports()
        {
            string jsonPath = CreateTempFile(".json", "[{\"name\":\"Ada\",\"active\":true}]");
            string xmlPath = CreateTempFile(".xml", "<root><item id=\"1\">Hello</item></root>");
            string nlpPath = CreateTempFile(".json", "[{\"text\":\"Alice likes Bob.\"}]");
            string xmlNlpPath = CreateTempFile(".xml", "<root><record>Alice likes Bob.</record></root>");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult json = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = jsonPath,
                    Format = DataFileFormat.Json
                });
                DataFileImportResult xml = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = xmlPath,
                    Format = DataFileFormat.Xml
                });
                DataFileImportResult nlp = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = nlpPath,
                    Format = DataFileFormat.Json,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });
                DataFileImportResult xmlNlp = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = xmlNlpPath,
                    Format = DataFileFormat.Xml,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });

                Assert(json.RecordsSucceeded == 1 && json.NodesCreated > 0,
                    "JSON array records should import as structured graph data");
                Assert(xml.RecordsSucceeded > 0 && xml.NodesCreated > 0,
                    "XML elements should import as structured graph data");
                Assert(nlp.RecordsProcessed == 1
                    && nlp.RecordsSucceeded + nlp.RecordsUnsupported + nlp.RecordsFailed == 1
                    && nlp.ConversationID.HasValue,
                    "JSON NLP extraction should route one record through a shared NLP conversation");
                Assert(xmlNlp.RecordsProcessed == 1
                    && xmlNlp.RecordsSucceeded + xmlNlp.RecordsUnsupported + xmlNlp.RecordsFailed == 1
                    && xmlNlp.ConversationID.HasValue
                    && xmlNlp.NodesCreated == 0,
                    "XML NLP extraction should process text records without creating structural nodes");
            }
            finally
            {
                File.Delete(jsonPath);
                File.Delete(xmlPath);
                File.Delete(nlpPath);
                File.Delete(xmlNlpPath);
            }
        }

        /// <summary>Verifies structured dry runs are read-only and unterminated CSV quotes stop import.</summary>
        private static void TestDryRunAndFatalCsv()
        {
            string dryRunPath = CreateTempFile(".json", "[{\"value\":1}]");
            string malformedPath = CreateTempFile(".csv", "name,value\n\"unclosed,value");
            try
            {
                LRD_Core core = NewCore();
                int before = core.GetDictionary()!.Count;
                DataFileImportResult dryRun = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = dryRunPath,
                    Format = DataFileFormat.Json,
                    DryRun = true
                });
                Assert(dryRun.DryRun && core.GetDictionary()!.Count == before,
                    "a structured dry run must not mutate the graph");

                DataFileImportResult malformed = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = malformedPath,
                    Format = DataFileFormat.Csv
                });
                Assert(malformed.FailedFatally && malformed.RecordsFailed > 0,
                    "an unterminated quoted CSV field should stop the import with a diagnostic");
                Assert(malformed.Diagnostics.Count > 0,
                    "fatal CSV errors should be reported to the user");
            }
            finally
            {
                File.Delete(dryRunPath);
                File.Delete(malformedPath);
            }
        }

        /// <summary>Verifies a header-only CSV does not commit a source checksum or inferred trust.</summary>
        private static void TestNoRecordOutcomeDoesNotCommitChecksum()
        {
            string path = CreateTempFile(".csv", "name,value\n");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv
                });

                Assert(result.RecordsProcessed == 0 && result.SourceNodeID.HasValue,
                    "a header-only table may establish its structural Source without processing data records");
                bool foundSource = core.TryGetNode(result.SourceNodeID!.Value, out Web_Node? source);
                Assert(foundSource && source?.Get_DataPointer()?.Checksum is null,
                    "a source checksum must not be committed when no record outcome was produced");
                Assert(source?.TrustRating is null,
                    "a header-only file must not receive an inferred trust rating");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies the fixed trust baselines for user and dictionary source nodes.</summary>
        private static void TestUserSourceTrustBaseline()
        {
            string path = CreateTempFile(".json", "{\"value\":1}");
            string dictionaryPath = CreateTempFile(".json", "{\"definition\":\"A dictionary entry.\"}");
            try
            {
                LRD_Core core = NewCore();
                Web_Node userSource = new Web_Node(
                    Guid.NewGuid(),
                    NodeTypes.Sources,
                    NodeLifetime.Permanent,
                    0);
                userSource.Set_Label("user");
                userSource.Set_TrustRating(0.5d);
                Assert(core.AddExistingNode(userSource), "the manual user Source should be added");

                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Json,
                    SourceNodeID = userSource.ID
                });

                Assert(Math.Abs((result.SourceTrustRating ?? 0d) - 0.5d) < 1e-9
                    && Math.Abs((userSource.TrustRating ?? 0d) - 0.5d) < 1e-9,
                    "a file import must preserve the manual user Source's 50% trust baseline");

                Web_Node dictionarySource = new Web_Node(
                    Guid.NewGuid(),
                    NodeTypes.Sources,
                    NodeLifetime.Permanent,
                    0);
                dictionarySource.Set_Label("dictionary");
                Assert(core.AddExistingNode(dictionarySource), "the dictionary Source should be added");
                DataFileImportResult dictionaryResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = dictionaryPath,
                    Format = DataFileFormat.Json,
                    SourceNodeID = dictionarySource.ID
                });
                Assert(Math.Abs((dictionaryResult.SourceTrustRating ?? 0d) - 1d) < 1e-9,
                    "dictionary file sources should retain the 100% trust baseline");
            }
            finally
            {
                File.Delete(path);
                File.Delete(dictionaryPath);
            }
        }

        /// <summary>Verifies trust scoring includes recoverable failed rows while later rows continue.</summary>
        private static void TestRecoverableRowTrustScore()
        {
            string path = CreateTempFile(".csv", "name\nAlice\n\"Bob\"x\nCarol\n");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv
                });

                Assert(result.RecordsProcessed == 3
                    && result.RecordsSucceeded == 2
                    && result.RecordsFailed == 1,
                    "a recoverable malformed CSV row should fail while later rows continue");
                Assert(Math.Abs((result.SourceTrustRating ?? 0d) - 0.70d) < 1e-9,
                    "trust should use 0.30 + 0.60 * 2 / 3 for two successes and one failure");
                Assert(core.TryGetNode(result.SourceNodeID!.Value, out Web_Node? source)
                    && source?.Get_DataPointer()?.Checksum is not null,
                    "a fully processed file with recoverable row failures should commit its checksum");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies CSV table separators, header rows, ordered cells, and positional pointers.</summary>
        private static void TestCsvTableAndCellPositions()
        {
            string contents = "first,second,third\nA,B,C\n\nleft,right\nD,E\n";
            string path = CreateTempFile(".csv", contents);
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv
                });

                Web_Node[] tables = core.GetDictionary()!.Values
                    .Where(node => node.Aliases.Contains("Table"))
                    .OrderBy(node => node.Label, StringComparer.Ordinal)
                    .ToArray();
                Assert(result.RecordsProcessed == 2 && result.RecordsSucceeded == 2,
                    "each CSV data row should count as one successful record");
                Assert(tables.Length == 2 && tables[0].Label == "table_0" && tables[1].Label == "table_1",
                    "blank records should separate uniquely labeled CSV tables");

                Web_Node[] headerCells = core.GetDictionary()!.Values
                    .Where(node => node.Aliases.Contains("HEADER"))
                    .OrderBy(node => node.Label, StringComparer.Ordinal)
                    .ToArray();
                Assert(headerCells.Select(node => node.Label).SequenceEqual(
                    new[] { "first", "left", "right", "second", "third" }),
                    "CSV header cells should retain their labels and the HEADER alias");

                for (int column = 0; column < 3; column++)
                {
                    string fragment = $"table_0/row_1/column_{column}";
                    Assert(core.GetDictionary()!.Values.Any(node =>
                        node.Get_DataPointer()?.Location.Fragment.TrimStart('#') == fragment),
                        $"the CSV data cell at column {column} should have a stable position pointer");
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies subject/predicate/object CSV rows become NLP sentences rather than structural tables.</summary>
        private static void TestCsvNlpTripleMapping()
        {
            string path = CreateTempFile(".csv", "subject,predicate,object\nAlice,likes,Bob\n");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });

                Assert(result.RecordsProcessed == 1
                    && result.RecordsSucceeded + result.RecordsUnsupported + result.RecordsFailed == 1,
                    "the CSV triple should be handled as one NLP record");
                Assert(result.ConversationID.HasValue && result.NodesCreated == 0,
                    "NLP-mode CSV should use one conversation and create no structural table nodes");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies JSON property names are escaped according to JSON Pointer.</summary>
        private static void TestJsonPointerEscaping()
        {
            string path = CreateTempFile(".json", "{\"a/b\":{\"~key\":null}}");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Json
                });

                bool hasEscapedValue = core.GetDictionary()!.Values.Any(node =>
                    node.Get_DataPointer()?.Location.Fragment.TrimStart('#')
                        == "json/root/a~1b/~0key/value"
                    && node.Aliases.Contains("nullValue"));
                Assert(result.RecordsSucceeded == 1 && hasEscapedValue,
                    "JSON pointer fragments should escape slash and tilde and preserve null values");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies XML DTD processing is prohibited and rejected without graph changes.</summary>
        private static void TestXmlDtdRejected()
        {
            string path = CreateTempFile(
                ".xml",
                "<!DOCTYPE root [<!ENTITY ext SYSTEM 'file:///etc/passwd'>]><root>&ext;</root>");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Xml
                });

                Assert(result.FailedFatally && result.Diagnostics.Count > 0,
                    "the XML DTD declaration should be reported as a fatal parse error");
                Assert(core.GetDictionary()!.Count == 0,
                    "rejected XML containing a DTD should not create graph nodes");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies structured cancellation retains completed records and leaves checksum unset.</summary>
        private static void TestStructuredCancellation()
        {
            string path = CreateTempFile(".json", "[{\"value\":1},{\"value\":2}]");
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult result = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Json,
                    CancellationToken = cancellation.Token,
                    Progress = progress =>
                    {
                        if (progress.ProcessedRecords == 1)
                            cancellation.Cancel();
                    }
                });

                Assert(result.Cancelled && result.RecordsProcessed == 1 && result.RecordsSucceeded == 1,
                    "cancellation should stop before the second JSON record while preserving the first");
                Assert(result.SourceNodeID.HasValue
                    && core.TryGetNode(result.SourceNodeID.Value, out Web_Node? source)
                    && source?.Get_DataPointer()?.Checksum is null,
                    "a cancelled import must leave its Source checksum uncommitted");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies the file-size and JSON nesting limits reject input before unsafe graph growth.</summary>
        private static void TestFileAndDepthLimits()
        {
            string path = CreateTempFile(".json", "{\"outer\":{\"inner\":1}}");
            try
            {
                LRD_Core core = NewCore();
                AssertThrows<InvalidOperationException>(() => DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Json,
                    MaxFileBytes = 1
                }), "an over-limit file should be rejected during preflight");
                Assert(core.GetDictionary()!.Count == 0,
                    "file-size validation should happen before creating graph nodes");

                DataFileImportResult deep = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Json,
                    MaxDepth = 1
                });
                Assert(deep.FailedFatally && deep.RecordsFailed > 0,
                    "JSON nesting beyond MaxDepth should be reported as a fatal format error");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Verifies scalar JSON roots and namespace-expanded XML element names.</summary>
        private static void TestJsonScalarAndNamespacedXml()
        {
            string jsonPath = CreateTempFile(".json", "42");
            string xmlPath = CreateTempFile(
                ".xml",
                "<root xmlns=\"urn:example\"><item code=\"7\">value</item></root>");
            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult json = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = jsonPath,
                    Format = DataFileFormat.Json
                });
                DataFileImportResult xml = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = xmlPath,
                    Format = DataFileFormat.Xml
                });

                Assert(json.RecordsSucceeded == 1
                    && core.GetDictionary()!.Values.Any(node => node.Label == "42"),
                    "a scalar JSON root should become one structured record");
                Assert(xml.RecordsSucceeded == 1
                    && core.GetDictionary()!.Values.Any(node => node.Label == "{urn:example}item")
                    && core.GetDictionary()!.Values.Any(node => node.Label == "code"),
                    "XML namespace-expanded names and attributes should be preserved structurally");
            }
            finally
            {
                File.Delete(jsonPath);
                File.Delete(xmlPath);
            }
        }

        /// <summary>Verifies zero-byte and whitespace-only files emit diagnostics without creating Source or data nodes.</summary>
        private static void TestEmptyFilesCreateNoNodes()
        {
            string emptyCsv = CreateTempFile(".csv", string.Empty);
            string blankCsv = CreateTempFile(".csv", "   \r\n\n  ");
            string emptyJsonArray = CreateTempFile(".json", "[]");
            string whitespaceJson = CreateTempFile(".json", "   \t\r\n");
            try
            {
                LRD_Core core = NewCore();

                DataFileImportResult zeroByteResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = emptyCsv,
                    Format = DataFileFormat.Csv
                });
                Assert(zeroByteResult.Empty && zeroByteResult.SourceNodeID is null && zeroByteResult.Diagnostics.Count > 0,
                    "a zero-byte file should be marked empty with a diagnostic and no Source node");
                Assert(core.GetDictionary()!.Count == 0,
                    "a zero-byte file must not create any graph nodes");

                DataFileImportResult blankCsvResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = blankCsv,
                    Format = DataFileFormat.Csv
                });
                Assert(blankCsvResult.Empty && blankCsvResult.SourceNodeID is null && core.GetDictionary()!.Count == 0,
                    "a whitespace-only CSV file must not create a Source or table node");

                DataFileImportResult emptyJsonResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = emptyJsonArray,
                    Format = DataFileFormat.Json
                });
                Assert(emptyJsonResult.Empty && emptyJsonResult.SourceNodeID is null && core.GetDictionary()!.Count == 0,
                    "an empty JSON root array must not create a Source or root node");

                DataFileImportResult whitespaceJsonResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = whitespaceJson,
                    Format = DataFileFormat.Json
                });
                Assert(whitespaceJsonResult.Empty && whitespaceJsonResult.SourceNodeID is null && core.GetDictionary()!.Count == 0,
                    "a whitespace-only JSON file must not create any graph nodes");
            }
            finally
            {
                File.Delete(emptyCsv);
                File.Delete(blankCsv);
                File.Delete(emptyJsonArray);
                File.Delete(whitespaceJson);
            }
        }

        /// <summary>Verifies record, column, value-character, XML-depth, and diagnostic-truncation caps.</summary>
        private static void TestRecordColumnValueAndDiagnosticLimits()
        {
            string csvPath = CreateTempFile(".csv", "a,b\n1,2\n3,4\n5,6\n");
            string wideAndLongCsvPath = CreateTempFile(
                ".csv",
                "a,b\n1,2,3\nlongvalue,ok\ngood,row\n");
            string deepXmlPath = CreateTempFile(".xml", "<root><a><b><c>deep</c></b></a></root>");
            StringBuilder manyBadRows = new StringBuilder("a,b\n");
            for (int index = 0; index < 8; index++)
                manyBadRows.Append("1,2,3\n");
            string diagCsvPath = CreateTempFile(".csv", manyBadRows.ToString());

            try
            {
                DataFileImportOptions defaults = new DataFileImportOptions { FilePath = csvPath };
                Assert(defaults.MaxFileBytes == 500L * 1024 * 1024
                    && defaults.MaxRecords == 10000
                    && defaults.MaxColumns == 1000
                    && defaults.MaxDepth == 32
                    && defaults.MaxValueCharacters == 15000
                    && defaults.MaxDiagnostics == 100,
                    "default DataFileImportOptions should expose the confirmed Module 8 Phase 5 safety bounds");

                LRD_Core core = NewCore();
                DataFileImportResult recordCapped = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = csvPath,
                    Format = DataFileFormat.Csv,
                    MaxRecords = 2
                });
                Assert(recordCapped.Truncated && recordCapped.RecordsProcessed == 2 && recordCapped.RecordsSucceeded == 2,
                    "reaching MaxRecords should truncate the import after the allowed records");
                Assert(recordCapped.SourceNodeID.HasValue
                    && core.TryGetNode(recordCapped.SourceNodeID.Value, out Web_Node? truncatedSource)
                    && truncatedSource?.Get_DataPointer()?.Checksum is null,
                    "a truncated import must not commit the completed file checksum");

                LRD_Core limitsCore = NewCore();
                DataFileImportResult columnAndValueResult = DataFileImporter.Import(limitsCore, new DataFileImportOptions
                {
                    FilePath = wideAndLongCsvPath,
                    Format = DataFileFormat.Csv,
                    MaxColumns = 2,
                    MaxValueCharacters = 5
                });
                Assert(columnAndValueResult.RecordsProcessed == 3
                    && columnAndValueResult.RecordsFailed == 2
                    && columnAndValueResult.RecordsSucceeded == 1,
                    "rows exceeding MaxColumns or MaxValueCharacters should fail recoverably while valid rows succeed");

                LRD_Core xmlCore = NewCore();
                DataFileImportResult deepXml = DataFileImporter.Import(xmlCore, new DataFileImportOptions
                {
                    FilePath = deepXmlPath,
                    Format = DataFileFormat.Xml,
                    MaxDepth = 2
                });
                Assert(deepXml.RecordsProcessed == 1 && deepXml.RecordsFailed == 1 && deepXml.RecordsSucceeded == 0,
                    "XML elements nested beyond MaxDepth should fail the record");
                Assert(Math.Abs((deepXml.SourceTrustRating ?? 0d) - 0.30d) < 1e-9,
                    "an import where all processed records fail should receive the 30% trust floor");

                LRD_Core diagCore = NewCore();
                DataFileImportResult diagResult = DataFileImporter.Import(diagCore, new DataFileImportOptions
                {
                    FilePath = diagCsvPath,
                    Format = DataFileFormat.Csv,
                    MaxDiagnostics = 3
                });
                Assert(diagResult.RecordsFailed == 8
                    && diagResult.Diagnostics.Count == 3
                    && diagResult.Diagnostics[^1] == "Additional diagnostics were omitted.",
                    "diagnostics beyond MaxDiagnostics should be capped and end with the omission notice");
            }
            finally
            {
                File.Delete(csvPath);
                File.Delete(wideAndLongCsvPath);
                File.Delete(deepXmlPath);
                File.Delete(diagCsvPath);
            }
        }

        /// <summary>Verifies JSON/XML structural schemas, typed value aliases, fixed relation IDs, and log persistence.</summary>
        private static void TestJsonAndXmlSchemasAndPersistence()
        {
            string jsonPath = CreateTempFile(
                ".json",
                "{\"title\":\"\",\"count\":12,\"flag\":false,\"nothing\":null,\"site\":\"https://example.com/doc\",\"nested\":{\"items\":[7]}}");
            string xmlPath = CreateTempFile(
                ".xml",
                "<root><mixed attr=\"true\">First<empty/>Second</mixed></root>");
            string logPath = Path.Combine(Path.GetTempPath(), "open-lrd-phase5-" + Guid.NewGuid().ToString("N") + ".lrdlog");

            try
            {
                OntologyRelations.EnsureRegistered();
                Assert(OntologyRelations.HasRecord == 20
                    && OntologyRelations.HasTable == 21
                    && OntologyRelations.HasRow == 22
                    && OntologyRelations.HasCell == 23
                    && OntologyRelations.HasChild == 24
                    && OntologyRelations.HasItem == 25
                    && OntologyRelations.HasAttribute == 26
                    && OntologyRelations.HasValue == 27
                    && OntologyRelations.HasProperty == 4,
                    "structural relations should keep their reserved ontology IDs");

                LRD_Core core = NewCore();
                DataFileImportResult jsonResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = jsonPath,
                    Format = DataFileFormat.Json
                });
                Assert(jsonResult.RecordsSucceeded == 1, "complex JSON object root should import");

                IEnumerable<Web_Node> nodes = core.GetDictionary()!.Values;
                Assert(nodes.Any(node => node.Get_NodeType().Id == OntologyTypes.MetadataValues.Id
                    && node.Label is null && node.Aliases.Contains("stringValue")),
                    "an empty JSON string should produce a MetadataValues node with alias stringValue");
                Assert(nodes.Any(node => node.Label == "12" && node.Aliases.Contains("numberValue"))
                    && nodes.Any(node => node.Label == "false" && node.Aliases.Contains("booleanValue"))
                    && nodes.Any(node => node.Label == "null" && node.Aliases.Contains("nullValue"))
                    && nodes.Any(node => node.Label == "https://example.com/doc" && node.Aliases.Contains("pathValue")),
                    "JSON values should carry numberValue, booleanValue, nullValue, and pathValue aliases");
                Assert(nodes.Any(node => node.Label == "item" && node.Aliases.Contains("array_0")),
                    "nested JSON arrays should create indexed array_N item nodes");

                DataFileImportResult xmlResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = xmlPath,
                    Format = DataFileFormat.Xml
                });
                Assert(xmlResult.RecordsSucceeded == 1, "mixed-content XML record should import");
                Assert(nodes.Any(node => node.Get_DataPointer()?.Location.Fragment.TrimStart('#') == "xml/root/mixed[0]/text_0"
                        && node.Label == "First")
                    && nodes.Any(node => node.Get_DataPointer()?.Location.Fragment.TrimStart('#') == "xml/root/mixed[0]/text_1"
                        && node.Label == "Second")
                    && nodes.Any(node => node.Get_DataPointer()?.Location.Fragment.TrimStart('#') == "xml/root/mixed[0]/empty[0]/text_0"
                        && node.Label is null
                        && node.Aliases.Contains("stringValue")),
                    "XML mixed content and empty leaf elements should preserve ordered text positions");

                CoreLogExporter.Export(core, logPath, includeTransient: false);
                LRD_Core restored = NewCore();
                PersistenceImportResult import = CoreLogImporter.ImportCore(restored, logPath, ImportMode.Replace);
                Assert(import.Success, "persisted Phase 5 structural graph should import cleanly");
                Web_Node restoredSource = restored.GetExistingNode(jsonResult.SourceNodeID!.Value)!;
                Assert(Math.Abs((restoredSource.TrustRating ?? 0d) - 0.90d) < 1e-9
                    && !string.IsNullOrEmpty(restoredSource.Get_DataPointer()?.Checksum),
                    "Source trust rating and completed checksum should round-trip through .lrdlog persistence");
            }
            finally
            {
                File.Delete(jsonPath);
                File.Delete(xmlPath);
                if (File.Exists(logPath))
                    File.Delete(logPath);
            }
        }

        /// <summary>Verifies non-triple CSV NLP rows, JSON leaf-string fallback, and cross-source vs same-source WeightUp rules.</summary>
        private static void TestNlpFallbacksAndSemanticDuplicates()
        {
            string ordinaryCsvPath = CreateTempFile(".csv", "person,company\nAlice,Acme\n");
            string fallbackJsonPath = CreateTempFile(".json", "[{\"customField\":\"Alice works at Acme.\"}]");
            string secondSourceJsonPath = CreateTempFile(".json", "[{\"text\":\"Alice works at Acme.\"}]");
            string unsupportedJsonPath = CreateTempFile(".json", "[{\"text\":\"Qqq zzz.\"}]");

            try
            {
                LRD_Core core = NewCore();

                DataFileImportResult csvNlp = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = ordinaryCsvPath,
                    Format = DataFileFormat.Csv,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });
                Assert(csvNlp.RecordsProcessed == 1 && csvNlp.RecordsSucceeded == 1 && csvNlp.ConversationID.HasValue,
                    "ordinary non-triple CSV rows should be formatted as header: value text and processed through NLP");

                DataFileImportResult firstJson = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = fallbackJsonPath,
                    Format = DataFileFormat.Json,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });
                Assert(firstJson.RecordsProcessed == 1 && firstJson.RecordsSucceeded == 1,
                    "JSON records without known prose keys should fall back to string leaf values");

                Web_Node alice = core.FindByLabel("Alice", NodeTypes.Entities).Single();
                float weightAfterFirstJson = alice.Get_WeightUp();

                DataFileImportResult secondSource = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = secondSourceJsonPath,
                    Format = DataFileFormat.Json,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });
                Assert(secondSource.RecordsSucceeded == 1, "second source NLP import should succeed");
                float weightAfterSecondSource = alice.Get_WeightUp();
                Assert(Math.Abs(weightAfterSecondSource - (weightAfterFirstJson + 1f)) < 1e-6f,
                    "reusing a semantic entity from a new file source should increase its WeightUp by exactly 1");

                File.WriteAllText(secondSourceJsonPath, "[{\"text\":\"Alice works at Acme.\"},{\"text\":\"Alice sees Bob.\"}]");
                DataFileImportResult sameSourceReimport = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = secondSourceJsonPath,
                    Format = DataFileFormat.Json,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });
                Assert(sameSourceReimport.RecordsSucceeded == 2, "revised same-source NLP import should process both records");
                Assert(Math.Abs(alice.Get_WeightUp() - weightAfterSecondSource) < 1e-6f,
                    "same-source re-import must not increase existing semantic nodes' WeightUp");

                DataFileImportResult unsupportedResult = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = unsupportedJsonPath,
                    Format = DataFileFormat.Json,
                    Mode = DataFileIngestionMode.ExtractTextForNlp
                });
                Assert(unsupportedResult.RecordsProcessed == 1
                    && unsupportedResult.RecordsSucceeded == 1
                    && unsupportedResult.RecordsUnsupported == 1
                    && Math.Abs((unsupportedResult.SourceTrustRating ?? 0d) - 0.90d) < 1e-9,
                    "syntactically valid but unsupported NLP records should count as successful reads in trust calculation");
            }
            finally
            {
                File.Delete(ordinaryCsvPath);
                File.Delete(fallbackJsonPath);
                File.Delete(secondSourceJsonPath);
                File.Delete(unsupportedJsonPath);
            }
        }

        /// <summary>Verifies re-import at or below 80% trust, moved-file new-source identity, and retry after partial import.</summary>
        private static void TestLowTrustReimportMovedFileAndRetry()
        {
            string path = CreateTempFile(".csv", "name\nAlice\n\"bad\"row\nCarol\n");
            string movedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".csv");

            try
            {
                LRD_Core core = NewCore();
                DataFileImportResult first = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv
                });
                Assert(!first.SkippedHighTrust
                    && Math.Abs((first.SourceTrustRating ?? 0d) - 0.70d) < 1e-9,
                    "initial import with 2 successes and 1 failure should receive 70% trust");

                DataFileImportResult reimportAt70 = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv
                });
                Assert(!reimportAt70.SkippedHighTrust && reimportAt70.RecordsProcessed == 3,
                    "unchanged files with source trust at or below 80% must be reprocessed rather than skipped");

                File.WriteAllText(path, "name\nAlice\nBob\nCarol\n");
                DataFileImportResult partial = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv,
                    MaxRecords = 1
                });
                Assert(partial.Truncated && Math.Abs((partial.SourceTrustRating ?? 0d) - 0.70d) < 1e-9,
                    "a partial/truncated import must not update the Source checksum or trust");

                DataFileImportResult completedRetry = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv
                });
                Assert(!completedRetry.SkippedHighTrust
                    && completedRetry.RecordsSucceeded == 3
                    && Math.Abs((completedRetry.SourceTrustRating ?? 0d) - 0.90d) < 1e-9,
                    "retrying after a partial import should process the file and raise Source trust to 90%");

                File.WriteAllText(path, "name\nAlice\n\"bad\"row\nCarol\n");
                DataFileImportResult lowerScoreReimport = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = path,
                    Format = DataFileFormat.Csv
                });
                Assert(Math.Abs((lowerScoreReimport.SourceTrustRating ?? 0d) - 0.90d) < 1e-9,
                    "a same-source re-import with a lower score must never lower existing Source trust");

                File.Move(path, movedPath);
                DataFileImportResult movedImport = DataFileImporter.Import(core, new DataFileImportOptions
                {
                    FilePath = movedPath,
                    Format = DataFileFormat.Csv
                });
                Assert(movedImport.SourceNodeCreated
                    && movedImport.SourceNodeID != first.SourceNodeID,
                    "moving or renaming a file should create a distinct Source node");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(movedPath))
                    File.Delete(movedPath);
            }
        }

        /// <summary>Creates a temporary UTF-8 fixture file with the requested extension.</summary>
        /// <param name="extension">The fixture file extension.</param>
        /// <param name="contents">The contents to write.</param>
        /// <returns>The absolute path to the temporary file.</returns>
        private static string CreateTempFile(string extension, string contents)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
            File.WriteAllText(path, contents);
            return path;
        }

        /// <summary>Initializes and returns the shared core graph for one smoke test.</summary>
        /// <returns>The initialized singleton graph core.</returns>
        private static LRD_Core NewCore()
        {
            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            return core;
        }
    }
}
