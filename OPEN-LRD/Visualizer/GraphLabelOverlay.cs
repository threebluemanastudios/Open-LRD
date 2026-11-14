using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using OPEN_LRD.Visualization;

namespace OPEN_LRD.Visualizer
{
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

        private GraphSnapshot? snapshot;
        private IReadOnlyList<Vector3>? positions;
        private CameraView? currentCamera;
        private int? selectedNodeIndex;
        private int? hoveredNodeIndex;
        private bool showLabels = true;
        private int maxLabels = 1000;
        private int characterLimit = 48;

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

        public void UpdateCamera(CameraView camera)
        {
            currentCamera = camera;
            InvalidateVisual();
        }

        public void Clear()
        {
            snapshot = null;
            positions = null;
            cachedLabels.Clear();
            selectedNodeIndex = null;
            hoveredNodeIndex = null;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (currentCamera is null || snapshot is null || positions is null)
                return;

            if (cachedLabels.Count == 0 || positions.Count < snapshot.Nodes.Count)
                return;

            double width = ActualWidth > 0 ? ActualWidth : currentCamera.ViewportWidth;
            double height = ActualHeight > 0 ? ActualHeight : currentCamera.ViewportHeight;

            // Collect candidate labels for nodes whose complete 3D box is visible
            List<ProjectedLabel> candidates = new List<ProjectedLabel>();
            float halfExtent = (float)(Math.Max(NodeBoxSize, 0.01) * 0.5);

            for (int index = 0; index < snapshot.Nodes.Count && index < cachedLabels.Count; index++)
            {
                if (IsNodeVisible is not null && !IsNodeVisible(index))
                    continue;

                Vector3 worldPos = positions[index];
                bool isSpecial = (selectedNodeIndex.HasValue && index == selectedNodeIndex.Value) ||
                                 (hoveredNodeIndex.HasValue && index == hoveredNodeIndex.Value);

                Vector2 screenPos;
                double depth;
                Rect boxScreenRect;

                if (isSpecial)
                {
                    // Special nodes (selected / hovered) only require their center to project
                    if (!currentCamera.TryProject(worldPos, out screenPos, out depth))
                        continue;

                    if (screenPos.X < -100 || screenPos.X > width + 100 ||
                        screenPos.Y < -100 || screenPos.Y > height + 100)
                    {
                        continue;
                    }

                    boxScreenRect = new Rect(screenPos.X - 10, screenPos.Y - 10, 20, 20);
                }
                else
                {
                    // Regular nodes: test all 8 corners of the 3D cube.
                    // The complete node box must be inside viewport bounds and in front of near clipping plane.
                    if (!currentCamera.TryProjectCompleteBox(
                        worldPos,
                        halfExtent,
                        out screenPos,
                        out depth,
                        out float minX,
                        out float minY,
                        out float maxX,
                        out float maxY))
                    {
                        continue;
                    }

                    // Proximity / distance culling
                    if (MaxLabelDistance.HasValue && depth > MaxLabelDistance.Value)
                        continue;

                    float boxWidth = maxX - minX;
                    float boxHeight = maxY - minY;

                    // Minimum projected box size (LOD): ignore tiny distant specks
                    if (boxWidth < 8f || boxHeight < 8f)
                        continue;

                    boxScreenRect = new Rect(minX, minY, boxWidth, boxHeight);
                }

                candidates.Add(new ProjectedLabel(index, screenPos, depth, boxScreenRect));
            }

            if (candidates.Count == 0)
                return;

            // Sort near to far so closer labels and boxes get priority
            candidates.Sort((a, b) => a.Depth.CompareTo(b.Depth));

            // Select which candidates to draw using screen-space decluttering and occlusion
            HashSet<int> indicesToDraw = new HashSet<int>();
            List<Rect> placedBadgeRects = new List<Rect>();
            List<Rect> foregroundBoxRects = new List<Rect>();

            // 1. Always prioritize selected and hovered nodes first
            ProjectedLabel? selectedLabel = null;
            ProjectedLabel? hoveredLabel = null;

            if (selectedNodeIndex.HasValue)
            {
                int selIdx = selectedNodeIndex.Value;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].Index == selIdx)
                    {
                        selectedLabel = candidates[i];
                        indicesToDraw.Add(selIdx);
                        placedBadgeRects.Add(GetBadgeRect(selIdx, candidates[i].ScreenPosition, isSelected: true));
                        foregroundBoxRects.Add(candidates[i].BoxScreenRect);
                        break;
                    }
                }
            }

            if (hoveredNodeIndex.HasValue && hoveredNodeIndex.Value != selectedNodeIndex)
            {
                int hovIdx = hoveredNodeIndex.Value;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].Index == hovIdx)
                    {
                        hoveredLabel = candidates[i];
                        indicesToDraw.Add(hovIdx);
                        placedBadgeRects.Add(GetBadgeRect(hovIdx, candidates[i].ScreenPosition, isSelected: false));
                        foregroundBoxRects.Add(candidates[i].BoxScreenRect);
                        break;
                    }
                }
            }

            // 2. Select remaining candidates front-to-back
            if (showLabels)
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

        private Rect GetBadgeRect(int nodeIndex, Vector2 screenPos, bool isSelected)
        {
            if (nodeIndex >= cachedLabels.Count)
                return Rect.Empty;

            CachedLabel label = cachedLabels[nodeIndex];
            FormattedText text = isSelected ? label.SelectedText : label.NormalText;

            const double paddingX = 6.0;
            const double paddingY = 3.0;
            double badgeWidth = text.Width + paddingX * 2.0;
            double badgeHeight = text.Height + paddingY * 2.0;

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
            FormattedText text = isSelected ? label.SelectedText : label.NormalText;

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
        }

        private void RebuildLabelCache()
        {
            cachedLabels.Clear();

            if (snapshot is null)
                return;

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (NodeRecord node in snapshot.Nodes)
            {
                string labelText = node.DisplayLabel;
                if (labelText.Length > characterLimit)
                    labelText = labelText[..characterLimit] + "…";

                FormattedText normal = new FormattedText(
                    labelText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    LabelTypeface,
                    11.0,
                    TextBrush,
                    pixelsPerDip);

                FormattedText selected = new FormattedText(
                    labelText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    SelectedTypeface,
                    11.0,
                    SelectedTextBrush,
                    pixelsPerDip);

                cachedLabels.Add(new CachedLabel(normal, selected));
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

        private readonly record struct CachedLabel(
            FormattedText NormalText,
            FormattedText SelectedText);

        private readonly record struct ProjectedLabel(
            int Index,
            Vector2 ScreenPosition,
            double Depth,
            Rect BoxScreenRect);
    }
}
