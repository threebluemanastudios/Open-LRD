using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal sealed record TrajectorySample(double Time, Vector3 Position, Vector3 Velocity);

    internal sealed record TrajectoryResult(
        IReadOnlyList<TrajectorySample> Samples,
        bool Impacted,
        double? ImpactTime);

    internal static class BasicPhysics
    {
        public static Vector3 Force(double mass, Vector3 acceleration)
        {
            EnsurePositive(mass, nameof(mass));
            EnsureFinite(acceleration, nameof(acceleration));
            return Math_2.ScaleVector(acceleration, mass);
        }

        public static double Force(double mass, double acceleration)
        {
            EnsurePositive(mass, nameof(mass));
            EnsureFinite(acceleration, nameof(acceleration));
            return mass * acceleration;
        }

        public static double MassFromForce(double force, double acceleration)
        {
            EnsureFinite(force, nameof(force));
            EnsureFinite(acceleration, nameof(acceleration));
            if (acceleration == 0.0)
            {
                throw new DivideByZeroException("Mass cannot be inferred from zero acceleration.");
            }
            double result = force / acceleration;
            if (result <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(force), "Mass must be positive.");
            }
            return result;
        }

        public static Vector3 Acceleration(Vector3 force, double mass)
        {
            EnsureFinite(force, nameof(force));
            EnsurePositive(mass, nameof(mass));
            return Math_2.ScaleVector(force, 1.0 / mass);
        }

        public static double Acceleration(double force, double mass)
        {
            EnsureFinite(force, nameof(force));
            EnsurePositive(mass, nameof(mass));
            return force / mass;
        }

        public static Vector3 Weight(double mass, Vector3 gravity) => Force(mass, gravity);

        public static double Weight(double mass)
        {
            EnsurePositive(mass, nameof(mass));
            return mass * PhysicalConstants.StandardGravity.ValueSI;
        }

        public static Vector3 Momentum(double mass, Vector3 velocity)
        {
            EnsurePositive(mass, nameof(mass));
            EnsureFinite(velocity, nameof(velocity));
            return Math_2.ScaleVector(velocity, mass);
        }

        public static Vector3 Impulse(Vector3 force, double time)
        {
            EnsureFinite(force, nameof(force));
            EnsureNonNegative(time, nameof(time));
            return Math_2.ScaleVector(force, time);
        }

        public static double Distance(Vector3 displacement)
        {
            EnsureFinite(displacement, nameof(displacement));
            return Math_2.CalculateVectorMagnitude(displacement);
        }

        public static double Distance(double speed, double time)
        {
            EnsureNonNegative(speed, nameof(speed));
            EnsureNonNegative(time, nameof(time));
            return speed * time;
        }

        public static double TravelTime(double distance, double speed)
        {
            EnsureNonNegative(distance, nameof(distance));
            EnsurePositive(speed, nameof(speed));
            return distance / speed;
        }

        public static double KineticEnergy(double mass, double speed)
        {
            EnsurePositive(mass, nameof(mass));
            EnsureFinite(speed, nameof(speed));
            return 0.5 * mass * speed * speed;
        }

        public static double GravitationalPotentialEnergy(double mass, double gravity, double height)
        {
            EnsurePositive(mass, nameof(mass));
            EnsureNonNegative(gravity, nameof(gravity));
            EnsureFinite(height, nameof(height));
            return mass * gravity * height;
        }

        public static double Work(Vector3 force, Vector3 displacement)
        {
            EnsureFinite(force, nameof(force));
            EnsureFinite(displacement, nameof(displacement));
            return Math_2.CalculateDotProduct(force, displacement);
        }

        public static double Power(double work, double time)
        {
            EnsureFinite(work, nameof(work));
            EnsurePositive(time, nameof(time));
            return work / time;
        }

        public static double Power(Vector3 force, Vector3 velocity)
        {
            EnsureFinite(force, nameof(force));
            EnsureFinite(velocity, nameof(velocity));
            return Math_2.CalculateDotProduct(force, velocity);
        }

        public static Vector3 Torque(Vector3 leverArm, Vector3 force)
        {
            EnsureFinite(leverArm, nameof(leverArm));
            EnsureFinite(force, nameof(force));
            return Math_2.CalculateCrossProduct(leverArm, force);
        }

        public static double RotationalPower(Vector3 torque, Vector3 angularVelocity)
        {
            EnsureFinite(torque, nameof(torque));
            EnsureFinite(angularVelocity, nameof(angularVelocity));
            return Math_2.CalculateDotProduct(torque, angularVelocity);
        }

        public static Vector3 PositionAtTime(Vector3 initialPosition, Vector3 initialVelocity, Vector3 acceleration, double time)
        {
            EnsureFinite(initialPosition, nameof(initialPosition));
            EnsureFinite(initialVelocity, nameof(initialVelocity));
            EnsureFinite(acceleration, nameof(acceleration));
            EnsureNonNegative(time, nameof(time));
            return initialPosition + (initialVelocity * time) + (acceleration * (0.5 * time * time));
        }

        public static Vector3 VelocityAtTime(Vector3 initialVelocity, Vector3 acceleration, double time)
        {
            EnsureFinite(initialVelocity, nameof(initialVelocity));
            EnsureFinite(acceleration, nameof(acceleration));
            EnsureNonNegative(time, nameof(time));
            return initialVelocity + (acceleration * time);
        }

        public static Vector3 GravitationalForce(double firstMass, double secondMass, Vector3 separation)
        {
            EnsurePositive(firstMass, nameof(firstMass));
            EnsurePositive(secondMass, nameof(secondMass));
            EnsureFinite(separation, nameof(separation));
            double radius = Math_2.CalculateVectorMagnitude(separation);
            EnsurePositive(radius, nameof(separation));
            double magnitude = PhysicalConstants.GravitationalConstant.ValueSI * firstMass * secondMass / (radius * radius);
            return Math_2.ScaleVector(separation, magnitude / radius);
        }

        public static double EscapeVelocity(double mass, double radius)
        {
            EnsurePositive(mass, nameof(mass));
            EnsurePositive(radius, nameof(radius));
            return Math.Sqrt(2.0 * PhysicalConstants.GravitationalConstant.ValueSI * mass / radius);
        }

        public static double CircularOrbitSpeed(double centralMass, double radius)
        {
            EnsurePositive(centralMass, nameof(centralMass));
            EnsurePositive(radius, nameof(radius));
            return Math.Sqrt(PhysicalConstants.GravitationalConstant.ValueSI * centralMass / radius);
        }

        public static Vector3 ProjectilePosition(Vector3 initialPosition, Vector3 initialVelocity, Vector3 gravity, double time)
        {
            return PositionAtTime(initialPosition, initialVelocity, gravity, time);
        }

        public static TrajectoryResult ProjectileTrajectory(
            Vector3 initialPosition,
            Vector3 initialVelocity,
            Vector3 gravity,
            IReadOnlyList<double> sampleTimes)
        {
            if (sampleTimes == null)
            {
                throw new ArgumentNullException(nameof(sampleTimes));
            }
            EnsureFinite(initialPosition, nameof(initialPosition));
            EnsureFinite(initialVelocity, nameof(initialVelocity));
            EnsureFinite(gravity, nameof(gravity));
            if (initialPosition.Y < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialPosition), "Projectile must start on or above the y=0 ground plane.");
            }
            double? impactTime = FindFlatGroundImpactTime(initialPosition.Y, initialVelocity.Y, gravity.Y);
            List<TrajectorySample> samples = new List<TrajectorySample>(sampleTimes.Count);
            double previous = -1.0;
            foreach (double time in sampleTimes)
            {
                EnsureNonNegative(time, nameof(sampleTimes));
                if (time < previous)
                {
                    throw new ArgumentException("Sample times must be in nondecreasing order.", nameof(sampleTimes));
                }
                if (impactTime.HasValue && time > impactTime.Value)
                {
                    previous = time;
                    continue;
                }
                samples.Add(new TrajectorySample(
                    time,
                    ProjectilePosition(initialPosition, initialVelocity, gravity, time),
                    VelocityAtTime(initialVelocity, gravity, time)));
                previous = time;
            }
            if (impactTime.HasValue
                && (samples.Count == 0 || Math.Abs(samples[^1].Time - impactTime.Value) > 1e-12 * (1.0 + impactTime.Value)))
            {
                samples.Add(new TrajectorySample(
                    impactTime.Value,
                    ProjectilePosition(initialPosition, initialVelocity, gravity, impactTime.Value),
                    VelocityAtTime(initialVelocity, gravity, impactTime.Value)));
            }
            return new TrajectoryResult(samples.AsReadOnly(), impactTime.HasValue, impactTime);
        }

        public static (double Time, double Range) FlatGroundImpact(double launchHeight, double verticalSpeed, double horizontalSpeed, double gravity)
        {
            EnsureNonNegative(launchHeight, nameof(launchHeight));
            EnsureFinite(verticalSpeed, nameof(verticalSpeed));
            EnsureNonNegative(horizontalSpeed, nameof(horizontalSpeed));
            EnsurePositive(gravity, nameof(gravity));
            double time = FindFlatGroundImpactTime(launchHeight, verticalSpeed, -gravity)
                ?? throw new InvalidOperationException("No descending flat-ground impact exists for these inputs.");
            EnsureFinite(time, nameof(time));
            double range = horizontalSpeed * time;
            EnsureFinite(range, nameof(horizontalSpeed));
            return (time, range);
        }

        private static double? FindFlatGroundImpactTime(double height, double verticalSpeed, double verticalAcceleration)
        {
            if (height == 0.0 && (verticalSpeed < 0.0 || (verticalSpeed == 0.0 && verticalAcceleration < 0.0)))
            {
                return 0.0;
            }
            if (verticalAcceleration == 0.0)
            {
                return verticalSpeed < 0.0 ? -height / verticalSpeed : null;
            }
            double discriminant = verticalSpeed * verticalSpeed - 2.0 * verticalAcceleration * height;
            if (discriminant < 0.0)
            {
                return null;
            }
            double root = Math.Sqrt(discriminant);
            double first = (-verticalSpeed - root) / verticalAcceleration;
            double second = (-verticalSpeed + root) / verticalAcceleration;
            double? impact = null;
            if (first >= 0.0 && verticalSpeed + verticalAcceleration * first <= 0.0)
            {
                impact = first;
            }
            if (second >= 0.0 && verticalSpeed + verticalAcceleration * second <= 0.0
                && (!impact.HasValue || second < impact.Value))
            {
                impact = second;
            }
            return impact;
        }

        private static void EnsureFinite(Vector3 vector, string name)
        {
            if (!double.IsFinite(vector.X) || !double.IsFinite(vector.Y) || !double.IsFinite(vector.Z))
            {
                throw new ArgumentOutOfRangeException(name, "Vector components must be finite.");
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
    }
}
