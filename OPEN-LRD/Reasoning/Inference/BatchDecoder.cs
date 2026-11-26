using System;
using System.Collections.Generic;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// Advances several paged sequences by one token per step. Each weight matrix is read once per step and applied
    /// to every sequence (see <see cref="MatVecEngine.RunBatch"/>). Attention runs per sequence over its own block table,
    /// so sequences may have different lengths and may share prefix blocks.
    /// Output for each sequence is bit-identical to a single-sequence <see cref="Decoder"/> on the same pool.
    /// All sequences must live on one <see cref="KvBlockPool"/>. Not thread-safe.
    /// </summary>
    public sealed class BatchDecoder
    {
        private readonly DecoderWeights w;
        private readonly ModelConfig cfg;
        private readonly PagedKvSequence[] seqs;
        private readonly Attention attention;
        private readonly MatVecEngine engine;
        private readonly float[] cos;
        private readonly float[] sin;
        private readonly int batch;
        private readonly float[] x, xn, q, k, v, ctx, gate, up, hid, logits;

        /// <summary>
        /// Creates a batch decoder over the given sequences.
        /// </summary>
        /// <param name="weights">Model weights.</param>
        /// <param name="sequences">Sequences to advance, at least one. All must use the same pool, which must match the weights.</param>
        /// <param name="blocks">Row blocks for the parallel matvec. Defaults to the processor count.</param>
        public BatchDecoder(DecoderWeights weights, IReadOnlyList<PagedKvSequence> sequences, int? blocks = null)
        {
            w = weights ?? throw new ArgumentNullException(nameof(weights));
            if (sequences is null) throw new ArgumentNullException(nameof(sequences));
            if (sequences.Count < 1) throw new ArgumentException("At least one sequence is required.", nameof(sequences));
            cfg = weights.Config;
            KvBlockPool pool = sequences[0].Pool;
            if (!pool.Config.Equals(cfg)) throw new ArgumentException("Pool shape does not match the weights.", nameof(sequences));
            seqs = new PagedKvSequence[sequences.Count];
            for (int i = 0; i < seqs.Length; i++)
            {
                if (sequences[i] is null) throw new ArgumentException("Sequence list contains null.", nameof(sequences));
                if (!ReferenceEquals(sequences[i].Pool, pool)) throw new ArgumentException("All sequences must share one pool.", nameof(sequences));
                seqs[i] = sequences[i];
            }
            batch = seqs.Length;
            attention = new Attention(cfg);
            engine = new MatVecEngine(blocks ?? Environment.ProcessorCount);

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

            x = new float[batch * cfg.Hidden];
            xn = new float[batch * cfg.Hidden];
            q = new float[batch * cfg.Hidden];
            k = new float[batch * cfg.KvDim];
            v = new float[batch * cfg.KvDim];
            ctx = new float[batch * cfg.Hidden];
            gate = new float[batch * cfg.Ffn];
            up = new float[batch * cfg.Ffn];
            hid = new float[batch * cfg.Ffn];
            logits = new float[batch * cfg.Vocab];
        }

        /// <summary>Number of sequences advanced per step.</summary>
        public int BatchSize => batch;

        /// <summary>The sequences, in the order given to the constructor.</summary>
        public IReadOnlyList<PagedKvSequence> Sequences => seqs;

        /// <summary>
        /// Processes one token for every sequence. Sequence i receives <paramref name="tokens"/>[i] at its own next
        /// position (its current <see cref="PagedKvSequence.Length"/>).
        /// If a pool runs out of blocks partway through, the step throws and the affected sequences may hold a partial
        /// position; clear them before reuse.
        /// </summary>
        /// <param name="tokens">One token id per sequence, each in [0, Vocab).</param>
        /// <returns>Logits for each sequence after this token. Fresh arrays, owned by the caller.</returns>
        public float[][] Forward(ReadOnlySpan<int> tokens)
        {
            if (tokens.Length != batch) throw new ArgumentException($"Expected {batch} tokens, got {tokens.Length}.", nameof(tokens));
            int[] pos = new int[batch];
            for (int b = 0; b < batch; b++)
            {
                if ((uint)tokens[b] >= cfg.Vocab) throw new ArgumentOutOfRangeException(nameof(tokens));
                pos[b] = seqs[b].Length;
                if (pos[b] >= cfg.MaxSeq) throw new InvalidOperationException($"Sequence {b} is full.");
            }

            int h = cfg.Hidden, f = cfg.Ffn, kvd = cfg.KvDim;
            for (int b = 0; b < batch; b++)
            {
                ReadOnlySpan<sbyte> emb = w.Embedding.Row(tokens[b]);
                float es = w.Embedding.Scale[tokens[b]];
                for (int i = 0; i < h; i++) x[b * h + i] = emb[i] * es;
            }

            for (int l = 0; l < cfg.Layers; l++)
            {
                for (int b = 0; b < batch; b++) Decoder.RmsNorm(X(b), w.Norm1, Xn(b));
                Mv(w.Wq[l], xn, q);
                Mv(w.Wk[l], xn, k);
                Mv(w.Wv[l], xn, v);
                for (int b = 0; b < batch; b++)
                {
                    Rope(Q(b), cfg.Heads, pos[b]);
                    Rope(K(b), cfg.KvHeads, pos[b]);
                    seqs[b].Write(l, pos[b], K(b), V(b));
                    attention.RunPaged(seqs[b], l, pos[b], Q(b), Ctx(b));
                }

                Mv(w.Wo[l], ctx, xn);
                for (int i = 0; i < batch * h; i++) x[i] += xn[i];

                for (int b = 0; b < batch; b++) Decoder.RmsNorm(X(b), w.Norm2, Xn(b));
                Mv(w.W1[l], xn, gate);
                Mv(w.W3[l], xn, up);
                for (int i = 0; i < batch * f; i++)
                {
                    float g = gate[i];
                    hid[i] = g / (1f + MathF.Exp(-g)) * up[i];
                }
                Mv(w.W2[l], hid, xn);
                for (int i = 0; i < batch * h; i++) x[i] += xn[i];
            }

            for (int b = 0; b < batch; b++) Decoder.RmsNorm(X(b), w.NormF, Xn(b));
            Mv(w.Embedding, xn, logits);

            float[][] result = new float[batch][];
            for (int b = 0; b < batch; b++)
            {
                result[b] = new float[cfg.Vocab];
                Array.Copy(logits, b * cfg.Vocab, result[b], 0, cfg.Vocab);
            }
            return result;
        }

        private void Mv(QMat m, float[] input, float[] output)
        {
            engine.RunBatch(m, batch, input, output);
        }

        private ReadOnlySpan<float> X(int b) => x.AsSpan(b * cfg.Hidden, cfg.Hidden);
        private Span<float> Xn(int b) => xn.AsSpan(b * cfg.Hidden, cfg.Hidden);
        private Span<float> Q(int b) => q.AsSpan(b * cfg.Hidden, cfg.Hidden);
        private Span<float> Ctx(int b) => ctx.AsSpan(b * cfg.Hidden, cfg.Hidden);
        private Span<float> K(int b) => k.AsSpan(b * cfg.KvDim, cfg.KvDim);
        private Span<float> V(int b) => v.AsSpan(b * cfg.KvDim, cfg.KvDim);

        private void Rope(Span<float> vec, int heads, int pos)
        {
            int half = cfg.HeadDim / 2;
            for (int hIdx = 0; hIdx < heads; hIdx++)
            {
                int off = hIdx * cfg.HeadDim;
                for (int i = 0; i < half; i++)
                {
                    float c = cos[pos * half + i], s = sin[pos * half + i];
                    float a = vec[off + i], bb = vec[off + i + half];
                    vec[off + i] = a * c - bb * s;
                    vec[off + i + half] = a * s + bb * c;
                }
            }
        }
    }
}
