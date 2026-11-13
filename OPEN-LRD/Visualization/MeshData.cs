using System;

namespace OPEN_LRD.Visualization
{
    internal sealed class MeshData
    {
        public static MeshData Empty { get; } = new MeshData(
            Array.Empty<float>(),
            Array.Empty<float>(),
            Array.Empty<int>());

        public MeshData(float[] positions, float[] normals, int[] indices)
        {
            ArgumentNullException.ThrowIfNull(positions);
            ArgumentNullException.ThrowIfNull(normals);
            ArgumentNullException.ThrowIfNull(indices);

            if (positions.Length % 3 != 0)
                throw new ArgumentException("Position data must contain xyz triples.", nameof(positions));

            if (normals.Length != positions.Length)
                throw new ArgumentException("Normal data must match the position count.", nameof(normals));

            if (indices.Length % 3 != 0)
                throw new ArgumentException("Index data must contain triangle triples.", nameof(indices));

            int vertexCount = positions.Length / 3;

            foreach (int index in indices)
            {
                if (index < 0 || index >= vertexCount)
                    throw new ArgumentOutOfRangeException(nameof(indices), "An index is outside the vertex range.");
            }

            Positions = positions;
            Normals = normals;
            Indices = indices;
        }

        public float[] Positions { get; }

        public float[] Normals { get; }

        public int[] Indices { get; }

        public int VertexCount => Positions.Length / 3;

        public int TriangleCount => Indices.Length / 3;

        public int IndexCount => Indices.Length;

        public bool IsEmpty => VertexCount == 0;
    }
}
