using System;
using System.Globalization;
using System.Numerics;

namespace OPEN_LRD.Foundations
{
    internal sealed class Fraction : IEquatable<Fraction>, IComparable<Fraction>
    {
        public const int MaximumParseLength = 16_384;
        public const int MaximumComponentDigitLength = 4_096;

        public BigInteger Numerator { get; }
        public BigInteger Denominator { get; }

        public Fraction(int numerator, int denominator)
            : this(new BigInteger(numerator), new BigInteger(denominator))
        {
        }

        public Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
            {
                throw new DivideByZeroException("The denominator of a fraction cannot be zero.");
            }

            if (denominator.Sign < 0)
            {
                numerator = BigInteger.Negate(numerator);
                denominator = BigInteger.Negate(denominator);
            }

            if (numerator.IsZero)
            {
                Numerator = BigInteger.Zero;
                Denominator = BigInteger.One;
                return;
            }

            BigInteger divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            Numerator = numerator / divisor;
            Denominator = denominator / divisor;
        }

        public double ToDecimal()
        {
            return (double)Numerator / (double)Denominator;
        }

        public float ToFloat()
        {
            return (float)Numerator / (float)Denominator;
        }

        public Fraction Invert()
        {
            if (Numerator.IsZero)
            {
                throw new DivideByZeroException("A zero fraction cannot be inverted.");
            }
            return new Fraction(Denominator * Numerator.Sign, BigInteger.Abs(Numerator));
        }

        public static Fraction Parse(string value)
        {
            if (!TryParse(value, out Fraction? fraction))
            {
                throw new FormatException("The value is not a valid invariant-culture fraction.");
            }
            return fraction!;
        }

        public static bool TryParse(string? value, out Fraction? fraction)
        {
            fraction = null;
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumParseLength)
            {
                return false;
            }

            string text = value.Trim();
            int separator = text.IndexOf('/');
            if (separator < 0)
            {
                if (text.Length > MaximumComponentDigitLength ||
                    !BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger integer))
                {
                    return false;
                }
                fraction = new Fraction(integer, BigInteger.One);
                return true;
            }

            ReadOnlySpan<char> numeratorSpan = text.AsSpan(0, separator).Trim();
            ReadOnlySpan<char> denominatorSpan = text.AsSpan(separator + 1).Trim();
            if (separator != text.LastIndexOf('/') ||
                numeratorSpan.Length > MaximumComponentDigitLength ||
                denominatorSpan.Length > MaximumComponentDigitLength ||
                !BigInteger.TryParse(numeratorSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger numerator) ||
                !BigInteger.TryParse(denominatorSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger denominator) ||
                denominator.IsZero)
            {
                return false;
            }

            fraction = new Fraction(numerator, denominator);
            return true;
        }

        public int CompareTo(Fraction? other)
        {
            if (other == null)
            {
                return 1;
            }
            return (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);
        }

        public bool Equals(Fraction? other)
        {
            return other != null && Numerator == other.Numerator && Denominator == other.Denominator;
        }

        public override bool Equals(object? obj) => obj is Fraction other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Numerator, Denominator);

        public static bool operator ==(Fraction? left, Fraction? right) => Equals(left, right);
        public static bool operator !=(Fraction? left, Fraction? right) => !Equals(left, right);
        public static bool operator <(Fraction left, Fraction right) => left.CompareTo(right) < 0;
        public static bool operator >(Fraction left, Fraction right) => left.CompareTo(right) > 0;
        public static bool operator <=(Fraction left, Fraction right) => left.CompareTo(right) <= 0;
        public static bool operator >=(Fraction left, Fraction right) => left.CompareTo(right) >= 0;

        public static Fraction operator +(Fraction left, Fraction right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);
            return new Fraction(
                (left.Numerator * right.Denominator) + (right.Numerator * left.Denominator),
                left.Denominator * right.Denominator);
        }

        public static Fraction operator -(Fraction left, Fraction right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);
            return new Fraction(
                (left.Numerator * right.Denominator) - (right.Numerator * left.Denominator),
                left.Denominator * right.Denominator);
        }

        public static Fraction operator *(Fraction left, Fraction right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);
            return new Fraction(left.Numerator * right.Numerator, left.Denominator * right.Denominator);
        }

        public static Fraction operator /(Fraction left, Fraction right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);
            if (right.Numerator.IsZero)
            {
                throw new DivideByZeroException("Cannot divide by a zero fraction.");
            }
            return new Fraction(left.Numerator * right.Denominator, left.Denominator * right.Numerator);
        }

        public static Fraction operator -(Fraction value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new Fraction(BigInteger.Negate(value.Numerator), value.Denominator);
        }

        public override string ToString()
        {
            return $"{Numerator.ToString(CultureInfo.InvariantCulture)}/{Denominator.ToString(CultureInfo.InvariantCulture)}";
        }

        public string ToFractionString() => ToString();
    }
}
