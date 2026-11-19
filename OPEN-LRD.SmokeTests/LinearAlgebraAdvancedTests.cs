using System;
using Complex = System.Numerics.Complex;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class LinearAlgebraAdvancedTests
    {
        public static void RunAll()
        {
            Run("Symmetry detection", TestSymmetry);
            Run("LU decomposition", TestLuDecomposition);
            Run("QR decomposition", TestQrDecomposition);
            Run("Cholesky decomposition", TestCholeskyDecomposition);
            Run("Symmetric eigen decomposition", TestSymmetricEigenDecomposition);
            Run("General eigenvalues", TestGeneralEigenvalues);
            Run("Square and diagonal SVD", TestSquareSvd);
            Run("Tall and wide SVD", TestRectangularSvd);
            Run("Rank-deficient SVD", TestRankDeficientSvd);
            Run("Pseudoinverse", TestPseudoInverse);
            Run("Nullspace and column space", TestNullAndColumnSpace);
            Run("Least squares", TestLeastSquares);
            Run("Matrix norms and condition number", TestNormsAndCondition);
            Run("Linear algebra validation", TestValidation);
        }

        private static void TestSymmetry()
        {
            Assert(LinearAlgebraAdvanced.IsSymmetric(new[,] { { 2.0, 1.0 }, { 1.0, 2.0 } }), "symmetric matrix should be detected");
            Assert(!LinearAlgebraAdvanced.IsSymmetric(new[,] { { 1.0, 2.0 }, { 3.0, 4.0 } }), "non-symmetric matrix should be rejected");
            Assert(!LinearAlgebraAdvanced.IsSymmetric(new[,] { { 1.0, 2.0 } }), "non-square matrix should not be symmetric");
        }

        private static void TestLuDecomposition()
        {
            double[,] matrix = { { 4.0, 3.0 }, { 6.0, 3.0 } };
            LuDecompositionResult result = LinearAlgebraAdvanced.LuDecomposition(matrix);
            double[,] permuted = new double[2, 2];
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 2; column++)
                {
                    permuted[row, column] = matrix[result.RowPermutation[row], column];
                }
            }
            AssertMatrixEqual(Multiply(result.Lower, result.Upper), permuted, "LU reconstruction");
            Assert(result.Sign == -1, "LU permutation sign");
            AssertThrows<InvalidOperationException>(
                () => LinearAlgebraAdvanced.LuDecomposition(new[,] { { 1.0, 2.0 }, { 2.0, 4.0 } }),
                "singular LU should be rejected");
        }

        private static void TestQrDecomposition()
        {
            double[,] matrix = { { 1.0, 1.0 }, { 1.0, 0.0 }, { 0.0, 1.0 } };
            QrDecompositionResult result = LinearAlgebraAdvanced.QrDecomposition(matrix);
            AssertMatrixEqual(Multiply(result.Q, result.R), matrix, "QR reconstruction");
            AssertMatrixEqual(Multiply(Transpose(result.Q), result.Q), Identity(3), "QR orthogonality");
            AssertEqual(result.R[1, 0], 0.0, "QR lower entry one");
            AssertEqual(result.R[2, 0], 0.0, "QR lower entry two");
        }

        private static void TestCholeskyDecomposition()
        {
            double[,] matrix = { { 4.0, 12.0, -16.0 }, { 12.0, 37.0, -43.0 }, { -16.0, -43.0, 98.0 } };
            CholeskyDecompositionResult result = LinearAlgebraAdvanced.CholeskyDecomposition(matrix);
            AssertMatrixEqual(Multiply(result.Lower, Transpose(result.Lower)), matrix, "Cholesky reconstruction");
            AssertEqual(result.Lower[0, 0], 2.0, "Cholesky first diagonal");
            AssertEqual(result.Lower[1, 1], 1.0, "Cholesky second diagonal");
            AssertEqual(result.Lower[2, 2], 3.0, "Cholesky third diagonal");
            Assert(!LinearAlgebraAdvanced.TryCholeskyDecomposition(new[,] { { 1.0, 2.0 }, { 2.0, 1.0 } }, out _), "indefinite matrix should fail Cholesky");
            AssertThrows<InvalidOperationException>(
                () => LinearAlgebraAdvanced.CholeskyDecomposition(new[,] { { 1.0, 2.0 }, { 2.0, 1.0 } }),
                "indefinite Cholesky should be rejected");
        }

        private static void TestSymmetricEigenDecomposition()
        {
            double[,] matrix = { { 2.0, 1.0 }, { 1.0, 2.0 } };
            SymmetricEigenResult result = LinearAlgebraAdvanced.SymmetricEigenDecomposition(matrix);
            AssertEqual(result.Eigenvalues[0], 3.0, "largest symmetric eigenvalue");
            AssertEqual(result.Eigenvalues[1], 1.0, "smallest symmetric eigenvalue");
            AssertEqual(Math.Abs(result.Eigenvectors[0, 0]), Math.Sqrt(0.5), "symmetric eigenvector", 1e-7);
            AssertMatrixEqual(
                Multiply(matrix, result.Eigenvectors),
                Multiply(result.Eigenvectors, Diagonal(result.Eigenvalues)),
                "symmetric eigen reconstruction");
            AssertMatrixEqual(Multiply(Transpose(result.Eigenvectors), result.Eigenvectors), Identity(2), "symmetric eigenvectors orthogonal");

            double[,] larger = { { 4.0, 1.0, 2.0 }, { 1.0, 3.0, 0.0 }, { 2.0, 0.0, 5.0 } };
            SymmetricEigenResult largerResult = LinearAlgebraAdvanced.SymmetricEigenDecomposition(larger);
            AssertMatrixEqual(
                Multiply(larger, largerResult.Eigenvectors),
                Multiply(largerResult.Eigenvectors, Diagonal(largerResult.Eigenvalues)),
                "three-by-three eigen reconstruction",
                1e-7);
            AssertMatrixEqual(
                Multiply(Transpose(largerResult.Eigenvectors), largerResult.Eigenvectors),
                Identity(3),
                "three-by-three eigenvectors orthogonal",
                1e-7);
        }

        private static void TestGeneralEigenvalues()
        {
            Complex[] imaginary = LinearAlgebraAdvanced.Eigenvalues(new[,] { { 0.0, -1.0 }, { 1.0, 0.0 } });
            Assert(imaginary.Length == 2, "rotation should have two eigenvalues");
            Assert(ContainsRoot(imaginary, Complex.ImaginaryOne), "rotation should contain i");
            Assert(ContainsRoot(imaginary, -Complex.ImaginaryOne), "rotation should contain -i");

            Complex[] symmetric = LinearAlgebraAdvanced.Eigenvalues(new[,] { { 2.0, 1.0 }, { 1.0, 2.0 } });
            Assert(ContainsRoot(symmetric, new Complex(1.0, 0.0)), "symmetric matrix should contain eigenvalue 1");
            Assert(ContainsRoot(symmetric, new Complex(3.0, 0.0)), "symmetric matrix should contain eigenvalue 3");

            Complex[] diagonal = LinearAlgebraAdvanced.Eigenvalues(
                new[,] { { 5.0, 0.0, 0.0 }, { 0.0, 2.0, 0.0 }, { 0.0, 0.0, -1.0 } });
            Assert(ContainsRoot(diagonal, new Complex(5.0, 0.0)), "diagonal matrix should contain eigenvalue 5");
            Assert(ContainsRoot(diagonal, new Complex(2.0, 0.0)), "diagonal matrix should contain eigenvalue 2");
            Assert(ContainsRoot(diagonal, new Complex(-1.0, 0.0)), "diagonal matrix should contain eigenvalue -1");
        }

        private static void TestSquareSvd()
        {
            double[,] matrix = { { 3.0, 0.0 }, { 0.0, 4.0 } };
            SvdResult result = LinearAlgebraAdvanced.SingularValueDecomposition(matrix);
            AssertEqual(result.SingularValues[0], 4.0, "largest singular value");
            AssertEqual(result.SingularValues[1], 3.0, "smallest singular value");
            AssertMatrixEqual(ReconstructSvd(result), matrix, "square SVD reconstruction");
            AssertMatrixEqual(Multiply(Transpose(result.U), result.U), Identity(2), "square SVD U orthogonality");
            AssertMatrixEqual(Multiply(result.Vt, Transpose(result.Vt)), Identity(2), "square SVD V orthogonality");
        }

        private static void TestRectangularSvd()
        {
            double[,] tall = { { 1.0, 2.0 }, { 3.0, 4.0 }, { 5.0, 6.0 } };
            SvdResult tallResult = LinearAlgebraAdvanced.SingularValueDecomposition(tall);
            Assert(tallResult.U.GetLength(0) == 3 && tallResult.U.GetLength(1) == 2, "tall SVD U shape");
            Assert(tallResult.Vt.GetLength(0) == 2 && tallResult.Vt.GetLength(1) == 2, "tall SVD V shape");
            AssertMatrixEqual(ReconstructSvd(tallResult), tall, "tall SVD reconstruction");

            double[,] wide = { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.0 } };
            SvdResult wideResult = LinearAlgebraAdvanced.SingularValueDecomposition(wide);
            Assert(wideResult.U.GetLength(0) == 2 && wideResult.U.GetLength(1) == 2, "wide SVD U shape");
            Assert(wideResult.Vt.GetLength(0) == 2 && wideResult.Vt.GetLength(1) == 3, "wide SVD V shape");
            AssertMatrixEqual(ReconstructSvd(wideResult), wide, "wide SVD reconstruction");
        }

        private static void TestRankDeficientSvd()
        {
            double[,] matrix = { { 1.0, 2.0 }, { 2.0, 4.0 } };
            SvdResult result = LinearAlgebraAdvanced.SingularValueDecomposition(matrix);
            Assert(result.SingularValues.Length == 2, "rank-deficient SVD singular value count");
            AssertEqual(result.SingularValues[1], 0.0, "rank-deficient smallest singular value", 1e-7);
            AssertMatrixEqual(ReconstructSvd(result), matrix, "rank-deficient SVD reconstruction");
        }

        private static void TestPseudoInverse()
        {
            double[,] matrix = { { 1.0, 2.0 }, { 3.0, 4.0 } };
            double[,] pseudo = LinearAlgebraAdvanced.PseudoInverse(matrix);
            AssertMatrixEqual(Multiply(Multiply(matrix, pseudo), matrix), matrix, "pseudoinverse left identity", 1e-7);
            AssertMatrixEqual(Multiply(Multiply(pseudo, matrix), pseudo), pseudo, "pseudoinverse right identity", 1e-7);

            double[,] rankDeficient = { { 1.0, 2.0 }, { 2.0, 4.0 } };
            double[,] deficientPseudo = LinearAlgebraAdvanced.PseudoInverse(rankDeficient);
            AssertMatrixEqual(
                Multiply(Multiply(rankDeficient, deficientPseudo), rankDeficient),
                rankDeficient,
                "rank-deficient pseudoinverse left identity",
                1e-7);
        }

        private static void TestNullAndColumnSpace()
        {
            double[,] matrix = { { 1.0, 2.0 }, { 2.0, 4.0 } };
            double[,] nullSpace = LinearAlgebraAdvanced.NullSpace(matrix);
            Assert(nullSpace.GetLength(0) == 2 && nullSpace.GetLength(1) == 1, "nullspace shape");
            double[,] nullVector = { { nullSpace[0, 0] }, { nullSpace[1, 0] } };
            AssertMatrixEqual(Multiply(matrix, nullVector), new double[2, 1], "nullspace product", 1e-7);

            double[,] columnMatrix = { { 1.0, 0.0 }, { 0.0, 0.0 } };
            double[,] columnSpace = LinearAlgebraAdvanced.ColumnSpace(columnMatrix);
            Assert(columnSpace.GetLength(0) == 2 && columnSpace.GetLength(1) == 1, "column space shape");
            Assert(columnSpace[0, 0] != 0.0, "column space should contain the nonzero column");
        }

        private static void TestLeastSquares()
        {
            double[,] matrix = { { 1.0, 0.0 }, { 1.0, 1.0 }, { 1.0, 2.0 } };
            LeastSquaresResult result = LinearAlgebraAdvanced.LeastSquares(matrix, new[] { 1.0, 3.0, 5.0 });
            AssertEqual(result.Solution[0], 1.0, "least squares intercept");
            AssertEqual(result.Solution[1], 2.0, "least squares slope");
            AssertEqual(result.ResidualNorm, 0.0, "least squares residual", 1e-7);
        }

        private static void TestNormsAndCondition()
        {
            double[,] matrix = { { 1.0, 2.0 }, { 3.0, 4.0 } };
            AssertEqual(LinearAlgebraAdvanced.MatrixNorm(matrix, MatrixNormType.Frobenius), Math.Sqrt(30.0), "Frobenius norm");
            AssertEqual(LinearAlgebraAdvanced.MatrixNorm(matrix, MatrixNormType.One), 6.0, "one norm");
            AssertEqual(LinearAlgebraAdvanced.MatrixNorm(matrix, MatrixNormType.Infinity), 7.0, "infinity norm");
            AssertEqual(LinearAlgebraAdvanced.MatrixNorm(matrix, MatrixNormType.Two), LinearAlgebraAdvanced.SingularValueDecomposition(matrix).SingularValues[0], "two norm");
            AssertEqual(LinearAlgebraAdvanced.ConditionNumber(Identity(2)), 1.0, "identity condition number");
            AssertEqual(
                LinearAlgebraAdvanced.ConditionNumber(new[,] { { 1.0, 0.0 }, { 0.0, 0.0 } }),
                double.PositiveInfinity,
                "singular condition number");

            double[] product = LinearAlgebraAdvanced.MatrixVectorMultiply(matrix, new[] { 1.0, 1.0 });
            AssertEqual(product[0], 3.0, "matrix-vector first value");
            AssertEqual(product[1], 7.0, "matrix-vector second value");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentException>(
                () => LinearAlgebraAdvanced.LuDecomposition(new[,] { { 1.0, 2.0 } }),
                "non-square LU should be rejected");
            AssertThrows<InvalidOperationException>(
                () => LinearAlgebraAdvanced.SymmetricEigenDecomposition(new[,] { { 1.0, 2.0 }, { 3.0, 4.0 } }),
                "non-symmetric eigen decomposition should be rejected");
            AssertThrows<ArgumentException>(
                () => LinearAlgebraAdvanced.LeastSquares(new[,] { { 1.0, 2.0, 3.0 } }, new[] { 1.0 }),
                "underdetermined least squares should be rejected");
            AssertThrows<ArgumentException>(
                () => LinearAlgebraAdvanced.LeastSquares(Identity(2), new[] { 1.0 }),
                "wrong right-hand side should be rejected");
            AssertThrows<ArgumentException>(
                () => LinearAlgebraAdvanced.MatrixVectorMultiply(Identity(2), new[] { 1.0 }),
                "wrong vector length should be rejected");
        }

        private static double[,] Multiply(double[,] left, double[,] right)
        {
            int leftRows = left.GetLength(0);
            int leftColumns = left.GetLength(1);
            int rightRows = right.GetLength(0);
            int rightColumns = right.GetLength(1);
            Assert(leftColumns == rightRows, "matrix dimensions should be compatible");
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

        private static double[,] Transpose(double[,] matrix)
        {
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

        private static double[,] Identity(int size)
        {
            double[,] result = new double[size, size];
            for (int index = 0; index < size; index++)
            {
                result[index, index] = 1.0;
            }
            return result;
        }

        private static double[,] Diagonal(double[] values)
        {
            double[,] result = new double[values.Length, values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index, index] = values[index];
            }
            return result;
        }

        private static double[,] ReconstructSvd(SvdResult result)
        {
            return Multiply(Multiply(result.U, Diagonal(result.SingularValues)), result.Vt);
        }
    }
}
