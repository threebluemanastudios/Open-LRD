using System;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ExtremeScalarsTests
    {
        public static void RunAll()
        {
            Run("Extreme scalar arithmetic and conversions", TestArithmetic);
            Run("Extreme scalar potentials and field evolution", TestFieldPhysics);
            Run("Extreme scalar validation", TestValidation);
        }

        private static void TestArithmetic()
        {
            ScaledScalar ordinary = ExtremeScalars.FromDouble(-12.5);
            AssertEqual(ExtremeScalars.ToDouble(ordinary), -12.5, "double round trip");
            ScaledScalar large = ExtremeScalars.FromLogMagnitude(1000.0, -1);
            Assert(ExtremeScalars.LogMagnitude(large) > 999.0 && large.Significand < 0.0, "log-magnitude construction");
            Assert(!ExtremeScalars.TryToDouble(large, out _), "out-of-range conversion reports failure");
            AssertThrows<OverflowException>(() => ExtremeScalars.ToDouble(large), "out-of-range conversion throws");
            ScaledScalar product = ExtremeScalars.Multiply(ExtremeScalars.FromDouble(1e200), ExtremeScalars.FromDouble(1e100));
            AssertEqual(ExtremeScalars.LogMagnitude(product), Math.Log(1e300), "scaled multiplication magnitude", 1e-12);
            ScaledScalar cancellation = ExtremeScalars.Subtract(ordinary, ordinary);
            AssertEqual(ExtremeScalars.ToDouble(cancellation), 0.0, "exact cancellation");
            AssertEqual(ExtremeScalars.ToDouble(ExtremeScalars.Power(ExtremeScalars.FromDouble(-2.0), 3)), -8.0, "integer power");
        }

        private static void TestFieldPhysics()
        {
            ScaledScalar field = ExtremeScalars.FromDouble(2.0);
            AssertEqual(ExtremeScalars.ToDouble(ExtremeScalars.QuadraticPotential(field, 3.0)), 6.0, "quadratic potential");
            AssertEqual(ExtremeScalars.ToDouble(ExtremeScalars.QuadraticPotentialDerivative(field, 3.0)), 6.0, "quadratic derivative");
            AssertEqual(ExtremeScalars.ToDouble(ExtremeScalars.QuarticPotential(field, 2.0)), 8.0, "quartic potential");
            AssertEqual(ExtremeScalars.ToDouble(ExtremeScalars.QuarticPotentialDerivative(field, 2.0)), 16.0, "quartic derivative");
            AssertEqual(ExtremeScalars.ToDouble(ExtremeScalars.DoubleWellPotential(field, 2.0, 1.0)), 4.5, "double-well potential");
            AssertEqual(ExtremeScalars.ToDouble(ExtremeScalars.DoubleWellPotentialDerivative(field, 2.0, 1.0)), 12.0, "double-well derivative");
            (ScaledScalar density, ScaledScalar pressure) = ExtremeScalars.CanonicalEnergyDensityAndPressure(
                ExtremeScalars.FromDouble(2.0), ExtremeScalars.FromDouble(3.0));
            AssertEqual(ExtremeScalars.ToDouble(density), 5.0, "canonical energy density");
            AssertEqual(ExtremeScalars.ToDouble(pressure), -1.0, "canonical pressure");

            ScalarFieldSolution solution = ExtremeScalars.Evolve(
                ExtremeScalars.FromDouble(0.0),
                ExtremeScalars.FromDouble(1.0),
                0.0,
                1.0,
                0.0,
                _ => ExtremeScalars.FromDouble(0.0),
                1e-7,
                1000);
            Assert(solution.Completed, "free scalar field completes");
            AssertEqual(ExtremeScalars.ToDouble(solution.States[^1].Field), 1.0, "free-field linear evolution", 1e-6);
            AssertEqual(ExtremeScalars.ToDouble(solution.States[^1].FieldVelocity), 1.0, "free-field velocity conservation", 1e-6);

            ScaledScalar extremeField = ExtremeScalars.FromLogMagnitude(1000.0);
            ScalarFieldSolution scaledSolution = ExtremeScalars.Evolve(
                extremeField,
                default,
                0.0,
                0.1,
                0.0,
                _ => default,
                1e-7,
                100);
            AssertEqual(ExtremeScalars.LogMagnitude(scaledSolution.States[^1].Field), 1000.0, "scaled evolution beyond double range", 1e-12);
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => ExtremeScalars.FromDouble(double.PositiveInfinity), "non-finite double rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => ExtremeScalars.FromLogMagnitude(0.0, 0), "invalid sign rejected");
            AssertThrows<DivideByZeroException>(() => ExtremeScalars.Divide(ExtremeScalars.FromDouble(1.0), default), "scaled divide by zero");
            AssertThrows<DivideByZeroException>(() => ExtremeScalars.Power(default, -1), "zero negative power");
        }
    }
}
