using System;
using System.Collections.Generic;

namespace OPEN_LRD.PhysicsSim
{
    /// <summary>Diagnostics recorded at one step.</summary>
    /// <param name="Step">Step index after the update (1-based count of completed steps).</param>
    /// <param name="Time">Simulation time after the update.</param>
    /// <param name="Kinetic">Kinetic energy.</param>
    /// <param name="Potential">Pair potential energy.</param>
    /// <param name="Total">Kinetic plus potential energy.</param>
    /// <param name="Momentum">Total linear momentum.</param>
    public readonly record struct NBodyDiagnostics(long Step, double Time, double Kinetic, double Potential, double Total, VecN Momentum);

    /// <summary>
    /// N-body system in N dimensions. Pairwise forces come from an <see cref="IPairLaw"/>, with optional
    /// Plummer-style softening and an optional cutoff. Integrated with velocity Verlet.
    /// </summary>
    public sealed class NBodySystem
    {
        private readonly List<double> masses = new List<double>();
        private readonly List<VecN> positions = new List<VecN>();
        private readonly List<VecN> velocities = new List<VecN>();
        private readonly IPairLaw law;
        private readonly double softening;
        private readonly double cutoff;
        private VecN[] accelerations = Array.Empty<VecN>();
        private bool accelerationsValid;
        private long stepCount;
        private double time;

        /// <summary>Creates an empty system.</summary>
        /// <param name="dimension">Spatial dimension, at least 1.</param>
        /// <param name="law">Pair law. Must not be null.</param>
        /// <param name="softening">Softening length eps. Pair distance is sqrt(r^2 + eps^2). Non-negative, finite.</param>
        /// <param name="cutoff">Optional cutoff radius. Pairs with r greater than this contribute nothing. Null for no cutoff.</param>
        public NBodySystem(int dimension, IPairLaw law, double softening = 0.0, double? cutoff = null)
        {
            if (dimension < 1) throw new ArgumentOutOfRangeException(nameof(dimension));
            Dimension = dimension;
            this.law = law ?? throw new ArgumentNullException(nameof(law));
            if (!double.IsFinite(softening) || softening < 0.0) throw new ArgumentOutOfRangeException(nameof(softening));
            if (cutoff.HasValue && (!double.IsFinite(cutoff.Value) || cutoff.Value <= 0.0)) throw new ArgumentOutOfRangeException(nameof(cutoff));
            this.softening = softening;
            this.cutoff = cutoff ?? double.PositiveInfinity;
        }

        /// <summary>Spatial dimension.</summary>
        public int Dimension { get; }

        /// <summary>Number of particles.</summary>
        public int Count => masses.Count;

        /// <summary>Current simulation time.</summary>
        public double Time => time;

        /// <summary>Number of completed steps.</summary>
        public long StepCount => stepCount;

        /// <summary>Adds a particle. Mass must be finite and positive; vectors must match the system dimension.</summary>
        /// <param name="mass">Particle mass.</param>
        /// <param name="position">Initial position. Copied.</param>
        /// <param name="velocity">Initial velocity. Copied.</param>
        /// <returns>The index of the new particle.</returns>
        public int AddParticle(double mass, VecN position, VecN velocity)
        {
            if (!double.IsFinite(mass) || mass <= 0.0) throw new ArgumentOutOfRangeException(nameof(mass), "Mass must be finite and positive.");
            if (position is null) throw new ArgumentNullException(nameof(position));
            if (velocity is null) throw new ArgumentNullException(nameof(velocity));
            if (position.Dimension != Dimension || velocity.Dimension != Dimension)
            {
                throw new ArgumentException($"Particle vectors must have dimension {Dimension}.");
            }
            if (!position.IsFinite() || !velocity.IsFinite()) throw new ArgumentException("Particle state must be finite.");

            masses.Add(mass);
            positions.Add(position.Clone());
            velocities.Add(velocity.Clone());
            accelerationsValid = false;
            return masses.Count - 1;
        }

        /// <summary>Returns a copy of the position of particle <paramref name="index"/>.</summary>
        public VecN GetPosition(int index) => positions[index].Clone();

        /// <summary>Returns a copy of the velocity of particle <paramref name="index"/>.</summary>
        public VecN GetVelocity(int index) => velocities[index].Clone();

        /// <summary>Advances the system by one velocity-Verlet step of size dt.</summary>
        /// <param name="dt">Time step, finite and positive.</param>
        public void Step(double dt)
        {
            if (!double.IsFinite(dt) || dt <= 0.0) throw new ArgumentOutOfRangeException(nameof(dt), "Time step must be finite and positive.");
            if (Count == 0)
            {
                time += dt;
                stepCount++;
                return;
            }

            VecN[] a = EnsureAccelerations();
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                positions[i].AddScaledInPlace(dt, velocities[i]);
                positions[i].AddScaledInPlace(0.5 * dt * dt, a[i]);
            }

            VecN[] aNew = ComputeAccelerations();
            for (int i = 0; i < n; i++)
            {
                velocities[i].AddScaledInPlace(0.5 * dt, a[i]);
                velocities[i].AddScaledInPlace(0.5 * dt, aNew[i]);
            }
            accelerations = aNew;
            accelerationsValid = true;
            stepCount++;
            time += dt;
        }

        /// <summary>Runs <paramref name="steps"/> steps and records diagnostics every <paramref name="diagnosticsEvery"/> steps and at the end.</summary>
        /// <param name="dt">Time step.</param>
        /// <param name="steps">Number of steps, non-negative.</param>
        /// <param name="diagnosticsEvery">Recording interval, positive.</param>
        /// <returns>The recorded diagnostics in step order.</returns>
        public List<NBodyDiagnostics> Simulate(double dt, int steps, int diagnosticsEvery)
        {
            if (steps < 0) throw new ArgumentOutOfRangeException(nameof(steps));
            if (diagnosticsEvery <= 0) throw new ArgumentOutOfRangeException(nameof(diagnosticsEvery));
            var record = new List<NBodyDiagnostics> { Diagnostics() };
            for (int s = 1; s <= steps; s++)
            {
                Step(dt);
                if (s % diagnosticsEvery == 0 || s == steps)
                {
                    record.Add(Diagnostics());
                }
            }
            return record;
        }

        /// <summary>Kinetic energy sum 1/2 m v^2.</summary>
        public double KineticEnergy()
        {
            double k = 0.0;
            for (int i = 0; i < Count; i++)
            {
                k += 0.5 * masses[i] * velocities[i].NormSquared();
            }
            return k;
        }

        /// <summary>Pair potential energy, summed over unordered pairs inside the cutoff.</summary>
        public double PotentialEnergy()
        {
            double u = 0.0;
            for (int i = 0; i < Count; i++)
            {
                for (int j = i + 1; j < Count; j++)
                {
                    double r = (positions[i] - positions[j]).Norm();
                    if (r > cutoff) continue;
                    u += law.Potential(softenedDistance(r), masses[i], masses[j]);
                }
            }
            return u;
        }

        /// <summary>Total linear momentum sum m v.</summary>
        public VecN Momentum()
        {
            VecN p = new VecN(Dimension);
            for (int i = 0; i < Count; i++)
            {
                p.AddScaledInPlace(masses[i], velocities[i]);
            }
            return p;
        }

        /// <summary>Returns the diagnostics at the current state.</summary>
        public NBodyDiagnostics Diagnostics()
        {
            double k = KineticEnergy();
            double u = PotentialEnergy();
            return new NBodyDiagnostics(stepCount, time, k, u, k + u, Momentum());
        }

        private double softenedDistance(double r) => Math.Sqrt(r * r + softening * softening);

        private VecN[] EnsureAccelerations()
        {
            if (!accelerationsValid)
            {
                accelerations = ComputeAccelerations();
                accelerationsValid = true;
            }
            return accelerations;
        }

        private VecN[] ComputeAccelerations()
        {
            int n = Count;
            VecN[] a = new VecN[n];
            for (int i = 0; i < n; i++) a[i] = new VecN(Dimension);

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    VecN delta = positions[i] - positions[j];
                    double r = delta.Norm();
                    if (r > cutoff) continue;
                    if (r == 0.0 && softening == 0.0)
                    {
                        throw new InvalidOperationException($"Particles {i} and {j} overlap with zero softening.");
                    }
                    double rs = softenedDistance(r);
                    if (rs == 0.0) continue;
                    double f = law.Force(rs, masses[i], masses[j]);
                    // Force on i is f * delta / rs (repulsive when f > 0). Force on j is the opposite.
                    double scale = f / rs;
                    a[i].AddScaledInPlace(scale / masses[i], delta);
                    a[j].AddScaledInPlace(-scale / masses[j], delta);
                }
            }
            return a;
        }
    }
}
