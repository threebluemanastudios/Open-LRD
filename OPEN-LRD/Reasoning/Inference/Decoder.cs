using System;
using System.Diagnostics;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// Weights for a decoder-only transformer. All projections are int8 (D1). Norms are float gains.
    /// The LM head is tied to the embedding matrix.
    /// </summary>
    public sealed class DecoderWeights
    {
        /// <summary>Creates weights from existing storage. Shapes are checked against the config.</summary>
        /// <param name="config">Model shape.</param>
        /// <param name="wq">Query projections, one per layer, [Hidden, Hidden].</param>
        /// <param name="wk">Key projections, one per layer, [KvDim, Hidden].</param>
        /// <param name="wv">Value projections, one per layer, [KvDim, Hidden].</param>
        /// <param name="wo">Output projections, one per layer, [Hidden, Hidden].</param>
        /// <param name="w1">SwiGLU gate projections, one per layer, [Ffn, Hidden].</param>
        /// <param name="w3">SwiGLU up projections, one per layer, [Ffn, Hidden].</param>
        /// <param name="w2">SwiGLU down projections, one per layer, [Hidden, Ffn].</param>
        /// <param name="embedding">Embedding and LM head, [Vocab, Hidden].</param>
        public DecoderWeights(ModelConfig config, QMat[] wq, QMat[] wk, QMat[] wv, QMat[] wo, QMat[] w1, QMat[] w3, QMat[] w2, QMat embedding)
        {
            config.Validate();
            Config = config;
            Wq = wq; Wk = wk; Wv = wv; Wo = wo; W1 = w1; W3 = w3; W2 = w2;
            Embedding = embedding;
            CheckShapes();
            Norm1 = Ones(config.Hidden);
            Norm2 = Ones(config.Hidden);
            NormF = Ones(config.Hidden);
        }

        /// <summary>Model shape.</summary>
        public ModelConfig Config { get; }

        /// <summary>Query projections per layer.</summary>
        public QMat[] Wq { get; }

        /// <summary>Key projections per layer.</summary>
        public QMat[] Wk { get; }

        /// <summary>Value projections per layer.</summary>
        public QMat[] Wv { get; }

        /// <summary>Output projections per layer.</summary>
        public QMat[] Wo { get; }

        /// <summary>SwiGLU gate projections per layer.</summary>
        public QMat[] W1 { get; }

        /// <summary>SwiGLU up projections per layer.</summary>
        public QMat[] W3 { get; }

        /// <summary>SwiGLU down projections per layer.</summary>
        public QMat[] W2 { get; }

        /// <summary>Tied embedding and LM head.</summary>
        public QMat Embedding { get; }

        /// <summary>Pre-attention RMSNorm gain (shared across layers in this version).</summary>
        public float[] Norm1 { get; }

        /// <summary>Pre-FFN RMSNorm gain (shared across layers in this version).</summary>
        public float[] Norm2 { get; }

        /// <summary>Final RMSNorm gain.</summary>
        public float[] NormF { get; }

        /// <summary>Creates weights with random int8 values from a seeded generator (benchmarks and tests only).</summary>
        /// <param name="config">Model shape.</param>
        /// <param name="rng">Seeded random source.</param>
        public static DecoderWeights Random(ModelConfig config, Random rng)
        {
            config.Validate();
            int h = config.Hidden, kv = config.KvDim, f = config.Ffn;
            QMat[] Make(int rows, int cols)
            {
                QMat[] a = new QMat[config.Layers];
                for (int l = 0; l < config.Layers; l++) a[l] = QMat.Random(rows, cols, rng);
                return a;
            }
            return new DecoderWeights(config,
                Make(h, h), Make(kv, h), Make(kv, h), Make(h, h),
                Make(f, h), Make(f, h), Make(h, f),
                QMat.Random(config.Vocab, h, rng));
        }

        private void CheckShapes()
        {
            ModelConfig c = Config;
            for (int l = 0; l < c.Layers; l++)
            {
                Expect(Wq[l], c.Hidden, c.Hidden);
                Expect(Wk[l], c.KvDim, c.Hidden);
                Expect(Wv[l], c.KvDim, c.Hidden);
                Expect(Wo[l], c.Hidden, c.Hidden);
                Expect(W1[l], c.Ffn, c.Hidden);
                Expect(W3[l], c.Ffn, c.Hidden);
                Expect(W2[l], c.Hidden, c.Ffn);
            }
            Expect(Embedding, c.Vocab, c.Hidden);
        }

        private static void Expect(QMat m, int rows, int cols)
        {
            if (m is null || m.Rows != rows || m.Cols != cols)
                throw new ArgumentException($"Weight matrix must be {rows} x {cols}.");
        }

        private static float[] Ones(int n)
        {
            float[] a = new float[n];
            Array.Fill(a, 1f);
            return a;
        }
    }

    /// <summary>
    /// Single-sequence transformer forward pass with an int8 weight path, a KV cache, and optional
    /// fused attention. Implements <see cref="ILocalModel"/>. Positions must be processed in order.
    /// Not thread-safe.
    /// </summary>
    public sealed class Decoder : ILocalModel
    {
        private readonly DecoderWeights w;
        private readonly ModelConfig cfg;
        private readonly KvCache? cache;          // contiguous cache, or null for a paged decoder
        private readonly PagedKvSequence? seq;    // paged sequence, or null for a contiguous decoder
        private readonly KvMode kvMode;
        private readonly Attention attention;
        private readonly MatVecEngine engine;
        private readonly float[] cos;
        private readonly float[] sin;
        private readonly float[] x, xn, q, k, v, ctx, gate, up, hid, logits;
        private AttnKernel attnKernel;
        private MatVecKernel matVecKernel = MatVecKernel.Avx2Parallel;
        private int length;

        /// <summary>Creates a decoder with an empty contiguous cache.</summary>
        /// <param name="weights">Model weights.</param>
        /// <param name="kvMode">KV cache precision. Null selects <see cref="KvPolicy.DefaultFor"/> from the context length.</param>
        /// <param name="blocks">Row blocks for the parallel matvec. Defaults to the processor count.</param>
        public Decoder(DecoderWeights weights, KvMode? kvMode = null, int? blocks = null)
            : this(weights, kvMode ?? KvPolicy.DefaultFor(weights?.Config.MaxSeq ?? throw new ArgumentNullException(nameof(weights))),
                   null, null, blocks)
        {
        }

        /// <summary>
        /// Creates a decoder that stores its KV state in a paged sequence. The sequence's pool must match the weights'
        /// shape. Its current length becomes the decoder's position, so a forked sequence continues from the fork point.
        /// </summary>
        /// <param name="weights">Model weights.</param>
        /// <param name="sequence">Paged sequence on a pool with the same shape.</param>
        /// <param name="blocks">Row blocks for the parallel matvec. Defaults to the processor count.</param>
        public Decoder(DecoderWeights weights, PagedKvSequence sequence, int? blocks = null)
            : this(weights, sequence?.Pool.Mode ?? throw new ArgumentNullException(nameof(sequence)), null, sequence, blocks)
        {
            if (!sequence.Pool.Config.Equals(weights.Config))
                throw new ArgumentException("Pool shape does not match the weights.", nameof(sequence));
        }

        private Decoder(DecoderWeights weights, KvMode mode, KvCache? contiguous, PagedKvSequence? sequence, int? blocks)
        {
            w = weights ?? throw new ArgumentNullException(nameof(weights));
            cfg = weights.Config;
            kvMode = mode;
            cache = contiguous ?? (sequence is null ? new KvCache(cfg, mode) : null);
            seq = sequence;
            length = sequence?.Length ?? 0;
            attention = new Attention(cfg);
            engine = new MatVecEngine(blocks ?? Environment.ProcessorCount);
            attnKernel = mode == KvMode.Int8 ? AttnKernel.FusedInt8 : AttnKernel.FusedFp32;

            cos = new float[cfg.MaxSeq * cfg.HeadDim / 2];
            sin = new float[cos.Length];
            for (int p = 0; p < cfg.MaxSeq; p++)
            {
                for (int i = 0; i < cfg.HeadDim / 2; i++)
                {
                    double theta = p * Math.Pow(10000.0, -2.0 * i / cfg.HeadDim);
                    cos[p * cfg.HeadDim / 2 + i] = (float)Math.Cos(theta);
                    sin[p * cfg.HeadDim / 2 + i] = (float)Math.Sin(theta);
                }
            }

            x = new float[cfg.Hidden];
            xn = new float[cfg.Hidden];
            q = new float[cfg.Hidden];
            k = new float[cfg.KvDim];
            v = new float[cfg.KvDim];
            ctx = new float[cfg.Hidden];
            gate = new float[cfg.Ffn];
            up = new float[cfg.Ffn];
            hid = new float[cfg.Ffn];
            logits = new float[cfg.Vocab];
        }

        /// <summary>Diagnostic counters. Ticks from <see cref="Stopwatch.GetTimestamp"/>.</summary>
        public long AttentionTicks { get; private set; }

        /// <summary>Diagnostic counter: total ticks spent in <see cref="Forward"/>.</summary>
        public long ForwardTicks { get; private set; }

        /// <summary>Contiguous KV cache, for inspection and tests. Throws for a paged decoder.</summary>
        public KvCache Cache => cache ?? throw new InvalidOperationException("Paged decoder has no contiguous cache.");

        /// <summary>Paged sequence, or null for a contiguous decoder.</summary>
        public PagedKvSequence? Sequence => seq;

        /// <summary>Attention kernel. Fused kernels require a matching KV mode.</summary>
        public AttnKernel AttentionKernel
        {
            get => attnKernel;
            set
            {
                bool needsInt8 = value == AttnKernel.FusedInt8;
                bool fused = value != AttnKernel.Scalar;
                if (fused && needsInt8 != (kvMode == KvMode.Int8))
                    throw new InvalidOperationException($"Kernel {value} does not match KV mode {kvMode}.");
                attnKernel = value;
            }
        }

        /// <summary>Matvec kernel used for all projections.</summary>
        public MatVecKernel MatVecKernel
        {
            get => matVecKernel;
            set => matVecKernel = value;
        }

        /// <inheritdoc />
        public int VocabSize => cfg.Vocab;

        /// <inheritdoc />
        public int MaxContext => cfg.MaxSeq;

        /// <inheritdoc />
        public int Length => length;

        /// <inheritdoc />
        public void Reset()
        {
            length = 0;
            seq?.Clear();
        }

        /// <summary>
        /// Benchmark helper: fills positions 0..positions-1 of every layer with one fixed key/value vector and sets
        /// <see cref="Length"/> to <paramref name="positions"/>. Lets timing runs start at a deep position without
        /// running that many forward passes. Output values are meaningless; do not use for inference.
        /// </summary>
        /// <param name="positions">Depth to prime, within MaxSeq.</param>
        /// <param name="rng">Source for the fixed vector.</param>
        public void PrimeCacheForBenchmark(int positions, Random rng)
        {
            if ((uint)positions > cfg.MaxSeq) throw new ArgumentOutOfRangeException(nameof(positions));
            float[] k = new float[cfg.KvDim], v = new float[cfg.KvDim];
            for (int i = 0; i < cfg.KvDim; i++)
            {
                k[i] = (float)(rng.NextDouble() * 2 - 1);
                v[i] = (float)(rng.NextDouble() * 2 - 1);
            }
            for (int p = 0; p < positions; p++)
                for (int l = 0; l < cfg.Layers; l++)
                    cache!.Write(l, p, k, v);
            length = positions;
        }

        /// <inheritdoc />
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
        public float[] Forward(int token, int position)
        {
            if (position != length)
                throw new InvalidOperationException($"Positions must be sequential: expected {length}, got {position}.");
            if (position >= cfg.MaxSeq)
                throw new InvalidOperationException("Context is full.");
            if ((uint)token >= cfg.Vocab)
                throw new ArgumentOutOfRangeException(nameof(token));

            long t0 = Stopwatch.GetTimestamp();
            int h = cfg.Hidden;
            ReadOnlySpan<sbyte> emb = w.Embedding.Row(token);
            float es = w.Embedding.Scale[token];
            for (int i = 0; i < h; i++) x[i] = emb[i] * es;

            for (int l = 0; l < cfg.Layers; l++)
            {
                RmsNorm(x, w.Norm1, xn);
                Mv(w.Wq[l], xn, q);
                Mv(w.Wk[l], xn, k);
                Mv(w.Wv[l], xn, v);
                Rope(q, cfg.Heads, position);
                Rope(k, cfg.KvHeads, position);
                if (seq is null) cache!.Write(l, position, k, v);
                else seq.Write(l, position, k, v);

                long ta = Stopwatch.GetTimestamp();
                if (seq is null) attention.Run(cache!, l, position, q, ctx, attnKernel);
                else attention.RunPaged(seq, l, position, q, ctx);
                AttentionTicks += Stopwatch.GetTimestamp() - ta;

                Mv(w.Wo[l], ctx, xn);
                for (int i = 0; i < h; i++) x[i] += xn[i];

                RmsNorm(x, w.Norm2, xn);
                Mv(w.W1[l], xn, gate);
                Mv(w.W3[l], xn, up);
                for (int i = 0; i < cfg.Ffn; i++)
                {
                    float g = gate[i];
                    hid[i] = g / (1f + MathF.Exp(-g)) * up[i];
                }
                Mv(w.W2[l], hid, xn);
                for (int i = 0; i < h; i++) x[i] += xn[i];
            }

            RmsNorm(x, w.NormF, xn);
            Mv(w.Embedding, xn, logits);
            length++;
            ForwardTicks += Stopwatch.GetTimestamp() - t0;
            return logits;
        }

        /// <summary>Diagnostic counter: ticks spent in projection matvecs.</summary>
        public long MatVecTicks { get; private set; }

        /// <summary>
        /// Processes a whole prompt from an empty cache, reusing cached prefix blocks when a <see cref="PrefixCache"/>
        /// is given. Full blocks that are computed here are stored in the cache. Returns the logits after the last token.
        /// With a hit, the restored KV rows are exact copies, so logits are bit-identical to a cold prefill.
        /// </summary>
        /// <param name="tokens">Prompt token ids, at least one, at most MaxSeq.</param>
        /// <param name="prefix">Prefix cache for this decoder's shape and precision, or null for no reuse.</param>
        /// <returns>A copy of the logits after the last prompt token.</returns>
        public float[] Prefill(ReadOnlySpan<int> tokens, PrefixCache? prefix = null)
        {
            if (tokens.IsEmpty) throw new ArgumentException("Prompt must contain at least one token.", nameof(tokens));
            if (tokens.Length > cfg.MaxSeq) throw new ArgumentException("Prompt is longer than the context.", nameof(tokens));
            if (prefix is not null)
            {
                if (seq is not null) throw new NotSupportedException("Prefix caching uses contiguous decoders; paged sequences do not support it yet.");
                if (prefix.Mode != kvMode || !prefix.Config.Equals(cfg))
                    throw new ArgumentException("Prefix cache shape or precision does not match this decoder.", nameof(prefix));
            }
            Reset();
            int block = prefix?.BlockTokens ?? 0;
            ulong h = PrefixCache.Seed;
            float[]? last = null;

            if (prefix is not null)
            {
                // Restore the longest run of cached full blocks from the start of the prompt.
                while (length + block <= tokens.Length)
                {
                    ReadOnlySpan<int> blk = tokens.Slice(length, block);
                    ulong next = PrefixCache.ChainHash(h, blk);
                    KvBlock? hit = prefix.Lookup(next, blk, h);
                    if (hit is null) break;
                    KvBlockCodec.Restore(hit, cache!, length);
                    length += block;
                    h = next;
                    last = (float[])hit.Logits.Clone();
                }
            }

            for (int p = length; p < tokens.Length; p++)
            {
                float[] logits = Forward(tokens[p], p);
                last = logits.AsSpan(0, cfg.Vocab).ToArray();
                if (prefix is not null && (p + 1) % block == 0)
                {
                    ReadOnlySpan<int> blk = tokens.Slice(p + 1 - block, block);
                    ulong next = PrefixCache.ChainHash(h, blk);
                    prefix.Insert(KvBlockCodec.Capture(cache!, p + 1 - block, block, next, h, blk, last));
                    h = next;
                }
            }
            return last!;
        }

        /// <summary>
        /// Prefill on a paged decoder with prefix reuse by reference. Full blocks found in the index are appended to
        /// the sequence as shared pool blocks (no copy). Computed full blocks are indexed. Logits are bit-identical to a
        /// cold prefill. The sequence is cleared first; the index keeps its own references, so reused blocks survive.
        /// </summary>
        /// <param name="tokens">Prompt token ids, at least one, at most MaxSeq.</param>
        /// <param name="index">Index over this sequence's pool.</param>
        /// <returns>A copy of the logits after the last prompt token.</returns>
        public float[] Prefill(ReadOnlySpan<int> tokens, PagedPrefixIndex index)
        {
            if (index is null) throw new ArgumentNullException(nameof(index));
            if (seq is null) throw new InvalidOperationException("Paged prefill needs a paged decoder.");
            if (!ReferenceEquals(index.Pool, seq.Pool)) throw new ArgumentException("Index belongs to a different pool.", nameof(index));
            if (tokens.IsEmpty) throw new ArgumentException("Prompt must contain at least one token.", nameof(tokens));
            if (tokens.Length > cfg.MaxSeq) throw new ArgumentException("Prompt is longer than the context.", nameof(tokens));

            Reset();
            int bs = index.Pool.BlockSize;
            ulong h = PrefixCache.Seed;
            float[]? last = null;

            while (length + bs <= tokens.Length)
            {
                ReadOnlySpan<int> blk = tokens.Slice(length, bs);
                ulong next = PrefixCache.ChainHash(h, blk);
                if (!index.TryGet(next, blk, h, out int block, out float[] logits)) break;
                seq.AppendShared(block);
                length += bs;
                h = next;
                last = logits;
            }

            for (int p = length; p < tokens.Length; p++)
            {
                float[] logits = Forward(tokens[p], p);
                last = logits.AsSpan(0, cfg.Vocab).ToArray();
                if ((p + 1) % bs == 0)
                {
                    ReadOnlySpan<int> blk = tokens.Slice(p + 1 - bs, bs);
                    ulong next = PrefixCache.ChainHash(h, blk);
                    index.Insert(next, h, blk, seq.Table[seq.Table.Count - 1], last);
                    h = next;
                }
            }
            return last!;
        }

        /// <summary>
        /// Batched prefill on a paged decoder. The uncached part of the prompt runs in chunks of up to
        /// <paramref name="chunk"/> tokens, so every weight matrix is read once per chunk instead of once per token.
        /// Attention stays causal: position p reads positions 0..p only. Logits are bit-identical to <see cref="Prefill(ReadOnlySpan{int}, PagedPrefixIndex)"/>.
        /// </summary>
        /// <param name="tokens">Prompt token ids, at least one, at most MaxSeq.</param>
        /// <param name="chunk">Tokens per chunk, between 1 and 256.</param>
        /// <param name="index">Optional prefix index on this sequence's pool. Reused blocks are shared; computed full blocks are indexed.</param>
        /// <returns>A copy of the logits after the last prompt token.</returns>
        public float[] PrefillBatched(ReadOnlySpan<int> tokens, int chunk = 64, PagedPrefixIndex? index = null)
        {
            if (seq is null) throw new InvalidOperationException("Batched prefill needs a paged decoder.");
            if (chunk < 1 || chunk > 256) throw new ArgumentOutOfRangeException(nameof(chunk));
            if (tokens.IsEmpty) throw new ArgumentException("Prompt must contain at least one token.", nameof(tokens));
            if (tokens.Length > cfg.MaxSeq) throw new ArgumentException("Prompt is longer than the context.", nameof(tokens));
            if (index is not null && !ReferenceEquals(index.Pool, seq.Pool))
                throw new ArgumentException("Index belongs to a different pool.", nameof(index));

            Reset();
            int n = tokens.Length;
            int bs = index?.Pool.BlockSize ?? 0;
            ulong h = PrefixCache.Seed;
            float[]? last = null;

            if (index is not null)
            {
                while (length + bs <= n)
                {
                    ReadOnlySpan<int> blk = tokens.Slice(length, bs);
                    ulong next = PrefixCache.ChainHash(h, blk);
                    if (!index.TryGet(next, blk, h, out int block, out float[] logits)) break;
                    seq.AppendShared(block);
                    length += bs;
                    h = next;
                    last = logits;
                }
            }

            while (length < n)
            {
                int start = length;
                int t = Math.Min(chunk, n - start);
                EnsureChunkScratch(t);
                ForwardChunk(tokens.Slice(start, t), start);
                length = start + t;

                // Logits are needed at block ends that get indexed, and at the prompt end.
                for (int i = 0; i < t; i++)
                {
                    int p = start + i;
                    bool blockEnd = index is not null && (p + 1) % bs == 0;
                    bool promptEnd = p == n - 1;
                    if (!blockEnd && !promptEnd) continue;
                    float[] logits = HeadLogits(i);
                    if (blockEnd)
                    {
                        ReadOnlySpan<int> blk = tokens.Slice(p + 1 - bs, bs);
                        ulong next = PrefixCache.ChainHash(h, blk);
                        index!.Insert(next, h, blk, seq.Table[p / bs], logits);   // the block holding position p, not the last allocated block
                        h = next;
                    }
                    if (promptEnd) last = logits;
                }
            }
            return last!;
        }

        private float[] cx = Array.Empty<float>(), cxn = Array.Empty<float>(), cq = Array.Empty<float>(), ck = Array.Empty<float>(),
            cv = Array.Empty<float>(), cctx = Array.Empty<float>(), cgate = Array.Empty<float>(), cup = Array.Empty<float>(),
            chid = Array.Empty<float>(), clogits = Array.Empty<float>();
        private int chunkCap;

        private void EnsureChunkScratch(int t)
        {
            if (t <= chunkCap) return;
            chunkCap = t;
            cx = new float[t * cfg.Hidden];
            cxn = new float[t * cfg.Hidden];
            cq = new float[t * cfg.Hidden];
            cctx = new float[t * cfg.Hidden];
            ck = new float[t * cfg.KvDim];
            cv = new float[t * cfg.KvDim];
            cgate = new float[t * cfg.Ffn];
            cup = new float[t * cfg.Ffn];
            chid = new float[t * cfg.Ffn];
            clogits = new float[t * cfg.Vocab];
        }

        // Hidden states for a chunk of t tokens at positions start..start+t-1, using the paged sequence.
        // Each token's arithmetic matches Forward exactly; only the matvecs are batched over the chunk.
        private void ForwardChunk(ReadOnlySpan<int> toks, int start)
        {
            int t = toks.Length, h = cfg.Hidden, f = cfg.Ffn, kvd = cfg.KvDim;
            for (int i = 0; i < t; i++)
            {
                ReadOnlySpan<sbyte> emb = w.Embedding.Row(toks[i]);
                float es = w.Embedding.Scale[toks[i]];
                for (int d = 0; d < h; d++) cx[i * h + d] = emb[d] * es;
            }

            for (int l = 0; l < cfg.Layers; l++)
            {
                for (int i = 0; i < t; i++) RmsNorm(cx.AsSpan(i * h, h), w.Norm1, cxn.AsSpan(i * h, h));
                MvBatch(w.Wq[l], t, cxn, cq);
                MvBatch(w.Wk[l], t, cxn, ck);
                MvBatch(w.Wv[l], t, cxn, cv);
                for (int i = 0; i < t; i++)
                {
                    int pos = start + i;
                    Rope(cq.AsSpan(i * h, h), cfg.Heads, pos);
                    Rope(ck.AsSpan(i * kvd, kvd), cfg.KvHeads, pos);
                    seq!.Write(l, pos, ck.AsSpan(i * kvd, kvd), cv.AsSpan(i * kvd, kvd));
                }
                attention.RunPagedChunk(seq!, l, start, t, cq.AsSpan(0, t * h), cctx.AsSpan(0, t * h));

                MvBatch(w.Wo[l], t, cctx, cxn);
                for (int i = 0; i < t * h; i++) cx[i] += cxn[i];

                for (int i = 0; i < t; i++) RmsNorm(cx.AsSpan(i * h, h), w.Norm2, cxn.AsSpan(i * h, h));
                MvBatch(w.W1[l], t, cxn, cgate);
                MvBatch(w.W3[l], t, cxn, cup);
                for (int i = 0; i < t * f; i++)
                {
                    float g = cgate[i];
                    chid[i] = g / (1f + MathF.Exp(-g)) * cup[i];
                }
                MvBatch(w.W2[l], t, chid, cxn);
                for (int i = 0; i < t * h; i++) cx[i] += cxn[i];
            }
        }

        // Final norm and LM head for token i of the last chunk. Returns a fresh array.
        private float[] HeadLogits(int i)
        {
            int h = cfg.Hidden;
            RmsNorm(cx.AsSpan(i * h, h), w.NormF, cxn.AsSpan(0, h));
            engine.Run(w.Embedding, cxn.AsSpan(0, h), clogits.AsSpan(0, cfg.Vocab));
            return clogits.AsSpan(0, cfg.Vocab).ToArray();
        }

        private void MvBatch(QMat m, int t, ReadOnlySpan<float> input, Span<float> output)
        {
            engine.RunBatch(m, t, input.Slice(0, t * m.Cols), output.Slice(0, t * m.Rows));
        }

        private void Mv(QMat m, ReadOnlySpan<float> input, Span<float> output)
        {
            long t = Stopwatch.GetTimestamp();
            switch (matVecKernel)
            {
                case MatVecKernel.Scalar: Kernels.MatVecScalar(m, input, output); break;
                case MatVecKernel.Avx2Serial: Kernels.MatVecAvx2(m, input, output); break;
                default: engine.Run(m, input, output); break;
            }
            MatVecTicks += Stopwatch.GetTimestamp() - t;
        }

        internal static void RmsNorm(ReadOnlySpan<float> src, ReadOnlySpan<float> gain, Span<float> dst)
        {
            float ss = 0f;
            for (int i = 0; i < src.Length; i++) ss += src[i] * src[i];
            float inv = 1f / MathF.Sqrt(ss / src.Length + 1e-6f);
            for (int i = 0; i < src.Length; i++) dst[i] = src[i] * inv * gain[i];
        }

        private void Rope(Span<float> vec, int heads, int pos)
        {
            int half = cfg.HeadDim / 2;
            for (int hIdx = 0; hIdx < heads; hIdx++)
            {
                int off = hIdx * cfg.HeadDim;
                for (int i = 0; i < half; i++)
                {
                    float c = cos[pos * half + i], s = sin[pos * half + i];
                    float a = vec[off + i], b = vec[off + i + half];
                    vec[off + i] = a * c - b * s;
                    vec[off + i + half] = a * s + b * c;
                }
            }
        }
    }
}
