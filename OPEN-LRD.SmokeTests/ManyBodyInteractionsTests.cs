using System;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class ManyBodyInteractionsTests
    {
        public static void RunAll()
        {
            Run("Many-body pair forces and energy", TestPairInteractions);
            Run("Many-body velocity-Verlet simulation", TestSimulation);
            Run("Many-body validation and softening", TestValidation);
        }

        private static void TestPairInteractions()
        {
            PairPotential gravity = ManyBodyInteractions.GravitationalPairPotential();
            ParticleState first = new ParticleState(2.0, 0.0, new Vector3(0.0, 0.0, 0.0), default);
            ParticleState second = new ParticleState(3.0, 0.0, new Vector3(2.0, 0.0, 0.0), default);
            AssertEqual(ManyBodyInteractions.PairPotentialEnergy(first, second, 2.0, gravity), -PhysicalConstants.GravitationalConstant.ValueSI * 3.0, "Newtonian pair energy");
            AssertEqual(ManyBodyInteractions.PairForce(first, second, 2.0, gravity), 1.5 * PhysicalConstants.GravitationalConstant.ValueSI, "radial gravity force");
            Vector3[] accelerations = ManyBodyInteractions.Accelerations(new[] { first, second }, gravity, 0.0);
            AssertEqual(first.Mass * accelerations[0].X + second.Mass * accelerations[1].X, 0.0, "Newton's third-law momentum balance", 1e-8);
            Vector3[] directGravity = ManyBodyInteractions.GravitationalAccelerations(new[] { first, second });
            AssertEqual(directGravity[0].X, accelerations[0].X, "direct N-body helper reuses Newtonian force");
            ParticleState positiveFirst = first with { Charge = 1.0 };
            ParticleState positiveSecond = second with { Charge = 1.0 };
            Vector3[] electrostatic = ManyBodyInteractions.CoulombAccelerations(new[] { positiveFirst, positiveSecond });
            Assert(electrostatic[0].X < 0.0 && electrostatic[1].X > 0.0, "like-charge N-body forces repel");

            PairPotential lennardJones = ManyBodyInteractions.LennardJonesPairPotential(1.0, 1.0);
            AssertEqual(ManyBodyInteractions.PairPotentialEnergy(1.0, lennardJones), 0.0, "Lennard-Jones zero crossing");
            AssertEqual(ManyBodyInteractions.PairPotentialEnergy(Math.Pow(2.0, 1.0 / 6.0), lennardJones), -1.0, "Lennard-Jones well minimum");
        }

        private static void TestSimulation()
        {
            PairPotential orbitPotential = new PairPotential(
                "unit gravitational orbit",
                (radius, _, _) => -1.0 / radius,
                (radius, _, _) => 1.0 / (radius * radius));
            double speed = Math.Sqrt(0.5);
            ParticleState[] orbit =
            {
                new ParticleState(1.0, 0.0, new Vector3(-0.5, 0.0, 0.0), new Vector3(0.0, -speed, 0.0)),
                new ParticleState(1.0, 0.0, new Vector3(0.5, 0.0, 0.0), new Vector3(0.0, speed, 0.0))
            };
            ManyBodySimulationResult result = ManyBodyInteractions.Simulate(orbit, orbitPotential, 0.001, 1000, 0.1, 0.0);
            Assert(result.Completed && result.Snapshots.Count == 11, "simulation completes at requested output intervals");
            AssertEqual(result.Snapshots[0].TotalEnergy, -0.5, "initial orbital energy");
            AssertEqual(result.Snapshots[^1].TotalEnergy, -0.5, "bounded velocity-Verlet energy drift", 1e-5);
            AssertEqual(result.Snapshots[^1].Time, 1.0, "final simulation time");
        }

        private static void TestValidation()
        {
            PairPotential potential = ManyBodyInteractions.GravitationalPairPotential();
            ParticleState[] overlap =
            {
                new ParticleState(1.0, 0.0, default, default),
                new ParticleState(1.0, 0.0, default, default)
            };
            AssertThrows<ArgumentException>(() => ManyBodyInteractions.Accelerations(overlap, potential, 0.0), "singular overlap rejected without softening");
            Vector3[] softened = ManyBodyInteractions.Accelerations(overlap, potential, 1.0);
            AssertEqual(softened[0].X, 0.0, "softened exact overlap has no preferred direction");
            AssertThrows<ArgumentException>(() => ManyBodyInteractions.Simulate(overlap, potential, 0.1, 1, 0.15, 1.0), "output interval must align with fixed steps");
        }
    }
}
