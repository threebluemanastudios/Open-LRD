using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
    /// <summary>Displays the graph in an interactive fullscreen 3D visualization window.</summary>
    public partial class VisualizerWindow : Window
    {
        private static readonly object exceptionLogLock = new object();
        private static readonly TimeSpan LayoutReplacementDebounce = TimeSpan.FromSeconds(2);

        private readonly LRD_Core core;
        private readonly VisualizerOptions options = new VisualizerOptions();
        private readonly GraphColorMap colors = new GraphColorMap();
        private readonly VisualizerScene scene;
        private readonly DispatcherTimer layoutReplacementTimer;
        private readonly Func<int, bool> nodeVisibilityFilter;
        private FlyCameraController? controller;

        private CancellationTokenSource? layoutCancellation;
        private GraphSnapshot? snapshot;
        private SceneGeometry? geometry;
        private SpatialNodeIndex? spatialIndex;
        private int snapshotMaxNodes = VisualizerOptions.DefaultMaxNodes;
        private int snapshotMaxLinks = VisualizerOptions.DefaultMaxLinks;
        private int? selectedNodeIndex;
        private int? hoveredNodeIndex;
        private bool isLoading;
        private bool isClosed;
        private bool isInitializingControls;
        private string? pendingLayoutAction;

        internal static string? ExceptionLogPathOverrideForTesting { get; set; }

        internal Func<GraphSnapshot, VisualizerOptions, CancellationToken, LayoutResult>? LayoutComputationForTesting { get; set; }

        internal bool HasPendingLayoutReplacement =>
            pendingLayoutAction is not null || layoutReplacementTimer.IsEnabled;

        /// <summary>Creates a Visualizer window attached to the initialized application core.</summary>
        public VisualizerWindow()
            : this(LRD_Core.Instance)
        {
        }

        /// <summary>Creates a Visualizer window bound to the specified graph core instance.</summary>
        internal VisualizerWindow(LRD_Core core)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            InitializeComponent();
            nodeVisibilityFilter = IsNodeTypeVisibleForIndex;

            scene = new VisualizerScene(GraphViewport);
            layoutReplacementTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            layoutReplacementTimer.Tick += LayoutReplacementTimer_Tick;

            controller = new FlyCameraController(ViewportHost, this, scene);
            controller.CameraMoved += OnCameraMoved;
            controller.SpeedChanged += OnSpeedChanged;
            controller.GetOrbitTarget = () =>
            {
                if (selectedNodeIndex.HasValue && geometry is not null && selectedNodeIndex.Value < geometry.Layout.Positions.Count)
                    return geometry.Layout.Positions[selectedNodeIndex.Value];
                return null;
            };

            LabelOverlay.IsNodeVisible = nodeVisibilityFilter;

            colors.Changed += OnColorsChanged;

            LoadSettings();
            if (controller is not null)
            {
                controller.InvertMouseX = options.InvertMouseX;
                controller.InvertMouseY = options.InvertMouseY;
            }
            UpdateCommands();
        }

        internal VisualizerScene Scene => scene;
        internal FlyCameraController? Controller => controller;
        internal GraphLabelOverlay Labels => LabelOverlay;
        internal GraphSnapshot? Snapshot => snapshot;
        internal SceneGeometry? Geometry => geometry;
        internal SpatialNodeIndex? NodeSpatialIndex => spatialIndex;
        internal GraphColorMap ColorsMap => colors;
        internal VisualizerOptions Options => options;
        internal int? SelectedIndex => selectedNodeIndex;
        internal int? HoveredIndex => hoveredNodeIndex;
        internal bool IsLoading => isLoading;
        internal bool IsClosed => isClosed;

        /// <summary>Triggers an asynchronous graph snapshot and layout refresh for test verification.</summary>
        internal Task RefreshGraphForTestingAsync() => RefreshGraphAsync();
        /// <summary>Selects or clears a snapshot node index for test verification.</summary>
        internal void SelectNodeForTesting(int? index) => SetSelectedNode(index);
        /// <summary>Hovers or clears a snapshot node index for test verification.</summary>
        internal void HoverNodeForTesting(int? index) => SetHoveredNode(index);
        /// <summary>Performs ray-AABB node picking at the specified viewport coordinate for test verification.</summary>
        internal int? PickNodeForTesting(Point point) => PickNode(point);
        /// <summary>Saves the current visualizer settings to disk for test verification.</summary>
        internal void SaveSettingsForTesting() => SaveSettings();
        /// <summary>Loads the persisted visualizer settings from disk for test verification.</summary>
        internal void LoadSettingsForTesting() => LoadSettings();

        /// <summary>Maximizes the borderless window on its active monitor after native initialization.</summary>
        /// <param name="e">The source-initialization event arguments.</param>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // WindowStyle=None plus Maximized makes the window cover the active
            // monitor, including the taskbar area.
            WindowState = WindowState.Maximized;
        }

        /// <summary>Initializes sidebar controls and starts the initial graph layout when the window loads.</summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RebuildColorPanels();
            SyncViewControls();

            if (!core.initialized)
            {
                ShowEmptyState(true);
                SetStatus("Launch Core before opening the Visualizer.");
                UpdateCommands();
                return;
            }

            await RefreshGraphAsync();
        }

        /// <summary>Handles global keyboard shortcuts for closing, help, view framing, and label toggling.</summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (HelpOverlay.Visibility == Visibility.Visible)
                {
                    HelpOverlay.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    return;
                }

                e.Handled = true;
                Close();
                return;
            }

            if (Keyboard.FocusedElement is TextBox)
                return;

            if (e.Key == Key.F1 || (e.Key == Key.OemQuestion && (Keyboard.Modifiers & ModifierKeys.Shift) != 0))
            {
                e.Handled = true;
                ToggleHelp();
            }
            else if (e.Key == Key.R)
            {
                e.Handled = true;
                ResetView_Button_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.F)
            {
                e.Handled = true;
                FrameAll_Button_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.L)
            {
                e.Handled = true;
                ToggleLabels();
            }
        }

        /// <summary>Persists settings, cancels active layouts, and releases scene resources when closing.</summary>
        private void Window_Closed(object? sender, EventArgs e)
        {
            isClosed = true;
            layoutReplacementTimer.Stop();
            pendingLayoutAction = null;

            SaveSettings();

            controller?.Dispose();
            controller = null;

            colors.Changed -= OnColorsChanged;

            layoutCancellation?.Cancel();
            layoutCancellation?.Dispose();
            layoutCancellation = null;

            spatialIndex = null;
            LabelOverlay.Clear();
            scene.Clear();
        }

        /// <summary>Takes a fresh snapshot from the core and recomputes the 3D layout.</summary>
        private async void Refresh_Button_Click(object sender, RoutedEventArgs e)
        {
            await RefreshGraphAsync();
        }

        /// <summary>Recomputes the 3D layout for the current graph snapshot.</summary>
        private async void Recompute_Button_Click(object sender, RoutedEventArgs e)
        {
            if (snapshot is null)
            {
                SetStatus("Refresh the graph before recomputing the layout.");
                return;
            }

            await RunLayoutAsync(snapshot, updateSnapshotSummary: false, actionName: "Recompute Layout");
        }

        /// <summary>Frames the selected node when one is active, or frames the entire graph.</summary>
        private void FrameAll_Button_Click(object sender, RoutedEventArgs e)
        {
            if (selectedNodeIndex.HasValue && geometry is not null && selectedNodeIndex.Value < geometry.Layout.Positions.Count)
            {
                Vector3 target = geometry.Layout.Positions[selectedNodeIndex.Value];
                scene.FrameTarget(target, (float)options.CubeSize * 2.5f);
                UpdateCameraView();
                SetStatus("Framed selected node.");
            }
            else
            {
                scene.FrameAll();
                UpdateCameraView();
                SetStatus("Framed all nodes.");
            }
        }

        /// <summary>Resets the camera orientation and frames all graph nodes.</summary>
        private void ResetView_Button_Click(object sender, RoutedEventArgs e)
        {
            scene.ResetView();
            UpdateCameraView();
            SetStatus("View reset.");
        }

        /// <summary>Updates label visibility from the toolbar toggle button and saves settings.</summary>
        private void ToggleLabels_Button_Click(object sender, RoutedEventArgs e)
        {
            options.ShowLabels = ToggleLabelsButton.IsChecked == true;
            LabelOverlay.ShowLabels = options.ShowLabels;
            SaveSettings();
            SetStatus(options.ShowLabels ? "Labels visible." : "Labels hidden.");
        }

        /// <summary>Inverts the current floating-label visibility state and updates UI controls.</summary>
        private void ToggleLabels()
        {
            options.ShowLabels = !options.ShowLabels;
            ToggleLabelsButton.IsChecked = options.ShowLabels;
            LabelOverlay.ShowLabels = options.ShowLabels;
            SaveSettings();
            SetStatus(options.ShowLabels ? "Labels visible." : "Labels hidden.");
        }

        /// <summary>Clears the active node selection and hides the selection HUD card.</summary>
        private void ClearSelection_Button_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedNode(null);
        }

        /// <summary>Centers and frames the camera on the currently selected node.</summary>
        private void CenterSelection_Button_Click(object sender, RoutedEventArgs e)
        {
            if (selectedNodeIndex.HasValue && geometry is not null && selectedNodeIndex.Value < geometry.Layout.Positions.Count)
            {
                Vector3 target = geometry.Layout.Positions[selectedNodeIndex.Value];
                scene.FrameTarget(target, (float)options.CubeSize * 2.5f);
                UpdateCameraView();
                SetStatus("Centered view on selected node.");
            }
        }

        /// <summary>Closes the Visualizer window when the toolbar close button is clicked.</summary>
        private void Close_Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>Toggles the keyboard and mouse help overlay when the help button is clicked.</summary>
        private void Help_Button_Click(object sender, RoutedEventArgs e)
        {
            ToggleHelp();
        }

        /// <summary>Hides the keyboard and mouse help overlay.</summary>
        private void CloseHelp_Click(object sender, RoutedEventArgs e)
        {
            HelpOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>Shows or hides the keyboard and mouse help overlay.</summary>
        private void ToggleHelp()
        {
            HelpOverlay.Visibility = HelpOverlay.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>Hides the display-cap truncation warning banner.</summary>
        private void DismissTruncationBanner_Click(object sender, RoutedEventArgs e)
        {
            TruncationBanner.Visibility = Visibility.Collapsed;
        }

        /// <summary>Synchronizes the label overlay projection whenever the camera moves.</summary>
        private void OnCameraMoved()
        {
            UpdateCameraView();
        }

        /// <summary>Updates the status-bar speed indicator when the fly-camera speed multiplier changes.</summary>
        private void OnSpeedChanged(double speed)
        {
            SpeedText.Text = $"Speed: {speed:F1}x (Shift 4x)";
        }

        /// <summary>Updates scene materials and persists settings when the color palette changes.</summary>
        private void OnColorsChanged(object? sender, EventArgs e)
        {
            scene.UpdateMaterials(colors);
            SaveSettings();
        }

        /// <summary>Pushes the current 3D camera projection view to the 2D label overlay.</summary>
        private void UpdateCameraView()
        {
            LabelOverlay.UpdateCamera(scene.GetCameraView());
        }

        /// <summary>Handles viewport mouse-down events for node picking and right-drag mouse look.</summary>
        private void ViewportHost_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewportHost.Focus();

            if (IsPointerOverSelectionCard(e))
            {
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Right)
            {
                controller?.HandleMouseDown(e);
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                Point pt = e.GetPosition(ViewportHost);
                int? hit = PickNode(pt);
                SetSelectedNode(hit);
                e.Handled = true;
            }
        }

        /// <summary>Releases right-button mouse look capture on the viewport.</summary>
        private void ViewportHost_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                controller?.HandleMouseUp(e);
            }
        }

        /// <summary>Handles viewport mouse movement for camera look rotation or hover node picking.</summary>
        private void ViewportHost_MouseMove(object sender, MouseEventArgs e)
        {
            if (IsPointerOverSelectionCard(e))
            {
                SetHoveredNode(null);
                return;
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                controller?.HandleMouseMove(e);
            }
            else
            {
                Point pt = e.GetPosition(ViewportHost);
                int? hit = PickNode(pt);
                SetHoveredNode(hit);
            }
        }

        /// <summary>Forwards mouse-wheel events on the viewport to the fly-camera speed controller.</summary>
        private void ViewportHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsPointerOverSelectionCard(e))
                return;

            controller?.HandleMouseWheel(e);
        }

        /// <summary>Determines whether the mouse pointer is currently inside the visible selection HUD card.</summary>
        private bool IsPointerOverSelectionCard(MouseEventArgs e)
        {
            if (SelectionCard.Visibility != Visibility.Visible)
                return false;

            Point point = e.GetPosition(SelectionCard);
            return point.X >= 0
                && point.Y >= 0
                && point.X <= SelectionCard.ActualWidth
                && point.Y <= SelectionCard.ActualHeight;
        }

        /// <summary>Finds the nearest visible node intersected by a ray through the specified viewport point.</summary>
        private int? PickNode(Point screenPoint)
        {
            if (geometry is null || snapshot is null || spatialIndex is null || snapshot.Nodes.Count == 0)
                return null;

            CameraView cameraView = scene.GetCameraView();
            if (!cameraView.TryCreateRay(new Vector2((float)screenPoint.X, (float)screenPoint.Y), out Vector3 origin, out Vector3 direction))
                return null;

            return spatialIndex.FindNearestRayHit(
                origin,
                direction,
                (float)(options.CubeSize * 0.5),
                nodeVisibilityFilter);
        }

        /// <summary>Determines whether the node at the specified snapshot index belongs to a visible node type.</summary>
        private bool IsNodeTypeVisibleForIndex(int index)
        {
            return snapshot is null
                || index < 0
                || index >= snapshot.Nodes.Count
                || scene.IsNodeTypeVisible(snapshot.Nodes[index].TypeId);
        }

        /// <summary>Updates the selected node state, 3D highlight cube, and selection HUD card.</summary>
        private void SetSelectedNode(int? index)
        {
            selectedNodeIndex = index;
            LabelOverlay.SelectedNodeIndex = index;

            if (index.HasValue && geometry is not null && snapshot is not null && index.Value < snapshot.Nodes.Count)
            {
                Vector3 pos = geometry.Layout.Positions[index.Value];
                scene.SetSelectedHighlight(pos, options.CubeSize);
                NodeRecord node = snapshot.Nodes[index.Value];

                SelectionCard.Visibility = Visibility.Visible;
                SelectedLabelText.Text = node.DisplayLabel;
                SelectedIdText.Text = "ID: " + node.Id.ToString();
                SelectedTypeText.Text = "Type: " + node.TypeName;
                SelectedLifetimeText.Text = "Lifetime: " + node.Lifetime.ToString();
                SelectedDegreeText.Text = $"Degree: {node.Degree}";
                SelectedAliasesText.Text = node.Aliases.Count > 0
                    ? "Aliases: " + string.Join(", ", node.Aliases)
                    : "Aliases: (none)";
                SetStatus($"Selected: {node.DisplayLabel} · {node.TypeName} · degree {node.Degree}");
            }
            else
            {
                scene.SetSelectedHighlight(null, options.CubeSize);
                SelectionCard.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>Updates the hovered node state, 3D hover highlight, and status bar summary.</summary>
        private void SetHoveredNode(int? index)
        {
            if (hoveredNodeIndex == index)
                return;

            hoveredNodeIndex = index;
            LabelOverlay.HoveredNodeIndex = index;

            if (index.HasValue && geometry is not null && snapshot is not null && index.Value < snapshot.Nodes.Count)
            {
                if (selectedNodeIndex != index)
                {
                    Vector3 pos = geometry.Layout.Positions[index.Value];
                    scene.SetHoveredHighlight(pos, options.CubeSize);
                }
                else
                {
                    scene.SetHoveredHighlight(null, options.CubeSize);
                }

                NodeRecord node = snapshot.Nodes[index.Value];
                SetStatus($"{node.DisplayLabel} · {node.TypeName} · degree {node.Degree}");
            }
            else
            {
                scene.SetHoveredHighlight(null, options.CubeSize);

                if (selectedNodeIndex.HasValue && snapshot is not null && selectedNodeIndex.Value < snapshot.Nodes.Count)
                {
                    NodeRecord sel = snapshot.Nodes[selectedNodeIndex.Value];
                    SetStatus($"Selected: {sel.DisplayLabel} · {sel.TypeName} · degree {sel.Degree}");
                }
                else if (geometry is not null)
                {
                    SetStatus(
                        $"Showing {geometry.NodeCount.ToString("N0", CultureInfo.InvariantCulture)} nodes and " +
                        $"{geometry.LinkCount.ToString("N0", CultureInfo.InvariantCulture)} links.");
                }
            }
        }

        /// <summary>Captures a fresh graph snapshot from the core and runs an asynchronous 3D layout.</summary>
        private async Task RefreshGraphAsync(string actionName = "Refresh Graph")
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
                ReportVisualizerException(actionName, ex, layoutWasActive: isLoading);
                return;
            }

            await RunLayoutAsync(fresh, updateSnapshotSummary: true, actionName);
        }

        /// <summary>Executes the background 3D layout, geometry preparation, and spatial indexing for a snapshot.</summary>
        private async Task RunLayoutAsync(
            GraphSnapshot fresh,
            bool updateSnapshotSummary,
            string actionName = "Layout")
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
                spatialIndex = null;
                scene.Clear();
                LabelOverlay.Clear();
                SetSelectedNode(null);
                SetHoveredNode(null);
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
                (SceneGeometry Geometry, PreparedSceneGeometry Prepared, SpatialNodeIndex SpatialIndex)? result =
                    await Task.Run<(SceneGeometry, PreparedSceneGeometry, SpatialNodeIndex)?>(() =>
                {
                    LayoutResult layout = LayoutComputationForTesting is null
                        ? DistanceLayout3D.Compute(fresh, options, token, progress)
                        : LayoutComputationForTesting(fresh, options, token);
                    token.ThrowIfCancellationRequested();

                    if (layout.Cancelled)
                        return null;

                    SceneGeometry builtGeometry = VisualizerSceneBuilder.Build(fresh, layout, options);
                    PreparedSceneGeometry prepared = VisualizerScene.PrepareGeometry(builtGeometry);
                    SpatialNodeIndex index = SpatialNodeIndex.Build(layout.Positions);
                    return (builtGeometry, prepared, index);
                }, token);

                if (isClosed || token.IsCancellationRequested)
                    return;

                if (result is null)
                {
                    SetStatus("Layout cancelled.");
                    return;
                }

                SceneGeometry built = result.Value.Geometry;
                geometry = built;
                spatialIndex = result.Value.SpatialIndex;
                snapshotMaxNodes = options.MaxNodes;
                snapshotMaxLinks = options.MaxLinks;
                scene.Apply(result.Value.Prepared, colors);
                controller?.SetSceneRadius(built.Radius);

                LabelOverlay.SetGraph(fresh, built.Layout.Positions, options.LabelCharacterLimit);
                LabelOverlay.SetSpatialIndex(spatialIndex);
                LabelOverlay.MaxLabels = options.MaxLabels;
                LabelOverlay.LabelMode = options.LabelMode;
                LabelOverlay.ShowLabels = options.ShowLabels;
                LabelOverlay.NodeBoxSize = options.CubeSize;
                LabelOverlay.MaxLabelDistance = Math.Max(built.Radius * 3.0f, 50.0f);
                UpdateCameraView();

                SetSelectedNode(null);
                SetHoveredNode(null);

                RebuildColorPanels();

                UpdateLayoutSummary(built.Layout);
                SetStatus(
                    $"Showing {built.NodeCount.ToString("N0", CultureInfo.InvariantCulture)} nodes and " +
                    $"{built.LinkCount.ToString("N0", CultureInfo.InvariantCulture)} links.");
            }
            catch (OperationCanceledException ex)
            {
                string cancellationAction = pendingLayoutAction is null
                    ? actionName
                    : actionName + " (replaced by " + pendingLayoutAction + ")";
                string? logError = TryAppendExceptionRecord(
                    cancellationAction,
                    ex,
                    layoutWasActive: true);
                if (!isClosed)
                {
                    SetStatus(pendingLayoutAction is null
                        ? "Layout cancelled."
                        : "Layout cancelled; " + pendingLayoutAction + " is queued.");
                    if (logError is not null)
                        WarningsText.Text = logError;
                }
            }
            catch (Exception ex)
            {
                ReportVisualizerException(actionName, ex, layoutWasActive: isLoading);
            }
            finally
            {
                FinishLayout(cancellation);
            }
        }

        /// <summary>Completes and disposes the active layout cancellation token source.</summary>
        private void FinishLayout(CancellationTokenSource cancellation)
        {
            bool isCurrentLayout = ReferenceEquals(layoutCancellation, cancellation);
            if (isCurrentLayout)
            {
                layoutCancellation = null;
                if (!isClosed)
                    SetLoading(false);
            }

            cancellation.Dispose();
        }

        /// <summary>Requests cancellation of any currently running layout task.</summary>
        private void CancelLayout()
        {
            layoutCancellation?.Cancel();
        }

        /// <summary>Cancels any active layout and queues a debounced replacement layout after a two-second pause.</summary>
        private void RequestLayoutReplacement(string actionName)
        {
            if (isClosed)
                return;

            pendingLayoutAction = actionName;
            if (isLoading || layoutCancellation is not null)
                CancelLayout();

            layoutReplacementTimer.Stop();
            layoutReplacementTimer.Interval = LayoutReplacementDebounce;
            layoutReplacementTimer.Start();
            SetStatus($"{actionName} queued; waiting for a two-second pause before rebuilding the layout.");
        }

        /// <summary>Starts the queued replacement layout once the trailing-edge debounce interval elapses.</summary>
        private async void LayoutReplacementTimer_Tick(object? sender, EventArgs e)
        {
            layoutReplacementTimer.Stop();
            if (isClosed)
            {
                pendingLayoutAction = null;
                return;
            }

            if (pendingLayoutAction is null)
                return;

            if (isLoading || layoutCancellation is not null)
            {
                layoutReplacementTimer.Interval = TimeSpan.FromMilliseconds(50);
                layoutReplacementTimer.Start();
                return;
            }

            string actionName = pendingLayoutAction;
            pendingLayoutAction = null;

            try
            {
                if (snapshot is null)
                {
                    SetStatus($"{actionName} settings saved. Refresh the graph to build a layout.");
                    return;
                }

                if (options.MaxNodes != snapshotMaxNodes || options.MaxLinks != snapshotMaxLinks)
                    await RefreshGraphAsync(actionName);
                else
                    await RunLayoutAsync(snapshot, updateSnapshotSummary: false, actionName: actionName);
            }
            catch (Exception ex)
            {
                ReportVisualizerException(actionName, ex, layoutWasActive: isLoading);
            }
        }

        /// <summary>Logs a visualizer exception and displays its concise summary in the status and warnings UI.</summary>
        private void ReportVisualizerException(string actionName, Exception exception, bool layoutWasActive)
        {
            string concise = $"{actionName} failed: {exception.Message}";
            string? logError = TryAppendExceptionRecord(actionName, exception, layoutWasActive);
            WarningsText.Text = logError is null
                ? exception.GetType().Name + ": " + exception.Message
                : exception.GetType().Name + ": " + exception.Message + " " + logError;
            SetStatus(concise);
        }

        /// <summary>Appends a structured exception record to the visualizer exception log file.</summary>
        private static string? TryAppendExceptionRecord(
            string actionName,
            Exception exception,
            bool layoutWasActive)
        {
            try
            {
                string path = ResolveExceptionLogPath();
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder record = new StringBuilder();
                record.AppendLine("TimestampUtc: " + DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                record.AppendLine("Action: " + actionName);
                record.AppendLine("Layout active: " + layoutWasActive.ToString(CultureInfo.InvariantCulture));
                record.AppendLine("Exception type: " + exception.GetType().FullName);
                record.AppendLine("Exception:");
                record.AppendLine(exception.ToString());
                record.AppendLine(new string('-', 80));

                lock (exceptionLogLock)
                    File.AppendAllText(path, record.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                return null;
            }
            catch (Exception logException)
            {
                return "Exception log write failed: " + logException.Message;
            }
        }

        /// <summary>Resolves the file path for the visualizer exception log.</summary>
        private static string ResolveExceptionLogPath()
        {
            if (!string.IsNullOrWhiteSpace(ExceptionLogPathOverrideForTesting))
                return Path.GetFullPath(ExceptionLogPathOverrideForTesting);

            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                string logDirectory = Path.Combine(directory.FullName, ".logs");
                if (Directory.Exists(logDirectory)
                    || File.Exists(Path.Combine(directory.FullName, "OPEN-LRD.slnx")))
                {
                    return Path.Combine(logDirectory, "_exceptions.log");
                }

                directory = directory.Parent;
            }

            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localApplicationData))
                localApplicationData = Path.GetTempPath();
            return Path.Combine(localApplicationData, "OPEN-LRD", ".logs", "_exceptions.log");
        }

        /// <summary>Shows or hides the layout progress overlay and updates toolbar command availability.</summary>
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

        /// <summary>Updates the progress bar and stage label from a background layout progress report.</summary>
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

        /// <summary>Enables or disables toolbar buttons based on loading state and scene availability.</summary>
        private void UpdateCommands()
        {
            bool ready = !isLoading && !isClosed;
            bool hasGraph = ready && snapshot is { Nodes.Count: > 0 };

            RefreshButton.IsEnabled = ready;
            RecomputeButton.IsEnabled = hasGraph;
            FrameAllButton.IsEnabled = ready && geometry is not null;
            ResetViewButton.IsEnabled = ready && geometry is not null;
            ToggleLabelsButton.IsEnabled = ready;
            CloseButton.IsEnabled = true;
        }

        /// <summary>Updates the sidebar snapshot summary counts and truncation banner.</summary>
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

            if (fresh.IsTruncated)
            {
                TruncationBanner.Visibility = Visibility.Visible;
                TruncationBannerText.Text =
                    $"Warning: Graph exceeded display caps (showing {fresh.Nodes.Count:N0} nodes and {fresh.Links.Count:N0} links).";
            }
            else
            {
                TruncationBanner.Visibility = Visibility.Collapsed;
            }

            UpdateWarnings(null);
        }

        /// <summary>Updates the sidebar layout timing, iteration count, and bounding-radius summary.</summary>
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

        /// <summary>Combines and displays snapshot and layout warning messages in the sidebar.</summary>
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

        /// <summary>Shows or hides the empty-graph overlay prompt.</summary>
        private void ShowEmptyState(bool show)
        {
            EmptyStateOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Sets the bottom status bar text.</summary>
        private void SetStatus(string message)
        {
            StatusText.Text = message;
        }

        // =====================================================================
        // Phase 5: Color Panels, Customization, Persistence & Sliders
        // =====================================================================

        /// <summary>Loads persisted visualizer color overrides and view options from disk.</summary>
        private void LoadSettings()
        {
            try
            {
                string path = VisualizerSettings.GetDefaultPath();
                if (File.Exists(path))
                {
                    VisualizerSettings settings = VisualizerSettings.Load(path, out _);
                    settings.ApplyTo(colors, options);
                }
            }
            catch
            {
                // Fallback cleanly to default options and colors
            }
        }

        /// <summary>Saves the current visualizer color overrides and view options to disk.</summary>
        private void SaveSettings()
        {
            try
            {
                string path = VisualizerSettings.GetDefaultPath();
                VisualizerSettings settings = VisualizerSettings.From(colors, options);
                settings.Save(path);
            }
            catch
            {
                // Defensive against IO access issues
            }
        }

        /// <summary>Synchronizes sidebar sliders, inputs, and checkboxes with the current options.</summary>
        private void SyncViewControls()
        {
            isInitializingControls = true;
            try
            {
                SpreadSlider.Value = options.Spread;
                SpreadValText.Text = options.Spread.ToString("F0", CultureInfo.InvariantCulture);

                CubeSizeSlider.Value = options.CubeSize;
                CubeSizeValText.Text = options.CubeSize.ToString("F1", CultureInfo.InvariantCulture);
                LabelOverlay.NodeBoxSize = options.CubeSize;

                LineWidthSlider.Value = options.LineWidth;
                LineWidthValText.Text = options.LineWidth.ToString("F2", CultureInfo.InvariantCulture);

                LabelModeComboBox.SelectedItem = null;
                foreach (object item in LabelModeComboBox.Items)
                {
                    if (item is ComboBoxItem comboItem
                        && string.Equals(comboItem.Tag as string, options.LabelMode.ToString(), StringComparison.Ordinal))
                    {
                        LabelModeComboBox.SelectedItem = comboItem;
                        break;
                    }
                }
                LabelOverlay.LabelMode = options.LabelMode;

                MaxLabelsSlider.Value = options.MaxLabels;
                MaxLabelsSlider.IsEnabled = options.LabelMode == VisualizerLabelMode.AllVisible;
                MaxLabelsValText.Text = options.MaxLabels.ToString(CultureInfo.InvariantCulture);
                LabelOverlay.MaxLabels = options.MaxLabels;

                MaxNodesSlider.Value = options.MaxNodes;
                MaxNodesInputBox.Text = options.MaxNodes.ToString("N0", CultureInfo.InvariantCulture);

                MaxLinksSlider.Value = options.MaxLinks;
                MaxLinksInputBox.Text = options.MaxLinks.ToString("N0", CultureInfo.InvariantCulture);

                NearPlaneSlider.Value = options.NearPlaneDistance;
                NearPlaneValText.Text = options.NearPlaneDistance.ToString("F2", CultureInfo.InvariantCulture);
                scene.NearPlaneDistance = options.NearPlaneDistance;

                ToggleLabelsButton.IsChecked = options.ShowLabels;
                LabelOverlay.ShowLabels = options.ShowLabels;

                InvertMouseXCheckBox.IsChecked = options.InvertMouseX;
                InvertMouseYCheckBox.IsChecked = options.InvertMouseY;

                if (controller is not null)
                {
                    controller.InvertMouseX = options.InvertMouseX;
                    controller.InvertMouseY = options.InvertMouseY;
                }
            }
            finally
            {
                isInitializingControls = false;
            }
        }

        /// <summary>Rebuilds the node, link, and class color customization panels in the sidebar.</summary>
        private void RebuildColorPanels()
        {
            RebuildNodeColorsPanel();
            RebuildLinkColorsPanel();
            RebuildClassColorsPanel();
        }

        /// <summary>Populates the node-type color and visibility controls in the sidebar.</summary>
        private void RebuildNodeColorsPanel()
        {
            NodeColorsPanel.Children.Clear();

            // Collect all unique node types from snapshot or registry
            List<(int Id, string Name)> nodeTypes = new List<(int, string)>();

            if (snapshot is not null && snapshot.Nodes.Count > 0)
            {
                foreach (int typeId in snapshot.NodeTypeIds)
                {
                    string name = NodeTypes.FromId(typeId).Name;
                    nodeTypes.Add((typeId, name));
                }
            }
            else
            {
                foreach (NodeTypes nt in NodeTypes.GetAll())
                {
                    nodeTypes.Add((nt.Id, nt.Name));
                }
            }

            foreach ((int typeId, string name) in nodeTypes)
            {
                RgbColor currentColor = colors.GetNodeColor(typeId);
                bool isVisible = scene.IsNodeTypeVisible(typeId);
                NodeColorsPanel.Children.Add(CreateColorRow(
                    name,
                    currentColor,
                    selectedColor =>
                    {
                        colors.SetNodeColor(typeId, selectedColor);
                        scene.UpdateMaterials(colors);
                    },
                    onVisibilityChanged: visible =>
                    {
                        scene.SetNodeTypeVisible(typeId, visible);
                        LabelOverlay.InvalidateVisual();
                    },
                    isVisible: isVisible));
            }
        }

        /// <summary>Populates the forward-link-type color and visibility controls in the sidebar.</summary>
        private void RebuildLinkColorsPanel()
        {
            LinkColorsPanel.Children.Clear();

            List<(int Id, string Name)> forwardTypes = new List<(int, string)>();

            if (snapshot is not null && snapshot.Links.Count > 0)
            {
                foreach (int fwdId in snapshot.ForwardTypeIds)
                {
                    string name = ForwardLinkTypes.FromId(fwdId).Name;
                    forwardTypes.Add((fwdId, name));
                }
            }
            else
            {
                foreach (ForwardLinkTypes flt in ForwardLinkTypes.GetAll())
                {
                    forwardTypes.Add((flt.Id, flt.Name));
                }
            }

            foreach ((int fwdId, string name) in forwardTypes)
            {
                RgbColor currentColor = colors.GetLinkColor(fwdId, 0);
                bool isVisible = scene.IsLinkTypeVisible(fwdId);
                LinkColorsPanel.Children.Add(CreateColorRow(
                    name,
                    currentColor,
                    selectedColor =>
                    {
                        colors.SetLinkColor(fwdId, selectedColor);
                        scene.UpdateMaterials(colors);
                    },
                    onVisibilityChanged: visible =>
                    {
                        scene.SetLinkTypeVisible(fwdId, visible);
                    },
                    isVisible: isVisible));
            }
        }

        /// <summary>Populates the link-class fallback color controls in the sidebar.</summary>
        private void RebuildClassColorsPanel()
        {
            ClassColorsPanel.Children.Clear();

            foreach (LinkClass lc in LinkClass.GetAll())
            {
                int classId = lc.Id;
                RgbColor currentColor = colors.GetClassColor(classId);
                ClassColorsPanel.Children.Add(CreateColorRow(
                    lc.Name,
                    currentColor,
                    selectedColor =>
                    {
                        colors.SetClassColor(classId, selectedColor);
                        scene.UpdateMaterials(colors);
                    }));
            }
        }

        /// <summary>Creates a single sidebar row with a color swatch, optional visibility checkbox, label, and palette selector.</summary>
        private UIElement CreateColorRow(
            string label,
            RgbColor currentColor,
            Action<RgbColor> onColorSelected,
            Action<bool>? onVisibilityChanged = null,
            bool isVisible = true)
        {
            DockPanel row = new DockPanel
            {
                Margin = new Thickness(0, 0, 0, 6),
                LastChildFill = true
            };

            Border swatch = new Border
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 8, 0),
                CornerRadius = new CornerRadius(3),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Background = ToSolidBrush(currentColor)
            };
            DockPanel.SetDock(swatch, Dock.Left);
            row.Children.Add(swatch);

            ComboBox combo = new ComboBox
            {
                Width = 115,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(combo, Dock.Right);

            ComboBoxItem? selectedItem = null;

            foreach (ColorChoice choice in GraphColorPalette.Palette)
            {
                StackPanel itemPanel = new StackPanel { Orientation = Orientation.Horizontal };
                Border itemSwatch = new Border
                {
                    Width = 12,
                    Height = 12,
                    Margin = new Thickness(0, 0, 6, 0),
                    CornerRadius = new CornerRadius(2),
                    Background = ToSolidBrush(choice.Color)
                };
                TextBlock itemText = new TextBlock
                {
                    Text = choice.Name,
                    Foreground = Brushes.White,
                    FontSize = 11
                };
                itemPanel.Children.Add(itemSwatch);
                itemPanel.Children.Add(itemText);

                ComboBoxItem item = new ComboBoxItem
                {
                    Content = itemPanel,
                    Tag = choice
                };

                combo.Items.Add(item);

                if (choice.Color == currentColor)
                    selectedItem = item;
            }

            if (selectedItem is not null)
                combo.SelectedItem = selectedItem;
            else if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;

            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ComboBoxItem cbi && cbi.Tag is ColorChoice chosen)
                {
                    swatch.Background = ToSolidBrush(chosen.Color);
                    onColorSelected(chosen.Color);
                }
            };

            row.Children.Add(combo);

            if (onVisibilityChanged is not null)
            {
                CheckBox visibilityCheckBox = new CheckBox
                {
                    IsChecked = isVisible,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0),
                    ToolTip = $"Toggle rendering of {label}"
                };
                visibilityCheckBox.Checked += (_, _) => onVisibilityChanged(true);
                visibilityCheckBox.Unchecked += (_, _) => onVisibilityChanged(false);

                DockPanel.SetDock(visibilityCheckBox, Dock.Left);
                row.Children.Add(visibilityCheckBox);
            }

            TextBlock labelText = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(215, 226, 240)),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            row.Children.Add(labelText);

            return row;
        }

        /// <summary>Creates a frozen WPF solid-color brush from an RGB color value.</summary>
        private static SolidColorBrush ToSolidBrush(RgbColor color)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        /// <summary>Resets all node-type color overrides and restores node-type visibility to defaults.</summary>
        private void ResetNodeColors_Click(object sender, RoutedEventArgs e)
        {
            colors.ClearNodeColors();
            if (snapshot is not null && snapshot.Nodes.Count > 0)
            {
                foreach (int typeId in snapshot.NodeTypeIds)
                    scene.SetNodeTypeVisible(typeId, true);
            }
            else
            {
                foreach (NodeTypes nt in NodeTypes.GetAll())
                    scene.SetNodeTypeVisible(nt.Id, true);
            }
            RebuildNodeColorsPanel();
            LabelOverlay.InvalidateVisual();
            scene.UpdateMaterials(colors);
            SaveSettings();
            SetStatus("Reset node colors and visibility to defaults.");
        }

        /// <summary>Resets all link and class color overrides and restores link-type visibility to defaults.</summary>
        private void ResetLinkColors_Click(object sender, RoutedEventArgs e)
        {
            colors.ClearLinkColors();
            if (snapshot is not null && snapshot.Links.Count > 0)
            {
                foreach (int fwdId in snapshot.ForwardTypeIds)
                    scene.SetLinkTypeVisible(fwdId, true);
            }
            else
            {
                foreach (ForwardLinkTypes flt in ForwardLinkTypes.GetAll())
                    scene.SetLinkTypeVisible(flt.Id, true);
            }
            RebuildLinkColorsPanel();
            RebuildClassColorsPanel();
            scene.UpdateMaterials(colors);
            SaveSettings();
            SetStatus("Reset link colors and visibility to defaults.");
        }

        /// <summary>Restores default view options and queues a debounced layout replacement.</summary>
        private void ResetViewSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VisualizerOptions defaults = new VisualizerOptions();
                options.Spread = defaults.Spread;
                options.CubeSize = defaults.CubeSize;
                options.LineWidth = defaults.LineWidth;
                options.MaxLabels = defaults.MaxLabels;
                options.LabelMode = defaults.LabelMode;
                options.MaxNodes = defaults.MaxNodes;
                options.MaxLinks = defaults.MaxLinks;
                options.ShowLabels = defaults.ShowLabels;
                options.InvertMouseX = defaults.InvertMouseX;
                options.InvertMouseY = defaults.InvertMouseY;
                options.NearPlaneDistance = defaults.NearPlaneDistance;
                scene.NearPlaneDistance = defaults.NearPlaneDistance;

                SyncViewControls();
                SaveSettings();
                RequestLayoutReplacement("Sidebar Reset View");
            }
            catch (Exception ex)
            {
                ReportVisualizerException("Sidebar Reset View", ex, layoutWasActive: isLoading);
            }
        }

        /// <summary>Updates horizontal mouse-look inversion from the sidebar checkbox.</summary>
        private void InvertMouseX_Click(object sender, RoutedEventArgs e)
        {
            options.InvertMouseX = InvertMouseXCheckBox.IsChecked == true;
            if (controller is not null)
                controller.InvertMouseX = options.InvertMouseX;
            SaveSettings();
            SetStatus(options.InvertMouseX ? "Horizontal mouse axis inverted." : "Horizontal mouse axis normal.");
        }

        /// <summary>Updates vertical mouse-look inversion from the sidebar checkbox.</summary>
        private void InvertMouseY_Click(object sender, RoutedEventArgs e)
        {
            options.InvertMouseY = InvertMouseYCheckBox.IsChecked == true;
            if (controller is not null)
                controller.InvertMouseY = options.InvertMouseY;
            SaveSettings();
            SetStatus(options.InvertMouseY ? "Vertical mouse axis inverted." : "Vertical mouse axis normal.");
        }

        /// <summary>Validates cap inputs, saves view settings, and queues a debounced layout replacement.</summary>
        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!TryApplyLimitInput(MaxNodesInputBox, isNodeLimit: true) ||
                    !TryApplyLimitInput(MaxLinksInputBox, isNodeLimit: false))
                {
                    return;
                }

                SaveSettings();
                RequestLayoutReplacement("Apply Layout");
            }
            catch (Exception ex)
            {
                ReportVisualizerException("Apply Layout", ex, layoutWasActive: isLoading);
            }
        }

        /// <summary>Updates the spread option and display label when the spread slider changes.</summary>
        private void SpreadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.Spread = VisualizerOptions.NormalizeSpread(SpreadSlider.Value);
            if (SpreadValText is not null)
                SpreadValText.Text = options.Spread.ToString("F0", CultureInfo.InvariantCulture);
        }

        /// <summary>Updates the node cube size option and label overlay box size when the slider changes.</summary>
        private void CubeSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.CubeSize = Math.Round(CubeSizeSlider.Value, 1);
            if (CubeSizeValText is not null)
                CubeSizeValText.Text = options.CubeSize.ToString("F1", CultureInfo.InvariantCulture);

            LabelOverlay.NodeBoxSize = options.CubeSize;
        }

        /// <summary>Updates the link ribbon width option when the line-width slider changes.</summary>
        private void LineWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.LineWidth = Math.Round(LineWidthSlider.Value, 2);
            if (LineWidthValText is not null)
                LineWidthValText.Text = options.LineWidth.ToString("F2", CultureInfo.InvariantCulture);
        }

        /// <summary>Updates the maximum visible label cap when the label-cap slider changes.</summary>
        private void MaxLabelsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.MaxLabels = (int)MaxLabelsSlider.Value;
            if (MaxLabelsValText is not null)
                MaxLabelsValText.Text = options.MaxLabels.ToString(CultureInfo.InvariantCulture);

            LabelOverlay.MaxLabels = options.MaxLabels;
        }

        /// <summary>Applies the selected label mode and updates label-cap slider availability.</summary>
        private void LabelModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializingControls)
                return;

            if (LabelModeComboBox.SelectedItem is not ComboBoxItem selectedItem)
                return;

            options.LabelMode = string.Equals(selectedItem.Tag as string, "AllVisible", StringComparison.Ordinal)
                ? VisualizerLabelMode.AllVisible
                : VisualizerLabelMode.Focused;
            LabelOverlay.LabelMode = options.LabelMode;
            MaxLabelsSlider.IsEnabled = options.LabelMode == VisualizerLabelMode.AllVisible;
            SaveSettings();
            SetStatus(options.LabelMode == VisualizerLabelMode.Focused
                ? "Focused labels enabled."
                : "All visible labels enabled (maximum 50).");
        }

        /// <summary>Synchronizes the node-cap option and text box when the max-nodes slider changes.</summary>
        private void MaxNodesSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls || MaxNodesInputBox is null)
                return;

            options.MaxNodes = Math.Clamp((int)Math.Round(MaxNodesSlider.Value), 1000, VisualizerOptions.MaximumMaxNodes);
            MaxNodesInputBox.Text = options.MaxNodes.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>Synchronizes the link-cap option and text box when the max-links slider changes.</summary>
        private void MaxLinksSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls || MaxLinksInputBox is null)
                return;

            options.MaxLinks = Math.Clamp((int)Math.Round(MaxLinksSlider.Value), 1000, VisualizerOptions.MaximumMaxLinks);
            MaxLinksInputBox.Text = options.MaxLinks.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>Validates and applies the node-cap text box value when focus leaves the control.</summary>
        private void MaxNodesInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TryApplyLimitInput(MaxNodesInputBox, isNodeLimit: true);
        }

        /// <summary>Validates and applies the link-cap text box value when focus leaves the control.</summary>
        private void MaxLinksInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TryApplyLimitInput(MaxLinksInputBox, isNodeLimit: false);
        }

        /// <summary>Parses, validates, and synchronizes a node or link cap text input control.</summary>
        private bool TryApplyLimitInput(TextBox input, bool isNodeLimit)
        {
            int maximum = isNodeLimit ? VisualizerOptions.MaximumMaxNodes : VisualizerOptions.MaximumMaxLinks;
            int minimum = 1000;
            bool parsed = int.TryParse(input.Text, NumberStyles.Integer | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out int value)
                || int.TryParse(input.Text, NumberStyles.Integer | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture, out value);

            if (!parsed || value < minimum || value > maximum)
            {
                SetStatus($"{(isNodeLimit ? "Max Nodes" : "Max Links")} must be between {minimum:N0} and {maximum:N0}.");
                SyncViewControls();
                return false;
            }

            isInitializingControls = true;
            try
            {
                if (isNodeLimit)
                {
                    options.MaxNodes = value;
                    MaxNodesSlider.Value = value;
                    MaxNodesInputBox.Text = value.ToString("N0", CultureInfo.InvariantCulture);
                }
                else
                {
                    options.MaxLinks = value;
                    MaxLinksSlider.Value = value;
                    MaxLinksInputBox.Text = value.ToString("N0", CultureInfo.InvariantCulture);
                }
            }
            finally
            {
                isInitializingControls = false;
            }

            return true;
        }

        /// <summary>Updates the camera near clipping plane distance when the near-plane slider changes.</summary>
        private void NearPlaneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls || scene is null || options is null)
                return;

            options.NearPlaneDistance = Math.Clamp(NearPlaneSlider.Value, 0.01, 100.0);
            scene.NearPlaneDistance = options.NearPlaneDistance;
            if (NearPlaneValText is not null)
                NearPlaneValText.Text = options.NearPlaneDistance.ToString("F2", CultureInfo.InvariantCulture);

            UpdateCameraView();
            SaveSettings();
        }
    }
}
