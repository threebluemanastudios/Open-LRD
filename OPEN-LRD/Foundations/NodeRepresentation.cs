using System;

namespace OPEN_LRD.Foundations
{
    /// <summary>Stores precomputed display and 3D model data for a graph node.</summary>
    internal sealed class NodeRepresentation
    {
        private readonly Guid nodeId;
        private uint color;
        private double size;
        private string? textLabel;
        private Vector3 position;
        private bool isReadOnly;

        internal NodeRepresentation(
            Guid nodeId,
            uint color,
            double size = 1.0,
            string? textLabel = null,
            Vector3? position = null,
            bool textLabelCustomized = false)
        {
            ValidateNodeId(nodeId, nameof(nodeId));
            ValidateSize(size, nameof(size));
            this.nodeId = nodeId;
            this.color = color;
            this.size = size;
            this.textLabel = NormalizeOptionalText(textLabel);
            this.position = position ?? Vector3.Zero;
            _ = textLabelCustomized;
        }

        internal Guid GetNodeID() => GraphSynchronization.Execute(() => nodeId);

        internal uint GetColor() => GraphSynchronization.Execute(() => color);

        internal void SetColor(uint value)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                color = value;
            });
        }

        internal double GetSize() => GraphSynchronization.Execute(() => size);

        internal void SetSize(double value)
        {
            ValidateSize(value, nameof(value));
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                size = value;
            });
        }

        internal string? GetTextLabel() => GraphSynchronization.Execute(() => textLabel);

        internal void SetTextLabel(string? value)
        {
            string? normalized = NormalizeOptionalText(value);
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                textLabel = normalized;
            });
        }

        internal Vector3 GetPosition() => GraphSynchronization.Execute(() => position);

        internal void SetPosition(Vector3 value)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                position = value;
            });
        }

        internal void SetPosition(double x, double y, double z)
        {
            SetPosition(new Vector3(x, y, z));
        }

        internal bool IsTextLabelCustomized => false;

        internal bool IsReadOnly => GraphSynchronization.Execute(() => isReadOnly);

        internal void UpdateDefaultTextLabel(string? value)
        {
            string? normalized = NormalizeOptionalText(value);
            GraphSynchronization.Execute(() =>
            {
                EnsureWritable();
                textLabel = normalized;
            });
        }

        internal void MarkReadOnly() => GraphSynchronization.Execute(() => isReadOnly = true);

        internal void MarkWritable() => GraphSynchronization.Execute(() => isReadOnly = false);

        private void EnsureWritable()
        {
            if (isReadOnly)
            {
                throw new InvalidOperationException("Disk-paged NodeRepresentations are read-only; materialize the disk store before mutation.");
            }
        }

        private static void ValidateNodeId(Guid value, string parameterName)
        {
            if (value == Guid.Empty)
            {
                throw new ArgumentException("Node identifiers cannot be Guid.Empty.", parameterName);
            }
        }

        private static void ValidateSize(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Node representation size must be finite and positive.");
            }
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
