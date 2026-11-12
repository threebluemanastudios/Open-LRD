using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using OPEN_LRD.Persistence;

namespace OPEN_LRD
{
    public partial class Tools : Window
    {
        private const string LogFilter =
            "OPEN-LRD logs (*.lrdlog)|*.lrdlog|JSON files (*.json)|*.json|All files (*.*)|*.*";

        private static LRD_Core Core => LRD_Core.Instance;

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
