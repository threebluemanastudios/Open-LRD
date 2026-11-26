using System;
using OPEN_LRD.Reasoning;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class LinkLearningTests
    {
        public static void RunAll()
        {
            Run("Path confidence product", TestPathConfidence);
            Run("Finite differences, path lengths 1-3", TestGradientFiniteDifferences);
            Run("Finite differences near 0 and 1", TestGradientNearBoundaries);
            Run("Prefix/suffix handles zero weights", TestGradientWithZeroWeight);
            Run("Sigmoid and logit round-trip", TestSigmoidLogitRoundTrip);
            Run("Logit clamps boundary weights", TestLogitClamp);
            Run("Link learning math validation", TestValidation);
        }

        private static void TestPathConfidence()
        {
            AssertEqual(LinkLearningMath.PathConfidence(new[] { 0.5, 0.5, 0.8 }), 0.2, "product of weights", 1e-12);
            AssertEqual(LinkLearningMath.PathConfidence(Array.Empty<double>()), 1.0, "empty path confidence", 1e-12);
            AssertEqual(LinkLearningMath.Loss(0.2, 1.0), 0.5 * 0.8 * 0.8, "loss", 1e-12);
        }

        private static void TestGradientFiniteDifferences()
        {
            double[][] logitSets =
            {
                new[] { 0.3 },
                new[] { -0.7, 1.2 },
                new[] { 0.4, -1.1, 2.0 },
            };
            foreach (double[] z in logitSets)
            {
                foreach (double y in new[] { 0.0, 0.35, 1.0 })
                {
                    CheckGradient(z, y, $"len {z.Length}, y {y}");
                }
            }
        }

        private static void TestGradientNearBoundaries()
        {
            // z = +/-9 gives weights near 1 and near 0 (about 1.2e-4 and 0.9999).
            CheckGradient(new[] { 9.0, 9.0 }, 0.5, "near 1");
            CheckGradient(new[] { -9.0, 9.0, 0.5 }, 0.5, "near 0 and 1 mixed");
            CheckGradient(new[] { -9.0 }, 1.0, "near 0 single");
        }

        private static void TestGradientWithZeroWeight()
        {
            double[] g = LinkLearningMath.LogitGradients(new[] { 0.0, 0.5 }, 1.0);
            // c = 0, residual = -1. Gradient for w0 uses P_0 = 0.5, w0(1-w0) = 0 -> 0.
            // Gradient for w1 uses P_1 = 0 -> 0.
            AssertEqual(g[0], 0.0, "zero weight gradient 0", 1e-12);
            AssertEqual(g[1], 0.0, "gradient through zero weight", 1e-12);
        }

        private static void TestSigmoidLogitRoundTrip()
        {
            foreach (double z in new[] { -5.0, -0.5, 0.0, 0.5, 5.0 })
            {
                double w = LinkLearningMath.Sigmoid(z);
                AssertEqual(LinkLearningMath.LogitFromWeight(w), z, $"logit round-trip at {z}", 1e-9);
            }
            AssertEqual(LinkLearningMath.Sigmoid(0.0), 0.5, "sigmoid(0)", 1e-15);
        }

        private static void TestLogitClamp()
        {
            double lo = LinkLearningMath.LogitFromWeight(0.0);
            double hi = LinkLearningMath.LogitFromWeight(1.0);
            Assert(double.IsFinite(lo) && double.IsFinite(hi), "clamped logits must be finite");
            AssertEqual(lo, Math.Log(1e-9 / (1 - 1e-9)), "lower clamp", 1e-9);
            // 1 - (1 - 1e-9) loses about 7 digits in double, so symmetry holds only to ~1e-6.
            AssertEqual(hi, -lo, "upper clamp symmetric", 1e-6);
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => LinkLearningMath.PathConfidence(new[] { 1.5 }), "weight above 1 rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => LinkLearningMath.PathConfidence(new[] { -0.1 }), "negative weight rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => LinkLearningMath.PathConfidence(new[] { double.NaN }), "NaN weight rejected");
            AssertThrows<ArgumentNullException>(() => LinkLearningMath.PathConfidence(null!), "null weights rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => LinkLearningMath.LogitGradients(new[] { 0.5 }, double.PositiveInfinity), "infinite outcome rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => LinkLearningMath.Sigmoid(double.NaN), "NaN logit rejected");
        }

        // Compares analytic dL/dz_i with central differences of L(z) = 1/2 (prod sigmoid(z_i) - y)^2.
        private static void CheckGradient(double[] z, double y, string label)
        {
            double[] w = new double[z.Length];
            for (int i = 0; i < z.Length; i++)
            {
                w[i] = LinkLearningMath.Sigmoid(z[i]);
            }
            double[] analytic = LinkLearningMath.LogitGradients(w, y);

            const double h = 1e-6;
            for (int i = 0; i < z.Length; i++)
            {
                double[] zp = (double[])z.Clone();
                double[] zm = (double[])z.Clone();
                zp[i] += h;
                zm[i] -= h;
                double lp = LossAt(zp, y);
                double lm = LossAt(zm, y);
                double numeric = (lp - lm) / (2 * h);
                double tolerance = 1e-7 * (1.0 + Math.Abs(numeric));
                if (Math.Abs(analytic[i] - numeric) > tolerance)
                {
                    throw new InvalidOperationException(
                        $"{label}: gradient {i} analytic {analytic[i]:E6} vs numeric {numeric:E6}");
                }
            }
        }

        private static double LossAt(double[] z, double y)
        {
            double[] w = new double[z.Length];
            for (int i = 0; i < z.Length; i++)
            {
                w[i] = LinkLearningMath.Sigmoid(z[i]);
            }
            return LinkLearningMath.Loss(LinkLearningMath.PathConfidence(w), y);
        }
    }
}
