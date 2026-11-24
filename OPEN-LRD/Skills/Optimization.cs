using System;
using System.Collections.Generic;

namespace OPEN_LRD.Skills
{
    internal sealed record OptimizationResult(
        double[] Point,
        double Value,
        double GradientNorm,
        int Iterations,
        bool Converged);

    internal sealed record LinearProgrammingResult(double ObjectiveValue, double[] Variables, bool Feasible);

    internal sealed record AssignmentResult(int[] Assignment, double TotalCost);

    internal static class Optimization
    {
        private const int MaxLineSearchSteps = 60;

        public static double GoldenSectionSearch(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxIterations = 200)
        {
            ValidateInterval(function, lower, upper, tolerance, maxIterations);
            const double ratio = 0.6180339887498948482;
            double first = upper - ratio * (upper - lower);
            double second = lower + ratio * (upper - lower);
            double firstValue = EvaluateFinite(function, first);
            double secondValue = EvaluateFinite(function, second);
            for (int iteration = 0; iteration < maxIterations && upper - lower > tolerance * (1.0 + Math.Max(Math.Abs(lower), Math.Abs(upper))); iteration++)
            {
                if (firstValue <= secondValue)
                {
                    upper = second;
                    second = first;
                    secondValue = firstValue;
                    first = upper - ratio * (upper - lower);
                    firstValue = EvaluateFinite(function, first);
                }
                else
                {
                    lower = first;
                    first = second;
                    firstValue = secondValue;
                    second = lower + ratio * (upper - lower);
                    secondValue = EvaluateFinite(function, second);
                }
            }
            return lower + (upper - lower) * 0.5;
        }

        public static double BrentMinimize(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxIterations = 200)
        {
            ValidateInterval(function, lower, upper, tolerance, maxIterations);
            const double golden = 0.3819660112501051518;
            double x = lower + golden * (upper - lower);
            double w = x;
            double v = x;
            double fx = EvaluateFinite(function, x);
            double fw = fx;
            double fv = fx;
            double movement = 0.0;
            double previousMovement = 0.0;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double midpoint = (lower + upper) * 0.5;
                double tol1 = tolerance * Math.Abs(x) + 1e-14;
                double tol2 = 2.0 * tol1;
                if (Math.Abs(x - midpoint) <= tol2 - (upper - lower) * 0.5)
                {
                    return x;
                }
                bool useParabola = Math.Abs(previousMovement) > tol1;
                if (useParabola)
                {
                    double r = (x - w) * (fx - fv);
                    double q = (x - v) * (fx - fw);
                    double p = (x - v) * q - (x - w) * r;
                    q = 2.0 * (q - r);
                    if (q > 0.0)
                    {
                        p = -p;
                    }
                    q = Math.Abs(q);
                    double oldMovement = previousMovement;
                    previousMovement = movement;
                    if (Math.Abs(p) < Math.Abs(0.5 * q * oldMovement) && p > q * (lower - x) && p < q * (upper - x))
                    {
                        movement = p / q;
                        double candidate = x + movement;
                        if (candidate - lower < tol2 || upper - candidate < tol2)
                        {
                            movement = x < midpoint ? tol1 : -tol1;
                        }
                    }
                    else
                    {
                        previousMovement = x < midpoint ? upper - x : lower - x;
                        movement = golden * previousMovement;
                    }
                }
                else
                {
                    previousMovement = x < midpoint ? upper - x : lower - x;
                    movement = golden * previousMovement;
                }
                double u = x + (Math.Abs(movement) >= tol1 ? movement : movement > 0.0 ? tol1 : -tol1);
                double fu = EvaluateFinite(function, u);
                if (fu <= fx)
                {
                    if (u < x)
                    {
                        upper = x;
                    }
                    else
                    {
                        lower = x;
                    }
                    v = w;
                    fv = fw;
                    w = x;
                    fw = fx;
                    x = u;
                    fx = fu;
                }
                else
                {
                    if (u < x)
                    {
                        lower = u;
                    }
                    else
                    {
                        upper = u;
                    }
                    if (fu <= fw || w == x)
                    {
                        v = w;
                        fv = fw;
                        w = u;
                        fw = fu;
                    }
                    else if (fu <= fv || v == x || v == w)
                    {
                        v = u;
                        fv = fu;
                    }
                }
            }
            return x;
        }

        public static OptimizationResult GradientDescent(
            Func<double[], double> function,
            Func<double[], double[]> gradient,
            double[] initialGuess,
            double learningRate,
            double tolerance = 1e-9,
            int maxIterations = 10000)
        {
            ValidateOptimizer(function, initialGuess, tolerance, maxIterations);
            ValidateFunction(gradient);
            EnsurePositiveFinite(learningRate, nameof(learningRate));
            double[] point = (double[])initialGuess.Clone();
            double value = EvaluateFinite(function, point);
            double[] gradientValue = EvaluateGradient(gradient, point);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double norm = Norm(gradientValue);
                if (norm <= tolerance)
                {
                    return new OptimizationResult(point, value, norm, iteration, true);
                }
                double step = learningRate;
                bool accepted = false;
                double[] candidate = point;
                double candidateValue = value;
                double directional = Dot(gradientValue, gradientValue);
                for (int line = 0; line < MaxLineSearchSteps; line++)
                {
                    candidate = AddScaled(point, gradientValue, -step);
                    candidateValue = EvaluateFinite(function, candidate);
                    if (candidateValue <= value - 1e-4 * step * directional)
                    {
                        accepted = true;
                        break;
                    }
                    step *= 0.5;
                }
                if (!accepted)
                {
                    return new OptimizationResult(point, value, norm, iteration, false);
                }
                point = candidate;
                value = candidateValue;
                gradientValue = EvaluateGradient(gradient, point);
            }
            return new OptimizationResult(point, value, Norm(gradientValue), maxIterations, false);
        }

        public static OptimizationResult Bfgs(
            Func<double[], double> function,
            Func<double[], double[]> gradient,
            double[] initialGuess,
            double tolerance = 1e-9,
            int maxIterations = 1000)
        {
            ValidateOptimizer(function, initialGuess, tolerance, maxIterations);
            ValidateFunction(gradient);
            int dimension = initialGuess.Length;
            double[] point = (double[])initialGuess.Clone();
            double value = EvaluateFinite(function, point);
            double[] gradientValue = EvaluateGradient(gradient, point);
            double[,] inverseHessian = Identity(dimension);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double gradientNorm = Norm(gradientValue);
                if (gradientNorm <= tolerance)
                {
                    return new OptimizationResult(point, value, gradientNorm, iteration, true);
                }
                double[] direction = Negate(Multiply(inverseHessian, gradientValue));
                if (Dot(direction, gradientValue) >= 0.0)
                {
                    inverseHessian = Identity(dimension);
                    direction = Negate(gradientValue);
                }
                if (!TryBacktracking(function, point, value, gradientValue, direction, out double[] candidate, out double candidateValue))
                {
                    return new OptimizationResult(point, value, gradientNorm, iteration, false);
                }
                double[] nextGradient = EvaluateGradient(gradient, candidate);
                double[] displacement = Subtract(candidate, point);
                double[] gradientChange = Subtract(nextGradient, gradientValue);
                double curvature = Dot(displacement, gradientChange);
                if (curvature > 1e-12 * Norm(displacement) * Norm(gradientChange))
                {
                    double rho = 1.0 / curvature;
                    inverseHessian = BfgsUpdate(inverseHessian, displacement, gradientChange, rho);
                }
                else
                {
                    inverseHessian = Identity(dimension);
                }
                point = candidate;
                value = candidateValue;
                gradientValue = nextGradient;
            }
            return new OptimizationResult(point, value, Norm(gradientValue), maxIterations, false);
        }

        public static OptimizationResult ConjugateGradient(
            Func<double[], double> function,
            Func<double[], double[]> gradient,
            double[] initialGuess,
            double tolerance = 1e-9,
            int maxIterations = 1000)
        {
            ValidateOptimizer(function, initialGuess, tolerance, maxIterations);
            ValidateFunction(gradient);
            double[] point = (double[])initialGuess.Clone();
            double value = EvaluateFinite(function, point);
            double[] gradientValue = EvaluateGradient(gradient, point);
            double[] direction = Negate(gradientValue);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double norm = Norm(gradientValue);
                if (norm <= tolerance)
                {
                    return new OptimizationResult(point, value, norm, iteration, true);
                }
                if (Dot(direction, gradientValue) >= 0.0)
                {
                    direction = Negate(gradientValue);
                }
                if (!TryBacktracking(function, point, value, gradientValue, direction, out double[] candidate, out double candidateValue))
                {
                    return new OptimizationResult(point, value, norm, iteration, false);
                }
                double[] nextGradient = EvaluateGradient(gradient, candidate);
                double[] difference = Subtract(nextGradient, gradientValue);
                double beta = Math.Max(0.0, Dot(nextGradient, difference) / Math.Max(Dot(gradientValue, gradientValue), 1e-30));
                direction = AddScaled(Negate(nextGradient), direction, beta);
                point = candidate;
                value = candidateValue;
                gradientValue = nextGradient;
            }
            return new OptimizationResult(point, value, Norm(gradientValue), maxIterations, false);
        }

        public static OptimizationResult NelderMead(Func<double[], double> function, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 1000)
        {
            ValidateOptimizer(function, initialGuess, tolerance, maxIterations);
            int dimension = initialGuess.Length;
            double[][] simplex = new double[dimension + 1][];
            double[] values = new double[dimension + 1];
            simplex[0] = (double[])initialGuess.Clone();
            values[0] = EvaluateFinite(function, simplex[0]);
            for (int vertex = 0; vertex < dimension; vertex++)
            {
                simplex[vertex + 1] = (double[])initialGuess.Clone();
                simplex[vertex + 1][vertex] += initialGuess[vertex] == 0.0 ? 0.00025 : 0.05 * Math.Abs(initialGuess[vertex]);
                values[vertex + 1] = EvaluateFinite(function, simplex[vertex + 1]);
            }

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                SortSimplex(simplex, values);
                double maximumDistance = 0.0;
                for (int vertex = 1; vertex <= dimension; vertex++)
                {
                    maximumDistance = Math.Max(maximumDistance, Norm(Subtract(simplex[vertex], simplex[0])));
                }
                if (maximumDistance <= tolerance && Math.Abs(values[^1] - values[0]) <= tolerance * (1.0 + Math.Abs(values[0])))
                {
                    return new OptimizationResult(simplex[0], values[0], ApproximateGradientNorm(function, simplex[0]), iteration, true);
                }

                double[] centroid = new double[dimension];
                for (int vertex = 0; vertex < dimension; vertex++)
                {
                    for (int coordinate = 0; coordinate < dimension; coordinate++)
                    {
                        centroid[coordinate] += simplex[vertex][coordinate] / dimension;
                    }
                }
                double[] reflected = AddScaled(centroid, Subtract(centroid, simplex[^1]), 1.0);
                double reflectedValue = EvaluateFinite(function, reflected);
                if (reflectedValue < values[0])
                {
                    double[] expanded = AddScaled(centroid, Subtract(reflected, centroid), 2.0);
                    double expandedValue = EvaluateFinite(function, expanded);
                    if (expandedValue < reflectedValue)
                    {
                        simplex[^1] = expanded;
                        values[^1] = expandedValue;
                    }
                    else
                    {
                        simplex[^1] = reflected;
                        values[^1] = reflectedValue;
                    }
                }
                else if (reflectedValue < values[dimension - 1])
                {
                    simplex[^1] = reflected;
                    values[^1] = reflectedValue;
                }
                else
                {
                    bool outside = reflectedValue < values[^1];
                    double[] contracted = outside
                        ? AddScaled(centroid, Subtract(reflected, centroid), 0.5)
                        : AddScaled(centroid, Subtract(simplex[^1], centroid), 0.5);
                    double contractedValue = EvaluateFinite(function, contracted);
                    if (contractedValue < (outside ? reflectedValue : values[^1]))
                    {
                        simplex[^1] = contracted;
                        values[^1] = contractedValue;
                    }
                    else
                    {
                        for (int vertex = 1; vertex <= dimension; vertex++)
                        {
                            simplex[vertex] = AddScaled(simplex[0], Subtract(simplex[vertex], simplex[0]), 0.5);
                            values[vertex] = EvaluateFinite(function, simplex[vertex]);
                        }
                    }
                }
            }
            SortSimplex(simplex, values);
            return new OptimizationResult(simplex[0], values[0], ApproximateGradientNorm(function, simplex[0]), maxIterations, false);
        }

        /// <summary>
        /// Searches for a minimum of function via simulated annealing from initialGuess.
        /// </summary>
        public static OptimizationResult SimulatedAnnealing(
            Func<double[], double> function,
            double[] initialGuess,
            double initialTemperature,
            double coolingRate,
            int maxIterations,
            Random random)
        {
            ValidateOptimizer(function, initialGuess, 1e-9, maxIterations);
            EnsurePositiveFinite(initialTemperature, nameof(initialTemperature));
            if (!double.IsFinite(coolingRate) || coolingRate <= 0.0 || coolingRate >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(coolingRate), "Cooling rate must be in (0, 1).");
            }
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }
            double[] current = (double[])initialGuess.Clone();
            double currentValue = EvaluateFinite(function, current);
            double[] best = (double[])current.Clone();
            double bestValue = currentValue;
            double temperature = initialTemperature;
            int stagnantIterations = 0;
            int stagnationLimit = Math.Max(100, initialGuess.Length * 50);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double[] candidate = (double[])current.Clone();
                for (int coordinate = 0; coordinate < candidate.Length; coordinate++)
                {
                    candidate[coordinate] += temperature * StandardNormal(random);
                }
                double candidateValue = EvaluateFinite(function, candidate);
                double difference = candidateValue - currentValue;
                if (difference <= 0.0 || (temperature > 0.0 && random.NextDouble() < Math.Exp(-difference / temperature)))
                {
                    current = candidate;
                    currentValue = candidateValue;
                    if (currentValue < bestValue)
                    {
                        if (bestValue - currentValue > 1e-12 * (1.0 + Math.Abs(bestValue)))
                        {
                            stagnantIterations = 0;
                        }
                        best = (double[])current.Clone();
                        bestValue = currentValue;
                    }
                }
                temperature *= coolingRate;
                if (stagnantIterations < stagnationLimit)
                {
                    stagnantIterations++;
                }
                if (temperature <= initialTemperature * 1e-10 || stagnantIterations >= stagnationLimit)
                {
                    return new OptimizationResult(best, bestValue, ApproximateGradientNorm(function, best), iteration + 1, true);
                }
            }
            return new OptimizationResult(best, bestValue, ApproximateGradientNorm(function, best), maxIterations, false);
        }

        public static OptimizationResult ProjectedGradient(
            Func<double[], double> function,
            Func<double[], double[]> gradient,
            double[] initialGuess,
            double[] lowerBounds,
            double[] upperBounds,
            double learningRate,
            double tolerance = 1e-9,
            int maxIterations = 10000)
        {
            ValidateOptimizer(function, initialGuess, tolerance, maxIterations);
            ValidateFunction(gradient);
            EnsurePositiveFinite(learningRate, nameof(learningRate));
            ValidateBounds(initialGuess.Length, lowerBounds, upperBounds);
            double[] point = Project((double[])initialGuess.Clone(), lowerBounds, upperBounds);
            double value = EvaluateFinite(function, point);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double[] gradientValue = EvaluateGradient(gradient, point);
                double[] projectedGradient = new double[point.Length];
                for (int index = 0; index < point.Length; index++)
                {
                    double projected = Math.Clamp(point[index] - gradientValue[index], lowerBounds[index], upperBounds[index]);
                    projectedGradient[index] = point[index] - projected;
                }
                double norm = Norm(projectedGradient);
                if (norm <= tolerance)
                {
                    return new OptimizationResult(point, value, norm, iteration, true);
                }
                double step = learningRate;
                bool accepted = false;
                for (int line = 0; line < MaxLineSearchSteps; line++)
                {
                    double[] candidate = Project(AddScaled(point, gradientValue, -step), lowerBounds, upperBounds);
                    double candidateValue = EvaluateFinite(function, candidate);
                    if (candidateValue < value)
                    {
                        point = candidate;
                        value = candidateValue;
                        accepted = true;
                        break;
                    }
                    step *= 0.5;
                }
                if (!accepted)
                {
                    return new OptimizationResult(point, value, norm, iteration, false);
                }
            }
            return new OptimizationResult(point, value, Norm(EvaluateGradient(gradient, point)), maxIterations, false);
        }

        public static OptimizationResult PenaltyMethod(
            Func<double[], double> objective,
            Func<double[], double[]> equalityConstraints,
            double[] initialGuess,
            double penaltyWeight,
            double tolerance = 1e-9,
            int maxIterations = 1000)
        {
            ValidateOptimizer(objective, initialGuess, tolerance, maxIterations);
            ValidateFunction(equalityConstraints);
            EnsurePositiveFinite(penaltyWeight, nameof(penaltyWeight));
            double[] point = (double[])initialGuess.Clone();
            double Penalized(double[] candidate)
            {
                double baseValue = EvaluateFinite(objective, candidate);
                double[] constraints = equalityConstraints((double[])candidate.Clone());
                if (constraints == null)
                {
                    throw new InvalidOperationException("Constraint function returned null.");
                }
                EnsureFinite(constraints, nameof(equalityConstraints));
                return baseValue + penaltyWeight * Dot(constraints, constraints);
            }

            double value = EvaluateFinite(Penalized, point);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double[] gradient = NumericalGradient(Penalized, point);
                double norm = Norm(gradient);
                if (norm <= tolerance)
                {
                    return new OptimizationResult(point, EvaluateFinite(objective, point), norm, iteration, true);
                }
                double step = 1.0;
                bool accepted = false;
                for (int line = 0; line < MaxLineSearchSteps; line++)
                {
                    double[] candidate = AddScaled(point, gradient, -step);
                    double candidateValue = EvaluateFinite(Penalized, candidate);
                    if (candidateValue < value)
                    {
                        point = candidate;
                        value = candidateValue;
                        accepted = true;
                        break;
                    }
                    step *= 0.5;
                }
                if (!accepted)
                {
                    return new OptimizationResult(point, EvaluateFinite(objective, point), norm, iteration, false);
                }
            }
            return new OptimizationResult(point, EvaluateFinite(objective, point), Norm(NumericalGradient(Penalized, point)), maxIterations, false);
        }

        public static LinearProgrammingResult Simplex(double[,] constraintMatrix, double[] rightHandSide, double[] objective)
        {
            ValidateLinearProgram(constraintMatrix, rightHandSide, objective);
            for (int index = 0; index < rightHandSide.Length; index++)
            {
                if (rightHandSide[index] < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(rightHandSide), "Simplex standard form requires non-negative right-hand sides.");
                }
            }
            return SolveLinearProgram(constraintMatrix, rightHandSide, objective, false);
        }

        public static LinearProgrammingResult TwoPhaseSimplex(double[,] constraintMatrix, double[] rightHandSide, double[] objective)
        {
            ValidateLinearProgram(constraintMatrix, rightHandSide, objective);
            return SolveLinearProgram(constraintMatrix, rightHandSide, objective, true);
        }

        public static AssignmentResult HungarianAssignment(double[,] costMatrix)
        {
            if (costMatrix == null)
            {
                throw new ArgumentNullException(nameof(costMatrix));
            }
            int rows = costMatrix.GetLength(0);
            int columns = costMatrix.GetLength(1);
            if (rows == 0 || columns == 0)
            {
                throw new ArgumentException("The assignment matrix must not be empty.", nameof(costMatrix));
            }
            EnsureFinite(costMatrix, nameof(costMatrix));
            int[] assignment = new int[rows];
            Array.Fill(assignment, -1);
            if (rows <= columns)
            {
                int[] rowToColumn = HungarianRowsAtMostColumns(costMatrix);
                Array.Copy(rowToColumn, assignment, rows);
            }
            else
            {
                double[,] transposed = new double[columns, rows];
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        transposed[column, row] = costMatrix[row, column];
                    }
                }
                int[] transposedAssignment = HungarianRowsAtMostColumns(transposed);
                for (int originalColumn = 0; originalColumn < transposedAssignment.Length; originalColumn++)
                {
                    assignment[transposedAssignment[originalColumn]] = originalColumn;
                }
            }
            double total = 0.0;
            for (int row = 0; row < rows; row++)
            {
                if (assignment[row] >= 0)
                {
                    total += costMatrix[row, assignment[row]];
                }
            }
            return new AssignmentResult(assignment, total);
        }

        private static LinearProgrammingResult SolveLinearProgram(double[,] input, double[] inputRhs, double[] objective, bool allowNegativeRhs)
        {
            int rows = input.GetLength(0);
            int variables = input.GetLength(1);
            bool[] greaterThan = new bool[rows];
            int artificialCount = 0;
            for (int row = 0; row < rows; row++)
            {
                greaterThan[row] = inputRhs[row] < 0.0;
                if (greaterThan[row] && !allowNegativeRhs)
                {
                    return new LinearProgrammingResult(0.0, Array.Empty<double>(), false);
                }
                if (greaterThan[row])
                {
                    artificialCount++;
                }
            }
            int regularColumns = variables + rows;
            int totalColumns = regularColumns + artificialCount;
            int rhsColumn = totalColumns;
            double[,] tableau = new double[rows + 1, totalColumns + 1];
            int[] basis = new int[rows];
            bool[] artificial = new bool[totalColumns];
            int artificialColumn = regularColumns;
            for (int row = 0; row < rows; row++)
            {
                double sign = greaterThan[row] ? -1.0 : 1.0;
                for (int column = 0; column < variables; column++)
                {
                    tableau[row + 1, column] = sign * input[row, column];
                }
                tableau[row + 1, variables + row] = greaterThan[row] ? -1.0 : 1.0;
                tableau[row + 1, rhsColumn] = sign * inputRhs[row];
                if (greaterThan[row])
                {
                    tableau[row + 1, artificialColumn] = 1.0;
                    basis[row] = artificialColumn;
                    artificial[artificialColumn] = true;
                    artificialColumn++;
                }
                else
                {
                    basis[row] = variables + row;
                }
            }

            if (artificialCount > 0)
            {
                for (int column = regularColumns; column < totalColumns; column++)
                {
                    tableau[0, column] = 1.0;
                }
                CanonicalizeObjective(tableau, basis, rows, totalColumns);
                bool[] phaseOneAllowed = new bool[totalColumns];
                Array.Fill(phaseOneAllowed, true);
                if (!OptimizeTableau(tableau, basis, rows, totalColumns, phaseOneAllowed, out _)
                    || tableau[0, rhsColumn] < -1e-8)
                {
                    return new LinearProgrammingResult(0.0, Array.Empty<double>(), false);
                }

                bool[] redundant = new bool[rows];
                for (int row = 0; row < rows; row++)
                {
                    if (!artificial[basis[row]])
                    {
                        continue;
                    }
                    int entering = -1;
                    for (int column = 0; column < regularColumns; column++)
                    {
                        if (Math.Abs(tableau[row + 1, column]) > 1e-10)
                        {
                            entering = column;
                            break;
                        }
                    }
                    if (entering < 0)
                    {
                        if (Math.Abs(tableau[row + 1, rhsColumn]) > 1e-8)
                        {
                            return new LinearProgrammingResult(0.0, Array.Empty<double>(), false);
                        }
                        redundant[row] = true;
                    }
                    else
                    {
                        Pivot(tableau, basis, row + 1, entering, totalColumns);
                    }
                }
                if (Array.Exists(redundant, value => value))
                {
                    (tableau, basis, rows) = RemoveRedundantRows(tableau, basis, redundant, totalColumns);
                }
            }

            Array.Clear(tableau, 0, totalColumns + 1);
            for (int column = 0; column < variables; column++)
            {
                tableau[0, column] = -objective[column];
            }
            CanonicalizeObjective(tableau, basis, rows, totalColumns);
            bool[] allowed = new bool[totalColumns];
            for (int column = 0; column < regularColumns; column++)
            {
                allowed[column] = true;
            }
            if (!OptimizeTableau(tableau, basis, rows, totalColumns, allowed, out _))
            {
                return new LinearProgrammingResult(0.0, Array.Empty<double>(), false);
            }
            double[] solution = new double[variables];
            for (int row = 0; row < rows; row++)
            {
                if (basis[row] < variables)
                {
                    solution[basis[row]] = tableau[row + 1, rhsColumn];
                }
            }
            return new LinearProgrammingResult(tableau[0, rhsColumn], solution, true);
        }

        private static bool OptimizeTableau(double[,] tableau, int[] basis, int rows, int columns, bool[] allowed, out int iterations)
        {
            int rhs = columns;
            for (iterations = 0; iterations < 10000; iterations++)
            {
                int entering = -1;
                for (int column = 0; column < columns; column++)
                {
                    if (allowed[column] && tableau[0, column] < -1e-10)
                    {
                        entering = column;
                        break;
                    }
                }
                if (entering < 0)
                {
                    return true;
                }
                int leaving = -1;
                double minimumRatio = double.PositiveInfinity;
                for (int row = 0; row < rows; row++)
                {
                    double coefficient = tableau[row + 1, entering];
                    if (coefficient <= 1e-12)
                    {
                        continue;
                    }
                    double ratio = tableau[row + 1, rhs] / coefficient;
                    if (ratio < minimumRatio - 1e-12 || (Math.Abs(ratio - minimumRatio) <= 1e-12 && (leaving < 0 || basis[row] < basis[leaving])))
                    {
                        minimumRatio = ratio;
                        leaving = row;
                    }
                }
                if (leaving < 0)
                {
                    return false;
                }
                Pivot(tableau, basis, leaving + 1, entering, columns);
            }
            return false;
        }

        private static void CanonicalizeObjective(double[,] tableau, int[] basis, int rows, int columns)
        {
            for (int row = 0; row < rows; row++)
            {
                double coefficient = tableau[0, basis[row]];
                if (coefficient == 0.0)
                {
                    continue;
                }
                for (int column = 0; column <= columns; column++)
                {
                    tableau[0, column] -= coefficient * tableau[row + 1, column];
                }
            }
        }

        private static void Pivot(double[,] tableau, int[] basis, int pivotRow, int pivotColumn, int columns)
        {
            double pivot = tableau[pivotRow, pivotColumn];
            for (int column = 0; column <= columns; column++)
            {
                tableau[pivotRow, column] /= pivot;
            }
            for (int row = 0; row < tableau.GetLength(0); row++)
            {
                if (row == pivotRow)
                {
                    continue;
                }
                double factor = tableau[row, pivotColumn];
                if (factor == 0.0)
                {
                    continue;
                }
                for (int column = 0; column <= columns; column++)
                {
                    tableau[row, column] -= factor * tableau[pivotRow, column];
                }
            }
            basis[pivotRow - 1] = pivotColumn;
        }

        private static (double[,] Tableau, int[] Basis, int Rows) RemoveRedundantRows(double[,] tableau, int[] basis, bool[] redundant, int columns)
        {
            int rows = basis.Length;
            int kept = 0;
            for (int row = 0; row < rows; row++)
            {
                if (!redundant[row])
                {
                    kept++;
                }
            }
            double[,] reduced = new double[kept + 1, columns + 1];
            int[] newBasis = new int[kept];
            Array.Copy(tableau, 0, reduced, 0, columns + 1);
            int destination = 0;
            for (int row = 0; row < rows; row++)
            {
                if (redundant[row])
                {
                    continue;
                }
                for (int column = 0; column <= columns; column++)
                {
                    reduced[destination + 1, column] = tableau[row + 1, column];
                }
                newBasis[destination] = basis[row];
                destination++;
            }
            return (reduced, newBasis, kept);
        }

        private static int[] HungarianRowsAtMostColumns(double[,] costs)
        {
            int rows = costs.GetLength(0);
            int columns = costs.GetLength(1);
            double[] rowPotential = new double[rows + 1];
            double[] columnPotential = new double[columns + 1];
            int[] matchedRow = new int[columns + 1];
            int[] previousColumn = new int[columns + 1];
            for (int row = 1; row <= rows; row++)
            {
                matchedRow[0] = row;
                int currentColumn = 0;
                double[] minimum = new double[columns + 1];
                Array.Fill(minimum, double.PositiveInfinity);
                bool[] used = new bool[columns + 1];
                do
                {
                    used[currentColumn] = true;
                    int currentRow = matchedRow[currentColumn];
                    double delta = double.PositiveInfinity;
                    int nextColumn = 0;
                    for (int column = 1; column <= columns; column++)
                    {
                        if (used[column])
                        {
                            continue;
                        }
                        double reducedCost = costs[currentRow - 1, column - 1] - rowPotential[currentRow] - columnPotential[column];
                        if (reducedCost < minimum[column])
                        {
                            minimum[column] = reducedCost;
                            previousColumn[column] = currentColumn;
                        }
                        if (minimum[column] < delta)
                        {
                            delta = minimum[column];
                            nextColumn = column;
                        }
                    }
                    for (int column = 0; column <= columns; column++)
                    {
                        if (used[column])
                        {
                            rowPotential[matchedRow[column]] += delta;
                            columnPotential[column] -= delta;
                        }
                        else
                        {
                            minimum[column] -= delta;
                        }
                    }
                    currentColumn = nextColumn;
                }
                while (matchedRow[currentColumn] != 0);

                do
                {
                    int previous = previousColumn[currentColumn];
                    matchedRow[currentColumn] = matchedRow[previous];
                    currentColumn = previous;
                }
                while (currentColumn != 0);
            }
            int[] assignment = new int[rows];
            Array.Fill(assignment, -1);
            for (int column = 1; column <= columns; column++)
            {
                if (matchedRow[column] != 0)
                {
                    assignment[matchedRow[column] - 1] = column - 1;
                }
            }
            return assignment;
        }

        private static void ValidateLinearProgram(double[,] matrix, double[] rhs, double[] objective)
        {
            if (matrix == null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }
            if (rhs == null)
            {
                throw new ArgumentNullException(nameof(rhs));
            }
            if (objective == null)
            {
                throw new ArgumentNullException(nameof(objective));
            }
            if (matrix.GetLength(0) == 0 || matrix.GetLength(1) == 0
                || rhs.Length != matrix.GetLength(0) || objective.Length != matrix.GetLength(1))
            {
                throw new ArgumentException("Linear program dimensions do not match.");
            }
            EnsureFinite(matrix, nameof(matrix));
            EnsureFinite(rhs, nameof(rhs));
            EnsureFinite(objective, nameof(objective));
        }

        private static void ValidateOptimizer(Func<double[], double> function, double[] initial, double tolerance, int maxIterations)
        {
            ValidateFunction(function);
            if (initial == null)
            {
                throw new ArgumentNullException(nameof(initial));
            }
            if (initial.Length == 0)
            {
                throw new ArgumentException("The initial point must not be empty.", nameof(initial));
            }
            EnsureFinite(initial, nameof(initial));
            EnsurePositiveFinite(tolerance, nameof(tolerance));
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations));
            }
        }

        private static void ValidateInterval(Func<double, double> function, double lower, double upper, double tolerance, int maxIterations)
        {
            ValidateFunction(function);
            EnsureFinite(lower, nameof(lower));
            EnsureFinite(upper, nameof(upper));
            if (upper <= lower)
            {
                throw new ArgumentOutOfRangeException(nameof(upper), "Upper bound must exceed lower bound.");
            }
            EnsurePositiveFinite(tolerance, nameof(tolerance));
            if (maxIterations < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIterations));
            }
        }

        private static void ValidateBounds(int dimension, double[] lower, double[] upper)
        {
            if (lower == null)
            {
                throw new ArgumentNullException(nameof(lower));
            }
            if (upper == null)
            {
                throw new ArgumentNullException(nameof(upper));
            }
            if (lower.Length != dimension || upper.Length != dimension)
            {
                throw new ArgumentException("Bound vectors must match the point dimension.");
            }
            EnsureFinite(lower, nameof(lower));
            EnsureFinite(upper, nameof(upper));
            for (int index = 0; index < dimension; index++)
            {
                if (lower[index] > upper[index])
                {
                    throw new ArgumentException("Each lower bound must not exceed its upper bound.");
                }
            }
        }

        private static void ValidateFunction(Delegate function)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
        }

        private static bool TryBacktracking(
            Func<double[], double> function,
            double[] point,
            double value,
            double[] gradient,
            double[] direction,
            out double[] candidate,
            out double candidateValue)
        {
            double slope = Dot(gradient, direction);
            double step = 1.0;
            for (int line = 0; line < MaxLineSearchSteps; line++)
            {
                candidate = AddScaled(point, direction, step);
                candidateValue = EvaluateFinite(function, candidate);
                if (candidateValue <= value + 1e-4 * step * slope)
                {
                    return true;
                }
                step *= 0.5;
            }
            candidate = point;
            candidateValue = value;
            return false;
        }

        private static double[] EvaluateGradient(Func<double[], double[]> gradient, double[] point)
        {
            double[] result = gradient((double[])point.Clone());
            if (result == null || result.Length != point.Length)
            {
                throw new ArgumentException("Gradient must match the point dimension.", nameof(gradient));
            }
            EnsureFinite(result, nameof(gradient));
            return result;
        }

        private static double[] NumericalGradient(Func<double[], double> function, double[] point)
        {
            double[] result = new double[point.Length];
            for (int index = 0; index < point.Length; index++)
            {
                double step = 1e-6 * (1.0 + Math.Abs(point[index]));
                double[] forward = (double[])point.Clone();
                double[] backward = (double[])point.Clone();
                forward[index] += step;
                backward[index] -= step;
                result[index] = (EvaluateFinite(function, forward) - EvaluateFinite(function, backward)) / (2.0 * step);
            }
            return result;
        }

        private static double ApproximateGradientNorm(Func<double[], double> function, double[] point)
        {
            return Norm(NumericalGradient(function, point));
        }

        private static double EvaluateFinite(Func<double[], double> function, double[] point)
        {
            double value = function((double[])point.Clone());
            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException("Objective returned a non-finite value.");
            }
            return value;
        }

        private static double EvaluateFinite(Func<double, double> function, double value)
        {
            double result = function(value);
            if (!double.IsFinite(result))
            {
                throw new InvalidOperationException("Function returned a non-finite value.");
            }
            return result;
        }

        private static double[,] BfgsUpdate(double[,] inverse, double[] s, double[] y, double rho)
        {
            int size = s.Length;
            double[,] left = Identity(size);
            double[,] right = Identity(size);
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column < size; column++)
                {
                    left[row, column] -= rho * s[row] * y[column];
                    right[row, column] -= rho * y[row] * s[column];
                }
            }
            return Add(Multiply(Multiply(left, inverse), right), OuterProduct(s, s, rho));
        }

        private static double[,] OuterProduct(double[] first, double[] second, double scale)
        {
            double[,] result = new double[first.Length, second.Length];
            for (int row = 0; row < first.Length; row++)
            {
                for (int column = 0; column < second.Length; column++)
                {
                    result[row, column] = scale * first[row] * second[column];
                }
            }
            return result;
        }

        private static double[,] Identity(int size)
        {
            double[,] result = new double[size, size];
            for (int index = 0; index < size; index++)
            {
                result[index, index] = 1.0;
            }
            return result;
        }

        private static double[,] Multiply(double[,] first, double[,] second)
        {
            return Algebra.MatrixMultiply(first, second);
        }

        private static double[,] Add(double[,] first, double[,] second)
        {
            double[,] result = new double[first.GetLength(0), first.GetLength(1)];
            for (int row = 0; row < result.GetLength(0); row++)
            {
                for (int column = 0; column < result.GetLength(1); column++)
                {
                    result[row, column] = first[row, column] + second[row, column];
                }
            }
            return result;
        }

        private static double[] Multiply(double[,] matrix, double[] vector)
        {
            return LinearAlgebraAdvanced.MatrixVectorMultiply(matrix, vector);
        }

        private static double[] Project(double[] point, double[] lower, double[] upper)
        {
            for (int index = 0; index < point.Length; index++)
            {
                point[index] = Math.Clamp(point[index], lower[index], upper[index]);
            }
            return point;
        }

        private static double[] AddScaled(double[] first, double[] second, double scale)
        {
            double[] result = new double[first.Length];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = first[index] + scale * second[index];
            }
            return result;
        }

        private static double[] Subtract(double[] first, double[] second)
        {
            double[] result = new double[first.Length];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = first[index] - second[index];
            }
            return result;
        }

        private static double[] Negate(double[] values)
        {
            double[] result = new double[values.Length];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = -values[index];
            }
            return result;
        }

        private static double Dot(double[] first, double[] second)
        {
            double result = 0.0;
            for (int index = 0; index < first.Length; index++)
            {
                result += first[index] * second[index];
            }
            return result;
        }

        private static double Norm(double[] values)
        {
            return Math.Sqrt(Dot(values, values));
        }

        private static double StandardNormal(Random random)
        {
            double first = Math.Max(random.NextDouble(), double.Epsilon);
            double second = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(first)) * Math.Cos(2.0 * Math.PI * second);
        }

        private static void SortSimplex(double[][] simplex, double[] values)
        {
            for (int first = 0; first < values.Length - 1; first++)
            {
                int best = first;
                for (int next = first + 1; next < values.Length; next++)
                {
                    if (values[next] < values[best])
                    {
                        best = next;
                    }
                }
                if (best != first)
                {
                    (values[first], values[best]) = (values[best], values[first]);
                    (simplex[first], simplex[best]) = (simplex[best], simplex[first]);
                }
            }
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
