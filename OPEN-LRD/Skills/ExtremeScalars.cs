using System;
using System.Collections.Generic;

namespace OPEN_LRD.Skills
{
    internal readonly record struct ScaledScalar(double Significand, long BinaryExponent);

    internal sealed record ScalarFieldState(ScaledScalar Field, ScaledScalar FieldVelocity);

    internal sealed record ScalarFieldSolution(
        double[] Time,
        IReadOnlyList<ScalarFieldState> States,
        bool Completed);

    internal static class ExtremeScalars
    {
        private const double LnTwo = 0.693147180559945309417232121458176568;

        /// <summary>
        /// Constructs a ScaledScalar representation from a standard double value.
        /// </summary>
        public static ScaledScalar FromDouble(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be finite.");
            }
            return Normalize(value, 0L);
        }

        public static ScaledScalar FromLogMagnitude(double logMagnitude, int sign = 1)
        {
            if (!double.IsFinite(logMagnitude))
            {
                throw new ArgumentOutOfRangeException(nameof(logMagnitude), "Log magnitude must be finite.");
            }
            if (sign != -1 && sign != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sign), "Sign must be -1 or 1.");
            }
            double exponentValue = Math.Floor(logMagnitude / LnTwo) + 1.0;
            if (exponentValue < long.MinValue || exponentValue > long.MaxValue)
            {
                throw new OverflowException("Log magnitude exceeds the scaled exponent range.");
            }
            long exponent = (long)exponentValue;
            double significand = sign * Math.Exp(logMagnitude - exponent * LnTwo);
            if (!double.IsFinite(significand) || significand == 0.0)
            {
                throw new OverflowException("Log magnitude cannot be normalized.");
            }
            return Normalize(significand, exponent);
        }

        public static double LogMagnitude(ScaledScalar value)
        {
            Validate(value, nameof(value));
            if (value.Significand == 0.0)
            {
                return double.NegativeInfinity;
            }
            return Math.Log(Math.Abs(value.Significand)) + value.BinaryExponent * LnTwo;
        }

        public static double ToDouble(ScaledScalar value)
        {
            Validate(value, nameof(value));
            if (value.Significand == 0.0)
            {
                return 0.0;
            }
            if (value.BinaryExponent > 1024 || value.BinaryExponent < -1074)
            {
                throw new OverflowException("Scaled value is outside the representable range of Double.");
            }
            double result = Math.ScaleB(value.Significand, (int)value.BinaryExponent);
            if (!double.IsFinite(result) || result == 0.0)
            {
                throw new OverflowException("Scaled value overflows or underflows Double.");
            }
            return result;
        }

        public static bool TryToDouble(ScaledScalar value, out double result)
        {
            Validate(value, nameof(value));
            if (value.Significand == 0.0)
            {
                result = 0.0;
                return true;
            }
            if (value.BinaryExponent > 1024 || value.BinaryExponent < -1074)
            {
                result = 0.0;
                return false;
            }
            result = Math.ScaleB(value.Significand, (int)value.BinaryExponent);
            if (!double.IsFinite(result) || result == 0.0)
            {
                result = 0.0;
                return false;
            }
            return true;
        }

        public static ScaledScalar Add(ScaledScalar first, ScaledScalar second)
        {
            Validate(first, nameof(first));
            Validate(second, nameof(second));
            if (first.Significand == 0.0)
            {
                return second;
            }
            if (second.Significand == 0.0)
            {
                return first;
            }
            ScaledScalar larger = first.BinaryExponent >= second.BinaryExponent ? first : second;
            ScaledScalar smaller = first.BinaryExponent >= second.BinaryExponent ? second : first;
            decimal exponentGap = (decimal)larger.BinaryExponent - smaller.BinaryExponent;
            if (exponentGap > 1075m)
            {
                return larger;
            }
            double alignedSmall = Math.ScaleB(smaller.Significand, -(int)exponentGap);
            return Normalize(larger.Significand + alignedSmall, larger.BinaryExponent);
        }

        public static ScaledScalar Subtract(ScaledScalar first, ScaledScalar second)
        {
            Validate(second, nameof(second));
            return Add(first, new ScaledScalar(-second.Significand, second.BinaryExponent));
        }

        public static ScaledScalar Multiply(ScaledScalar first, ScaledScalar second)
        {
            Validate(first, nameof(first));
            Validate(second, nameof(second));
            if (first.Significand == 0.0 || second.Significand == 0.0)
            {
                return default;
            }
            long exponent = checked(first.BinaryExponent + second.BinaryExponent);
            return Normalize(first.Significand * second.Significand, exponent);
        }

        public static ScaledScalar Divide(ScaledScalar numerator, ScaledScalar denominator)
        {
            Validate(numerator, nameof(numerator));
            Validate(denominator, nameof(denominator));
            if (denominator.Significand == 0.0)
            {
                throw new DivideByZeroException();
            }
            if (numerator.Significand == 0.0)
            {
                return default;
            }
            long exponent = checked(numerator.BinaryExponent - denominator.BinaryExponent);
            return Normalize(numerator.Significand / denominator.Significand, exponent);
        }

        public static ScaledScalar Power(ScaledScalar value, int exponent)
        {
            Validate(value, nameof(value));
            if (exponent == 0)
            {
                return FromDouble(1.0);
            }
            if (value.Significand == 0.0)
            {
                if (exponent < 0)
                {
                    throw new DivideByZeroException("Zero cannot be raised to a negative power.");
                }
                return default;
            }
            long power = exponent;
            ScaledScalar factor = exponent < 0 ? Divide(FromDouble(1.0), value) : value;
            ulong count = (ulong)Math.Abs(power);
            ScaledScalar result = FromDouble(1.0);
            while (count > 0)
            {
                if ((count & 1UL) != 0)
                {
                    result = Multiply(result, factor);
                }
                count >>= 1;
                if (count > 0)
                {
                    factor = Multiply(factor, factor);
                }
            }
            return result;
        }
        /// <summary>
        /// Computes the first derivative (dV/d?) of the double-well potential for field, coupling, and vacuumExpectationValue.
        /// </summary>

        public static ScaledScalar Negate(ScaledScalar value)
        {
            Validate(value, nameof(value));
            return new ScaledScalar(-value.Significand, value.BinaryExponent);
        }

        public static ScaledScalar Abs(ScaledScalar value)
        {
            Validate(value, nameof(value));
            return new ScaledScalar(Math.Abs(value.Significand), value.BinaryExponent);
        }

        /// <summary>
        /// Computes scalar field acceleration from field, fieldVelocity, expansionRate, and potentialDerivative.
        /// </summary>
        public static ScaledScalar QuadraticPotential(ScaledScalar field, double massSquared)
        {
            EnsureNonNegative(massSquared, nameof(massSquared));
            return Multiply(FromDouble(0.5 * massSquared), Power(field, 2));
        }

        public static ScaledScalar QuarticPotential(ScaledScalar field, double coupling)
        {
            EnsureNonNegative(coupling, nameof(coupling));
            return Multiply(FromDouble(coupling / 4.0), Power(field, 4));
        }

        public static ScaledScalar DoubleWellPotential(ScaledScalar field, double coupling, double vacuumExpectationValue)
        {
            EnsureNonNegative(coupling, nameof(coupling));
            EnsureFinite(vacuumExpectationValue, nameof(vacuumExpectationValue));
            ScaledScalar offset = Subtract(Power(field, 2), Power(FromDouble(vacuumExpectationValue), 2));
            return Multiply(FromDouble(coupling / 4.0), Power(offset, 2));
        }

        public static ScaledScalar QuadraticPotentialDerivative(ScaledScalar field, double massSquared)
        {
            EnsureNonNegative(massSquared, nameof(massSquared));
            return Multiply(FromDouble(massSquared), field);
        }

        public static ScaledScalar QuarticPotentialDerivative(ScaledScalar field, double coupling)
        {
            EnsureNonNegative(coupling, nameof(coupling));
            return Multiply(FromDouble(coupling), Power(field, 3));
        }

        public static ScaledScalar DoubleWellPotentialDerivative(ScaledScalar field, double coupling, double vacuumExpectationValue)
        {
            EnsureNonNegative(coupling, nameof(coupling));
            EnsureFinite(vacuumExpectationValue, nameof(vacuumExpectationValue));
            ScaledScalar bracket = Subtract(Power(field, 2), Power(FromDouble(vacuumExpectationValue), 2));
            return Multiply(Multiply(FromDouble(coupling), field), bracket);
        }

        public static (ScaledScalar EnergyDensity, ScaledScalar Pressure) CanonicalEnergyDensityAndPressure(
            ScaledScalar fieldVelocity,
            ScaledScalar potential)
        {
            ScaledScalar kinetic = Multiply(FromDouble(0.5), Power(fieldVelocity, 2));
            return (Add(kinetic, potential), Subtract(kinetic, potential));
        }

        public static ScaledScalar ScalarFieldAcceleration(
            ScaledScalar field,
            ScaledScalar fieldVelocity,
            double expansionRate,
            ScaledScalar potentialDerivative)
        {
            Validate(field, nameof(field));
            Validate(fieldVelocity, nameof(fieldVelocity));
            Validate(potentialDerivative, nameof(potentialDerivative));
            EnsureFinite(expansionRate, nameof(expansionRate));
            ScaledScalar friction = Multiply(FromDouble(-3.0 * expansionRate), fieldVelocity);
            return Subtract(friction, potentialDerivative);
        }

        public static ScalarFieldSolution Evolve(
            ScaledScalar initialField,
            ScaledScalar initialVelocity,
            double startTime,
            double endTime,
            double expansionRate,
            Func<ScaledScalar, ScaledScalar> potentialDerivative,
            double tolerance = 1e-7,
            int maxSteps = 10000)
        {
            Validate(initialField, nameof(initialField));
            Validate(initialVelocity, nameof(initialVelocity));
            EnsureFinite(startTime, nameof(startTime));
            EnsureFinite(endTime, nameof(endTime));
            EnsureFinite(expansionRate, nameof(expansionRate));
            if (potentialDerivative == null)
            {
                throw new ArgumentNullException(nameof(potentialDerivative));
            }
            EnsurePositive(tolerance, nameof(tolerance));
            if (endTime <= startTime)
            {
                throw new ArgumentOutOfRangeException(nameof(endTime), "End time must exceed start time.");
            }
            if (maxSteps < 1 || maxSteps > 100000)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSteps), "Maximum steps must be in [1, 100000].");
            }

            ScalarFieldSolution? ordinaryPrecisionSolution = TryEvolveWithNumericalMethods(
                initialField,
                initialVelocity,
                startTime,
                endTime,
                expansionRate,
                potentialDerivative,
                tolerance,
                maxSteps);
            if (ordinaryPrecisionSolution != null)
            {
                return ordinaryPrecisionSolution;
            }

            List<double> times = new List<double> { startTime };
            List<ScalarFieldState> states = new List<ScalarFieldState> { new ScalarFieldState(initialField, initialVelocity) };
            ScalarFieldState state = states[0];
            double time = startTime;
            double step = (endTime - startTime) / Math.Min(100.0, maxSteps);
            for (int attempt = 0; attempt < maxSteps && time < endTime; attempt++)
            {
                step = Math.Min(step, endTime - time);
                if (step <= Math.Max(1e-14, Math.Abs(time) * 1e-14))
                {
                    throw new InvalidOperationException("Scalar-field adaptive step size underflow.");
                }
                ScalarFieldState full = Rk4Step(state, step, expansionRate, potentialDerivative);
                ScalarFieldState half = Rk4Step(state, step * 0.5, expansionRate, potentialDerivative);
                ScalarFieldState refined = Rk4Step(half, step * 0.5, expansionRate, potentialDerivative);
                double error = Math.Max(RelativeDifference(full.Field, refined.Field), RelativeDifference(full.FieldVelocity, refined.FieldVelocity));
                double factor = error == 0.0 ? 2.0 : Math.Clamp(0.9 * Math.Pow(tolerance / error, 0.2), 0.2, 2.0);
                if (error <= tolerance)
                {
                    state = refined;
                    time += step;
                    if (endTime - time <= 1e-14 * (1.0 + Math.Abs(endTime)))
                    {
                        time = endTime;
                    }
                    times.Add(time);
                    states.Add(state);
                }
                step *= factor;
            }
            if (time < endTime)
            {
                throw new InvalidOperationException("Scalar-field evolution exceeded the maximum step count.");
            }
            return new ScalarFieldSolution(times.ToArray(), states.AsReadOnly(), true);
        }

        private static ScalarFieldSolution? TryEvolveWithNumericalMethods(
            ScaledScalar initialField,
            ScaledScalar initialVelocity,
            double startTime,
            double endTime,
            double expansionRate,
            Func<ScaledScalar, ScaledScalar> potentialDerivative,
            double tolerance,
            int maxSteps)
        {
            if (!TryToDouble(initialField, out double field)
                || !TryToDouble(initialVelocity, out double velocity)
                || Math.Abs(field) > 1e100
                || Math.Abs(velocity) > 1e100)
            {
                return null;
            }
            try
            {
                OdeSystemSolution solution = NumericalMethods.RungeKutta45System(
                    (time, values) =>
                    {
                        _ = time;
                        if (!double.IsFinite(values[0]) || !double.IsFinite(values[1])
                            || Math.Abs(values[0]) > 1e100 || Math.Abs(values[1]) > 1e100)
                        {
                            throw new NotRepresentableInDoubleException();
                        }
                        ScaledScalar scaledField = FromDouble(values[0]);
                        ScaledScalar scaledVelocity = FromDouble(values[1]);
                        ScaledScalar derivative = potentialDerivative(scaledField);
                        Validate(derivative, nameof(potentialDerivative));
                        ScaledScalar acceleration = ScalarFieldAcceleration(scaledField, scaledVelocity, expansionRate, derivative);
                        if (!TryToDouble(acceleration, out double accelerationValue))
                        {
                            throw new NotRepresentableInDoubleException();
                        }
                        if (Math.Abs(accelerationValue) > 1e100)
                        {
                            throw new NotRepresentableInDoubleException();
                        }
                        return new[] { values[1], accelerationValue };
                    },
                    startTime,
                    endTime,
                    new[] { field, velocity },
                    tolerance,
                    maxSteps);
                ScalarFieldState[] states = new ScalarFieldState[solution.Y.Length];
                for (int index = 0; index < states.Length; index++)
                {
                    states[index] = new ScalarFieldState(FromDouble(solution.Y[index][0]), FromDouble(solution.Y[index][1]));
                }
                return new ScalarFieldSolution(solution.X, Array.AsReadOnly(states), true);
            }
            catch (NotRepresentableInDoubleException)
            {
                return null;
            }
        }

        private static ScalarFieldState Rk4Step(
            ScalarFieldState state,
            double step,
            double expansionRate,
            Func<ScaledScalar, ScaledScalar> potentialDerivative)
        {
            (ScaledScalar k1Field, ScaledScalar k1Velocity) = Derivative(state, expansionRate, potentialDerivative);
            ScalarFieldState second = AddScaled(state, k1Field, k1Velocity, step * 0.5);
            (ScaledScalar k2Field, ScaledScalar k2Velocity) = Derivative(second, expansionRate, potentialDerivative);
            ScalarFieldState third = AddScaled(state, k2Field, k2Velocity, step * 0.5);
            (ScaledScalar k3Field, ScaledScalar k3Velocity) = Derivative(third, expansionRate, potentialDerivative);
            ScalarFieldState fourth = AddScaled(state, k3Field, k3Velocity, step);
            (ScaledScalar k4Field, ScaledScalar k4Velocity) = Derivative(fourth, expansionRate, potentialDerivative);
            ScaledScalar fieldSlope = Add(
                Add(k1Field, Multiply(FromDouble(2.0), k2Field)),
                Add(Multiply(FromDouble(2.0), k3Field), k4Field));
            ScaledScalar velocitySlope = Add(
                Add(k1Velocity, Multiply(FromDouble(2.0), k2Velocity)),
                Add(Multiply(FromDouble(2.0), k3Velocity), k4Velocity));
            ScaledScalar scale = FromDouble(step / 6.0);
            return new ScalarFieldState(
                Add(state.Field, Multiply(scale, fieldSlope)),
                Add(state.FieldVelocity, Multiply(scale, velocitySlope)));
        }

        private static (ScaledScalar Field, ScaledScalar Velocity) Derivative(
            ScalarFieldState state,
            double expansionRate,
            Func<ScaledScalar, ScaledScalar> potentialDerivative)
        {
            ScaledScalar potentialSlope = potentialDerivative(state.Field);
            Validate(potentialSlope, nameof(potentialDerivative));
            return (state.FieldVelocity,
                ScalarFieldAcceleration(state.Field, state.FieldVelocity, expansionRate, potentialSlope));
        }

        private static ScalarFieldState AddScaled(ScalarFieldState state, ScaledScalar fieldSlope, ScaledScalar velocitySlope, double scale)
        {
            ScaledScalar factor = FromDouble(scale);
            return new ScalarFieldState(
                Add(state.Field, Multiply(factor, fieldSlope)),
                Add(state.FieldVelocity, Multiply(factor, velocitySlope)));
        }

        private static double RelativeDifference(ScaledScalar first, ScaledScalar second)
        {
            ScaledScalar difference = Abs(Subtract(first, second));
            ScaledScalar scale;
            if (first.Significand == 0.0)
            {
                scale = Abs(second);
            }
            else if (second.Significand == 0.0)
            {
                scale = Abs(first);
            }
            else if (first.BinaryExponent != second.BinaryExponent)
            {
                scale = first.BinaryExponent > second.BinaryExponent ? Abs(first) : Abs(second);
            }
            else
            {
                scale = Math.Abs(first.Significand) >= Math.Abs(second.Significand) ? Abs(first) : Abs(second);
            }
            if (scale.Significand == 0.0)
            {
                return 0.0;
            }
            ScaledScalar relative = Divide(difference, scale);
            return TryToDouble(relative, out double result) ? result : 0.0;
        }

        private static ScaledScalar Normalize(double significand, long exponent)
        {
            if (!double.IsFinite(significand))
            {
                throw new OverflowException("Scaled significand must be finite.");
            }
            if (significand == 0.0)
            {
                return default;
            }
            int adjustment = Math.ILogB(Math.Abs(significand)) + 1;
            long normalizedExponent = checked(exponent + adjustment);
            double normalizedSignificand = Math.ScaleB(significand, -adjustment);
            if (Math.Abs(normalizedSignificand) < 0.5 || Math.Abs(normalizedSignificand) >= 1.0)
            {
                throw new InvalidOperationException("Scaled-scalar normalization failed.");
            }
            return new ScaledScalar(normalizedSignificand, normalizedExponent);
        }

        private static void Validate(ScaledScalar value, string name)
        {
            if (!double.IsFinite(value.Significand)
                || (value.Significand != 0.0 && (Math.Abs(value.Significand) < 0.5 || Math.Abs(value.Significand) >= 1.0))
                || (value.Significand == 0.0 && value.BinaryExponent != 0))
            {
                throw new ArgumentException("Scaled scalar is not normalized.", name);
            }
        }

        private static void EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }

        private static void EnsurePositive(double value, string name)
        {
            EnsureFinite(value, name);
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive.");
            }
        }

        private static void EnsureNonNegative(double value, string name)
        {
            EnsureFinite(value, name);
            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be non-negative.");
            }
        }

        private sealed class NotRepresentableInDoubleException : Exception
        {
        }
    }
}
