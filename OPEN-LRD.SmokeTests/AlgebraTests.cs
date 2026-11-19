using System;
using System.Linq;
using Complex = System.Numerics.Complex;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class AlgebraTests
    {
        public static void RunAll()
        {
            Run("Polynomial normalization", TestPolynomialNormalization);
            Run("Polynomial arithmetic", TestPolynomialArithmetic);
            Run("Polynomial division and calculus", TestPolynomialDivisionAndCalculus);
            Run("Polynomial roots", TestPolynomialRoots);
            Run("Linear and quadratic equations", TestLinearAndQuadraticEquations);
            Run("Matrix operations", TestMatrixOperations);
            Run("Linear system solving", TestLinearSystemSolving);
            Run("Complex arithmetic", TestComplexArithmetic);
            Run("Quaternion rotation", TestQuaternionRotation);
            Run("Symbolic polynomial parsing", TestSymbolicPolynomialParsing);
        }

        private static void TestPolynomialNormalization()
        {
            double[] normalized = Algebra.NormalizePolynomial(new[] { 1.0, 2.0, 0.0, 0.0 });
            Assert(normalized.SequenceEqual(new[] { 1.0, 2.0 }), "trailing zero coefficients should be removed");
            Assert(Algebra.PolynomialDegree(new[] { 0.0 }) == -1, "the zero polynomial should have degree -1");
            AssertEqual(Algebra.PolynomialDegree(new[] { 1.0, 2.0, 3.0 }), 2.0, "polynomial degree");
            AssertEqual(Algebra.PolynomialLeadingCoefficient(new[] { 1.0, 2.0, 3.0 }), 3.0, "leading coefficient");
            AssertEqual(Algebra.PolynomialEvaluate(new[] { 1.0, 2.0, 3.0 }, 2.0), 17.0, "polynomial evaluation");
        }

        private static void TestPolynomialArithmetic()
        {
            Assert(
                Algebra.AddPolynomials(new[] { 1.0, 2.0, 3.0 }, new[] { 4.0, 5.0 }).SequenceEqual(new[] { 5.0, 7.0, 3.0 }),
                "polynomial addition");
            Assert(
                Algebra.SubtractPolynomials(new[] { 1.0, 2.0, 3.0 }, new[] { 4.0, 5.0 }).SequenceEqual(new[] { -3.0, -3.0, 3.0 }),
                "polynomial subtraction");
            Assert(
                Algebra.MultiplyPolynomials(new[] { 1.0, 1.0 }, new[] { 1.0, -1.0 }).SequenceEqual(new[] { 1.0, 0.0, -1.0 }),
                "polynomial multiplication");
            Assert(
                Algebra.ScalePolynomial(new[] { 1.0, 2.0 }, 3.0).SequenceEqual(new[] { 3.0, 6.0 }),
                "polynomial scaling");
            Assert(
                Algebra.ComposePolynomials(new[] { 1.0, 0.0, 1.0 }, new[] { 1.0, 1.0 }).SequenceEqual(new[] { 2.0, 2.0, 1.0 }),
                "polynomial composition");
        }

        private static void TestPolynomialDivisionAndCalculus()
        {
            PolynomialDivisionResult division = Algebra.DividePolynomials(new[] { -1.0, 0.0, 1.0 }, new[] { -1.0, 1.0 });
            Assert(division.Quotient.SequenceEqual(new[] { 1.0, 1.0 }), "polynomial quotient");
            Assert(division.Remainder.SequenceEqual(new[] { 0.0 }), "polynomial remainder");
            Assert(Algebra.PolynomialDerivative(new[] { 1.0, 2.0, 3.0 }).SequenceEqual(new[] { 2.0, 6.0 }), "polynomial derivative");
            Assert(Algebra.PolynomialIntegral(new[] { 1.0, 2.0, 3.0 }).SequenceEqual(new[] { 0.0, 1.0, 1.0, 1.0 }), "polynomial integral");
        }

        private static void TestPolynomialRoots()
        {
            double[] realRoots = Algebra.SolvePolynomialReal(new[] { -6.0, 11.0, -6.0, 1.0 });
            Assert(realRoots.Length == 3, "cubic should have three real roots");
            AssertEqual(realRoots[0], 1.0, "first cubic root");
            AssertEqual(realRoots[1], 2.0, "second cubic root");
            AssertEqual(realRoots[2], 3.0, "third cubic root");

            Complex[] complexRoots = Algebra.SolvePolynomial(new[] { 1.0, 0.0, 1.0 });
            Assert(complexRoots.Length == 2, "quadratic should have two complex roots");
            Assert(ContainsRoot(complexRoots, Complex.ImaginaryOne), "missing i root");
            Assert(ContainsRoot(complexRoots, -Complex.ImaginaryOne), "missing -i root");

            double[] rootsWithRepeatedZero = Algebra.FindRationalRoots(new[] { 0.0, 0.0, -1.0, 1.0 });
            Assert(rootsWithRepeatedZero.Length == 2, "zero-root factors should be removed without duplicating the root");
            AssertEqual(rootsWithRepeatedZero[0], 0.0, "zero rational root");
            AssertEqual(rootsWithRepeatedZero[1], 1.0, "nonzero rational root after zero factors");
        }

        private static void TestLinearAndQuadraticEquations()
        {
            LinearSolution unique = Algebra.SolveLinear(2.0, -4.0);
            Assert(unique.Kind == LinearSolutionKind.Unique, "linear equation should have one solution");
            AssertEqual(unique.Value, 2.0, "linear equation solution");
            Assert(Algebra.SolveLinear(0.0, 0.0).Kind == LinearSolutionKind.Infinite, "zero equation should be infinite");
            Assert(Algebra.SolveLinear(0.0, 1.0).Kind == LinearSolutionKind.NoSolution, "constant equation should have no solution");

            double[] roots = Algebra.SolveQuadraticReal(1.0, -3.0, 2.0);
            Assert(roots.Length == 2, "quadratic should have two real roots");
            AssertEqual(roots[0], 1.0, "first quadratic root");
            AssertEqual(roots[1], 2.0, "second quadratic root");
            Assert(Algebra.SolveQuadraticReal(1.0, 0.0, 1.0).Length == 0, "negative discriminant should have no real roots");

            Complex[] complexRoots = Algebra.SolveQuadraticComplex(1.0, 0.0, 1.0);
            Assert(ContainsRoot(complexRoots, Complex.ImaginaryOne), "quadratic should contain i");
            Assert(ContainsRoot(complexRoots, -Complex.ImaginaryOne), "quadratic should contain -i");

            (double X, double Y) vertex = Algebra.CalculateQuadraticVertex(1.0, -2.0, 3.0);
            AssertEqual(vertex.X, 1.0, "quadratic vertex x");
            AssertEqual(vertex.Y, 2.0, "quadratic vertex y");
        }

        private static void TestMatrixOperations()
        {
            double[,] matrix = { { 1.0, 2.0 }, { 3.0, 4.0 } };
            AssertEqual(Algebra.MatrixTrace(matrix), 5.0, "matrix trace");
            AssertEqual(Algebra.MatrixDeterminant(matrix), -2.0, "matrix determinant");
            AssertMatrixEqual(
                Algebra.MatrixMultiply(matrix, Algebra.MatrixInverse(matrix)),
                Algebra.MatrixIdentity(2),
                "matrix inverse");

            double[,] transpose = Algebra.MatrixTranspose(new[,] { { 1.0, 2.0, 3.0 }, { 4.0, 5.0, 6.0 } });
            Assert(transpose.GetLength(0) == 3 && transpose.GetLength(1) == 2, "matrix transpose shape");
            AssertEqual(transpose[2, 1], 6.0, "matrix transpose value");
            AssertEqual(Algebra.MatrixRank(new[,] { { 1.0, 2.0 }, { 2.0, 4.0 } }), 1.0, "matrix rank");
        }

        private static void TestLinearSystemSolving()
        {
            double[,] matrix = { { 2.0, 1.0 }, { 1.0, 3.0 } };
            double[] solution = Algebra.SolveLinearSystem(matrix, new[] { 5.0, 10.0 });
            AssertEqual(solution[0], 1.0, "linear system first value");
            AssertEqual(solution[1], 3.0, "linear system second value");
            Assert(
                Algebra.TrySolveLinearSystem(matrix, new[] { 5.0, 10.0 }, out double[] solved),
                "linear system should be solvable");
            AssertEqual(solved[0], 1.0, "try-solve first value");
            AssertThrows<InvalidOperationException>(
                () => Algebra.SolveLinearSystem(new[,] { { 1.0, 2.0 }, { 2.0, 4.0 } }, new[] { 1.0, 2.0 }),
                "singular system should be rejected");
        }

        private static void TestComplexArithmetic()
        {
            AssertComplexEqual(
                Algebra.MultiplyComplex(new Complex(1.0, 2.0), new Complex(3.0, -1.0)),
                new Complex(5.0, 5.0),
                "complex multiplication");
            AssertComplexEqual(
                Algebra.DivideComplex(new Complex(1.0, 1.0), new Complex(1.0, -1.0)),
                Complex.ImaginaryOne,
                "complex division");
            AssertComplexEqual(Algebra.ComplexModulus(new Complex(3.0, 4.0)), new Complex(5.0, 0.0), "complex modulus");

            Complex[] roots = Algebra.ComplexRootsOfUnity(4);
            Assert(roots.Length == 4, "fourth roots should have four values");
            Assert(ContainsRoot(roots, Complex.ImaginaryOne), "missing fourth root i");
            Assert(ContainsRoot(roots, -Complex.One), "missing fourth root -1");
        }

        private static void TestQuaternionRotation()
        {
            Quaternion rotation = Algebra.QuaternionFromAxisAngle(0.0, 0.0, 1.0, Math.PI / 2.0);
            Vector3 rotated = Algebra.RotateVector(rotation, new Vector3(1.0, 0.0, 0.0));
            AssertEqual(rotated.X, 0.0, "rotated x", 1e-7);
            AssertEqual(rotated.Y, 1.0, "rotated y", 1e-7);
            AssertEqual(rotated.Z, 0.0, "rotated z", 1e-7);

            Algebra.QuaternionToAxisAngle(rotation, out Vector3 axis, out double angle);
            AssertEqual(axis.Z, 1.0, "rotation axis z");
            AssertEqual(angle, Math.PI / 2.0, "rotation angle");
        }

        private static void TestSymbolicPolynomialParsing()
        {
            Assert(Algebra.TryGetPolynomial("x^2 - 1", "x", out double[] coefficients), "expression should parse as a polynomial");
            Assert(coefficients.SequenceEqual(new[] { -1.0, 0.0, 1.0 }), "parsed polynomial coefficients");
            AssertEqual(Algebra.PolynomialEvaluate(coefficients, 2.0), 3.0, "parsed polynomial evaluation");
        }
    }
}
