using System;

namespace OPEN_LRD.Skills
{
    internal static class Thermodynamics
    {
        private static double GasConstant => PhysicalConstants.IdealGasConstant.ValueSI;

        public static double IdealGasPressure(double moles, double volume, double temperatureKelvin)
        {
            ValidateGas(moles, volume, temperatureKelvin);
            return moles * GasConstant * temperatureKelvin / volume;
        }

        public static double IdealGasVolume(double moles, double pressure, double temperatureKelvin)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(pressure, nameof(pressure));
            EnsurePositive(temperatureKelvin, nameof(temperatureKelvin));
            return moles * GasConstant * temperatureKelvin / pressure;
        }

        public static double IdealGasTemperature(double moles, double pressure, double volume)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(pressure, nameof(pressure));
            EnsurePositive(volume, nameof(volume));
            return pressure * volume / (moles * GasConstant);
        }

        public static double IdealGasMoles(double pressure, double volume, double temperatureKelvin)
        {
            EnsurePositive(pressure, nameof(pressure));
            EnsurePositive(volume, nameof(volume));
            EnsurePositive(temperatureKelvin, nameof(temperatureKelvin));
            return pressure * volume / (GasConstant * temperatureKelvin);
        }

        public static double HeatTransfer(double mass, double specificHeat, double temperatureChange)
        {
            EnsurePositive(mass, nameof(mass));
            EnsurePositive(specificHeat, nameof(specificHeat));
            EnsureFinite(temperatureChange, nameof(temperatureChange));
            return mass * specificHeat * temperatureChange;
        }

        public static double LatentHeat(double mass, double specificLatentHeat)
        {
            EnsureNonNegative(mass, nameof(mass));
            EnsureNonNegative(specificLatentHeat, nameof(specificLatentHeat));
            return mass * specificLatentHeat;
        }

        public static double InternalEnergyChange(double heatAdded, double workBySystem)
        {
            EnsureFinite(heatAdded, nameof(heatAdded));
            EnsureFinite(workBySystem, nameof(workBySystem));
            return heatAdded - workBySystem;
        }

        public static double ThermalExpansion(double initialLength, double coefficient, double temperatureChange)
        {
            EnsureNonNegative(initialLength, nameof(initialLength));
            EnsureNonNegative(coefficient, nameof(coefficient));
            EnsureFinite(temperatureChange, nameof(temperatureChange));
            return initialLength * coefficient * temperatureChange;
        }

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

        public static double EntropyChange(double heatTransferred, double reservoirTemperatureKelvin)
        {
            EnsureFinite(heatTransferred, nameof(heatTransferred));
            EnsurePositive(reservoirTemperatureKelvin, nameof(reservoirTemperatureKelvin));
            return heatTransferred / reservoirTemperatureKelvin;
        }

        public static double IdealGasIsothermalEntropyChange(double moles, double initialVolume, double finalVolume)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(initialVolume, nameof(initialVolume));
            EnsurePositive(finalVolume, nameof(finalVolume));
            return moles * GasConstant * Math.Log(finalVolume / initialVolume);
        }

        public static double IdealGasConstantVolumeEntropyChange(double moles, double initialTemperature, double finalTemperature, double molarHeatCapacity)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(initialTemperature, nameof(initialTemperature));
            EnsurePositive(finalTemperature, nameof(finalTemperature));
            EnsurePositive(molarHeatCapacity, nameof(molarHeatCapacity));
            return moles * molarHeatCapacity * Math.Log(finalTemperature / initialTemperature);
        }

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

        private static void ValidateGas(double moles, double volume, double temperature)
        {
            EnsurePositive(moles, nameof(moles));
            EnsurePositive(volume, nameof(volume));
            EnsurePositive(temperature, nameof(temperature));
        }

        private static void EnsurePositive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
            }
        }

        private static void EnsureNonNegative(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be non-negative and finite.");
            }
        }

        private static void EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }
    }
}
