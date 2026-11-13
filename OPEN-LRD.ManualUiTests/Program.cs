using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using OPEN_LRD;
using OPEN_LRD.Nodes;
using OPEN_LRD.Persistence;

namespace OPEN_LRD.ManualUiTests
{
    public static class Program
    {
        private static int passedChecks = 0;
        private static int failedChecks = 0;
        private static string fixtureDir = "";

        [STAThread]
        public static void Main()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("OPEN-LRD MANUAL UI VERIFICATION RUNNER");
            Console.WriteLine("LARGE TEXT FILE IMPORT (To-Do.txt / AdditionalTools.txt §16)");
            Console.WriteLine("================================================================================");

            // Initialize WPF Application and SynchronizationContext on the STA thread
            if (Application.Current is null)
            {
                new Application();
            }
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            fixtureDir = Path.Combine(Path.GetTempPath(), "open-lrd-manual");

            try
            {
                RunSection0_Preparation();
                RunSection1_LayoutAndDefaults();
                RunSection2_InputValidation();
                RunSection3_DryRun();
                (Guid largeSourceId, Guid largeConvId) = RunSection4_RealImportAutomaticSource();
                RunSection5_ProvidedSourceAndTopicGuids(largeSourceId);
                RunSection6_FailureContinuation();
                RunSection7_SafetyLimits();
                RunSection8_CancellationAndBusyGating();
                RunSection9_ConversationContinuityAndEncoding();
                RunSection10_SignOff();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nUNHANDLED EXCEPTION: {ex}");
                Console.ResetColor();
                failedChecks++;
            }

            Console.WriteLine("\n================================================================================");
            Console.WriteLine($"VERIFICATION COMPLETE: {passedChecks} passed, {failedChecks} failed.");
            Console.WriteLine("================================================================================");

            if (failedChecks > 0)
                Environment.Exit(1);
        }

        private static void Check(bool condition, string message)
        {
            if (condition)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  [x] PASS: ");
                Console.ResetColor();
                Console.WriteLine(message);
                passedChecks++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  [!] FAIL: ");
                Console.ResetColor();
                Console.WriteLine(message);
                failedChecks++;
            }
        }

        private static void DoEvents()
        {
            DispatcherFrame frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(f =>
                {
                    ((DispatcherFrame)f).Continue = false;
                    return null;
                }),
                frame);
            Dispatcher.PushFrame(frame);
        }

        private static void WaitForCondition(Func<bool> condition, int timeoutMs = 60000, string description = "")
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (!condition())
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                {
                    throw new TimeoutException($"Timed out waiting for condition: {description} (after {timeoutMs}ms)");
                }
                DoEvents();
                Thread.Sleep(15);
            }
            DoEvents();
        }

        private static void ClickImport(Tools window)
        {
            MethodInfo? method = typeof(Tools).GetMethod(
                "TextImport_Button_Click",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method is null)
                throw new InvalidOperationException("Could not find TextImport_Button_Click");

            method.Invoke(window, new object[] { window.TextImportButton, new RoutedEventArgs() });
            DoEvents();
        }

        private static void ClickCancel(Tools window)
        {
            MethodInfo? method = typeof(Tools).GetMethod(
                "TextImportCancel_Button_Click",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method is null)
                throw new InvalidOperationException("Could not find TextImportCancel_Button_Click");

            method.Invoke(window, new object[] { window.TextImportCancelButton, new RoutedEventArgs() });
            DoEvents();
        }

        private static void WaitForImportToFinish(Tools window, int timeoutMs = 60000)
        {
            WaitForCondition(
                () => window.TextImportButton.IsEnabled && !window.TextImportCancelButton.IsEnabled,
                timeoutMs,
                "Text import completion (TextImportButton enabled, Cancel disabled)");
        }

        // ================================================================================
        // 0. PREPARATION
        // ================================================================================
        private static void RunSection0_Preparation()
        {
            Console.WriteLine("\n--- SECTION 0: PREPARATION ---");

            Check(Directory.Exists(fixtureDir), $"Test fixture directory exists: {fixtureDir}");

            string[] requiredFiles = { "small-valid.txt", "large.txt", "mixed.txt", "cancel.txt", "binary.bin", "oversize.txt" };
            foreach (string file in requiredFiles)
            {
                string path = Path.Combine(fixtureDir, file);
                Check(File.Exists(path), $"Fixture file exists: {file} ({new FileInfo(path).Length} bytes)");
            }

            LRD_Core.Instance.InitializeCore();
            Check(LRD_Core.Instance.initialized, "Core initialized successfully.");
        }

        // ================================================================================
        // 1. WINDOW LAYOUT AND DEFAULTS
        // ================================================================================
        private static void RunSection1_LayoutAndDefaults()
        {
            Console.WriteLine("\n--- SECTION 1: WINDOW LAYOUT AND DEFAULTS ---");

            Tools window = new Tools();
            DoEvents();

            Check(window.ExportLogsButton is not null, "Export section present (ExportLogsButton).");
            Check(window.ImportCoreLogButton is not null, "Import section present (ImportCoreLogButton).");
            Check(window.SeedOntologyButton is not null, "Ontology section present (SeedOntologyButton).");
            Check(window.StatusBox is not null, "Status box present (StatusBox).");

            Check(window.TextImportPathBox.Text == "", "Default File path is empty.");
            Check(window.TextImportPathBox.IsReadOnly, "File path box is read-only.");

            Check(window.TextImportSourceBox.Text == "", "Default Source ID is blank.");
            Check(window.TextImportTopicBox.Text == "", "Default Topic ID is blank.");

            Check(window.TextImportDryRunCheckBox.IsChecked == false, "Default Dry run is unchecked.");
            Check(window.TextImportMaxMegabytesBox.Text == "50", "Default Max file MB is 50.");
            Check(window.TextImportMaxChunksBox.Text == "10000", "Default Max chunks is 10000.");

            Check(window.TextImportButton.IsEnabled, "Default Import Text File button is enabled.");
            Check(!window.TextImportCancelButton.IsEnabled, "Default Cancel button is disabled.");

            Check(window.TextImportProgressBar.Value == 0, "Default Progress bar value is 0.");
            Check(!window.TextImportProgressBar.IsIndeterminate, "Default Progress bar is determinate.");

            Check(window.TextImportStatusText.Text == "Ready. 0 chunks.", "Default status line is 'Ready. 0 chunks.'");

            Check(window.MinWidth == 760, $"Window MinWidth is 760 (actual: {window.MinWidth})");
            Check(window.MinHeight == 700, $"Window MinHeight is 700 (actual: {window.MinHeight})");

            FieldInfo? filterField = typeof(Tools).GetField("TextFileFilter", BindingFlags.NonPublic | BindingFlags.Static);
            string? filter = filterField?.GetValue(null) as string;
            Check(filter == "Text files (*.txt;*.md;*.log)|*.txt;*.md;*.log|All files (*.*)|*.*",
                $"Browse dialog filter matches specification: '{filter}'");
        }

        // ================================================================================
        // 2. INPUT VALIDATION (NO IMPORT MAY START)
        // ================================================================================
        private static void RunSection2_InputValidation()
        {
            Console.WriteLine("\n--- SECTION 2: INPUT VALIDATION (NO IMPORT MAY START) ---");

            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            int initialNodeCount = LRD_Core.Instance.GetDictionary()?.Count ?? 0;

            // 1. No file selected
            window.TextImportPathBox.Text = "";
            ClickImport(window);
            Check(window.TextImportStatusText.Text == "Choose a text file first.",
                $"No file selected: status reports '{window.TextImportStatusText.Text}'");
            Check(window.TextImportProgressBar.Value == 0, "Progress remains 0.");
            Check(window.StatusBox.Text == "Ready.", $"Status box report remains untouched (actual: '{window.StatusBox.Text}').");

            // 2. Non-existent file
            window.TextImportPathBox.Text = Path.Combine(fixtureDir, "does-not-exist.txt");
            ClickImport(window);
            Check(window.TextImportStatusText.Text == "The selected text file no longer exists.",
                $"Missing file: status reports '{window.TextImportStatusText.Text}'");

            // Valid file for subsequent validation tests
            string smallPath = Path.Combine(fixtureDir, "small-valid.txt");
            window.TextImportPathBox.Text = smallPath;

            // 3. Source ID = "abc"
            window.TextImportSourceBox.Text = "abc";
            ClickImport(window);
            Check(window.TextImportStatusText.Text == "Source ID must be a non-empty GUID, or blank.",
                $"Invalid Source ID 'abc': status reports '{window.TextImportStatusText.Text}'");

            // 4. Source ID = Empty GUID
            window.TextImportSourceBox.Text = Guid.Empty.ToString();
            ClickImport(window);
            Check(window.TextImportStatusText.Text == "Source ID must be a non-empty GUID, or blank.",
                $"Empty Source GUID: status reports '{window.TextImportStatusText.Text}'");
            window.TextImportSourceBox.Text = "";

            // 5. Topic ID = "not-a-guid"
            window.TextImportTopicBox.Text = "not-a-guid";
            ClickImport(window);
            Check(window.TextImportStatusText.Text == "Topic ID must be a non-empty GUID, or blank.",
                $"Invalid Topic ID: status reports '{window.TextImportStatusText.Text}'");
            window.TextImportTopicBox.Text = "";

            // 6. Max file MB = 0, then -5, then "abc"
            string[] badMb = { "0", "-5", "abc" };
            foreach (string val in badMb)
            {
                window.TextImportMaxMegabytesBox.Text = val;
                ClickImport(window);
                Check(window.TextImportStatusText.Text == "Max file MB must be a positive whole number.",
                    $"Max file MB = '{val}': status reports '{window.TextImportStatusText.Text}'");
            }
            window.TextImportMaxMegabytesBox.Text = "50";

            // 7. Max chunks = 0, then -5, then "abc"
            string[] badChunks = { "0", "-5", "abc" };
            foreach (string val in badChunks)
            {
                window.TextImportMaxChunksBox.Text = val;
                ClickImport(window);
                Check(window.TextImportStatusText.Text == "Max chunks must be a positive whole number.",
                    $"Max chunks = '{val}': status reports '{window.TextImportStatusText.Text}'");
            }
            window.TextImportMaxChunksBox.Text = "10000";

            // 8. Max file MB overflow (2147483648)
            window.TextImportMaxMegabytesBox.Text = "2147483648";
            ClickImport(window);
            Check(window.TextImportStatusText.Text == "Max file MB must be a positive whole number.",
                $"Max file MB overflow: status reports '{window.TextImportStatusText.Text}'");
            window.TextImportMaxMegabytesBox.Text = "50";

            // 9. Cancel while idle
            string statusBeforeCancel = window.TextImportStatusText.Text;
            ClickCancel(window);
            Check(window.TextImportStatusText.Text == statusBeforeCancel,
                $"Cancel button while idle does nothing (status remained '{statusBeforeCancel}').");

            // 10. No graph activity
            int finalNodeCount = LRD_Core.Instance.GetDictionary()?.Count ?? 0;
            Check(finalNodeCount == initialNodeCount, $"Graph untouched across all validation tests (nodes: {finalNodeCount}).");
        }

        // ================================================================================
        // 3. DRY RUN (STRICTLY READ-ONLY)
        // ================================================================================
        private static void RunSection3_DryRun()
        {
            Console.WriteLine("\n--- SECTION 3: DRY RUN (STRICTLY READ-ONLY) ---");

            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            string largePath = Path.Combine(fixtureDir, "large.txt");
            window.TextImportPathBox.Text = largePath;
            window.TextImportDryRunCheckBox.IsChecked = true;
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = "";

            ClickImport(window);
            WaitForImportToFinish(window);

            string status = window.TextImportStatusText.Text;
            Console.WriteLine($"  Status line: {status}");
            Check(status.StartsWith("Dry run: "), "Status line begins with 'Dry run: '");

            Match m = Regex.Match(status, @"Dry run:\s+(\d+)\s+chunk\(s\),\s+(\d+)\s+paragraph\(s\),\s+(\d+)\s+sentence\(s\),\s+(\d+)\s+character\(s\)\.");
            Check(m.Success, $"Status line matches format regex: '{status}'");
            if (m.Success)
            {
                int chunks = int.Parse(m.Groups[1].Value);
                int chars = int.Parse(m.Groups[4].Value);
                Check(chunks > 1, $"Chunks N > 1 (actual: {chunks})");
                Check(chars > 20000, $"Characters C > 20,000 (actual: {chars})");
            }

            Check(window.TextImportProgressBar.Value == 100, $"Progress bar reached 100% (actual: {window.TextImportProgressBar.Value})");

            string report = window.StatusBox.Text;
            Check(report.Contains("Text import dry run."), "Status box contains 'Text import dry run.'");
            Check(report.Contains("File: " + largePath), "Status box lists File.");
            Check(report.Contains("Characters:"), "Status box lists Characters.");
            Check(report.Contains("Paragraphs:"), "Status box lists Paragraphs.");
            Check(report.Contains("Sentences:"), "Status box lists Sentences.");
            Check(report.Contains("Chunks:"), "Status box lists Chunks.");
            Check(report.Contains("Processed: 0"), "Status box lists 'Processed: 0'.");

            Check(!report.Contains("Ingested:"), "Dry run does NOT list Ingested.");
            Check(!report.Contains("Unsupported:"), "Dry run does NOT list Unsupported.");
            Check(!report.Contains("Failed:"), "Dry run does NOT list Failed.");
            Check(!report.Contains("Skipped:"), "Dry run does NOT list Skipped.");
            Check(!report.Contains("Conversation:"), "Dry run does NOT list Conversation.");
            Check(!report.Contains("Source node:"), "Dry run does NOT list Source node.");

            int nodeCount = LRD_Core.Instance.GetDictionary()?.Count ?? 0;
            Check(nodeCount == 0, $"Core graph has 0 nodes after dry run (actual: {nodeCount}).");

            // Dry run with unknown-but-valid Source ID
            Guid unknownGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
            window.TextImportSourceBox.Text = unknownGuid.ToString();
            ClickImport(window);
            WaitForImportToFinish(window);

            string expectedErr = $"Text import failed: Source node '{unknownGuid}' was not found.";
            Check(window.TextImportStatusText.Text == expectedErr,
                $"Dry run unknown Source ID fails validation before import: '{window.TextImportStatusText.Text}'");
            Check((LRD_Core.Instance.GetDictionary()?.Count ?? 0) == 0, "Core graph remains clean.");
        }

        // ================================================================================
        // 4. REAL IMPORT WITH AUTOMATIC SOURCE (large.txt)
        // ================================================================================
        private static (Guid sourceId, Guid convId) RunSection4_RealImportAutomaticSource()
        {
            Console.WriteLine("\n--- SECTION 4: REAL IMPORT WITH AUTOMATIC SOURCE (large.txt) ---");

            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            string largePath = Path.Combine(fixtureDir, "large.txt");
            window.TextImportPathBox.Text = largePath;
            window.TextImportDryRunCheckBox.IsChecked = false;
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = "";

            ClickImport(window);
            WaitForImportToFinish(window, 120000);

            string status = window.TextImportStatusText.Text;
            Console.WriteLine($"  Final status line: {status}");

            Match m = Regex.Match(status, @"Processed (\d+) of (\d+) chunk\(s\): (\d+) ingested, (\d+) unsupported, (\d+) failed\.");
            Check(m.Success, $"Status line matches expected summary format: '{status}'");

            if (m.Success)
            {
                int processed = int.Parse(m.Groups[1].Value);
                int total = int.Parse(m.Groups[2].Value);
                int ingested = int.Parse(m.Groups[3].Value);
                int unsupported = int.Parse(m.Groups[4].Value);
                int failed = int.Parse(m.Groups[5].Value);

                Check(processed == total && total > 1, $"Processed all {total} chunks.");
                Check(failed == 0, $"Failed count F == 0 (actual: {failed}).");
                Check(ingested > 0, $"Ingested count X > 0 (actual: {ingested}, unsupported: {unsupported}).");
            }

            Check(window.TextImportProgressBar.Value == 100, $"Progress bar reached 100% (actual: {window.TextImportProgressBar.Value})");

            string report = window.StatusBox.Text;
            Check(report.Contains("Text import complete."), "Report contains 'Text import complete.'");
            Check(report.Contains("File: " + largePath), "Report contains File path.");
            Check(report.Contains("Characters:"), "Report contains Characters.");
            Check(report.Contains("Paragraphs:"), "Report contains Paragraphs.");
            Check(report.Contains("Sentences:"), "Report contains Sentences.");
            Check(report.Contains("Chunks:"), "Report contains Chunks.");
            Check(report.Contains("Processed:"), "Report contains Processed.");
            Check(report.Contains("Ingested:"), "Report contains Ingested.");
            Check(report.Contains("Unsupported:"), "Report contains Unsupported.");
            Check(report.Contains("Failed: 0"), "Report contains 'Failed: 0'.");
            Check(report.Contains("Skipped: 0"), "Report contains 'Skipped: 0'.");

            Match convMatch = Regex.Match(report, @"Conversation:\s+([a-fA-F0-9\-]{36})");
            Check(convMatch.Success, $"Conversation GUID present: '{convMatch.Value}'");
            Guid convId = convMatch.Success ? Guid.Parse(convMatch.Groups[1].Value) : Guid.Empty;

            Match srcMatch = Regex.Match(report, @"Source node:\s+([a-fA-F0-9\-]{36})\s+\(created\)");
            Check(srcMatch.Success, $"Source node GUID with '(created)' present: '{srcMatch.Value}'");
            Guid sourceId = srcMatch.Success ? Guid.Parse(srcMatch.Groups[1].Value) : Guid.Empty;

            // Verify in Core directly and via export
            IReadOnlyDictionary<Guid, Web_Node>? dict = LRD_Core.Instance.GetDictionary();
            Check(dict is not null && dict.ContainsKey(sourceId), $"Source node '{sourceId}' exists in core dictionary.");

            if (dict is not null && dict.TryGetValue(sourceId, out Web_Node? sourceNode))
            {
                Check(sourceNode.Label == "Source: large.txt", $"Source node label is 'Source: large.txt' (actual: '{sourceNode.Label}')");
                Check(sourceNode.Get_NodeType() == NodeTypes.Sources, $"Source node type is NodeTypes.Sources (actual: {sourceNode.Get_NodeType()})");
                Check(sourceNode.Get_Lifetime() == NodeLifetime.Permanent, $"Source node lifetime is Permanent (actual: {sourceNode.Get_Lifetime()})");
                Check(sourceNode.Get_DataPointer()?.Location.ToString().StartsWith("file://") == true, $"DataPointer location is file URI: '{sourceNode.Get_DataPointer()?.Location}'");
            }

            // Export core log and check JSON
            string exportPath = Path.Combine(Path.GetTempPath(), "test-export-large.lrdlog");
            CoreLogExportResult exportResult = CoreLogExporter.Export(LRD_Core.Instance, exportPath, false);
            string json = File.ReadAllText(exportPath);
            int count = Regex.Matches(json, Regex.Escape("\"label\": \"Source: large.txt\"")).Count;
            Check(count == 1, $"Exported log contains exactly one '\"label\": \"Source: large.txt\"' (actual: {count})");

            // Import large.txt again with Source blank
            ClickImport(window);
            WaitForImportToFinish(window, 120000);

            string report2 = window.StatusBox.Text;
            Match srcMatch2 = Regex.Match(report2, @"Source node:\s+([a-fA-F0-9\-]{36})");
            Check(srcMatch2.Success && !report2.Contains(srcMatch2.Groups[1].Value + " (created)"),
                "Second import reports Source node with NO ' (created)'.");
            if (srcMatch2.Success)
            {
                Guid secondSourceId = Guid.Parse(srcMatch2.Groups[1].Value);
                Check(secondSourceId == sourceId, $"Reused the exact same Source GUID: {sourceId}");
            }

            CoreLogExporter.Export(LRD_Core.Instance, exportPath, false);
            json = File.ReadAllText(exportPath);
            count = Regex.Matches(json, Regex.Escape("\"label\": \"Source: large.txt\"")).Count;
            Check(count == 1, $"Exported log still contains exactly one 'Source: large.txt' node after re-import (actual: {count})");

            return (sourceId, convId);
        }

        // ================================================================================
        // 5. PROVIDED SOURCE AND TOPIC GUIDS
        // ================================================================================
        private static void RunSection5_ProvidedSourceAndTopicGuids(Guid existingSourceId)
        {
            Console.WriteLine("\n--- SECTION 5: PROVIDED SOURCE AND TOPIC GUIDS ---");

            Tools window = new Tools();
            DoEvents();

            string smallPath = Path.Combine(fixtureDir, "small-valid.txt");
            window.TextImportPathBox.Text = smallPath;
            window.TextImportDryRunCheckBox.IsChecked = false;

            // 1. Provided Source ID
            window.TextImportSourceBox.Text = existingSourceId.ToString();
            window.TextImportTopicBox.Text = "";

            ClickImport(window);
            WaitForImportToFinish(window);

            string report = window.StatusBox.Text;
            Check(report.Contains("Text import complete."), "Import with provided Source ID succeeds.");
            Check(report.Contains($"Source node: {existingSourceId}") && !report.Contains($"Source node: {existingSourceId} (created)"),
                "Source node line reports provided ID with NO '(created)'.");

            // Verify no duplicate "Source: small-valid.txt" node was created
            var dict = LRD_Core.Instance.GetDictionary()!;
            bool hasSmallSource = dict.Values.Any(n => n.Label == "Source: small-valid.txt");
            Check(!hasSmallSource, "Provided source suppresses automatic 'Source: small-valid.txt' creation.");

            // 2. Provided Topic ID (valid existing node)
            Guid topicId = existingSourceId; // use existing node as topic
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = topicId.ToString();

            ClickImport(window);
            WaitForImportToFinish(window);

            Check(window.StatusBox.Text.Contains("Text import complete."), "Import with provided Topic ID succeeds.");

            // 3. Unknown Source ID
            Guid unknownSource = Guid.Parse("11111111-1111-1111-1111-111111111111");
            window.TextImportSourceBox.Text = unknownSource.ToString();
            window.TextImportTopicBox.Text = "";

            ClickImport(window);
            WaitForImportToFinish(window);

            Check(window.TextImportStatusText.Text == $"Text import failed: Source node '{unknownSource}' was not found.",
                $"Unknown Source ID reports expected error: '{window.TextImportStatusText.Text}'");

            // 4. Unknown Topic ID
            Guid unknownTopic = Guid.Parse("22222222-2222-2222-2222-222222222222");
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = unknownTopic.ToString();

            ClickImport(window);
            WaitForImportToFinish(window);

            Check(window.TextImportStatusText.Text == $"Text import failed: Topic node '{unknownTopic}' was not found.",
                $"Unknown Topic ID reports expected error: '{window.TextImportStatusText.Text}'");
        }

        // ================================================================================
        // 6. FAILURE CONTINUATION (mixed.txt)
        // ================================================================================
        private static void RunSection6_FailureContinuation()
        {
            Console.WriteLine("\n--- SECTION 6: FAILURE CONTINUATION (mixed.txt) ---");

            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            string mixedPath = Path.Combine(fixtureDir, "mixed.txt");
            window.TextImportPathBox.Text = mixedPath;
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = "";
            window.TextImportDryRunCheckBox.IsChecked = false;

            ClickImport(window);
            WaitForImportToFinish(window);

            string status = window.TextImportStatusText.Text;
            Console.WriteLine($"  Status line: {status}");
            Check(status == "Processed 3 of 3 chunk(s): 2 ingested, 1 unsupported, 0 failed.",
                $"Status line confirms continuation: '{status}'");

            string report = window.StatusBox.Text;
            Check(report.Contains("Ingested: 2"), "Report shows Ingested: 2.");
            Check(report.Contains("Unsupported: 1"), "Report shows Unsupported: 1.");
            Check(report.Contains("Failed: 0"), "Report shows Failed: 0.");
            Check(report.Contains("Notes:") && report.Contains("Chunk 2 contained no ingestible relations"),
                "Report Notes lists per-chunk outcome for chunk 2 ('Qqq zzz.').");

            var dict = LRD_Core.Instance.GetDictionary()!;
            bool hasAcme = dict.Values.Any(n => n.Label == "Acme" || n.Aliases.Contains("Acme"));
            bool hasGlobex = dict.Values.Any(n => n.Label == "Globex" || n.Aliases.Contains("Globex"));
            int sourceCount = dict.Values.Count(n => n.Label == "Source: mixed.txt");

            Check(hasAcme, "Entity 'Acme' ingested successfully.");
            Check(hasGlobex, "Entity 'Globex' ingested successfully.");
            Check(sourceCount == 1, $"Source node 'Source: mixed.txt' exists exactly once (actual: {sourceCount}).");
        }

        // ================================================================================
        // 7. SAFETY LIMITS
        // ================================================================================
        private static void RunSection7_SafetyLimits()
        {
            Console.WriteLine("\n--- SECTION 7: SAFETY LIMITS ---");

            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            int initialNodes = LRD_Core.Instance.GetDictionary()?.Count ?? 0;

            // 1. Oversize file with 1 MB limit
            string oversizePath = Path.Combine(fixtureDir, "oversize.txt");
            window.TextImportPathBox.Text = oversizePath;
            window.TextImportMaxMegabytesBox.Text = "1";
            window.TextImportMaxChunksBox.Text = "10000";

            ClickImport(window);
            WaitForImportToFinish(window);

            string status = window.TextImportStatusText.Text;
            Console.WriteLine($"  Oversize status: {status}");
            Check(status.StartsWith("Text import failed: The file is") && status.EndsWith("bytes, which exceeds the 1048576-byte limit."),
                $"Oversize error message matches: '{status}'");
            Check((LRD_Core.Instance.GetDictionary()?.Count ?? 0) == initialNodes, "No nodes created on oversize rejection.");

            // 2. Binary file
            string binaryPath = Path.Combine(fixtureDir, "binary.bin");
            window.TextImportPathBox.Text = binaryPath;
            window.TextImportMaxMegabytesBox.Text = "50";

            ClickImport(window);
            WaitForImportToFinish(window);

            string binStatus = window.TextImportStatusText.Text;
            Console.WriteLine($"  Binary status: {binStatus}");
            Check(binStatus == "Text import failed: The file appears to be binary and cannot be imported as text.",
                $"Binary rejection error matches: '{binStatus}'");
            Check((LRD_Core.Instance.GetDictionary()?.Count ?? 0) == initialNodes, "No nodes created on binary rejection.");

            // 3. Max chunks = 1
            string largePath = Path.Combine(fixtureDir, "large.txt");
            window.TextImportPathBox.Text = largePath;
            window.TextImportMaxChunksBox.Text = "1";
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = "";

            ClickImport(window);
            WaitForImportToFinish(window);

            string chunkStatus = window.TextImportStatusText.Text;
            Console.WriteLine($"  Max chunks status: {chunkStatus}");
            Check(chunkStatus.Contains("Processed 1 of 1 chunk(s):") && chunkStatus.Contains("Chunk limit reached; the file remainder was not imported."),
                $"Status line confirms chunk limit reached: '{chunkStatus}'");

            string chunkReport = window.StatusBox.Text;
            Check(chunkReport.Contains("The chunk limit was reached; the remainder of the file was not imported."),
                "Status box notes that remainder of the file was not imported.");

            // Restore defaults
            window.TextImportMaxMegabytesBox.Text = "50";
            window.TextImportMaxChunksBox.Text = "10000";
            Check(window.TextImportMaxMegabytesBox.Text == "50" && window.TextImportMaxChunksBox.Text == "10000",
                "Restored default limits (50 MB, 10000 chunks).");
        }

        // ================================================================================
        // 8. CANCELLATION AND BUSY-STATE GATING
        // ================================================================================
        private static void RunSection8_CancellationAndBusyGating()
        {
            Console.WriteLine("\n--- SECTION 8: CANCELLATION AND BUSY-STATE GATING ---");

            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            string cancelPath = Path.Combine(fixtureDir, "cancel.txt");
            window.TextImportPathBox.Text = cancelPath;
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = "";
            window.TextImportDryRunCheckBox.IsChecked = false;

            // Start import
            ClickImport(window);

            // Verify controls disabled while active
            Check(!window.TextImportButton.IsEnabled, "Busy state: TextImportButton is disabled.");
            Check(!window.TextImportBrowseButton.IsEnabled, "Busy state: TextImportBrowseButton is disabled.");
            Check(!window.TextImportPathBox.IsEnabled, "Busy state: TextImportPathBox is disabled.");
            Check(!window.TextImportSourceBox.IsEnabled, "Busy state: TextImportSourceBox is disabled.");
            Check(!window.TextImportTopicBox.IsEnabled, "Busy state: TextImportTopicBox is disabled.");
            Check(!window.TextImportDryRunCheckBox.IsEnabled, "Busy state: TextImportDryRunCheckBox is disabled.");
            Check(!window.TextImportMaxMegabytesBox.IsEnabled, "Busy state: TextImportMaxMegabytesBox is disabled.");
            Check(!window.TextImportMaxChunksBox.IsEnabled, "Busy state: TextImportMaxChunksBox is disabled.");
            Check(!window.ImportCoreLogButton.IsEnabled, "Busy state: ImportCoreLogButton is disabled.");
            Check(!window.ImportTransientLogButton.IsEnabled, "Busy state: ImportTransientLogButton is disabled.");
            Check(!window.MergeModeRadio.IsEnabled, "Busy state: MergeModeRadio is disabled.");
            Check(!window.ReplaceModeRadio.IsEnabled, "Busy state: ReplaceModeRadio is disabled.");
            Check(!window.ExportLogsButton.IsEnabled, "Busy state: ExportLogsButton is disabled.");
            Check(!window.SeedOntologyButton.IsEnabled, "Busy state: SeedOntologyButton is disabled.");
            Check(!window.LoadGcideButton.IsEnabled, "Busy state: LoadGcideButton is disabled.");
            Check(!window.ExportOntologyButton.IsEnabled, "Busy state: ExportOntologyButton is disabled.");

            Check(window.TextImportCancelButton.IsEnabled, "Busy state: TextImportCancelButton is ENABLED.");
            Check(!window.TextImportProgressBar.IsIndeterminate, "Busy state: Progress bar is determinate.");

            // Wait until at least 1 chunk has processed, then click Cancel
            WaitForCondition(
                () => window.TextImportStatusText.Text.StartsWith("Importing chunk") && !window.TextImportStatusText.Text.Contains("chunk 0 of"),
                15000,
                "At least one chunk processed");

            ClickCancel(window);
            Check(window.TextImportStatusText.Text == "Cancelling after the current chunk...",
                $"Click Cancel sets status to: '{window.TextImportStatusText.Text}'");

            // Wait for cancel to finalize
            WaitForImportToFinish(window);

            string finalStatus = window.TextImportStatusText.Text;
            Console.WriteLine($"  Final status line: {finalStatus}");
            Check(finalStatus.Contains("Import cancelled.") || finalStatus.Contains("cancelled"),
                $"Final status line indicates cancellation: '{finalStatus}'");

            string cancelReport = window.StatusBox.Text;
            Check(cancelReport.Contains("The import was cancelled; ingested chunks remain."),
                "Status box reports: 'The import was cancelled; ingested chunks remain.'");

            Check(window.TextImportProgressBar.Value < 100, $"Progress bar stopped below 100% (value: {window.TextImportProgressBar.Value})");
            Check(Regex.IsMatch(cancelReport, @"Skipped:\s+[1-9]\d*"), "Report shows Skipped > 0.");

            // Verify knowledge ingested before cancellation remains
            var dict = LRD_Core.Instance.GetDictionary()!;
            Check(dict.Count > 0, $"Ingested nodes remain in Core after cancellation (node count: {dict.Count}).");

            // Verify controls re-enabled
            Check(window.TextImportButton.IsEnabled, "After cancel: TextImportButton is re-enabled.");
            Check(window.TextImportBrowseButton.IsEnabled, "After cancel: TextImportBrowseButton is re-enabled.");
            Check(!window.TextImportCancelButton.IsEnabled, "After cancel: Cancel button is disabled.");
            Check(window.ExportLogsButton.IsEnabled, "After cancel: ExportLogsButton is re-enabled.");
            Check(window.SeedOntologyButton.IsEnabled, "After cancel: SeedOntologyButton is re-enabled.");

            // Verify starting small-valid.txt afterwards works normally
            string smallPath = Path.Combine(fixtureDir, "small-valid.txt");
            window.TextImportPathBox.Text = smallPath;
            ClickImport(window);
            WaitForImportToFinish(window);

            Check(window.StatusBox.Text.Contains("Text import complete."), "Starting small-valid.txt after cancellation works cleanly.");
            Check(!window.TextImportStatusText.Text.Contains("already running"), "No stale 'already running' status.");
        }

        // ================================================================================
        // 9. CONVERSATION CONTINUITY AND ENCODING
        // ================================================================================
        private static void RunSection9_ConversationContinuityAndEncoding()
        {
            Console.WriteLine("\n--- SECTION 9: CONVERSATION CONTINUITY AND ENCODING ---");

            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            string smallPath = Path.Combine(fixtureDir, "small-valid.txt");
            window.TextImportPathBox.Text = smallPath;
            window.TextImportSourceBox.Text = "";
            window.TextImportTopicBox.Text = "";
            window.TextImportDryRunCheckBox.IsChecked = false;

            ClickImport(window);
            WaitForImportToFinish(window);

            string report = window.StatusBox.Text;
            Check(report.Contains("Ingested: 2"), "Both chunks of small-valid.txt ingested (Ingested: 2).");

            Match convMatch = Regex.Match(report, @"Conversation:\s+([a-fA-F0-9\-]{36})");
            Check(convMatch.Success, $"Report contains exactly one Conversation ID: {convMatch.Value}");

            var dict = LRD_Core.Instance.GetDictionary()!;

            // Verify "She" did not become a node
            bool hasShe = dict.Values.Any(n => string.Equals(n.Label, "She", StringComparison.OrdinalIgnoreCase)
                || n.Aliases.Any(a => string.Equals(a, "She", StringComparison.OrdinalIgnoreCase)));
            Check(!hasShe, "Pronoun 'She' was resolved via conversation and did NOT become a separate node.");

            // Verify Alice and Bob exist and are connected
            Web_Node? aliceNode = dict.Values.FirstOrDefault(n => n.Label == "Alice" || n.Aliases.Contains("Alice"));
            Web_Node? bobNode = dict.Values.FirstOrDefault(n => n.Label == "Bob" || n.Aliases.Contains("Bob"));

            Check(aliceNode is not null, "Entity node 'Alice' exists.");
            Check(bobNode is not null, "Entity node 'Bob' exists.");

            if (aliceNode is not null && bobNode is not null)
            {
                var links = LRD_Core.Instance.GetLinks(aliceNode.ID);
                bool linkedToBob = links.Any(l => l.DestinationID == bobNode.ID);
                Check(linkedToBob, "Alice is linked to Bob through the second chunk's resolved relation ('see').");
            }

            int sourceNodeCount = dict.Values.Count(n => n.Label == "Source: small-valid.txt");
            Check(sourceNodeCount == 1, $"Source node 'Source: small-valid.txt' exists exactly once (actual: {sourceNodeCount}).");

            // Verify BOM and CRLF encoding handled cleanly
            byte[] fileBytes = File.ReadAllBytes(smallPath);
            bool hasBom = fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF;
            Check(hasBom, "small-valid.txt verified as UTF-8 with BOM.");
            Check(aliceNode is not null && aliceNode.Label is not null && !aliceNode.Label.Contains("\uFEFF"), "Alice label contains no stray BOM characters.");
        }

        // ================================================================================
        // 10. SIGN-OFF
        // ================================================================================
        private static void RunSection10_SignOff()
        {
            Console.WriteLine("\n--- SECTION 10: SIGN-OFF ---");
            Console.WriteLine($"All automated UI checklist sections executed.");
            Console.WriteLine($"Total checks passed: {passedChecks}");
            Console.WriteLine($"Total checks failed: {failedChecks}");
            Check(failedChecks == 0, "All UI checklist requirements passed with 0 failures.");
        }
    }
}
