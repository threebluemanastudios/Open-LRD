using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    
    internal static class Math_1
    {
        public static int AddInt(int a, int b)
        {
            return a + b;
        }

        public static int AddRange(List<int> numbers)
        {
            if (numbers.Count == 0)
            {
                return 0;
            }
            return CalculateSum(numbers);
        }

        public static int SubtractInt(int a, int b)
        {
            return a - b;
        }

        public static int MultiplyInt(int a, int b)
        {
            return a * b;
        }

        public static int DivideInt(int a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return a / b;
        }

        public static int ModulusInt(int a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Cannot perform modulus by zero.");
            }
            return a % b;
        }

        public static double AddDouble(double a, double b)
        {
            return a + b;
        }

        public static double AddRangeDouble(List<double> numbers)
        {
            if (numbers.Count == 0)
            {
                return 0.0;
            }
            return CalculateSum(numbers);
        }

        public static double SubtractDouble(double a, double b)
        {
            return a - b;
        }

        public static double MultiplyDouble(double a, double b)
        {
            return a * b;
        }

        public static double DivideDouble(double a, double b)
        {
            if (b == 0.0)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return a / b;
        }

        public static double ModulusDouble(double a, double b)
        {
            if (b == 0.0)
            {
                throw new DivideByZeroException("Cannot perform modulus by zero.");
            }
            return a % b;
        }

        public static float AddFloat(float a, float b)
        {
            return a + b;
        }

        public static float AddRangeFloat(List<float> numbers)
        {
            if (numbers.Count == 0)
            {
                return 0.0f;
            }
            return CalculateSum(numbers);
        }

        public static float SubtractFloat(float a, float b)
        {
            return a - b;
        }
        public static float MultiplyFloat(float a, float b)
        {
            return a * b;
        }
        public static float DivideFloat(float a, float b)
        {
            if (b == 0.0f)
            {
                throw new DivideByZeroException("Cannot divide by zero.");
            }
            return a / b;
        }
        public static float ModulusFloat(float a, float b)
        {
            if (b == 0.0f)
            {
                throw new DivideByZeroException("Cannot perform modulus by zero.");
            }
            return a % b;
        }
        public static float CalculateAverage(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            return CalculateSum(numbers) / numbers.Count;
        }

        public static float CalculateStandardDeviation(List<float> numbers)
        {
            if (numbers == null || numbers.Count == 0)
            {
                throw new ArgumentException("The list of numbers cannot be null or empty.");
            }
            return (float)Math.Sqrt(CalculateVariance(numbers));
        }
        
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

        public static float CalculateRootMeanSquaredError(List<float> actual, List<float> predicted)
        {
            float mse = CalculateMeanSquaredError(actual, predicted);
            return (float)Math.Sqrt(mse);
        }   

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

        public static Fraction AddFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 + fraction2;
        }

        public static Fraction SubtractFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 - fraction2;
        }

        public static Fraction MultiplyFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 * fraction2;
        }

        public static  Fraction DivideFraction(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1 / fraction2;
        }

        public static  Fraction SimplifyFraction(Fraction fraction)
        {
            ArgumentNullException.ThrowIfNull(fraction);
            return new Fraction(fraction.Numerator, fraction.Denominator);
        }

        public static Fraction SimplifyFraction(BigInteger numerator, BigInteger denominator)
        {
            return new Fraction(numerator, denominator);
        }

        public static Fraction SimplifyFraction(int numerator, int denominator) =>
            SimplifyFraction(new BigInteger(numerator), new BigInteger(denominator));

        public static Fraction InvertFraction(Fraction fraction)
        {
            ArgumentNullException.ThrowIfNull(fraction);
            return fraction.Invert();
        }

        public static Fraction NegateFraction(Fraction fraction)
        {
            ArgumentNullException.ThrowIfNull(fraction);
            return -fraction;
        }

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

        public static int CalculateGCD(int a, int b)
        {
            BigInteger gcd = CalculateGCD(new BigInteger(a), new BigInteger(b));
            return gcd > int.MaxValue ? throw new OverflowException("The greatest common divisor is outside the range of Int32.") : (int)gcd;
        }

        public static BigInteger CalculateGCD(BigInteger a, BigInteger b)
        {
            return BigInteger.GreatestCommonDivisor(BigInteger.Abs(a), BigInteger.Abs(b));
        }

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

        public static int AbsInt(int value)
        {
            if (value == int.MinValue)
            {
                throw new OverflowException("The absolute value of Int32.MinValue is outside the range of Int32.");
            }
            return Math.Abs(value);
        }

        public static double AbsDouble(double value)
        {
            return Math.Abs(value);
        }

        public static float AbsFloat(float value)
        {
            return Math.Abs(value);
        }

        public static int NegateInt(int value)
        {
            if (value == int.MinValue)
            {
                throw new OverflowException("The negation of Int32.MinValue is outside the range of Int32.");
            }
            return -value;
        }

        public static double NegateDouble(double value)
        {
            return -value;
        }

        public static float NegateFloat(float value)
        {
            return -value;
        }

        public static int MinInt(int a, int b)
        {
            return Math.Min(a, b);
        }

        public static double MinDouble(double a, double b)
        {
            return Math.Min(a, b);
        }

        public static float MinFloat(float a, float b)
        {
            return Math.Min(a, b);
        }

        public static int MaxInt(int a, int b)
        {
            return Math.Max(a, b);
        }

        public static double MaxDouble(double a, double b)
        {
            return Math.Max(a, b);
        }

        public static float MaxFloat(float a, float b)
        {
            return Math.Max(a, b);
        }

        public static int ClampInt(int value, int min, int max)
        {
            return Math.Clamp(value, min, max);
        }

        public static double ClampDouble(double value, double min, double max)
        {
            return Math.Clamp(value, min, max);
        }

        public static float ClampFloat(float value, float min, float max)
        {
            return Math.Clamp(value, min, max);
        }

        public static int SquareInt(int value)
        {
            return value * value;
        }

        public static double SquareDouble(double value)
        {
            return value * value;
        }

        public static float SquareFloat(float value)
        {
            return value * value;
        }

        public static double SqrtInt(int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the square root of a negative number.");
            }
            return Math.Sqrt(value);
        }

        public static double SqrtDouble(double value)
        {
            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the square root of a negative number.");
            }
            return Math.Sqrt(value);
        }

        public static float SqrtFloat(float value)
        {
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the square root of a negative number.");
            }
            return (float)Math.Sqrt(value);
        }

        public static double CbrtInt(int value)
        {
            return Math.Cbrt(value);
        }

        public static double CbrtDouble(double value)
        {
            return Math.Cbrt(value);
        }

        public static float CbrtFloat(float value)
        {
            return (float)Math.Cbrt(value);
        }

        public static double PowDouble(double value, double exponent)
        {
            return Math.Pow(value, exponent);
        }

        public static float PowFloat(float value, float exponent)
        {
            return (float)Math.Pow(value, exponent);
        }

        public static int FloorInt(int value)
        {
            return value;
        }

        public static double FloorDouble(double value)
        {
            return Math.Floor(value);
        }

        public static float FloorFloat(float value)
        {
            return (float)Math.Floor(value);
        }

        public static int CeilingInt(int value)
        {
            return value;
        }

        public static double CeilingDouble(double value)
        {
            return Math.Ceiling(value);
        }

        public static float CeilingFloat(float value)
        {
            return (float)Math.Ceiling(value);
        }

        public static int RoundInt(int value)
        {
            return value;
        }

        public static double RoundDouble(double value)
        {
            return Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public static double RoundDouble(double value, int digits)
        {
            return Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }

        public static float RoundFloat(float value)
        {
            return (float)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public static float RoundFloat(float value, int digits)
        {
            return (float)Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }

        public static int TruncateInt(int value)
        {
            return value;
        }

        public static double TruncateDouble(double value)
        {
            return Math.Truncate(value);
        }

        public static float TruncateFloat(float value)
        {
            return (float)Math.Truncate(value);
        }

        public static int SignInt(int value)
        {
            return Math.Sign(value);
        }

        public static int SignDouble(double value)
        {
            return Math.Sign(value);
        }

        public static int SignFloat(float value)
        {
            return Math.Sign(value);
        }

        public static double LogDouble(double value)
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return Math.Log(value);
        }

        public static float LogFloat(float value)
        {
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return (float)Math.Log(value);
        }

        public static double Log10Double(double value)
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return Math.Log10(value);
        }

        public static float Log10Float(float value)
        {
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot calculate the logarithm of a non-positive number.");
            }
            return (float)Math.Log10(value);
        }

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

        public static double ExpDouble(double value)
        {
            return Math.Exp(value);
        }

        public static float ExpFloat(float value)
        {
            return (float)Math.Exp(value);
        }

        public static double SinDouble(double radians)
        {
            return Math.Sin(radians);
        }

        public static double CosDouble(double radians)
        {
            return Math.Cos(radians);
        }

        public static double TanDouble(double radians)
        {
            return Math.Tan(radians);
        }

        public static float SinFloat(float radians)
        {
            return (float)Math.Sin(radians);
        }

        public static float CosFloat(float radians)
        {
            return (float)Math.Cos(radians);
        }

        public static float TanFloat(float radians)
        {
            return (float)Math.Tan(radians);
        }

        public static double Atan2Double(double y, double x)
        {
            return Math.Atan2(y, x);
        }

        public static float Atan2Float(float y, float x)
        {
            return (float)Math.Atan2(y, x);
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        public static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180.0f);
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        public static float RadiansToDegrees(float radians)
        {
            return radians * (180.0f / MathF.PI);
        }

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

        public static List<float> CalculateSimpleMovingAverage(List<float> numbers, int windowSize)
        {
            return CalculateMovingAverage(numbers, windowSize);
        }

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

        public static float CalculateSampleStandardDeviation(List<float> numbers)
        {
            return (float)Math.Sqrt(CalculateSampleVariance(numbers));
        }

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

        public static bool IsEven(int number)
        {
            return number % 2 == 0;
        }

        public static bool IsOdd(int number)
        {
            return number % 2 != 0;
        }

        public static bool IsPerfectSquare(int number)
        {
            if (number < 0)
            {
                return false;
            }
            long root = (long)Math.Sqrt(number);
            return root * root == number;
        }

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

        public static float CalculatePercentage(float value, float total)
        {
            if (total == 0.0f)
            {
                throw new DivideByZeroException("Cannot calculate a percentage with a total of zero.");
            }
            return (value / total) * 100.0f;
        }

        public static float CalculatePercentageChange(float oldValue, float newValue)
        {
            if (oldValue == 0.0f)
            {
                throw new DivideByZeroException("Cannot calculate a percentage change from a value of zero.");
            }
            return ((newValue - oldValue) / Math.Abs(oldValue)) * 100.0f;
        }

        public static float CalculatePercentageIncrease(float value, float percentage)
        {
            return value * (1.0f + (percentage / 100.0f));
        }

        public static float CalculatePercentageDecrease(float value, float percentage)
        {
            return value * (1.0f - (percentage / 100.0f));
        }

        public static int CompareFractions(Fraction fraction1, Fraction fraction2)
        {
            ArgumentNullException.ThrowIfNull(fraction1);
            ArgumentNullException.ThrowIfNull(fraction2);
            return fraction1.CompareTo(fraction2);
        }

        public static int CalculateLCD(int a, int b)
        {
            if (a == 0 || b == 0)
            {
                return 0;
            }
            return Math.Abs(CalculateLCM(a, b));
        }

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
