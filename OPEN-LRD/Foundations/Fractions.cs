using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD.Foundations
{
    internal class Fraction
    {
        public string? FractionString { get; private set; }
        public int Numerator { get; private set; }
        public int Denominator { get; private set; }
        public Fraction(int numerator, int denominator)
        {
            if (denominator == 0)
            {
                throw new DivideByZeroException("The denominator of a fraction cannot be zero.");
            }
            if (denominator < 0)
            {
                if (numerator == int.MinValue)
                {
                    throw new OverflowException("The negated numerator is outside the range of Int32.");
                }
                numerator = -numerator;
                denominator = -denominator;
            }
            Numerator = numerator;
            Denominator = denominator;
        }
        public double ToDecimal()
        {
            return (double)Numerator / Denominator;
        }

        public float ToFloat()
        {
            return (float)Numerator / Denominator;
        }
        public override string ToString()
        {
            return $"{Numerator}/{Denominator}";
        }

        public string ToFractionString()
        {
            return $"{Numerator}/{Denominator}";
        }

        public void SetFractionString(string fractionString)
        {
            FractionString = fractionString;
        }

        
    }
}
