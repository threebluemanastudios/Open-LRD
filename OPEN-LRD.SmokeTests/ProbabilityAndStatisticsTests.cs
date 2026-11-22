using System;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ProbabilityAndStatisticsTests
    {
        public static void RunAll()
        {
            Run("Special functions", TestSpecialFunctions);
            Run("Normal distribution", TestNormalDistribution);
            Run("Discrete distributions", TestDiscreteDistributions);
            Run("Continuous distributions", TestContinuousDistributions);
            Run("Confidence intervals", TestConfidenceIntervals);
            Run("Hypothesis tests", TestHypothesisTests);
            Run("Bayesian updates", TestBayesianUpdates);
            Run("Linear regressions", TestRegressions);
            Run("Seeded distribution sampling", TestSampling);
            Run("Probability and statistics validation", TestValidation);
        }

        private static void TestSpecialFunctions()
        {
            AssertEqual(ProbabilityAndStatistics.Erf(0.0), 0.0, "erf zero");
            AssertEqual(ProbabilityAndStatistics.Erf(1.0), 0.8427007929497149, "erf one", 2e-7);
            AssertEqual(ProbabilityAndStatistics.Erf(-2.0), -0.9953222650189527, "erf symmetry", 2e-7);
            AssertEqual(ProbabilityAndStatistics.Erfc(2.0), 0.004677734981047266, "erfc two", 2e-7);
            AssertEqual(ProbabilityAndStatistics.LogGamma(5.0), Math.Log(24.0), "log gamma factorial");
            AssertEqual(ProbabilityAndStatistics.RegularizedGammaP(1.0, 1.0), 1.0 - Math.Exp(-1.0), "regularized gamma P");
            AssertEqual(ProbabilityAndStatistics.RegularizedGammaQ(1.0, 1.0), Math.Exp(-1.0), "regularized gamma Q");
            AssertEqual(ProbabilityAndStatistics.RegularizedIncompleteBeta(2.0, 2.0, 0.5), 0.5, "regularized beta");
            AssertEqual(ProbabilityAndStatistics.IncompleteBeta(2.0, 2.0, 0.5), 1.0 / 12.0, "incomplete beta");
        }

        private static void TestNormalDistribution()
        {
            AssertEqual(ProbabilityAndStatistics.NormalPdf(0.0, 0.0, 1.0), 1.0 / Math.Sqrt(2.0 * Math.PI), "standard normal PDF");
            AssertEqual(ProbabilityAndStatistics.NormalCdf(0.0, 0.0, 1.0), 0.5, "standard normal CDF");
            AssertEqual(ProbabilityAndStatistics.NormalCdf(1.0, 0.0, 1.0), 0.8413447460685429, "normal CDF at one", 2e-7);
            AssertEqual(ProbabilityAndStatistics.NormalQuantile(0.975, 0.0, 1.0), 1.959963984540054, "normal quantile", 1e-7);
            AssertEqual(
                ProbabilityAndStatistics.NormalCdf(ProbabilityAndStatistics.NormalQuantile(0.123, 4.0, 3.0), 4.0, 3.0),
                0.123,
                "normal CDF/quantile round trip",
                1e-8);
        }

        private static void TestDiscreteDistributions()
        {
            AssertEqual(ProbabilityAndStatistics.BinomialPmf(1, 2, 0.5), 0.5, "binomial PMF");
            AssertEqual(ProbabilityAndStatistics.BinomialCdf(1, 2, 0.5), 0.75, "binomial CDF");
            AssertEqual(ProbabilityAndStatistics.PoissonPmf(0, 3.0), Math.Exp(-3.0), "Poisson PMF");
            AssertEqual(ProbabilityAndStatistics.PoissonCdf(0, 3.0), Math.Exp(-3.0), "Poisson CDF");
            AssertEqual(ProbabilityAndStatistics.BinomialPmf(0, 5, 0.0), 1.0, "binomial p=0 endpoint");
            AssertEqual(ProbabilityAndStatistics.BinomialPmf(5, 5, 1.0), 1.0, "binomial p=1 endpoint");
            AssertEqual(ProbabilityAndStatistics.BinomialPmf(2, 5, 0.0), 0.0, "impossible successes at p=0");
            AssertEqual(ProbabilityAndStatistics.BinomialCdf(0, 5, 1.0), 0.0, "binomial CDF at p=1 below its support");
            Assert(ProbabilityAndStatistics.BinomialSample(new Random(7), 8, 0.0) == 0 &&
                   ProbabilityAndStatistics.BinomialSample(new Random(7), 8, 1.0) == 8,
                "Binomial sampling should be deterministic at probability endpoints.");
        }

        private static void TestContinuousDistributions()
        {
            AssertEqual(ProbabilityAndStatistics.ExponentialPdf(0.5, 2.0), 2.0 * Math.Exp(-1.0), "exponential PDF");
            AssertEqual(ProbabilityAndStatistics.ExponentialCdf(0.5, 2.0), 1.0 - Math.Exp(-1.0), "exponential CDF");
            AssertEqual(ProbabilityAndStatistics.ExponentialQuantile(0.5, 2.0), Math.Log(2.0) / 2.0, "exponential quantile");
            AssertEqual(ProbabilityAndStatistics.UniformPdf(0.5, 0.0, 2.0), 0.5, "uniform PDF");
            AssertEqual(ProbabilityAndStatistics.UniformCdf(0.5, 0.0, 2.0), 0.25, "uniform CDF");
            AssertEqual(ProbabilityAndStatistics.BetaPdf(0.5, 2.0, 2.0), 1.5, "beta PDF");
            AssertEqual(ProbabilityAndStatistics.BetaCdf(0.5, 2.0, 2.0), 0.5, "beta CDF");
            AssertEqual(ProbabilityAndStatistics.GammaPdf(1.0, 2.0, 3.0), 9.0 * Math.Exp(-3.0), "gamma PDF");
            AssertEqual(ProbabilityAndStatistics.GammaCdf(1.0, 2.0, 3.0), 1.0 - 4.0 * Math.Exp(-3.0), "gamma CDF");
            AssertEqual(ProbabilityAndStatistics.StudentTPdf(0.0, 1.0), 1.0 / Math.PI, "Student-t PDF");
            AssertEqual(ProbabilityAndStatistics.StudentTCdf(0.0, 5.0), 0.5, "Student-t CDF");
            AssertEqual(ProbabilityAndStatistics.ChiSquarePdf(2.0, 2.0), 0.5 * Math.Exp(-1.0), "chi-square PDF");
            AssertEqual(ProbabilityAndStatistics.ChiSquareCdf(2.0, 2.0), 1.0 - Math.Exp(-1.0), "chi-square CDF");
            AssertEqual(ProbabilityAndStatistics.FPdf(1.0, 2.0, 2.0), 0.25, "F PDF");
            AssertEqual(ProbabilityAndStatistics.FCdf(1.0, 2.0, 2.0), 0.5, "F CDF");
        }

        private static void TestConfidenceIntervals()
        {
            ConfidenceInterval normal = ProbabilityAndStatistics.NormalConfidenceInterval(10.0, 2.0, 100, 0.95);
            AssertEqual(normal.Lower, 9.60800720309199, "normal confidence lower", 1e-7);
            AssertEqual(normal.Upper, 10.39199279690801, "normal confidence upper", 1e-7);

            ConfidenceInterval mean = ProbabilityAndStatistics.MeanConfidenceInterval(new[] { 2.0, 4.0, 6.0, 8.0, 10.0 }, 0.95);
            AssertEqual(mean.Estimate, 6.0, "mean interval estimate");
            Assert(mean.Lower < 6.0 && mean.Upper > 6.0, "mean interval should contain sample mean");

            ConfidenceInterval proportion = ProbabilityAndStatistics.ProportionConfidenceInterval(50, 100, 0.95);
            Assert(proportion.Lower < 0.5 && proportion.Upper > 0.5, "Wilson interval should contain observed proportion");
            ConfidenceInterval difference = ProbabilityAndStatistics.DifferenceInMeansConfidenceInterval(
                new[] { 1.0, 2.0, 3.0 },
                new[] { 2.0, 3.0, 4.0 },
                0.95);
            AssertEqual(difference.Estimate, -1.0, "difference-in-means estimate");
        }

        private static void TestHypothesisTests()
        {
            HypothesisTestResult zTest = ProbabilityAndStatistics.OneSampleZTest(2.0, 1.0, 25, 1.0, TailType.Upper);
            AssertEqual(zTest.Statistic, 5.0, "one-sample z statistic");
            Assert(zTest.RejectNull && zTest.PValue < 1e-5, "upper-tail z test should reject");

            HypothesisTestResult tTest = ProbabilityAndStatistics.OneSampleTTest(new[] { 2.0, 4.0, 6.0, 8.0, 10.0 }, 0.0, TailType.TwoSided);
            Assert(tTest.RejectNull && tTest.PValue < 0.05, "one-sample t test");
            AssertEqual(tTest.Statistic, 6.0 / Math.Sqrt(2.0), "one-sample t statistic");

            HypothesisTestResult paired = ProbabilityAndStatistics.PairedTTest(
                new[] { 3.0, 5.0, 7.0, 9.0 },
                new[] { 1.0, 3.0, 5.0, 7.0 },
                TailType.Upper);
            Assert(paired.RejectNull && paired.Name == "Paired t-test", "paired t test");

            HypothesisTestResult twoSample = ProbabilityAndStatistics.TwoSampleTTest(
                new[] { 2.0, 4.0, 6.0 },
                new[] { 4.0, 6.0, 8.0 },
                true,
                TailType.TwoSided);
            AssertEqual(twoSample.Statistic, -Math.Sqrt(1.5), "pooled two-sample t statistic");

            HypothesisTestResult proportion = ProbabilityAndStatistics.ProportionZTest(55, 100, 0.5, TailType.TwoSided);
            AssertEqual(proportion.Statistic, 1.0, "proportion z statistic");

            HypothesisTestResult goodnessOfFit = ProbabilityAndStatistics.ChiSquareGoodnessOfFit(new[] { 50, 50 }, new[] { 0.5, 0.5 });
            AssertEqual(goodnessOfFit.Statistic, 0.0, "goodness-of-fit statistic");
            AssertEqual(goodnessOfFit.PValue, 1.0, "goodness-of-fit p-value");

            HypothesisTestResult independence = ProbabilityAndStatistics.ChiSquareIndependence(new[,] { { 10, 20 }, { 20, 10 } });
            AssertEqual(independence.Statistic, 20.0 / 3.0, "independence statistic");
            Assert(independence.RejectNull, "independence test should reject");

            AnovaResult anova = ProbabilityAndStatistics.OneWayAnova(new[] { 1.0, 2.0, 3.0 }, new[] { 4.0, 5.0, 6.0 });
            AssertEqual(anova.FStatistic, 13.5, "one-way ANOVA F statistic");
            Assert(anova.RejectNull, "ANOVA should reject equal means");
        }

        private static void TestBayesianUpdates()
        {
            AssertEqual(ProbabilityAndStatistics.BayesTheorem(0.2, 0.5, 0.25), 0.4, "Bayes theorem");
            (double alpha, double beta) = ProbabilityAndStatistics.BetaBinomialPosterior(3, 2, 2.0, 2.0);
            AssertEqual(alpha, 5.0, "beta posterior alpha");
            AssertEqual(beta, 4.0, "beta posterior beta");
            (double mean, double variance) = ProbabilityAndStatistics.NormalNormalPosterior(0.0, 1.0, new[] { 1.0, 1.0 }, 1.0);
            AssertEqual(mean, 2.0 / 3.0, "normal posterior mean");
            AssertEqual(variance, 1.0 / 3.0, "normal posterior variance");
            (double shape, double rate) = ProbabilityAndStatistics.GammaPoissonPosterior(4, 2.0, 3.0, 1.0);
            AssertEqual(shape, 7.0, "gamma-Poisson posterior shape");
            AssertEqual(rate, 3.0, "gamma-Poisson posterior rate");
        }

        private static void TestRegressions()
        {
            RegressionResult simple = ProbabilityAndStatistics.SimpleLinearRegression(
                new[] { 0.0, 1.0, 2.0, 3.0 },
                new[] { 1.0, 3.0, 5.0, 7.0 });
            AssertEqual(simple.Coefficients[0], 1.0, "simple regression intercept");
            AssertEqual(simple.Coefficients[1], 2.0, "simple regression slope");
            AssertEqual(simple.RSquared, 1.0, "simple regression R squared");
            AssertEqual(simple.StandardError, 0.0, "simple regression residual error");

            double[,] design = { { 1.0, 0.0 }, { 1.0, 1.0 }, { 1.0, 2.0 }, { 1.0, 3.0 } };
            RegressionResult multiple = ProbabilityAndStatistics.MultipleLinearRegression(design, new[] { 1.0, 3.0, 5.0, 7.0 });
            AssertEqual(multiple.Coefficients[0], 1.0, "multiple regression intercept");
            AssertEqual(multiple.Coefficients[1], 2.0, "multiple regression slope");
            AssertEqual(multiple.RSquared, 1.0, "multiple regression R squared");

            RegressionResult logistic = ProbabilityAndStatistics.LogisticRegression(
                new[,] { { -2.0 }, { -1.0 }, { 0.0 }, { 1.0 }, { 2.0 }, { 3.0 } },
                new[] { 0, 0, 0, 1, 1, 1 },
                1.0,
                100);
            Assert(logistic.Coefficients.Length == 2, "logistic regression should include intercept and slope");
            Assert(logistic.Coefficients[1] > 0.0, "logistic regression should learn a positive slope");
        }

        private static void TestSampling()
        {
            Random firstRandom = new Random(1234);
            Random secondRandom = new Random(1234);
            for (int index = 0; index < 10; index++)
            {
                AssertEqual(
                    ProbabilityAndStatistics.NormalSample(firstRandom, 2.0, 3.0),
                    ProbabilityAndStatistics.NormalSample(secondRandom, 2.0, 3.0),
                    "seeded normal sample");
                Assert(
                    ProbabilityAndStatistics.BinomialSample(firstRandom, 20, 0.4)
                        == ProbabilityAndStatistics.BinomialSample(secondRandom, 20, 0.4),
                    "seeded binomial sample");
                Assert(
                    ProbabilityAndStatistics.PoissonSample(firstRandom, 15.0)
                        == ProbabilityAndStatistics.PoissonSample(secondRandom, 15.0),
                    "seeded Poisson sample");
            }
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => ProbabilityAndStatistics.NormalPdf(0.0, 0.0, 0.0), "zero normal deviation");
            AssertThrows<ArgumentOutOfRangeException>(() => ProbabilityAndStatistics.BinomialPmf(3, 2, 0.5), "invalid binomial successes");
            AssertThrows<ArgumentOutOfRangeException>(() => ProbabilityAndStatistics.ProportionConfidenceInterval(0, 0, 0.95), "empty proportion sample");
            AssertThrows<ArgumentOutOfRangeException>(() => ProbabilityAndStatistics.StudentTCdf(0.0, 0.0), "zero t degrees of freedom");
            AssertThrows<ArgumentException>(() => ProbabilityAndStatistics.MeanConfidenceInterval(new[] { 1.0 }, 0.95), "small mean sample");
            AssertThrows<ArgumentException>(() => ProbabilityAndStatistics.OneSampleTTest(Array.Empty<double>(), 0.0, TailType.TwoSided),
                "empty t-test sample");
            AssertThrows<ArgumentOutOfRangeException>(() => ProbabilityAndStatistics.MeanConfidenceInterval(new[] { double.PositiveInfinity, 1.0 }, 0.95),
                "non-finite sample observation");
            AssertThrows<ArgumentNullException>(() => ProbabilityAndStatistics.NormalSample(null!, 0.0, 1.0), "null random source");
        }
    }
}
