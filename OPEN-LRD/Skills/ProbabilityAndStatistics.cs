using System;
using System.Collections.Generic;

namespace OPEN_LRD.Skills
{
    internal enum TailType
    {
        TwoSided,
        Upper,
        Lower
    }

    internal sealed record ConfidenceInterval(double Estimate, double Lower, double Upper, double ConfidenceLevel);

    internal sealed record HypothesisTestResult(
        string Name,
        double Statistic,
        double PValue,
        double CriticalValue,
        double DegreesOfFreedom,
        bool RejectNull);

    internal sealed record RegressionResult(
        double[] Coefficients,
        double RSquared,
        double AdjustedRSquared,
        double StandardError,
        double[,] Covariance);

    internal sealed record AnovaResult(
        double BetweenVariance,
        double WithinVariance,
        double FStatistic,
        double PValue,
        bool RejectNull);

    internal static class ProbabilityAndStatistics
    {
        private const double SqrtTwo = 1.4142135623730950488;
        private const double SqrtTwoPi = 2.5066282746310005024;
        private const double LogSqrtTwoPi = 0.9189385332046727418;
        private const double Epsilon = 1e-14;
        private const int MaximumIterations = 10000;
        private static readonly double[] LanczosCoefficients =
        {
            676.5203681218851,
            -1259.1392167224028,
            771.32342877765313,
            -176.61502916214059,
            12.507343278686905,
            -0.13857109526572012,
            9.9843695780195716e-6,
            1.5056327351493116e-7
        };

        /// <summary>
        /// Evaluates the error function at value.
        /// </summary>
        public static double Erf(double value)
        {
            EnsureFinite(value, nameof(value));
            if (value == 0.0)
            {
                return 0.0;
            }
            return value > 0.0 ? 1.0 - ErfcPositive(value) : ErfcPositive(-value) - 1.0;
        }

        /// <summary>
        /// Evaluates the complementary error function at value.
        /// </summary>
        public static double Erfc(double value)
        {
            EnsureFinite(value, nameof(value));
            return value >= 0.0 ? ErfcPositive(value) : 2.0 - ErfcPositive(-value);
        }

        /// <summary>
        /// Evaluates the natural logarithm of the gamma function at value.
        /// </summary>
        public static double LogGamma(double value)
        {
            EnsureFinite(value, nameof(value));
            if (value <= 0.0 && value == Math.Truncate(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "LogGamma is undefined at non-positive integers.");
            }
            if (value < 0.5)
            {
                return Math.Log(Math.PI) - Math.Log(Math.Abs(Math.Sin(Math.PI * value))) - LogGamma(1.0 - value);
            }

            double shifted = value - 1.0;
            double sum = 0.99999999999980993;
            for (int index = 0; index < LanczosCoefficients.Length; index++)
            {
                sum += LanczosCoefficients[index] / (shifted + index + 1.0);
            }
            double t = shifted + LanczosCoefficients.Length - 0.5;
            return LogSqrtTwoPi + ((shifted + 0.5) * Math.Log(t)) - t + Math.Log(sum);
        }

        /// <summary>
        /// Evaluates the regularized lower incomplete gamma function P(a, x).
        /// </summary>
        public static double RegularizedGammaP(double a, double x)
        {
            ValidateGammaArguments(a, x);
            if (x == 0.0)
            {
                return 0.0;
            }
            if (x >= a + 1.0)
            {
                return ClampProbability(1.0 - GammaContinuedFractionQ(a, x));
            }

            double term = 1.0 / a;
            double sum = term;
            double denominator = a;
            for (int iteration = 1; iteration <= MaximumIterations; iteration++)
            {
                denominator += 1.0;
                term *= x / denominator;
                sum += term;
                if (Math.Abs(term) <= Math.Abs(sum) * Epsilon)
                {
                    return ClampProbability(sum * Math.Exp((-x) + (a * Math.Log(x)) - LogGamma(a)));
                }
            }
            throw new InvalidOperationException("Regularized gamma series did not converge.");
        }

        /// <summary>
        /// Evaluates the regularized upper incomplete gamma function Q(a, x).
        /// </summary>
        public static double RegularizedGammaQ(double a, double x)
        {
            ValidateGammaArguments(a, x);
            if (x == 0.0)
            {
                return 1.0;
            }
            if (x < a + 1.0)
            {
                return ClampProbability(1.0 - RegularizedGammaP(a, x));
            }
            return ClampProbability(GammaContinuedFractionQ(a, x));
        }

        /// <summary>
        /// Evaluates the incomplete beta function with parameters a and b at x.
        /// </summary>
        public static double IncompleteBeta(double a, double b, double x)
        {
            ValidateBetaArguments(a, b, x);
            if (x == 0.0 || x == 1.0)
            {
                return x == 0.0 ? 0.0 : Math.Exp(LogBeta(a, b));
            }
            return Math.Exp(LogBeta(a, b)) * RegularizedIncompleteBeta(a, b, x);
        }

        /// <summary>
        /// Evaluates the regularized incomplete beta function I(x; a, b).
        /// </summary>
        public static double RegularizedIncompleteBeta(double a, double b, double x)
        {
            ValidateBetaArguments(a, b, x);
            if (x == 0.0)
            {
                return 0.0;
            }
            if (x == 1.0)
            {
                return 1.0;
            }
            double front = Math.Exp((a * Math.Log(x)) + (b * LogOnePlus(-x)) - LogBeta(a, b));
            if (x < (a + 1.0) / (a + b + 2.0))
            {
                return ClampProbability(front * BetaContinuedFraction(a, b, x) / a);
            }
            return ClampProbability(1.0 - (front * BetaContinuedFraction(b, a, 1.0 - x) / b));
        }
        /// <summary>
        /// Evaluates the normal probability density at x with mean and standardDeviation.
        /// </summary>

        public static double NormalPdf(double x, double mean, double standardDeviation)
        {
            EnsureFinite(x, nameof(x));
            EnsureFinite(mean, nameof(mean));
            EnsurePositiveFinite(standardDeviation, nameof(standardDeviation));
            double z = (x - mean) / standardDeviation;
            return Math.Exp(-0.5 * z * z) / (standardDeviation * SqrtTwoPi);
        }

        /// <summary>
        /// Evaluates the normal cumulative distribution at x with mean and standardDeviation.
        /// </summary>
        public static double NormalCdf(double x, double mean, double standardDeviation)
        {
            EnsureFinite(x, nameof(x));
            EnsureFinite(mean, nameof(mean));
            EnsurePositiveFinite(standardDeviation, nameof(standardDeviation));
            double z = (x - mean) / standardDeviation;
            return z < 0.0
                ? 0.5 * ErfcPositive(-z / SqrtTwo)
                : 1.0 - (0.5 * ErfcPositive(z / SqrtTwo));
        }

        /// <summary>
        /// Evaluates the normal quantile (inverse CDF) at probability with mean and standardDeviation.
        /// </summary>
        public static double NormalQuantile(double probability, double mean, double standardDeviation)
        {
            EnsureProbability(probability, nameof(probability));
            EnsureFinite(mean, nameof(mean));
            EnsurePositiveFinite(standardDeviation, nameof(standardDeviation));
            if (probability == 0.0)
            {
                return double.NegativeInfinity;
            }
            if (probability == 1.0)
            {
                return double.PositiveInfinity;
            }
            return mean + (standardDeviation * StandardNormalQuantile(probability));
        }

        /// <summary>
        /// Draws a normally distributed sample with mean and standardDeviation using the Box-Muller transform.
        /// </summary>
        public static double NormalSample(Random random, double mean, double standardDeviation)
        {
            EnsureRandom(random);
            EnsureFinite(mean, nameof(mean));
            EnsurePositiveFinite(standardDeviation, nameof(standardDeviation));
            double first = Math.Max(random.NextDouble(), double.Epsilon);
            double second = random.NextDouble();
            double standardNormal = Math.Sqrt(-2.0 * Math.Log(first)) * Math.Cos(2.0 * Math.PI * second);
            return mean + (standardDeviation * standardNormal);
        }

        /// <summary>
        /// Computes a z-based confidence interval for the mean with known standardDeviation at confidenceLevel.
        /// </summary>
        public static ConfidenceInterval NormalConfidenceInterval(double sampleMean, double standardDeviation, int sampleSize, double confidenceLevel)
        {
            EnsureFinite(sampleMean, nameof(sampleMean));
            EnsurePositiveFinite(standardDeviation, nameof(standardDeviation));
            EnsureSampleSize(sampleSize);
            EnsureConfidenceLevel(confidenceLevel);
            double critical = StandardNormalQuantile(0.5 + (confidenceLevel / 2.0));
            double margin = critical * standardDeviation / Math.Sqrt(sampleSize);
            return new ConfidenceInterval(sampleMean, sampleMean - margin, sampleMean + margin, confidenceLevel);
        }

        /// <summary>
        /// Evaluates the binomial probability mass of exactly successes out of trials at probability.
        /// </summary>
        public static double BinomialPmf(int successes, int trials, double probability)
        {
            ValidateBinomial(successes, trials, probability);
            if (probability == 0.0)
            {
                return successes == 0 ? 1.0 : 0.0;
            }
            if (probability == 1.0)
            {
                return successes == trials ? 1.0 : 0.0;
            }
            return Math.Exp(LogChoose(trials, successes)
                + (successes * Math.Log(probability))
                + ((trials - successes) * LogOnePlus(-probability)));
        }

        /// <summary>
        /// Evaluates the binomial CDF: the probability of at most successes out of trials at probability.
        /// </summary>
        public static double BinomialCdf(int successes, int trials, double probability)
        {
            ValidateBinomial(successes, trials, probability);
            if (successes == trials || probability == 0.0)
            {
                return 1.0;
            }
            if (probability == 1.0)
            {
                return 0.0;
            }
            return RegularizedIncompleteBeta(trials - successes, successes + 1.0, 1.0 - probability);
        }

        /// <summary>
        /// Draws a binomial sample: the number of successes in trials Bernoulli trials at probability.
        /// </summary>
        public static int BinomialSample(Random random, int trials, double probability)
        {
            EnsureRandom(random);
            ValidateBinomial(0, trials, probability);
            int successes = 0;
            for (int trial = 0; trial < trials; trial++)
            {
                if (random.NextDouble() < probability)
                {
                    successes++;
                }
            }
            return successes;
        }

        /// <summary>
        /// Evaluates the Poisson probability mass at count with rate.
        /// </summary>
        public static double PoissonPmf(int count, double rate)
        {
            EnsurePositiveFinite(rate, nameof(rate));
            if (count < 0)
            {
                return 0.0;
            }
            return Math.Exp((count * Math.Log(rate)) - rate - LogGamma(count + 1.0));
        }

        /// <summary>
        /// Evaluates the Poisson CDF: the probability of at most count events at rate.
        /// </summary>
        public static double PoissonCdf(int count, double rate)
        {
            EnsurePositiveFinite(rate, nameof(rate));
            return count < 0 ? 0.0 : RegularizedGammaQ(count + 1.0, rate);
        }

        /// <summary>
        /// Draws a Poisson sample with rate, using multiplication for small rates and transformed rejection for larger rates.
        /// </summary>
        public static int PoissonSample(Random random, double rate)
        {
            EnsureRandom(random);
            EnsurePositiveFinite(rate, nameof(rate));
            if (rate < 10.0)
            {
                double threshold = Math.Exp(-rate);
                double product = 1.0;
                int count = 0;
                do
                {
                    count++;
                    product *= Math.Max(random.NextDouble(), double.Epsilon);
                }
                while (product > threshold);
                return count - 1;
            }

            double b = 0.931 + (2.53 * Math.Sqrt(rate));
            double a = -0.059 + (0.02483 * b);
            double inverseAlpha = 1.1239 + (1.1328 / (b - 3.4));
            double squeeze = 0.9277 - (3.6224 / (b - 2.0));
            while (true)
            {
                double u = random.NextDouble() - 0.5;
                double v = random.NextDouble();
                double us = 0.5 - Math.Abs(u);
                int candidate = (int)Math.Floor(((2.0 * a / us) + b) * u + rate + 0.43);
                if (us >= 0.07 && v <= squeeze && candidate >= 0)
                {
                    return candidate;
                }
                if (candidate < 0 || (us < 0.013 && v > us))
                {
                    continue;
                }
                double lhs = Math.Log(v * inverseAlpha / (a / (us * us) + b));
                double rhs = -rate + (candidate * Math.Log(rate)) - LogGamma(candidate + 1.0);
                if (lhs <= rhs)
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// Evaluates the exponential probability density at x with rate.
        /// </summary>
        public static double ExponentialPdf(double x, double rate)
        {
            EnsureFinite(x, nameof(x));
            EnsurePositiveFinite(rate, nameof(rate));
            return x < 0.0 ? 0.0 : rate * Math.Exp(-rate * x);
        }

        /// <summary>
        /// Evaluates the exponential cumulative distribution at x with rate.
        /// </summary>
        public static double ExponentialCdf(double x, double rate)
        {
            EnsureFinite(x, nameof(x));
            EnsurePositiveFinite(rate, nameof(rate));
            return x <= 0.0 ? 0.0 : -ExpMinusOne(-rate * x);
        }

        /// <summary>
        /// Evaluates the exponential quantile (inverse CDF) at probability with rate.
        /// </summary>
        public static double ExponentialQuantile(double probability, double rate)
        {
            EnsureProbability(probability, nameof(probability));
            EnsurePositiveFinite(rate, nameof(rate));
            return probability == 1.0 ? double.PositiveInfinity : -LogOnePlus(-probability) / rate;
        }

        /// <summary>
        /// Draws an exponentially distributed sample with rate using inverse transform sampling.
        /// </summary>
        public static double ExponentialSample(Random random, double rate)
        {
            EnsureRandom(random);
            EnsurePositiveFinite(rate, nameof(rate));
            return -LogOnePlus(-random.NextDouble()) / rate;
        }

        /// <summary>
        /// Evaluates the continuous uniform probability density at x over [lower, upper].
        /// </summary>
        public static double UniformPdf(double x, double lower, double upper)
        {
            EnsureFinite(x, nameof(x));
            ValidateUniform(lower, upper);
            return x < lower || x > upper ? 0.0 : 1.0 / (upper - lower);
        }

        /// <summary>
        /// Evaluates the continuous uniform cumulative distribution at x over [lower, upper].
        /// </summary>
        public static double UniformCdf(double x, double lower, double upper)
        {
            EnsureFinite(x, nameof(x));
            ValidateUniform(lower, upper);
            return x <= lower ? 0.0 : x >= upper ? 1.0 : (x - lower) / (upper - lower);
        }

        /// <summary>
        /// Evaluates the beta probability density at x with alpha and beta.
        /// </summary>
        public static double BetaPdf(double x, double alpha, double beta)
        {
            EnsurePositiveFinite(alpha, nameof(alpha));
            EnsurePositiveFinite(beta, nameof(beta));
            EnsureFinite(x, nameof(x));
            if (x < 0.0 || x > 1.0)
            {
                return 0.0;
            }
            if (x == 0.0)
            {
                return alpha < 1.0 ? double.PositiveInfinity : alpha == 1.0 ? beta : 0.0;
            }
            if (x == 1.0)
            {
                return beta < 1.0 ? double.PositiveInfinity : beta == 1.0 ? alpha : 0.0;
            }
            return Math.Exp(((alpha - 1.0) * Math.Log(x))
                + ((beta - 1.0) * LogOnePlus(-x))
                - LogBeta(alpha, beta));
        }
        /// <summary>
        /// Evaluates the beta CDF at x with alpha and beta via the regularized incomplete beta function.
        /// </summary>

        public static double BetaCdf(double x, double alpha, double beta)
        {
            EnsurePositiveFinite(alpha, nameof(alpha));
            EnsurePositiveFinite(beta, nameof(beta));
            EnsureFinite(x, nameof(x));
            return x <= 0.0 ? 0.0 : x >= 1.0 ? 1.0 : RegularizedIncompleteBeta(alpha, beta, x);
        }
        /// <summary>
        /// Evaluates the gamma PDF at x with shape and rate.
        /// </summary>

        public static double GammaPdf(double x, double shape, double rate)
        {
            EnsurePositiveFinite(shape, nameof(shape));
            EnsurePositiveFinite(rate, nameof(rate));
            EnsureFinite(x, nameof(x));
            if (x < 0.0)
            {
                return 0.0;
            }
            if (x == 0.0)
            {
                return shape < 1.0 ? double.PositiveInfinity : shape == 1.0 ? rate : 0.0;
            }
            return Math.Exp((shape * Math.Log(rate))
                + ((shape - 1.0) * Math.Log(x))
                - (rate * x)
                - LogGamma(shape));
        }
        /// <summary>
        /// Evaluates the gamma CDF at x with shape and rate via the regularized lower incomplete gamma function.
        /// </summary>

        public static double GammaCdf(double x, double shape, double rate)
        {
            EnsurePositiveFinite(shape, nameof(shape));
            EnsurePositiveFinite(rate, nameof(rate));
            EnsureFinite(x, nameof(x));
            if (x <= 0.0)
            {
                return 0.0;
            }
            return x > double.MaxValue / rate ? 1.0 : RegularizedGammaP(shape, rate * x);
        }
        /// <summary>
        /// Evaluates the Student t PDF at x with degreesOfFreedom.
        /// </summary>

        public static double StudentTPdf(double x, double degreesOfFreedom)
        {
            EnsureFinite(x, nameof(x));
            EnsurePositiveFinite(degreesOfFreedom, nameof(degreesOfFreedom));
            return Math.Exp(LogGamma((degreesOfFreedom + 1.0) / 2.0)
                - LogGamma(degreesOfFreedom / 2.0)
                - 0.5 * (Math.Log(degreesOfFreedom) + Math.Log(Math.PI))
                - ((degreesOfFreedom + 1.0) / 2.0) * LogOnePlus(x * x / degreesOfFreedom));
        }

        /// <summary>
        /// Evaluates the Student t cumulative distribution at x with degreesOfFreedom.
        /// </summary>
        public static double StudentTCdf(double x, double degreesOfFreedom)
        {
            if (double.IsNaN(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Value must not be NaN.");
            }
            EnsurePositiveFinite(degreesOfFreedom, nameof(degreesOfFreedom));
            if (double.IsNegativeInfinity(x))
            {
                return 0.0;
            }
            if (double.IsPositiveInfinity(x))
            {
                return 1.0;
            }
            double beta = RegularizedIncompleteBeta(
                degreesOfFreedom / 2.0,
                0.5,
                degreesOfFreedom / (degreesOfFreedom + (x * x)));
            return x >= 0.0 ? 1.0 - (0.5 * beta) : 0.5 * beta;
        }

        /// <summary>
        /// Evaluates the chi-square probability density at x with degreesOfFreedom.
        /// </summary>
        public static double ChiSquarePdf(double x, double degreesOfFreedom)
        {
            EnsurePositiveFinite(degreesOfFreedom, nameof(degreesOfFreedom));
            EnsureFinite(x, nameof(x));
            return x < 0.0 ? 0.0 : GammaPdf(x, degreesOfFreedom / 2.0, 0.5);
        }

        /// <summary>
        /// Evaluates the chi-square cumulative distribution at x with degreesOfFreedom via the regularized lower incomplete gamma function.
        /// </summary>
        public static double ChiSquareCdf(double x, double degreesOfFreedom)
        {
            EnsurePositiveFinite(degreesOfFreedom, nameof(degreesOfFreedom));
            EnsureFinite(x, nameof(x));
            return x <= 0.0 ? 0.0 : RegularizedGammaP(degreesOfFreedom / 2.0, x / 2.0);
        }

        /// <summary>
        /// Evaluates the F distribution probability density at x with the numerator and denominator degrees of freedom.
        /// </summary>
        public static double FPdf(double x, double numeratorDegreesOfFreedom, double denominatorDegreesOfFreedom)
        {
            EnsurePositiveFinite(numeratorDegreesOfFreedom, nameof(numeratorDegreesOfFreedom));
            EnsurePositiveFinite(denominatorDegreesOfFreedom, nameof(denominatorDegreesOfFreedom));
            EnsureFinite(x, nameof(x));
            if (x < 0.0)
            {
                return 0.0;
            }
            double d1 = numeratorDegreesOfFreedom;
            double d2 = denominatorDegreesOfFreedom;
            if (x == 0.0)
            {
                return d1 < 2.0 ? double.PositiveInfinity : d1 == 2.0 ? 1.0 : 0.0;
            }
            double logValue = (d1 / 2.0) * Math.Log(d1 / d2)
                + ((d1 / 2.0) - 1.0) * Math.Log(x)
                - ((d1 + d2) / 2.0) * LogOnePlus((d1 / d2) * x)
                - LogBeta(d1 / 2.0, d2 / 2.0);
            return Math.Exp(logValue);
        }

        /// <summary>
        /// Evaluates the F distribution cumulative distribution at x via the regularized incomplete beta function.
        /// </summary>
        public static double FCdf(double x, double numeratorDegreesOfFreedom, double denominatorDegreesOfFreedom)
        {
            EnsurePositiveFinite(numeratorDegreesOfFreedom, nameof(numeratorDegreesOfFreedom));
            EnsurePositiveFinite(denominatorDegreesOfFreedom, nameof(denominatorDegreesOfFreedom));
            EnsureFinite(x, nameof(x));
            if (x <= 0.0)
            {
                return 0.0;
            }
            double d1 = numeratorDegreesOfFreedom;
            double d2 = denominatorDegreesOfFreedom;
            double transformed = d1 * x / (d1 * x + d2);
            return RegularizedIncompleteBeta(d1 / 2.0, d2 / 2.0, transformed);
        }
        /// <summary>
        /// Computes a t-based confidence interval for the mean of sample at confidenceLevel.
        /// </summary>

        public static ConfidenceInterval MeanConfidenceInterval(double[] sample, double confidenceLevel)
        {
            EnsureSample(sample, 2, nameof(sample));
            EnsureConfidenceLevel(confidenceLevel);
            double mean = Mean(sample);
            double standardError = SampleStandardDeviation(sample) / Math.Sqrt(sample.Length);
            double critical = StudentTQuantile(0.5 + confidenceLevel / 2.0, sample.Length - 1);
            return new ConfidenceInterval(mean, mean - critical * standardError, mean + critical * standardError, confidenceLevel);
        }

        /// <summary>
        /// Computes a Wilson score confidence interval for the proportion of successes out of trials at confidenceLevel.
        /// </summary>
        public static ConfidenceInterval ProportionConfidenceInterval(int successes, int trials, double confidenceLevel)
        {
            if (trials < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(trials));
            }
            ValidateBinomial(successes, trials, trials == 0 ? 0.0 : Math.Clamp((double)successes / trials, 0.0, 1.0));
            EnsureConfidenceLevel(confidenceLevel);
            double proportion = (double)successes / trials;
            double z = StandardNormalQuantile(0.5 + confidenceLevel / 2.0);
            double zSquared = z * z;
            double denominator = 1.0 + zSquared / trials;
            double center = (proportion + zSquared / (2.0 * trials)) / denominator;
            double margin = z * Math.Sqrt((proportion * (1.0 - proportion) / trials) + zSquared / (4.0 * trials * trials)) / denominator;
            return new ConfidenceInterval(proportion, Math.Max(0.0, center - margin), Math.Min(1.0, center + margin), confidenceLevel);
        }

        /// <summary>
        /// Computes a Welch t-based confidence interval for the difference in means of first and second at confidenceLevel.
        /// </summary>
        public static ConfidenceInterval DifferenceInMeansConfidenceInterval(double[] first, double[] second, double confidenceLevel)
        {
            EnsureSample(first, 2, nameof(first));
            EnsureSample(second, 2, nameof(second));
            EnsureConfidenceLevel(confidenceLevel);
            double firstVariance = SampleVariance(first);
            double secondVariance = SampleVariance(second);
            double firstTerm = firstVariance / first.Length;
            double secondTerm = secondVariance / second.Length;
            double standardError = Math.Sqrt(firstTerm + secondTerm);
            double degrees = Math.Pow(firstTerm + secondTerm, 2.0)
                / (Math.Pow(firstTerm, 2.0) / (first.Length - 1.0) + Math.Pow(secondTerm, 2.0) / (second.Length - 1.0));
            double critical = StudentTQuantile(0.5 + confidenceLevel / 2.0, degrees);
            double estimate = Mean(first) - Mean(second);
            return new ConfidenceInterval(estimate, estimate - critical * standardError, estimate + critical * standardError, confidenceLevel);
        }

        /// <summary>
        /// Performs a one-sample z-test of sampleMean against nullMean with a known populationStandardDeviation and tail.
        /// </summary>
        public static HypothesisTestResult OneSampleZTest(double sampleMean, double populationStandardDeviation, int sampleSize, double nullMean, TailType tail)
        {
            EnsureFinite(sampleMean, nameof(sampleMean));
            EnsurePositiveFinite(populationStandardDeviation, nameof(populationStandardDeviation));
            EnsureSampleSize(sampleSize);
            EnsureFinite(nullMean, nameof(nullMean));
            double statistic = (sampleMean - nullMean) / (populationStandardDeviation / Math.Sqrt(sampleSize));
            return BuildTest("One-sample z-test", statistic, double.PositiveInfinity, value => NormalCdf(value, 0.0, 1.0), tail);
        }

        /// <summary>
        /// Performs a one-sample t-test of sample against nullMean with tail.
        /// </summary>
        public static HypothesisTestResult OneSampleTTest(double[] sample, double nullMean, TailType tail)
        {
            EnsureSample(sample, 2, nameof(sample));
            EnsureFinite(nullMean, nameof(nullMean));
            double degrees = sample.Length - 1.0;
            double statistic = (Mean(sample) - nullMean) / (SampleStandardDeviation(sample) / Math.Sqrt(sample.Length));
            return BuildTest("One-sample t-test", statistic, degrees, value => StudentTCdf(value, degrees), tail);
        }

        /// <summary>
        /// Performs a paired t-test on the differences between before and after measurements with tail.
        /// </summary>
        public static HypothesisTestResult PairedTTest(double[] before, double[] after, TailType tail)
        {
            EnsureSameLength(before, after, nameof(before), nameof(after), 2);
            double[] differences = new double[before.Length];
            for (int index = 0; index < before.Length; index++)
            {
                differences[index] = before[index] - after[index];
            }
            return OneSampleTTest(differences, 0.0, tail) with { Name = "Paired t-test" };
        }

        /// <summary>
        /// Performs a two-sample t-test comparing the means of first and second, pooled when equalVariances is true and Welch's otherwise, with tail.
        /// </summary>
        public static HypothesisTestResult TwoSampleTTest(double[] first, double[] second, bool equalVariances, TailType tail)
        {
            EnsureSample(first, 2, nameof(first));
            EnsureSample(second, 2, nameof(second));
            double firstVariance = SampleVariance(first);
            double secondVariance = SampleVariance(second);
            double firstTerm;
            double secondTerm;
            double degrees;
            if (equalVariances)
            {
                degrees = first.Length + second.Length - 2.0;
                double pooled = (((first.Length - 1.0) * firstVariance) + ((second.Length - 1.0) * secondVariance)) / degrees;
                firstTerm = pooled / first.Length;
                secondTerm = pooled / second.Length;
            }
            else
            {
                firstTerm = firstVariance / first.Length;
                secondTerm = secondVariance / second.Length;
                degrees = Math.Pow(firstTerm + secondTerm, 2.0)
                    / (Math.Pow(firstTerm, 2.0) / (first.Length - 1.0) + Math.Pow(secondTerm, 2.0) / (second.Length - 1.0));
            }
            double statistic = (Mean(first) - Mean(second)) / Math.Sqrt(firstTerm + secondTerm);
            return BuildTest("Two-sample t-test", statistic, degrees, value => StudentTCdf(value, degrees), tail);
        }
        /// <summary>
        /// Performs a proportion z-test for successes out of trials against nullProportion with tail.
        /// </summary>

        public static HypothesisTestResult ProportionZTest(int successes, int trials, double nullProportion, TailType tail)
        {
            ValidateBinomial(successes, trials, nullProportion);
            if (nullProportion == 0.0 || nullProportion == 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(nullProportion), "The null proportion must be strictly between zero and one.");
            }
            double observed = (double)successes / trials;
            double standardError = Math.Sqrt(nullProportion * (1.0 - nullProportion) / trials);
            double statistic = (observed - nullProportion) / standardError;
            return BuildTest("Proportion z-test", statistic, double.PositiveInfinity, value => NormalCdf(value, 0.0, 1.0), tail);
        }

        /// <summary>
        /// Performs a chi-square goodness-of-fit test of observed counts against expectedProportions.
        /// </summary>
        public static HypothesisTestResult ChiSquareGoodnessOfFit(int[] observed, double[] expectedProportions)
        {
            if (observed == null)
            {
                throw new ArgumentNullException(nameof(observed));
            }
            if (expectedProportions == null)
            {
                throw new ArgumentNullException(nameof(expectedProportions));
            }
            if (observed.Length < 2 || observed.Length != expectedProportions.Length)
            {
                throw new ArgumentException("Observed counts and expected proportions must have the same length of at least two.");
            }
            int total = 0;
            double proportionTotal = 0.0;
            for (int index = 0; index < observed.Length; index++)
            {
                if (observed[index] < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(observed), "Observed counts must be non-negative.");
                }
                EnsurePositiveFinite(expectedProportions[index], nameof(expectedProportions));
                total = checked(total + observed[index]);
                proportionTotal += expectedProportions[index];
            }
            if (total == 0 || Math.Abs(proportionTotal - 1.0) > 1e-9)
            {
                throw new ArgumentException("Observed total must be positive and expected proportions must sum to one.");
            }
            double statistic = 0.0;
            for (int index = 0; index < observed.Length; index++)
            {
                double expected = total * expectedProportions[index];
                double difference = observed[index] - expected;
                statistic += difference * difference / expected;
            }
            double degrees = observed.Length - 1.0;
            double pValue = RegularizedGammaQ(degrees / 2.0, statistic / 2.0);
            double critical = ChiSquareQuantile(0.95, degrees);
            return new HypothesisTestResult("Chi-square goodness-of-fit", statistic, pValue, critical, degrees, pValue < 0.05);
        }

        /// <summary>
        /// Performs a chi-square test of independence on a contingency table of counts.
        /// </summary>
        public static HypothesisTestResult ChiSquareIndependence(int[,] counts)
        {
            if (counts == null)
            {
                throw new ArgumentNullException(nameof(counts));
            }
            int rows = counts.GetLength(0);
            int columns = counts.GetLength(1);
            if (rows < 2 || columns < 2)
            {
                throw new ArgumentException("The contingency table must have at least two rows and columns.", nameof(counts));
            }
            double[] rowTotals = new double[rows];
            double[] columnTotals = new double[columns];
            double total = 0.0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (counts[row, column] < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(counts), "Counts must be non-negative.");
                    }
                    rowTotals[row] += counts[row, column];
                    columnTotals[column] += counts[row, column];
                    total += counts[row, column];
                }
            }
            if (total == 0.0)
            {
                throw new ArgumentException("The contingency table must have a positive total.", nameof(counts));
            }
            double statistic = 0.0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    double expected = rowTotals[row] * columnTotals[column] / total;
                    if (expected <= 0.0)
                    {
                        throw new ArgumentException("All expected counts must be positive.", nameof(counts));
                    }
                    double difference = counts[row, column] - expected;
                    statistic += difference * difference / expected;
                }
            }
            double degrees = (rows - 1.0) * (columns - 1.0);
            double pValue = RegularizedGammaQ(degrees / 2.0, statistic / 2.0);
            return new HypothesisTestResult("Chi-square independence", statistic, pValue, ChiSquareQuantile(0.95, degrees), degrees, pValue < 0.05);
        }

        /// <summary>
        /// Performs a one-way ANOVA across the supplied groups.
        /// </summary>
        public static AnovaResult OneWayAnova(params double[][] groups)
        {
            if (groups == null)
            {
                throw new ArgumentNullException(nameof(groups));
            }
            if (groups.Length < 2)
            {
                throw new ArgumentException("ANOVA requires at least two groups.", nameof(groups));
            }
            int totalCount = 0;
            double grandTotal = 0.0;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                EnsureSample(groups[groupIndex], 1, nameof(groups));
                totalCount += groups[groupIndex].Length;
                grandTotal += Sum(groups[groupIndex]);
            }
            if (totalCount <= groups.Length)
            {
                throw new ArgumentException("ANOVA requires positive within-group degrees of freedom.", nameof(groups));
            }
            double grandMean = grandTotal / totalCount;
            double betweenSum = 0.0;
            double withinSum = 0.0;
            foreach (double[] group in groups)
            {
                double groupMean = Mean(group);
                betweenSum += group.Length * Math.Pow(groupMean - grandMean, 2.0);
                foreach (double value in group)
                {
                    withinSum += Math.Pow(value - groupMean, 2.0);
                }
            }
            double betweenVariance = betweenSum / (groups.Length - 1.0);
            double withinDegrees = totalCount - groups.Length;
            double withinVariance = withinDegrees > 0.0 ? withinSum / withinDegrees : 0.0;
            double statistic = withinVariance == 0.0
                ? (betweenVariance == 0.0 ? 0.0 : double.PositiveInfinity)
                : betweenVariance / withinVariance;
            double pValue = double.IsPositiveInfinity(statistic)
                ? 0.0
                : 1.0 - FCdf(statistic, groups.Length - 1.0, withinDegrees);
            return new AnovaResult(betweenVariance, withinVariance, statistic, pValue, pValue < 0.05);
        }

        /// <summary>
        /// Computes the posterior probability from prior, likelihood, and marginalLikelihood using Bayes' theorem.
        /// </summary>
        public static double BayesTheorem(double prior, double likelihood, double marginalLikelihood)
        {
            EnsureProbability(prior, nameof(prior));
            EnsureProbability(likelihood, nameof(likelihood));
            EnsurePositiveFinite(marginalLikelihood, nameof(marginalLikelihood));
            double posterior = prior * likelihood / marginalLikelihood;
            if (posterior > 1.0 + 1e-12)
            {
                throw new ArgumentException("The supplied marginal likelihood is inconsistent with the prior and likelihood.");
            }
            return Math.Clamp(posterior, 0.0, 1.0);
        }

        /// <summary>
        /// Computes the posterior Beta alpha and beta parameters after observing successes and failures with a Beta prior.
        /// </summary>
        public static (double Alpha, double Beta) BetaBinomialPosterior(int successes, int failures, double priorAlpha, double priorBeta)
        {
            if (successes < 0 || failures < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(successes), "Successes and failures must be non-negative.");
            }
            EnsurePositiveFinite(priorAlpha, nameof(priorAlpha));
            EnsurePositiveFinite(priorBeta, nameof(priorBeta));
            return (priorAlpha + successes, priorBeta + failures);
        }

        /// <summary>
        /// Computes the posterior mean and variance of a normal mean given a normal prior and known observationVariance.
        /// </summary>
        public static (double Mean, double Variance) NormalNormalPosterior(double priorMean, double priorVariance, double[] observations, double observationVariance)
        {
            EnsureFinite(priorMean, nameof(priorMean));
            EnsurePositiveFinite(priorVariance, nameof(priorVariance));
            EnsureSample(observations, 1, nameof(observations));
            EnsurePositiveFinite(observationVariance, nameof(observationVariance));
            double precision = 1.0 / priorVariance + observations.Length / observationVariance;
            double mean = ((priorMean / priorVariance) + (Sum(observations) / observationVariance)) / precision;
            return (mean, 1.0 / precision);
        }

        /// <summary>
        /// Computes the posterior Gamma shape and rate after observing totalCount events over exposure with a Gamma prior.
        /// </summary>
        public static (double Shape, double Rate) GammaPoissonPosterior(int totalCount, double exposure, double priorShape, double priorRate)
        {
            if (totalCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalCount));
            }
            EnsurePositiveFinite(exposure, nameof(exposure));
            EnsurePositiveFinite(priorShape, nameof(priorShape));
            EnsurePositiveFinite(priorRate, nameof(priorRate));
            return (priorShape + totalCount, priorRate + exposure);
        }

        /// <summary>
        /// Fits a simple linear regression of y on x by ordinary least squares.
        /// </summary>
        public static RegressionResult SimpleLinearRegression(double[] x, double[] y)
        {
            EnsureSameLength(x, y, nameof(x), nameof(y), 3);
            double meanX = Mean(x);
            double meanY = Mean(y);
            double covariance = 0.0;
            double varianceX = 0.0;
            for (int index = 0; index < x.Length; index++)
            {
                covariance += (x[index] - meanX) * (y[index] - meanY);
                varianceX += (x[index] - meanX) * (x[index] - meanX);
            }
            if (varianceX <= 0.0)
            {
                throw new ArgumentException("The predictor values must not all be equal.", nameof(x));
            }
            double slope = covariance / varianceX;
            double intercept = meanY - slope * meanX;
            double[,] design = new double[x.Length, 2];
            for (int index = 0; index < x.Length; index++)
            {
                design[index, 0] = 1.0;
                design[index, 1] = x[index];
            }
            return BuildRegressionResult(design, y, new[] { intercept, slope });
        }

        /// <summary>
        /// Fits a multiple linear regression of response on designMatrix by least squares.
        /// </summary>
        public static RegressionResult MultipleLinearRegression(double[,] designMatrix, double[] response)
        {
            if (designMatrix == null)
            {
                throw new ArgumentNullException(nameof(designMatrix));
            }
            EnsureSample(response, 2, nameof(response));
            int rows = designMatrix.GetLength(0);
            int columns = designMatrix.GetLength(1);
            if (rows != response.Length || columns == 0 || rows <= columns)
            {
                throw new ArgumentException("The design matrix must have more rows than columns and match the response length.", nameof(designMatrix));
            }
            EnsureMatrixFinite(designMatrix, nameof(designMatrix));
            LeastSquaresResult leastSquares = LinearAlgebraAdvanced.LeastSquares(designMatrix, response);
            return BuildRegressionResult(designMatrix, response, leastSquares.Solution);
        }

        /// <summary>
        /// Fits a logistic regression of binary labels on designMatrix using damped Newton iterations scaled by learningRate.
        /// </summary>
        public static RegressionResult LogisticRegression(double[,] designMatrix, int[] labels, double learningRate, int maxIterations)
        {
            if (designMatrix == null)
            {
                throw new ArgumentNullException(nameof(designMatrix));
            }
            if (labels == null)
            {
                throw new ArgumentNullException(nameof(labels));
            }
            EnsurePositiveFinite(learningRate, nameof(learningRate));
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations));
            }
            int rows = designMatrix.GetLength(0);
            int features = designMatrix.GetLength(1);
            if (rows < 2 || features == 0 || labels.Length != rows)
            {
                throw new ArgumentException("The design matrix must have non-empty features and match the labels.", nameof(designMatrix));
            }
            EnsureMatrixFinite(designMatrix, nameof(designMatrix));
            double[,] augmented = new double[rows, features + 1];
            for (int row = 0; row < rows; row++)
            {
                if (labels[row] != 0 && labels[row] != 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(labels), "Logistic labels must be zero or one.");
                }
                augmented[row, 0] = 1.0;
                for (int column = 0; column < features; column++)
                {
                    augmented[row, column + 1] = designMatrix[row, column];
                }
            }

            int parameterCount = features + 1;
            double[] coefficients = new double[parameterCount];
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double[] gradient = new double[parameterCount];
                double[,] hessian = new double[parameterCount, parameterCount];
                for (int row = 0; row < rows; row++)
                {
                    double linear = 0.0;
                    for (int column = 0; column < parameterCount; column++)
                    {
                        linear += augmented[row, column] * coefficients[column];
                    }
                    double probability = StableSigmoid(linear);
                    double weight = Math.Max(probability * (1.0 - probability), 1e-12);
                    double residual = labels[row] - probability;
                    for (int first = 0; first < parameterCount; first++)
                    {
                        gradient[first] += augmented[row, first] * residual;
                        for (int second = 0; second < parameterCount; second++)
                        {
                            hessian[first, second] += weight * augmented[row, first] * augmented[row, second];
                        }
                    }
                }
                for (int diagonal = 0; diagonal < parameterCount; diagonal++)
                {
                    hessian[diagonal, diagonal] += 1e-6;
                }
                if (!Algebra.TrySolveLinearSystem(hessian, gradient, out double[] step))
                {
                    throw new InvalidOperationException("Logistic regression Hessian is singular.");
                }
                double stepNorm = 0.0;
                for (int index = 0; index < parameterCount; index++)
                {
                    double update = learningRate * step[index];
                    coefficients[index] += update;
                    stepNorm += update * update;
                }
                if (Math.Sqrt(stepNorm) <= 1e-8)
                {
                    break;
                }
            }

            double logLikelihood = 0.0;
            double nullLogLikelihood = 0.0;
            double labelMean = (double)Sum(labels) / rows;
            double[,] information = new double[parameterCount, parameterCount];
            for (int row = 0; row < rows; row++)
            {
                double linear = 0.0;
                for (int column = 0; column < parameterCount; column++)
                {
                    linear += augmented[row, column] * coefficients[column];
                }
                double probability = Math.Clamp(StableSigmoid(linear), 1e-15, 1.0 - 1e-15);
                logLikelihood += labels[row] * Math.Log(probability) + (1 - labels[row]) * LogOnePlus(-probability);
                double nullProbability = Math.Clamp(labelMean, 1e-15, 1.0 - 1e-15);
                nullLogLikelihood += labels[row] * Math.Log(nullProbability) + (1 - labels[row]) * LogOnePlus(-nullProbability);
                double weight = probability * (1.0 - probability);
                for (int first = 0; first < parameterCount; first++)
                {
                    for (int second = 0; second < parameterCount; second++)
                    {
                        information[first, second] += weight * augmented[row, first] * augmented[row, second];
                    }
                }
            }
            for (int diagonal = 0; diagonal < parameterCount; diagonal++)
            {
                information[diagonal, diagonal] += 1e-6;
            }
            double[,] covariance = Algebra.MatrixInverse(information);
            double pseudoRSquared = nullLogLikelihood == 0.0 ? 1.0 : 1.0 - logLikelihood / nullLogLikelihood;
            double adjusted = 1.0 - ((1.0 - pseudoRSquared) * (rows - 1.0) / Math.Max(1.0, rows - parameterCount - 1.0));
            double standardError = Math.Sqrt(Math.Max(0.0, -2.0 * logLikelihood / rows));
            return new RegressionResult(coefficients, pseudoRSquared, adjusted, standardError, covariance);
        }

        /// <summary>
        /// Builds regression statistics (R-squared, adjusted R-squared, standard error, and covariance) for fitted coefficients.
        /// </summary>
        private static RegressionResult BuildRegressionResult(double[,] design, double[] response, double[] coefficients)
        {
            int rows = design.GetLength(0);
            int columns = design.GetLength(1);
            double mean = Mean(response);
            double residualSum = 0.0;
            double totalSum = 0.0;
            for (int row = 0; row < rows; row++)
            {
                double predicted = 0.0;
                for (int column = 0; column < columns; column++)
                {
                    predicted += design[row, column] * coefficients[column];
                }
                residualSum += Math.Pow(response[row] - predicted, 2.0);
                totalSum += Math.Pow(response[row] - mean, 2.0);
            }
            double rSquared = totalSum == 0.0 ? (residualSum == 0.0 ? 1.0 : 0.0) : 1.0 - residualSum / totalSum;
            double adjusted = rows > columns + 1
                ? 1.0 - ((1.0 - rSquared) * (rows - 1.0) / (rows - columns - 1.0))
                : double.NaN;
            double residualVariance = residualSum / (rows - columns);
            double[,] transpose = Algebra.MatrixTranspose(design);
            double[,] information = Algebra.MatrixMultiply(transpose, design);
            double[,] covarianceBase = Algebra.MatrixInverse(information);
            for (int row = 0; row < covarianceBase.GetLength(0); row++)
            {
                for (int column = 0; column < covarianceBase.GetLength(1); column++)
                {
                    covarianceBase[row, column] *= residualVariance;
                }
            }
            return new RegressionResult(coefficients, rSquared, adjusted, Math.Sqrt(residualVariance), covarianceBase);
        }

        /// <summary>
        /// Builds a hypothesis test result from a statistic, its cumulative distribution function, and the requested tail.
        /// </summary>
        private static HypothesisTestResult BuildTest(
            string name,
            double statistic,
            double degreesOfFreedom,
            Func<double, double> cdf,
            TailType tail,
            double significance = 0.05)
        {
            EnsureTail(tail);
            double lowerTail = ClampProbability(cdf(statistic));
            double pValue = tail switch
            {
                TailType.TwoSided => Math.Min(1.0, 2.0 * Math.Min(lowerTail, 1.0 - lowerTail)),
                TailType.Upper => 1.0 - lowerTail,
                TailType.Lower => lowerTail,
                _ => throw new ArgumentOutOfRangeException(nameof(tail))
            };
            double critical = tail switch
            {
                TailType.TwoSided => Quantile(0.5 + significance / 2.0, cdf),
                TailType.Upper => Quantile(1.0 - significance, cdf),
                TailType.Lower => Quantile(significance, cdf),
                _ => 0.0
            };
            return new HypothesisTestResult(name, statistic, pValue, critical, degreesOfFreedom, pValue < significance);
        }

        /// <summary>
        /// Evaluates the Student t quantile (inverse CDF) at probability with degreesOfFreedom.
        /// </summary>
        private static double StudentTQuantile(double probability, double degreesOfFreedom)
        {
            EnsureProbability(probability, nameof(probability));
            EnsurePositiveFinite(degreesOfFreedom, nameof(degreesOfFreedom));
            if (probability == 0.0)
            {
                return double.NegativeInfinity;
            }
            if (probability == 1.0)
            {
                return double.PositiveInfinity;
            }
            return Quantile(probability, value => StudentTCdf(value, degreesOfFreedom));
        }

        /// <summary>
        /// Evaluates the chi-square quantile (inverse CDF) at probability with degreesOfFreedom.
        /// </summary>
        private static double ChiSquareQuantile(double probability, double degreesOfFreedom)
        {
            return Quantile(probability, value => ChiSquareCdf(value, degreesOfFreedom));
        }

        /// <summary>
        /// Numerically inverts cdf by bisection to find the value whose cumulative probability equals probability.
        /// </summary>
        private static double Quantile(double probability, Func<double, double> cdf)
        {
            if (probability <= 0.0)
            {
                return double.NegativeInfinity;
            }
            if (probability >= 1.0)
            {
                return double.PositiveInfinity;
            }
            double lower = -1.0;
            double upper = 1.0;
            while (cdf(lower) > probability && lower > -1e12)
            {
                lower *= 2.0;
            }
            while (cdf(upper) < probability && upper < 1e12)
            {
                upper *= 2.0;
            }
            for (int iteration = 0; iteration < 200; iteration++)
            {
                double middle = lower + (upper - lower) * 0.5;
                if (cdf(middle) < probability)
                {
                    lower = middle;
                }
                else
                {
                    upper = middle;
                }
            }
            return lower + (upper - lower) * 0.5;
        }

        /// <summary>
        /// Evaluates the standard normal quantile using a rational approximation refined by one Newton step.
        /// </summary>
        private static double StandardNormalQuantile(double probability)
        {
            EnsureProbability(probability, nameof(probability));
            if (probability == 0.0)
            {
                return double.NegativeInfinity;
            }
            if (probability == 1.0)
            {
                return double.PositiveInfinity;
            }

            double[] a = { -39.69683028665376, 220.9460984245205, -275.9285104469687, 138.3577518672690, -30.66479806614716, 2.506628277459239 };
            double[] b = { -54.47609879822406, 161.5858368580409, -155.6989798598866, 66.80131188771972, -13.28068155288572 };
            double[] c = { -0.007784894002430293, -0.3223964580411365, -2.400758277161838, -2.549732539343734, 4.374664141464968, 2.938163982698783 };
            double[] d = { 0.007784695709041462, 0.3224671290700398, 2.445134137142996, 3.754408661907416 };
            const double low = 0.02425;
            const double high = 1.0 - low;
            double result;
            if (probability < low)
            {
                double q = Math.Sqrt(-2.0 * Math.Log(probability));
                result = (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5])
                    / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1.0);
            }
            else if (probability <= high)
            {
                double q = probability - 0.5;
                double r = q * q;
                result = (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q
                    / (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1.0);
            }
            else
            {
                double q = Math.Sqrt(-2.0 * LogOnePlus(-probability));
                result = -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5])
                    / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1.0);
            }

            double error = NormalCdf(result, 0.0, 1.0) - probability;
            return result - error / NormalPdf(result, 0.0, 1.0);
        }

        /// <summary>
        /// Evaluates the complementary error function for value using a rational approximation.
        /// </summary>
        private static double ErfcPositive(double value)
        {
            if (value == 0.0)
            {
                return 1.0;
            }
            double t = 1.0 / (1.0 + 0.5 * value);
            double polynomial = -1.26551223
                + t * (1.00002368
                + t * (0.37409196
                + t * (0.09678418
                + t * (-0.18628806
                + t * (0.27886807
                + t * (-1.13520398
                + t * (1.48851587
                + t * (-0.82215223
                + t * 0.17087277))))))));
            return t * Math.Exp(-value * value + polynomial);
        }

        /// <summary>
        /// Evaluates the regularized upper incomplete gamma function Q(a, x) using its continued fraction expansion.
        /// </summary>
        private static double GammaContinuedFractionQ(double a, double x)
        {
            double b = x + 1.0 - a;
            double c = 1.0 / 1e-300;
            double d = 1.0 / b;
            double h = d;
            for (int iteration = 1; iteration <= MaximumIterations; iteration++)
            {
                double an = -iteration * (iteration - a);
                b += 2.0;
                d = an * d + b;
                if (Math.Abs(d) < 1e-300)
                {
                    d = 1e-300;
                }
                c = b + an / c;
                if (Math.Abs(c) < 1e-300)
                {
                    c = 1e-300;
                }
                d = 1.0 / d;
                double delta = d * c;
                h *= delta;
                if (Math.Abs(delta - 1.0) <= Epsilon)
                {
                    return h * Math.Exp((-x) + (a * Math.Log(x)) - LogGamma(a));
                }
            }
            throw new InvalidOperationException("Regularized gamma continued fraction did not converge.");
        }

        /// <summary>
        /// Evaluates the continued fraction expansion used by the regularized incomplete beta function.
        /// </summary>
        private static double BetaContinuedFraction(double a, double b, double x)
        {
            const double tiny = 1e-300;
            double qab = a + b;
            double qap = a + 1.0;
            double qam = a - 1.0;
            double c = 1.0;
            double d = 1.0 - qab * x / qap;
            if (Math.Abs(d) < tiny)
            {
                d = tiny;
            }
            d = 1.0 / d;
            double h = d;
            for (int iteration = 1; iteration <= MaximumIterations; iteration++)
            {
                int twice = 2 * iteration;
                double aa = iteration * (b - iteration) * x / ((qam + twice) * (a + twice));
                h *= UpdateBetaFraction(ref c, ref d, aa);
                aa = -(a + iteration) * (qab + iteration) * x / ((a + twice) * (qap + twice));
                double delta = UpdateBetaFraction(ref c, ref d, aa);
                h *= delta;
                if (Math.Abs(delta - 1.0) <= Epsilon)
                {
                    return h;
                }
            }
            throw new InvalidOperationException("Incomplete beta continued fraction did not converge.");
        }

        /// <summary>
        /// Applies one step of the modified Lentz continued fraction update and returns the delta factor.
        /// </summary>
        private static double UpdateBetaFraction(ref double c, ref double d, double coefficient)
        {
            const double tiny = 1e-300;
            d = 1.0 + coefficient * d;
            if (Math.Abs(d) < tiny)
            {
                d = tiny;
            }
            c = 1.0 + coefficient / c;
            if (Math.Abs(c) < tiny)
            {
                c = tiny;
            }
            d = 1.0 / d;
            return d * c;
        }

        /// <summary>
        /// Evaluates the natural logarithm of the beta function B(a, b).
        /// </summary>
        private static double LogBeta(double a, double b)
        {
            return LogGamma(a) + LogGamma(b) - LogGamma(a + b);
        }

        /// <summary>
        /// Evaluates log(1 + value), using a series expansion for values near zero.
        /// </summary>
        private static double LogOnePlus(double value)
        {
            if (value <= -1.0)
            {
                return Math.Log(1.0 + value);
            }
            if (Math.Abs(value) > 1e-4)
            {
                return Math.Log(1.0 + value);
            }
            double term = value;
            double sum = 0.0;
            for (int index = 1; index <= 20; index++)
            {
                sum += (index % 2 == 0 ? -term : term) / index;
                term *= value;
            }
            return sum;
        }

        /// <summary>
        /// Evaluates exp(value) - 1, using a series expansion for values near zero.
        /// </summary>
        private static double ExpMinusOne(double value)
        {
            if (Math.Abs(value) > 1e-5)
            {
                return Math.Exp(value) - 1.0;
            }
            double term = value;
            double sum = value;
            for (int index = 2; index <= 20; index++)
            {
                term *= value / index;
                sum += term;
            }
            return sum;
        }

        /// <summary>
        /// Evaluates the natural logarithm of the binomial coefficient n choose k.
        /// </summary>
        private static double LogChoose(int n, int k)
        {
            return LogGamma(n + 1.0) - LogGamma(k + 1.0) - LogGamma(n - k + 1.0);
        }

        /// <summary>
        /// Computes the arithmetic mean of values.
        /// </summary>
        private static double Mean(double[] values)
        {
            return Sum(values) / values.Length;
        }

        /// <summary>
        /// Computes the sum of values.
        /// </summary>
        private static double Sum(double[] values)
        {
            double sum = 0.0;
            foreach (double value in values)
            {
                sum += value;
            }
            return sum;
        }

        /// <summary>
        /// Computes the sum of values, throwing on integer overflow.
        /// </summary>
        private static int Sum(int[] values)
        {
            int sum = 0;
            foreach (int value in values)
            {
                sum = checked(sum + value);
            }
            return sum;
        }

        /// <summary>
        /// Computes the unbiased sample variance of values.
        /// </summary>
        private static double SampleVariance(double[] values)
        {
            double mean = Mean(values);
            double sum = 0.0;
            foreach (double value in values)
            {
                sum += (value - mean) * (value - mean);
            }
            return sum / (values.Length - 1.0);
        }

        /// <summary>
        /// Computes the unbiased sample standard deviation of values.
        /// </summary>
        private static double SampleStandardDeviation(double[] values)
        {
            return Math.Sqrt(SampleVariance(values));
        }

        /// <summary>
        /// Computes the logistic sigmoid of value in a numerically stable manner.
        /// </summary>
        private static double StableSigmoid(double value)
        {
            if (value >= 0.0)
            {
                double exp = Math.Exp(-value);
                return 1.0 / (1.0 + exp);
            }
            double negativeExp = Math.Exp(value);
            return negativeExp / (1.0 + negativeExp);
        }

        /// <summary>
        /// Validates that values is non-null, has at least minimumLength elements, and contains only finite values.
        /// </summary>
        private static void EnsureSample(double[] values, int minimumLength, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (values.Length < minimumLength)
            {
                throw new ArgumentException($"The sample must contain at least {minimumLength} values.", parameterName);
            }
            foreach (double value in values)
            {
                EnsureFinite(value, parameterName);
            }
        }

        /// <summary>
        /// Validates both samples and that they have equal lengths.
        /// </summary>
        private static void EnsureSameLength(double[] first, double[] second, string firstName, string secondName, int minimumLength)
        {
            EnsureSample(first, minimumLength, firstName);
            EnsureSample(second, minimumLength, secondName);
            if (first.Length != second.Length)
            {
                throw new ArgumentException("The arrays must have equal lengths.", secondName);
            }
        }

        /// <summary>
        /// Validates that every element of matrix is finite.
        /// </summary>
        private static void EnsureMatrixFinite(double[,] matrix, string parameterName)
        {
            for (int row = 0; row < matrix.GetLength(0); row++)
            {
                for (int column = 0; column < matrix.GetLength(1); column++)
                {
                    EnsureFinite(matrix[row, column], parameterName);
                }
            }
        }

        /// <summary>
        /// Validates binomial arguments: non-negative trials, successes within [0, trials], and probability within [0, 1].
        /// </summary>
        private static void ValidateBinomial(int successes, int trials, double probability)
        {
            if (trials < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trials));
            }
            if (successes < 0 || successes > trials)
            {
                throw new ArgumentOutOfRangeException(nameof(successes));
            }
            EnsureProbability(probability, nameof(probability));
        }

        /// <summary>
        /// Validates that lower and upper are finite and that upper exceeds lower.
        /// </summary>
        private static void ValidateUniform(double lower, double upper)
        {
            EnsureFinite(lower, nameof(lower));
            EnsureFinite(upper, nameof(upper));
            if (upper <= lower)
            {
                throw new ArgumentOutOfRangeException(nameof(upper), "The upper bound must exceed the lower bound.");
            }
        }

        /// <summary>
        /// Validates gamma function arguments: a must be positive and finite, and x must be finite and non-negative.
        /// </summary>
        private static void ValidateGammaArguments(double a, double x)
        {
            EnsurePositiveFinite(a, nameof(a));
            EnsureFinite(x, nameof(x));
            if (x < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "The gamma argument must be non-negative.");
            }
        }

        /// <summary>
        /// Validates beta function arguments: a and b must be positive and finite, and x must lie in [0, 1].
        /// </summary>
        private static void ValidateBetaArguments(double a, double b, double x)
        {
            EnsurePositiveFinite(a, nameof(a));
            EnsurePositiveFinite(b, nameof(b));
            EnsureFinite(x, nameof(x));
            if (x < 0.0 || x > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "The beta argument must be in [0, 1].");
            }
        }

        /// <summary>
        /// Validates that sampleSize is at least one.
        /// </summary>
        private static void EnsureSampleSize(int sampleSize)
        {
            if (sampleSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleSize));
            }
        }

        /// <summary>
        /// Validates that confidenceLevel is finite and within (0, 1).
        /// </summary>
        private static void EnsureConfidenceLevel(double confidenceLevel)
        {
            if (!double.IsFinite(confidenceLevel) || confidenceLevel <= 0.0 || confidenceLevel >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(confidenceLevel), "Confidence level must be in (0, 1).");
            }
        }

        /// <summary>
        /// Validates that probability is finite and within [0, 1].
        /// </summary>
        private static void EnsureProbability(double probability, string parameterName)
        {
            if (!double.IsFinite(probability) || probability < 0.0 || probability > 1.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Probability must be in [0, 1].");
            }
        }

        /// <summary>
        /// Validates that value is finite and strictly positive.
        /// </summary>
        private static void EnsurePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
            }
        }

        /// <summary>
        /// Validates that value is finite.
        /// </summary>
        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        /// <summary>
        /// Validates that random is not null.
        /// </summary>
        private static void EnsureRandom(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }
        }

        /// <summary>
        /// Validates that tail is a defined TailType value.
        /// </summary>
        private static void EnsureTail(TailType tail)
        {
            if (tail != TailType.TwoSided && tail != TailType.Upper && tail != TailType.Lower)
            {
                throw new ArgumentOutOfRangeException(nameof(tail));
            }
        }

        /// <summary>
        /// Clamps value to the range [0, 1].
        /// </summary>
        private static double ClampProbability(double value)
        {
            return Math.Clamp(value, 0.0, 1.0);
        }
    }
}
