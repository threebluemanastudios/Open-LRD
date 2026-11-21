using System;
using Complex = System.Numerics.Complex;

namespace OPEN_LRD.SmokeTests
{
    internal static class Program
    {
        private static int passed;
        private static int failed;

        private static void Main()
        {
            CoreIdentityTests.RunAll();
            CoreNodeGraphTests.RunAll();
            CoreDiskStreamingTests.RunAll();
            ToolFileParsingTests.RunAll();
            AlgebraTests.RunAll();
            MathHelperReuseTests.RunAll();
            MathFunctionDispatcherTests.RunAll();
            TimeAndDateTests.RunAll();
            ProbabilityAndStatisticsTests.RunAll();
            NumericalMethodsTests.RunAll();
            OptimizationTests.RunAll();
            DiscreteMathTests.RunAll();
            InformationTheoryAndMLMathTests.RunAll();
            Geometry3DAndGraphicsTests.RunAll();
            TransformsAndSignalsTests.RunAll();
            FinancialMathTests.RunAll();
            UnitsAndMeasurementTests.RunAll();
            BasicPhysicsTests.RunAll();
            ThermodynamicsTests.RunAll();
            FluidMechanicsTests.RunAll();
            ElectromagnetismTests.RunAll();
            CondensedMatterTests.RunAll();
            ManyBodyInteractionsTests.RunAll();
            CurvedSpacetimeTests.RunAll();
            ExtremeScalarsTests.RunAll();
            GraphAlgorithmsTests.RunAll();
            LinearAlgebraAdvancedTests.RunAll();

            Console.WriteLine($"{passed} passed, {failed} failed");
            Environment.ExitCode = failed == 0 ? 0 : 1;
        }

        internal static void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        internal static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        internal static void AssertEqual(double actual, double expected, string message, double tolerance = 1e-8)
        {
            if (double.IsNaN(actual) || double.IsNaN(expected))
            {
                throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
            }
            if (double.IsInfinity(actual) || double.IsInfinity(expected))
            {
                if (actual != expected)
                {
                    throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
                }
                return;
            }
            double scale = 1.0 + Math.Max(Math.Abs(actual), Math.Abs(expected));
            if (Math.Abs(actual - expected) > tolerance * scale)
            {
                throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
            }
        }

        internal static void AssertComplexEqual(Complex actual, Complex expected, string message, double tolerance = 1e-8)
        {
            if (Complex.Abs(actual - expected) > tolerance * (1.0 + Complex.Abs(expected)))
            {
                throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
            }
        }

        internal static void AssertMatrixEqual(double[,] actual, double[,] expected, string message, double tolerance = 1e-8)
        {
            if (actual.GetLength(0) != expected.GetLength(0) || actual.GetLength(1) != expected.GetLength(1))
            {
                throw new InvalidOperationException(
                    $"{message}: expected {expected.GetLength(0)}x{expected.GetLength(1)}, got {actual.GetLength(0)}x{actual.GetLength(1)}");
            }
            double maximum = 0.0;
            for (int row = 0; row < actual.GetLength(0); row++)
            {
                for (int column = 0; column < actual.GetLength(1); column++)
                {
                    maximum = Math.Max(maximum, Math.Abs(actual[row, column] - expected[row, column]));
                }
            }
            if (maximum > tolerance)
            {
                throw new InvalidOperationException($"{message}: maximum difference {maximum}");
            }
        }

        internal static void AssertThrows<T>(Action action, string message)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"{message}: wrong exception {exception.GetType().Name}");
            }
            throw new InvalidOperationException($"{message}: no exception thrown");
        }

        internal static bool ContainsRoot(Complex[] values, Complex expected, double tolerance = 1e-7)
        {
            foreach (Complex value in values)
            {
                if (Complex.Abs(value - expected) <= tolerance * (1.0 + Complex.Abs(expected)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
