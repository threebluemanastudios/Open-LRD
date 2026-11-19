using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal enum LimitDirection
    {
        Both,
        Left,
        Right
    }

    internal enum RiemannSumType
    {
        Left,
        Right,
        Midpoint,
        Trapezoid
    }

    internal enum ExtremumType
    {
        Minimum,
        Maximum,
        Inflection,
        Unknown
    }

    internal static class Math_3
    {
        private const double DefaultTolerance = 1e-10;
        private const double LimitTolerance = 1e-8;

        public static Expression Parse(string expression)
        {
            return ExpressionParser.Parse(expression);
        }

        public static string Simplify(string expression)
        {
            return SymbolicCalculus.Simplify(Parse(expression)).ToString();
        }

        public static string Simplify(Expression expression)
        {
            return SymbolicCalculus.Simplify(expression).ToString();
        }

        public static Expression Differentiate(Expression expression, string variable = "x")
        {
            return SymbolicCalculus.Differentiate(expression, variable);
        }

        public static Expression Differentiate(string expression, string variable = "x")
        {
            return SymbolicCalculus.Differentiate(Parse(expression), variable);
        }

        public static Expression Differentiate(Expression expression, string variable, int order)
        {
            return SymbolicCalculus.Differentiate(expression, variable, order);
        }

        public static Expression Differentiate(string expression, string variable, int order)
        {
            return SymbolicCalculus.Differentiate(Parse(expression), variable, order);
        }

        public static string DifferentiateToString(string expression, string variable = "x")
        {
            return Simplify(Differentiate(expression, variable));
        }

        public static string DifferentiateToString(string expression, string variable, int order)
        {
            return Simplify(Differentiate(expression, variable, order));
        }

        public static Expression Integrate(Expression expression, string variable = "x")
        {
            if (!SymbolicCalculus.TryIntegrate(expression, variable, out Expression? result))
            {
                throw new InvalidOperationException("The expression does not have a supported symbolic antiderivative.");
            }
            return result!;
        }

        public static Expression Integrate(string expression, string variable = "x")
        {
            return Integrate(Parse(expression), variable);
        }

        public static bool TryIntegrate(Expression expression, string variable, out Expression? result)
        {
            return SymbolicCalculus.TryIntegrate(expression, variable, out result);
        }

        public static bool TryIntegrate(string expression, string variable, out Expression? result)
        {
            return SymbolicCalculus.TryIntegrate(Parse(expression), variable, out result);
        }

        public static string IntegrateToString(string expression, string variable = "x")
        {
            return Simplify(Integrate(expression, variable));
        }

        public static double Evaluate(Expression expression, string variable, double value)
        {
            return SymbolicCalculus.Evaluate(expression, variable, value);
        }

        public static double Evaluate(string expression, string variable, double value)
        {
            return Evaluate(Parse(expression), variable, value);
        }

        public static float EvaluateFloat(string expression, string variable, float value)
        {
            return (float)Evaluate(expression, variable, value);
        }

        public static double Evaluate(Expression expression, IReadOnlyDictionary<string, double> variables)
        {
            return SymbolicCalculus.Evaluate(expression, variables);
        }

        public static double DefiniteIntegral(Expression expression, string variable, double lower, double upper)
        {
            if (lower == upper)
            {
                return 0.0;
            }
            if (SymbolicCalculus.TryIntegrate(expression, variable, out Expression? antiderivative))
            {
                try
                {
                    double upperValue = SymbolicCalculus.Evaluate(antiderivative!, variable, upper);
                    double lowerValue = SymbolicCalculus.Evaluate(antiderivative!, variable, lower);
                    if (double.IsFinite(upperValue) && double.IsFinite(lowerValue))
                    {
                        return upperValue - lowerValue;
                    }
                }
                catch (ArithmeticException)
                {
                }
                catch (ArgumentException)
                {
                }
            }
            return NumericIntegral(value => SymbolicCalculus.Evaluate(expression, variable, value), lower, upper);
        }

        public static double DefiniteIntegral(string expression, string variable, double lower, double upper)
        {
            return DefiniteIntegral(Parse(expression), variable, lower, upper);
        }

        public static float DefiniteIntegralFloat(string expression, string variable, float lower, float upper)
        {
            return (float)DefiniteIntegral(expression, variable, lower, upper);
        }

        public static double NumericDerivative(Func<double, double> function, double point, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (step <= 0.0)
            {
                step = Math.Max(1e-6, Math.Abs(point) * 1e-6);
            }
            double coarse = (function(point + step) - function(point - step)) / (2.0 * step);
            double halfStep = step * 0.5;
            double fine = (function(point + halfStep) - function(point - halfStep)) / (2.0 * halfStep);
            return (4.0 * fine - coarse) / 3.0;
        }

        public static double NumericDerivative(Func<double, double> function, double point, int order, double step = 0.0)
        {
            if (order < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(order), "The derivative order must be at least one.");
            }
            Func<double, double> current = function;
            for (int index = 0; index < order; index++)
            {
                Func<double, double> previous = current;
                current = value => NumericDerivative(previous, value, step);
            }
            return current(point);
        }

        public static double NumericSecondDerivative(Func<double, double> function, double point, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (step <= 0.0)
            {
                step = Math.Max(1e-5, Math.Abs(point) * 1e-5);
            }
            return (function(point + step) - 2.0 * function(point) + function(point - step)) / (step * step);
        }

        public static double NumericDerivativeAtPoint(string expression, string variable, double point, double step = 0.0)
        {
            Expression parsed = Parse(expression);
            return NumericDerivative(value => SymbolicCalculus.Evaluate(parsed, variable, value), point, step);
        }

        public static double NumericIntegral(Func<double, double> function, double lower, double upper, int intervals = 1000)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (intervals < 2)
            {
                intervals = 2;
            }
            if (intervals % 2 != 0)
            {
                intervals++;
            }
            double step = (upper - lower) / intervals;
            double sum = function(lower) + function(upper);
            for (int index = 1; index < intervals; index++)
            {
                double value = function(lower + index * step);
                sum += index % 2 == 0 ? 2.0 * value : 4.0 * value;
            }
            return sum * step / 3.0;
        }

        public static double AdaptiveIntegral(Func<double, double> function, double lower, double upper, double tolerance = DefaultTolerance, int maxDepth = 20)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (tolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
            }
            return AdaptiveSimpson(function, lower, upper, tolerance, maxDepth);
        }

        public static double TrapezoidalIntegral(Func<double, double> function, double lower, double upper, int intervals = 1000)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (intervals < 1)
            {
                intervals = 1;
            }
            double step = (upper - lower) / intervals;
            double sum = 0.5 * (function(lower) + function(upper));
            for (int index = 1; index < intervals; index++)
            {
                sum += function(lower + index * step);
            }
            return sum * step;
        }

        public static double Limit(Expression expression, string variable, double approach, LimitDirection direction = LimitDirection.Both)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (!double.IsFinite(approach))
            {
                return LimitAtInfinity(expression, variable, approach > 0.0);
            }
            if (TryEvaluateAt(expression, variable, approach, out double direct))
            {
                return direct;
            }
            if (TryLHopital(expression, variable, approach, out double hopital))
            {
                return hopital;
            }
            Func<double, double> function = value => SymbolicCalculus.Evaluate(expression, variable, value);
            if (direction == LimitDirection.Right)
            {
                return NumericOneSidedLimit(function, approach, false);
            }
            if (direction == LimitDirection.Left)
            {
                return NumericOneSidedLimit(function, approach, true);
            }
            double leftValue = NumericOneSidedLimit(function, approach, true);
            double rightValue = NumericOneSidedLimit(function, approach, false);
            if (leftValue == rightValue)
            {
                return leftValue;
            }
            if (double.IsFinite(leftValue) && double.IsFinite(rightValue)
                && Math.Abs(leftValue - rightValue) <= 1e-6 * (1.0 + Math.Abs(leftValue)))
            {
                return 0.5 * (leftValue + rightValue);
            }
            throw new InvalidOperationException("The two-sided limit does not exist.");
        }

        public static double Limit(string expression, string variable, double approach, LimitDirection direction = LimitDirection.Both)
        {
            return Limit(Parse(expression), variable, approach, direction);
        }

        public static double LimitAtInfinity(Expression expression, string variable, bool positive)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (TryPolynomialInfinityLimit(expression, variable, positive, out double polynomialLimit))
            {
                return polynomialLimit;
            }
            Func<double, double> function = value => SymbolicCalculus.Evaluate(expression, variable, value);
            double sign = positive ? 1.0 : -1.0;
            double? previous = null;
            double last = double.NaN;
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            bool grew = false;
            for (int exponent = 2; exponent <= 12; exponent += 2)
            {
                double x = sign * Math.Pow(10.0, exponent);
                double value;
                try
                {
                    value = function(x);
                }
                catch (ArithmeticException)
                {
                    continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (double.IsNaN(value))
                {
                    continue;
                }
                if (double.IsInfinity(value))
                {
                    return value;
                }
                last = value;
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
                if (previous.HasValue)
                {
                    double difference = Math.Abs(value - previous.Value);
                    if (difference <= LimitTolerance * (1.0 + Math.Abs(value)))
                    {
                        return 0.5 * (value + previous.Value);
                    }
                    grew = Math.Abs(value) >= 10.0 * Math.Max(1.0, Math.Abs(previous.Value));
                }
                previous = value;
            }
            if (double.IsNaN(last))
            {
                throw new InvalidOperationException("The limit could not be evaluated.");
            }
            if (grew && (maximum - minimum) > LimitTolerance * (1.0 + Math.Abs(last)))
            {
                return Math.Sign(last) * double.PositiveInfinity;
            }
            throw new InvalidOperationException("The limit at infinity does not appear to exist.");
        }

        public static double LimitAtInfinity(string expression, string variable, bool positive)
        {
            return LimitAtInfinity(Parse(expression), variable, positive);
        }

        public static Expression TaylorSeries(Expression expression, string variable, double center, int order)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (order < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(order), "The series order cannot be negative.");
            }
            Expression result = ExpressionBuilder.Constant(0.0);
            Expression offset = center == 0.0
                ? ExpressionBuilder.Variable(variable)
                : ExpressionBuilder.Subtract(
                    ExpressionBuilder.Variable(variable),
                    ExpressionBuilder.Constant(center));
            for (int degree = 0; degree <= order; degree++)
            {
                double coefficient = TaylorCoefficient(expression, variable, center, degree);
                Expression term = degree == 0
                    ? ExpressionBuilder.Constant(coefficient)
                    : degree == 1
                        ? ExpressionBuilder.Multiply(ExpressionBuilder.Constant(coefficient), offset)
                        : ExpressionBuilder.Multiply(
                            ExpressionBuilder.Constant(coefficient),
                            ExpressionBuilder.Power(offset, ExpressionBuilder.Constant(degree)));
                result = ExpressionBuilder.Add(result, term);
            }
            return SymbolicCalculus.Simplify(result);
        }

        public static Expression TaylorSeries(string expression, string variable, double center, int order)
        {
            return TaylorSeries(Parse(expression), variable, center, order);
        }

        public static string TaylorSeriesToString(string expression, string variable, double center, int order)
        {
            return Simplify(TaylorSeries(expression, variable, center, order));
        }

        public static Expression MaclaurinSeries(Expression expression, string variable, int order)
        {
            return TaylorSeries(expression, variable, 0.0, order);
        }

        public static Expression MaclaurinSeries(string expression, string variable, int order)
        {
            return TaylorSeries(expression, variable, 0.0, order);
        }

        public static double TaylorSeriesValue(Expression expression, string variable, double center, int order, double point)
        {
            Expression series = TaylorSeries(expression, variable, center, order);
            return SymbolicCalculus.Evaluate(series, variable, point);
        }

        public static double TaylorCoefficient(Expression expression, string variable, double center, int degree)
        {
            if (degree < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), "The derivative order cannot be negative.");
            }
            Expression derivative = degree == 0
                ? expression
                : SymbolicCalculus.Differentiate(expression, variable, degree);
            double value = SymbolicCalculus.Evaluate(derivative, variable, center);
            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException($"The derivative of order {degree} is not finite at the center.");
            }
            return value / Factorial(degree);
        }

        public static double GeometricSeriesSum(double firstTerm, double ratio)
        {
            if (Math.Abs(ratio) >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(ratio), "The geometric ratio must satisfy |r| < 1.");
            }
            return firstTerm / (1.0 - ratio);
        }

        public static double InfiniteSeriesSum(Func<int, double> term, int firstIndex = 0, double tolerance = 1e-12, int maxTerms = 100000)
        {
            if (term == null)
            {
                throw new ArgumentNullException(nameof(term));
            }
            if (tolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
            }
            if (maxTerms < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTerms), "The term limit must be positive.");
            }
            double sum = 0.0;
            double magnitude = 0.0;
            for (int index = 0; index < maxTerms; index++)
            {
                double value = term(firstIndex + index);
                if (!double.IsFinite(value))
                {
                    throw new InvalidOperationException("The series produced a non-finite term.");
                }
                sum += value;
                magnitude += Math.Abs(value);
                if (Math.Abs(value) <= tolerance * (1.0 + magnitude))
                {
                    return sum;
                }
            }
            throw new InvalidOperationException("The series did not converge within the term limit.");
        }

        public static double RiemannSum(Func<double, double> function, double lower, double upper, int intervals, RiemannSumType type = RiemannSumType.Midpoint)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (intervals < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(intervals), "The interval count must be positive.");
            }
            double step = (upper - lower) / intervals;
            double sum = 0.0;
            for (int index = 0; index < intervals; index++)
            {
                double left = lower + index * step;
                double right = left + step;
                double sample = type switch
                {
                    RiemannSumType.Left => left,
                    RiemannSumType.Right => right,
                    RiemannSumType.Midpoint => 0.5 * (left + right),
                    RiemannSumType.Trapezoid => 0.0,
                    _ => 0.5 * (left + right)
                };
                if (type == RiemannSumType.Trapezoid)
                {
                    sum += 0.5 * (function(left) + function(right)) * step;
                }
                else
                {
                    sum += function(sample) * step;
                }
            }
            return sum;
        }

        public static double ArcLength(Expression expression, string variable, double lower, double upper)
        {
            Expression derivative = SymbolicCalculus.Differentiate(expression, variable);
            Func<double, double> function = value =>
            {
                double slope = SymbolicCalculus.Evaluate(derivative, variable, value);
                return Math.Sqrt(1.0 + slope * slope);
            };
            return AdaptiveIntegral(function, lower, upper);
        }

        public static double ArcLength(string expression, string variable, double lower, double upper)
        {
            return ArcLength(Parse(expression), variable, lower, upper);
        }

        public static double ArcLengthParametric(Func<double, double> xOfT, Func<double, double> yOfT, double lower, double upper)
        {
            if (xOfT == null)
            {
                throw new ArgumentNullException(nameof(xOfT));
            }
            if (yOfT == null)
            {
                throw new ArgumentNullException(nameof(yOfT));
            }
            Func<double, double> speed = value =>
            {
                double dx = NumericDerivative(xOfT, value);
                double dy = NumericDerivative(yOfT, value);
                return Math.Sqrt(dx * dx + dy * dy);
            };
            return AdaptiveIntegral(speed, lower, upper);
        }

        public static double VolumeOfRevolutionDisk(Expression expression, string variable, double lower, double upper)
        {
            Func<double, double> function = value =>
            {
                double radius = SymbolicCalculus.Evaluate(expression, variable, value);
                return Math.PI * radius * radius;
            };
            return AdaptiveIntegral(function, lower, upper);
        }

        public static double VolumeOfRevolutionDisk(string expression, string variable, double lower, double upper)
        {
            return VolumeOfRevolutionDisk(Parse(expression), variable, lower, upper);
        }

        public static double VolumeOfRevolutionWasher(Expression outer, Expression inner, string variable, double lower, double upper)
        {
            Func<double, double> function = value =>
            {
                double outerRadius = SymbolicCalculus.Evaluate(outer, variable, value);
                double innerRadius = SymbolicCalculus.Evaluate(inner, variable, value);
                return Math.PI * (outerRadius * outerRadius - innerRadius * innerRadius);
            };
            return AdaptiveIntegral(function, lower, upper);
        }

        public static double VolumeOfRevolutionShell(Expression expression, string variable, double lower, double upper)
        {
            Func<double, double> function = value =>
            {
                double height = SymbolicCalculus.Evaluate(expression, variable, value);
                return 2.0 * Math.PI * Math.Abs(value) * height;
            };
            return AdaptiveIntegral(function, lower, upper);
        }

        public static double SurfaceAreaOfRevolution(Expression expression, string variable, double lower, double upper)
        {
            Expression derivative = SymbolicCalculus.Differentiate(expression, variable);
            Func<double, double> function = value =>
            {
                double radius = SymbolicCalculus.Evaluate(expression, variable, value);
                double slope = SymbolicCalculus.Evaluate(derivative, variable, value);
                return 2.0 * Math.PI * Math.Abs(radius) * Math.Sqrt(1.0 + slope * slope);
            };
            return AdaptiveIntegral(function, lower, upper);
        }

        public static double AverageValue(Expression expression, string variable, double lower, double upper)
        {
            if (lower == upper)
            {
                throw new ArgumentException("The interval cannot have zero width.");
            }
            return DefiniteIntegral(expression, variable, lower, upper) / (upper - lower);
        }

        public static double AverageValue(string expression, string variable, double lower, double upper)
        {
            return AverageValue(Parse(expression), variable, lower, upper);
        }

        public static double MeanValueTheoremPoint(Expression expression, string variable, double lower, double upper)
        {
            if (lower == upper)
            {
                throw new ArgumentException("The interval cannot have zero width.");
            }
            double average = AverageValue(expression, variable, lower, upper);
            Expression derivative = SymbolicCalculus.Differentiate(expression, variable);
            Func<double, double> target = value => SymbolicCalculus.Evaluate(derivative, variable, value) - average;
            return FindRoot(target, 0.5 * (lower + upper), lower, upper);
        }

        public static double NewtonRaphson(Func<double, double> function, double initialGuess, double tolerance = 1e-10, int maxIterations = 100)
        {
            return NewtonRaphson(function, value => NumericDerivative(function, value), initialGuess, tolerance, maxIterations);
        }

        public static double NewtonRaphson(Func<double, double> function, Func<double, double> derivative, double initialGuess, double tolerance = 1e-10, int maxIterations = 100)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (derivative == null)
            {
                throw new ArgumentNullException(nameof(derivative));
            }
            if (tolerance <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be positive.");
            }
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations), "The iteration limit must be positive.");
            }
            double current = initialGuess;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double value = function(current);
                if (Math.Abs(value) <= tolerance)
                {
                    return current;
                }
                double slope = derivative(current);
                if (Math.Abs(slope) < 1e-14)
                {
                    break;
                }
                double next = current - value / slope;
                if (!double.IsFinite(next))
                {
                    break;
                }
                if (Math.Abs(next - current) <= tolerance * (1.0 + Math.Abs(current)))
                {
                    return next;
                }
                current = next;
            }
            throw new InvalidOperationException("Newton's method did not converge.");
        }

        public static double NewtonRaphson(Expression expression, string variable, double initialGuess, double tolerance = 1e-10, int maxIterations = 100)
        {
            return NewtonRaphson(value => SymbolicCalculus.Evaluate(expression, variable, value), initialGuess, tolerance, maxIterations);
        }

        public static double NewtonRaphson(string expression, string variable, double initialGuess, double tolerance = 1e-10, int maxIterations = 100)
        {
            return NewtonRaphson(Parse(expression), variable, initialGuess, tolerance, maxIterations);
        }

        public static double Bisection(Func<double, double> function, double lower, double upper, double tolerance = 1e-10, int maxIterations = 200)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (lower > upper)
            {
                (lower, upper) = (upper, lower);
            }
            double lowerValue = function(lower);
            double upperValue = function(upper);
            if (Math.Abs(lowerValue) <= tolerance)
            {
                return lower;
            }
            if (Math.Abs(upperValue) <= tolerance)
            {
                return upper;
            }
            if (lowerValue * upperValue > 0.0)
            {
                throw new ArgumentException("The interval does not bracket a root.");
            }
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double middle = 0.5 * (lower + upper);
                double middleValue = function(middle);
                if (Math.Abs(middleValue) <= tolerance || (upper - lower) <= tolerance * (1.0 + Math.Abs(middle)))
                {
                    return middle;
                }
                if (lowerValue * middleValue < 0.0)
                {
                    upper = middle;
                    upperValue = middleValue;
                }
                else
                {
                    lower = middle;
                    lowerValue = middleValue;
                }
            }
            return 0.5 * (lower + upper);
        }

        public static double FindRoot(Func<double, double> function, double initialGuess, double lower, double upper, double tolerance = 1e-10, int samples = 1000)
        {
            try
            {
                return NewtonRaphson(function, initialGuess, tolerance);
            }
            catch (InvalidOperationException)
            {
            }
            double[] roots = FindRoots(function, lower, upper, samples, tolerance);
            if (roots.Length == 0)
            {
                throw new InvalidOperationException("No root was found in the interval.");
            }
            double best = roots[0];
            double bestDistance = Math.Abs(best - initialGuess);
            foreach (double root in roots)
            {
                double distance = Math.Abs(root - initialGuess);
                if (distance < bestDistance)
                {
                    best = root;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static double[] FindRoots(Func<double, double> function, double lower, double upper, int samples = 1000, double tolerance = 1e-10)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (samples < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(samples), "At least two samples are required.");
            }
            if (lower > upper)
            {
                (lower, upper) = (upper, lower);
            }
            List<double> roots = new List<double>();
            double step = (upper - lower) / samples;
            double previousX = lower;
            double previousValue = function(previousX);
            if (Math.Abs(previousValue) <= tolerance)
            {
                AddDistinct(roots, previousX, tolerance);
            }
            for (int index = 1; index <= samples; index++)
            {
                double x = lower + index * step;
                double value = function(x);
                if (Math.Abs(value) <= tolerance)
                {
                    AddDistinct(roots, x, tolerance);
                }
                else if (double.IsFinite(previousValue) && previousValue * value < 0.0)
                {
                    AddDistinct(roots, Bisection(function, previousX, x, tolerance), tolerance);
                }
                previousX = x;
                previousValue = value;
            }
            return roots.ToArray();
        }

        public static double[] FindCriticalPoints(Expression expression, string variable, double lower, double upper, int samples = 1000)
        {
            Expression derivative = SymbolicCalculus.Differentiate(expression, variable);
            Func<double, double> function = value => SymbolicCalculus.Evaluate(derivative, variable, value);
            return FindRoots(function, lower, upper, samples);
        }

        public static double[] FindCriticalPoints(string expression, string variable, double lower, double upper, int samples = 1000)
        {
            return FindCriticalPoints(Parse(expression), variable, lower, upper, samples);
        }

        public static double[] FindInflectionPoints(Expression expression, string variable, double lower, double upper, int samples = 1000)
        {
            Expression derivative = SymbolicCalculus.Differentiate(expression, variable, 2);
            Func<double, double> function = value => SymbolicCalculus.Evaluate(derivative, variable, value);
            return FindRoots(function, lower, upper, samples);
        }

        public static double[] FindInflectionPoints(string expression, string variable, double lower, double upper, int samples = 1000)
        {
            return FindInflectionPoints(Parse(expression), variable, lower, upper, samples);
        }

        public static ExtremumType ClassifyCriticalPoint(Expression expression, string variable, double point)
        {
            Expression derivative = SymbolicCalculus.Differentiate(expression, variable, 2);
            double secondDerivative = SymbolicCalculus.Evaluate(derivative, variable, point);
            if (secondDerivative > 1e-9)
            {
                return ExtremumType.Minimum;
            }
            if (secondDerivative < -1e-9)
            {
                return ExtremumType.Maximum;
            }
            double step = Math.Max(1e-5, Math.Abs(point) * 1e-5);
            double left = SymbolicCalculus.Evaluate(expression, variable, point - step);
            double middle = SymbolicCalculus.Evaluate(expression, variable, point);
            double right = SymbolicCalculus.Evaluate(expression, variable, point + step);
            if (middle <= left && middle <= right)
            {
                return ExtremumType.Minimum;
            }
            if (middle >= left && middle >= right)
            {
                return ExtremumType.Maximum;
            }
            return ExtremumType.Inflection;
        }

        public static ExtremumType ClassifyCriticalPoint(string expression, string variable, double point)
        {
            return ClassifyCriticalPoint(Parse(expression), variable, point);
        }

        private static double AdaptiveSimpson(Func<double, double> function, double lower, double upper, double tolerance, int depth)
        {
            double middle = 0.5 * (lower + upper);
            double whole = SimpsonSegment(function, lower, upper, middle);
            return AdaptiveSimpsonRecursive(function, lower, upper, tolerance, depth, whole);
        }

        private static double AdaptiveSimpsonRecursive(Func<double, double> function, double lower, double upper, double tolerance, int depth, double estimate)
        {
            double middle = 0.5 * (lower + upper);
            double leftMiddle = 0.5 * (lower + middle);
            double rightMiddle = 0.5 * (middle + upper);
            double left = SimpsonSegment(function, lower, middle, leftMiddle);
            double right = SimpsonSegment(function, middle, upper, rightMiddle);
            double refined = left + right;
            if (depth <= 0 || Math.Abs(refined - estimate) <= 15.0 * tolerance)
            {
                return refined + (refined - estimate) / 15.0;
            }
            return AdaptiveSimpsonRecursive(function, lower, middle, tolerance * 0.5, depth - 1, left)
                + AdaptiveSimpsonRecursive(function, middle, upper, tolerance * 0.5, depth - 1, right);
        }

        private static double SimpsonSegment(Func<double, double> function, double lower, double upper, double middle)
        {
            return (upper - lower) / 6.0 * (function(lower) + 4.0 * function(middle) + function(upper));
        }

        private static double NumericOneSidedLimit(Func<double, double> function, double approach, bool left)
        {
            double scale = Math.Max(1.0, Math.Abs(approach));
            double? previous = null;
            double last = double.NaN;
            bool grew = false;
            for (int exponent = 1; exponent <= 14; exponent++)
            {
                double offset = Math.Pow(10.0, -exponent) * scale;
                double x = left ? approach - offset : approach + offset;
                double value;
                try
                {
                    value = function(x);
                }
                catch (ArithmeticException)
                {
                    continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
                if (double.IsNaN(value))
                {
                    continue;
                }
                if (double.IsInfinity(value))
                {
                    return value;
                }
                last = value;
                if (previous.HasValue)
                {
                    double difference = Math.Abs(value - previous.Value);
                    if (difference <= LimitTolerance * (1.0 + Math.Abs(value)))
                    {
                        return 0.5 * (value + previous.Value);
                    }
                    grew = Math.Abs(value) >= 10.0 * Math.Max(1.0, Math.Abs(previous.Value));
                }
                previous = value;
            }
            if (double.IsNaN(last))
            {
                throw new InvalidOperationException("The one-sided limit could not be evaluated.");
            }
            return grew ? Math.Sign(last) * double.PositiveInfinity : last;
        }

        private static bool TryLHopital(Expression expression, string variable, double approach, out double result)
        {
            result = double.NaN;
            Expression current = SymbolicCalculus.Simplify(expression);
            for (int iteration = 0; iteration < 12; iteration++)
            {
                if (TryEvaluateAt(current, variable, approach, out double direct))
                {
                    result = direct;
                    return true;
                }
                if (current is not BinaryExpression division || division.Operator != BinaryOperator.Divide)
                {
                    return false;
                }
                if (!TryEvaluateAny(division.Left, variable, approach, out double numerator)
                    || !TryEvaluateAny(division.Right, variable, approach, out double denominator))
                {
                    return false;
                }
                bool zeroOverZero = IsNearlyZero(numerator) && IsNearlyZero(denominator);
                bool infinityOverInfinity = double.IsInfinity(numerator) && double.IsInfinity(denominator);
                if (!zeroOverZero && !infinityOverInfinity)
                {
                    if (IsNearlyZero(denominator) && !IsNearlyZero(numerator))
                    {
                        return false;
                    }
                    if (double.IsFinite(numerator) && double.IsFinite(denominator) && denominator != 0.0)
                    {
                        result = numerator / denominator;
                        return true;
                    }
                    return false;
                }
                Expression differentiatedNumerator = SymbolicCalculus.Differentiate(division.Left, variable);
                Expression differentiatedDenominator = SymbolicCalculus.Differentiate(division.Right, variable);
                current = SymbolicCalculus.Simplify(
                    ExpressionBuilder.Divide(differentiatedNumerator, differentiatedDenominator));
            }
            return false;
        }

        private static bool TryPolynomialInfinityLimit(Expression expression, string variable, bool positive, out double result)
        {
            result = double.NaN;
            if (SymbolicCalculus.TryGetPolynomial(expression, variable, out double[] polynomial))
            {
                int degree = GetPolynomialDegree(polynomial);
                if (degree <= 0)
                {
                    result = polynomial.Length > 0 ? polynomial[0] : 0.0;
                    return true;
                }
                double leading = polynomial[degree];
                bool negative = leading < 0.0;
                if (!positive && degree % 2 == 1)
                {
                    negative = !negative;
                }
                result = negative ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            }
            if (expression is BinaryExpression division
                && division.Operator == BinaryOperator.Divide
                && SymbolicCalculus.TryGetPolynomial(division.Left, variable, out double[] numerator)
                && SymbolicCalculus.TryGetPolynomial(division.Right, variable, out double[] denominator))
            {
                int numeratorDegree = GetPolynomialDegree(numerator);
                int denominatorDegree = GetPolynomialDegree(denominator);
                if (numeratorDegree < 0)
                {
                    result = 0.0;
                    return true;
                }
                if (denominatorDegree < 0)
                {
                    return false;
                }
                if (numeratorDegree < denominatorDegree)
                {
                    result = 0.0;
                    return true;
                }
                if (numeratorDegree == denominatorDegree)
                {
                    result = numerator[numeratorDegree] / denominator[denominatorDegree];
                    return true;
                }
                double ratio = numerator[numeratorDegree] / denominator[denominatorDegree];
                bool negative = ratio < 0.0;
                if (!positive && (numeratorDegree - denominatorDegree) % 2 == 1)
                {
                    negative = !negative;
                }
                result = negative ? double.NegativeInfinity : double.PositiveInfinity;
                return true;
            }
            return false;
        }

        private static int GetPolynomialDegree(double[] coefficients)
        {
            for (int index = coefficients.Length - 1; index >= 0; index--)
            {
                if (Math.Abs(coefficients[index]) > DefaultTolerance)
                {
                    return index;
                }
            }
            return -1;
        }

        private static bool TryEvaluateAt(Expression expression, string variable, double value, out double result)
        {
            try
            {
                result = SymbolicCalculus.Evaluate(expression, variable, value);
            }
            catch (ArithmeticException)
            {
                result = double.NaN;
                return false;
            }
            catch (ArgumentException)
            {
                result = double.NaN;
                return false;
            }
            return double.IsFinite(result);
        }

        private static bool TryEvaluateAny(Expression expression, string variable, double value, out double result)
        {
            try
            {
                result = SymbolicCalculus.Evaluate(expression, variable, value);
            }
            catch (ArithmeticException)
            {
                result = double.NaN;
                return false;
            }
            catch (ArgumentException)
            {
                result = double.NaN;
                return false;
            }
            return !double.IsNaN(result);
        }

        private static bool IsNearlyZero(double value)
        {
            return Math.Abs(value) <= DefaultTolerance;
        }

        private static double Factorial(int value)
        {
            double result = 1.0;
            for (int index = 2; index <= value; index++)
            {
                result *= index;
            }
            return result;
        }

        private static void AddDistinct(List<double> values, double candidate, double tolerance)
        {
            foreach (double existing in values)
            {
                if (Math.Abs(existing - candidate) <= tolerance * (1.0 + Math.Abs(candidate)))
                {
                    return;
                }
            }
            values.Add(candidate);
        }
    }
}
