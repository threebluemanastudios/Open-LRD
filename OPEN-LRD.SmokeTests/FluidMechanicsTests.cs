using System;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class FluidMechanicsTests
    {
        public static void RunAll()
        {
            Run("Fluid mechanics hydrostatics and flow", TestHydrostaticsAndFlow);
            Run("Fluid mechanics validation", TestValidation);
        }

        private static void TestHydrostaticsAndFlow()
        {
            AssertEqual(FluidMechanics.HydrostaticPressure(100000.0, 1000.0, 10.0, 2.0), 120000.0, "hydrostatic pressure");
            AssertEqual(FluidMechanics.BuoyantForce(1000.0, 10.0, 0.02), 200.0, "buoyant force");
            AssertEqual(FluidMechanics.MassFlowRate(2.0, 3.0), 6.0, "mass flow rate");
            AssertEqual(FluidMechanics.ContinuityVelocity(2.0, 3.0, 6.0), 1.0, "continuity velocity");
            AssertEqual(FluidMechanics.BernoulliPressure(1000.0, 2.0, 3.0, 2.0, 0.0, 9.0), 1005.0, "Bernoulli pressure");
            AssertEqual(FluidMechanics.ReynoldsNumber(1000.0, 2.0, 0.5, 1.0), 1000.0, "Reynolds number");
            AssertEqual(FluidMechanics.PoiseuilleFlowRate(1.0, 8.0, 1.0, 1.0), Math.PI, "Poiseuille flow");
            AssertEqual(FluidMechanics.DragForce(1.0, 2.0, 3.0, 4.0), 36.0, "drag force");
            AssertEqual(FluidMechanics.DarcyWeisbachPressureDrop(0.02, 1000.0, 2.0, 10.0, 0.5), 800.0, "Darcy-Weisbach pressure loss");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => FluidMechanics.ReynoldsNumber(1.0, 1.0, 1.0, 0.0), "Reynolds number requires viscosity");
            AssertThrows<ArgumentOutOfRangeException>(() => FluidMechanics.HydrostaticPressure(1.0, 1.0, 1.0, -1.0), "depth must be non-negative");
            AssertThrows<InvalidOperationException>(() => FluidMechanics.BernoulliPressure(1.0, 1000.0, 0.0, 10.0, 0.0, 9.8), "absolute pressure cannot be negative");
        }
    }
}
