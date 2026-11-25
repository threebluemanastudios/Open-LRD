using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

// Phase 0 benchmark: int8 weights, per-row float scales, float activations.
// Config approximates a 2B decoder (GQA, SwiGLU, RoPE, tied embeddings).

namespace RcBench;

using static RcBench.Cfg;

public static class Cfg
{
public const int Hidden = 2560;
public const int Layers = 28;
public const int Heads = 20;
public const int KvHeads = 4;
public const int HeadDim = 128;
public const int Ffn = 6912;
public const int Vocab = 32000;
public const int MaxSeq = 16384;

public static sbyte[] RandomI8(int n, Random rng)
{
    sbyte[] a = new sbyte[n];
    for (int i = 0; i < n; i++) a[i] = (sbyte)rng.Next(-127, 128);
    return a;
}

public static float[] RandomScales(int rows, Random rng)
{
    float[] s = new float[rows];
    for (int i = 0; i < rows; i++) s[i] = (float)(rng.NextDouble() * 0.002 + 0.0005);
    return s;
}

}

sealed class QMat
{
    public readonly int Rows, Cols;
    public readonly sbyte[] W;     // row-major [Rows, Cols]
    public readonly float[] Scale; // per row
    public QMat(int rows, int cols, Random rng)
    {
        Rows = rows; Cols = cols;
        W = RandomI8(rows * cols, rng);
        Scale = RandomScales(rows, rng);
    }
}

static class Kernels
{
    // Scalar reference: y[r] = scale[r] * sum_c W[r,c] * x[c]
    public static void MatVecScalar(QMat m, ReadOnlySpan<float> x, Span<float> y)
    {
        int cols = m.Cols;
        for (int r = 0; r < m.Rows; r++)
        {
            int baseIdx = r * cols;
            float acc = 0f;
            for (int c = 0; c < cols; c++)
                acc += m.W[baseIdx + c] * x[c];
            y[r] = acc * m.Scale[r];
        }
    }

    // AVX2/FMA: 8 weights per step. Fixed reduction order per row (deterministic on this machine).
    public static unsafe void MatVecAvx2(QMat m, ReadOnlySpan<float> x, Span<float> y)
    {
        int cols = m.Cols;
        int vecEnd = cols & ~7;
        fixed (sbyte* wp = m.W)
        fixed (float* xp = x)
        {
            for (int r = 0; r < m.Rows; r++)
            {
                sbyte* row = wp + (long)r * cols;
                Vector256<float> acc = Vector256<float>.Zero;
                int c = 0;
                for (; c < vecEnd; c += 8)
                {
                    long packed = Unsafe.ReadUnaligned<long>(row + c);
                    Vector128<sbyte> b8 = Vector128.CreateScalarUnsafe(packed).AsSByte();
                    Vector256<float> wf = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(b8));
                    Vector256<float> xv = Avx.LoadVector256(xp + c);
                    acc = Fma.MultiplyAdd(wf, xv, acc);
                }
                float sum = HorizontalSum(acc);
                for (; c < cols; c++) sum += row[c] * xp[c];
                y[r] = sum * m.Scale[r];
            }
        }
    }

    static float HorizontalSum(Vector256<float> v)
    {
        Vector128<float> lo = v.GetLower();
        Vector128<float> hi = v.GetUpper();
        Vector128<float> s = Sse.Add(lo, hi);
        s = Sse.Add(s, Sse.MoveHighToLow(s, s));
        s = Sse.Add(s, Sse.Shuffle(s, s, 0x55));
        return s.ToScalar();
    }

    // Row-parallel AVX2. Each row is still reduced in fixed order, so output is deterministic.
    public static void MatVecAvx2Parallel(QMat m, ReadOnlySpan<float> x, Span<float> y, int chunks)
    {
        // Parallel over row blocks; x is shared read-only.
        float[] xCopy = x.ToArray();
        float[] yArr = new float[m.Rows];
        int block = (m.Rows + chunks - 1) / chunks;
        Parallel.For(0, chunks, new ParallelOptions { MaxDegreeOfParallelism = chunks }, ci =>
        {
            int r0 = ci * block;
            int r1 = Math.Min(m.Rows, r0 + block);
            if (r0 >= r1) return;
            SubMatVecAvx2(m, xCopy, yArr, r0, r1);
        });
        yArr.AsSpan().CopyTo(y);
    }

    static unsafe void SubMatVecAvx2(QMat m, float[] x, float[] y, int r0, int r1)
    {
        int cols = m.Cols;
        int vecEnd = cols & ~7;
        fixed (sbyte* wp = m.W)
        fixed (float* xp = x)
        fixed (float* yp = y)
        {
            for (int r = r0; r < r1; r++)
            {
                sbyte* row = wp + (long)r * cols;
                Vector256<float> acc = Vector256<float>.Zero;
                int c = 0;
                for (; c < vecEnd; c += 8)
                {
                    long packed = Unsafe.ReadUnaligned<long>(row + c);
                    Vector128<sbyte> b8 = Vector128.CreateScalarUnsafe(packed).AsSByte();
                    Vector256<float> wf = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(b8));
                    Vector256<float> xv = Avx.LoadVector256(xp + c);
                    acc = Fma.MultiplyAdd(wf, xv, acc);
                }
                float sum = HorizontalSum(acc);
                for (; c < cols; c++) sum += row[c] * xp[c];
                yp[r] = sum * m.Scale[r];
            }
        }
    }
}

sealed class Model
{
    public readonly QMat[] Wq, Wk, Wv, Wo, W1, W3, W2; // per layer
    public readonly QMat Lm;                             // tied embedding / lm head [Vocab, Hidden]
    public readonly float[][] Kc, Vc;                    // per layer KV cache [MaxSeq, KvHeads*HeadDim]
    public readonly float[] Cos, Sin;                    // RoPE tables [MaxSeq, HeadDim/2]
    public readonly float[] Norm1, Norm2;                // per layer RMSNorm gains (simplified shared)
    public readonly float[] NormF;

    // scratch
    public readonly float[] X, Xn, Q, K, V, Att, Ctx, Gate, Up, Hid, Logits;

    public Model(Random rng)
    {
        Wq = new QMat[Layers]; Wk = new QMat[Layers]; Wv = new QMat[Layers]; Wo = new QMat[Layers];
        W1 = new QMat[Layers]; W3 = new QMat[Layers]; W2 = new QMat[Layers];
        int kvDim = KvHeads * HeadDim;
        for (int l = 0; l < Layers; l++)
        {
            Wq[l] = new QMat(Hidden, Hidden, rng);
            Wk[l] = new QMat(kvDim, Hidden, rng);
            Wv[l] = new QMat(kvDim, Hidden, rng);
            Wo[l] = new QMat(Hidden, Hidden, rng);
            W1[l] = new QMat(Ffn, Hidden, rng);
            W3[l] = new QMat(Ffn, Hidden, rng);
            W2[l] = new QMat(Hidden, Ffn, rng);
        }
        Lm = new QMat(Vocab, Hidden, rng);
        Kc = new float[Layers][]; Vc = new float[Layers][];
        for (int l = 0; l < Layers; l++)
        {
            Kc[l] = new float[MaxSeq * kvDim];
            Vc[l] = new float[MaxSeq * kvDim];
        }
        Cos = new float[MaxSeq * HeadDim / 2];
        Sin = new float[MaxSeq * HeadDim / 2];
        for (int p = 0; p < MaxSeq; p++)
            for (int i = 0; i < HeadDim / 2; i++)
            {
                double theta = p * Math.Pow(10000.0, -2.0 * i / HeadDim);
                Cos[p * HeadDim / 2 + i] = (float)Math.Cos(theta);
                Sin[p * HeadDim / 2 + i] = (float)Math.Sin(theta);
            }
        Norm1 = Ones(Hidden); Norm2 = Ones(Hidden); NormF = Ones(Hidden);
        X = new float[Hidden]; Xn = new float[Hidden];
        Q = new float[Hidden]; K = new float[kvDim]; V = new float[kvDim];
        Att = new float[MaxSeq]; Ctx = new float[Hidden];
        Gate = new float[Ffn]; Up = new float[Ffn]; Hid = new float[Ffn];
        Logits = new float[Vocab];
    }

    static float[] Ones(int n) { float[] a = new float[n]; Array.Fill(a, 1f); return a; }

    public long ParamCount()
    {
        long p = Lm.Rows * (long)Lm.Cols;
        for (int l = 0; l < Layers; l++)
            p += Wq[l].W.Length + Wk[l].W.Length + Wv[l].W.Length + Wo[l].W.Length
               + W1[l].W.Length + W3[l].W.Length + W2[l].W.Length;
        return p;
    }

    public long WeightBytes()
    {
        long b = Lm.W.Length + Lm.Scale.Length * 4L;
        for (int l = 0; l < Layers; l++)
        {
            foreach (QMat m in new[] { Wq[l], Wk[l], Wv[l], Wo[l], W1[l], W3[l], W2[l] })
                b += m.W.Length + m.Scale.Length * 4L;
        }
        return b;
    }

    public delegate void MatVec(QMat m, ReadOnlySpan<float> x, Span<float> y);

    // One decode step at position pos. Returns argmax token (keeps the work observable).

    public static bool VecAttn = false;

    static float AttnDotScalar(float[] q, int qOff, float[] k, int kOff)
    {
        float dot = 0f;
        for (int d = 0; d < HeadDim; d++) dot += q[qOff + d] * k[kOff + d];
        return dot;
    }

    static unsafe float AttnDotAvx2(float[] q, int qOff, float[] k, int kOff)
    {
        Vector256<float> acc = Vector256<float>.Zero;
        fixed (float* qp = q)
        fixed (float* kp = k)
        {
            for (int d = 0; d < HeadDim; d += 8)
                acc = Fma.MultiplyAdd(Avx.LoadVector256(qp + qOff + d), Avx.LoadVector256(kp + kOff + d), acc);
        }
        Vector128<float> s = Sse.Add(acc.GetLower(), acc.GetUpper());
        s = Sse.Add(s, Sse.MoveHighToLow(s, s));
        s = Sse.Add(s, Sse.Shuffle(s, s, 0x55));
        return s.ToScalar();
    }

    static unsafe void AttnAxpyAvx2(float[] ctx, int cOff, float a, float[] v, int vOff)
    {
        Vector256<float> av = Vector256.Create(a);
        fixed (float* cp = ctx)
        fixed (float* vp = v)
        {
            for (int d = 0; d < HeadDim; d += 8)
                Avx.Store(cp + cOff + d, Fma.MultiplyAdd(av, Avx.LoadVector256(vp + vOff + d), Avx.LoadVector256(cp + cOff + d)));
        }
    }

    public static long AttnTicks, TotalTicks;
    public int DecodeStep(int token, int pos, MatVec mv)
    {
        long tStart = Stopwatch.GetTimestamp();
        int kvDim = KvHeads * HeadDim;
        int group = Heads / KvHeads;
        float scaleAtt = 1f / MathF.Sqrt(HeadDim);

        // embedding lookup (int8 row dequant)
        for (int i = 0; i < Hidden; i++) X[i] = Lm.W[token * Hidden + i] * Lm.Scale[token];

        for (int l = 0; l < Layers; l++)
        {
            RmsNorm(X, Norm1, Xn);
            mv(Wq[l], Xn, Q);
            mv(Wk[l], Xn, K);
            mv(Wv[l], Xn, V);
            Rope(Q, Heads, pos);
            Rope(K, KvHeads, pos);
            Array.Copy(K, 0, Kc[l], pos * kvDim, kvDim);
            Array.Copy(V, 0, Vc[l], pos * kvDim, kvDim);

            long tAttn = Stopwatch.GetTimestamp();
            // attention over pos+1 cached positions
            Array.Clear(Ctx);
            for (int h = 0; h < Heads; h++)
            {
                int kvh = h / group;
                int qOff = h * HeadDim;
                for (int t = 0; t <= pos; t++)
                {
                    int kOff = t * kvDim + kvh * HeadDim;
                    Att[t] = (VecAttn ? AttnDotAvx2(Q, qOff, Kc[l], kOff) : AttnDotScalar(Q, qOff, Kc[l], kOff)) * scaleAtt;
                }
                Softmax(Att, pos + 1);
                for (int t = 0; t <= pos; t++)
                {
                    float a = Att[t];
                    int vOff = t * kvDim + kvh * HeadDim;
                    if (VecAttn) AttnAxpyAvx2(Ctx, qOff, a, Vc[l], vOff);
                    else for (int d = 0; d < HeadDim; d++) Ctx[qOff + d] += a * Vc[l][vOff + d];
                }
            }
            AttnTicks += Stopwatch.GetTimestamp() - tAttn;
            mv(Wo[l], Ctx, Xn);
            for (int i = 0; i < Hidden; i++) X[i] += Xn[i];

            RmsNorm(X, Norm2, Xn);
            mv(W1[l], Xn, Gate);
            mv(W3[l], Xn, Up);
            for (int i = 0; i < Ffn; i++)
            {
                float g = Gate[i];
                Hid[i] = (g / (1f + MathF.Exp(-g))) * Up[i];
            }
            mv(W2[l], Hid, Xn);
            for (int i = 0; i < Hidden; i++) X[i] += Xn[i];
        }

        RmsNorm(X, NormF, Xn);
        mv(Lm, Xn, Logits);
        TotalTicks += Stopwatch.GetTimestamp() - tStart;
        int best = 0;
        for (int i = 1; i < Vocab; i++) if (Logits[i] > Logits[best]) best = i;
        return best;
    }

    static void RmsNorm(float[] x, float[] g, float[] y)
    {
        float ss = 0f;
        for (int i = 0; i < x.Length; i++) ss += x[i] * x[i];
        float inv = 1f / MathF.Sqrt(ss / x.Length + 1e-6f);
        for (int i = 0; i < x.Length; i++) y[i] = x[i] * inv * g[i];
    }

    void Rope(float[] v, int heads, int pos)
    {
        int half = HeadDim / 2;
        for (int h = 0; h < heads; h++)
        {
            int off = h * HeadDim;
            for (int i = 0; i < half; i++)
            {
                float c = Cos[pos * half + i], s = Sin[pos * half + i];
                float a = v[off + i], b = v[off + i + half];
                v[off + i] = a * c - b * s;
                v[off + i + half] = a * s + b * c;
            }
        }
    }

    static void Softmax(float[] a, int n)
    {
        float m = float.NegativeInfinity;
        for (int i = 0; i < n; i++) if (a[i] > m) m = a[i];
        float sum = 0f;
        for (int i = 0; i < n; i++) { a[i] = MathF.Exp(a[i] - m); sum += a[i]; }
        float inv = 1f / sum;
        for (int i = 0; i < n; i++) a[i] *= inv;
    }
}

