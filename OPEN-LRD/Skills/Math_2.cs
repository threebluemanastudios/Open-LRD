using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal static class Math_2
    {
        /// <summary>
        /// Computes the inverse sine (arcsine) in radians for a double-precision input in [-1, 1].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double AsinDouble(double value)
        {
            EnsureUnitCircle(value, nameof(value));
            return Math.Asin(value);
        }

        /// <summary>
        /// Computes the inverse sine (arcsine) in radians for a single-precision input in [-1, 1].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float AsinFloat(float value)
        {
            return (float)AsinDouble(value);
        }

        /// <summary>
        /// Computes the inverse cosine (arccosine) in radians for a double-precision input in [-1, 1].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double AcosDouble(double value)
        {
            EnsureUnitCircle(value, nameof(value));
            return Math.Acos(value);
        }

        /// <summary>
        /// Computes the inverse cosine (arccosine) in radians for a single-precision input in [-1, 1].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float AcosFloat(float value)
        {
            return (float)AcosDouble(value);
        }

        /// <summary>
        /// Computes the inverse tangent (arctangent) in radians for a double-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double AtanDouble(double value)
        {
            return Math.Atan(value);
        }

        /// <summary>
        /// Computes the inverse tangent (arctangent) in radians for a single-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float AtanFloat(float value)
        {
            return (float)Math.Atan(value);
        }

        /// <summary>
        /// Computes the secant (1 / cos(radians)) of an angle in radians (double precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double SecDouble(double radians)
        {
            double cosine = Math.Cos(radians);
            if (Math.Abs(cosine) < 1e-12)
            {
                throw new DivideByZeroException("The secant is undefined where the cosine is zero.");
            }
            return 1.0 / cosine;
        }

        /// <summary>
        /// Computes the secant (1 / cos(radians)) of an angle in radians (single precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float SecFloat(float radians)
        {
            return (float)SecDouble(radians);
        }

        /// <summary>
        /// Computes the cosecant (1 / sin(radians)) of an angle in radians (double precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double CscDouble(double radians)
        {
            double sine = Math.Sin(radians);
            if (Math.Abs(sine) < 1e-12)
            {
                throw new DivideByZeroException("The cosecant is undefined where the sine is zero.");
            }
            return 1.0 / sine;
        }

        /// <summary>
        /// Computes the cosecant (1 / sin(radians)) of an angle in radians (single precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float CscFloat(float radians)
        {
            return (float)CscDouble(radians);
        }

        /// <summary>
        /// Computes the cotangent (1 / tan(radians)) of an angle in radians (double precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double CotDouble(double radians)
        {
            double sine = Math.Sin(radians);
            if (Math.Abs(sine) < 1e-12)
            {
                throw new DivideByZeroException("The cotangent is undefined where the sine is zero.");
            }
            return Math.Cos(radians) / sine;
        }

        /// <summary>
        /// Computes the cotangent (1 / tan(radians)) of an angle in radians (single precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float CotFloat(float radians)
        {
            return (float)CotDouble(radians);
        }

        /// <summary>
        /// Computes the hyperbolic sine of a double-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double SinhDouble(double value)
        {
            return Math.Sinh(value);
        }

        /// <summary>
        /// Computes the hyperbolic sine of a single-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float SinhFloat(float value)
        {
            return (float)Math.Sinh(value);
        }

        /// <summary>
        /// Computes the hyperbolic cosine of a double-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double CoshDouble(double value)
        {
            return Math.Cosh(value);
        }

        /// <summary>
        /// Computes the hyperbolic cosine of a single-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float CoshFloat(float value)
        {
            return (float)Math.Cosh(value);
        }

        /// <summary>
        /// Computes the hyperbolic tangent of a double-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double TanhDouble(double value)
        {
            return Math.Tanh(value);
        }

        /// <summary>
        /// Computes the hyperbolic tangent of a single-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float TanhFloat(float value)
        {
            return (float)Math.Tanh(value);
        }

        /// <summary>
        /// Computes the inverse hyperbolic sine of a double-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double AsinhDouble(double value)
        {
            return Math.Asinh(value);
        }

        /// <summary>
        /// Computes the inverse hyperbolic sine of a single-precision value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float AsinhFloat(float value)
        {
            return (float)Math.Asinh(value);
        }

        /// <summary>
        /// Computes the inverse hyperbolic cosine of a double-precision value (value >= 1).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double AcoshDouble(double value)
        {
            if (value < 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The argument of the inverse hyperbolic cosine must be at least one.");
            }
            return Math.Acosh(value);
        }

        /// <summary>
        /// Computes the inverse hyperbolic cosine of a single-precision value (value >= 1).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float AcoshFloat(float value)
        {
            return (float)AcoshDouble(value);
        }

        /// <summary>
        /// Computes the inverse hyperbolic tangent of a double-precision value (|value| &lt; 1).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static double AtanhDouble(double value)
        {
            if (value <= -1.0 || value >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The argument of the inverse hyperbolic tangent must be between -1 and 1.");
            }
            return Math.Atanh(value);
        }

        /// <summary>
        /// Computes the inverse hyperbolic tangent of a single-precision value (|value| &lt; 1).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The result.</returns>
        public static float AtanhFloat(float value)
        {
            return (float)AtanhDouble(value);
        }

        /// <summary>
        /// Wraps an angle in radians into a canonical normalized range (single precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static double NormalizeRadians(double radians)
        {
            const double twoPi = 2.0 * Math.PI;
            double normalized = radians % twoPi;
            if (normalized < 0.0)
            {
                normalized += twoPi;
            }
            return normalized;
        }

        /// <summary>
        /// Wraps an angle in radians into a canonical normalized range (single precision).
        /// </summary>
        /// <param name="radians">The radians.</param>
        /// <returns>The result.</returns>
        public static float NormalizeRadians(float radians)
        {
            return (float)NormalizeRadians((double)radians);
        }

        /// <summary>
        /// Wraps an angle in degrees into a canonical normalized range (single precision).
        /// </summary>
        /// <param name="degrees">The degrees.</param>
        /// <returns>The result.</returns>
        public static double NormalizeDegrees(double degrees)
        {
            double normalized = degrees % 360.0;
            if (normalized < 0.0)
            {
                normalized += 360.0;
            }
            return normalized;
        }

        /// <summary>
        /// Wraps an angle in degrees into a canonical normalized range (single precision).
        /// </summary>
        /// <param name="degrees">The degrees.</param>
        /// <returns>The result.</returns>
        public static float NormalizeDegrees(float degrees)
        {
            return (float)NormalizeDegrees((double)degrees);
        }

        /// <summary>
        /// Computes the angular difference between two angles in radians (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleDifference(double angleA, double angleB)
        {
            return NormalizeRadians(Math.Abs(angleB - angleA));
        }

        /// <summary>
        /// Computes the angular difference between two angles in radians (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static float CalculateAngleDifference(float angleA, float angleB)
        {
            return (float)CalculateAngleDifference((double)angleA, (double)angleB);
        }

        /// <summary>
        /// Computes the angular difference between two angles in degrees (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleDifferenceDegrees(double angleA, double angleB)
        {
            return NormalizeDegrees(Math.Abs(angleB - angleA));
        }

        /// <summary>
        /// Computes the angular difference between two angles in degrees (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static float CalculateAngleDifferenceDegrees(float angleA, float angleB)
        {
            return (float)CalculateAngleDifferenceDegrees((double)angleA, (double)angleB);
        }

        /// <summary>
        /// Computes the hypotenuse length of a right triangle from two perpendicular legs (single precision).
        /// </summary>
        /// <param name="legA">The leg a.</param>
        /// <param name="legB">The leg b.</param>
        /// <returns>The result.</returns>
        public static double CalculateHypotenuse(double legA, double legB)
        {
            EnsureNonNegative(legA, nameof(legA));
            EnsureNonNegative(legB, nameof(legB));
            return Math.Sqrt((legA * legA) + (legB * legB));
        }

        /// <summary>
        /// Computes the hypotenuse length of a right triangle from two perpendicular legs (single precision).
        /// </summary>
        /// <param name="legA">The leg a.</param>
        /// <param name="legB">The leg b.</param>
        /// <returns>The result.</returns>
        public static float CalculateHypotenuse(float legA, float legB)
        {
            return (float)CalculateHypotenuse((double)legA, (double)legB);
        }

        /// <summary>
        /// Computes the unknown leg length of a right triangle from the hypotenuse and one leg (single precision).
        /// </summary>
        /// <param name="hypotenuse">The hypotenuse.</param>
        /// <param name="knownLeg">The known leg.</param>
        /// <returns>The result.</returns>
        public static double CalculateLeg(double hypotenuse, double knownLeg)
        {
            EnsurePositive(hypotenuse, nameof(hypotenuse));
            EnsureNonNegative(knownLeg, nameof(knownLeg));
            if (knownLeg > hypotenuse)
            {
                throw new ArgumentException("A leg cannot be longer than the hypotenuse.");
            }
            return Math.Sqrt((hypotenuse * hypotenuse) - (knownLeg * knownLeg));
        }

        /// <summary>
        /// Computes the unknown leg length of a right triangle from the hypotenuse and one leg (single precision).
        /// </summary>
        /// <param name="hypotenuse">The hypotenuse.</param>
        /// <param name="knownLeg">The known leg.</param>
        /// <returns>The result.</returns>
        public static float CalculateLeg(float hypotenuse, float knownLeg)
        {
            return (float)CalculateLeg((double)hypotenuse, (double)knownLeg);
        }

        /// <summary>
        /// Determines whether three side lengths satisfy the Pythagorean theorem within the specified tolerance (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The result.</returns>
        public static bool IsRightTriangle(double sideA, double sideB, double sideC, double tolerance = 1e-9)
        {
            EnsureTriangleInequality(sideA, sideB, sideC);
            double[] sides = { sideA, sideB, sideC };
            Array.Sort(sides);
            double difference = (sides[0] * sides[0]) + (sides[1] * sides[1]) - (sides[2] * sides[2]);
            return Math.Abs(difference) <= tolerance;
        }

        /// <summary>
        /// Determines whether three side lengths satisfy the Pythagorean theorem within the specified tolerance (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The result.</returns>
        public static bool IsRightTriangle(float sideA, float sideB, float sideC, float tolerance = 1e-6f)
        {
            return IsRightTriangle((double)sideA, (double)sideB, (double)sideC, (double)tolerance);
        }

        /// <summary>
        /// Computes the third interior angle of a triangle in radians from two known angles (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleThirdAngle(double angleA, double angleB)
        {
            EnsureAngle(angleA);
            EnsureAngle(angleB);
            if (angleA + angleB >= Math.PI)
            {
                throw new ArgumentException("The two known angles must sum to less than pi radians.");
            }
            return Math.PI - angleA - angleB;
        }

        /// <summary>
        /// Computes the third interior angle of a triangle in radians from two known angles (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleThirdAngle(float angleA, float angleB)
        {
            return (float)CalculateTriangleThirdAngle((double)angleA, (double)angleB);
        }

        /// <summary>
        /// Computes the third interior angle of a triangle in degrees from two known angles (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleThirdAngleDegrees(double angleA, double angleB)
        {
            EnsureAngleDegrees(angleA);
            EnsureAngleDegrees(angleB);
            if (angleA + angleB >= 180.0)
            {
                throw new ArgumentException("The two known angles must sum to less than 180 degrees.");
            }
            return 180.0 - angleA - angleB;
        }

        /// <summary>
        /// Computes the third interior angle of a triangle in degrees from two known angles (single precision).
        /// </summary>
        /// <param name="angleA">The angle a.</param>
        /// <param name="angleB">The angle b.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleThirdAngleDegrees(float angleA, float angleB)
        {
            return (float)CalculateTriangleThirdAngleDegrees((double)angleA, (double)angleB);
        }

        /// <summary>
        /// Computes an unknown triangle side length in radians using the Law of Sines (single precision).
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngle">The known angle.</param>
        /// <param name="targetAngle">The target angle.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleSideLawOfSines(double knownSide, double knownAngle, double targetAngle)
        {
            EnsurePositive(knownSide, nameof(knownSide));
            EnsureAngle(knownAngle);
            EnsureAngle(targetAngle);
            return knownSide * Math.Sin(targetAngle) / Math.Sin(knownAngle);
        }

        /// <summary>
        /// Computes an unknown triangle side length in radians using the Law of Sines (single precision).
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngle">The known angle.</param>
        /// <param name="targetAngle">The target angle.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleSideLawOfSines(float knownSide, float knownAngle, float targetAngle)
        {
            return (float)CalculateTriangleSideLawOfSines((double)knownSide, (double)knownAngle, (double)targetAngle);
        }

        /// <summary>
        /// Computes an unknown triangle side length in degrees using the Law of Sines (single precision).
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngleDegrees">The known angle degrees.</param>
        /// <param name="targetAngleDegrees">The target angle degrees.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleSideLawOfSinesDegrees(double knownSide, double knownAngleDegrees, double targetAngleDegrees)
        {
            return CalculateTriangleSideLawOfSines(knownSide, DegreesToRadians(knownAngleDegrees), DegreesToRadians(targetAngleDegrees));
        }

        /// <summary>
        /// Computes an unknown triangle side length in degrees using the Law of Sines (single precision).
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngleDegrees">The known angle degrees.</param>
        /// <param name="targetAngleDegrees">The target angle degrees.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleSideLawOfSinesDegrees(float knownSide, float knownAngleDegrees, float targetAngleDegrees)
        {
            return (float)CalculateTriangleSideLawOfSinesDegrees((double)knownSide, (double)knownAngleDegrees, (double)targetAngleDegrees);
        }

        /// <summary>
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngle">The known angle.</param>
        /// <param name="targetSide">The target side.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAngleLawOfSines(double knownSide, double knownAngle, double targetSide)
        {
            EnsurePositive(knownSide, nameof(knownSide));
            EnsurePositive(targetSide, nameof(targetSide));
            EnsureAngle(knownAngle);
            double sine = targetSide * Math.Sin(knownAngle) / knownSide;
            return Math.Asin(ClampUnitCircle(sine, nameof(targetSide)));
        }

        /// <summary>
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngle">The known angle.</param>
        /// <param name="targetSide">The target side.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAngleLawOfSines(float knownSide, float knownAngle, float targetSide)
        {
            return (float)CalculateTriangleAngleLawOfSines((double)knownSide, (double)knownAngle, (double)targetSide);
        }

        /// <summary>
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngleDegrees">The known angle degrees.</param>
        /// <param name="targetSide">The target side.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAngleLawOfSinesDegrees(double knownSide, double knownAngleDegrees, double targetSide)
        {
            return RadiansToDegrees(CalculateTriangleAngleLawOfSines(knownSide, DegreesToRadians(knownAngleDegrees), targetSide));
        }

        /// <summary>
        /// </summary>
        /// <param name="knownSide">The known side.</param>
        /// <param name="knownAngleDegrees">The known angle degrees.</param>
        /// <param name="targetSide">The target side.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAngleLawOfSinesDegrees(float knownSide, float knownAngleDegrees, float targetSide)
        {
            return (float)CalculateTriangleAngleLawOfSinesDegrees((double)knownSide, (double)knownAngleDegrees, (double)targetSide);
        }

        /// <summary>
        /// Computes a triangle side length from two adjacent sides and their included angle in radians using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngle">The included angle.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleSideLawOfCosines(double sideA, double sideB, double includedAngle)
        {
            EnsurePositive(sideA, nameof(sideA));
            EnsurePositive(sideB, nameof(sideB));
            EnsureAngle(includedAngle);
            double radicand = (sideA * sideA) + (sideB * sideB) - (2.0 * sideA * sideB * Math.Cos(includedAngle));
            if (radicand < 0.0)
            {
                radicand = 0.0;
            }
            return Math.Sqrt(radicand);
        }

        /// <summary>
        /// Computes a triangle side length from two adjacent sides and their included angle in radians using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngle">The included angle.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleSideLawOfCosines(float sideA, float sideB, float includedAngle)
        {
            return (float)CalculateTriangleSideLawOfCosines((double)sideA, (double)sideB, (double)includedAngle);
        }

        /// <summary>
        /// Computes a triangle side length from two adjacent sides and their included angle in degrees using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngleDegrees">The included angle degrees.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleSideLawOfCosinesDegrees(double sideA, double sideB, double includedAngleDegrees)
        {
            return CalculateTriangleSideLawOfCosines(sideA, sideB, DegreesToRadians(includedAngleDegrees));
        }

        /// <summary>
        /// Computes a triangle side length from two adjacent sides and their included angle in degrees using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngleDegrees">The included angle degrees.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleSideLawOfCosinesDegrees(float sideA, float sideB, float includedAngleDegrees)
        {
            return (float)CalculateTriangleSideLawOfCosinesDegrees((double)sideA, (double)sideB, (double)includedAngleDegrees);
        }

        /// <summary>
        /// Computes a triangle interior angle in radians from three side lengths using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="oppositeSide">The opposite side.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAngleLawOfCosines(double sideA, double sideB, double oppositeSide)
        {
            EnsureTriangleInequality(sideA, sideB, oppositeSide);
            double cosine = ((sideA * sideA) + (sideB * sideB) - (oppositeSide * oppositeSide)) / (2.0 * sideA * sideB);
            return Math.Acos(ClampUnitCircle(cosine, nameof(oppositeSide)));
        }

        /// <summary>
        /// Computes a triangle interior angle in radians from three side lengths using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="oppositeSide">The opposite side.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAngleLawOfCosines(float sideA, float sideB, float oppositeSide)
        {
            return (float)CalculateTriangleAngleLawOfCosines((double)sideA, (double)sideB, (double)oppositeSide);
        }

        /// <summary>
        /// Computes a triangle interior angle in degrees from three side lengths using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="oppositeSide">The opposite side.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAngleLawOfCosinesDegrees(double sideA, double sideB, double oppositeSide)
        {
            return RadiansToDegrees(CalculateTriangleAngleLawOfCosines(sideA, sideB, oppositeSide));
        }

        /// <summary>
        /// Computes a triangle interior angle in degrees from three side lengths using the Law of Cosines (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="oppositeSide">The opposite side.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAngleLawOfCosinesDegrees(float sideA, float sideB, float oppositeSide)
        {
            return (float)CalculateTriangleAngleLawOfCosinesDegrees((double)sideA, (double)sideB, (double)oppositeSide);
        }

        /// <summary>
        /// Computes triangle area from two sides and their included angle in radians (0.5 * a * b * sin(?), single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngle">The included angle.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAreaSas(double sideA, double sideB, double includedAngle)
        {
            EnsurePositive(sideA, nameof(sideA));
            EnsurePositive(sideB, nameof(sideB));
            EnsureAngle(includedAngle);
            return 0.5 * sideA * sideB * Math.Sin(includedAngle);
        }

        /// <summary>
        /// Computes triangle area from two sides and their included angle in radians (0.5 * a * b * sin(?), single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngle">The included angle.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAreaSas(float sideA, float sideB, float includedAngle)
        {
            return (float)CalculateTriangleAreaSas((double)sideA, (double)sideB, (double)includedAngle);
        }

        /// <summary>
        /// Computes triangle area from two sides and their included angle in degrees (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngleDegrees">The included angle degrees.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAreaSasDegrees(double sideA, double sideB, double includedAngleDegrees)
        {
            return CalculateTriangleAreaSas(sideA, sideB, DegreesToRadians(includedAngleDegrees));
        }

        /// <summary>
        /// Computes triangle area from two sides and their included angle in degrees (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="includedAngleDegrees">The included angle degrees.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAreaSasDegrees(float sideA, float sideB, float includedAngleDegrees)
        {
            return (float)CalculateTriangleAreaSasDegrees((double)sideA, (double)sideB, (double)includedAngleDegrees);
        }

        /// <summary>
        /// Computes triangle area from base length and altitude (0.5 * base * height, single precision).
        /// </summary>
        /// <param name="baseLength">The base length.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleArea(double baseLength, double height)
        {
            EnsureNonNegative(baseLength, nameof(baseLength));
            EnsureNonNegative(height, nameof(height));
            return 0.5 * baseLength * height;
        }

        /// <summary>
        /// Computes triangle area from base length and altitude (0.5 * base * height, single precision).
        /// </summary>
        /// <param name="baseLength">The base length.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleArea(float baseLength, float height)
        {
            return (float)CalculateTriangleArea((double)baseLength, (double)height);
        }

        /// <summary>
        /// Computes triangle area from three side lengths using Heron's formula (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAreaHeron(double sideA, double sideB, double sideC)
        {
            EnsureTriangleInequality(sideA, sideB, sideC);
            double semiPerimeter = (sideA + sideB + sideC) / 2.0;
            return Math.Sqrt(semiPerimeter * (semiPerimeter - sideA) * (semiPerimeter - sideB) * (semiPerimeter - sideC));
        }

        /// <summary>
        /// Computes triangle area from three side lengths using Heron's formula (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAreaHeron(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTriangleAreaHeron((double)sideA, (double)sideB, (double)sideC);
        }

        /// <summary>
        /// Computes the perimeter (a + b + c) of a triangle (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static double CalculateTrianglePerimeter(double sideA, double sideB, double sideC)
        {
            EnsureTriangleInequality(sideA, sideB, sideC);
            return sideA + sideB + sideC;
        }

        /// <summary>
        /// Computes the perimeter (a + b + c) of a triangle (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static float CalculateTrianglePerimeter(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTrianglePerimeter((double)sideA, (double)sideB, (double)sideC);
        }

        /// <summary>
        /// Computes the circumcircle radius of a triangle from its three side lengths (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleCircumradius(double sideA, double sideB, double sideC)
        {
            double area = CalculateTriangleAreaHeron(sideA, sideB, sideC);
            return sideA * sideB * sideC / (4.0 * area);
        }

        /// <summary>
        /// Computes the circumcircle radius of a triangle from its three side lengths (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleCircumradius(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTriangleCircumradius((double)sideA, (double)sideB, (double)sideC);
        }

        /// <summary>
        /// Computes the inscribed circle radius of a triangle from its three side lengths (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleInradius(double sideA, double sideB, double sideC)
        {
            double area = CalculateTriangleAreaHeron(sideA, sideB, sideC);
            double semiPerimeter = (sideA + sideB + sideC) / 2.0;
            return area / semiPerimeter;
        }

        /// <summary>
        /// Computes the inscribed circle radius of a triangle from its three side lengths (single precision).
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleInradius(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTriangleInradius((double)sideA, (double)sideB, (double)sideC);
        }

        /// <summary>
        /// Computes the area of an equilateral triangle from its side length ((?3 / 4) * side , single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleAreaEquilateral(double side)
        {
            EnsurePositive(side, nameof(side));
            return Math.Sqrt(3.0) * side * side / 4.0;
        }

        /// <summary>
        /// Computes the area of an equilateral triangle from its side length ((?3 / 4) * side , single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleAreaEquilateral(float side)
        {
            return (float)CalculateTriangleAreaEquilateral((double)side);
        }

        /// <summary>
        /// Computes the altitude of an equilateral triangle from its side length ((?3 / 2) * side, single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateTriangleHeightEquilateral(double side)
        {
            EnsurePositive(side, nameof(side));
            return Math.Sqrt(3.0) * side / 2.0;
        }

        /// <summary>
        /// Computes the altitude of an equilateral triangle from its side length ((?3 / 2) * side, single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateTriangleHeightEquilateral(float side)
        {
            return (float)CalculateTriangleHeightEquilateral((double)side);
        }

        /// <summary>
        /// Computes circle area (? * r , single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleArea(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return Math.PI * radius * radius;
        }

        /// <summary>
        /// Computes circle area (? * r , single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleArea(float radius)
        {
            return (float)CalculateCircleArea((double)radius);
        }

        /// <summary>
        /// Computes circle circumference (2 * ? * r, single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleCircumference(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return 2.0 * Math.PI * radius;
        }

        /// <summary>
        /// Computes circle circumference (2 * ? * r, single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleCircumference(float radius)
        {
            return (float)CalculateCircleCircumference((double)radius);
        }

        /// <summary>
        /// Computes circular arc length for a central angle in radians (r * ?, single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleArcLength(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return radius * angleRadians;
        }

        /// <summary>
        /// Computes circular arc length for a central angle in radians (r * ?, single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleArcLength(float radius, float angleRadians)
        {
            return (float)CalculateCircleArcLength((double)radius, (double)angleRadians);
        }

        /// <summary>
        /// Computes circular arc length for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleArcLengthDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleArcLength(radius, DegreesToRadians(angleDegrees));
        }

        /// <summary>
        /// Computes circular arc length for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleArcLengthDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleArcLengthDegrees((double)radius, (double)angleDegrees);
        }

        /// <summary>
        /// Computes circular sector area for a central angle in radians (0.5 * r  * ?, single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleSectorArea(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return 0.5 * radius * radius * angleRadians;
        }

        /// <summary>
        /// Computes circular sector area for a central angle in radians (0.5 * r  * ?, single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleSectorArea(float radius, float angleRadians)
        {
            return (float)CalculateCircleSectorArea((double)radius, (double)angleRadians);
        }

        /// <summary>
        /// Computes circular sector area for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleSectorAreaDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleSectorArea(radius, DegreesToRadians(angleDegrees));
        }

        /// <summary>
        /// Computes circular sector area for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleSectorAreaDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleSectorAreaDegrees((double)radius, (double)angleDegrees);
        }

        /// <summary>
        /// Computes chord length for a central angle in radians (2 * r * sin(? / 2), single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleChord(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return 2.0 * radius * Math.Sin(angleRadians / 2.0);
        }

        /// <summary>
        /// Computes chord length for a central angle in radians (2 * r * sin(? / 2), single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleChord(float radius, float angleRadians)
        {
            return (float)CalculateCircleChord((double)radius, (double)angleRadians);
        }

        /// <summary>
        /// Computes chord length for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleChordDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleChord(radius, DegreesToRadians(angleDegrees));
        }

        /// <summary>
        /// Computes chord length for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleChordDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleChordDegrees((double)radius, (double)angleDegrees);
        }

        /// <summary>
        /// Computes circular segment area for a central angle in radians (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleSegmentArea(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return 0.5 * radius * radius * (angleRadians - Math.Sin(angleRadians));
        }

        /// <summary>
        /// Computes circular segment area for a central angle in radians (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleSegmentArea(float radius, float angleRadians)
        {
            return (float)CalculateCircleSegmentArea((double)radius, (double)angleRadians);
        }

        /// <summary>
        /// Computes circular segment area for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static double CalculateCircleSegmentAreaDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleSegmentArea(radius, DegreesToRadians(angleDegrees));
        }

        /// <summary>
        /// Computes circular segment area for a central angle in degrees (single precision).
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static float CalculateCircleSegmentAreaDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleSegmentAreaDegrees((double)radius, (double)angleDegrees);
        }

        /// <summary>
        /// Computes the area of a circular ring/annulus (? * (R  - r ), single precision).
        /// </summary>
        /// <param name="outerRadius">The outer radius.</param>
        /// <param name="innerRadius">The inner radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateAnnulusArea(double outerRadius, double innerRadius)
        {
            EnsureNonNegative(outerRadius, nameof(outerRadius));
            EnsureNonNegative(innerRadius, nameof(innerRadius));
            if (innerRadius > outerRadius)
            {
                throw new ArgumentException("The inner radius cannot be larger than the outer radius.");
            }
            return Math.PI * ((outerRadius * outerRadius) - (innerRadius * innerRadius));
        }

        /// <summary>
        /// Computes the area of a circular ring/annulus (? * (R  - r ), single precision).
        /// </summary>
        /// <param name="outerRadius">The outer radius.</param>
        /// <param name="innerRadius">The inner radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateAnnulusArea(float outerRadius, float innerRadius)
        {
            return (float)CalculateAnnulusArea((double)outerRadius, (double)innerRadius);
        }

        /// <summary>
        /// Computes rectangle area (length * width, single precision).
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <returns>The result.</returns>
        public static double CalculateRectangleArea(double length, double width)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            return length * width;
        }

        /// <summary>
        /// Computes rectangle area (length * width, single precision).
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <returns>The result.</returns>
        public static float CalculateRectangleArea(float length, float width)
        {
            return (float)CalculateRectangleArea((double)length, (double)width);
        }

        /// <summary>
        /// Computes rectangle perimeter (2 * (length + width), single precision).
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <returns>The result.</returns>
        public static double CalculateRectanglePerimeter(double length, double width)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            return 2.0 * (length + width);
        }

        /// <summary>
        /// Computes rectangle perimeter (2 * (length + width), single precision).
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <returns>The result.</returns>
        public static float CalculateRectanglePerimeter(float length, float width)
        {
            return (float)CalculateRectanglePerimeter((double)length, (double)width);
        }

        /// <summary>
        /// Computes rectangle diagonal length (?(length  + width ), single precision).
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <returns>The result.</returns>
        public static double CalculateRectangleDiagonal(double length, double width)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            return CalculateHypotenuse(length, width);
        }

        /// <summary>
        /// Computes rectangle diagonal length (?(length  + width ), single precision).
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <returns>The result.</returns>
        public static float CalculateRectangleDiagonal(float length, float width)
        {
            return (float)CalculateRectangleDiagonal((double)length, (double)width);
        }

        /// <summary>
        /// Computes square area (side , single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateSquareArea(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return side * side;
        }

        /// <summary>
        /// Computes square area (side , single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateSquareArea(float side)
        {
            return (float)CalculateSquareArea((double)side);
        }

        /// <summary>
        /// Computes square perimeter (4 * side, single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateSquarePerimeter(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return 4.0 * side;
        }

        /// <summary>
        /// Computes square perimeter (4 * side, single precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateSquarePerimeter(float side)
        {
            return (float)CalculateSquarePerimeter((double)side);
        }

        /// <summary>
        /// Computes square diagonal length (side * ?2, double precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateSquareDiagonal(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return Math.Sqrt(2.0) * side;
        }

        /// <summary>
        /// Computes square diagonal length (side * ?2, double precision).
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateSquareDiagonal(float side)
        {
            return (float)CalculateSquareDiagonal((double)side);
        }

        /// <summary>
        /// </summary>
        /// <param name="baseLength">The base length.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateParallelogramArea(double baseLength, double height)
        {
            EnsureNonNegative(baseLength, nameof(baseLength));
            EnsureNonNegative(height, nameof(height));
            return baseLength * height;
        }

        /// <summary>
        /// </summary>
        /// <param name="baseLength">The base length.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateParallelogramArea(float baseLength, float height)
        {
            return (float)CalculateParallelogramArea((double)baseLength, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <returns>The result.</returns>
        public static double CalculateParallelogramPerimeter(double sideA, double sideB)
        {
            EnsureNonNegative(sideA, nameof(sideA));
            EnsureNonNegative(sideB, nameof(sideB));
            return 2.0 * (sideA + sideB);
        }

        /// <summary>
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <returns>The result.</returns>
        public static float CalculateParallelogramPerimeter(float sideA, float sideB)
        {
            return (float)CalculateParallelogramPerimeter((double)sideA, (double)sideB);
        }

        /// <summary>
        /// </summary>
        /// <param name="baseA">The base a.</param>
        /// <param name="baseB">The base b.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateTrapezoidArea(double baseA, double baseB, double height)
        {
            EnsureNonNegative(baseA, nameof(baseA));
            EnsureNonNegative(baseB, nameof(baseB));
            EnsureNonNegative(height, nameof(height));
            return 0.5 * (baseA + baseB) * height;
        }

        /// <summary>
        /// </summary>
        /// <param name="baseA">The base a.</param>
        /// <param name="baseB">The base b.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateTrapezoidArea(float baseA, float baseB, float height)
        {
            return (float)CalculateTrapezoidArea((double)baseA, (double)baseB, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="diagonalA">The diagonal a.</param>
        /// <param name="diagonalB">The diagonal b.</param>
        /// <returns>The result.</returns>
        public static double CalculateRhombusArea(double diagonalA, double diagonalB)
        {
            EnsureNonNegative(diagonalA, nameof(diagonalA));
            EnsureNonNegative(diagonalB, nameof(diagonalB));
            return 0.5 * diagonalA * diagonalB;
        }

        /// <summary>
        /// </summary>
        /// <param name="diagonalA">The diagonal a.</param>
        /// <param name="diagonalB">The diagonal b.</param>
        /// <returns>The result.</returns>
        public static float CalculateRhombusArea(float diagonalA, float diagonalB)
        {
            return (float)CalculateRhombusArea((double)diagonalA, (double)diagonalB);
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateRhombusPerimeter(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return 4.0 * side;
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateRhombusPerimeter(float side)
        {
            return (float)CalculateRhombusPerimeter((double)side);
        }

        /// <summary>
        /// </summary>
        /// <param name="sides">The sides.</param>
        /// <param name="sideLength">The side length.</param>
        /// <returns>The result.</returns>
        public static double CalculateRegularPolygonArea(int sides, double sideLength)
        {
            EnsurePolygonSides(sides);
            EnsurePositive(sideLength, nameof(sideLength));
            return (sides * sideLength * sideLength) / (4.0 * Math.Tan(Math.PI / sides));
        }

        /// <summary>
        /// </summary>
        /// <param name="sides">The sides.</param>
        /// <param name="sideLength">The side length.</param>
        /// <returns>The result.</returns>
        public static float CalculateRegularPolygonArea(int sides, float sideLength)
        {
            return (float)CalculateRegularPolygonArea(sides, (double)sideLength);
        }

        /// <summary>
        /// </summary>
        /// <param name="sides">The sides.</param>
        /// <param name="sideLength">The side length.</param>
        /// <returns>The result.</returns>
        public static double CalculateRegularPolygonPerimeter(int sides, double sideLength)
        {
            EnsurePolygonSides(sides);
            EnsurePositive(sideLength, nameof(sideLength));
            return sides * sideLength;
        }

        /// <summary>
        /// </summary>
        /// <param name="sides">The sides.</param>
        /// <param name="sideLength">The side length.</param>
        /// <returns>The result.</returns>
        public static float CalculateRegularPolygonPerimeter(int sides, float sideLength)
        {
            return (float)CalculateRegularPolygonPerimeter(sides, (double)sideLength);
        }

        /// <summary>
        /// </summary>
        /// <param name="sides">The sides.</param>
        /// <param name="sideLength">The side length.</param>
        /// <returns>The result.</returns>
        public static double CalculateRegularPolygonApothem(int sides, double sideLength)
        {
            EnsurePolygonSides(sides);
            EnsurePositive(sideLength, nameof(sideLength));
            return sideLength / (2.0 * Math.Tan(Math.PI / sides));
        }

        /// <summary>
        /// </summary>
        /// <param name="sides">The sides.</param>
        /// <param name="sideLength">The side length.</param>
        /// <returns>The result.</returns>
        public static float CalculateRegularPolygonApothem(int sides, float sideLength)
        {
            return (float)CalculateRegularPolygonApothem(sides, (double)sideLength);
        }

        /// <summary>
        /// </summary>
        /// <param name="semiMajorAxis">The semi major axis.</param>
        /// <param name="semiMinorAxis">The semi minor axis.</param>
        /// <returns>The result.</returns>
        public static double CalculateEllipseArea(double semiMajorAxis, double semiMinorAxis)
        {
            EnsurePositive(semiMajorAxis, nameof(semiMajorAxis));
            EnsurePositive(semiMinorAxis, nameof(semiMinorAxis));
            return Math.PI * semiMajorAxis * semiMinorAxis;
        }

        /// <summary>
        /// </summary>
        /// <param name="semiMajorAxis">The semi major axis.</param>
        /// <param name="semiMinorAxis">The semi minor axis.</param>
        /// <returns>The result.</returns>
        public static float CalculateEllipseArea(float semiMajorAxis, float semiMinorAxis)
        {
            return (float)CalculateEllipseArea((double)semiMajorAxis, (double)semiMinorAxis);
        }

        /// <summary>
        /// </summary>
        /// <param name="semiMajorAxis">The semi major axis.</param>
        /// <param name="semiMinorAxis">The semi minor axis.</param>
        /// <returns>The result.</returns>
        public static double CalculateEllipsePerimeter(double semiMajorAxis, double semiMinorAxis)
        {
            EnsurePositive(semiMajorAxis, nameof(semiMajorAxis));
            EnsurePositive(semiMinorAxis, nameof(semiMinorAxis));
            return Math.PI * ((3.0 * (semiMajorAxis + semiMinorAxis)) - Math.Sqrt(((3.0 * semiMajorAxis) + semiMinorAxis) * (semiMajorAxis + (3.0 * semiMinorAxis))));
        }

        /// <summary>
        /// </summary>
        /// <param name="semiMajorAxis">The semi major axis.</param>
        /// <param name="semiMinorAxis">The semi minor axis.</param>
        /// <returns>The result.</returns>
        public static float CalculateEllipsePerimeter(float semiMajorAxis, float semiMinorAxis)
        {
            return (float)CalculateEllipsePerimeter((double)semiMajorAxis, (double)semiMinorAxis);
        }

        /// <summary>
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <param name="sideD">The side d.</param>
        /// <returns>The result.</returns>
        public static double CalculateCyclicQuadrilateralArea(double sideA, double sideB, double sideC, double sideD)
        {
            EnsurePositive(sideA, nameof(sideA));
            EnsurePositive(sideB, nameof(sideB));
            EnsurePositive(sideC, nameof(sideC));
            EnsurePositive(sideD, nameof(sideD));
            double semiPerimeter = (sideA + sideB + sideC + sideD) / 2.0;
            if (semiPerimeter - sideA <= 0.0 || semiPerimeter - sideB <= 0.0 || semiPerimeter - sideC <= 0.0 || semiPerimeter - sideD <= 0.0)
            {
                throw new ArgumentException("The sides cannot form a cyclic quadrilateral.");
            }
            return Math.Sqrt((semiPerimeter - sideA) * (semiPerimeter - sideB) * (semiPerimeter - sideC) * (semiPerimeter - sideD));
        }

        /// <summary>
        /// </summary>
        /// <param name="sideA">The side a.</param>
        /// <param name="sideB">The side b.</param>
        /// <param name="sideC">The side c.</param>
        /// <param name="sideD">The side d.</param>
        /// <returns>The result.</returns>
        public static float CalculateCyclicQuadrilateralArea(float sideA, float sideB, float sideC, float sideD)
        {
            return (float)CalculateCyclicQuadrilateralArea((double)sideA, (double)sideB, (double)sideC, (double)sideD);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static double CalculateDistance2D(double x1, double y1, double x2, double y2)
        {
            double deltaX = x2 - x1;
            double deltaY = y2 - y1;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static float CalculateDistance2D(float x1, float y1, float x2, float y2)
        {
            return (float)CalculateDistance2D((double)x1, (double)y1, (double)x2, (double)y2);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="z1">The z1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <param name="z2">The z2.</param>
        /// <returns>The result.</returns>
        public static double CalculateDistance3D(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            double deltaX = x2 - x1;
            double deltaY = y2 - y1;
            double deltaZ = z2 - z1;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="z1">The z1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <param name="z2">The z2.</param>
        /// <returns>The result.</returns>
        public static float CalculateDistance3D(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            return (float)CalculateDistance3D((double)x1, (double)y1, (double)z1, (double)x2, (double)y2, (double)z2);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static Vector2 CalculateMidpoint2D(double x1, double y1, double x2, double y2)
        {
            return new Vector2((x1 + x2) / 2.0, (y1 + y2) / 2.0);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static Vector2 CalculateMidpoint2D(float x1, float y1, float x2, float y2)
        {
            return CalculateMidpoint2D((double)x1, (double)y1, (double)x2, (double)y2);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="z1">The z1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <param name="z2">The z2.</param>
        /// <returns>The result.</returns>
        public static Vector3 CalculateMidpoint3D(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            return new Vector3((x1 + x2) / 2.0, (y1 + y2) / 2.0, (z1 + z2) / 2.0);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="z1">The z1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <param name="z2">The z2.</param>
        /// <returns>The result.</returns>
        public static Vector3 CalculateMidpoint3D(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            return CalculateMidpoint3D((double)x1, (double)y1, (double)z1, (double)x2, (double)y2, (double)z2);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static double CalculateSlope(double x1, double y1, double x2, double y2)
        {
            if (x1 == x2)
            {
                throw new ArgumentException("The slope of a vertical line is undefined.");
            }
            return (y2 - y1) / (x2 - x1);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static float CalculateSlope(float x1, float y1, float x2, float y2)
        {
            return (float)CalculateSlope((double)x1, (double)y1, (double)x2, (double)y2);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static double CalculateLineIntercept(double x1, double y1, double x2, double y2)
        {
            double slope = CalculateSlope(x1, y1, x2, y2);
            return y1 - (slope * x1);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <returns>The result.</returns>
        public static float CalculateLineIntercept(float x1, float y1, float x2, float y2)
        {
            return (float)CalculateLineIntercept((double)x1, (double)y1, (double)x2, (double)y2);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The first x coordinate.</param>
        /// <param name="y1">The first y coordinate.</param>
        /// <param name="x2">The second x coordinate.</param>
        /// <param name="y2">The second y coordinate.</param>
        /// <returns>The line equation coefficients.</returns>
        public static (double A, double B, double C) CalculateLineEquation(double x1, double y1, double x2, double y2)
        {
            if (x1 == x2 && y1 == y2)
            {
                throw new ArgumentException("Two distinct points are required to define a line.");
            }
            double a = y1 - y2;
            double b = x2 - x1;
            double c = (x1 * y2) - (x2 * y1);
            return (a, b, c);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The first x coordinate.</param>
        /// <param name="y1">The first y coordinate.</param>
        /// <param name="x2">The second x coordinate.</param>
        /// <param name="y2">The second y coordinate.</param>
        /// <returns>The line equation coefficients.</returns>
        public static (double A, double B, double C) CalculateLineEquation(float x1, float y1, float x2, float y2)
        {
            return CalculateLineEquation((double)x1, (double)y1, (double)x2, (double)y2);
        }

        /// <summary>
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <returns>The result.</returns>
        public static double CalculatePointLineDistance(double x, double y, double a, double b, double c)
        {
            if (a == 0.0 && b == 0.0)
            {
                throw new ArgumentException("The coefficients a and b cannot both be zero.");
            }
            return Math.Abs((a * x) + (b * y) + c) / CalculateVectorMagnitude(new Vector2(a, b));
        }

        /// <summary>
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <returns>The result.</returns>
        public static float CalculatePointLineDistance(float x, float y, float a, float b, float c)
        {
            return (float)CalculatePointLineDistance((double)x, (double)y, (double)a, (double)b, (double)c);
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <param name="t">The t.</param>
        /// <returns>The result.</returns>
        public static Vector2 CalculateSectionPoint(double x1, double y1, double x2, double y2, double t)
        {
            return new Vector2(((1.0 - t) * x1) + (t * x2), ((1.0 - t) * y1) + (t * y2));
        }

        /// <summary>
        /// </summary>
        /// <param name="x1">The x1.</param>
        /// <param name="y1">The y1.</param>
        /// <param name="x2">The x2.</param>
        /// <param name="y2">The y2.</param>
        /// <param name="t">The t.</param>
        /// <returns>The result.</returns>
        public static Vector2 CalculateSectionPoint(float x1, float y1, float x2, float y2, float t)
        {
            return CalculateSectionPoint((double)x1, (double)y1, (double)x2, (double)y2, (double)t);
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleBetweenLines(double slopeA, double slopeB)
        {
            double denominator = 1.0 + (slopeA * slopeB);
            if (Math.Abs(denominator) < 1e-12)
            {
                return Math.PI / 2.0;
            }
            return Math.Atan(Math.Abs((slopeB - slopeA) / denominator));
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <returns>The result.</returns>
        public static float CalculateAngleBetweenLines(float slopeA, float slopeB)
        {
            return (float)CalculateAngleBetweenLines((double)slopeA, (double)slopeB);
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleBetweenLinesDegrees(double slopeA, double slopeB)
        {
            return RadiansToDegrees(CalculateAngleBetweenLines(slopeA, slopeB));
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <returns>The result.</returns>
        public static float CalculateAngleBetweenLinesDegrees(float slopeA, float slopeB)
        {
            return (float)CalculateAngleBetweenLinesDegrees((double)slopeA, (double)slopeB);
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The result.</returns>
        public static bool AreLinesParallel(double slopeA, double slopeB, double tolerance = 1e-9)
        {
            return Math.Abs(slopeA - slopeB) <= tolerance;
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The result.</returns>
        public static bool AreLinesParallel(float slopeA, float slopeB, float tolerance = 1e-6f)
        {
            return AreLinesParallel((double)slopeA, (double)slopeB, (double)tolerance);
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The result.</returns>
        public static bool AreLinesPerpendicular(double slopeA, double slopeB, double tolerance = 1e-9)
        {
            return Math.Abs((slopeA * slopeB) + 1.0) <= tolerance;
        }

        /// <summary>
        /// </summary>
        /// <param name="slopeA">The slope a.</param>
        /// <param name="slopeB">The slope b.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The result.</returns>
        public static bool AreLinesPerpendicular(float slopeA, float slopeB, float tolerance = 1e-6f)
        {
            return AreLinesPerpendicular((double)slopeA, (double)slopeB, (double)tolerance);
        }

        /// <summary>
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <returns>The result.</returns>
        public static double CalculatePolygonAreaShoelace(List<Vector2> vertices)
        {
            if (vertices == null || vertices.Count < 3)
            {
                throw new ArgumentException("A polygon requires at least three vertices.");
            }
            double sum = 0.0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 current = vertices[i];
                Vector2 next = vertices[(i + 1) % vertices.Count];
                sum += (current.X * next.Y) - (next.X * current.Y);
            }
            return Math.Abs(sum) / 2.0;
        }

        /// <summary>
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <returns>The result.</returns>
        public static double CalculatePolygonPerimeter(List<Vector2> vertices)
        {
            if (vertices == null || vertices.Count < 2)
            {
                throw new ArgumentException("A polygon requires at least two vertices.");
            }
            double perimeter = 0.0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 current = vertices[i];
                Vector2 next = vertices[(i + 1) % vertices.Count];
                perimeter += CalculateDistance2D(current.X, current.Y, next.X, next.Y);
            }
            return perimeter;
        }

        /// <summary>
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <returns>The result.</returns>
        public static Vector2 CalculatePolygonCentroid(List<Vector2> vertices)
        {
            if (vertices == null || vertices.Count < 3)
            {
                throw new ArgumentException("A polygon requires at least three vertices.");
            }
            double area = 0.0;
            double centerX = 0.0;
            double centerY = 0.0;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector2 current = vertices[i];
                Vector2 next = vertices[(i + 1) % vertices.Count];
                double cross = (current.X * next.Y) - (next.X * current.Y);
                area += cross;
                centerX += (current.X + next.X) * cross;
                centerY += (current.Y + next.Y) * cross;
            }
            area /= 2.0;
            if (area == 0.0)
            {
                throw new InvalidOperationException("The polygon has zero area, so its centroid is undefined.");
            }
            return new Vector2(centerX / (6.0 * area), centerY / (6.0 * area));
        }

        /// <summary>
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="angleRadians">The angle radians.</param>
        /// <returns>The result.</returns>
        public static Vector2 RotatePoint(Vector2 point, double angleRadians)
        {
            double cosine = Math.Cos(angleRadians);
            double sine = Math.Sin(angleRadians);
            return new Vector2((point.X * cosine) - (point.Y * sine), (point.X * sine) + (point.Y * cosine));
        }

        /// <summary>
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="angleDegrees">The angle degrees.</param>
        /// <returns>The result.</returns>
        public static Vector2 RotatePointDegrees(Vector2 point, double angleDegrees)
        {
            return RotatePoint(point, DegreesToRadians(angleDegrees));
        }

        /// <summary>
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="deltaX">The delta x.</param>
        /// <param name="deltaY">The delta y.</param>
        /// <returns>The result.</returns>
        public static Vector2 TranslatePoint(Vector2 point, double deltaX, double deltaY)
        {
            return new Vector2(point.X + deltaX, point.Y + deltaY);
        }

        /// <summary>
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The result.</returns>
        public static Vector2 ScalePoint(Vector2 point, double scale)
        {
            return new Vector2(point.X * scale, point.Y * scale);
        }

        /// <summary>
        /// </summary>
        /// <param name="point1">The first point.</param>
        /// <param name="point2">The second point.</param>
        /// <param name="point3">The third point.</param>
        /// <returns>The circle center and radius.</returns>
        public static (Vector2 Center, double Radius) CalculateCircleThroughPoints(Vector2 point1, Vector2 point2, Vector2 point3)
        {
            double determinant = 2.0 * ((point1.X * (point2.Y - point3.Y)) + (point2.X * (point3.Y - point1.Y)) + (point3.X * (point1.Y - point2.Y)));
            if (Math.Abs(determinant) < 1e-12)
            {
                throw new ArgumentException("Three non-collinear points are required to define a circle.");
            }
            double point1Squared = (point1.X * point1.X) + (point1.Y * point1.Y);
            double point2Squared = (point2.X * point2.X) + (point2.Y * point2.Y);
            double point3Squared = (point3.X * point3.X) + (point3.Y * point3.Y);
            double centerX = ((point1Squared * (point2.Y - point3.Y)) + (point2Squared * (point3.Y - point1.Y)) + (point3Squared * (point1.Y - point2.Y))) / determinant;
            double centerY = ((point1Squared * (point3.X - point2.X)) + (point2Squared * (point1.X - point3.X)) + (point3Squared * (point2.X - point1.X))) / determinant;
            Vector2 center = new Vector2(centerX, centerY);
            double radius = CalculateDistance2D(centerX, centerY, point1.X, point1.Y);
            return (center, radius);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateSphereVolume(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return (4.0 / 3.0) * Math.PI * radius * radius * radius;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateSphereVolume(float radius)
        {
            return (float)CalculateSphereVolume((double)radius);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateSphereSurfaceArea(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return 4.0 * Math.PI * radius * radius;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateSphereSurfaceArea(float radius)
        {
            return (float)CalculateSphereSurfaceArea((double)radius);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateHemisphereVolume(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return (2.0 / 3.0) * Math.PI * radius * radius * radius;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateHemisphereVolume(float radius)
        {
            return (float)CalculateHemisphereVolume((double)radius);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateHemisphereSurfaceArea(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return 3.0 * Math.PI * radius * radius;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateHemisphereSurfaceArea(float radius)
        {
            return (float)CalculateHemisphereSurfaceArea((double)radius);
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateCubeVolume(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return side * side * side;
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateCubeVolume(float side)
        {
            return (float)CalculateCubeVolume((double)side);
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateCubeSurfaceArea(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return 6.0 * side * side;
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateCubeSurfaceArea(float side)
        {
            return (float)CalculateCubeSurfaceArea((double)side);
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static double CalculateCubeSpaceDiagonal(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return Math.Sqrt(3.0) * side;
        }

        /// <summary>
        /// </summary>
        /// <param name="side">The side.</param>
        /// <returns>The result.</returns>
        public static float CalculateCubeSpaceDiagonal(float side)
        {
            return (float)CalculateCubeSpaceDiagonal((double)side);
        }

        /// <summary>
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateCuboidVolume(double length, double width, double height)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            EnsureNonNegative(height, nameof(height));
            return length * width * height;
        }

        /// <summary>
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateCuboidVolume(float length, float width, float height)
        {
            return (float)CalculateCuboidVolume((double)length, (double)width, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateCuboidSurfaceArea(double length, double width, double height)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            EnsureNonNegative(height, nameof(height));
            return 2.0 * ((length * width) + (length * height) + (width * height));
        }

        /// <summary>
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateCuboidSurfaceArea(float length, float width, float height)
        {
            return (float)CalculateCuboidSurfaceArea((double)length, (double)width, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateCuboidSpaceDiagonal(double length, double width, double height)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            EnsureNonNegative(height, nameof(height));
            return Math.Sqrt((length * length) + (width * width) + (height * height));
        }

        /// <summary>
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateCuboidSpaceDiagonal(float length, float width, float height)
        {
            return (float)CalculateCuboidSpaceDiagonal((double)length, (double)width, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateCylinderVolume(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return Math.PI * radius * radius * height;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateCylinderVolume(float radius, float height)
        {
            return (float)CalculateCylinderVolume((double)radius, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateCylinderLateralSurfaceArea(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return 2.0 * Math.PI * radius * height;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateCylinderLateralSurfaceArea(float radius, float height)
        {
            return (float)CalculateCylinderLateralSurfaceArea((double)radius, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateCylinderTotalSurfaceArea(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return 2.0 * Math.PI * radius * (radius + height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateCylinderTotalSurfaceArea(float radius, float height)
        {
            return (float)CalculateCylinderTotalSurfaceArea((double)radius, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateConeVolume(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return Math.PI * radius * radius * height / 3.0;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateConeVolume(float radius, float height)
        {
            return (float)CalculateConeVolume((double)radius, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateConeSlantHeight(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return CalculateHypotenuse(radius, height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateConeSlantHeight(float radius, float height)
        {
            return (float)CalculateConeSlantHeight((double)radius, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateConeLateralSurfaceArea(double radius, double height)
        {
            double slantHeight = CalculateConeSlantHeight(radius, height);
            return Math.PI * radius * slantHeight;
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateConeLateralSurfaceArea(float radius, float height)
        {
            return (float)CalculateConeLateralSurfaceArea((double)radius, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateConeTotalSurfaceArea(double radius, double height)
        {
            double slantHeight = CalculateConeSlantHeight(radius, height);
            return Math.PI * radius * (radius + slantHeight);
        }

        /// <summary>
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateConeTotalSurfaceArea(float radius, float height)
        {
            return (float)CalculateConeTotalSurfaceArea((double)radius, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="baseSide">The base side.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateSquarePyramidVolume(double baseSide, double height)
        {
            EnsureNonNegative(baseSide, nameof(baseSide));
            EnsureNonNegative(height, nameof(height));
            return baseSide * baseSide * height / 3.0;
        }

        /// <summary>
        /// </summary>
        /// <param name="baseSide">The base side.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateSquarePyramidVolume(float baseSide, float height)
        {
            return (float)CalculateSquarePyramidVolume((double)baseSide, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="baseSide">The base side.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static double CalculateSquarePyramidSurfaceArea(double baseSide, double height)
        {
            EnsureNonNegative(baseSide, nameof(baseSide));
            EnsureNonNegative(height, nameof(height));
            double slantHeight = Math.Sqrt(((baseSide / 2.0) * (baseSide / 2.0)) + (height * height));
            return (baseSide * baseSide) + (2.0 * baseSide * slantHeight);
        }

        /// <summary>
        /// </summary>
        /// <param name="baseSide">The base side.</param>
        /// <param name="height">The height.</param>
        /// <returns>The result.</returns>
        public static float CalculateSquarePyramidSurfaceArea(float baseSide, float height)
        {
            return (float)CalculateSquarePyramidSurfaceArea((double)baseSide, (double)height);
        }

        /// <summary>
        /// </summary>
        /// <param name="majorRadius">The major radius.</param>
        /// <param name="minorRadius">The minor radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateTorusVolume(double majorRadius, double minorRadius)
        {
            EnsureNonNegative(majorRadius, nameof(majorRadius));
            EnsureNonNegative(minorRadius, nameof(minorRadius));
            return 2.0 * Math.PI * Math.PI * majorRadius * minorRadius * minorRadius;
        }

        /// <summary>
        /// </summary>
        /// <param name="majorRadius">The major radius.</param>
        /// <param name="minorRadius">The minor radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateTorusVolume(float majorRadius, float minorRadius)
        {
            return (float)CalculateTorusVolume((double)majorRadius, (double)minorRadius);
        }

        /// <summary>
        /// </summary>
        /// <param name="majorRadius">The major radius.</param>
        /// <param name="minorRadius">The minor radius.</param>
        /// <returns>The result.</returns>
        public static double CalculateTorusSurfaceArea(double majorRadius, double minorRadius)
        {
            EnsureNonNegative(majorRadius, nameof(majorRadius));
            EnsureNonNegative(minorRadius, nameof(minorRadius));
            return 4.0 * Math.PI * Math.PI * majorRadius * minorRadius;
        }

        /// <summary>
        /// </summary>
        /// <param name="majorRadius">The major radius.</param>
        /// <param name="minorRadius">The minor radius.</param>
        /// <returns>The result.</returns>
        public static float CalculateTorusSurfaceArea(float majorRadius, float minorRadius)
        {
            return (float)CalculateTorusSurfaceArea((double)majorRadius, (double)minorRadius);
        }

        /// <summary>
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <param name="d">The d.</param>
        /// <returns>The result.</returns>
        public static double CalculateDistancePointToPlane(double x, double y, double z, double a, double b, double c, double d)
        {
            if (a == 0.0 && b == 0.0 && c == 0.0)
            {
                throw new ArgumentException("The plane coefficients a, b, and c cannot all be zero.");
            }
            return Math.Abs((a * x) + (b * y) + (c * z) + d) / CalculateVectorMagnitude(new Vector3(a, b, c));
        }

        /// <summary>
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <param name="z">The z.</param>
        /// <param name="a">The a.</param>
        /// <param name="b">The b.</param>
        /// <param name="c">The c.</param>
        /// <param name="d">The d.</param>
        /// <returns>The result.</returns>
        public static float CalculateDistancePointToPlane(float x, float y, float z, float a, float b, float c, float d)
        {
            return (float)CalculateDistancePointToPlane((double)x, (double)y, (double)z, (double)a, (double)b, (double)c, (double)d);
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>The result.</returns>
        public static double CalculateVectorMagnitude(Vector2 vector)
        {
            return Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y));
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>The result.</returns>
        public static double CalculateVectorMagnitude(Vector3 vector)
        {
            return Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static Vector2 AddVectors(Vector2 vectorA, Vector2 vectorB)
        {
            return new Vector2(vectorA.X + vectorB.X, vectorA.Y + vectorB.Y);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static Vector3 AddVectors(Vector3 vectorA, Vector3 vectorB)
        {
            return new Vector3(vectorA.X + vectorB.X, vectorA.Y + vectorB.Y, vectorA.Z + vectorB.Z);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static Vector2 SubtractVectors(Vector2 vectorA, Vector2 vectorB)
        {
            return new Vector2(vectorA.X - vectorB.X, vectorA.Y - vectorB.Y);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static Vector3 SubtractVectors(Vector3 vectorA, Vector3 vectorB)
        {
            return new Vector3(vectorA.X - vectorB.X, vectorA.Y - vectorB.Y, vectorA.Z - vectorB.Z);
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The result.</returns>
        public static Vector2 ScaleVector(Vector2 vector, double scale)
        {
            return new Vector2(vector.X * scale, vector.Y * scale);
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>The result.</returns>
        public static Vector3 ScaleVector(Vector3 vector, double scale)
        {
            return new Vector3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateDotProduct(Vector2 vectorA, Vector2 vectorB)
        {
            return (vectorA.X * vectorB.X) + (vectorA.Y * vectorB.Y);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateDotProduct(Vector3 vectorA, Vector3 vectorB)
        {
            return (vectorA.X * vectorB.X) + (vectorA.Y * vectorB.Y) + (vectorA.Z * vectorB.Z);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static Vector3 CalculateCrossProduct(Vector3 vectorA, Vector3 vectorB)
        {
            return new Vector3(
                (vectorA.Y * vectorB.Z) - (vectorA.Z * vectorB.Y),
                (vectorA.Z * vectorB.X) - (vectorA.X * vectorB.Z),
                (vectorA.X * vectorB.Y) - (vectorA.Y * vectorB.X));
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>The result.</returns>
        public static Vector2 NormalizeVector(Vector2 vector)
        {
            double magnitude = CalculateVectorMagnitude(vector);
            if (magnitude == 0.0)
            {
                throw new DivideByZeroException("Cannot normalize a zero vector.");
            }
            return new Vector2(vector.X / magnitude, vector.Y / magnitude);
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <returns>The result.</returns>
        public static Vector3 NormalizeVector(Vector3 vector)
        {
            double magnitude = CalculateVectorMagnitude(vector);
            if (magnitude == 0.0)
            {
                throw new DivideByZeroException("Cannot normalize a zero vector.");
            }
            return new Vector3(vector.X / magnitude, vector.Y / magnitude, vector.Z / magnitude);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleBetweenVectors(Vector2 vectorA, Vector2 vectorB)
        {
            double magnitudeA = CalculateVectorMagnitude(vectorA);
            double magnitudeB = CalculateVectorMagnitude(vectorB);
            if (magnitudeA == 0.0 || magnitudeB == 0.0)
            {
                throw new DivideByZeroException("The angle between vectors is undefined when either vector is zero.");
            }
            double cosine = CalculateDotProduct(vectorA, vectorB) / (magnitudeA * magnitudeB);
            return Math.Acos(ClampUnitCircle(cosine, nameof(vectorB)));
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleBetweenVectors(Vector3 vectorA, Vector3 vectorB)
        {
            double magnitudeA = CalculateVectorMagnitude(vectorA);
            double magnitudeB = CalculateVectorMagnitude(vectorB);
            if (magnitudeA == 0.0 || magnitudeB == 0.0)
            {
                throw new DivideByZeroException("The angle between vectors is undefined when either vector is zero.");
            }
            double cosine = CalculateDotProduct(vectorA, vectorB) / (magnitudeA * magnitudeB);
            return Math.Acos(ClampUnitCircle(cosine, nameof(vectorB)));
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleBetweenVectorsDegrees(Vector2 vectorA, Vector2 vectorB)
        {
            return RadiansToDegrees(CalculateAngleBetweenVectors(vectorA, vectorB));
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateAngleBetweenVectorsDegrees(Vector3 vectorA, Vector3 vectorB)
        {
            return RadiansToDegrees(CalculateAngleBetweenVectors(vectorA, vectorB));
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="onto">The onto.</param>
        /// <returns>The result.</returns>
        public static Vector2 CalculateVectorProjection(Vector2 vector, Vector2 onto)
        {
            double denominator = CalculateDotProduct(onto, onto);
            if (denominator == 0.0)
            {
                throw new DivideByZeroException("Cannot project onto a zero vector.");
            }
            return ScaleVector(onto, CalculateDotProduct(vector, onto) / denominator);
        }

        /// <summary>
        /// </summary>
        /// <param name="vector">The vector.</param>
        /// <param name="onto">The onto.</param>
        /// <returns>The result.</returns>
        public static Vector3 CalculateVectorProjection(Vector3 vector, Vector3 onto)
        {
            double denominator = CalculateDotProduct(onto, onto);
            if (denominator == 0.0)
            {
                throw new DivideByZeroException("Cannot project onto a zero vector.");
            }
            return ScaleVector(onto, CalculateDotProduct(vector, onto) / denominator);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateVectorDistance(Vector2 vectorA, Vector2 vectorB)
        {
            return CalculateDistance2D(vectorA.X, vectorA.Y, vectorB.X, vectorB.Y);
        }

        /// <summary>
        /// </summary>
        /// <param name="vectorA">The vector a.</param>
        /// <param name="vectorB">The vector b.</param>
        /// <returns>The result.</returns>
        public static double CalculateVectorDistance(Vector3 vectorA, Vector3 vectorB)
        {
            return CalculateDistance3D(vectorA.X, vectorA.Y, vectorA.Z, vectorB.X, vectorB.Y, vectorB.Z);
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        private static void EnsurePositive(double value, string parameterName)
        {
            if (value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive.");
            }
        }

        private static void EnsureNonNegative(double value, string parameterName)
        {
            if (value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value cannot be negative.");
            }
        }

        private static void EnsureUnitCircle(double value, string parameterName)
        {
            if (value < -1.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be between -1 and 1.");
            }
        }

        private static double ClampUnitCircle(double value, string parameterName)
        {
            if (value < -1.0 - 1e-12 || value > 1.0 + 1e-12)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be between -1 and 1.");
            }
            if (value < -1.0)
            {
                return -1.0;
            }
            if (value > 1.0)
            {
                return 1.0;
            }
            return value;
        }

        private static void EnsureAngle(double radians)
        {
            if (radians <= 0.0 || radians >= Math.PI)
            {
                throw new ArgumentOutOfRangeException(nameof(radians), "Angle must be between 0 and pi radians.");
            }
        }

        private static void EnsureAngleDegrees(double degrees)
        {
            if (degrees <= 0.0 || degrees >= 180.0)
            {
                throw new ArgumentOutOfRangeException(nameof(degrees), "Angle must be between 0 and 180 degrees.");
            }
        }

        private static void EnsureAngleRange(double radians)
        {
            if (radians < 0.0 || radians > (2.0 * Math.PI))
            {
                throw new ArgumentOutOfRangeException(nameof(radians), "Angle must be between 0 and 2 pi radians.");
            }
        }

        private static void EnsureTriangleInequality(double sideA, double sideB, double sideC)
        {
            EnsurePositive(sideA, nameof(sideA));
            EnsurePositive(sideB, nameof(sideB));
            EnsurePositive(sideC, nameof(sideC));
            if (sideA + sideB <= sideC || sideA + sideC <= sideB || sideB + sideC <= sideA)
            {
                throw new ArgumentException("The sides do not satisfy the triangle inequality.");
            }
        }

        private static void EnsurePolygonSides(int sides)
        {
            if (sides < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(sides), "A regular polygon must have at least three sides.");
            }
        }
    }
}
