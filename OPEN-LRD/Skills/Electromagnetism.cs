using System;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal static class Electromagnetism
    {
        private static double CoulombConstant => 1.0 / (4.0 * Math.PI * PhysicalConstants.VacuumPermittivity.ValueSI);

        /// <summary>
        /// Computes the electrostatic force vector between two point charges (firstCharge and secondCharge) separated by displacement vector separation using Coulomb's law.
        /// </summary>
        /// <param name="firstCharge">The first charge.</param>
        /// <param name="secondCharge">The second charge.</param>
        /// <param name="separation">The separation.</param>
        /// <returns>The result.</returns>
        public static Vector3 CoulombForce(double firstCharge, double secondCharge, Vector3 separation)
        {
            EnsureFinite(firstCharge, nameof(firstCharge));
            EnsureFinite(secondCharge, nameof(secondCharge));
            EnsureFinite(separation, nameof(separation));
            double radius = Math_2.CalculateVectorMagnitude(separation);
            EnsurePositive(radius, nameof(separation));
            return Math_2.ScaleVector(separation, -CoulombConstant * firstCharge * secondCharge / (radius * radius * radius));
        }

        /// <summary>
        /// Computes the electric field vector (E) at displacement from a point charge.
        /// </summary>
        /// <param name="charge">The charge.</param>
        /// <param name="displacement">The displacement.</param>
        /// <returns>The result.</returns>
        public static Vector3 ElectricFieldPointCharge(double charge, Vector3 displacement)
        {
            EnsureFinite(charge, nameof(charge));
            EnsureFinite(displacement, nameof(displacement));
            double radius = Math_2.CalculateVectorMagnitude(displacement);
            EnsurePositive(radius, nameof(displacement));
            return Math_2.ScaleVector(displacement, CoulombConstant * charge / (radius * radius * radius));
        }

        /// <summary>
        /// Computes the electrostatic scalar potential (V = k_e * q / r) at distance from a point charge.
        /// </summary>
        /// <param name="charge">The charge.</param>
        /// <param name="distance">The distance.</param>
        /// <returns>The result.</returns>
        public static double ElectricPotentialPointCharge(double charge, double distance)
        {
            EnsureFinite(charge, nameof(charge));
            EnsurePositive(distance, nameof(distance));
            return CoulombConstant * charge / distance;
        }

        /// <summary>
        /// Computes electric current (I = V / R) from voltage and non-zero resistance via Ohm's law.
        /// </summary>
        /// <param name="voltage">The voltage.</param>
        /// <param name="resistance">The resistance.</param>
        /// <returns>The result.</returns>
        public static double OhmsLawCurrent(double voltage, double resistance)
        {
            EnsureFinite(voltage, nameof(voltage));
            EnsurePositive(resistance, nameof(resistance));
            return voltage / resistance;
        }

        /// <summary>
        /// Computes electric potential difference / voltage (V = I * R) from current and resistance via Ohm's law.
        /// </summary>
        /// <param name="current">The current.</param>
        /// <param name="resistance">The resistance.</param>
        /// <returns>The result.</returns>
        public static double OhmsLawVoltage(double current, double resistance)
        {
            EnsureFinite(current, nameof(current));
            EnsureNonNegative(resistance, nameof(resistance));
            return current * resistance;
        }

        /// <summary>
        /// Computes electrical resistance (R = V / I) from voltage and non-zero current via Ohm's law.
        /// </summary>
        /// <param name="voltage">The voltage.</param>
        /// <param name="current">The current.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes stored electric charge (Q = C * V) from capacitance and voltage.
        /// </summary>
        /// <param name="capacitance">The capacitance.</param>
        /// <param name="voltage">The voltage.</param>
        /// <returns>The result.</returns>
        public static double CapacitanceCharge(double capacitance, double voltage)
        {
            EnsureNonNegative(capacitance, nameof(capacitance));
            EnsureFinite(voltage, nameof(voltage));
            return capacitance * voltage;
        }

        /// <summary>
        /// Computes electrostatic energy (U = 0.5 * C * V ) stored in a capacitor from capacitance and voltage.
        /// </summary>
        /// <param name="capacitance">The capacitance.</param>
        /// <param name="voltage">The voltage.</param>
        /// <returns>The result.</returns>
        public static double CapacitorEnergy(double capacitance, double voltage)
        {
            EnsureNonNegative(capacitance, nameof(capacitance));
            EnsureFinite(voltage, nameof(voltage));
            return 0.5 * capacitance * voltage * voltage;
        }

        /// <summary>
        /// Computes the transient capacitor voltage (V(t)) in an RC circuit at time transitioning from initialVoltage toward finalVoltage with time constant ? = R * C.
        /// </summary>
        /// <param name="initialVoltage">The initial voltage.</param>
        /// <param name="finalVoltage">The final voltage.</param>
        /// <param name="time">The time.</param>
        /// <param name="resistance">The resistance.</param>
        /// <param name="capacitance">The capacitance.</param>
        /// <returns>The result.</returns>
        public static double RcVoltage(double initialVoltage, double finalVoltage, double time, double resistance, double capacitance)
        {
            EnsureFinite(initialVoltage, nameof(initialVoltage));
            EnsureFinite(finalVoltage, nameof(finalVoltage));
            EnsureNonNegative(time, nameof(time));
            EnsurePositive(resistance, nameof(resistance));
            EnsurePositive(capacitance, nameof(capacitance));
            return finalVoltage + (initialVoltage - finalVoltage) * Math.Exp(-time / (resistance * capacitance));
        }

        /// <summary>
        /// Computes the Lorentz force vector (F = q * (E + v   B)) acting on a moving charge in electricField and magneticField.
        /// </summary>
        /// <param name="charge">The charge.</param>
        /// <param name="electricField">The electric field.</param>
        /// <param name="velocity">The velocity.</param>
        /// <param name="magneticField">The magnetic field.</param>
        /// <returns>The result.</returns>
        public static Vector3 LorentzForce(double charge, Vector3 electricField, Vector3 velocity, Vector3 magneticField)
        {
            EnsureFinite(charge, nameof(charge));
            EnsureFinite(electricField, nameof(electricField));
            EnsureFinite(velocity, nameof(velocity));
            EnsureFinite(magneticField, nameof(magneticField));
            Vector3 magneticTerm = Math_2.CalculateCrossProduct(velocity, magneticField);
            return Math_2.ScaleVector(electricField + magneticTerm, charge);
        }

        /// <summary>
        /// Computes the magnetic force vector (F = I * (L   B)) on a current-carrying conductor segment in magneticField.
        /// </summary>
        /// <param name="current">The current.</param>
        /// <param name="wireDirection">The wire direction.</param>
        /// <param name="magneticField">The magnetic field.</param>
        /// <returns>The result.</returns>
        public static Vector3 MagneticForceOnWire(double current, Vector3 wireDirection, Vector3 magneticField)
        {
            EnsureFinite(current, nameof(current));
            EnsureFinite(wireDirection, nameof(wireDirection));
            EnsureFinite(magneticField, nameof(magneticField));
            return Math_2.ScaleVector(Math_2.CalculateCrossProduct(wireDirection, magneticField), current);
        }

        /// <summary>
        /// Computes the magnetic flux density magnitude (B = ?? * I / (2 * ? * r)) at radial distance from an infinitely long straight wire carrying current.
        /// </summary>
        /// <param name="current">The current.</param>
        /// <param name="distance">The distance.</param>
        /// <returns>The result.</returns>
        public static double MagneticFieldLongStraightWire(double current, double distance)
        {
            EnsureFinite(current, nameof(current));
            EnsurePositive(distance, nameof(distance));
            return PhysicalConstants.VacuumPermeability.ValueSI * current / (2.0 * Math.PI * distance);
        }

        /// <summary>
        /// Computes the total equivalent resistance (R_eq = ? R_i) of resistors connected in series.
        /// </summary>
        /// <param name="resistances">The resistances.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the total equivalent resistance (1 / R_eq = ? (1 / R_i)) of resistors connected in parallel.
        /// </summary>
        /// <param name="resistances">The resistances.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the total equivalent capacitance (C_eq = ? C_i) of capacitors connected in parallel.
        /// </summary>
        /// <param name="capacitances">The capacitances.</param>
        /// <returns>The result.</returns>
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

        /// <summary>
        /// Computes the total equivalent capacitance (1 / C_eq = ? (1 / C_i)) of capacitors connected in series.
        /// </summary>
        /// <param name="capacitances">The capacitances.</param>
        /// <returns>The result.</returns>
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
