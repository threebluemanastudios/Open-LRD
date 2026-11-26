using System;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>Row-major int8 matrix with one float scale per row. Dequantized value = W[r,c] * Scale[r].</summary>
    public sealed class QMat
    {
        /// <summary>Creates a matrix from existing storage. Arrays are not copied.</summary>
        /// <param name="rows">Row count, positive.</param>
        /// <param name="cols">Column count, positive.</param>
        /// <param name="w">Row-major weights, length rows * cols.</param>
        /// <param name="scale">Per-row scales, length rows.</param>
        public QMat(int rows, int cols, sbyte[] w, float[] scale)
        {
            if (rows < 1 || cols < 1) throw new ArgumentOutOfRangeException(nameof(rows), "Dimensions must be positive.");
            if (w is null || w.Length != (long)rows * cols) throw new ArgumentException("Weight length must be rows * cols.", nameof(w));
            if (scale is null || scale.Length != rows) throw new ArgumentException("Scale length must equal rows.", nameof(scale));
            Rows = rows;
            Cols = cols;
            W = w;
            Scale = scale;
        }

        /// <summary>Row count.</summary>
        public int Rows { get; }

        /// <summary>Column count.</summary>
        public int Cols { get; }

        /// <summary>Row-major int8 weights.</summary>
        public sbyte[] W { get; }

        /// <summary>Per-row dequantization scales.</summary>
        public float[] Scale { get; }

        /// <summary>Returns row <paramref name="r"/> as a span over the weights.</summary>
        /// <param name="r">Row index.</param>
        public ReadOnlySpan<sbyte> Row(int r) => W.AsSpan(r * Cols, Cols);

        /// <summary>Creates a matrix with random int8 weights in [-127, 127] and scales in [0.0005, 0.0025).</summary>
        /// <param name="rows">Row count.</param>
        /// <param name="cols">Column count.</param>
        /// <param name="rng">Random source (seeded by the caller for reproducibility).</param>
        public static QMat Random(int rows, int cols, Random rng)
        {
            sbyte[] w = new sbyte[(long)rows * cols];
            for (int i = 0; i < w.Length; i++) w[i] = (sbyte)rng.Next(-127, 128);
            float[] s = new float[rows];
            for (int i = 0; i < rows; i++) s[i] = (float)(rng.NextDouble() * 0.002 + 0.0005);
            return new QMat(rows, cols, w, s);
        }
    }
}
