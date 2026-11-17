using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using OPEN_LRD.NLP;
using OPEN_LRD.Nodes;
using OPEN_LRD.Maintenance;
using OPEN_LRD.Ontology;
using OPEN_LRD.Ontology.Gcide;
using OPEN_LRD.Persistence;

namespace OPEN_LRD
{
    /// <summary>Hosts graph persistence, text ingestion, ontology, maintenance, and data-file tools.</summary>
    public partial class Tools : Window
    {
        private const string LogFilter =
            "OPEN-LRD logs (*.lrdlog)|*.lrdlog|JSON files (*.json)|*.json|All files (*.*)|*.*";

        private const string TextFileFilter =
            "Text files (*.txt;*.md;*.log)|*.txt;*.md;*.log|All files (*.*)|*.*";

        private const string DataFileFilter =
            "Supported data files (*.txt;*.md;*.log;*.csv;*.json;*.xml)|*.txt;*.md;*.log;*.csv;*.json;*.xml|All files (*.*)|*.*";

        private static LRD_Core Core => LRD_Core.Instance;

        private CancellationTokenSource? ontologyLoad;
        private CancellationTokenSource? textImport;
        private bool directIngestBusy;

        /// <summary>Creates the Tools window and initializes its shared topic and file selectors.</summary>
        public Tools()
        {
            InitializeComponent();
            InitializeTopicSelectors();
            DataFileFormatComboBox.SelectedIndex = 0;
            DataFileModeComboBox.SelectedIndex = 0;
            UpdateDataFileControls();
        }

        private bool IsIngestionBusy => textImport is not null || ontologyLoad is not null || directIngestBusy;

        /// <summary>Populates topic type selectors and initializes their selected values.</summary>
        private void InitializeTopicSelectors()
        {
            OntologyTypes.EnsureRegistered();
            TopicNodeTypeComboBox.ItemsSource = NodeTypes.GetAll()
                .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            TopicNodeTypeComboBox.SelectedItem = NodeTypes.Concepts;
            TopicModeComboBox.SelectedIndex = 0;
            UpdateTopicSelectionControls();
        }

        /// <summary>Refreshes topic-entry controls after the topic selection mode changes.</summary>
        private void TopicModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TopicSelectionStatusText is null)
                return;

            TextImportTopicBox.Clear();
            UpdateTopicSelectionControls();
        }

        /// <summary>Refreshes available topic nodes after the requested node type changes.</summary>
        private void TopicNodeTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TopicNodePickerComboBox is null)
                return;

            RefreshTopicNodePicker();
        }

        /// <summary>Shows and enables the topic input controls required by the selected mode.</summary>
        private void UpdateTopicSelectionControls()
        {
            string mode = SelectedTopicMode;
            bool usesText = mode is "Guid" or "Label" or "Alias";
            bool usesNodePicker = mode is "Sense" or "Category" or "Field" or "Taxon"
                or "Reality" or "NodeType";
            bool usesNodeType = mode == "NodeType";

            TopicValueLabel.Text = mode switch
            {
                "Guid" => "Node GUID:",
                "Label" => "Node label:",
                "Alias" => "Alias:",
                _ => "Value:"
            };
            TopicValueLabel.Visibility = usesText ? Visibility.Visible : Visibility.Collapsed;
            TextImportTopicBox.Visibility = usesText ? Visibility.Visible : Visibility.Collapsed;
            TextImportTopicBox.IsEnabled = usesText && !IsIngestionBusy;
            TopicNodeTypeLabel.Visibility = usesNodeType ? Visibility.Visible : Visibility.Collapsed;
            TopicNodeTypeComboBox.Visibility = usesNodeType ? Visibility.Visible : Visibility.Collapsed;
            TopicNodeTypeComboBox.IsEnabled = usesNodeType && !IsIngestionBusy;
            TopicNodePickerLabel.Visibility = usesNodePicker ? Visibility.Visible : Visibility.Collapsed;
            TopicNodePickerComboBox.Visibility = usesNodePicker ? Visibility.Visible : Visibility.Collapsed;
            TopicNodePickerComboBox.IsEnabled = usesNodePicker && !IsIngestionBusy;

            TopicSelectionStatusText.Text = mode switch
            {
                "Base" => "No topic override; use base/general/none.",
                "CurrentTopic" => "Uses the most recent topic recorded by a conversation.",
                "Source" => "Uses the source node for this text or file import.",
                _ => "Select or enter one existing graph node as the topic."
            };

            if (usesNodePicker)
                RefreshTopicNodePicker();
            else
                TopicNodePickerComboBox.ItemsSource = Array.Empty<Web_Node>();
        }

        /// <summary>Loads graph nodes that match the selected topic category.</summary>
        private void RefreshTopicNodePicker()
        {
            if (TopicNodePickerComboBox is null)
                return;

            Guid? previouslySelected = (TopicNodePickerComboBox.SelectedItem as Web_Node)?.ID;
            if (!Core.initialized)
            {
                TopicNodePickerComboBox.ItemsSource = Array.Empty<Web_Node>();
                TopicNodePickerComboBox.SelectedIndex = -1;
                return;
            }

            NodeTypes? type = SelectedTopicMode switch
            {
                "Sense" => OntologyTypes.Senses,
                "Category" => OntologyTypes.Categories,
                "Field" => OntologyTypes.Fields,
                "Taxon" => OntologyTypes.Taxa,
                "Reality" => OntologyTypes.Realities,
                "NodeType" => TopicNodeTypeComboBox.SelectedItem as NodeTypes,
                _ => null
            };

            Web_Node[] nodes = type is null
                ? Array.Empty<Web_Node>()
                : Core.FindByType(type)
                    .Where(node => !string.IsNullOrWhiteSpace(node.Label))
                    .OrderBy(node => node.Label, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(node => node.ID)
                    .ToArray();
            TopicNodePickerComboBox.ItemsSource = nodes;
            TopicNodePickerComboBox.SelectedItem = previouslySelected.HasValue
                ? nodes.FirstOrDefault(node => node.ID == previouslySelected.Value)
                : null;
        }

        private string SelectedTopicMode =>
            (TopicModeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string
            ?? "Base";

        /// <summary>Resolves the selected topic mode to a graph node before ingestion begins.</summary>
        private bool TryResolveTopicNode(
            Guid? sourceNodeID,
            string? filePath,
            bool dryRun,
            out Guid? topicNodeID,
            out bool useSourceAsTopic)
        {
            topicNodeID = null;
            useSourceAsTopic = false;

            switch (SelectedTopicMode)
            {
                case "Base":
                    return true;

                case "Guid":
                    string guidText = TextImportTopicBox.Text.Trim();
                    if (!Guid.TryParse(guidText, out Guid topicID) || topicID == Guid.Empty)
                        return TopicSelectionError("Enter a non-empty Topic GUID.");
                    if (!Core.TryGetNode(topicID, out _))
                        return TopicSelectionError($"Topic node '{topicID}' was not found.");
                    topicNodeID = topicID;
                    return true;

                case "Label":
                    return ResolveUniqueTopic(
                        Core.FindByLabel(TextImportTopicBox.Text.Trim()),
                        "Node Label",
                        out topicNodeID);

                case "Alias":
                    string alias = TextImportTopicBox.Text.Trim();
                    if (alias.Length == 0)
                        return TopicSelectionError("Enter a node alias.");
                    return ResolveUniqueTopic(
                        Core.FindNodes().Where(node => node.Has_Alias(alias)).ToArray(),
                        "Alias",
                        out topicNodeID);

                case "Sense":
                case "Category":
                case "Field":
                case "Taxon":
                case "Reality":
                case "NodeType":
                    if (TopicNodePickerComboBox.SelectedItem is not Web_Node selectedTopic
                        || !Core.TryGetNode(selectedTopic.ID, out _))
                    {
                        return TopicSelectionError("Select an existing topic node.");
                    }
                    topicNodeID = selectedTopic.ID;
                    return true;

                case "CurrentTopic":
                    topicNodeID = FindMostRecentTopicNodeID();
                    if (!topicNodeID.HasValue)
                        TopicSelectionStatusText.Text = "No prior topic was found; using base/general/none.";
                    return true;

                case "Source":
                    if (sourceNodeID.HasValue)
                    {
                        topicNodeID = sourceNodeID;
                        return true;
                    }

                    if (filePath is not null)
                    {
                        // TextFileImporter creates/reuses the file Source node after
                        // validation; this avoids mutating the graph during a dry run.
                        useSourceAsTopic = true;
                        return true;
                    }

                    if (dryRun)
                    {
                        topicNodeID = FindUserSourceNode()?.ID;
                        return true;
                    }

                    return TopicSelectionError("The user Source node is unavailable.");

                default:
                    return TopicSelectionError("Select a valid topic option.");
            }
        }

        /// <summary>Resolves a unique topic candidate and reports missing or ambiguous matches.</summary>
        private bool ResolveUniqueTopic(
            IReadOnlyList<Web_Node> matches,
            string selectionName,
            out Guid? topicNodeID)
        {
            topicNodeID = null;
            if (matches.Count == 0)
                return TopicSelectionError($"No graph node matches the selected {selectionName}.");
            if (matches.Count > 1)
                return TopicSelectionError(
                    $"The selected {selectionName} matches multiple graph nodes; choose a GUID or a more specific selection.");

            topicNodeID = matches[0].ID;
            return true;
        }

        /// <summary>Stores a topic validation error and returns a failed resolution result.</summary>
        private bool TopicSelectionError(string message)
        {
            TopicSelectionStatusText.Text = message;
            return false;
        }

        /// <summary>Finds the most recent topic node recorded in conversation provenance.</summary>
        private Guid? FindMostRecentTopicNodeID()
        {
            IReadOnlyDictionary<Guid, ConversationContext>? conversations = Core.GetConversations();
            if (conversations is null)
                return null;

            foreach (ConversationContext conversation in conversations.Values
                .OrderByDescending(candidate => candidate.TurnIndex))
            {
                Guid? topic = conversation.Provenance
                    .OrderByDescending(entry => entry.TurnIndex)
                    .ThenByDescending(entry => entry.Sequence)
                    .Select(entry => entry.TopicNodeID)
                    .FirstOrDefault(candidate => candidate.HasValue);
                if (topic.HasValue)
                    return topic;
            }

            return null;
        }

        /// <summary>Finds the graph's reserved user Source node.</summary>
        private static Web_Node? FindUserSourceNode()
        {
            return Core.FindByLabel("user", NodeTypes.Sources).FirstOrDefault();
        }

        /// <summary>Finds or creates the reserved user Source node with its fixed trust baseline.</summary>
        private static Web_Node EnsureUserSourceNode()
        {
            Web_Node? existing = FindUserSourceNode();
            if (existing is not null)
            {
                existing.Set_TrustRating(0.5);
                return existing;
            }

            Web_Node source = new Web_Node(
                Guid.NewGuid(),
                NodeTypes.Sources,
                NodeLifetime.Permanent,
                0);
            source.Set_Label("user");
            source.Set_TrustRating(0.5);
            if (!Core.AddExistingNode(source))
                throw new InvalidOperationException("The user Source node could not be created.");
            return source;
        }

        /// <summary>Enables or disables ingestion and shared topic controls together.</summary>
        private void SetIngestionControlsEnabled(bool enabled)
        {
            DirectTextInput.IsEnabled = enabled;
            DirectTextDryRunCheckBox.IsEnabled = enabled;
            DirectTextIngestButton.IsEnabled = enabled;
            DirectTextClearButton.IsEnabled = enabled;
            TopicModeComboBox.IsEnabled = enabled;
            TextImportPathBox.IsEnabled = enabled;
            TextImportBrowseButton.IsEnabled = enabled;
            TextImportSourceBox.IsEnabled = enabled;
            TextImportDryRunCheckBox.IsEnabled = enabled;
            TextImportMaxMegabytesBox.IsEnabled = enabled;
            TextImportMaxChunksBox.IsEnabled = enabled;
            DataFileFormatComboBox.IsEnabled = enabled;
            DataFileModeComboBox.IsEnabled = enabled;
            TextImportButton.IsEnabled = enabled;
            TextImportTopicBox.IsEnabled = enabled && TextImportTopicBox.Visibility == Visibility.Visible;
            TopicNodeTypeComboBox.IsEnabled = enabled && TopicNodeTypeComboBox.Visibility == Visibility.Visible;
            TopicNodePickerComboBox.IsEnabled = enabled && TopicNodePickerComboBox.Visibility == Visibility.Visible;
        }

        /// <summary>Runs the selected graph maintenance stages and displays their report.</summary>
        private void RunMaintenance_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            try
            {
                MaintenanceReport report = Core.RunMaintenanceCycle(new MaintenanceOptions
                {
                    EnableReasoningMaintenance = true,
                    EnableNodeMerging = MaintenanceMergeNodesCheckBox.IsChecked == true,
                    EnableNodeMetadataReasoning = MaintenanceMetadataCheckBox.IsChecked == true,
                    EnableCompositionalInduction = MaintenanceCompositionCheckBox.IsChecked == true,
                    EnableFrequentPatternAbstraction = MaintenancePatternCheckBox.IsChecked == true,
                    EnableConceptRelationElevation = MaintenanceElevationCheckBox.IsChecked == true,
                    EnableSupersededLinkPruning = MaintenancePruningCheckBox.IsChecked == true
                });

                MaintenanceStatusText.Text =
                    $"Examined {report.NodesExamined} nodes; created {report.NodesCreated}; " +
                    $"removed {report.NodesRemoved} expired/merged nodes; " +
                    $"merged {report.NodesMerged}; examined {report.RelationPatternsExamined} relation pattern(s); " +
                    $"metadata scanned {report.MetadataNodesExamined} node(s), created {report.MetadataValuesCreated} value node(s), " +
                    $"added {report.MetadataFactsAdded} fact(s), inactivated {report.MetadataFactsInvalidated}; " +
                    $"registered {report.RelationTypesRegistered} relation type(s); " +
                    $"created {report.RelationNodesCreated} relation concept(s); generated {report.LinksGenerated} link(s); " +
                    $"invalidated {report.LinksInvalidated} link(s) in {report.Elapsed.TotalSeconds:F2}s." +
                    (report.WorkBudgetExhausted ? " A work budget was reached." : string.Empty);

                StatusBox.Text = report.Diagnostics.Count == 0
                    ? MaintenanceStatusText.Text
                    : MaintenanceStatusText.Text + Environment.NewLine + string.Join(Environment.NewLine, report.Diagnostics);
            }
            catch (Exception ex)
            {
                MaintenanceStatusText.Text = "Maintenance failed: " + ex.Message;
                SetStatus(MaintenanceStatusText.Text);
            }
        }

        /// <summary>Exports the core and transient logs to the selected destination.</summary>
        private void ExportLogs_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export OPEN-LRD Logs",
                Filter = LogFilter,
                DefaultExt = ".lrdlog",
                AddExtension = true,
                FileName = "open-lrd-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".lrdlog"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                bool includeTransient = IncludeTransientCheckBox.IsChecked == true;
                CoreLogExportResult result = CoreLogExporter.Export(
                    Core,
                    dialog.FileName,
                    includeTransient);

                LastExportPathText.Text = result.TransientPath is null
                    ? result.CorePath
                    : result.CorePath + "  (+ " + Path.GetFileName(result.TransientPath) + ")";
                SetStatus(FormatExportReport(result));
            }
            catch (Exception ex)
            {
                SetStatus("Export failed: " + ex.Message);
            }
        }

        /// <summary>Imports a core log using the selected merge or replace policy.</summary>
        private void ImportCoreLog_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            string? path = SelectLogPath("Import Core Log");

            if (path is null)
                return;

            ImportMode mode = GetSelectedImportMode();

            if (!ConfirmCoreImport(mode))
                return;

            try
            {
                PersistenceImportResult result = CoreLogImporter.ImportCore(Core, path, mode);
                SetStatus(FormatImportReport("Core import", result, isTransient: false));
            }
            catch (Exception ex)
            {
                SetStatus("Core import failed: " + ex.Message);
            }
        }

        /// <summary>Imports transient query and response chains using the selected policy.</summary>
        private void ImportTransientLog_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            string? path = SelectLogPath("Import Transient Log");

            if (path is null)
                return;

            ImportMode mode = GetSelectedImportMode();

            if (!ConfirmTransientImport(mode))
                return;

            try
            {
                PersistenceImportResult result = CoreLogImporter.ImportTransient(Core, path, mode);
                SetStatus(FormatImportReport("Transient import", result, isTransient: true));
            }
            catch (Exception ex)
            {
                SetStatus("Transient import failed: " + ex.Message);
            }
        }

        /// <summary>Ingests the typed sentence input with selected source, topic, and dry-run settings.</summary>
        private void DirectTextIngest_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            if (IsIngestionBusy)
            {
                SetDirectTextStatus("An ingestion or ontology load is already running.");
                return;
            }

            string text = DirectTextInput.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetDirectTextStatus("Enter text to ingest.");
                return;
            }

            if (text.Length > 1000)
            {
                SetDirectTextStatus("Direct text must be 1,000 characters or fewer.");
                return;
            }

            bool dryRun = DirectTextDryRunCheckBox.IsChecked == true;
            NlpPipeline? pipeline = null;
            Guid? temporaryConversationID = null;
            bool keepConversation = false;
            SetDirectTextBusy(true);
            DirectTextProgressBar.Value = 0;
            SetDirectTextStatus(dryRun ? "Preparing dry run..." : "Preparing ingestion...");

            try
            {
                pipeline = new NlpPipeline(Core);
                if (dryRun)
                    temporaryConversationID = pipeline.ConversationID;

                NlpResult initialAnalysis = pipeline.Analyze(text);
                if (initialAnalysis.Status == NlpStatus.InvalidInput || initialAnalysis.Parsed is null)
                {
                    SetDirectTextStatus(
                        initialAnalysis.Diagnostics.FirstOrDefault() ?? "Text could not be analyzed.");
                    SetStatus("Direct text ingestion failed: " + DirectTextStatusText.Text);
                    return;
                }

                string[] sentences = initialAnalysis.Parsed.Sentences
                    .Select(sentence => sentence.Text.Trim())
                    .Where(sentence => sentence.Length > 0)
                    .ToArray();

                if (sentences.Length == 0)
                {
                    SetDirectTextStatus("No sentences were found in the input.");
                    SetStatus("Direct text ingestion found no sentences.");
                    return;
                }

                Guid? sourceNodeID = dryRun
                    ? FindUserSourceNode()?.ID
                    : SelectedTopicMode == "Source"
                        ? EnsureUserSourceNode().ID
                        : null;
                if (!TryResolveTopicNode(
                    sourceNodeID,
                    null,
                    dryRun,
                    out Guid? topicNodeID,
                    out _))
                {
                    SetDirectTextStatus(TopicSelectionStatusText.Text);
                    SetStatus(TopicSelectionStatusText.Text);
                    return;
                }

                if (!dryRun && !sourceNodeID.HasValue)
                    sourceNodeID = EnsureUserSourceNode().ID;
                if (!dryRun)
                    keepConversation = true;

                int ingested = 0;
                int unsupported = 0;
                int failed = 0;
                List<string> sentenceDiagnostics = new List<string>();

                for (int index = 0; index < sentences.Length; index++)
                {
                    string sentence = sentences[index];
                    if (dryRun)
                    {
                        NlpResult analysis = pipeline.Analyze(sentence);
                        if (analysis.Status == NlpStatus.Parsed
                            && analysis.Frames.Any(frame => frame.Ingestible))
                        {
                            ingested++;
                        }
                        else
                        {
                            unsupported++;
                        }

                        sentenceDiagnostics.AddRange(analysis.Diagnostics);
                    }
                    else
                    {
                        NlpResult result = pipeline.Ingest(sentence, sourceNodeID, topicNodeID);
                        if (result.Status == NlpStatus.Ingested)
                            ingested++;
                        else if (result.Status == NlpStatus.Unsupported)
                            unsupported++;
                        else
                            failed++;

                        sentenceDiagnostics.AddRange(result.Diagnostics);
                    }

                    DirectTextProgressBar.Value = (double)(index + 1) / sentences.Length * 100.0;
                    SetDirectTextStatus(
                        $"Processed sentence {index + 1} of {sentences.Length}: " +
                        $"{ingested} supported, {unsupported} unsupported, {failed} failed.");
                }

                string summary = dryRun
                    ? $"Direct text dry run: {sentences.Length} sentence(s), {ingested} supported, {unsupported} unsupported."
                    : $"Direct text ingestion: {sentences.Length} sentence(s), {ingested} ingested, {unsupported} unsupported, {failed} failed.";
                SetDirectTextStatus(summary);

                StringBuilder report = new StringBuilder();
                report.AppendLine(dryRun ? "Direct text dry run." : "Direct text ingestion complete.");
                report.AppendLine($"Sentences: {sentences.Length}");
                report.AppendLine($"Ingested: {ingested}");
                report.AppendLine($"Unsupported: {unsupported}");
                report.AppendLine($"Failed: {failed}");
                report.AppendLine("Conversation: " + (dryRun ? "(temporary)" : pipeline.ConversationID.ToString()));
                report.AppendLine("Source node: " + (sourceNodeID?.ToString() ?? "(none)"));
                report.AppendLine("Topic node: " + (topicNodeID?.ToString() ?? "(base/general/none)"));
                AppendMessages(report, "Notes", sentenceDiagnostics
                    .Distinct(StringComparer.Ordinal)
                    .ToList());
                SetStatus(report.ToString().TrimEnd());
            }
            catch (Exception ex)
            {
                SetDirectTextStatus("Direct text ingestion failed: " + ex.Message);
                SetStatus(DirectTextStatusText.Text);
            }
            finally
            {
                pipeline?.Dispose();
                if (temporaryConversationID.HasValue && (dryRun || !keepConversation))
                    Core.RemoveConversation(temporaryConversationID.Value);

                SetDirectTextBusy(false);
            }
        }

        /// <summary>Clears the direct text input and its preview state.</summary>
        private void DirectTextClear_Button_Click(object sender, RoutedEventArgs e)
        {
            DirectTextInput.Clear();
            DirectTextProgressBar.Value = 0;
            SetDirectTextStatus("Ready.");
        }

        /// <summary>Updates direct-ingestion busy state and shared control availability.</summary>
        private void SetDirectTextBusy(bool busy)
        {
            directIngestBusy = busy;
            SetIngestionControlsEnabled(!busy);
            UpdateTopicSelectionControls();
        }

        /// <summary>Sets the direct-ingestion status message.</summary>
        private void SetDirectTextStatus(string text)
        {
            DirectTextStatusText.Text = text;
        }

        /// <summary>Opens the supported data-file picker and stores the chosen path.</summary>
        private void TextImportBrowse_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select a data file to import",
                Filter = DataFileFilter,
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
            {
                TextImportPathBox.Text = dialog.FileName;
                UpdateDataFileControls();
            }
        }

        /// <summary>Refreshes format detection and mode controls after a format selection.</summary>
        private void DataFileFormatComboBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateDataFileControls();
        }

        /// <summary>Refreshes file-ingestion controls after the ingestion mode changes.</summary>
        private void DataFileModeComboBox_SelectionChanged(
            object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateDataFileControls();
        }

        /// <summary>Shows the detected format and enforces format-specific mode availability.</summary>
        private void UpdateDataFileControls()
        {
            if (DataFileFormatComboBox is null || DataFileModeComboBox is null)
                return;

            string format = (DataFileFormatComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string
                ?? "Auto";
            string path = TextImportPathBox?.Text ?? string.Empty;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            System.Windows.Controls.ComboBoxItem structuredMode =
                (System.Windows.Controls.ComboBoxItem)DataFileModeComboBox.Items[0];
            bool isPlainText = format == nameof(DataFileFormat.Text)
                || (format == "Auto" && extension is ".txt" or ".md" or ".log");
            structuredMode.IsEnabled = !isPlainText;
            if (isPlainText)
                DataFileModeComboBox.SelectedIndex = 1;

            extension = extension.TrimStart('.').ToUpperInvariant();
            string suggestedFormat = extension switch
            {
                "TXT" or "MD" or "LOG" => "TEXT",
                "CSV" => "CSV",
                "JSON" => "JSON",
                "XML" => "XML",
                _ => string.Empty
            };
            DataFileDetectedFormatText.Text = format == "Auto"
                ? suggestedFormat.Length == 0 ? "(auto-detect)" : $"Auto: {suggestedFormat}"
                : suggestedFormat.Length > 0 && suggestedFormat != format.ToUpperInvariant()
                    ? $"{format.ToUpperInvariant()} (override; extension suggests {suggestedFormat})"
                    : format.ToUpperInvariant();
        }

        /// <summary>Validates options and asynchronously imports the selected text, CSV, JSON, or XML file.</summary>
        private async void TextImport_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            if (textImport is not null)
            {
                SetTextImportStatus("A text file import is already running.");
                return;
            }

            if (ontologyLoad is not null)
            {
                SetTextImportStatus("An ontology load is already running.");
                return;
            }

            string path = TextImportPathBox.Text.Trim();

            if (path.Length == 0)
            {
                SetTextImportStatus("Choose a text file first.");
                return;
            }

            if (!File.Exists(path))
            {
                SetTextImportStatus("The selected text file no longer exists.");
                return;
            }

            UpdateDataFileControls();

            if (!TryParseOptionalGuid(TextImportSourceBox.Text, "Source ID", out Guid? sourceNodeID)
                || !TryParsePositiveInt(TextImportMaxMegabytesBox.Text, "Max file MB", out int maxMegabytes)
                || !TryParsePositiveInt(TextImportMaxChunksBox.Text, "Max chunks", out int maxRecords))
            {
                return;
            }

            string? formatTag = (DataFileFormatComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            string? modeTag = (DataFileModeComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag as string;
            if (!Enum.TryParse(formatTag, ignoreCase: true, out DataFileFormat format)
                || !Enum.TryParse(modeTag == "Nlp" ? "ExtractTextForNlp" : "StructuredData",
                    ignoreCase: true,
                    out DataFileIngestionMode mode))
            {
                SetTextImportStatus("Choose a supported format and ingestion mode.");
                return;
            }

            bool dryRun = TextImportDryRunCheckBox.IsChecked == true;
            bool isPlainTextImport = format == DataFileFormat.Text
                || (format == DataFileFormat.Auto
                    && Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".md" or ".log");
            Guid? topicNodeID = null;
            bool useSourceAsTopic = false;
            if (mode == DataFileIngestionMode.ExtractTextForNlp
                && !TryResolveTopicNode(
                    sourceNodeID,
                    path,
                    dryRun,
                    out topicNodeID,
                    out useSourceAsTopic))
            {
                SetTextImportStatus((isPlainTextImport ? "Text import failed: " : "Data-file import failed: ")
                    + TopicSelectionStatusText.Text);
                return;
            }

            long maxFileBytes;

            try
            {
                maxFileBytes = checked(maxMegabytes * 1024L * 1024L);
            }
            catch (OverflowException)
            {
                SetTextImportStatus("Max file MB is too large.");
                return;
            }

            DataFileImportOptions options = new DataFileImportOptions
            {
                FilePath = path,
                Format = format,
                Mode = mode,
                SourceNodeID = sourceNodeID,
                TopicNodeID = topicNodeID,
                UseSourceAsTopic = useSourceAsTopic,
                DryRun = dryRun,
                MaxFileBytes = maxFileBytes,
                MaxRecords = maxRecords
            };

            textImport = new CancellationTokenSource();
            options = options with { CancellationToken = textImport.Token };
            CancellationToken progressToken = options.CancellationToken;
            options = options with
            {
                Progress = progress =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (textImport is null
                            || textImport.Token != progressToken
                            || progressToken.IsCancellationRequested)
                        {
                            return;
                        }

                        TextImportProgressBar.Value = progress.RecordLimit == 0
                            ? 0
                            : Math.Min(100.0, (double)progress.ProcessedRecords / progress.RecordLimit * 100.0);
                        TextImportStatusText.Text = isPlainTextImport
                            ? $"Importing chunk {progress.ProcessedRecords} of {progress.RecordLimit}..."
                            : $"{progress.Stage}: {progress.ProcessedRecords} of {progress.RecordLimit}...";
                    }));
                }
            };

            SetTextImportBusy(true);
            TextImportProgressBar.Value = 0;
            SetTextImportStatus("Preparing import...");

            try
            {
                DataFileImportResult result = await Task.Run(
                    () => DataFileImporter.Import(Core, options));

                if (!result.Cancelled)
                    TextImportProgressBar.Value = 100;

                SetTextImportStatus(FormatDataFileImportSummary(result));
                SetStatus(FormatDataFileImportReport(result));
            }
            catch (OperationCanceledException)
            {
                SetTextImportStatus(isPlainTextImport
                    ? "Text import cancelled. Ingested chunks remain."
                    : "Data-file import cancelled. Completed records remain.");
            }
            catch (Exception ex)
            {
                SetTextImportStatus((isPlainTextImport ? "Text import failed: " : "Data-file import failed: ")
                    + ex.Message);
            }
            finally
            {
                textImport.Dispose();
                textImport = null;
                SetTextImportBusy(false);
            }
        }

        /// <summary>Requests cooperative cancellation of the active file import.</summary>
        private void TextImportCancel_Button_Click(object sender, RoutedEventArgs e)
        {
            if (textImport is null)
                return;

            textImport.Cancel();
            SetTextImportStatus("Cancelling after the current chunk...");
        }

        /// <summary>Seeds the foundational upper ontology into the initialized core.</summary>
        private void SeedOntology_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            try
            {
                int created = OntologyLoader.BuildFoundation(Core);
                string message = created == 0
                    ? "The upper ontology is already present."
                    : $"Seeded the upper ontology with {created} new categories.";
                SetOntologyStatus(message);
                SetStatus(message);
            }
            catch (Exception ex)
            {
                SetOntologyStatus("Ontology seed failed: " + ex.Message);
            }
        }

        /// <summary>Loads the selected GCIDE subset asynchronously with progress and cancellation.</summary>
        private async void LoadGcide_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            if (ontologyLoad is not null)
            {
                SetStatus("A GCIDE load is already running.");
                return;
            }

            OpenFolderDialog dialog = new OpenFolderDialog
            {
                Title = "Select the gcide-0.54 corpus folder",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
                return;

            GcideLoadOptions? options = BuildGcideOptions(dialog.FolderName);

            if (options is null)
                return;

            ontologyLoad = new CancellationTokenSource();
            options.CancellationToken = ontologyLoad.Token;
            options.Progress = (loaded, label) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    OntologyStatusText.Text = $"Loading {loaded} entries ({label})...";
                }));
            };

            SetOntologyBusy(true);

            try
            {
                GcideLoadResult result = await Task.Run(
                    () => OntologyLoader.LoadCorpus(Core, options));

                SetOntologyStatus(
                    $"Loaded {result.EntriesLoaded} of {result.EntriesRead} entries, "
                    + $"{result.WordNodes} words, {result.SenseNodes} senses, "
                    + $"{result.LinksCreated} links, {result.SkippedRelations} skipped relations.");
                SetStatus(FormatGcideReport(result));
            }
            catch (OperationCanceledException)
            {
                SetOntologyStatus("GCIDE load cancelled. Nodes created before cancellation remain.");
            }
            catch (Exception ex)
            {
                SetOntologyStatus("GCIDE load failed: " + ex.Message);
            }
            finally
            {
                ontologyLoad.Dispose();
                ontologyLoad = null;
                SetOntologyBusy(false);
            }
        }

        /// <summary>Requests cancellation of the active ontology corpus load.</summary>
        private void CancelOntology_Button_Click(object sender, RoutedEventArgs e)
        {
            ontologyLoad?.Cancel();
        }

        /// <summary>Exports ontology and lexical graph data to a core log.</summary>
        private void ExportOntology_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCoreInitialized())
                return;

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export Ontology Log",
                Filter = LogFilter,
                DefaultExt = ".lrdlog",
                AddExtension = true,
                FileName = "open-lrd-ontology-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".lrdlog"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                CoreLogExportResult result = CoreLogExporter.Export(
                    Core,
                    dialog.FileName,
                    includeTransient: false);

                SetOntologyStatus(
                    $"Exported {result.NodeCount} nodes and {result.LinkCount} links to "
                    + Path.GetFileName(result.CorePath) + ".");
                SetStatus(FormatExportReport(result));
            }
            catch (Exception ex)
            {
                SetOntologyStatus("Ontology export failed: " + ex.Message);
            }
        }

        /// <summary>Builds GCIDE loader settings from the current UI controls.</summary>
        private GcideLoadOptions? BuildGcideOptions(string directory)
        {
            GcideSubset subset = CorpusSubsetCombo.SelectedIndex switch
            {
                1 => GcideSubset.All,
                2 => GcideSubset.ByLetter,
                _ => GcideSubset.SemanticOnly
            };

            char? letter = null;

            if (subset == GcideSubset.ByLetter)
            {
                string value = CorpusLetterBox.Text.Trim();

                if (value.Length != 1 || !char.IsLetter(value[0]))
                {
                    SetOntologyStatus("Enter a single letter for the single-letter subset.");
                    return null;
                }

                letter = value[0];
            }

            if (!int.TryParse(CorpusMaxEntriesBox.Text.Trim(), out int maxEntries) || maxEntries < 0)
            {
                SetOntologyStatus("Max entries must be zero or a positive whole number.");
                return null;
            }

            return new GcideLoadOptions
            {
                CorpusDirectory = directory,
                Subset = subset,
                Letter = letter,
                MaxEntries = maxEntries,
                CreatePlaceholderTargets = false
            };
        }

        /// <summary>Updates ontology loading state and disables conflicting operations.</summary>
        private void SetOntologyBusy(bool busy)
        {
            SeedOntologyButton.IsEnabled = !busy;
            LoadGcideButton.IsEnabled = !busy;
            ExportOntologyButton.IsEnabled = !busy;
            CancelOntologyButton.IsEnabled = busy;
            OntologyProgressBar.IsIndeterminate = busy;
            TextImportCancelButton.IsEnabled = false;
            SetIngestionControlsEnabled(!busy);
        }

        /// <summary>Sets the ontology status message.</summary>
        private void SetOntologyStatus(string text)
        {
            OntologyStatusText.Text = text;
        }

        /// <summary>Updates file-import busy state and disables conflicting controls.</summary>
        private void SetTextImportBusy(bool busy)
        {
            SetIngestionControlsEnabled(!busy);
            TextImportCancelButton.IsEnabled = busy;

            ImportCoreLogButton.IsEnabled = !busy;
            ImportTransientLogButton.IsEnabled = !busy;
            MergeModeRadio.IsEnabled = !busy;
            ReplaceModeRadio.IsEnabled = !busy;
            ExportLogsButton.IsEnabled = !busy;
            SeedOntologyButton.IsEnabled = !busy;
            LoadGcideButton.IsEnabled = !busy;
            ExportOntologyButton.IsEnabled = !busy;
            TextImportProgressBar.IsIndeterminate = false;
        }

        /// <summary>Sets the file-import status message.</summary>
        private void SetTextImportStatus(string text)
        {
            TextImportStatusText.Text = text;
        }

        /// <summary>Parses an optional non-empty GUID and reports invalid text.</summary>
        private bool TryParseOptionalGuid(string value, string label, out Guid? result)
        {
            result = null;
            string text = value.Trim();

            if (text.Length == 0)
                return true;

            if (!Guid.TryParse(text, out Guid parsed) || parsed == Guid.Empty)
            {
                SetTextImportStatus(label + " must be a non-empty GUID, or blank.");
                return false;
            }

            result = parsed;
            return true;
        }

        /// <summary>Parses a positive integer and reports invalid or out-of-range input.</summary>
        private bool TryParsePositiveInt(string value, string label, out int result)
        {
            if (!int.TryParse(value.Trim(), out result) || result <= 0)
            {
                SetTextImportStatus(label + " must be a positive whole number.");
                return false;
            }

            return true;
        }

        /// <summary>Formats the concise summary for the legacy text importer result.</summary>
        private static string FormatTextImportSummary(TextImportResult result)
        {
            StringBuilder summary = new StringBuilder();

            if (result.DryRun)
            {
                summary.Append($"Dry run: {result.ChunkCount} chunk(s), ");
                summary.Append($"{result.ParagraphCount} paragraph(s), ");
                summary.Append($"{result.SentenceCount} sentence(s), ");
                summary.Append($"{result.TotalCharacters} character(s).");
            }
            else
            {
                summary.Append(
                    $"Processed {result.ProcessedChunks} of {result.ChunkCount} chunk(s): ");
                summary.Append(
                    $"{result.IngestedChunks} ingested, {result.UnsupportedChunks} unsupported, ");
                summary.Append($"{result.FailedChunks} failed.");
            }

            if (result.Truncated)
                summary.Append(" Chunk limit reached; the file remainder was not imported.");

            if (result.Cancelled)
                summary.Append(" Import cancelled.");

            return summary.ToString();
        }

        /// <summary>Formats the concise status summary for a multi-format file import.</summary>
        private static string FormatDataFileImportSummary(DataFileImportResult result)
        {
            StringBuilder summary = new StringBuilder();
            if (result.Format == DataFileFormat.Text)
            {
                if (result.FailedFatally)
                {
                    string failure = result.Diagnostics.FirstOrDefault() ?? "The file could not be imported.";
                    if (failure.StartsWith("Import stopped: ", StringComparison.Ordinal))
                        failure = failure["Import stopped: ".Length..];
                    return "Text import failed: " + failure;
                }

                if (result.DryRun)
                {
                    summary.Append($"Dry run: {result.TextChunkCount} chunk(s), ");
                    summary.Append($"{result.TextParagraphs} paragraph(s), ");
                    summary.Append($"{result.TextSentences} sentence(s), ");
                    summary.Append($"{result.TextCharacters} character(s).");
                }
                else
                {
                    summary.Append(
                        $"Processed {result.RecordsProcessed} of {result.TextChunkCount} chunk(s): ");
                    summary.Append(
                        $"{result.TextIngestedChunks} ingested, {result.RecordsUnsupported} unsupported, ");
                    summary.Append($"{result.RecordsFailed} failed.");
                }
                if (result.Truncated)
                    summary.Append(" Chunk limit reached; the file remainder was not imported.");
                if (result.Cancelled)
                    summary.Append(" Import cancelled.");
                return summary.ToString();
            }

            summary.Append(result.DryRun ? "Dry run: " : "Processed ");
            summary.Append($"{result.RecordsProcessed} record(s): ");
            summary.Append($"{result.RecordsSucceeded} succeeded, ");
            summary.Append($"{result.RecordsUnsupported} unsupported, ");
            summary.Append($"{result.RecordsFailed} failed.");
            if (result.NodesCreated > 0 || result.LinksCreated > 0)
                summary.Append($" Created {result.NodesCreated} node(s) and {result.LinksCreated} link(s).");
            if (result.SkippedHighTrust)
                summary.Append(" Unchanged high-trust source; import skipped.");
            if (result.Truncated)
                summary.Append(" Import limit reached; remaining records were skipped.");
            if (result.Cancelled)
                summary.Append(" Import cancelled; completed records remain.");
            if (result.FailedFatally)
                summary.Append(" Import stopped after a fatal error.");
            return summary.ToString();
        }

        /// <summary>Builds a detailed report for a multi-format file import.</summary>
        private static string FormatDataFileImportReport(DataFileImportResult result)
        {
            StringBuilder report = new StringBuilder();
            if (result.Format == DataFileFormat.Text)
            {
                report.AppendLine(result.FailedFatally
                    ? "Text import failed."
                    : result.DryRun ? "Text import dry run." : "Text import complete.");
                report.AppendLine("File: " + result.FilePath);
                report.AppendLine($"Characters: {result.TextCharacters}");
                report.AppendLine($"Paragraphs: {result.TextParagraphs}");
                report.AppendLine($"Sentences: {result.TextSentences}");
                report.AppendLine($"Chunks: {result.TextChunkCount}");
                report.AppendLine($"Processed: {result.RecordsProcessed}");
                if (!result.DryRun)
                {
                    report.AppendLine($"Ingested: {result.TextIngestedChunks}");
                    report.AppendLine($"Unsupported: {result.RecordsUnsupported}");
                    report.AppendLine($"Failed: {result.RecordsFailed}");
                    report.AppendLine($"Skipped: {result.TextSkippedChunks}");
                    report.AppendLine("Conversation: " + (result.ConversationID?.ToString() ?? "(none)"));
                    report.AppendLine("Source node: " + (result.SourceNodeID?.ToString() ?? "(none)")
                        + (result.SourceNodeCreated ? " (created)" : string.Empty));
                }
                if (result.Truncated)
                    report.AppendLine("The chunk limit was reached; the remainder of the file was not imported.");
                if (result.Cancelled)
                    report.AppendLine("The import was cancelled; ingested chunks remain.");
                AppendMessages(report, "Notes", result.Diagnostics);
                return report.ToString().TrimEnd();
            }

            report.AppendLine(result.DryRun ? "Data-file import dry run." : "Data-file import complete.");
            report.AppendLine("File: " + result.FilePath);
            report.AppendLine($"Format: {result.Format}");
            report.AppendLine($"Mode: {result.Mode}");
            report.AppendLine($"Records processed: {result.RecordsProcessed}");
            report.AppendLine($"Records succeeded: {result.RecordsSucceeded}");
            report.AppendLine($"Records unsupported: {result.RecordsUnsupported}");
            report.AppendLine($"Records failed: {result.RecordsFailed}");
            report.AppendLine($"Nodes created: {result.NodesCreated}");
            report.AppendLine($"Links created: {result.LinksCreated}");
            report.AppendLine("Source node: " + (result.SourceNodeID?.ToString() ?? "(none)"));
            report.AppendLine("Source trust: " + (result.SourceTrustRating?.ToString("P0") ?? "(not updated)"));
            report.AppendLine("Conversation: " + (result.ConversationID?.ToString() ?? "(none)"));
            if (result.Truncated)
                report.AppendLine("An import limit was reached; remaining records were skipped.");
            if (result.Cancelled)
                report.AppendLine("The import was cancelled; completed records remain.");
            if (result.FailedFatally)
                report.AppendLine("The import stopped after a fatal error.");
            foreach (string diagnostic in result.Diagnostics)
                report.AppendLine("- " + diagnostic);
            return report.ToString();
        }

        /// <summary>Builds a detailed report for a text-file import.</summary>
        private static string FormatTextImportReport(TextImportResult result)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine(result.DryRun ? "Text import dry run." : "Text import complete.");
            report.AppendLine("File: " + result.FilePath);
            report.AppendLine($"Characters: {result.TotalCharacters}");
            report.AppendLine($"Paragraphs: {result.ParagraphCount}");
            report.AppendLine($"Sentences: {result.SentenceCount}");
            report.AppendLine($"Chunks: {result.ChunkCount}");
            report.AppendLine($"Processed: {result.ProcessedChunks}");

            if (!result.DryRun)
            {
                report.AppendLine($"Ingested: {result.IngestedChunks}");
                report.AppendLine($"Unsupported: {result.UnsupportedChunks}");
                report.AppendLine($"Failed: {result.FailedChunks}");
                report.AppendLine($"Skipped: {result.SkippedChunks}");
                report.AppendLine(
                    "Conversation: " + (result.ConversationID?.ToString() ?? "(none)"));
                report.AppendLine(
                    "Source node: " + (result.SourceNodeID?.ToString() ?? "(none)")
                    + (result.SourceNodeCreated ? " (created)" : string.Empty));
            }

            if (result.Truncated)
            {
                report.AppendLine(
                    "The chunk limit was reached; the remainder of the file was not imported.");
            }

            if (result.Cancelled)
                report.AppendLine("The import was cancelled; ingested chunks remain.");

            AppendMessages(report, "Notes", result.Diagnostics);
            return report.ToString().TrimEnd();
        }

        /// <summary>Builds a detailed completion report for a GCIDE load.</summary>
        private static string FormatGcideReport(GcideLoadResult result)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("GCIDE load complete.");
            report.AppendLine($"Entries read: {result.EntriesRead}");
            report.AppendLine($"Entries loaded: {result.EntriesLoaded}");
            report.AppendLine($"Word nodes created: {result.WordNodes}");
            report.AppendLine($"Sense nodes created: {result.SenseNodes}");
            report.AppendLine($"Links created: {result.LinksCreated}");
            report.AppendLine($"Unresolved relations skipped: {result.SkippedRelations}");
            report.AppendLine(
                "Export the core log to persist the load, or import a previous ontology log later.");
            return report.ToString().TrimEnd();
        }

        /// <summary>Returns the selected core-log merge or replace policy.</summary>
        private ImportMode GetSelectedImportMode()
        {
            return ReplaceModeRadio.IsChecked == true
                ? ImportMode.Replace
                : ImportMode.Merge;
        }

        /// <summary>Confirms the consequences of importing a core log with the selected policy.</summary>
        private bool ConfirmCoreImport(ImportMode mode)
        {
            string message = mode == ImportMode.Replace
                ? "Replace will discard the current graph, conversations, transient chains, "
                    + "and pipeline attachments. Open workbench sessions will become stale.\n\n"
                    + "Continue?"
                : "Merge keeps existing nodes, links, conversations, and type definitions "
                    + "when IDs conflict. Non-conflicting imported items are added.\n\n"
                    + "Continue?";
            MessageBoxImage image = mode == ImportMode.Replace
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information;

            return MessageBox.Show(
                this,
                message,
                "Import Core Log",
                MessageBoxButton.OKCancel,
                image) == MessageBoxResult.OK;
        }

        /// <summary>Confirms the consequences of importing transient chains with the selected policy.</summary>
        private bool ConfirmTransientImport(ImportMode mode)
        {
            string message = mode == ImportMode.Replace
                ? "Replace will clear the current query and response chains and restore "
                    + "the imported chains.\n\nContinue?"
                : "Merge appends imported query and response chains. Exact duplicate chains "
                    + "are skipped and existing chains remain.\n\nContinue?";

            return MessageBox.Show(
                this,
                message,
                "Import Transient Log",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information) == MessageBoxResult.OK;
        }

        /// <summary>Opens a log-file picker and returns the chosen path.</summary>
        private string? SelectLogPath(string title)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = title,
                Filter = LogFilter,
                DefaultExt = ".lrdlog",
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog(this) == true ? dialog.FileName : null;
        }

        /// <summary>Checks that the core is initialized and displays a launch prompt when needed.</summary>
        private bool EnsureCoreInitialized()
        {
            if (Core.initialized)
                return true;

            SetStatus("Launch Core before using Tools.");
            return false;
        }

        /// <summary>Updates the Tools window's shared status report.</summary>
        private void SetStatus(string text)
        {
            StatusBox.Text = text;
        }

        /// <summary>Formats the outcome of a core and transient log export.</summary>
        private static string FormatExportReport(CoreLogExportResult result)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Export complete.");
            report.AppendLine("Core log: " + result.CorePath);

            if (result.IsSplit)
            {
                report.AppendLine(
                    $"Segmented export: {result.CorePartCount} core part(s), " +
                    $"{result.TransientPartCount} transient part(s).");
            }

            report.AppendLine($"Total bytes written: {result.TotalBytes}");

            if (result.TransientPath is not null)
                report.AppendLine("Transient log: " + result.TransientPath);

            report.AppendLine($"Nodes: {result.NodeCount}");
            report.AppendLine($"Links: {result.LinkCount}");
            report.AppendLine($"Conversations: {result.ConversationCount}");

            if (result.TransientPath is not null)
            {
                report.AppendLine($"Query chains: {result.QueryChainCount}");
                report.AppendLine($"Response chains: {result.ResponseChainCount}");
            }

            AppendMessages(report, "Warnings", result.Warnings);
            return report.ToString().TrimEnd();
        }

        /// <summary>Formats a persistence import result for display.</summary>
        private static string FormatImportReport(
            string action,
            PersistenceImportResult result,
            bool isTransient)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine(action + (result.Success ? " completed." : " failed."));
            report.AppendLine("Mode: " + result.Mode);

            if (isTransient)
            {
                report.AppendLine($"Query chains added: {result.QueryChainsAdded}");
                report.AppendLine($"Response chains added: {result.ResponseChainsAdded}");
                report.AppendLine($"Chains skipped: {result.ChainsSkipped}");
            }
            else
            {
                report.AppendLine($"Nodes: {result.NodesAdded} added, {result.NodesSkipped} skipped");
                report.AppendLine($"Links: {result.LinksAdded} added, {result.LinksSkipped} skipped");
                report.AppendLine(
                    $"Conversations: {result.ConversationsAdded} added, "
                    + $"{result.ConversationsSkipped} skipped");
                report.AppendLine(
                    $"Types: {result.TypesRegistered} registered, "
                    + $"{result.TypesSkipped} kept or skipped");
            }

            AppendMessages(report, "Warnings", result.Warnings);
            AppendMessages(report, "Errors", result.Errors);

            if (result.Success && !isTransient && result.Mode == ImportMode.Replace)
            {
                report.AppendLine(
                    "Replaced core state invalidates open workbench sessions; "
                    + "refresh or reattach them.");
            }

            return report.ToString().TrimEnd();
        }

        /// <summary>Appends a labeled list of diagnostic messages to a report.</summary>
        private static void AppendMessages(
            StringBuilder report,
            string label,
            IReadOnlyList<string> messages)
        {
            if (messages.Count == 0)
                return;

            report.AppendLine();
            report.AppendLine(label + ":");

            foreach (string message in messages)
                report.AppendLine("- " + message);
        }
    }
}
