using System;
using System.Collections.Generic;

namespace OPEN_LRD.Skills
{
    internal enum SplineBoundary
    {
        Natural,
        Clamped
    }

    internal sealed record RootFindingResult(double Root, double Residual, int Iterations, bool Converged);

    internal sealed record CurveFitResult(double[] Coefficients, double RSquared, double RootMeanSquaredError);

    internal sealed record IntegrationResult(double Value, double EstimatedError, int FunctionEvaluations);

    internal sealed record IterativeSolverResult(double[] Solution, double ResidualNorm, int Iterations, bool Converged);

    internal sealed record SystemRootFindingResult(double[] Root, double ResidualNorm, int Iterations, bool Converged);

    internal sealed class CubicSpline
    {
        private readonly double[] x;
        private readonly double[] y;
        private readonly double[] secondDerivatives;

        internal CubicSpline(double[] xValues, double[] yValues, SplineBoundary boundary, double? firstDerivative, double? lastDerivative)
        {
            NumericalMethods.ValidateInterpolationData(xValues, yValues, 2);
            if (!Enum.IsDefined(boundary))
            {
                throw new ArgumentOutOfRangeException(nameof(boundary));
            }
            if (boundary == SplineBoundary.Clamped && (!firstDerivative.HasValue || !lastDerivative.HasValue))
            {
                throw new ArgumentException("Clamped splines require both endpoint derivatives.");
            }
            if (firstDerivative.HasValue && !double.IsFinite(firstDerivative.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(firstDerivative));
            }
            if (lastDerivative.HasValue && !double.IsFinite(lastDerivative.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(lastDerivative));
            }

            x = (double[])xValues.Clone();
            y = (double[])yValues.Clone();
            secondDerivatives = new double[x.Length];
            double[] work = new double[x.Length - 1];
            if (boundary == SplineBoundary.Natural)
            {
                secondDerivatives[0] = 0.0;
                work[0] = 0.0;
            }
            else
            {
                double firstWidth = x[1] - x[0];
                secondDerivatives[0] = -0.5;
                work[0] = (3.0 / firstWidth) * ((y[1] - y[0]) / firstWidth - firstDerivative!.Value);
            }

            for (int index = 1; index < x.Length - 1; index++)
            {
                double leftWidth = x[index] - x[index - 1];
                double rightWidth = x[index + 1] - x[index];
                double span = x[index + 1] - x[index - 1];
                double sigma = leftWidth / span;
                double pivot = sigma * secondDerivatives[index - 1] + 2.0;
                secondDerivatives[index] = (sigma - 1.0) / pivot;
                double slopeDifference = (y[index + 1] - y[index]) / rightWidth - (y[index] - y[index - 1]) / leftWidth;
                work[index] = (6.0 * slopeDifference / span - sigma * work[index - 1]) / pivot;
            }

            double qn;
            double un;
            if (boundary == SplineBoundary.Natural)
            {
                qn = 0.0;
                un = 0.0;
            }
            else
            {
                double width = x[^1] - x[^2];
                qn = 0.5;
                un = (3.0 / width) * (lastDerivative!.Value - (y[^1] - y[^2]) / width);
            }
            secondDerivatives[^1] = (un - qn * work[^1]) / (qn * secondDerivatives[^2] + 1.0);
            for (int index = x.Length - 2; index >= 0; index--)
            {
                secondDerivatives[index] = secondDerivatives[index] * secondDerivatives[index + 1] + work[index];
            }
        }

        public double Evaluate(double value)
        {
            (int lower, int upper, double width, double a, double b) = FindInterval(value);
            double curvature = ((a * a * a - a) * secondDerivatives[lower]
                + (b * b * b - b) * secondDerivatives[upper]) * width * width / 6.0;
            return a * y[lower] + b * y[upper] + curvature;
        }

        public double Derivative(double value)
        {
            (int lower, int upper, double width, double a, double b) = FindInterval(value);
            return (y[upper] - y[lower]) / width
                + ((1.0 - 3.0 * a * a) * secondDerivatives[lower]
                    + (3.0 * b * b - 1.0) * secondDerivatives[upper]) * width / 6.0;
        }

        public double SecondDerivative(double value)
        {
            (int lower, int upper, double _, double a, double b) = FindInterval(value);
            return a * secondDerivatives[lower] + b * secondDerivatives[upper];
        }

        private (int Lower, int Upper, double Width, double A, double B) FindInterval(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            int lower = 0;
            int upper = x.Length - 1;
            while (upper - lower > 1)
            {
                int middle = (upper + lower) / 2;
                if (x[middle] > value)
                {
                    upper = middle;
                }
                else
                {
                    lower = middle;
                }
            }
            double width = x[upper] - x[lower];
            double a = (x[upper] - value) / width;
            double b = (value - x[lower]) / width;
            return (lower, upper, width, a, b);
        }
    }

    internal static class NumericalMethods
    {
        private const double DefaultTolerance = 1e-9;
        private const int MaximumOdeSteps = 100000;

        public static double LagrangeInterpolate(double[] x, double[] y, double value)
        {
            ValidateInterpolationData(x, y, 2);
            EnsureFinite(value, nameof(value));
            double result = 0.0;
            for (int index = 0; index < x.Length; index++)
            {
                double basis = y[index];
                for (int other = 0; other < x.Length; other++)
                {
                    if (other != index)
                    {
                        basis *= (value - x[other]) / (x[index] - x[other]);
                    }
                }
                result += basis;
            }
            return result;
        }

        public static double NewtonDividedDifference(double[] x, double[] y, double value)
        {
            ValidateInterpolationData(x, y, 2);
            EnsureFinite(value, nameof(value));
            double[] coefficients = DividedDifferences(x, y);
            double result = coefficients[^1];
            for (int index = coefficients.Length - 2; index >= 0; index--)
            {
                result = coefficients[index] + (value - x[index]) * result;
            }
            return result;
        }

        public static Func<double, double> BuildNewtonInterpolator(double[] x, double[] y)
        {
            ValidateInterpolationData(x, y, 2);
            double[] nodes = (double[])x.Clone();
            double[] coefficients = DividedDifferences(x, y);
            return value =>
            {
                EnsureFinite(value, nameof(value));
                double result = coefficients[^1];
                for (int index = coefficients.Length - 2; index >= 0; index--)
                {
                    result = coefficients[index] + (value - nodes[index]) * result;
                }
                return result;
            };
        }

        public static CubicSpline BuildCubicSpline(double[] x, double[] y, SplineBoundary boundary, double? firstDerivative = null, double? lastDerivative = null)
        {
            return new CubicSpline(x, y, boundary, firstDerivative, lastDerivative);
        }

        public static double CubicSplineInterpolate(double[] x, double[] y, double value, SplineBoundary boundary, double? firstDerivative = null, double? lastDerivative = null)
        {
            return BuildCubicSpline(x, y, boundary, firstDerivative, lastDerivative).Evaluate(value);
        }

        public static double PchipInterpolate(double[] x, double[] y, double value)
        {
            ValidateInterpolationData(x, y, 2);
            EnsureFinite(value, nameof(value));
            int count = x.Length;
            double[] widths = new double[count - 1];
            double[] slopes = new double[count - 1];
            for (int index = 0; index < count - 1; index++)
            {
                widths[index] = x[index + 1] - x[index];
                slopes[index] = (y[index + 1] - y[index]) / widths[index];
            }
            double[] derivatives = new double[count];
            if (count == 2)
            {
                derivatives[0] = slopes[0];
                derivatives[1] = slopes[0];
            }
            else
            {
                derivatives[0] = EndpointSlope(widths[0], widths[1], slopes[0], slopes[1]);
                derivatives[^1] = EndpointSlope(widths[^1], widths[^2], slopes[^1], slopes[^2]);
                for (int index = 1; index < count - 1; index++)
                {
                    if (slopes[index - 1] == 0.0 || slopes[index] == 0.0 || Math.Sign(slopes[index - 1]) != Math.Sign(slopes[index]))
                    {
                        derivatives[index] = 0.0;
                    }
                    else
                    {
                        double firstWeight = 2.0 * widths[index] + widths[index - 1];
                        double secondWeight = widths[index] + 2.0 * widths[index - 1];
                        derivatives[index] = (firstWeight + secondWeight)
                            / (firstWeight / slopes[index - 1] + secondWeight / slopes[index]);
                    }
                }
            }

            int interval = FindInterval(x, value);
            double h = widths[interval];
            double t = (value - x[interval]) / h;
            double t2 = t * t;
            double t3 = t2 * t;
            return (2.0 * t3 - 3.0 * t2 + 1.0) * y[interval]
                + (t3 - 2.0 * t2 + t) * h * derivatives[interval]
                + (-2.0 * t3 + 3.0 * t2) * y[interval + 1]
                + (t3 - t2) * h * derivatives[interval + 1];
        }

        public static CurveFitResult PolynomialLeastSquares(double[] x, double[] y, int degree)
        {
            ValidateFitData(x, y, 1);
            if (degree < 0 || degree >= x.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be non-negative and less than the point count.");
            }
            double[,] design = new double[x.Length, degree + 1];
            for (int row = 0; row < x.Length; row++)
            {
                double power = 1.0;
                for (int column = 0; column <= degree; column++)
                {
                    design[row, column] = power;
                    power *= x[row];
                }
            }
            double[] coefficients = LinearAlgebraAdvanced.LeastSquares(design, y).Solution;
            return BuildCurveFitResult(x, y, coefficients, value => EvaluatePolynomial(coefficients, value));
        }

        public static CurveFitResult ExponentialFit(double[] x, double[] y)
        {
            ValidatePositiveFitData(x, y, nameof(y));
            double[] logarithms = new double[y.Length];
            for (int index = 0; index < y.Length; index++)
            {
                logarithms[index] = Math.Log(y[index]);
            }
            CurveFitResult linear = PolynomialLeastSquares(x, logarithms, 1);
            double[] coefficients = { Math.Exp(linear.Coefficients[0]), linear.Coefficients[1] };
            return BuildCurveFitResult(x, y, coefficients, value => coefficients[0] * Math.Exp(coefficients[1] * value));
        }

        public static CurveFitResult PowerFit(double[] x, double[] y)
        {
            ValidatePositiveFitData(x, y, nameof(y));
            double[] logX = new double[x.Length];
            double[] logY = new double[y.Length];
            for (int index = 0; index < x.Length; index++)
            {
                if (x[index] <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(x), "Power-fit x values must be positive.");
                }
                logX[index] = Math.Log(x[index]);
                logY[index] = Math.Log(y[index]);
            }
            CurveFitResult linear = PolynomialLeastSquares(logX, logY, 1);
            double[] coefficients = { Math.Exp(linear.Coefficients[0]), linear.Coefficients[1] };
            return BuildCurveFitResult(x, y, coefficients, value => coefficients[0] * Math.Pow(value, coefficients[1]));
        }

        public static CurveFitResult LogarithmicFit(double[] x, double[] y)
        {
            ValidateFitData(x, y, 2);
            double[] logX = new double[x.Length];
            for (int index = 0; index < x.Length; index++)
            {
                if (x[index] <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(x), "Logarithmic-fit x values must be positive.");
                }
                logX[index] = Math.Log(x[index]);
            }
            CurveFitResult linear = PolynomialLeastSquares(logX, y, 1);
            double[] coefficients = (double[])linear.Coefficients.Clone();
            return BuildCurveFitResult(x, y, coefficients, value => coefficients[0] + coefficients[1] * Math.Log(value));
        }

        public static RootFindingResult Secant(Func<double, double> function, double firstGuess, double secondGuess, double tolerance = DefaultTolerance, int maxIterations = 100)
        {
            ValidateFunction(function);
            ValidateRootOptions(tolerance, maxIterations);
            EnsureFinite(firstGuess, nameof(firstGuess));
            EnsureFinite(secondGuess, nameof(secondGuess));
            double firstValue = EvaluateFinite(function, firstGuess);
            double secondValue = EvaluateFinite(function, secondGuess);
            if (Math.Abs(firstValue) <= tolerance)
            {
                return new RootFindingResult(firstGuess, Math.Abs(firstValue), 0, true);
            }
            if (Math.Abs(secondValue) <= tolerance)
            {
                return new RootFindingResult(secondGuess, Math.Abs(secondValue), 0, true);
            }
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                double denominator = secondValue - firstValue;
                if (Math.Abs(denominator) <= double.Epsilon * Math.Max(1.0, Math.Abs(secondValue)))
                {
                    return new RootFindingResult(secondGuess, Math.Abs(secondValue), iteration - 1, false);
                }
                double next = secondGuess - secondValue * (secondGuess - firstGuess) / denominator;
                if (!double.IsFinite(next))
                {
                    return new RootFindingResult(secondGuess, Math.Abs(secondValue), iteration, false);
                }
                double nextValue = EvaluateFinite(function, next);
                if (Math.Abs(nextValue) <= tolerance || Math.Abs(next - secondGuess) <= tolerance * (1.0 + Math.Abs(next)))
                {
                    return new RootFindingResult(next, Math.Abs(nextValue), iteration, true);
                }
                firstGuess = secondGuess;
                firstValue = secondValue;
                secondGuess = next;
                secondValue = nextValue;
            }
            return new RootFindingResult(secondGuess, Math.Abs(secondValue), maxIterations, false);
        }

        public static RootFindingResult FalsePosition(Func<double, double> function, double lower, double upper, double tolerance = DefaultTolerance, int maxIterations = 100)
        {
            ValidateBracket(function, lower, upper, tolerance, maxIterations, out double lowerValue, out double upperValue);
            if (lowerValue == 0.0)
            {
                return new RootFindingResult(lower, 0.0, 0, true);
            }
            if (upperValue == 0.0)
            {
                return new RootFindingResult(upper, 0.0, 0, true);
            }
            double root = lower;
            double value = lowerValue;
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                root = (lower * upperValue - upper * lowerValue) / (upperValue - lowerValue);
                value = EvaluateFinite(function, root);
                if (Math.Abs(value) <= tolerance || Math.Abs(upper - lower) <= tolerance * (1.0 + Math.Abs(root)))
                {
                    return new RootFindingResult(root, Math.Abs(value), iteration, true);
                }
                if (Math.Sign(value) == Math.Sign(lowerValue))
                {
                    lower = root;
                    lowerValue = value;
                }
                else
                {
                    upper = root;
                    upperValue = value;
                }
            }
            return new RootFindingResult(root, Math.Abs(value), maxIterations, false);
        }

        public static RootFindingResult FixedPoint(Func<double, double> iteration, double initialGuess, double tolerance = DefaultTolerance, int maxIterations = 100)
        {
            ValidateFunction(iteration);
            ValidateRootOptions(tolerance, maxIterations);
            EnsureFinite(initialGuess, nameof(initialGuess));
            double current = initialGuess;
            for (int step = 1; step <= maxIterations; step++)
            {
                double next = EvaluateFinite(iteration, current);
                double residual = Math.Abs(next - current);
                if (residual <= tolerance * (1.0 + Math.Abs(next)))
                {
                    return new RootFindingResult(next, residual, step, true);
                }
                current = next;
            }
            return new RootFindingResult(current, Math.Abs(EvaluateFinite(iteration, current) - current), maxIterations, false);
        }

        public static RootFindingResult Brent(Func<double, double> function, double lower, double upper, double tolerance = DefaultTolerance, int maxIterations = 100)
        {
            ValidateBracket(function, lower, upper, tolerance, maxIterations, out double lowerValue, out double upperValue);
            if (lowerValue == 0.0)
            {
                return new RootFindingResult(lower, 0.0, 0, true);
            }
            if (upperValue == 0.0)
            {
                return new RootFindingResult(upper, 0.0, 0, true);
            }
            double previousPoint = double.NaN;
            double previousValue = double.NaN;
            double root = lower;
            double value = lowerValue;
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                double width = upper - lower;
                double candidate = upper - upperValue * width / (upperValue - lowerValue);
                if (!double.IsNaN(previousPoint)
                    && lowerValue != upperValue
                    && lowerValue != previousValue
                    && upperValue != previousValue)
                {
                    double inverseQuadratic = lower * upperValue * previousValue
                        / ((lowerValue - upperValue) * (lowerValue - previousValue))
                        + upper * lowerValue * previousValue
                        / ((upperValue - lowerValue) * (upperValue - previousValue))
                        + previousPoint * lowerValue * upperValue
                        / ((previousValue - lowerValue) * (previousValue - upperValue));
                    double minimum = lower + 0.1 * width;
                    double maximum = upper - 0.1 * width;
                    if (double.IsFinite(inverseQuadratic) && inverseQuadratic > minimum && inverseQuadratic < maximum)
                    {
                        candidate = inverseQuadratic;
                    }
                }
                double margin = Math.Max(tolerance * (1.0 + Math.Max(Math.Abs(lower), Math.Abs(upper))), 0.1 * width);
                if (!double.IsFinite(candidate) || candidate <= lower + margin || candidate >= upper - margin)
                {
                    candidate = lower + width * 0.5;
                }
                root = candidate;
                value = EvaluateFinite(function, root);
                if (Math.Abs(value) <= tolerance || width <= tolerance * (1.0 + Math.Abs(root)))
                {
                    return new RootFindingResult(root, Math.Abs(value), iteration, true);
                }
                if (Math.Sign(value) == Math.Sign(lowerValue))
                {
                    previousPoint = lower;
                    previousValue = lowerValue;
                    lower = root;
                    lowerValue = value;
                }
                else
                {
                    previousPoint = upper;
                    previousValue = upperValue;
                    upper = root;
                    upperValue = value;
                }
            }
            return new RootFindingResult(root, Math.Abs(value), maxIterations, false);
        }

        public static SystemRootFindingResult NewtonSystem(Func<double[], double[]> function, Func<double[], double[,]> jacobian, double[] initialGuess, double tolerance = DefaultTolerance, int maxIterations = 100)
        {
            ValidateFunction(function);
            ValidateFunction(jacobian);
            if (initialGuess == null)
            {
                throw new ArgumentNullException(nameof(initialGuess));
            }
            if (initialGuess.Length == 0)
            {
                throw new ArgumentException("The initial guess must not be empty.", nameof(initialGuess));
            }
            ValidateRootOptions(tolerance, maxIterations);
            double[] current = (double[])initialGuess.Clone();
            EnsureFinite(current, nameof(initialGuess));
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double[] residual = EvaluateSystem(function, current, current.Length);
                double residualNorm = Norm(residual);
                if (residualNorm <= tolerance)
                {
                    return new SystemRootFindingResult(current, residualNorm, iteration, true);
                }
                double[,] derivative = jacobian((double[])current.Clone());
                if (derivative == null || derivative.GetLength(0) != current.Length || derivative.GetLength(1) != current.Length)
                {
                    throw new ArgumentException("The Jacobian must be a square matrix matching the system dimension.", nameof(jacobian));
                }
                EnsureFinite(derivative, nameof(jacobian));
                if (!Algebra.TrySolveLinearSystem(derivative, residual, out double[] correction))
                {
                    return new SystemRootFindingResult(current, residualNorm, iteration, false);
                }
                bool accepted = false;
                double scale = 1.0;
                for (int lineSearch = 0; lineSearch < 30; lineSearch++)
                {
                    double[] candidate = new double[current.Length];
                    for (int index = 0; index < current.Length; index++)
                    {
                        candidate[index] = current[index] - scale * correction[index];
                    }
                    double[] candidateResidual = EvaluateSystem(function, candidate, current.Length);
                    if (Norm(candidateResidual) < residualNorm)
                    {
                        current = candidate;
                        accepted = true;
                        break;
                    }
                    scale *= 0.5;
                }
                if (!accepted)
                {
                    return new SystemRootFindingResult(current, residualNorm, iteration + 1, false);
                }
            }
            return new SystemRootFindingResult(current, Norm(EvaluateSystem(function, current, current.Length)), maxIterations, false);
        }

        public static IntegrationResult Romberg(Func<double, double> function, double lower, double upper, double tolerance = DefaultTolerance, int maxLevels = 20)
        {
            ValidateQuadrature(function, lower, upper);
            EnsurePositiveFinite(tolerance, nameof(tolerance));
            if (maxLevels < 1 || maxLevels > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(maxLevels), "Romberg levels must be in [1, 20].");
            }
            if (lower == upper)
            {
                return new IntegrationResult(0.0, 0.0, 0);
            }
            int evaluations = 0;
            double Evaluate(double x)
            {
                evaluations++;
                return EvaluateFinite(function, x);
            }
            double[] previous = new double[maxLevels];
            double[] current = new double[maxLevels];
            double width = upper - lower;
            previous[0] = width * (Evaluate(lower) + Evaluate(upper)) * 0.5;
            double error = double.PositiveInfinity;
            for (int level = 1; level < maxLevels; level++)
            {
                int newPoints = 1 << (level - 1);
                double spacing = width / (1 << level);
                double sum = 0.0;
                for (int index = 0; index < newPoints; index++)
                {
                    sum += Evaluate(lower + (2 * index + 1) * spacing);
                }
                current[0] = 0.5 * previous[0] + spacing * sum;
                double factor = 4.0;
                for (int order = 1; order <= level; order++)
                {
                    current[order] = current[order - 1] + (current[order - 1] - previous[order - 1]) / (factor - 1.0);
                    factor *= 4.0;
                }
                error = Math.Abs(current[level] - previous[level - 1]);
                if (error <= tolerance * (1.0 + Math.Abs(current[level])))
                {
                    return new IntegrationResult(current[level], error, evaluations);
                }
                (previous, current) = (current, previous);
            }
            return new IntegrationResult(previous[maxLevels - 1], error, evaluations);
        }

        public static IntegrationResult AdaptiveSimpson(Func<double, double> function, double lower, double upper, double tolerance = DefaultTolerance, int maxDepth = 20)
        {
            ValidateQuadrature(function, lower, upper);
            EnsurePositiveFinite(tolerance, nameof(tolerance));
            if (maxDepth < 1 || maxDepth > 40)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), "Adaptive Simpson depth must be in [1, 40].");
            }
            if (lower == upper)
            {
                return new IntegrationResult(0.0, 0.0, 0);
            }
            int evaluations = 0;
            double Evaluate(double x)
            {
                evaluations++;
                return EvaluateFinite(function, x);
            }
            double middle = lower + (upper - lower) * 0.5;
            double first = Evaluate(lower);
            double center = Evaluate(middle);
            double last = Evaluate(upper);
            double whole = SimpsonSegment(lower, upper, first, center, last);
            (double value, double error) = AdaptiveSimpsonRecursive(Evaluate, lower, upper, first, center, last, whole, tolerance, maxDepth);
            return new IntegrationResult(value, error, evaluations);
        }

        public static IntegrationResult GaussLegendre(Func<double, double> function, double lower, double upper, int order)
        {
            ValidateQuadrature(function, lower, upper);
            if (order < 2 || order > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(order), "Gauss-Legendre order must be in [2, 10].");
            }
            if (lower == upper)
            {
                return new IntegrationResult(0.0, 0.0, 0);
            }
            double midpoint = lower + (upper - lower) * 0.5;
            double halfWidth = (upper - lower) * 0.5;
            double sum = 0.0;
            int evaluations = 0;
            int roots = (order + 1) / 2;
            for (int index = 0; index < roots; index++)
            {
                double node = Math.Cos(Math.PI * (index + 0.75) / (order + 0.5));
                double derivative = 0.0;
                for (int iteration = 0; iteration < 100; iteration++)
                {
                    (double polynomial, derivative) = LegendrePolynomial(order, node);
                    double next = node - polynomial / derivative;
                    if (Math.Abs(next - node) <= 1e-15)
                    {
                        node = next;
                        break;
                    }
                    node = next;
                }
                (_, derivative) = LegendrePolynomial(order, node);
                double weight = 2.0 / ((1.0 - node * node) * derivative * derivative);
                double leftValue = EvaluateFinite(function, midpoint - halfWidth * node);
                evaluations++;
                if (Math.Abs(node) <= 1e-15)
                {
                    sum += weight * leftValue;
                }
                else
                {
                    double rightValue = EvaluateFinite(function, midpoint + halfWidth * node);
                    evaluations++;
                    sum += weight * (leftValue + rightValue);
                }
            }
            return new IntegrationResult(sum * halfWidth, 0.0, evaluations);
        }

        public static IntegrationResult MonteCarloIntegral(Func<double, double> function, double lower, double upper, int samples, Random random)
        {
            ValidateQuadrature(function, lower, upper);
            if (samples < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(samples));
            }
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }
            if (lower == upper)
            {
                return new IntegrationResult(0.0, 0.0, 0);
            }
            double mean = 0.0;
            double sumSquares = 0.0;
            for (int index = 0; index < samples; index++)
            {
                double value = EvaluateFinite(function, lower + (upper - lower) * random.NextDouble());
                double delta = value - mean;
                mean += delta / (index + 1.0);
                sumSquares += delta * (value - mean);
            }
            double width = upper - lower;
            double error = samples > 1 ? Math.Abs(width) * Math.Sqrt(sumSquares / (samples - 1.0) / samples) : double.PositiveInfinity;
            return new IntegrationResult(width * mean, error, samples);
        }

        public static IterativeSolverResult Jacobi(double[,] matrix, double[] rightHandSide, double[] initialGuess, double tolerance = DefaultTolerance, int maxIterations = 1000)
        {
            ValidateLinearSystem(matrix, rightHandSide, initialGuess, tolerance, maxIterations);
            int size = rightHandSide.Length;
            double[] current = (double[])initialGuess.Clone();
            double[] next = new double[size];
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                for (int row = 0; row < size; row++)
                {
                    double diagonal = matrix[row, row];
                    if (Math.Abs(diagonal) <= 1e-14)
                    {
                        throw new InvalidOperationException("Jacobi iteration requires a non-zero diagonal.");
                    }
                    double sum = rightHandSide[row];
                    for (int column = 0; column < size; column++)
                    {
                        if (column != row)
                        {
                            sum -= matrix[row, column] * current[column];
                        }
                    }
                    next[row] = sum / diagonal;
                }
                double residual = ResidualNorm(matrix, next, rightHandSide);
                if (residual <= tolerance)
                {
                    return new IterativeSolverResult((double[])next.Clone(), residual, iteration, true);
                }
                (current, next) = (next, current);
            }
            return new IterativeSolverResult(current, ResidualNorm(matrix, current, rightHandSide), maxIterations, false);
        }

        public static IterativeSolverResult GaussSeidel(double[,] matrix, double[] rightHandSide, double[] initialGuess, double tolerance = DefaultTolerance, int maxIterations = 1000)
        {
            ValidateLinearSystem(matrix, rightHandSide, initialGuess, tolerance, maxIterations);
            int size = rightHandSide.Length;
            double[] current = (double[])initialGuess.Clone();
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                for (int row = 0; row < size; row++)
                {
                    double diagonal = matrix[row, row];
                    if (Math.Abs(diagonal) <= 1e-14)
                    {
                        throw new InvalidOperationException("Gauss-Seidel iteration requires a non-zero diagonal.");
                    }
                    double sum = rightHandSide[row];
                    for (int column = 0; column < size; column++)
                    {
                        if (column != row)
                        {
                            sum -= matrix[row, column] * current[column];
                        }
                    }
                    current[row] = sum / diagonal;
                }
                double residual = ResidualNorm(matrix, current, rightHandSide);
                if (residual <= tolerance)
                {
                    return new IterativeSolverResult((double[])current.Clone(), residual, iteration, true);
                }
            }
            return new IterativeSolverResult(current, ResidualNorm(matrix, current, rightHandSide), maxIterations, false);
        }

        public static IterativeSolverResult ConjugateGradient(double[,] matrix, double[] rightHandSide, double[] initialGuess, double tolerance = DefaultTolerance, int maxIterations = 1000)
        {
            ValidateLinearSystem(matrix, rightHandSide, initialGuess, tolerance, maxIterations);
            if (!LinearAlgebraAdvanced.IsSymmetric(matrix))
            {
                throw new ArgumentException("Conjugate gradient requires a symmetric matrix.", nameof(matrix));
            }
            int size = rightHandSide.Length;
            double[] solution = (double[])initialGuess.Clone();
            double[] residual = VectorSubtract(rightHandSide, Multiply(matrix, solution));
            double[] direction = (double[])residual.Clone();
            double residualSquared = Dot(residual, residual);
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                double[] product = Multiply(matrix, direction);
                double denominator = Dot(direction, product);
                if (denominator <= 0.0)
                {
                    return new IterativeSolverResult(solution, Math.Sqrt(residualSquared), iteration - 1, false);
                }
                double alpha = residualSquared / denominator;
                for (int index = 0; index < size; index++)
                {
                    solution[index] += alpha * direction[index];
                    residual[index] -= alpha * product[index];
                }
                double nextSquared = Dot(residual, residual);
                if (Math.Sqrt(nextSquared) <= tolerance)
                {
                    return new IterativeSolverResult(solution, Math.Sqrt(nextSquared), iteration, true);
                }
                double beta = nextSquared / residualSquared;
                for (int index = 0; index < size; index++)
                {
                    direction[index] = residual[index] + beta * direction[index];
                }
                residualSquared = nextSquared;
            }
            return new IterativeSolverResult(solution, ResidualNorm(matrix, solution, rightHandSide), maxIterations, false);
        }

        public static OdeSolution RungeKutta45(Func<double, double, double> derivative, double start, double end, double initialValue, double tolerance = 1e-6)
        {
            ValidateFunction(derivative);
            double[][] initial = { new[] { initialValue } };
            OdeSystemSolution system = RungeKutta45System((time, values) => new[] { derivative(time, values[0]) }, start, end, initial[0], tolerance);
            double[] result = new double[system.X.Length];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = system.Y[index][0];
            }
            return new OdeSolution(system.X, result);
        }

        public static OdeSolution DormandPrince(Func<double, double, double> derivative, double start, double end, double initialValue, double tolerance = 1e-6)
        {
            return RungeKutta45(derivative, start, end, initialValue, tolerance);
        }

        public static OdeSystemSolution RungeKutta45System(
            Func<double, double[], double[]> derivative,
            double start,
            double end,
            double[] initialValues,
            double tolerance = 1e-6,
            int maxSteps = MaximumOdeSteps)
        {
            ValidateFunction(derivative);
            EnsureFinite(start, nameof(start));
            EnsureFinite(end, nameof(end));
            if (end <= start)
            {
                throw new ArgumentOutOfRangeException(nameof(end), "The end of the integration interval must exceed the start.");
            }
            EnsurePositiveFinite(tolerance, nameof(tolerance));
            if (maxSteps < 1 || maxSteps > MaximumOdeSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSteps), $"Maximum steps must be in [1, {MaximumOdeSteps}].");
            }
            if (initialValues == null || initialValues.Length == 0)
            {
                throw new ArgumentException("Initial values must be non-empty.", nameof(initialValues));
            }
            EnsureFinite(initialValues, nameof(initialValues));

            List<double> times = new List<double> { start };
            List<double[]> values = new List<double[]> { (double[])initialValues.Clone() };
            double time = start;
            double[] state = (double[])initialValues.Clone();
            double step = (end - start) / 100.0;
            int attempts = 0;
            while (attempts < maxSteps && time < end)
            {
                attempts++;
                step = Math.Min(step, end - time);
                if (step <= Math.Max(1e-14, Math.Abs(time) * 1e-14))
                {
                    throw new InvalidOperationException("Adaptive Runge-Kutta step size underflow.");
                }
                (double[] fifth, double[] error) = DormandPrinceStep(derivative, time, state, step);
                double errorNorm = Norm(error) / (1.0 + Norm(fifth));
                double factor = errorNorm == 0.0 ? 5.0 : Math.Clamp(0.9 * Math.Pow(tolerance / errorNorm, 0.2), 0.2, 5.0);
                if (errorNorm <= tolerance)
                {
                    time += step;
                    state = fifth;
                    if (end - time <= 1e-14 * (1.0 + Math.Abs(end)))
                    {
                        time = end;
                    }
                    times.Add(time);
                    values.Add((double[])state.Clone());
                }
                step *= factor;
            }
            if (time < end)
            {
                throw new InvalidOperationException("Adaptive Runge-Kutta exceeded the maximum step count.");
            }
            return new OdeSystemSolution(times.ToArray(), values.ToArray());
        }

        public static double RichardsonExtrapolation(Func<double, double> estimate, double step, int levels)
        {
            ValidateFunction(estimate);
            EnsurePositiveFinite(step, nameof(step));
            if (levels < 1 || levels > 20)
            {
                throw new ArgumentOutOfRangeException(nameof(levels));
            }
            double[] row = new double[levels + 1];
            for (int level = 0; level <= levels; level++)
            {
                row[level] = EvaluateFinite(estimate, step / (1 << level));
                double factor = 4.0;
                for (int order = 1; order <= level; order++)
                {
                    row[level - order] = row[level - order + 1]
                        + (row[level - order + 1] - row[level - order]) / (factor - 1.0);
                    factor *= 4.0;
                }
            }
            return row[0];
        }

        public static double AitkenDeltaSquared(double first, double second, double third)
        {
            EnsureFinite(first, nameof(first));
            EnsureFinite(second, nameof(second));
            EnsureFinite(third, nameof(third));
            double denominator = third - (2.0 * second) + first;
            if (Math.Abs(denominator) <= 1e-14 * (1.0 + Math.Abs(first) + Math.Abs(second) + Math.Abs(third)))
            {
                throw new InvalidOperationException("Aitken acceleration denominator is too close to zero.");
            }
            return first - ((second - first) * (second - first) / denominator);
        }

        internal static void ValidateInterpolationData(double[] x, double[] y, int minimumCount)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }
            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }
            if (x.Length != y.Length || x.Length < minimumCount)
            {
                throw new ArgumentException("Interpolation arrays must have equal lengths and contain enough points.");
            }
            EnsureFinite(x, nameof(x));
            EnsureFinite(y, nameof(y));
            for (int index = 1; index < x.Length; index++)
            {
                if (x[index] <= x[index - 1])
                {
                    throw new ArgumentException("Interpolation x-values must be strictly increasing.", nameof(x));
                }
            }
        }

        private static double[] DividedDifferences(double[] x, double[] y)
        {
            double[] coefficients = (double[])y.Clone();
            for (int order = 1; order < x.Length; order++)
            {
                for (int index = x.Length - 1; index >= order; index--)
                {
                    coefficients[index] = (coefficients[index] - coefficients[index - 1]) / (x[index] - x[index - order]);
                }
            }
            return coefficients;
        }

        private static double EndpointSlope(double firstWidth, double secondWidth, double firstSlope, double secondSlope)
        {
            double slope = ((2.0 * firstWidth + secondWidth) * firstSlope - firstWidth * secondSlope) / (firstWidth + secondWidth);
            if (Math.Sign(slope) != Math.Sign(firstSlope))
            {
                return 0.0;
            }
            if (Math.Sign(firstSlope) != Math.Sign(secondSlope) && Math.Abs(slope) > Math.Abs(3.0 * firstSlope))
            {
                return 3.0 * firstSlope;
            }
            return slope;
        }

        private static int FindInterval(double[] x, double value)
        {
            if (value <= x[0])
            {
                return 0;
            }
            if (value >= x[^1])
            {
                return x.Length - 2;
            }
            int low = 0;
            int high = x.Length - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (x[middle] > value)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }
            return low;
        }

        private static CurveFitResult BuildCurveFitResult(double[] x, double[] y, double[] coefficients, Func<double, double> predictor)
        {
            double mean = 0.0;
            foreach (double value in y)
            {
                mean += value;
            }
            mean /= y.Length;
            double total = 0.0;
            double residual = 0.0;
            for (int index = 0; index < y.Length; index++)
            {
                total += (y[index] - mean) * (y[index] - mean);
                double error = y[index] - predictor(x[index]);
                residual += error * error;
            }
            double rSquared = total == 0.0 ? (residual == 0.0 ? 1.0 : 0.0) : 1.0 - residual / total;
            return new CurveFitResult(coefficients, rSquared, Math.Sqrt(residual / y.Length));
        }

        private static double EvaluatePolynomial(double[] coefficients, double value)
        {
            double result = 0.0;
            for (int index = coefficients.Length - 1; index >= 0; index--)
            {
                result = result * value + coefficients[index];
            }
            return result;
        }

        private static void ValidatePositiveFitData(double[] x, double[] y, string yName)
        {
            ValidateFitData(x, y, 2);
            for (int index = 0; index < y.Length; index++)
            {
                if (y[index] <= 0.0)
                {
                    throw new ArgumentOutOfRangeException(yName, "Fit y-values must be positive.");
                }
            }
        }

        private static void ValidateFitData(double[] x, double[] y, int minimumCount)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }
            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }
            if (x.Length != y.Length || x.Length < minimumCount)
            {
                throw new ArgumentException("Fit arrays must have equal lengths and contain enough points.");
            }
            EnsureFinite(x, nameof(x));
            EnsureFinite(y, nameof(y));
        }

        private static void ValidateFunction(Delegate function)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
        }

        private static void ValidateRootOptions(double tolerance, int maxIterations)
        {
            EnsurePositiveFinite(tolerance, nameof(tolerance));
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations));
            }
        }

        private static void ValidateBracket(Func<double, double> function, double lower, double upper, double tolerance, int maxIterations, out double lowerValue, out double upperValue)
        {
            ValidateFunction(function);
            ValidateRootOptions(tolerance, maxIterations);
            EnsureFinite(lower, nameof(lower));
            EnsureFinite(upper, nameof(upper));
            if (upper <= lower)
            {
                throw new ArgumentOutOfRangeException(nameof(upper), "Upper bracket bound must exceed lower bound.");
            }
            lowerValue = EvaluateFinite(function, lower);
            upperValue = EvaluateFinite(function, upper);
            if (lowerValue != 0.0 && upperValue != 0.0 && Math.Sign(lowerValue) == Math.Sign(upperValue))
            {
                throw new ArgumentException("The function values at the bracket endpoints must have opposite signs.");
            }
        }

        private static void ValidateQuadrature(Func<double, double> function, double lower, double upper)
        {
            ValidateFunction(function);
            EnsureFinite(lower, nameof(lower));
            EnsureFinite(upper, nameof(upper));
            if (upper < lower)
            {
                throw new ArgumentOutOfRangeException(nameof(upper), "Quadrature bounds must be ordered.");
            }
        }

        private static (double Value, double Error) AdaptiveSimpsonRecursive(
            Func<double, double> evaluate,
            double lower,
            double upper,
            double first,
            double center,
            double last,
            double whole,
            double tolerance,
            int depth)
        {
            double middle = lower + (upper - lower) * 0.5;
            double leftMiddle = lower + (middle - lower) * 0.5;
            double rightMiddle = middle + (upper - middle) * 0.5;
            double leftCenter = evaluate(leftMiddle);
            double rightCenter = evaluate(rightMiddle);
            double left = SimpsonSegment(lower, middle, first, leftCenter, center);
            double right = SimpsonSegment(middle, upper, center, rightCenter, last);
            double refined = left + right;
            double error = Math.Abs(refined - whole) / 15.0;
            if (depth <= 0 || error <= tolerance * (1.0 + Math.Abs(refined)))
            {
                return (refined + (refined - whole) / 15.0, error);
            }
            (double leftValue, double leftError) = AdaptiveSimpsonRecursive(evaluate, lower, middle, first, leftCenter, center, left, tolerance * 0.5, depth - 1);
            (double rightValue, double rightError) = AdaptiveSimpsonRecursive(evaluate, middle, upper, center, rightCenter, last, right, tolerance * 0.5, depth - 1);
            return (leftValue + rightValue, leftError + rightError);
        }

        private static double SimpsonSegment(double lower, double upper, double first, double middle, double last)
        {
            return (upper - lower) * (first + 4.0 * middle + last) / 6.0;
        }

        private static (double Value, double Derivative) LegendrePolynomial(int order, double value)
        {
            double previous = 1.0;
            double current = value;
            if (order == 0)
            {
                return (previous, 0.0);
            }
            for (int degree = 2; degree <= order; degree++)
            {
                double next = ((2.0 * degree - 1.0) * value * current - (degree - 1.0) * previous) / degree;
                previous = current;
                current = next;
            }
            double derivative = order * (value * current - previous) / (value * value - 1.0);
            return (current, derivative);
        }

        private static void ValidateLinearSystem(double[,] matrix, double[] rightHandSide, double[] initialGuess, double tolerance, int maxIterations)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }
            if (rightHandSide == null)
            {
                throw new ArgumentNullException(nameof(rightHandSide));
            }
            if (initialGuess == null)
            {
                throw new ArgumentNullException(nameof(initialGuess));
            }
            int size = matrix.GetLength(0);
            if (size == 0 || matrix.GetLength(1) != size || rightHandSide.Length != size || initialGuess.Length != size)
            {
                throw new ArgumentException("The matrix must be square and match both vectors.");
            }
            ValidateRootOptions(tolerance, maxIterations);
            EnsureFinite(matrix, nameof(matrix));
            EnsureFinite(rightHandSide, nameof(rightHandSide));
            EnsureFinite(initialGuess, nameof(initialGuess));
        }

        private static double ResidualNorm(double[,] matrix, double[] solution, double[] rightHandSide)
        {
            return Norm(VectorSubtract(Multiply(matrix, solution), rightHandSide));
        }

        private static double[] Multiply(double[,] matrix, double[] vector)
        {
            return LinearAlgebraAdvanced.MatrixVectorMultiply(matrix, vector);
        }

        private static double[] VectorSubtract(double[] first, double[] second)
        {
            double[] result = new double[first.Length];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = first[index] - second[index];
            }
            return result;
        }

        private static double Dot(double[] first, double[] second)
        {
            double sum = 0.0;
            for (int index = 0; index < first.Length; index++)
            {
                sum += first[index] * second[index];
            }
            return sum;
        }

        private static double Norm(double[] vector)
        {
            return Math.Sqrt(Dot(vector, vector));
        }

        private static (double[] FifthOrder, double[] Error) DormandPrinceStep(
            Func<double, double[], double[]> derivative,
            double time,
            double[] state,
            double step)
        {
            double[] k1 = EvaluateSystem(derivative, time, state, state.Length);
            double[] k2 = EvaluateSystem(derivative, time + step / 5.0, Combine(state, step, (1.0 / 5.0, k1)), state.Length);
            double[] k3 = EvaluateSystem(derivative, time + 3.0 * step / 10.0, Combine(state, step, (3.0 / 40.0, k1), (9.0 / 40.0, k2)), state.Length);
            double[] k4 = EvaluateSystem(derivative, time + 4.0 * step / 5.0, Combine(state, step, (44.0 / 45.0, k1), (-56.0 / 15.0, k2), (32.0 / 9.0, k3)), state.Length);
            double[] k5 = EvaluateSystem(derivative, time + 8.0 * step / 9.0, Combine(state, step, (19372.0 / 6561.0, k1), (-25360.0 / 2187.0, k2), (64448.0 / 6561.0, k3), (-212.0 / 729.0, k4)), state.Length);
            double[] k6 = EvaluateSystem(derivative, time + step, Combine(state, step, (9017.0 / 3168.0, k1), (-355.0 / 33.0, k2), (46732.0 / 5247.0, k3), (49.0 / 176.0, k4), (-5103.0 / 18656.0, k5)), state.Length);
            double[] fifth = Combine(state, step, (35.0 / 384.0, k1), (500.0 / 1113.0, k3), (125.0 / 192.0, k4), (-2187.0 / 6784.0, k5), (11.0 / 84.0, k6));
            double[] k7 = EvaluateSystem(derivative, time + step, fifth, state.Length);
            double[] fourth = Combine(state, step, (5179.0 / 57600.0, k1), (7571.0 / 16695.0, k3), (393.0 / 640.0, k4), (-92097.0 / 339200.0, k5), (187.0 / 2100.0, k6), (1.0 / 40.0, k7));
            return (fifth, VectorSubtract(fifth, fourth));
        }

        private static double[] Combine(double[] state, double step, params (double Coefficient, double[] Derivative)[] terms)
        {
            double[] result = (double[])state.Clone();
            for (int index = 0; index < result.Length; index++)
            {
                double increment = 0.0;
                foreach ((double coefficient, double[] derivative) in terms)
                {
                    increment += coefficient * derivative[index];
                }
                result[index] += step * increment;
            }
            return result;
        }

        private static double[] EvaluateSystem(Func<double, double[], double[]> function, double time, double[] state, int expectedLength)
        {
            EnsureFinite(time, nameof(time));
            double[] result = function(time, (double[])state.Clone());
            if (result == null || result.Length != expectedLength)
            {
                throw new ArgumentException("ODE derivative must return a vector matching the state dimension.", nameof(function));
            }
            EnsureFinite(result, nameof(function));
            return result;
        }

        private static double[] EvaluateSystem(Func<double[], double[]> function, double[] point, int expectedLength)
        {
            double[] result = function((double[])point.Clone());
            if (result == null || result.Length != expectedLength)
            {
                throw new ArgumentException("System function must return a vector matching the point dimension.", nameof(function));
            }
            EnsureFinite(result, nameof(function));
            return result;
        }

        private static double EvaluateFinite(Func<double, double> function, double value)
        {
            double result = function(value);
            if (!double.IsFinite(result))
            {
                throw new InvalidOperationException("The function returned a non-finite value.");
            }
            return result;
        }

        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        private static void EnsureFinite(double[] values, string parameterName)
        {
            foreach (double value in values)
            {
                EnsureFinite(value, parameterName);
            }
        }

        private static void EnsureFinite(double[,] values, string parameterName)
        {
            for (int row = 0; row < values.GetLength(0); row++)
            {
                for (int column = 0; column < values.GetLength(1); column++)
                {
                    EnsureFinite(values[row, column], parameterName);
                }
            }
        }

        private static void EnsurePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
            }
        }
    }
}
