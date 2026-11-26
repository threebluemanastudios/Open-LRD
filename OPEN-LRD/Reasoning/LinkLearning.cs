using System;

namespace OPEN_LRD.Reasoning
{
    /// <summary>
    /// Pure math for link-weight learning along a path (BP-3). No graph access.
    /// Path confidence c = product of weights w_i, loss L = 1/2 (c - y)^2, and
    /// weights are parameterized as w = sigmoid(z).
    /// </summary>
    public static class LinkLearningMath
    {
        /// <summary>Smallest distance from 0 or 1 used when converting a weight back to a logit.</summary>
        public const double WeightClamp = 1e-9;

        /// <summary>Path confidence c = product of all weights. An empty path has confidence 1.</summary>
        /// <param name="weights">Edge weights, each finite and in [0, 1].</param>
        /// <returns>The product of the weights.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="weights"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a weight is not finite or lies outside [0, 1].</exception>
        public static double PathConfidence(double[] weights)
        {
            ValidateWeights(weights);
            double c = 1.0;
            for (int i = 0; i < weights.Length; i++)
            {
                c *= weights[i];
            }
            return c;
        }

        /// <summary>Loss L = 1/2 (c - y)^2.</summary>
        /// <param name="confidence">Path confidence c.</param>
        /// <param name="observedOutcome">Observed outcome y.</param>
        /// <returns>The squared-error loss.</returns>
        public static double Loss(double confidence, double observedOutcome)
        {
            double d = confidence - observedOutcome;
            return 0.5 * d * d;
        }

        /// <summary>
        /// Gradient of the loss with respect to each logit z_i, where w_i = sigmoid(z_i):
        /// dL/dz_i = (c - y) * P_i * w_i * (1 - w_i), with P_i = product of w_j for j != i.
        /// Prefix and suffix products give P_i without division, so zero weights are safe.
        /// </summary>
        /// <param name="weights">Edge weights, each finite and in [0, 1].</param>
        /// <param name="observedOutcome">Observed outcome y.</param>
        /// <returns>An array of gradients, one per weight.</returns>
        public static double[] LogitGradients(double[] weights, double observedOutcome)
        {
            ValidateWeights(weights);
            ValidateFinite(observedOutcome, nameof(observedOutcome));

            int n = weights.Length;
            double[] prefix = new double[n + 1];
            double[] suffix = new double[n + 1];
            prefix[0] = 1.0;
            for (int i = 0; i < n; i++)
            {
                prefix[i + 1] = prefix[i] * weights[i];
            }
            suffix[n] = 1.0;
            for (int i = n - 1; i >= 0; i--)
            {
                suffix[i] = suffix[i + 1] * weights[i];
            }

            double c = prefix[n];
            double residual = c - observedOutcome;
            double[] gradients = new double[n];
            for (int i = 0; i < n; i++)
            {
                double others = prefix[i] * suffix[i + 1];
                double w = weights[i];
                gradients[i] = residual * others * w * (1.0 - w);
            }
            return gradients;
        }

        /// <summary>Logistic function sigmoid(z) = 1 / (1 + exp(-z)).</summary>
        /// <param name="z">Unconstrained logit.</param>
        /// <returns>A weight in (0, 1).</returns>
        public static double Sigmoid(double z)
        {
            ValidateFinite(z, nameof(z));
            if (z >= 0)
            {
                return 1.0 / (1.0 + Math.Exp(-z));
            }
            double e = Math.Exp(z);
            return e / (1.0 + e);
        }

        /// <summary>Inverse of the sigmoid, after clamping the weight to [1e-9, 1 - 1e-9].</summary>
        /// <param name="weight">Weight in [0, 1].</param>
        /// <returns>The logit z such that sigmoid(z) is approximately <paramref name="weight"/>.</returns>
        public static double LogitFromWeight(double weight)
        {
            ValidateFinite(weight, nameof(weight));
            double w = Math.Clamp(weight, WeightClamp, 1.0 - WeightClamp);
            return Math.Log(w / (1.0 - w));
        }

        private static void ValidateWeights(double[] weights)
        {
            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }
            for (int i = 0; i < weights.Length; i++)
            {
                double w = weights[i];
                if (!double.IsFinite(w) || w < 0.0 || w > 1.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(weights), $"Weight {i} must be finite and in [0, 1], got {w}.");
                }
            }
        }

        private static void ValidateFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, $"{name} must be finite, got {value}.");
            }
        }
    }
}
