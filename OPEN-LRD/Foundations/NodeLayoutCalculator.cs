using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OPEN_LRD.Foundations
{
    internal readonly record struct NodeLayoutBucket(
        string Key,
        NodeTargetKind TargetKind,
        bool? IsTrusted,
        Vector3 Center,
        double Radius);

    internal sealed class NodeLayoutResult
    {
        internal NodeLayoutResult(
            IReadOnlyDictionary<Guid, Vector3> positions,
            IReadOnlyList<NodeLayoutBucket> buckets,
            IReadOnlyDictionary<Guid, string> nodeBuckets,
            Vector3 coreAnchor)
        {
            Positions = positions ?? throw new ArgumentNullException(nameof(positions));
            Buckets = buckets ?? throw new ArgumentNullException(nameof(buckets));
            NodeBuckets = nodeBuckets ?? throw new ArgumentNullException(nameof(nodeBuckets));
            CoreAnchor = coreAnchor;
        }

        internal IReadOnlyDictionary<Guid, Vector3> Positions { get; }
        internal IReadOnlyList<NodeLayoutBucket> Buckets { get; }
        internal IReadOnlyDictionary<Guid, string> NodeBuckets { get; }
        internal Vector3 CoreAnchor { get; }
    }

    /// <summary>
    /// Deterministically places primary-store buckets around the origin. Spoke order is
    /// Symbol, Word, Sentence, Sense, Usage, Source, Group, Function. Bucket radii are
    /// clamped cube-root volumes; positions use stable-ID uniform-volume samples,
    /// bounded weight/neighbor offsets, fixed spacing passes, and best-effort clearance.
    /// A sample has radius cbrt(u) * bucketRadius * 0.88; NodeWeightBias scales its offset
    /// by max(0, 1 - bias * weight). Two confidence-weighted neighbor passes lerp toward
    /// the weighted neighbor average by NeighborPositionBias. Four spacing passes require
    /// 0.55 * (sizeA + sizeB) + 0.4 separation; two clearance passes target 0.55 * nodeSize
    /// + 0.35 from non-incident link segments.
    /// </summary>
    internal static class NodeLayoutCalculator
    {
        /// <summary>Virtual core anchor; it creates no graph or presentation record.</summary>
        internal static readonly Vector3 CoreAnchor = Vector3.Zero;

        private static readonly NodeTargetKind[] SpokeOrder =
        {
            NodeTargetKind.Symbol,
            NodeTargetKind.Word,
            NodeTargetKind.Sentence,
            NodeTargetKind.Sense,
            NodeTargetKind.Usage,
            NodeTargetKind.Source,
            NodeTargetKind.Group,
            NodeTargetKind.Function
        };

        // Radius = clamp(cbrt(12 * nodeCount + 3 * uniqueInternalLinkCount) * 2.25, 6, 4096).
        private const double MinimumRadius = 6.0;
        private const double MaximumRadius = 4096.0;
        private const double NodeVolume = 12.0;
        private const double LinkVolume = 3.0;
        // Sphere centers retain an 8-unit margin outside the virtual 6-unit core anchor.
        private const double SphereMargin = 8.0;
        // Samples and final coordinates fill at most 88% of each sphere radius.
        private const double PositionFill = 0.88;
        // Four size-aware pair-spacing passes and two link-clearance passes use these factors.
        private const double SpacingFactor = 0.55;
        private const double ClearanceFactor = 0.55;
        private const double GoldenAngle = 2.399963229728653;

        internal static NodeLayoutResult Calculate(
            IReadOnlyDictionary<Guid, NodeDerivationSnapshot> snapshots,
            IReadOnlyDictionary<Guid, double> sizes,
            IReadOnlyCollection<NodeTargetKind> splitKinds,
            NodeLayoutOptions options)
        {
            ArgumentNullException.ThrowIfNull(snapshots);
            ArgumentNullException.ThrowIfNull(sizes);
            ArgumentNullException.ThrowIfNull(splitKinds);
            ArgumentNullException.ThrowIfNull(options);

            Dictionary<Guid, string> nodeBuckets = new Dictionary<Guid, string>();
            Dictionary<string, int> nodeCounts = new Dictionary<string, int>();
            Dictionary<string, int> internalLinkCounts = new Dictionary<string, int>();
            HashSet<(string Bucket, Guid AssertionId)> countedInternalAssertions = new HashSet<(string, Guid)>();
            HashSet<NodeTargetKind> splitSet = new HashSet<NodeTargetKind>(splitKinds);

            foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots.OrderBy(pair => pair.Key))
            {
                NodeDerivationSnapshot snapshot = entry.Value;
                bool? trust = splitSet.Contains(snapshot.TargetKind) ? snapshot.IsTrusted : null;
                string key = BucketKey(snapshot.TargetKind, trust);
                nodeBuckets[entry.Key] = key;
                nodeCounts.TryGetValue(key, out int count);
                nodeCounts[key] = count + 1;
            }

            foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots.OrderBy(pair => pair.Key))
            {
                NodeDerivationSnapshot snapshot = entry.Value;
                if (!nodeBuckets.TryGetValue(snapshot.NodeId, out string? sourceBucket)) continue;
                foreach (NodeDerivationLink link in snapshot.Links.OrderBy(value => value.AssertionId)
                             .ThenBy(value => value.OtherNodeId).ThenBy(value => value.RelationType))
                {
                    if (!nodeBuckets.TryGetValue(link.OtherNodeId, out string? targetBucket)) continue;
                    if (sourceBucket != targetBucket) continue;
                    if (!countedInternalAssertions.Add((sourceBucket, link.AssertionId))) continue;
                    internalLinkCounts.TryGetValue(sourceBucket, out int count);
                    internalLinkCounts[sourceBucket] = count + 1;
                }
            }

            List<NodeLayoutBucket> buckets = new List<NodeLayoutBucket>();
            List<LayoutGroup> groups = new List<LayoutGroup>();
            foreach (NodeTargetKind kind in SpokeOrder)
            {
                nodeCounts.TryGetValue(BucketKey(kind, true), out int trustedCount);
                nodeCounts.TryGetValue(BucketKey(kind, false), out int untrustedCount);
                nodeCounts.TryGetValue(BucketKey(kind, null), out int sharedCount);
                internalLinkCounts.TryGetValue(BucketKey(kind, true), out int trustedLinks);
                internalLinkCounts.TryGetValue(BucketKey(kind, false), out int untrustedLinks);
                internalLinkCounts.TryGetValue(BucketKey(kind, null), out int sharedLinks);

                Vector3 raw = DirectionForKind(kind);
                if (splitSet.Contains(kind))
                {
                    Vector3 trustedDirection = new Vector3(
                        Math.Max(0.7, Math.Abs(raw.X)), raw.Y, raw.Z).Normalize();
                    Vector3 untrustedDirection = new Vector3(
                        -trustedDirection.X, trustedDirection.Y, trustedDirection.Z);
                    groups.Add(new LayoutGroup(new[]
                    {
                        new BucketDefinition(BucketKey(kind, true), kind, true, trustedCount, trustedLinks, trustedDirection),
                        new BucketDefinition(BucketKey(kind, false), kind, false, untrustedCount, untrustedLinks, untrustedDirection)
                    }));
                }
                else
                {
                    Vector3 sharedDirection = new Vector3(0.0, raw.Y, raw.Z);
                    if (sharedDirection.Length() < 1e-6) sharedDirection = new Vector3(0.0, 1.0, 0.0);
                    groups.Add(new LayoutGroup(new[]
                    {
                        new BucketDefinition(BucketKey(kind, null), kind, null, sharedCount, sharedLinks,
                            sharedDirection.Normalize())
                    }));
                }
            }

            AssignBucketCenters(groups, buckets);
            Dictionary<string, NodeLayoutBucket> bucketByKey = buckets.ToDictionary(bucket => bucket.Key);
            Dictionary<Guid, Vector3> positions = new Dictionary<Guid, Vector3>();

            foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots.OrderBy(pair => pair.Key))
            {
                NodeDerivationSnapshot snapshot = entry.Value;
                NodeLayoutBucket bucket = bucketByKey[nodeBuckets[entry.Key]];
                double size = sizes.TryGetValue(entry.Key, out double value) ? Math.Max(0.1, value) : 1.0;
                Vector3 offset = SampleInSphere(bucket.Key, entry.Key, bucket.Center, bucket.Radius) - bucket.Center;
                offset = offset * Math.Max(0.0, 1.0 - options.NodeWeightBias * snapshot.Weight);
                positions[entry.Key] = ClampToSphere(bucket.Center + offset, bucket.Center, bucket.Radius * PositionFill - size);
            }

            ApplyNeighborPasses(snapshots, positions, bucketByKey, nodeBuckets, options, sizes);
            ApplySpacingPasses(snapshots, positions, bucketByKey, nodeBuckets, sizes);
            ApplyLinkClearancePasses(snapshots, positions, bucketByKey, nodeBuckets, options, sizes);
            ApplyFinalClamp(snapshots, positions, bucketByKey, nodeBuckets, sizes);

            return new NodeLayoutResult(positions, buckets, nodeBuckets, CoreAnchor);
        }

        internal static string BucketKey(NodeTargetKind kind, bool? isTrusted)
        {
            string trust = isTrusted.HasValue ? (isTrusted.Value ? "trusted" : "untrusted") : "shared";
            return $"{kind}:{trust}";
        }

        // angle = (kind + 1) * GoldenAngle; normalize (0.85 + 0.15*cos(0.41*angle),
        // 0.55*sin(0.73*angle), sin(angle)). Trusted/untrusted pairs mirror X while
        // retaining Y/Z; shared spokes project into the YZ plane.
        private static Vector3 DirectionForKind(NodeTargetKind kind)
        {
            double angle = ((int)kind + 1) * GoldenAngle;
            double x = 0.85 + 0.15 * Math.Cos(angle * 0.41);
            double y = 0.55 * Math.Sin(angle * 0.73);
            double z = Math.Sin(angle);
            return new Vector3(x, y, z).Normalize();
        }

        /// <summary>Returns the clamped cube-root radius for a bucket's nodes and distinct internal assertions.</summary>
        internal static double CalculateRadius(int nodeCount, int linkCount)
        {
            if (nodeCount < 0) throw new ArgumentOutOfRangeException(nameof(nodeCount));
            if (linkCount < 0) throw new ArgumentOutOfRangeException(nameof(linkCount));
            double raw = Math.Cbrt(nodeCount * NodeVolume + linkCount * LinkVolume) * 2.25;
            if (!double.IsFinite(raw)) return MaximumRadius;
            return Math.Clamp(raw, MinimumRadius, MaximumRadius);
        }

        private static void AssignBucketCenters(List<LayoutGroup> groups, List<NodeLayoutBucket> buckets)
        {
            double maximumRadius = groups.SelectMany(group => group.Definitions)
                .Max(definition => definition.Radius);
            // Initial distance is max(48, 4 * largestRadius + 24); deterministic overlap
            // corrections expand it by the measured overlap plus the 8-unit sphere margin.
            double initialDistance = Math.Max(48.0, maximumRadius * 4.0 + 24.0);
            List<PlacedBucket> placed = new List<PlacedBucket>();

            foreach (LayoutGroup group in groups.OrderBy(group => group.Definitions.First().TargetKind))
            {
                double distance = initialDistance;
                bool groupPlaced = false;
                for (int attempt = 0; attempt < 512; attempt++)
                {
                    Vector3[] centers = group.Definitions
                        .Select(definition => CoreAnchor + definition.Direction * distance).ToArray();
                    double overlap = 0.0;

                    for (int i = 0; i < group.Definitions.Length; i++)
                    {
                        double distanceFromOrigin = centers[i].Distance(CoreAnchor);
                        double originClearance = distanceFromOrigin - group.Definitions[i].Radius - MinimumRadius;
                        if (originClearance < SphereMargin) overlap = Math.Max(overlap, SphereMargin - originClearance);

                        for (int j = i + 1; j < group.Definitions.Length; j++)
                        {
                            double required = group.Definitions[i].Radius + group.Definitions[j].Radius + SphereMargin;
                            double actual = centers[i].Distance(centers[j]);
                            if (actual < required) overlap = Math.Max(overlap, required - actual);
                        }
                    }

                    foreach (PlacedBucket other in placed)
                    {
                        for (int i = 0; i < group.Definitions.Length; i++)
                        {
                            double required = group.Definitions[i].Radius + other.Bucket.Radius + SphereMargin;
                            double actual = centers[i].Distance(other.Center);
                            if (actual < required) overlap = Math.Max(overlap, required - actual);
                        }
                    }

                    if (overlap <= 0.0)
                    {
                        for (int i = 0; i < group.Definitions.Length; i++)
                        {
                            BucketDefinition definition = group.Definitions[i];
                            buckets.Add(new NodeLayoutBucket(definition.Key, definition.TargetKind,
                                definition.IsTrusted, centers[i], definition.Radius));
                            placed.Add(new PlacedBucket(buckets[buckets.Count - 1], centers[i]));
                        }
                        groupPlaced = true;
                        break;
                    }

                    distance += overlap + SphereMargin;
                }
                if (!groupPlaced)
                    throw new InvalidOperationException($"Could not place the {group.Definitions[0].TargetKind} layout spoke without overlap.");
            }
        }

        private static void ApplyNeighborPasses(
            IReadOnlyDictionary<Guid, NodeDerivationSnapshot> snapshots,
            Dictionary<Guid, Vector3> positions,
            IReadOnlyDictionary<string, NodeLayoutBucket> buckets,
            IReadOnlyDictionary<Guid, string> nodeBuckets,
            NodeLayoutOptions options,
            IReadOnlyDictionary<Guid, double> sizes)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Dictionary<Guid, Vector3> previous = new Dictionary<Guid, Vector3>(positions);
                foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots.OrderBy(pair => pair.Key))
                {
                    NodeDerivationSnapshot snapshot = entry.Value;
                    Vector3 weightedSum = Vector3.Zero;
                    double weightTotal = 0.0;
                    foreach (NodeDerivationLink link in snapshot.Links)
                    {
                        if (link.Weight <= 0.0 || !previous.TryGetValue(link.OtherNodeId, out Vector3 neighbor)) continue;
                        weightedSum = weightedSum + neighbor * link.Weight;
                        weightTotal += link.Weight;
                    }
                    if (weightTotal <= 0.0 || !double.IsFinite(weightTotal)) continue;

                    Vector3 average = weightedSum / weightTotal;
                    Vector3 candidate = Vector3.Lerp(positions[entry.Key], average, options.NeighborPositionBias);
                    NodeLayoutBucket bucket = buckets[nodeBuckets[entry.Key]];
                    double size = sizes.TryGetValue(entry.Key, out double value) ? Math.Max(0.1, value) : 1.0;
                    positions[entry.Key] = ClampToSphere(candidate, bucket.Center, bucket.Radius * PositionFill - size);
                }
            }
        }

        private static void ApplySpacingPasses(
            IReadOnlyDictionary<Guid, NodeDerivationSnapshot> snapshots,
            Dictionary<Guid, Vector3> positions,
            IReadOnlyDictionary<string, NodeLayoutBucket> buckets,
            IReadOnlyDictionary<Guid, string> nodeBuckets,
            IReadOnlyDictionary<Guid, double> sizes)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (IGrouping<string, KeyValuePair<Guid, NodeDerivationSnapshot>> bucketNodes in snapshots
                             .OrderBy(pair => pair.Key)
                             .GroupBy(pair => nodeBuckets[pair.Key])
                             .OrderBy(group => group.Key))
                {
                    NodeLayoutBucket bucket = buckets[bucketNodes.Key];
                    List<Guid> ids = bucketNodes.Select(pair => pair.Key).ToList();
                    for (int i = 0; i < ids.Count; i++)
                    {
                        for (int j = i + 1; j < ids.Count; j++)
                        {
                            Guid firstId = ids[i];
                            Guid secondId = ids[j];
                            double firstSize = sizes.TryGetValue(firstId, out double firstValue) ? Math.Max(0.1, firstValue) : 1.0;
                            double secondSize = sizes.TryGetValue(secondId, out double secondValue) ? Math.Max(0.1, secondValue) : 1.0;
                            double required = SpacingFactor * (firstSize + secondSize) + 0.4;
                            Vector3 delta = positions[secondId] - positions[firstId];
                            double distance = delta.Length();
                            if (distance >= required) continue;
                            Vector3 direction = distance > 1e-9
                                ? delta / distance
                                : DeterministicDirection(bucketNodes.Key, firstId, secondId);
                            double move = (required - distance) / 2.0;
                            positions[firstId] = ClampToSphere(positions[firstId] - direction * move, bucket.Center,
                                bucket.Radius * PositionFill - firstSize);
                            positions[secondId] = ClampToSphere(positions[secondId] + direction * move, bucket.Center,
                                bucket.Radius * PositionFill - secondSize);
                        }
                    }
                }
            }
        }

        private static void ApplyLinkClearancePasses(
            IReadOnlyDictionary<Guid, NodeDerivationSnapshot> snapshots,
            Dictionary<Guid, Vector3> positions,
            IReadOnlyDictionary<string, NodeLayoutBucket> buckets,
            IReadOnlyDictionary<Guid, string> nodeBuckets,
            NodeLayoutOptions options,
            IReadOnlyDictionary<Guid, double> sizes)
        {
            Dictionary<(Guid AssertionId, Guid First, Guid Second), NodeDerivationLink> uniqueLinks =
                new Dictionary<(Guid, Guid, Guid), NodeDerivationLink>();
            foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots.OrderBy(pair => pair.Key))
            {
                foreach (NodeDerivationLink link in entry.Value.Links)
                {
                    Guid first = entry.Key < link.OtherNodeId ? entry.Key : link.OtherNodeId;
                    Guid second = entry.Key < link.OtherNodeId ? link.OtherNodeId : entry.Key;
                    uniqueLinks[(link.AssertionId, first, second)] = link;
                }
            }

            List<KeyValuePair<(Guid AssertionId, Guid First, Guid Second), NodeDerivationLink>> ordered =
                uniqueLinks.OrderBy(pair => pair.Key.First).ThenBy(pair => pair.Key.Second)
                    .ThenBy(pair => pair.Key.AssertionId).ToList();

            for (int pass = 0; pass < 2; pass++)
            {
                foreach (KeyValuePair<(Guid AssertionId, Guid First, Guid Second), NodeDerivationLink> item in ordered)
                {
                    if (!positions.TryGetValue(item.Key.First, out Vector3 start) ||
                        !positions.TryGetValue(item.Key.Second, out Vector3 end))
                    {
                        continue;
                    }
                    foreach (KeyValuePair<Guid, NodeDerivationSnapshot> node in snapshots.OrderBy(pair => pair.Key))
                    {
                        Guid otherId = node.Key;
                        if (otherId == item.Key.First || otherId == item.Key.Second) continue;
                        Vector3 point = ClosestPointOnSegment(positions[otherId], start, end);
                        Vector3 delta = positions[otherId] - point;
                        double distance = delta.Length();
                        double size = sizes.TryGetValue(otherId, out double value) ? Math.Max(0.1, value) : 1.0;
                        double clearance = ClearanceFactor * size + 0.35;
                        if (distance >= clearance) continue;
                        Vector3 direction = distance > 1e-9
                            ? delta / distance
                            : DeterministicDirection(nodeBuckets[otherId], item.Key.First, otherId);
                        Vector3 candidate = positions[otherId] + direction * (clearance - distance);
                        NodeLayoutBucket bucket = buckets[nodeBuckets[otherId]];
                        positions[otherId] = ClampToSphere(candidate, bucket.Center, bucket.Radius * PositionFill - size);
                    }
                }
            }
        }

        private static void ApplyFinalClamp(
            IReadOnlyDictionary<Guid, NodeDerivationSnapshot> snapshots,
            Dictionary<Guid, Vector3> positions,
            IReadOnlyDictionary<string, NodeLayoutBucket> buckets,
            IReadOnlyDictionary<Guid, string> nodeBuckets,
            IReadOnlyDictionary<Guid, double> sizes)
        {
            foreach (KeyValuePair<Guid, NodeDerivationSnapshot> entry in snapshots.OrderBy(pair => pair.Key))
            {
                NodeLayoutBucket bucket = buckets[nodeBuckets[entry.Key]];
                double size = sizes.TryGetValue(entry.Key, out double value) ? Math.Max(0.1, value) : 1.0;
                positions[entry.Key] = ClampToSphere(positions[entry.Key], bucket.Center,
                    bucket.Radius * PositionFill - size);
            }
        }

        private static Vector3 SampleInSphere(string bucketKey, Guid nodeId, Vector3 center, double radius)
        {
            ulong first = StableHash(bucketKey, nodeId);
            ulong second = StableHash(bucketKey + ":polar", nodeId);
            ulong third = StableHash(bucketKey + ":radius", nodeId);
            double azimuth = UnitInterval(first) * Math.PI * 2.0;
            double polar = UnitInterval(second) * 2.0 - 1.0;
            double distance = Math.Cbrt(UnitInterval(third)) * radius * PositionFill;
            double radial = Math.Sqrt(Math.Max(0.0, 1.0 - polar * polar)) * distance;
            return center + new Vector3(radial * Math.Cos(azimuth), polar * distance, radial * Math.Sin(azimuth));
        }

        private static Vector3 DeterministicDirection(string bucketKey, Guid first, Guid second)
        {
            ulong hash = StableHash(bucketKey + ":direction", first) ^ StableHash(bucketKey + ":direction", second);
            double x = UnitInterval(hash) * 2.0 - 1.0;
            double y = UnitInterval(hash ^ 0x9E3779B97F4A7C15UL) * 2.0 - 1.0;
            double z = UnitInterval(hash ^ 0xC2B2AE3D27D4EB4FUL) * 2.0 - 1.0;
            Vector3 direction = new Vector3(x, y, z);
            return direction.Length() < 1e-9 ? Vector3.UnitX : direction.Normalize();
        }

        private static Vector3 ClampToSphere(Vector3 point, Vector3 center, double maximumOffset)
        {
            if (maximumOffset <= 0.0) return center;
            Vector3 offset = point - center;
            double distance = offset.Length();
            if (distance <= maximumOffset) return point;
            return center + offset * (maximumOffset / distance);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            double lengthSquared = segment.LengthSquared();
            if (lengthSquared <= 1e-12) return start;
            double projection = (point - start).Dot(segment) / lengthSquared;
            double clamped = Math.Clamp(projection, 0.0, 1.0);
            return start + segment * clamped;
        }

        private static ulong StableHash(string value, Guid id)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            foreach (byte valueByte in Encoding.UTF8.GetBytes(value))
            {
                hash ^= valueByte;
                hash *= prime;
            }
            foreach (byte idByte in id.ToByteArray())
            {
                hash ^= idByte;
                hash *= prime;
            }
            return hash == 0UL ? 0x9E3779B97F4A7C15UL : hash;
        }

        private static double UnitInterval(ulong hash)
        {
            return (double)(hash >> 11) / 9007199254740992.0;
        }

        private readonly record struct BucketDefinition(
            string Key,
            NodeTargetKind TargetKind,
            bool? IsTrusted,
            int NodeCount,
            int InternalLinkCount,
            Vector3 Direction)
        {
            internal double Radius => CalculateRadius(NodeCount, InternalLinkCount);
        }

        private sealed class LayoutGroup
        {
            internal LayoutGroup(IEnumerable<BucketDefinition> definitions)
            {
                Definitions = definitions.ToArray();
            }

            internal BucketDefinition[] Definitions { get; }
        }

        private readonly record struct PlacedBucket(NodeLayoutBucket Bucket, Vector3 Center);
    }
}
