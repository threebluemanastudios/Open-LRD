using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal sealed record ParticleState(double Mass, double Charge, Vector3 Position, Vector3 Velocity);

    internal sealed record ManyBodySnapshot(double Time, IReadOnlyList<ParticleState> Particles, double TotalEnergy);

    internal sealed record ManyBodySimulationResult(IReadOnlyList<ManyBodySnapshot> Snapshots, bool Completed);

    internal sealed record PairPotential(
        string Name,
        Func<double, ParticleState, ParticleState, double> Energy,
        Func<double, ParticleState, ParticleState, double> RadialDerivative);

    internal static class ManyBodyInteractions
    {
        private const int MaximumParticles = 1024;
        private const int MaximumSteps = 100000;
        private const int MaximumSnapshots = 10000;

        public static PairPotential GravitationalPairPotential()
        {
            return new PairPotential(
                "Newtonian gravity",
                (radius, first, second) => -PhysicalConstants.GravitationalConstant.ValueSI * first.Mass * second.Mass / radius,
                (radius, first, second) => PhysicalConstants.GravitationalConstant.ValueSI * first.Mass * second.Mass / (radius * radius));
        }

        public static PairPotential CoulombPairPotential()
        {
            return new PairPotential(
                "Coulomb",
                (radius, first, second) => CoulombCoefficient * first.Charge * second.Charge / radius,
                (radius, first, second) => -CoulombCoefficient * first.Charge * second.Charge / (radius * radius));
        }

        /// <summary>
        /// Constructs a Lennard-Jones pair potential parameterized by epsilon and sigma.
        /// </summary>
        public static PairPotential LennardJonesPairPotential(double epsilon, double sigma)
        {
            EnsurePositive(epsilon, nameof(epsilon));
            EnsurePositive(sigma, nameof(sigma));
            return new PairPotential(
                "Lennard-Jones",
                (radius, _, _) => LennardJonesEnergy(radius, epsilon, sigma),
        /// <summary>
        /// Computes pair potential energy at separation under potential.
        /// </summary>
                (radius, _, _) => LennardJonesDerivative(radius, epsilon, sigma));
        }

        /// <summary>
        /// Computes pair potential energy at separation under potential.
        /// </summary>
        public static double PairPotentialEnergy(double separation, PairPotential potential)
        {
            ParticleState unit = new ParticleState(1.0, 1.0, default, default);
            return PairPotentialEnergy(unit, unit, separation, potential);
        }

        /// <summary>
        /// Computes interparticle force at separation under potential.
        /// </summary>
        public static double PairPotentialEnergy(ParticleState first, ParticleState second, double separation, PairPotential potential)
        {
            ValidatePotential(potential);
        /// <summary>
        /// Computes interparticle force at separation under potential.
        /// </summary>
            ValidateParticle(first, nameof(first));
            ValidateParticle(second, nameof(second));
            EnsurePositive(separation, nameof(separation));
            return EnsureFinite(potential.Energy(separation, first, second), nameof(potential));
        }

        /// <summary>
        /// Computes total potential energy for particles under potential with softening.
        /// </summary>
        public static double PairForce(double separation, PairPotential potential)
        {
            ParticleState unit = new ParticleState(1.0, 1.0, default, default);
            return PairForce(unit, unit, separation, potential);
        }

        public static double PairForce(ParticleState first, ParticleState second, double separation, PairPotential potential)
        {
            ValidatePotential(potential);
            ValidateParticle(first, nameof(first));
            ValidateParticle(second, nameof(second));
            EnsurePositive(separation, nameof(separation));
            return EnsureFinite(potential.RadialDerivative(separation, first, second), nameof(potential));
        }
        /// <summary>
        /// Computes accelerations for particles under potential with softening.
        /// </summary>

        public static double TotalPotentialEnergy(IReadOnlyList<ParticleState> particles, PairPotential potential, double softening)
        {
            ValidateParticles(particles, nameof(particles));
            ValidatePotential(potential);
            EnsureNonNegative(softening, nameof(softening));
            double total = 0.0;
            for (int first = 0; first < particles.Count; first++)
            {
                for (int second = first + 1; second < particles.Count; second++)
                {
                    double radius = EffectiveSeparation(particles[first].Position, particles[second].Position, softening);
                    total += PairPotentialEnergy(particles[first], particles[second], radius, potential);
                }
            }
            return EnsureFinite(total, nameof(particles));
        }

        public static Vector3[] Accelerations(IReadOnlyList<ParticleState> particles, PairPotential potential, double softening)
        {
            ValidateParticles(particles, nameof(particles));
            ValidatePotential(potential);
            EnsureNonNegative(softening, nameof(softening));
            Vector3[] forces = new Vector3[particles.Count];
            for (int first = 0; first < particles.Count; first++)
            {
                for (int second = first + 1; second < particles.Count; second++)
                {
                    Vector3 separation = particles[second].Position - particles[first].Position;
                    double radius = EffectiveSeparation(particles[first].Position, particles[second].Position, softening);
                    double derivative = PairForce(particles[first], particles[second], radius, potential);
                    Vector3 forceOnFirst = Math_2.ScaleVector(separation, derivative / radius);
                    EnsureFinite(forceOnFirst, nameof(potential));
                    forces[first] = forces[first] + forceOnFirst;
                    forces[second] = forces[second] - forceOnFirst;
                }
            }
            Vector3[] accelerations = new Vector3[particles.Count];
            for (int index = 0; index < particles.Count; index++)
            {
                accelerations[index] = Math_2.ScaleVector(forces[index], 1.0 / particles[index].Mass);
                EnsureFinite(accelerations[index], nameof(particles));
            }
            return accelerations;
        }

        public static Vector3[] GravitationalAccelerations(IReadOnlyList<ParticleState> particles, double softening = 0.0)
        {
            ValidateParticles(particles, nameof(particles));
            EnsureNonNegative(softening, nameof(softening));
            Vector3[] forces = new Vector3[particles.Count];
            for (int first = 0; first < particles.Count; first++)
            {
                for (int second = first + 1; second < particles.Count; second++)
                {
                    Vector3 separation = particles[second].Position - particles[first].Position;
                    Vector3 force = softening == 0.0
                        ? BasicPhysics.GravitationalForce(particles[first].Mass, particles[second].Mass, separation)
                        : SoftenedCentralForce(separation, particles[first].Mass, particles[second].Mass, softening,
                            PhysicalConstants.GravitationalConstant.ValueSI);
                    EnsureFinite(force, nameof(particles));
                    forces[first] = forces[first] + force;
                    forces[second] = forces[second] - force;
                }
            }
            Vector3[] accelerations = new Vector3[particles.Count];
            for (int index = 0; index < particles.Count; index++)
            {
                accelerations[index] = Math_2.ScaleVector(forces[index], 1.0 / particles[index].Mass);
                EnsureFinite(accelerations[index], nameof(particles));
            }
            return accelerations;
        }

        public static Vector3[] CoulombAccelerations(IReadOnlyList<ParticleState> particles, double softening = 0.0)
        {
            ValidateParticles(particles, nameof(particles));
            EnsureNonNegative(softening, nameof(softening));
            Vector3[] forces = new Vector3[particles.Count];
            for (int first = 0; first < particles.Count; first++)
            {
        /// <summary>
        /// Advances particles by timeStep under potential and softening via Velocity Verlet.
        /// </summary>
                for (int second = first + 1; second < particles.Count; second++)
                {
                    Vector3 separation = particles[second].Position - particles[first].Position;
                    Vector3 force = softening == 0.0
                        ? Electromagnetism.CoulombForce(particles[first].Charge, particles[second].Charge, separation)
                        : SoftenedCentralForce(separation, particles[first].Charge, particles[second].Charge, softening,
                            CoulombCoefficient);
                    EnsureFinite(force, nameof(particles));
                    forces[first] = forces[first] + force;
                    forces[second] = forces[second] - force;
                }
            }
            Vector3[] accelerations = new Vector3[particles.Count];
            for (int index = 0; index < particles.Count; index++)
            {
                accelerations[index] = Math_2.ScaleVector(forces[index], 1.0 / particles[index].Mass);
                EnsureFinite(accelerations[index], nameof(particles));
            }
            return accelerations;
        }

        public static ParticleState[] VelocityVerletStep(IReadOnlyList<ParticleState> particles, PairPotential potential, double timeStep, double softening)
        {
        /// <summary>
        /// Simulates many-body dynamics from initialState under potential for steps of timeStep.
        /// </summary>
            ValidateParticles(particles, nameof(particles));
            ValidatePotential(potential);
            EnsurePositive(timeStep, nameof(timeStep));
            EnsureNonNegative(softening, nameof(softening));
            Vector3[] initialAccelerations = Accelerations(particles, potential, softening);
            ParticleState[] intermediate = new ParticleState[particles.Count];
            for (int index = 0; index < particles.Count; index++)
            {
                ParticleState particle = particles[index];
                Vector3 nextPosition = particle.Position + (particle.Velocity * timeStep)
                    + (initialAccelerations[index] * (0.5 * timeStep * timeStep));
                intermediate[index] = particle with { Position = nextPosition };
            }
            Vector3[] nextAccelerations = Accelerations(intermediate, potential, softening);
            ParticleState[] result = new ParticleState[particles.Count];
            for (int index = 0; index < particles.Count; index++)
            {
                Vector3 velocity = particles[index].Velocity
                    + ((initialAccelerations[index] + nextAccelerations[index]) * (0.5 * timeStep));
                result[index] = intermediate[index] with { Velocity = velocity };
            }
            return result;
        }

        public static ManyBodySimulationResult Simulate(
            IReadOnlyList<ParticleState> initialState,
            PairPotential potential,
            double timeStep,
            int steps,
            double outputInterval,
            double softening)
        {
            ValidateParticles(initialState, nameof(initialState));
            ValidatePotential(potential);
            EnsurePositive(timeStep, nameof(timeStep));
            EnsureNonNegative(outputInterval, nameof(outputInterval));
            EnsureNonNegative(softening, nameof(softening));
            if (steps < 0 || steps > MaximumSteps)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), $"Step count must be in [0, {MaximumSteps}].");
            }
            int outputEvery = outputInterval == 0.0 ? 1 : (int)Math.Round(outputInterval / timeStep);
            if (outputEvery < 1 || Math.Abs(outputEvery * timeStep - outputInterval) > 1e-9 * Math.Max(1.0, outputInterval))
            {
                throw new ArgumentException("Output interval must be a positive integer multiple of the time step, or zero for every step.", nameof(outputInterval));
            }
            if (steps / outputEvery + 2 > MaximumSnapshots)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), "Requested output would exceed the snapshot limit.");
            }

            ParticleState[] state = CopyParticles(initialState);
            List<ManyBodySnapshot> snapshots = new List<ManyBodySnapshot>
            {
                CreateSnapshot(0.0, state, potential, softening)
            };
            for (int step = 1; step <= steps; step++)
            {
                state = VelocityVerletStep(state, potential, timeStep, softening);
                if (step % outputEvery == 0 || step == steps)
                {
                    snapshots.Add(CreateSnapshot(step * timeStep, state, potential, softening));
                }
            }
            return new ManyBodySimulationResult(snapshots.AsReadOnly(), true);
        }

        private static double CoulombCoefficient => 1.0 / (4.0 * Math.PI * PhysicalConstants.VacuumPermittivity.ValueSI);

        private static Vector3 SoftenedCentralForce(Vector3 separation, double firstFactor, double secondFactor, double softening, double coupling)
        {
            double radiusSquared = Math_2.CalculateDotProduct(separation, separation) + softening * softening;
            if (radiusSquared == 0.0)
            {
                throw new ArgumentException("Overlapping particles require explicit positive softening.");
            }
            double coefficient = coupling * firstFactor * secondFactor / (radiusSquared * Math.Sqrt(radiusSquared));
            return Math_2.ScaleVector(separation, coefficient);
        }

        private static double LennardJonesEnergy(double radius, double epsilon, double sigma)
        {
            EnsurePositive(radius, nameof(radius));
            double ratio6 = Math.Pow(sigma / radius, 6.0);
            return 4.0 * epsilon * (ratio6 * ratio6 - ratio6);
        }

        private static double LennardJonesDerivative(double radius, double epsilon, double sigma)
        {
            EnsurePositive(radius, nameof(radius));
            double ratio6 = Math.Pow(sigma / radius, 6.0);
            return 24.0 * epsilon * (ratio6 - 2.0 * ratio6 * ratio6) / radius;
        }

        private static double EffectiveSeparation(Vector3 first, Vector3 second, double softening)
        {
            Vector3 difference = second - first;
            double squared = Math_2.CalculateDotProduct(difference, difference) + softening * softening;
            if (squared == 0.0)
            {
                throw new ArgumentException("Overlapping particles require explicit positive softening.");
            }
            double radius = Math.Sqrt(squared);
            return EnsurePositive(radius, nameof(softening));
        }

        private static ManyBodySnapshot CreateSnapshot(double time, ParticleState[] particles, PairPotential potential, double softening)
        {
            double kinetic = 0.0;
            foreach (ParticleState particle in particles)
            {
                double speedSquared = Math_2.CalculateDotProduct(particle.Velocity, particle.Velocity);
                kinetic += 0.5 * particle.Mass * speedSquared;
            }
            double energy = EnsureFinite(kinetic + TotalPotentialEnergy(particles, potential, softening), nameof(particles));
            return new ManyBodySnapshot(time, Array.AsReadOnly((ParticleState[])particles.Clone()), energy);
        }

        private static ParticleState[] CopyParticles(IReadOnlyList<ParticleState> particles)
        {
            ParticleState[] copy = new ParticleState[particles.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = particles[index] with { };
            }
            return copy;
        }

        private static void ValidatePotential(PairPotential potential)
        {
            if (potential == null)
            {
                throw new ArgumentNullException(nameof(potential));
            }
            if (string.IsNullOrWhiteSpace(potential.Name) || potential.Energy == null || potential.RadialDerivative == null)
            {
                throw new ArgumentException("Potential name and delegates are required.", nameof(potential));
            }
        }

        private static void ValidateParticles(IReadOnlyList<ParticleState> particles, string name)
        {
            if (particles == null)
            {
                throw new ArgumentNullException(name);
            }
            if (particles.Count > MaximumParticles)
            {
                throw new ArgumentOutOfRangeException(name, $"Direct pair interactions are limited to {MaximumParticles} particles.");
            }
            for (int index = 0; index < particles.Count; index++)
            {
                ValidateParticle(particles[index], name);
            }
        }

        private static void ValidateParticle(ParticleState particle, string name)
        {
            if (particle == null)
            {
                throw new ArgumentException("Particle state cannot be null.", name);
            }
            EnsurePositive(particle.Mass, nameof(particle.Mass));
            EnsureFinite(particle.Charge, nameof(particle.Charge));
            EnsureFinite(particle.Position, nameof(particle.Position));
            EnsureFinite(particle.Velocity, nameof(particle.Velocity));
        }

        private static double EnsurePositive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
            }
            return value;
        }

        private static void EnsureNonNegative(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be non-negative and finite.");
            }
        }

        private static double EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException($"Non-finite numerical result for {name}.");
            }
            return value;
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
