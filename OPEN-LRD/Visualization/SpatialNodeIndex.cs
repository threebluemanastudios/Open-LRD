using System;
using System.Collections.Generic;
using System.Numerics;

namespace OPEN_LRD.Visualization
{
    /// <summary>Indexes projected graph nodes in a spatial tree for bounded picking and visibility queries.</summary>
    internal sealed class SpatialNodeIndex
    {
        private readonly Vector3[] positions;
        private readonly int[] orderedIndices;
        private readonly Node[] nodes;
        private readonly List<int> traversalStack = new List<int>();

        /// <summary>Creates a bounding-volume hierarchy over the specified node positions.</summary>
        private SpatialNodeIndex(Vector3[] positions, int leafCapacity)
        {
            this.positions = positions;
            orderedIndices = new int[positions.Length];
            for (int index = 0; index < orderedIndices.Length; index++)
                orderedIndices[index] = index;

            if (positions.Length == 0)
            {
                nodes = Array.Empty<Node>();
                return;
            }

            List<Node> builtNodes = new List<Node>(Math.Max(1, positions.Length / leafCapacity * 2));
            BuildNode(builtNodes, 0, positions.Length, leafCapacity);
            nodes = builtNodes.ToArray();
        }

        public int Count => positions.Length;

        /// <summary>Validates node coordinates and builds a spatial index with the requested leaf capacity.</summary>
        public static SpatialNodeIndex Build(
            IReadOnlyList<Vector3> positions,
            int leafCapacity = VisualizerOptions.SpatialIndexLeafCapacity)
        {
            ArgumentNullException.ThrowIfNull(positions);
            if (leafCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(leafCapacity));

            Vector3[] copy = new Vector3[positions.Count];
            for (int index = 0; index < positions.Count; index++)
            {
                Vector3 position = positions[index];
                if (!IsFinite(position))
                    throw new ArgumentException($"Position {index} must contain finite coordinates.", nameof(positions));
                copy[index] = position;
            }

            return new SpatialNodeIndex(copy, leafCapacity);
        }

        /// <summary>Collects up to the specified number of visible node indices inside the camera frustum.</summary>
        public void CollectFrustumCandidates(
            CameraView camera,
            double maximumDepth,
            int maximumCandidates,
            Func<int, bool>? isNodeVisible,
            int? priorityIndexA,
            int? priorityIndexB,
            List<int> destination)
        {
            ArgumentNullException.ThrowIfNull(camera);
            ArgumentNullException.ThrowIfNull(destination);
            if (!double.IsFinite(maximumDepth) || maximumDepth <= 0d)
                throw new ArgumentOutOfRangeException(nameof(maximumDepth));
            if (maximumCandidates < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumCandidates));

            destination.Clear();
            if (nodes.Length == 0 || maximumCandidates == 0)
                return;

            traversalStack.Clear();
            traversalStack.Add(0);

            while (traversalStack.Count > 0 && destination.Count < maximumCandidates)
            {
                int nodeIndex = traversalStack[^1];
                traversalStack.RemoveAt(traversalStack.Count - 1);
                Node node = nodes[nodeIndex];

                if (!camera.IntersectsFrustum(node.Minimum, node.Maximum, maximumDepth))
                    continue;

                if (node.IsLeaf)
                {
                    for (int offset = 0; offset < node.Count && destination.Count < maximumCandidates; offset++)
                    {
                        int candidateIndex = orderedIndices[node.Start + offset];
                        if (candidateIndex == priorityIndexA || candidateIndex == priorityIndexB)
                            continue;
                        if (isNodeVisible is not null && !isNodeVisible(candidateIndex))
                            continue;

                        if (camera.IsPointInsideFrustum(positions[candidateIndex], maximumDepth))
                            destination.Add(candidateIndex);
                    }

                    continue;
                }

                double leftDepth = camera.GetAabbNearDepth(nodes[node.Left].Minimum, nodes[node.Left].Maximum);
                double rightDepth = camera.GetAabbNearDepth(nodes[node.Right].Minimum, nodes[node.Right].Maximum);

                // LIFO stack: push the farther branch first so the nearest branch is traversed first.
                if (leftDepth <= rightDepth)
                {
                    traversalStack.Add(node.Right);
                    traversalStack.Add(node.Left);
                }
                else
                {
                    traversalStack.Add(node.Left);
                    traversalStack.Add(node.Right);
                }
            }
        }

        /// <summary>Finds the nearest visible node cube intersected by the specified world-space ray.</summary>
        public int? FindNearestRayHit(
            Vector3 origin,
            Vector3 direction,
            float nodeHalfExtent,
            Func<int, bool>? isNodeVisible = null)
        {
            if (!IsFinite(origin))
                throw new ArgumentOutOfRangeException(nameof(origin));
            if (!IsFinite(direction) || direction.LengthSquared() < 1e-12f)
                throw new ArgumentOutOfRangeException(nameof(direction));
            if (!float.IsFinite(nodeHalfExtent) || nodeHalfExtent < 0f)
                throw new ArgumentOutOfRangeException(nameof(nodeHalfExtent));
            if (nodes.Length == 0)
                return null;

            direction = Vector3.Normalize(direction);
            int? nearestIndex = null;
            float nearestDistance = float.PositiveInfinity;
            FindNearestRayHit(0, origin, direction, nodeHalfExtent, isNodeVisible, ref nearestIndex, ref nearestDistance);
            return nearestIndex;
        }

        /// <summary>Recursively traverses the BVH to locate the closest ray-node intersection.</summary>
        private void FindNearestRayHit(
            int nodeIndex,
            Vector3 origin,
            Vector3 direction,
            float nodeHalfExtent,
            Func<int, bool>? isNodeVisible,
            ref int? nearestIndex,
            ref float nearestDistance)
        {
            Node node = nodes[nodeIndex];
            Vector3 expansion = new Vector3(nodeHalfExtent);
            if (!ProjectionMath.TryIntersectRayAabb(
                    origin,
                    direction,
                    node.Minimum - expansion,
                    node.Maximum + expansion,
                    out float nodeEntry)
                || nodeEntry > nearestDistance)
            {
                return;
            }

            if (node.IsLeaf)
            {
                Vector3 halfVector = new Vector3(nodeHalfExtent);
                for (int offset = 0; offset < node.Count; offset++)
                {
                    int candidateIndex = orderedIndices[node.Start + offset];
                    if (isNodeVisible is not null && !isNodeVisible(candidateIndex))
                        continue;

                    Vector3 minimum = positions[candidateIndex] - halfVector;
                    Vector3 maximum = positions[candidateIndex] + halfVector;
                    if (ProjectionMath.TryIntersectRayAabb(origin, direction, minimum, maximum, out float distance)
                        && distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestIndex = candidateIndex;
                    }
                }

                return;
            }

            float leftEntry = GetRayEntry(nodes[node.Left], origin, direction, nodeHalfExtent);
            float rightEntry = GetRayEntry(nodes[node.Right], origin, direction, nodeHalfExtent);
            if (leftEntry <= rightEntry)
            {
                VisitRayChild(node.Left, leftEntry, origin, direction, nodeHalfExtent, isNodeVisible, ref nearestIndex, ref nearestDistance);
                VisitRayChild(node.Right, rightEntry, origin, direction, nodeHalfExtent, isNodeVisible, ref nearestIndex, ref nearestDistance);
            }
            else
            {
                VisitRayChild(node.Right, rightEntry, origin, direction, nodeHalfExtent, isNodeVisible, ref nearestIndex, ref nearestDistance);
                VisitRayChild(node.Left, leftEntry, origin, direction, nodeHalfExtent, isNodeVisible, ref nearestIndex, ref nearestDistance);
            }
        }

        /// <summary>Visits a child BVH node when its ray entry distance can improve the current nearest hit.</summary>
        private void VisitRayChild(
            int childIndex,
            float entry,
            Vector3 origin,
            Vector3 direction,
            float nodeHalfExtent,
            Func<int, bool>? isNodeVisible,
            ref int? nearestIndex,
            ref float nearestDistance)
        {
            if (float.IsFinite(entry) && entry <= nearestDistance)
            {
                FindNearestRayHit(
                    childIndex,
                    origin,
                    direction,
                    nodeHalfExtent,
                    isNodeVisible,
                    ref nearestIndex,
                    ref nearestDistance);
            }
        }

        /// <summary>Returns the ray entry distance into an expanded BVH node bounding box.</summary>
        private static float GetRayEntry(Node node, Vector3 origin, Vector3 direction, float nodeHalfExtent)
        {
            Vector3 expansion = new Vector3(nodeHalfExtent);
            return ProjectionMath.TryIntersectRayAabb(
                origin,
                direction,
                node.Minimum - expansion,
                node.Maximum + expansion,
                out float entry)
                ? entry
                : float.PositiveInfinity;
        }

        /// <summary>Recursively partitions node indices along the longest axis to build a BVH subtree.</summary>
        private int BuildNode(List<Node> builtNodes, int start, int count, int leafCapacity)
        {
            GetBounds(start, count, out Vector3 minimum, out Vector3 maximum);
            int nodeIndex = builtNodes.Count;
            builtNodes.Add(default);

            if (count <= leafCapacity)
            {
                builtNodes[nodeIndex] = new Node(minimum, maximum, -1, -1, start, count);
                return nodeIndex;
            }

            Vector3 extent = maximum - minimum;
            int axis = extent.X >= extent.Y && extent.X >= extent.Z
                ? 0
                : extent.Y >= extent.Z ? 1 : 2;
            Array.Sort(orderedIndices, start, count, new PositionComparer(positions, axis));

            int leftCount = count / 2;
            int left = BuildNode(builtNodes, start, leftCount, leafCapacity);
            int right = BuildNode(builtNodes, start + leftCount, count - leftCount, leafCapacity);
            builtNodes[nodeIndex] = new Node(minimum, maximum, left, right, 0, 0);
            return nodeIndex;
        }

        /// <summary>Computes the axis-aligned bounding box for a range of ordered node indices.</summary>
        private void GetBounds(int start, int count, out Vector3 minimum, out Vector3 maximum)
        {
            Vector3 first = positions[orderedIndices[start]];
            minimum = first;
            maximum = first;
            for (int offset = 1; offset < count; offset++)
            {
                Vector3 position = positions[orderedIndices[start + offset]];
                minimum = Vector3.Min(minimum, position);
                maximum = Vector3.Max(maximum, position);
            }
        }

        /// <summary>Determines whether all components of a 3D vector are finite numbers.</summary>
        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        /// <summary>Stores one spatially indexed node's original graph index and position.</summary>
        private readonly record struct Node(
            Vector3 Minimum,
            Vector3 Maximum,
            int Left,
            int Right,
            int Start,
            int Count)
        {
            public bool IsLeaf => Left < 0;
        }

        /// <summary>Orders node indices by their coordinate along the active partition axis.</summary>
        private sealed class PositionComparer : IComparer<int>
        {
            private readonly Vector3[] positions;
            private readonly int axis;

            /// <summary>Creates a comparer for sorting node indices along the specified coordinate axis.</summary>
            public PositionComparer(Vector3[] positions, int axis)
            {
                this.positions = positions;
                this.axis = axis;
            }

            /// <summary>Compares two node indices by their coordinate along the partition axis.</summary>
            public int Compare(int left, int right)
            {
                float a = GetCoordinate(positions[left], axis);
                float b = GetCoordinate(positions[right], axis);
                int comparison = a.CompareTo(b);
                return comparison != 0 ? comparison : left.CompareTo(right);
            }
        }

        /// <summary>Returns the X, Y, or Z component of a vector for the given axis index.</summary>
        private static float GetCoordinate(Vector3 value, int axis) => axis switch
        {
            0 => value.X,
            1 => value.Y,
            _ => value.Z
        };
    }
}
