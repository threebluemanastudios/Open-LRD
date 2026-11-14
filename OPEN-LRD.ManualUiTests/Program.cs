using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using OPEN_LRD;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Persistence;
using OPEN_LRD.Visualization;
using OPEN_LRD.Visualizer;

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
                RunSection11_VisualizerWindow();
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
        // 11. 3D VISUALIZER SHELL AND LIFECYCLE (Phase 3)
        // ================================================================================
        private static void RunSection11_VisualizerWindow()
        {
            Console.WriteLine("\n--- SECTION 11: 3D VISUALIZER SHELL AND LIFECYCLE (Phase 3) ---");

            string defaultSettingsPath = VisualizerSettings.GetDefaultPath();
            if (File.Exists(defaultSettingsPath))
                File.Delete(defaultSettingsPath);

            // 1. MainWindow gating
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            DoEvents();

            Check(mainWindow.VisualizerButton is not null, "MainWindow has a dedicated VisualizerButton.");
            Check(!mainWindow.VisualizerButton!.IsEnabled, "VisualizerButton is disabled before Launch Core.");
            Check(mainWindow.VisualizerButton.ToolTip?.ToString()?.Contains("Launch Core") == true,
                "VisualizerButton tooltip prompts to launch Core first.");

            // Launch core via click handler
            MethodInfo? launchClick = typeof(MainWindow).GetMethod(
                "Launch_Button_Click",
                BindingFlags.NonPublic | BindingFlags.Instance);
            launchClick?.Invoke(mainWindow, new object[] { mainWindow.LaunchButton, new RoutedEventArgs() });
            DoEvents();

            Check(mainWindow.VisualizerButton.IsEnabled, "VisualizerButton is enabled after successful Core launch.");

            // 2. Open VisualizerWindow from MainWindow
            MethodInfo? visualizerClick = typeof(MainWindow).GetMethod(
                "Visualizer_Button_Click",
                BindingFlags.NonPublic | BindingFlags.Instance);
            visualizerClick?.Invoke(mainWindow, new object[] { mainWindow.VisualizerButton, new RoutedEventArgs() });
            DoEvents();

            FieldInfo? winField = typeof(MainWindow).GetField(
                "visualizerWindow",
                BindingFlags.NonPublic | BindingFlags.Instance);
            VisualizerWindow? visualizerWin = winField?.GetValue(mainWindow) as VisualizerWindow;

            Check(visualizerWin is not null, "Visualizer_Button_Click creates and displays VisualizerWindow.");
            Check(visualizerWin?.WindowStyle == WindowStyle.None, "VisualizerWindow uses WindowStyle.None (borderless).");
            Check(visualizerWin?.WindowState == WindowState.Maximized, "VisualizerWindow opens WindowState.Maximized (fullscreen).");
            Check(visualizerWin?.ResizeMode == ResizeMode.NoResize, "VisualizerWindow uses ResizeMode.NoResize.");
            Check(visualizerWin?.ShowInTaskbar == true, "VisualizerWindow shows in taskbar.");
            Check(visualizerWin?.Title == "OPEN-LRD Visualizer", "VisualizerWindow title is 'OPEN-LRD Visualizer'.");

            // 3. Single-instance activation
            visualizerWin!.WindowState = WindowState.Minimized;
            visualizerClick?.Invoke(mainWindow, new object[] { mainWindow.VisualizerButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.WindowState == WindowState.Maximized, "Subsequent Visualizer button click restores minimized window to Maximized.");
            Check(ReferenceEquals(winField?.GetValue(mainWindow), visualizerWin), "Subsequent click reuses single instance (no duplicate window).");

            // 4. Empty state verification
            LRD_Core.Instance.InitializeCore();
            Task emptyRefresh = visualizerWin.RefreshGraphForTestingAsync();
            while (!emptyRefresh.IsCompleted)
            {
                DoEvents();
                Thread.Sleep(10);
            }
            emptyRefresh.GetAwaiter().GetResult();
            DoEvents();

            Check(visualizerWin.EmptyStateOverlay.Visibility == Visibility.Visible, "Empty graph displays EmptyStateOverlay.");
            Check(visualizerWin.SummaryNodesText.Text.Contains("0"), "Summary shows 0 nodes.");
            Check(!visualizerWin.FrameAllButton.IsEnabled, "FrameAllButton is disabled on empty graph.");
            Check(!visualizerWin.RecomputeButton.IsEnabled, "RecomputeButton is disabled on empty graph.");
            Check(!visualizerWin.ResetViewButton.IsEnabled, "ResetViewButton is disabled on empty graph.");
            Check(!visualizerWin.Scene.HasGeometry, "VisualizerScene contains no geometry on empty graph.");

            // 5. Populated scene verification
            Guid n1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
            Guid n2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
            Guid n3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
            Web_Node node1 = new Web_Node(n1, NodeTypes.Basics, NodeLifetime.Permanent, 100);
            node1.Set_Label("Node Alpha");
            Web_Node node2 = new Web_Node(n2, NodeTypes.Basics, NodeLifetime.Permanent, 100);
            node2.Set_Label("Node Beta");
            Web_Node node3 = new Web_Node(n3, NodeTypes.Concepts, NodeLifetime.Permanent, 100);
            node3.Set_Label("Node Gamma");
            LRD_Core.Instance.AddExistingNode(node1);
            LRD_Core.Instance.AddExistingNode(node2);
            LRD_Core.Instance.AddExistingNode(node3);
            LRD_Core.Instance.AddConnection(n1, n2);
            LRD_Core.Instance.AddConnection(n2, n3);

            Task populatedRefresh = visualizerWin.RefreshGraphForTestingAsync();
            while (!populatedRefresh.IsCompleted)
            {
                DoEvents();
                Thread.Sleep(10);
            }
            populatedRefresh.GetAwaiter().GetResult();
            DoEvents();

            Check(visualizerWin.EmptyStateOverlay.Visibility == Visibility.Collapsed, "Populated graph hides EmptyStateOverlay.");
            Check(visualizerWin.ProgressOverlay.Visibility == Visibility.Collapsed, "ProgressOverlay is collapsed after layout.");
            Check(visualizerWin.Scene.HasGeometry, "VisualizerScene has geometry after refresh.");
            Check(visualizerWin.Scene.GeometryModelCount >= 2, $"VisualizerScene has model batches (count: {visualizerWin.Scene.GeometryModelCount}).");
            Check(visualizerWin.SummaryNodesText.Text.Contains("3"), $"Summary reports 3 nodes (actual: '{visualizerWin.SummaryNodesText.Text}').");
            Check(visualizerWin.SummaryLinksText.Text.Contains("2"), $"Summary reports 2 links (actual: '{visualizerWin.SummaryLinksText.Text}').");
            Check(visualizerWin.SummaryClassesText.Text.Contains("2"), $"Summary reports 2 node classes (actual: '{visualizerWin.SummaryClassesText.Text}').");
            Check(visualizerWin.SummaryTypesText.Text.Contains("1"), $"Summary reports 1 link type (actual: '{visualizerWin.SummaryTypesText.Text}').");
            Check(visualizerWin.LayoutTimeText.Text.Contains("iteration"), $"Layout summary reports iterations (actual: '{visualizerWin.LayoutTimeText.Text}').");
            Check(visualizerWin.SummaryRadiusText.Text.Contains("units"), $"Layout summary reports radius (actual: '{visualizerWin.SummaryRadiusText.Text}').");
            Check(visualizerWin.FrameAllButton.IsEnabled, "FrameAllButton is enabled on populated graph.");
            Check(visualizerWin.RecomputeButton.IsEnabled, "RecomputeButton is enabled on populated graph.");
            Check(visualizerWin.ResetViewButton.IsEnabled, "ResetViewButton is enabled on populated graph.");

            // 6. Toolbar actions
            MethodInfo? frameClick = typeof(VisualizerWindow).GetMethod("FrameAll_Button_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            frameClick?.Invoke(visualizerWin, new object[] { visualizerWin.FrameAllButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.StatusText.Text == "Framed all nodes.", "FrameAllButton sets status to 'Framed all nodes.'");

            MethodInfo? resetClick = typeof(VisualizerWindow).GetMethod("ResetView_Button_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            resetClick?.Invoke(visualizerWin, new object[] { visualizerWin.ResetViewButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.StatusText.Text == "View reset.", "ResetViewButton sets status to 'View reset.'");

            MethodInfo? recomputeClick = typeof(VisualizerWindow).GetMethod("Recompute_Button_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            recomputeClick?.Invoke(visualizerWin, new object[] { visualizerWin.RecomputeButton, new RoutedEventArgs() });
            WaitForCondition(() => !visualizerWin.IsLoading, 10000, "Recompute layout completion");
            Check(visualizerWin.StatusText.Text.Contains("Showing 3 nodes"), $"Recompute layout completes successfully: '{visualizerWin.StatusText.Text}'");

            // 7. Phase 4: Fly Navigation, Speed & Mouse Look
            Check(visualizerWin.Controller is not null, "FlyCameraController is attached to the window.");
            Check(visualizerWin.Controller!.SpeedMultiplier == 1.0, "Initial speed multiplier is 1.0.");
            visualizerWin.Controller.SpeedMultiplier = 2.0;
            Check(visualizerWin.Controller.SpeedMultiplier == 2.0, "SpeedMultiplier can be adjusted.");
            Check(visualizerWin.SpeedText.Text.Contains("2.0x"), $"SpeedText updates to reflect speed multiplier (actual: '{visualizerWin.SpeedText.Text}').");

            Vector3 initialPos = visualizerWin.Controller.FlyCamera.Position;
            visualizerWin.Controller.FlyCamera.Move(1.0, 0.0, 0.0, 10.0, 0.1);
            visualizerWin.Scene.SyncCameraFromFlyCamera();
            Check(visualizerWin.Controller.FlyCamera.Position != initialPos, "FlyCamera.Move translates the camera position.");

            double initialYaw = visualizerWin.Controller.FlyCamera.Yaw;
            double initialPitch = visualizerWin.Controller.FlyCamera.Pitch;
            visualizerWin.Controller.FlyCamera.Look(15.0, 10.0);
            visualizerWin.Scene.SyncCameraFromFlyCamera();
            Check(visualizerWin.Controller.FlyCamera.Yaw != initialYaw, "FlyCamera.Look updates Yaw.");
            Check(visualizerWin.Controller.FlyCamera.Pitch != initialPitch, "FlyCamera.Look updates Pitch.");

            // 8. Phase 4: Label Overlay & 'L' Key Toggle
            Check(visualizerWin.Labels.ShowLabels, "LabelOverlay ShowLabels is true by default.");
            MethodInfo? previewKey = typeof(VisualizerWindow).GetMethod("Window_PreviewKeyDown", BindingFlags.NonPublic | BindingFlags.Instance);

            KeyEventArgs lKeyEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.L)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            previewKey?.Invoke(visualizerWin, new object[] { visualizerWin, lKeyEvent });
            DoEvents();

            Check(!visualizerWin.Labels.ShowLabels, "'L' key toggles LabelOverlay ShowLabels to false.");
            Check(visualizerWin.ToggleLabelsButton.IsChecked == false, "'L' key updates ToggleLabelsButton.IsChecked to false.");

            previewKey?.Invoke(visualizerWin, new object[] { visualizerWin, lKeyEvent });
            DoEvents();

            Check(visualizerWin.Labels.ShowLabels, "'L' key toggles LabelOverlay ShowLabels back to true.");
            Check(visualizerWin.ToggleLabelsButton.IsChecked == true, "'L' key updates ToggleLabelsButton.IsChecked to true.");

            // 9. Phase 4: Ray-AABB Picking & Selection Card
            CameraView camView = visualizerWin.Scene.GetCameraView();
            Vector3 node0Pos = visualizerWin.Geometry!.Layout.Positions[0];
            Check(camView.TryProject(node0Pos, out Vector2 node0Screen, out _), "Node 0 projects to visible screen space.");

            int? hitPick = visualizerWin.PickNodeForTesting(new Point(node0Screen.X, node0Screen.Y));
            Check(hitPick == 0, $"Ray-AABB picking accurately hits Node 0 at ({node0Screen.X:F0}, {node0Screen.Y:F0}) (actual hit: {hitPick}).");

            int? missPick = visualizerWin.PickNodeForTesting(new Point(-1000, -1000));
            Check(missPick is null, "Ray-AABB picking misses when ray points outside scene.");

            // Select node 0
            visualizerWin.SelectNodeForTesting(0);
            DoEvents();

            Check(visualizerWin.SelectedIndex == 0, "Selected node index is set to 0.");
            Check(visualizerWin.SelectionCard.Visibility == Visibility.Visible, "SelectionCard is visible when a node is selected.");
            Check(visualizerWin.SelectedLabelText.Text.Contains("Node Alpha"), $"Selection card displays node label (actual: '{visualizerWin.SelectedLabelText.Text}').");
            Check(visualizerWin.SelectedIdText.Text.Contains(n1.ToString()), "Selection card displays node GUID.");
            Check(visualizerWin.SelectedTypeText.Text.Contains("Basic"), $"Selection card displays node type (actual: '{visualizerWin.SelectedTypeText.Text}').");
            Check(visualizerWin.Labels.SelectedNodeIndex == 0, "LabelOverlay reflects SelectedNodeIndex = 0.");

            // Center View button in SelectionCard
            MethodInfo? centerSelectionClick = typeof(VisualizerWindow).GetMethod("CenterSelection_Button_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            centerSelectionClick?.Invoke(visualizerWin, new object[] { visualizerWin.CenterSelectionButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.StatusText.Text == "Centered view on selected node.", "CenterSelectionButton centers view on selected node.");

            // 'F' key frames selection when one exists
            KeyEventArgs fKeyEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.F)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            previewKey?.Invoke(visualizerWin, new object[] { visualizerWin, fKeyEvent });
            DoEvents();
            Check(visualizerWin.StatusText.Text == "Framed selected node.", "'F' key frames selected node when selection exists.");

            // Hover node 1
            visualizerWin.HoverNodeForTesting(1);
            DoEvents();

            Check(visualizerWin.HoveredIndex == 1, "Hovered node index is set to 1.");
            Check(visualizerWin.StatusText.Text.Contains("Node Beta"), $"Status bar displays hovered node info (actual: '{visualizerWin.StatusText.Text}').");
            Check(visualizerWin.Labels.HoveredNodeIndex == 1, "LabelOverlay reflects HoveredNodeIndex = 1.");

            visualizerWin.HoverNodeForTesting(null);
            DoEvents();
            Check(visualizerWin.HoveredIndex is null, "Hovered index cleared.");

            // Clear selection via Clear button
            MethodInfo? clearSelectionClick = typeof(VisualizerWindow).GetMethod("ClearSelection_Button_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            clearSelectionClick?.Invoke(visualizerWin, new object[] { visualizerWin.ClearSelectionButton, new RoutedEventArgs() });
            DoEvents();

            Check(visualizerWin.SelectedIndex is null, "ClearSelectionButton clears the selected node.");
            Check(visualizerWin.SelectionCard.Visibility == Visibility.Collapsed, "SelectionCard collapses when selection is cleared.");

            // 'F' key frames all nodes when no selection exists
            previewKey?.Invoke(visualizerWin, new object[] { visualizerWin, fKeyEvent });
            DoEvents();
            Check(visualizerWin.StatusText.Text == "Framed all nodes.", "'F' key frames all nodes when no selection exists.");

            // 10. Phase 5: Three-Tab Side Panel, Colors, Swatches, Fallbacks & Reset
            Check(visualizerWin.SideTabControl.Items.Count == 3, "Side panel has 3 tabs (Node Colors, Link Colors, View).");
            Check(visualizerWin.NodeColorsPanel.Children.Count >= 2, $"Node colors panel has rows for node types (count: {visualizerWin.NodeColorsPanel.Children.Count}).");
            Check(visualizerWin.LinkColorsPanel.Children.Count >= 1, $"Link colors panel has rows for forward link types (count: {visualizerWin.LinkColorsPanel.Children.Count}).");
            Check(visualizerWin.ClassColorsPanel.Children.Count >= 8, $"Class colors panel has rows for link classes (count: {visualizerWin.ClassColorsPanel.Children.Count}).");

            // Verify visibility toggle checkboxes in Node Colors and Link Colors
            DockPanel firstNodeRow = (DockPanel)visualizerWin.NodeColorsPanel.Children[0];
            CheckBox? nodeCheckBox = firstNodeRow.Children.OfType<CheckBox>().FirstOrDefault();
            Check(nodeCheckBox is not null, "Node color row contains visibility CheckBox.");
            Check(nodeCheckBox!.IsChecked == true, "Node visibility CheckBox is checked by default.");

            DockPanel firstLinkRow = (DockPanel)visualizerWin.LinkColorsPanel.Children[0];
            CheckBox? linkCheckBox = firstLinkRow.Children.OfType<CheckBox>().FirstOrDefault();
            Check(linkCheckBox is not null, "Link color row contains visibility CheckBox.");
            Check(linkCheckBox!.IsChecked == true, "Link visibility CheckBox is checked by default.");

            // Toggle node visibility off
            nodeCheckBox.IsChecked = false;
            DoEvents();
            Check(!visualizerWin.Scene.IsNodeTypeVisible(NodeTypes.Basics.Id), "Unchecking node visibility hides node type in scene.");

            // Toggle node visibility back on
            nodeCheckBox.IsChecked = true;
            DoEvents();
            Check(visualizerWin.Scene.IsNodeTypeVisible(NodeTypes.Basics.Id), "Re-checking node visibility shows node type in scene.");

            // Live color override and material update
            RgbColor overrideColor = new RgbColor(220, 60, 60); // Red
            visualizerWin.ColorsMap.SetNodeColor(NodeTypes.Concepts.Id, overrideColor);
            DoEvents();
            Check(visualizerWin.ColorsMap.GetNodeColor(NodeTypes.Concepts.Id) == overrideColor, "Node color override applied to GraphColorMap.");

            visualizerWin.ColorsMap.SetLinkColor(ForwardLinkTypes.basic.Id, overrideColor);
            DoEvents();
            Check(visualizerWin.ColorsMap.GetLinkColor(ForwardLinkTypes.basic.Id, 0) == overrideColor, "Link color override applied to GraphColorMap.");

            visualizerWin.ColorsMap.SetClassColor(LinkClass.identities.Id, overrideColor);
            DoEvents();
            Check(visualizerWin.ColorsMap.GetClassColor(LinkClass.identities.Id) == overrideColor, "Class fallback override applied to GraphColorMap.");

            // Reset color defaults
            MethodInfo? resetNodeColors = typeof(VisualizerWindow).GetMethod("ResetNodeColors_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            resetNodeColors?.Invoke(visualizerWin, new object[] { visualizerWin.ResetNodeColorsButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.ColorsMap.NodeColors.Count == 0, "ResetNodeColors clears all node color overrides.");

            MethodInfo? resetLinkColors = typeof(VisualizerWindow).GetMethod("ResetLinkColors_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            resetLinkColors?.Invoke(visualizerWin, new object[] { visualizerWin.ResetLinkColorsButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.ColorsMap.LinkColors.Count == 0 && visualizerWin.ColorsMap.ClassColors.Count == 0, "ResetLinkColors clears all link and class overrides.");

            // 11. Phase 5: View Sliders, Settings & Persistence
            visualizerWin.SpreadSlider.Value = 7.5;
            visualizerWin.CubeSizeSlider.Value = 1.2;
            visualizerWin.LineWidthSlider.Value = 0.2;
            visualizerWin.MaxLabelsSlider.Value = 500;
            visualizerWin.MaxNodesSlider.Value = 30000;
            visualizerWin.MaxLinksSlider.Value = 75000;
            visualizerWin.NearPlaneSlider.Value = 0.25;
            DoEvents();

            visualizerWin.MaxNodesInputBox.Text = "31,000";
            MethodInfo? maxNodesInput = typeof(VisualizerWindow).GetMethod("MaxNodesInputBox_LostFocus", BindingFlags.NonPublic | BindingFlags.Instance);
            maxNodesInput?.Invoke(visualizerWin, new object[] { visualizerWin.MaxNodesInputBox, new RoutedEventArgs() });
            visualizerWin.MaxLinksInputBox.Text = "76,000";
            MethodInfo? maxLinksInput = typeof(VisualizerWindow).GetMethod("MaxLinksInputBox_LostFocus", BindingFlags.NonPublic | BindingFlags.Instance);
            maxLinksInput?.Invoke(visualizerWin, new object[] { visualizerWin.MaxLinksInputBox, new RoutedEventArgs() });
            DoEvents();

            Check(visualizerWin.Options.Spread == 7.5, "SpreadSlider updates options.Spread.");
            Check(visualizerWin.Options.CubeSize == 1.2, "CubeSizeSlider updates options.CubeSize.");
            Check(visualizerWin.Options.LineWidth == 0.2, "LineWidthSlider updates options.LineWidth.");
            Check(visualizerWin.Options.MaxLabels == 500, "MaxLabelsSlider updates options.MaxLabels.");
            Check(visualizerWin.Options.MaxNodes == 31000, "MaxNodes numeric input updates options.MaxNodes.");
            Check(visualizerWin.Options.MaxLinks == 76000, "MaxLinks numeric input updates options.MaxLinks.");
            Check(visualizerWin.Options.NearPlaneDistance == 0.25, "NearPlaneSlider updates options.NearPlaneDistance.");
            Check(visualizerWin.Scene.NearPlaneDistance == 0.25, "NearPlaneSlider updates scene.NearPlaneDistance.");
            Check(visualizerWin.Labels.MaxLabels == 500, "MaxLabels updates LabelOverlay.MaxLabels.");

            MethodInfo? applySettings = typeof(VisualizerWindow).GetMethod("ApplySettings_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            applySettings?.Invoke(visualizerWin, new object[] { visualizerWin.ApplySettingsButton, new RoutedEventArgs() });
            WaitForCondition(() => !visualizerWin.IsLoading, 10000, "Apply layout with raised graph caps");
            Check(visualizerWin.Snapshot?.Nodes.Count == 3, "Apply Layout refreshes graph snapshot after changing caps.");

            visualizerWin.SaveSettingsForTesting();
            string settingsPath = VisualizerSettings.GetDefaultPath();
            Check(File.Exists(settingsPath), $"Visualizer settings saved to JSON file: {settingsPath}");

            string settingsJson = File.ReadAllText(settingsPath);
            Check(settingsJson.Contains("\"spread\": 7.5"), "Saved settings JSON contains updated spread (7.5).");
            Check(settingsJson.Contains("\"cubeSize\": 1.2"), "Saved settings JSON contains updated cubeSize (1.2).");
            Check(settingsJson.Contains("\"maxNodes\": 31000"), "Saved settings JSON contains updated maxNodes (31000).");
            Check(settingsJson.Contains("\"maxLinks\": 76000"), "Saved settings JSON contains updated maxLinks (76000).");

            // Mouse Axis Inversion controls
            Check(visualizerWin.InvertMouseXCheckBox.IsChecked == true, "InvertMouseXCheckBox is checked by default (horizontal inverted).");
            Check(visualizerWin.InvertMouseYCheckBox.IsChecked == false, "InvertMouseYCheckBox is unchecked by default.");
            Check(visualizerWin.Controller!.InvertMouseX == true, "Controller InvertMouseX is true.");
            Check(visualizerWin.Controller!.InvertMouseY == false, "Controller InvertMouseY is false.");

            visualizerWin.InvertMouseXCheckBox.IsChecked = false;
            MethodInfo? invertXClick = typeof(VisualizerWindow).GetMethod("InvertMouseX_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            invertXClick?.Invoke(visualizerWin, new object[] { visualizerWin.InvertMouseXCheckBox, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.Options.InvertMouseX == false, "InvertMouseX_Click sets options.InvertMouseX to false.");
            Check(visualizerWin.Controller.InvertMouseX == false, "InvertMouseX_Click updates Controller.InvertMouseX to false.");

            visualizerWin.InvertMouseYCheckBox.IsChecked = true;
            MethodInfo? invertYClick = typeof(VisualizerWindow).GetMethod("InvertMouseY_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            invertYClick?.Invoke(visualizerWin, new object[] { visualizerWin.InvertMouseYCheckBox, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.Options.InvertMouseY == true, "InvertMouseY_Click sets options.InvertMouseY to true.");
            Check(visualizerWin.Controller.InvertMouseY == true, "InvertMouseY_Click updates Controller.InvertMouseY to true.");

            // Reset view settings
            MethodInfo? resetViewSettings = typeof(VisualizerWindow).GetMethod("ResetViewSettings_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            resetViewSettings?.Invoke(visualizerWin, new object[] { visualizerWin.ResetViewSettingsButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.Options.Spread == 6.0, "ResetViewSettings restores default Spread (6.0).");
            Check(visualizerWin.Options.CubeSize == 1.0, "ResetViewSettings restores default CubeSize (1.0).");
            Check(visualizerWin.Options.MaxNodes == VisualizerOptions.DefaultMaxNodes, "ResetViewSettings restores default node cap.");
            Check(visualizerWin.Options.MaxLinks == VisualizerOptions.DefaultMaxLinks, "ResetViewSettings restores default link cap.");
            Check(visualizerWin.Options.MaxIterations == 300, "ResetViewSettings keeps max layout iterations at 300.");

            // 12. Phase 5: Help Overlay
            Check(visualizerWin.HelpOverlay.Visibility == Visibility.Collapsed, "HelpOverlay is collapsed by default.");
            MethodInfo? helpClick = typeof(VisualizerWindow).GetMethod("Help_Button_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            helpClick?.Invoke(visualizerWin, new object[] { visualizerWin.HelpButton, new RoutedEventArgs() });
            DoEvents();
            Check(visualizerWin.HelpOverlay.Visibility == Visibility.Visible, "Help button opens HelpOverlay.");

            // Esc while HelpOverlay is visible closes overlay without closing the window
            KeyEventArgs escOverlayEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            previewKey?.Invoke(visualizerWin, new object[] { visualizerWin, escOverlayEvent });
            DoEvents();
            Check(visualizerWin.HelpOverlay.Visibility == Visibility.Collapsed, "Esc closes HelpOverlay while keeping VisualizerWindow open.");
            Check(!visualizerWin.IsClosed, "VisualizerWindow remains open after dismissing HelpOverlay.");

            // 13. Esc closes window
            KeyEventArgs escEvent = new KeyEventArgs(
                Keyboard.PrimaryDevice,
                new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero),
                0,
                Key.Escape)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            previewKey?.Invoke(visualizerWin, new object[] { visualizerWin, escEvent });
            DoEvents();

            Check(visualizerWin!.IsClosed, "Esc key triggers window Close (IsClosed == true).");
            Check(!visualizerWin.Scene.HasGeometry, "Closed window clears scene geometry.");
            Check(winField?.GetValue(mainWindow) is null, "Closed window nulls MainWindow's instance reference.");

            // 8. Toolbar Close (Esc) button on fresh window
            visualizerClick?.Invoke(mainWindow, new object[] { mainWindow.VisualizerButton, new RoutedEventArgs() });
            DoEvents();
            VisualizerWindow? freshWin = winField?.GetValue(mainWindow) as VisualizerWindow;
            Check(freshWin is not null, "New VisualizerWindow opens cleanly after previous window closed.");

            MethodInfo? closeClick = typeof(VisualizerWindow).GetMethod("Close_Button_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            closeClick?.Invoke(freshWin, new object[] { freshWin!.CloseButton, new RoutedEventArgs() });
            DoEvents();
            Check(freshWin!.IsClosed, "Toolbar Close (Esc) button closes the window.");
            Check(winField?.GetValue(mainWindow) is null, "Toolbar close nulls MainWindow's instance reference.");

            mainWindow.Close();
            DoEvents();
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
