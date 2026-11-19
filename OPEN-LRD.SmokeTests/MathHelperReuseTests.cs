using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class MathHelperReuseTests
    {
        public static void RunAll()
        {
            Run("Math_1 helper reuse", TestMath1);
            Run("Math_2 helper reuse", TestMath2);
            Run("Math_3 helper reuse", TestMath3);
            Run("Math_4 helper reuse", TestMath4);
        }

        private static void TestMath1()
        {
            List<float> values = new List<float> { 1.0f, 2.0f, 3.0f };
            AssertEqual(Math_1.CalculateAverage(values), 2.0, "average");
            AssertEqual(Math_1.CalculateStandardDeviation(values), Math.Sqrt(2.0 / 3.0), "population standard deviation", 1e-7);
            AssertEqual(Math_1.CalculateGeometricMean(new List<float> { 1.0f, 4.0f }), 2.0, "geometric mean");
            AssertEqual(Math_1.CalculateLinearRegressionPredict(4.0f, values, new List<float> { 2.0f, 4.0f, 6.0f }), 8.0, "linear regression prediction");

            Assert(Math_1.AddRange(new List<int>()) == 0, "empty integer range sum");
            AssertEqual(Math_1.AddRangeDouble(new List<double>()), 0.0, "empty double range sum");
            AssertEqual(Math_1.AddRangeFloat(new List<float>()), 0.0, "empty float range sum");
            AssertEqual(Math_1.AddRangeFloat(values), 6.0, "float range sum");

            List<float> actual = new List<float> { 1.0f, 2.0f, 4.0f };
            List<float> predicted = new List<float> { 2.0f, 3.0f, 5.0f };
            AssertEqual(Math_1.CalculateMeanAbsoluteScaledError(actual, predicted), 2.0 / 3.0, "mean absolute scaled error", 1e-7);
            AssertThrows<ArgumentException>(
                () => Math_1.CalculateMeanAbsoluteScaledError(new List<float> { 1.0f }, new List<float> { 2.0f }),
                "MASE should require at least two values");
            AssertThrows<InvalidOperationException>(
                () => Math_1.CalculateMeanAbsoluteScaledError(new List<float> { 1.0f, 1.0f }, new List<float> { 2.0f, 2.0f }),
                "MASE should reject a zero baseline difference");
        }

        private static void TestMath2()
        {
            AssertEqual(Math_2.CalculateRectangleDiagonal(3.0, 4.0), 5.0, "rectangle diagonal");
            AssertEqual(Math_2.CalculateConeSlantHeight(3.0, 4.0), 5.0, "cone slant height");
            AssertEqual(Math_2.CalculatePointLineDistance(0.0, 2.0, 0.0, 1.0, 0.0), 2.0, "point-to-line distance");
            AssertEqual(Math_2.CalculateDistancePointToPlane(0.0, 0.0, 3.0, 0.0, 0.0, 1.0, 0.0), 3.0, "point-to-plane distance");
            AssertThrows<ArgumentOutOfRangeException>(
                () => Math_2.CalculateRectangleDiagonal(-1.0, 2.0),
                "rectangle diagonal should retain nonnegative validation");
        }

        private static void TestMath3()
        {
            Expression series = Math_3.TaylorSeries("x^2", "x", 1.0, 2);
            AssertEqual(Math_3.Evaluate(series, "x", 2.0), 4.0, "Taylor series value");
            AssertEqual(Math_3.TaylorCoefficient(Math_3.Parse("x^2"), "x", 1.0, 2), 1.0, "Taylor coefficient");
            AssertEqual(Math_3.NewtonRaphson(value => value * value - 2.0, 1.0), Math.Sqrt(2.0), "Newton-Raphson root", 1e-7);
        }

        private static void TestMath4()
        {
            AssertEqual(
                Math_4.DoubleIntegral((x, y) => x + y, 0.0, 1.0, 0.0, 1.0, 10),
                1.0,
                "double integral through Simpson integration");
            AssertEqual(
                Math_4.ArcLengthParametric2D(t => t, _ => 0.0, 0.0, 1.0),
                1.0,
                "2D parametric arc length",
                1e-6);
            AssertEqual(
                Math_4.ArcLength(t => new Vector3(t, 0.0, 0.0), 0.0, 1.0),
                1.0,
                "3D curve arc length",
                1e-5);
            AssertEqual(
                Math_4.SurfaceArea((u, v) => new Vector3(u, v, 0.0), 0.0, 1.0, 0.0, 1.0, 10),
                1.0,
                "parametric surface area",
                1e-5);
        }
    }
}
