using System;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ElectromagnetismTests
    {
        public static void RunAll()
        {
            Run("Electromagnetism fields and circuits", TestFieldsAndCircuits);
            Run("Electromagnetism validation", TestValidation);
        }

        private static void TestFieldsAndCircuits()
        {
            Vector3 force = Electromagnetism.CoulombForce(1e-6, 1e-6, new Vector3(0.1, 0.0, 0.0));
            AssertEqual(force.X, -0.89875517923, "force on first charge points away for like charges", 1e-6);
            Vector3 field = Electromagnetism.ElectricFieldPointCharge(1e-6, new Vector3(0.1, 0.0, 0.0));
            AssertEqual(field.X, 898755.17923, "point-charge field", 1e-6);
            AssertEqual(Electromagnetism.ElectricPotentialPointCharge(1e-6, 0.1), 89875.517923, "point-charge potential", 1e-6);
            AssertEqual(Electromagnetism.OhmsLawCurrent(12.0, 4.0), 3.0, "Ohm's law current");
            AssertEqual(Electromagnetism.OhmsLawVoltage(3.0, 4.0), 12.0, "Ohm's law voltage");
            AssertEqual(Electromagnetism.OhmsLawResistance(12.0, 3.0), 4.0, "Ohm's law resistance");
            AssertEqual(Electromagnetism.CapacitanceCharge(2.0, 3.0), 6.0, "capacitor charge");
            AssertEqual(Electromagnetism.CapacitorEnergy(2.0, 3.0), 9.0, "capacitor energy");
            AssertEqual(Electromagnetism.RcVoltage(0.0, 10.0, 0.0, 2.0, 3.0), 0.0, "RC initial voltage");
            Vector3 lorentz = Electromagnetism.LorentzForce(2.0, default, new Vector3(1.0, 0.0, 0.0), new Vector3(0.0, 0.0, 3.0));
            AssertEqual(lorentz.Y, -6.0, "Lorentz-force direction");
            Vector3 wire = Electromagnetism.MagneticForceOnWire(2.0, new Vector3(1.0, 0.0, 0.0), new Vector3(0.0, 3.0, 0.0));
            AssertEqual(wire.Z, 6.0, "magnetic force on wire");
            AssertEqual(Electromagnetism.MagneticFieldLongStraightWire(2.0, 1.0), PhysicalConstants.VacuumPermeability.ValueSI / Math.PI, "straight-wire field");
            AssertEqual(Electromagnetism.EquivalentSeriesResistance(2.0, 3.0), 5.0, "series resistance");
            AssertEqual(Electromagnetism.EquivalentParallelResistance(2.0, 2.0), 1.0, "parallel resistance");
            AssertEqual(Electromagnetism.EquivalentSeriesCapacitance(2.0, 2.0), 1.0, "series capacitance");
            AssertEqual(Electromagnetism.EquivalentParallelCapacitance(2.0, 3.0), 5.0, "parallel capacitance");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => Electromagnetism.CoulombForce(1.0, 1.0, default), "point-charge separation must be nonzero");
            AssertThrows<ArgumentOutOfRangeException>(() => Electromagnetism.MagneticFieldLongStraightWire(1.0, 0.0), "wire distance must be positive");
            AssertThrows<DivideByZeroException>(() => Electromagnetism.OhmsLawResistance(1.0, 0.0), "resistance cannot be inferred at zero current");
        }
    }
}
