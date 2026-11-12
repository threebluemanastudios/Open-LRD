using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OPEN_LRD.NLP;
using OPEN_LRD.Reasoning;

namespace OPEN_LRD
{
    public sealed record CandidateRow(
        string Label,
        string NodeType,
        string Score,
        string Depth,
        string RelationLinkType,
        string LinkClass,
        string Temporary,
        string PathNodeIDs);

    public sealed record FrameRow(string Kind, string Summary);

    public sealed record MentionRow(
        string Sequence,
        string Turn,
        string NodeID,
        string Label,
        string Surface,
        string NodeType,
        string Role,
        string FromAnswer);

    public sealed record ProvenanceRow(
        string Sequence,
        string Turn,
        string Sentence,
        string SourceText,
        string Status,
        string Relation,
        string LinkClass,
        string SubjectNodeID,
        string ObjectNodeID,
        string QualifierNodeID,
        string TrustSourceNodeID,
        string TopicNodeID,
        string TemporaryNodeCount,
        string GraphMutated,
        string UsableForAnswer);

    public sealed record FocusRow(string Field, string Value);

    public partial class NlpWorkbenchWindow : Window
    {
        private readonly NlpWorkbench workbench;

        public NlpWorkbenchWindow()
        {
            InitializeComponent();
            workbench = new NlpWorkbench(LRD_Core.Instance);

            string initialStatus;
            if (!workbench.IsCoreInitialized)
            {
                initialStatus = "Launch Core before using the NLP Workbench.";
            }
            else if (workbench.StartNewConversation())
            {
                initialStatus = "New conversation created.";
            }
            else
            {
                initialStatus = "No conversation session is active.";
            }

            ClearResultDisplay();
            RefreshWorkbench(initialStatus);
        }

        protected override void OnClosed(EventArgs e)
        {
            workbench.Dispose();
            base.OnClosed(e);
        }

        private void Ingest_Button_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadOptionalGuid(IngestSourceBox, "Source Node ID", out Guid? sourceNodeID, out string? sourceError))
            {
                SetStatus(sourceError!);
                return;
            }

            if (!TryReadOptionalGuid(IngestTopicBox, "Topic Node ID", out Guid? topicNodeID, out string? topicError))
            {
                SetStatus(topicError!);
                return;
            }

            ExecuteOperation(
                "Ingest",
                () => workbench.Ingest(IngestInput.Text, sourceNodeID, topicNodeID));
        }

        private void Ask_Button_Click(object sender, RoutedEventArgs e)
        {
            ExecuteOperation("Ask", () => workbench.Ask(AskInput.Text));
        }

        private void NewConversation_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!workbench.StartNewConversation())
                {
                    SetStatus("Launch Core before starting a conversation.");
                    return;
                }

                ClearResultDisplay();
                RefreshWorkbench("New conversation created.");
            }
            catch (Exception ex)
            {
                ShowOperationError("New Conversation", ex);
            }
        }

        private void AttachConversation_Button_Click(object sender, RoutedEventArgs e)
        {
            string text = AttachConversationBox.Text.Trim();

            if (!Guid.TryParse(text, out Guid conversationID) || conversationID == Guid.Empty)
            {
                SetStatus("Attach ID must be a non-empty GUID.");
                return;
            }

            try
            {
                if (!workbench.TryAttachConversation(conversationID, out string? error))
                {
                    SetStatus(error ?? "The conversation could not be attached.");
                    return;
                }

                ClearResultDisplay();
                RefreshWorkbench("Conversation attached.");
            }
            catch (Exception ex)
            {
                ShowOperationError("Attach Conversation", ex);
            }
        }

        private void ResetConversation_Button_Click(object sender, RoutedEventArgs e)
        {
            if (workbench.State?.HasSession != true)
            {
                SetStatus("There is no current conversation to reset.");
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "Reset the current conversation memory? Graph data will not be deleted.",
                "Reset Conversation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmation != MessageBoxResult.Yes)
                return;

            try
            {
                bool reset = workbench.ResetCurrentConversation();
                ClearResultDisplay();
                RefreshWorkbench(reset ? "Conversation reset." : "The conversation was no longer registered.");
            }
            catch (Exception ex)
            {
                ShowOperationError("Reset Conversation", ex);
            }
        }

        private void RemoveConversation_Button_Click(object sender, RoutedEventArgs e)
        {
            if (workbench.State?.HasSession != true)
            {
                SetStatus("There is no current conversation to remove.");
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "Remove the current conversation? It cannot be recovered. Graph data is unchanged.",
                "Remove Conversation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            try
            {
                bool removed = workbench.RemoveCurrentConversation();
                ClearResultDisplay();
                RefreshWorkbench(removed ? "Conversation removed." : "The conversation was no longer registered.");
            }
            catch (Exception ex)
            {
                ShowOperationError("Remove Conversation", ex);
            }
        }

        private void ClearConversations_Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "Clear all conversations? All sessions will be removed. Graph data is unchanged.",
                "Clear All Conversations",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
                return;

            try
            {
                workbench.ClearAllConversations();
                ClearResultDisplay();
                RefreshWorkbench("All conversations cleared.");
            }
            catch (Exception ex)
            {
                ShowOperationError("Clear All Conversations", ex);
            }
        }

        private void CopyConversation_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Guid? conversationID = workbench.ConversationID;

                if (!conversationID.HasValue)
                {
                    SetStatus("There is no conversation ID to copy.");
                    return;
                }

                Clipboard.SetText(conversationID.Value.ToString());
                SetStatus("Conversation ID copied.");
            }
            catch (Exception ex)
            {
                SetStatus($"The conversation ID could not be copied: {ex.Message}");
            }
        }

        private void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshWorkbench("State refreshed.");
            }
            catch (Exception ex)
            {
                ShowOperationError("Refresh", ex);
            }
        }

        private void ExecuteOperation(string operation, Func<NlpResult> action)
        {
            if (workbench.State?.HasSession != true)
            {
                SetStatus("Start or attach a conversation before running an operation.");
                return;
            }

            try
            {
                NlpResult result = action();
                RefreshWorkbench();
                ShowResult(operation, result);
                SetStatus($"{operation} completed.");
            }
            catch (Exception ex)
            {
                ShowOperationError(operation, ex);
            }
        }

        private void ShowResult(string operation, NlpResult result)
        {
            string answerStatus = result.Answer is null
                ? result.Status.ToString()
                : $"{result.Status} / {result.Answer.Status}";

            OperationStatusText.Text = $"{operation}: {answerStatus}";
            AnswerText.Text = result.Answer?.Text ?? BuildIngestSummary(result);
            ShortAnswerText.Text = result.Answer?.ShortAnswer ?? "(none)";
            ConfidenceText.Text = result.Answer is null
                ? "Confidence: (n/a)"
                : $"Confidence: {result.Answer.Confidence.ToString("F2", CultureInfo.InvariantCulture)}";

            bool temporaryEvidence = result.Answer?.UsesTemporaryEvidence
                ?? result.Provenance.Any(entry => entry.TemporaryNodeIDs.Count > 0);
            bool sessionProvenance = result.Answer?.UsesSessionProvenance
                ?? result.Provenance.Count > 0;
            bool graphMutated = result.Provenance.Any(entry => entry.GraphMutated);
            EvidenceFlagsText.Text =
                $"Temporary evidence: {temporaryEvidence}   Session provenance: {sessionProvenance}   Graph mutated: {graphMutated}";

            DiagnosticsList.ItemsSource = result.Diagnostics.ToArray();
            CandidatesGrid.ItemsSource = result.Answer is null
                ? Array.Empty<CandidateRow>()
                : result.Answer.Candidates.Select(ToCandidateRow).ToArray();
            FramesGrid.ItemsSource = BuildFrameRows(result);
        }

        private static string BuildIngestSummary(NlpResult result)
        {
            if (result.Provenance.Count > 0)
                return $"{result.Status}. {result.Provenance.Count} provenance entries recorded.";

            if (result.Frames.Count > 0)
                return $"{result.Status}. {result.Frames.Count} relation frames and {result.Chains.Count} chains.";

            return result.Status.ToString();
        }

        private static CandidateRow ToCandidateRow(AnswerCandidate candidate)
        {
            return new CandidateRow(
                candidate.Label,
                candidate.NodeType.ToString(),
                candidate.Score.ToString("F2", CultureInfo.InvariantCulture),
                candidate.Depth.ToString(CultureInfo.InvariantCulture),
                candidate.RelationLinkTypeId.ToString(CultureInfo.InvariantCulture),
                candidate.LinkClass.ToString(),
                candidate.UsesTemporaryEvidence.ToString(),
                string.Join(", ", candidate.PathNodeIDs));
        }

        private static IReadOnlyList<FrameRow> BuildFrameRows(NlpResult result)
        {
            List<FrameRow> rows = new List<FrameRow>();

            foreach (RelationFrame frame in result.Frames)
            {
                string arguments = string.Join(
                    "; ",
                    frame.Arguments.Select(argument => $"{argument.Role}: {argument.SurfaceText}"));
                rows.Add(new FrameRow(
                    "Frame",
                    $"{frame.RelationName} ({frame.LinkClass}) [{arguments}] ingestible={frame.Ingestible}"));
            }

            foreach (LinkedList<Nodes.Chain_Node> chain in result.Chains)
            {
                string path = string.Join(
                    " -> ",
                    chain.Select(node => node.Label ?? node.ID.ToString()));
                rows.Add(new FrameRow("Chain", path));
            }

            if (rows.Count == 0)
                rows.Add(new FrameRow("Frames", "No frames or chains."));

            return rows;
        }

        private void RefreshWorkbench(string? status = null)
        {
            NlpWorkbenchState state = workbench.RefreshState();
            bool coreReady = state.CoreInitialized;
            bool hasSession = state.HasSession;

            CoreStatusText.Text = coreReady ? "Core: Initialized" : "Core: Not initialized";
            ConversationIDText.Text = FormatGuid(state.ConversationID);
            PipelineIDText.Text = FormatGuid(state.PipelineID);
            TurnIndexText.Text = $"Turns: {state.TurnIndex.ToString(CultureInfo.InvariantCulture)}";
            GraphCountsText.Text =
                $"Nodes: {state.GraphNodeCount.ToString(CultureInfo.InvariantCulture)}   " +
                $"Directed Links: {state.GraphLinkCount.ToString(CultureInfo.InvariantCulture)}";

            CopyConversationButton.IsEnabled = hasSession;
            NewConversationButton.IsEnabled = coreReady;
            AttachConversationBox.IsEnabled = coreReady;
            AttachConversationButton.IsEnabled = coreReady;
            ClearConversationsButton.IsEnabled = coreReady;
            RefreshButton.IsEnabled = true;

            ResetConversationButton.IsEnabled = hasSession;
            RemoveConversationButton.IsEnabled = hasSession;
            IngestInput.IsEnabled = hasSession;
            IngestSourceBox.IsEnabled = hasSession;
            IngestTopicBox.IsEnabled = hasSession;
            IngestButton.IsEnabled = hasSession;
            AskInput.IsEnabled = hasSession;
            AskButton.IsEnabled = hasSession;

            MentionsGrid.ItemsSource = state.Mentions.Select(ToMentionRow).ToArray();
            ProvenanceGrid.ItemsSource = state.Provenance.Select(ToProvenanceRow).ToArray();
            FocusGrid.ItemsSource = BuildFocusRows(state);

            if (status is not null)
                SetStatus(status);
        }

        private void ClearResultDisplay()
        {
            OperationStatusText.Text = "No operation yet.";
            AnswerText.Text = string.Empty;
            ShortAnswerText.Text = string.Empty;
            ConfidenceText.Text = "Confidence: (n/a)";
            EvidenceFlagsText.Text = "Temporary evidence: False   Session provenance: False";
            DiagnosticsList.ItemsSource = Array.Empty<string>();
            CandidatesGrid.ItemsSource = Array.Empty<CandidateRow>();
            FramesGrid.ItemsSource = Array.Empty<FrameRow>();
        }

        private static MentionRow ToMentionRow(ConversationMention mention)
        {
            return new MentionRow(
                mention.Sequence.ToString(CultureInfo.InvariantCulture),
                mention.TurnIndex.ToString(CultureInfo.InvariantCulture),
                mention.NodeID.ToString(),
                mention.Label,
                mention.SurfaceText,
                mention.NodeType.ToString(),
                mention.Role.ToString(),
                mention.FromAnswer.ToString());
        }

        private static ProvenanceRow ToProvenanceRow(SessionProvenanceEntry entry)
        {
            return new ProvenanceRow(
                entry.Sequence.ToString(CultureInfo.InvariantCulture),
                entry.TurnIndex.ToString(CultureInfo.InvariantCulture),
                entry.SentenceIndex?.ToString(CultureInfo.InvariantCulture) ?? "(none)",
                entry.SourceText,
                entry.Status.ToString(),
                entry.RelationName ?? "(none)",
                entry.LinkClass.ToString(),
                FormatGuid(entry.SubjectNodeID),
                FormatGuid(entry.ObjectNodeID),
                FormatGuid(entry.QualifierNodeID),
                FormatGuid(entry.TrustSourceNodeID),
                FormatGuid(entry.TopicNodeID),
                entry.TemporaryNodeIDs.Count.ToString(CultureInfo.InvariantCulture),
                entry.GraphMutated.ToString(),
                entry.IsUsableForAnswer.ToString());
        }

        private static IReadOnlyList<FocusRow> BuildFocusRows(NlpWorkbenchState state)
        {
            return new[]
            {
                new FocusRow("Focus Node ID", FormatGuid(state.FocusNodeID)),
                new FocusRow("Last Subject Node ID", FormatGuid(state.LastSubjectNodeID)),
                new FocusRow("Last Object Node ID", FormatGuid(state.LastObjectNodeID)),
                new FocusRow("Last Qualifier Node ID", FormatGuid(state.LastQualifierNodeID)),
                new FocusRow("Last Answer Node ID", FormatGuid(state.LastAnswerNodeID))
            };
        }

        private bool TryReadOptionalGuid(
            TextBox box,
            string fieldName,
            out Guid? value,
            out string? error)
        {
            value = null;
            error = null;
            string text = box.Text.Trim();

            if (text.Length == 0)
                return true;

            if (!Guid.TryParse(text, out Guid parsed) || parsed == Guid.Empty)
            {
                error = $"{fieldName} must be a non-empty GUID.";
                return false;
            }

            if (!LRD_Core.Instance.TryGetNode(parsed, out _))
            {
                error = $"{fieldName} '{parsed}' was not found in the graph.";
                return false;
            }

            value = parsed;
            return true;
        }

        private static string FormatGuid(Guid? value)
        {
            return value?.ToString() ?? "(none)";
        }

        private void ShowOperationError(string operation, Exception exception)
        {
            SetStatus($"{operation} failed: {exception.Message}");
            MessageBox.Show(
                this,
                exception.Message,
                $"{operation} Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            RefreshWorkbench();
        }

        private void SetStatus(string message)
        {
            WorkbenchStatusBar.Text = message;
        }
    }
}
