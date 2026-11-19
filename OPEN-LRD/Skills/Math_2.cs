using System;
using System.Collections.Generic;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal static class Math_2
    {
        public static double AsinDouble(double value)
        {
            EnsureUnitCircle(value, nameof(value));
            return Math.Asin(value);
        }

        public static float AsinFloat(float value)
        {
            return (float)AsinDouble(value);
        }

        public static double AcosDouble(double value)
        {
            EnsureUnitCircle(value, nameof(value));
            return Math.Acos(value);
        }

        public static float AcosFloat(float value)
        {
            return (float)AcosDouble(value);
        }

        public static double AtanDouble(double value)
        {
            return Math.Atan(value);
        }

        public static float AtanFloat(float value)
        {
            return (float)Math.Atan(value);
        }

        public static double SecDouble(double radians)
        {
            double cosine = Math.Cos(radians);
            if (Math.Abs(cosine) < 1e-12)
            {
                throw new DivideByZeroException("The secant is undefined where the cosine is zero.");
            }
            return 1.0 / cosine;
        }

        public static float SecFloat(float radians)
        {
            return (float)SecDouble(radians);
        }

        public static double CscDouble(double radians)
        {
            double sine = Math.Sin(radians);
            if (Math.Abs(sine) < 1e-12)
            {
                throw new DivideByZeroException("The cosecant is undefined where the sine is zero.");
            }
            return 1.0 / sine;
        }

        public static float CscFloat(float radians)
        {
            return (float)CscDouble(radians);
        }

        public static double CotDouble(double radians)
        {
            double sine = Math.Sin(radians);
            if (Math.Abs(sine) < 1e-12)
            {
                throw new DivideByZeroException("The cotangent is undefined where the sine is zero.");
            }
            return Math.Cos(radians) / sine;
        }

        public static float CotFloat(float radians)
        {
            return (float)CotDouble(radians);
        }

        public static double SinhDouble(double value)
        {
            return Math.Sinh(value);
        }

        public static float SinhFloat(float value)
        {
            return (float)Math.Sinh(value);
        }

        public static double CoshDouble(double value)
        {
            return Math.Cosh(value);
        }

        public static float CoshFloat(float value)
        {
            return (float)Math.Cosh(value);
        }

        public static double TanhDouble(double value)
        {
            return Math.Tanh(value);
        }

        public static float TanhFloat(float value)
        {
            return (float)Math.Tanh(value);
        }

        public static double AsinhDouble(double value)
        {
            return Math.Asinh(value);
        }

        public static float AsinhFloat(float value)
        {
            return (float)Math.Asinh(value);
        }

        public static double AcoshDouble(double value)
        {
            if (value < 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The argument of the inverse hyperbolic cosine must be at least one.");
            }
            return Math.Acosh(value);
        }

        public static float AcoshFloat(float value)
        {
            return (float)AcoshDouble(value);
        }

        public static double AtanhDouble(double value)
        {
            if (value <= -1.0 || value >= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The argument of the inverse hyperbolic tangent must be between -1 and 1.");
            }
            return Math.Atanh(value);
        }

        public static float AtanhFloat(float value)
        {
            return (float)AtanhDouble(value);
        }

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

        public static float NormalizeRadians(float radians)
        {
            return (float)NormalizeRadians((double)radians);
        }

        public static double NormalizeDegrees(double degrees)
        {
            double normalized = degrees % 360.0;
            if (normalized < 0.0)
            {
                normalized += 360.0;
            }
            return normalized;
        }

        public static float NormalizeDegrees(float degrees)
        {
            return (float)NormalizeDegrees((double)degrees);
        }

        public static double CalculateAngleDifference(double angleA, double angleB)
        {
            return NormalizeRadians(Math.Abs(angleB - angleA));
        }

        public static float CalculateAngleDifference(float angleA, float angleB)
        {
            return (float)CalculateAngleDifference((double)angleA, (double)angleB);
        }

        public static double CalculateAngleDifferenceDegrees(double angleA, double angleB)
        {
            return NormalizeDegrees(Math.Abs(angleB - angleA));
        }

        public static float CalculateAngleDifferenceDegrees(float angleA, float angleB)
        {
            return (float)CalculateAngleDifferenceDegrees((double)angleA, (double)angleB);
        }

        public static double CalculateHypotenuse(double legA, double legB)
        {
            EnsureNonNegative(legA, nameof(legA));
            EnsureNonNegative(legB, nameof(legB));
            return Math.Sqrt((legA * legA) + (legB * legB));
        }

        public static float CalculateHypotenuse(float legA, float legB)
        {
            return (float)CalculateHypotenuse((double)legA, (double)legB);
        }

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

        public static float CalculateLeg(float hypotenuse, float knownLeg)
        {
            return (float)CalculateLeg((double)hypotenuse, (double)knownLeg);
        }

        public static bool IsRightTriangle(double sideA, double sideB, double sideC, double tolerance = 1e-9)
        {
            EnsureTriangleInequality(sideA, sideB, sideC);
            double[] sides = { sideA, sideB, sideC };
            Array.Sort(sides);
            double difference = (sides[0] * sides[0]) + (sides[1] * sides[1]) - (sides[2] * sides[2]);
            return Math.Abs(difference) <= tolerance;
        }

        public static bool IsRightTriangle(float sideA, float sideB, float sideC, float tolerance = 1e-6f)
        {
            return IsRightTriangle((double)sideA, (double)sideB, (double)sideC, (double)tolerance);
        }

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

        public static float CalculateTriangleThirdAngle(float angleA, float angleB)
        {
            return (float)CalculateTriangleThirdAngle((double)angleA, (double)angleB);
        }

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

        public static float CalculateTriangleThirdAngleDegrees(float angleA, float angleB)
        {
            return (float)CalculateTriangleThirdAngleDegrees((double)angleA, (double)angleB);
        }

        public static double CalculateTriangleSideLawOfSines(double knownSide, double knownAngle, double targetAngle)
        {
            EnsurePositive(knownSide, nameof(knownSide));
            EnsureAngle(knownAngle);
            EnsureAngle(targetAngle);
            return knownSide * Math.Sin(targetAngle) / Math.Sin(knownAngle);
        }

        public static float CalculateTriangleSideLawOfSines(float knownSide, float knownAngle, float targetAngle)
        {
            return (float)CalculateTriangleSideLawOfSines((double)knownSide, (double)knownAngle, (double)targetAngle);
        }

        public static double CalculateTriangleSideLawOfSinesDegrees(double knownSide, double knownAngleDegrees, double targetAngleDegrees)
        {
            return CalculateTriangleSideLawOfSines(knownSide, DegreesToRadians(knownAngleDegrees), DegreesToRadians(targetAngleDegrees));
        }

        public static float CalculateTriangleSideLawOfSinesDegrees(float knownSide, float knownAngleDegrees, float targetAngleDegrees)
        {
            return (float)CalculateTriangleSideLawOfSinesDegrees((double)knownSide, (double)knownAngleDegrees, (double)targetAngleDegrees);
        }

        public static double CalculateTriangleAngleLawOfSines(double knownSide, double knownAngle, double targetSide)
        {
            EnsurePositive(knownSide, nameof(knownSide));
            EnsurePositive(targetSide, nameof(targetSide));
            EnsureAngle(knownAngle);
            double sine = targetSide * Math.Sin(knownAngle) / knownSide;
            return Math.Asin(ClampUnitCircle(sine, nameof(targetSide)));
        }

        public static float CalculateTriangleAngleLawOfSines(float knownSide, float knownAngle, float targetSide)
        {
            return (float)CalculateTriangleAngleLawOfSines((double)knownSide, (double)knownAngle, (double)targetSide);
        }

        public static double CalculateTriangleAngleLawOfSinesDegrees(double knownSide, double knownAngleDegrees, double targetSide)
        {
            return RadiansToDegrees(CalculateTriangleAngleLawOfSines(knownSide, DegreesToRadians(knownAngleDegrees), targetSide));
        }

        public static float CalculateTriangleAngleLawOfSinesDegrees(float knownSide, float knownAngleDegrees, float targetSide)
        {
            return (float)CalculateTriangleAngleLawOfSinesDegrees((double)knownSide, (double)knownAngleDegrees, (double)targetSide);
        }

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

        public static float CalculateTriangleSideLawOfCosines(float sideA, float sideB, float includedAngle)
        {
            return (float)CalculateTriangleSideLawOfCosines((double)sideA, (double)sideB, (double)includedAngle);
        }

        public static double CalculateTriangleSideLawOfCosinesDegrees(double sideA, double sideB, double includedAngleDegrees)
        {
            return CalculateTriangleSideLawOfCosines(sideA, sideB, DegreesToRadians(includedAngleDegrees));
        }

        public static float CalculateTriangleSideLawOfCosinesDegrees(float sideA, float sideB, float includedAngleDegrees)
        {
            return (float)CalculateTriangleSideLawOfCosinesDegrees((double)sideA, (double)sideB, (double)includedAngleDegrees);
        }

        public static double CalculateTriangleAngleLawOfCosines(double sideA, double sideB, double oppositeSide)
        {
            EnsureTriangleInequality(sideA, sideB, oppositeSide);
            double cosine = ((sideA * sideA) + (sideB * sideB) - (oppositeSide * oppositeSide)) / (2.0 * sideA * sideB);
            return Math.Acos(ClampUnitCircle(cosine, nameof(oppositeSide)));
        }

        public static float CalculateTriangleAngleLawOfCosines(float sideA, float sideB, float oppositeSide)
        {
            return (float)CalculateTriangleAngleLawOfCosines((double)sideA, (double)sideB, (double)oppositeSide);
        }

        public static double CalculateTriangleAngleLawOfCosinesDegrees(double sideA, double sideB, double oppositeSide)
        {
            return RadiansToDegrees(CalculateTriangleAngleLawOfCosines(sideA, sideB, oppositeSide));
        }

        public static float CalculateTriangleAngleLawOfCosinesDegrees(float sideA, float sideB, float oppositeSide)
        {
            return (float)CalculateTriangleAngleLawOfCosinesDegrees((double)sideA, (double)sideB, (double)oppositeSide);
        }

        public static double CalculateTriangleAreaSas(double sideA, double sideB, double includedAngle)
        {
            EnsurePositive(sideA, nameof(sideA));
            EnsurePositive(sideB, nameof(sideB));
            EnsureAngle(includedAngle);
            return 0.5 * sideA * sideB * Math.Sin(includedAngle);
        }

        public static float CalculateTriangleAreaSas(float sideA, float sideB, float includedAngle)
        {
            return (float)CalculateTriangleAreaSas((double)sideA, (double)sideB, (double)includedAngle);
        }

        public static double CalculateTriangleAreaSasDegrees(double sideA, double sideB, double includedAngleDegrees)
        {
            return CalculateTriangleAreaSas(sideA, sideB, DegreesToRadians(includedAngleDegrees));
        }

        public static float CalculateTriangleAreaSasDegrees(float sideA, float sideB, float includedAngleDegrees)
        {
            return (float)CalculateTriangleAreaSasDegrees((double)sideA, (double)sideB, (double)includedAngleDegrees);
        }

        public static double CalculateTriangleArea(double baseLength, double height)
        {
            EnsureNonNegative(baseLength, nameof(baseLength));
            EnsureNonNegative(height, nameof(height));
            return 0.5 * baseLength * height;
        }

        public static float CalculateTriangleArea(float baseLength, float height)
        {
            return (float)CalculateTriangleArea((double)baseLength, (double)height);
        }

        public static double CalculateTriangleAreaHeron(double sideA, double sideB, double sideC)
        {
            EnsureTriangleInequality(sideA, sideB, sideC);
            double semiPerimeter = (sideA + sideB + sideC) / 2.0;
            return Math.Sqrt(semiPerimeter * (semiPerimeter - sideA) * (semiPerimeter - sideB) * (semiPerimeter - sideC));
        }

        public static float CalculateTriangleAreaHeron(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTriangleAreaHeron((double)sideA, (double)sideB, (double)sideC);
        }

        public static double CalculateTrianglePerimeter(double sideA, double sideB, double sideC)
        {
            EnsureTriangleInequality(sideA, sideB, sideC);
            return sideA + sideB + sideC;
        }

        public static float CalculateTrianglePerimeter(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTrianglePerimeter((double)sideA, (double)sideB, (double)sideC);
        }

        public static double CalculateTriangleCircumradius(double sideA, double sideB, double sideC)
        {
            double area = CalculateTriangleAreaHeron(sideA, sideB, sideC);
            return sideA * sideB * sideC / (4.0 * area);
        }

        public static float CalculateTriangleCircumradius(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTriangleCircumradius((double)sideA, (double)sideB, (double)sideC);
        }

        public static double CalculateTriangleInradius(double sideA, double sideB, double sideC)
        {
            double area = CalculateTriangleAreaHeron(sideA, sideB, sideC);
            double semiPerimeter = (sideA + sideB + sideC) / 2.0;
            return area / semiPerimeter;
        }

        public static float CalculateTriangleInradius(float sideA, float sideB, float sideC)
        {
            return (float)CalculateTriangleInradius((double)sideA, (double)sideB, (double)sideC);
        }

        public static double CalculateTriangleAreaEquilateral(double side)
        {
            EnsurePositive(side, nameof(side));
            return Math.Sqrt(3.0) * side * side / 4.0;
        }

        public static float CalculateTriangleAreaEquilateral(float side)
        {
            return (float)CalculateTriangleAreaEquilateral((double)side);
        }

        public static double CalculateTriangleHeightEquilateral(double side)
        {
            EnsurePositive(side, nameof(side));
            return Math.Sqrt(3.0) * side / 2.0;
        }

        public static float CalculateTriangleHeightEquilateral(float side)
        {
            return (float)CalculateTriangleHeightEquilateral((double)side);
        }

        public static double CalculateCircleArea(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return Math.PI * radius * radius;
        }

        public static float CalculateCircleArea(float radius)
        {
            return (float)CalculateCircleArea((double)radius);
        }

        public static double CalculateCircleCircumference(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return 2.0 * Math.PI * radius;
        }

        public static float CalculateCircleCircumference(float radius)
        {
            return (float)CalculateCircleCircumference((double)radius);
        }

        public static double CalculateCircleArcLength(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return radius * angleRadians;
        }

        public static float CalculateCircleArcLength(float radius, float angleRadians)
        {
            return (float)CalculateCircleArcLength((double)radius, (double)angleRadians);
        }

        public static double CalculateCircleArcLengthDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleArcLength(radius, DegreesToRadians(angleDegrees));
        }

        public static float CalculateCircleArcLengthDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleArcLengthDegrees((double)radius, (double)angleDegrees);
        }

        public static double CalculateCircleSectorArea(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return 0.5 * radius * radius * angleRadians;
        }

        public static float CalculateCircleSectorArea(float radius, float angleRadians)
        {
            return (float)CalculateCircleSectorArea((double)radius, (double)angleRadians);
        }

        public static double CalculateCircleSectorAreaDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleSectorArea(radius, DegreesToRadians(angleDegrees));
        }

        public static float CalculateCircleSectorAreaDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleSectorAreaDegrees((double)radius, (double)angleDegrees);
        }

        public static double CalculateCircleChord(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return 2.0 * radius * Math.Sin(angleRadians / 2.0);
        }

        public static float CalculateCircleChord(float radius, float angleRadians)
        {
            return (float)CalculateCircleChord((double)radius, (double)angleRadians);
        }

        public static double CalculateCircleChordDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleChord(radius, DegreesToRadians(angleDegrees));
        }

        public static float CalculateCircleChordDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleChordDegrees((double)radius, (double)angleDegrees);
        }

        public static double CalculateCircleSegmentArea(double radius, double angleRadians)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureAngleRange(angleRadians);
            return 0.5 * radius * radius * (angleRadians - Math.Sin(angleRadians));
        }

        public static float CalculateCircleSegmentArea(float radius, float angleRadians)
        {
            return (float)CalculateCircleSegmentArea((double)radius, (double)angleRadians);
        }

        public static double CalculateCircleSegmentAreaDegrees(double radius, double angleDegrees)
        {
            return CalculateCircleSegmentArea(radius, DegreesToRadians(angleDegrees));
        }

        public static float CalculateCircleSegmentAreaDegrees(float radius, float angleDegrees)
        {
            return (float)CalculateCircleSegmentAreaDegrees((double)radius, (double)angleDegrees);
        }

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

        public static float CalculateAnnulusArea(float outerRadius, float innerRadius)
        {
            return (float)CalculateAnnulusArea((double)outerRadius, (double)innerRadius);
        }

        public static double CalculateRectangleArea(double length, double width)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            return length * width;
        }

        public static float CalculateRectangleArea(float length, float width)
        {
            return (float)CalculateRectangleArea((double)length, (double)width);
        }

        public static double CalculateRectanglePerimeter(double length, double width)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            return 2.0 * (length + width);
        }

        public static float CalculateRectanglePerimeter(float length, float width)
        {
            return (float)CalculateRectanglePerimeter((double)length, (double)width);
        }

        public static double CalculateRectangleDiagonal(double length, double width)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            return CalculateHypotenuse(length, width);
        }

        public static float CalculateRectangleDiagonal(float length, float width)
        {
            return (float)CalculateRectangleDiagonal((double)length, (double)width);
        }

        public static double CalculateSquareArea(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return side * side;
        }

        public static float CalculateSquareArea(float side)
        {
            return (float)CalculateSquareArea((double)side);
        }

        public static double CalculateSquarePerimeter(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return 4.0 * side;
        }

        public static float CalculateSquarePerimeter(float side)
        {
            return (float)CalculateSquarePerimeter((double)side);
        }

        public static double CalculateSquareDiagonal(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return Math.Sqrt(2.0) * side;
        }

        public static float CalculateSquareDiagonal(float side)
        {
            return (float)CalculateSquareDiagonal((double)side);
        }

        public static double CalculateParallelogramArea(double baseLength, double height)
        {
            EnsureNonNegative(baseLength, nameof(baseLength));
            EnsureNonNegative(height, nameof(height));
            return baseLength * height;
        }

        public static float CalculateParallelogramArea(float baseLength, float height)
        {
            return (float)CalculateParallelogramArea((double)baseLength, (double)height);
        }

        public static double CalculateParallelogramPerimeter(double sideA, double sideB)
        {
            EnsureNonNegative(sideA, nameof(sideA));
            EnsureNonNegative(sideB, nameof(sideB));
            return 2.0 * (sideA + sideB);
        }

        public static float CalculateParallelogramPerimeter(float sideA, float sideB)
        {
            return (float)CalculateParallelogramPerimeter((double)sideA, (double)sideB);
        }

        public static double CalculateTrapezoidArea(double baseA, double baseB, double height)
        {
            EnsureNonNegative(baseA, nameof(baseA));
            EnsureNonNegative(baseB, nameof(baseB));
            EnsureNonNegative(height, nameof(height));
            return 0.5 * (baseA + baseB) * height;
        }

        public static float CalculateTrapezoidArea(float baseA, float baseB, float height)
        {
            return (float)CalculateTrapezoidArea((double)baseA, (double)baseB, (double)height);
        }

        public static double CalculateRhombusArea(double diagonalA, double diagonalB)
        {
            EnsureNonNegative(diagonalA, nameof(diagonalA));
            EnsureNonNegative(diagonalB, nameof(diagonalB));
            return 0.5 * diagonalA * diagonalB;
        }

        public static float CalculateRhombusArea(float diagonalA, float diagonalB)
        {
            return (float)CalculateRhombusArea((double)diagonalA, (double)diagonalB);
        }

        public static double CalculateRhombusPerimeter(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return 4.0 * side;
        }

        public static float CalculateRhombusPerimeter(float side)
        {
            return (float)CalculateRhombusPerimeter((double)side);
        }

        public static double CalculateRegularPolygonArea(int sides, double sideLength)
        {
            EnsurePolygonSides(sides);
            EnsurePositive(sideLength, nameof(sideLength));
            return (sides * sideLength * sideLength) / (4.0 * Math.Tan(Math.PI / sides));
        }

        public static float CalculateRegularPolygonArea(int sides, float sideLength)
        {
            return (float)CalculateRegularPolygonArea(sides, (double)sideLength);
        }

        public static double CalculateRegularPolygonPerimeter(int sides, double sideLength)
        {
            EnsurePolygonSides(sides);
            EnsurePositive(sideLength, nameof(sideLength));
            return sides * sideLength;
        }

        public static float CalculateRegularPolygonPerimeter(int sides, float sideLength)
        {
            return (float)CalculateRegularPolygonPerimeter(sides, (double)sideLength);
        }

        public static double CalculateRegularPolygonApothem(int sides, double sideLength)
        {
            EnsurePolygonSides(sides);
            EnsurePositive(sideLength, nameof(sideLength));
            return sideLength / (2.0 * Math.Tan(Math.PI / sides));
        }

        public static float CalculateRegularPolygonApothem(int sides, float sideLength)
        {
            return (float)CalculateRegularPolygonApothem(sides, (double)sideLength);
        }

        public static double CalculateEllipseArea(double semiMajorAxis, double semiMinorAxis)
        {
            EnsurePositive(semiMajorAxis, nameof(semiMajorAxis));
            EnsurePositive(semiMinorAxis, nameof(semiMinorAxis));
            return Math.PI * semiMajorAxis * semiMinorAxis;
        }

        public static float CalculateEllipseArea(float semiMajorAxis, float semiMinorAxis)
        {
            return (float)CalculateEllipseArea((double)semiMajorAxis, (double)semiMinorAxis);
        }

        public static double CalculateEllipsePerimeter(double semiMajorAxis, double semiMinorAxis)
        {
            EnsurePositive(semiMajorAxis, nameof(semiMajorAxis));
            EnsurePositive(semiMinorAxis, nameof(semiMinorAxis));
            return Math.PI * ((3.0 * (semiMajorAxis + semiMinorAxis)) - Math.Sqrt(((3.0 * semiMajorAxis) + semiMinorAxis) * (semiMajorAxis + (3.0 * semiMinorAxis))));
        }

        public static float CalculateEllipsePerimeter(float semiMajorAxis, float semiMinorAxis)
        {
            return (float)CalculateEllipsePerimeter((double)semiMajorAxis, (double)semiMinorAxis);
        }

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

        public static float CalculateCyclicQuadrilateralArea(float sideA, float sideB, float sideC, float sideD)
        {
            return (float)CalculateCyclicQuadrilateralArea((double)sideA, (double)sideB, (double)sideC, (double)sideD);
        }

        public static double CalculateDistance2D(double x1, double y1, double x2, double y2)
        {
            double deltaX = x2 - x1;
            double deltaY = y2 - y1;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        }

        public static float CalculateDistance2D(float x1, float y1, float x2, float y2)
        {
            return (float)CalculateDistance2D((double)x1, (double)y1, (double)x2, (double)y2);
        }

        public static double CalculateDistance3D(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            double deltaX = x2 - x1;
            double deltaY = y2 - y1;
            double deltaZ = z2 - z1;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
        }

        public static float CalculateDistance3D(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            return (float)CalculateDistance3D((double)x1, (double)y1, (double)z1, (double)x2, (double)y2, (double)z2);
        }

        public static Vector2 CalculateMidpoint2D(double x1, double y1, double x2, double y2)
        {
            return new Vector2((x1 + x2) / 2.0, (y1 + y2) / 2.0);
        }

        public static Vector2 CalculateMidpoint2D(float x1, float y1, float x2, float y2)
        {
            return CalculateMidpoint2D((double)x1, (double)y1, (double)x2, (double)y2);
        }

        public static Vector3 CalculateMidpoint3D(double x1, double y1, double z1, double x2, double y2, double z2)
        {
            return new Vector3((x1 + x2) / 2.0, (y1 + y2) / 2.0, (z1 + z2) / 2.0);
        }

        public static Vector3 CalculateMidpoint3D(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            return CalculateMidpoint3D((double)x1, (double)y1, (double)z1, (double)x2, (double)y2, (double)z2);
        }

        public static double CalculateSlope(double x1, double y1, double x2, double y2)
        {
            if (x1 == x2)
            {
                throw new ArgumentException("The slope of a vertical line is undefined.");
            }
            return (y2 - y1) / (x2 - x1);
        }

        public static float CalculateSlope(float x1, float y1, float x2, float y2)
        {
            return (float)CalculateSlope((double)x1, (double)y1, (double)x2, (double)y2);
        }

        public static double CalculateLineIntercept(double x1, double y1, double x2, double y2)
        {
            double slope = CalculateSlope(x1, y1, x2, y2);
            return y1 - (slope * x1);
        }

        public static float CalculateLineIntercept(float x1, float y1, float x2, float y2)
        {
            return (float)CalculateLineIntercept((double)x1, (double)y1, (double)x2, (double)y2);
        }

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

        public static (double A, double B, double C) CalculateLineEquation(float x1, float y1, float x2, float y2)
        {
            return CalculateLineEquation((double)x1, (double)y1, (double)x2, (double)y2);
        }

        public static double CalculatePointLineDistance(double x, double y, double a, double b, double c)
        {
            if (a == 0.0 && b == 0.0)
            {
                throw new ArgumentException("The coefficients a and b cannot both be zero.");
            }
            return Math.Abs((a * x) + (b * y) + c) / CalculateVectorMagnitude(new Vector2(a, b));
        }

        public static float CalculatePointLineDistance(float x, float y, float a, float b, float c)
        {
            return (float)CalculatePointLineDistance((double)x, (double)y, (double)a, (double)b, (double)c);
        }

        public static Vector2 CalculateSectionPoint(double x1, double y1, double x2, double y2, double t)
        {
            return new Vector2(((1.0 - t) * x1) + (t * x2), ((1.0 - t) * y1) + (t * y2));
        }

        public static Vector2 CalculateSectionPoint(float x1, float y1, float x2, float y2, float t)
        {
            return CalculateSectionPoint((double)x1, (double)y1, (double)x2, (double)y2, (double)t);
        }

        public static double CalculateAngleBetweenLines(double slopeA, double slopeB)
        {
            double denominator = 1.0 + (slopeA * slopeB);
            if (Math.Abs(denominator) < 1e-12)
            {
                return Math.PI / 2.0;
            }
            return Math.Atan(Math.Abs((slopeB - slopeA) / denominator));
        }

        public static float CalculateAngleBetweenLines(float slopeA, float slopeB)
        {
            return (float)CalculateAngleBetweenLines((double)slopeA, (double)slopeB);
        }

        public static double CalculateAngleBetweenLinesDegrees(double slopeA, double slopeB)
        {
            return RadiansToDegrees(CalculateAngleBetweenLines(slopeA, slopeB));
        }

        public static float CalculateAngleBetweenLinesDegrees(float slopeA, float slopeB)
        {
            return (float)CalculateAngleBetweenLinesDegrees((double)slopeA, (double)slopeB);
        }

        public static bool AreLinesParallel(double slopeA, double slopeB, double tolerance = 1e-9)
        {
            return Math.Abs(slopeA - slopeB) <= tolerance;
        }

        public static bool AreLinesParallel(float slopeA, float slopeB, float tolerance = 1e-6f)
        {
            return AreLinesParallel((double)slopeA, (double)slopeB, (double)tolerance);
        }

        public static bool AreLinesPerpendicular(double slopeA, double slopeB, double tolerance = 1e-9)
        {
            return Math.Abs((slopeA * slopeB) + 1.0) <= tolerance;
        }

        public static bool AreLinesPerpendicular(float slopeA, float slopeB, float tolerance = 1e-6f)
        {
            return AreLinesPerpendicular((double)slopeA, (double)slopeB, (double)tolerance);
        }

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

        public static Vector2 RotatePoint(Vector2 point, double angleRadians)
        {
            double cosine = Math.Cos(angleRadians);
            double sine = Math.Sin(angleRadians);
            return new Vector2((point.X * cosine) - (point.Y * sine), (point.X * sine) + (point.Y * cosine));
        }

        public static Vector2 RotatePointDegrees(Vector2 point, double angleDegrees)
        {
            return RotatePoint(point, DegreesToRadians(angleDegrees));
        }

        public static Vector2 TranslatePoint(Vector2 point, double deltaX, double deltaY)
        {
            return new Vector2(point.X + deltaX, point.Y + deltaY);
        }

        public static Vector2 ScalePoint(Vector2 point, double scale)
        {
            return new Vector2(point.X * scale, point.Y * scale);
        }

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

        public static double CalculateSphereVolume(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return (4.0 / 3.0) * Math.PI * radius * radius * radius;
        }

        public static float CalculateSphereVolume(float radius)
        {
            return (float)CalculateSphereVolume((double)radius);
        }

        public static double CalculateSphereSurfaceArea(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return 4.0 * Math.PI * radius * radius;
        }

        public static float CalculateSphereSurfaceArea(float radius)
        {
            return (float)CalculateSphereSurfaceArea((double)radius);
        }

        public static double CalculateHemisphereVolume(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return (2.0 / 3.0) * Math.PI * radius * radius * radius;
        }

        public static float CalculateHemisphereVolume(float radius)
        {
            return (float)CalculateHemisphereVolume((double)radius);
        }

        public static double CalculateHemisphereSurfaceArea(double radius)
        {
            EnsureNonNegative(radius, nameof(radius));
            return 3.0 * Math.PI * radius * radius;
        }

        public static float CalculateHemisphereSurfaceArea(float radius)
        {
            return (float)CalculateHemisphereSurfaceArea((double)radius);
        }

        public static double CalculateCubeVolume(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return side * side * side;
        }

        public static float CalculateCubeVolume(float side)
        {
            return (float)CalculateCubeVolume((double)side);
        }

        public static double CalculateCubeSurfaceArea(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return 6.0 * side * side;
        }

        public static float CalculateCubeSurfaceArea(float side)
        {
            return (float)CalculateCubeSurfaceArea((double)side);
        }

        public static double CalculateCubeSpaceDiagonal(double side)
        {
            EnsureNonNegative(side, nameof(side));
            return Math.Sqrt(3.0) * side;
        }

        public static float CalculateCubeSpaceDiagonal(float side)
        {
            return (float)CalculateCubeSpaceDiagonal((double)side);
        }

        public static double CalculateCuboidVolume(double length, double width, double height)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            EnsureNonNegative(height, nameof(height));
            return length * width * height;
        }

        public static float CalculateCuboidVolume(float length, float width, float height)
        {
            return (float)CalculateCuboidVolume((double)length, (double)width, (double)height);
        }

        public static double CalculateCuboidSurfaceArea(double length, double width, double height)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            EnsureNonNegative(height, nameof(height));
            return 2.0 * ((length * width) + (length * height) + (width * height));
        }

        public static float CalculateCuboidSurfaceArea(float length, float width, float height)
        {
            return (float)CalculateCuboidSurfaceArea((double)length, (double)width, (double)height);
        }

        public static double CalculateCuboidSpaceDiagonal(double length, double width, double height)
        {
            EnsureNonNegative(length, nameof(length));
            EnsureNonNegative(width, nameof(width));
            EnsureNonNegative(height, nameof(height));
            return Math.Sqrt((length * length) + (width * width) + (height * height));
        }

        public static float CalculateCuboidSpaceDiagonal(float length, float width, float height)
        {
            return (float)CalculateCuboidSpaceDiagonal((double)length, (double)width, (double)height);
        }

        public static double CalculateCylinderVolume(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return Math.PI * radius * radius * height;
        }

        public static float CalculateCylinderVolume(float radius, float height)
        {
            return (float)CalculateCylinderVolume((double)radius, (double)height);
        }

        public static double CalculateCylinderLateralSurfaceArea(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return 2.0 * Math.PI * radius * height;
        }

        public static float CalculateCylinderLateralSurfaceArea(float radius, float height)
        {
            return (float)CalculateCylinderLateralSurfaceArea((double)radius, (double)height);
        }

        public static double CalculateCylinderTotalSurfaceArea(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return 2.0 * Math.PI * radius * (radius + height);
        }

        public static float CalculateCylinderTotalSurfaceArea(float radius, float height)
        {
            return (float)CalculateCylinderTotalSurfaceArea((double)radius, (double)height);
        }

        public static double CalculateConeVolume(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return Math.PI * radius * radius * height / 3.0;
        }

        public static float CalculateConeVolume(float radius, float height)
        {
            return (float)CalculateConeVolume((double)radius, (double)height);
        }

        public static double CalculateConeSlantHeight(double radius, double height)
        {
            EnsureNonNegative(radius, nameof(radius));
            EnsureNonNegative(height, nameof(height));
            return CalculateHypotenuse(radius, height);
        }

        public static float CalculateConeSlantHeight(float radius, float height)
        {
            return (float)CalculateConeSlantHeight((double)radius, (double)height);
        }

        public static double CalculateConeLateralSurfaceArea(double radius, double height)
        {
            double slantHeight = CalculateConeSlantHeight(radius, height);
            return Math.PI * radius * slantHeight;
        }

        public static float CalculateConeLateralSurfaceArea(float radius, float height)
        {
            return (float)CalculateConeLateralSurfaceArea((double)radius, (double)height);
        }

        public static double CalculateConeTotalSurfaceArea(double radius, double height)
        {
            double slantHeight = CalculateConeSlantHeight(radius, height);
            return Math.PI * radius * (radius + slantHeight);
        }

        public static float CalculateConeTotalSurfaceArea(float radius, float height)
        {
            return (float)CalculateConeTotalSurfaceArea((double)radius, (double)height);
        }

        public static double CalculateSquarePyramidVolume(double baseSide, double height)
        {
            EnsureNonNegative(baseSide, nameof(baseSide));
            EnsureNonNegative(height, nameof(height));
            return baseSide * baseSide * height / 3.0;
        }

        public static float CalculateSquarePyramidVolume(float baseSide, float height)
        {
            return (float)CalculateSquarePyramidVolume((double)baseSide, (double)height);
        }

        public static double CalculateSquarePyramidSurfaceArea(double baseSide, double height)
        {
            EnsureNonNegative(baseSide, nameof(baseSide));
            EnsureNonNegative(height, nameof(height));
            double slantHeight = Math.Sqrt(((baseSide / 2.0) * (baseSide / 2.0)) + (height * height));
            return (baseSide * baseSide) + (2.0 * baseSide * slantHeight);
        }

        public static float CalculateSquarePyramidSurfaceArea(float baseSide, float height)
        {
            return (float)CalculateSquarePyramidSurfaceArea((double)baseSide, (double)height);
        }

        public static double CalculateTorusVolume(double majorRadius, double minorRadius)
        {
            EnsureNonNegative(majorRadius, nameof(majorRadius));
            EnsureNonNegative(minorRadius, nameof(minorRadius));
            return 2.0 * Math.PI * Math.PI * majorRadius * minorRadius * minorRadius;
        }

        public static float CalculateTorusVolume(float majorRadius, float minorRadius)
        {
            return (float)CalculateTorusVolume((double)majorRadius, (double)minorRadius);
        }

        public static double CalculateTorusSurfaceArea(double majorRadius, double minorRadius)
        {
            EnsureNonNegative(majorRadius, nameof(majorRadius));
            EnsureNonNegative(minorRadius, nameof(minorRadius));
            return 4.0 * Math.PI * Math.PI * majorRadius * minorRadius;
        }

        public static float CalculateTorusSurfaceArea(float majorRadius, float minorRadius)
        {
            return (float)CalculateTorusSurfaceArea((double)majorRadius, (double)minorRadius);
        }

        public static double CalculateDistancePointToPlane(double x, double y, double z, double a, double b, double c, double d)
        {
            if (a == 0.0 && b == 0.0 && c == 0.0)
            {
                throw new ArgumentException("The plane coefficients a, b, and c cannot all be zero.");
            }
            return Math.Abs((a * x) + (b * y) + (c * z) + d) / CalculateVectorMagnitude(new Vector3(a, b, c));
        }

        public static float CalculateDistancePointToPlane(float x, float y, float z, float a, float b, float c, float d)
        {
            return (float)CalculateDistancePointToPlane((double)x, (double)y, (double)z, (double)a, (double)b, (double)c, (double)d);
        }

        public static double CalculateVectorMagnitude(Vector2 vector)
        {
            return Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y));
        }

        public static double CalculateVectorMagnitude(Vector3 vector)
        {
            return Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
        }

        public static Vector2 AddVectors(Vector2 vectorA, Vector2 vectorB)
        {
            return new Vector2(vectorA.X + vectorB.X, vectorA.Y + vectorB.Y);
        }

        public static Vector3 AddVectors(Vector3 vectorA, Vector3 vectorB)
        {
            return new Vector3(vectorA.X + vectorB.X, vectorA.Y + vectorB.Y, vectorA.Z + vectorB.Z);
        }

        public static Vector2 SubtractVectors(Vector2 vectorA, Vector2 vectorB)
        {
            return new Vector2(vectorA.X - vectorB.X, vectorA.Y - vectorB.Y);
        }

        public static Vector3 SubtractVectors(Vector3 vectorA, Vector3 vectorB)
        {
            return new Vector3(vectorA.X - vectorB.X, vectorA.Y - vectorB.Y, vectorA.Z - vectorB.Z);
        }

        public static Vector2 ScaleVector(Vector2 vector, double scale)
        {
            return new Vector2(vector.X * scale, vector.Y * scale);
        }

        public static Vector3 ScaleVector(Vector3 vector, double scale)
        {
            return new Vector3(vector.X * scale, vector.Y * scale, vector.Z * scale);
        }

        public static double CalculateDotProduct(Vector2 vectorA, Vector2 vectorB)
        {
            return (vectorA.X * vectorB.X) + (vectorA.Y * vectorB.Y);
        }

        public static double CalculateDotProduct(Vector3 vectorA, Vector3 vectorB)
        {
            return (vectorA.X * vectorB.X) + (vectorA.Y * vectorB.Y) + (vectorA.Z * vectorB.Z);
        }

        public static Vector3 CalculateCrossProduct(Vector3 vectorA, Vector3 vectorB)
        {
            return new Vector3(
                (vectorA.Y * vectorB.Z) - (vectorA.Z * vectorB.Y),
                (vectorA.Z * vectorB.X) - (vectorA.X * vectorB.Z),
                (vectorA.X * vectorB.Y) - (vectorA.Y * vectorB.X));
        }

        public static Vector2 NormalizeVector(Vector2 vector)
        {
            double magnitude = CalculateVectorMagnitude(vector);
            if (magnitude == 0.0)
            {
                throw new DivideByZeroException("Cannot normalize a zero vector.");
            }
            return new Vector2(vector.X / magnitude, vector.Y / magnitude);
        }

        public static Vector3 NormalizeVector(Vector3 vector)
        {
            double magnitude = CalculateVectorMagnitude(vector);
            if (magnitude == 0.0)
            {
                throw new DivideByZeroException("Cannot normalize a zero vector.");
            }
            return new Vector3(vector.X / magnitude, vector.Y / magnitude, vector.Z / magnitude);
        }

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

        public static double CalculateAngleBetweenVectorsDegrees(Vector2 vectorA, Vector2 vectorB)
        {
            return RadiansToDegrees(CalculateAngleBetweenVectors(vectorA, vectorB));
        }

        public static double CalculateAngleBetweenVectorsDegrees(Vector3 vectorA, Vector3 vectorB)
        {
            return RadiansToDegrees(CalculateAngleBetweenVectors(vectorA, vectorB));
        }

        public static Vector2 CalculateVectorProjection(Vector2 vector, Vector2 onto)
        {
            double denominator = CalculateDotProduct(onto, onto);
            if (denominator == 0.0)
            {
                throw new DivideByZeroException("Cannot project onto a zero vector.");
            }
            return ScaleVector(onto, CalculateDotProduct(vector, onto) / denominator);
        }

        public static Vector3 CalculateVectorProjection(Vector3 vector, Vector3 onto)
        {
            double denominator = CalculateDotProduct(onto, onto);
            if (denominator == 0.0)
            {
                throw new DivideByZeroException("Cannot project onto a zero vector.");
            }
            return ScaleVector(onto, CalculateDotProduct(vector, onto) / denominator);
        }

        public static double CalculateVectorDistance(Vector2 vectorA, Vector2 vectorB)
        {
            return CalculateDistance2D(vectorA.X, vectorA.Y, vectorB.X, vectorB.Y);
        }

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
