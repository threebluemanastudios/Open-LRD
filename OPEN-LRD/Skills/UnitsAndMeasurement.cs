using System;
using System.Collections.Generic;
using System.Globalization;

namespace OPEN_LRD.Skills
{
    internal readonly record struct UnitDimension(
        int Length,
        int Mass,
        int Time,
        int Current,
        int Temperature,
        int Amount,
        int LuminousIntensity);

    internal sealed record Unit(string Symbol, double Scale, double Offset, UnitDimension Dimension);

    internal readonly record struct Measurement(double Value, double Uncertainty, Unit Unit);

    internal sealed record UncertaintyResult(double Value, double Uncertainty);

    internal static class UnitCatalog
    {
        private static readonly UnitDimension Dimensionless = new UnitDimension(0, 0, 0, 0, 0, 0, 0);
        private static readonly UnitDimension LengthDimension = new UnitDimension(1, 0, 0, 0, 0, 0, 0);
        private static readonly UnitDimension MassDimension = new UnitDimension(0, 1, 0, 0, 0, 0, 0);
        private static readonly UnitDimension TimeDimension = new UnitDimension(0, 0, 1, 0, 0, 0, 0);
        private static readonly UnitDimension CurrentDimension = new UnitDimension(0, 0, 0, 1, 0, 0, 0);
        private static readonly UnitDimension TemperatureDimension = new UnitDimension(0, 0, 0, 0, 1, 0, 0);
        private static readonly UnitDimension AmountDimension = new UnitDimension(0, 0, 0, 0, 0, 1, 0);
        private static readonly UnitDimension LuminousIntensityDimension = new UnitDimension(0, 0, 0, 0, 0, 0, 1);
        private static readonly UnitDimension AreaDimension = new UnitDimension(2, 0, 0, 0, 0, 0, 0);
        private static readonly UnitDimension VolumeDimension = new UnitDimension(3, 0, 0, 0, 0, 0, 0);
        private static readonly UnitDimension ForceDimension = UnitsAndMeasurement.Divide(
            UnitsAndMeasurement.Multiply(MassDimension, LengthDimension), UnitsAndMeasurement.Power(TimeDimension, 2));
        private static readonly UnitDimension EnergyDimension = UnitsAndMeasurement.Multiply(ForceDimension, LengthDimension);
        private static readonly UnitDimension PowerDimension = UnitsAndMeasurement.Divide(EnergyDimension, TimeDimension);
        private static readonly UnitDimension PressureDimension = UnitsAndMeasurement.Divide(ForceDimension, VolumeDimension);
        private static readonly UnitDimension FrequencyDimension = UnitsAndMeasurement.Power(TimeDimension, -1);
        private static readonly UnitDimension ChargeDimension = UnitsAndMeasurement.Multiply(CurrentDimension, TimeDimension);
        private static readonly UnitDimension VoltageDimension = UnitsAndMeasurement.Divide(PowerDimension, CurrentDimension);
        private static readonly UnitDimension ResistanceDimension = UnitsAndMeasurement.Divide(VoltageDimension, CurrentDimension);
        private static readonly UnitDimension CapacitanceDimension = UnitsAndMeasurement.Divide(ChargeDimension, VoltageDimension);
        private static readonly UnitDimension ConductanceDimension = UnitsAndMeasurement.Divide(CurrentDimension, VoltageDimension);
        private static readonly UnitDimension MagneticFluxDimension = UnitsAndMeasurement.Multiply(VoltageDimension, TimeDimension);
        private static readonly UnitDimension MagneticFieldDimension = UnitsAndMeasurement.Divide(MagneticFluxDimension, UnitsAndMeasurement.Power(LengthDimension, 2));
        private static readonly UnitDimension InductanceDimension = UnitsAndMeasurement.Divide(MagneticFluxDimension, CurrentDimension);
        private static readonly UnitDimension VelocityDimension = UnitsAndMeasurement.Divide(LengthDimension, TimeDimension);
        private static readonly UnitDimension AccelerationDimension = UnitsAndMeasurement.Divide(VelocityDimension, TimeDimension);
        private static readonly UnitDimension DensityDimension = UnitsAndMeasurement.Divide(MassDimension, VolumeDimension);
        private static readonly UnitDimension SpecificHeatCapacityDimension = UnitsAndMeasurement.Divide(
            EnergyDimension, UnitsAndMeasurement.Multiply(MassDimension, TemperatureDimension));
        private static readonly UnitDimension SpecificLatentHeatDimension = UnitsAndMeasurement.Divide(EnergyDimension, MassDimension);
        private static readonly UnitDimension MolarHeatCapacityDimension = UnitsAndMeasurement.Divide(
            EnergyDimension, UnitsAndMeasurement.Multiply(AmountDimension, TemperatureDimension));
        private static readonly UnitDimension EntropyDimension = UnitsAndMeasurement.Divide(EnergyDimension, TemperatureDimension);
        private static readonly UnitDimension DynamicViscosityDimension = UnitsAndMeasurement.Multiply(PressureDimension, TimeDimension);
        private static readonly UnitDimension VolumetricFlowRateDimension = UnitsAndMeasurement.Divide(VolumeDimension, TimeDimension);
        private static readonly UnitDimension MassFlowRateDimension = UnitsAndMeasurement.Divide(MassDimension, TimeDimension);
        private static readonly UnitDimension ThermalExpansionCoefficientDimension = UnitsAndMeasurement.Power(TemperatureDimension, -1);
        private static readonly UnitDimension ElectricFieldDimension = UnitsAndMeasurement.Divide(ForceDimension, ChargeDimension);
        private static readonly UnitDimension LinearMomentumDimension = UnitsAndMeasurement.Multiply(MassDimension, VelocityDimension);
        private static readonly UnitDimension WaveNumberDimension = UnitsAndMeasurement.Power(LengthDimension, -1);
        private static readonly UnitDimension NumberDensityDimension = UnitsAndMeasurement.Power(LengthDimension, -3);
        private static readonly UnitDimension SpringConstantDimension = UnitsAndMeasurement.Divide(ForceDimension, LengthDimension);
        private static readonly UnitDimension KretschmannScalarDimension = UnitsAndMeasurement.Power(LengthDimension, -4);

        public static Unit DimensionlessUnit { get; } = new Unit("", 1.0, 0.0, Dimensionless);
        public static Unit Meter { get; } = new Unit("m", 1.0, 0.0, LengthDimension);
        public static Unit Kilometer { get; } = new Unit("km", 1000.0, 0.0, LengthDimension);
        public static Unit Centimeter { get; } = new Unit("cm", 0.01, 0.0, LengthDimension);
        public static Unit Millimeter { get; } = new Unit("mm", 0.001, 0.0, LengthDimension);
        public static Unit Inch { get; } = new Unit("in", 0.0254, 0.0, LengthDimension);
        public static Unit Foot { get; } = new Unit("ft", 0.3048, 0.0, LengthDimension);
        public static Unit Yard { get; } = new Unit("yd", 0.9144, 0.0, LengthDimension);
        public static Unit Mile { get; } = new Unit("mi", 1609.344, 0.0, LengthDimension);
        public static Unit Kilogram { get; } = new Unit("kg", 1.0, 0.0, MassDimension);
        public static Unit Gram { get; } = new Unit("g", 0.001, 0.0, MassDimension);
        public static Unit Pound { get; } = new Unit("lb", 0.45359237, 0.0, MassDimension);
        public static Unit Ounce { get; } = new Unit("oz", 0.028349523125, 0.0, MassDimension);
        public static Unit USShortTon { get; } = new Unit("US ton", 907.18474, 0.0, MassDimension);
        public static Unit Ampere { get; } = new Unit("A", 1.0, 0.0, CurrentDimension);
        public static Unit Mole { get; } = new Unit("mol", 1.0, 0.0, AmountDimension);
        public static Unit Candela { get; } = new Unit("cd", 1.0, 0.0, LuminousIntensityDimension);
        public static Unit Second { get; } = new Unit("s", 1.0, 0.0, TimeDimension);
        public static Unit Minute { get; } = new Unit("min", 60.0, 0.0, TimeDimension);
        public static Unit Hour { get; } = new Unit("h", 3600.0, 0.0, TimeDimension);
        public static Unit Day { get; } = new Unit("day", 86400.0, 0.0, TimeDimension);
        public static Unit Kelvin { get; } = new Unit("K", 1.0, 0.0, TemperatureDimension);
        public static Unit Celsius { get; } = new Unit("°C", 1.0, 273.15, TemperatureDimension);
        public static Unit Fahrenheit { get; } = new Unit("°F", 5.0 / 9.0, 273.15 - (32.0 * 5.0 / 9.0), TemperatureDimension);
        public static Unit CubicMeter { get; } = new Unit("m³", 1.0, 0.0, VolumeDimension);
        public static Unit Liter { get; } = new Unit("L", 0.001, 0.0, VolumeDimension);
        public static Unit Milliliter { get; } = new Unit("mL", 1e-6, 0.0, VolumeDimension);
        public static Unit SquareMeter { get; } = new Unit("m²", 1.0, 0.0, AreaDimension);
        public static Unit SquareFoot { get; } = new Unit("ft²", 0.09290304, 0.0, AreaDimension);
        public static Unit SquareMile { get; } = new Unit("mi²", 2589988.110336, 0.0, AreaDimension);
        public static Unit Acre { get; } = new Unit("acre", 4046.8564224, 0.0, AreaDimension);
        public static Unit CubicInch { get; } = new Unit("in³", 1.6387064e-5, 0.0, VolumeDimension);
        public static Unit CubicFoot { get; } = new Unit("ft³", 0.028316846592, 0.0, VolumeDimension);
        public static Unit CubicYard { get; } = new Unit("yd³", 0.764554857984, 0.0, VolumeDimension);
        public static Unit USLiquidGallon { get; } = new Unit("US gal", 0.003785411784, 0.0, VolumeDimension);
        public static Unit USLiquidQuart { get; } = new Unit("US qt", 0.000946352946, 0.0, VolumeDimension);
        public static Unit USLiquidPint { get; } = new Unit("US pt", 0.000473176473, 0.0, VolumeDimension);
        public static Unit USCustomaryCup { get; } = new Unit("US cup", 0.0002365882365, 0.0, VolumeDimension);
        public static Unit USFluidOunce { get; } = new Unit("US fl oz", 2.95735295625e-5, 0.0, VolumeDimension);
        public static Unit USTablespoon { get; } = new Unit("US tbsp", 1.478676478125e-5, 0.0, VolumeDimension);
        public static Unit USTeaspoon { get; } = new Unit("US tsp", 4.92892159375e-6, 0.0, VolumeDimension);
        public static Unit Newton { get; } = new Unit("N", 1.0, 0.0, ForceDimension);
        public static Unit PoundForce { get; } = new Unit("lbf", 4.4482216152605, 0.0, ForceDimension);
        public static Unit Joule { get; } = new Unit("J", 1.0, 0.0, EnergyDimension);
        public static Unit BritishThermalUnit { get; } = new Unit("Btu", 1055.05585262, 0.0, EnergyDimension);
        public static Unit Watt { get; } = new Unit("W", 1.0, 0.0, PowerDimension);
        public static Unit HorsepowerMechanical { get; } = new Unit("hp", 745.6998715822702, 0.0, PowerDimension);
        public static Unit Pascal { get; } = new Unit("Pa", 1.0, 0.0, PressureDimension);
        public static Unit PoundPerSquareInch { get; } = new Unit("psi", 6894.757293168, 0.0, PressureDimension);
        public static Unit Hertz { get; } = new Unit("Hz", 1.0, 0.0, FrequencyDimension);
        public static Unit Coulomb { get; } = new Unit("C", 1.0, 0.0, ChargeDimension);
        public static Unit Volt { get; } = new Unit("V", 1.0, 0.0, VoltageDimension);
        public static Unit Ohm { get; } = new Unit("Ω", 1.0, 0.0, ResistanceDimension);
        public static Unit Farad { get; } = new Unit("F", 1.0, 0.0, CapacitanceDimension);
        public static Unit Siemens { get; } = new Unit("S", 1.0, 0.0, ConductanceDimension);
        public static Unit Weber { get; } = new Unit("Wb", 1.0, 0.0, MagneticFluxDimension);
        public static Unit Tesla { get; } = new Unit("T", 1.0, 0.0, MagneticFieldDimension);
        public static Unit Henry { get; } = new Unit("H", 1.0, 0.0, InductanceDimension);
        public static Unit MeterPerSecond { get; } = new Unit("m/s", 1.0, 0.0, VelocityDimension);
        public static Unit MeterPerSecondSquared { get; } = new Unit("m/s²", 1.0, 0.0, AccelerationDimension);
        public static Unit KilogramPerCubicMeter { get; } = new Unit("kg/m³", 1.0, 0.0, DensityDimension);
        public static Unit MilePerHour { get; } = new Unit("mph", 0.44704, 0.0, VelocityDimension);
        public static Unit FootPerSecond { get; } = new Unit("ft/s", 0.3048, 0.0, VelocityDimension);
        public static Unit PoundPerCubicFoot { get; } = new Unit("lb/ft³", 16.01846337396, 0.0, DensityDimension);
        public static Unit TorqueUnit { get; } = new Unit("N·m", 1.0, 0.0, EnergyDimension);
        public static Unit Electronvolt { get; } = new Unit("eV", 1.602176634e-19, 0.0, EnergyDimension);
        public static Unit Atmosphere { get; } = new Unit("atm", 101325.0, 0.0, PressureDimension);
        public static Unit Bar { get; } = new Unit("bar", 100000.0, 0.0, PressureDimension);
        public static Unit AstronomicalUnit { get; } = new Unit("au", 149597870700.0, 0.0, LengthDimension);
        public static Unit Parsec { get; } = new Unit("pc", 3.0856775814913673e16, 0.0, LengthDimension);
        public static Unit SpecificHeatCapacity { get; } = new Unit("J/(kg·K)", 1.0, 0.0, SpecificHeatCapacityDimension);
        public static Unit SpecificLatentHeat { get; } = new Unit("J/kg", 1.0, 0.0, SpecificLatentHeatDimension);
        public static Unit MolarHeatCapacity { get; } = new Unit("J/(mol·K)", 1.0, 0.0, MolarHeatCapacityDimension);
        public static Unit Entropy { get; } = new Unit("J/K", 1.0, 0.0, EntropyDimension);
        public static Unit DynamicViscosity { get; } = new Unit("Pa·s", 1.0, 0.0, DynamicViscosityDimension);
        public static Unit VolumetricFlowRate { get; } = new Unit("m³/s", 1.0, 0.0, VolumetricFlowRateDimension);
        public static Unit MassFlowRate { get; } = new Unit("kg/s", 1.0, 0.0, MassFlowRateDimension);
        public static Unit ThermalExpansionCoefficient { get; } = new Unit("1/K", 1.0, 0.0, ThermalExpansionCoefficientDimension);
        public static Unit ElectricField { get; } = new Unit("N/C", 1.0, 0.0, ElectricFieldDimension);
        public static Unit LinearMomentum { get; } = new Unit("kg·m/s", 1.0, 0.0, LinearMomentumDimension);
        public static Unit Impulse { get; } = new Unit("N·s", 1.0, 0.0, LinearMomentumDimension);
        public static Unit Radian { get; } = new Unit("rad", 1.0, 0.0, Dimensionless);
        public static Unit RadianPerSecond { get; } = new Unit("rad/s", 1.0, 0.0, FrequencyDimension);
        public static Unit ReciprocalMeter { get; } = new Unit("1/m", 1.0, 0.0, WaveNumberDimension);
        public static Unit ReciprocalCubicMeter { get; } = new Unit("1/m³", 1.0, 0.0, NumberDensityDimension);
        public static Unit NewtonPerMeter { get; } = new Unit("N/m", 1.0, 0.0, SpringConstantDimension);
        public static Unit ReciprocalQuarticMeter { get; } = new Unit("1/m⁴", 1.0, 0.0, KretschmannScalarDimension);
        private static readonly IReadOnlyDictionary<string, Unit> Units = BuildUnits();

        private static readonly IReadOnlyDictionary<string, int> PrefixPowers = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Q"] = 30, ["R"] = 27, ["Y"] = 24, ["Z"] = 21, ["E"] = 18,
            ["P"] = 15, ["T"] = 12, ["G"] = 9, ["M"] = 6, ["k"] = 3,
            ["h"] = 2, ["da"] = 1, [""] = 0, ["d"] = -1, ["c"] = -2,
            ["m"] = -3, ["μ"] = -6, ["u"] = -6, ["n"] = -9, ["p"] = -12,
            ["f"] = -15, ["a"] = -18, ["z"] = -21, ["y"] = -24, ["r"] = -27,
            ["q"] = -30
        };

        public static IReadOnlyList<Unit> Length => new[] { Meter, Kilometer, Centimeter, Millimeter, Inch, Foot, Yard, Mile };
        public static IReadOnlyList<Unit> Mass => new[] { Kilogram, Gram, Pound, Ounce, USShortTon };
        public static IReadOnlyList<Unit> Time => new[] { Second, Minute, Hour, Day };
        public static IReadOnlyList<Unit> Temperature => new[] { Kelvin, Celsius, Fahrenheit };
        public static IReadOnlyList<Unit> Area => new[] { SquareMeter, SquareFoot, SquareMile, Acre };
        public static IReadOnlyList<Unit> Volume => new[]
        {
            CubicMeter, Liter, Milliliter, CubicInch, CubicFoot, CubicYard,
            USLiquidGallon, USLiquidQuart, USLiquidPint, USCustomaryCup,
            USFluidOunce, USTablespoon, USTeaspoon
        };
        public static Unit Force => Newton;
        public static Unit Energy => Joule;
        public static Unit Power => Watt;
        public static Unit Pressure => Pascal;
        public static Unit Frequency => Hertz;
        public static Unit Current => Ampere;
        public static Unit Charge => Coulomb;
        public static Unit Voltage => Volt;
        public static Unit Resistance => Ohm;
        public static Unit Capacitance => Farad;
        public static Unit Conductance => Siemens;
        public static Unit MagneticFlux => Weber;
        public static Unit MagneticField => Tesla;
        public static Unit Inductance => Henry;
        public static Unit Velocity => MeterPerSecond;
        public static Unit Acceleration => MeterPerSecondSquared;
        public static Unit Density => KilogramPerCubicMeter;
        public static Unit Torque => TorqueUnit;
        public static Unit PSI => PoundPerSquareInch;
        public static Unit USGallon => USLiquidGallon;
        public static Unit USQuart => USLiquidQuart;
        public static Unit USPint => USLiquidPint;
        public static Unit USCup => USCustomaryCup;

        public static Unit CreatePrefixedUnit(Unit unit, string prefixSymbol, int exponent)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }
            if (!double.IsFinite(unit.Scale) || unit.Scale <= 0.0 || !double.IsFinite(unit.Offset))
            {
                throw new ArgumentException("Unit scale must be positive and finite and offset must be finite.", nameof(unit));
            }
            if (prefixSymbol == null)
            {
                throw new ArgumentNullException(nameof(prefixSymbol));
            }
            if (unit.Offset != 0.0)
            {
                throw new ArgumentException("Affine units cannot be prefixed.", nameof(unit));
            }
            if (!PrefixPowers.TryGetValue(prefixSymbol, out int prefixPower))
            {
                throw new ArgumentException("The SI prefix is not supported.", nameof(prefixSymbol));
            }
            if (exponent < -12 || exponent > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(exponent), "Unit exponents must be in [-12, 12].");
            }
            double scale = Math.Pow(unit.Scale * Math.Pow(10.0, prefixPower), exponent);
            if (!double.IsFinite(scale) || scale <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(exponent), "The prefixed unit scale is outside the finite positive range.");
            }
            string symbol = exponent == 0
                ? string.Empty
                : exponent == 1 ? $"{prefixSymbol}{unit.Symbol}" : $"{prefixSymbol}{unit.Symbol}^{exponent}";
            return new Unit(symbol, scale, 0.0, UnitsAndMeasurement.Power(unit.Dimension, exponent));
        }

        public static Unit ElectronVolt => Electronvolt;
        public static Unit AstronomicalLength => AstronomicalUnit;

        public static Unit FindUnit(string symbol)
        {
            if (symbol == null)
            {
                throw new ArgumentNullException(nameof(symbol));
            }
            if (!Units.TryGetValue(symbol, out Unit? unit))
            {
                throw new KeyNotFoundException($"Unknown unit symbol '{symbol}'.");
            }
            return unit;
        }

        public static bool TryFindUnit(string symbol, out Unit unit)
        {
            if (symbol == null)
            {
                throw new ArgumentNullException(nameof(symbol));
            }
            if (Units.TryGetValue(symbol, out Unit? found))
            {
                unit = found;
                return true;
            }
            unit = null!;
            return false;
        }

        private static IReadOnlyDictionary<string, Unit> BuildUnits()
        {
            Dictionary<string, Unit> units = new Dictionary<string, Unit>(StringComparer.Ordinal)
            {
                ["m"] = Meter,
                ["km"] = Kilometer,
                ["cm"] = Centimeter,
                ["mm"] = Millimeter,
                ["in"] = Inch,
                ["inch"] = Inch,
                ["ft"] = Foot,
                ["foot"] = Foot,
                ["feet"] = Foot,
                ["yd"] = Yard,
                ["yard"] = Yard,
                ["mi"] = Mile,
                ["mile"] = Mile,
                ["kg"] = Kilogram,
                ["g"] = Gram,
                ["lb"] = Pound,
                ["lbs"] = Pound,
                ["lbm"] = Pound,
                ["oz"] = Ounce,
                ["ton"] = USShortTon,
                ["US ton"] = USShortTon,
                ["short ton"] = USShortTon,
                ["A"] = Ampere,
                ["mol"] = Mole,
                ["cd"] = Candela,
                ["s"] = Second,
                ["min"] = Minute,
                ["h"] = Hour,
                ["day"] = Day,
                ["K"] = Kelvin,
                ["degC"] = Celsius,
                ["°C"] = Celsius,
                ["degF"] = Fahrenheit,
                ["°F"] = Fahrenheit,
                ["m3"] = CubicMeter,
                ["m³"] = CubicMeter,
                ["L"] = Liter,
                ["mL"] = Milliliter,
                ["m²"] = SquareMeter,
                ["ft2"] = SquareFoot,
                ["ft²"] = SquareFoot,
                ["mi2"] = SquareMile,
                ["mi²"] = SquareMile,
                ["acre"] = Acre,
                ["ac"] = Acre,
                ["in3"] = CubicInch,
                ["in³"] = CubicInch,
                ["ft3"] = CubicFoot,
                ["ft³"] = CubicFoot,
                ["yd3"] = CubicYard,
                ["yd³"] = CubicYard,
                ["gal"] = USLiquidGallon,
                ["US gal"] = USLiquidGallon,
                ["qt"] = USLiquidQuart,
                ["US qt"] = USLiquidQuart,
                ["pt"] = USLiquidPint,
                ["US pt"] = USLiquidPint,
                ["cup"] = USCustomaryCup,
                ["US cup"] = USCustomaryCup,
                ["fl oz"] = USFluidOunce,
                ["US fl oz"] = USFluidOunce,
                ["tbsp"] = USTablespoon,
                ["tsp"] = USTeaspoon,
                ["N"] = Newton,
                ["lbf"] = PoundForce,
                ["J"] = Joule,
                ["Btu"] = BritishThermalUnit,
                ["W"] = Watt,
                ["hp"] = HorsepowerMechanical,
                ["Pa"] = Pascal,
                ["psi"] = PoundPerSquareInch,
                ["Hz"] = Hertz,
                ["C"] = Coulomb,
                ["V"] = Volt,
                ["ohm"] = Ohm,
                ["Ω"] = Ohm,
                ["F"] = Farad,
                ["S"] = Siemens,
                ["Wb"] = Weber,
                ["T"] = Tesla,
                ["H"] = Henry,
                ["m/s"] = MeterPerSecond,
                ["m/s2"] = MeterPerSecondSquared,
                ["m/s²"] = MeterPerSecondSquared,
                ["kg/m3"] = KilogramPerCubicMeter,
                ["kg/m³"] = KilogramPerCubicMeter,
                ["mph"] = MilePerHour,
                ["ft/s"] = FootPerSecond,
                ["lb/ft3"] = PoundPerCubicFoot,
                ["lb/ft³"] = PoundPerCubicFoot,
                ["N·m"] = TorqueUnit,
                ["eV"] = Electronvolt,
                ["atm"] = Atmosphere,
                ["bar"] = Bar,
                ["au"] = AstronomicalUnit,
                ["pc"] = Parsec,
                ["1"] = DimensionlessUnit,
                ["J/(kg·K)"] = SpecificHeatCapacity,
                ["J/kg"] = SpecificLatentHeat,
                ["J/(mol·K)"] = MolarHeatCapacity,
                ["J/K"] = Entropy,
                ["Pa·s"] = DynamicViscosity,
                ["m³/s"] = VolumetricFlowRate,
                ["kg/s"] = MassFlowRate,
                ["1/K"] = ThermalExpansionCoefficient,
                ["N/C"] = ElectricField,
                ["kg·m/s"] = LinearMomentum,
                ["N·s"] = Impulse,
                ["rad"] = Radian,
                ["rad/s"] = RadianPerSecond,
                ["1/m"] = ReciprocalMeter,
                ["1/m3"] = ReciprocalCubicMeter,
                ["1/m³"] = ReciprocalCubicMeter,
                ["N/m"] = NewtonPerMeter,
                ["1/m4"] = ReciprocalQuarticMeter,
                ["1/m⁴"] = ReciprocalQuarticMeter
            };
            return units;
        }
    }

    internal static class UnitsAndMeasurement
    {
        public static UnitDimension Multiply(UnitDimension left, UnitDimension right)
        {
            return new UnitDimension(
                checked(left.Length + right.Length),
                checked(left.Mass + right.Mass),
                checked(left.Time + right.Time),
                checked(left.Current + right.Current),
                checked(left.Temperature + right.Temperature),
                checked(left.Amount + right.Amount),
                checked(left.LuminousIntensity + right.LuminousIntensity));
        }

        public static UnitDimension Divide(UnitDimension left, UnitDimension right)
        {
            return new UnitDimension(
                checked(left.Length - right.Length),
                checked(left.Mass - right.Mass),
                checked(left.Time - right.Time),
                checked(left.Current - right.Current),
                checked(left.Temperature - right.Temperature),
                checked(left.Amount - right.Amount),
                checked(left.LuminousIntensity - right.LuminousIntensity));
        }

        public static UnitDimension Power(UnitDimension dimension, int exponent)
        {
            return new UnitDimension(
                checked(dimension.Length * exponent),
                checked(dimension.Mass * exponent),
                checked(dimension.Time * exponent),
                checked(dimension.Current * exponent),
                checked(dimension.Temperature * exponent),
                checked(dimension.Amount * exponent),
                checked(dimension.LuminousIntensity * exponent));
        }

        public static Unit FindUnit(string symbol)
        {
            return UnitCatalog.FindUnit(symbol);
        }

        public static bool TryFindUnit(string symbol, out Unit unit)
        {
            return UnitCatalog.TryFindUnit(symbol, out unit);
        }

        public static double Convert(double value, Unit from, Unit to)
        {
            ValidateUnit(from, nameof(from));
            ValidateUnit(to, nameof(to));
            EnsureCompatible(from, to);
            return ConvertFromSI(ConvertToSI(value, from), to);
        }

        /// <summary>Converts a value to its SI base-unit value (for example, length to meters).</summary>
        {
            ValidateUnit(from, nameof(from));
            EnsureFinite(value, nameof(value));
            double result = value * from.Scale + from.Offset;
            EnsureFinite(result, nameof(value));
            return result;
        }

        /// <summary>Converts an SI base-unit value into the requested unit.</summary>
        /// <summary>Converts an SI base-unit value into the requested unit.</summary>
        public static double ConvertFromSI(double siValue, Unit to)
        {
            ValidateUnit(to, nameof(to));
            EnsureFinite(siValue, nameof(siValue));
            double result = (siValue - to.Offset) / to.Scale;
            EnsureFinite(result, nameof(siValue));
            return result;
        }

        public static bool TryConvert(double value, Unit from, Unit to, out double converted)
        {
            ValidateUnit(from, nameof(from));
            ValidateUnit(to, nameof(to));
            EnsureFinite(value, nameof(value));
            if (from.Dimension != to.Dimension)
            {
                converted = 0.0;
                return false;
            }
            converted = Convert(value, from, to);
            return true;
        }

        public static Measurement Convert(Measurement measurement, Unit to)
        {
            ValidateMeasurement(measurement, nameof(measurement));
            ValidateUnit(to, nameof(to));
            EnsureCompatible(measurement.Unit, to);
            double value = Convert(measurement.Value, measurement.Unit, to);
            double uncertainty = measurement.Uncertainty * measurement.Unit.Scale / to.Scale;
            return new Measurement(value, uncertainty, to);
        }

        public static Measurement Add(Measurement left, Measurement right)
        {
            ValidateMeasurement(left, nameof(left));
            ValidateMeasurement(right, nameof(right));
            EnsureCompatible(left.Unit, right.Unit);
            double leftBase = left.Value * left.Unit.Scale + left.Unit.Offset;
            double rightBase = right.Value * right.Unit.Scale + right.Unit.Offset;
            double value = (leftBase + rightBase - left.Unit.Offset) / left.Unit.Scale;
            double uncertainty = Math.Sqrt(Math.Pow(left.Uncertainty * left.Unit.Scale, 2.0)
                + Math.Pow(right.Uncertainty * right.Unit.Scale, 2.0)) / left.Unit.Scale;
            return new Measurement(value, uncertainty, left.Unit);
        }

        public static Measurement Subtract(Measurement left, Measurement right)
        {
            ValidateMeasurement(left, nameof(left));
            ValidateMeasurement(right, nameof(right));
            EnsureCompatible(left.Unit, right.Unit);
            double leftBase = left.Value * left.Unit.Scale + left.Unit.Offset;
            double rightBase = right.Value * right.Unit.Scale + right.Unit.Offset;
            double value = (leftBase - rightBase - left.Unit.Offset) / left.Unit.Scale;
            double uncertainty = Math.Sqrt(Math.Pow(left.Uncertainty * left.Unit.Scale, 2.0)
                + Math.Pow(right.Uncertainty * right.Unit.Scale, 2.0)) / left.Unit.Scale;
            return new Measurement(value, uncertainty, left.Unit);
        }

        public static Measurement Multiply(Measurement left, Measurement right)
        {
            ValidateMeasurement(left, nameof(left));
            ValidateMeasurement(right, nameof(right));
            EnsureMultiplicativeUnit(left.Unit, nameof(left));
            EnsureMultiplicativeUnit(right.Unit, nameof(right));
            double leftBase = left.Value * left.Unit.Scale;
            double rightBase = right.Value * right.Unit.Scale;
            double leftUncertainty = left.Uncertainty * left.Unit.Scale;
            double rightUncertainty = right.Uncertainty * right.Unit.Scale;
            double baseValue = leftBase * rightBase;
            double baseUncertainty = Math.Sqrt(Math.Pow(rightBase * leftUncertainty, 2.0) + Math.Pow(leftBase * rightUncertainty, 2.0));
            Unit resultUnit = new Unit(
                CombineSymbols(left.Unit.Symbol, right.Unit.Symbol, "·"),
                left.Unit.Scale * right.Unit.Scale,
                0.0,
                Multiply(left.Unit.Dimension, right.Unit.Dimension));
            return new Measurement(baseValue / resultUnit.Scale, baseUncertainty / resultUnit.Scale, resultUnit);
        }

        public static Measurement Divide(Measurement left, Measurement right)
        {
            ValidateMeasurement(left, nameof(left));
            ValidateMeasurement(right, nameof(right));
            EnsureMultiplicativeUnit(left.Unit, nameof(left));
            EnsureMultiplicativeUnit(right.Unit, nameof(right));
            double leftBase = left.Value * left.Unit.Scale;
            double rightBase = right.Value * right.Unit.Scale;
            if (rightBase == 0.0)
            {
                throw new DivideByZeroException("Measurement division by zero.");
            }
            double leftUncertainty = left.Uncertainty * left.Unit.Scale;
            double rightUncertainty = right.Uncertainty * right.Unit.Scale;
            double baseValue = leftBase / rightBase;
            double baseUncertainty = Math.Sqrt(
                Math.Pow(leftUncertainty / rightBase, 2.0)
                + Math.Pow(leftBase * rightUncertainty / (rightBase * rightBase), 2.0));
            Unit resultUnit = new Unit(
                CombineSymbols(left.Unit.Symbol, right.Unit.Symbol, "/"),
                left.Unit.Scale / right.Unit.Scale,
                0.0,
                Divide(left.Unit.Dimension, right.Unit.Dimension));
            return new Measurement(baseValue / resultUnit.Scale, baseUncertainty / resultUnit.Scale, resultUnit);
        }

        public static Measurement Power(Measurement measurement, int exponent)
        {
            ValidateMeasurement(measurement, nameof(measurement));
            EnsureMultiplicativeUnit(measurement.Unit, nameof(measurement));
            double baseValue = measurement.Value * measurement.Unit.Scale;
            if (baseValue == 0.0 && exponent < 0)
            {
                throw new DivideByZeroException("Zero measurement cannot be raised to a negative power.");
            }
            double value = Math.Pow(baseValue, exponent);
            double exponentValue = exponent;
            double uncertainty = exponent == 0
                ? 0.0
                : Math.Abs(exponentValue * Math.Pow(baseValue, exponentValue - 1.0)) * measurement.Uncertainty * measurement.Unit.Scale;
            double scale = Math.Pow(measurement.Unit.Scale, exponent);
            EnsurePositiveFinite(scale, nameof(exponent));
            Unit resultUnit = new Unit(
                exponent == 0 ? "" : $"{measurement.Unit.Symbol}^{exponent}",
                scale,
                0.0,
                UnitsAndMeasurement.Power(measurement.Unit.Dimension, exponent));
            return new Measurement(value / scale, uncertainty / scale, resultUnit);
        }

        public static int CountSignificantFigures(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            string trimmed = text.Trim();
            if (trimmed.Length == 0 || !double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) || !double.IsFinite(parsed))
            {
                throw new FormatException("Input must be a finite numeric string.");
            }
            int exponentIndex = trimmed.IndexOfAny(new[] { 'e', 'E' });
            string mantissa = exponentIndex < 0 ? trimmed : trimmed[..exponentIndex];
            if (mantissa.StartsWith('+') || mantissa.StartsWith('-'))
            {
                mantissa = mantissa[1..];
            }
            bool hasDecimal = mantissa.Contains('.');
            string digits = mantissa.Replace(".", string.Empty, StringComparison.Ordinal);
            int firstNonZero = 0;
            while (firstNonZero < digits.Length && digits[firstNonZero] == '0')
            {
                firstNonZero++;
            }
            if (firstNonZero == digits.Length)
            {
                return 1;
            }
            int end = digits.Length;
            if (!hasDecimal)
            {
                while (end > firstNonZero && digits[end - 1] == '0')
                {
                    end--;
                }
            }
            return end - firstNonZero;
        }

        public static double RoundToSignificantFigures(double value, int significantFigures)
        {
            EnsureFinite(value, nameof(value));
            ValidateSignificantFigures(significantFigures);
            if (value == 0.0)
            {
                return value;
            }
            double exponent = Math.Floor(Math.Log10(Math.Abs(value)));
            double magnitude = Math.Pow(10.0, exponent);
            if (magnitude == 0.0 || !double.IsFinite(magnitude))
            {
                return value;
            }
            double normalized = value / magnitude;
            double rounded = Math.Round(normalized, significantFigures - 1, MidpointRounding.AwayFromZero) * magnitude;
            if (!double.IsFinite(rounded))
            {
                throw new OverflowException("Rounded value is outside the finite range of Double.");
            }
            return rounded;
        }

        public static string FormatSignificantFigures(double value, int significantFigures)
        {
            EnsureFinite(value, nameof(value));
            ValidateSignificantFigures(significantFigures);
            return RoundToSignificantFigures(value, significantFigures).ToString($"G{significantFigures}", CultureInfo.InvariantCulture);
        }

        public static string FormatMeasurement(Measurement measurement)
        {
            ValidateMeasurement(measurement, nameof(measurement));
            string value = FormatSignificantFigures(measurement.Value, SignificantFiguresFor(measurement.Value));
            string uncertainty = FormatSignificantFigures(measurement.Uncertainty, SignificantFiguresFor(measurement.Uncertainty));
            string unit = measurement.Unit.Symbol.Length == 0 ? string.Empty : $" {measurement.Unit.Symbol}";
            return $"{value} ± {uncertainty}{unit}";
        }

        public static UncertaintyResult CombineAdditive(double firstValue, double firstUncertainty, double secondValue, double secondUncertainty)
        {
            EnsureFinite(firstValue, nameof(firstValue));
            EnsureFinite(secondValue, nameof(secondValue));
            ValidateUncertainty(firstUncertainty, nameof(firstUncertainty));
            ValidateUncertainty(secondUncertainty, nameof(secondUncertainty));
            return new UncertaintyResult(firstValue + secondValue, Math.Sqrt(firstUncertainty * firstUncertainty + secondUncertainty * secondUncertainty));
        }

        public static UncertaintyResult CombineMultiplicative(double firstValue, double firstUncertainty, double secondValue, double secondUncertainty)
        {
            EnsureFinite(firstValue, nameof(firstValue));
            EnsureFinite(secondValue, nameof(secondValue));
            ValidateUncertainty(firstUncertainty, nameof(firstUncertainty));
            ValidateUncertainty(secondUncertainty, nameof(secondUncertainty));
            double value = firstValue * secondValue;
            double uncertainty = Math.Sqrt(
                Math.Pow(secondValue * firstUncertainty, 2.0)
                + Math.Pow(firstValue * secondUncertainty, 2.0));
            return new UncertaintyResult(value, uncertainty);
        }

        public static UncertaintyResult PropagatePower(double value, double uncertainty, int exponent)
        {
            EnsureFinite(value, nameof(value));
            ValidateUncertainty(uncertainty, nameof(uncertainty));
            if (value == 0.0 && exponent < 0)
            {
                throw new DivideByZeroException("Zero cannot be raised to a negative power.");
            }
            double result = Math.Pow(value, exponent);
            double exponentValue = exponent;
            double propagated = exponent == 0
                ? 0.0
                : Math.Abs(exponentValue * Math.Pow(value, exponentValue - 1.0)) * uncertainty;
            return new UncertaintyResult(result, propagated);
        }

        public static UncertaintyResult PropagateFunction(double value, double uncertainty, Func<double, double> function, Func<double, double> derivative)
        {
            EnsureFinite(value, nameof(value));
            ValidateUncertainty(uncertainty, nameof(uncertainty));
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }
            if (derivative == null)
            {
                throw new ArgumentNullException(nameof(derivative));
            }
            double result = function(value);
            double slope = derivative(value);
            EnsureFinite(result, nameof(function));
            EnsureFinite(slope, nameof(derivative));
            return new UncertaintyResult(result, Math.Abs(slope) * uncertainty);
        }

        private static void ValidateMeasurement(Measurement measurement, string parameterName)
        {
            EnsureFinite(measurement.Value, parameterName);
            ValidateUncertainty(measurement.Uncertainty, parameterName);
            ValidateUnit(measurement.Unit, parameterName);
        }

        private static void ValidateUnit(Unit unit, string parameterName)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (unit.Symbol == null)
            {
                throw new ArgumentException("Unit symbol must not be null.", parameterName);
            }
            EnsurePositiveFinite(unit.Scale, parameterName);
            EnsureFinite(unit.Offset, parameterName);
            if (unit.Offset != 0.0 && unit.Dimension != new UnitDimension(0, 0, 0, 0, 1, 0, 0))
            {
                throw new ArgumentException("Only temperature units may use an affine offset.", parameterName);
            }
        }

        private static void EnsureCompatible(Unit first, Unit second)
        {
            if (first.Dimension != second.Dimension)
            {
                throw new ArgumentException("Units have incompatible dimensions.");
            }
        }

        private static void EnsureMultiplicativeUnit(Unit unit, string parameterName)
        {
            if (unit.Offset != 0.0)
            {
                throw new ArgumentException("Affine units cannot be used in multiplication or division.", parameterName);
            }
        }

        private static string CombineSymbols(string first, string second, string operation)
        {
            string left = first.Length == 0 ? "1" : first;
            string right = second.Length == 0 ? "1" : second;
            return $"{left}{operation}{right}";
        }

        private static int SignificantFiguresFor(double value)
        {
            return value == 0.0 ? 1 : 3;
        }

        private static void ValidateSignificantFigures(int significantFigures)
        {
            if (significantFigures < 1 || significantFigures > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(significantFigures), "Significant figure count must be in [1, 15].");
            }
        }

        private static void ValidateUncertainty(double uncertainty, string parameterName)
        {
            if (!double.IsFinite(uncertainty) || uncertainty < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Uncertainty must be finite and non-negative.");
            }
        }

        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        private static void EnsurePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be positive and finite.");
            }
        }
    }

    internal sealed record PhysicalConstant(
        string Symbol,
        double ValueSI,
        Unit Unit,
        bool IsExact,
        string Source,
        double? StandardUncertainty = null);

    internal static class PhysicalConstants
    {
        private const string SiDefinitions = "SI defining constants (2019 SI)";
        private const string Codata2022 = "CODATA 2022 recommended values";

        private static readonly Unit GravitationalConstantUnit = new Unit(
            "m³/(kg·s²)", 1.0, 0.0, new UnitDimension(3, -1, -2, 0, 0, 0, 0));
        private static readonly Unit GasConstantUnit = new Unit(
            "J/(mol·K)", 1.0, 0.0, new UnitDimension(2, 1, -2, 0, -1, -1, 0));
        private static readonly Unit VacuumPermittivityUnit = new Unit(
            "F/m", 1.0, 0.0, new UnitDimension(-3, -1, 4, 2, 0, 0, 0));
        private static readonly Unit VacuumPermeabilityUnit = new Unit(
            "N/A²", 1.0, 0.0, new UnitDimension(1, 1, -2, -2, 0, 0, 0));
        private static readonly Unit PlanckUnit = new Unit(
            "J·s", 1.0, 0.0, new UnitDimension(2, 1, -1, 0, 0, 0, 0));

        public static PhysicalConstant SpeedOfLight { get; } = new PhysicalConstant(
            "c", 299792458.0, UnitCatalog.MeterPerSecond, true, SiDefinitions);
        public static PhysicalConstant StandardGravity { get; } = new PhysicalConstant(
            "g₀", 9.80665, UnitCatalog.MeterPerSecondSquared, true, "Standard gravity convention (exact by definition)");
        public static PhysicalConstant GravitationalConstant { get; } = new PhysicalConstant(
            "G", 6.67430e-11, GravitationalConstantUnit, false, Codata2022, 0.00015e-11);
        public static PhysicalConstant PlanckConstant { get; } = new PhysicalConstant(
            "h", 6.62607015e-34, PlanckUnit, true, SiDefinitions);
        public static PhysicalConstant ReducedPlanckConstant { get; } = new PhysicalConstant(
            "ħ", 6.62607015e-34 / (2.0 * Math.PI), PlanckUnit, true, "Derived from the exact SI Planck constant");
        public static PhysicalConstant BoltzmannConstant { get; } = new PhysicalConstant(
            "kB", 1.380649e-23, new Unit("J/K", 1.0, 0.0, new UnitDimension(2, 1, -2, 0, -1, 0, 0)), true, SiDefinitions);
        public static PhysicalConstant ElementaryCharge { get; } = new PhysicalConstant(
            "e", 1.602176634e-19, UnitCatalog.Coulomb, true, SiDefinitions);
        public static PhysicalConstant AvogadroConstant { get; } = new PhysicalConstant(
            "NA", 6.02214076e23, new Unit("mol⁻¹", 1.0, 0.0, new UnitDimension(0, 0, 0, 0, 0, -1, 0)), true, SiDefinitions);
        public static PhysicalConstant IdealGasConstant { get; } = new PhysicalConstant(
            "R", 8.31446261815324, GasConstantUnit, true, "Derived from the exact SI Boltzmann and Avogadro constants");
        public static PhysicalConstant VacuumPermittivity { get; } = new PhysicalConstant(
            "ε₀", 8.8541878188e-12, VacuumPermittivityUnit, false, Codata2022, 1.4e-21);
        public static PhysicalConstant VacuumPermeability { get; } = new PhysicalConstant(
            "μ₀", 1.25663706127e-6, VacuumPermeabilityUnit, false, Codata2022, 2.0e-16);
    }
}
