using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>Attention kernel selector.</summary>
    public enum AttnKernel
    {
        /// <summary>One query head at a time, plain loops. Works on either cache mode. Reference for tests.</summary>
        Scalar,
        /// <summary>Fused GQA kernel over an fp32 cache.</summary>
        FusedFp32,
        /// <summary>Fused GQA kernel over an int8 cache.</summary>
        FusedInt8,
    }

    /// <summary>
    /// Attention over a <see cref="KvCache"/> for one query position.
    /// The fused kernel reads each key and value row once and uses it for all query heads of its group.
    /// Work is split into (KV head, chunk) tasks of <see cref="Chunk"/> positions. Each task owns a scratch
    /// slot, and per-head results are merged in chunk order, so output does not depend on scheduling.
    /// Not reentrant: use one instance per decoder.
    /// </summary>
    public sealed class Attention
    {
        /// <summary>Positions per task.</summary>
        public const int Chunk = 1024;

        private readonly ModelConfig cfg;
        private readonly int maxChunks;
        private readonly int group;
        private readonly float[] qBuf;
        private readonly float[] scores;
        private readonly float[] accs;
        private readonly float[] meta;   // per (slot, query): max, then sum
        private readonly float[] kbuf;
        private readonly float[] vbuf;
        private readonly Action<int> body;

        // Per-call parameters read by the task body.
        private KvCache? kv;
        private int curLayer;
        private int curPos;
        private int curChunks;
        private float curScale;
        private bool curInt8;
        private PagedKvSequence? curPaged;

        /// <summary>Creates scratch for the given shape.</summary>
        /// <param name="config">Model shape. Validated.</param>
        public Attention(ModelConfig config)
        {
            config.Validate();
            cfg = config;
            group = config.Group;
            maxChunks = (config.MaxSeq + Chunk - 1) / Chunk;
            int slots = config.KvHeads * maxChunks;
            qBuf = new float[config.Heads * config.HeadDim];
            scores = new float[slots * group * Chunk];
            accs = new float[slots * group * config.HeadDim];
            meta = new float[slots * group * 2];
            kbuf = new float[slots * config.HeadDim];
            vbuf = new float[slots * config.HeadDim];
            body = TaskBody;
        }

        /// <summary>
        /// Attention output for query position <paramref name="pos"/> over cached positions 0..pos of one layer.
        /// The current position must already be written to the cache.
        /// </summary>
        /// <param name="cache">KV cache. Mode must match the kernel.</param>
        /// <param name="layer">Layer index.</param>
        /// <param name="pos">Current position.</param>
        /// <param name="q">Query, Heads * HeadDim values.</param>
        /// <param name="ctx">Output, Heads * HeadDim values. Overwritten.</param>
        /// <param name="kernel">Kernel to run.</param>
        /// <param name="parallel">Run fused tasks on the thread pool.</param>
        public void Run(KvCache cache, int layer, int pos, ReadOnlySpan<float> q, Span<float> ctx, AttnKernel kernel, bool parallel = true)
        {
            CheckArgs(cache, layer, pos, q, ctx);
            if (kernel == AttnKernel.Scalar)
            {
                ScalarReference(cache, layer, pos, q, ctx);
                return;
            }
            bool wantInt8 = kernel == AttnKernel.FusedInt8;
            if (wantInt8 != (cache.Mode == KvMode.Int8))
                throw new InvalidOperationException($"Kernel {kernel} does not match KV mode {cache.Mode}.");
            Kernels.RequireSimd();

            q.CopyTo(qBuf);
            kv = cache;
            curPaged = null;
            batchT = 0;
            curLayer = layer;
            curPos = pos;
            curChunks = pos / Chunk + 1;
            curScale = 1f / MathF.Sqrt(cfg.HeadDim);
            curInt8 = wantInt8;

            int tasks = cfg.KvHeads * curChunks;
            if (parallel && tasks >= 8) Parallel.For(0, tasks, body);
            else for (int t = 0; t < tasks; t++) TaskBody(t);

            Merge(ctx, curChunks);
        }

        /// <summary>
        /// Fused attention over a paged sequence. Same arithmetic and merge order as <see cref="Run"/>, so the output
        /// is bit-identical to the contiguous cache holding the same data.
        /// </summary>
        /// <param name="seq">Paged sequence. Must already hold position <paramref name="pos"/>.</param>
        /// <param name="layer">Layer index.</param>
        /// <param name="pos">Current position.</param>
        /// <param name="q">Query, Heads * HeadDim values.</param>
        /// <param name="ctx">Output, Heads * HeadDim values.</param>
        /// <param name="parallel">Run tasks on the thread pool.</param>
        public void RunPaged(PagedKvSequence seq, int layer, int pos, ReadOnlySpan<float> q, Span<float> ctx, bool parallel = true)
        {
            if (seq is null) throw new ArgumentNullException(nameof(seq));
            KvBlockPool pool = seq.Pool;
            if (!pool.Config.Equals(cfg)) throw new ArgumentException("Pool shape does not match this attention instance.", nameof(seq));
            if ((uint)layer >= cfg.Layers) throw new ArgumentOutOfRangeException(nameof(layer));
            if ((uint)pos >= seq.Length) throw new ArgumentOutOfRangeException(nameof(pos), "Position is not written yet.");
            int n = cfg.Heads * cfg.HeadDim;
            if (q.Length != n || ctx.Length < n) throw new ArgumentException($"Query and context must have {n} values.");
            Kernels.RequireSimd();

            q.CopyTo(qBuf);
            kv = null;
            curPaged = seq;
            batchT = 0;
            curLayer = layer;
            curPos = pos;
            curChunks = pos / Chunk + 1;
            curScale = 1f / MathF.Sqrt(cfg.HeadDim);
            curInt8 = pool.Mode == KvMode.Int8;

            int tasks = cfg.KvHeads * curChunks;
            if (parallel && tasks >= 8) Parallel.For(0, tasks, body);
            else for (int t = 0; t < tasks; t++) TaskBody(t);

            Merge(ctx, curChunks);
        }

        private void CheckArgs(KvCache cache, int layer, int pos, ReadOnlySpan<float> q, Span<float> ctx)
        {
            if (cache is null) throw new ArgumentNullException(nameof(cache));
            if (!cache.Config.Equals(cfg)) throw new ArgumentException("Cache shape does not match this attention instance.", nameof(cache));
            if ((uint)layer >= cfg.Layers) throw new ArgumentOutOfRangeException(nameof(layer));
            if ((uint)pos >= cfg.MaxSeq) throw new ArgumentOutOfRangeException(nameof(pos));
            int n = cfg.Heads * cfg.HeadDim;
            if (q.Length != n || ctx.Length < n) throw new ArgumentException($"Query and context must have {n} values.");
        }

        // Deterministic merge in chunk order (online-softmax combine), per query head.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void Merge(Span<float> ctx, int chunks)
        {
            int hd = cfg.HeadDim;
            for (int g = 0; g < cfg.KvHeads; g++)
            {
                for (int qi = 0; qi < group; qi++)
                {
                    float maxAll = float.NegativeInfinity;
                    for (int c = 0; c < chunks; c++)
                    {
                        float m = meta[(((g * maxChunks + c) * group) + qi) * 2];
                        if (m > maxAll) maxAll = m;
                    }
                    float sumAll = 0f;
                    Span<float> outRow = ctx.Slice((g * group + qi) * hd, hd);
                    outRow.Clear();
                    for (int c = 0; c < chunks; c++)
                    {
                        int slot = g * maxChunks + c;
                        int mi = (slot * group + qi) * 2;
                        float w = MathF.Exp(meta[mi] - maxAll);
                        sumAll += meta[mi + 1] * w;
                        ReadOnlySpan<float> acc = accs.AsSpan((slot * group + qi) * hd, hd);
                        for (int d = 0; d < hd; d++) outRow[d] += acc[d] * w;
                    }
                    float inv = 1f / sumAll;
                    for (int d = 0; d < hd; d++) outRow[d] *= inv;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void TaskBody(int task)
        {
            int g = task / curChunks;
            int c = task - g * curChunks;
            if (batchT > 0) ComputeBatchChunk(g * curChunks + c, g, c);
            else ComputeChunk(g * maxChunks + c, g, c);
        }

        // Batch state for RunPagedChunk. batchT is zero outside a batched call.
        private int batchT, batchStart;
        private float[] bq = Array.Empty<float>(), bScores = Array.Empty<float>(), bAccs = Array.Empty<float>(), bMeta = Array.Empty<float>();

        /// <summary>
        /// Fused attention for a chunk of <paramref name="t"/> consecutive positions of a paged sequence, in one call.
        /// Each key or value row is read once per task and applied to every chunk query that covers it. For each query the
        /// arithmetic (scores, chunk max, exp and sum, accumulation, merge over the chunks it covers) is the same as
        /// <see cref="RunPaged"/>, so output is bit-identical to running the positions one at a time.
        /// All positions startPos..startPos+t-1 must already be written (for every layer used).
        /// </summary>
        /// <param name="seq">Paged sequence.</param>
        /// <param name="layer">Layer index.</param>
        /// <param name="startPos">Position of the first query.</param>
        /// <param name="t">Number of consecutive queries, at least 1.</param>
        /// <param name="q">Queries, t * Heads * HeadDim values, token-major.</param>
        /// <param name="ctx">Outputs, t * Heads * HeadDim values, token-major. Overwritten.</param>
        /// <param name="parallel">Run tasks on the thread pool.</param>
        public void RunPagedChunk(PagedKvSequence seq, int layer, int startPos, int t, ReadOnlySpan<float> q, Span<float> ctx, bool parallel = true)
        {
            if (seq is null) throw new ArgumentNullException(nameof(seq));
            KvBlockPool pool = seq.Pool;
            if (!pool.Config.Equals(cfg)) throw new ArgumentException("Pool shape does not match this attention instance.", nameof(seq));
            if ((uint)layer >= cfg.Layers) throw new ArgumentOutOfRangeException(nameof(layer));
            if (t < 1 || startPos < 0 || startPos + t > seq.Length) throw new ArgumentOutOfRangeException(nameof(t), "Positions must be written before attention.");
            int n = t * cfg.Hidden;
            if (q.Length != n || ctx.Length < n) throw new ArgumentException($"Queries and context must have {n} values.");
            Kernels.RequireSimd();

            int chunks = (startPos + t - 1) / Chunk + 1;
            EnsureBatchScratch(t, chunks);
            q.Slice(0, n).CopyTo(bq);
            kv = null;
            curPaged = seq;
            batchT = t;
            batchStart = startPos;
            curLayer = layer;
            curPos = startPos + t - 1;
            curChunks = chunks;
            curScale = 1f / MathF.Sqrt(cfg.HeadDim);
            curInt8 = pool.Mode == KvMode.Int8;
            try
            {
                int tasks = cfg.KvHeads * chunks;
                if (parallel && tasks >= 8) Parallel.For(0, tasks, body);
                else for (int task = 0; task < tasks; task++) TaskBody(task);
                MergeBatch(ctx, t);
            }
            finally
            {
                batchT = 0;
            }
        }

        // Scratch for a batched call, grown on demand. Slots are (KV head, chunk) with slot = g * chunks + c.
        private void EnsureBatchScratch(int t, int chunks)
        {
            int slots = cfg.KvHeads * chunks;
            int g = group, hd = cfg.HeadDim;
            if (bq.Length < t * cfg.Hidden) bq = new float[t * cfg.Hidden];
            if (bScores.Length < slots * t * g * Chunk) bScores = new float[slots * t * g * Chunk];
            if (bAccs.Length < slots * t * g * hd) bAccs = new float[slots * t * g * hd];
            if (bMeta.Length < slots * t * g * 2) bMeta = new float[slots * t * g * 2];
        }

        // One (KV head, chunk) task for a batch of queries. Query i is at position startPos + i.
        private void ComputeBatchChunk(int slot, int g, int c)
        {
            PagedKvSequence seq = curPaged!;
            KvBlockPool pool = seq.Pool;
            int hd = cfg.HeadDim, bs = pool.BlockSize, T = batchT, G = group;
            int t0 = c * Chunk;
            int tEnd = Math.Min(batchStart + T, t0 + Chunk);
            IReadOnlyList<int> table = seq.Table;
            Span<float> sc = bScores.AsSpan(slot * T * G * Chunk, T * G * Chunk);
            Span<float> kb = kbuf.AsSpan(slot * hd, hd);
            Span<float> vb = vbuf.AsSpan(slot * hd, hd);

            // Scores: one key row, dotted with every query that is at or past this position.
            for (int s = t0; s < tEnd; s++)
            {
                int row = (table[s / bs] * cfg.KvHeads + g) * bs + (s % bs);
                ReadOnlySpan<float> kr;
                float f = curScale;
                if (curInt8)
                {
                    Convert(pool.Keys8[curLayer].AsSpan(row * hd, hd), kb);
                    kr = kb;
                    f *= pool.KeyScale[curLayer][row];
                }
                else
                {
                    kr = pool.KeysF[curLayer].AsSpan(row * hd, hd);
                }
                ref float kref = ref MemoryMarshal.GetReference(kr);
                int iFirst = Math.Max(0, s - batchStart);
                for (int i = iFirst; i < T; i++)
                {
                    for (int qi = 0; qi < G; qi++)
                    {
                        ReadOnlySpan<float> qs = bq.AsSpan(i * cfg.Hidden + (g * G + qi) * hd, hd);
                        sc[(i * G + qi) * Chunk + (s - t0)] = DotRef(ref MemoryMarshal.GetReference(qs), ref kref, hd) * f;
                    }
                }
            }

            // Per query: max, exponentials, and sum over the positions of this chunk that it can see.
            for (int i = 0; i < T; i++)
            {
                int p = batchStart + i;
                if (p < t0) continue;   // this chunk is not covered by query i; the merge skips it too
                int n = Math.Min(p + 1, t0 + Chunk) - t0;
                for (int qi = 0; qi < G; qi++)
                {
                    Span<float> row = sc.Slice((i * G + qi) * Chunk, n);
                    float m = float.NegativeInfinity;
                    for (int j = 0; j < n; j++) if (row[j] > m) m = row[j];
                    float sum = 0f;
                    for (int j = 0; j < n; j++)
                    {
                        float e = MathF.Exp(row[j] - m);
                        row[j] = e;
                        sum += e;
                    }
                    int mi = ((slot * T + i) * G + qi) * 2;
                    bMeta[mi] = m;
                    bMeta[mi + 1] = sum;
                }
            }

            // Values: one value row, applied to every query that can see it.
            Span<float> acc = bAccs.AsSpan(slot * T * G * hd, T * G * hd);
            acc.Clear();
            for (int s = t0; s < tEnd; s++)
            {
                int row = (table[s / bs] * cfg.KvHeads + g) * bs + (s % bs);
                ReadOnlySpan<float> vr;
                float wScale = 1f;
                if (curInt8)
                {
                    Convert(pool.Vals8[curLayer].AsSpan(row * hd, hd), vb);
                    vr = vb;
                    wScale = pool.ValScale[curLayer][row];
                }
                else
                {
                    vr = pool.ValsF[curLayer].AsSpan(row * hd, hd);
                }
                ref float vref = ref MemoryMarshal.GetReference(vr);
                int iFirst = Math.Max(0, s - batchStart);
                for (int i = iFirst; i < T; i++)
                {
                    for (int qi = 0; qi < G; qi++)
                    {
                        float w = sc[(i * G + qi) * Chunk + (s - t0)] * wScale;
                        ref float ar = ref MemoryMarshal.GetReference(acc.Slice((i * G + qi) * hd, hd));
                        AxpyRef(ref ar, w, ref vref, hd);
                    }
                }
            }
        }

        // Merge per query over the chunks that query covers, in chunk order, as Merge does for one query.
        private void MergeBatch(Span<float> ctx, int T)
        {
            int hd = cfg.HeadDim, G = group;
            for (int i = 0; i < T; i++)
            {
                int chunksForQuery = (batchStart + i) / Chunk + 1;
                for (int g = 0; g < cfg.KvHeads; g++)
                {
                    for (int qi = 0; qi < G; qi++)
                    {
                        float maxAll = float.NegativeInfinity;
                        for (int c = 0; c < chunksForQuery; c++)
                        {
                            float m = bMeta[((((g * curChunks + c) * T) + i) * G + qi) * 2];
                            if (m > maxAll) maxAll = m;
                        }
                        float sumAll = 0f;
                        Span<float> outRow = ctx.Slice(i * cfg.Hidden + (g * G + qi) * hd, hd);
                        outRow.Clear();
                        for (int c = 0; c < chunksForQuery; c++)
                        {
                            int slot = g * curChunks + c;
                            int mi = ((slot * T + i) * G + qi) * 2;
                            float w = MathF.Exp(bMeta[mi] - maxAll);
                            sumAll += bMeta[mi + 1] * w;
                            ReadOnlySpan<float> acc = bAccs.AsSpan(((slot * T + i) * G + qi) * hd, hd);
                            for (int d = 0; d < hd; d++) outRow[d] += acc[d] * w;
                        }
                        float inv = 1f / sumAll;
                        for (int d = 0; d < hd; d++) outRow[d] *= inv;
                    }
                }
            }
        }

        // One (KV head, chunk) task: scores, chunk max and sum, and unnormalized accumulators for all group queries.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void ComputeChunk(int slot, int g, int c)
        {
            if (curPaged is not null) ComputePagedChunk(slot, g, c, curPaged);
            else if (curInt8) ComputeChunkInt8(slot, g, c);
            else ComputeChunkFp32(slot, g, c);
        }

        // Paged task: same steps as the contiguous paths. Each position's row is found through the block table.
        // Score scaling, exp, and accumulation order match ComputeChunkFp32 / ComputeChunkInt8 exactly.
        private void ComputePagedChunk(int slot, int g, int c, PagedKvSequence seq)
        {
            KvBlockPool pool = seq.Pool;
            int hd = cfg.HeadDim;
            int bs = pool.BlockSize;
            int t0 = c * Chunk;
            int n = Math.Min(curPos + 1, t0 + Chunk) - t0;
            IReadOnlyList<int> table = seq.Table;
            ReadOnlySpan<float> qh = qBuf.AsSpan(g * group * hd, group * hd);
            Span<float> sc = scores.AsSpan(slot * group * Chunk, group * Chunk);
            Span<float> kb = kbuf.AsSpan(slot * hd, hd);
            Span<float> vb = vbuf.AsSpan(slot * hd, hd);
            Span<float> acc = accs.AsSpan(slot * group * hd, group * hd);

            for (int i = 0; i < n; i++)
            {
                int t = t0 + i;
                int row = (table[t / bs] * cfg.KvHeads + g) * bs + (t % bs);
                ReadOnlySpan<float> kr;
                float f = curScale;
                if (curInt8)
                {
                    Convert(pool.Keys8[curLayer].AsSpan(row * hd, hd), kb);
                    kr = kb;
                    f *= pool.KeyScale[curLayer][row];
                }
                else
                {
                    kr = pool.KeysF[curLayer].AsSpan(row * hd, hd);
                }
                ref float kref = ref MemoryMarshal.GetReference(kr);
                ref float qref = ref MemoryMarshal.GetReference(qh);
                for (int qi = 0; qi < group; qi++)
                    sc[qi * Chunk + i] = DotRef(ref Unsafe.Add(ref qref, qi * hd), ref kref, hd) * f;
            }

            for (int qi = 0; qi < group; qi++)
            {
                Span<float> rowS = sc.Slice(qi * Chunk, n);
                float m = float.NegativeInfinity;
                for (int i = 0; i < n; i++) if (rowS[i] > m) m = rowS[i];
                float s = 0f;
                for (int i = 0; i < n; i++)
                {
                    float e = MathF.Exp(rowS[i] - m);
                    rowS[i] = e;
                    s += e;
                }
                meta[(slot * group + qi) * 2] = m;
                meta[(slot * group + qi) * 2 + 1] = s;
            }

            acc.Clear();
            for (int i = 0; i < n; i++)
            {
                int t = t0 + i;
                int row = (table[t / bs] * cfg.KvHeads + g) * bs + (t % bs);
                ReadOnlySpan<float> vr;
                float wScale = 1f;
                if (curInt8)
                {
                    Convert(pool.Vals8[curLayer].AsSpan(row * hd, hd), vb);
                    vr = vb;
                    wScale = pool.ValScale[curLayer][row];
                }
                else
                {
                    vr = pool.ValsF[curLayer].AsSpan(row * hd, hd);
                }
                ref float vref = ref MemoryMarshal.GetReference(vr);
                ref float aref = ref MemoryMarshal.GetReference(acc);
                for (int qi = 0; qi < group; qi++)
                {
                    float w = sc[qi * Chunk + i] * wScale;
                    AxpyRef(ref Unsafe.Add(ref aref, qi * hd), w, ref vref, hd);
                }
            }
        }

        // fp32 path. All loops address the arrays through refs, so the hot inner loops have no per-row span
        // creation and no bounds checks. Same arithmetic order as the int8 path.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void ComputeChunkFp32(int slot, int g, int c)
        {
            KvCache cache = kv!;
            int hd = cfg.HeadDim;
            int t0 = c * Chunk;
            int n = Math.Min(curPos + 1, t0 + Chunk) - t0;
            float[] kArr = cache.KeysF(curLayer);
            float[] vArr = cache.ValsF(curLayer);
            ref float kRow0 = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(kArr), (g * cfg.MaxSeq + t0) * hd);
            ref float vRow0 = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(vArr), (g * cfg.MaxSeq + t0) * hd);
            ref float q0 = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(qBuf), g * group * hd);
            ref float sc = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(scores), slot * group * Chunk);
            ref float acc = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(accs), slot * group * hd);
            float scale = curScale;

            // Scores: each key row is read once and dotted with all group queries.
            for (int i = 0; i < n; i++)
            {
                ref float kr = ref Unsafe.Add(ref kRow0, i * hd);
                for (int qi = 0; qi < group; qi++)
                {
                    Unsafe.Add(ref sc, qi * Chunk + i) = DotRef(ref Unsafe.Add(ref q0, qi * hd), ref kr, hd) * scale;
                }
            }

            // Chunk max, exponentials, and sum per query.
            for (int qi = 0; qi < group; qi++)
            {
                ref float row = ref Unsafe.Add(ref sc, qi * Chunk);
                float m = float.NegativeInfinity;
                for (int i = 0; i < n; i++)
                {
                    float v = Unsafe.Add(ref row, i);
                    if (v > m) m = v;
                }
                float s = 0f;
                for (int i = 0; i < n; i++)
                {
                    float e = MathF.Exp(Unsafe.Add(ref row, i) - m);
                    Unsafe.Add(ref row, i) = e;
                    s += e;
                }
                meta[(slot * group + qi) * 2] = m;
                meta[(slot * group + qi) * 2 + 1] = s;
            }

            // Values: each value row is read once and applied to all group queries.
            new Span<float>(accs, slot * group * hd, group * hd).Clear();
            for (int i = 0; i < n; i++)
            {
                ref float vr = ref Unsafe.Add(ref vRow0, i * hd);
                for (int qi = 0; qi < group; qi++)
                {
                    AxpyRef(ref Unsafe.Add(ref acc, qi * hd), Unsafe.Add(ref sc, qi * Chunk + i), ref vr, hd);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void ComputeChunkInt8(int slot, int g, int c)
        {
            KvCache cache = kv!;
            int hd = cfg.HeadDim;
            int t0 = c * Chunk;
            int t1 = Math.Min(curPos + 1, t0 + Chunk);
            int n = t1 - t0;
            int rowBase = g * cfg.MaxSeq;   // row index of (kvHead g, position 0) inside the layer arrays
            ReadOnlySpan<float> qh = qBuf.AsSpan(g * group * hd, group * hd);
            Span<float> sc = scores.AsSpan(slot * group * Chunk, group * Chunk);
            Span<float> kb = kbuf.AsSpan(slot * hd, hd);
            Span<float> vb = vbuf.AsSpan(slot * hd, hd);

            // Scores: each key row is read once and dotted with all group queries.
            for (int t = t0; t < t1; t++)
            {
                ReadOnlySpan<float> kr;
                float f = curScale;
                if (curInt8)
                {
                    Convert(cache.Keys8(curLayer).AsSpan((rowBase + t) * hd, hd), kb);
                    kr = kb;
                    f *= cache.KeyScale(curLayer)[rowBase + t];
                }
                else
                {
                    kr = cache.KeysF(curLayer).AsSpan((rowBase + t) * hd, hd);
                }
                ref float kref = ref MemoryMarshal.GetReference(kr);
                ref float qref = ref MemoryMarshal.GetReference(qh);
                for (int qi = 0; qi < group; qi++)
                {
                    sc[qi * Chunk + (t - t0)] = DotRef(ref Unsafe.Add(ref qref, qi * hd), ref kref, hd) * f;
                }
            }

            // Chunk max, exponentials, and sum per query.
            for (int qi = 0; qi < group; qi++)
            {
                Span<float> row = sc.Slice(qi * Chunk, n);
                float m = float.NegativeInfinity;
                for (int i = 0; i < n; i++) if (row[i] > m) m = row[i];
                float s = 0f;
                for (int i = 0; i < n; i++)
                {
                    float e = MathF.Exp(row[i] - m);
                    row[i] = e;
                    s += e;
                }
                meta[(slot * group + qi) * 2] = m;
                meta[(slot * group + qi) * 2 + 1] = s;
            }

            // Values: each value row is read once and applied to all group queries.
            Span<float> acc = accs.AsSpan(slot * group * hd, group * hd);
            acc.Clear();
            for (int t = t0; t < t1; t++)
            {
                ReadOnlySpan<float> vr;
                float wScale = 1f;
                if (curInt8)
                {
                    Convert(cache.Vals8(curLayer).AsSpan((rowBase + t) * hd, hd), vb);
                    vr = vb;
                    wScale = cache.ValScale(curLayer)[rowBase + t];
                }
                else
                {
                    vr = cache.ValsF(curLayer).AsSpan((rowBase + t) * hd, hd);
                }
                ref float vref = ref MemoryMarshal.GetReference(vr);
                ref float aref = ref MemoryMarshal.GetReference(acc);
                for (int qi = 0; qi < group; qi++)
                {
                    float w = sc[qi * Chunk + (t - t0)] * wScale;
                    AxpyRef(ref Unsafe.Add(ref aref, qi * hd), w, ref vref, hd);
                }
            }
        }

        // ---- AVX2 helpers. Lengths are multiples of 8 (HeadDim is a multiple of 16). ----

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DotRef(ref float a, ref float b, int n)
        {
            Vector256<float> acc = Vector256<float>.Zero;
            for (int d = 0; d < n; d += 8)
                acc = Fma.MultiplyAdd(Vector256.LoadUnsafe(ref a, (nuint)d), Vector256.LoadUnsafe(ref b, (nuint)d), acc);
            Vector128<float> s = Sse.Add(acc.GetLower(), acc.GetUpper());
            s = Sse.Add(s, Sse.MoveHighToLow(s, s));
            s = Sse.Add(s, Sse.Shuffle(s, s, 0x55));
            return s.ToScalar();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AxpyRef(ref float acc, float w, ref float v, int n)
        {
            Vector256<float> wv = Vector256.Create(w);
            for (int d = 0; d < n; d += 8)
            {
                Vector256<float> r = Fma.MultiplyAdd(wv, Vector256.LoadUnsafe(ref v, (nuint)d), Vector256.LoadUnsafe(ref acc, (nuint)d));
                r.StoreUnsafe(ref acc, (nuint)d);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DotAvx2(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            ref float ar = ref MemoryMarshal.GetReference(a);
            ref float br = ref MemoryMarshal.GetReference(b);
            Vector256<float> acc = Vector256<float>.Zero;
            for (int d = 0; d < a.Length; d += 8)
                acc = Fma.MultiplyAdd(Vector256.LoadUnsafe(ref ar, (nuint)d), Vector256.LoadUnsafe(ref br, (nuint)d), acc);
            Vector128<float> s = Sse.Add(acc.GetLower(), acc.GetUpper());
            s = Sse.Add(s, Sse.MoveHighToLow(s, s));
            s = Sse.Add(s, Sse.Shuffle(s, s, 0x55));
            return s.ToScalar();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Axpy(Span<float> acc, float w, ReadOnlySpan<float> v)
        {
            ref float ar = ref MemoryMarshal.GetReference(acc);
            ref float vr = ref MemoryMarshal.GetReference(v);
            Vector256<float> wv = Vector256.Create(w);
            for (int d = 0; d < acc.Length; d += 8)
            {
                Vector256<float> r = Fma.MultiplyAdd(wv, Vector256.LoadUnsafe(ref vr, (nuint)d), Vector256.LoadUnsafe(ref ar, (nuint)d));
                r.StoreUnsafe(ref ar, (nuint)d);
            }
        }

        // int8 -> float, 16 values per step, using the same widening as the matvec kernel.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Convert(ReadOnlySpan<sbyte> src, Span<float> dst)
        {
            ref sbyte sr = ref MemoryMarshal.GetReference(src);
            ref float dr = ref MemoryMarshal.GetReference(dst);
            int n = src.Length;
            for (int d = 0; d < n; d += 16)
            {
                Vector128<sbyte> v = Vector128.LoadUnsafe(ref sr, (nuint)d);
                Vector256<float> lo = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(v));
                Vector256<float> hi = Avx.ConvertToVector256Single(
                    Avx2.ConvertToVector256Int32(Sse2.ShiftRightLogical128BitLane(v.AsByte(), 8).AsSByte()));
                lo.StoreUnsafe(ref dr, (nuint)d);
                hi.StoreUnsafe(ref dr, (nuint)(d + 8));
            }
        }

        /// <summary>
        /// Scalar reference attention, one query head at a time. Works on either cache mode, so the int8
        /// kernel can be checked against the same quantized data.
        /// </summary>
        /// <param name="cache">KV cache.</param>
        /// <param name="layer">Layer index.</param>
        /// <param name="pos">Current position.</param>
        /// <param name="q">Query, Heads * HeadDim values.</param>
        /// <param name="ctx">Output, Heads * HeadDim values.</param>
        public void ScalarReference(KvCache cache, int layer, int pos, ReadOnlySpan<float> q, Span<float> ctx)
        {
            int hd = cfg.HeadDim;
            float scale = 1f / MathF.Sqrt(hd);
            float[] att = new float[pos + 1];
            for (int h = 0; h < cfg.Heads; h++)
            {
                int g = h / group;
                int rowBase = g * cfg.MaxSeq;
                ReadOnlySpan<float> qh = q.Slice(h * hd, hd);
                for (int t = 0; t <= pos; t++)
                {
                    float dot = 0f;
                    if (cache.Mode == KvMode.Fp32)
                    {
                        ReadOnlySpan<float> kr = cache.KeysF(layer).AsSpan((rowBase + t) * hd, hd);
                        for (int d = 0; d < hd; d++) dot += qh[d] * kr[d];
                    }
                    else
                    {
                        ReadOnlySpan<sbyte> kr = cache.Keys8(layer).AsSpan((rowBase + t) * hd, hd);
                        for (int d = 0; d < hd; d++) dot += qh[d] * kr[d];
                        dot *= cache.KeyScale(layer)[rowBase + t];
                    }
                    att[t] = dot * scale;
                }
                float m = float.NegativeInfinity;
                for (int t = 0; t <= pos; t++) if (att[t] > m) m = att[t];
                float sum = 0f;
                for (int t = 0; t <= pos; t++) { att[t] = MathF.Exp(att[t] - m); sum += att[t]; }

                Span<float> outRow = ctx.Slice(h * hd, hd);
                outRow.Clear();
                for (int t = 0; t <= pos; t++)
                {
                    float w = att[t] / sum;
                    if (cache.Mode == KvMode.Fp32)
                    {
                        ReadOnlySpan<float> vr = cache.ValsF(layer).AsSpan((rowBase + t) * hd, hd);
                        for (int d = 0; d < hd; d++) outRow[d] += w * vr[d];
                    }
                    else
                    {
                        ReadOnlySpan<sbyte> vr = cache.Vals8(layer).AsSpan((rowBase + t) * hd, hd);
                        float ws = w * cache.ValScale(layer)[rowBase + t];
                        for (int d = 0; d < hd; d++) outRow[d] += ws * vr[d];
                    }
                }
            }
        }
    }
}
