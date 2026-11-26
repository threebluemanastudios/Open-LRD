using System;
using System.Collections.Generic;
using System.Numerics;
using OPEN_LRD.PhysicsSim;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class PhysicsSimTests
    {
        public static void RunAll()
        {
            Run("VecN arithmetic and dimension checks", TestVecN);
            Run("VecN rejects non-finite and mismatched input", TestVecNValidation);
            Run("Velocity Verlet order and oscillator energy", TestVelocityVerlet);
            Run("RK4 order on exponential decay", TestRk4Order);
            Run("Fixed-step loop timing", TestFixedStepLoop);
            Run("Conjugate gradient solves SPD system", TestConjugateGradient);
            Run("Conjugate gradient reports non-convergence", TestConjugateGradientCap);
            Run("FFT matches naive DFT (radix-2 and Bluestein)", TestFftAgainstDft);
            Run("FFT round-trip for power-of-two and other lengths", TestFftRoundTrip);
            Run("FFT validation", TestFftValidation);
            Run("N-body two-body orbit period", TestTwoBodyPeriod);
            Run("N-body energy drift is bounded", TestEnergyDrift);
            Run("N-body momentum conservation", TestMomentumConservation);
            Run("N-body 2D and 3D agree on planar data", TestDimensionConsistency);
            Run("N-body Coulomb like charges repel", TestCoulombRepulsion);
            Run("N-body softening handles overlap", TestSoftening);
            Run("N-body validation", TestNBodyValidation);
        }

        // ---------- PS-0: VecN ----------

        private static void TestVecN()
        {
            VecN a = new VecN(1.0, 2.0, 3.0);
            VecN b = new VecN(4.0, -1.0, 0.5);
            VecN sum = a + b;
            AssertEqual(sum[0], 5.0, "sum x", 1e-12);
            AssertEqual(sum[2], 3.5, "sum z", 1e-12);
            AssertEqual(VecN.Dot(a, b), 4.0 - 2.0 + 1.5, "dot", 1e-12);
            AssertEqual((2.0 * a)[1], 4.0, "scalar multiple", 1e-12);
            AssertEqual(new VecN(3.0, 4.0).Norm(), 5.0, "norm", 1e-12);
            AssertEqual(VecN.DistanceSquared(new VecN(0, 0, 0, 0), new VecN(1, 1, 1, 1)), 4.0, "4D distance squared", 1e-12);
            VecN c = a.Clone();
            c[0] = 99.0;
            AssertEqual(a[0], 1.0, "clone is deep", 1e-12);
        }

        private static void TestVecNValidation()
        {
            AssertThrows<ArgumentException>(() => { var _ = new VecN(1.0, 2.0) + new VecN(1.0, 2.0, 3.0); }, "dimension mismatch in add");
            AssertThrows<ArgumentException>(() => VecN.Dot(new VecN(1.0), new VecN(1.0, 2.0)), "dimension mismatch in dot");
            AssertThrows<ArgumentOutOfRangeException>(() => new VecN(0), "zero dimension rejected");
            Assert(!new VecN(1.0, double.NaN).IsFinite(), "NaN component reported as non-finite");
            Assert(new VecN(1.0, 2.0).IsFinite(), "finite vector reported as finite");
        }

        // ---------- PS-0: integrators ----------

        private static void TestVelocityVerlet()
        {
            // x'' = -x, x0 = 1, v0 = 0 -> x(t) = cos t.
            double ErrorAt(double dt, double T)
            {
                int steps = (int)Math.Round(T / dt);
                VecN x = new VecN(1.0);
                VecN v = new VecN(0.0);
                Func<VecN, VecN> accel = p => -1.0 * p;
                VecN a = accel(x);
                for (int i = 0; i < steps; i++)
                {
                    a = Integrators.VelocityVerletStep(dt, x, v, a, accel);
                }
                return Math.Abs(x[0] - Math.Cos(steps * dt));
            }

            double e1 = ErrorAt(0.1, 1.0);
            double e2 = ErrorAt(0.05, 1.0);
            double ratio = e1 / e2;
            Assert(ratio > 3.6 && ratio < 4.4, $"Verlet should be second order, error ratio was {ratio:F3}");

            // Energy stays bounded over many periods.
            VecN xe = new VecN(1.0);
            VecN ve = new VecN(0.0);
            Func<VecN, VecN> accelE = p => -1.0 * p;
            VecN ae = accelE(xe);
            double e0 = 0.5 * ve.NormSquared() + 0.5 * xe.NormSquared();
            double maxDrift = 0.0;
            for (int i = 0; i < 20000; i++)
            {
                ae = Integrators.VelocityVerletStep(0.01, xe, ve, ae, accelE);
                double e = 0.5 * ve.NormSquared() + 0.5 * xe.NormSquared();
                maxDrift = Math.Max(maxDrift, Math.Abs(e - e0));
            }
            Assert(maxDrift < 1e-3, $"Verlet oscillator energy drift too large: {maxDrift:E3}");
        }

        private static void TestRk4Order()
        {
            // y' = -y, y0 = 1 -> y(1) = exp(-1).
            double ErrorAt(double dt)
            {
                int steps = (int)Math.Round(1.0 / dt);
                double[] y = { 1.0 };
                for (int i = 0; i < steps; i++)
                {
                    y = Integrators.Rk4Step(i * dt, dt, y, (t, s) => new[] { -s[0] });
                }
                return Math.Abs(y[0] - Math.Exp(-1.0));
            }

            double ratio = ErrorAt(0.1) / ErrorAt(0.05);
            Assert(ratio > 12.0 && ratio < 20.0, $"RK4 should be fourth order, error ratio was {ratio:F3}");
        }

        private static void TestFixedStepLoop()
        {
            int calls = 0;
            double last = -1;
            double end = FixedStepLoop.Run(0.25, 8, (i, t) =>
            {
                calls++;
                last = t;
            });
            Assert(calls == 8, "loop should call step 8 times");
            AssertEqual(last, 7 * 0.25, "last start time", 1e-15);
            AssertEqual(end, 2.0, "final time", 1e-15);
            AssertThrows<ArgumentOutOfRangeException>(() => FixedStepLoop.Run(0.0, 1, (i, t) => { }), "zero dt rejected");
        }

        // ---------- PS-0: solvers ----------

        private static void TestConjugateGradient()
        {
            // 1D Laplacian with Dirichlet ends: tridiagonal (2, -1), SPD.
            const int n = 50;
            double[] truth = new double[n];
            for (int i = 0; i < n; i++) truth[i] = Math.Sin(0.1 * (i + 1));
            double[] apply(double[] v)
            {
                double[] r = new double[n];
                for (int i = 0; i < n; i++)
                {
                    r[i] = 2 * v[i];
                    if (i > 0) r[i] -= v[i - 1];
                    if (i < n - 1) r[i] -= v[i + 1];
                }
                return r;
            }
            double[] b = apply(truth);
            CgResult res = ConjugateGradient.Solve(apply, b, 1e-12, 500);
            Assert(res.Converged, "CG should converge on a 50-point Laplacian");
            for (int i = 0; i < n; i++)
            {
                AssertEqual(res.Solution[i], truth[i], $"CG solution component {i}", 1e-8);
            }
        }

        private static void TestConjugateGradientCap()
        {
            const int n = 50;
            double[] b = new double[n];
            b[0] = 1.0;
            CgResult res = ConjugateGradient.Solve(v =>
            {
                double[] r = new double[n];
                for (int i = 0; i < n; i++)
                {
                    r[i] = 2 * v[i];
                    if (i > 0) r[i] -= v[i - 1];
                    if (i < n - 1) r[i] -= v[i + 1];
                }
                return r;
            }, b, 1e-14, 3);
            Assert(!res.Converged, "CG with 3 iterations on 50 unknowns should not converge");
            Assert(res.Iterations == 3, "iteration count should equal the cap");
        }

        private static Complex[] NaiveDft(Complex[] x, bool inverse)
        {
            int n = x.Length;
            double sign = inverse ? 1.0 : -1.0;
            Complex[] y = new Complex[n];
            for (int k = 0; k < n; k++)
            {
                Complex s = Complex.Zero;
                for (int j = 0; j < n; j++)
                {
                    double ang = sign * 2 * Math.PI * ((long)j * k % n) / n;
                    s += x[j] * new Complex(Math.Cos(ang), Math.Sin(ang));
                }
                y[k] = inverse ? s / n : s;
            }
            return y;
        }

        private static Complex[] Sample(int n, int seed)
        {
            Random rng = new Random(seed);
            Complex[] x = new Complex[n];
            for (int i = 0; i < n; i++) x[i] = new Complex(rng.NextDouble() * 2 - 1, rng.NextDouble() * 2 - 1);
            return x;
        }

        private static void TestFftAgainstDft()
        {
            foreach (int n in new[] { 1, 2, 8, 16, 7, 12, 25 })
            {
                Complex[] x = Sample(n, 100 + n);
                Complex[] fast = Fft.Transform(x, false);
                Complex[] slow = NaiveDft(x, false);
                for (int k = 0; k < n; k++)
                {
                    AssertComplexEqual(fast[k], slow[k], $"forward DFT n={n} k={k}", 1e-9);
                }
            }
        }

        private static void TestFftRoundTrip()
        {
            foreach (int n in new[] { 1, 4, 64, 6, 10, 97 })
            {
                Complex[] x = Sample(n, 7 + n);
                Complex[] back = Fft.Transform(Fft.Transform(x, false), true);
                for (int k = 0; k < n; k++)
                {
                    AssertComplexEqual(back[k], x[k], $"round trip n={n} k={k}", 1e-9);
                }
            }
            Assert(Fft.Transform(Array.Empty<Complex>(), false).Length == 0, "empty input returns empty output");
        }

        private static void TestFftValidation()
        {
            AssertThrows<ArgumentNullException>(() => Fft.Transform(null!, false), "null input rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => Fft.Transform(new[] { new Complex(double.NaN, 0) }, false), "NaN input rejected");
        }

        // ---------- PS-2: N-body ----------

        // Two bodies, centre of mass at rest, relative separation r0 with circular relative speed.
        private static NBodySystem CircularBinary(int dimension, double bigMass, double smallMass, double r0, double G, out double period)
        {
            var law = new NewtonianGravity(G);
            var sys = new NBodySystem(dimension, law);
            double mu = bigMass + smallMass;
            double vRel = Math.Sqrt(G * mu / r0);
            // Positions: big at -smallMass/mu * r0 along x, small at +bigMass/mu * r0. Velocities mirror this.
            double xBig = -smallMass / mu * r0;
            double xSmall = bigMass / mu * r0;
            double vBig = -smallMass / mu * vRel;
            double vSmall = bigMass / mu * vRel;
            VecN pBig = new VecN(dimension);
            VecN pSmall = new VecN(dimension);
            VecN vBigV = new VecN(dimension);
            VecN vSmallV = new VecN(dimension);
            pBig[0] = xBig;
            pSmall[0] = xSmall;
            if (dimension >= 2)
            {
                vBigV[1] = vBig;
                vSmallV[1] = vSmall;
            }
            sys.AddParticle(bigMass, pBig, vBigV);
            sys.AddParticle(smallMass, pSmall, vSmallV);
            period = 2 * Math.PI * r0 / vRel;
            return sys;
        }

        private static void TestTwoBodyPeriod()
        {
            double G = 1.0;
            double period;
            NBodySystem sys = CircularBinary(3, 1.0, 1e-3, 1.0, G, out period);
            VecN start = sys.GetPosition(1) - sys.GetPosition(0);
            int steps = 4000;
            double dt = period / steps;
            // Relative vector returns to its starting value after one period.
            for (int s = 0; s < steps; s++) sys.Step(dt);
            VecN end = sys.GetPosition(1) - sys.GetPosition(0);
            double miss = (end - start).Norm();
            Assert(miss < 2e-3, $"relative orbit should close after one period, miss {miss:E3}");
        }

        private static void TestEnergyDrift()
        {
            double period;
            NBodySystem sys = CircularBinary(3, 1.0, 1e-3, 1.0, 1.0, out period);
            double e0 = sys.Diagnostics().Total;
            int steps = 10 * 1000;
            double dt = 10 * period / steps;
            double maxRel = 0.0;
            for (int s = 0; s < steps; s++)
            {
                sys.Step(dt);
                if (s % 100 == 0)
                {
                    maxRel = Math.Max(maxRel, Math.Abs(sys.Diagnostics().Total - e0) / Math.Abs(e0));
                }
            }
            Assert(maxRel < 1e-4, $"energy drift over 10 orbits too large: {maxRel:E3}");
        }

        private static NBodySystem RandomCluster(int dimension, int count, int seed, double softening = 0.05)
        {
            Random rng = new Random(seed);
            var sys = new NBodySystem(dimension, new NewtonianGravity(1.0), softening);
            for (int i = 0; i < count; i++)
            {
                VecN p = new VecN(dimension);
                VecN v = new VecN(dimension);
                for (int d = 0; d < dimension; d++)
                {
                    p[d] = rng.NextDouble() * 2 - 1;
                    v[d] = (rng.NextDouble() * 2 - 1) * 0.2;
                }
                sys.AddParticle(0.5 + rng.NextDouble(), p, v);
            }
            return sys;
        }

        private static void TestMomentumConservation()
        {
            NBodySystem sys = RandomCluster(3, 6, 2024);
            VecN p0 = sys.Momentum();
            for (int s = 0; s < 500; s++) sys.Step(0.001);
            VecN p1 = sys.Momentum();
            Assert((p1 - p0).Norm() < 1e-11, $"momentum drift {(p1 - p0).Norm():E3}");
        }

        private static void TestDimensionConsistency()
        {
            // Same planar configuration embedded in 2D and 3D (z = 0) must give identical trajectories.
            NBodySystem a = new NBodySystem(2, new NewtonianGravity(1.0), 0.05);
            NBodySystem b = new NBodySystem(3, new NewtonianGravity(1.0), 0.05);
            Random rng = new Random(5);
            for (int i = 0; i < 4; i++)
            {
                double m = 0.5 + rng.NextDouble();
                double x = rng.NextDouble() - 0.5, y = rng.NextDouble() - 0.5;
                double vx = rng.NextDouble() - 0.5, vy = rng.NextDouble() - 0.5;
                a.AddParticle(m, new VecN(x, y), new VecN(vx, vy));
                b.AddParticle(m, new VecN(x, y, 0.0), new VecN(vx, vy, 0.0));
            }
            for (int s = 0; s < 200; s++)
            {
                a.Step(0.002);
                b.Step(0.002);
            }
            for (int i = 0; i < 4; i++)
            {
                VecN pa = a.GetPosition(i);
                VecN pb = b.GetPosition(i);
                AssertEqual(pb[0], pa[0], $"x of particle {i}", 1e-12);
                AssertEqual(pb[1], pa[1], $"y of particle {i}", 1e-12);
                AssertEqual(pb[2], 0.0, $"z of particle {i} stays zero", 1e-12);
            }
        }

        private static void TestCoulombRepulsion()
        {
            var sys = new NBodySystem(2, new Coulomb(1.0));
            sys.AddParticle(1.0, new VecN(0.0, 0.0), new VecN(0.0, 0.0));
            sys.AddParticle(1.0, new VecN(1.0, 0.0), new VecN(0.0, 0.0));
            AssertEqual(sys.PotentialEnergy(), 1.0, "Coulomb potential k q1 q2 / r", 1e-12);
            double before = (sys.GetPosition(1) - sys.GetPosition(0)).Norm();
            sys.Step(0.001);
            double after = (sys.GetPosition(1) - sys.GetPosition(0)).Norm();
            Assert(after > before, "like charges should repel");
        }

        private static void TestSoftening()
        {
            var sys = new NBodySystem(3, new NewtonianGravity(1.0), 0.1);
            sys.AddParticle(1.0, new VecN(0, 0, 0), new VecN(0, 0, 0));
            sys.AddParticle(1.0, new VecN(0, 0, 0), new VecN(0, 0, 0));
            sys.Step(0.01);
            Assert(double.IsFinite(sys.Diagnostics().Total), "softened overlap stays finite");
        }

        private static void TestNBodyValidation()
        {
            AssertThrows<ArgumentNullException>(() => new NBodySystem(3, null!), "null law rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => new NBodySystem(3, new NewtonianGravity(1.0), -1.0), "negative softening rejected");
            var sys = new NBodySystem(3, new NewtonianGravity(1.0));
            AssertThrows<ArgumentOutOfRangeException>(() => sys.AddParticle(0.0, new VecN(3), new VecN(3)), "zero mass rejected");
            AssertThrows<ArgumentException>(() => sys.AddParticle(1.0, new VecN(2), new VecN(3)), "wrong dimension rejected");
            AssertThrows<ArgumentOutOfRangeException>(() => sys.Step(0.0), "zero dt rejected");
        }
    }
}
