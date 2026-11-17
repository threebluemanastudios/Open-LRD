using System;
using System.Diagnostics;
using System.Globalization;
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
using System.Windows.Media;
using System.Windows.Threading;
using OPEN_LRD;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Ontology;
using OPEN_LRD.Persistence;
using OPEN_LRD.Visualization;
using OPEN_LRD.Visualizer;

namespace OPEN_LRD.ManualUiTests
{
    /// <summary>Runs end-to-end WPF checks for Tools, the NLP Workbench, and the Visualizer.</summary>
    public static class Program
    {
        private static int passedChecks = 0;
        private static int failedChecks = 0;
        private static string fixtureDir = "";

        /// <summary>Creates the WPF application context and executes the manual UI verification suite.</summary>
        [STAThread]
        public static void Main()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("OPEN-LRD MANUAL UI VERIFICATION RUNNER");
            Console.WriteLine("TOOLS, NLP WORKBENCH, TEMPORAL EVENTS, AND 3D VISUALIZER");
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
                RunSection1_DirectTextIngestion();
                RunSection2_InputValidation();
                RunSection3_DryRun();
                (Guid largeSourceId, Guid largeConvId) = RunSection4_RealImportAutomaticSource();
                RunSection5_ProvidedSourceAndTopicGuids(largeSourceId);
                RunSection6_FailureContinuation();
                RunSection7_SafetyLimits();
                RunSection8_CancellationAndBusyGating();
                RunSection9_ConversationContinuityAndEncoding();
                RunSection13_DataFileImportUi();
                RunSection12_NlpWorkbenchCandidateSynthesis();
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

        /// <summary>Records and prints a pass or fail result for a single manual UI check.</summary>
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

        /// <summary>Pumps pending WPF dispatcher messages on the current STA thread.</summary>
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

        /// <summary>Renders the graph label overlay into a temporary drawing visual for test verification.</summary>
        private static void RenderLabelsForTesting(GraphLabelOverlay labels)
        {
            DrawingVisual visual = new DrawingVisual();
            using DrawingContext drawingContext = visual.RenderOpen();
            labels.RenderForTesting(drawingContext);
        }

        /// <summary>Pumps the WPF dispatcher until the specified condition succeeds or times out.</summary>
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

        /// <summary>Invokes the Tools window's file-import click handler and pumps dispatcher events.</summary>
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

        /// <summary>Invokes the Tools window's direct-text ingestion click handler and pumps dispatcher events.</summary>
        private static void ClickDirectIngest(Tools window)
        {
            MethodInfo? method = typeof(Tools).GetMethod(
                "DirectTextIngest_Button_Click",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method is null)
                throw new InvalidOperationException("Could not find DirectTextIngest_Button_Click");

            method.Invoke(window, new object[] { window.DirectTextIngestButton, new RoutedEventArgs() });
            DoEvents();
        }

        /// <summary>Selects the requested topic mode item in the Tools window dropdown.</summary>
        private static void SelectTopicMode(Tools window, string mode)
        {
            ComboBoxItem? item = window.TopicModeComboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Tag?.ToString(),
                    mode,
                    StringComparison.Ordinal));
            if (item is null)
                throw new InvalidOperationException($"Topic mode '{mode}' was not found.");

            window.TopicModeComboBox.SelectedItem = item;
            DoEvents();
        }

        /// <summary>Invokes the Tools window's file-import cancel click handler and pumps dispatcher events.</summary>
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

        /// <summary>Waits until an asynchronous file import in the Tools window completes.</summary>
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
        /// <summary>Verifies test fixture files exist and initializes the core graph.</summary>
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
        /// <summary>Verifies default control visibility, tab ordering, and initial field values in the Tools window.</summary>
        private static void RunSection1_LayoutAndDefaults()
        {
            Console.WriteLine("\n--- SECTION 1: WINDOW LAYOUT AND DEFAULTS ---");

            Tools window = new Tools();
            DoEvents();

            Check(window.ExportLogsButton is not null, "Export section present (ExportLogsButton).");
            Check(window.ImportCoreLogButton is not null, "Import section present (ImportCoreLogButton).");
            Check(window.SeedOntologyButton is not null, "Ontology section present (SeedOntologyButton).");
            Check(window.RunMaintenanceButton is not null, "Graph Maintenance section present (RunMaintenanceButton).");
            Check(window.MaintenanceMergeNodesCheckBox.IsChecked == true, "Node consolidation is selected by default in the explicit maintenance panel.");
            Check(window.MaintenanceMetadataCheckBox.IsChecked == true, "Node metadata materialization is selected by default in the explicit maintenance panel.");
            Check(window.MaintenanceCompositionCheckBox.IsChecked == true, "Composition is selected by default in the explicit maintenance panel.");
            Check(window.MaintenancePatternCheckBox.IsChecked == true, "Pattern abstraction is selected by default in the explicit maintenance panel.");
            Check(window.MaintenanceElevationCheckBox.IsChecked == true, "Relation concept elevation is selected by default in the explicit maintenance panel.");
            Check(window.MaintenancePruningCheckBox.IsChecked == true, "Superseded-link pruning is selected by default in the explicit maintenance panel.");
            Check(window.StatusBox is not null, "Status box present (StatusBox).");
            string[] tabHeaders = window.ToolsTabControl.Items
                .Cast<TabItem>()
                .Select(item => item.Header?.ToString() ?? string.Empty)
                .ToArray();
            Check(tabHeaders.SequenceEqual(new[] { "Logs", "Ingestion", "Ontology", "Maintenance", "Data Files" }),
                $"Tools tabs appear in the planned order (actual: {string.Join(", ", tabHeaders)}).");
            Check(window.DirectTextInput.MaxLength == 1000, "Direct text input is limited to 1,000 characters.");
            Check(window.TopicModeComboBox.SelectedItem is ComboBoxItem defaultTopic
                && defaultTopic.Tag?.ToString() == "Base",
                "Default topic mode is base/general/none.");
            string[] topicModes = window.TopicModeComboBox.Items
                .Cast<ComboBoxItem>()
                .Select(item => item.Tag?.ToString() ?? string.Empty)
                .ToArray();
            Check(topicModes.SequenceEqual(new[]
                {
                    "Base", "Guid", "Sense", "Label", "NodeType", "Alias", "Category",
                    "Field", "Taxon", "Reality", "CurrentTopic", "Source"
                }),
                $"Topic dropdown contains separate single-select modes (actual: {string.Join(", ", topicModes)}).");
            TabItem? dataFilesTab = window.ToolsTabControl.Items
                .Cast<TabItem>()
                .FirstOrDefault(item => item.Header?.ToString() == "Data Files");
            Check(dataFilesTab?.Content is ScrollViewer,
                "Data Files tab presents its file-ingestion controls in a scrollable panel.");
            Check(window.DataFileFormatComboBox.SelectedIndex == 0
                && window.DataFileModeComboBox.SelectedIndex == 0,
                "Data Files defaults to Auto-detect and Structured Data.");
            Check(window.DataFileFormatComboBox.Items.Count == 5
                && window.DataFileModeComboBox.Items.Count == 2,
                "Data Files offers the supported formats and ingestion modes.");

            Check(window.TextImportPathBox.Text == "", "Default File path is empty.");
            Check(window.TextImportPathBox.IsReadOnly, "File path box is read-only.");

            Check(window.TextImportSourceBox.Text == "", "Default Source ID is blank.");
            Check(window.TextImportTopicBox.Text == "", "Default Topic ID is blank.");

            Check(window.TextImportDryRunCheckBox.IsChecked == false, "Default Dry run is unchecked.");
            Check(window.TextImportMaxMegabytesBox.Text == "500", "Default Max file MB is 500.");
            Check(window.TextImportMaxChunksBox.Text == "10000", "Default Max chunks is 10000.");

            Check(window.TextImportButton.IsEnabled, "Default Import Text File button is enabled.");
            Check(!window.TextImportCancelButton.IsEnabled, "Default Cancel button is disabled.");

            Check(window.TextImportProgressBar.Value == 0, "Default Progress bar value is 0.");
            Check(!window.TextImportProgressBar.IsIndeterminate, "Default Progress bar is determinate.");

            Check(window.TextImportStatusText.Text == "Ready. 0 records.", "Default status line is 'Ready. 0 records.'");

            Check(window.MinWidth == 760, $"Window MinWidth is 760 (actual: {window.MinWidth})");
            Check(window.MinHeight == 700, $"Window MinHeight is 700 (actual: {window.MinHeight})");

            FieldInfo? filterField = typeof(Tools).GetField("DataFileFilter", BindingFlags.NonPublic | BindingFlags.Static);
            string? filter = filterField?.GetValue(null) as string;
            Check(filter == "Supported data files (*.txt;*.md;*.log;*.csv;*.json;*.xml)|*.txt;*.md;*.log;*.csv;*.json;*.xml|All files (*.*)|*.*",
                $"Browse dialog filter matches specification: '{filter}'");
        }

        /// <summary>Verifies direct sentence ingestion, dry-run preview, user Source creation, and all topic selection modes.</summary>
        private static void RunSection1_DirectTextIngestion()
        {
            Console.WriteLine("\n--- SECTION 1A: DIRECT TEXT INGESTION AND TOPIC SELECTION ---");

            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            string twoSentences = "Alice works at Acme. Bob sees Carol.";
            int initialNodeCount = core.GetDictionary()?.Count ?? 0;
            int initialConversationCount = core.GetConversations()?.Count ?? 0;
            window.DirectTextInput.Text = twoSentences;
            window.DirectTextDryRunCheckBox.IsChecked = true;
            ClickDirectIngest(window);
            Check(window.DirectTextStatusText.Text.Contains("2 sentence(s)", StringComparison.Ordinal)
                && window.DirectTextStatusText.Text.Contains("supported", StringComparison.OrdinalIgnoreCase),
                $"Direct dry run reports two sentence chunks (actual: '{window.DirectTextStatusText.Text}').");
            Check((core.GetDictionary()?.Count ?? 0) == initialNodeCount,
                "Direct dry run does not mutate the graph or create the user Source node.");
            Check((core.GetConversations()?.Count ?? 0) == initialConversationCount,
                "Direct dry run removes its temporary conversation after preview.");

            window.DirectTextInput.Text = "Alice works at Acme. She sees Bob.";
            window.DirectTextDryRunCheckBox.IsChecked = false;
            ClickDirectIngest(window);
            Check(window.DirectTextStatusText.Text.Contains("2 sentence(s)", StringComparison.Ordinal)
                && window.DirectTextStatusText.Text.Contains("2 ingested", StringComparison.Ordinal),
                $"Direct ingestion processes two sentences (actual: '{window.DirectTextStatusText.Text}').");
            Web_Node? userSource = core.FindByLabel("user", NodeTypes.Sources).SingleOrDefault();
            Check(userSource is not null, "Direct text creates/reuses the user Source node.");
            Check(userSource?.TrustRating == 0.5, "The user Source node has a persisted 50% trust rating.");
            Check(userSource?.Get_DataPointer() is null, "Manually entered text has no fabricated file pointer.");

            ConversationContext firstConversation = GetDirectConversation(window);
            Check(firstConversation.Provenance.Select(entry => entry.TurnIndex)
                    .Distinct().OrderBy(index => index).SequenceEqual(new[] { 1, 2 }),
                $"The sentences are ingested on separate turns in one conversation (turns: {string.Join(", ", firstConversation.Provenance.Select(entry => entry.TurnIndex).Distinct())}).");
            Check(firstConversation.Provenance.Select(entry => entry.TrustSourceNodeID).Distinct().SequenceEqual(new[] { userSource?.ID }),
                "Both sentence chunks use the same user Source node.");
            Check(firstConversation.Provenance.All(entry => entry.TopicNodeID is null),
                "The default base/general/none topic maps to no TopicNodeID.");

            OntologyTypes.EnsureRegistered();
            Guid guidTopic = AddTopicNode(core, NodeTypes.Concepts, "GuidTopic");
            Guid labelTopic = AddTopicNode(core, NodeTypes.Entities, "LabelTopic");
            Guid aliasTopic = AddTopicNode(core, NodeTypes.Entities, "AliasTopic", "topic-alias");
            Guid typeTopic = AddTopicNode(core, NodeTypes.Concepts, "TypeTopic");
            Guid senseTopic = AddTopicNode(core, OntologyTypes.Senses, "SenseTopic");
            Guid categoryTopic = AddTopicNode(core, OntologyTypes.Categories, "CategoryTopic");
            Guid fieldTopic = AddTopicNode(core, OntologyTypes.Fields, "FieldTopic");
            Guid taxonTopic = AddTopicNode(core, OntologyTypes.Taxa, "TaxonTopic");
            Guid realityTopic = AddTopicNode(core, OntologyTypes.Realities, "RealityTopic");

            // Executes one direct-text ingestion check under the specified topic selection mode.
            void RunTopicCase(
                string mode,
                string subjectName,
                Guid? expectedTopicID,
                string? value = null,
                NodeTypes? typeFilter = null,
                Guid? pickerID = null)
            {
                SelectTopicMode(window, mode);
                if (mode is "Guid" or "Label" or "Alias")
                    window.TextImportTopicBox.Text = value ?? string.Empty;
                if (mode == "NodeType")
                {
                    window.TopicNodeTypeComboBox.SelectedItem = typeFilter;
                    DoEvents();
                }
                if (pickerID.HasValue)
                {
                    Web_Node? topicChoice = window.TopicNodePickerComboBox.Items
                        .Cast<Web_Node>()
                        .FirstOrDefault(node => node.ID == pickerID.Value);
                    Check(topicChoice is not null,
                        $"Topic mode '{mode}' picker contains the requested graph node.");
                    window.TopicNodePickerComboBox.SelectedItem = topicChoice;
                }

                window.DirectTextInput.Text = subjectName + " works at Acme.";
                ClickDirectIngest(window);
                Check(window.DirectTextStatusText.Text.Contains("1 ingested", StringComparison.Ordinal),
                    $"Topic mode '{mode}' ingests its direct-text sentence (actual: '{window.DirectTextStatusText.Text}').");
                ConversationContext conversation = GetDirectConversation(window);
                Guid? actualTopic = conversation.Provenance.LastOrDefault()?.TopicNodeID;
                Check(actualTopic == expectedTopicID,
                    $"Topic mode '{mode}' resolves to '{expectedTopicID}' (actual: '{actualTopic}').");
            }

            RunTopicCase("CurrentTopic", "Dora", null);
            RunTopicCase("Guid", "Elena", guidTopic, guidTopic.ToString());
            RunTopicCase("CurrentTopic", "Fiona", guidTopic);
            RunTopicCase("Label", "Grace", labelTopic, "LabelTopic");
            RunTopicCase("Alias", "Heidi", aliasTopic, "topic-alias");
            RunTopicCase("NodeType", "Judy", typeTopic, typeFilter: NodeTypes.Concepts, pickerID: typeTopic);
            RunTopicCase("Sense", "Mallory", senseTopic, pickerID: senseTopic);
            RunTopicCase("Category", "Oscar", categoryTopic, pickerID: categoryTopic);
            RunTopicCase("Field", "Peggy", fieldTopic, pickerID: fieldTopic);
            RunTopicCase("Taxon", "Sybil", taxonTopic, pickerID: taxonTopic);
            RunTopicCase("Reality", "Trent", realityTopic, pickerID: realityTopic);
            RunTopicCase("Source", "Victor", userSource!.ID);
            RunTopicCase("Base", "Wendy", null);

            window.DirectTextInput.Text = new string('x', 1001);
            int beforeOverLimit = core.GetDictionary()?.Count ?? 0;
            ClickDirectIngest(window);
            Check(window.DirectTextStatusText.Text.Contains("1,000 characters", StringComparison.Ordinal),
                $"Direct text rejects content above the character limit (actual: '{window.DirectTextStatusText.Text}').");
            Check((core.GetDictionary()?.Count ?? 0) == beforeOverLimit,
                "Over-limit direct text does not mutate the graph.");

            window.Close();
            DoEvents();

            // Creates and adds a permanent topic node fixture with an optional alias.
            static Guid AddTopicNode(LRD_Core core, NodeTypes type, string label, string? alias = null)
            {
                Web_Node node = new Web_Node(Guid.NewGuid(), type, NodeLifetime.Permanent, 0);
                node.Set_Label(label);
                if (alias is not null)
                    node.Add_Alias(alias);
                Check(core.AddExistingNode(node), $"Topic node '{label}' should be added.");
                return node.ID;
            }

            // Extracts and resolves the conversation context reported by a direct-text ingestion run.
            static ConversationContext GetDirectConversation(Tools window)
            {
                Match match = Regex.Match(
                    window.StatusBox.Text,
                    @"Conversation: (?<id>[0-9a-fA-F-]{36})");
                if (!match.Success || !Guid.TryParse(match.Groups["id"].Value, out Guid conversationID))
                    throw new InvalidOperationException("Direct ingestion report did not include a Conversation ID.");
                return LRD_Core.Instance.GetConversation(conversationID);
            }
        }

        // ================================================================================
        // 2. INPUT VALIDATION (NO IMPORT MAY START)
        // ================================================================================
        /// <summary>Verifies preflight input validation for file paths, GUIDs, and numeric limits in the Tools window.</summary>
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
            SelectTopicMode(window, "Guid");
            window.TextImportTopicBox.Text = "not-a-guid";
            ClickImport(window);
            Check(window.TextImportStatusText.Text == "Text import failed: Enter a non-empty Topic GUID.",
                $"Invalid Topic ID: status reports '{window.TextImportStatusText.Text}'");
            window.TextImportTopicBox.Text = "";
            SelectTopicMode(window, "Base");

            // 6. Max file MB = 0, then -5, then "abc"
            string[] badMb = { "0", "-5", "abc" };
            foreach (string val in badMb)
            {
                window.TextImportMaxMegabytesBox.Text = val;
                ClickImport(window);
                Check(window.TextImportStatusText.Text == "Max file MB must be a positive whole number.",
                    $"Max file MB = '{val}': status reports '{window.TextImportStatusText.Text}'");
            }
            window.TextImportMaxMegabytesBox.Text = "500";

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
            window.TextImportMaxMegabytesBox.Text = "500";

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
        /// <summary>Verifies that a text-file dry run reports chunk counts without mutating the graph.</summary>
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
        /// <summary>Verifies full text-file import, automatic Source node creation, and Source reuse on re-import.</summary>
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
        /// <summary>Verifies explicit Source and Topic GUID validation and Source-as-topic resolution.</summary>
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
            Check(window.TextImportStatusText.Text == "Text import failed: The selected Source node points to a different file.",
                "A provided Source ID that points to another file is rejected.");

            // Verify no duplicate "Source: small-valid.txt" node was created
            var dict = LRD_Core.Instance.GetDictionary()!;
            bool hasSmallSource = dict.Values.Any(n => n.Label == "Source: small-valid.txt");
            Check(!hasSmallSource, "Provided source suppresses automatic 'Source: small-valid.txt' creation.");

            // 2. Provided Topic ID (valid existing node)
            Guid topicId = existingSourceId; // use existing node as topic
            window.TextImportSourceBox.Text = "";
            SelectTopicMode(window, "Guid");
            window.TextImportTopicBox.Text = topicId.ToString();

            ClickImport(window);
            WaitForImportToFinish(window);

            Check(window.StatusBox.Text.Contains("Text import complete."), "Import with provided Topic ID succeeds.");

            // 3. Unknown Source ID
            Guid unknownSource = Guid.Parse("11111111-1111-1111-1111-111111111111");
            window.TextImportSourceBox.Text = unknownSource.ToString();
            SelectTopicMode(window, "Base");
            window.TextImportTopicBox.Text = "";

            ClickImport(window);
            WaitForImportToFinish(window);

            Check(window.TextImportStatusText.Text == $"Text import failed: Source node '{unknownSource}' was not found.",
                $"Unknown Source ID reports expected error: '{window.TextImportStatusText.Text}'");

            // 4. Unknown Topic ID
            Guid unknownTopic = Guid.Parse("22222222-2222-2222-2222-222222222222");
            window.TextImportSourceBox.Text = "";
            SelectTopicMode(window, "Guid");
            window.TextImportTopicBox.Text = unknownTopic.ToString();

            ClickImport(window);
            WaitForImportToFinish(window);

            Check(window.TextImportStatusText.Text == $"Text import failed: Topic node '{unknownTopic}' was not found.",
                $"Unknown Topic ID reports expected error: '{window.TextImportStatusText.Text}'");

            // 5. Use the automatically attributed file Source as the topic.
            window.TextImportSourceBox.Text = "";
            SelectTopicMode(window, "Source");
            string sourceTopicPath = Path.Combine(
                Path.GetTempPath(),
                "open-lrd-source-topic-" + Guid.NewGuid().ToString("N") + ".txt");
            File.Copy(smallPath, sourceTopicPath);
            window.TextImportPathBox.Text = sourceTopicPath;
            try
            {
                ClickImport(window);
                WaitForImportToFinish(window);
                Match sourceMatch = Regex.Match(window.StatusBox.Text, @"Source node:\s+([a-fA-F0-9\-]{36})");
                Match conversationMatch = Regex.Match(window.StatusBox.Text, @"Conversation:\s+([a-fA-F0-9\-]{36})");
                Check(sourceMatch.Success && conversationMatch.Success,
                    "File Source topic import reports its Source and Conversation IDs.");
                if (sourceMatch.Success && conversationMatch.Success)
                {
                    Guid fileSourceID = Guid.Parse(sourceMatch.Groups[1].Value);
                    Guid conversationID = Guid.Parse(conversationMatch.Groups[1].Value);
                    Check(LRD_Core.Instance.GetConversation(conversationID).Provenance
                            .All(entry => entry.TopicNodeID == fileSourceID),
                        "Source topic mode uses the file's automatically created Source node as topic.");
                }
            }
            finally
            {
                File.Delete(sourceTopicPath);
            }
        }

        // ================================================================================
        // 6. FAILURE CONTINUATION (mixed.txt)
        // ================================================================================
        /// <summary>Verifies that unsupported chunks in a text file do not abort subsequent valid chunks.</summary>
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
        /// <summary>Verifies oversize file rejection, binary file rejection, and chunk-count truncation in the Tools UI.</summary>
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
            window.TextImportMaxMegabytesBox.Text = "500";
            window.DataFileFormatComboBox.SelectedIndex = 1;

            ClickImport(window);
            WaitForImportToFinish(window);

            string binStatus = window.TextImportStatusText.Text;
            Console.WriteLine($"  Binary status: {binStatus}");
            Check(binStatus == "Text import failed: The file appears to be binary and cannot be imported as text.",
                $"Binary rejection error matches: '{binStatus}'");
            Check((LRD_Core.Instance.GetDictionary()?.Count ?? 0) == initialNodes, "No nodes created on binary rejection.");
            window.DataFileFormatComboBox.SelectedIndex = 0;

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
            window.TextImportMaxMegabytesBox.Text = "500";
            window.TextImportMaxChunksBox.Text = "10000";
            Check(window.TextImportMaxMegabytesBox.Text == "500" && window.TextImportMaxChunksBox.Text == "10000",
                "Restored default limits (500 MiB, 10000 chunks).");
        }

        // ================================================================================
        // 8. CANCELLATION AND BUSY-STATE GATING
        // ================================================================================
        /// <summary>Verifies control disabling during active imports, cooperative cancellation, and post-cancel recovery.</summary>
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
            Check(!window.DirectTextInput.IsEnabled && !window.DirectTextIngestButton.IsEnabled,
                "Busy state: Direct text input and Ingest button are disabled.");
            Check(!window.TopicModeComboBox.IsEnabled,
                "Busy state: shared Topic selector is disabled.");
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
        /// <summary>Verifies cross-chunk pronoun resolution and UTF-8 BOM handling during file import.</summary>
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
        /// <summary>Verifies format detection, mode gating, dry runs, and high-trust checksum skips in the Data Files UI.</summary>
        private static void RunSection13_DataFileImportUi()
        {
            Console.WriteLine("\n--- SECTION 13: DATA FILE IMPORT UI ---");
            LRD_Core.Instance.InitializeCore();
            Tools window = new Tools();
            DoEvents();

            string csvPath = Path.Combine(Path.GetTempPath(), "open-lrd-data-file-ui.csv");
            File.WriteAllText(csvPath, "name,age\nAda,36\n");
            try
            {
                window.TextImportPathBox.Text = csvPath;
                window.DataFileFormatComboBox.SelectedIndex = 0;
                window.DataFileModeComboBox.SelectedIndex = 0;
                window.DataFileFormatComboBox.SelectedIndex = 3;
                Check(window.DataFileDetectedFormatText.Text.Contains("JSON (override; extension suggests CSV)"),
                    "Format override remains visible alongside the detected CSV extension.");
                window.DataFileFormatComboBox.SelectedIndex = 0;
                window.DataFileFormatComboBox.SelectedIndex = 1;
                Check(!((ComboBoxItem)window.DataFileModeComboBox.Items[0]).IsEnabled
                    && window.DataFileModeComboBox.SelectedIndex == 1,
                    "Plain-text format forces Extract Text for NLP mode.");
                window.DataFileFormatComboBox.SelectedIndex = 0;
                window.DataFileModeComboBox.SelectedIndex = 0;
                SelectTopicMode(window, "Guid");
                window.TextImportTopicBox.Text = "not-a-guid";
                window.TextImportDryRunCheckBox.IsChecked = true;
                ClickImport(window);
                WaitForImportToFinish(window);

                Check(window.TextImportStatusText.Text.StartsWith("Dry run: 1 record(s):"),
                    "Structured CSV dry run ignores the NLP-only Topic selector and reports the parsed record.");
                Check((LRD_Core.Instance.GetDictionary()?.Count ?? 0) == 0,
                    "CSV dry run leaves the graph unchanged.");

                SelectTopicMode(window, "Base");
                window.TextImportDryRunCheckBox.IsChecked = false;
                ClickImport(window);
                WaitForImportToFinish(window);
                Check(window.TextImportStatusText.Text.Contains("1 record(s): 1 succeeded"),
                    "Structured CSV import reports a successfully processed record.");
                Check(window.StatusBox.Text.Contains("Data-file import complete."),
                    "Structured CSV import writes a detailed completion report.");
                Check(window.StatusBox.Text.Contains("Source node:"),
                    "Structured CSV report includes its provenance Source.");

                int importedNodeCount = LRD_Core.Instance.GetDictionary()?.Count ?? 0;
                ClickImport(window);
                WaitForImportToFinish(window);
                Check(window.TextImportStatusText.Text.Contains("Unchanged high-trust source; import skipped."),
                    "Unchanged high-trust CSV re-import is reported as skipped.");
                Check((LRD_Core.Instance.GetDictionary()?.Count ?? 0) == importedNodeCount,
                    "Skipped CSV re-import does not grow the graph.");
            }
            finally
            {
                File.Delete(csvPath);
            }
        }

        /// <summary>Verifies synthesized, temporal, dictionary, and large-graph answer candidates in the Workbench.</summary>
        private static void RunSection12_NlpWorkbenchCandidateSynthesis()
        {
            Console.WriteLine("\n--- SECTION 12: NLP WORKBENCH MULTI-ANSWER CANDIDATES ---");

            LRD_Core core = LRD_Core.Instance;
            core.InitializeCore();

            Web_Node alice = new Web_Node(Guid.NewGuid(), NodeTypes.Entities, NodeLifetime.Permanent, 0);
            alice.Set_Label("Alice");
            Web_Node bob = new Web_Node(Guid.NewGuid(), NodeTypes.Entities, NodeLifetime.Permanent, 0);
            bob.Set_Label("Bob");
            Web_Node acme = new Web_Node(Guid.NewGuid(), NodeTypes.Entities, NodeLifetime.Permanent, 0);
            acme.Set_Label("Acme");
            Check(core.AddExistingNode(alice) && core.AddExistingNode(bob) && core.AddExistingNode(acme),
                "Workbench synthesis fixture nodes were added.");

            RelationLinkType workAt = RelationRegistry.GetOrRegister("work_at");
            Check(core.AddDirectedConnection(alice.ID, acme.ID, LinkClass.actions, workAt.Forward.Id)
                && core.AddDirectedConnection(bob.ID, acme.ID, LinkClass.actions, workAt.Forward.Id),
                "Workbench synthesis fixture links were added.");

            NlpWorkbenchWindow window = new NlpWorkbenchWindow();
            window.AskInput.Text = "Who works at Acme?";
            MethodInfo? askClick = typeof(NlpWorkbenchWindow).GetMethod(
                "Ask_Button_Click",
                BindingFlags.NonPublic | BindingFlags.Instance);
            askClick?.Invoke(window, new object[] { window.AskButton, new RoutedEventArgs() });
            DoEvents();

            CandidateRow[] rows = window.CandidatesGrid.ItemsSource
                ?.Cast<CandidateRow>()
                .ToArray() ?? Array.Empty<CandidateRow>();
            Check(rows.Length == 2,
                $"Evidence / Candidates table retains both synthesized answers (rows: {rows.Length}).");
            Check(rows.Select(row => row.Label).ToHashSet(StringComparer.Ordinal)
                .SetEquals(new[] { "Alice", "Bob" }),
                "Evidence / Candidates table shows each synthesized answer label.");
            Check(rows.All(row => !string.IsNullOrWhiteSpace(row.PathNodeIDs)
                && row.PathNodeIDs.Contains(acme.ID.ToString(), StringComparison.Ordinal)),
                "candidate rows retain their graph evidence paths.");
            Check(window.AnswerText.Text.Contains("Alice and Bob work at Acme.", StringComparison.Ordinal),
                $"Workbench displays the synthesized conversational answer (actual: '{window.AnswerText.Text}').");

            window.AskInput.Text = "How many people work at Acme?";
            askClick?.Invoke(window, new object[] { window.AskButton, new RoutedEventArgs() });
            DoEvents();
            CandidateRow[] countRows = window.CandidatesGrid.ItemsSource
                ?.Cast<CandidateRow>()
                .ToArray() ?? Array.Empty<CandidateRow>();
            Check(window.ShortAnswerText.Text == "2"
                && window.AnswerText.Text.Contains("2 people", StringComparison.Ordinal),
                $"Workbench shows a numeric quantity answer (short: '{window.ShortAnswerText.Text}', text: '{window.AnswerText.Text}').");
            Check(countRows.Select(row => row.Label).ToHashSet(StringComparer.Ordinal)
                .SetEquals(new[] { "Alice", "Bob" }),
                "quantity answer retains all counted nodes in Evidence / Candidates.");

            window.AskInput.Text = "Tell me about Acme";
            askClick?.Invoke(window, new object[] { window.AskButton, new RoutedEventArgs() });
            DoEvents();
            CandidateRow[] incomingRows = window.CandidatesGrid.ItemsSource
                ?.Cast<CandidateRow>()
                .ToArray() ?? Array.Empty<CandidateRow>();
            Check(incomingRows.Select(row => row.Label).ToHashSet(StringComparer.Ordinal)
                .SetEquals(new[] { "Alice", "Bob" }),
                "entity exploration shows incoming graph subjects in Evidence / Candidates.");
            Check(window.AnswerText.Text.Contains("Alice works at Acme.", StringComparison.Ordinal)
                && window.AnswerText.Text.Contains("Bob works at Acme.", StringComparison.Ordinal),
                $"entity exploration renders incoming facts in their stored direction (actual: '{window.AnswerText.Text}').");

            DateTimeOffset eventStart = DateTimeOffset.UtcNow.Date.AddDays(-2).AddHours(14);
            Web_Node launch = core.CreateEvent(
                "Project Launch",
                eventStart,
                eventStart.AddHours(1),
                alice.ID,
                "launch",
                bob.ID,
                acme.ID);
            string eventDate = eventStart.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string nextDate = eventStart.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            window.AskInput.Text = $"What happened between {eventDate} and {nextDate}?";
            askClick?.Invoke(window, new object[] { window.AskButton, new RoutedEventArgs() });
            DoEvents();
            CandidateRow[] temporalRows = window.CandidatesGrid.ItemsSource
                ?.Cast<CandidateRow>()
                .ToArray() ?? Array.Empty<CandidateRow>();
            Check(temporalRows.Any(row => row.Label == "Project Launch"),
                "temporal range search seeds matching Event nodes in Evidence / Candidates.");
            Check(window.AnswerText.Text.Contains("Project Launch", StringComparison.Ordinal)
                && window.AnswerText.Text.Contains("Alice launched Bob", StringComparison.Ordinal),
                $"Workbench renders ranged event details (actual: '{window.AnswerText.Text}').");

            OntologyTypes.EnsureRegistered();
            OntologyRelations.EnsureRegistered();
            Web_Node commitLexeme = new Web_Node(
                Guid.NewGuid(),
                OntologyTypes.Lexemes,
                NodeLifetime.Permanent,
                0);
            commitLexeme.Set_Label("commit");
            Guid lowSenseId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            Guid highSenseId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            Web_Node highSense = new Web_Node(highSenseId, OntologyTypes.Senses, NodeLifetime.Permanent, 0);
            highSense.Set_Label("A second meaning.");
            Check(core.AddExistingNode(commitLexeme) && core.AddExistingNode(highSense),
                "Workbench dictionary fixture Lexeme and first Sense were added.");
            Check(core.AddDirectedConnection(
                    commitLexeme.ID,
                    highSenseId,
                    OntologyTypes.lexicon,
                    OntologyRelations.HasSense),
                "Workbench dictionary fixture has_sense link was added.");

            window.AskInput.Text = "What is commit?";
            askClick?.Invoke(window, new object[] { window.AskButton, new RoutedEventArgs() });
            DoEvents();
            CandidateRow[] definitionRows = window.CandidatesGrid.ItemsSource
                ?.Cast<CandidateRow>()
                .ToArray() ?? Array.Empty<CandidateRow>();
            Check(window.AnswerText.Text == "The definition of commit is A second meaning.",
                $"Workbench shows the dictionary definition (actual: '{window.AnswerText.Text}').");
            Check(window.ShortAnswerText.Text == "A second meaning.",
                $"Workbench Short Answer shows the Sense definition (actual: '{window.ShortAnswerText.Text}').");
            Check(definitionRows.Length == 1 && definitionRows[0].Label == "A second meaning."
                && definitionRows[0].PathNodeIDs.Contains(commitLexeme.ID.ToString(), StringComparison.Ordinal),
                "Workbench single-definition candidate retains its Lexeme/Sense path.");

            Web_Node lowSense = new Web_Node(lowSenseId, OntologyTypes.Senses, NodeLifetime.Permanent, 0);
            lowSense.Set_Label("A first meaning.");
            Check(core.AddExistingNode(lowSense), "Workbench dictionary fixture second Sense was added.");
            Check(core.AddDirectedConnection(
                    commitLexeme.ID,
                    lowSenseId,
                    OntologyTypes.lexicon,
                    OntologyRelations.HasSense),
                "Workbench dictionary fixture second has_sense link was added.");

            window.AskInput.Text = "What is the commit?";
            askClick?.Invoke(window, new object[] { window.AskButton, new RoutedEventArgs() });
            DoEvents();
            CandidateRow[] definitionCandidates = window.CandidatesGrid.ItemsSource
                ?.Cast<CandidateRow>()
                .ToArray() ?? Array.Empty<CandidateRow>();
            Check(window.AnswerText.Text == "Multiple definitions for the commit were found.",
                $"Workbench shows the multiple-definition message (actual: '{window.AnswerText.Text}').");
            Check(definitionCandidates.Select(row => row.Label)
                    .SequenceEqual(new[] { "A first meaning.", "A second meaning." }),
                "Workbench displays Sense candidates in ascending GUID order.");
            Check(definitionCandidates.All(row =>
                    row.PathNodeIDs.Contains(commitLexeme.ID.ToString(), StringComparison.Ordinal)),
                "Workbench multi-definition candidates retain the Lexeme evidence path.");

            Web_Node mullLexeme = new Web_Node(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeef"),
                OntologyTypes.Lexemes,
                NodeLifetime.Permanent,
                0);
            mullLexeme.Set_Label("mull");
            Check(core.AddExistingNode(mullLexeme), "Large-graph dictionary Lexeme was added.");

            bool unrelatedNodesAdded = true;
            for (int index = 3; index <= 62; index++)
            {
                Web_Node unrelatedSense = new Web_Node(
                    Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
                    OntologyTypes.Senses,
                    NodeLifetime.Permanent,
                    0);
                unrelatedSense.Set_Label($"Unrelated definition {index}.");
                unrelatedNodesAdded &= core.AddExistingNode(unrelatedSense);
            }
            Check(unrelatedNodesAdded, "Large-graph unrelated Senses were added before the target senses.");

            Guid[] connectedSenseIDs = Enumerable.Range(1, 12)
                .Select(index => Guid.Parse($"ffffffff-ffff-ffff-ffff-{index:D12}"))
                .ToArray();
            bool connectedSensesAdded = true;
            foreach ((Guid senseID, int index) in connectedSenseIDs.Select((id, index) => (id, index)))
            {
                Web_Node sense = new Web_Node(senseID, OntologyTypes.Senses, NodeLifetime.Permanent, 0);
                sense.Set_Label($"Mull definition {index + 1}.");
                bool senseAdded = core.AddExistingNode(sense);
                bool senseLinked = senseAdded && core.AddDirectedConnection(
                    mullLexeme.ID,
                    senseID,
                    OntologyTypes.lexicon,
                    OntologyRelations.HasSense);
                connectedSensesAdded &= senseLinked;
            }
            Check(connectedSensesAdded, "Large-graph dictionary Senses were linked to their Lexeme.");

            window.AskInput.Text = "What is mull?";
            askClick?.Invoke(window, new object[] { window.AskButton, new RoutedEventArgs() });
            DoEvents();
            CandidateRow[] largeGraphCandidates = window.CandidatesGrid.ItemsSource
                ?.Cast<CandidateRow>()
                .ToArray() ?? Array.Empty<CandidateRow>();
            Check(largeGraphCandidates.Length == 10,
                $"Large-graph dictionary query displays the reasoning match limit (rows: {largeGraphCandidates.Length}).");
            Check(largeGraphCandidates.All(row => row.Label.StartsWith("Mull definition", StringComparison.Ordinal)
                    && row.PathNodeIDs.Contains(mullLexeme.ID.ToString(), StringComparison.Ordinal)),
                "Large-graph dictionary candidate rows contain only senses linked to the requested Lexeme.");

            window.Close();
            DoEvents();
        }

        /// <summary>Verifies the 3D Visualizer window lifecycle, navigation, picking, labels, colors, and settings persistence.</summary>
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
            for (int aliasIndex = 0; aliasIndex < 100; aliasIndex++)
                node1.Add_Alias("Long selected-node alias segment number " + aliasIndex.ToString());
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
            Check(visualizerWin.SelectionCard.Parent == visualizerWin.ViewportHost,
                "Selected-node HUD is an overlay in the viewport, outside the sidebar.");
            Check(visualizerWin.SelectionScrollViewer.VerticalScrollBarVisibility == ScrollBarVisibility.Auto,
                "Selected-node HUD uses an auto-hiding vertical scrollbar.");
            Check(visualizerWin.MaxLabelsSlider.Minimum == 2.0 && visualizerWin.MaxLabelsSlider.Maximum == 50.0,
                "All Visible label cap ranges from 2 through 50.");

            visualizerWin.LabelModeComboBox.SelectedIndex = 0;
            DoEvents();
            Check(visualizerWin.Options.LabelMode == VisualizerLabelMode.Focused,
                "Label Mode selector applies Focused mode.");
            Check(!visualizerWin.MaxLabelsSlider.IsEnabled,
                "Max Visible Labels cap is disabled in Focused mode.");

            visualizerWin.LabelModeComboBox.SelectedIndex = 1;
            DoEvents();
            Check(visualizerWin.Options.LabelMode == VisualizerLabelMode.AllVisible,
                "Label Mode selector applies All Visible mode.");
            Check(visualizerWin.MaxLabelsSlider.IsEnabled,
                "Max Visible Labels cap is enabled in All Visible mode.");
            visualizerWin.MaxLabelsSlider.Value = 2;
            DoEvents();
            Check(visualizerWin.Options.MaxLabels == 2,
                "All Visible mode allows the minimum cap of two priority labels.");

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
            Check(FlyCameraController.GetOrbitRollDirection(clockwiseKeyDown: true, counterClockwiseKeyDown: false) == 1.0,
                "Alt+E maps to clockwise camera roll.");
            Check(FlyCameraController.GetOrbitRollDirection(clockwiseKeyDown: false, counterClockwiseKeyDown: true) == -1.0,
                "Alt+Q maps to counter-clockwise camera roll.");
            Check(FlyCameraController.GetOrbitRollDirection(clockwiseKeyDown: false, counterClockwiseKeyDown: false) == 0.0,
                "No Alt+E/Q input produces no camera roll.");

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
            Check(visualizerWin.SelectedLabelText.TextWrapping == TextWrapping.Wrap
                && visualizerWin.SelectedAliasesText.TextWrapping == TextWrapping.Wrap,
                "Long selected-node labels and aliases wrap inside the HUD.");
            visualizerWin.SelectionScrollViewer.UpdateLayout();
            Check(visualizerWin.SelectionScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible,
                "Long selected-node content shows the auto vertical scrollbar.");
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

            visualizerWin.Scene.FrameAll();
            typeof(VisualizerWindow).GetMethod("UpdateCameraView", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(visualizerWin, null);
            RenderLabelsForTesting(visualizerWin.Labels);
            Check(visualizerWin.Labels.LastDrawnLabelCount == 2,
                $"All Visible cap 2 retains the selected and hovered floating labels (drawn: {visualizerWin.Labels.LastDrawnLabelCount}, projected: {visualizerWin.Labels.LastProjectedLabelCount}).");
            Check(visualizerWin.Labels.LastCandidateProjectionCount <= VisualizerOptions.MaxLabelCandidatesPerFrame,
                "All Visible background projection work respects its 250-candidate budget.");

            visualizerWin.LabelModeComboBox.SelectedIndex = 0;
            DoEvents();
            RenderLabelsForTesting(visualizerWin.Labels);
            Check(visualizerWin.Labels.LastProjectedLabelCount == 2
                && visualizerWin.Labels.LastDrawnLabelCount == 2,
                $"Focused mode projects and draws only selected and hovered nodes (projected: {visualizerWin.Labels.LastProjectedLabelCount}, drawn: {visualizerWin.Labels.LastDrawnLabelCount}).");
            visualizerWin.Labels.ShowLabels = false;
            RenderLabelsForTesting(visualizerWin.Labels);
            Check(visualizerWin.Labels.LastProjectedLabelCount == 0
                && visualizerWin.Labels.LastDrawnLabelCount == 0,
                "Turning labels off skips priority-label projection and drawing.");
            visualizerWin.Labels.ShowLabels = true;
            visualizerWin.LabelModeComboBox.SelectedIndex = 1;
            DoEvents();

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
            visualizerWin.SelectNodeForTesting(0);
            visualizerWin.HoverNodeForTesting(1);
            nodeCheckBox.IsChecked = false;
            DoEvents();
            Check(!visualizerWin.Scene.IsNodeTypeVisible(NodeTypes.Basics.Id), "Unchecking node visibility hides node type in scene.");
            Check(visualizerWin.SelectedIndex == 0 && visualizerWin.HoveredIndex == 1,
                "Hiding a node type retains selected and hovered node state.");
            RenderLabelsForTesting(visualizerWin.Labels);
            Check(visualizerWin.Labels.LastDrawnLabelCount == 2,
                $"Selected and hovered floating labels bypass node-type visibility filters (drawn: {visualizerWin.Labels.LastDrawnLabelCount}, projected: {visualizerWin.Labels.LastProjectedLabelCount}).");

            // Toggle node visibility back on
            nodeCheckBox.IsChecked = true;
            DoEvents();
            Check(visualizerWin.Scene.IsNodeTypeVisible(NodeTypes.Basics.Id), "Re-checking node visibility shows node type in scene.");
            visualizerWin.HoverNodeForTesting(null);
            visualizerWin.SelectNodeForTesting(null);
            DoEvents();

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
            Check(visualizerWin.SpreadSlider.Minimum == 10.0 && visualizerWin.SpreadSlider.Maximum == 100.0,
                "SpreadSlider range is 10 through 100.");
            Check(visualizerWin.SpreadSlider.TickFrequency == 1.0 && visualizerWin.SpreadSlider.IsSnapToTickEnabled,
                "SpreadSlider snaps to whole units.");
            visualizerWin.SpreadSlider.Value = 17.6;
            visualizerWin.CubeSizeSlider.Value = 1.2;
            visualizerWin.LineWidthSlider.Value = 0.2;
            visualizerWin.MaxLabelsSlider.Value = 50;
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

            Check(visualizerWin.Options.Spread == 18.0, "SpreadSlider rounds/snaps to a whole-unit options.Spread.");
            Check(visualizerWin.Options.CubeSize == 1.2, "CubeSizeSlider updates options.CubeSize.");
            Check(visualizerWin.Options.LineWidth == 0.2, "LineWidthSlider updates options.LineWidth.");
            Check(visualizerWin.Options.MaxLabels == 50, "MaxLabelsSlider updates options.MaxLabels within the new cap.");
            Check(visualizerWin.Options.MaxNodes == 31000, "MaxNodes numeric input updates options.MaxNodes.");
            Check(visualizerWin.Options.MaxLinks == 76000, "MaxLinks numeric input updates options.MaxLinks.");
            Check(visualizerWin.Options.NearPlaneDistance == 0.25, "NearPlaneSlider updates options.NearPlaneDistance.");
            Check(visualizerWin.Scene.NearPlaneDistance == 0.25, "NearPlaneSlider updates scene.NearPlaneDistance.");
            Check(visualizerWin.Labels.MaxLabels == 50, "MaxLabels updates LabelOverlay.MaxLabels.");
            Check(visualizerWin.Options.LabelMode == VisualizerLabelMode.AllVisible,
                "The saved label mode is All Visible.");

            MethodInfo? applySettings = typeof(VisualizerWindow).GetMethod("ApplySettings_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            applySettings?.Invoke(visualizerWin, new object[] { visualizerWin.ApplySettingsButton, new RoutedEventArgs() });
            WaitForCondition(() => !visualizerWin.HasPendingLayoutReplacement && !visualizerWin.IsLoading,
                15000, "debounced Apply layout with raised graph caps");
            Check(visualizerWin.Snapshot?.Nodes.Count == 3, "Apply Layout refreshes graph snapshot after changing caps.");

            visualizerWin.SaveSettingsForTesting();
            string settingsPath = VisualizerSettings.GetDefaultPath();
            Check(File.Exists(settingsPath), $"Visualizer settings saved to JSON file: {settingsPath}");

            string settingsJson = File.ReadAllText(settingsPath);
            Check(settingsJson.Contains("\"spread\": 18"), "Saved settings JSON contains updated whole-unit spread (18).");
            Check(settingsJson.Contains("\"cubeSize\": 1.2"), "Saved settings JSON contains updated cubeSize (1.2).");
            Check(settingsJson.Contains("\"maxNodes\": 31000"), "Saved settings JSON contains updated maxNodes (31000).");
            Check(settingsJson.Contains("\"maxLinks\": 76000"), "Saved settings JSON contains updated maxLinks (76000).");
            Check(settingsJson.Contains("\"maxLabels\": 50"), "Saved settings JSON contains the 50-label cap.");
            Check(settingsJson.Contains("\"labelMode\": 1"), "Saved settings JSON contains All Visible label mode.");

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
            WaitForCondition(() => !visualizerWin.HasPendingLayoutReplacement && !visualizerWin.IsLoading,
                15000, "debounced sidebar Reset View");
            Check(visualizerWin.Options.Spread == 20.0, "Sidebar Reset View restores default Spread (20.0).");
            Check(visualizerWin.Options.CubeSize == 1.0, "ResetViewSettings restores default CubeSize (1.0).");
            Check(visualizerWin.Options.MaxNodes == VisualizerOptions.DefaultMaxNodes, "ResetViewSettings restores default node cap.");
            Check(visualizerWin.Options.MaxLinks == VisualizerOptions.DefaultMaxLinks, "ResetViewSettings restores default link cap.");
            Check(visualizerWin.Options.MaxLabels == VisualizerOptions.DefaultMaxLabels,
                "ResetViewSettings restores the default 50-label cap.");
            Check(visualizerWin.Options.LabelMode == VisualizerLabelMode.Focused,
                "ResetViewSettings restores Focused label mode.");
            Check(!visualizerWin.MaxLabelsSlider.IsEnabled,
                "ResetViewSettings disables the cap control in Focused mode.");
            Check(visualizerWin.Options.MaxIterations == 300, "ResetViewSettings keeps max layout iterations at 300.");

            // Phase 3: cancel-and-replace, trailing-edge debounce, and exception logging.
            string exceptionDirectory = Path.Combine(
                Path.GetTempPath(),
                "open-lrd-visualizer-exceptions-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(exceptionDirectory);
            string exceptionLogPath = Path.Combine(exceptionDirectory, "_exceptions.log");
            VisualizerWindow.ExceptionLogPathOverrideForTesting = exceptionLogPath;
            try
            {
                using ManualResetEventSlim firstLayoutStarted = new ManualResetEventSlim();
                int layoutCalls = 0;
                long replacementStartTicks = 0;
                visualizerWin.LayoutComputationForTesting = (graph, options, token) =>
                {
                    int call = Interlocked.Increment(ref layoutCalls);
                    if (call == 1)
                    {
                        firstLayoutStarted.Set();
                        token.WaitHandle.WaitOne();
                        token.ThrowIfCancellationRequested();
                    }
                    else if (call == 2)
                    {
                        Interlocked.Exchange(ref replacementStartTicks, DateTime.UtcNow.Ticks);
                    }

                    return DistanceLayout3D.Compute(graph, options, token);
                };

                Task cancelledLayout = visualizerWin.RefreshGraphForTestingAsync();
                WaitForCondition(() => firstLayoutStarted.IsSet, 10000, "initial injected layout to start");
                Check(visualizerWin.IsLoading, "Injected initial layout is actively generating.");

                resetViewSettings?.Invoke(
                    visualizerWin,
                    new object[] { visualizerWin.ResetViewSettingsButton, new RoutedEventArgs() });
                visualizerWin.SpreadSlider.Value = 37.0;
                DateTime latestSettingsClickUtc = DateTime.UtcNow;
                applySettings?.Invoke(
                    visualizerWin,
                    new object[] { visualizerWin.ApplySettingsButton, new RoutedEventArgs() });

                WaitForCondition(
                    () => Interlocked.Read(ref replacementStartTicks) > 0,
                    15000,
                    "debounced replacement layout to start");
                TimeSpan debounceElapsed = new TimeSpan(
                    Interlocked.Read(ref replacementStartTicks) - latestSettingsClickUtc.Ticks);
                Check(debounceElapsed >= TimeSpan.FromSeconds(1.95),
                    $"Replacement respects the two-second debounce within dispatcher timer tolerance (actual: {debounceElapsed.TotalSeconds:F2}s).");
                WaitForCondition(
                    () => cancelledLayout.IsCompleted
                        && !visualizerWin.HasPendingLayoutReplacement
                        && !visualizerWin.IsLoading,
                    15000,
                    "cancelled and replacement layouts to complete");
                Check(layoutCalls == 2, $"Latest settings replace the cancelled layout (layout calls: {layoutCalls}).");
                Check(visualizerWin.Options.Spread == 37.0,
                    "The latest Apply Layout click wins over the earlier Reset View request.");

                string cancellationLog = File.ReadAllText(exceptionLogPath);
                Check(cancellationLog.Contains(nameof(OperationCanceledException), StringComparison.Ordinal),
                    "OperationCanceledException is appended to the exception log during replacement.");
                Check(cancellationLog.Contains("Apply Layout", StringComparison.Ordinal),
                    "Exception log records the latest action that replaced the active layout.");

                visualizerWin.LayoutComputationForTesting = (_, _, _) =>
                    throw new InvalidOperationException("injected visualizer layout failure");
                resetViewSettings?.Invoke(
                    visualizerWin,
                    new object[] { visualizerWin.ResetViewSettingsButton, new RoutedEventArgs() });
                WaitForCondition(
                    () => !visualizerWin.HasPendingLayoutReplacement
                        && !visualizerWin.IsLoading
                        && visualizerWin.StatusText.Text.Contains("injected visualizer layout failure", StringComparison.Ordinal),
                    15000,
                    "injected layout exception to be reported");
                string appendedLog = File.ReadAllText(exceptionLogPath);
                Check(appendedLog.Contains("InvalidOperationException", StringComparison.Ordinal)
                    && appendedLog.Contains("injected visualizer layout failure", StringComparison.Ordinal),
                    "Unexpected layout exceptions are appended with their type and message.");

                string blockingFile = Path.Combine(exceptionDirectory, "not-a-directory");
                File.WriteAllText(blockingFile, "block directory creation");
                VisualizerWindow.ExceptionLogPathOverrideForTesting = Path.Combine(
                    blockingFile,
                    "nested",
                    "_exceptions.log");
                resetViewSettings?.Invoke(
                    visualizerWin,
                    new object[] { visualizerWin.ResetViewSettingsButton, new RoutedEventArgs() });
                WaitForCondition(
                    () => !visualizerWin.HasPendingLayoutReplacement
                        && !visualizerWin.IsLoading
                        && visualizerWin.WarningsText.Text.Contains("Exception log write failed", StringComparison.Ordinal),
                    15000,
                    "exception-log write failure to be reported safely");
                Check(visualizerWin.StatusText.Text.Contains("injected visualizer layout failure", StringComparison.Ordinal),
                    "Log write failure does not mask the original exception.");
            }
            finally
            {
                visualizerWin.LayoutComputationForTesting = null;
                VisualizerWindow.ExceptionLogPathOverrideForTesting = null;
                if (Directory.Exists(exceptionDirectory))
                    Directory.Delete(exceptionDirectory, recursive: true);
            }

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

            RunLargeGraphLabelBudgetCheck();
        }

        /// <summary>Verifies candidate-projection and label-drawing budgets on a 20,000-node synthetic graph.</summary>
        private static void RunLargeGraphLabelBudgetCheck()
        {
            const int syntheticNodeCount = 20000;
            LRD_Core core = LRD_Core.Instance;
            for (int index = 0; index < syntheticNodeCount; index++)
            {
                Web_Node node = new Web_Node(Guid.NewGuid(), NodeTypes.Basics, NodeLifetime.Permanent, 0);
                node.Set_Label("Performance node " + index.ToString(CultureInfo.InvariantCulture));
                if (!core.AddExistingNode(node))
                    throw new InvalidOperationException("A synthetic Visualizer performance node could not be added.");
            }

            VisualizerOptions snapshotOptions = new VisualizerOptions
            {
                MaxNodes = VisualizerOptions.MaximumMaxNodes,
                MaxLinks = VisualizerOptions.MaximumMaxLinks
            };
            GraphSnapshot graph = GraphSnapshot.FromCore(core, snapshotOptions);
            CameraView camera = new CameraView(
                Vector3.Zero,
                Vector3.UnitZ,
                Vector3.UnitY,
                55d,
                1600d,
                900d,
                0.05d,
                1000d);
            double tangentHorizontal = Math.Tan(camera.FieldOfViewDegrees * Math.PI / 360d);
            double tangentVertical = tangentHorizontal / camera.AspectRatio;
            Vector3[] positions = new Vector3[graph.Nodes.Count];
            const int columns = 200;
            for (int index = 0; index < positions.Length; index++)
            {
                int column = index % columns;
                int row = index / columns;
                double horizontal = ((column + 0.5d) / columns * 2d - 1d) * 100d * tangentHorizontal * 0.8d;
                double vertical = ((row + 0.5d) / Math.Ceiling(positions.Length / (double)columns) * 2d - 1d)
                    * 100d * tangentVertical * 0.8d;
                positions[index] = camera.Position
                    + camera.LookDirection * 100f
                    + camera.Right * (float)horizontal
                    + camera.Up * (float)vertical;
            }
            if (positions.Length > 0)
                positions[0] = new Vector3(0f, 0f, 100f);
            if (positions.Length > 1)
                positions[1] = new Vector3(0.2f, 0f, 100f);

            GraphLabelOverlay overlay = new GraphLabelOverlay
            {
                MaxLabelDistance = 500d,
                NodeBoxSize = 1d
            };
            overlay.SetGraph(graph, positions, VisualizerOptions.DefaultLabelCharacterLimit);
            overlay.SetSpatialIndex(SpatialNodeIndex.Build(positions));
            overlay.UpdateCamera(camera);
            overlay.LabelMode = VisualizerLabelMode.AllVisible;
            overlay.MaxLabels = VisualizerOptions.DefaultMaxLabels;

            Stopwatch renderTimer = Stopwatch.StartNew();
            RenderLabelsForTesting(overlay);
            renderTimer.Stop();
            Check(graph.Nodes.Count >= syntheticNodeCount,
                $"Large-label fixture contains at least {syntheticNodeCount:N0} nodes (actual: {graph.Nodes.Count:N0}).");
            Check(overlay.LastCandidateProjectionCount == VisualizerOptions.MaxLabelCandidatesPerFrame,
                $"All Visible projects exactly the 250-node budget on a large graph (actual: {overlay.LastCandidateProjectionCount}).");
            Check(overlay.LastDrawnLabelCount <= VisualizerOptions.DefaultMaxLabels,
                $"Large-graph label drawing stays within the 50-label cap (actual: {overlay.LastDrawnLabelCount}).");
            Console.WriteLine($"  Large graph label overlay: {overlay.LastCandidateProjectionCount} candidates, "
                + $"{overlay.LastDrawnLabelCount} labels in {renderTimer.Elapsed.TotalMilliseconds:F2} ms.");

            const int cameraMotionFrames = 60;
            FlyCamera movingCamera = new FlyCamera(Vector3.Zero, 0d, 0d);
            overlay.SelectedNodeIndex = 0;
            overlay.HoveredNodeIndex = 1;
            overlay.LabelMode = VisualizerLabelMode.Focused;
            Stopwatch focusedMotionTimer = Stopwatch.StartNew();
            for (int frame = 0; frame < cameraMotionFrames; frame++)
            {
                movingCamera.Move(forward: 0.1d, strafe: 0.05d, vertical: 0d, speed: 1d, deltaSeconds: 1d / 60d);
                overlay.UpdateCamera(new CameraView(
                    movingCamera.Position,
                    movingCamera.LookDirection,
                    movingCamera.Up,
                    camera.FieldOfViewDegrees,
                    camera.ViewportWidth,
                    camera.ViewportHeight,
                    camera.NearPlaneDistance,
                    camera.FarPlaneDistance));
                RenderLabelsForTesting(overlay);
            }
            focusedMotionTimer.Stop();
            Check(overlay.LastProjectedLabelCount == 2 && overlay.LastDrawnLabelCount == 2,
                "Focused mode projects and draws only the selected and hovered nodes on a large graph.");

            movingCamera = new FlyCamera(Vector3.Zero, 0d, 0d);
            overlay.LabelMode = VisualizerLabelMode.AllVisible;
            Stopwatch allVisibleMotionTimer = Stopwatch.StartNew();
            bool allVisibleBudgetRespected = true;
            for (int frame = 0; frame < cameraMotionFrames; frame++)
            {
                movingCamera.Move(forward: 0.1d, strafe: 0.05d, vertical: 0d, speed: 1d, deltaSeconds: 1d / 60d);
                overlay.UpdateCamera(new CameraView(
                    movingCamera.Position,
                    movingCamera.LookDirection,
                    movingCamera.Up,
                    camera.FieldOfViewDegrees,
                    camera.ViewportWidth,
                    camera.ViewportHeight,
                    camera.NearPlaneDistance,
                    camera.FarPlaneDistance));
                RenderLabelsForTesting(overlay);
                allVisibleBudgetRespected &=
                    overlay.LastCandidateProjectionCount <= VisualizerOptions.MaxLabelCandidatesPerFrame;
            }
            allVisibleMotionTimer.Stop();
            Check(allVisibleBudgetRespected,
                "All Visible respects the candidate projection budget during large-graph camera movement.");
            Console.WriteLine($"  Large graph camera-motion labels ({cameraMotionFrames} frames): "
                + $"Focused {focusedMotionTimer.Elapsed.TotalMilliseconds:F2} ms; "
                + $"All Visible {allVisibleMotionTimer.Elapsed.TotalMilliseconds:F2} ms.");

            overlay.ShowLabels = false;
            RenderLabelsForTesting(overlay);
            Check(overlay.LastProjectedLabelCount == 0 && overlay.LastDrawnLabelCount == 0,
                "Labels-off mode skips all projection and drawing on a large graph.");
        }

        // ================================================================================
        // 10. SIGN-OFF
        // ================================================================================
        /// <summary>Prints the final verification summary and asserts that zero checks failed.</summary>
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
