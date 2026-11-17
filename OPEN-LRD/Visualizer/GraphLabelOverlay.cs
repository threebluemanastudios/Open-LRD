using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
    /// <summary>Renders bounded, decluttered labels for selected, hovered, and visible graph nodes.</summary>
    internal sealed class GraphLabelOverlay : FrameworkElement
    {
        private static readonly Typeface LabelTypeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);

        private static readonly Typeface SelectedTypeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.SemiBold,
            FontStretches.Normal);

        private static readonly SolidColorBrush BackgroundBrush =
            CreateFrozenBrush(Color.FromArgb(220, 11, 14, 20));

        private static readonly SolidColorBrush TextBrush =
            CreateFrozenBrush(Color.FromRgb(224, 230, 237));

        private static readonly SolidColorBrush SelectedTextBrush =
            CreateFrozenBrush(Colors.White);

        private static readonly Pen NormalPen =
            CreateFrozenPen(Color.FromArgb(60, 255, 255, 255), 1.0);

        private static readonly Pen HoveredPen =
            CreateFrozenPen(Color.FromRgb(255, 213, 79), 1.5); // Gold/Amber

        private static readonly Pen SelectedPen =
            CreateFrozenPen(Color.FromRgb(79, 195, 247), 1.8); // Bright Sky/Cyan

        private readonly List<CachedLabel> cachedLabels = new List<CachedLabel>();
        private readonly List<int> spatialCandidates = new List<int>(VisualizerOptions.MaxLabelCandidatesPerFrame);
        private readonly List<ProjectedLabel> candidates = new List<ProjectedLabel>(VisualizerOptions.MaxLabelCandidatesPerFrame + 2);
        private readonly HashSet<int> indicesToDraw = new HashSet<int>();
        private readonly List<Rect> placedBadgeRects = new List<Rect>(VisualizerOptions.DefaultMaxLabels);
        private readonly List<Rect> foregroundBoxRects = new List<Rect>(VisualizerOptions.DefaultMaxLabels);

        private GraphSnapshot? snapshot;
        private IReadOnlyList<Vector3>? positions;
        private SpatialNodeIndex? spatialIndex;
        private CameraView? currentCamera;
        private int? selectedNodeIndex;
        private int? hoveredNodeIndex;
        private bool showLabels = true;
        private int maxLabels = VisualizerOptions.DefaultMaxLabels;
        private VisualizerLabelMode labelMode = VisualizerLabelMode.Focused;
        private int characterLimit = 48;

        internal int LastCandidateProjectionCount { get; private set; }
        internal int LastProjectedLabelCount { get; private set; }
        internal int LastDrawnLabelCount { get; private set; }

        public GraphLabelOverlay()
        {
            IsHitTestVisible = false;
        }

        public bool ShowLabels
        {
            get => showLabels;
            set
            {
                if (showLabels != value)
                {
                    showLabels = value;
                    InvalidateVisual();
                }
            }
        }

        public Func<int, bool>? IsNodeVisible { get; set; }

        public double? MaxLabelDistance { get; set; }

        public double NodeBoxSize { get; set; } = 1.0;

        public int MaxLabels
        {
            get => maxLabels;
            set
            {
                if (maxLabels != value)
                {
                    maxLabels = value;
                    InvalidateVisual();
                }
            }
        }

        public VisualizerLabelMode LabelMode
        {
            get => labelMode;
            set
            {
                if (labelMode != value)
                {
                    labelMode = Enum.IsDefined(value) ? value : VisualizerLabelMode.Focused;
                    InvalidateVisual();
                }
            }
        }

        public int? SelectedNodeIndex
        {
            get => selectedNodeIndex;
            set
            {
                if (selectedNodeIndex != value)
                {
                    selectedNodeIndex = value;
                    InvalidateVisual();
                }
            }
        }

        public int? HoveredNodeIndex
        {
            get => hoveredNodeIndex;
            set
            {
                if (hoveredNodeIndex != value)
                {
                    hoveredNodeIndex = value;
                    InvalidateVisual();
                }
            }
        }

        public void SetGraph(
            GraphSnapshot? newSnapshot,
            IReadOnlyList<Vector3>? newPositions,
            int charLimit)
        {
            snapshot = newSnapshot;
            positions = newPositions;
            characterLimit = Math.Max(1, charLimit);
            RebuildLabelCache();
            InvalidateVisual();
        }

        public void SetSpatialIndex(SpatialNodeIndex? index)
        {
            spatialIndex = index;
            InvalidateVisual();
        }

        public void UpdateCamera(CameraView camera)
        {
            currentCamera = camera;
            InvalidateVisual();
        }

        public void Clear()
        {
            snapshot = null;
            positions = null;
            spatialIndex = null;
            cachedLabels.Clear();
            selectedNodeIndex = null;
            hoveredNodeIndex = null;
            InvalidateVisual();
        }

        internal void RenderForTesting(DrawingContext drawingContext)
        {
            ArgumentNullException.ThrowIfNull(drawingContext);
            OnRender(drawingContext);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            LastCandidateProjectionCount = 0;
            LastProjectedLabelCount = 0;
            LastDrawnLabelCount = 0;

            if (!showLabels)
                return;

            if (currentCamera is null || snapshot is null || positions is null)
                return;

            if (cachedLabels.Count == 0 || positions.Count < snapshot.Nodes.Count)
                return;

            double width = ActualWidth > 0 ? ActualWidth : currentCamera.ViewportWidth;
            double height = ActualHeight > 0 ? ActualHeight : currentCamera.ViewportHeight;

            candidates.Clear();
            indicesToDraw.Clear();
            placedBadgeRects.Clear();
            foregroundBoxRects.Clear();
            float halfExtent = (float)(Math.Max(NodeBoxSize, 0.01) * 0.5);

            ProjectedLabel? selectedLabel = AddPriorityLabel(
                selectedNodeIndex,
                width,
                height);
            ProjectedLabel? hoveredLabel = hoveredNodeIndex == selectedNodeIndex
                ? null
                : AddPriorityLabel(
                    hoveredNodeIndex,
                    width,
                    height);

            if (labelMode == VisualizerLabelMode.AllVisible && spatialIndex is not null)
            {
                spatialIndex.CollectFrustumCandidates(
                    currentCamera,
                    MaxLabelDistance ?? currentCamera.FarPlaneDistance,
                    VisualizerOptions.MaxLabelCandidatesPerFrame,
                    IsNodeVisible,
                    selectedNodeIndex,
                    hoveredNodeIndex,
                    spatialCandidates);

                for (int candidateIndex = 0; candidateIndex < spatialCandidates.Count; candidateIndex++)
                {
                    int index = spatialCandidates[candidateIndex];
                    if (index < 0 || index >= snapshot.Nodes.Count || index >= cachedLabels.Count || index >= positions.Count)
                        continue;

                    LastCandidateProjectionCount++;
                    LastProjectedLabelCount++;
                    if (!currentCamera.TryProjectCompleteBox(
                            positions[index],
                            halfExtent,
                            out Vector2 screenPos,
                            out double depth,
                            out float minX,
                            out float minY,
                            out float maxX,
                            out float maxY))
                        continue;

                    if (MaxLabelDistance.HasValue && depth > MaxLabelDistance.Value)
                        continue;

                    float boxWidth = maxX - minX;
                    float boxHeight = maxY - minY;
                    if (boxWidth < 8f || boxHeight < 8f)
                        continue;

                    candidates.Add(new ProjectedLabel(index, screenPos, depth, new Rect(minX, minY, boxWidth, boxHeight)));
                }
            }

            if (candidates.Count == 0)
            {
                DrawPriorityLabels(dc, hoveredLabel, selectedLabel);
                return;
            }

            if (labelMode == VisualizerLabelMode.AllVisible)
            {
                // Sort the bounded candidate set near-to-far so closer labels and
                // boxes receive priority. Focused mode has no background ordering.
                candidates.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            }

            AddPriorityForDecluttering(selectedLabel, isSelected: true);
            AddPriorityForDecluttering(hoveredLabel, isSelected: false);

            // 2. Select remaining candidates front-to-back
            if (labelMode == VisualizerLabelMode.AllVisible)
            {
                for (int i = 0; i < candidates.Count && indicesToDraw.Count < maxLabels; i++)
                {
                    ProjectedLabel proj = candidates[i];
                    if (indicesToDraw.Contains(proj.Index))
                        continue;

                    // Occlusion test: Is this node box occluded / covered by a closer foreground box?
                    bool boxOccluded = false;
                    for (int b = 0; b < foregroundBoxRects.Count; b++)
                    {
                        if (foregroundBoxRects[b].Contains(new Point(proj.ScreenPosition.X, proj.ScreenPosition.Y)))
                        {
                            boxOccluded = true;
                            break;
                        }
                    }

                    if (boxOccluded)
                        continue;

                    Rect candidateBadgeRect = GetBadgeRect(proj.Index, proj.ScreenPosition, isSelected: false);

                    // Badge collision test: Does the candidate badge collide with already placed badges?
                    // Include 8px breathing margin around badges to avoid tiled walls of text.
                    Rect expandedBadgeRect = new Rect(
                        candidateBadgeRect.Left - 4.0,
                        candidateBadgeRect.Top - 3.0,
                        candidateBadgeRect.Width + 8.0,
                        candidateBadgeRect.Height + 6.0);

                    bool collides = false;
                    for (int r = 0; r < placedBadgeRects.Count; r++)
                    {
                        if (expandedBadgeRect.IntersectsWith(placedBadgeRects[r]))
                        {
                            collides = true;
                            break;
                        }
                    }

                    // Does the badge cover an existing foreground node box?
                    if (!collides)
                    {
                        for (int b = 0; b < foregroundBoxRects.Count; b++)
                        {
                            if (candidateBadgeRect.IntersectsWith(foregroundBoxRects[b]))
                            {
                                collides = true;
                                break;
                            }
                        }
                    }

                    if (!collides)
                    {
                        indicesToDraw.Add(proj.Index);
                        placedBadgeRects.Add(candidateBadgeRect);
                    }

                    // Register this visible box so it blocks farther boxes and badges behind it
                    foregroundBoxRects.Add(proj.BoxScreenRect);
                }
            }

            // Draw regular labels in far-to-near order so nearer badges paint over any background geometry
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                ProjectedLabel proj = candidates[i];
                if (!indicesToDraw.Contains(proj.Index))
                    continue;

                if (selectedNodeIndex.HasValue && proj.Index == selectedNodeIndex.Value)
                    continue;

                if (hoveredNodeIndex.HasValue && proj.Index == hoveredNodeIndex.Value)
                    continue;

                DrawLabel(dc, proj.Index, proj.ScreenPosition, isSelected: false, isHovered: false);
            }

            // Draw hovered label on top of regular labels
            if (hoveredLabel.HasValue && hoveredLabel.Value.Index != selectedNodeIndex)
            {
                DrawLabel(dc, hoveredLabel.Value.Index, hoveredLabel.Value.ScreenPosition, isSelected: false, isHovered: true);
            }

            // Draw selected label on topmost z-order
            if (selectedLabel.HasValue)
            {
                DrawLabel(dc, selectedLabel.Value.Index, selectedLabel.Value.ScreenPosition, isSelected: true, isHovered: false);
            }
        }

        private ProjectedLabel? AddPriorityLabel(
            int? index,
            double width,
            double height)
        {
            if (!index.HasValue
                || snapshot is null
                || positions is null
                || currentCamera is null
                || index.Value < 0
                || index.Value >= snapshot.Nodes.Count
                || index.Value >= positions.Count
                || index.Value >= cachedLabels.Count)
            {
                return null;
            }

            LastProjectedLabelCount++;
            if (!currentCamera.TryProject(positions[index.Value], out Vector2 screenPosition, out double depth))
                return null;

            if (screenPosition.X < -100 || screenPosition.X > width + 100
                || screenPosition.Y < -100 || screenPosition.Y > height + 100)
            {
                return null;
            }

            Rect box = new Rect(screenPosition.X - 10, screenPosition.Y - 10, 20, 20);
            ProjectedLabel projected = new ProjectedLabel(index.Value, screenPosition, depth, box);
            candidates.Add(projected);
            return projected;
        }

        private void AddPriorityForDecluttering(ProjectedLabel? label, bool isSelected)
        {
            if (!label.HasValue)
                return;

            ProjectedLabel projected = label.Value;
            indicesToDraw.Add(projected.Index);
            placedBadgeRects.Add(GetBadgeRect(projected.Index, projected.ScreenPosition, isSelected));
            foregroundBoxRects.Add(projected.BoxScreenRect);
        }

        private void DrawPriorityLabels(
            DrawingContext dc,
            ProjectedLabel? hoveredLabel,
            ProjectedLabel? selectedLabel)
        {
            if (hoveredLabel.HasValue && hoveredLabel.Value.Index != selectedNodeIndex)
                DrawLabel(dc, hoveredLabel.Value.Index, hoveredLabel.Value.ScreenPosition, isSelected: false, isHovered: true);
            if (selectedLabel.HasValue)
                DrawLabel(dc, selectedLabel.Value.Index, selectedLabel.Value.ScreenPosition, isSelected: true, isHovered: false);
        }

        private Rect GetBadgeRect(int nodeIndex, Vector2 screenPos, bool isSelected)
        {
            if (nodeIndex >= cachedLabels.Count)
                return Rect.Empty;

            CachedLabel label = cachedLabels[nodeIndex];

            const double paddingX = 6.0;
            const double paddingY = 3.0;
            // Conservative estimates avoid formatting text for candidates that are later culled.
            double badgeWidth = label.Text.Length * 8.0 + paddingX * 2.0;
            double badgeHeight = 21.0 + paddingY * 2.0;

            double left = screenPos.X - badgeWidth * 0.5;
            double top = screenPos.Y - badgeHeight - 10.0;

            return new Rect(left, top, badgeWidth, badgeHeight);
        }

        private void DrawLabel(
            DrawingContext dc,
            int nodeIndex,
            Vector2 screenPos,
            bool isSelected,
            bool isHovered)
        {
            if (nodeIndex >= cachedLabels.Count)
                return;

            CachedLabel label = cachedLabels[nodeIndex];
            FormattedText text = label.GetFormattedText(isSelected, VisualTreeHelper.GetDpi(this).PixelsPerDip);

            const double paddingX = 6.0;
            const double paddingY = 3.0;
            double badgeWidth = text.Width + paddingX * 2.0;
            double badgeHeight = text.Height + paddingY * 2.0;

            // Position badge horizontally centered and slightly above node center
            double left = screenPos.X - badgeWidth * 0.5;
            double top = screenPos.Y - badgeHeight - 10.0;

            Rect badgeRect = new Rect(left, top, badgeWidth, badgeHeight);
            Pen pen = isSelected ? SelectedPen : (isHovered ? HoveredPen : NormalPen);

            dc.DrawRoundedRectangle(BackgroundBrush, pen, badgeRect, 4.0, 4.0);
            dc.DrawText(text, new Point(left + paddingX, top + paddingY));
            LastDrawnLabelCount++;
        }

        private void RebuildLabelCache()
        {
            cachedLabels.Clear();

            if (snapshot is null)
                return;

            foreach (NodeRecord node in snapshot.Nodes)
            {
                string labelText = node.DisplayLabel;
                if (labelText.Length > characterLimit)
                    labelText = labelText[..characterLimit] + "…";

                cachedLabels.Add(new CachedLabel(labelText));
            }
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Pen CreateFrozenPen(Color color, double thickness)
        {
            Pen pen = new Pen(new SolidColorBrush(color), thickness);
            pen.Freeze();
            return pen;
        }

        /// <summary>Caches formatted text and WPF drawing data for one node label.</summary>
        private sealed class CachedLabel
        {
            public CachedLabel(string text) => Text = text;

            public string Text { get; }
            private FormattedText? normalText;
            private FormattedText? selectedText;

            public FormattedText GetFormattedText(bool isSelected, double pixelsPerDip)
            {
                if (isSelected)
                {
                    return selectedText ??= new FormattedText(
                        Text,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        SelectedTypeface,
                        11.0,
                        SelectedTextBrush,
                        pixelsPerDip);
                }

                return normalText ??= new FormattedText(
                    Text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    LabelTypeface,
                    11.0,
                    TextBrush,
                    pixelsPerDip);
            }
        }

        /// <summary>Stores a projected label rectangle, priority, and draw ordering.</summary>
        private readonly record struct ProjectedLabel(
            int Index,
            Vector2 ScreenPosition,
            double Depth,
            Rect BoxScreenRect);
    }
}
