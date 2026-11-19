using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal delegate double ScalarFunction2D(double x, double y);

    internal delegate double ScalarFunction3D(double x, double y, double z);

    internal delegate Vector2 VectorField2D(double x, double y);

    internal delegate Vector3 VectorField3D(double x, double y, double z);

    internal delegate Vector3 ParametricCurve(double t);

    internal delegate Vector3 ParametricSurface(double u, double v);

    internal sealed record OdeSolution(double[] X, double[] Y);

    internal sealed record OdeSystemSolution(double[] X, double[][] Y);

    internal static class Math_4
    {
        public static Expression PartialDerivative(Expression expression, string variable)
        {
            return SymbolicCalculus.Differentiate(expression, variable);
        }

        public static Expression PartialDerivative(string expression, string variable)
        {
            return SymbolicCalculus.Differentiate(Math_3.Parse(expression), variable);
        }

        public static Expression[] Gradient(Expression expression, params string[] variables)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }
            if (variables == null || variables.Length == 0)
            {
                throw new ArgumentException("At least one variable is required.", nameof(variables));
            }
            Expression[] result = new Expression[variables.Length];
            for (int index = 0; index < variables.Length; index++)
            {
                result[index] = SymbolicCalculus.Differentiate(expression, variables[index]);
            }
            return result;
        }

        public static Expression[] Gradient(string expression, params string[] variables)
        {
            return Gradient(Math_3.Parse(expression), variables);
        }

        public static Expression Laplacian(Expression expression, params string[] variables)
        {
            Expression[] gradient = Gradient(expression, variables);
            Expression second = ExpressionBuilder.Constant(0.0);
            for (int index = 0; index < variables.Length; index++)
            {
                second = ExpressionBuilder.Add(
                    second,
                    SymbolicCalculus.Differentiate(gradient[index], variables[index]));
            }
            return SymbolicCalculus.Simplify(second);
        }

        public static Expression Laplacian(string expression, params string[] variables)
        {
            return Laplacian(Math_3.Parse(expression), variables);
        }

        public static Expression[,] Hessian(Expression expression, string firstVariable, string secondVariable)
        {
            Expression[,] result = new Expression[2, 2];
            Expression firstDerivative = SymbolicCalculus.Differentiate(expression, firstVariable);
            Expression secondDerivative = SymbolicCalculus.Differentiate(expression, secondVariable);
            result[0, 0] = SymbolicCalculus.Differentiate(firstDerivative, firstVariable);
            result[0, 1] = SymbolicCalculus.Differentiate(firstDerivative, secondVariable);
            result[1, 0] = SymbolicCalculus.Differentiate(secondDerivative, firstVariable);
            result[1, 1] = SymbolicCalculus.Differentiate(secondDerivative, secondVariable);
            return result;
        }

        public static double Evaluate(Expression expression, IReadOnlyDictionary<string, double> variables)
        {
            return SymbolicCalculus.Evaluate(expression, variables);
        }

        public static double Evaluate(string expression, IReadOnlyDictionary<string, double> variables)
        {
            return SymbolicCalculus.Evaluate(Math_3.Parse(expression), variables);
        }

        public static Vector2 GradientAtPoint(Expression expression, string xVariable, string yVariable, double x, double y)
        {
            IReadOnlyDictionary<string, double> values = new Dictionary<string, double>
            {
                [xVariable] = x,
                [yVariable] = y
            };
            return new Vector2(
                SymbolicCalculus.Evaluate(SymbolicCalculus.Differentiate(expression, xVariable), values),
                SymbolicCalculus.Evaluate(SymbolicCalculus.Differentiate(expression, yVariable), values));
        }

        public static Vector3 GradientAtPoint(Expression expression, string xVariable, string yVariable, string zVariable, double x, double y, double z)
        {
            IReadOnlyDictionary<string, double> values = new Dictionary<string, double>
            {
                [xVariable] = x,
                [yVariable] = y,
                [zVariable] = z
            };
            return new Vector3(
                SymbolicCalculus.Evaluate(SymbolicCalculus.Differentiate(expression, xVariable), values),
                SymbolicCalculus.Evaluate(SymbolicCalculus.Differentiate(expression, yVariable), values),
                SymbolicCalculus.Evaluate(SymbolicCalculus.Differentiate(expression, zVariable), values));
        }

        public static double PartialDerivative(ScalarFunction2D function, double x, double y, int variableIndex, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (variableIndex != 0 && variableIndex != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(variableIndex), "The variable index must be 0 or 1.");
            }
            double coordinate = variableIndex == 0 ? x : y;
            double scale = ResolveStep(step, coordinate);
            double Coarse(double offset)
            {
                return variableIndex == 0
                    ? (function(x + offset, y) - function(x - offset, y)) / (2.0 * offset)
                    : (function(x, y + offset) - function(x, y - offset)) / (2.0 * offset);
            }
            return (4.0 * Coarse(scale * 0.5) - Coarse(scale)) / 3.0;
        }

        public static double PartialDerivative(ScalarFunction3D function, double x, double y, double z, int variableIndex, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (variableIndex < 0 || variableIndex > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(variableIndex), "The variable index must be between 0 and 2.");
            }
            double coordinate = variableIndex switch
            {
                0 => x,
                1 => y,
                _ => z
            };
            double scale = ResolveStep(step, coordinate);
            double Coarse(double offset)
            {
                return variableIndex switch
                {
                    0 => (function(x + offset, y, z) - function(x - offset, y, z)) / (2.0 * offset),
                    1 => (function(x, y + offset, z) - function(x, y - offset, z)) / (2.0 * offset),
                    _ => (function(x, y, z + offset) - function(x, y, z - offset)) / (2.0 * offset)
                };
            }
            return (4.0 * Coarse(scale * 0.5) - Coarse(scale)) / 3.0;
        }

        public static Vector2 Gradient(ScalarFunction2D function, double x, double y, double step = 0.0)
        {
            return new Vector2(
                PartialDerivative(function, x, y, 0, step),
                PartialDerivative(function, x, y, 1, step));
        }

        public static Vector3 Gradient(ScalarFunction3D function, double x, double y, double z, double step = 0.0)
        {
            return new Vector3(
                PartialDerivative(function, x, y, z, 0, step),
                PartialDerivative(function, x, y, z, 1, step),
                PartialDerivative(function, x, y, z, 2, step));
        }

        public static double SecondPartialDerivative(ScalarFunction2D function, double x, double y, int variableIndex, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (variableIndex != 0 && variableIndex != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(variableIndex), "The variable index must be 0 or 1.");
            }
            double coordinate = variableIndex == 0 ? x : y;
            double scale = ResolveStep(step, coordinate);
            return variableIndex == 0
                ? (function(x + scale, y) - 2.0 * function(x, y) + function(x - scale, y)) / (scale * scale)
                : (function(x, y + scale) - 2.0 * function(x, y) + function(x, y - scale)) / (scale * scale);
        }

        public static double SecondPartialDerivative(ScalarFunction3D function, double x, double y, double z, int variableIndex, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (variableIndex < 0 || variableIndex > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(variableIndex), "The variable index must be between 0 and 2.");
            }
            double coordinate = variableIndex switch
            {
                0 => x,
                1 => y,
                _ => z
            };
            double scale = ResolveStep(step, coordinate);
            return variableIndex switch
            {
                0 => (function(x + scale, y, z) - 2.0 * function(x, y, z) + function(x - scale, y, z)) / (scale * scale),
                1 => (function(x, y + scale, z) - 2.0 * function(x, y, z) + function(x, y - scale, z)) / (scale * scale),
                _ => (function(x, y, z + scale) - 2.0 * function(x, y, z) + function(x, y, z - scale)) / (scale * scale)
            };
        }

        public static double MixedPartialDerivative(ScalarFunction2D function, double x, double y, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            double xStep = ResolveStep(step, x);
            double yStep = ResolveStep(step, y);
            return (function(x + xStep, y + yStep)
                - function(x + xStep, y - yStep)
                - function(x - xStep, y + yStep)
                + function(x - xStep, y - yStep)) / (4.0 * xStep * yStep);
        }

        public static double[,] Hessian(ScalarFunction2D function, double x, double y, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            double[,] result = new double[2, 2];
            result[0, 0] = SecondPartialDerivative(function, x, y, 0, step);
            result[1, 1] = SecondPartialDerivative(function, x, y, 1, step);
            result[0, 1] = MixedPartialDerivative(function, x, y, step);
            result[1, 0] = result[0, 1];
            return result;
        }

        public static double[,] Hessian(ScalarFunction3D function, double x, double y, double z, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            double[,] result = new double[3, 3];
            for (int index = 0; index < 3; index++)
            {
                result[index, index] = SecondPartialDerivative(function, x, y, z, index, step);
            }
            for (int row = 0; row < 3; row++)
            {
                for (int column = row + 1; column < 3; column++)
                {
                    double value = MixedPartialDerivative(function, x, y, z, row, column, step);
                    result[row, column] = value;
                    result[column, row] = value;
                }
            }
            return result;
        }

        public static double DirectionalDerivative(ScalarFunction2D function, double x, double y, double xDirection, double yDirection, double step = 0.0)
        {
            double length = Math.Sqrt(xDirection * xDirection + yDirection * yDirection);
            if (length == 0.0)
            {
                throw new ArgumentException("The direction vector cannot be zero.");
            }
            Vector2 gradient = Gradient(function, x, y, step);
            return (gradient.X * xDirection + gradient.Y * yDirection) / length;
        }

        public static double DirectionalDerivative(ScalarFunction3D function, double x, double y, double z, double xDirection, double yDirection, double zDirection, double step = 0.0)
        {
            double length = Math.Sqrt(xDirection * xDirection + yDirection * yDirection + zDirection * zDirection);
            if (length == 0.0)
            {
                throw new ArgumentException("The direction vector cannot be zero.");
            }
            Vector3 gradient = Gradient(function, x, y, z, step);
            return (gradient.X * xDirection + gradient.Y * yDirection + gradient.Z * zDirection) / length;
        }

        public static double Laplacian(ScalarFunction2D function, double x, double y, double step = 0.0)
        {
            return SecondPartialDerivative(function, x, y, 0, step)
                + SecondPartialDerivative(function, x, y, 1, step);
        }

        public static double Laplacian(ScalarFunction3D function, double x, double y, double z, double step = 0.0)
        {
            return SecondPartialDerivative(function, x, y, z, 0, step)
                + SecondPartialDerivative(function, x, y, z, 1, step)
                + SecondPartialDerivative(function, x, y, z, 2, step);
        }

        public static double Divergence(VectorField2D field, double x, double y, double step = 0.0)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            return PartialDerivative((a, b) => field(a, b).X, x, y, 0, step)
                + PartialDerivative((a, b) => field(a, b).Y, x, y, 1, step);
        }

        public static double Divergence(VectorField3D field, double x, double y, double z, double step = 0.0)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            return PartialDerivative((a, b, c) => field(a, b, c).X, x, y, z, 0, step)
                + PartialDerivative((a, b, c) => field(a, b, c).Y, x, y, z, 1, step)
                + PartialDerivative((a, b, c) => field(a, b, c).Z, x, y, z, 2, step);
        }

        public static double Curl(VectorField2D field, double x, double y, double step = 0.0)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            return PartialDerivative((a, b) => field(a, b).Y, x, y, 0, step)
                - PartialDerivative((a, b) => field(a, b).X, x, y, 1, step);
        }

        public static Vector3 Curl(VectorField3D field, double x, double y, double z, double step = 0.0)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            return new Vector3(
                PartialDerivative((a, b, c) => field(a, b, c).Z, x, y, z, 1, step)
                    - PartialDerivative((a, b, c) => field(a, b, c).Y, x, y, z, 2, step),
                PartialDerivative((a, b, c) => field(a, b, c).X, x, y, z, 2, step)
                    - PartialDerivative((a, b, c) => field(a, b, c).Z, x, y, z, 0, step),
                PartialDerivative((a, b, c) => field(a, b, c).Y, x, y, z, 0, step)
                    - PartialDerivative((a, b, c) => field(a, b, c).X, x, y, z, 1, step));
        }

        public static double Jacobian(ScalarFunction2D firstComponent, ScalarFunction2D secondComponent, double x, double y, double step = 0.0)
        {
            if (firstComponent == null)
            {
                throw new ArgumentNullException(nameof(firstComponent));
            }
            if (secondComponent == null)
            {
                throw new ArgumentNullException(nameof(secondComponent));
            }
            return PartialDerivative(firstComponent, x, y, 0, step) * PartialDerivative(secondComponent, x, y, 1, step)
                - PartialDerivative(firstComponent, x, y, 1, step) * PartialDerivative(secondComponent, x, y, 0, step);
        }

        public static double DoubleIntegral(ScalarFunction2D function, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            return Simpson(
                x => Simpson(y => function(x, y), yLower, yUpper, intervals),
                xLower,
                xUpper,
                intervals);
        }

        public static double DoubleIntegral(ScalarFunction2D function, double xLower, double xUpper, Func<double, double> yLower, Func<double, double> yUpper, int intervals = 100)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (yLower == null)
            {
                throw new ArgumentNullException(nameof(yLower));
            }
            if (yUpper == null)
            {
                throw new ArgumentNullException(nameof(yUpper));
            }
            return Simpson(
                x => Simpson(y => function(x, y), yLower(x), yUpper(x), intervals),
                xLower,
                xUpper,
                intervals);
        }

        public static double DoubleIntegralInPolarCoordinates(ScalarFunction2D function, double radiusLower, double radiusUpper, double angleLower, double angleUpper, int intervals = 100)
        {
            return DoubleIntegral(
                (radius, angle) => function(radius * Math.Cos(angle), radius * Math.Sin(angle)) * radius,
                radiusLower,
                radiusUpper,
                angleLower,
                angleUpper,
                intervals);
        }

        public static double TripleIntegral(ScalarFunction3D function, double xLower, double xUpper, double yLower, double yUpper, double zLower, double zUpper, int intervals = 60)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            return Simpson(
                x => Simpson(
                    y => Simpson(z => function(x, y, z), zLower, zUpper, intervals),
                    yLower,
                    yUpper,
                    intervals),
                xLower,
                xUpper,
                intervals);
        }

        public static double TripleIntegralInCylindricalCoordinates(ScalarFunction3D function, double radiusLower, double radiusUpper, double angleLower, double angleUpper, double zLower, double zUpper, int intervals = 60)
        {
            return TripleIntegral(
                (radius, angle, z) => function(radius * Math.Cos(angle), radius * Math.Sin(angle), z) * radius,
                radiusLower,
                radiusUpper,
                angleLower,
                angleUpper,
                zLower,
                zUpper,
                intervals);
        }

        public static double TripleIntegralInSphericalCoordinates(ScalarFunction3D function, double rhoLower, double rhoUpper, double thetaLower, double thetaUpper, double phiLower, double phiUpper, int intervals = 60)
        {
            return TripleIntegral(
                (rho, theta, phi) =>
                {
                    double sine = Math.Sin(phi);
                    return function(
                        rho * sine * Math.Cos(theta),
                        rho * sine * Math.Sin(theta),
                        rho * Math.Cos(phi)) * rho * rho * sine;
                },
                rhoLower,
                rhoUpper,
                thetaLower,
                thetaUpper,
                phiLower,
                phiUpper,
                intervals);
        }

        public static double ScalarLineIntegral(ScalarFunction2D function, Func<double, double> xOfT, Func<double, double> yOfT, double lower, double upper)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (xOfT == null)
            {
                throw new ArgumentNullException(nameof(xOfT));
            }
            if (yOfT == null)
            {
                throw new ArgumentNullException(nameof(yOfT));
            }
            Func<double, double> integrand = t =>
            {
                double dx = Math_3.NumericDerivative(xOfT, t);
                double dy = Math_3.NumericDerivative(yOfT, t);
                return function(xOfT(t), yOfT(t)) * Math.Sqrt(dx * dx + dy * dy);
            };
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        public static double ScalarLineIntegral(ScalarFunction3D function, ParametricCurve curve, double lower, double upper)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            Func<double, double> integrand = t =>
            {
                Vector3 point = curve(t);
                Vector3 derivative = CurveDerivative(curve, t);
                return function(point.X, point.Y, point.Z) * Magnitude(derivative);
            };
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        public static double VectorLineIntegral(VectorField2D field, Func<double, double> xOfT, Func<double, double> yOfT, double lower, double upper)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            if (xOfT == null)
            {
                throw new ArgumentNullException(nameof(xOfT));
            }
            if (yOfT == null)
            {
                throw new ArgumentNullException(nameof(yOfT));
            }
            Func<double, double> integrand = t =>
            {
                Vector2 value = field(xOfT(t), yOfT(t));
                double dx = Math_3.NumericDerivative(xOfT, t);
                double dy = Math_3.NumericDerivative(yOfT, t);
                return value.X * dx + value.Y * dy;
            };
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        public static double VectorLineIntegral(VectorField3D field, ParametricCurve curve, double lower, double upper)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            Func<double, double> integrand = t =>
            {
                Vector3 point = curve(t);
                Vector3 value = field(point.X, point.Y, point.Z);
                Vector3 derivative = CurveDerivative(curve, t);
                return value.X * derivative.X + value.Y * derivative.Y + value.Z * derivative.Z;
            };
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        public static double FluxLineIntegral(VectorField2D field, Func<double, double> xOfT, Func<double, double> yOfT, double lower, double upper)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            if (xOfT == null)
            {
                throw new ArgumentNullException(nameof(xOfT));
            }
            if (yOfT == null)
            {
                throw new ArgumentNullException(nameof(yOfT));
            }
            Func<double, double> integrand = t =>
            {
                Vector2 value = field(xOfT(t), yOfT(t));
                double dx = Math_3.NumericDerivative(xOfT, t);
                double dy = Math_3.NumericDerivative(yOfT, t);
                return value.X * dy - value.Y * dx;
            };
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        public static double SurfaceArea(ParametricSurface surface, double uLower, double uUpper, double vLower, double vUpper, int intervals = 40)
        {
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }
            return Simpson(
                u => Simpson(v => Magnitude(SurfaceCross(surface, u, v)), vLower, vUpper, intervals),
                uLower,
                uUpper,
                intervals);
        }

        public static double SurfaceAreaOfGraph(ScalarFunction2D function, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            return Simpson(
                x => Simpson(
                    y =>
                    {
                        double dx = PartialDerivative(function, x, y, 0);
                        double dy = PartialDerivative(function, x, y, 1);
                        return Math.Sqrt(1.0 + dx * dx + dy * dy);
                    },
                    yLower,
                    yUpper,
                    intervals),
                xLower,
                xUpper,
                intervals);
        }

        public static double ScalarSurfaceIntegral(ScalarFunction3D function, ParametricSurface surface, double uLower, double uUpper, double vLower, double vUpper, int intervals = 40)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }
            return Simpson(
                u => Simpson(
                    v =>
                    {
                        Vector3 point = surface(u, v);
                        return function(point.X, point.Y, point.Z) * Magnitude(SurfaceCross(surface, u, v));
                    },
                    vLower,
                    vUpper,
                    intervals),
                uLower,
                uUpper,
                intervals);
        }

        public static double FluxIntegral(VectorField3D field, ParametricSurface surface, double uLower, double uUpper, double vLower, double vUpper, int intervals = 40)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }
            return Simpson(
                u => Simpson(
                    v =>
                    {
                        Vector3 point = surface(u, v);
                        Vector3 value = field(point.X, point.Y, point.Z);
                        Vector3 cross = SurfaceCross(surface, u, v);
                        return value.X * cross.X + value.Y * cross.Y + value.Z * cross.Z;
                    },
                    vLower,
                    vUpper,
                    intervals),
                uLower,
                uUpper,
                intervals);
        }

        public static double ArcLength(ParametricCurve curve, double lower, double upper)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            Func<double, double> integrand = t => Magnitude(CurveDerivative(curve, t));
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        public static double ArcLengthParametric2D(Func<double, double> xOfT, Func<double, double> yOfT, double lower, double upper)
        {
            return Math_3.ArcLengthParametric(xOfT, yOfT, lower, upper);
        }

        public static double AreaOfPolarRegion(Func<double, double> radius, double lower, double upper)
        {
            if (radius == null)
            {
                throw new ArgumentNullException(nameof(radius));
            }
            Func<double, double> integrand = theta =>
            {
                double value = radius(theta);
                return 0.5 * value * value;
            };
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        public static Vector2 CenterOfMass(ScalarFunction2D density, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            if (density == null)
            {
                throw new ArgumentNullException(nameof(density));
            }
            double mass = DoubleIntegral(density, xLower, xUpper, yLower, yUpper, intervals);
            if (mass == 0.0)
            {
                throw new DivideByZeroException("The total mass is zero.");
            }
            double momentX = DoubleIntegral((x, y) => x * density(x, y), xLower, xUpper, yLower, yUpper, intervals);
            double momentY = DoubleIntegral((x, y) => y * density(x, y), xLower, xUpper, yLower, yUpper, intervals);
            return new Vector2(momentX / mass, momentY / mass);
        }

        public static double MomentOfInertiaAboutX(ScalarFunction2D density, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            if (density == null)
            {
                throw new ArgumentNullException(nameof(density));
            }
            return DoubleIntegral((x, y) => y * y * density(x, y), xLower, xUpper, yLower, yUpper, intervals);
        }

        public static double MomentOfInertiaAboutY(ScalarFunction2D density, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            if (density == null)
            {
                throw new ArgumentNullException(nameof(density));
            }
            return DoubleIntegral((x, y) => x * x * density(x, y), xLower, xUpper, yLower, yUpper, intervals);
        }

        public static double AverageValue(ScalarFunction2D function, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            double area = (xUpper - xLower) * (yUpper - yLower);
            if (area == 0.0)
            {
                throw new ArgumentException("The region has zero area.");
            }
            return DoubleIntegral(function, xLower, xUpper, yLower, yUpper, intervals) / area;
        }

        public static double WorkDoneByField(VectorField3D field, ParametricCurve curve, double lower, double upper)
        {
            return VectorLineIntegral(field, curve, lower, upper);
        }

        public static Vector2 GradientDescent(ScalarFunction2D function, double x, double y, double learningRate, int maxIterations, double tolerance = 1e-9, double step = 0.0)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (learningRate <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(learningRate), "The learning rate must be positive.");
            }
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations), "The iteration limit must be positive.");
            }
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                Vector2 gradient = Gradient(function, x, y, step);
                double nextX = x - learningRate * gradient.X;
                double nextY = y - learningRate * gradient.Y;
                if (!double.IsFinite(nextX) || !double.IsFinite(nextY))
                {
                    break;
                }
                double change = Math.Sqrt((nextX - x) * (nextX - x) + (nextY - y) * (nextY - y));
                x = nextX;
                y = nextY;
                if (change <= tolerance * (1.0 + Math.Sqrt(x * x + y * y)))
                {
                    return new Vector2(x, y);
                }
            }
            return new Vector2(x, y);
        }

        public static OdeSolution Euler(ScalarFunction2D derivative, double xStart, double yStart, double xEnd, int steps)
        {
            ValidateOdeArguments(derivative, steps, xStart, xEnd);
            double step = (xEnd - xStart) / steps;
            double[] xValues = new double[steps + 1];
            double[] yValues = new double[steps + 1];
            double x = xStart;
            double y = yStart;
            xValues[0] = x;
            yValues[0] = y;
            for (int index = 1; index <= steps; index++)
            {
                y += step * derivative(x, y);
                x = xStart + index * step;
                xValues[index] = x;
                yValues[index] = y;
            }
            return new OdeSolution(xValues, yValues);
        }

        public static OdeSolution ImprovedEuler(ScalarFunction2D derivative, double xStart, double yStart, double xEnd, int steps)
        {
            ValidateOdeArguments(derivative, steps, xStart, xEnd);
            double step = (xEnd - xStart) / steps;
            double[] xValues = new double[steps + 1];
            double[] yValues = new double[steps + 1];
            double x = xStart;
            double y = yStart;
            xValues[0] = x;
            yValues[0] = y;
            for (int index = 1; index <= steps; index++)
            {
                double slopeStart = derivative(x, y);
                double predictor = y + step * slopeStart;
                double slopeEnd = derivative(x + step, predictor);
                y += 0.5 * step * (slopeStart + slopeEnd);
                x = xStart + index * step;
                xValues[index] = x;
                yValues[index] = y;
            }
            return new OdeSolution(xValues, yValues);
        }

        public static OdeSolution RungeKutta4(ScalarFunction2D derivative, double xStart, double yStart, double xEnd, int steps)
        {
            ValidateOdeArguments(derivative, steps, xStart, xEnd);
            double step = (xEnd - xStart) / steps;
            double[] xValues = new double[steps + 1];
            double[] yValues = new double[steps + 1];
            double x = xStart;
            double y = yStart;
            xValues[0] = x;
            yValues[0] = y;
            for (int index = 1; index <= steps; index++)
            {
                double k1 = derivative(x, y);
                double k2 = derivative(x + 0.5 * step, y + 0.5 * step * k1);
                double k3 = derivative(x + 0.5 * step, y + 0.5 * step * k2);
                double k4 = derivative(x + step, y + step * k3);
                y += step / 6.0 * (k1 + 2.0 * k2 + 2.0 * k3 + k4);
                x = xStart + index * step;
                xValues[index] = x;
                yValues[index] = y;
            }
            return new OdeSolution(xValues, yValues);
        }

        public static OdeSystemSolution RungeKutta4System(Func<double, double[], double[]> derivative, double start, double[] initial, double end, int steps)
        {
            if (derivative == null)
            {
                throw new ArgumentNullException(nameof(derivative));
            }
            if (initial == null)
            {
                throw new ArgumentNullException(nameof(initial));
            }
            if (steps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), "The step count must be positive.");
            }
            int dimension = initial.Length;
            double step = (end - start) / steps;
            double[] times = new double[steps + 1];
            double[][] values = new double[steps + 1][];
            double[] current = new double[dimension];
            Array.Copy(initial, current, dimension);
            times[0] = start;
            values[0] = Copy(current);
            for (int index = 1; index <= steps; index++)
            {
                double time = start + (index - 1) * step;
                double[] k1 = derivative(time, current);
                double[] k2 = derivative(time + 0.5 * step, AddScaled(current, k1, 0.5 * step));
                double[] k3 = derivative(time + 0.5 * step, AddScaled(current, k2, 0.5 * step));
                double[] k4 = derivative(time + step, AddScaled(current, k3, step));
                for (int component = 0; component < dimension; component++)
                {
                    current[component] += step / 6.0 * (k1[component] + 2.0 * k2[component] + 2.0 * k3[component] + k4[component]);
                }
                times[index] = start + index * step;
                values[index] = Copy(current);
            }
            return new OdeSystemSolution(times, values);
        }

        private static double MixedPartialDerivative(ScalarFunction3D function, double x, double y, double z, int firstIndex, int secondIndex, double step)
        {
            double[] coordinates = { x, y, z };
            double firstStep = ResolveStep(step, coordinates[firstIndex]);
            double secondStep = ResolveStep(step, coordinates[secondIndex]);
            double Evaluate(double firstOffset, double secondOffset)
            {
                double[] point = { x, y, z };
                point[firstIndex] += firstOffset;
                point[secondIndex] += secondOffset;
                return function(point[0], point[1], point[2]);
            }
            return (Evaluate(firstStep, secondStep)
                - Evaluate(firstStep, -secondStep)
                - Evaluate(-firstStep, secondStep)
                + Evaluate(-firstStep, -secondStep)) / (4.0 * firstStep * secondStep);
        }

        private static Vector3 CurveDerivative(ParametricCurve curve, double t)
        {
            double step = ResolveStep(0.0, t);
            Vector3 forward = curve(t + step);
            Vector3 backward = curve(t - step);
            return new Vector3(
                (forward.X - backward.X) / (2.0 * step),
                (forward.Y - backward.Y) / (2.0 * step),
                (forward.Z - backward.Z) / (2.0 * step));
        }

        private static Vector3 SurfaceCross(ParametricSurface surface, double u, double v)
        {
            double uStep = ResolveStep(0.0, u);
            double vStep = ResolveStep(0.0, v);
            Vector3 uForward = surface(u + uStep, v);
            Vector3 uBackward = surface(u - uStep, v);
            Vector3 vForward = surface(u, v + vStep);
            Vector3 vBackward = surface(u, v - vStep);
            Vector3 uDerivative = new Vector3(
                (uForward.X - uBackward.X) / (2.0 * uStep),
                (uForward.Y - uBackward.Y) / (2.0 * uStep),
                (uForward.Z - uBackward.Z) / (2.0 * uStep));
            Vector3 vDerivative = new Vector3(
                (vForward.X - vBackward.X) / (2.0 * vStep),
                (vForward.Y - vBackward.Y) / (2.0 * vStep),
                (vForward.Z - vBackward.Z) / (2.0 * vStep));
            return Math_2.CalculateCrossProduct(uDerivative, vDerivative);
        }

        private static double Magnitude(Vector3 value)
        {
            return Math_2.CalculateVectorMagnitude(value);
        }

        private static double Simpson(Func<double, double> function, double lower, double upper, int intervals)
        {
            return Math_3.NumericIntegral(function, lower, upper, intervals);
        }

        private static double ResolveStep(double step, double coordinate)
        {
            if (step > 0.0)
            {
                return step;
            }
            return Math.Max(1e-5, Math.Abs(coordinate) * 1e-5);
        }

        private static void ValidateOdeArguments(ScalarFunction2D derivative, int steps, double start, double end)
        {
            if (derivative == null)
            {
                throw new ArgumentNullException(nameof(derivative));
            }
            if (steps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), "The step count must be positive.");
            }
            if (!double.IsFinite(start) || !double.IsFinite(end))
            {
                throw new ArgumentException("The interval endpoints must be finite.");
            }
        }

        private static double[] AddScaled(double[] values, double[] increment, double scale)
        {
            double[] result = new double[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = values[index] + scale * increment[index];
            }
            return result;
        }

        private static double[] Copy(double[] values)
        {
            double[] result = new double[values.Length];
            Array.Copy(values, result, values.Length);
            return result;
        }
    }
}
