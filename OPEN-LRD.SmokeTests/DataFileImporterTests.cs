using System.IO;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
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
