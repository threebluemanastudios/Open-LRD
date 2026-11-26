using System;

namespace OPEN_LRD.PhysicsSim
{
    /// <summary>
    /// Fixed-dimension real vector backed by a double array. Operations check that
    /// dimensions match. Dimension 3 takes an unrolled path in the hot operations.
    /// </summary>
    public sealed class VecN
    {
        private readonly double[] data;

        /// <summary>Creates a zero vector of the given dimension.</summary>
        /// <param name="dimension">Number of components, at least 1.</param>
        public VecN(int dimension)
        {
            if (dimension < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be at least 1.");
            }
            data = new double[dimension];
        }

        /// <summary>Creates a vector from components. Components are copied.</summary>
        /// <param name="components">Vector components, at least one.</param>
        public VecN(params double[] components)
            : this(components?.Length ?? throw new ArgumentNullException(nameof(components)))
        {
            Array.Copy(components, data, components.Length);
        }

        /// <summary>Number of components.</summary>
        public int Dimension => data.Length;

        /// <summary>Component access.</summary>
        /// <param name="index">Zero-based component index.</param>
        public double this[int index]
        {
            get => data[index];
            set => data[index] = value;
        }

        /// <summary>Returns a deep copy.</summary>
        public VecN Clone()
        {
            VecN copy = new VecN(data.Length);
            Array.Copy(data, copy.data, data.Length);
            return copy;
        }

        /// <summary>Returns true when every component is finite.</summary>
        public bool IsFinite()
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (!double.IsFinite(data[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Component-wise sum.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand, same dimension.</param>
        public static VecN operator +(VecN a, VecN b)
        {
            RequireSameDimension(a, b);
            VecN r = new VecN(a.data.Length);
            if (a.data.Length == 3)
            {
                r.data[0] = a.data[0] + b.data[0];
                r.data[1] = a.data[1] + b.data[1];
                r.data[2] = a.data[2] + b.data[2];
                return r;
            }
            for (int i = 0; i < a.data.Length; i++)
            {
                r.data[i] = a.data[i] + b.data[i];
            }
            return r;
        }

        /// <summary>Component-wise difference.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand, same dimension.</param>
        public static VecN operator -(VecN a, VecN b)
        {
            RequireSameDimension(a, b);
            VecN r = new VecN(a.data.Length);
            if (a.data.Length == 3)
            {
                r.data[0] = a.data[0] - b.data[0];
                r.data[1] = a.data[1] - b.data[1];
                r.data[2] = a.data[2] - b.data[2];
                return r;
            }
            for (int i = 0; i < a.data.Length; i++)
            {
                r.data[i] = a.data[i] - b.data[i];
            }
            return r;
        }

        /// <summary>Scalar multiple.</summary>
        /// <param name="s">Scale factor.</param>
        /// <param name="v">Vector.</param>
        public static VecN operator *(double s, VecN v)
        {
            VecN r = new VecN(v.data.Length);
            for (int i = 0; i < v.data.Length; i++)
            {
                r.data[i] = s * v.data[i];
            }
            return r;
        }

        /// <summary>Dot product.</summary>
        /// <param name="a">Left operand.</param>
        /// <param name="b">Right operand, same dimension.</param>
        public static double Dot(VecN a, VecN b)
        {
            RequireSameDimension(a, b);
            if (a.data.Length == 3)
            {
                return a.data[0] * b.data[0] + a.data[1] * b.data[1] + a.data[2] * b.data[2];
            }
            double s = 0.0;
            for (int i = 0; i < a.data.Length; i++)
            {
                s += a.data[i] * b.data[i];
            }
            return s;
        }

        /// <summary>Squared Euclidean norm.</summary>
        public double NormSquared() => Dot(this, this);

        /// <summary>Euclidean norm.</summary>
        public double Norm() => Math.Sqrt(NormSquared());

        /// <summary>Squared distance between two points of the same dimension.</summary>
        /// <param name="a">First point.</param>
        /// <param name="b">Second point.</param>
        public static double DistanceSquared(VecN a, VecN b)
        {
            RequireSameDimension(a, b);
            double s = 0.0;
            for (int i = 0; i < a.data.Length; i++)
            {
                double d = a.data[i] - b.data[i];
                s += d * d;
            }
            return s;
        }

        /// <summary>Adds <paramref name="scale"/> times <paramref name="v"/> to this vector in place.</summary>
        /// <param name="scale">Multiplier applied to <paramref name="v"/>.</param>
        /// <param name="v">Vector to add, same dimension.</param>
        public void AddScaledInPlace(double scale, VecN v)
        {
            RequireSameDimension(this, v);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] += scale * v.data[i];
            }
        }

        /// <summary>Copies all components from <paramref name="source"/> into this vector.</summary>
        /// <param name="source">Source vector, same dimension.</param>
        public void CopyFrom(VecN source)
        {
            RequireSameDimension(this, source);
            Array.Copy(source.data, data, data.Length);
        }

        /// <summary>Returns the components as a new array.</summary>
        public double[] ToArray()
        {
            return (double[])data.Clone();
        }

        private static void RequireSameDimension(VecN a, VecN b)
        {
            if (a is null) throw new ArgumentNullException(nameof(a));
            if (b is null) throw new ArgumentNullException(nameof(b));
            if (a.data.Length != b.data.Length)
            {
                throw new ArgumentException($"Dimension mismatch: {a.data.Length} vs {b.data.Length}.");
            }
        }
    }
}
