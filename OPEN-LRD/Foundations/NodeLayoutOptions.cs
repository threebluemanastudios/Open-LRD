using System;

namespace OPEN_LRD.Foundations
{
    /// <summary>Selects which checkpoint's layout biases are restored before merge recalculation.</summary>
    internal enum LayoutOptionsMergePreference
    {
        /// <summary>Retain the active base-core settings. This is the merge default.</summary>
        BaseWins = 0,
        /// <summary>Use incoming settings, or defaults when the incoming checkpoint has no settings record.</summary>
        IncomingWins = 1
    }

    internal sealed class NodeLayoutOptions
    {
        internal const double MinimumBias = 0.0;
        internal const double MaximumBias = 0.5;
        internal const double DefaultBias = 0.2;
        internal static readonly Guid RecordKey = Guid.Parse("7d48f3b2-6c91-4b0a-9f35-8c1a2e4d6c07");

        internal NodeLayoutOptions(double nodeWeightBias = DefaultBias, double neighborPositionBias = DefaultBias)
        {
            ValidateBias(nodeWeightBias, nameof(nodeWeightBias));
            ValidateBias(neighborPositionBias, nameof(neighborPositionBias));
            NodeWeightBias = nodeWeightBias;
            NeighborPositionBias = neighborPositionBias;
        }

        internal double NodeWeightBias { get; }
        internal double NeighborPositionBias { get; }

        internal bool ValuesEqual(NodeLayoutOptions other)
        {
            return other != null && NodeWeightBias == other.NodeWeightBias &&
                   NeighborPositionBias == other.NeighborPositionBias;
        }

        private static void ValidateBias(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value < MinimumBias || value > MaximumBias)
            {
                throw new ArgumentOutOfRangeException(parameterName,
                    $"Layout bias must be finite and in [{MinimumBias}, {MaximumBias}].");
            }
        }
    }
}
