using System;

namespace OPEN_LRD.PhysicsSim
{
    /// <summary>Time integrators for second-order and first-order systems.</summary>
    public static class Integrators
    {
        /// <summary>
        /// One velocity-Verlet step for x'' = a(x). Updates <paramref name="x"/> and <paramref name="v"/> in place.
        /// </summary>
        /// <param name="dt">Time step, finite and positive.</param>
        /// <param name="x">Position, updated in place.</param>
        /// <param name="v">Velocity, updated in place.</param>
        /// <param name="a">Acceleration at the current position.</param>
        /// <param name="accel">Acceleration function a(x).</param>
        /// <returns>The acceleration at the new position, to be passed to the next step.</returns>
        public static VecN VelocityVerletStep(double dt, VecN x, VecN v, VecN a, Func<VecN, VecN> accel)
        {
            RequireStep(dt);
            if (x is null) throw new ArgumentNullException(nameof(x));
            if (v is null) throw new ArgumentNullException(nameof(v));
            if (a is null) throw new ArgumentNullException(nameof(a));
            if (accel is null) throw new ArgumentNullException(nameof(accel));

            // x += v dt + 1/2 a dt^2
            x.AddScaledInPlace(dt, v);
            x.AddScaledInPlace(0.5 * dt * dt, a);

            VecN aNew = accel(x);
            if (aNew is null || aNew.Dimension != a.Dimension)
            {
                throw new ArgumentException("Acceleration function returned a vector of the wrong dimension.");
            }

            // v += 1/2 (a + aNew) dt
            v.AddScaledInPlace(0.5 * dt, a);
            v.AddScaledInPlace(0.5 * dt, aNew);
            return aNew;
        }

        /// <summary>
        /// One classical Runge-Kutta (RK4) step for y' = f(t, y). Returns a new state array.
        /// </summary>
        /// <param name="t">Current time.</param>
        /// <param name="dt">Time step, finite and positive.</param>
        /// <param name="y">Current state. Not modified.</param>
        /// <param name="f">Right-hand side f(t, y). Must return an array of the same length as y.</param>
        /// <returns>The state at time t + dt.</returns>
        public static double[] Rk4Step(double t, double dt, double[] y, Func<double, double[], double[]> f)
        {
            RequireStep(dt);
            if (y is null) throw new ArgumentNullException(nameof(y));
            if (f is null) throw new ArgumentNullException(nameof(f));
            int n = y.Length;

            double[] k1 = Eval(f, t, y, n);
            double[] tmp = new double[n];
            for (int i = 0; i < n; i++) tmp[i] = y[i] + 0.5 * dt * k1[i];
            double[] k2 = Eval(f, t + 0.5 * dt, tmp, n);
            for (int i = 0; i < n; i++) tmp[i] = y[i] + 0.5 * dt * k2[i];
            double[] k3 = Eval(f, t + 0.5 * dt, tmp, n);
            for (int i = 0; i < n; i++) tmp[i] = y[i] + dt * k3[i];
            double[] k4 = Eval(f, t + dt, tmp, n);

            double[] result = new double[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = y[i] + dt / 6.0 * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]);
            }
            return result;
        }

        private static double[] Eval(Func<double, double[], double[]> f, double t, double[] y, int n)
        {
            double[] k = f(t, y);
            if (k is null || k.Length != n)
            {
                throw new ArgumentException("Right-hand side must return an array of the same length as the state.");
            }
            return k;
        }

        private static void RequireStep(double dt)
        {
            if (!double.IsFinite(dt) || dt <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(dt), "Time step must be finite and positive.");
            }
        }
    }

    /// <summary>Fixed-step loop with deterministic step times t = i * dt.</summary>
    public static class FixedStepLoop
    {
        /// <summary>
        /// Runs <paramref name="steps"/> calls of <paramref name="step"/>. Each call receives the step index
        /// and its start time t = index * dt, so time does not accumulate rounding error.
        /// </summary>
        /// <param name="dt">Time step, finite and positive.</param>
        /// <param name="steps">Number of steps, non-negative.</param>
        /// <param name="step">Callback receiving (stepIndex, startTime).</param>
        /// <returns>The final time steps * dt.</returns>
        public static double Run(double dt, int steps, Action<int, double> step)
        {
            if (!double.IsFinite(dt) || dt <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(dt), "Time step must be finite and positive.");
            }
            if (steps < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(steps), "Step count must be non-negative.");
            }
            if (step is null)
            {
                throw new ArgumentNullException(nameof(step));
            }
            for (int i = 0; i < steps; i++)
            {
                step(i, i * dt);
            }
            return steps * dt;
        }
    }
}
