using System;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class UnitsAndMeasurementTests
    {
        public static void RunAll()
        {
            Run("Dimensions and unit lookup", TestDimensionsAndLookup);
            Run("Unit conversion", TestConversion);
            Run("Measurement arithmetic", TestMeasurementArithmetic);
            Run("Significant figures", TestSignificantFigures);
            Run("Uncertainty propagation", TestUncertainty);
            Run("Units and measurement validation", TestValidation);
            Run("Physics unit catalog and constants", TestPhysicsCatalog);
            Run("SI prefix construction", TestPrefixes);
            Run("US customary to SI conversions", TestUsCustomaryConversions);
        }

        private static void TestDimensionsAndLookup()
        {
            UnitDimension velocity = UnitsAndMeasurement.Divide(UnitCatalog.Length[0].Dimension, UnitCatalog.Time[0].Dimension);
            Assert(velocity.Length == 1 && velocity.Time == -1, "velocity dimensions");
            UnitDimension area = UnitsAndMeasurement.Power(UnitCatalog.Meter.Dimension, 2);
            Assert(area.Length == 2, "area dimensions");
            UnitDimension force = UnitsAndMeasurement.Multiply(
                UnitCatalog.Meter.Dimension,
                UnitsAndMeasurement.Divide(UnitCatalog.Kilogram.Dimension, UnitsAndMeasurement.Power(UnitCatalog.Second.Dimension, 2)));
            Assert(force.Length == 1 && force.Mass == 1 && force.Time == -2, "force dimensions");
            Assert(UnitsAndMeasurement.FindUnit("km") == UnitCatalog.Kilometer, "unit lookup");
            Assert(UnitsAndMeasurement.TryFindUnit("°C", out Unit celsius) && celsius == UnitCatalog.Celsius, "try unit lookup");
            Assert(!UnitsAndMeasurement.TryFindUnit("unknown", out _), "unknown unit lookup");
        }

        private static void TestConversion()
        {
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Meter, UnitCatalog.Centimeter), 100.0, "meter to centimeter");
            AssertEqual(UnitsAndMeasurement.Convert(12.0, UnitCatalog.Inch, UnitCatalog.Centimeter), 30.48, "inch to centimeter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Kilogram, UnitCatalog.Gram), 1000.0, "kilogram to gram");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Liter, UnitCatalog.Milliliter), 1000.0, "liter to milliliter");
            AssertEqual(UnitsAndMeasurement.Convert(0.0, UnitCatalog.Celsius, UnitCatalog.Fahrenheit), 32.0, "Celsius to Fahrenheit");
            AssertEqual(UnitsAndMeasurement.Convert(32.0, UnitCatalog.Fahrenheit, UnitCatalog.Celsius), 0.0, "Fahrenheit to Celsius");
            AssertEqual(UnitsAndMeasurement.Convert(0.0, UnitCatalog.Celsius, UnitCatalog.Kelvin), 273.15, "Celsius to Kelvin");

            Measurement measurement = new Measurement(1.0, 0.01, UnitCatalog.Meter);
            Measurement converted = UnitsAndMeasurement.Convert(measurement, UnitCatalog.Centimeter);
            AssertEqual(converted.Value, 100.0, "measurement value conversion");
            AssertEqual(converted.Uncertainty, 1.0, "measurement uncertainty conversion");
            Assert(!UnitsAndMeasurement.TryConvert(1.0, UnitCatalog.Meter, UnitCatalog.Second, out _), "try conversion rejects incompatible dimensions");
        }

        private static void TestMeasurementArithmetic()
        {
            Measurement firstLength = new Measurement(100.0, 1.0, UnitCatalog.Centimeter);
            Measurement secondLength = new Measurement(1.0, 0.02, UnitCatalog.Meter);
            Measurement sum = UnitsAndMeasurement.Add(firstLength, secondLength);
            AssertEqual(sum.Value, 200.0, "addition keeps left unit");
            AssertEqual(sum.Uncertainty, Math.Sqrt(5.0), "additive measurement uncertainty");
            Measurement difference = UnitsAndMeasurement.Subtract(secondLength, firstLength);
            AssertEqual(difference.Value, 0.0, "measurement subtraction");

            Measurement distance = new Measurement(2.0, 0.1, UnitCatalog.Meter);
            Measurement time = new Measurement(3.0, 0.2, UnitCatalog.Second);
            Measurement product = UnitsAndMeasurement.Multiply(distance, time);
            AssertEqual(product.Value, 6.0, "measurement multiplication");
            AssertEqual(product.Uncertainty, 0.5, "multiplicative measurement uncertainty");
            Assert(product.Unit.Dimension.Length == 1 && product.Unit.Dimension.Time == 1, "product unit dimensions");

            Measurement quotient = UnitsAndMeasurement.Divide(distance, time);
            AssertEqual(quotient.Value, 2.0 / 3.0, "measurement division");
            Assert(quotient.Unit.Dimension.Length == 1 && quotient.Unit.Dimension.Time == -1, "quotient unit dimensions");
            Measurement squared = UnitsAndMeasurement.Power(distance, 2);
            AssertEqual(squared.Value, 4.0, "measurement power");
            AssertEqual(squared.Uncertainty, 0.4, "power uncertainty propagation");
        }

        private static void TestSignificantFigures()
        {
            Assert(UnitsAndMeasurement.CountSignificantFigures("0.00340") == 3, "leading zeros are not significant");
            Assert(UnitsAndMeasurement.CountSignificantFigures("1200") == 2, "integer trailing zeros are ambiguous and omitted");
            Assert(UnitsAndMeasurement.CountSignificantFigures("1.200e3") == 4, "decimal trailing zeros are significant");
            Assert(UnitsAndMeasurement.CountSignificantFigures("100.") == 3, "decimal point marks trailing zeros significant");
            AssertEqual(UnitsAndMeasurement.RoundToSignificantFigures(12345.0, 3), 12300.0, "round significant figures");
            AssertEqual(UnitsAndMeasurement.RoundToSignificantFigures(-0.012345, 2), -0.012, "negative significant-figure rounding");
            Assert(UnitsAndMeasurement.FormatSignificantFigures(12345.0, 3) == "1.23E+04", "significant-figure formatting");
            Assert(UnitsAndMeasurement.FormatMeasurement(new Measurement(12.345, 0.4567, UnitCatalog.Meter)) == "12.3 ± 0.457 m", "measurement formatting");
        }

        private static void TestUncertainty()
        {
            UncertaintyResult additive = UnitsAndMeasurement.CombineAdditive(2.0, 3.0, 5.0, 4.0);
            AssertEqual(additive.Value, 7.0, "additive result");
            AssertEqual(additive.Uncertainty, 5.0, "root-sum-square uncertainty");
            UncertaintyResult multiplicative = UnitsAndMeasurement.CombineMultiplicative(2.0, 0.1, 3.0, 0.2);
            AssertEqual(multiplicative.Value, 6.0, "multiplicative result");
            AssertEqual(multiplicative.Uncertainty, 0.5, "multiplicative uncertainty");
            AssertEqual(UnitsAndMeasurement.PropagatePower(3.0, 0.1, 2).Uncertainty, 0.6, "power uncertainty");
            UncertaintyResult propagated = UnitsAndMeasurement.PropagateFunction(3.0, 0.1, value => value * value, value => 2.0 * value);
            AssertEqual(propagated.Value, 9.0, "function propagation result");
            AssertEqual(propagated.Uncertainty, 0.6, "function propagation uncertainty");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentException>(
                () => UnitsAndMeasurement.Convert(1.0, UnitCatalog.Meter, UnitCatalog.Second),
                "incompatible unit conversion");
            AssertThrows<KeyNotFoundException>(() => UnitsAndMeasurement.FindUnit("not-a-unit"),
                "unknown unit lookup should throw a clear missing-key error");
            AssertThrows<ArgumentException>(() => UnitsAndMeasurement.Convert(1.0,
                    new Unit("zero-scale", 0.0, 0.0, UnitCatalog.Meter.Dimension), UnitCatalog.Meter),
                "custom units with zero scale should be rejected");
            Unit hugeScale = new Unit("huge", double.MaxValue, 0.0, UnitCatalog.Meter.Dimension);
            AssertThrows<ArgumentOutOfRangeException>(() => UnitCatalog.CreatePrefixedUnit(hugeScale, "k", 2),
                "prefixed-unit scale overflow should be rejected");
            Unit tinyScale = new Unit("tiny", 1e-300, 0.0, UnitCatalog.Meter.Dimension);
            AssertThrows<ArgumentOutOfRangeException>(() => UnitsAndMeasurement.Convert(double.MaxValue, UnitCatalog.Meter, tinyScale),
                "conversion overflow should be rejected rather than returning infinity");
            AssertThrows<ArgumentOutOfRangeException>(
                () => UnitsAndMeasurement.Convert(double.NaN, UnitCatalog.Meter, UnitCatalog.Centimeter),
                "non-finite conversion input");
            AssertThrows<ArgumentException>(
                () => UnitsAndMeasurement.Add(
                    new Measurement(1.0, 0.0, UnitCatalog.Celsius),
                    new Measurement(1.0, 0.0, UnitCatalog.Meter)),
                "incompatible measurement addition");
            AssertThrows<ArgumentException>(
                () => UnitsAndMeasurement.Multiply(
                    new Measurement(20.0, 0.1, UnitCatalog.Celsius),
                    new Measurement(2.0, 0.1, UnitCatalog.Meter)),
                "affine units are not multiplicative");
            AssertThrows<DivideByZeroException>(
                () => UnitsAndMeasurement.Divide(
                    new Measurement(1.0, 0.0, UnitCatalog.Meter),
                    new Measurement(0.0, 0.0, UnitCatalog.Second)),
                "measurement division by zero");
            AssertThrows<FormatException>(
                () => UnitsAndMeasurement.CountSignificantFigures("not a number"),
                "invalid significant-figure string");
            AssertThrows<ArgumentOutOfRangeException>(
                () => UnitsAndMeasurement.RoundToSignificantFigures(1.0, 0),
                "significant figure count must be positive");
            AssertThrows<ArgumentOutOfRangeException>(
                () => UnitsAndMeasurement.CombineAdditive(1.0, -1.0, 2.0, 1.0),
                "uncertainty must be non-negative");
        }

        private static void TestPhysicsCatalog()
        {
            Assert(UnitCatalog.Force.Dimension == new UnitDimension(1, 1, -2, 0, 0, 0, 0), "newton dimensions");
            Assert(UnitCatalog.Energy.Dimension == UnitCatalog.Torque.Dimension, "energy and torque share dimension");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Electronvolt, UnitCatalog.Joule), 1.602176634e-19, "electronvolt conversion", 1e-12);
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Atmosphere, UnitCatalog.Pascal), 101325.0, "atmosphere conversion");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Bar, UnitCatalog.Pascal), 100000.0, "bar conversion");
            AssertEqual(UnitCatalog.AstronomicalUnit.Scale, 149597870700.0, "astronomical unit definition");
            Assert(UnitCatalog.FindUnit("C") == UnitCatalog.Coulomb, "coulomb symbol lookup");
            Assert(UnitCatalog.FindUnit("degC") == UnitCatalog.Celsius, "Celsius alias lookup");
            Assert(PhysicalConstants.SpeedOfLight.IsExact, "speed of light is exact");
            Assert(!PhysicalConstants.GravitationalConstant.IsExact && PhysicalConstants.GravitationalConstant.StandardUncertainty.HasValue, "measured constant metadata");
            AssertEqual(PhysicalConstants.SpeedOfLight.ValueSI, 299792458.0, "speed of light constant");
        }

        private static void TestPrefixes()
        {
            Unit kilometer = UnitCatalog.CreatePrefixedUnit(UnitCatalog.Meter, "k", 1);
            AssertEqual(kilometer.Scale, 1000.0, "kilo prefix scale");
            Assert(kilometer.Symbol == "km" && kilometer.Dimension == UnitCatalog.Meter.Dimension, "prefixed unit symbol and dimension");
            Unit squareCentimeter = UnitCatalog.CreatePrefixedUnit(UnitCatalog.Meter, "c", 2);
            AssertEqual(squareCentimeter.Scale, 1e-4, "squared centimeter scale");
            Assert(squareCentimeter.Dimension.Length == 2, "prefix unit exponent dimension");
            AssertThrows<ArgumentException>(() => UnitCatalog.CreatePrefixedUnit(UnitCatalog.Celsius, "m", 1), "affine temperature cannot be prefixed");
            AssertThrows<ArgumentOutOfRangeException>(() => UnitCatalog.CreatePrefixedUnit(UnitCatalog.Meter, "k", 13), "prefix exponent is bounded");
            AssertThrows<ArgumentException>(() => UnitCatalog.CreatePrefixedUnit(UnitCatalog.Meter, "unknown", 1), "unknown prefix rejected");
        }

        private static void TestUsCustomaryConversions()
        {
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Mile, UnitCatalog.Meter), 1609.344, "mile to meter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Yard, UnitCatalog.Meter), 0.9144, "yard to meter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Ounce, UnitCatalog.Gram), 28.349523125, "avoirdupois ounce to gram");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USShortTon, UnitCatalog.Kilogram), 907.18474, "US short ton to kilogram");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USLiquidGallon, UnitCatalog.Liter), 3.785411784, "US liquid gallon to liter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USLiquidQuart, UnitCatalog.Liter), 0.946352946, "US liquid quart to liter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USLiquidPint, UnitCatalog.Milliliter), 473.176473, "US liquid pint to milliliter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USCustomaryCup, UnitCatalog.Milliliter), 236.5882365, "US cup to milliliter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USFluidOunce, UnitCatalog.Milliliter), 29.5735295625, "US fluid ounce to milliliter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USTablespoon, UnitCatalog.Milliliter), 14.78676478125, "US tablespoon to milliliter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.USTeaspoon, UnitCatalog.Milliliter), 4.92892159375, "US teaspoon to milliliter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.SquareFoot, UnitCatalog.SquareMeter), 0.09290304, "square foot to square meter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.Acre, UnitCatalog.SquareMeter), 4046.8564224, "acre to square meter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.CubicFoot, UnitCatalog.CubicMeter), 0.028316846592, "cubic foot to cubic meter");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.PoundForce, UnitCatalog.Newton), 4.4482216152605, "pound-force to newton");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.PSI, UnitCatalog.Pascal), 6894.757293168, "psi to pascal");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.BritishThermalUnit, UnitCatalog.Joule), 1055.05585262, "Btu to joule");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.HorsepowerMechanical, UnitCatalog.Watt), 745.6998715822702, "mechanical horsepower to watt");
            AssertEqual(UnitsAndMeasurement.Convert(60.0, UnitCatalog.MilePerHour, UnitCatalog.MeterPerSecond), 26.8224, "miles per hour to meters per second");
            AssertEqual(UnitsAndMeasurement.Convert(1.0, UnitCatalog.PoundPerCubicFoot, UnitCatalog.KilogramPerCubicMeter), 16.01846337396, "pounds per cubic foot to SI density");

            double gallonInSi = UnitsAndMeasurement.ConvertToSI(1.0, UnitCatalog.USLiquidGallon);
            AssertEqual(gallonInSi, 0.003785411784, "US gallon to SI base cubic meters");
            AssertEqual(UnitsAndMeasurement.ConvertFromSI(gallonInSi, UnitCatalog.USLiquidGallon), 1.0, "SI cubic meters to US gallon");
            AssertEqual(UnitsAndMeasurement.ConvertToSI(32.0, UnitCatalog.Fahrenheit), 273.15, "Fahrenheit to SI kelvin");
            AssertEqual(UnitsAndMeasurement.ConvertFromSI(273.15, UnitCatalog.Fahrenheit), 32.0, "SI kelvin to Fahrenheit");

            Assert(UnitsAndMeasurement.FindUnit("gal") == UnitCatalog.USLiquidGallon, "US gallon alias lookup");
            Assert(UnitsAndMeasurement.FindUnit("psi") == UnitCatalog.PoundPerSquareInch, "psi alias lookup");
            Measurement usVolume = new Measurement(2.0, 0.1, UnitCatalog.USLiquidGallon);
            Measurement siVolume = UnitsAndMeasurement.Convert(usVolume, UnitCatalog.Liter);
            AssertEqual(siVolume.Value, 7.570823568, "measurement US gallon to liter");
            AssertEqual(siVolume.Uncertainty, 0.3785411784, "US gallon uncertainty conversion");
        }
    }
}
