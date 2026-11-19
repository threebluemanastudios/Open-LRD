using System;
using System.Collections.Generic;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class InformationTheoryAndMLMathTests
    {
        public static void RunAll()
        {
            Run("Entropy and divergence", TestInformationTheory);
            Run("Activations and losses", TestActivationsAndLosses);
            Run("Classification metrics and ROC AUC", TestClassification);
            Run("Distance metrics", TestDistances);
            Run("Standardization and PCA", TestDimensionalityReduction);
            Run("Seeded k-means", TestKMeans);
            Run("Information and ML math validation", TestValidation);
        }

        private static void TestInformationTheory()
        {
            AssertEqual(InformationTheoryAndMLMath.Entropy(new[] { 0.25, 0.25, 0.25, 0.25 }), 2.0, "uniform entropy");
            double[,] independent = { { 0.25, 0.25 }, { 0.25, 0.25 } };
            AssertEqual(InformationTheoryAndMLMath.JointEntropy(independent), 2.0, "joint entropy");
            AssertEqual(InformationTheoryAndMLMath.ConditionalEntropy(independent), 1.0, "conditional entropy");
            AssertEqual(InformationTheoryAndMLMath.MutualInformation(independent), 0.0, "independent mutual information");

            double[,] dependent = { { 0.5, 0.0 }, { 0.0, 0.5 } };
            AssertEqual(InformationTheoryAndMLMath.ConditionalEntropy(dependent), 0.0, "dependent conditional entropy");
            AssertEqual(InformationTheoryAndMLMath.MutualInformation(dependent), 1.0, "dependent mutual information");
            AssertEqual(InformationTheoryAndMLMath.KlDivergence(new[] { 0.5, 0.5 }, new[] { 0.5, 0.5 }), 0.0, "identical KL divergence");
            AssertEqual(InformationTheoryAndMLMath.KlDivergence(new[] { 1.0, 0.0 }, new[] { 0.5, 0.5 }), 1.0, "KL divergence in bits");
            Assert(InformationTheoryAndMLMath.JensenShannonDivergence(new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 }) > 0.0, "Jensen-Shannon divergence");
            AssertEqual(InformationTheoryAndMLMath.CrossEntropy(new[] { 1.0, 0.0 }, new[] { 0.5, 0.5 }), 1.0, "cross entropy in bits");
        }

        private static void TestActivationsAndLosses()
        {
            AssertEqual(InformationTheoryAndMLMath.Sigmoid(0.0), 0.5, "sigmoid zero");
            AssertEqual(InformationTheoryAndMLMath.Sigmoid(1000.0), 1.0, "stable positive sigmoid");
            AssertEqual(InformationTheoryAndMLMath.Sigmoid(-1000.0), 0.0, "stable negative sigmoid");
            double[] softmax = InformationTheoryAndMLMath.Softmax(new[] { 1000.0, 1001.0, 999.0 });
            AssertEqual(softmax[0] + softmax[1] + softmax[2], 1.0, "softmax normalization");
            Assert(softmax[1] > softmax[0] && softmax[0] > softmax[2], "softmax ordering");
            AssertEqual(InformationTheoryAndMLMath.Relu(-2.0), 0.0, "ReLU");
            AssertEqual(InformationTheoryAndMLMath.LeakyRelu(-2.0, 0.1), -0.2, "leaky ReLU");
            AssertEqual(InformationTheoryAndMLMath.Tanh(0.0), 0.0, "tanh");
            AssertEqual(InformationTheoryAndMLMath.Gelu(0.0), 0.0, "GELU zero");
            AssertEqual(InformationTheoryAndMLMath.Softplus(0.0), Math.Log(2.0), "softplus zero");
            AssertEqual(InformationTheoryAndMLMath.MeanSquaredError(new[] { 1.0, 2.0 }, new[] { 2.0, 4.0 }), 2.5, "mean squared error");
            AssertEqual(InformationTheoryAndMLMath.MeanAbsoluteError(new[] { 1.0, 2.0 }, new[] { 2.0, 4.0 }), 1.5, "mean absolute error");
            AssertEqual(InformationTheoryAndMLMath.BinaryCrossEntropy(1.0, 0.5), Math.Log(2.0), "binary cross entropy");
            AssertEqual(InformationTheoryAndMLMath.CategoricalCrossEntropy(new[] { 1.0, 0.0 }, new[] { 0.5, 0.5 }), Math.Log(2.0), "categorical cross entropy");
            AssertEqual(InformationTheoryAndMLMath.HingeLoss(1.0, 1.0, 0.2), 0.8, "hinge loss");
        }

        private static void TestClassification()
        {
            int[] actual = { 1, 1, 0, 0, 1, 0 };
            int[] predicted = { 1, 0, 1, 0, 1, 0 };
            ConfusionMatrix confusion = InformationTheoryAndMLMath.BuildConfusionMatrix(actual, predicted, 1);
            Assert(confusion.TruePositive == 2 && confusion.TrueNegative == 2, "confusion matrix true counts");
            Assert(confusion.FalsePositive == 1 && confusion.FalseNegative == 1, "confusion matrix false counts");
            ClassificationMetrics metrics = InformationTheoryAndMLMath.CalculateClassificationMetrics(actual, predicted, 1);
            AssertEqual(metrics.Accuracy, 4.0 / 6.0, "classification accuracy");
            AssertEqual(metrics.Precision, 2.0 / 3.0, "classification precision");
            AssertEqual(metrics.Recall, 2.0 / 3.0, "classification recall");
            AssertEqual(metrics.F1, 2.0 / 3.0, "classification F1");
            AssertEqual(InformationTheoryAndMLMath.RocAuc(new[] { 0.9, 0.8, 0.3, 0.2 }, new[] { 1, 0, 1, 0 }), 0.75, "ROC AUC");
        }

        private static void TestDistances()
        {
            double[] origin = { 0.0, 0.0 };
            double[] point = { 3.0, 4.0 };
            AssertEqual(InformationTheoryAndMLMath.Distance(origin, point, DistanceMetric.Euclidean), 5.0, "Euclidean distance");
            AssertEqual(InformationTheoryAndMLMath.Distance(origin, point, DistanceMetric.Manhattan), 7.0, "Manhattan distance");
            AssertEqual(InformationTheoryAndMLMath.Distance(origin, point, DistanceMetric.Chebyshev), 4.0, "Chebyshev distance");
            AssertEqual(InformationTheoryAndMLMath.Distance(origin, point, DistanceMetric.Minkowski, 2.0), 5.0, "Minkowski distance");
            AssertEqual(InformationTheoryAndMLMath.CosineSimilarity(new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 }), 0.0, "orthogonal cosine similarity");
            AssertEqual(InformationTheoryAndMLMath.Distance(new[] { 1.0, 0.0, 1.0 }, new[] { 0.0, 1.0, 1.0 }, DistanceMetric.Jaccard), 2.0 / 3.0, "binary Jaccard distance");
            AssertEqual(
                InformationTheoryAndMLMath.CosineSimilarity(new[] { 1e300, 1e300 }, new[] { 1e300, 1e300 }),
                1.0,
                "scaled cosine similarity avoids overflow");
            AssertEqual(
                InformationTheoryAndMLMath.JaccardSimilarity(new HashSet<int>(), new HashSet<int>()),
                1.0,
                "Jaccard similarity of two empty sets");
        }

        private static void TestDimensionalityReduction()
        {
            double[,] data = { { 1.0, 2.0 }, { 2.0, 2.0 }, { 3.0, 2.0 }, { 4.0, 2.0 } };
            StandardizationResult standardized = InformationTheoryAndMLMath.Standardize(data);
            AssertEqual(standardized.Mean[0], 2.5, "standardization mean");
            AssertEqual(standardized.Mean[1], 2.0, "constant-column mean");
            AssertEqual(standardized.Scale[1], 1.0, "constant-column scale");
            AssertEqual(standardized.Data[0, 1], 0.0, "constant column standardizes to zero");

            double[,] correlated = { { 1.0, 1.0 }, { 2.0, 2.0 }, { 3.0, 3.0 }, { 4.0, 4.0 } };
            PcaResult pca = InformationTheoryAndMLMath.Pca(correlated, 1);
            AssertEqual(pca.ExplainedVarianceRatio[0], 1.0, "PCA explained variance ratio");
            AssertEqual(Math.Abs(pca.Components[0, 0]), 1.0 / Math.Sqrt(2.0), "PCA first component x");
            AssertEqual(Math.Abs(pca.Components[1, 0]), 1.0 / Math.Sqrt(2.0), "PCA first component y");
            Assert(pca.Transformed.GetLength(0) == 4 && pca.Transformed.GetLength(1) == 1, "PCA projection dimensions");
        }

        private static void TestKMeans()
        {
            double[,] data = { { 0.0, 0.0 }, { 0.0, 1.0 }, { 10.0, 10.0 }, { 10.0, 11.0 } };
            KMeansResult result = InformationTheoryAndMLMath.KMeans(data, 2, 100, 42);
            Assert(result.Centroids.GetLength(0) == 2, "k-means centroid count");
            Assert(result.Inertia < 2.0, "k-means separates well-spaced clusters");
            Assert(result.Assignments[0] == result.Assignments[1], "first cluster grouping");
            Assert(result.Assignments[2] == result.Assignments[3], "second cluster grouping");
            Assert(result.Assignments[0] != result.Assignments[2], "cluster labels differ");

            KMeansResult repeated = InformationTheoryAndMLMath.KMeans(data, 2, 100, 42);
            Assert(result.Inertia == repeated.Inertia, "fixed-seed k-means is reproducible");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentException>(() => InformationTheoryAndMLMath.Entropy(new[] { 0.2, 0.2 }), "probabilities must sum to one");
            AssertThrows<ArgumentException>(() => InformationTheoryAndMLMath.KlDivergence(new[] { 1.0, 0.0 }, new[] { 0.0, 1.0 }), "KL support mismatch");
            AssertThrows<ArgumentException>(() => InformationTheoryAndMLMath.MeanSquaredError(new[] { 1.0 }, Array.Empty<double>()), "mismatched loss arrays");
            AssertThrows<ArgumentOutOfRangeException>(() => InformationTheoryAndMLMath.HingeLoss(1.0, 0.0, 1.0), "hinge labels are signed");
            AssertThrows<InvalidOperationException>(() => InformationTheoryAndMLMath.CosineSimilarity(new[] { 0.0 }, new[] { 1.0 }), "cosine similarity rejects zero vector");
            AssertThrows<ArgumentException>(() => InformationTheoryAndMLMath.Pca(new[,] { { 1.0, 2.0 } }, 1), "PCA requires multiple rows");
            AssertThrows<ArgumentOutOfRangeException>(() => InformationTheoryAndMLMath.KMeans(new[,] { { 1.0 }, { 2.0 } }, 3, 10, 1), "cluster count exceeds samples");
        }
    }
}
