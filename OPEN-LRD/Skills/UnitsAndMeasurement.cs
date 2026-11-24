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
        /// <summary>Dimension with all exponents zero, used for dimensionless quantities.</summary>
        private static readonly UnitDimension Dimensionless = new UnitDimension(0, 0, 0, 0, 0, 0, 0);
        /// <summary>Dimension of length.</summary>
        private static readonly UnitDimension LengthDimension = new UnitDimension(1, 0, 0, 0, 0, 0, 0);
        /// <summary>Dimension of mass.</summary>
        private static readonly UnitDimension MassDimension = new UnitDimension(0, 1, 0, 0, 0, 0, 0);
        /// <summary>Dimension of time.</summary>
        private static readonly UnitDimension TimeDimension = new UnitDimension(0, 0, 1, 0, 0, 0, 0);
        /// <summary>Dimension of electric current.</summary>
        private static readonly UnitDimension CurrentDimension = new UnitDimension(0, 0, 0, 1, 0, 0, 0);
        /// <summary>Dimension of thermodynamic temperature.</summary>
        private static readonly UnitDimension TemperatureDimension = new UnitDimension(0, 0, 0, 0, 1, 0, 0);
        /// <summary>Dimension of amount of substance.</summary>
        private static readonly UnitDimension AmountDimension = new UnitDimension(0, 0, 0, 0, 0, 1, 0);
        /// <summary>Dimension of luminous intensity.</summary>
        private static readonly UnitDimension LuminousIntensityDimension = new UnitDimension(0, 0, 0, 0, 0, 0, 1);
        /// <summary>Dimension of area (length squared).</summary>
        private static readonly UnitDimension AreaDimension = new UnitDimension(2, 0, 0, 0, 0, 0, 0);
        /// <summary>Dimension of volume (length cubed).</summary>
        private static readonly UnitDimension VolumeDimension = new UnitDimension(3, 0, 0, 0, 0, 0, 0);
        /// <summary>Dimension of force (mass times length divided by time squared).</summary>
        private static readonly UnitDimension ForceDimension = UnitsAndMeasurement.Divide(
            UnitsAndMeasurement.Multiply(MassDimension, LengthDimension), UnitsAndMeasurement.Power(TimeDimension, 2));
        /// <summary>Dimension of energy (force times length).</summary>
        private static readonly UnitDimension EnergyDimension = UnitsAndMeasurement.Multiply(ForceDimension, LengthDimension);
        /// <summary>Dimension of power (energy divided by time).</summary>
        private static readonly UnitDimension PowerDimension = UnitsAndMeasurement.Divide(EnergyDimension, TimeDimension);
        /// <summary>Dimension of pressure (force divided by area).</summary>
        private static readonly UnitDimension PressureDimension = UnitsAndMeasurement.Divide(ForceDimension, AreaDimension);
        /// <summary>Dimension of frequency (inverse time).</summary>
        private static readonly UnitDimension FrequencyDimension = UnitsAndMeasurement.Power(TimeDimension, -1);
        /// <summary>Dimension of electric charge (current times time).</summary>
        private static readonly UnitDimension ChargeDimension = UnitsAndMeasurement.Multiply(CurrentDimension, TimeDimension);
        /// <summary>Dimension of electric potential (power divided by current).</summary>
        private static readonly UnitDimension VoltageDimension = UnitsAndMeasurement.Divide(PowerDimension, CurrentDimension);
        /// <summary>Dimension of electrical resistance (voltage divided by current).</summary>
        private static readonly UnitDimension ResistanceDimension = UnitsAndMeasurement.Divide(VoltageDimension, CurrentDimension);
        /// <summary>Dimension of capacitance (charge divided by voltage).</summary>
        private static readonly UnitDimension CapacitanceDimension = UnitsAndMeasurement.Divide(ChargeDimension, VoltageDimension);
        /// <summary>Dimension of electrical conductance (current divided by voltage).</summary>
        private static readonly UnitDimension ConductanceDimension = UnitsAndMeasurement.Divide(CurrentDimension, VoltageDimension);
        /// <summary>Dimension of magnetic flux (voltage times time).</summary>
        private static readonly UnitDimension MagneticFluxDimension = UnitsAndMeasurement.Multiply(VoltageDimension, TimeDimension);
        /// <summary>Dimension of magnetic flux density (magnetic flux divided by area).</summary>
        private static readonly UnitDimension MagneticFieldDimension = UnitsAndMeasurement.Divide(MagneticFluxDimension, UnitsAndMeasurement.Power(LengthDimension, 2));
        /// <summary>Dimension of inductance (magnetic flux divided by current).</summary>
        private static readonly UnitDimension InductanceDimension = UnitsAndMeasurement.Divide(MagneticFluxDimension, CurrentDimension);
        /// <summary>Dimension of velocity (length divided by time).</summary>
        private static readonly UnitDimension VelocityDimension = UnitsAndMeasurement.Divide(LengthDimension, TimeDimension);
        /// <summary>Dimension of acceleration (velocity divided by time).</summary>
        private static readonly UnitDimension AccelerationDimension = UnitsAndMeasurement.Divide(VelocityDimension, TimeDimension);
        /// <summary>Dimension of mass density (mass divided by volume).</summary>
        private static readonly UnitDimension DensityDimension = UnitsAndMeasurement.Divide(MassDimension, VolumeDimension);
        /// <summary>Dimension of specific heat capacity (energy per mass per temperature).</summary>
        private static readonly UnitDimension SpecificHeatCapacityDimension = UnitsAndMeasurement.Divide(
            EnergyDimension, UnitsAndMeasurement.Multiply(MassDimension, TemperatureDimension));
        /// <summary>Dimension of specific latent heat (energy divided by mass).</summary>
        private static readonly UnitDimension SpecificLatentHeatDimension = UnitsAndMeasurement.Divide(EnergyDimension, MassDimension);
        /// <summary>Dimension of molar heat capacity (energy per amount per temperature).</summary>
        private static readonly UnitDimension MolarHeatCapacityDimension = UnitsAndMeasurement.Divide(
            EnergyDimension, UnitsAndMeasurement.Multiply(AmountDimension, TemperatureDimension));
        /// <summary>Dimension of entropy (energy divided by temperature).</summary>
        private static readonly UnitDimension EntropyDimension = UnitsAndMeasurement.Divide(EnergyDimension, TemperatureDimension);
        /// <summary>Dimension of dynamic viscosity (pressure times time).</summary>
        private static readonly UnitDimension DynamicViscosityDimension = UnitsAndMeasurement.Multiply(PressureDimension, TimeDimension);
        /// <summary>Dimension of volumetric flow rate (volume divided by time).</summary>
        private static readonly UnitDimension VolumetricFlowRateDimension = UnitsAndMeasurement.Divide(VolumeDimension, TimeDimension);
        /// <summary>Dimension of mass flow rate (mass divided by time).</summary>
        private static readonly UnitDimension MassFlowRateDimension = UnitsAndMeasurement.Divide(MassDimension, TimeDimension);
        /// <summary>Dimension of the thermal expansion coefficient (inverse temperature).</summary>
        private static readonly UnitDimension ThermalExpansionCoefficientDimension = UnitsAndMeasurement.Power(TemperatureDimension, -1);
        /// <summary>Dimension of electric field strength (force divided by charge).</summary>
        private static readonly UnitDimension ElectricFieldDimension = UnitsAndMeasurement.Divide(ForceDimension, ChargeDimension);
        /// <summary>Dimension of linear momentum (mass times velocity).</summary>
        private static readonly UnitDimension LinearMomentumDimension = UnitsAndMeasurement.Multiply(MassDimension, VelocityDimension);
        /// <summary>Dimension of wave number (inverse length).</summary>
        private static readonly UnitDimension WaveNumberDimension = UnitsAndMeasurement.Power(LengthDimension, -1);
        /// <summary>Dimension of number density (inverse volume).</summary>
        private static readonly UnitDimension NumberDensityDimension = UnitsAndMeasurement.Power(LengthDimension, -3);
        /// <summary>Dimension of a spring constant (force divided by length).</summary>
        private static readonly UnitDimension SpringConstantDimension = UnitsAndMeasurement.Divide(ForceDimension, LengthDimension);
        /// <summary>Dimension of the Kretschmann scalar (inverse length to the fourth power).</summary>
        private static readonly UnitDimension KretschmannScalarDimension = UnitsAndMeasurement.Power(LengthDimension, -4);

        /// <summary>Unit for dimensionless quantities, with an empty symbol.</summary>
        public static Unit DimensionlessUnit { get; } = new Unit("", 1.0, 0.0, Dimensionless);
        /// <summary>Meter (m), the SI unit of length.</summary>
        public static Unit Meter { get; } = new Unit("m", 1.0, 0.0, LengthDimension);
        /// <summary>Kilometer (km), equal to 1000 meters.</summary>
        public static Unit Kilometer { get; } = new Unit("km", 1000.0, 0.0, LengthDimension);
        /// <summary>Centimeter (cm), equal to 0.01 meters.</summary>
        public static Unit Centimeter { get; } = new Unit("cm", 0.01, 0.0, LengthDimension);
        /// <summary>Millimeter (mm), equal to 0.001 meters.</summary>
        public static Unit Millimeter { get; } = new Unit("mm", 0.001, 0.0, LengthDimension);
        /// <summary>International inch (in), equal to 0.0254 meters.</summary>
        public static Unit Inch { get; } = new Unit("in", 0.0254, 0.0, LengthDimension);
        /// <summary>International foot (ft), equal to 0.3048 meters.</summary>
        public static Unit Foot { get; } = new Unit("ft", 0.3048, 0.0, LengthDimension);
        /// <summary>International yard (yd), equal to 0.9144 meters.</summary>
        public static Unit Yard { get; } = new Unit("yd", 0.9144, 0.0, LengthDimension);
        /// <summary>International mile (mi), equal to 1609.344 meters.</summary>
        public static Unit Mile { get; } = new Unit("mi", 1609.344, 0.0, LengthDimension);
        /// <summary>Kilogram (kg), the SI unit of mass.</summary>
        public static Unit Kilogram { get; } = new Unit("kg", 1.0, 0.0, MassDimension);
        /// <summary>Gram (g), equal to 0.001 kilograms.</summary>
        public static Unit Gram { get; } = new Unit("g", 0.001, 0.0, MassDimension);
        /// <summary>Avoirdupois pound (lb), equal to 0.45359237 kilograms.</summary>
        public static Unit Pound { get; } = new Unit("lb", 0.45359237, 0.0, MassDimension);
        /// <summary>Avoirdupois ounce (oz), equal to one sixteenth of a pound.</summary>
        public static Unit Ounce { get; } = new Unit("oz", 0.028349523125, 0.0, MassDimension);
        /// <summary>US short ton (US ton), equal to 2000 pounds.</summary>
        public static Unit USShortTon { get; } = new Unit("US ton", 907.18474, 0.0, MassDimension);
        /// <summary>Ampere (A), the SI unit of electric current.</summary>
        public static Unit Ampere { get; } = new Unit("A", 1.0, 0.0, CurrentDimension);
        /// <summary>Mole (mol), the SI unit of amount of substance.</summary>
        public static Unit Mole { get; } = new Unit("mol", 1.0, 0.0, AmountDimension);
        /// <summary>Candela (cd), the SI unit of luminous intensity.</summary>
        public static Unit Candela { get; } = new Unit("cd", 1.0, 0.0, LuminousIntensityDimension);
        /// <summary>Second (s), the SI unit of time.</summary>
        public static Unit Second { get; } = new Unit("s", 1.0, 0.0, TimeDimension);
        /// <summary>Minute (min), equal to 60 seconds.</summary>
        public static Unit Minute { get; } = new Unit("min", 60.0, 0.0, TimeDimension);
        /// <summary>Hour (h), equal to 3600 seconds.</summary>
        public static Unit Hour { get; } = new Unit("h", 3600.0, 0.0, TimeDimension);
        /// <summary>Day, equal to 86400 seconds.</summary>
        public static Unit Day { get; } = new Unit("day", 86400.0, 0.0, TimeDimension);
        /// <summary>Kelvin (K), the SI unit of thermodynamic temperature.</summary>
        public static Unit Kelvin { get; } = new Unit("K", 1.0, 0.0, TemperatureDimension);
        /// <summary>Degree Celsius (°C), an offset unit relative to kelvin.</summary>
        public static Unit Celsius { get; } = new Unit("°C", 1.0, 273.15, TemperatureDimension);
        /// <summary>Degree Fahrenheit (°F), an offset unit relative to kelvin.</summary>
        public static Unit Fahrenheit { get; } = new Unit("°F", 5.0 / 9.0, 273.15 - (32.0 * 5.0 / 9.0), TemperatureDimension);
        /// <summary>Cubic meter (m³), the SI unit of volume.</summary>
        public static Unit CubicMeter { get; } = new Unit("m³", 1.0, 0.0, VolumeDimension);
        /// <summary>Liter (L), equal to 0.001 cubic meters.</summary>
        public static Unit Liter { get; } = new Unit("L", 0.001, 0.0, VolumeDimension);
        /// <summary>Milliliter (mL), equal to one millionth of a cubic meter.</summary>
        public static Unit Milliliter { get; } = new Unit("mL", 1e-6, 0.0, VolumeDimension);
        /// <summary>Square meter (m²), the SI unit of area.</summary>
        public static Unit SquareMeter { get; } = new Unit("m²", 1.0, 0.0, AreaDimension);
        /// <summary>Square foot (ft²), the area of a square with one-foot sides.</summary>
        public static Unit SquareFoot { get; } = new Unit("ft²", 0.09290304, 0.0, AreaDimension);
        /// <summary>Square mile (mi²).</summary>
        public static Unit SquareMile { get; } = new Unit("mi²", 2589988.110336, 0.0, AreaDimension);
        /// <summary>International acre (acre).</summary>
        public static Unit Acre { get; } = new Unit("acre", 4046.8564224, 0.0, AreaDimension);
        /// <summary>Cubic inch (in³).</summary>
        public static Unit CubicInch { get; } = new Unit("in³", 1.6387064e-5, 0.0, VolumeDimension);
        /// <summary>Cubic foot (ft³).</summary>
        public static Unit CubicFoot { get; } = new Unit("ft³", 0.028316846592, 0.0, VolumeDimension);
        /// <summary>Cubic yard (yd³).</summary>
        public static Unit CubicYard { get; } = new Unit("yd³", 0.764554857984, 0.0, VolumeDimension);
        /// <summary>US liquid gallon (US gal).</summary>
        public static Unit USLiquidGallon { get; } = new Unit("US gal", 0.003785411784, 0.0, VolumeDimension);
        /// <summary>US liquid quart (US qt).</summary>
        public static Unit USLiquidQuart { get; } = new Unit("US qt", 0.000946352946, 0.0, VolumeDimension);
        /// <summary>US liquid pint (US pt).</summary>
        public static Unit USLiquidPint { get; } = new Unit("US pt", 0.000473176473, 0.0, VolumeDimension);
        /// <summary>US customary cup (US cup).</summary>
        public static Unit USCustomaryCup { get; } = new Unit("US cup", 0.0002365882365, 0.0, VolumeDimension);
        /// <summary>US fluid ounce (US fl oz).</summary>
        public static Unit USFluidOunce { get; } = new Unit("US fl oz", 2.95735295625e-5, 0.0, VolumeDimension);
        /// <summary>US tablespoon (US tbsp).</summary>
        public static Unit USTablespoon { get; } = new Unit("US tbsp", 1.478676478125e-5, 0.0, VolumeDimension);
        /// <summary>US teaspoon (US tsp).</summary>
        public static Unit USTeaspoon { get; } = new Unit("US tsp", 4.92892159375e-6, 0.0, VolumeDimension);
        /// <summary>Newton (N), the SI unit of force.</summary>
        public static Unit Newton { get; } = new Unit("N", 1.0, 0.0, ForceDimension);
        /// <summary>Pound-force (lbf).</summary>
        public static Unit PoundForce { get; } = new Unit("lbf", 4.4482216152605, 0.0, ForceDimension);
        /// <summary>Joule (J), the SI unit of energy.</summary>
        public static Unit Joule { get; } = new Unit("J", 1.0, 0.0, EnergyDimension);
        /// <summary>British thermal unit (Btu).</summary>
        public static Unit BritishThermalUnit { get; } = new Unit("Btu", 1055.05585262, 0.0, EnergyDimension);
        /// <summary>Watt (W), the SI unit of power.</summary>
        public static Unit Watt { get; } = new Unit("W", 1.0, 0.0, PowerDimension);
        /// <summary>Mechanical horsepower (hp).</summary>
        public static Unit HorsepowerMechanical { get; } = new Unit("hp", 745.6998715822702, 0.0, PowerDimension);
        /// <summary>Pascal (Pa), the SI unit of pressure.</summary>
        public static Unit Pascal { get; } = new Unit("Pa", 1.0, 0.0, PressureDimension);
        /// <summary>Pound-force per square inch (psi).</summary>
        public static Unit PoundPerSquareInch { get; } = new Unit("psi", 6894.757293168, 0.0, PressureDimension);
        /// <summary>Hertz (Hz), the SI unit of frequency.</summary>
        public static Unit Hertz { get; } = new Unit("Hz", 1.0, 0.0, FrequencyDimension);
        /// <summary>Coulomb (C), the SI unit of electric charge.</summary>
        public static Unit Coulomb { get; } = new Unit("C", 1.0, 0.0, ChargeDimension);
        /// <summary>Volt (V), the SI unit of electric potential.</summary>
        public static Unit Volt { get; } = new Unit("V", 1.0, 0.0, VoltageDimension);
        /// <summary>Ohm (Ω), the SI unit of electrical resistance.</summary>
        public static Unit Ohm { get; } = new Unit("Ω", 1.0, 0.0, ResistanceDimension);
        /// <summary>Farad (F), the SI unit of capacitance.</summary>
        public static Unit Farad { get; } = new Unit("F", 1.0, 0.0, CapacitanceDimension);
        /// <summary>Siemens (S), the SI unit of electrical conductance.</summary>
        public static Unit Siemens { get; } = new Unit("S", 1.0, 0.0, ConductanceDimension);
        /// <summary>Weber (Wb), the SI unit of magnetic flux.</summary>
        public static Unit Weber { get; } = new Unit("Wb", 1.0, 0.0, MagneticFluxDimension);
        /// <summary>Tesla (T), the SI unit of magnetic flux density.</summary>
        public static Unit Tesla { get; } = new Unit("T", 1.0, 0.0, MagneticFieldDimension);
        /// <summary>Henry (H), the SI unit of inductance.</summary>
        public static Unit Henry { get; } = new Unit("H", 1.0, 0.0, InductanceDimension);
        /// <summary>Meter per second (m/s), the SI unit of velocity.</summary>
        public static Unit MeterPerSecond { get; } = new Unit("m/s", 1.0, 0.0, VelocityDimension);
        /// <summary>Meter per second squared (m/s²), the SI unit of acceleration.</summary>
        public static Unit MeterPerSecondSquared { get; } = new Unit("m/s²", 1.0, 0.0, AccelerationDimension);
        /// <summary>Kilogram per cubic meter (kg/m³), the SI unit of density.</summary>
        public static Unit KilogramPerCubicMeter { get; } = new Unit("kg/m³", 1.0, 0.0, DensityDimension);
        /// <summary>Mile per hour (mph).</summary>
        public static Unit MilePerHour { get; } = new Unit("mph", 0.44704, 0.0, VelocityDimension);
        /// <summary>Foot per second (ft/s).</summary>
        public static Unit FootPerSecond { get; } = new Unit("ft/s", 0.3048, 0.0, VelocityDimension);
        /// <summary>Pound per cubic foot (lb/ft³).</summary>
        public static Unit PoundPerCubicFoot { get; } = new Unit("lb/ft³", 16.01846337396, 0.0, DensityDimension);
        /// <summary>Newton meter (N·m), the unit of torque.</summary>
        public static Unit TorqueUnit { get; } = new Unit("N·m", 1.0, 0.0, EnergyDimension);
        /// <summary>Electronvolt (eV), a unit of energy.</summary>
        public static Unit Electronvolt { get; } = new Unit("eV", 1.602176634e-19, 0.0, EnergyDimension);
        /// <summary>Standard atmosphere (atm).</summary>
        public static Unit Atmosphere { get; } = new Unit("atm", 101325.0, 0.0, PressureDimension);
        /// <summary>Bar (bar), equal to 100000 pascals.</summary>
        public static Unit Bar { get; } = new Unit("bar", 100000.0, 0.0, PressureDimension);
        /// <summary>Astronomical unit (au).</summary>
        public static Unit AstronomicalUnit { get; } = new Unit("au", 149597870700.0, 0.0, LengthDimension);
        /// <summary>Parsec (pc).</summary>
        public static Unit Parsec { get; } = new Unit("pc", 3.0856775814913673e16, 0.0, LengthDimension);
        /// <summary>Joule per kilogram kelvin (J/(kg·K)), the unit of specific heat capacity.</summary>
        public static Unit SpecificHeatCapacity { get; } = new Unit("J/(kg·K)", 1.0, 0.0, SpecificHeatCapacityDimension);
        /// <summary>Joule per kilogram (J/kg), the unit of specific latent heat.</summary>
        public static Unit SpecificLatentHeat { get; } = new Unit("J/kg", 1.0, 0.0, SpecificLatentHeatDimension);
        /// <summary>Joule per mole kelvin (J/(mol·K)), the unit of molar heat capacity.</summary>
        public static Unit MolarHeatCapacity { get; } = new Unit("J/(mol·K)", 1.0, 0.0, MolarHeatCapacityDimension);
        /// <summary>Joule per kelvin (J/K), the unit of entropy.</summary>
        public static Unit Entropy { get; } = new Unit("J/K", 1.0, 0.0, EntropyDimension);
        /// <summary>Pascal second (Pa·s), the unit of dynamic viscosity.</summary>
        public static Unit DynamicViscosity { get; } = new Unit("Pa·s", 1.0, 0.0, DynamicViscosityDimension);
        /// <summary>Cubic meter per second (m³/s), the unit of volumetric flow rate.</summary>
        public static Unit VolumetricFlowRate { get; } = new Unit("m³/s", 1.0, 0.0, VolumetricFlowRateDimension);
        /// <summary>Kilogram per second (kg/s), the unit of mass flow rate.</summary>
        public static Unit MassFlowRate { get; } = new Unit("kg/s", 1.0, 0.0, MassFlowRateDimension);
        /// <summary>Per kelvin (1/K), the unit of thermal expansion coefficient.</summary>
        public static Unit ThermalExpansionCoefficient { get; } = new Unit("1/K", 1.0, 0.0, ThermalExpansionCoefficientDimension);
        /// <summary>Newton per coulomb (N/C), the unit of electric field strength.</summary>
        public static Unit ElectricField { get; } = new Unit("N/C", 1.0, 0.0, ElectricFieldDimension);
        /// <summary>Kilogram meter per second (kg·m/s), the unit of linear momentum.</summary>
        public static Unit LinearMomentum { get; } = new Unit("kg·m/s", 1.0, 0.0, LinearMomentumDimension);
        /// <summary>Newton second (N·s), the unit of impulse.</summary>
        public static Unit Impulse { get; } = new Unit("N·s", 1.0, 0.0, LinearMomentumDimension);
        /// <summary>Radian (rad), a dimensionless unit of angle.</summary>
        public static Unit Radian { get; } = new Unit("rad", 1.0, 0.0, Dimensionless);
        /// <summary>Radian per second (rad/s), the unit of angular velocity.</summary>
        public static Unit RadianPerSecond { get; } = new Unit("rad/s", 1.0, 0.0, FrequencyDimension);
        /// <summary>Reciprocal meter (1/m), the unit of wave number.</summary>
        public static Unit ReciprocalMeter { get; } = new Unit("1/m", 1.0, 0.0, WaveNumberDimension);
        /// <summary>Reciprocal cubic meter (1/m³), the unit of number density.</summary>
        public static Unit ReciprocalCubicMeter { get; } = new Unit("1/m³", 1.0, 0.0, NumberDensityDimension);
        /// <summary>Newton per meter (N/m), the unit of spring constant.</summary>
        public static Unit NewtonPerMeter { get; } = new Unit("N/m", 1.0, 0.0, SpringConstantDimension);
        /// <summary>Reciprocal quartic meter (1/m⁴), the unit of the Kretschmann scalar.</summary>
        public static Unit ReciprocalQuarticMeter { get; } = new Unit("1/m⁴", 1.0, 0.0, KretschmannScalarDimension);
        /// <summary>Lookup table that maps unit symbols to units, built once when the catalog is initialized.</summary>
        private static readonly IReadOnlyDictionary<string, Unit> Units = BuildUnits();

        /// <summary>Maps SI prefix symbols to their powers of ten.</summary>
        private static readonly IReadOnlyDictionary<string, int> PrefixPowers = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Q"] = 30, ["R"] = 27, ["Y"] = 24, ["Z"] = 21, ["E"] = 18,
            ["P"] = 15, ["T"] = 12, ["G"] = 9, ["M"] = 6, ["k"] = 3,
            ["h"] = 2, ["da"] = 1, [""] = 0, ["d"] = -1, ["c"] = -2,
            ["m"] = -3, ["μ"] = -6, ["u"] = -6, ["n"] = -9, ["p"] = -12,
            ["f"] = -15, ["a"] = -18, ["z"] = -21, ["y"] = -24, ["r"] = -27,
            ["q"] = -30
        };

        /// <summary>Units commonly used for length.</summary>
        public static IReadOnlyList<Unit> Length => new[] { Meter, Kilometer, Centimeter, Millimeter, Inch, Foot, Yard, Mile };
        /// <summary>Units commonly used for mass.</summary>
        public static IReadOnlyList<Unit> Mass => new[] { Kilogram, Gram, Pound, Ounce, USShortTon };
        /// <summary>Units commonly used for time.</summary>
        public static IReadOnlyList<Unit> Time => new[] { Second, Minute, Hour, Day };
        /// <summary>Units commonly used for temperature.</summary>
        public static IReadOnlyList<Unit> Temperature => new[] { Kelvin, Celsius, Fahrenheit };
        /// <summary>Units commonly used for area.</summary>
        public static IReadOnlyList<Unit> Area => new[] { SquareMeter, SquareFoot, SquareMile, Acre };
        /// <summary>Units commonly used for volume.</summary>
        public static IReadOnlyList<Unit> Volume => new[]
        {
            CubicMeter, Liter, Milliliter, CubicInch, CubicFoot, CubicYard,
            USLiquidGallon, USLiquidQuart, USLiquidPint, USCustomaryCup,
            USFluidOunce, USTablespoon, USTeaspoon
        };
        /// <summary>Alias for the newton unit.</summary>
        public static Unit Force => Newton;
        /// <summary>Alias for the joule unit.</summary>
        public static Unit Energy => Joule;
        /// <summary>Alias for the watt unit.</summary>
        public static Unit Power => Watt;
        /// <summary>Alias for the pascal unit.</summary>
        public static Unit Pressure => Pascal;
        /// <summary>Alias for the hertz unit.</summary>
        public static Unit Frequency => Hertz;
        /// <summary>Alias for the ampere unit.</summary>
        public static Unit Current => Ampere;
        /// <summary>Alias for the coulomb unit.</summary>
        public static Unit Charge => Coulomb;
        /// <summary>Alias for the volt unit.</summary>
        public static Unit Voltage => Volt;
        /// <summary>Alias for the ohm unit.</summary>
        public static Unit Resistance => Ohm;
        /// <summary>Alias for the farad unit.</summary>
        public static Unit Capacitance => Farad;
        /// <summary>Alias for the siemens unit.</summary>
        public static Unit Conductance => Siemens;
        /// <summary>Alias for the weber unit.</summary>
        public static Unit MagneticFlux => Weber;
        /// <summary>Alias for the tesla unit.</summary>
        public static Unit MagneticField => Tesla;
        /// <summary>Alias for the henry unit.</summary>
        public static Unit Inductance => Henry;
        /// <summary>Alias for the meter per second unit.</summary>
        public static Unit Velocity => MeterPerSecond;
        /// <summary>Alias for the meter per second squared unit.</summary>
        public static Unit Acceleration => MeterPerSecondSquared;
        /// <summary>Alias for the kilogram per cubic meter unit.</summary>
        public static Unit Density => KilogramPerCubicMeter;
        /// <summary>Alias for the newton meter torque unit.</summary>
        public static Unit Torque => TorqueUnit;
        /// <summary>Alias for the pound-force per square inch unit.</summary>
        public static Unit PSI => PoundPerSquareInch;
        /// <summary>Alias for the US liquid gallon unit.</summary>
        public static Unit USGallon => USLiquidGallon;
        /// <summary>Alias for the US liquid quart unit.</summary>
        public static Unit USQuart => USLiquidQuart;
        /// <summary>Alias for the US liquid pint unit.</summary>
        public static Unit USPint => USLiquidPint;
        /// <summary>Alias for the US customary cup unit.</summary>
        public static Unit USCup => USCustomaryCup;

        /// <summary>Creates a unit by applying an SI prefix and an integer exponent to an existing unit, scaling it accordingly.</summary>
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

        /// <summary>Alias for the electronvolt unit.</summary>
        public static Unit ElectronVolt => Electronvolt;
        /// <summary>Alias for the astronomical unit.</summary>
        public static Unit AstronomicalLength => AstronomicalUnit;

        /// <summary>Looks up a unit by its symbol and throws if the symbol is unknown.</summary>
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

        /// <summary>Looks up a unit by its symbol and returns whether it was found.</summary>
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

        /// <summary>Builds the symbol-to-unit lookup table used by FindUnit and TryFindUnit.</summary>
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
        /// <summary>Combines two dimensions by adding their exponents.</summary>
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

        /// <summary>Combines two dimensions by subtracting the exponents of the right dimension from the left.</summary>
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

        /// <summary>Raises a dimension to an integer power by multiplying each exponent.</summary>
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

        /// <summary>Looks up a unit by its symbol through the unit catalog and throws if the symbol is unknown.</summary>
        public static Unit FindUnit(string symbol)
        {
            return UnitCatalog.FindUnit(symbol);
        }

        /// <summary>Looks up a unit by its symbol through the unit catalog and returns whether it was found.</summary>
        public static bool TryFindUnit(string symbol, out Unit unit)
        {
            return UnitCatalog.TryFindUnit(symbol, out unit);
        }

        /// <summary>Converts a numeric value from one unit to another unit of the same dimension.</summary>
        public static double Convert(double value, Unit from, Unit to)
        {
            ValidateUnit(from, nameof(from));
            ValidateUnit(to, nameof(to));
            EnsureCompatible(from, to);
            return ConvertFromSI(ConvertToSI(value, from), to);
        }

        /// <summary>Converts a value to its SI base-unit value (for example, length to meters).</summary>
        public static double ConvertToSI(double value, Unit from)
        {
            ValidateUnit(from, nameof(from));
            EnsureFinite(value, nameof(value));
            double result = value * from.Scale + from.Offset;
            EnsureFinite(result, nameof(value));
            return result;
        }

        /// <summary>Converts an SI base-unit value into the requested unit.</summary>
        public static double ConvertFromSI(double siValue, Unit to)
        {
            ValidateUnit(to, nameof(to));
            EnsureFinite(siValue, nameof(siValue));
            double result = (siValue - to.Offset) / to.Scale;
            EnsureFinite(result, nameof(siValue));
            return result;
        }

        /// <summary>Converts a value between units if their dimensions match, returning false rather than throwing when they do not.</summary>
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

        /// <summary>Converts a measurement to another unit of the same dimension, scaling its uncertainty accordingly.</summary>
        public static Measurement Convert(Measurement measurement, Unit to)
        {
            ValidateMeasurement(measurement, nameof(measurement));
            ValidateUnit(to, nameof(to));
            EnsureCompatible(measurement.Unit, to);
            double value = Convert(measurement.Value, measurement.Unit, to);
            double uncertainty = measurement.Uncertainty * measurement.Unit.Scale / to.Scale;
            return new Measurement(value, uncertainty, to);
        }

        /// <summary>Adds two measurements with compatible units and combines their uncertainties in quadrature.</summary>
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

        /// <summary>Subtracts one measurement from another with compatible units and combines their uncertainties in quadrature.</summary>
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

        /// <summary>Multiplies two measurements and propagates their uncertainties, producing a unit that combines both symbols.</summary>
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

        /// <summary>Divides one measurement by another and propagates their uncertainties, producing a unit that combines both symbols.</summary>
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

        /// <summary>Raises a measurement to an integer power and propagates its uncertainty.</summary>
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

        /// <summary>Counts the significant figures in a numeric string.</summary>
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

        /// <summary>Rounds a value to the given number of significant figures.</summary>
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

        /// <summary>Formats a value rounded to the given number of significant figures.</summary>
        public static string FormatSignificantFigures(double value, int significantFigures)
        {
            EnsureFinite(value, nameof(value));
            ValidateSignificantFigures(significantFigures);
            return RoundToSignificantFigures(value, significantFigures).ToString($"G{significantFigures}", CultureInfo.InvariantCulture);
        }

        /// <summary>Formats a measurement as its value plus or minus its uncertainty, followed by the unit symbol.</summary>
        public static string FormatMeasurement(Measurement measurement)
        {
            ValidateMeasurement(measurement, nameof(measurement));
            string value = FormatSignificantFigures(measurement.Value, SignificantFiguresFor(measurement.Value));
            string uncertainty = FormatSignificantFigures(measurement.Uncertainty, SignificantFiguresFor(measurement.Uncertainty));
            string unit = measurement.Unit.Symbol.Length == 0 ? string.Empty : $" {measurement.Unit.Symbol}";
            return $"{value} ± {uncertainty}{unit}";
        }

        /// <summary>Adds two values and combines their uncertainties in quadrature.</summary>
        public static UncertaintyResult CombineAdditive(double firstValue, double firstUncertainty, double secondValue, double secondUncertainty)
        {
            EnsureFinite(firstValue, nameof(firstValue));
            EnsureFinite(secondValue, nameof(secondValue));
            ValidateUncertainty(firstUncertainty, nameof(firstUncertainty));
            ValidateUncertainty(secondUncertainty, nameof(secondUncertainty));
            return new UncertaintyResult(firstValue + secondValue, Math.Sqrt(firstUncertainty * firstUncertainty + secondUncertainty * secondUncertainty));
        }

        /// <summary>Multiplies two values and propagates their uncertainties to first order.</summary>
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

        /// <summary>Raises a value with uncertainty to an integer power and propagates the uncertainty.</summary>
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

        /// <summary>Applies a function to a value with uncertainty and propagates the uncertainty using the function's derivative.</summary>
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

        /// <summary>Validates that a measurement has a finite value, a valid uncertainty, and a valid unit.</summary>
        private static void ValidateMeasurement(Measurement measurement, string parameterName)
        {
            EnsureFinite(measurement.Value, parameterName);
            ValidateUncertainty(measurement.Uncertainty, parameterName);
            ValidateUnit(measurement.Unit, parameterName);
        }

        /// <summary>Validates that a unit is non-null, has a symbol, a positive finite scale, and an offset used only by temperature units.</summary>
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

        /// <summary>Throws if two units do not share the same dimension.</summary>
        private static void EnsureCompatible(Unit first, Unit second)
        {
            if (first.Dimension != second.Dimension)
            {
                throw new ArgumentException("Units have incompatible dimensions.");
            }
        }

        /// <summary>Throws if a unit has an affine offset, since such units cannot be multiplied or divided.</summary>
        private static void EnsureMultiplicativeUnit(Unit unit, string parameterName)
        {
            if (unit.Offset != 0.0)
            {
                throw new ArgumentException("Affine units cannot be used in multiplication or division.", parameterName);
            }
        }

        /// <summary>Builds a combined unit symbol from two symbols and an operator, substituting 1 for an empty symbol.</summary>
        private static string CombineSymbols(string first, string second, string operation)
        {
            string left = first.Length == 0 ? "1" : first;
            string right = second.Length == 0 ? "1" : second;
            return $"{left}{operation}{right}";
        }

        /// <summary>Returns the default number of significant figures used when formatting a value: 1 for zero, otherwise 3.</summary>
        private static int SignificantFiguresFor(double value)
        {
            return value == 0.0 ? 1 : 3;
        }

        /// <summary>Throws if the significant figure count is outside the range 1 to 15.</summary>
        private static void ValidateSignificantFigures(int significantFigures)
        {
            if (significantFigures < 1 || significantFigures > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(significantFigures), "Significant figure count must be in [1, 15].");
            }
        }

        /// <summary>Throws if an uncertainty is negative or not finite.</summary>
        private static void ValidateUncertainty(double uncertainty, string parameterName)
        {
            if (!double.IsFinite(uncertainty) || uncertainty < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Uncertainty must be finite and non-negative.");
            }
        }

        /// <summary>Throws if a value is not finite.</summary>
        private static void EnsureFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
            }
        }

        /// <summary>Throws if a value is not positive and finite.</summary>
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
        /// <summary>Source label for SI defining constants.</summary>
        private const string SiDefinitions = "SI defining constants (2019 SI)";
        /// <summary>Source label for CODATA 2022 recommended values.</summary>
        private const string Codata2022 = "CODATA 2022 recommended values";

        /// <summary>Unit m³/(kg·s²) of the gravitational constant.</summary>
        private static readonly Unit GravitationalConstantUnit = new Unit(
            "m³/(kg·s²)", 1.0, 0.0, new UnitDimension(3, -1, -2, 0, 0, 0, 0));
        /// <summary>Unit J/(mol·K) of the ideal gas constant.</summary>
        private static readonly Unit GasConstantUnit = new Unit(
            "J/(mol·K)", 1.0, 0.0, new UnitDimension(2, 1, -2, 0, -1, -1, 0));
        /// <summary>Unit F/m of the vacuum permittivity.</summary>
        private static readonly Unit VacuumPermittivityUnit = new Unit(
            "F/m", 1.0, 0.0, new UnitDimension(-3, -1, 4, 2, 0, 0, 0));
        /// <summary>Unit N/A² of the vacuum permeability.</summary>
        private static readonly Unit VacuumPermeabilityUnit = new Unit(
            "N/A²", 1.0, 0.0, new UnitDimension(1, 1, -2, -2, 0, 0, 0));
        /// <summary>Unit J·s of the Planck constant.</summary>
        private static readonly Unit PlanckUnit = new Unit(
            "J·s", 1.0, 0.0, new UnitDimension(2, 1, -1, 0, 0, 0, 0));

        /// <summary>Speed of light in vacuum, an exact value in SI units.</summary>
        public static PhysicalConstant SpeedOfLight { get; } = new PhysicalConstant(
            "c", 299792458.0, UnitCatalog.MeterPerSecond, true, SiDefinitions);
        /// <summary>Standard acceleration of gravity, exact by convention.</summary>
        public static PhysicalConstant StandardGravity { get; } = new PhysicalConstant(
            "g₀", 9.80665, UnitCatalog.MeterPerSecondSquared, true, "Standard gravity convention (exact by definition)");
        /// <summary>Newtonian gravitational constant, from CODATA 2022.</summary>
        public static PhysicalConstant GravitationalConstant { get; } = new PhysicalConstant(
            "G", 6.67430e-11, GravitationalConstantUnit, false, Codata2022, 0.00015e-11);
        /// <summary>Planck constant, an exact value in SI units.</summary>
        public static PhysicalConstant PlanckConstant { get; } = new PhysicalConstant(
            "h", 6.62607015e-34, PlanckUnit, true, SiDefinitions);
        /// <summary>Reduced Planck constant, equal to the Planck constant divided by 2π.</summary>
        public static PhysicalConstant ReducedPlanckConstant { get; } = new PhysicalConstant(
            "ħ", 6.62607015e-34 / (2.0 * Math.PI), PlanckUnit, true, "Derived from the exact SI Planck constant");
        /// <summary>Boltzmann constant, an exact value in SI units.</summary>
        public static PhysicalConstant BoltzmannConstant { get; } = new PhysicalConstant(
            "kB", 1.380649e-23, new Unit("J/K", 1.0, 0.0, new UnitDimension(2, 1, -2, 0, -1, 0, 0)), true, SiDefinitions);
        /// <summary>Elementary charge, an exact value in SI units.</summary>
        public static PhysicalConstant ElementaryCharge { get; } = new PhysicalConstant(
            "e", 1.602176634e-19, UnitCatalog.Coulomb, true, SiDefinitions);
        /// <summary>Avogadro constant, an exact value in SI units.</summary>
        public static PhysicalConstant AvogadroConstant { get; } = new PhysicalConstant(
            "NA", 6.02214076e23, new Unit("mol⁻¹", 1.0, 0.0, new UnitDimension(0, 0, 0, 0, 0, -1, 0)), true, SiDefinitions);
        /// <summary>Molar ideal gas constant, derived from the exact Boltzmann and Avogadro constants.</summary>
        public static PhysicalConstant IdealGasConstant { get; } = new PhysicalConstant(
            "R", 8.31446261815324, GasConstantUnit, true, "Derived from the exact SI Boltzmann and Avogadro constants");
        /// <summary>Vacuum electric permittivity, from CODATA 2022.</summary>
        public static PhysicalConstant VacuumPermittivity { get; } = new PhysicalConstant(
            "ε₀", 8.8541878188e-12, VacuumPermittivityUnit, false, Codata2022, 1.4e-21);
        /// <summary>Vacuum magnetic permeability, from CODATA 2022.</summary>
        public static PhysicalConstant VacuumPermeability { get; } = new PhysicalConstant(
            "μ₀", 1.25663706127e-6, VacuumPermeabilityUnit, false, Codata2022, 2.0e-16);
    }
}
