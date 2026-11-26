using System;

namespace OPEN_LRD.Reasoning.Inference
{
    /// <summary>
    /// Seeded token sampler with temperature and nucleus (top-p) filtering. Uses an in-repo xoshiro256**
    /// generator, so output is reproducible on any runtime. Not thread-safe.
    /// </summary>
    public sealed class Sampler
    {
        /// <summary>Default temperature from the Reasoning Cache plan (D3).</summary>
        public const double DefaultTemperature = 0.7;

        /// <summary>Default nucleus probability from the Reasoning Cache plan (D3).</summary>
        public const double DefaultTopP = 0.8;

        private ulong s0, s1, s2, s3;

        /// <summary>Creates a sampler. The same seed always gives the same sequence.</summary>
        /// <param name="seed">Seed. Expanded with SplitMix64.</param>
        public Sampler(ulong seed)
        {
            ulong z = seed;
            s0 = SplitMix(ref z);
            s1 = SplitMix(ref z);
            s2 = SplitMix(ref z);
            s3 = SplitMix(ref z);
        }

        /// <summary>Next raw 64-bit output (xoshiro256**).</summary>
        public ulong NextUInt64()
        {
            ulong result = Rotl(s1 * 5, 7) * 9;
            ulong t = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = Rotl(s3, 45);
            return result;
        }

        /// <summary>Uniform double in [0, 1) with 53 bits of precision.</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        /// <summary>
        /// Samples a token from the logits. Probabilities are softmax(logits / temperature), restricted to the
        /// smallest set of highest-probability tokens whose mass reaches <paramref name="topP"/>, then renormalized.
        /// Ties in probability are broken by lower index, so the result is deterministic.
        /// </summary>
        /// <param name="logits">Unnormalized scores, at least one value, all finite.</param>
        /// <param name="temperature">Positive temperature.</param>
        /// <param name="topP">Nucleus mass in (0, 1].</param>
        /// <returns>The sampled token index.</returns>
        public int Sample(ReadOnlySpan<float> logits, double temperature = DefaultTemperature, double topP = DefaultTopP)
        {
            if (logits.IsEmpty) throw new ArgumentException("Logits must not be empty.", nameof(logits));
            if (!(temperature > 0) || !double.IsFinite(temperature)) throw new ArgumentOutOfRangeException(nameof(temperature));
            if (!(topP > 0) || topP > 1) throw new ArgumentOutOfRangeException(nameof(topP));

            int n = logits.Length;
            double max = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                if (!float.IsFinite(logits[i])) throw new ArgumentException("Logits must be finite.", nameof(logits));
                if (logits[i] > max) max = logits[i];
            }
            double[] prob = new double[n];
            double total = 0;
            for (int i = 0; i < n; i++)
            {
                prob[i] = Math.Exp((logits[i] - max) / temperature);
                total += prob[i];
            }
            for (int i = 0; i < n; i++) prob[i] /= total;

            int[] order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int c = prob[b].CompareTo(prob[a]);
                return c != 0 ? c : a.CompareTo(b);
            });

            double cumulative = 0;
            int keep = 0;
            while (keep < n)
            {
                cumulative += prob[order[keep]];
                keep++;
                if (cumulative >= topP) break;
            }

            double r = NextDouble() * cumulative;
            double acc = 0;
            for (int i = 0; i < keep; i++)
            {
                acc += prob[order[i]];
                if (r < acc) return order[i];
            }
            return order[keep - 1];
        }

        private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

        private static ulong SplitMix(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
