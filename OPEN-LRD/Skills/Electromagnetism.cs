using System;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal static class Electromagnetism
    {
        private static double CoulombConstant => 1.0 / (4.0 * Math.PI * PhysicalConstants.VacuumPermittivity.ValueSI);

        public static Vector3 CoulombForce(double firstCharge, double secondCharge, Vector3 separation)
        {
            EnsureFinite(firstCharge, nameof(firstCharge));
            EnsureFinite(secondCharge, nameof(secondCharge));
            EnsureFinite(separation, nameof(separation));
            double radius = Math_2.CalculateVectorMagnitude(separation);
            EnsurePositive(radius, nameof(separation));
            return Math_2.ScaleVector(separation, -CoulombConstant * firstCharge * secondCharge / (radius * radius * radius));
        }

        public static Vector3 ElectricFieldPointCharge(double charge, Vector3 displacement)
        {
            EnsureFinite(charge, nameof(charge));
            EnsureFinite(displacement, nameof(displacement));
            double radius = Math_2.CalculateVectorMagnitude(displacement);
            EnsurePositive(radius, nameof(displacement));
            return Math_2.ScaleVector(displacement, CoulombConstant * charge / (radius * radius * radius));
        }

        public static double ElectricPotentialPointCharge(double charge, double distance)
        {
            EnsureFinite(charge, nameof(charge));
            EnsurePositive(distance, nameof(distance));
            return CoulombConstant * charge / distance;
        }

        public static double OhmsLawCurrent(double voltage, double resistance)
        {
            EnsureFinite(voltage, nameof(voltage));
            EnsurePositive(resistance, nameof(resistance));
            return voltage / resistance;
        }

        public static double OhmsLawVoltage(double current, double resistance)
        {
            EnsureFinite(current, nameof(current));
            EnsureNonNegative(resistance, nameof(resistance));
            return current * resistance;
        }

        public static double OhmsLawResistance(double voltage, double current)
        {
            EnsureFinite(voltage, nameof(voltage));
            EnsureFinite(current, nameof(current));
            if (current == 0.0)
            {
                throw new DivideByZeroException("Resistance cannot be inferred from zero current.");
            }
            double resistance = voltage / current;
            if (resistance < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(voltage), "Resistance must be non-negative.");
            }
            return resistance;
        }

        public static double CapacitanceCharge(double capacitance, double voltage)
        {
            EnsureNonNegative(capacitance, nameof(capacitance));
            EnsureFinite(voltage, nameof(voltage));
            return capacitance * voltage;
        }

        public static double CapacitorEnergy(double capacitance, double voltage)
        {
            EnsureNonNegative(capacitance, nameof(capacitance));
            EnsureFinite(voltage, nameof(voltage));
            return 0.5 * capacitance * voltage * voltage;
        }

        public static double RcVoltage(double initialVoltage, double finalVoltage, double time, double resistance, double capacitance)
        {
            EnsureFinite(initialVoltage, nameof(initialVoltage));
            EnsureFinite(finalVoltage, nameof(finalVoltage));
            EnsureNonNegative(time, nameof(time));
            EnsurePositive(resistance, nameof(resistance));
            EnsurePositive(capacitance, nameof(capacitance));
            return finalVoltage + (initialVoltage - finalVoltage) * Math.Exp(-time / (resistance * capacitance));
        }

        public static Vector3 LorentzForce(double charge, Vector3 electricField, Vector3 velocity, Vector3 magneticField)
        {
            EnsureFinite(charge, nameof(charge));
            EnsureFinite(electricField, nameof(electricField));
            EnsureFinite(velocity, nameof(velocity));
            EnsureFinite(magneticField, nameof(magneticField));
            Vector3 magneticTerm = Math_2.CalculateCrossProduct(velocity, magneticField);
            return Math_2.ScaleVector(electricField + magneticTerm, charge);
        }

        public static Vector3 MagneticForceOnWire(double current, Vector3 wireDirection, Vector3 magneticField)
        {
            EnsureFinite(current, nameof(current));
            EnsureFinite(wireDirection, nameof(wireDirection));
            EnsureFinite(magneticField, nameof(magneticField));
            return Math_2.ScaleVector(Math_2.CalculateCrossProduct(wireDirection, magneticField), current);
        }

        public static double MagneticFieldLongStraightWire(double current, double distance)
        {
            EnsureFinite(current, nameof(current));
            EnsurePositive(distance, nameof(distance));
            return PhysicalConstants.VacuumPermeability.ValueSI * current / (2.0 * Math.PI * distance);
        }

        public static double EquivalentSeriesResistance(params double[] resistances)
        {
            ValidateComponents(resistances, nameof(resistances));
            double total = 0.0;
            foreach (double resistance in resistances)
            {
                total += resistance;
            }
            return total;
        }

        public static double EquivalentParallelResistance(params double[] resistances)
        {
            ValidateComponents(resistances, nameof(resistances));
            double reciprocalSum = 0.0;
            foreach (double resistance in resistances)
            {
                if (resistance == 0.0)
                {
                    return 0.0;
                }
                reciprocalSum += 1.0 / resistance;
            }
            return 1.0 / reciprocalSum;
        }

        public static double EquivalentParallelCapacitance(params double[] capacitances)
        {
            ValidateComponents(capacitances, nameof(capacitances));
            double total = 0.0;
            foreach (double capacitance in capacitances)
            {
                total += capacitance;
            }
            return total;
        }

        public static double EquivalentSeriesCapacitance(params double[] capacitances)
        {
            ValidateComponents(capacitances, nameof(capacitances));
            double reciprocalSum = 0.0;
            foreach (double capacitance in capacitances)
            {
                if (capacitance == 0.0)
                {
                    return 0.0;
                }
                reciprocalSum += 1.0 / capacitance;
            }
            return 1.0 / reciprocalSum;
        }

        private static void ValidateComponents(double[] components, string name)
        {
            if (components == null)
            {
                throw new ArgumentNullException(name);
            }
            if (components.Length == 0)
            {
                throw new ArgumentException("At least one component is required.", name);
            }
            foreach (double component in components)
            {
                EnsureNonNegative(component, name);
            }
        }

        private static void EnsurePositive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
            }
        }

        private static void EnsureNonNegative(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be non-negative and finite.");
            }
        }

        private static void EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }

        private static void EnsureFinite(Vector3 vector, string name)
        {
            if (!double.IsFinite(vector.X) || !double.IsFinite(vector.Y) || !double.IsFinite(vector.Z))
            {
                throw new ArgumentOutOfRangeException(name, "Vector components must be finite.");
            }
        }
    }
}
