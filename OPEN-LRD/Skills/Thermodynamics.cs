using System;

namespace OPEN_LRD.Skills
{
    internal static class Thermodynamics
    {
        /// <summary>Ideal gas constant in SI units, used by the ideal gas relations.</summary>
        private static double GasConstant => PhysicalConstants.IdealGasConstant.ValueSI;

        /// <summary>Computes the pressure of an ideal gas from its amount, volume, and temperature.</summary>
        public static double IdealGasPressure(double moles, double volume, double temperatureKelvin)
        {
            ValidateGas(moles, volume, temperatureKelvin);
            return moles * GasConstant * temperatureKelvin / volume;
        }

        /// <summary>Computes the volume of an ideal gas from its amount, pressure, and temperature.</summary>
        public static double IdealGasVolume(double moles, double pressure, double temperatureKelvin)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(pressure, nameof(pressure));
            EnsurePositive(temperatureKelvin, nameof(temperatureKelvin));
            return moles * GasConstant * temperatureKelvin / pressure;
        }

        /// <summary>Computes the temperature of an ideal gas from its amount, pressure, and volume.</summary>
        public static double IdealGasTemperature(double moles, double pressure, double volume)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(pressure, nameof(pressure));
            EnsurePositive(volume, nameof(volume));
            return pressure * volume / (moles * GasConstant);
        }

        /// <summary>Computes the amount of substance of an ideal gas from its pressure, volume, and temperature.</summary>
        public static double IdealGasMoles(double pressure, double volume, double temperatureKelvin)
        {
            EnsurePositive(pressure, nameof(pressure));
            EnsurePositive(volume, nameof(volume));
            EnsurePositive(temperatureKelvin, nameof(temperatureKelvin));
            return pressure * volume / (GasConstant * temperatureKelvin);
        }

        /// <summary>Computes the heat added to or removed from a body from its mass, specific heat, and temperature change.</summary>
        public static double HeatTransfer(double mass, double specificHeat, double temperatureChange)
        {
            EnsurePositive(mass, nameof(mass));
            EnsurePositive(specificHeat, nameof(specificHeat));
            EnsureFinite(temperatureChange, nameof(temperatureChange));
            return mass * specificHeat * temperatureChange;
        }

        /// <summary>Computes the heat absorbed or released during a phase change from mass and specific latent heat.</summary>
        public static double LatentHeat(double mass, double specificLatentHeat)
        {
            EnsureNonNegative(mass, nameof(mass));
            EnsureNonNegative(specificLatentHeat, nameof(specificLatentHeat));
            return mass * specificLatentHeat;
        }

        /// <summary>Computes the change in internal energy from heat added to a system and work done by the system.</summary>
        public static double InternalEnergyChange(double heatAdded, double workBySystem)
        {
            EnsureFinite(heatAdded, nameof(heatAdded));
            EnsureFinite(workBySystem, nameof(workBySystem));
            return heatAdded - workBySystem;
        }

        /// <summary>Computes the linear thermal expansion of a solid from its initial length, expansion coefficient, and temperature change.</summary>
        public static double ThermalExpansion(double initialLength, double coefficient, double temperatureChange)
        {
            EnsureNonNegative(initialLength, nameof(initialLength));
            EnsureNonNegative(coefficient, nameof(coefficient));
            EnsureFinite(temperatureChange, nameof(temperatureChange));
            return initialLength * coefficient * temperatureChange;
        }

        /// <summary>Computes the maximum efficiency of a heat engine operating between hot and cold reservoirs.</summary>
        public static double CarnotEfficiency(double hotTemperatureKelvin, double coldTemperatureKelvin)
        {
            EnsurePositive(hotTemperatureKelvin, nameof(hotTemperatureKelvin));
            EnsureNonNegative(coldTemperatureKelvin, nameof(coldTemperatureKelvin));
            if (coldTemperatureKelvin >= hotTemperatureKelvin)
            {
                throw new ArgumentOutOfRangeException(nameof(coldTemperatureKelvin), "Cold-reservoir temperature must be below the hot-reservoir temperature.");
            }
            return 1.0 - coldTemperatureKelvin / hotTemperatureKelvin;
        }

        /// <summary>Computes the entropy change from heat transferred to a reservoir held at a constant temperature.</summary>
        public static double EntropyChange(double heatTransferred, double reservoirTemperatureKelvin)
        {
            EnsureFinite(heatTransferred, nameof(heatTransferred));
            EnsurePositive(reservoirTemperatureKelvin, nameof(reservoirTemperatureKelvin));
            return heatTransferred / reservoirTemperatureKelvin;
        }

        /// <summary>Computes the entropy change of an ideal gas undergoing an isothermal volume change.</summary>
        public static double IdealGasIsothermalEntropyChange(double moles, double initialVolume, double finalVolume)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(initialVolume, nameof(initialVolume));
            EnsurePositive(finalVolume, nameof(finalVolume));
            return moles * GasConstant * Math.Log(finalVolume / initialVolume);
        }

        /// <summary>Computes the entropy change of an ideal gas undergoing a constant-volume temperature change.</summary>
        public static double IdealGasConstantVolumeEntropyChange(double moles, double initialTemperature, double finalTemperature, double molarHeatCapacity)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(initialTemperature, nameof(initialTemperature));
            EnsurePositive(finalTemperature, nameof(finalTemperature));
            EnsurePositive(molarHeatCapacity, nameof(molarHeatCapacity));
            return moles * molarHeatCapacity * Math.Log(finalTemperature / initialTemperature);
        }

        /// <summary>Computes the final temperature of an ideal gas undergoing a reversible adiabatic volume change.</summary>
        public static double AdiabaticFinalTemperature(double initialTemperature, double initialVolume, double finalVolume, double heatCapacityRatio)
        {
            EnsurePositive(initialTemperature, nameof(initialTemperature));
            EnsurePositive(initialVolume, nameof(initialVolume));
            EnsurePositive(finalVolume, nameof(finalVolume));
            if (!double.IsFinite(heatCapacityRatio) || heatCapacityRatio <= 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(heatCapacityRatio), "Heat-capacity ratio must be finite and greater than one.");
            }
            return initialTemperature * Math.Pow(initialVolume / finalVolume, heatCapacityRatio - 1.0);
        }

        /// <summary>Validates that the moles, volume, and temperature of a gas are all positive and finite.</summary>
        private static void ValidateGas(double moles, double volume, double temperature)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(volume, nameof(volume));
            EnsurePositive(temperature, nameof(temperature));
        }

        /// <summary>Throws if a value is not finite or is not strictly positive.</summary>
        private static void EnsurePositive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
            }
        }

        /// <summary>Throws if a value is not finite or is negative.</summary>
        private static void EnsureNonNegative(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be non-negative and finite.");
            }
        }

        /// <summary>Throws if a value is not finite.</summary>
        private static void EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }
    }
}
