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
