using System;
using System.Collections.Generic;

namespace OPEN_LRD.Visualization
{
    internal sealed class GraphColorMap
    {
        private readonly Dictionary<int, RgbColor> nodeColors = new Dictionary<int, RgbColor>();
        private readonly Dictionary<int, RgbColor> linkColors = new Dictionary<int, RgbColor>();
        private readonly Dictionary<int, RgbColor> classColors = new Dictionary<int, RgbColor>();

        public event EventHandler? Changed;

        public IReadOnlyDictionary<int, RgbColor> NodeColors => nodeColors;

        public IReadOnlyDictionary<int, RgbColor> LinkColors => linkColors;

        public IReadOnlyDictionary<int, RgbColor> ClassColors => classColors;

        public RgbColor GetNodeColor(int nodeTypeId)
        {
            return nodeColors.TryGetValue(nodeTypeId, out RgbColor color)
                ? color
                : GraphColorPalette.GetDefaultNodeColor(nodeTypeId).Color;
        }

        public RgbColor GetClassColor(int classId)
        {
            return classColors.TryGetValue(classId, out RgbColor color)
                ? color
                : GraphColorPalette.GetDefaultClassColor(classId).Color;
        }

        public RgbColor GetLinkColor(int forwardTypeId, int classId)
        {
            if (linkColors.TryGetValue(forwardTypeId, out RgbColor color))
                return color;

            if (classColors.TryGetValue(classId, out color))
                return color;

            return GraphColorPalette.GetDefaultLinkColor(forwardTypeId).Color;
        }

        public void SetNodeColor(int nodeTypeId, RgbColor color)
        {
            SetColor(nodeColors, nodeTypeId, color);
        }

        public void SetLinkColor(int forwardTypeId, RgbColor color)
        {
            SetColor(linkColors, forwardTypeId, color);
        }

        public void SetClassColor(int classId, RgbColor color)
        {
            SetColor(classColors, classId, color);
        }

        public bool ClearNodeColor(int nodeTypeId)
        {
            return RemoveColor(nodeColors, nodeTypeId);
        }

        public bool ClearLinkColor(int forwardTypeId)
        {
            return RemoveColor(linkColors, forwardTypeId);
        }

        public bool ClearClassColor(int classId)
        {
            return RemoveColor(classColors, classId);
        }

        public void ClearNodeColors()
        {
            if (nodeColors.Count == 0)
                return;

            nodeColors.Clear();
            RaiseChanged();
        }

        public void ClearLinkColors()
        {
            if (linkColors.Count == 0 && classColors.Count == 0)
                return;

            linkColors.Clear();
            classColors.Clear();
            RaiseChanged();
        }

        public void Reset()
        {
            if (nodeColors.Count == 0 && linkColors.Count == 0 && classColors.Count == 0)
                return;

            nodeColors.Clear();
            linkColors.Clear();
            classColors.Clear();
            RaiseChanged();
        }

        public void ReplaceColors(
            IReadOnlyDictionary<int, RgbColor>? newNodeColors,
            IReadOnlyDictionary<int, RgbColor>? newLinkColors,
            IReadOnlyDictionary<int, RgbColor>? newClassColors)
        {
            nodeColors.Clear();
            linkColors.Clear();
            classColors.Clear();

            if (newNodeColors is not null)
            {
                foreach ((int typeId, RgbColor color) in newNodeColors)
                    nodeColors[typeId] = color;
            }

            if (newLinkColors is not null)
            {
                foreach ((int typeId, RgbColor color) in newLinkColors)
                    linkColors[typeId] = color;
            }

            if (newClassColors is not null)
            {
                foreach ((int classId, RgbColor color) in newClassColors)
                    classColors[classId] = color;
            }

            RaiseChanged();
        }

        private void SetColor(Dictionary<int, RgbColor> target, int typeId, RgbColor color)
        {
            if (target.TryGetValue(typeId, out RgbColor existing) && existing == color)
                return;

            target[typeId] = color;
            RaiseChanged();
        }

        private bool RemoveColor(Dictionary<int, RgbColor> target, int typeId)
        {
            if (!target.Remove(typeId))
                return false;

            RaiseChanged();
            return true;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
