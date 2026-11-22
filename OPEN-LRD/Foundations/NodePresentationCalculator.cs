using System;
using System.Collections.Generic;
using System.Linq;

namespace OPEN_LRD.Foundations
{
    /// <summary>
    /// Pure, deterministic formulas for derived Node presentation fields. Weight is the
    /// mean confidence of distinct incident assertions; Class is the most frequent
    /// relation with enum-order tie-breaking; size is a bounded blend of weight and the
    /// distinct-neighbor fraction; trust uses verified, invalid, and twice-weighted
    /// incident-confidence evidence. RGB channels use class scale
    /// 0.85 + 0.05 * (enumValue mod 7) and trust scale 0.6 + 0.4 * TrustRating.
    /// </summary>
    internal static class NodePresentationCalculator
    {
        // Size = 0.6 + (2.0 - 0.6) * (0.65 * weight + 0.35 * neighbors / (1 + neighbors)).
        private const double MinimumSize = 0.6;
        private const double MaximumSize = 2.0;
        private const double WeightInfluence = 0.65;
        private const double NeighborInfluence = 0.35;
        // Class/trust color modulation never reduces RGB below 60% of its base channel.
        private const double TrustColorFloor = 0.6;

        /// <summary>Counts each distinct incident assertion; ties use the lowest relation enum value.</summary>
        internal static NodeRelationType? CalculateClass(IReadOnlyList<NodeDerivationLink> links)
        {
            ArgumentNullException.ThrowIfNull(links);
            if (links.Count == 0) return null;

            Dictionary<NodeRelationType, int> counts = new Dictionary<NodeRelationType, int>();
            foreach (NodeDerivationLink link in links)
            {
                counts.TryGetValue(link.RelationType, out int count);
                counts[link.RelationType] = count + 1;
            }

            NodeRelationType? result = null;
            int bestCount = 0;
            foreach (KeyValuePair<NodeRelationType, int> entry in counts.OrderBy(pair => pair.Key))
            {
                if (entry.Value > bestCount || (entry.Value == bestCount && result.HasValue && entry.Key < result.Value))
                {
                    result = entry.Key;
                    bestCount = entry.Value;
                }
            }
            return result;
        }

        /// <summary>Returns the arithmetic mean of distinct incident confidences, clamped to [0, 1]; isolated Nodes weigh 0.</summary>
        internal static double CalculateWeight(IReadOnlyList<NodeDerivationLink> links)
        {
            ArgumentNullException.ThrowIfNull(links);
            if (links.Count == 0) return 0.0;
            double average = links.Sum(link => link.Weight) / links.Count;
            if (!double.IsFinite(average)) return 0.0;
            return Math.Clamp(average, 0.0, 1.0);
        }

        internal static double CalculateTrustRating(int verifiedReferences, int invalidReferences,
            double incidentConfidenceSum, bool isTrusted)
        {
            if (verifiedReferences < 0) throw new ArgumentOutOfRangeException(nameof(verifiedReferences));
            if (invalidReferences < 0) throw new ArgumentOutOfRangeException(nameof(invalidReferences));
            if (!double.IsFinite(incidentConfidenceSum) || incidentConfidenceSum < 0.0)
                throw new ArgumentOutOfRangeException(nameof(incidentConfidenceSum));

            // Trust = (verified + 2 * incidentConfidence) /
            //         (verified + invalid + 2 * incidentConfidence).
            double weightedEvidence = incidentConfidenceSum * 2.0;
            double verifiedEvidence = verifiedReferences;
            double invalidEvidence = invalidReferences;
            double denominator = verifiedEvidence + invalidEvidence + weightedEvidence;
            if (denominator <= 0.0 || !double.IsFinite(denominator)) return isTrusted ? 1.0 : 0.0;
            double numerator = verifiedEvidence + weightedEvidence;
            double rating = numerator / denominator;
            if (!double.IsFinite(rating)) return isTrusted ? 1.0 : 0.0;
            return Math.Clamp(rating, 0.0, 1.0);
        }

        internal static string? CalculateLabel(NodeDerivationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            string summary = snapshot.Summary ??
                             $"{snapshot.TargetKind} {snapshot.TargetId.ToString("N")[..8]}";
            if (!snapshot.DerivedClass.HasValue) return summary;
            return $"{summary} [{snapshot.DerivedClass.Value}]";
        }

        /// <summary>Applies class and trust scales to base RGB; trusted alpha is 255, untrusted alpha is 160 + floor(60 * trust).</summary>
        internal static uint CalculateColor(NodeDerivationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            uint baseColor = snapshot.TargetKind switch
            {
                NodeTargetKind.Symbol => 0xFF4E79A7,
                NodeTargetKind.Word => 0xFFF28E2B,
                NodeTargetKind.Sentence => 0xFFE15759,
                NodeTargetKind.Sense => 0xFF76B7B2,
                NodeTargetKind.Usage => 0xFF59A14F,
                NodeTargetKind.Source => 0xFFEDC949,
                NodeTargetKind.Group => 0xFFAF7AA1,
                NodeTargetKind.Function => 0xFFFF9DA7,
                _ => 0xFFBAB0AC
            };

            int classOrdinal = snapshot.DerivedClass.HasValue ? (int)snapshot.DerivedClass.Value : 0;
            double classScale = 0.85 + (classOrdinal % 7) * 0.05;
            double trustScale = TrustColorFloor + (1.0 - TrustColorFloor) * snapshot.TrustRating;
            byte alpha = snapshot.IsTrusted
                ? (byte)255
                : (byte)Math.Clamp(160 + (int)(60 * snapshot.TrustRating), 0, 255);
            byte red = ScaleChannel((byte)(baseColor >> 16), classScale * trustScale);
            byte green = ScaleChannel((byte)(baseColor >> 8), classScale * trustScale);
            byte blue = ScaleChannel((byte)baseColor, classScale * trustScale);
            return ((uint)alpha << 24) | ((uint)red << 16) | ((uint)green << 8) | blue;
        }

        internal static double CalculateSize(NodeDerivationSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            int neighborCount = snapshot.Neighbors.Count;
            double neighborFactor = neighborCount / (1.0 + neighborCount);
            // The bounded blend remains positive and monotonic for weight and neighbor count.
            double normalized = WeightInfluence * snapshot.Weight + NeighborInfluence * neighborFactor;
            double size = MinimumSize + (MaximumSize - MinimumSize) * normalized;
            if (!double.IsFinite(size)) return MinimumSize;
            return Math.Clamp(size, MinimumSize, MaximumSize);
        }

        private static byte ScaleChannel(byte channel, double scale)
        {
            double value = channel * scale;
            if (!double.IsFinite(value)) value = 0.0;
            return (byte)Math.Clamp(Math.Round(value), 0.0, 255.0);
        }
    }
}
