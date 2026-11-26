using System;
using System.Numerics;

namespace OPEN_LRD.PhysicsSim
{
    /// <summary>Result of an iterative linear solve.</summary>
    /// <param name="Solution">Approximate solution x of A x = b.</param>
    /// <param name="Iterations">Number of CG iterations performed.</param>
    /// <param name="ResidualNorm">Final Euclidean norm of b - A x.</param>
    /// <param name="Converged">True when the residual met the tolerance.</param>
    public readonly record struct CgResult(double[] Solution, int Iterations, double ResidualNorm, bool Converged);

    /// <summary>Conjugate gradient solver for symmetric positive-definite operators.</summary>
    public static class ConjugateGradient
    {
        /// <summary>
        /// Solves A x = b with unpreconditioned conjugate gradients. The operator must be symmetric positive definite.
        /// </summary>
        /// <param name="apply">Computes A v for a vector v. Must not modify its argument.</param>
        /// <param name="b">Right-hand side.</param>
        /// <param name="tolerance">Stop when the residual norm is at most tolerance * ||b||, positive and finite.</param>
        /// <param name="maxIterations">Iteration cap, positive.</param>
        /// <returns>The solve result. <see cref="CgResult.Converged"/> is false if the cap was reached.</returns>
        public static CgResult Solve(Func<double[], double[]> apply, double[] b, double tolerance, int maxIterations)
        {
            if (apply is null) throw new ArgumentNullException(nameof(apply));
            if (b is null) throw new ArgumentNullException(nameof(b));
            if (!double.IsFinite(tolerance) || tolerance <= 0.0) throw new ArgumentOutOfRangeException(nameof(tolerance));
            if (maxIterations <= 0) throw new ArgumentOutOfRangeException(nameof(maxIterations));
            foreach (double value in b)
            {
                if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(b), "Right-hand side must be finite.");
            }

            int n = b.Length;
            double[] x = new double[n];
            double[] r = (double[])b.Clone();
            double[] p = (double[])r.Clone();
            double rr = Dot(r, r);
            double bNorm = Math.Sqrt(rr);
            if (bNorm == 0.0)
            {
                return new CgResult(x, 0, 0.0, true);
            }
            double target = tolerance * bNorm;

            for (int it = 0; it < maxIterations; it++)
            {
                if (Math.Sqrt(rr) <= target)
                {
                    return new CgResult(x, it, Math.Sqrt(rr), true);
                }
                double[] ap = apply(p);
                if (ap is null || ap.Length != n) throw new InvalidOperationException("Operator returned a vector of the wrong length.");
                double pap = Dot(p, ap);
                if (!(pap > 0.0) || !double.IsFinite(pap))
                {
                    // Not positive definite along p, or numerical breakdown.
                    return new CgResult(x, it, Math.Sqrt(rr), false);
                }
                double alpha = rr / pap;
                for (int i = 0; i < n; i++)
                {
                    x[i] += alpha * p[i];
                    r[i] -= alpha * ap[i];
                }
                double rrNew = Dot(r, r);
                double beta = rrNew / rr;
                for (int i = 0; i < n; i++)
                {
                    p[i] = r[i] + beta * p[i];
                }
                rr = rrNew;
            }
            double finalNorm = Math.Sqrt(rr);
            return new CgResult(x, maxIterations, finalNorm, finalNorm <= target);
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0.0;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }
    }

    /// <summary>Discrete Fourier transform: radix-2 Cooley-Tukey, Bluestein for other lengths.</summary>
    public static class Fft
    {
        /// <summary>
        /// Forward DFT X_k = sum_j x_j exp(-2 pi i j k / n). Inverse applies the conjugate kernel and divides by n.
        /// Power-of-two lengths use radix-2. Other lengths use Bluestein's chirp-z algorithm.
        /// </summary>
        /// <param name="input">Input samples. Not modified.</param>
        /// <param name="inverse">True for the inverse transform.</param>
        /// <returns>The transformed array.</returns>
        public static Complex[] Transform(Complex[] input, bool inverse)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));
            int n = input.Length;
            if (n == 0) return Array.Empty<Complex>();
            foreach (Complex c in input)
            {
                if (!double.IsFinite(c.Real) || !double.IsFinite(c.Imaginary))
                {
                    throw new ArgumentOutOfRangeException(nameof(input), "FFT input must be finite.");
                }
            }

            Complex[] result = (n & (n - 1)) == 0
                ? Radix2((Complex[])input.Clone(), inverse)
                : Bluestein(input, inverse);

            if (inverse)
            {
                for (int i = 0; i < n; i++) result[i] /= n;
            }
            return result;
        }

        private static Complex[] Radix2(Complex[] a, bool inverse)
        {
            int n = a.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) (a[i], a[j]) = (a[j], a[i]);
            }

            double sign = inverse ? 1.0 : -1.0;
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = sign * 2.0 * Math.PI / len;
                int half = len >> 1;
                for (int start = 0; start < n; start += len)
                {
                    for (int k = 0; k < half; k++)
                    {
                        Complex w = new Complex(Math.Cos(ang * k), Math.Sin(ang * k));
                        Complex u = a[start + k];
                        Complex v = a[start + k + half] * w;
                        a[start + k] = u + v;
                        a[start + k + half] = u - v;
                    }
                }
            }
            return a;
        }

        private static Complex[] Bluestein(Complex[] x, bool inverse)
        {
            int n = x.Length;
            double sign = inverse ? 1.0 : -1.0;

            // Chirp c_m = exp(sign * i * pi * m^2 / n). m^2 is reduced mod 2n to keep the angle small.
            Complex[] chirp = new Complex[n];
            long twoN = 2L * n;
            for (int m = 0; m < n; m++)
            {
                long sq = ((long)m * m) % twoN;
                double ang = sign * Math.PI * sq / n;
                chirp[m] = new Complex(Math.Cos(ang), Math.Sin(ang));
            }

            int size = 1;
            while (size < 2 * n - 1) size <<= 1;

            Complex[] a = new Complex[size];
            for (int j = 0; j < n; j++) a[j] = x[j] * chirp[j];

            Complex[] b = new Complex[size];
            b[0] = Complex.Conjugate(chirp[0]);
            for (int m = 1; m < n; m++)
            {
                Complex cm = Complex.Conjugate(chirp[m]);
                b[m] = cm;
                b[size - m] = cm;
            }

            Complex[] fa = Radix2(a, false);
            Complex[] fb = Radix2(b, false);
            for (int i = 0; i < size; i++) fa[i] *= fb[i];
            Complex[] conv = Radix2(fa, true);
            for (int i = 0; i < size; i++) conv[i] /= size;

            Complex[] result = new Complex[n];
            for (int k = 0; k < n; k++) result[k] = chirp[k] * conv[k];
            return result;
        }
    }
}
