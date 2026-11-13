using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using OPEN_LRD.NLP;
using OPEN_LRD.Ontology;
using OPEN_LRD.Ontology.Gcide;
using OPEN_LRD.Persistence;

namespace OPEN_LRD
{
    public partial class Tools : Window
    {
        private const string LogFilter =
            "OPEN-LRD logs (*.lrdlog)|*.lrdlog|JSON files (*.json)|*.json|All files (*.*)|*.*";

        private const string TextFileFilter =
            "Text files (*.txt;*.md;*.log)|*.txt;*.md;*.log|All files (*.*)|*.*";

        private static LRD_Core Core => LRD_Core.Instance;

        private CancellationTokenSource? ontologyLoad;
        private CancellationTokenSource? textImport;

        public Tools()
        {
            InitializeComponent();
        }

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

        private void TextImportBrowse_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Select a text file to import",
                Filter = TextFileFilter,
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true)
                TextImportPathBox.Text = dialog.FileName;
        }

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

            if (!TryParseOptionalGuid(TextImportSourceBox.Text, "Source ID", out Guid? sourceNodeID)
                || !TryParseOptionalGuid(TextImportTopicBox.Text, "Topic ID", out Guid? topicNodeID)
                || !TryParsePositiveInt(TextImportMaxMegabytesBox.Text, "Max file MB", out int maxMegabytes)
                || !TryParsePositiveInt(TextImportMaxChunksBox.Text, "Max chunks", out int maxChunks))
            {
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

            TextImportOptions options = new TextImportOptions
            {
                FilePath = path,
                SourceNodeID = sourceNodeID,
                TopicNodeID = topicNodeID,
                DryRun = TextImportDryRunCheckBox.IsChecked == true,
                MaxFileBytes = maxFileBytes,
                MaxChunks = maxChunks
            };

            textImport = new CancellationTokenSource();
            options = options with { CancellationToken = textImport.Token };
            options = options with
            {
                Progress = progress =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        TextImportProgressBar.Value = progress.TotalChunks == 0
                            ? 0
                            : (double)progress.ProcessedChunks / progress.TotalChunks * 100.0;
                        TextImportStatusText.Text =
                            $"Importing chunk {progress.ProcessedChunks} of {progress.TotalChunks}...";
                    }));
                }
            };

            SetTextImportBusy(true);
            TextImportProgressBar.Value = 0;
            SetTextImportStatus("Preparing import...");

            try
            {
                TextImportResult result = await Task.Run(
                    () => TextFileImporter.Import(Core, options));

                if (!result.Cancelled)
                    TextImportProgressBar.Value = 100;

                SetTextImportStatus(FormatTextImportSummary(result));
                SetStatus(FormatTextImportReport(result));
            }
            catch (OperationCanceledException)
            {
                SetTextImportStatus("Text import cancelled. Ingested chunks remain.");
            }
            catch (Exception ex)
            {
                SetTextImportStatus("Text import failed: " + ex.Message);
            }
            finally
            {
                textImport.Dispose();
                textImport = null;
                SetTextImportBusy(false);
            }
        }

        private void TextImportCancel_Button_Click(object sender, RoutedEventArgs e)
        {
            if (textImport is null)
                return;

            textImport.Cancel();
            SetTextImportStatus("Cancelling after the current chunk...");
        }

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

        private void CancelOntology_Button_Click(object sender, RoutedEventArgs e)
        {
            ontologyLoad?.Cancel();
        }

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

        private void SetOntologyBusy(bool busy)
        {
            SeedOntologyButton.IsEnabled = !busy;
            LoadGcideButton.IsEnabled = !busy;
            ExportOntologyButton.IsEnabled = !busy;
            CancelOntologyButton.IsEnabled = busy;
            OntologyProgressBar.IsIndeterminate = busy;
            TextImportButton.IsEnabled = !busy;
            TextImportBrowseButton.IsEnabled = !busy;
        }

        private void SetOntologyStatus(string text)
        {
            OntologyStatusText.Text = text;
        }

        private void SetTextImportBusy(bool busy)
        {
            TextImportButton.IsEnabled = !busy;
            TextImportBrowseButton.IsEnabled = !busy;
            TextImportPathBox.IsEnabled = !busy;
            TextImportSourceBox.IsEnabled = !busy;
            TextImportTopicBox.IsEnabled = !busy;
            TextImportDryRunCheckBox.IsEnabled = !busy;
            TextImportMaxMegabytesBox.IsEnabled = !busy;
            TextImportMaxChunksBox.IsEnabled = !busy;
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

        private void SetTextImportStatus(string text)
        {
            TextImportStatusText.Text = text;
        }

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

        private bool TryParsePositiveInt(string value, string label, out int result)
        {
            if (!int.TryParse(value.Trim(), out result) || result <= 0)
            {
                SetTextImportStatus(label + " must be a positive whole number.");
                return false;
            }

            return true;
        }

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

        private ImportMode GetSelectedImportMode()
        {
            return ReplaceModeRadio.IsChecked == true
                ? ImportMode.Replace
                : ImportMode.Merge;
        }

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

        private bool EnsureCoreInitialized()
        {
            if (Core.initialized)
                return true;

            SetStatus("Launch Core before importing or exporting logs.");
            return false;
        }

        private void SetStatus(string text)
        {
            StatusBox.Text = text;
        }

        private static string FormatExportReport(CoreLogExportResult result)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("Export complete.");
            report.AppendLine("Core log: " + result.CorePath);

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
