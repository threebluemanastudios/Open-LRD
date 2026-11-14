using System;
using System.Collections.Generic;
using System.Numerics;

namespace OPEN_LRD.Visualization
{
    internal static class GraphMeshBuilder
    {
        private const int CubeVerticesPerNode = 24;
        private const int CubeIndicesPerNode = 36;
        private const int LinkVerticesPerEdge = 8;
        private const int LinkIndicesPerEdge = 12;

        private static readonly CubeFace[] cubeFaces = new[]
        {
            new CubeFace(
                new Vector3(1f, 0f, 0f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f)),
            new CubeFace(
                new Vector3(-1f, 0f, 0f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f)),
            new CubeFace(
                new Vector3(0f, 1f, 0f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f)),
            new CubeFace(
                new Vector3(0f, -1f, 0f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f)),
            new CubeFace(
                new Vector3(0f, 0f, 1f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f)),
            new CubeFace(
                new Vector3(0f, 0f, -1f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f))
        };

        public static MeshData CreateCube(Vector3 center, double size)
        {
            ValidatePositiveFinite(size, nameof(size));

            float[] positions = new float[CubeVerticesPerNode * 3];
            float[] normals = new float[CubeVerticesPerNode * 3];
            int[] indices = new int[CubeIndicesPerNode];

            WriteCube(
                positions,
                normals,
                indices,
                vertexStart: 0,
                indexStart: 0,
                center,
                (float)size);

            return new MeshData(positions, normals, indices);
        }

        public static MeshData BuildHighlightCube(Vector3 center, double cubeSize)
        {
            ValidatePositiveFinite(cubeSize, nameof(cubeSize));
            return CreateCube(center, cubeSize * 1.15);
        }

        public static MeshData BuildNodeCubes(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double cubeSize)
        {
            return BuildNodeCubesCore(snapshot, positions, cubeSize, nodeTypeId: null);
        }

        public static MeshData BuildNodeCubesForType(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double cubeSize,
            int nodeTypeId)
        {
            return BuildNodeCubesCore(snapshot, positions, cubeSize, nodeTypeId);
        }

        public static Dictionary<int, MeshData> BuildNodeCubesByType(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double cubeSize)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(positions);
            ValidatePositiveFinite(cubeSize, nameof(cubeSize));

            if (positions.Count < snapshot.Nodes.Count)
                throw new ArgumentException("Every snapshot node must have a position.", nameof(positions));

            Dictionary<int, List<int>> nodeIndicesByType = new Dictionary<int, List<int>>();
            for (int index = 0; index < snapshot.Nodes.Count; index++)
            {
                int typeId = snapshot.Nodes[index].TypeId;
                if (!nodeIndicesByType.TryGetValue(typeId, out List<int>? indices))
                {
                    indices = new List<int>();
                    nodeIndicesByType.Add(typeId, indices);
                }
                indices.Add(index);
            }

            Dictionary<int, MeshData> meshes = new Dictionary<int, MeshData>(nodeIndicesByType.Count);
            foreach ((int typeId, List<int> indices) in nodeIndicesByType)
                meshes.Add(typeId, BuildNodeCubesForIndices(indices, positions, cubeSize));

            return meshes;
        }

        public static MeshData BuildLinkLines(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double lineWidth,
            double cubeSize)
        {
            ValidateLinkArguments(snapshot, positions, lineWidth, cubeSize);
            Dictionary<LinkPair, int> pairCounts = BuildPairCounts(snapshot);
            return BuildLinkLinesCore(snapshot.Links, positions, lineWidth, cubeSize, pairCounts);
        }

        public static MeshData BuildLinkLinesForType(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double lineWidth,
            double cubeSize,
            int forwardTypeId)
        {
            ValidateLinkArguments(snapshot, positions, lineWidth, cubeSize);
            List<LinkRecord> matching = new List<LinkRecord>();
            foreach (LinkRecord link in snapshot.Links)
            {
                if (link.ForwardTypeId == forwardTypeId)
                    matching.Add(link);
            }

            return BuildLinkLinesCore(matching, positions, lineWidth, cubeSize, BuildPairCounts(snapshot));
        }

        public static Dictionary<int, MeshData> BuildLinkLinesByType(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double lineWidth,
            double cubeSize)
        {
            ValidateLinkArguments(snapshot, positions, lineWidth, cubeSize);

            Dictionary<int, List<LinkRecord>> linksByType = new Dictionary<int, List<LinkRecord>>();
            foreach (LinkRecord link in snapshot.Links)
            {
                if (!linksByType.TryGetValue(link.ForwardTypeId, out List<LinkRecord>? links))
                {
                    links = new List<LinkRecord>();
                    linksByType.Add(link.ForwardTypeId, links);
                }
                links.Add(link);
            }

            Dictionary<LinkPair, int> pairCounts = BuildPairCounts(snapshot);
            Dictionary<int, MeshData> meshes = new Dictionary<int, MeshData>(linksByType.Count);
            foreach ((int forwardTypeId, List<LinkRecord> links) in linksByType)
                meshes.Add(forwardTypeId, BuildLinkLinesCore(links, positions, lineWidth, cubeSize, pairCounts));

            return meshes;
        }

        private static MeshData BuildNodeCubesCore(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double cubeSize,
            int? nodeTypeId)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(positions);
            ValidatePositiveFinite(cubeSize, nameof(cubeSize));

            if (positions.Count < snapshot.Nodes.Count)
                throw new ArgumentException("Every snapshot node must have a position.", nameof(positions));

            int selectedCount = 0;

            for (int index = 0; index < snapshot.Nodes.Count; index++)
            {
                if (nodeTypeId is null || snapshot.Nodes[index].TypeId == nodeTypeId.Value)
                    selectedCount++;
            }

            if (selectedCount == 0)
                return MeshData.Empty;

            float[] cubePositions = new float[selectedCount * CubeVerticesPerNode * 3];
            float[] cubeNormals = new float[selectedCount * CubeVerticesPerNode * 3];
            int[] cubeIndices = new int[selectedCount * CubeIndicesPerNode];
            int vertexStart = 0;
            int indexStart = 0;

            for (int index = 0; index < snapshot.Nodes.Count; index++)
            {
                if (nodeTypeId is not null && snapshot.Nodes[index].TypeId != nodeTypeId.Value)
                    continue;

                WriteCube(
                    cubePositions,
                    cubeNormals,
                    cubeIndices,
                    vertexStart,
                    indexStart,
                    positions[index],
                    (float)cubeSize);

                vertexStart += CubeVerticesPerNode;
                indexStart += CubeIndicesPerNode;
            }

            return new MeshData(cubePositions, cubeNormals, cubeIndices);
        }

        private static MeshData BuildNodeCubesForIndices(
            IReadOnlyList<int> nodeIndices,
            IReadOnlyList<Vector3> positions,
            double cubeSize)
        {
            if (nodeIndices.Count == 0)
                return MeshData.Empty;

            float[] cubePositions = new float[nodeIndices.Count * CubeVerticesPerNode * 3];
            float[] cubeNormals = new float[nodeIndices.Count * CubeVerticesPerNode * 3];
            int[] cubeIndices = new int[nodeIndices.Count * CubeIndicesPerNode];
            int vertexStart = 0;
            int indexStart = 0;

            foreach (int nodeIndex in nodeIndices)
            {
                WriteCube(
                    cubePositions,
                    cubeNormals,
                    cubeIndices,
                    vertexStart,
                    indexStart,
                    positions[nodeIndex],
                    (float)cubeSize);
                vertexStart += CubeVerticesPerNode;
                indexStart += CubeIndicesPerNode;
            }

            return new MeshData(cubePositions, cubeNormals, cubeIndices);
        }

        private static MeshData BuildLinkLinesCore(
            IReadOnlyList<LinkRecord> links,
            IReadOnlyList<Vector3> positions,
            double lineWidth,
            double cubeSize,
            IReadOnlyDictionary<LinkPair, int> pairCounts)
        {
            if (links.Count == 0)
                return MeshData.Empty;

            Dictionary<LinkPair, int> pairOrdinals = new Dictionary<LinkPair, int>();

            float[] linkPositions = new float[links.Count * LinkVerticesPerEdge * 3];
            float[] linkNormals = new float[links.Count * LinkVerticesPerEdge * 3];
            int[] linkIndices = new int[links.Count * LinkIndicesPerEdge];
            int vertexStart = 0;
            int indexStart = 0;
            float halfWidth = (float)(lineWidth * 0.5);

            foreach (LinkRecord link in links)
            {
                Vector3 start = positions[link.AIndex];
                Vector3 end = positions[link.BIndex];
                Vector3 delta = end - start;
                float length = delta.Length();
                Vector3 direction = length > 1e-6f
                    ? delta / length
                    : Vector3.UnitX;

                Vector3 reference = Math.Abs(direction.Y) < 0.9f
                    ? Vector3.UnitY
                    : Vector3.UnitX;
                Vector3 u = Vector3.Normalize(Vector3.Cross(direction, reference));
                Vector3 v = Vector3.Normalize(Vector3.Cross(direction, u));

                LinkPair pair = new LinkPair(link.AIndex, link.BIndex);
                int pairCount = pairCounts[pair];
                int ordinal = pairOrdinals.TryGetValue(pair, out int currentOrdinal)
                    ? currentOrdinal
                    : 0;
                pairOrdinals[pair] = ordinal + 1;

                float offset = (float)((ordinal - (pairCount - 1) * 0.5) * cubeSize * 0.4);
                Vector3 shift = v * offset;
                start += shift;
                end += shift;

                Vector3 uWidth = u * halfWidth;
                Vector3 vWidth = v * halfWidth;

                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 0, start - uWidth, v);
                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 1, start + uWidth, v);
                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 2, end + uWidth, v);
                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 3, end - uWidth, v);

                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 4, start - vWidth, u);
                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 5, start + vWidth, u);
                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 6, end + vWidth, u);
                WriteLinkVertex(linkPositions, linkNormals, vertexStart + 7, end - vWidth, u);

                linkIndices[indexStart + 0] = vertexStart + 0;
                linkIndices[indexStart + 1] = vertexStart + 1;
                linkIndices[indexStart + 2] = vertexStart + 2;
                linkIndices[indexStart + 3] = vertexStart + 0;
                linkIndices[indexStart + 4] = vertexStart + 2;
                linkIndices[indexStart + 5] = vertexStart + 3;
                linkIndices[indexStart + 6] = vertexStart + 4;
                linkIndices[indexStart + 7] = vertexStart + 5;
                linkIndices[indexStart + 8] = vertexStart + 6;
                linkIndices[indexStart + 9] = vertexStart + 4;
                linkIndices[indexStart + 10] = vertexStart + 6;
                linkIndices[indexStart + 11] = vertexStart + 7;

                vertexStart += LinkVerticesPerEdge;
                indexStart += LinkIndicesPerEdge;
            }

            return new MeshData(linkPositions, linkNormals, linkIndices);
        }

        private static void ValidateLinkArguments(
            GraphSnapshot snapshot,
            IReadOnlyList<Vector3> positions,
            double lineWidth,
            double cubeSize)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(positions);
            ValidatePositiveFinite(lineWidth, nameof(lineWidth));
            ValidatePositiveFinite(cubeSize, nameof(cubeSize));

            if (positions.Count < snapshot.Nodes.Count)
                throw new ArgumentException("Every snapshot node must have a position.", nameof(positions));
        }

        private static Dictionary<LinkPair, int> BuildPairCounts(GraphSnapshot snapshot)
        {
            Dictionary<LinkPair, int> counts = new Dictionary<LinkPair, int>();

            foreach (LinkRecord link in snapshot.Links)
            {
                LinkPair pair = new LinkPair(link.AIndex, link.BIndex);
                counts[pair] = counts.TryGetValue(pair, out int count) ? count + 1 : 1;
            }

            return counts;
        }

        private static void WriteCube(
            float[] positions,
            float[] normals,
            int[] indices,
            int vertexStart,
            int indexStart,
            Vector3 center,
            float size)
        {
            for (int faceIndex = 0; faceIndex < cubeFaces.Length; faceIndex++)
            {
                CubeFace face = cubeFaces[faceIndex];
                int faceVertexStart = vertexStart + faceIndex * 4;

                WriteCubeVertex(positions, normals, faceVertexStart + 0, center, size, face.Normal, face.V0);
                WriteCubeVertex(positions, normals, faceVertexStart + 1, center, size, face.Normal, face.V1);
                WriteCubeVertex(positions, normals, faceVertexStart + 2, center, size, face.Normal, face.V2);
                WriteCubeVertex(positions, normals, faceVertexStart + 3, center, size, face.Normal, face.V3);

                int faceIndexStart = indexStart + faceIndex * 6;
                indices[faceIndexStart + 0] = faceVertexStart + 0;
                indices[faceIndexStart + 1] = faceVertexStart + 1;
                indices[faceIndexStart + 2] = faceVertexStart + 2;
                indices[faceIndexStart + 3] = faceVertexStart + 0;
                indices[faceIndexStart + 4] = faceVertexStart + 2;
                indices[faceIndexStart + 5] = faceVertexStart + 3;
            }
        }

        private static void WriteCubeVertex(
            float[] positions,
            float[] normals,
            int vertexIndex,
            Vector3 center,
            float size,
            Vector3 normal,
            Vector3 offset)
        {
            int positionIndex = vertexIndex * 3;
            positions[positionIndex + 0] = center.X + offset.X * size;
            positions[positionIndex + 1] = center.Y + offset.Y * size;
            positions[positionIndex + 2] = center.Z + offset.Z * size;
            normals[positionIndex + 0] = normal.X;
            normals[positionIndex + 1] = normal.Y;
            normals[positionIndex + 2] = normal.Z;
        }

        private static void WriteLinkVertex(
            float[] positions,
            float[] normals,
            int vertexIndex,
            Vector3 position,
            Vector3 normal)
        {
            int positionIndex = vertexIndex * 3;
            positions[positionIndex + 0] = position.X;
            positions[positionIndex + 1] = position.Y;
            positions[positionIndex + 2] = position.Z;
            normals[positionIndex + 0] = normal.X;
            normals[positionIndex + 1] = normal.Y;
            normals[positionIndex + 2] = normal.Z;
        }

        private static void ValidatePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and greater than zero.");
            }
        }

        private readonly record struct CubeFace(
            Vector3 Normal,
            Vector3 V0,
            Vector3 V1,
            Vector3 V2,
            Vector3 V3);

        private readonly record struct LinkPair(int AIndex, int BIndex);
    }
}
