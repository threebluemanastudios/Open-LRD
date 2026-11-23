using System;
using System.Collections.Generic;
using Complex = System.Numerics.Complex;

namespace OPEN_LRD.Skills
{
    internal enum MatrixNormType
    {
        Frobenius,
        One,
        Infinity,
        Two
    }

    internal sealed record LuDecompositionResult(double[,] Lower, double[,] Upper, int[] RowPermutation, int Sign);

    internal sealed record QrDecompositionResult(double[,] Q, double[,] R);

    internal sealed record CholeskyDecompositionResult(double[,] Lower);

    internal sealed record SymmetricEigenResult(double[] Eigenvalues, double[,] Eigenvectors);

    internal sealed record SvdResult(double[,] U, double[] SingularValues, double[,] Vt);

    internal sealed record LeastSquaresResult(double[] Solution, double ResidualNorm);

    internal static class LinearAlgebraAdvanced
    {
        private const double Tolerance = 1e-9;

        public static bool IsSymmetric(double[,] matrix, double tolerance = Tolerance)
        {
            if (matrix is null)
            {
                throw new ArgumentNullException(nameof(matrix));
            }
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            if (rows != columns)
            {
                return false;
            }
            for (int row = 0; row < rows; row++)
            {
                for (int column = row + 1; column < columns; column++)
                {
                    double scale = 1.0 + Math.Max(Math.Abs(matrix[row, column]), Math.Abs(matrix[column, row]));
                    if (Math.Abs(matrix[row, column] - matrix[column, row]) > tolerance * scale)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Computes LU decomposition of matrix.
        /// </summary>
        public static LuDecompositionResult LuDecomposition(double[,] matrix)
        {
            EnsureSquare(matrix, nameof(matrix));
            int size = matrix.GetLength(0);
            double[,] lower = Algebra.MatrixIdentity(size);
            double[,] upper = CloneMatrix(matrix);
            int[] permutation = new int[size];
            for (int index = 0; index < size; index++)
            {
                permutation[index] = index;
            }
            int sign = 1;
            for (int column = 0; column < size; column++)
            {
                int pivotRow = column;
                double pivotMagnitude = Math.Abs(upper[column, column]);
                for (int row = column + 1; row < size; row++)
                {
                    double candidate = Math.Abs(upper[row, column]);
                    if (candidate > pivotMagnitude)
                    {
                        pivotMagnitude = candidate;
                        pivotRow = row;
                    }
                }
                if (pivotMagnitude <= Tolerance)
                {
                    throw new InvalidOperationException("The matrix is singular and has no LU decomposition with partial pivoting.");
                }
                if (pivotRow != column)
                {
                    SwapRows(upper, pivotRow, column);
                    for (int index = 0; index < column; index++)
                    {
                        double temporary = lower[pivotRow, index];
                        lower[pivotRow, index] = lower[column, index];
                        lower[column, index] = temporary;
                    }
                    int temporaryPermutation = permutation[pivotRow];
                    permutation[pivotRow] = permutation[column];
                    permutation[column] = temporaryPermutation;
                    sign = -sign;
                }
                double pivot = upper[column, column];
                for (int row = column + 1; row < size; row++)
                {
                    double factor = upper[row, column] / pivot;
                    lower[row, column] = factor;
                    for (int index = column; index < size; index++)
                    {
                        upper[row, index] -= factor * upper[column, index];
                    }
                }
            }
        /// <summary>
        /// Computes QR decomposition of matrix.
        /// </summary>
            return new LuDecompositionResult(lower, upper, permutation, sign);
        }

        public static QrDecompositionResult QrDecomposition(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] q = Algebra.MatrixIdentity(rows);
            double[,] r = CloneMatrix(matrix);
            int steps = Math.Min(rows - 1, columns);
            for (int column = 0; column < steps; column++)
            {
                double norm = 0.0;
                for (int row = column; row < rows; row++)
                {
                    norm += r[row, column] * r[row, column];
                }
                norm = Math.Sqrt(norm);
                if (norm <= Tolerance)
                {
                    continue;
                }
                double alpha = r[column, column] >= 0.0 ? -norm : norm;
                double[] reflector = new double[rows];
                for (int row = column; row < rows; row++)
                {
                    reflector[row] = r[row, column];
                }
                reflector[column] -= alpha;
                double reflectorNormSquared = 0.0;
                for (int row = column; row < rows; row++)
                {
                    reflectorNormSquared += reflector[row] * reflector[row];
                }
                if (reflectorNormSquared <= Tolerance * Tolerance)
                {
                    continue;
                }
                for (int index = column; index < columns; index++)
                {
                    double dot = 0.0;
                    for (int row = column; row < rows; row++)
                    {
                        dot += reflector[row] * r[row, index];
                    }
                    double factor = 2.0 * dot / reflectorNormSquared;
                    for (int row = column; row < rows; row++)
                    {
                        r[row, index] -= factor * reflector[row];
                    }
                }
                for (int row = 0; row < rows; row++)
                {
                    double dot = 0.0;
                    for (int index = 0; index < rows; index++)
                    {
                        dot += q[row, index] * reflector[index];
                    }
                    double factor = 2.0 * dot / reflectorNormSquared;
                    for (int index = 0; index < rows; index++)
                    {
                        q[row, index] -= factor * reflector[index];
                    }
                }
                r[column, column] = alpha;
                for (int row = column + 1; row < rows; row++)
                {
        /// <summary>
        /// Attempts Cholesky decomposition of matrix, outputting lower-triangular lower factor.
        /// </summary>
                    r[row, column] = 0.0;
                }
            }
            return new QrDecompositionResult(q, r);
        }

        public static bool TryCholeskyDecomposition(double[,] matrix, out double[,] lower)
        {
            EnsureSquare(matrix, nameof(matrix));
            int size = matrix.GetLength(0);
            lower = new double[size, size];
            if (!IsSymmetric(matrix))
            {
                return false;
            }
            for (int row = 0; row < size; row++)
            {
                for (int column = 0; column <= row; column++)
                {
                    double sum = matrix[row, column];
                    for (int index = 0; index < column; index++)
                    {
                        sum -= lower[row, index] * lower[column, index];
                    }
                    if (row == column)
                    {
                        if (sum <= Tolerance)
                        {
                            return false;
                        }
                        lower[row, column] = Math.Sqrt(sum);
                    }
                    else
                    {
                        lower[row, column] = sum / lower[column, column];
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Computes eigendecomposition of symmetric matrix.
        /// </summary>
        public static CholeskyDecompositionResult CholeskyDecomposition(double[,] matrix)
        {
            if (!TryCholeskyDecomposition(matrix, out double[,] lower))
            {
                throw new InvalidOperationException("The matrix is not symmetric positive definite.");
            }
            return new CholeskyDecompositionResult(lower);
        }

        public static SymmetricEigenResult SymmetricEigenDecomposition(double[,] matrix)
        {
            EnsureSquare(matrix, nameof(matrix));
            if (!IsSymmetric(matrix))
            {
                throw new InvalidOperationException("Symmetric eigen decomposition requires a symmetric matrix.");
            }
            int size = matrix.GetLength(0);
            double[,] working = CloneMatrix(matrix);
            double[,] vectors = Algebra.MatrixIdentity(size);
            int maxIterations = Math.Max(1, 50 * size * size);
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double offDiagonal = 0.0;
                for (int row = 0; row < size; row++)
                {
                    for (int column = row + 1; column < size; column++)
                    {
                        offDiagonal += working[row, column] * working[row, column];
                    }
                }
                if (Math.Sqrt(offDiagonal) <= Tolerance)
                {
                    break;
                }
                for (int first = 0; first < size - 1; first++)
                {
                    for (int second = first + 1; second < size; second++)
                    {
                        double off = working[first, second];
                        if (Math.Abs(off) <= Tolerance)
                        {
                            continue;
                        }
                        double firstDiagonal = working[first, first];
                        double secondDiagonal = working[second, second];
                        double angle = 0.5 * (secondDiagonal - firstDiagonal);
                        double tau = angle / off;
                        double tangent = tau == 0.0
                            ? 1.0
                            : Math.Sign(tau) / (Math.Abs(tau) + Math.Sqrt(1.0 + tau * tau));
                        double cosine = 1.0 / Math.Sqrt(1.0 + tangent * tangent);
                        double sine = tangent * cosine;
                        for (int index = 0; index < size; index++)
                        {
                            if (index == first || index == second)
                            {
                                continue;
                            }
                            double firstValue = working[index, first];
                            double secondValue = working[index, second];
                            working[index, first] = cosine * firstValue - sine * secondValue;
                            working[first, index] = working[index, first];
                            working[index, second] = sine * firstValue + cosine * secondValue;
                            working[second, index] = working[index, second];
                        }
                        working[first, first] = cosine * cosine * firstDiagonal
                            - 2.0 * sine * cosine * off
                            + sine * sine * secondDiagonal;
                        working[second, second] = sine * sine * firstDiagonal
                            + 2.0 * sine * cosine * off
                            + cosine * cosine * secondDiagonal;
                        working[first, second] = 0.0;
                        working[second, first] = 0.0;
                        for (int row = 0; row < size; row++)
                        {
                            double firstValue = vectors[row, first];
                            double secondValue = vectors[row, second];
                            vectors[row, first] = cosine * firstValue - sine * secondValue;
                            vectors[row, second] = sine * firstValue + cosine * secondValue;
                        }
                    }
                }
            }
            int[] order = new int[size];
            for (int index = 0; index < size; index++)
            {
                order[index] = index;
            }
            Array.Sort(order, (first, second) =>
            {
                int comparison = working[second, second].CompareTo(working[first, first]);
                return comparison != 0 ? comparison : first.CompareTo(second);
            });
            double[] eigenvalues = new double[size];
            double[,] eigenvectors = new double[size, size];
        /// <summary>
        /// Computes eigenvalues of matrix.
        /// </summary>
            for (int column = 0; column < size; column++)
            {
                int source = order[column];
                eigenvalues[column] = working[source, source];
                for (int row = 0; row < size; row++)
                {
                    eigenvectors[row, column] = vectors[row, source];
                }
            }
            return new SymmetricEigenResult(eigenvalues, eigenvectors);
        }

        public static Complex[] Eigenvalues(double[,] matrix)
        {
            EnsureSquare(matrix, nameof(matrix));
            int size = matrix.GetLength(0);
            double[] coefficients = new double[size + 1];
            double[,] working = Algebra.MatrixIdentity(size);
            for (int iteration = 1; iteration <= size; iteration++)
            {
                working = Algebra.MatrixMultiply(matrix, working);
                double coefficient = -Algebra.MatrixTrace(working) / iteration;
                coefficients[size - iteration] = coefficient;
        /// <summary>
        /// Computes singular value decomposition of matrix.
        /// </summary>
                for (int index = 0; index < size; index++)
                {
                    working[index, index] += coefficient;
                }
            }
            coefficients[size] = 1.0;
            Complex[] roots = Algebra.SolvePolynomial(coefficients);
            Array.Sort(roots, (first, second) =>
            {
                int comparison = first.Real.CompareTo(second.Real);
                return comparison != 0 ? comparison : first.Imaginary.CompareTo(second.Imaginary);
            });
            return roots;
        }

        public static SvdResult SingularValueDecomposition(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            if (rows >= columns)
            {
                return ComputeTallSvd(matrix);
            }
            SvdResult transposed = ComputeTallSvd(Algebra.MatrixTranspose(matrix));
            double[,] u = Algebra.MatrixTranspose(transposed.Vt);
            double[,] vt = Algebra.MatrixTranspose(transposed.U);
            return new SvdResult(u, transposed.SingularValues, vt);
        }

        public static double[,] PseudoInverse(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            SvdResult decomposition = SingularValueDecomposition(matrix);
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] result = new double[columns, rows];
            for (int singular = 0; singular < decomposition.SingularValues.Length; singular++)
            {
                double value = decomposition.SingularValues[singular];
                if (value <= Tolerance)
                {
                    continue;
                }
                double reciprocal = 1.0 / value;
                for (int row = 0; row < columns; row++)
                {
                    for (int column = 0; column < rows; column++)
                    {
                        result[row, column] += decomposition.Vt[singular, row]
                            * reciprocal
                            * decomposition.U[column, singular];
                    }
                }
            }
            return result;
        }

        public static double[,] NullSpace(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] reduced = Algebra.MatrixRowReduce(matrix);
            bool[] isPivot = new bool[columns];
            List<int> pivotColumns = new List<int>();
            int pivotRow = 0;
            for (int column = 0; column < columns && pivotRow < rows; column++)
            {
                if (Math.Abs(reduced[pivotRow, column]) > Tolerance)
                {
                    isPivot[column] = true;
                    pivotColumns.Add(column);
                    pivotRow++;
                }
            }
            List<int> freeColumns = new List<int>();
            for (int column = 0; column < columns; column++)
            {
                if (!isPivot[column])
                {
                    freeColumns.Add(column);
                }
            }
            double[,] result = new double[columns, freeColumns.Count];
            for (int free = 0; free < freeColumns.Count; free++)
            {
                int freeColumn = freeColumns[free];
                result[freeColumn, free] = 1.0;
                for (int pivot = 0; pivot < pivotColumns.Count; pivot++)
                {
                    result[pivotColumns[pivot], free] = -reduced[pivot, freeColumn];
                }
            }
            return result;
        }

        public static double[,] ColumnSpace(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            SvdResult decomposition = SingularValueDecomposition(matrix);
            int rank = 0;
            foreach (double value in decomposition.SingularValues)
            {
                if (value > Tolerance)
                {
                    rank++;
                }
            }
            int rows = decomposition.U.GetLength(0);
            double[,] result = new double[rows, rank];
            for (int column = 0; column < rank; column++)
            {
                for (int row = 0; row < rows; row++)
                {
                    result[row, column] = decomposition.U[row, column];
                }
            }
            return result;
        }

        public static LeastSquaresResult LeastSquares(double[,] matrix, double[] rightHandSide)
        {
            EnsureMatrix(matrix, nameof(matrix));
            if (rightHandSide is null)
            {
                throw new ArgumentNullException(nameof(rightHandSide));
            }
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            if (rows < columns)
            {
                throw new ArgumentException("Least squares requires at least as many rows as columns.", nameof(matrix));
            }
            if (rightHandSide.Length != rows)
            {
                throw new ArgumentException("The right-hand side length must match the matrix row count.", nameof(rightHandSide));
            }
            QrDecompositionResult decomposition = QrDecomposition(matrix);
            double[] transformed = new double[columns];
            for (int column = 0; column < columns; column++)
            {
                double sum = 0.0;
                for (int row = 0; row < rows; row++)
                {
                    sum += decomposition.Q[row, column] * rightHandSide[row];
                }
                transformed[column] = sum;
            }
            double[] solution = SolveUpperTriangular(decomposition.R, columns, transformed);
            double residualSquared = 0.0;
            double[] predictedValues = MatrixVectorMultiply(matrix, solution);
            for (int row = 0; row < rows; row++)
            {
                double difference = rightHandSide[row] - predictedValues[row];
                residualSquared += difference * difference;
            }
            return new LeastSquaresResult(solution, Math.Sqrt(residualSquared));
        }

        public static double MatrixNorm(double[,] matrix, MatrixNormType type = MatrixNormType.Frobenius)
        {
            EnsureMatrix(matrix, nameof(matrix));
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            switch (type)
            {
                case MatrixNormType.Frobenius:
                {
                    double sum = 0.0;
                    for (int row = 0; row < rows; row++)
                    {
                        for (int column = 0; column < columns; column++)
                        {
                            sum += matrix[row, column] * matrix[row, column];
                        }
                    }
                    return Math.Sqrt(sum);
                }
                case MatrixNormType.One:
                {
                    double maximum = 0.0;
                    for (int column = 0; column < columns; column++)
                    {
                        double sum = 0.0;
                        for (int row = 0; row < rows; row++)
                        {
                            sum += Math.Abs(matrix[row, column]);
                        }
                        maximum = Math.Max(maximum, sum);
                    }
                    return maximum;
                }
                case MatrixNormType.Infinity:
                {
                    double maximum = 0.0;
                    for (int row = 0; row < rows; row++)
                    {
                        double sum = 0.0;
                        for (int column = 0; column < columns; column++)
                        {
        /// <summary>
        /// Computes the condition number of matrix.
        /// </summary>
                            sum += Math.Abs(matrix[row, column]);
                        }
                        maximum = Math.Max(maximum, sum);
                    }
                    return maximum;
                }
                case MatrixNormType.Two:
                {
                    SvdResult decomposition = SingularValueDecomposition(matrix);
                    return decomposition.SingularValues.Length == 0 ? 0.0 : decomposition.SingularValues[0];
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(type), "The matrix norm type is not supported.");
        /// <summary>
        /// Computes product of matrix and vector.
        /// </summary>
                }
            }
        }

        public static double ConditionNumber(double[,] matrix)
        {
            EnsureMatrix(matrix, nameof(matrix));
            SvdResult decomposition = SingularValueDecomposition(matrix);
            if (decomposition.SingularValues.Length == 0)
            {
                return double.PositiveInfinity;
            }
            double largest = decomposition.SingularValues[0];
            double smallest = decomposition.SingularValues[decomposition.SingularValues.Length - 1];
            if (largest <= Tolerance || smallest <= Tolerance)
            {
                return double.PositiveInfinity;
            }
            return largest / smallest;
        }

        public static double[] MatrixVectorMultiply(double[,] matrix, double[] vector)
        {
            EnsureMatrix(matrix, nameof(matrix));
            if (vector is null)
            {
                throw new ArgumentNullException(nameof(vector));
            }
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            if (vector.Length != columns)
            {
                throw new ArgumentException("The vector length must match the matrix column count.", nameof(vector));
            }
            double[] result = new double[rows];
            for (int row = 0; row < rows; row++)
            {
                double sum = 0.0;
                for (int column = 0; column < columns; column++)
                {
                    sum += matrix[row, column] * vector[column];
                }
                result[row] = sum;
            }
            return result;
        }

        private static SvdResult ComputeTallSvd(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] gram = Algebra.MatrixMultiply(Algebra.MatrixTranspose(matrix), matrix);
            SymmetricEigenResult eigen = SymmetricEigenDecomposition(gram);
            double[] singularValues = new double[columns];
            for (int index = 0; index < columns; index++)
            {
                singularValues[index] = Math.Sqrt(Math.Max(eigen.Eigenvalues[index], 0.0));
            }
            double[,] u = new double[rows, columns];
            List<double[]> columnsOfU = new List<double[]>(columns);
            for (int column = 0; column < columns; column++)
            {
                double[] candidate = new double[rows];
                bool ready = false;
                if (singularValues[column] > Tolerance)
                {
                    for (int row = 0; row < rows; row++)
                    {
                        double sum = 0.0;
                        for (int index = 0; index < columns; index++)
                        {
                            sum += matrix[row, index] * eigen.Eigenvectors[index, column];
                        }
                        candidate[row] = sum / singularValues[column];
                    }
                    ready = Orthonormalize(candidate, columnsOfU);
                }
                if (!ready)
                {
                    candidate = FindOrthonormalBasisVector(rows, columnsOfU);
                }
                columnsOfU.Add(candidate);
                for (int row = 0; row < rows; row++)
                {
                    u[row, column] = candidate[row];
                }
            }
            return new SvdResult(u, singularValues, Algebra.MatrixTranspose(eigen.Eigenvectors));
        }

        private static bool Orthonormalize(double[] vector, List<double[]> basis)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (double[] existing in basis)
                {
                    double dot = 0.0;
                    for (int index = 0; index < vector.Length; index++)
                    {
                        dot += vector[index] * existing[index];
                    }
                    for (int index = 0; index < vector.Length; index++)
                    {
                        vector[index] -= dot * existing[index];
                    }
                }
            }
            double norm = 0.0;
            for (int index = 0; index < vector.Length; index++)
            {
                norm += vector[index] * vector[index];
            }
            norm = Math.Sqrt(norm);
            if (norm <= Tolerance)
            {
                return false;
            }
            for (int index = 0; index < vector.Length; index++)
            {
                vector[index] /= norm;
            }
            return true;
        }

        private static double[] FindOrthonormalBasisVector(int dimension, List<double[]> basis)
        {
            for (int index = 0; index < dimension; index++)
            {
                double[] candidate = new double[dimension];
                candidate[index] = 1.0;
                if (Orthonormalize(candidate, basis))
                {
                    return candidate;
                }
            }
            throw new InvalidOperationException("Unable to complete the orthonormal basis.");
        }

        private static double[] SolveUpperTriangular(double[,] matrix, int size, double[] rightHandSide)
        {
            double[] result = new double[size];
            for (int row = size - 1; row >= 0; row--)
            {
                double sum = rightHandSide[row];
                for (int column = row + 1; column < size; column++)
                {
                    sum -= matrix[row, column] * result[column];
                }
                if (Math.Abs(matrix[row, row]) <= Tolerance)
                {
                    throw new InvalidOperationException("The upper triangular system is singular.");
                }
                result[row] = sum / matrix[row, row];
            }
            return result;
        }

        private static double[,] CloneMatrix(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            double[,] result = new double[rows, columns];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    result[row, column] = matrix[row, column];
                }
            }
            return result;
        }

        private static void SwapRows(double[,] matrix, int first, int second)
        {
            int columns = matrix.GetLength(1);
            for (int column = 0; column < columns; column++)
            {
                double temporary = matrix[first, column];
                matrix[first, column] = matrix[second, column];
                matrix[second, column] = temporary;
            }
        }

        private static void EnsureMatrix(double[,] matrix, string parameterName)
        {
            if (matrix is null)
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
    }
}
