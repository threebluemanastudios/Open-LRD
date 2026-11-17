using System;
using System.Collections.Generic;

namespace OPEN_LRD.Visualization
{
    /// <summary>Stores default and user-selected graph palette overrides by node, link, and class ID.</summary>
    internal sealed class GraphColorMap
    {
        private readonly Dictionary<int, RgbColor> nodeColors = new Dictionary<int, RgbColor>();
        private readonly Dictionary<int, RgbColor> linkColors = new Dictionary<int, RgbColor>();
        private readonly Dictionary<int, RgbColor> classColors = new Dictionary<int, RgbColor>();

        public event EventHandler? Changed;

        public IReadOnlyDictionary<int, RgbColor> NodeColors => nodeColors;

        public IReadOnlyDictionary<int, RgbColor> LinkColors => linkColors;

        public IReadOnlyDictionary<int, RgbColor> ClassColors => classColors;

        /// <summary>Returns the overridden or default palette color for a node type ID.</summary>
        public RgbColor GetNodeColor(int nodeTypeId)
        {
            return nodeColors.TryGetValue(nodeTypeId, out RgbColor color)
                ? color
                : GraphColorPalette.GetDefaultNodeColor(nodeTypeId).Color;
        }

        /// <summary>Returns the overridden or default palette color for a link class ID.</summary>
        public RgbColor GetClassColor(int classId)
        {
            return classColors.TryGetValue(classId, out RgbColor color)
                ? color
                : GraphColorPalette.GetDefaultClassColor(classId).Color;
        }

        /// <summary>Returns the effective link color using forward-type override, class override, or default precedence.</summary>
        public RgbColor GetLinkColor(int forwardTypeId, int classId)
        {
            if (linkColors.TryGetValue(forwardTypeId, out RgbColor color))
                return color;

            if (classColors.TryGetValue(classId, out color))
                return color;

            return GraphColorPalette.GetDefaultLinkColor(forwardTypeId).Color;
        }

        /// <summary>Sets a custom color override for a node type ID.</summary>
        public void SetNodeColor(int nodeTypeId, RgbColor color)
        {
            SetColor(nodeColors, nodeTypeId, color);
        }

        /// <summary>Sets a custom color override for a forward link type ID.</summary>
        public void SetLinkColor(int forwardTypeId, RgbColor color)
        {
            SetColor(linkColors, forwardTypeId, color);
        }

        /// <summary>Sets a custom fallback color override for a link class ID.</summary>
        public void SetClassColor(int classId, RgbColor color)
        {
            SetColor(classColors, classId, color);
        }

        /// <summary>Removes any custom color override for a node type ID.</summary>
        public bool ClearNodeColor(int nodeTypeId)
        {
            return RemoveColor(nodeColors, nodeTypeId);
        }

        /// <summary>Removes any custom color override for a forward link type ID.</summary>
        public bool ClearLinkColor(int forwardTypeId)
        {
            return RemoveColor(linkColors, forwardTypeId);
        }

        /// <summary>Removes any custom color override for a link class ID.</summary>
        public bool ClearClassColor(int classId)
        {
            return RemoveColor(classColors, classId);
        }

        /// <summary>Clears all custom node color overrides.</summary>
        public void ClearNodeColors()
        {
            if (nodeColors.Count == 0)
                return;

            nodeColors.Clear();
            RaiseChanged();
        }

        /// <summary>Clears all custom forward-link and link-class color overrides.</summary>
        public void ClearLinkColors()
        {
            if (linkColors.Count == 0 && classColors.Count == 0)
                return;

            linkColors.Clear();
            classColors.Clear();
            RaiseChanged();
        }

        /// <summary>Clears all node, link, and class color overrides.</summary>
        public void Reset()
        {
            if (nodeColors.Count == 0 && linkColors.Count == 0 && classColors.Count == 0)
                return;

            nodeColors.Clear();
            linkColors.Clear();
            classColors.Clear();
            RaiseChanged();
        }

        /// <summary>Replaces all stored node, link, and class color overrides in a single update.</summary>
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

        /// <summary>Updates a color entry in the target dictionary and raises the change event when modified.</summary>
        private void SetColor(Dictionary<int, RgbColor> target, int typeId, RgbColor color)
        {
            if (target.TryGetValue(typeId, out RgbColor existing) && existing == color)
                return;

            target[typeId] = color;
            RaiseChanged();
        }

        /// <summary>Removes a color entry from the target dictionary and raises the change event when removed.</summary>
        private bool RemoveColor(Dictionary<int, RgbColor> target, int typeId)
        {
            if (!target.Remove(typeId))
                return false;

            RaiseChanged();
            return true;
        }

        /// <summary>Notifies listeners that one or more color overrides have changed.</summary>
        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
