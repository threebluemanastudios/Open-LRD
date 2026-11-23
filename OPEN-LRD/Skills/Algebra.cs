using System;
using System.Collections.Generic;
using System.Globalization;
using OPEN_LRD.Foundations;
using Complex = System.Numerics.Complex;

namespace OPEN_LRD.Skills
{
    internal enum InequalityRelation
    {
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual
    }

    internal readonly record struct Interval(double Lower, double Upper, bool LowerClosed, bool UpperClosed)
    {
        /// <summary>
        /// </summary>
        /// </summary>
        /// <returns>The result.</returns>
        public override string ToString()
        {
            string lower = double.IsNegativeInfinity(Lower)
                ? "-∞"
                : Lower.ToString("G", CultureInfo.InvariantCulture);
            string upper = double.IsPositiveInfinity(Upper)
                ? "∞"
                : Upper.ToString("G", CultureInfo.InvariantCulture);
            string left = LowerClosed && !double.IsNegativeInfinity(Lower) ? "[" : "(";
            string right = UpperClosed && !double.IsPositiveInfinity(Upper) ? "]" : ")";
            return $"{left}{lower}, {upper}{right}";
        }
    }

    internal enum LinearSolutionKind
    {
        Unique,
        NoSolution,
        Infinite
    }

    internal sealed record LinearSolution(LinearSolutionKind Kind, double Value);

    internal sealed record PolynomialDivisionResult(double[] Quotient, double[] Remainder);

    internal sealed record PolynomialFactor(double[] Factor, int Multiplicity);

    internal readonly record struct SquareRootSimplification(int Outside, int Inside);

    internal readonly record struct Quaternion(double W, double X, double Y, double Z)
    {
        /// <summary>
        /// </summary>
        /// </summary>
        public static Quaternion Identity => new Quaternion(1.0, 0.0, 0.0, 0.0);

        /// <summary>
        /// </summary>
        /// </summary>
        /// <returns>The result.</returns>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} + {1}i + {2}j + {3}k",
                W,
                X,
                Y,
                Z);
        }
    }

    internal static class Algebra
    {
        private const double Tolerance = 1e-9;
        private const int MaxExpansionDepth = 64;
        private const int MaxExpansionPower = 32;

        /// <summary>
        /// Normalizes a polynomial coefficient array by trimming trailing near-zero or leading zero terms into a canonical representation.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static double[] NormalizePolynomial(double[] coefficients)
        {
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }
            if (coefficients.Length == 0)
            {
                return new[] { 0.0 };
            }
            int last = coefficients.Length - 1;
            while (last > 0 && Math.Abs(coefficients[last]) <= Tolerance)
            {
                last--;
            }
            double[] result = new double[last + 1];
            Array.Copy(coefficients, result, last + 1);
            if (result.Length == 1 && Math.Abs(result[0]) <= Tolerance)
            {
                result[0] = 0.0;
            }
            return result;
        }

        /// <summary>
        /// Computes the algebraic degree of the polynomial represented by coefficients.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static int PolynomialDegree(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            return IsZeroPolynomial(polynomial) ? -1 : polynomial.Length - 1;
        }

        /// <summary>
        /// Returns the leading coefficient of the highest-degree non-zero term in the polynomial.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static double PolynomialLeadingCoefficient(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            return IsZeroPolynomial(polynomial) ? 0.0 : polynomial[polynomial.Length - 1];
        }

        /// <summary>
        /// Evaluates the polynomial at a Complex number value (typically via Horner's method).
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double PolynomialEvaluate(double[] coefficients, double value)
        {
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }
            double result = 0.0;
            for (int index = coefficients.Length - 1; index >= 0; index--)
            {
                result = result * value + coefficients[index];
            }
            return result;
        }

        /// <summary>
        /// Evaluates the polynomial at a Complex number value (typically via Horner's method).
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex PolynomialEvaluate(double[] coefficients, Complex value)
        {
            if (coefficients == null)
            {
                throw new ArgumentNullException(nameof(coefficients));
            }
            Complex result = Complex.Zero;
            for (int index = coefficients.Length - 1; index >= 0; index--)
            {
                result = result * value + coefficients[index];
            }
            return result;
        }

        /// <summary>
        /// Computes the sum of two polynomials (left + right) and returns the resulting coefficient array.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[] AddPolynomials(double[] left, double[] right)
        {
            double[] first = NormalizePolynomial(left);
            double[] second = NormalizePolynomial(right);
            int length = Math.Max(first.Length, second.Length);
            double[] result = new double[length];
            for (int index = 0; index < length; index++)
            {
                double firstValue = index < first.Length ? first[index] : 0.0;
                double secondValue = index < second.Length ? second[index] : 0.0;
                result[index] = firstValue + secondValue;
            }
            return NormalizePolynomial(result);
        }

        /// <summary>
        /// Computes the difference of two polynomials (left - right) and returns the resulting coefficient array.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[] SubtractPolynomials(double[] left, double[] right)
        {
            double[] first = NormalizePolynomial(left);
            double[] second = NormalizePolynomial(right);
            int length = Math.Max(first.Length, second.Length);
            double[] result = new double[length];
            for (int index = 0; index < length; index++)
            {
                double firstValue = index < first.Length ? first[index] : 0.0;
                double secondValue = index < second.Length ? second[index] : 0.0;
                result[index] = firstValue - secondValue;
            }
            return NormalizePolynomial(result);
        }

        /// <summary>
        /// Computes the Cauchy product of two polynomials (left * right) and returns the resulting coefficient array.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[] MultiplyPolynomials(double[] left, double[] right)
        {
            double[] first = NormalizePolynomial(left);
            double[] second = NormalizePolynomial(right);
            if (IsZeroPolynomial(first) || IsZeroPolynomial(second))
            {
                return new[] { 0.0 };
            }
            double[] result = new double[first.Length + second.Length - 1];
            for (int firstIndex = 0; firstIndex < first.Length; firstIndex++)
            {
                for (int secondIndex = 0; secondIndex < second.Length; secondIndex++)
                {
                    result[firstIndex + secondIndex] += first[firstIndex] * second[secondIndex];
                }
            }
            return NormalizePolynomial(result);
        }

        /// <summary>
        /// Multiplies every coefficient of the polynomial by a scalar scale factor.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The result.</returns>
        public static double[] ScalePolynomial(double[] coefficients, double scale)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            double[] result = new double[polynomial.Length];
            for (int index = 0; index < polynomial.Length; index++)
            {
                result[index] = polynomial[index] * scale;
            }
            return NormalizePolynomial(result);
        }

        /// <summary>
        /// Computes the functional composition outer(inner(x)) of two polynomials.
        /// </summary>
        /// <param name="outer">The outer.</param>
        /// <param name="inner">The inner.</param>
        /// <returns>The result.</returns>
        public static double[] ComposePolynomials(double[] outer, double[] inner)
        {
            double[] outerPolynomial = NormalizePolynomial(outer);
            double[] innerPolynomial = NormalizePolynomial(inner);
            double[] result = new[] { 0.0 };
            for (int index = outerPolynomial.Length - 1; index >= 0; index--)
            {
                result = AddPolynomials(MultiplyPolynomials(result, innerPolynomial), new[] { outerPolynomial[index] });
            }
            return NormalizePolynomial(result);
        }

        /// <summary>
        /// Performs polynomial long division of dividend by divisor to obtain quotient and remainder.
        /// </summary>
        /// <param name="dividend">The dividend.</param>
        /// <param name="divisor">The divisor.</param>
        /// <returns>The result.</returns>
        public static PolynomialDivisionResult DividePolynomials(double[] dividend, double[] divisor)
        {
            double[] current = NormalizePolynomial(dividend);
            double[] denominator = NormalizePolynomial(divisor);
            if (IsZeroPolynomial(denominator))
            {
                throw new DivideByZeroException("Cannot divide by the zero polynomial.");
            }
            int dividendDegree = current.Length - 1;
            int divisorDegree = denominator.Length - 1;
            if (dividendDegree < divisorDegree)
            {
                return new PolynomialDivisionResult(new[] { 0.0 }, current);
            }
            int quotientLength = dividendDegree - divisorDegree + 1;
            double[] quotient = new double[quotientLength];
            double[] remainder = new double[current.Length];
            Array.Copy(current, remainder, current.Length);
            for (int index = dividendDegree; index >= divisorDegree; index--)
            {
                double coefficient = remainder[index] / denominator[divisorDegree];
                int shift = index - divisorDegree;
                quotient[shift] = coefficient;
                for (int divisorIndex = 0; divisorIndex < denominator.Length; divisorIndex++)
                {
                    remainder[shift + divisorIndex] -= coefficient * denominator[divisorIndex];
                }
            }
            return new PolynomialDivisionResult(NormalizePolynomial(quotient), NormalizePolynomial(remainder));
        }

        /// <summary>
        /// Computes the first symbolic derivative of the polynomial with respect to its variable.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static double[] PolynomialDerivative(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            if (polynomial.Length <= 1)
            {
                return new[] { 0.0 };
            }
            double[] result = new double[polynomial.Length - 1];
            for (int index = 1; index < polynomial.Length; index++)
            {
                result[index - 1] = polynomial[index] * index;
            }
            return NormalizePolynomial(result);
        }

        /// <summary>
        /// Computes the indefinite integral (antiderivative) of the polynomial with constant of integration set to zero.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static double[] PolynomialIntegral(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            double[] result = new double[polynomial.Length + 1];
            for (int index = 0; index < polynomial.Length; index++)
            {
                result[index + 1] = polynomial[index] / (index + 1);
            }
            return NormalizePolynomial(result);
        }

        /// <summary>
        /// Attempts synthetic division of the polynomial by linear factor (x - root), outputting quotient and remainder.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <param name="root">The root.</param>
        /// <param name="quotient">The quotient.</param>
        /// <param name="remainder">The remainder.</param>
        /// <returns>The result.</returns>
        public static bool TrySyntheticDivision(double[] coefficients, double root, out double[] quotient, out double remainder)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            if (polynomial.Length <= 1)
            {
                quotient = new[] { 0.0 };
                remainder = polynomial[0];
                return true;
            }
            int degree = polynomial.Length - 1;
            double[] result = new double[degree];
            double carry = polynomial[degree];
            result[degree - 1] = carry;
            for (int index = degree - 1; index >= 1; index--)
            {
                carry = carry * root + polynomial[index];
                result[index - 1] = carry;
            }
            remainder = carry * root + polynomial[0];
            quotient = NormalizePolynomial(result);
            return true;
        }

        /// <summary>
        /// Computes the polynomial greatest common divisor (GCD) of left and right using the Euclidean algorithm.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[] PolynomialGCD(double[] left, double[] right)
        {
            double[] first = NormalizePolynomial(left);
            double[] second = NormalizePolynomial(right);
            if (IsZeroPolynomial(first))
            {
                return MonicPolynomial(second);
            }
            if (IsZeroPolynomial(second))
            {
                return MonicPolynomial(first);
            }
            while (!IsZeroPolynomial(second))
            {
                PolynomialDivisionResult division = DividePolynomials(first, second);
                first = second;
                second = division.Remainder;
            }
            return MonicPolynomial(first);
        }

        /// <summary>
        /// Computes the polynomial least common multiple (LCM) of left and right.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[] PolynomialLCM(double[] left, double[] right)
        {
            double[] first = NormalizePolynomial(left);
            double[] second = NormalizePolynomial(right);
            if (IsZeroPolynomial(first) || IsZeroPolynomial(second))
            {
                return new[] { 0.0 };
            }
            double[] product = MultiplyPolynomials(first, second);
            double[] gcd = PolynomialGCD(first, second);
            return MonicPolynomial(DividePolynomials(product, gcd).Quotient);
        }

        /// <summary>
        /// Finds all rational roots of a polynomial using the Rational Root Theorem.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static double[] FindRationalRoots(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            List<double> roots = new List<double>();
            if (IsZeroPolynomial(polynomial))
            {
                return roots.ToArray();
            }
            while (polynomial.Length > 1 && Math.Abs(polynomial[0]) <= Tolerance)
            {
                AddDistinctRoot(roots, 0.0);
                TrySyntheticDivision(polynomial, 0.0, out double[] quotient, out _);
                polynomial = quotient;
            }
            if (polynomial.Length <= 1)
            {
                roots.Sort();
                return roots.ToArray();
            }
            if (!HasIntegerCoefficients(polynomial))
            {
                roots.Sort();
                return roots.ToArray();
            }
            long constantTerm = (long)Math.Round(Math.Abs(polynomial[0]));
            long leadingTerm = (long)Math.Round(Math.Abs(polynomial[polynomial.Length - 1]));
            if (constantTerm == 0L || leadingTerm == 0L || constantTerm > 100000L || leadingTerm > 100000L)
            {
                roots.Sort();
                return roots.ToArray();
            }
            List<long> numeratorDivisors = GetDivisors(constantTerm);
            List<long> denominatorDivisors = GetDivisors(leadingTerm);
            foreach (long numeratorDivisor in numeratorDivisors)
            {
                foreach (long denominatorDivisor in denominatorDivisors)
                {
                    double candidate = (double)numeratorDivisor / denominatorDivisor;
                    if (Math.Abs(PolynomialEvaluate(polynomial, candidate)) <= 1e-7)
                    {
                        AddDistinctRoot(roots, candidate);
                    }
                    if (Math.Abs(PolynomialEvaluate(polynomial, -candidate)) <= 1e-7)
                    {
                        AddDistinctRoot(roots, -candidate);
                    }
                }
            }
            roots.Sort();
            return roots.ToArray();
        }

        /// <summary>
        /// Factors a polynomial into lower-degree algebraic or linear/quadratic factors.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static PolynomialFactor[] FactorPolynomial(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            if (IsZeroPolynomial(polynomial))
            {
                throw new ArgumentException("The zero polynomial cannot be factored.", nameof(coefficients));
            }
            List<PolynomialFactor> factors = new List<PolynomialFactor>();
            double[] current = polynomial;
            while (current.Length > 1)
            {
                double[] rationalRoots = FindRationalRoots(current);
                if (rationalRoots.Length == 0)
                {
                    factors.Add(new PolynomialFactor(current, 1));
                    current = new[] { 1.0 };
                    break;
                }
                double root = rationalRoots[0];
                int multiplicity = 0;
                while (current.Length > 1
                    && TrySyntheticDivision(current, root, out double[] quotient, out double remainder)
                    && Math.Abs(remainder) <= 1e-7)
                {
                    current = quotient;
                    multiplicity++;
                }
                if (multiplicity == 0)
                {
                    factors.Add(new PolynomialFactor(current, 1));
                    current = new[] { 1.0 };
                    break;
                }
                factors.Add(new PolynomialFactor(new[] { -root, 1.0 }, multiplicity));
            }
            if (current.Length == 1 && Math.Abs(current[0] - 1.0) > Tolerance)
            {
                factors.Add(new PolynomialFactor(current, 1));
            }
            return factors.ToArray();
        }

        /// <summary>
        /// Attempts to parse and extract polynomial coefficients in variable from an expression string.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static bool TryGetPolynomial(string expression, string variable, out double[] coefficients)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }
            return SymbolicCalculus.TryGetPolynomial(ExpressionParser.Parse(expression), variable, out coefficients);
        }

        /// <summary>
        /// Converts a polynomial coefficient array into a symbolic Expression in variable.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static Expression PolynomialToExpression(double[] coefficients, string variable)
        {
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }
            double[] polynomial = NormalizePolynomial(coefficients);
            Expression result = ExpressionBuilder.Constant(0.0);
            for (int index = 0; index < polynomial.Length; index++)
            {
                double coefficient = polynomial[index];
                if (Math.Abs(coefficient) <= Tolerance)
                {
                    continue;
                }
                Expression term;
                if (index == 0)
                {
                    term = ExpressionBuilder.Constant(coefficient);
                }
                else if (index == 1)
                {
                    term = ExpressionBuilder.Multiply(
                        ExpressionBuilder.Constant(coefficient),
                        ExpressionBuilder.Variable(variable));
                }
                else
                {
                    term = ExpressionBuilder.Multiply(
                        ExpressionBuilder.Constant(coefficient),
                        ExpressionBuilder.Power(
                            ExpressionBuilder.Variable(variable),
                            ExpressionBuilder.Constant(index)));
                }
                result = ExpressionBuilder.Add(result, term);
            }
            return SymbolicCalculus.Simplify(result);
        }

        /// <summary>
        /// Formats a polynomial coefficient array as a human-readable mathematical string in variable.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static string FormatPolynomial(double[] coefficients, string variable)
        {
            return PolynomialToExpression(coefficients, variable).ToString();
        }

        /// <summary>
        /// Solves the linear equation coefficient * x + constant = 0 (or a*x + b = 0) for x.
        /// </summary>
        /// <param name="coefficient">The coefficient.</param>
        /// <param name="constant">The constant.</param>
        /// <returns>The result.</returns>
        public static LinearSolution SolveLinear(double coefficient, double constant)
        {
            if (Math.Abs(coefficient) <= Tolerance)
            {
                return Math.Abs(constant) <= Tolerance
                    ? new LinearSolution(LinearSolutionKind.Infinite, 0.0)
                    : new LinearSolution(LinearSolutionKind.NoSolution, 0.0);
            }
            return new LinearSolution(LinearSolutionKind.Unique, -constant / coefficient);
        }

        /// <summary>
        /// Computes the quadratic discriminant (? = b  - 4ac) for ax  + bx + c = 0.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <returns>The result.</returns>
        public static double CalculateDiscriminant(double a, double b, double c)
        {
            return b * b - 4.0 * a * c;
        }

        /// <summary>
        /// Computes the axis of symmetry (x = -b / (2a)) of the parabola defined by a and b.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <returns>The result.</returns>
        public static double CalculateQuadraticAxisOfSymmetry(double a, double b)
        {
            if (Math.Abs(a) <= Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(a), "The quadratic coefficient cannot be zero.");
            }
            return -b / (2.0 * a);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="a">The quadratic coefficient.</param>
        /// <param name="b">The linear coefficient.</param>
        /// <param name="c">The constant coefficient.</param>
        /// <returns>The vertex coordinates.</returns>
        public static (double X, double Y) CalculateQuadraticVertex(double a, double b, double c)
        {
            double x = CalculateQuadraticAxisOfSymmetry(a, b);
            return (x, a * x * x + b * x + c);
        }

        /// <summary>
        /// Solves ax  + bx + c = 0 for real roots using the quadratic formula.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <returns>The result.</returns>
        public static double[] SolveQuadraticReal(double a, double b, double c)
        {
            if (Math.Abs(a) <= Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(a), "The quadratic coefficient cannot be zero.");
            }
            double discriminant = CalculateDiscriminant(a, b, c);
            double scale = Math.Max(1.0, Math.Abs(b * b) + Math.Abs(4.0 * a * c));
            if (discriminant < -Tolerance * scale)
            {
                return Array.Empty<double>();
            }
            if (Math.Abs(discriminant) <= Tolerance * scale)
            {
                return new[] { -b / (2.0 * a) };
            }
            double root = Math.Sqrt(discriminant);
            double first = (-b + root) / (2.0 * a);
            double second = (-b - root) / (2.0 * a);
            return first <= second ? new[] { first, second } : new[] { second, first };
        }

        /// <summary>
        /// Solves ax  + bx + c = 0 over the complex numbers, including conjugate pairs when discriminant is negative.
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <returns>The result.</returns>
        public static Complex[] SolveQuadraticComplex(double a, double b, double c)
        {
            if (Math.Abs(a) <= Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(a), "The quadratic coefficient cannot be zero.");
            }
            Complex discriminant = b * b - 4.0 * a * c;
            Complex root = Complex.Sqrt(discriminant);
            return new[]
            {
                (-b + root) / (2.0 * a),
                (-b - root) / (2.0 * a)
            };
        }

        /// <summary>
        /// Finds roots (real and/or complex) of the polynomial defined by coefficients.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static Complex[] SolvePolynomial(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            if (IsZeroPolynomial(polynomial))
            {
                throw new ArgumentException("The zero polynomial has infinitely many roots.", nameof(coefficients));
            }
            if (polynomial.Length == 1)
            {
                return Array.Empty<Complex>();
            }
            List<Complex> roots = new List<Complex>();
            double[] current = polynomial;
            while (current.Length > 1)
            {
                double[] rationalRoots = FindRationalRoots(current);
                if (rationalRoots.Length == 0)
                {
                    break;
                }
                bool divided = false;
                foreach (double root in rationalRoots)
                {
                    while (current.Length > 1
                        && TrySyntheticDivision(current, root, out double[] quotient, out double remainder)
                        && Math.Abs(remainder) <= 1e-7)
                    {
                        current = quotient;
                        roots.Add(root);
                        divided = true;
                    }
                    if (current.Length <= 1)
                    {
                        break;
                    }
                }
                if (!divided)
                {
                    break;
                }
            }
            if (current.Length == 2)
            {
                roots.Add(-current[0] / current[1]);
            }
            else if (current.Length == 3)
            {
                roots.AddRange(SolveQuadraticComplex(current[2], current[1], current[0]));
            }
            else if (current.Length > 3)
            {
                roots.AddRange(DurandKerner(current));
            }
            return roots.ToArray();
        }

        /// <summary>
        /// Finds the real roots of the polynomial defined by coefficients.
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <returns>The result.</returns>
        public static double[] SolvePolynomialReal(double[] coefficients)
        {
            Complex[] roots = SolvePolynomial(coefficients);
            List<double> result = new List<double>();
            foreach (Complex root in roots)
            {
                if (Math.Abs(root.Imaginary) <= 1e-7)
                {
                    AddDistinctRoot(result, root.Real, 1e-7);
                }
            }
            result.Sort();
            return result.ToArray();
        }

        /// <summary>
        /// Computes the degree-th root (value^(1/degree)) of value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="degree">The degree.</param>
        /// <returns>The result.</returns>
        public static double NthRoot(double value, int degree)
        {
            if (degree < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), "The root degree must be positive.");
            }
            if (degree == 1)
            {
                return value;
            }
            if (value < 0.0)
            {
                if (degree % 2 == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "An even root of a negative number is not real.");
                }
                return -Math.Pow(-value, 1.0 / degree);
            }
            return Math.Pow(value, 1.0 / degree);
        }

        /// <summary>
        /// Evaluates value^(numerator / denominator) for integer numerator and non-zero denominator.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="numerator">The numerator.</param>
        /// <param name="denominator">The denominator.</param>
        /// <returns>The result.</returns>
        public static double RationalPower(double value, int numerator, int denominator)
        {
            if (denominator == 0)
            {
                throw new DivideByZeroException("The rational-power denominator cannot be zero.");
            }
            if (denominator < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }
            if (value < 0.0 && denominator % 2 == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A rational power with an even denominator is not real.");
            }
            double magnitude = Math.Pow(Math.Abs(value), (double)numerator / denominator);
            if (value < 0.0 && numerator % 2 != 0)
            {
                return -magnitude;
            }
            return magnitude;
        }

        /// <summary>
        /// Simplifies a square root ?radicand by extracting the largest square factor.
        /// </summary>
        /// <param name="radicand">The radicand.</param>
        /// <returns>The result.</returns>
        public static SquareRootSimplification SimplifySquareRoot(int radicand)
        {
            if (radicand < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(radicand), "The radicand cannot be negative.");
            }
            if (radicand == 0)
            {
                return new SquareRootSimplification(0, 0);
            }
            int outside = 1;
            int inside = radicand;
            for (int factor = 2; factor * factor <= inside; factor++)
            {
                while (inside % (factor * factor) == 0)
                {
                    inside /= factor * factor;
                    outside *= factor;
                }
            }
            return new SquareRootSimplification(outside, inside);
        }

        /// <summary>
        /// Solves baseValue^x = result for x using logarithms.
        /// </summary>
        /// <param name="baseValue">The base value.</param>
        /// <param name="result">The result.</param>
        /// <returns>The result.</returns>
        public static double SolveExponentialEquation(double baseValue, double result)
        {
            if (baseValue <= 0.0 || Math.Abs(baseValue - 1.0) <= Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), "The exponential base must be positive and not equal to one.");
            }
            if (result <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(result), "The exponential result must be positive.");
            }
            return Math.Log(result, baseValue);
        }

        /// <summary>
        /// Solves log_{baseValue}(x) = result (or equivalent logarithmic form) for x.
        /// </summary>
        /// <param name="baseValue">The base value.</param>
        /// <param name="result">The result.</param>
        /// <returns>The result.</returns>
        public static double SolveLogarithmicEquation(double baseValue, double result)
        {
            if (baseValue <= 0.0 || Math.Abs(baseValue - 1.0) <= Tolerance)
            {
                throw new ArgumentOutOfRangeException(nameof(baseValue), "The logarithm base must be positive and not equal to one.");
            }
            return Math.Pow(baseValue, result);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixIdentity(int size)
        {
            if (size < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "The matrix size must be positive.");
            }
            double[,] result = new double[size, size];
            for (int index = 0; index < size; index++)
            {
                result[index, index] = 1.0;
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixTranspose(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] result = new double[columns, rows];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    result[column, row] = matrix[row, column];
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixAdd(double[,] left, double[,] right)
        {
            EnsureSameShape(left, right);
            int rows = left.GetLength(0);
            int columns = left.GetLength(1);
            double[,] result = new double[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    result[row, column] = left[row, column] + right[row, column];
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixSubtract(double[,] left, double[,] right)
        {
            EnsureSameShape(left, right);
            int rows = left.GetLength(0);
            int columns = left.GetLength(1);
            double[,] result = new double[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    result[row, column] = left[row, column] - right[row, column];
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixScale(double[,] matrix, double scale)
        {
            EnsureMatrix(matrix, nameof(matrix));
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] result = new double[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    result[row, column] = matrix[row, column] * scale;
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixMultiply(double[,] left, double[,] right)
        {
            EnsureMatrix(left, nameof(left));
            EnsureMatrix(right, nameof(right));
            int leftRows = left.GetLength(0);
            int leftColumns = left.GetLength(1);
            int rightRows = right.GetLength(0);
            int rightColumns = right.GetLength(1);
            if (leftColumns != rightRows)
            {
                throw new ArgumentException("The matrix dimensions are not compatible for multiplication.");
            }
            double[,] result = new double[leftRows, rightColumns];
            for (int row = 0; row < leftRows; row++)
            {
                for (int column = 0; column < rightColumns; column++)
                {
                    double sum = 0.0;
                    for (int index = 0; index < leftColumns; index++)
                    {
                        sum += left[row, index] * right[index, column];
                    }
                    result[row, column] = sum;
                }
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <param name="exponent">The exponent.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixPower(double[,] matrix, int exponent)
        {
            EnsureSquare(matrix, nameof(matrix));
            if (exponent < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exponent), "The matrix exponent cannot be negative.");
            }
            int size = matrix.GetLength(0);
            double[,] result = MatrixIdentity(size);
            double[,] factor = CloneMatrix(matrix);
            int remaining = exponent;
            while (remaining > 0)
            {
                if ((remaining & 1) == 1)
                {
                    result = MatrixMultiply(result, factor);
                }
                remaining >>= 1;
                if (remaining > 0)
                {
                    factor = MatrixMultiply(factor, factor);
                }
            }
            return result;
        }

        /// <summary>
        /// Computes the trace (sum of main diagonal entries) of a square matrix.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>The result.</returns>
        public static double MatrixTrace(double[,] matrix)
        {
            EnsureSquare(matrix, nameof(matrix));
            int size = matrix.GetLength(0);
            double sum = 0.0;
            for (int index = 0; index < size; index++)
            {
                sum += matrix[index, index];
            }
            return sum;
        }

        /// <summary>
        /// Computes the determinant (det(A)) of a square matrix.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>The result.</returns>
        public static double MatrixDeterminant(double[,] matrix)
        {
            EnsureSquare(matrix, nameof(matrix));
            int size = matrix.GetLength(0);
            if (size == 0)
            {
                throw new ArgumentException("The matrix must not be empty.", nameof(matrix));
            }
            double[,] working = CloneMatrix(matrix);
            double determinant = 1.0;
            int sign = 1;
            for (int column = 0; column < size; column++)
            {
                int pivotRow = column;
                double pivotValue = Math.Abs(working[column, column]);
                for (int row = column + 1; row < size; row++)
                {
                    double candidate = Math.Abs(working[row, column]);
                    if (candidate > pivotValue)
                    {
                        pivotValue = candidate;
                        pivotRow = row;
                    }
                }
                if (pivotValue <= Tolerance)
                {
                    return 0.0;
                }
                if (pivotRow != column)
                {
                    SwapRows(working, pivotRow, column);
                    sign = -sign;
                }
                double pivot = working[column, column];
                determinant *= pivot;
                for (int row = column + 1; row < size; row++)
                {
                    double factor = working[row, column] / pivot;
                    if (Math.Abs(factor) <= Tolerance)
                    {
                        continue;
                    }
                    for (int index = column; index < size; index++)
                    {
                        working[row, index] -= factor * working[column, index];
                    }
                }
            }
            return determinant * sign;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixInverse(double[,] matrix)
        {
            EnsureSquare(matrix, nameof(matrix));
            int size = matrix.GetLength(0);
            if (size == 0)
            {
                throw new ArgumentException("The matrix must not be empty.", nameof(matrix));
            }
            double[,] augmented = new double[size, size * 2];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    augmented[row, column] = matrix[row, column];
                }
                augmented[row, size + row] = 1.0;
            }
            for (int column = 0; column < size; column++)
            {
                int pivotRow = column;
                double pivotValue = Math.Abs(augmented[column, column]);
                for (int row = column + 1; row < size; row++)
                {
                    double candidate = Math.Abs(augmented[row, column]);
                    if (candidate > pivotValue)
                    {
                        pivotValue = candidate;
                        pivotRow = row;
                    }
                }
                if (pivotValue <= Tolerance)
                {
                    throw new InvalidOperationException("The matrix is singular and cannot be inverted.");
                }
                if (pivotRow != column)
                {
                    SwapRows(augmented, pivotRow, column);
                }
                double pivot = augmented[column, column];
                for (int index = 0; index < size * 2; index++)
                {
                    augmented[column, index] /= pivot;
                }
                for (int row = 0; row < size; row++)
                {
                    if (row == column)
                    {
                        continue;
                    }
                    double factor = augmented[row, column];
                    if (Math.Abs(factor) <= Tolerance)
                    {
                        continue;
                    }
                    for (int index = 0; index < size * 2; index++)
                    {
                        augmented[row, index] -= factor * augmented[column, index];
                    }
                }
            }
            double[,] result = new double[size, size];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    result[row, column] = augmented[row, size + column];
                }
            }
            return result;
        }

        /// <summary>
        /// Computes the rank (number of linearly independent rows or columns) of the matrix.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>The result.</returns>
        public static int MatrixRank(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            double[,] working = MatrixRowReduce(matrix);
            int rows = working.GetLength(0);
            int columns = working.GetLength(1);
            int rank = 0;
            for (int row = 0; row < rows; row++)
            {
                bool hasPivot = false;
                for (int column = 0; column < columns; column++)
                {
                    if (Math.Abs(working[row, column]) > Tolerance)
                    {
                        hasPivot = true;
                        break;
                    }
                }
                if (hasPivot)
                {
                    rank++;
                }
            }
            return rank;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <returns>The result.</returns>
        public static double[,] MatrixRowReduce(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] working = CloneMatrix(matrix);
            int pivotRow = 0;
            for (int column = 0; column < columns && pivotRow < rows; column++)
            {
                int selectedRow = pivotRow;
                double selectedValue = Math.Abs(working[pivotRow, column]);
                for (int row = pivotRow + 1; row < rows; row++)
                {
                    double candidate = Math.Abs(working[row, column]);
                    if (candidate > selectedValue)
                    {
                        selectedValue = candidate;
                        selectedRow = row;
                    }
                }
                if (selectedValue <= Tolerance)
                {
                    continue;
                }
                if (selectedRow != pivotRow)
                {
                    SwapRows(working, selectedRow, pivotRow);
                }
                double pivot = working[pivotRow, column];
                for (int index = column; index < columns; index++)
                {
                    working[pivotRow, index] /= pivot;
                }
                for (int row = 0; row < rows; row++)
                {
                    if (row == pivotRow)
                    {
                        continue;
                    }
                    double factor = working[row, column];
                    if (Math.Abs(factor) <= Tolerance)
                    {
                        continue;
                    }
                    for (int index = column; index < columns; index++)
                    {
                        working[row, index] -= factor * working[pivotRow, index];
                    }
                }
                pivotRow++;
            }
            return working;
        }

        /// <summary>
        /// Attempts to solve the linear system matrix * x = rightHandSide, outputting solution if a unique solution exists.
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <param name="rightHandSide">The right hand side.</param>
        /// <param name="solution">The solution.</param>
        /// <returns>The result.</returns>
        public static bool TrySolveLinearSystem(double[,] matrix, double[] rightHandSide, out double[] solution)
        {
            EnsureMatrix(matrix, nameof(matrix));
            EnsureSquare(matrix, nameof(matrix));
            if (rightHandSide == null)
            {
                throw new ArgumentNullException(nameof(rightHandSide));
            }
            int size = matrix.GetLength(0);
            if (rightHandSide.Length != size)
            {
                throw new ArgumentException("The right-hand side length must match the matrix size.", nameof(rightHandSide));
            }
            double[,] augmented = new double[size, size + 1];
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    augmented[row, column] = matrix[row, column];
                }
                augmented[row, size] = rightHandSide[row];
            }
            double[,] reduced = MatrixRowReduce(augmented);
            solution = new double[size];
            int pivotCount = 0;
            for (int row = 0; row < size; row++)
            {
                bool hasCoefficient = false;
                for (int column = 0; column < size; column++)
                {
                    if (Math.Abs(reduced[row, column]) > Tolerance)
                    {
                        hasCoefficient = true;
                        break;
                    }
                }
                if (!hasCoefficient)
                {
                    if (Math.Abs(reduced[row, size]) > Tolerance)
                    {
                        solution = Array.Empty<double>();
                        return false;
                    }
                    continue;
                }
                pivotCount++;
            }
            if (pivotCount != size)
            {
                solution = Array.Empty<double>();
                return false;
            }
            for (int column = 0; column < size; column++)
            {
                int pivotRow = -1;
                for (int row = 0; row < size; row++)
                {
                    if (Math.Abs(reduced[row, column]) > Tolerance)
                    {
                        pivotRow = row;
                        break;
                    }
                }
                if (pivotRow < 0)
                {
                    solution = Array.Empty<double>();
                    return false;
                }
                solution[column] = reduced[pivotRow, size];
            }
            return true;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="matrix">The matrix.</param>
        /// <param name="rightHandSide">The right hand side.</param>
        /// <returns>The result.</returns>
        public static double[] SolveLinearSystem(double[,] matrix, double[] rightHandSide)
        {
            if (!TrySolveLinearSystem(matrix, rightHandSide, out double[] solution))
            {
                throw new InvalidOperationException("The linear system has no unique solution.");
            }
            return solution;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static Complex AddComplex(Complex left, Complex right)
        {
            return left + right;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static Complex SubtractComplex(Complex left, Complex right)
        {
            return left - right;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static Complex MultiplyComplex(Complex left, Complex right)
        {
            return left * right;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static Complex DivideComplex(Complex left, Complex right)
        {
            if (Complex.Abs(right) <= Tolerance)
            {
                throw new DivideByZeroException("Cannot divide by zero in the complex plane.");
            }
            return left / right;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex NegateComplex(Complex value)
        {
            return -value;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex ConjugateComplex(Complex value)
        {
            return Complex.Conjugate(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double ComplexModulus(Complex value)
        {
            return Complex.Abs(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double ComplexArgument(Complex value)
        {
            return value.Phase;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="exponent">The exponent.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexPower(Complex value, Complex exponent)
        {
            return Complex.Pow(value, exponent);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexSquareRoot(Complex value)
        {
            return Complex.Sqrt(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexExponential(Complex value)
        {
            return Complex.Exp(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexLogarithm(Complex value)
        {
            return Complex.Log(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexSine(Complex value)
        {
            return Complex.Sin(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexCosine(Complex value)
        {
            return Complex.Cos(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexTangent(Complex value)
        {
            return Complex.Tan(value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="modulus">The modulus.</param>
        /// <param name="argument">The argument.</param>
        /// <returns>The result.</returns>
        public static Complex ComplexFromPolar(double modulus, double argument)
        {
            return Complex.FromPolarCoordinates(modulus, argument);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The complex value.</param>
        /// <returns>The modulus and argument.</returns>
        public static (double Modulus, double Argument) ComplexToPolar(Complex value)
        {
            return (Complex.Abs(value), value.Phase);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="degree">The degree.</param>
        /// <returns>The result.</returns>
        public static Complex[] ComplexRootsOfUnity(int degree)
        {
            if (degree < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), "The root degree must be positive.");
            }
            Complex[] result = new Complex[degree];
            for (int index = 0; index < degree; index++)
            {
                result[index] = Complex.FromPolarCoordinates(1.0, 2.0 * Math.PI * index / degree);
            }
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static Quaternion AddQuaternions(Quaternion left, Quaternion right)
        {
            return new Quaternion(left.W + right.W, left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static Quaternion SubtractQuaternions(Quaternion left, Quaternion right)
        {
            return new Quaternion(left.W - right.W, left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Quaternion NegateQuaternion(Quaternion value)
        {
            return new Quaternion(-value.W, -value.X, -value.Y, -value.Z);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The result.</returns>
        public static Quaternion ScaleQuaternion(Quaternion value, double scale)
        {
            return new Quaternion(value.W * scale, value.X * scale, value.Y * scale, value.Z * scale);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static Quaternion MultiplyQuaternions(Quaternion left, Quaternion right)
        {
            double w = left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z;
            double x = left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y;
            double y = left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X;
            double z = left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W;
            return new Quaternion(w, x, y, z);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Quaternion ConjugateQuaternion(Quaternion value)
        {
            return new Quaternion(value.W, -value.X, -value.Y, -value.Z);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double QuaternionNormSquared(Quaternion value)
        {
            return value.W * value.W + value.X * value.X + value.Y * value.Y + value.Z * value.Z;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double QuaternionNorm(Quaternion value)
        {
            return Math.Sqrt(QuaternionNormSquared(value));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Quaternion NormalizeQuaternion(Quaternion value)
        {
            double norm = QuaternionNorm(value);
            if (norm <= Tolerance)
            {
                throw new DivideByZeroException("The quaternion has zero norm.");
            }
            return ScaleQuaternion(value, 1.0 / norm);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Quaternion InverseQuaternion(Quaternion value)
        {
            double normSquared = QuaternionNormSquared(value);
            if (normSquared <= Tolerance)
            {
                throw new DivideByZeroException("The quaternion has zero norm.");
            }
            return ScaleQuaternion(ConjugateQuaternion(value), 1.0 / normSquared);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result.</returns>
        public static double DotQuaternions(Quaternion left, Quaternion right)
        {
            return left.W * right.W + left.X * right.X + left.Y * right.Y + left.Z * right.Z;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="axis">The axis.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static Quaternion QuaternionFromAxisAngle(Vector3 axis, double angleRadians)
        {
            double magnitude = Math.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z);
            if (magnitude <= Tolerance)
            {
                throw new ArgumentException("The rotation axis cannot be zero.", nameof(axis));
            }
            double halfAngle = angleRadians * 0.5;
            double sine = Math.Sin(halfAngle);
            return new Quaternion(
                Math.Cos(halfAngle),
                axis.X / magnitude * sine,
                axis.Y / magnitude * sine,
                axis.Z / magnitude * sine);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static Quaternion QuaternionFromAxisAngle(double x, double y, double z, double angleRadians)
        {
            return QuaternionFromAxisAngle(new Vector3(x, y, z), angleRadians);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="axis">The axis.</param>
        /// <param name="angleRadians">The angle radians.</param>
        public static void QuaternionToAxisAngle(Quaternion value, out Vector3 axis, out double angleRadians)
        {
            Quaternion normalized = NormalizeQuaternion(value);
            double clamped = Math.Max(-1.0, Math.Min(1.0, normalized.W));
            angleRadians = 2.0 * Math.Acos(clamped);
            double sine = Math.Sqrt(Math.Max(0.0, 1.0 - clamped * clamped));
            if (sine <= Tolerance)
            {
                axis = new Vector3(1.0, 0.0, 0.0);
                return;
            }
            axis = new Vector3(normalized.X / sine, normalized.Y / sine, normalized.Z / sine);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="rotation">The rotation.</param>
        /// <param name="vector">The vector.</param>
        /// <returns>The result.</returns>
        public static Vector3 RotateVector(Quaternion rotation, Vector3 vector)
        {
            Quaternion normalized = NormalizeQuaternion(rotation);
            Vector3 axis = new Vector3(normalized.X, normalized.Y, normalized.Z);
            Vector3 cross = Cross(axis, vector);
            Vector3 intermediate = Scale(cross, 2.0);
            Vector3 result = Add(
                Add(vector, Scale(intermediate, normalized.W)),
                Cross(axis, intermediate));
            return result;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>The result.</returns>
        public static Quaternion QuaternionLerp(Quaternion start, Quaternion end, double amount)
        {
            if (amount < 0.0 || amount > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "The interpolation amount must be between zero and one.");
            }
            Quaternion first = NormalizeQuaternion(start);
            Quaternion second = NormalizeQuaternion(end);
            return NormalizeQuaternion(AddQuaternions(
                ScaleQuaternion(first, 1.0 - amount),
                ScaleQuaternion(second, amount)));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>The result.</returns>
        public static Quaternion QuaternionSlerp(Quaternion start, Quaternion end, double amount)
        {
            if (amount < 0.0 || amount > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "The interpolation amount must be between zero and one.");
            }
            Quaternion first = NormalizeQuaternion(start);
            Quaternion second = NormalizeQuaternion(end);
            double dot = DotQuaternions(first, second);
            if (dot < 0.0)
            {
                second = NegateQuaternion(second);
                dot = -dot;
            }
            if (dot > 0.9995)
            {
                return QuaternionLerp(first, second, amount);
            }
            double clamped = Math.Max(-1.0, Math.Min(1.0, dot));
            double theta = Math.Acos(clamped);
            double sine = Math.Sin(theta);
            double firstScale = Math.Sin((1.0 - amount) * theta) / sine;
            double secondScale = Math.Sin(amount * theta) / sine;
            return NormalizeQuaternion(AddQuaternions(
                ScaleQuaternion(first, firstScale),
                ScaleQuaternion(second, secondScale)));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="coefficient">The coefficient.</param>
        /// <param name="constant">The constant.</param>
        /// <param name="relation">The relation.</param>
        /// <returns>The result.</returns>
        public static Interval[] SolveLinearInequality(double coefficient, double constant, InequalityRelation relation)
        {
            return SolvePolynomialInequality(new[] { constant, coefficient }, relation);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <param name="relation">The relation.</param>
        /// <returns>The result.</returns>
        public static Interval[] SolveQuadraticInequality(double a, double b, double c, InequalityRelation relation)
        {
            return SolvePolynomialInequality(new[] { c, b, a }, relation);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="coefficients">The coefficients.</param>
        /// <param name="relation">The relation.</param>
        /// <returns>The result.</returns>
        public static Interval[] SolvePolynomialInequality(double[] coefficients, InequalityRelation relation)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            if (IsZeroPolynomial(polynomial))
            {
                return AllowsEquality(relation)
                    ? new[] { new Interval(double.NegativeInfinity, double.PositiveInfinity, false, false) }
                    : Array.Empty<Interval>();
            }
            double[] roots = SolvePolynomialReal(polynomial);
            List<Interval> intervals = new List<Interval>();
            bool equalityAllowed = AllowsEquality(relation);
            double lower = double.NegativeInfinity;
            for (int index = 0; index <= roots.Length; index++)
            {
                double upper = index < roots.Length ? roots[index] : double.PositiveInfinity;
                double sample;
                if (double.IsNegativeInfinity(lower) && double.IsPositiveInfinity(upper))
                {
                    sample = 0.0;
                }
                else if (double.IsNegativeInfinity(lower))
                {
                    sample = upper - 1.0;
                }
                else if (double.IsPositiveInfinity(upper))
                {
                    sample = lower + 1.0;
                }
                else
                {
                    sample = 0.5 * (lower + upper);
                }
                double value = PolynomialEvaluate(polynomial, sample);
                if (MatchesOpenInequality(value, relation))
                {
                    bool lowerClosed = equalityAllowed && !double.IsNegativeInfinity(lower);
                    bool upperClosed = equalityAllowed && !double.IsPositiveInfinity(upper);
                    intervals.Add(new Interval(lower, upper, lowerClosed, upperClosed));
                }
                lower = upper;
            }
            if (equalityAllowed)
            {
                foreach (double root in roots)
                {
                    if (!IsCovered(intervals, root))
                    {
                        intervals.Add(new Interval(root, root, true, true));
                    }
                }
            }
            return MergeIntervals(intervals);
        }

        /// <summary>
        /// Algebraically simplifies a parsed mathematical Expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The result.</returns>
        public static Expression Simplify(Expression expression)
        {
            return SymbolicCalculus.Simplify(expression);
        }

        /// <summary>
        /// Algebraically simplifies a parsed mathematical Expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The result.</returns>
        public static string Simplify(string expression)
        {
            return SymbolicCalculus.Simplify(ExpressionParser.Parse(expression)).ToString();
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The result.</returns>
        public static Expression Expand(Expression expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            ExpressionParser.ValidateExpressionLimits(expression);
            Expression expanded = ExpandInternal(expression, 0);
            ExpressionParser.ValidateExpressionLimits(expanded);
            return expanded;
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The result.</returns>
        public static Expression Expand(string expression)
        {
            return Expand(ExpressionParser.Parse(expression));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>The result.</returns>
        public static string ExpandToString(string expression)
        {
            return Expand(expression).ToString();
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="replacement">The replacement.</param>
        /// <returns>The result.</returns>
        public static Expression Substitute(Expression expression, string variable, Expression replacement)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (variable == null)
            {
                throw new ArgumentNullException(nameof(variable));
            }
            string normalizedVariable = variable.Trim();
            if (normalizedVariable.Length > ExpressionParser.MaximumIdentifierLength)
            {
                throw new FormatException($"Identifier exceeds the {ExpressionParser.MaximumIdentifierLength} character limit.");
            }
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }
            ExpressionParser.ValidateExpressionLimits(expression);
            ExpressionParser.ValidateExpressionLimits(replacement);
            return SymbolicCalculus.Simplify(SubstituteInternal(expression, normalizedVariable, replacement));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Expression Substitute(Expression expression, string variable, double value)
        {
            return Substitute(expression, variable, ExpressionBuilder.Constant(value));
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static Expression Substitute(string expression, string variable, double value)
        {
            return Substitute(ExpressionParser.Parse(expression), variable, value);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static Expression CollectPolynomialTerms(Expression expression, string variable)
        {
            if (!SymbolicCalculus.TryGetPolynomial(expression, variable, out double[] coefficients))
            {
                throw new InvalidOperationException("The expression is not a polynomial in the requested variable.");
            }
            return PolynomialToExpression(coefficients, variable);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static Expression Factor(Expression expression, string variable)
        {
            if (!SymbolicCalculus.TryGetPolynomial(expression, variable, out double[] coefficients))
            {
                throw new InvalidOperationException("The expression is not a polynomial in the requested variable.");
            }
            PolynomialFactor[] factors = FactorPolynomial(coefficients);
            Expression result = ExpressionBuilder.Constant(1.0);
            foreach (PolynomialFactor factor in factors)
            {
                Expression factorExpression = PolynomialToExpression(factor.Factor, variable);
                if (factor.Multiplicity > 1)
                {
                    factorExpression = ExpressionBuilder.Power(
                        factorExpression,
                        ExpressionBuilder.Constant(factor.Multiplicity));
                }
                result = ExpressionBuilder.Multiply(result, factorExpression);
            }
            return SymbolicCalculus.Simplify(result);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static Expression Factor(string expression, string variable)
        {
            return Factor(ExpressionParser.Parse(expression), variable);
        }

        /// <summary>
        /// </summary>
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static string FactorToString(string expression, string variable)
        {
            return Factor(expression, variable).ToString();
        }

        private static Expression ExpandInternal(Expression expression, int depth)
        {
            if (depth > MaxExpansionDepth)
            {
                throw new InvalidOperationException("The expression is too deeply nested to expand.");
            }
            switch (expression)
            {
                case ConstantExpression:
                case VariableExpression:
                    return expression;
                case NegateExpression negate:
                    return ExpressionBuilder.Negate(ExpandInternal(negate.Operand, depth + 1));
                case FunctionExpression function:
                    return ExpressionBuilder.Function(function.Function, ExpandInternal(function.Argument, depth + 1));
                case CallExpression call:
                {
                    Expression[] arguments = new Expression[call.Arguments.Count];
                    for (int index = 0; index < arguments.Length; index++)
                    {
                        arguments[index] = ExpandInternal(call.Arguments[index], depth + 1);
                    }
                    return ExpressionBuilder.Call(call.FunctionName, arguments);
                }
                case ConditionalExpression conditional:
                    return ExpressionBuilder.Conditional(
                        ExpandInternal(conditional.Condition, depth + 1),
                        ExpandInternal(conditional.WhenTrue, depth + 1),
                        ExpandInternal(conditional.WhenFalse, depth + 1));
                case BinaryExpression binary:
                {
                    Expression left = ExpandInternal(binary.Left, depth + 1);
                    Expression right = ExpandInternal(binary.Right, depth + 1);
                    Expression expanded = binary.Operator switch
                    {
                        BinaryOperator.Add => ExpressionBuilder.Add(left, right),
                        BinaryOperator.Subtract => ExpressionBuilder.Subtract(left, right),
                        BinaryOperator.Multiply => Distribute(left, right, depth + 1),
                        BinaryOperator.Divide => ExpressionBuilder.Divide(left, right),
                        BinaryOperator.Power => TryGetNonNegativeInteger(right, out int exponent)
                            ? ExpandPower(left, exponent, depth + 1)
                            : ExpressionBuilder.Power(left, right),
                        _ => ExpressionBuilder.Binary(binary.Operator, left, right)
                    };
                    return SymbolicCalculus.Simplify(expanded);
                }
                default:
                    return expression;
            }
        }

        private static Expression Distribute(Expression left, Expression right, int depth)
        {
            if (depth > MaxExpansionDepth)
            {
                throw new InvalidOperationException("The expression is too deeply nested to expand.");
            }
            if (left is BinaryExpression { Operator: BinaryOperator.Add } leftAdd)
            {
                return ExpressionBuilder.Add(
                    Distribute(leftAdd.Left, right, depth + 1),
                    Distribute(leftAdd.Right, right, depth + 1));
            }
            if (left is BinaryExpression { Operator: BinaryOperator.Subtract } leftSubtract)
            {
                return ExpressionBuilder.Subtract(
                    Distribute(leftSubtract.Left, right, depth + 1),
                    Distribute(leftSubtract.Right, right, depth + 1));
            }
            if (right is BinaryExpression { Operator: BinaryOperator.Add } rightAdd)
            {
                return ExpressionBuilder.Add(
                    Distribute(left, rightAdd.Left, depth + 1),
                    Distribute(left, rightAdd.Right, depth + 1));
            }
            if (right is BinaryExpression { Operator: BinaryOperator.Subtract } rightSubtract)
            {
                return ExpressionBuilder.Subtract(
                    Distribute(left, rightSubtract.Left, depth + 1),
                    Distribute(left, rightSubtract.Right, depth + 1));
            }
            return ExpressionBuilder.Multiply(left, right);
        }

        private static Expression ExpandPower(Expression operand, int exponent, int depth)
        {
            if (exponent > MaxExpansionPower)
            {
                throw new InvalidOperationException("The power is too large to expand.");
            }
            Expression result = ExpressionBuilder.Constant(1.0);
            Expression factor = operand;
            int remaining = exponent;
            while (remaining > 0)
            {
                if ((remaining & 1) == 1)
                {
                    result = Distribute(result, factor, depth + 1);
                }
                remaining >>= 1;
                if (remaining > 0)
                {
                    factor = Distribute(factor, factor, depth + 1);
                }
            }
            return result;
        }

        private static bool TryGetNonNegativeInteger(Expression expression, out int value)
        {
            if (expression is ConstantExpression constant
                && constant.Value >= 0.0
                && constant.Value <= MaxExpansionPower
                && Math.Abs(constant.Value - Math.Round(constant.Value)) <= Tolerance)
            {
                value = (int)Math.Round(constant.Value);
                return true;
            }
            value = 0;
            return false;
        }

        private static Expression SubstituteInternal(Expression expression, string variable, Expression replacement)
        {
            switch (expression)
            {
                case VariableExpression variableExpression:
                    return variableExpression.Name == variable ? replacement : expression;
                case NegateExpression negate:
                    return ExpressionBuilder.Negate(SubstituteInternal(negate.Operand, variable, replacement));
                case BinaryExpression binary:
                    return new BinaryExpression(
                        binary.Operator,
                        SubstituteInternal(binary.Left, variable, replacement),
                        SubstituteInternal(binary.Right, variable, replacement));
                case FunctionExpression function:
                    return ExpressionBuilder.Function(
                        function.Function,
                        SubstituteInternal(function.Argument, variable, replacement));
                case CallExpression call:
                {
                    Expression[] arguments = new Expression[call.Arguments.Count];
                    for (int index = 0; index < arguments.Length; index++)
                    {
                        arguments[index] = SubstituteInternal(call.Arguments[index], variable, replacement);
                    }
                    return ExpressionBuilder.Call(call.FunctionName, arguments);
                }
                case ConditionalExpression conditional:
                    return ExpressionBuilder.Conditional(
                        SubstituteInternal(conditional.Condition, variable, replacement),
                        SubstituteInternal(conditional.WhenTrue, variable, replacement),
                        SubstituteInternal(conditional.WhenFalse, variable, replacement));
                default:
                    return expression;
            }
        }

        private static bool MatchesOpenInequality(double value, InequalityRelation relation)
        {
            return relation switch
            {
                InequalityRelation.LessThan => value < -Tolerance,
                InequalityRelation.LessThanOrEqual => value < -Tolerance,
                InequalityRelation.GreaterThan => value > Tolerance,
                InequalityRelation.GreaterThanOrEqual => value > Tolerance,
                _ => false
            };
        }

        private static bool AllowsEquality(InequalityRelation relation)
        {
            return relation == InequalityRelation.LessThanOrEqual || relation == InequalityRelation.GreaterThanOrEqual;
        }

        private static bool IsCovered(List<Interval> intervals, double value)
        {
            foreach (Interval interval in intervals)
            {
                bool aboveLower = value > interval.Lower + Tolerance
                    || (Math.Abs(value - interval.Lower) <= Tolerance && interval.LowerClosed);
                bool belowUpper = value < interval.Upper - Tolerance
                    || (Math.Abs(value - interval.Upper) <= Tolerance && interval.UpperClosed);
                if (aboveLower && belowUpper)
                {
                    return true;
                }
            }
            return false;
        }

        private static Interval[] MergeIntervals(List<Interval> intervals)
        {
            intervals.Sort((left, right) => left.Lower.CompareTo(right.Lower));
            List<Interval> merged = new List<Interval>();
            foreach (Interval interval in intervals)
            {
                if (merged.Count == 0)
                {
                    merged.Add(interval);
                    continue;
                }
                Interval last = merged[merged.Count - 1];
                bool overlaps = interval.Lower < last.Upper - Tolerance;
                bool touches = Math.Abs(interval.Lower - last.Upper) <= Tolerance
                    && (last.UpperClosed || interval.LowerClosed);
                if (!overlaps && !touches)
                {
                    merged.Add(interval);
                    continue;
                }
                double upper = interval.Upper > last.Upper ? interval.Upper : last.Upper;
                bool upperClosed;
                if (interval.Upper > last.Upper + Tolerance)
                {
                    upperClosed = interval.UpperClosed;
                }
                else if (interval.Upper < last.Upper - Tolerance)
                {
                    upperClosed = last.UpperClosed;
                }
                else
                {
                    upperClosed = last.UpperClosed || interval.UpperClosed;
                }
                merged[merged.Count - 1] = last with { Upper = upper, UpperClosed = upperClosed };
            }
            return merged.ToArray();
        }

        private static Complex[] DurandKerner(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            int degree = polynomial.Length - 1;
            double leading = polynomial[degree];
            double[] monic = new double[polynomial.Length];
            for (int index = 0; index < polynomial.Length; index++)
            {
                monic[index] = polynomial[index] / leading;
            }
            double radius = 1.0;
            for (int index = 0; index < degree; index++)
            {
                radius = Math.Max(radius, 1.0 + Math.Abs(monic[index]));
            }
            Complex[] roots = new Complex[degree];
            for (int index = 0; index < degree; index++)
            {
                double angle = 2.0 * Math.PI * index / degree + 0.4;
                roots[index] = new Complex(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }
            for (int iteration = 0; iteration < 2000; iteration++)
            {
                Complex[] next = new Complex[degree];
                double maximumChange = 0.0;
                for (int index = 0; index < degree; index++)
                {
                    Complex numerator = PolynomialEvaluate(monic, roots[index]);
                    Complex denominator = Complex.One;
                    for (int other = 0; other < degree; other++)
                    {
                        if (other != index)
                        {
                            denominator *= roots[index] - roots[other];
                        }
                    }
                    if (Complex.Abs(denominator) <= 1e-15)
                    {
                        denominator = new Complex(1e-15, 1e-15);
                    }
                    next[index] = roots[index] - numerator / denominator;
                    maximumChange = Math.Max(maximumChange, Complex.Abs(next[index] - roots[index]));
                }
                roots = next;
                if (maximumChange <= 1e-12)
                {
                    break;
                }
            }
            return roots;
        }

        private static bool IsZeroPolynomial(double[] polynomial)
        {
            return polynomial.Length == 1 && Math.Abs(polynomial[0]) <= Tolerance;
        }

        private static double[] MonicPolynomial(double[] coefficients)
        {
            double[] polynomial = NormalizePolynomial(coefficients);
            if (IsZeroPolynomial(polynomial))
            {
                return polynomial;
            }
            double leading = polynomial[polynomial.Length - 1];
            if (Math.Abs(leading - 1.0) <= Tolerance)
            {
                return polynomial;
            }
            double[] result = new double[polynomial.Length];
            for (int index = 0; index < polynomial.Length; index++)
            {
                result[index] = polynomial[index] / leading;
            }
            return NormalizePolynomial(result);
        }

        private static bool HasIntegerCoefficients(double[] polynomial)
        {
            foreach (double coefficient in polynomial)
            {
                if (Math.Abs(coefficient - Math.Round(coefficient)) > 1e-7)
                {
                    return false;
                }
            }
            return true;
        }

        private static List<long> GetDivisors(long value)
        {
            List<long> divisors = new List<long>();
            if (value <= 0L)
            {
                return divisors;
            }
            for (long divisor = 1L; divisor * divisor <= value; divisor++)
            {
                if (value % divisor == 0L)
                {
                    divisors.Add(divisor);
                    if (divisor != value / divisor)
                    {
                        divisors.Add(value / divisor);
                    }
                }
            }
            return divisors;
        }

        private static void AddDistinctRoot(List<double> roots, double candidate, double tolerance = 1e-9)
        {
            foreach (double root in roots)
            {
                if (Math.Abs(root - candidate) <= tolerance * (1.0 + Math.Abs(candidate)))
                {
                    return;
                }
            }
            roots.Add(candidate);
        }

        private static Vector3 Add(Vector3 left, Vector3 right)
        {
            return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        private static Vector3 Scale(Vector3 value, double scale)
        {
            return new Vector3(value.X * scale, value.Y * scale, value.Z * scale);
        }

        private static Vector3 Cross(Vector3 left, Vector3 right)
        {
            return new Vector3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);
        }

        private static void EnsureMatrix(double[,] matrix, string parameterName)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (matrix.GetLength(0) == 0 || matrix.GetLength(1) == 0)
            {
                throw new ArgumentException("The matrix must not be empty.", parameterName);
            }
        }

        private static void EnsureSquare(double[,] matrix, string parameterName)
        {
            EnsureMatrix(matrix, parameterName);
            if (matrix.GetLength(0) != matrix.GetLength(1))
            {
                throw new ArgumentException("The matrix must be square.", parameterName);
            }
        }

        private static void EnsureSameShape(double[,] left, double[,] right)
        {
            EnsureMatrix(left, nameof(left));
            EnsureMatrix(right, nameof(right));
            if (left.GetLength(0) != right.GetLength(0) || left.GetLength(1) != right.GetLength(1))
            {
                throw new ArgumentException("The matrix dimensions must match.");
            }
        }

        private static double[,] CloneMatrix(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] result = new double[rows, columns];
            Array.Copy(matrix, result, matrix.Length);
            return result;
        }

        private static void SwapRows(double[,] matrix, int firstRow, int secondRow)
        {
            int columns = matrix.GetLength(1);
            for (int column = 0; column < columns; column++)
            {
                (matrix[firstRow, column], matrix[secondRow, column]) =
                    (matrix[secondRow, column], matrix[firstRow, column]);
            }
        }
    }
}
