using System;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class BasicPhysicsTests
    {
        public static void RunAll()
        {
            Run("Basic physics mechanics", TestMechanics);
            Run("Basic physics trajectories and gravity", TestTrajectoriesAndGravity);
            Run("Basic physics validation", TestValidation);
        }

        private static void TestMechanics()
        {
            Vector3 force = BasicPhysics.Force(2.0, new Vector3(0.0, -3.0, 0.0));
            AssertEqual(force.Y, -6.0, "vector F=ma");
            AssertEqual(BasicPhysics.MassFromForce(12.0, 3.0), 4.0, "mass from force");
            AssertEqual(BasicPhysics.Acceleration(12.0, 4.0), 3.0, "scalar acceleration");
            AssertEqual(BasicPhysics.Weight(2.0), 19.6133, "standard weight");
            AssertEqual(BasicPhysics.KineticEnergy(2.0, 3.0), 9.0, "kinetic energy");
            AssertEqual(BasicPhysics.KineticEnergy(2.0, 0.0), 0.0, "a stationary object has zero kinetic energy");
            AssertEqual(BasicPhysics.GravitationalPotentialEnergy(2.0, 10.0, 4.0), 80.0, "gravitational potential energy");
            AssertEqual(BasicPhysics.Work(new Vector3(2.0, 0.0, 0.0), new Vector3(3.0, 0.0, 0.0)), 6.0, "work from dot product");
            AssertEqual(BasicPhysics.Power(12.0, 3.0), 4.0, "power from work");
            AssertEqual(BasicPhysics.Power(new Vector3(2.0, 0.0, 0.0), new Vector3(4.0, 0.0, 0.0)), 8.0, "instantaneous power");
            Vector3 torque = BasicPhysics.Torque(new Vector3(1.0, 0.0, 0.0), new Vector3(0.0, 2.0, 0.0));
            AssertEqual(torque.Z, 2.0, "torque cross product");
            AssertEqual(BasicPhysics.RotationalPower(torque, new Vector3(0.0, 0.0, 3.0)), 6.0, "rotational power");
            AssertEqual(BasicPhysics.Distance(3.0, 4.0), 12.0, "constant-speed distance");
            AssertEqual(BasicPhysics.TravelTime(12.0, 3.0), 4.0, "constant-speed time");
        }

        private static void TestTrajectoriesAndGravity()
        {
            Vector3 position = BasicPhysics.ProjectilePosition(default, new Vector3(2.0, 3.0, 0.0), new Vector3(0.0, -2.0, 0.0), 2.0);
            AssertEqual(position.X, 4.0, "projectile horizontal position");
            AssertEqual(position.Y, 2.0, "projectile vertical position");
            Vector3 velocity = BasicPhysics.VelocityAtTime(new Vector3(2.0, 3.0, 0.0), new Vector3(0.0, -2.0, 0.0), 2.0);
            AssertEqual(velocity.Y, -1.0, "constant-acceleration velocity");
            (double time, double range) = BasicPhysics.FlatGroundImpact(0.0, 10.0, 3.0, 10.0);
            AssertEqual(time, 2.0, "flat-ground impact time");
            AssertEqual(range, 6.0, "flat-ground impact range");
            AssertEqual(BasicPhysics.EscapeVelocity(1.0e20, 1.0e6), Math.Sqrt(2.0 * PhysicalConstants.GravitationalConstant.ValueSI * 1.0e20 / 1.0e6), "escape velocity");
            Vector3 gravity = BasicPhysics.GravitationalForce(1.0e10, 1.0e10, new Vector3(2.0, 0.0, 0.0));
            Vector3 farther = BasicPhysics.GravitationalForce(1.0e10, 1.0e10, new Vector3(4.0, 0.0, 0.0));
            AssertEqual(gravity.X / farther.X, 4.0, "inverse-square force scaling");
            TrajectoryResult result = BasicPhysics.ProjectileTrajectory(default, new Vector3(1.0, 0.0, 0.0), default, new[] { 0.0, 1.0 });
            Assert(result.Samples.Count == 2 && !result.Impacted, "sampled analytic trajectory");
            TrajectoryResult impact = BasicPhysics.ProjectileTrajectory(
                new Vector3(0.0, 2.0, 0.0),
                new Vector3(2.0, 0.0, 0.0),
                new Vector3(0.0, -2.0, 0.0),
                new[] { 0.0, 1.0, 2.0 });
            Assert(impact.Impacted && impact.ImpactTime.HasValue, "trajectory detects flat-ground impact");
            AssertEqual(impact.ImpactTime!.Value, Math.Sqrt(2.0), "analytic impact time");
            AssertEqual(impact.Samples[^1].Position.Y, 0.0, "impact sample lies on ground");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => BasicPhysics.Force(0.0, new Vector3(0.0, 0.0, 0.0)), "mass must be positive");
            AssertThrows<ArgumentOutOfRangeException>(() => BasicPhysics.TravelTime(1.0, 0.0), "travel time rejects zero speed");
            AssertThrows<ArgumentOutOfRangeException>(() => BasicPhysics.GravitationalForce(1.0, 1.0, default), "gravity rejects zero separation");
        }
    }
}
