using System;

namespace OPEN_LRD.PhysicsSim
{
    /// <summary>
    /// Central pair interaction between two bodies. Force is repulsive-positive: a positive value pushes the
    /// bodies apart. Potential satisfies Force = -dU/dr.
    /// </summary>
    public interface IPairLaw
    {
        /// <summary>Human-readable law name, recorded in run summaries.</summary>
        string Name { get; }

        /// <summary>Radial force magnitude at separation r (positive = repulsive).</summary>
        /// <param name="r">Separation, strictly positive.</param>
        /// <param name="m1">Property of the first body (mass or charge).</param>
        /// <param name="m2">Property of the second body (mass or charge).</param>
        double Force(double r, double m1, double m2);

        /// <summary>Pair potential energy at separation r.</summary>
        /// <param name="r">Separation, strictly positive.</param>
        /// <param name="m1">Property of the first body.</param>
        /// <param name="m2">Property of the second body.</param>
        double Potential(double r, double m1, double m2);
    }

    /// <summary>Newtonian gravity: F = -G m1 m2 / r^2 (attractive), U = -G m1 m2 / r.</summary>
    public sealed class NewtonianGravity : IPairLaw
    {
        /// <summary>Creates the law with gravitational constant G (finite, positive).</summary>
        /// <param name="g">Gravitational constant.</param>
        public NewtonianGravity(double g = 1.0)
        {
            if (!double.IsFinite(g) || g <= 0.0) throw new ArgumentOutOfRangeException(nameof(g));
            G = g;
        }

        /// <summary>Gravitational constant.</summary>
        public double G { get; }

        /// <inheritdoc />
        public string Name => "Newtonian gravity";

        /// <inheritdoc />
        public double Force(double r, double m1, double m2) => -G * m1 * m2 / (r * r);

        /// <inheritdoc />
        public double Potential(double r, double m1, double m2) => -G * m1 * m2 / r;
    }

    /// <summary>Coulomb interaction with the two "masses" read as charges: F = k q1 q2 / r^2, U = k q1 q2 / r.</summary>
    public sealed class Coulomb : IPairLaw
    {
        /// <summary>Creates the law with Coulomb constant k (finite, positive).</summary>
        /// <param name="k">Coulomb constant.</param>
        public Coulomb(double k = 1.0)
        {
            if (!double.IsFinite(k) || k <= 0.0) throw new ArgumentOutOfRangeException(nameof(k));
            K = k;
        }

        /// <summary>Coulomb constant.</summary>
        public double K { get; }

        /// <inheritdoc />
        public string Name => "Coulomb";

        /// <inheritdoc />
        public double Force(double r, double m1, double m2) => K * m1 * m2 / (r * r);

        /// <inheritdoc />
        public double Potential(double r, double m1, double m2) => K * m1 * m2 / r;
    }
}
