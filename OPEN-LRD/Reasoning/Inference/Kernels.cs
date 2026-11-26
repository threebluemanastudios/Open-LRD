using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>Matvec implementation selector.</summary>
    public enum MatVecKernel
    {
        /// <summary>Plain loops. Reference for tests.</summary>
        Scalar,
        /// <summary>AVX2/FMA, single thread.</summary>
        Avx2Serial,
        /// <summary>AVX2/FMA, row blocks in parallel through a <see cref="MatVecEngine"/>.</summary>
        Avx2Parallel,
    }

    /// <summary>
    /// int8 matrix-vector kernels. Each output element is reduced in a fixed order that depends only on the
    /// row and the column count, so results do not change with thread count or batch size.
    /// </summary>
    public static class Kernels
    {
        /// <summary>True when the AVX2 and FMA kernels can run on this machine.</summary>
        public static bool SimdSupported => Avx2.IsSupported && Fma.IsSupported;

        /// <summary>Scalar reference: y[r] = Scale[r] * sum_c W[r,c] * x[c].</summary>
        /// <param name="m">Weight matrix.</param>
        /// <param name="x">Input, length m.Cols.</param>
        /// <param name="y">Output, length at least m.Rows.</param>
        public static void MatVecScalar(QMat m, ReadOnlySpan<float> x, Span<float> y)
        {
            CheckShape(m, x, y);
            for (int r = 0; r < m.Rows; r++)
            {
                ReadOnlySpan<sbyte> row = m.Row(r);
                float acc = 0f;
                for (int c = 0; c < row.Length; c++) acc += row[c] * x[c];
                y[r] = acc * m.Scale[r];
            }
        }

        /// <summary>AVX2/FMA matvec on the calling thread.</summary>
        /// <param name="m">Weight matrix.</param>
        /// <param name="x">Input, length m.Cols.</param>
        /// <param name="y">Output, length at least m.Rows.</param>
        public static void MatVecAvx2(QMat m, ReadOnlySpan<float> x, Span<float> y)
        {
            CheckShape(m, x, y);
            RequireSimd();
            for (int r = 0; r < m.Rows; r++)
            {
                y[r] = DotRowAvx2(m.Row(r), x) * m.Scale[r];
            }
        }

        /// <summary>
        /// Batched matvec: Y[b] = m * X[b] for each of <paramref name="batch"/> inputs stored contiguously.
        /// Rows are the outer loop, so each int8 row is read once and reused from cache for every input.
        /// Each output uses <see cref="DotRowAvx2"/>, so a batch of B gives the same bits as B single matvecs.
        /// </summary>
        /// <param name="m">Weight matrix.</param>
        /// <param name="x">Inputs, batch * m.Cols values, row-major.</param>
        /// <param name="batch">Number of inputs, positive.</param>
        /// <param name="y">Outputs, batch * m.Rows values, row-major.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void MatMat(QMat m, ReadOnlySpan<float> x, int batch, Span<float> y)
        {
            CheckBatch(m, x, batch, y);
            RequireSimd();
            int cols = m.Cols, rows = m.Rows;
            for (int r = 0; r < rows; r++)
            {
                ReadOnlySpan<sbyte> row = m.Row(r);
                float scale = m.Scale[r];
                for (int b0 = 0; b0 < batch; b0 += 4)
                {
                    int last = batch - 1;
                    DotRowAvx2Group4(row,
                        x.Slice(Math.Min(b0, last) * cols, cols), x.Slice(Math.Min(b0 + 1, last) * cols, cols),
                        x.Slice(Math.Min(b0 + 2, last) * cols, cols), x.Slice(Math.Min(b0 + 3, last) * cols, cols),
                        out float s0, out float s1, out float s2, out float s3);
                    y[b0 * rows + r] = s0 * scale;
                    if (b0 + 1 < batch) y[(b0 + 1) * rows + r] = s1 * scale;
                    if (b0 + 2 < batch) y[(b0 + 2) * rows + r] = s2 * scale;
                    if (b0 + 3 < batch) y[(b0 + 3) * rows + r] = s3 * scale;
                }
            }
        }

        internal static void CheckBatch(QMat m, ReadOnlySpan<float> x, int batch, Span<float> y)
        {
            if (m is null) throw new ArgumentNullException(nameof(m));
            if (batch < 1) throw new ArgumentOutOfRangeException(nameof(batch));
            if (x.Length != batch * m.Cols) throw new ArgumentException("Input length must be batch * Cols.", nameof(x));
            if (y.Length < batch * m.Rows) throw new ArgumentException("Output too short.", nameof(y));
        }

        /// <summary>
        /// Dot product of one int8 row with a float vector. Each step covers 16 columns with two FMAs into one
        /// accumulator. Reduction order is fixed by the column count.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static float DotRowAvx2(ReadOnlySpan<sbyte> row, ReadOnlySpan<float> x)
        {
            int cols = row.Length;
            ref byte wb = ref Unsafe.As<sbyte, byte>(ref MemoryMarshal.GetReference(row));
            ref float xr = ref MemoryMarshal.GetReference(x);
            Vector256<float> acc = Vector256<float>.Zero;
            int c = 0;
            for (; c + 8 <= cols; c += 8)
            {
                // Read 8 int8 weights as one 64-bit value, widen to int32, then to float (same as the scratch kernel).
                long packed = Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref wb, (nuint)c));
                Vector128<sbyte> b8 = Vector128.CreateScalarUnsafe(packed).AsSByte();
                Vector256<float> wf = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(b8));
                acc = Fma.MultiplyAdd(wf, Vector256.LoadUnsafe(ref xr, (nuint)c), acc);
            }
            float sum = HorizontalSum(acc);
            for (; c < cols; c++) sum += row[c] * x[c];
            return sum;
        }

        /// <summary>
        /// Four dot products of one int8 row with four inputs. The row is converted to float once per 8-column chunk and
        /// fed to four accumulators. Each accumulator is reduced in the same order as <see cref="DotRowAvx2"/>, so each
        /// result has the same bits as a single call.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void DotRowAvx2Group4(ReadOnlySpan<sbyte> row,
            ReadOnlySpan<float> x0, ReadOnlySpan<float> x1, ReadOnlySpan<float> x2, ReadOnlySpan<float> x3,
            out float s0, out float s1, out float s2, out float s3)
        {
            int cols = row.Length;
            ref byte wb = ref Unsafe.As<sbyte, byte>(ref MemoryMarshal.GetReference(row));
            ref float r0 = ref MemoryMarshal.GetReference(x0);
            ref float r1 = ref MemoryMarshal.GetReference(x1);
            ref float r2 = ref MemoryMarshal.GetReference(x2);
            ref float r3 = ref MemoryMarshal.GetReference(x3);
            Vector256<float> a0 = Vector256<float>.Zero, a1 = a0, a2 = a0, a3 = a0;
            int c = 0;
            for (; c + 8 <= cols; c += 8)
            {
                long packed = Unsafe.ReadUnaligned<long>(ref Unsafe.Add(ref wb, (nuint)c));
                Vector128<sbyte> b8 = Vector128.CreateScalarUnsafe(packed).AsSByte();
                Vector256<float> wf = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(b8));
                a0 = Fma.MultiplyAdd(wf, Vector256.LoadUnsafe(ref r0, (nuint)c), a0);
                a1 = Fma.MultiplyAdd(wf, Vector256.LoadUnsafe(ref r1, (nuint)c), a1);
                a2 = Fma.MultiplyAdd(wf, Vector256.LoadUnsafe(ref r2, (nuint)c), a2);
                a3 = Fma.MultiplyAdd(wf, Vector256.LoadUnsafe(ref r3, (nuint)c), a3);
            }
            float t0 = HorizontalSum(a0), t1 = HorizontalSum(a1), t2 = HorizontalSum(a2), t3 = HorizontalSum(a3);
            for (; c < cols; c++)
            {
                float w = row[c];
                t0 += w * x0[c];
                t1 += w * x1[c];
                t2 += w * x2[c];
                t3 += w * x3[c];
            }
            s0 = t0; s1 = t1; s2 = t2; s3 = t3;
        }

        private static float HorizontalSum(Vector256<float> v)
        {
            Vector128<float> s = Sse.Add(v.GetLower(), v.GetUpper());
            s = Sse.Add(s, Sse.MoveHighToLow(s, s));
            s = Sse.Add(s, Sse.Shuffle(s, s, 0x55));
            return s.ToScalar();
        }

        internal static void CheckShape(QMat m, ReadOnlySpan<float> x, Span<float> y)
        {
            if (m is null) throw new ArgumentNullException(nameof(m));
            if (x.Length != m.Cols) throw new ArgumentException($"Input length must be {m.Cols}.", nameof(x));
            if (y.Length < m.Rows) throw new ArgumentException($"Output length must be at least {m.Rows}.", nameof(y));
        }

        internal static void RequireSimd()
        {
            if (!SimdSupported) throw new PlatformNotSupportedException("AVX2 and FMA are required for this kernel.");
        }
    }

    /// <summary>
    /// Row-parallel AVX2 matvec. Owns its input and output buffers, so a call allocates nothing.
    /// Not reentrant: use one engine per thread of control.
    /// </summary>
    public sealed class MatVecEngine
    {
        private readonly int blocks;
        private readonly Action<int> body;
        private float[] inBuf = Array.Empty<float>();
        private float[] outBuf = Array.Empty<float>();
        private QMat? current;
        private int rows;
        private int blockRows;
        private int batch = 1;

        /// <summary>Creates an engine that splits each matvec into <paramref name="blocks"/> row blocks.</summary>
        /// <param name="blocks">Row blocks per call, positive. Usually the processor count.</param>
        public MatVecEngine(int blocks)
        {
            if (blocks < 1) throw new ArgumentOutOfRangeException(nameof(blocks));
            this.blocks = blocks;
            body = RunBlock;
        }

        /// <summary>Computes y = m * x. The result is bit-identical to <see cref="Kernels.MatVecAvx2"/>.</summary>
        /// <param name="m">Weight matrix.</param>
        /// <param name="x">Input, length m.Cols.</param>
        /// <param name="y">Output, length at least m.Rows.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Run(QMat m, ReadOnlySpan<float> x, Span<float> y)
        {
            Kernels.CheckShape(m, x, y);
            RunBatch(m, 1, x, y);
        }

        /// <summary>
        /// Batched form: Y[b] = m * X[b] for <paramref name="batch"/> inputs. Row blocks run in parallel, and each
        /// row is read once for all inputs. Output for each input is bit-identical to <see cref="Run"/>.
        /// </summary>
        /// <param name="m">Weight matrix.</param>
        /// <param name="batch">Number of inputs, positive.</param>
        /// <param name="x">Inputs, batch * m.Cols values.</param>
        /// <param name="y">Outputs, batch * m.Rows values.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void RunBatch(QMat m, int batch, ReadOnlySpan<float> x, Span<float> y)
        {
            Kernels.CheckBatch(m, x, batch, y);
            Kernels.RequireSimd();
            if (inBuf.Length < batch * m.Cols) inBuf = new float[batch * m.Cols];
            if (outBuf.Length < batch * m.Rows) outBuf = new float[batch * m.Rows];
            x.Slice(0, batch * m.Cols).CopyTo(inBuf);
            current = m;
            rows = m.Rows;
            this.batch = batch;
            if (blocks == 1 || rows < 256)
            {
                blockRows = rows;   // one block covers every row
                RunBlock(0);
            }
            else
            {
                blockRows = (rows + blocks - 1) / blocks;
                Parallel.For(0, blocks, body);
            }
            outBuf.AsSpan(0, batch * rows).CopyTo(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void RunBlock(int block)
        {
            int r0 = block * blockRows;
            int r1 = Math.Min(rows, r0 + blockRows);
            if (r0 >= r1) return;
            QMat m = current!;
            int cols = m.Cols;
            for (int r = r0; r < r1; r++)
            {
                ReadOnlySpan<sbyte> row = m.Row(r);
                float scale = m.Scale[r];
                for (int b0 = 0; b0 < batch; b0 += 4)
                {
                    int last = batch - 1;
                    ReadOnlySpan<float> x0 = inBuf.AsSpan(Math.Min(b0, last) * cols, cols);
                    ReadOnlySpan<float> x1 = inBuf.AsSpan(Math.Min(b0 + 1, last) * cols, cols);
                    ReadOnlySpan<float> x2 = inBuf.AsSpan(Math.Min(b0 + 2, last) * cols, cols);
                    ReadOnlySpan<float> x3 = inBuf.AsSpan(Math.Min(b0 + 3, last) * cols, cols);
                    Kernels.DotRowAvx2Group4(row, x0, x1, x2, x3, out float s0, out float s1, out float s2, out float s3);
                    outBuf[b0 * rows + r] = s0 * scale;
                    if (b0 + 1 < batch) outBuf[(b0 + 1) * rows + r] = s1 * scale;
                    if (b0 + 2 < batch) outBuf[(b0 + 2) * rows + r] = s2 * scale;
                    if (b0 + 3 < batch) outBuf[(b0 + 3) * rows + r] = s3 * scale;
                }
            }
        }
    }
}
