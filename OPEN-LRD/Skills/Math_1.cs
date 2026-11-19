using System;
using System.Collections.Generic;
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
            int sum = 0;
            foreach (int number in numbers)
            {
                sum += number;
            }
            return sum;
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
            double sum = 0.0;
            foreach (double number in numbers)
            {
                sum += number;
            }
            return sum;
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
            float sum = 0.0f;
            foreach (float number in numbers)
            {
                sum += number;
            }
            return sum;
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
            float sum = 0.0f;
            foreach (float number in numbers)
            {
                sum += number;
            }
            return sum / numbers.Count;
        }

        public static float CalculateStandardDeviation(List<float> numbers)
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
            return (float)Math.Sqrt(sumOfSquares / numbers.Count);
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
            float intercept = CalculateLinearRegressionIntercept(xValues, yValues);
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
            float product = 1.0f;
            foreach (float number in numbers)
            {
                if (number <= 0.0f)
                {
                    throw new ArgumentException("All numbers must be positive to calculate geometric mean.");
                }
                product *= number;
            }
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
            float sumOfAbsoluteErrors = 0.0f;
            for (int i = 0; i < actual.Count; i++)
            {
                sumOfAbsoluteErrors += Math.Abs(actual[i] - predicted[i]);
            }
            float meanAbsoluteError = sumOfAbsoluteErrors / actual.Count;
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
            int numerator = (fraction1.Numerator * fraction2.Denominator) + (fraction2.Numerator * fraction1.Denominator);
            int denominator = fraction1.Denominator * fraction2.Denominator;
            return SimplifyFraction(numerator, denominator);
        }

        public static Fraction SubtractFraction(Fraction fraction1, Fraction fraction2)
        {
            int numerator = (fraction1.Numerator * fraction2.Denominator) - (fraction2.Numerator * fraction1.Denominator);
            int denominator = fraction1.Denominator * fraction2.Denominator;
            return SimplifyFraction(numerator, denominator);
        }

        public static Fraction MultiplyFraction(Fraction fraction1, Fraction fraction2)
        {
            int numerator = fraction1.Numerator * fraction2.Numerator;
            int denominator = fraction1.Denominator * fraction2.Denominator;
            return SimplifyFraction(numerator, denominator);
        }

        public static  Fraction DivideFraction(Fraction fraction1, Fraction fraction2)
        {
            if (fraction2.Numerator == 0)
            {
                throw new DivideByZeroException("Cannot divide by a fraction with a numerator of zero.");
            }
            int numerator = fraction1.Numerator * fraction2.Denominator;
            int denominator = fraction1.Denominator * fraction2.Numerator;
            return SimplifyFraction(numerator, denominator);
        }

        public static  Fraction SimplifyFraction(Fraction fraction)
        {
            int gcd = CalculateGCD(fraction.Numerator, fraction.Denominator);
            return new Fraction(fraction.Numerator / gcd, fraction.Denominator / gcd);
        }

        public static Fraction SimplifyFraction(int numerator, int denominator)
        {
            int gcd = CalculateGCD(numerator, denominator);
            return new Fraction(numerator / gcd, denominator / gcd);
        }

        public static Fraction InvertFraction(Fraction fraction)
        {
            if (fraction.Numerator == 0)
            {
                throw new InvalidOperationException("Cannot invert a fraction with a numerator of zero.");
            }
            return new Fraction(fraction.Denominator, fraction.Numerator);
        }

        public static Fraction NegateFraction(Fraction fraction)
        {
            return new Fraction(-fraction.Numerator, fraction.Denominator);
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
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }





    }
}
