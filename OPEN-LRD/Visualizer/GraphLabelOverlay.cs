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

            // Collect on-screen candidate labels
            List<ProjectedLabel> candidates = new List<ProjectedLabel>();

            for (int index = 0; index < snapshot.Nodes.Count && index < cachedLabels.Count; index++)
            {
                Vector3 worldPos = positions[index];

                if (!currentCamera.TryProject(worldPos, out Vector2 screenPos, out double depth))
                    continue;

                // Screen margin culling
                if (screenPos.X < -60 || screenPos.X > width + 60 ||
                    screenPos.Y < -40 || screenPos.Y > height + 40)
                {
                    continue;
                }

                candidates.Add(new ProjectedLabel(index, screenPos, depth));
            }

            if (candidates.Count == 0)
                return;

            // Sort far to near so closer labels render on top
            candidates.Sort((a, b) => b.Depth.CompareTo(a.Depth));

            // Select which candidates to draw
            HashSet<int> indicesToDraw = new HashSet<int>();

            if (showLabels)
            {
                int countToTake = Math.Min(candidates.Count, maxLabels);
                for (int i = 0; i < countToTake; i++)
                {
                    indicesToDraw.Add(candidates[i].Index);
                }
            }

            // Always ensure selected and hovered nodes are included if on-screen
            if (selectedNodeIndex.HasValue)
                indicesToDraw.Add(selectedNodeIndex.Value);
            if (hoveredNodeIndex.HasValue)
                indicesToDraw.Add(hoveredNodeIndex.Value);

            // Draw regular labels first
            ProjectedLabel? selectedLabel = null;
            ProjectedLabel? hoveredLabel = null;

            foreach (ProjectedLabel proj in candidates)
            {
                if (!indicesToDraw.Contains(proj.Index))
                    continue;

                if (selectedNodeIndex.HasValue && proj.Index == selectedNodeIndex.Value)
                {
                    selectedLabel = proj;
                    continue;
                }

                if (hoveredNodeIndex.HasValue && proj.Index == hoveredNodeIndex.Value)
                {
                    hoveredLabel = proj;
                    continue;
                }

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
            double Depth);
    }
}
