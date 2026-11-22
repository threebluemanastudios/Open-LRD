using System;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ThermodynamicsTests
    {
        public static void RunAll()
        {
            Run("Thermodynamics ideal gas and heat", TestGasAndHeat);
            Run("Thermodynamics entropy and efficiency", TestEntropyAndEfficiency);
            Run("Thermodynamics validation", TestValidation);
        }

        private static void TestGasAndHeat()
        {
            double pressure = Thermodynamics.IdealGasPressure(1.0, 0.024, 300.0);
            AssertEqual(Thermodynamics.IdealGasVolume(1.0, pressure, 300.0), 0.024, "ideal gas volume round trip");
            AssertEqual(Thermodynamics.IdealGasTemperature(1.0, pressure, 0.024), 300.0, "ideal gas temperature");
            AssertEqual(Thermodynamics.IdealGasMoles(pressure, 0.024, 300.0), 1.0, "ideal gas mole count");
            AssertEqual(Thermodynamics.HeatTransfer(2.0, 3.0, 4.0), 24.0, "calorimetry");
            AssertEqual(Thermodynamics.LatentHeat(2.0, 5.0), 10.0, "latent heat");
            AssertEqual(Thermodynamics.InternalEnergyChange(10.0, 4.0), 6.0, "first-law sign convention");
            AssertEqual(Thermodynamics.ThermalExpansion(2.0, 0.01, 5.0), 0.1, "linear expansion");
            AssertEqual(Thermodynamics.HeatTransfer(2.0, 3.0, 0.0), 0.0, "zero temperature change transfers no heat");
        }

        private static void TestEntropyAndEfficiency()
        {
            AssertEqual(Thermodynamics.CarnotEfficiency(500.0, 300.0), 0.4, "Carnot efficiency");
            AssertEqual(Thermodynamics.EntropyChange(600.0, 300.0), 2.0, "reservoir entropy");
            AssertEqual(Thermodynamics.IdealGasIsothermalEntropyChange(1.0, 1.0, Math.E), PhysicalConstants.IdealGasConstant.ValueSI, "isothermal entropy");
            AssertEqual(Thermodynamics.IdealGasConstantVolumeEntropyChange(1.0, 1.0, Math.E, 2.0), 2.0, "constant-volume entropy");
            AssertEqual(Thermodynamics.AdiabaticFinalTemperature(300.0, 2.0, 1.0, 1.4), 300.0 * Math.Pow(2.0, 0.4), "adiabatic relation");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => Thermodynamics.IdealGasPressure(1.0, 0.0, 300.0), "ideal gas rejects zero volume");
            AssertThrows<ArgumentOutOfRangeException>(() => Thermodynamics.CarnotEfficiency(300.0, 300.0), "Carnot temperatures must be ordered");
            AssertThrows<ArgumentOutOfRangeException>(() => Thermodynamics.EntropyChange(1.0, 0.0), "entropy temperature must be positive");
            AssertThrows<ArgumentOutOfRangeException>(() => Thermodynamics.IdealGasPressure(1.0, 1.0, -1.0),
                "ideal-gas temperature cannot be below absolute zero");
            AssertThrows<ArgumentOutOfRangeException>(() => Thermodynamics.AdiabaticFinalTemperature(300.0, 1.0, 1.0, 1.0),
                "adiabatic heat-capacity ratio must exceed one even when volumes match");
        }
    }
}
