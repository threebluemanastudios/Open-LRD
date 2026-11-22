using System;
using System.Globalization;

namespace OPEN_LRD.Foundations
{
    internal readonly struct Vector2 : IEquatable<Vector2>
    {
        public double X { get; }
        public double Y { get; }

        public static Vector2 Zero => new Vector2(0.0, 0.0);
        public static Vector2 UnitX => new Vector2(1.0, 0.0);
        public static Vector2 UnitY => new Vector2(0.0, 1.0);
        public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);

        public Vector2(double x, double y)
        {
            ValidateFinite(x, nameof(x));
            ValidateFinite(y, nameof(y));
            X = x;
            Y = y;
        }

        public static Vector2 operator +(Vector2 left, Vector2 right)
        {
            return new Vector2(left.X + right.X, left.Y + right.Y);
        }

        public static Vector2 operator -(Vector2 left, Vector2 right)
        {
            return new Vector2(left.X - right.X, left.Y - right.Y);
        }

        public static Vector2 operator -(Vector2 value)
        {
            return new Vector2(-value.X, -value.Y);
        }

        public static Vector2 operator *(Vector2 vector, double scale)
        {
            ValidateFinite(scale, nameof(scale));
            return new Vector2(vector.X * scale, vector.Y * scale);
        }

        public static Vector2 operator *(double scale, Vector2 vector)
        {
            return vector * scale;
        }

        public static Vector2 operator /(Vector2 vector, double divisor)
        {
            ValidateFinite(divisor, nameof(divisor));
            if (divisor == 0.0)
            {
                throw new DivideByZeroException("A vector cannot be divided by zero.");
            }
            return new Vector2(vector.X / divisor, vector.Y / divisor);
        }

        public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);
        public static bool operator !=(Vector2 left, Vector2 right) => !left.Equals(right);

        public double Dot(Vector2 other)
        {
            return ValidateFiniteResult((X * other.X) + (Y * other.Y), "Dot product");
        }

        public double LengthSquared()
        {
            return ValidateFiniteResult((X * X) + (Y * Y), "Squared vector length");
        }

        public double Length()
        {
            double scale = Math.Max(Math.Abs(X), Math.Abs(Y));
            if (scale == 0.0) return 0.0;
            double scaledX = X / scale;
            double scaledY = Y / scale;
            return ValidateFiniteResult(scale * Math.Sqrt((scaledX * scaledX) + (scaledY * scaledY)), "Vector length");
        }

        public Vector2 Normalize()
        {
            double length = Length();
            if (length == 0.0)
            {
                throw new InvalidOperationException("A zero-length vector cannot be normalized.");
            }
            return this / length;
        }

        public double Distance(Vector2 other)
        {
            return (this - other).Length();
        }

        public double Angle(Vector2 other)
        {
            Vector2 left = Normalize();
            Vector2 right = other.Normalize();
            return Math.Acos(Math.Clamp(left.Dot(right), -1.0, 1.0));
        }

        public static Vector2 Lerp(Vector2 start, Vector2 end, double amount)
        {
            ValidateFinite(amount, nameof(amount));
            return start + ((end - start) * amount);
        }

        public bool Equals(Vector2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString()
        {
            return $"({X.ToString("R", CultureInfo.InvariantCulture)}, {Y.ToString("R", CultureInfo.InvariantCulture)})";
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Vector components and scalar operands must be finite.");
            }
        }

        private static double ValidateFiniteResult(double value, string operation)
        {
            if (!double.IsFinite(value))
            {
                throw new ArithmeticException($"{operation} produced a non-finite result.");
            }
            return value;
        }
    }

    internal readonly struct Vector3 : IEquatable<Vector3>
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static Vector3 Zero => new Vector3(0.0, 0.0, 0.0);
        public static Vector3 UnitX => new Vector3(1.0, 0.0, 0.0);
        public static Vector3 UnitY => new Vector3(0.0, 1.0, 0.0);
        public static Vector3 UnitZ => new Vector3(0.0, 0.0, 1.0);
        public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

        public Vector3(double x, double y, double z)
        {
            ValidateFinite(x, nameof(x));
            ValidateFinite(y, nameof(y));
            ValidateFinite(z, nameof(z));
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3 operator +(Vector3 left, Vector3 right)
        {
            return new Vector3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static Vector3 operator -(Vector3 left, Vector3 right)
        {
            return new Vector3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
        }

        public static Vector3 operator -(Vector3 value)
        {
            return new Vector3(-value.X, -value.Y, -value.Z);
        }

        public static Vector3 operator *(Vector3 vector, double scale)
        {
            ValidateFinite(scale, nameof(scale));
            return new Vector3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        public static Vector3 operator *(double scale, Vector3 vector)
        {
            return vector * scale;
        }

        public static Vector3 operator /(Vector3 vector, double divisor)
        {
            ValidateFinite(divisor, nameof(divisor));
            if (divisor == 0.0)
            {
                throw new DivideByZeroException("A vector cannot be divided by zero.");
            }
            return new Vector3(vector.X / divisor, vector.Y / divisor, vector.Z / divisor);
        }

        public static bool operator ==(Vector3 left, Vector3 right) => left.Equals(right);
        public static bool operator !=(Vector3 left, Vector3 right) => !left.Equals(right);

        public double Dot(Vector3 other)
        {
            return ValidateFiniteResult((X * other.X) + (Y * other.Y) + (Z * other.Z), "Dot product");
        }

        public Vector3 Cross(Vector3 other)
        {
            return new Vector3(
                (Y * other.Z) - (Z * other.Y),
                (Z * other.X) - (X * other.Z),
                (X * other.Y) - (Y * other.X));
        }

        public double LengthSquared()
        {
            return ValidateFiniteResult((X * X) + (Y * Y) + (Z * Z), "Squared vector length");
        }

        public double Length()
        {
            double scale = Math.Max(Math.Abs(X), Math.Max(Math.Abs(Y), Math.Abs(Z)));
            if (scale == 0.0) return 0.0;
            double scaledX = X / scale;
            double scaledY = Y / scale;
            double scaledZ = Z / scale;
            return ValidateFiniteResult(scale * Math.Sqrt((scaledX * scaledX) + (scaledY * scaledY) + (scaledZ * scaledZ)), "Vector length");
        }

        public Vector3 Normalize()
        {
            double length = Length();
            if (length == 0.0)
            {
                throw new InvalidOperationException("A zero-length vector cannot be normalized.");
            }
            return this / length;
        }

        public double Distance(Vector3 other)
        {
            return (this - other).Length();
        }

        public double Angle(Vector3 other)
        {
            Vector3 left = Normalize();
            Vector3 right = other.Normalize();
            return Math.Acos(Math.Clamp(left.Dot(right), -1.0, 1.0));
        }

        public static Vector3 Lerp(Vector3 start, Vector3 end, double amount)
        {
            ValidateFinite(amount, nameof(amount));
            return start + ((end - start) * amount);
        }

        public bool Equals(Vector3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public override string ToString()
        {
            return $"({X.ToString("R", CultureInfo.InvariantCulture)}, {Y.ToString("R", CultureInfo.InvariantCulture)}, {Z.ToString("R", CultureInfo.InvariantCulture)})";
        }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Vector components and scalar operands must be finite.");
            }
        }

        private static double ValidateFiniteResult(double value, string operation)
        {
            if (!double.IsFinite(value))
            {
                throw new ArithmeticException($"{operation} produced a non-finite result.");
            }
            return value;
        }
    }
}
