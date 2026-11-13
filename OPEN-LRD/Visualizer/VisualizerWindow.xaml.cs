using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OPEN_LRD.Nodes;
using OPEN_LRD.Nodes.Links;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
    public partial class VisualizerWindow : Window
    {
        private readonly LRD_Core core;
        private readonly VisualizerOptions options = new VisualizerOptions();
        private readonly GraphColorMap colors = new GraphColorMap();
        private readonly VisualizerScene scene;
        private FlyCameraController? controller;

        private CancellationTokenSource? layoutCancellation;
        private GraphSnapshot? snapshot;
        private SceneGeometry? geometry;
        private int? selectedNodeIndex;
        private int? hoveredNodeIndex;
        private bool isLoading;
        private bool isClosed;
        private bool isInitializingControls;

        public VisualizerWindow()
            : this(LRD_Core.Instance)
        {
        }

        internal VisualizerWindow(LRD_Core core)
        {
            this.core = core ?? throw new ArgumentNullException(nameof(core));
            InitializeComponent();

            scene = new VisualizerScene(GraphViewport);

            controller = new FlyCameraController(ViewportHost, this, scene);
            controller.CameraMoved += OnCameraMoved;
            controller.SpeedChanged += OnSpeedChanged;

            colors.Changed += OnColorsChanged;

            LoadSettings();
            UpdateCommands();
        }

        internal VisualizerScene Scene => scene;
        internal FlyCameraController? Controller => controller;
        internal GraphLabelOverlay Labels => LabelOverlay;
        internal GraphSnapshot? Snapshot => snapshot;
        internal SceneGeometry? Geometry => geometry;
        internal GraphColorMap ColorsMap => colors;
        internal VisualizerOptions Options => options;
        internal int? SelectedIndex => selectedNodeIndex;
        internal int? HoveredIndex => hoveredNodeIndex;
        internal bool IsLoading => isLoading;
        internal bool IsClosed => isClosed;

        internal Task RefreshGraphForTestingAsync() => RefreshGraphAsync();
        internal void SelectNodeForTesting(int? index) => SetSelectedNode(index);
        internal void HoverNodeForTesting(int? index) => SetHoveredNode(index);
        internal int? PickNodeForTesting(Point point) => PickNode(point);
        internal void SaveSettingsForTesting() => SaveSettings();
        internal void LoadSettingsForTesting() => LoadSettings();

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // WindowStyle=None plus Maximized makes the window cover the active
            // monitor, including the taskbar area.
            WindowState = WindowState.Maximized;
        }

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

        private void Window_Closed(object? sender, EventArgs e)
        {
            isClosed = true;

            SaveSettings();

            controller?.Dispose();
            controller = null;

            colors.Changed -= OnColorsChanged;

            layoutCancellation?.Cancel();
            layoutCancellation?.Dispose();
            layoutCancellation = null;

            LabelOverlay.Clear();
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

        private void ResetView_Button_Click(object sender, RoutedEventArgs e)
        {
            scene.ResetView();
            UpdateCameraView();
            SetStatus("View reset.");
        }

        private void ToggleLabels_Button_Click(object sender, RoutedEventArgs e)
        {
            options.ShowLabels = ToggleLabelsButton.IsChecked == true;
            LabelOverlay.ShowLabels = options.ShowLabels;
            SaveSettings();
            SetStatus(options.ShowLabels ? "Labels visible." : "Labels hidden.");
        }

        private void ToggleLabels()
        {
            options.ShowLabels = !options.ShowLabels;
            ToggleLabelsButton.IsChecked = options.ShowLabels;
            LabelOverlay.ShowLabels = options.ShowLabels;
            SaveSettings();
            SetStatus(options.ShowLabels ? "Labels visible." : "Labels hidden.");
        }

        private void ClearSelection_Button_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedNode(null);
            SetStatus("Selection cleared.");
        }

        private void Close_Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Help_Button_Click(object sender, RoutedEventArgs e)
        {
            ToggleHelp();
        }

        private void CloseHelp_Click(object sender, RoutedEventArgs e)
        {
            HelpOverlay.Visibility = Visibility.Collapsed;
        }

        private void ToggleHelp()
        {
            HelpOverlay.Visibility = HelpOverlay.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void DismissTruncationBanner_Click(object sender, RoutedEventArgs e)
        {
            TruncationBanner.Visibility = Visibility.Collapsed;
        }

        private void OnCameraMoved()
        {
            UpdateCameraView();
        }

        private void OnSpeedChanged(double speed)
        {
            SpeedText.Text = $"Speed: {speed:F1}x (Shift 4x)";
        }

        private void OnColorsChanged(object? sender, EventArgs e)
        {
            scene.UpdateMaterials(colors);
            SaveSettings();
        }

        private void UpdateCameraView()
        {
            LabelOverlay.UpdateCamera(scene.GetCameraView());
        }

        private void ViewportHost_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewportHost.Focus();

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

        private void ViewportHost_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                controller?.HandleMouseUp(e);
            }
        }

        private void ViewportHost_MouseMove(object sender, MouseEventArgs e)
        {
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

        private void ViewportHost_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            controller?.HandleMouseWheel(e);
        }

        private int? PickNode(Point screenPoint)
        {
            if (geometry is null || snapshot is null || snapshot.Nodes.Count == 0)
                return null;

            CameraView cameraView = scene.GetCameraView();
            if (!cameraView.TryCreateRay(new Vector2((float)screenPoint.X, (float)screenPoint.Y), out Vector3 origin, out Vector3 direction))
                return null;

            float halfExtent = (float)(options.CubeSize * 0.5);
            Vector3 halfVector = new Vector3(halfExtent);
            int? hitIndex = null;
            float minDistance = float.PositiveInfinity;

            for (int i = 0; i < geometry.Layout.Positions.Count && i < snapshot.Nodes.Count; i++)
            {
                Vector3 center = geometry.Layout.Positions[i];
                Vector3 min = center - halfVector;
                Vector3 max = center + halfVector;

                if (ProjectionMath.TryIntersectRayAabb(origin, direction, min, max, out float dist))
                {
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        hitIndex = i;
                    }
                }
            }

            return hitIndex;
        }

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
                controller?.SetSceneRadius(built.Radius);

                LabelOverlay.SetGraph(fresh, built.Layout.Positions, options.LabelCharacterLimit);
                LabelOverlay.MaxLabels = options.MaxLabels;
                LabelOverlay.ShowLabels = options.ShowLabels;
                UpdateCameraView();

                SetSelectedNode(null);
                SetHoveredNode(null);

                RebuildColorPanels();

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
            ToggleLabelsButton.IsEnabled = ready;
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

        // =====================================================================
        // Phase 5: Color Panels, Customization, Persistence & Sliders
        // =====================================================================

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

        private void SyncViewControls()
        {
            isInitializingControls = true;
            try
            {
                SpreadSlider.Value = options.Spread;
                SpreadValText.Text = options.Spread.ToString("F1", CultureInfo.InvariantCulture);

                CubeSizeSlider.Value = options.CubeSize;
                CubeSizeValText.Text = options.CubeSize.ToString("F1", CultureInfo.InvariantCulture);

                LineWidthSlider.Value = options.LineWidth;
                LineWidthValText.Text = options.LineWidth.ToString("F2", CultureInfo.InvariantCulture);

                MaxLabelsSlider.Value = options.MaxLabels;
                MaxLabelsValText.Text = options.MaxLabels.ToString(CultureInfo.InvariantCulture);

                ToggleLabelsButton.IsChecked = options.ShowLabels;
            }
            finally
            {
                isInitializingControls = false;
            }
        }

        private void RebuildColorPanels()
        {
            RebuildNodeColorsPanel();
            RebuildLinkColorsPanel();
            RebuildClassColorsPanel();
        }

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
                NodeColorsPanel.Children.Add(CreateColorRow(
                    name,
                    currentColor,
                    selectedColor =>
                    {
                        colors.SetNodeColor(typeId, selectedColor);
                        scene.UpdateMaterials(colors);
                    }));
            }
        }

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
                LinkColorsPanel.Children.Add(CreateColorRow(
                    name,
                    currentColor,
                    selectedColor =>
                    {
                        colors.SetLinkColor(fwdId, selectedColor);
                        scene.UpdateMaterials(colors);
                    }));
            }
        }

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

        private UIElement CreateColorRow(
            string label,
            RgbColor currentColor,
            Action<RgbColor> onColorSelected)
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

        private static SolidColorBrush ToSolidBrush(RgbColor color)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }

        private void ResetNodeColors_Click(object sender, RoutedEventArgs e)
        {
            colors.ClearNodeColors();
            RebuildNodeColorsPanel();
            scene.UpdateMaterials(colors);
            SaveSettings();
            SetStatus("Reset node colors to defaults.");
        }

        private void ResetLinkColors_Click(object sender, RoutedEventArgs e)
        {
            colors.ClearLinkColors();
            RebuildLinkColorsPanel();
            RebuildClassColorsPanel();
            scene.UpdateMaterials(colors);
            SaveSettings();
            SetStatus("Reset link colors to defaults.");
        }

        private void ResetViewSettings_Click(object sender, RoutedEventArgs e)
        {
            VisualizerOptions defaults = new VisualizerOptions();
            options.Spread = defaults.Spread;
            options.CubeSize = defaults.CubeSize;
            options.LineWidth = defaults.LineWidth;
            options.MaxLabels = defaults.MaxLabels;
            options.ShowLabels = defaults.ShowLabels;

            SyncViewControls();
            SaveSettings();

            if (snapshot is not null)
                Recompute_Button_Click(this, new RoutedEventArgs());
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            if (snapshot is not null)
                Recompute_Button_Click(this, new RoutedEventArgs());
        }

        private void SpreadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.Spread = Math.Round(SpreadSlider.Value, 1);
            if (SpreadValText is not null)
                SpreadValText.Text = options.Spread.ToString("F1", CultureInfo.InvariantCulture);
        }

        private void CubeSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.CubeSize = Math.Round(CubeSizeSlider.Value, 1);
            if (CubeSizeValText is not null)
                CubeSizeValText.Text = options.CubeSize.ToString("F1", CultureInfo.InvariantCulture);
        }

        private void LineWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.LineWidth = Math.Round(LineWidthSlider.Value, 2);
            if (LineWidthValText is not null)
                LineWidthValText.Text = options.LineWidth.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void MaxLabelsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isInitializingControls)
                return;

            options.MaxLabels = (int)MaxLabelsSlider.Value;
            if (MaxLabelsValText is not null)
                MaxLabelsValText.Text = options.MaxLabels.ToString(CultureInfo.InvariantCulture);

            LabelOverlay.MaxLabels = options.MaxLabels;
        }
    }
}
