using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    
    internal static class Math_1
    {
        /// <summary>
        /// Computes the sum of two 32-bit signed integers.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int AddInt(int a, int b)
        {
            return a + b;
        }

        /// <summary>
        /// Computes the sum of all integers in the specified list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int AddRange(List<int> numbers)
        {
            if (numbers.Count == 0)
            {
                return 0;
            }
            return CalculateSum(numbers);
        }

        /// <summary>
        /// Computes the difference between two integers (a - b).
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int SubtractInt(int a, int b)
        {
            return a - b;
        }

        /// <summary>
        /// Computes the product of two integers.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int MultiplyInt(int a, int b)
        {
            return a * b;
        }

        /// <summary>
        /// Divides the first integer by the second integer (a / b).
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int DivideInt(int a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return a / b;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int ModulusInt(int a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Cannot perform modulus by zero.");
            }
            return a % b;
        }

        /// <summary>
        /// Computes the sum of two double-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double AddDouble(double a, double b)
        {
            return a + b;
        }

        /// <summary>
        /// Computes the sum of all double-precision floating-point values in the specified list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double AddRangeDouble(List<double> numbers)
        {
            if (numbers.Count == 0)
            {
                return 0.0;
            }
            return CalculateSum(numbers);
        }

        /// <summary>
        /// Computes the difference between two double-precision floating-point values (a - b).
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double SubtractDouble(double a, double b)
        {
            return a - b;
        }

        /// <summary>
        /// Computes the product of two double-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double MultiplyDouble(double a, double b)
        {
            return a * b;
        }

        /// <summary>
        /// Divides the first double-precision value by the second (a / b).
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double DivideDouble(double a, double b)
        {
            if (b == 0.0)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return a / b;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double ModulusDouble(double a, double b)
        {
            if (b == 0.0)
            {
                throw new DivideByZeroException("Cannot perform modulus by zero.");
            }
            return a % b;
        }

        /// <summary>
        /// Computes the sum of two single-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static float AddFloat(float a, float b)
        {
            return a + b;
        }

        /// <summary>
        /// Computes the sum of all single-precision floating-point values in the specified list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float AddRangeFloat(List<float> numbers)
        {
            if (numbers.Count == 0)
            {
                return 0.0f;
            }
            return CalculateSum(numbers);
        }

        /// <summary>
        /// Computes the difference between two single-precision floating-point values (a - b).
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static float SubtractFloat(float a, float b)
        {
            return a - b;
        }
        /// <summary>
        /// Computes the product of two single-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static float MultiplyFloat(float a, float b)
        {
            return a * b;
        }
        /// <summary>
        /// Divides the first single-precision value by the second (a / b).
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static float DivideFloat(float a, float b)
        {
            if (b == 0.0f)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return a / b;
        }
        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static float ModulusFloat(float a, float b)
        {
            if (b == 0.0f)
            {
                throw new DivideByZeroException("Cannot perform modulus by zero.");
            }
            return a % b;
        }
        /// <summary>
        /// Computes the arithmetic mean of a sequence of single-precision values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateAverage(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            return CalculateSum(numbers) / numbers.Count;
        }

        /// <summary>
        /// Computes the standard deviation of a sequence of single-precision values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateStandardDeviation(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            return (float)Math.Sqrt(CalculateVariance(numbers));
        }
        
        /// <summary>
        /// Computes the variance of a sequence of single-precision values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateVariance(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float average = CalculateAverage(numbers);
            float sumOfSquares = 0.0f;
            foreach (float number in numbers)
            {
                sumOfSquares += (number - average) * (number - average);
            }
            return sumOfSquares / numbers.Count;
        }

        /// <summary>
        /// Computes the standard score (z-score) of a value relative to the sample distribution.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateZScore(float value, List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float average = CalculateAverage(numbers);
            float standardDeviation = CalculateStandardDeviation(numbers);
            if (standardDeviation == 0.0f)
            {
                throw new InvalidOperationException("Standard deviation is zero, cannot calculate Z-score.");
            }
            return (value - average) / standardDeviation;
        }

        /// <summary>
        /// Computes the percentile rank or percentile value for the dataset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculatePercentile(float value, List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int count = 0;
            foreach (float number in numbers)
            {
                if (number < value)
                {
                    count++;
                }
            }
            return (float)count / numbers.Count * 100.0f;
        }

        /// <summary>
        /// Computes the median of a sequence of single-precision values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateMedian(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            List<float> sortedNumbers = new List<float>(numbers);
            sortedNumbers.Sort();
            int count = sortedNumbers.Count;
            if (count % 2 == 0)
            {
                // Even number of elements
                return (sortedNumbers[count / 2 - 1] + sortedNumbers[count / 2]) / 2.0f;
            }
            else
            {
                // Odd number of elements
                return sortedNumbers[count / 2];
            }
        }

        /// <summary>
        /// Computes the mode (most frequent value) of a sequence of single-precision values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateMode(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            Dictionary<float, int> frequencyMap = new Dictionary<float, int>();
            foreach (float number in numbers)
            {
                if (frequencyMap.ContainsKey(number))
                {
                    frequencyMap[number]++;
                }
                else
                {
                    frequencyMap[number] = 1;
                }
            }
            int maxFrequency = 0;
            float mode = float.NaN;
            foreach (var kvp in frequencyMap)
            {
                if (kvp.Value > maxFrequency)
                {
                    maxFrequency = kvp.Value;
                    mode = kvp.Key;
                }
            }
            return mode;
        }

        /// <summary>
        /// Computes the difference between the maximum and minimum values in the dataset.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateRange(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (float number in numbers)
            {
                if (number < min)
                {
                    min = number;
                }
                if (number > max)
                {
                    max = number;
                }
            }
            return max - min;
        }

        /// <summary>
        /// Computes the relative standard deviation (ratio of standard deviation to mean).
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateCoefficientOfVariation(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float average = CalculateAverage(numbers);
            float standardDeviation = CalculateStandardDeviation(numbers);
            if (average == 0.0f)
            {
                throw new InvalidOperationException("Average is zero, cannot calculate coefficient of variation.");
            }
            return standardDeviation / average;
        }

        /// <summary>
        /// Computes the skewness measuring asymmetry of the data distribution.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateSkewness(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float average = CalculateAverage(numbers);
            float standardDeviation = CalculateStandardDeviation(numbers);
            if (standardDeviation == 0.0f)
            {
                throw new InvalidOperationException("Standard deviation is zero, cannot calculate skewness.");
            }
            float sumCubedDeviations = 0.0f;
            foreach (float number in numbers)
            {
                sumCubedDeviations += (float)Math.Pow(number - average, 3);
            }
            return sumCubedDeviations / (numbers.Count * (float)Math.Pow(standardDeviation, 3));
        }

        /// <summary>
        /// Computes the kurtosis measuring tail weight of the data distribution.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateKurtosis(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float average = CalculateAverage(numbers);
            float standardDeviation = CalculateStandardDeviation(numbers);
            if (standardDeviation == 0.0f)
            {
                throw new InvalidOperationException("Standard deviation is zero, cannot calculate kurtosis.");
            }
            float sumQuarticDeviations = 0.0f;
            foreach (float number in numbers)
            {
                sumQuarticDeviations += (float)Math.Pow(number - average, 4);
            }
            return sumQuarticDeviations / (numbers.Count * (float)Math.Pow(standardDeviation, 4)) - 3.0f; // Excess kurtosis
        }

        /// <summary>
        /// Computes the sample or population covariance between two paired series.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The result.</returns>
        public static float CalculateCovariance(List<float> x, List<float> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float averageX = CalculateAverage(x);
            float averageY = CalculateAverage(y);
            float covariance = 0.0f;
            for (int i = 0; i < x.Count; i++)
            {
                covariance += (x[i] - averageX) * (y[i] - averageY);
            }
            return covariance / x.Count;
        }

        /// <summary>
        /// Computes the Pearson linear correlation coefficient between two paired series.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The result.</returns>
        public static float CalculateCorrelationCoefficient(List<float> x, List<float> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float covariance = CalculateCovariance(x, y);
            float standardDeviationX = CalculateStandardDeviation(x);
            float standardDeviationY = CalculateStandardDeviation(y);
            if (standardDeviationX == 0.0f || standardDeviationY == 0.0f)
            {
                throw new InvalidOperationException("Standard deviation is zero for one of the lists, cannot calculate correlation coefficient.");
            }
            return covariance / (standardDeviationX * standardDeviationY);
        }

        /// <summary>
        /// Computes the ordinary least-squares slope of a linear regression line.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The result.</returns>
        public static float CalculateLinearRegressionSlope(List<float> x, List<float> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float covariance = CalculateCovariance(x, y);
            float varianceX = CalculateVariance(x);
            if (varianceX == 0.0f)
            {
                throw new InvalidOperationException("Variance of x is zero, cannot calculate linear regression slope.");
            }
            return covariance / varianceX;
        }

        /// <summary>
        /// Computes the y-intercept of an ordinary least-squares linear regression line.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The result.</returns>
        public static float CalculateLinearRegressionIntercept(List<float> x, List<float> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float averageX = CalculateAverage(x);
            float averageY = CalculateAverage(y);
            float slope = CalculateLinearRegressionSlope(x, y);
            return averageY - slope * averageX;
        }

        /// <summary>
        /// Predicts the dependent response at x using a fitted linear regression model.
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="xValues">The x values.</param>
        /// <param name="yValues">The y values.</param>
        /// <returns>The result.</returns>
        public static float CalculateLinearRegressionPredict(float x, List<float> xValues, List<float> yValues)
        {
            if (xValues == null || yValues == null || xValues.Count != yValues.Count || xValues.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float slope = CalculateLinearRegressionSlope(xValues, yValues);
            float averageX = CalculateAverage(xValues);
            float averageY = CalculateAverage(yValues);
            float intercept = averageY - slope * averageX;
            return slope * x + intercept;
        }

        /// <summary>
        /// Computes the exponential moving average using the specified smoothing factor.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <param name="smoothingFactor">The smoothing factor.</param>
        /// <returns>The result.</returns>
        public static float CalculateExponentialMovingAverage(List<float> numbers, float smoothingFactor)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            if (smoothingFactor <= 0.0f || smoothingFactor >= 1.0f)
            {
                throw new ArgumentException("Smoothing factor must be between 0 and 1.");
            }
            float ema = numbers[0]; // Start with the first number as the initial EMA
            for (int i = 1; i < numbers.Count; i++)
            {
                ema = (numbers[i] * smoothingFactor) + (ema * (1 - smoothingFactor));
            }
            return ema;
        }

        /// <summary>
        /// Computes the weighted arithmetic mean of values using corresponding weights.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <param name="weights">The weights.</param>
        /// <returns>The result.</returns>
        public static float CalculateWeightedAverage(List<float> numbers, List<float> weights)
        {
            if (numbers == null || weights == null || numbers.Count != weights.Count || numbers.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float weightedSum = 0.0f;
            float totalWeight = 0.0f;
            for (int i = 0; i < numbers.Count; i++)
            {
                weightedSum += numbers[i] * weights[i];
                totalWeight += weights[i];
            }
            if (totalWeight == 0.0f)
            {
                throw new InvalidOperationException("Total weight is zero, cannot calculate weighted average.");
            }
            return weightedSum / totalWeight;
        }

        /// <summary>
        /// Computes the geometric mean of a set of positive values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateGeometricMean(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            foreach (float number in numbers)
            {
                if (number <= 0.0f)
                {
                    throw new ArgumentException("All numbers must be positive to calculate geometric mean.");
                }
            }
            float product = CalculateProduct(numbers);
            return (float)Math.Pow(product, 1.0f / numbers.Count);
        }

        /// <summary>
        /// Computes the harmonic mean of a set of non-zero values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateHarmonicMean(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float sumOfReciprocals = 0.0f;
            foreach (float number in numbers)
            {
                if (number <= 0.0f)
                {
                    throw new ArgumentException("All numbers must be positive to calculate harmonic mean.");
                }
                sumOfReciprocals += 1.0f / number;
            }
            return numbers.Count / sumOfReciprocals;
        }

        /// <summary>
        /// Computes the quadratic mean (root-mean-square) of a sequence of values.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateRootMeanSquare(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float sumOfSquares = 0.0f;
            foreach (float number in numbers)
            {
                sumOfSquares += number * number;
            }
            return (float)Math.Sqrt(sumOfSquares / numbers.Count);
        }

        /// <summary>
        /// Computes the mean absolute deviation around the central tendency.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateMeanAbsoluteDeviation(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float average = CalculateAverage(numbers);
            float sumOfAbsoluteDeviations = 0.0f;
            foreach (float number in numbers)
            {
                sumOfAbsoluteDeviations += Math.Abs(number - average);
            }
            return sumOfAbsoluteDeviations / numbers.Count;
        }

        /// <summary>
        /// Computes the interquartile range (Q3 - Q1) of the dataset.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateInterquartileRange(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            List<float> sortedNumbers = new List<float>(numbers);
            sortedNumbers.Sort();
            int count = sortedNumbers.Count;
            float q1 = CalculateMedian(sortedNumbers.GetRange(0, count / 2));
            float q3 = CalculateMedian(sortedNumbers.GetRange((count + 1) / 2, count / 2));
            return q3 - q1;
        }

        /// <summary>
        /// Computes the mean absolute percentage error (MAPE) between actual and predicted values.
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <returns>The result.</returns>
        public static float CalculateMeanAbsolutePercentageError(List<float> actual, List<float> predicted)
        {
            if (actual == null || predicted == null || actual.Count != predicted.Count || actual.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float sumOfPercentageErrors = 0.0f;
            for (int i = 0; i < actual.Count; i++)
            {
                if (actual[i] == 0.0f)
                {
                    throw new InvalidOperationException("Actual value is zero, cannot calculate MAPE.");
                }
                sumOfPercentageErrors += Math.Abs((actual[i] - predicted[i]) / actual[i]);
            }
            return (sumOfPercentageErrors / actual.Count) * 100.0f;
        }

        /// <summary>
        /// Computes the mean squared error (MSE) between actual and predicted values.
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <returns>The result.</returns>
        public static float CalculateMeanSquaredError(List<float> actual, List<float> predicted)
        {
            if (actual == null || predicted == null || actual.Count != predicted.Count || actual.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float sumOfSquaredErrors = 0.0f;
            for (int i = 0; i < actual.Count; i++)
            {
                float error = actual[i] - predicted[i];
                sumOfSquaredErrors += error * error;
            }
            return sumOfSquaredErrors / actual.Count;
        }

        /// <summary>
        /// Computes the root-mean-square error (RMSE) between actual and predicted values.
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <returns>The result.</returns>
        public static float CalculateRootMeanSquaredError(List<float> actual, List<float> predicted)
        {
            float mse = CalculateMeanSquaredError(actual, predicted);
            return (float)Math.Sqrt(mse);
        }   

        /// <summary>
        /// Computes the coefficient of determination (R ) for predicted versus actual values.
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <returns>The result.</returns>
        public static float CalculateR2Score(List<float> actual, List<float> predicted)
        {
            if (actual == null || predicted == null || actual.Count != predicted.Count || actual.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float meanActual = CalculateAverage(actual);
            float totalSumOfSquares = 0.0f;
            float residualSumOfSquares = 0.0f;
            for (int i = 0; i < actual.Count; i++)
            {
                totalSumOfSquares += (actual[i] - meanActual) * (actual[i] - meanActual);
                residualSumOfSquares += (actual[i] - predicted[i]) * (actual[i] - predicted[i]);
            }
            return 1.0f - (residualSumOfSquares / totalSumOfSquares);
        }

        /// <summary>
        /// Computes the predictor-adjusted coefficient of determination (Adjusted R ).
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <param name="numberOfPredictors">The number of predictors.</param>
        /// <returns>The result.</returns>
        public static float CalculateAdjustedR2Score(List<float> actual, List<float> predicted, int numberOfPredictors)
        {
            if (actual == null || predicted == null || actual.Count != predicted.Count || actual.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            if (numberOfPredictors < 1)
            {
                throw new ArgumentException("Number of predictors must be at least 1.");
            }
            float r2 = CalculateR2Score(actual, predicted);
            int n = actual.Count;
            return 1.0f - ((1.0f - r2) * (n - 1) / (n - numberOfPredictors - 1));
        }

        /// <summary>
        /// Computes the mean absolute scaled error (MASE) of a forecast series.
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <returns>The result.</returns>
        public static float CalculateMeanAbsoluteScaledError(List<float> actual, List<float> predicted)
        {
            if (actual == null || predicted == null || actual.Count != predicted.Count || actual.Count < 2)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and contain at least two elements.");
            }
            float meanAbsoluteError = CalculateMeanAbsoluteError(actual, predicted);
            float sumOfAbsoluteDifferences = 0.0f;
            for (int i = 1; i < actual.Count; i++)
            {
                sumOfAbsoluteDifferences += Math.Abs(actual[i] - actual[i - 1]);
            }
            float meanAbsoluteDifference = sumOfAbsoluteDifferences / (actual.Count - 1);
            if (meanAbsoluteDifference == 0.0f)
            {
                throw new InvalidOperationException("Mean absolute difference is zero, cannot calculate MASE.");
            }
            return meanAbsoluteError / meanAbsoluteDifference;
        }

        /// <summary>
        /// Computes the proportion of steps where predicted direction matches actual direction.
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <returns>The result.</returns>
        public static float CalculateMeanDirectionalAccuracy(List<float> actual, List<float> predicted)
        {
            if (actual == null || predicted == null || actual.Count != predicted.Count || actual.Count < 2)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and contain at least two elements.");
            }
            int correctDirectionCount = 0;
            for (int i = 1; i < actual.Count; i++)
            {
                bool actualDirection = actual[i] - actual[i - 1] > 0;
                bool predictedDirection = predicted[i] - predicted[i - 1] > 0;
                if (actualDirection == predictedDirection)
                {
                    correctDirectionCount++;
                }
            }
            return (float)correctDirectionCount / (actual.Count - 1) * 100.0f;
        }

        /// <summary>
        /// Computes the sum of two fractions.
        /// </summary>
        /// <param name="fraction1">The fraction1.</param>
        /// <param name="fraction2">The fraction2.</param>
        /// <returns>The result.</returns>
        public static Fraction AddFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 + fraction2;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="fraction1">The fraction1.</param>
        /// <param name="fraction2">The fraction2.</param>
        /// <returns>The result.</returns>
        public static Fraction SubtractFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 - fraction2;
        }

        /// <summary>
        /// Computes the product of two fractions.
        /// </summary>
        /// <param name="fraction1">The fraction1.</param>
        /// <param name="fraction2">The fraction2.</param>
        /// <returns>The result.</returns>
        public static Fraction MultiplyFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 * fraction2;
        }

        /// <summary>
        /// Divides the first fraction by the second fraction.
        /// </summary>
        /// <param name="fraction1">The fraction1.</param>
        /// <param name="fraction2">The fraction2.</param>
        /// <returns>The result.</returns>
        public static  Fraction DivideFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 / fraction2;
        }

        /// <summary>
        /// Constructs and reduces a fraction to lowest terms from integer numerator and denominator.
        /// </summary>
        /// <param name="fraction">The fraction.</param>
        /// <returns>The result.</returns>
        public static  Fraction SimplifyFraction(Fraction fraction)
        {
            ArgumentNullException.ThrowIfNull(fraction);
            return new Fraction(fraction.Numerator, fraction.Denominator);
        }

        /// <summary>
        /// Constructs and reduces a fraction to lowest terms from integer numerator and denominator.
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The result.</returns>
        public static Fraction SimplifyFraction(BigInteger numerator, BigInteger denominator)
        {
            return new Fraction(numerator, denominator);
        }

        /// <summary>
        /// Constructs and reduces a fraction to lowest terms from integer numerator and denominator.
        /// </summary>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The result.</returns>
        public static Fraction SimplifyFraction(int numerator, int denominator) =>
            SimplifyFraction(new BigInteger(numerator), new BigInteger(denominator));

        /// <summary>
        /// Returns the reciprocal (1 / fraction) of the specified fraction.
        /// </summary>
        /// <param name="fraction">The fraction.</param>
        /// <returns>The result.</returns>
        public static Fraction InvertFraction(Fraction fraction)
        {
            ArgumentNullException.ThrowIfNull(fraction);
            return fraction.Invert();
        }

        /// <summary>
        /// Returns the additive inverse (-fraction) of the specified fraction.
        /// </summary>
        /// <param name="fraction">The fraction.</param>
        /// <returns>The result.</returns>
        public static Fraction NegateFraction(Fraction fraction)
        {
            ArgumentNullException.ThrowIfNull(fraction);
            return -fraction;
        }

        /// <summary>
        /// Computes the sum of all fractions in the specified list.
        /// </summary>
        /// <param name="fractions">The fractions.</param>
        /// <returns>The result.</returns>
        public static Fraction AddFractions(List<Fraction> fractions)
        {
            if (fractions == null || fractions.Count == 0)
            {
                throw new ArgumentException("The list of fractions cannot be null or empty.");
            }
            Fraction result = fractions[0];
            for (int i = 1; i < fractions.Count; i++)
            {
                result = AddFraction(result, fractions[i]);
            }
            return result;
        }

        /// <summary>
        /// Computes the greatest common divisor of two BigInteger values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int CalculateGCD(int a, int b)
        {
            BigInteger gcd = CalculateGCD(new BigInteger(a), new BigInteger(b));
            return gcd > int.MaxValue ? throw new OverflowException("The greatest common divisor is outside the range of Int32.") : (int)gcd;
        }

        /// <summary>
        /// Computes the greatest common divisor of two BigInteger values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static BigInteger CalculateGCD(BigInteger a, BigInteger b)
        {
            return BigInteger.GreatestCommonDivisor(BigInteger.Abs(a), BigInteger.Abs(b));
        }

        /// <summary>
        /// Sequentially subtracts subsequent integers in the list from the first element.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int SubtractRange(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int result = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                result -= numbers[i];
            }
            return result;
        }

        /// <summary>
        /// Sequentially subtracts subsequent double-precision values in the list from the first element.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double SubtractRangeDouble(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            double result = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                result -= numbers[i];
            }
            return result;
        }

        /// <summary>
        /// Sequentially subtracts subsequent single-precision values in the list from the first element.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float SubtractRangeFloat(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float result = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                result -= numbers[i];
            }
            return result;
        }

        /// <summary>
        /// Computes the product of all integers in the specified list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int MultiplyRange(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int result = 1;
            foreach (int number in numbers)
            {
                result *= number;
            }
            return result;
        }

        /// <summary>
        /// Computes the product of all double-precision floating-point values in the specified list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double MultiplyRangeDouble(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            double result = 1.0;
            foreach (double number in numbers)
            {
                result *= number;
            }
            return result;
        }

        /// <summary>
        /// Computes the product of all single-precision floating-point values in the specified list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float MultiplyRangeFloat(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float result = 1.0f;
            foreach (float number in numbers)
            {
                result *= number;
            }
            return result;
        }

        /// <summary>
        /// Sequentially divides the first integer by each subsequent integer in the list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int DivideRange(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int result = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                if (numbers[i] == 0)
                {
                    throw new DivideByZeroException("Cannot divide by zero.");
                }
                result /= numbers[i];
            }
            return result;
        }

        /// <summary>
        /// Sequentially divides the first double-precision value by each subsequent value in the list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double DivideRangeDouble(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            double result = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                if (numbers[i] == 0.0)
                {
                    throw new DivideByZeroException("Cannot divide by zero.");
                }
                result /= numbers[i];
            }
            return result;
        }

        /// <summary>
        /// Sequentially divides the first single-precision value by each subsequent value in the list.
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float DivideRangeFloat(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float result = numbers[0];
            for (int i = 1; i < numbers.Count; i++)
            {
                if (numbers[i] == 0.0f)
                {
                    throw new DivideByZeroException("Cannot divide by zero.");
                }
                result /= numbers[i];
            }
            return result;
        }

        /// <summary>
        /// Returns the absolute value of a 32-bit signed integer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int AbsInt(int value)
        {
            if (value == int.MinValue)
            {
                throw new OverflowException("The absolute value of Int32.MinValue is outside the range of Int32.");
            }
            return Math.Abs(value);
        }

        /// <summary>
        /// Returns the absolute value of a double-precision floating-point number.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double AbsDouble(double value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Returns the absolute value of a single-precision floating-point number.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float AbsFloat(float value)
        {
            return Math.Abs(value);
        }

        /// <summary>
        /// Returns the arithmetic negation (-value) of an integer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int NegateInt(int value)
        {
            if (value == int.MinValue)
            {
                throw new OverflowException("The negation of Int32.MinValue is outside the range of Int32.");
            }
            return -value;
        }

        /// <summary>
        /// Returns the arithmetic negation (-value) of a double-precision floating-point number.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double NegateDouble(double value)
        {
            return -value;
        }

        /// <summary>
        /// Returns the arithmetic negation (-value) of a single-precision floating-point number.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float NegateFloat(float value)
        {
            return -value;
        }

        /// <summary>
        /// Returns the smaller of two integers.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int MinInt(int a, int b)
        {
            return Math.Min(a, b);
        }

        /// <summary>
        /// Returns the smaller of two double-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double MinDouble(double a, double b)
        {
            return Math.Min(a, b);
        }

        /// <summary>
        /// Returns the smaller of two single-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static float MinFloat(float a, float b)
        {
            return Math.Min(a, b);
        }

        /// <summary>
        /// Returns the larger of two integers.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int MaxInt(int a, int b)
        {
            return Math.Max(a, b);
        }

        /// <summary>
        /// Returns the larger of two double-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double MaxDouble(double a, double b)
        {
            return Math.Max(a, b);
        }

        /// <summary>
        /// Returns the larger of two single-precision floating-point values.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static float MaxFloat(float a, float b)
        {
            return Math.Max(a, b);
        }

        /// <summary>
        /// Clamps an integer to the inclusive range [min, max].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>The result.</returns>
        public static int ClampInt(int value, int min, int max)
        {
            return Math.Clamp(value, min, max);
        }

        /// <summary>
        /// Clamps a double-precision value to the inclusive range [min, max].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>The result.</returns>
        public static double ClampDouble(double value, double min, double max)
        {
            return Math.Clamp(value, min, max);
        }

        /// <summary>
        /// Clamps a single-precision value to the inclusive range [min, max].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="min">The min.</param>
        /// <param name="max">The max.</param>
        /// <returns>The result.</returns>
        public static float ClampFloat(float value, float min, float max)
        {
            return Math.Clamp(value, min, max);
        }

        /// <summary>
        /// Computes the square (value ) of an integer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int SquareInt(int value)
        {
            return value * value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double SquareDouble(double value)
        {
            return value * value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float SquareFloat(float value)
        {
            return value * value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double SqrtInt(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the square root of a negative number.");
            }
            return Math.Sqrt(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double SqrtDouble(double value)
        {
            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the square root of a negative number.");
            }
            return Math.Sqrt(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float SqrtFloat(float value)
        {
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the square root of a negative number.");
            }
            return (float)Math.Sqrt(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double CbrtInt(int value)
        {
            return Math.Cbrt(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double CbrtDouble(double value)
        {
            return Math.Cbrt(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float CbrtFloat(float value)
        {
            return (float)Math.Cbrt(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="exponent">The exponent.</param>
        /// <returns>The result.</returns>
        public static double PowDouble(double value, double exponent)
        {
            return Math.Pow(value, exponent);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="exponent">The exponent.</param>
        /// <returns>The result.</returns>
        public static float PowFloat(float value, float exponent)
        {
            return (float)Math.Pow(value, exponent);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int FloorInt(int value)
        {
            return value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double FloorDouble(double value)
        {
            return Math.Floor(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float FloorFloat(float value)
        {
            return (float)Math.Floor(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int CeilingInt(int value)
        {
            return value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double CeilingDouble(double value)
        {
            return Math.Ceiling(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float CeilingFloat(float value)
        {
            return (float)Math.Ceiling(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int RoundInt(int value)
        {
            return value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double RoundDouble(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="digits">The digits.</param>
        /// <returns>The result.</returns>
        public static double RoundDouble(double value, int digits)
        {
            return Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float RoundFloat(float value)
        {
            return (float)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="digits">The digits.</param>
        /// <returns>The result.</returns>
        public static float RoundFloat(float value, int digits)
        {
            return (float)Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int TruncateInt(int value)
        {
            return value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double TruncateDouble(double value)
        {
            return Math.Truncate(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float TruncateFloat(float value)
        {
            return (float)Math.Truncate(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int SignInt(int value)
        {
            return Math.Sign(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int SignDouble(double value)
        {
            return Math.Sign(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static int SignFloat(float value)
        {
            return Math.Sign(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double LogDouble(double value)
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return Math.Log(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float LogFloat(float value)
        {
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return (float)Math.Log(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double Log10Double(double value)
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return Math.Log10(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float Log10Float(float value)
        {
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return (float)Math.Log10(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="baseValue">The base value.</param>
        /// <returns>The result.</returns>
        public static double LogBaseDouble(double value, double baseValue)
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            if (baseValue <= 0.0 || baseValue == 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), "The logarithm base must be positive and not equal to one.");
            }
            return Math.Log(value, baseValue);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="baseValue">The base value.</param>
        /// <returns>The result.</returns>
        public static float LogBaseFloat(float value, float baseValue)
        {
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            if (baseValue <= 0.0f || baseValue == 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), "The logarithm base must be positive and not equal to one.");
            }
            return (float)Math.Log(value, baseValue);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double ExpDouble(double value)
        {
            return Math.Exp(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float ExpFloat(float value)
        {
            return (float)Math.Exp(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double SinDouble(double radians)
        {
            return Math.Sin(radians);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double CosDouble(double radians)
        {
            return Math.Cos(radians);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double TanDouble(double radians)
        {
            return Math.Tan(radians);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float SinFloat(float radians)
        {
            return (float)Math.Sin(radians);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float CosFloat(float radians)
        {
            return (float)Math.Cos(radians);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float TanFloat(float radians)
        {
            return (float)Math.Tan(radians);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="y">The y.</param>
        /// <param name="x">The x.</param>
        /// <returns>The result.</returns>
        public static double Atan2Double(double y, double x)
        {
            return Math.Atan2(y, x);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="y">The y.</param>
        /// <param name="x">The x.</param>
        /// <returns>The result.</returns>
        public static float Atan2Float(float y, float x)
        {
            return (float)Math.Atan2(y, x);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="degrees">The degrees.</param>
        /// <returns>The result.</returns>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="degrees">The degrees.</param>
        /// <returns>The result.</returns>
        public static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180.0f);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float RadiansToDegrees(float radians)
        {
            return radians * (180.0f / MathF.PI);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int CalculateMin(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int min = numbers[0];
            foreach (int number in numbers)
            {
                if (number < min)
                {
                    min = number;
                }
            }
            return min;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double CalculateMin(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            double min = numbers[0];
            foreach (double number in numbers)
            {
                if (number < min)
                {
                    min = number;
                }
            }
            return min;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateMin(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float min = numbers[0];
            foreach (float number in numbers)
            {
                if (number < min)
                {
                    min = number;
                }
            }
            return min;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int CalculateMax(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int max = numbers[0];
            foreach (int number in numbers)
            {
                if (number > max)
                {
                    max = number;
                }
            }
            return max;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double CalculateMax(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            double max = numbers[0];
            foreach (double number in numbers)
            {
                if (number > max)
                {
                    max = number;
                }
            }
            return max;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateMax(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float max = numbers[0];
            foreach (float number in numbers)
            {
                if (number > max)
                {
                    max = number;
                }
            }
            return max;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int CalculateSum(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int sum = 0;
            foreach (int number in numbers)
            {
                sum += number;
            }
            return sum;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double CalculateSum(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            double sum = 0.0;
            foreach (double number in numbers)
            {
                sum += number;
            }
            return sum;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateSum(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float sum = 0.0f;
            foreach (float number in numbers)
            {
                sum += number;
            }
            return sum;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static int CalculateProduct(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            int product = 1;
            foreach (int number in numbers)
            {
                product *= number;
            }
            return product;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static double CalculateProduct(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            double product = 1.0;
            foreach (double number in numbers)
            {
                product *= number;
            }
            return product;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateProduct(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float product = 1.0f;
            foreach (float number in numbers)
            {
                product *= number;
            }
            return product;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static List<int> CalculateCumulativeSum(List<int> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            List<int> cumulative = new List<int>(numbers.Count);
            int sum = 0;
            foreach (int number in numbers)
            {
                sum += number;
                cumulative.Add(sum);
            }
            return cumulative;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static List<double> CalculateCumulativeSum(List<double> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            List<double> cumulative = new List<double>(numbers.Count);
            double sum = 0.0;
            foreach (double number in numbers)
            {
                sum += number;
                cumulative.Add(sum);
            }
            return cumulative;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static List<float> CalculateCumulativeSum(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            List<float> cumulative = new List<float>(numbers.Count);
            float sum = 0.0f;
            foreach (float number in numbers)
            {
                sum += number;
                cumulative.Add(sum);
            }
            return cumulative;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <param name="windowSize">The window size.</param>
        /// <returns>The result.</returns>
        public static List<float> CalculateMovingAverage(List<float> numbers, int windowSize)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            if (windowSize <= 0 || windowSize > numbers.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be greater than zero and no larger than the list.");
            }
            List<float> movingAverages = new List<float>(numbers.Count - windowSize + 1);
            for (int i = 0; i <= numbers.Count - windowSize; i++)
            {
                float sum = 0.0f;
                for (int j = i; j < i + windowSize; j++)
                {
                    sum += numbers[j];
                }
                movingAverages.Add(sum / windowSize);
            }
            return movingAverages;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <param name="windowSize">The window size.</param>
        /// <returns>The result.</returns>
        public static List<float> CalculateSimpleMovingAverage(List<float> numbers, int windowSize)
        {
            return CalculateMovingAverage(numbers, windowSize);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateMedianAbsoluteDeviation(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            float median = CalculateMedian(numbers);
            List<float> deviations = new List<float>(numbers.Count);
            foreach (float number in numbers)
            {
                deviations.Add(Math.Abs(number - median));
            }
            return CalculateMedian(deviations);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <param name="trimPercentage">The trim percentage.</param>
        /// <returns>The result.</returns>
        public static float CalculateTrimmedMean(List<float> numbers, float trimPercentage)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            if (trimPercentage < 0.0f || trimPercentage >= 50.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(trimPercentage), "Trim percentage must be at least 0 and less than 50.");
            }
            List<float> sortedNumbers = new List<float>(numbers);
            sortedNumbers.Sort();
            int removeCount = (int)(sortedNumbers.Count * (trimPercentage / 100.0f));
            if (removeCount * 2 >= sortedNumbers.Count)
            {
                return CalculateAverage(sortedNumbers);
            }
            List<float> trimmedNumbers = sortedNumbers.GetRange(removeCount, sortedNumbers.Count - (removeCount * 2));
            return CalculateAverage(trimmedNumbers);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="actual">The actual.</param>
        /// <param name="predicted">The predicted.</param>
        /// <returns>The result.</returns>
        public static float CalculateMeanAbsoluteError(List<float> actual, List<float> predicted)
        {
            if (actual == null || predicted == null || actual.Count != predicted.Count || actual.Count == 0)
            {
                throw new ArgumentException("The lists must be non-null, of equal length, and non-empty.");
            }
            float sumOfAbsoluteErrors = 0.0f;
            for (int i = 0; i < actual.Count; i++)
            {
                sumOfAbsoluteErrors += Math.Abs(actual[i] - predicted[i]);
            }
            return sumOfAbsoluteErrors / actual.Count;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateSampleVariance(List<float> numbers)
        {
            if (numbers == null || numbers.Count < 2)
            {
                throw new ArgumentException("The list must contain at least two numbers.");
            }
            float average = CalculateAverage(numbers);
            float sumOfSquares = 0.0f;
            foreach (float number in numbers)
            {
                sumOfSquares += (number - average) * (number - average);
            }
            return sumOfSquares / (numbers.Count - 1);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="numbers">The numbers.</param>
        /// <returns>The result.</returns>
        public static float CalculateSampleStandardDeviation(List<float> numbers)
        {
            return (float)Math.Sqrt(CalculateSampleVariance(numbers));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int CalculateLCM(int a, int b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }
            long result = Math.Abs((long)a / CalculateGCD(a, b) * b);
            if (result > int.MaxValue)
            {
                throw new OverflowException("The least common multiple is outside the range of Int32.");
            }
            return (int)result;
        }

        /// <summary>
        /// Determines whether a 64-bit integer value is prime.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static bool IsPrime(int number)
        {
            if (number < 2)
            {
                return false;
            }
            if (number == 2)
            {
                return true;
            }
            if (number % 2 == 0)
            {
                return false;
            }
            for (int divisor = 3; (long)divisor * divisor <= number; divisor += 2)
            {
                if (number % divisor == 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static bool IsEven(int number)
        {
            return number % 2 == 0;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static bool IsOdd(int number)
        {
            return number % 2 != 0;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static bool IsPerfectSquare(int number)
        {
            if (number < 0)
            {
                return false;
            }
            long root = (long)Math.Sqrt(number);
            return root * root == number;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static long CalculateFactorial(int number)
        {
            if (number < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Factorial is not defined for negative numbers.");
            }
            if (number > 20)
            {
                throw new OverflowException("Factorial is outside the range of Int64 for numbers greater than 20.");
            }
            long result = 1;
            for (int i = 2; i <= number; i++)
            {
                result *= i;
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="n">The n.</param>
        /// <param name="r">The r.</param>
        /// <returns>The result.</returns>
        public static long CalculateCombinations(int n, int r)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "The total number of items cannot be negative.");
            }
            if (r < 0 || r > n)
            {
                throw new ArgumentOutOfRangeException(nameof(r), "The number of chosen items must be between zero and the total number of items.");
            }
            if (r > n - r)
            {
                r = n - r;
            }
            long result = 1;
            for (int i = 1; i <= r; i++)
            {
                result = result * (n - r + i) / i;
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="n">The n.</param>
        /// <param name="r">The r.</param>
        /// <returns>The result.</returns>
        public static long CalculatePermutations(int n, int r)
        {
            if (n < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(n), "The total number of items cannot be negative.");
            }
            if (r < 0 || r > n)
            {
                throw new ArgumentOutOfRangeException(nameof(r), "The number of chosen items must be between zero and the total number of items.");
            }
            long result = 1;
            for (int i = 0; i < r; i++)
            {
                result *= n - i;
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static List<int> CalculatePrimeFactors(int number)
        {
            if (number < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Prime factorization requires a number of at least two.");
            }
            List<int> factors = new List<int>();
            int remaining = number;
            for (int divisor = 2; (long)divisor * divisor <= remaining; divisor++)
            {
                while (remaining % divisor == 0)
                {
                    factors.Add(divisor);
                    remaining /= divisor;
                }
            }
            if (remaining > 1)
            {
                factors.Add(remaining);
            }
            return factors;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static int SumOfDigits(int number)
        {
            long value = Math.Abs((long)number);
            int sum = 0;
            while (value > 0)
            {
                sum += (int)(value % 10);
                value /= 10;
            }
            return sum;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>The result.</returns>
        public static int ReverseInt(int number)
        {
            long value = Math.Abs((long)number);
            long reversed = 0;
            while (value > 0)
            {
                reversed = (reversed * 10) + (value % 10);
                value /= 10;
            }
            if (number < 0)
            {
                reversed = -reversed;
            }
            if (reversed < int.MinValue || reversed > int.MaxValue)
            {
                throw new OverflowException("The reversed number is outside the range of Int32.");
            }
            return (int)reversed;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="baseValue">The base value.</param>
        /// <param name="exponent">The exponent.</param>
        /// <returns>The result.</returns>
        public static int CalculatePowerInt(int baseValue, int exponent)
        {
            if (exponent < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exponent), "The exponent cannot be negative.");
            }
            int result = 1;
            for (int i = 0; i < exponent; i++)
            {
                result *= baseValue;
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="total">The total.</param>
        /// <returns>The result.</returns>
        public static float CalculatePercentage(float value, float total)
        {
            if (total == 0.0f)
            {
                throw new DivideByZeroException("Cannot calculate a percentage with a total of zero.");
            }
            return (value / total) * 100.0f;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>The result.</returns>
        public static float CalculatePercentageChange(float oldValue, float newValue)
        {
            if (oldValue == 0.0f)
            {
                throw new DivideByZeroException("Cannot calculate a percentage change from a value of zero.");
            }
            return ((newValue - oldValue) / Math.Abs(oldValue)) * 100.0f;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="percentage">The percentage.</param>
        /// <returns>The result.</returns>
        public static float CalculatePercentageIncrease(float value, float percentage)
        {
            return value * (1.0f + (percentage / 100.0f));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="percentage">The percentage.</param>
        /// <returns>The result.</returns>
        public static float CalculatePercentageDecrease(float value, float percentage)
        {
            return value * (1.0f - (percentage / 100.0f));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="fraction1">The fraction1.</param>
        /// <param name="fraction2">The fraction2.</param>
        /// <returns>The result.</returns>
        public static int CompareFractions(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1.CompareTo(fraction2);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static int CalculateLCD(int a, int b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }
            return Math.Abs(CalculateLCM(a, b));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="fraction">The fraction.</param>
        /// <returns>The result.</returns>
        public static string ConvertToMixedNumber(Fraction fraction)
        {
            ArgumentNullException.ThrowIfNull(fraction);
            BigInteger whole = fraction.Numerator / fraction.Denominator;
            BigInteger remainder = fraction.Numerator % fraction.Denominator;
            if (remainder.IsZero)
            {
                return whole.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (whole.IsZero)
            {
                return $"{remainder.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{fraction.Denominator.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }
            return $"{whole.ToString(System.Globalization.CultureInfo.InvariantCulture)} {BigInteger.Abs(remainder).ToString(System.Globalization.CultureInfo.InvariantCulture)}/{fraction.Denominator.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }





    }
}
