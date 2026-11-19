using System;
using System.Collections.Generic;

namespace OPEN_LRD.Skills
{
    internal enum DistanceMetric
    {
        Euclidean,
        Manhattan,
        Chebyshev,
        Minkowski,
        Cosine,
        Jaccard
    }

    internal sealed record ConfusionMatrix(int TruePositive, int TrueNegative, int FalsePositive, int FalseNegative);

    internal sealed record ClassificationMetrics(
        double Accuracy,
        double Precision,
        double Recall,
        double F1,
        double Specificity,
        double BalancedAccuracy);

    internal sealed record PcaResult(
        double[] Mean,
        double[,] Components,
        double[] ExplainedVariance,
        double[] ExplainedVarianceRatio,
        double[,] Transformed);

    internal sealed record KMeansResult(double[,] Centroids, int[] Assignments, double Inertia, int Iterations);

    internal sealed record StandardizationResult(double[,] Data, double[] Mean, double[] Scale);

    internal static class InformationTheoryAndMLMath
    {
        private const double ProbabilityTolerance = 1e-9;
        private const double CrossEntropyFloor = 1e-15;

        public static double Entropy(double[] probabilities)
        {
            ValidateProbabilityVector(probabilities, nameof(probabilities));
            double result = 0.0;
            foreach (double probability in probabilities)
            {
                if (probability > 0.0)
                {
                    result -= probability * Math.Log2(probability);
                }
            }
            return result;
        }

        public static double JointEntropy(double[,] jointProbabilities)
        {
            ValidateJointDistribution(jointProbabilities, nameof(jointProbabilities));
            double entropy = 0.0;
            foreach (double probability in jointProbabilities)
            {
                if (probability > 0.0)
                {
                    entropy -= probability * Math.Log2(probability);
                }
            }
            return entropy;
        }

        public static double ConditionalEntropy(double[,] jointProbabilities)
        {
            ValidateJointDistribution(jointProbabilities, nameof(jointProbabilities));
            int rows = jointProbabilities.GetLength(0);
            int columns = jointProbabilities.GetLength(1);
            double[] rowMarginals = new double[rows];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    rowMarginals[row] += jointProbabilities[row, column];
                }
            }
            return Math.Max(0.0, JointEntropy(jointProbabilities) - Entropy(rowMarginals));
        }

        public static double MutualInformation(double[,] jointProbabilities)
        {
            ValidateJointDistribution(jointProbabilities, nameof(jointProbabilities));
            int rows = jointProbabilities.GetLength(0);
            int columns = jointProbabilities.GetLength(1);
            double[] rowMarginals = new double[rows];
            double[] columnMarginals = new double[columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    rowMarginals[row] += jointProbabilities[row, column];
                    columnMarginals[column] += jointProbabilities[row, column];
                }
            }
            double result = 0.0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    double probability = jointProbabilities[row, column];
                    if (probability > 0.0)
                    {
                        result += probability * Math.Log2(probability / (rowMarginals[row] * columnMarginals[column]));
                    }
                }
            }
            return Math.Max(0.0, result);
        }

        public static double KlDivergence(double[] first, double[] second)
        {
            ValidateProbabilityVector(first, nameof(first));
            ValidateProbabilityVector(second, nameof(second));
            EnsureSameLength(first, second, nameof(second));
            double result = 0.0;
            for (int index = 0; index < first.Length; index++)
            {
                if (first[index] == 0.0)
                {
                    continue;
                }
                if (second[index] == 0.0)
                {
                    throw new ArgumentException("KL divergence is infinite when the first distribution assigns mass where the second assigns zero.", nameof(second));
                }
                result += first[index] * Math.Log2(first[index] / second[index]);
            }
            return Math.Max(0.0, result);
        }

        public static double JensenShannonDivergence(double[] first, double[] second)
        {
            ValidateProbabilityVector(first, nameof(first));
            ValidateProbabilityVector(second, nameof(second));
            EnsureSameLength(first, second, nameof(second));
            double[] midpoint = new double[first.Length];
            for (int index = 0; index < midpoint.Length; index++)
            {
                midpoint[index] = 0.5 * (first[index] + second[index]);
            }
            return 0.5 * KlDivergence(first, midpoint) + 0.5 * KlDivergence(second, midpoint);
        }

        public static double CrossEntropy(double[] actual, double[] predicted)
        {
            ValidateProbabilityVector(actual, nameof(actual));
            ValidateProbabilityVector(predicted, nameof(predicted));
            EnsureSameLength(actual, predicted, nameof(predicted));
            double result = 0.0;
            for (int index = 0; index < actual.Length; index++)
            {
                if (actual[index] > 0.0)
                {
                    result -= actual[index] * Math.Log2(Math.Max(predicted[index], CrossEntropyFloor));
                }
            }
            return result;
        }

        public static double Sigmoid(double value)
        {
            EnsureFinite(value, nameof(value));
            if (value >= 0.0)
            {
                double exponential = Math.Exp(-value);
                return 1.0 / (1.0 + exponential);
            }
            double negativeExponential = Math.Exp(value);
            return negativeExponential / (1.0 + negativeExponential);
        }

        public static double[] Sigmoid(double[] values)
        {
            EnsureVector(values, nameof(values));
            double[] result = new double[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = Sigmoid(values[index]);
            }
            return result;
        }

        public static double[] Softmax(double[] values)
        {
            EnsureVector(values, nameof(values));
            double maximum = values[0];
            foreach (double value in values)
            {
                maximum = Math.Max(maximum, value);
            }
            double[] result = new double[values.Length];
            double sum = 0.0;
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = Math.Exp(values[index] - maximum);
                sum += result[index];
            }
            for (int index = 0; index < result.Length; index++)
            {
                result[index] /= sum;
            }
            return result;
        }

        public static double Relu(double value)
        {
            EnsureFinite(value, nameof(value));
            return Math.Max(0.0, value);
        }

        public static double LeakyRelu(double value, double alpha)
        {
            EnsureFinite(value, nameof(value));
            if (!double.IsFinite(alpha) || alpha < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(alpha));
            }
            return value >= 0.0 ? value : alpha * value;
        }

        public static double Tanh(double value)
        {
            EnsureFinite(value, nameof(value));
            return Math.Tanh(value);
        }

        public static double Gelu(double value)
        {
            EnsureFinite(value, nameof(value));
            double scale = Math.Sqrt(2.0 / Math.PI);
            return 0.5 * value * (1.0 + Math.Tanh(scale * (value + 0.044715 * value * value * value)));
        }

        public static double Softplus(double value)
        {
            EnsureFinite(value, nameof(value));
            return Math.Max(value, 0.0) + Math.Log(1.0 + Math.Exp(-Math.Abs(value)));
        }

        public static double MeanSquaredError(double[] actual, double[] predicted)
        {
            EnsureSameNonEmptyLength(actual, predicted, nameof(actual), nameof(predicted));
            double sum = 0.0;
            for (int index = 0; index < actual.Length; index++)
            {
                double error = actual[index] - predicted[index];
                sum += error * error;
            }
            return sum / actual.Length;
        }

        public static double MeanAbsoluteError(double[] actual, double[] predicted)
        {
            EnsureSameNonEmptyLength(actual, predicted, nameof(actual), nameof(predicted));
            double sum = 0.0;
            for (int index = 0; index < actual.Length; index++)
            {
                sum += Math.Abs(actual[index] - predicted[index]);
            }
            return sum / actual.Length;
        }

        public static double BinaryCrossEntropy(double actual, double predicted)
        {
            EnsureProbability(actual, nameof(actual));
            EnsureProbability(predicted, nameof(predicted));
            double bounded = Math.Clamp(predicted, CrossEntropyFloor, 1.0 - CrossEntropyFloor);
            return -(actual * Math.Log(bounded) + (1.0 - actual) * Math.Log(1.0 - bounded));
        }

        public static double CategoricalCrossEntropy(double[] actual, double[] predicted)
        {
            ValidateProbabilityVector(actual, nameof(actual));
            ValidateProbabilityVector(predicted, nameof(predicted));
            EnsureSameLength(actual, predicted, nameof(predicted));
            double result = 0.0;
            for (int index = 0; index < actual.Length; index++)
            {
                if (actual[index] > 0.0)
                {
                    result -= actual[index] * Math.Log(Math.Max(predicted[index], CrossEntropyFloor));
                }
            }
            return result;
        }

        public static double HingeLoss(double margin, double actual, double predicted)
        {
            EnsurePositiveFinite(margin, nameof(margin));
            EnsureFinite(actual, nameof(actual));
            EnsureFinite(predicted, nameof(predicted));
            if (actual != -1.0 && actual != 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(actual), "Hinge labels must be -1 or +1.");
            }
            return Math.Max(0.0, margin - actual * predicted);
        }

        public static ConfusionMatrix BuildConfusionMatrix(int[] actual, int[] predicted, int positiveLabel)
        {
            EnsureSameNonEmptyLength(actual, predicted, nameof(actual), nameof(predicted));
            int truePositive = 0;
            int trueNegative = 0;
            int falsePositive = 0;
            int falseNegative = 0;
            for (int index = 0; index < actual.Length; index++)
            {
                bool actualPositive = actual[index] == positiveLabel;
                bool predictedPositive = predicted[index] == positiveLabel;
                if (actualPositive && predictedPositive)
                {
                    truePositive++;
                }
                else if (actualPositive)
                {
                    falseNegative++;
                }
                else if (predictedPositive)
                {
                    falsePositive++;
                }
                else
                {
                    trueNegative++;
                }
            }
            return new ConfusionMatrix(truePositive, trueNegative, falsePositive, falseNegative);
        }

        public static ClassificationMetrics CalculateClassificationMetrics(int[] actual, int[] predicted, int positiveLabel)
        {
            ConfusionMatrix matrix = BuildConfusionMatrix(actual, predicted, positiveLabel);
            double accuracy = (double)(matrix.TruePositive + matrix.TrueNegative) / actual.Length;
            double precision = SafeRatio(matrix.TruePositive, matrix.TruePositive + matrix.FalsePositive);
            double recall = SafeRatio(matrix.TruePositive, matrix.TruePositive + matrix.FalseNegative);
            double specificity = SafeRatio(matrix.TrueNegative, matrix.TrueNegative + matrix.FalsePositive);
            double f1 = precision + recall == 0.0 ? 0.0 : 2.0 * precision * recall / (precision + recall);
            return new ClassificationMetrics(accuracy, precision, recall, f1, specificity, 0.5 * (recall + specificity));
        }

        public static double RocAuc(double[] scores, int[] labels)
        {
            EnsureSameNonEmptyLength(scores, labels, nameof(scores), nameof(labels));
            int positives = 0;
            foreach (int label in labels)
            {
                if (label != 0 && label != 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(labels), "ROC labels must be zero or one.");
                }
                positives += label;
            }
            int negatives = labels.Length - positives;
            if (positives == 0 || negatives == 0)
            {
                throw new ArgumentException("ROC AUC requires both positive and negative labels.", nameof(labels));
            }
            int[] order = new int[scores.Length];
            for (int index = 0; index < order.Length; index++)
            {
                order[index] = index;
            }
            Array.Sort(order, (left, right) => scores[left].CompareTo(scores[right]));
            double positiveRankSum = 0.0;
            int start = 0;
            while (start < order.Length)
            {
                int end = start + 1;
                while (end < order.Length && scores[order[end]] == scores[order[start]])
                {
                    end++;
                }
                double averageRank = ((start + 1.0) + end) * 0.5;
                for (int index = start; index < end; index++)
                {
                    if (labels[order[index]] == 1)
                    {
                        positiveRankSum += averageRank;
                    }
                }
                start = end;
            }
            return (positiveRankSum - positives * (positives + 1.0) * 0.5) / (positives * (double)negatives);
        }

        public static double Distance(double[] first, double[] second, DistanceMetric metric, double p = 2.0)
        {
            EnsureSameNonEmptyLength(first, second, nameof(first), nameof(second));
            if (!Enum.IsDefined(metric))
            {
                throw new ArgumentOutOfRangeException(nameof(metric));
            }
            switch (metric)
            {
                case DistanceMetric.Euclidean:
                    return StablePDistance(first, second, 2.0);
                case DistanceMetric.Manhattan:
                    return StablePDistance(first, second, 1.0);
                case DistanceMetric.Chebyshev:
                {
                    double maximum = 0.0;
                    for (int index = 0; index < first.Length; index++)
                    {
                        maximum = Math.Max(maximum, Math.Abs(first[index] - second[index]));
                    }
                    return maximum;
                }
                case DistanceMetric.Minkowski:
                    EnsurePositiveFinite(p, nameof(p));
                    return StablePDistance(first, second, p);
                case DistanceMetric.Cosine:
                    return 1.0 - CosineSimilarity(first, second);
                case DistanceMetric.Jaccard:
                    return 1.0 - JaccardSimilarity(ToPresenceSet(first), ToPresenceSet(second));
                default:
                    throw new ArgumentOutOfRangeException(nameof(metric));
            }
        }

        public static double CosineSimilarity(double[] first, double[] second)
        {
            EnsureSameNonEmptyLength(first, second, nameof(first), nameof(second));
            double firstScale = 0.0;
            double secondScale = 0.0;
            for (int index = 0; index < first.Length; index++)
            {
                firstScale = Math.Max(firstScale, Math.Abs(first[index]));
                secondScale = Math.Max(secondScale, Math.Abs(second[index]));
            }
            if (firstScale == 0.0 || secondScale == 0.0)
            {
                throw new InvalidOperationException("Cosine similarity is undefined for a zero vector.");
            }
            double dot = 0.0;
            double firstNorm = 0.0;
            double secondNorm = 0.0;
            for (int index = 0; index < first.Length; index++)
            {
                double scaledFirst = first[index] / firstScale;
                double scaledSecond = second[index] / secondScale;
                dot += scaledFirst * scaledSecond;
                firstNorm += scaledFirst * scaledFirst;
                secondNorm += scaledSecond * scaledSecond;
            }
            return Math.Clamp(dot / Math.Sqrt(firstNorm * secondNorm), -1.0, 1.0);
        }

        public static double JaccardSimilarity<T>(IReadOnlySet<T> first, IReadOnlySet<T> second)
            where T : notnull
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }
            if (first.Count == 0 && second.Count == 0)
            {
                return 1.0;
            }
            int intersection = 0;
            foreach (T value in first)
            {
                if (second.Contains(value))
                {
                    intersection++;
                }
            }
            int union = first.Count + second.Count - intersection;
            return (double)intersection / union;
        }

        public static StandardizationResult Standardize(double[,] data)
        {
            ValidateDataMatrix(data, minimumRows: 1);
            int rows = data.GetLength(0);
            int columns = data.GetLength(1);
            double[] mean = new double[columns];
            double[] scale = new double[columns];
            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows; row++)
                {
                    mean[column] += data[row, column] / rows;
                }
                double sumSquares = 0.0;
                for (int row = 0; row < rows; row++)
                {
                    double difference = data[row, column] - mean[column];
                    sumSquares += difference * difference;
                }
                scale[column] = Math.Sqrt(sumSquares / rows);
                if (scale[column] == 0.0)
                {
                    scale[column] = 1.0;
                }
            }
            double[,] standardized = new double[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    standardized[row, column] = (data[row, column] - mean[column]) / scale[column];
                }
            }
            return new StandardizationResult(standardized, mean, scale);
        }

        public static PcaResult Pca(double[,] data, int components)
        {
            ValidateDataMatrix(data, minimumRows: 2);
            int rows = data.GetLength(0);
            int columns = data.GetLength(1);
            if (components < 1 || components > columns)
            {
                throw new ArgumentOutOfRangeException(nameof(components), "Component count must be in [1, feature count].");
            }
            double[] mean = new double[columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    mean[column] += data[row, column] / rows;
                }
            }
            double[,] centered = new double[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    centered[row, column] = data[row, column] - mean[column];
                }
            }
            double[,] covariance = new double[columns, columns];
            for (int first = 0; first < columns; first++)
            {
                for (int second = first; second < columns; second++)
                {
                    double sum = 0.0;
                    for (int row = 0; row < rows; row++)
                    {
                        sum += centered[row, first] * centered[row, second];
                    }
                    covariance[first, second] = sum / (rows - 1.0);
                    covariance[second, first] = covariance[first, second];
                }
            }
            SymmetricEigenResult eigen = LinearAlgebraAdvanced.SymmetricEigenDecomposition(covariance);
            int[] order = new int[eigen.Eigenvalues.Length];
            for (int index = 0; index < order.Length; index++)
            {
                order[index] = index;
            }
            Array.Sort(order, (left, right) => eigen.Eigenvalues[right].CompareTo(eigen.Eigenvalues[left]));
            double[] explained = new double[components];
            double[] ratios = new double[components];
            double[,] directions = new double[columns, components];
            double totalVariance = 0.0;
            foreach (double value in eigen.Eigenvalues)
            {
                totalVariance += Math.Max(0.0, value);
            }
            for (int component = 0; component < components; component++)
            {
                int source = order[component];
                explained[component] = Math.Max(0.0, eigen.Eigenvalues[source]);
                ratios[component] = totalVariance == 0.0 ? 0.0 : explained[component] / totalVariance;
                for (int feature = 0; feature < columns; feature++)
                {
                    directions[feature, component] = eigen.Eigenvectors[feature, source];
                }
            }
            double[,] transformed = new double[rows, components];
            for (int row = 0; row < rows; row++)
            {
                for (int component = 0; component < components; component++)
                {
                    for (int feature = 0; feature < columns; feature++)
                    {
                        transformed[row, component] += centered[row, feature] * directions[feature, component];
                    }
                }
            }
            return new PcaResult(mean, directions, explained, ratios, transformed);
        }

        public static KMeansResult KMeans(double[,] data, int clusters, int maxIterations, int seed)
        {
            ValidateDataMatrix(data, minimumRows: 1);
            int samples = data.GetLength(0);
            int dimensions = data.GetLength(1);
            if (clusters < 1 || clusters > samples)
            {
                throw new ArgumentOutOfRangeException(nameof(clusters), "Cluster count must be in [1, sample count].");
            }
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations));
            }
            Random random = new Random(seed);
            double[,] centroids = InitializeKMeansPlusPlus(data, clusters, random);
            int[] assignments = new int[samples];
            Array.Fill(assignments, -1);
            int iterations = 0;
            for (; iterations < maxIterations; iterations++)
            {
                bool changed = AssignClusters(data, centroids, assignments);
                double[,] sums = new double[clusters, dimensions];
                int[] counts = new int[clusters];
                for (int row = 0; row < samples; row++)
                {
                    int cluster = assignments[row];
                    counts[cluster]++;
                    for (int column = 0; column < dimensions; column++)
                    {
                        sums[cluster, column] += data[row, column];
                    }
                }
                for (int cluster = 0; cluster < clusters; cluster++)
                {
                    if (counts[cluster] == 0)
                    {
                        int farthest = FarthestSample(data, centroids, assignments, counts);
                        int source = assignments[farthest];
                        counts[source]--;
                        counts[cluster] = 1;
                        assignments[farthest] = cluster;
                        for (int column = 0; column < dimensions; column++)
                        {
                            sums[source, column] -= data[farthest, column];
                            sums[cluster, column] = data[farthest, column];
                        }
                        changed = true;
                    }
                }
                for (int cluster = 0; cluster < clusters; cluster++)
                {
                    for (int column = 0; column < dimensions; column++)
                    {
                        centroids[cluster, column] = sums[cluster, column] / counts[cluster];
                    }
                }
                if (!changed)
                {
                    iterations++;
                    break;
                }
            }
            double inertia = 0.0;
            for (int row = 0; row < samples; row++)
            {
                inertia += SquaredDistance(data, row, centroids, assignments[row]);
            }
            return new KMeansResult(centroids, assignments, inertia, iterations);
        }

        private static double StablePDistance(double[] first, double[] second, double p)
        {
            double maximum = 0.0;
            for (int index = 0; index < first.Length; index++)
            {
                maximum = Math.Max(maximum, Math.Abs(first[index] - second[index]));
            }
            if (maximum == 0.0)
            {
                return 0.0;
            }
            if (double.IsPositiveInfinity(maximum))
            {
                return double.PositiveInfinity;
            }
            double sum = 0.0;
            for (int index = 0; index < first.Length; index++)
            {
                sum += Math.Pow(Math.Abs(first[index] - second[index]) / maximum, p);
            }
            return maximum * Math.Pow(sum, 1.0 / p);
        }

        private static double[,] InitializeKMeansPlusPlus(double[,] data, int clusters, Random random)
        {
            int samples = data.GetLength(0);
            int dimensions = data.GetLength(1);
            double[,] centroids = new double[clusters, dimensions];
            bool[] selected = new bool[samples];
            int first = random.Next(samples);
            selected[first] = true;
            CopySample(data, first, centroids, 0);
            double[] minimumDistances = new double[samples];
            for (int cluster = 1; cluster < clusters; cluster++)
            {
                double total = 0.0;
                for (int sample = 0; sample < samples; sample++)
                {
                    double distance = SquaredDistance(data, sample, centroids, cluster - 1);
                    if (cluster > 1)
                    {
                        distance = Math.Min(minimumDistances[sample], distance);
                    }
                    minimumDistances[sample] = distance;
                    total += distance;
                }
                int chosen = -1;
                if (total > 0.0)
                {
                    double target = random.NextDouble() * total;
                    double cumulative = 0.0;
                    for (int sample = 0; sample < samples; sample++)
                    {
                        cumulative += minimumDistances[sample];
                        if (cumulative >= target && !selected[sample])
                        {
                            chosen = sample;
                            break;
                        }
                    }
                }
                if (chosen < 0)
                {
                    for (int sample = 0; sample < samples; sample++)
                    {
                        if (!selected[sample])
                        {
                            chosen = sample;
                            break;
                        }
                    }
                }
                selected[chosen] = true;
                CopySample(data, chosen, centroids, cluster);
            }
            return centroids;
        }

        private static bool AssignClusters(double[,] data, double[,] centroids, int[] assignments)
        {
            bool changed = false;
            for (int row = 0; row < data.GetLength(0); row++)
            {
                int best = 0;
                double bestDistance = SquaredDistance(data, row, centroids, 0);
                for (int cluster = 1; cluster < centroids.GetLength(0); cluster++)
                {
                    double distance = SquaredDistance(data, row, centroids, cluster);
                    if (distance < bestDistance)
                    {
                        best = cluster;
                        bestDistance = distance;
                    }
                }
                if (assignments[row] != best)
                {
                    changed = true;
                    assignments[row] = best;
                }
            }
            return changed;
        }

        private static int FarthestSample(double[,] data, double[,] centroids, int[] assignments, int[] counts)
        {
            int bestSample = 0;
            double bestDistance = double.NegativeInfinity;
            for (int row = 0; row < data.GetLength(0); row++)
            {
                if (counts[assignments[row]] <= 1)
                {
                    continue;
                }
                double distance = SquaredDistance(data, row, centroids, assignments[row]);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestSample = row;
                }
            }
            return bestSample;
        }

        private static double SquaredDistance(double[,] data, int row, double[,] centroids, int cluster)
        {
            double sum = 0.0;
            for (int column = 0; column < data.GetLength(1); column++)
            {
                double difference = data[row, column] - centroids[cluster, column];
                sum += difference * difference;
            }
            return sum;
        }

        private static void CopySample(double[,] data, int sample, double[,] centroids, int cluster)
        {
            for (int column = 0; column < data.GetLength(1); column++)
            {
                centroids[cluster, column] = data[sample, column];
            }
        }

        private static void ValidateProbabilityVector(double[] probabilities, string parameterName)
        {
            EnsureVector(probabilities, parameterName);
            double total = 0.0;
            foreach (double probability in probabilities)
            {
                EnsureProbability(probability, parameterName);
                total += probability;
            }
            if (Math.Abs(total - 1.0) > ProbabilityTolerance)
            {
                throw new ArgumentException("Probability values must sum to one.", parameterName);
            }
        }

        private static void ValidateJointDistribution(double[,] probabilities, string parameterName)
        {
            if (probabilities == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (probabilities.GetLength(0) == 0 || probabilities.GetLength(1) == 0)
            {
                throw new ArgumentException("Joint distribution must have at least one row and column.", parameterName);
            }
            double total = 0.0;
            foreach (double probability in probabilities)
            {
                EnsureProbability(probability, parameterName);
                total += probability;
            }
            if (Math.Abs(total - 1.0) > ProbabilityTolerance)
            {
                throw new ArgumentException("Joint probabilities must sum to one.", parameterName);
            }
        }

        private static void EnsureVector(double[] values, string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (values.Length == 0)
            {
                throw new ArgumentException("Vector must not be empty.", parameterName);
            }
            foreach (double value in values)
            {
                EnsureFinite(value, parameterName);
            }
        }

        private static void EnsureSameLength(double[] first, double[] second, string secondName)
        {
            if (first.Length != second.Length)
            {
                throw new ArgumentException("Vectors must have equal lengths.", secondName);
            }
        }

        private static void EnsureSameNonEmptyLength(double[] first, double[] second, string firstName, string secondName)
        {
            EnsureVector(first, firstName);
            EnsureVector(second, secondName);
            EnsureSameLength(first, second, secondName);
        }

        private static void EnsureSameNonEmptyLength(int[] first, int[] second, string firstName, string secondName)
        {
            if (first == null)
            {
                throw new ArgumentNullException(firstName);
            }
            if (second == null)
            {
                throw new ArgumentNullException(secondName);
            }
            if (first.Length == 0 || first.Length != second.Length)
            {
                throw new ArgumentException("Arrays must have equal non-empty lengths.");
            }
        }

        private static void EnsureSameNonEmptyLength(double[] scores, int[] labels, string scoresName, string labelsName)
        {
            EnsureVector(scores, scoresName);
            if (labels == null)
            {
                throw new ArgumentNullException(labelsName);
            }
            if (labels.Length == 0 || labels.Length != scores.Length)
            {
                throw new ArgumentException("Score and label arrays must have equal non-empty lengths.");
            }
        }

        private static void ValidateDataMatrix(double[,] data, int minimumRows)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.GetLength(0) < minimumRows || data.GetLength(1) == 0)
            {
                throw new ArgumentException("Data matrix must have enough rows and at least one column.", nameof(data));
            }
            for (int row = 0; row < data.GetLength(0); row++)
            {
                for (int column = 0; column < data.GetLength(1); column++)
                {
                    EnsureFinite(data[row, column], nameof(data));
                }
            }
        }

        private static HashSet<int> ToPresenceSet(double[] vector)
        {
            HashSet<int> result = new HashSet<int>();
            for (int index = 0; index < vector.Length; index++)
            {
                if (vector[index] != 0.0)
                {
                    result.Add(index);
                }
            }
            return result;
        }

        private static double SafeRatio(int numerator, int denominator)
        {
            return denominator == 0 ? 0.0 : (double)numerator / denominator;
        }

        private static void EnsureProbability(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Probability must be in [0, 1].");
            }
        }

        private static void EnsurePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
            }
        }

        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }
    }
}
