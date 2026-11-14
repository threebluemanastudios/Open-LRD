using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace OPEN_LRD.Visualization
{
    internal static class DistanceLayout3D
    {
        private const double GoldenAngle = 2.399963229728653;
        private const double GoldenRatioFraction = 0.6180339887498949;
        private const float MinimumEdgeLength = 1e-6f;
        private const float MinimumCollisionDistance = 1e-4f;
        private const int PackingAttempts = 512;
        private const float MaximumExtent = 1e6f;

        public static LayoutResult Compute(
            GraphSnapshot snapshot,
            VisualizerOptions? options = null,
            CancellationToken cancellationToken = default,
            IProgress<LayoutProgress>? progress = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            options ??= new VisualizerOptions();
            options.Validate();

            Stopwatch stopwatch = Stopwatch.StartNew();
            int nodeCount = snapshot.Nodes.Count;

            if (nodeCount == 0)
            {
                stopwatch.Stop();
                progress?.Report(new LayoutProgress(
                    LayoutStage.Completed,
                    0,
                    options.MaxIterations,
                    1d,
                    0,
                    0));

                return new LayoutResult(
                    Array.Empty<Vector3>(),
                    Vector3.Zero,
                    Vector3.Zero,
                    Vector3.Zero,
                    0f,
                    iterations: 0,
                    converged: true,
                    cancelled: false,
                    elapsedSeconds: stopwatch.Elapsed.TotalSeconds,
                    warnings: Array.Empty<string>(),
                    linkCount: snapshot.Links.Count);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                progress?.Report(new LayoutProgress(
                    LayoutStage.Cancelled,
                    0,
                    options.MaxIterations,
                    0d,
                    0,
                    0));

                return new LayoutResult(
                    new Vector3[nodeCount],
                    Vector3.Zero,
                    Vector3.Zero,
                    Vector3.Zero,
                    0f,
                    iterations: 0,
                    converged: false,
                    cancelled: true,
                    elapsedSeconds: stopwatch.Elapsed.TotalSeconds,
                    warnings: new[] { "Layout was cancelled before it started." },
                    linkCount: snapshot.Links.Count);
            }

            progress?.Report(new LayoutProgress(
                LayoutStage.Initializing,
                0,
                options.MaxIterations,
                0d,
                0,
                0));

            List<int>[] adjacency = BuildAdjacency(snapshot);
            List<Component> components = FindComponents(adjacency, out List<int> isolated);
            components.Sort(CompareComponents);

            int totalComponents = components.Count + (isolated.Count > 0 ? 1 : 0);
            float meanRestLength = ComputeMeanRestLength(snapshot, options);
            Vector3[] positions = new Vector3[nodeCount];
            InitializePositions(snapshot, adjacency, components, meanRestLength, positions);

            progress?.Report(new LayoutProgress(
                LayoutStage.Initializing,
                0,
                options.MaxIterations,
                0.05d,
                0,
                totalComponents));

            bool cancelled = Relax(
                snapshot,
                positions,
                components,
                meanRestLength,
                options,
                cancellationToken,
                progress,
                totalComponents,
                out int iterations,
                out bool converged,
                out int resetNodeCount);

            if (!cancelled)
            {
                progress?.Report(new LayoutProgress(
                    LayoutStage.Packing,
                    iterations,
                    options.MaxIterations,
                    0.9d,
                    components.Count,
                    totalComponents));

                cancelled = Pack(
                    positions,
                    components,
                    isolated,
                    options,
                    cancellationToken);
            }

            progress?.Report(new LayoutProgress(
                cancelled ? LayoutStage.Cancelled : LayoutStage.Finalizing,
                iterations,
                options.MaxIterations,
                cancelled ? 0.95d : 0.95d,
                components.Count,
                totalComponents));

            List<string> warnings = new List<string>();

            if (cancelled)
                warnings.Add("Layout was cancelled; the partial layout is shown.");

            resetNodeCount += SanitizePositions(positions);

            if (resetNodeCount > 0)
            {
                warnings.Add(
                    $"{resetNodeCount} node position(s) produced non-finite coordinates and were reset to the origin.");
            }

            Vector3 centroid = ComputeCentroid(positions);

            for (int index = 0; index < positions.Length; index++)
                positions[index] -= centroid;

            ComputeBounds(positions, out Vector3 boundsMinimum, out Vector3 boundsMaximum, out float radius);

            if (radius > MaximumExtent)
            {
                float scale = MaximumExtent / radius;

                for (int index = 0; index < positions.Length; index++)
                    positions[index] *= scale;

                ComputeBounds(positions, out boundsMinimum, out boundsMaximum, out radius);
                warnings.Add("Layout extents exceeded 1e6 world units and were scaled down.");
            }

            if (!converged && !cancelled)
            {
                warnings.Add(
                    $"Layout stopped after {iterations} iteration(s) without reaching the convergence tolerance.");
            }

            progress?.Report(new LayoutProgress(
                cancelled ? LayoutStage.Cancelled : LayoutStage.Completed,
                iterations,
                options.MaxIterations,
                1d,
                components.Count,
                totalComponents));

            stopwatch.Stop();

            return new LayoutResult(
                positions,
                boundsMinimum,
                boundsMaximum,
                Vector3.Zero,
                radius,
                iterations,
                converged,
                cancelled,
                stopwatch.Elapsed.TotalSeconds,
                warnings,
                snapshot.Links.Count);
        }

        private static List<int>[] BuildAdjacency(GraphSnapshot snapshot)
        {
            List<int>[] adjacency = new List<int>[snapshot.Nodes.Count];

            for (int index = 0; index < adjacency.Length; index++)
                adjacency[index] = new List<int>();

            foreach (LinkRecord link in snapshot.Links)
            {
                adjacency[link.AIndex].Add(link.BIndex);
                adjacency[link.BIndex].Add(link.AIndex);
            }

            foreach (List<int> neighbors in adjacency)
            {
                neighbors.Sort();
                int write = 0;

                for (int read = 0; read < neighbors.Count; read++)
                {
                    if (write == 0 || neighbors[read] != neighbors[write - 1])
                        neighbors[write++] = neighbors[read];
                }

                if (write < neighbors.Count)
                    neighbors.RemoveRange(write, neighbors.Count - write);
            }

            return adjacency;
        }

        private static List<Component> FindComponents(
            IReadOnlyList<int>[] adjacency,
            out List<int> isolated)
        {
            int nodeCount = adjacency.Length;
            bool[] visited = new bool[nodeCount];
            List<Component> components = new List<Component>();
            isolated = new List<int>();
            Queue<int> queue = new Queue<int>();

            for (int start = 0; start < nodeCount; start++)
            {
                if (visited[start])
                    continue;

                List<int> members = new List<int>();
                visited[start] = true;
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    members.Add(current);

                    foreach (int neighbor in adjacency[current])
                    {
                        if (visited[neighbor])
                            continue;

                        visited[neighbor] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                if (members.Count == 1 && adjacency[start].Count == 0)
                {
                    isolated.Add(start);
                    continue;
                }

                members.Sort();
                components.Add(new Component(members.ToArray()));
            }

            return components;
        }

        private static int CompareComponents(Component left, Component right)
        {
            int comparison = right.Nodes.Length.CompareTo(left.Nodes.Length);

            if (comparison != 0)
                return comparison;

            return left.Nodes[0].CompareTo(right.Nodes[0]);
        }

        private static float ComputeMeanRestLength(GraphSnapshot snapshot, VisualizerOptions options)
        {
            if (snapshot.Links.Count == 0)
                return (float)options.Spread;

            double totalDistance = 0d;

            foreach (LinkRecord link in snapshot.Links)
                totalDistance += link.Distance;

            double mean = totalDistance / snapshot.Links.Count * options.Spread;

            if (!double.IsFinite(mean) || mean <= 0d)
                return (float)options.Spread;

            return (float)mean;
        }

        private static void InitializePositions(
            GraphSnapshot snapshot,
            IReadOnlyList<int>[] adjacency,
            IReadOnlyList<Component> components,
            float meanRestLength,
            Vector3[] positions)
        {
            Queue<int> queue = new Queue<int>();

            foreach (Component component in components)
            {
                int root = component.Nodes[0];
                int bestDegree = -1;

                foreach (int nodeIndex in component.Nodes)
                {
                    int degree = snapshot.Nodes[nodeIndex].Degree;

                    if (degree > bestDegree)
                    {
                        bestDegree = degree;
                        root = nodeIndex;
                    }
                }

                bool[] assigned = new bool[component.Nodes.Length];
                Dictionary<int, int> localIndex = new Dictionary<int, int>(component.Nodes.Length);

                for (int index = 0; index < component.Nodes.Length; index++)
                    localIndex[component.Nodes[index]] = index;

                positions[root] = Vector3.Zero;
                component.Directions[root] = Vector3.UnitY;
                assigned[localIndex[root]] = true;
                queue.Enqueue(root);
                int counter = 1;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    Vector3 parentDirection = component.Directions[current];

                    foreach (int neighbor in adjacency[current])
                    {
                        if (assigned[localIndex[neighbor]])
                            continue;

                        Vector3 candidate = SphereDirection(counter++);

                        // Deflect if pointing directly back into the incoming parent direction
                        if (parentDirection.LengthSquared() > 0.5f && Vector3.Dot(candidate, -parentDirection) > 0.8f)
                        {
                            candidate = Vector3.Normalize(candidate + parentDirection * 0.6f);
                        }

                        component.Directions[neighbor] = candidate;
                        positions[neighbor] = positions[current] + candidate * meanRestLength;
                        assigned[localIndex[neighbor]] = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private static bool Relax(
            GraphSnapshot snapshot,
            Vector3[] positions,
            IReadOnlyList<Component> components,
            float meanRestLength,
            VisualizerOptions options,
            CancellationToken cancellationToken,
            IProgress<LayoutProgress>? progress,
            int totalComponents,
            out int iterations,
            out bool converged,
            out int resetNodeCount)
        {
            int nodeCount = positions.Length;
            float[] displacement = new float[nodeCount];
            double tolerance = Math.Max(options.ConvergenceTolerance * meanRestLength, 1e-6);
            float gravity = (float)options.Gravity;
            int reportInterval = Math.Max(1, options.MaxIterations / 20);
            int stableIterations = 0;
            SpatialGrid collisionGrid = new SpatialGrid(nodeCount, Math.Max((float)options.MinSpacing, MinimumCollisionDistance));

            iterations = 0;
            converged = false;
            resetNodeCount = 0;

            for (int iteration = 0; iteration < options.MaxIterations; iteration++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return true;

                float stiffness = options.MaxIterations > 1
                    ? (float)(1.0 - 0.4 * iteration / (options.MaxIterations - 1))
                    : 1f;

                Array.Clear(displacement);

                foreach (LinkRecord link in snapshot.Links)
                {
                    int a = link.AIndex;
                    int b = link.BIndex;
                    Vector3 delta = positions[b] - positions[a];
                    float length = delta.Length();
                    float restLength = (float)(link.Distance * options.Spread);

                    if (!float.IsFinite(length) || length < MinimumEdgeLength)
                    {
                        Vector3 direction = DeterministicDirection(a * 31 + b);
                        positions[b] = positions[a] + direction * MinimumCollisionDistance;
                        displacement[a] += 1f;
                        displacement[b] += 1f;
                        continue;
                    }

                    float correctionScale = (length - restLength) / length * 0.5f * stiffness;
                    Vector3 correction = delta * correctionScale;
                    positions[a] += correction;
                    positions[b] -= correction;
                    float moved = correction.Length();
                    displacement[a] += moved;
                    displacement[b] += moved;
                }

                SeparateCollisions(positions, (float)options.MinSpacing, displacement, collisionGrid);

                float maximumMove = 0f;

                foreach (float moved in displacement)
                {
                    if (moved > maximumMove)
                        maximumMove = moved;
                }

                if (gravity > 0f)
                {
                    int gravityIterations = options.MaxIterations * 3 / 4;
                    float gravityScale = iteration < gravityIterations
                        ? gravity * (1f - iteration / (float)gravityIterations)
                        : 0f;

                    if (gravityScale > 0f)
                    {
                        foreach (Component component in components)
                        {
                            Vector3 centroid = ComputeComponentCentroid(positions, component);

                            foreach (int nodeIndex in component.Nodes)
                                positions[nodeIndex] += (centroid - positions[nodeIndex]) * gravityScale;
                        }
                    }
                }

                iterations = iteration + 1;

                if (iterations % 10 == 0)
                {
                    foreach (Component component in components)
                    {
                        Vector3 centroid = ComputeComponentCentroid(positions, component);

                        foreach (int nodeIndex in component.Nodes)
                            positions[nodeIndex] -= centroid;
                    }

                    resetNodeCount += SanitizePositions(positions);
                }

                if (iteration % reportInterval == 0 || iteration + 1 == options.MaxIterations)
                {
                    progress?.Report(new LayoutProgress(
                        LayoutStage.Relaxing,
                        iterations,
                        options.MaxIterations,
                        iterations / (double)options.MaxIterations,
                        0,
                        totalComponents));
                }

                if (maximumMove < tolerance)
                {
                    stableIterations++;

                    if (stableIterations >= 2)
                    {
                        converged = true;
                        break;
                    }
                }
                else
                {
                    stableIterations = 0;
                }
            }

            return false;
        }

        private static void SeparateCollisions(
            Vector3[] positions,
            float minimumSpacing,
            float[] displacement,
            SpatialGrid grid)
        {
            grid.Rebuild(positions);

            for (int a = 0; a < positions.Length; a++)
            {
                int cellX = grid.CellX[a];
                int cellY = grid.CellY[a];
                int cellZ = grid.CellZ[a];

                for (int deltaX = -1; deltaX <= 1; deltaX++)
                {
                    for (int deltaY = -1; deltaY <= 1; deltaY++)
                    {
                        for (int deltaZ = -1; deltaZ <= 1; deltaZ++)
                        {
                            int neighborX = cellX + deltaX;
                            int neighborY = cellY + deltaY;
                            int neighborZ = cellZ + deltaZ;
                            int bucket = grid.GetBucket(neighborX, neighborY, neighborZ);

                            for (int b = grid.Head[bucket]; b >= 0; b = grid.Next[b])
                            {
                                if (b <= a)
                                    continue;

                                // Hash buckets may contain multiple cells. Only visit the exact neighbor.
                                if (grid.CellX[b] != neighborX || grid.CellY[b] != neighborY || grid.CellZ[b] != neighborZ)
                                    continue;

                                Vector3 delta = positions[b] - positions[a];
                                float distance = delta.Length();

                                if (!float.IsFinite(distance) || distance >= minimumSpacing)
                                    continue;

                                Vector3 direction = distance > MinimumEdgeLength
                                    ? delta / distance
                                    : DeterministicDirection(a * 131 + b);

                                float push = (minimumSpacing - Math.Max(distance, 0f)) * 0.5f;
                                positions[a] -= direction * push;
                                positions[b] += direction * push;
                                displacement[a] += push;
                                displacement[b] += push;
                            }
                        }
                    }
                }
            }
        }

        private static bool Pack(
            Vector3[] positions,
            IReadOnlyList<Component> components,
            IReadOnlyList<int> isolated,
            VisualizerOptions options,
            CancellationToken cancellationToken)
        {
            float margin = (float)Math.Max(0.25 * options.Spread, MinimumCollisionDistance);
            List<PlacedSphere> placed = new List<PlacedSphere>(components.Count);

            for (int compIndex = 0; compIndex < components.Count; compIndex++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return true;

                Component component = components[compIndex];
                component.Centroid = ComputeComponentCentroid(positions, component);
                component.Radius = ComputeComponentRadius(positions, component);

                Vector3 target = placed.Count == 0
                    ? Vector3.Zero
                    : FindPackSpot(placed, compIndex, component.Radius, margin);
                Vector3 offset = target - component.Centroid;

                foreach (int nodeIndex in component.Nodes)
                    positions[nodeIndex] += offset;

                placed.Add(new PlacedSphere(target, component.Radius));
            }

            if (isolated.Count == 0)
                return false;

            if (cancellationToken.IsCancellationRequested)
                return true;

            float spacing = (float)Math.Max(options.MinSpacing * 1.5, 0.1);
            int count = isolated.Count;

            // Uniform 3D spherical volume distribution for isolated nodes
            float cloudRadius = count > 1
                ? spacing * MathF.Cbrt(count) * 0.95f
                : 0f;

            Vector3 targetCenter = placed.Count == 0
                ? Vector3.Zero
                : FindPackSpot(placed, placed.Count, cloudRadius, margin);

            for (int index = 0; index < count; index++)
            {
                float r = count > 1
                    ? cloudRadius * MathF.Cbrt((index + 0.5f) / count)
                    : 0f;

                Vector3 dir = SphereDirection(index + 1);
                positions[isolated[index]] = targetCenter + dir * r;
            }

            // Ensure all isolated nodes respect minimum collision spacing
            float[] tempDisplacement = new float[positions.Length];
            SpatialGrid collisionGrid = new SpatialGrid(
                positions.Length,
                Math.Max((float)options.MinSpacing, MinimumCollisionDistance));
            for (int pass = 0; pass < 8; pass++)
            {
                Array.Clear(tempDisplacement);
                SeparateCollisions(positions, (float)options.MinSpacing, tempDisplacement, collisionGrid);

                float maxDisp = 0f;
                foreach (float d in tempDisplacement)
                {
                    if (d > maxDisp)
                        maxDisp = d;
                }

                if (maxDisp < 1e-4f)
                    break;
            }

            return false;
        }

        private static Vector3 FindPackSpot(
            IReadOnlyList<PlacedSphere> placed,
            int componentIndex,
            float radius,
            float margin)
        {
            if (placed.Count == 0)
                return Vector3.Zero;

            Vector3 bestSpot = Vector3.Zero;
            float bestDistanceSq = float.PositiveInfinity;

            const int directionCandidates = 24;
            for (int d = 0; d < directionCandidates; d++)
            {
                int dirIndex = componentIndex * directionCandidates + d + 1;
                Vector3 dir = SphereDirection(dirIndex);

                float requiredDistance = 0f;

                foreach (PlacedSphere sphere in placed)
                {
                    float combinedRadius = sphere.Radius + radius + margin;
                    float proj = Vector3.Dot(sphere.Center, dir);
                    float perpSq = sphere.Center.LengthSquared() - proj * proj;

                    if (perpSq < combinedRadius * combinedRadius)
                    {
                        float halfChord = MathF.Sqrt(Math.Max(0f, combinedRadius * combinedRadius - Math.Max(0f, perpSq)));
                        float blockedFar = proj + halfChord;
                        if (blockedFar > requiredDistance)
                            requiredDistance = blockedFar;
                    }
                }

                Vector3 candidate = dir * requiredDistance;
                bool fits = true;
                foreach (PlacedSphere sphere in placed)
                {
                    float minSeparation = sphere.Radius + radius + margin;
                    if (Vector3.DistanceSquared(candidate, sphere.Center) < minSeparation * minSeparation * 0.999f)
                    {
                        fits = false;
                        break;
                    }
                }

                if (fits && requiredDistance * requiredDistance < bestDistanceSq)
                {
                    bestDistanceSq = requiredDistance * requiredDistance;
                    bestSpot = candidate;
                }
            }

            if (float.IsFinite(bestDistanceSq))
                return bestSpot;

            float outer = 0f;
            foreach (PlacedSphere s in placed)
                outer = Math.Max(outer, s.Center.Length() + s.Radius);
            return SphereDirection(componentIndex + 1) * (outer + radius + margin);
        }

        private static Vector3 ComputeComponentCentroid(
            Vector3[] positions,
            Component component)
        {
            Vector3 total = Vector3.Zero;

            foreach (int nodeIndex in component.Nodes)
                total += positions[nodeIndex];

            return total / component.Nodes.Length;
        }

        private static float ComputeComponentRadius(
            Vector3[] positions,
            Component component)
        {
            Vector3 centroid = ComputeComponentCentroid(positions, component);
            float radius = 0f;

            foreach (int nodeIndex in component.Nodes)
                radius = Math.Max(radius, Vector3.Distance(positions[nodeIndex], centroid));

            return radius;
        }

        private static Vector3 ComputeCentroid(Vector3[] positions)
        {
            if (positions.Length == 0)
                return Vector3.Zero;

            Vector3 total = Vector3.Zero;

            foreach (Vector3 position in positions)
                total += position;

            return total / positions.Length;
        }

        private static void ComputeBounds(
            Vector3[] positions,
            out Vector3 boundsMinimum,
            out Vector3 boundsMaximum,
            out float radius)
        {
            if (positions.Length == 0)
            {
                boundsMinimum = Vector3.Zero;
                boundsMaximum = Vector3.Zero;
                radius = 0f;
                return;
            }

            boundsMinimum = new Vector3(float.PositiveInfinity);
            boundsMaximum = new Vector3(float.NegativeInfinity);
            radius = 0f;

            foreach (Vector3 position in positions)
            {
                boundsMinimum = Vector3.Min(boundsMinimum, position);
                boundsMaximum = Vector3.Max(boundsMaximum, position);
                radius = Math.Max(radius, position.Length());
            }
        }

        private static int SanitizePositions(Vector3[] positions)
        {
            int resetCount = 0;

            for (int index = 0; index < positions.Length; index++)
            {
                Vector3 position = positions[index];

                if (float.IsFinite(position.X)
                    && float.IsFinite(position.Y)
                    && float.IsFinite(position.Z))
                {
                    continue;
                }

                positions[index] = Vector3.Zero;
                resetCount++;
            }

            return resetCount;
        }

        private static Vector3 SphereDirection(int index)
        {
            double fraction = (index + 0.5) * GoldenRatioFraction;
            fraction -= Math.Floor(fraction);
            float z = 1f - 2f * (float)fraction;
            float radial = MathF.Sqrt(Math.Max(0f, 1f - z * z));
            double angle = index * GoldenAngle;

            return new Vector3(
                radial * (float)Math.Cos(angle),
                z,
                radial * (float)Math.Sin(angle));
        }

        private static Vector3 DeterministicDirection(int seed)
        {
            uint value = unchecked((uint)seed * 2654435761u);
            float x = ((value & 0xFF) / 127.5f) - 1f;
            float y = (((value >> 8) & 0xFF) / 127.5f) - 1f;
            float z = (((value >> 16) & 0xFF) / 127.5f) - 1f;
            Vector3 direction = new Vector3(x, y, z);

            if (direction.LengthSquared() < 1e-6f)
                return Vector3.UnitX;

            return Vector3.Normalize(direction);
        }

        private sealed class Component
        {
            public Component(int[] nodes)
            {
                Nodes = nodes;
                Directions = new Dictionary<int, Vector3>(nodes.Length);
            }

            public int[] Nodes { get; }

            public Dictionary<int, Vector3> Directions { get; }

            public Vector3 Centroid { get; set; }

            public float Radius { get; set; }
        }

        private readonly record struct PlacedSphere(Vector3 Center, float Radius);

        private sealed class SpatialGrid
        {
            private readonly float cellSize;
            private readonly int bucketMask;

            public SpatialGrid(int nodeCount, float cellSize)
            {
                this.cellSize = cellSize;
                int bucketCount = 1;
                while (bucketCount < Math.Max(16, nodeCount * 2))
                    bucketCount <<= 1;
                Head = new int[bucketCount];
                Next = new int[nodeCount];
                CellX = new int[nodeCount];
                CellY = new int[nodeCount];
                CellZ = new int[nodeCount];
                bucketMask = bucketCount - 1;
            }

            public int[] Head { get; }
            public int[] Next { get; }
            public int[] CellX { get; }
            public int[] CellY { get; }
            public int[] CellZ { get; }

            public void Rebuild(Vector3[] positions)
            {
                Array.Fill(Head, -1);

                // Insert backwards so bucket traversal preserves the old ascending node order.
                for (int index = positions.Length - 1; index >= 0; index--)
                {
                    Vector3 position = positions[index];
                    int x = (int)MathF.Floor(position.X / cellSize);
                    int y = (int)MathF.Floor(position.Y / cellSize);
                    int z = (int)MathF.Floor(position.Z / cellSize);
                    CellX[index] = x;
                    CellY[index] = y;
                    CellZ[index] = z;

                    int bucket = GetBucket(x, y, z);
                    Next[index] = Head[bucket];
                    Head[bucket] = index;
                }
            }

            public int GetBucket(int x, int y, int z)
            {
                unchecked
                {
                    uint hash = (uint)x * 73856093u
                        ^ (uint)y * 19349663u
                        ^ (uint)z * 83492791u;
                    return (int)(hash & (uint)bucketMask);
                }
            }
        }
    }
}
