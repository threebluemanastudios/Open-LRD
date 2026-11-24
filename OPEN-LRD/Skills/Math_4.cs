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
        /// <summary>
        /// Computes the symbolic first partial derivative of an expression with respect to variable.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static Expression PartialDerivative(Expression expression, string variable)
        {
            return SymbolicCalculus.Differentiate(expression, variable);
        }

        /// <summary>
        /// Parses expression and computes its symbolic first partial derivative with respect to variable.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variable">The variable.</param>
        /// <returns>The result.</returns>
        public static Expression PartialDerivative(string expression, string variable)
        {
            return SymbolicCalculus.Differentiate(Math_3.Parse(expression), variable);
        }

        /// <summary>
        /// Computes the symbolic gradient of an expression: the array of first partial derivatives with respect to each variable.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variables">The variables.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Parses expression and computes its symbolic gradient with respect to the given variables.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variables">The variables.</param>
        /// <returns>The result.</returns>
        public static Expression[] Gradient(string expression, params string[] variables)
        {
            return Gradient(Math_3.Parse(expression), variables);
        }

        /// <summary>
        /// Computes the symbolic Laplacian (sum of unmixed second partial derivatives) of an expression with respect to the given variables.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variables">The variables.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the symbolic Laplacian (sum of unmixed second partial derivatives) of an expression with respect to the given variables.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variables">The variables.</param>
        /// <returns>The result.</returns>
        public static Expression Laplacian(string expression, params string[] variables)
        {
            return Laplacian(Math_3.Parse(expression), variables);
        }

        /// <summary>
        /// Computes the symbolic 2x2 Hessian matrix of second partial derivatives of an expression with respect to two variables.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="firstVariable">The first variable.</param>
        /// <param name="secondVariable">The second variable.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Evaluates an Expression numerically using the provided variable-to-value mapping.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variables">The variables.</param>
        /// <returns>The result.</returns>
        public static double Evaluate(Expression expression, IReadOnlyDictionary<string, double> variables)
        {
            return SymbolicCalculus.Evaluate(expression, variables);
        }

        /// <summary>
        /// Evaluates an Expression numerically using the provided variable-to-value mapping.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="variables">The variables.</param>
        /// <returns>The result.</returns>
        public static double Evaluate(string expression, IReadOnlyDictionary<string, double> variables)
        {
            return SymbolicCalculus.Evaluate(Math_3.Parse(expression), variables);
        }

        /// <summary>
        /// Computes the gradient of a 2D expression at the point (x, y) by symbolic differentiation and evaluation.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="xVariable">The x variable.</param>
        /// <param name="yVariable">The y variable.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the gradient of a 3D expression at the point (x, y, z) by symbolic differentiation and evaluation.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="xVariable">The x variable.</param>
        /// <param name="yVariable">The y variable.</param>
        /// <param name="zVariable">The z variable.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the first partial derivative of a 2D scalar function at (x, y) with respect to variableIndex (0 for x, 1 for y).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="variableIndex">The variable index.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the first partial derivative of a 3D scalar function at (x, y, z).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="variableIndex">The variable index.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the gradient of a 2D scalar function at (x, y).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
        public static Vector2 Gradient(ScalarFunction2D function, double x, double y, double step = 0.0)
        {
            return new Vector2(
                PartialDerivative(function, x, y, 0, step),
                PartialDerivative(function, x, y, 1, step));
        }

        /// <summary>
        /// Numerically approximates the gradient of a 3D scalar function at (x, y, z).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
        public static Vector3 Gradient(ScalarFunction3D function, double x, double y, double z, double step = 0.0)
        {
            return new Vector3(
                PartialDerivative(function, x, y, z, 0, step),
                PartialDerivative(function, x, y, z, 1, step),
                PartialDerivative(function, x, y, z, 2, step));
        }

        /// <summary>
        /// Numerically approximates an unmixed second partial derivative of a 2D scalar function at (x, y) with respect to variableIndex (0 for x, 1 for y).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="variableIndex">The variable index.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates an unmixed second partial derivative of a 3D scalar function at (x, y, z).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="variableIndex">The variable index.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the mixed second partial derivative (partial^2 f / (partial x partial y)) of a 2D scalar function at (x, y).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the 2x2 Hessian matrix of a 2D scalar function at (x, y) using second partial derivatives and the mixed partial.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the 3x3 Hessian matrix of a 3D scalar function at (x, y, z) using second partial derivatives and mixed partials.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the directional derivative of a 2D scalar function at (x, y) along the direction (xDirection, yDirection).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="xDirection">The x direction.</param>
        /// <param name="yDirection">The y direction.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the directional derivative of a 3D scalar function at (x, y, z).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="xDirection">The x direction.</param>
        /// <param name="yDirection">The y direction.</param>
        /// <param name="zDirection">The z direction.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the Laplacian (sum of unmixed second partial derivatives) of a 2D scalar function at (x, y).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
        public static double Laplacian(ScalarFunction2D function, double x, double y, double step = 0.0)
        {
            return SecondPartialDerivative(function, x, y, 0, step)
                + SecondPartialDerivative(function, x, y, 1, step);
        }

        /// <summary>
        /// Numerically approximates the Laplacian (sum of unmixed second partial derivatives) of a 3D scalar function at (x, y, z).
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
        public static double Laplacian(ScalarFunction3D function, double x, double y, double z, double step = 0.0)
        {
            return SecondPartialDerivative(function, x, y, z, 0, step)
                + SecondPartialDerivative(function, x, y, z, 1, step)
                + SecondPartialDerivative(function, x, y, z, 2, step);
        }

        /// <summary>
        /// Numerically approximates the divergence (div F) of a 2D vector field at (x, y).
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
        public static double Divergence(VectorField2D field, double x, double y, double step = 0.0)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            return PartialDerivative((a, b) => field(a, b).X, x, y, 0, step)
                + PartialDerivative((a, b) => field(a, b).Y, x, y, 1, step);
        }

        /// <summary>
        /// Numerically approximates the divergence (div F) of a 3D vector field at (x, y, z).
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the scalar curl (z-component of curl F) of a 2D vector field at (x, y).
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
        public static double Curl(VectorField2D field, double x, double y, double step = 0.0)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            return PartialDerivative((a, b) => field(a, b).Y, x, y, 0, step)
                - PartialDerivative((a, b) => field(a, b).X, x, y, 1, step);
        }

        /// <summary>
        /// Numerically approximates the curl vector (curl F) of a 3D vector field at (x, y, z).
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the Jacobian determinant of the 2D map defined by two scalar component functions at (x, y).
        /// </summary>
        /// <param name="firstComponent">The first component.</param>
        /// <param name="secondComponent">The second component.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the double integral of a 2D scalar function over the rectangular region [xLower, xUpper] x [yLower, yUpper] using nested Simpson's rule.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the double integral of a 2D scalar function over a non-rectangular region bounded vertically by yLower(x) and yUpper(x) for x in [xLower, xUpper].
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the double integral in polar coordinates (r, theta) over [radiusLower, radiusUpper] x [angleLower, angleUpper], applying the r Jacobian area element.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="radiusLower">The radius lower.</param>
        /// <param name="radiusUpper">The radius upper.</param>
        /// <param name="angleLower">The angle lower.</param>
        /// <param name="angleUpper">The angle upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the triple integral of a 3D scalar function over the box domain [xLower, xUpper] x [yLower, yUpper] x [zLower, zUpper].
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="zLower">The z lower.</param>
        /// <param name="zUpper">The z upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the triple integral in cylindrical coordinates (r, theta, z), applying the r Jacobian volume element.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="radiusLower">The radius lower.</param>
        /// <param name="radiusUpper">The radius upper.</param>
        /// <param name="angleLower">The angle lower.</param>
        /// <param name="angleUpper">The angle upper.</param>
        /// <param name="zLower">The z lower.</param>
        /// <param name="zUpper">The z upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the triple integral in spherical coordinates (rho, theta, phi), applying the rho^2 sin(phi) Jacobian volume element.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="rhoLower">The rho lower.</param>
        /// <param name="rhoUpper">The rho upper.</param>
        /// <param name="thetaLower">The theta lower.</param>
        /// <param name="thetaUpper">The theta upper.</param>
        /// <param name="phiLower">The phi lower.</param>
        /// <param name="phiUpper">The phi upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the scalar line integral (integral of f(x(t), y(t)) ds) of a 2D scalar field along the parametric curve (x(t), y(t)) for t in [lower, upper].
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="xOfT">The x of t.</param>
        /// <param name="yOfT">The y of t.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the scalar line integral (integral of f(r(t)) ds) of a 3D scalar field along a ParametricCurve for parameter t in [lower, upper].
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="curve">The curve.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the line integral (integral of F . dr) of a 2D vector field along the parametric curve (x(t), y(t)) for t in [lower, upper].
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="xOfT">The x of t.</param>
        /// <param name="yOfT">The y of t.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the line integral (integral of F . dr) of a 3D vector field along a ParametricCurve for parameter t in [lower, upper].
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="curve">The curve.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the 2D normal flux line integral (integral of F . n ds) across the parametric curve (x(t), y(t)) for t in [lower, upper].
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="xOfT">The x of t.</param>
        /// <param name="yOfT">The y of t.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the surface area of a ParametricSurface over the parameter domain [uLower, uUpper] x [vLower, vUpper].
        /// </summary>
        /// <param name="surface">The surface.</param>
        /// <param name="uLower">The u lower.</param>
        /// <param name="uUpper">The u upper.</param>
        /// <param name="vLower">The v lower.</param>
        /// <param name="vUpper">The v upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the surface area of the explicit graph z = f(x, y) over [xLower, xUpper] x [yLower, yUpper].
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the surface integral (integral of f(x, y, z) dS) of a 3D scalar function over a ParametricSurface on [uLower, uUpper] x [vLower, vUpper].
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="surface">The surface.</param>
        /// <param name="uLower">The u lower.</param>
        /// <param name="uUpper">The u upper.</param>
        /// <param name="vLower">The v lower.</param>
        /// <param name="vUpper">The v upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the surface flux integral (integral of F . dS) of a 3D vector field across a ParametricSurface on [uLower, uUpper] x [vLower, vUpper].
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="surface">The surface.</param>
        /// <param name="uLower">The u lower.</param>
        /// <param name="uUpper">The u upper.</param>
        /// <param name="vLower">The v lower.</param>
        /// <param name="vUpper">The v upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the arc length of a ParametricCurve for parameter t in [lower, upper].
        /// </summary>
        /// <param name="curve">The curve.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
        public static double ArcLength(ParametricCurve curve, double lower, double upper)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }
            Func<double, double> integrand = t => Magnitude(CurveDerivative(curve, t));
            return Math_3.AdaptiveIntegral(integrand, lower, upper);
        }

        /// <summary>
        /// Computes the arc length of a 2D parametric curve (x(t), y(t)) for parameter t in [lower, upper].
        /// </summary>
        /// <param name="xOfT">The x of t.</param>
        /// <param name="yOfT">The y of t.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
        public static double ArcLengthParametric2D(Func<double, double> xOfT, Func<double, double> yOfT, double lower, double upper)
        {
            return Math_3.ArcLengthParametric(xOfT, yOfT, lower, upper);
        }

        /// <summary>
        /// Computes the area (one-half integral of r(theta)^2 d theta) of a polar region bounded by r(theta) for theta in [lower, upper].
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the center of mass (x-bar, y-bar) of a 2D planar lamina with areal density over [xLower, xUpper] x [yLower, yUpper].
        /// </summary>
        /// <param name="density">The density.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically computes the moment of inertia (I_x = integral of y^2 rho(x, y) dA) about the x-axis for a 2D lamina over [xLower, xUpper] x [yLower, yUpper].
        /// </summary>
        /// <param name="density">The density.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
        public static double MomentOfInertiaAboutX(ScalarFunction2D density, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            if (density == null)
            {
                throw new ArgumentNullException(nameof(density));
            }
            return DoubleIntegral((x, y) => y * y * density(x, y), xLower, xUpper, yLower, yUpper, intervals);
        }

        /// <summary>
        /// Numerically computes the moment of inertia (I_y = integral of x^2 rho(x, y) dA) about the y-axis for a 2D lamina over [xLower, xUpper] x [yLower, yUpper].
        /// </summary>
        /// <param name="density">The density.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
        public static double MomentOfInertiaAboutY(ScalarFunction2D density, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            if (density == null)
            {
                throw new ArgumentNullException(nameof(density));
            }
            return DoubleIntegral((x, y) => x * x * density(x, y), xLower, xUpper, yLower, yUpper, intervals);
        }

        /// <summary>
        /// Numerically computes the mean value of a 2D scalar function over the rectangular domain [xLower, xUpper] x [yLower, yUpper].
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="xLower">The x lower.</param>
        /// <param name="xUpper">The x upper.</param>
        /// <param name="yLower">The y lower.</param>
        /// <param name="yUpper">The y upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
        public static double AverageValue(ScalarFunction2D function, double xLower, double xUpper, double yLower, double yUpper, int intervals = 100)
        {
            double area = (xUpper - xLower) * (yUpper - yLower);
            if (area == 0.0)
            {
                throw new ArgumentException("The region has zero area.");
            }
            return DoubleIntegral(function, xLower, xUpper, yLower, yUpper, intervals) / area;
        }

        /// <summary>
        /// Computes the work done by a 3D force field along a ParametricCurve for parameter t in [lower, upper]; equivalent to the line integral of the field.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <param name="curve">The curve.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <returns>The result.</returns>
        public static double WorkDoneByField(VectorField3D field, ParametricCurve curve, double lower, double upper)
        {
            return VectorLineIntegral(field, curve, lower, upper);
        }

        /// <summary>
        /// Minimizes a 2D scalar function by fixed-step gradient descent starting at (x, y), stopping early when the update falls below tolerance; returns the final point.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="learningRate">The learning rate.</param>
        /// <param name="maxIterations">The max iterations.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Solves the first-order ODE y' = derivative(x, y) from xStart to xEnd with the explicit Euler method using the given number of steps.
        /// </summary>
        /// <param name="derivative">The derivative.</param>
        /// <param name="xStart">The x start.</param>
        /// <param name="yStart">The y start.</param>
        /// <param name="xEnd">The x end.</param>
        /// <param name="steps">The steps.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Solves the first-order ODE y' = derivative(x, y) from xStart to xEnd with the improved Euler (Heun predictor-corrector) method using the given number of steps.
        /// </summary>
        /// <param name="derivative">The derivative.</param>
        /// <param name="xStart">The x start.</param>
        /// <param name="yStart">The y start.</param>
        /// <param name="xEnd">The x end.</param>
        /// <param name="steps">The steps.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Solves the first-order ODE y' = derivative(x, y) from xStart to xEnd with the classical fourth-order Runge-Kutta method using the given number of steps.
        /// </summary>
        /// <param name="derivative">The derivative.</param>
        /// <param name="xStart">The x start.</param>
        /// <param name="yStart">The y start.</param>
        /// <param name="xEnd">The x end.</param>
        /// <param name="steps">The steps.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Solves a system of first-order ODEs y' = derivative(t, y) from start to end with the classical fourth-order Runge-Kutta method, starting from the initial state vector.
        /// </summary>
        /// <param name="derivative">The derivative.</param>
        /// <param name="start">The start.</param>
        /// <param name="initial">The initial.</param>
        /// <param name="end">The end.</param>
        /// <param name="steps">The steps.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the mixed second partial derivative of a 3D scalar function at (x, y, z) with respect to the coordinates at firstIndex and secondIndex.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="firstIndex">The first index.</param>
        /// <param name="secondIndex">The second index.</param>
        /// <param name="step">The step.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Numerically approximates the derivative of a ParametricCurve with respect to its parameter at t using a central difference.
        /// </summary>
        /// <param name="curve">The curve.</param>
        /// <param name="t">The parameter value.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the cross product of the numerically estimated partial derivatives of a ParametricSurface with respect to u and v at (u, v).
        /// </summary>
        /// <param name="surface">The surface.</param>
        /// <param name="u">The u.</param>
        /// <param name="v">The v.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Returns the Euclidean length of value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        private static double Magnitude(Vector3 value)
        {
            return Math_2.CalculateVectorMagnitude(value);
        }

        /// <summary>
        /// Numerically integrates function over [lower, upper] using composite Simpson's rule with the given number of intervals.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <param name="lower">The lower.</param>
        /// <param name="upper">The upper.</param>
        /// <param name="intervals">The intervals.</param>
        /// <returns>The result.</returns>
        private static double Simpson(Func<double, double> function, double lower, double upper, int intervals)
        {
            return Math_3.NumericIntegral(function, lower, upper, intervals);
        }

        /// <summary>
        /// Returns step if it is positive; otherwise returns a finite-difference step scaled from the magnitude of coordinate.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <param name="coordinate">The coordinate.</param>
        /// <returns>The result.</returns>
        private static double ResolveStep(double step, double coordinate)
        {
            if (step > 0.0)
            {
                return step;
            }
            return Math.Max(1e-5, Math.Abs(coordinate) * 1e-5);
        }

        /// <summary>
        /// Validates the derivative delegate, step count, and interval endpoints shared by the single-equation ODE solvers.
        /// </summary>
        /// <param name="derivative">The derivative.</param>
        /// <param name="steps">The steps.</param>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <exception cref="ArgumentNullException">Thrown when derivative is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when steps is less than one.</exception>
        /// <exception cref="ArgumentException">Thrown when start or end is not finite.</exception>
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

        /// <summary>
        /// Returns a new array equal to values plus scale times increment, computed element-wise.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="increment">The increment.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The result.</returns>
        private static double[] AddScaled(double[] values, double[] increment, double scale)
        {
            double[] result = new double[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = values[index] + scale * increment[index];
            }
            return result;
        }

        /// <summary>
        /// Returns a new array containing a copy of the elements of values.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <returns>The result.</returns>
        private static double[] Copy(double[] values)
        {
            double[] result = new double[values.Length];
            Array.Copy(values, result, values.Length);
            return result;
        }
    }
}
