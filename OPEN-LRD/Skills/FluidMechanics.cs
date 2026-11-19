using System;

namespace OPEN_LRD.Skills
{
    internal static class FluidMechanics
    {
        public static double HydrostaticPressure(double surfacePressure, double density, double gravity, double depth)
        {
            EnsureNonNegative(surfacePressure, nameof(surfacePressure));
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(gravity, nameof(gravity));
            EnsureNonNegative(depth, nameof(depth));
            return surfacePressure + density * gravity * depth;
        }

        public static double BuoyantForce(double fluidDensity, double gravity, double displacedVolume)
        {
            EnsurePositive(fluidDensity, nameof(fluidDensity));
            EnsureNonNegative(gravity, nameof(gravity));
            EnsureNonNegative(displacedVolume, nameof(displacedVolume));
            return fluidDensity * gravity * displacedVolume;
        }

        public static double MassFlowRate(double density, double volumetricFlowRate)
        {
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(volumetricFlowRate, nameof(volumetricFlowRate));
            return density * volumetricFlowRate;
        }

        public static double ContinuityVelocity(double inletArea, double inletVelocity, double outletArea)
        {
            EnsurePositive(inletArea, nameof(inletArea));
            EnsureFinite(inletVelocity, nameof(inletVelocity));
            EnsurePositive(outletArea, nameof(outletArea));
            return inletArea * inletVelocity / outletArea;
        }

        public static double BernoulliPressure(double referencePressure, double density, double referenceSpeed, double speed, double elevationChange, double gravity)
        {
            EnsureNonNegative(referencePressure, nameof(referencePressure));
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(referenceSpeed, nameof(referenceSpeed));
            EnsureNonNegative(speed, nameof(speed));
            EnsureFinite(elevationChange, nameof(elevationChange));
            EnsureNonNegative(gravity, nameof(gravity));
            double pressure = referencePressure + 0.5 * density * (referenceSpeed * referenceSpeed - speed * speed)
                - density * gravity * elevationChange;
            if (!double.IsFinite(pressure) || pressure < 0.0)
            {
                throw new InvalidOperationException("Bernoulli model produced a negative or non-finite absolute pressure.");
            }
            return pressure;
        }

        public static double ReynoldsNumber(double density, double speed, double characteristicLength, double dynamicViscosity)
        {
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(speed, nameof(speed));
            EnsurePositive(characteristicLength, nameof(characteristicLength));
            EnsurePositive(dynamicViscosity, nameof(dynamicViscosity));
            return density * speed * characteristicLength / dynamicViscosity;
        }

        public static double PoiseuilleFlowRate(double radius, double pressureDrop, double pipeLength, double dynamicViscosity)
        {
            EnsurePositive(radius, nameof(radius));
            EnsureNonNegative(pressureDrop, nameof(pressureDrop));
            EnsurePositive(pipeLength, nameof(pipeLength));
            EnsurePositive(dynamicViscosity, nameof(dynamicViscosity));
            return Math.PI * Math.Pow(radius, 4.0) * pressureDrop / (8.0 * dynamicViscosity * pipeLength);
        }

        public static double DragForce(double dragCoefficient, double density, double speed, double referenceArea)
        {
            EnsureNonNegative(dragCoefficient, nameof(dragCoefficient));
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(speed, nameof(speed));
            EnsurePositive(referenceArea, nameof(referenceArea));
            return 0.5 * dragCoefficient * density * speed * speed * referenceArea;
        }

        public static double DarcyWeisbachPressureDrop(double frictionFactor, double density, double speed, double pipeLength, double diameter)
        {
            EnsureNonNegative(frictionFactor, nameof(frictionFactor));
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(speed, nameof(speed));
            EnsureNonNegative(pipeLength, nameof(pipeLength));
            EnsurePositive(diameter, nameof(diameter));
            return frictionFactor * pipeLength / diameter * density * speed * speed / 2.0;
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
