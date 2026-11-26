using System;
using System.Linq;
using OPEN_LRD.Reasoning.Inference;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ReasoningInferenceTests
    {
        // Small decoder shape for fast tests. Hidden = Heads * HeadDim.
        private static readonly ModelConfig Small = new ModelConfig(Hidden: 64, Layers: 2, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 128, Vocab: 50, MaxSeq: 64);

        public static void RunAll()
        {
            Run("Inference kernels: AVX2 vs scalar", TestKernelsAgainstScalar);
            Run("Inference kernels: parallel and batched bit-identical to serial", TestParallelAndBatchBitIdentical);
            Run("Inference attention: fused fp32 vs scalar across chunks", TestAttentionFp32);
            Run("Inference attention: fused int8 vs scalar and quality", TestAttentionInt8);
            Run("Inference KV cache round-trip", TestKvRoundTrip);
            Run("Inference sampler: determinism and top-p", TestSampler);
            Run("Inference sampler: distribution", TestSamplerDistribution);
            Run("Inference decoder: deterministic and kernel-consistent", TestDecoderDeterminism);
            Run("Inference decoder: sequential positions enforced", TestDecoderSequencing);
            Run("Inference ILocalModel: fake model sequencing", TestFakeLocalModel);
            Run("Inference config validation", TestConfigValidation);
            Run("Inference KV policy: int8 default for long context", TestKvPolicy);
            Run("Prefix cache: hit is bit-identical to cold prefill (fp32, int8)", TestPrefixHitIdentical);
            Run("Prefix cache: extended prompt reuses cached blocks", TestPrefixExtension);
            Run("Prefix cache: eviction to disk and disk hit", TestPrefixDiskTier);
            Run("Prefix cache: corrupt disk block is a miss", TestPrefixCorruptFile);
            Run("Prefix cache: shape and mode checks", TestPrefixValidation);
            Run("Prefix cache: chain hash is sensitive to order and parent", TestChainHash);
            Run("Paged KV: logits bit-identical to contiguous (fp32, int8)", TestPagedMatchesContiguous);
            Run("Paged KV: fork and copy-on-write keep each branch exact", TestPagedForkCopyOnWrite);
            Run("Paged KV: pool exhaustion and release", TestPagedPoolLifecycle);
            Run("Paged KV: shape and argument validation", TestPagedValidation);
            Run("Paged prefix index: reuse shares physical blocks, bit-identical", TestPagedPrefixReuse);
            Run("Paged prefix index: eviction releases blocks", TestPagedPrefixEviction);
            Run("Paged prefix index: validation", TestPagedPrefixValidation);
            Run("Batched decode: each sequence bit-identical to a single paged decoder", TestBatchMatchesSingle);
            Run("Batched decode: shared prefix blocks and different lengths", TestBatchSharedPrefix);
            Run("Batched decode: MatMat matches single matvec bit for bit", TestMatMatBits);
            Run("Batched decode: validation", TestBatchValidation);
            Run("Batched prefill: bit-identical to sequential prefill for several chunk sizes", TestBatchedPrefillIdentical);
            Run("Batched prefill: prefix index reuse and inserts", TestBatchedPrefillPrefixIndex);
            Run("Batched prefill: validation", TestBatchedPrefillValidation);
            Run("Batched attention: chunk crossing the 1024-position boundary matches per-token", TestBatchedAttentionLongContext);
            Run("Batched prefill: disk tier restores evicted prefix blocks, bit-identical", TestBatchedPrefillDiskTier);
        }

        private static float[] RandomVec(Random rng, int n, float scale)
        {
            float[] a = new float[n];
            for (int i = 0; i < n; i++) a[i] = (float)((rng.NextDouble() * 2 - 1) * scale);
            return a;
        }

        private static void TestKernelsAgainstScalar()
        {
            Random rng = new Random(11);
            foreach ((int rows, int cols) in new[] { (6912, 2560), (512, 2560), (33, 50), (100, 16) })
            {
                QMat m = QMat.Random(rows, cols, rng);
                float[] x = RandomVec(rng, cols, 1f);
                float[] s = new float[rows], a = new float[rows];
                Kernels.MatVecScalar(m, x, s);
                Kernels.MatVecAvx2(m, x, a);
                // Reduction order differs, so bound the error relative to the output magnitude (fp32 rounding).
                double maxErr = 0, maxRef = 1;
                for (int i = 0; i < rows; i++)
                {
                    maxErr = Math.Max(maxErr, Math.Abs(s[i] - a[i]));
                    maxRef = Math.Max(maxRef, Math.Abs(s[i]));
                }
                Assert(maxErr < 1e-5 * maxRef, $"AVX2 vs scalar {rows}x{cols}: max abs error {maxErr:E3} (max output {maxRef:E3})");
            }
        }

        private static void TestParallelAndBatchBitIdentical()
        {
            Random rng = new Random(12);
            var engine = new MatVecEngine(16);
            foreach ((int rows, int cols) in new[] { (6912, 2560), (512, 2560), (33, 50) })
            {
                QMat m = QMat.Random(rows, cols, rng);
                float[] x = RandomVec(rng, cols, 1f);
                float[] serial = new float[rows], par = new float[rows];
                Kernels.MatVecAvx2(m, x, serial);
                engine.Run(m, x, par);
                for (int i = 0; i < rows; i++)
                    Assert(BitConverter.SingleToInt32Bits(serial[i]) == BitConverter.SingleToInt32Bits(par[i]), $"parallel differs from serial at {rows}x{cols} row {i}");
            }

            // Batch of 3: each row must match the single-vector result bit for bit.
            QMat mb = QMat.Random(300, 64, rng);
            float[] xs = RandomVec(rng, 3 * 64, 1f);
            float[] ys = new float[3 * 300];
            Kernels.MatMat(mb, xs, 3, ys);
            for (int b = 0; b < 3; b++)
            {
                float[] single = new float[300];
                Kernels.MatVecAvx2(mb, xs.AsSpan(b * 64, 64), single);
                for (int i = 0; i < 300; i++)
                    Assert(BitConverter.SingleToInt32Bits(single[i]) == BitConverter.SingleToInt32Bits(ys[b * 300 + i]), $"batch element {b} row {i} differs");
            }
        }

        private static void FillCache(KvCache cache, int layers, int positions, Random rng)
        {
            float[] k = new float[cache.Config.KvDim], v = new float[cache.Config.KvDim];
            for (int p = 0; p < positions; p++)
            {
                for (int l = 0; l < layers; l++)
                {
                    RandomVec(rng, k.Length, 1f).CopyTo(k, 0);
                    RandomVec(rng, v.Length, 1f).CopyTo(v, 0);
                    cache.Write(l, p, k, v);
                }
            }
        }

        private static ModelConfig AttnShape() => new ModelConfig(Hidden: 64, Layers: 1, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 16, Vocab: 8, MaxSeq: 4096);

        private static void TestAttentionFp32()
        {
            ModelConfig cfg = AttnShape();
            Random rng = new Random(21);
            KvCache cache = new KvCache(cfg, KvMode.Fp32);
            FillCache(cache, 1, 4096, rng);
            Attention att = new Attention(cfg);
            float[] q = RandomVec(rng, cfg.Hidden, 3f);
            float[] s = new float[cfg.Hidden], f = new float[cfg.Hidden], fs = new float[cfg.Hidden];
            foreach (int pos in new[] { 0, 1, 1023, 1024, 2047, 4095 })
            {
                att.ScalarReference(cache, 0, pos, q, s);
                att.Run(cache, 0, pos, q, f, AttnKernel.FusedFp32, parallel: true);
                att.Run(cache, 0, pos, q, fs, AttnKernel.FusedFp32, parallel: false);
                double maxErr = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    maxErr = Math.Max(maxErr, Math.Abs(s[i] - f[i]));
                    Assert(BitConverter.SingleToInt32Bits(f[i]) == BitConverter.SingleToInt32Bits(fs[i]), $"parallel != serial at pos {pos}");
                }
                Assert(maxErr < 1e-5, $"fused fp32 vs scalar at pos {pos}: {maxErr:E3}");
            }
        }

        private static void TestAttentionInt8()
        {
            ModelConfig cfg = AttnShape();
            Random rng = new Random(22);
            KvCache c8 = new KvCache(cfg, KvMode.Int8);
            KvCache cf = new KvCache(cfg, KvMode.Fp32);
            float[] k = new float[cfg.KvDim], v = new float[cfg.KvDim];
            for (int p = 0; p < 2048; p++)
            {
                RandomVec(rng, k.Length, 1f).CopyTo(k, 0);
                RandomVec(rng, v.Length, 1f).CopyTo(v, 0);
                c8.Write(0, p, k, v);
                cf.Write(0, p, k, v);
            }
            Attention att = new Attention(cfg);
            float[] q = RandomVec(rng, cfg.Hidden, 3f);
            float[] s = new float[cfg.Hidden], f = new float[cfg.Hidden], fs = new float[cfg.Hidden];
            float[] ref8 = new float[cfg.Hidden];
            foreach (int pos in new[] { 0, 1, 1023, 1024, 2047 })
            {
                att.ScalarReference(c8, 0, pos, q, s);
                att.Run(c8, 0, pos, q, f, AttnKernel.FusedInt8, parallel: true);
                att.Run(c8, 0, pos, q, fs, AttnKernel.FusedInt8, parallel: false);
                double maxErr = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    maxErr = Math.Max(maxErr, Math.Abs(s[i] - f[i]));
                    Assert(BitConverter.SingleToInt32Bits(f[i]) == BitConverter.SingleToInt32Bits(fs[i]), $"int8 parallel != serial at pos {pos}");
                }
                Assert(maxErr < 1e-5, $"fused int8 vs int8 scalar at pos {pos}: {maxErr:E3}");

                // Quality: int8 cache vs fp32 cache with the same data.
                att.ScalarReference(cf, 0, pos, q, ref8);
                double num = 0, den = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    double d = s[i] - ref8[i];
                    num += d * d;
                    den += (double)ref8[i] * ref8[i];
                }
                double rel = Math.Sqrt(num / Math.Max(den, 1e-30));
                Assert(rel < 0.02, $"int8 KV relative RMS error at pos {pos}: {rel:E3}");
            }
            Assert(AttnKernelMismatchThrows(cf, cfg), "fused int8 on fp32 cache must throw");
        }

        private static bool AttnKernelMismatchThrows(KvCache fp, ModelConfig cfg)
        {
            try
            {
                new Attention(cfg).Run(fp, 0, 0, new float[cfg.Hidden], new float[cfg.Hidden], AttnKernel.FusedInt8);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        private static void TestKvRoundTrip()
        {
            Random rng = new Random(31);
            ModelConfig cfg = Small;
            float[] k = RandomVec(rng, cfg.KvDim, 2f), v = RandomVec(rng, cfg.KvDim, 2f);
            float[] back = new float[cfg.HeadDim];
            KvCache f = new KvCache(cfg, KvMode.Fp32);
            f.Write(1, 7, k, v);
            for (int g = 0; g < cfg.KvHeads; g++)
            {
                f.ReadRow(1, g, 7, value: false, back);
                for (int d = 0; d < cfg.HeadDim; d++) Assert(back[d] == k[g * cfg.HeadDim + d], "fp32 key round-trip must be exact");
                f.ReadRow(1, g, 7, value: true, back);
                for (int d = 0; d < cfg.HeadDim; d++) Assert(back[d] == v[g * cfg.HeadDim + d], "fp32 value round-trip must be exact");
            }
            KvCache q8 = new KvCache(cfg, KvMode.Int8);
            q8.Write(1, 7, k, v);
            for (int g = 0; g < cfg.KvHeads; g++)
            {
                q8.ReadRow(1, g, 7, value: false, back);
                float maxAbs = 0;
                for (int d = 0; d < cfg.HeadDim; d++) maxAbs = Math.Max(maxAbs, Math.Abs(k[g * cfg.HeadDim + d]));
                for (int d = 0; d < cfg.HeadDim; d++)
                    Assert(Math.Abs(back[d] - k[g * cfg.HeadDim + d]) <= maxAbs / 254f + 1e-6f, "int8 key error must be within half a step");
            }
            Assert(q8.Bytes < f.Bytes, "int8 cache must be smaller than fp32");
        }

        private static void TestSampler()
        {
            float[] logits = { 0.1f, 2.0f, -1.0f, 1.5f, 0.3f };
            Sampler a = new Sampler(123), b = new Sampler(123), c = new Sampler(124);
            bool anyDiff = false;
            for (int i = 0; i < 500; i++)
            {
                int ta = a.Sample(logits), tb = b.Sample(logits), tc = c.Sample(logits);
                Assert(ta == tb, "same seed must give the same token");
                if (ta != tc) anyDiff = true;
            }
            Assert(anyDiff, "different seeds should differ somewhere over 500 draws");

            Sampler s = new Sampler(5);
            for (int i = 0; i < 100; i++)
                Assert(s.Sample(logits, temperature: 0.7, topP: 1e-9) == 1, "tiny top-p must keep only the argmax");

            double u = new Sampler(9).NextDouble();
            Assert(u >= 0 && u < 1, "NextDouble in [0, 1)");
            AssertThrows<ArgumentOutOfRangeException>(() => s.Sample(logits, temperature: 0.0), "zero temperature rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => s.Sample(logits, topP: 1.5), "top-p above 1 rejected");
        }

        private static void TestSamplerDistribution()
        {
            // Two tokens with logits ln(1) and ln(2): probabilities 1/3 and 2/3 at temperature 1, no truncation.
            float[] logits = { 0f, (float)Math.Log(2) };
            Sampler s = new Sampler(77);
            int ones = 0;
            const int draws = 20000;
            for (int i = 0; i < draws; i++) ones += s.Sample(logits, temperature: 1.0, topP: 1.0);
            double freq = ones / (double)draws;
            Assert(Math.Abs(freq - 2.0 / 3.0) < 0.02, $"token 1 frequency {freq:F4}, expected about 0.6667");
        }

        private static void TestDecoderDeterminism()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(41));
            Decoder a = new Decoder(w, KvMode.Fp32, blocks: 4);
            Decoder b = new Decoder(w, KvMode.Fp32, blocks: 1);
            a.MatVecKernel = MatVecKernel.Avx2Parallel;
            b.MatVecKernel = MatVecKernel.Avx2Serial;
            Decoder scalar = new Decoder(w, KvMode.Fp32);
            scalar.MatVecKernel = MatVecKernel.Scalar;

            int[] tokens = { 3, 17, 42, 8, 0 };
            for (int p = 0; p < tokens.Length; p++)
            {
                float[] la = a.Forward(tokens[p], p);
                float[] lb = b.Forward(tokens[p], p);
                float[] ls = scalar.Forward(tokens[p], p);
                for (int i = 0; i < la.Length; i++)
                {
                    Assert(BitConverter.SingleToInt32Bits(la[i]) == BitConverter.SingleToInt32Bits(lb[i]), $"parallel and serial decoder logits differ at pos {p}");
                    Assert(Math.Abs(la[i] - ls[i]) < 1e-3f * (1 + Math.Abs(ls[i])), $"AVX2 and scalar decoder logits differ at pos {p}");
                }
            }
            Assert(a.Length == tokens.Length, "length tracks processed positions");

            // Int8 KV path runs and produces finite logits.
            Decoder q = new Decoder(w, KvMode.Int8);
            float[] lq = null!;
            for (int p = 0; p < tokens.Length; p++) lq = q.Forward(tokens[p], p);
            foreach (float v in lq) Assert(float.IsFinite(v), "int8 decoder logits must be finite");

            // Reset starts a new sequence with identical output.
            a.Reset();
            float[] again = a.Forward(tokens[0], 0);
            float[] first = new Decoder(w, KvMode.Fp32, blocks: 4).Forward(tokens[0], 0);
            for (int i = 0; i < again.Length; i++)
                Assert(BitConverter.SingleToInt32Bits(again[i]) == BitConverter.SingleToInt32Bits(first[i]), "reset must reproduce the first output");
        }

        private static void TestDecoderSequencing()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(42));
            Decoder d = new Decoder(w);
            AssertThrows<InvalidOperationException>(() => d.Forward(1, 1), "position must start at 0");
            d.Forward(1, 0);
            AssertThrows<InvalidOperationException>(() => d.Forward(1, 0), "repeated position rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => d.Forward(999, 1), "token out of range rejected");
        }

        // Fake model: next token is (token + 1) mod V, with the highest logit at that index.
        private sealed class FakeModel : ILocalModel
        {
            private readonly float[] buf = new float[10];
            public int VocabSize => 10;
            public int MaxContext => 4;
            public int Length { get; private set; }
            public float[] Forward(int token, int position)
            {
                if (position != Length) throw new InvalidOperationException("sequencing");
                if (Length >= MaxContext) throw new InvalidOperationException("full");
                Array.Clear(buf);
                buf[(token + 1) % 10] = 5f;
                Length++;
                return buf;
            }
            public void Reset() => Length = 0;
        }

        private static void TestFakeLocalModel()
        {
            ILocalModel m = new FakeModel();
            int token = 0;
            for (int p = 0; p < m.MaxContext; p++)
            {
                float[] logits = m.Forward(token, p);
                Assert(logits.Length == m.VocabSize, "logits length equals vocabulary size");
                int best = 0;
                for (int i = 1; i < logits.Length; i++) if (logits[i] > logits[best]) best = i;
                token = best;
            }
            Assert(token == 4, $"fake sequence ended at {token}, expected 4");
            Assert(m.Length == m.MaxContext, "length tracks processed positions");
            AssertThrows<InvalidOperationException>(() => m.Forward(0, 0), "out-of-order position rejected");
            m.Reset();
            Assert(m.Length == 0, "reset clears length");

            // Decoder is usable through the interface.
            ILocalModel dec = new Decoder(DecoderWeights.Random(Small, new Random(43)));
            Assert(dec.VocabSize == Small.Vocab && dec.MaxContext == Small.MaxSeq, "decoder reports its shape");
            Assert(dec.Forward(2, 0).Length == Small.Vocab, "decoder logits length");
        }

        private static void TestKvPolicy()
        {
            Assert(KvPolicy.DefaultFor(4095) == KvMode.Fp32, "below threshold keeps fp32");
            Assert(KvPolicy.DefaultFor(4096) == KvMode.Int8, "threshold selects int8");
            Assert(KvPolicy.DefaultFor(ModelConfig.Benchmark.MaxSeq) == KvMode.Int8, "16k context selects int8");
            Assert(new Decoder(DecoderWeights.Random(Small, new Random(51))).Cache.Mode == KvMode.Fp32, "short-context decoder defaults to fp32");
            ModelConfig longCfg = new ModelConfig(Hidden: 64, Layers: 1, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 16, Vocab: 8, MaxSeq: 4096);
            Decoder longDec = new Decoder(DecoderWeights.Random(longCfg, new Random(52)));
            Assert(longDec.Cache.Mode == KvMode.Int8, "long-context decoder defaults to int8");
            Assert(longDec.AttentionKernel == AttnKernel.FusedInt8, "long-context decoder uses the int8 kernel");
            Assert(new Decoder(DecoderWeights.Random(longCfg, new Random(53)), KvMode.Fp32).Cache.Mode == KvMode.Fp32, "explicit fp32 overrides the policy");
        }

        private static int[] Prompt(int n, int seed)
        {
            Random r = new Random(seed);
            int[] t = new int[n];
            for (int i = 0; i < n; i++) t[i] = r.Next(Small.Vocab);
            return t;
        }

        private static void AssertSameBits(float[] a, float[] b, string msg)
        {
            Assert(a.Length == b.Length, msg + ": length");
            for (int i = 0; i < a.Length; i++)
                Assert(BitConverter.SingleToInt32Bits(a[i]) == BitConverter.SingleToInt32Bits(b[i]), msg + $": logit {i} differs");
        }

        private static string TempDir()
        {
            string d = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kvb-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(d);
            return d;
        }

        private static void TestPrefixHitIdentical()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(61));
            int[] tokens = Prompt(40, 1);
            foreach (KvMode mode in new[] { KvMode.Fp32, KvMode.Int8 })
            {
                float[] cold = new Decoder(w, mode).Prefill(tokens);
                PrefixCache pc = new PrefixCache(Small, mode, 16, 1L << 30);
                float[] first = new Decoder(w, mode).Prefill(tokens, pc);
                AssertSameBits(first, cold, $"{mode} first prefill with cache");
                Assert(pc.HotCount == 2, $"{mode}: two full blocks should be stored, got {pc.HotCount}");

                Decoder second = new Decoder(w, mode);
                float[] hit = second.Prefill(tokens, pc);
                AssertSameBits(hit, cold, $"{mode} prefill from cache");
                Assert(pc.Stats.Hits == 2, $"{mode}: expected 2 block hits, got {pc.Stats.Hits}");
                Assert(second.Length == tokens.Length, $"{mode}: length after prefill");
            }
        }

        private static void TestPrefixExtension()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(62));
            int[] tokens = Prompt(40, 2);
            int[] longer = new int[50];
            Array.Copy(tokens, longer, 40);
            for (int i = 40; i < 50; i++) longer[i] = (i * 7) % Small.Vocab;

            PrefixCache pc = new PrefixCache(Small, KvMode.Fp32, 16, 1L << 30);
            new Decoder(w).Prefill(tokens, pc);
            long before = pc.Stats.Hits;
            float[] cold = new Decoder(w).Prefill(longer);
            float[] warm = new Decoder(w).Prefill(longer, pc);
            AssertSameBits(warm, cold, "extended prompt");
            Assert(pc.Stats.Hits - before == 2, $"extended prompt should reuse 2 blocks, reused {pc.Stats.Hits - before}");

            // A prompt that shares only the first block reuses exactly one block.
            int[] divergent = (int[])tokens.Clone();
            divergent[20] = (divergent[20] + 1) % Small.Vocab;
            long before2 = pc.Stats.Hits;
            AssertSameBits(new Decoder(w).Prefill(divergent, pc), new Decoder(w).Prefill(divergent), "divergent prompt");
            Assert(pc.Stats.Hits - before2 == 1, $"divergent prompt should reuse one block, reused {pc.Stats.Hits - before2}");
        }

        private static void TestPrefixDiskTier()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(63));
            int[] tokens = Prompt(40, 3);
            string dir = TempDir();
            try
            {
                // Budget of one fp32 block: 2 layers * 2 KV heads * 16 positions * 16 dims * 8 bytes + logits and tokens.
                long oneBlock = new KvBlock(0, 0, KvMode.Fp32, Small, 16).Bytes;
                PrefixCache pc = new PrefixCache(Small, KvMode.Fp32, 16, oneBlock, dir);
                float[] cold = new Decoder(w).Prefill(tokens);
                AssertSameBits(new Decoder(w).Prefill(tokens, pc), cold, "prefill with tiny RAM budget");
                Assert(pc.Stats.Evictions >= 1, "second block insert must evict the first");
                Assert(pc.Stats.DiskWrites >= 1, "evicted block must be written to disk");
                Assert(pc.HotCount == 1, "RAM tier holds one block");

                // Fresh decoder: block 0 is only on disk, so it must come back as a disk hit.
                long diskBefore = pc.Stats.DiskHits;
                float[] fromDisk = new Decoder(w).Prefill(tokens, pc);
                AssertSameBits(fromDisk, cold, "prefill with disk hit");
                Assert(pc.Stats.DiskHits > diskBefore, "expected a disk-tier hit");
            }
            finally
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        private static void TestPrefixCorruptFile()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(64));
            int[] tokens = Prompt(40, 4);
            string dir = TempDir();
            try
            {
                long oneBlock = new KvBlock(0, 0, KvMode.Int8, Small, 16).Bytes;
                PrefixCache pc = new PrefixCache(Small, KvMode.Int8, 16, oneBlock, dir);
                float[] cold = new Decoder(w, KvMode.Int8).Prefill(tokens);
                new Decoder(w, KvMode.Int8).Prefill(tokens, pc);

                foreach (string f in System.IO.Directory.GetFiles(dir, "*.kvb"))
                {
                    byte[] data = System.IO.File.ReadAllBytes(f);
                    System.IO.File.WriteAllBytes(f, data.AsSpan(0, data.Length / 2).ToArray());
                }
                // Corrupt files must be rejected and the prompt recomputed, with identical output.
                float[] again = new Decoder(w, KvMode.Int8).Prefill(tokens, pc);
                AssertSameBits(again, cold, "prefill after corruption");
                Assert(pc.Stats.CorruptFiles >= 1, "truncated file must be counted as corrupt");
            }
            finally
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        private static void TestPrefixValidation()
        {
            AssertThrows<ArgumentException>(() =>
                new Decoder(DecoderWeights.Random(Small, new Random(65)), KvMode.Int8)
                    .Prefill(Prompt(20, 5), new PrefixCache(Small, KvMode.Fp32, 16, 1L << 30)),
                "mode mismatch rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => new PrefixCache(Small, KvMode.Fp32, 0, 1024), "zero block size rejected");
            AssertThrows<ArgumentException>(() =>
                new Decoder(DecoderWeights.Random(Small, new Random(66))).Prefill(Array.Empty<int>()), "empty prompt rejected");
            AssertThrows<ArgumentException>(() =>
                new PrefixCache(Small, KvMode.Fp32, 16, 1L << 30).Insert(new KvBlock(1, 2, KvMode.Int8, Small, 16)),
                "block of the wrong mode rejected");
        }

        private static void TestChainHash()
        {
            int[] a = { 1, 2, 3 }, b = { 3, 2, 1 };
            ulong ha = PrefixCache.ChainHash(PrefixCache.Seed, a);
            Assert(ha == PrefixCache.ChainHash(PrefixCache.Seed, a), "hash must be deterministic");
            Assert(ha != PrefixCache.ChainHash(PrefixCache.Seed, b), "token order must change the hash");
            Assert(ha != PrefixCache.ChainHash(12345UL, a), "parent hash must change the hash");
        }

        private static void TestPagedMatchesContiguous()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(71));
            int[] tokens = Prompt(40, 7);
            foreach (KvMode mode in new[] { KvMode.Fp32, KvMode.Int8 })
            {
                KvBlockPool pool = new KvBlockPool(Small, mode, 16, 8);
                PagedKvSequence seq = new PagedKvSequence(pool);
                Decoder paged = new Decoder(w, seq, blocks: 4);
                Decoder contiguous = new Decoder(w, mode, blocks: 4);
                for (int p = 0; p < tokens.Length; p++)
                {
                    float[] a = paged.Forward(tokens[p], p);
                    float[] b = contiguous.Forward(tokens[p], p);
                    AssertSameBits(a, b, $"{mode} paged vs contiguous at position {p}");
                }
                Assert(seq.Length == tokens.Length, "sequence length tracks positions");
                Assert(seq.Table.Count == 3, $"40 positions in 16-position blocks need 3 blocks, have {seq.Table.Count}");
            }
        }

        private static void TestPagedForkCopyOnWrite()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(72));
            int[] prompt = Prompt(40, 8);
            int x = 5, y = 9;
            int[] withX = new int[41], withY = new int[41];
            Array.Copy(prompt, withX, 40); withX[40] = x;
            Array.Copy(prompt, withY, 40); withY[40] = y;
            float[] coldX = new Decoder(w, KvMode.Fp32).Prefill(withX);
            float[] coldY = new Decoder(w, KvMode.Fp32).Prefill(withY);

            KvBlockPool pool = new KvBlockPool(Small, KvMode.Fp32, 16, 8);
            PagedKvSequence a = new PagedKvSequence(pool);
            Decoder decA = new Decoder(w, a, blocks: 4);
            decA.Prefill(prompt);
            Assert(a.Table.Count == 3, "prompt uses three blocks");
            int free0 = pool.FreeCount;

            PagedKvSequence b = a.Fork();
            Assert(pool.FreeCount == free0, "fork allocates no blocks");
            Assert(pool.RefCount(a.Table[2]) == 2, "partial block is shared after fork");
            Decoder decB = new Decoder(w, b, blocks: 4);
            float[] outB = decB.Forward(x, 40);
            Assert(pool.FreeCount == free0 - 1, "writing a shared block copies it (one new block)");
            Assert(pool.RefCount(a.Table[2]) == 1, "original block is no longer shared after the copy");

            float[] outA = decA.Forward(y, 40);
            AssertSameBits(outB, coldX, "forked branch B matches its own cold run");
            AssertSameBits(outA, coldY, "original branch A matches its own cold run");
            Assert(a.Table[0] == b.Table[0] && a.Table[1] == b.Table[1], "full prefix blocks stay shared");
        }

        private static void TestPagedPoolLifecycle()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(73));
            KvBlockPool pool = new KvBlockPool(Small, KvMode.Fp32, 16, 2);
            PagedKvSequence seq = new PagedKvSequence(pool);
            Decoder dec = new Decoder(w, seq, blocks: 1);
            int[] tokens = Prompt(40, 9);
            AssertThrows<InvalidOperationException>(() =>
            {
                for (int p = 0; p < tokens.Length; p++) dec.Forward(tokens[p], p);
            }, "third block must fail when the pool has two blocks");
            seq.Clear();
            Assert(pool.FreeCount == 2, $"clear returns all blocks, free {pool.FreeCount}");
            Assert(seq.Length == 0 && seq.Table.Count == 0, "cleared sequence is empty");

            AssertThrows<InvalidOperationException>(() => pool.Release(0), "releasing a free block must throw");
            int b = pool.Allocate();
            pool.Retain(b);
            pool.Release(b);
            Assert(pool.RefCount(b) == 1, "retain and release balance");
            pool.Release(b);
            Assert(pool.FreeCount == 2, "last release frees the block");
        }

        private static void TestPagedValidation()
        {
            ModelConfig other = new ModelConfig(Hidden: 64, Layers: 2, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 128, Vocab: 50, MaxSeq: 128);
            KvBlockPool pool = new KvBlockPool(other, KvMode.Fp32, 16, 4);
            AssertThrows<ArgumentException>(() => new Decoder(DecoderWeights.Random(Small, new Random(74)), new PagedKvSequence(pool)), "pool shape mismatch rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => new KvBlockPool(Small, KvMode.Fp32, 12, 4), "non power-of-two block size rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => new KvBlockPool(Small, KvMode.Fp32, 16, 0), "zero blocks rejected");
            PagedKvSequence seq = new PagedKvSequence(new KvBlockPool(Small, KvMode.Fp32, 16, 4));
            AssertThrows<InvalidOperationException>(() => { KvCache unused = new Decoder(DecoderWeights.Random(Small, new Random(76)), seq).Cache; }, "paged decoder has no contiguous cache");
            AssertThrows<NotSupportedException>(() => new Decoder(DecoderWeights.Random(Small, new Random(77)), seq).Prefill(Prompt(20, 10), new PrefixCache(Small, KvMode.Fp32, 16, 1L << 30)), "prefix cache not yet supported on paged decoders");
        }

        private static void TestPagedPrefixReuse()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(81));
            int[] tokens = Prompt(40, 11);
            foreach (KvMode mode in new[] { KvMode.Fp32, KvMode.Int8 })
            {
                float[] cold = new Decoder(w, mode).Prefill(tokens);
                KvBlockPool pool = new KvBlockPool(Small, mode, 16, 16);
                PagedPrefixIndex index = new PagedPrefixIndex(pool, 8);

                PagedKvSequence a = new PagedKvSequence(pool);
                AssertSameBits(new Decoder(w, a).Prefill(tokens, index), cold, $"{mode} first paged prefill");
                Assert(index.Count == 2, $"{mode}: two full blocks indexed, have {index.Count}");
                int freeAfterA = pool.FreeCount;

                PagedKvSequence b = new PagedKvSequence(pool);
                AssertSameBits(new Decoder(w, b).Prefill(tokens, index), cold, $"{mode} second paged prefill");
                Assert(a.Table[0] == b.Table[0] && a.Table[1] == b.Table[1], $"{mode}: full prefix blocks are the same physical blocks");
                Assert(b.Table[2] != a.Table[2], $"{mode}: partial block is private to each sequence");
                Assert(pool.RefCount(a.Table[0]) == 3, $"{mode}: block referenced by A, B, and the index");
                Assert(pool.FreeCount == freeAfterA - 1, $"{mode}: reuse allocates only the partial block");
                Assert(index.Stats.Hits == 2, $"{mode}: two index hits, have {index.Stats.Hits}");

                // Extended prompt shares the same two blocks and computes the rest.
                int[] longer = new int[50];
                Array.Copy(tokens, longer, 40);
                for (int i = 40; i < 50; i++) longer[i] = (i * 5) % Small.Vocab;
                PagedKvSequence c = new PagedKvSequence(pool);
                AssertSameBits(new Decoder(w, c).Prefill(longer, index), new Decoder(w, mode).Prefill(longer), $"{mode} extended paged prefill");
                Assert(c.Table[0] == a.Table[0], $"{mode}: extension shares the prefix block");

                // Divergent sequences keep their own blocks after the shared prefix.
                a.Clear(); b.Clear(); c.Clear();
                Assert(pool.FreeCount == pool.BlockCount - index.Count, $"{mode}: only index references remain after clearing sequences");
                index.Clear();
                Assert(pool.FreeCount == pool.BlockCount, $"{mode}: clearing the index returns every block");
            }
        }

        private static void TestPagedPrefixEviction()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(82));
            int[] tokens = Prompt(40, 12);
            KvBlockPool pool = new KvBlockPool(Small, KvMode.Fp32, 16, 16);
            PagedPrefixIndex index = new PagedPrefixIndex(pool, 1);
            PagedKvSequence a = new PagedKvSequence(pool);
            new Decoder(w, a).Prefill(tokens, index);
            Assert(index.Count == 1, "limit of one entry keeps one block");
            Assert(index.Stats.Evictions == 1, $"second block insert evicts the first, evictions {index.Stats.Evictions}");
            int evictedBlock = a.Table[0];
            a.Clear();
            // Only the surviving entry (block 1) holds a reference now; the evicted block 0 is free.
            Assert(pool.RefCount(evictedBlock) == 0, "evicted block with no sequence is returned to the pool");
            index.Clear();
            Assert(pool.FreeCount == pool.BlockCount, "all blocks free after clearing");
        }

        private static void TestPagedPrefixValidation()
        {
            KvBlockPool pool = new KvBlockPool(Small, KvMode.Fp32, 16, 8);
            PagedPrefixIndex index = new PagedPrefixIndex(pool, 4);
            AssertThrows<ArgumentException>(() => index.Insert(1, 2, new int[8], 0, new float[Small.Vocab]), "partial block tokens rejected");
            AssertThrows<InvalidOperationException>(() => new Decoder(DecoderWeights.Random(Small, new Random(83)))
                .Prefill(Prompt(20, 13), index), "contiguous decoder rejected for paged prefill");
            AssertThrows<ArgumentException>(() => new Decoder(DecoderWeights.Random(Small, new Random(84)), new PagedKvSequence(new KvBlockPool(Small, KvMode.Fp32, 16, 8)))
                .Prefill(Prompt(20, 14), index), "index from another pool rejected");
            PagedKvSequence seq = new PagedKvSequence(pool);
            AssertThrows<InvalidOperationException>(() => seq.AppendShared(0), "shared append needs a block boundary with table in sync");
        }

        private static void TestMatMatBits()
        {
            Random rng = new Random(91);
            QMat m = QMat.Random(300, 64, rng);
            float[] xs = RandomVec(rng, 5 * 64, 1f);
            float[] ys = new float[5 * 300];
            Kernels.MatMat(m, xs, 5, ys);
            var eng = new MatVecEngine(4);
            float[] yb = new float[5 * 300];
            eng.RunBatch(m, 5, xs, yb);
            for (int b = 0; b < 5; b++)
            {
                float[] single = new float[300];
                Kernels.MatVecAvx2(m, xs.AsSpan(b * 64, 64), single);
                for (int i = 0; i < 300; i++)
                {
                    Assert(BitConverter.SingleToInt32Bits(single[i]) == BitConverter.SingleToInt32Bits(ys[b * 300 + i]), $"MatMat element {b},{i} differs");
                    Assert(BitConverter.SingleToInt32Bits(single[i]) == BitConverter.SingleToInt32Bits(yb[b * 300 + i]), $"RunBatch element {b},{i} differs");
                }
            }
        }

        private static void TestBatchMatchesSingle()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(92));
            foreach (KvMode mode in new[] { KvMode.Fp32, KvMode.Int8 })
            {
                int[][] prompts = { Prompt(20, 21), Prompt(37, 22), Prompt(5, 23) };
                // Reference: each prompt decoded alone on its own paged sequence.
                float[][][] reference = new float[prompts.Length][][];
                for (int s = 0; s < prompts.Length; s++)
                {
                    KvBlockPool p1 = new KvBlockPool(Small, mode, 16, 8);
                    Decoder d = new Decoder(w, new PagedKvSequence(p1), blocks: 4);
                    reference[s] = new float[prompts[s].Length][];
                    for (int t = 0; t < prompts[s].Length; t++) reference[s][t] = (float[])d.Forward(prompts[s][t], t).Clone();   // Forward reuses its output buffer
                }

                // Batched: all three sequences on one pool, advanced together until the shortest prompt ends.
                KvBlockPool pool = new KvBlockPool(Small, mode, 16, 32);
                var seqs = new List<PagedKvSequence>();
                for (int s = 0; s < prompts.Length; s++) seqs.Add(new PagedKvSequence(pool));
                BatchDecoder bd = new BatchDecoder(w, seqs, blocks: 4);
                int steps = 0;
                for (int t = 0; t < prompts[2].Length; t++)
                {
                    int[] toks = { prompts[0][t], prompts[1][t], prompts[2][t] };
                    float[][] outs = bd.Forward(toks);
                    steps++;
                    for (int s = 0; s < prompts.Length; s++)
                        AssertSameBits(outs[s], reference[s][t], $"{mode} batch seq {s} at step {t}");
                }
                Assert(steps == 5, "ran to the shortest prompt");
                // Lengths differ and tracking follows each sequence.
                Assert(seqs[0].Length == 5 && seqs[1].Length == 5 && seqs[2].Length == 5, "lengths track steps");
            }
        }

        private static void TestBatchSharedPrefix()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(93));
            int[] prefix = Prompt(32, 24);
            int[] ta = new int[prefix.Length + 1], tb = new int[prefix.Length + 2];
            Array.Copy(prefix, ta, prefix.Length); ta[prefix.Length] = 3;
            Array.Copy(prefix, tb, prefix.Length); tb[prefix.Length] = 7; tb[prefix.Length + 1] = 11;

            KvBlockPool pool = new KvBlockPool(Small, KvMode.Fp32, 16, 16);
            PagedPrefixIndex index = new PagedPrefixIndex(pool, 8);
            PagedKvSequence seed = new PagedKvSequence(pool);
            new Decoder(w, seed, blocks: 4).Prefill(prefix, index);   // indexes the two full prefix blocks

            PagedKvSequence a = new PagedKvSequence(pool), b = new PagedKvSequence(pool);
            new Decoder(w, a, blocks: 4).Prefill(prefix, index);
            new Decoder(w, b, blocks: 4).Prefill(prefix, index);
            Assert(a.Table[0] == b.Table[0] && a.Table[1] == b.Table[1], "prefix blocks are shared");

            // Batched continuation: A and B have the same length; advance them with different tokens.
            BatchDecoder bd = new BatchDecoder(w, new[] { a, b }, blocks: 4);
            float[][] outs = bd.Forward(new[] { ta[prefix.Length], tb[prefix.Length] });
            float[] refA = new Decoder(w).Prefill(ta);
            float[] refB = new Decoder(w).Prefill(tb.AsSpan(0, prefix.Length + 1).ToArray());
            AssertSameBits(outs[0], refA, "batched A on shared prefix");
            AssertSameBits(outs[1], refB, "batched B on shared prefix");

            // Continue B alone (it is now one token longer than A): A and B have different lengths after this.
            float[][] step2 = bd.Forward(new[] { 0, tb[prefix.Length + 1] });
            Assert(a.Length == prefix.Length + 2 && b.Length == prefix.Length + 2, "both advanced one step");
            AssertSameBits(step2[1], new Decoder(w).Prefill(tb), "batched B second token");
        }

        private static void TestBatchValidation()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(94));
            KvBlockPool pool = new KvBlockPool(Small, KvMode.Fp32, 16, 8);
            KvBlockPool other = new KvBlockPool(Small, KvMode.Fp32, 16, 8);
            AssertThrows<ArgumentException>(() => new BatchDecoder(w, new[] { new PagedKvSequence(pool), new PagedKvSequence(other) }), "sequences on different pools rejected");
            AssertThrows<ArgumentException>(() => new BatchDecoder(w, Array.Empty<PagedKvSequence>()), "empty batch rejected");
            BatchDecoder bd = new BatchDecoder(w, new[] { new PagedKvSequence(pool) });
            AssertThrows<ArgumentException>(() => bd.Forward(new[] { 1, 2 }), "wrong token count rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => bd.Forward(new[] { 999 }), "token out of range rejected");
            AssertThrows<ArgumentException>(() => new BatchDecoder(DecoderWeights.Random(new ModelConfig(Hidden: 64, Layers: 2, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 128, Vocab: 50, MaxSeq: 128), new Random(95)), new[] { new PagedKvSequence(pool) }), "pool shape mismatch rejected");
        }

        private static void TestBatchedPrefillIdentical()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(101));
            int[] tokens = Prompt(40, 31);
            foreach (KvMode mode in new[] { KvMode.Fp32, KvMode.Int8 })
            {
                KvBlockPool refPool = new KvBlockPool(Small, mode, 16, 8);
                float[] reference = (float[])new Decoder(w, new PagedKvSequence(refPool), blocks: 4).Prefill(tokens).Clone();
                foreach (int chunk in new[] { 1, 7, 16, 64 })
                {
                    KvBlockPool pool = new KvBlockPool(Small, mode, 16, 8);
                    PagedKvSequence seq = new PagedKvSequence(pool);
                    Decoder dec = new Decoder(w, seq, blocks: 4);
                    float[] got = dec.PrefillBatched(tokens, chunk);
                    AssertSameBits(got, reference, $"{mode} batched prefill, chunk {chunk}");
                    Assert(dec.Length == tokens.Length && seq.Length == tokens.Length, $"{mode} chunk {chunk}: length");
                    Assert(seq.Table.Count == 3, $"{mode} chunk {chunk}: block count");

                    // Decoding continues correctly from the batched prefill.
                    float[] next = dec.Forward(5, tokens.Length);
                    float[] nextRef = new Decoder(w, new PagedKvSequence(new KvBlockPool(Small, mode, 16, 8)), blocks: 4).Prefill(tokens.Concat(new[] { 5 }).ToArray());
                    AssertSameBits(next, nextRef, $"{mode} decode after batched prefill, chunk {chunk}");
                }
            }
        }

        private static void TestBatchedPrefillPrefixIndex()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(102));
            int[] tokens = Prompt(40, 32);
            KvBlockPool pool = new KvBlockPool(Small, KvMode.Int8, 16, 16);
            PagedPrefixIndex index = new PagedPrefixIndex(pool, 8);
            float[] reference = (float[])new Decoder(w, KvMode.Int8).Prefill(tokens).Clone();

            PagedKvSequence a = new PagedKvSequence(pool);
            AssertSameBits(new Decoder(w, a, blocks: 4).PrefillBatched(tokens, 16, index), reference, "first batched prefill with index");
            Assert(index.Count == 2, $"two full blocks indexed, have {index.Count}");

            PagedKvSequence b = new PagedKvSequence(pool);
            AssertSameBits(new Decoder(w, b, blocks: 4).PrefillBatched(tokens, 7, index), reference, "second batched prefill reuses blocks");
            Assert(a.Table[0] == b.Table[0] && a.Table[1] == b.Table[1], "prefix blocks shared by reference");
            Assert(index.Stats.Hits == 2, $"two index hits, have {index.Stats.Hits}");

            // Mixed: a prompt that extends the prefix computes only the suffix, and its logits match a cold run.
            int[] longer = tokens.Concat(new[] { 3, 9, 27 }).ToArray();
            PagedKvSequence c = new PagedKvSequence(pool);
            AssertSameBits(new Decoder(w, c, blocks: 4).PrefillBatched(longer, 4, index),
                new Decoder(w, KvMode.Int8).Prefill(longer), "extended prompt, batched suffix");
        }

        private static void TestBatchedPrefillValidation()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(103));
            AssertThrows<InvalidOperationException>(() => new Decoder(w).PrefillBatched(Prompt(10, 33)), "contiguous decoder rejected");
            Decoder dec = new Decoder(w, new PagedKvSequence(new KvBlockPool(Small, KvMode.Fp32, 16, 8)));
            AssertThrows<ArgumentOutOfRangeException>(() => dec.PrefillBatched(Prompt(10, 34), 0), "zero chunk rejected");
            AssertThrows<ArgumentException>(() => dec.PrefillBatched(Array.Empty<int>()), "empty prompt rejected");
            AssertThrows<ArgumentException>(() => dec.PrefillBatched(Prompt(65, 35)), "prompt longer than context rejected");
            AssertThrows<ArgumentException>(() => dec.PrefillBatched(Prompt(10, 36), 4, new PagedPrefixIndex(new KvBlockPool(Small, KvMode.Fp32, 16, 8), 2)), "index from another pool rejected");
        }

        private static void TestBatchedAttentionLongContext()
        {
            // Small width, long context: prompt of 1100 positions, so one chunk spans the 1024-position attention boundary.
            ModelConfig longCfg = new ModelConfig(Hidden: 64, Layers: 1, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 16, Vocab: 8, MaxSeq: 2100);
            DecoderWeights w = DecoderWeights.Random(longCfg, new Random(111));
            Random r = new Random(112);
            int[] tokens = new int[1100];
            for (int i = 0; i < tokens.Length; i++) tokens[i] = r.Next(longCfg.Vocab);
            foreach (KvMode mode in new[] { KvMode.Fp32, KvMode.Int8 })
            {
                Decoder reference = new Decoder(w, new PagedKvSequence(new KvBlockPool(longCfg, mode, 256, 8)), blocks: 2);
                float[] refLogits = null!;
                for (int p = 0; p < tokens.Length; p++) refLogits = (float[])reference.Forward(tokens[p], p).Clone();

                Decoder batched = new Decoder(w, new PagedKvSequence(new KvBlockPool(longCfg, mode, 256, 8)), blocks: 2);
                float[] got = batched.PrefillBatched(tokens, chunk: 100);   // chunks 900-999, 1000-1099 cross 1024
                AssertSameBits(got, refLogits, $"{mode} batched attention across 1024 boundary");
                float[] next = (float[])batched.Forward(3, tokens.Length).Clone();
                AssertSameBits(next, ContinuationRef(w, longCfg, mode, tokens), $"{mode} decode after batched long prefill");
            }
        }

        private static float[] ContinuationRef(DecoderWeights w, ModelConfig cfg, KvMode mode, int[] tokens)
        {
            Decoder d = new Decoder(w, new PagedKvSequence(new KvBlockPool(cfg, mode, 256, 8)), blocks: 2);
            float[] last = null!;
            for (int p = 0; p <= tokens.Length; p++) last = (float[])d.Forward(p < tokens.Length ? tokens[p] : 3, p).Clone();
            return last;
        }

        private static void TestBatchedPrefillDiskTier()
        {
            DecoderWeights w = DecoderWeights.Random(Small, new Random(121));
            int[] tokens = Prompt(40, 41);
            foreach (KvMode mode in new[] { KvMode.Fp32, KvMode.Int8 })
            {
                float[] cold = (float[])new Decoder(w, mode).Prefill(tokens).Clone();
                string dir = TempDir();
                try
                {
                    KvBlockPool pool = new KvBlockPool(Small, mode, 16, 16);
                    PrefixCache spill = new PrefixCache(Small, mode, 16, 1, dir);   // one byte of RAM: every spill goes to disk
                    PagedPrefixIndex index = new PagedPrefixIndex(pool, 1, spill);  // RAM index holds one block

                    PagedKvSequence a = new PagedKvSequence(pool);
                    AssertSameBits(new Decoder(w, a, blocks: 4).PrefillBatched(tokens, 7, index), cold, $"{mode} first batched prefill");
                    Assert(spill.Stats.DiskWrites >= 1, $"{mode}: evicted block was written to disk");
                    a.Clear();

                    // Block 0 is only on disk; block 1 is in RAM when the prompt starts.
                    PagedKvSequence b = new PagedKvSequence(pool);
                    AssertSameBits(new Decoder(w, b, blocks: 4).PrefillBatched(tokens, 16, index), cold, $"{mode} batched prefill from disk tier");
                    Assert(index.Stats.DiskHits >= 1, $"{mode}: at least one disk-tier hit, have {index.Stats.DiskHits}");
                    Assert(pool.RefCount(b.Table[1]) == 2, $"{mode}: last restored block is shared by the sequence and the index");   // one-entry index: block 0 was evicted again

                    b.Clear();
                    index.Clear();
                    Assert(pool.FreeCount == pool.BlockCount, $"{mode}: every pool block returned");
                }
                finally
                {
                    System.IO.Directory.Delete(dir, recursive: true);
                }
            }
        }

        private static void TestConfigValidation()
        {
            AssertThrows<ArgumentException>(() => new ModelConfig(Hidden: 64, Layers: 1, Heads: 4, KvHeads: 3, HeadDim: 16, Ffn: 16, Vocab: 8, MaxSeq: 8).Validate(), "KvHeads must divide Heads");
            AssertThrows<ArgumentException>(() => new ModelConfig(Hidden: 60, Layers: 1, Heads: 4, KvHeads: 2, HeadDim: 15, Ffn: 16, Vocab: 8, MaxSeq: 8).Validate(), "HeadDim must be a multiple of 16");
            AssertThrows<ArgumentException>(() => new ModelConfig(Hidden: 65, Layers: 1, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 16, Vocab: 8, MaxSeq: 8).Validate(), "Hidden must equal Heads * HeadDim");
            AssertThrows<ArgumentException>(() => new ModelConfig(Hidden: 64, Layers: 1, Heads: 4, KvHeads: 2, HeadDim: 16, Ffn: 20, Vocab: 8, MaxSeq: 8).Validate(), "Ffn must be a multiple of 16");
            ModelConfig.Benchmark.Validate();
        }
    }
}
