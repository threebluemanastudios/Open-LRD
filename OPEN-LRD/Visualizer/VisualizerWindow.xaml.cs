using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
    public partial class VisualizerWindow : Window
    {
        private readonly LRD_Core core;
        private readonly VisualizerOptions options = new VisualizerOptions();
        private readonly GraphColorMap colors = new GraphColorMap();
        private readonly VisualizerScene scene;

        private CancellationTokenSource? layoutCancellation;
        private GraphSnapshot? snapshot;
        private SceneGeometry? geometry;
        private bool isLoading;
        private bool isClosed;

        public VisualizerWindow()
            : this(LRD_Core.Instance)
        {
        }

        internal VisualizerWindow(LRD_Core core)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            InitializeComponent();
            scene = new VisualizerScene(GraphViewport);
            UpdateCommands();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // WindowStyle=None plus Maximized makes the window cover the active
            // monitor, including the taskbar area.
            WindowState = WindowState.Maximized;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!core.initialized)
            {
                ShowEmptyState(true);
                SetStatus("Launch Core before opening the Visualizer.");
                UpdateCommands();
                return;
            }

            await RefreshGraphAsync();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;
            Close();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            isClosed = true;
            layoutCancellation?.Cancel();
            layoutCancellation?.Dispose();
            layoutCancellation = null;
            scene.Clear();
        }

        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            await RefreshGraphAsync();
        }

        private async void Recompute_Button_Click(object sender, RoutedEventArgs e)
        {
            if (snapshot is null)
            {
                SetStatus("Refresh the graph before recomputing the layout.");
                return;
            }

            await RunLayoutAsync(snapshot, updateSnapshotSummary: false);
        }

        private void FrameAll_Button_Click(object sender, RoutedEventArgs e)
        {
            scene.FrameAll();
            SetStatus("Framed all nodes.");
        }

        private void ResetView_Button_Click(object sender, RoutedEventArgs e)
        {
            scene.ResetView();
            SetStatus("View reset.");
        }

        private void Close_Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task RefreshGraphAsync()
        {
            if (isClosed)
                return;

            if (!core.initialized)
            {
                ShowEmptyState(true);
                SetStatus("Launch Core before opening the Visualizer.");
                UpdateCommands();
                return;
            }

            GraphSnapshot fresh;

            try
            {
                fresh = GraphSnapshot.FromCore(core, options);
            }
            catch (Exception ex)
            {
                SetStatus($"The graph could not be read: {ex.Message}");
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Visualizer Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            await RunLayoutAsync(fresh, updateSnapshotSummary: true);
        }

        private async Task RunLayoutAsync(GraphSnapshot fresh, bool updateSnapshotSummary)
        {
            if (isClosed)
                return;

            CancelLayout();

            CancellationTokenSource cancellation = new CancellationTokenSource();
            layoutCancellation = cancellation;
            CancellationToken token = cancellation.Token;

            snapshot = fresh;

            if (updateSnapshotSummary)
                UpdateSnapshotSummary(fresh);

            UpdateLayoutSummary(null);
            UpdateCommands();

            if (fresh.Nodes.Count == 0)
            {
                geometry = null;
                scene.Clear();
                ShowEmptyState(true);
                SetStatus("No nodes to display. Launch Core and add or load knowledge first.");
                FinishLayout(cancellation);
                return;
            }

            ShowEmptyState(false);
            SetLoading(true);
            Progress<LayoutProgress> progress = new Progress<LayoutProgress>(UpdateProgress);

            try
            {
                SceneGeometry? built = await Task.Run<SceneGeometry?>(() =>
                {
                    LayoutResult layout = DistanceLayout3D.Compute(fresh, options, token, progress);
                    token.ThrowIfCancellationRequested();

                    return layout.Cancelled
                        ? null
                        : VisualizerSceneBuilder.Build(fresh, layout, options);
                }, token);

                if (isClosed || token.IsCancellationRequested)
                    return;

                if (built is null)
                {
                    SetStatus("Layout cancelled.");
                    return;
                }

                geometry = built;
                scene.Apply(built, colors);
                UpdateLayoutSummary(built.Layout);
                SetStatus(
                    $"Showing {built.NodeCount.ToString("N0", CultureInfo.InvariantCulture)} nodes and " +
                    $"{built.LinkCount.ToString("N0", CultureInfo.InvariantCulture)} links.");
            }
            catch (OperationCanceledException)
            {
                if (!isClosed)
                    SetStatus("Layout cancelled.");
            }
            catch (Exception ex)
            {
                if (isClosed)
                    return;

                SetStatus($"The visualizer could not build the scene: {ex.Message}");
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Visualizer Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                FinishLayout(cancellation);
            }
        }

        private void FinishLayout(CancellationTokenSource cancellation)
        {
            if (!isClosed)
                SetLoading(false);

            if (ReferenceEquals(layoutCancellation, cancellation))
                layoutCancellation = null;

            cancellation.Dispose();
        }

        private void CancelLayout()
        {
            layoutCancellation?.Cancel();
        }

        private void SetLoading(bool loading)
        {
            isLoading = loading;
            ProgressOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;

            if (loading)
            {
                LayoutProgressBar.Value = 0.0;
                ProgressText.Text = "Preparing...";
                OverlayDetailText.Text =
                    $"Nodes: {snapshot?.Nodes.Count.ToString("N0", CultureInfo.InvariantCulture) ?? "0"} · " +
                    $"Links: {snapshot?.Links.Count.ToString("N0", CultureInfo.InvariantCulture) ?? "0"}";
            }
            else
            {
                LayoutProgressBar.Value = 0.0;
                ProgressText.Text = string.Empty;
            }

            UpdateCommands();
        }

        private void UpdateProgress(LayoutProgress progress)
        {
            if (!isLoading || isClosed)
                return;

            LayoutProgressBar.Value = Math.Clamp(progress.Fraction, 0.0, 1.0) * 100.0;
            ProgressText.Text = progress.Stage switch
            {
                LayoutStage.Initializing => "Preparing...",
                LayoutStage.Relaxing =>
                    $"Layout {progress.Iteration.ToString(CultureInfo.InvariantCulture)}/" +
                    $"{progress.MaxIterations.ToString(CultureInfo.InvariantCulture)}",
                LayoutStage.Packing => "Packing components...",
                LayoutStage.Finalizing => "Finalizing...",
                LayoutStage.Completed => "Layout complete.",
                LayoutStage.Cancelled => "Layout cancelled.",
                _ => progress.Stage.ToString()
            };
        }

        private void UpdateCommands()
        {
            bool ready = !isLoading && !isClosed;
            bool hasGraph = ready && snapshot is { Nodes.Count: > 0 };

            RefreshButton.IsEnabled = ready;
            RecomputeButton.IsEnabled = hasGraph;
            FrameAllButton.IsEnabled = ready && geometry is not null;
            ResetViewButton.IsEnabled = ready && geometry is not null;
            CloseButton.IsEnabled = true;
        }

        private void UpdateSnapshotSummary(GraphSnapshot fresh)
        {
            SummaryNodesText.Text =
                $"Nodes: {fresh.Nodes.Count.ToString("N0", CultureInfo.InvariantCulture)} " +
                $"of {fresh.SourceNodeCount.ToString("N0", CultureInfo.InvariantCulture)}";
            SummaryLinksText.Text =
                $"Links: {fresh.Links.Count.ToString("N0", CultureInfo.InvariantCulture)} " +
                $"of {fresh.SourceLinkCount.ToString("N0", CultureInfo.InvariantCulture)}";
            SummaryClassesText.Text =
                $"Node classes: {fresh.NodeTypeIds.Count.ToString("N0", CultureInfo.InvariantCulture)}";
            SummaryTypesText.Text =
                $"Link types: {fresh.ForwardTypeIds.Count.ToString("N0", CultureInfo.InvariantCulture)}";
            UpdateWarnings(null);
        }

        private void UpdateLayoutSummary(LayoutResult? layout)
        {
            if (layout is null)
            {
                LayoutTimeText.Text = "Layout: (not run)";
                SummaryRadiusText.Text = "Radius: (not run)";
                UpdateWarnings(null);
                return;
            }

            LayoutTimeText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Layout: {0:F2} s · {1} iteration(s) · {2}",
                layout.ElapsedSeconds,
                layout.Iterations,
                layout.Converged ? "converged" : "iteration cap");
            SummaryRadiusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Radius: {0:F1} units",
                layout.Radius);
            UpdateWarnings(layout);
        }

        private void UpdateWarnings(LayoutResult? layout)
        {
            List<string> messages = new List<string>();

            if (snapshot is not null)
                messages.AddRange(snapshot.Warnings);

            if (layout is not null)
                messages.AddRange(layout.Warnings);

            WarningsText.Text = messages.Count == 0
                ? "None."
                : string.Join(Environment.NewLine, messages);
        }

        private void ShowEmptyState(bool show)
        {
            EmptyStateOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetStatus(string message)
        {
            StatusText.Text = message;
        }
    }
}
