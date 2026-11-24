using System;

namespace OPEN_LRD.Skills
{
    internal static class FluidMechanics
    {
        /// <summary>
        /// Computes hydrostatic pressure from surfacePressure, density, gravity, and depth.
        /// </summary>
        public static double HydrostaticPressure(double surfacePressure, double density, double gravity, double depth)
        {
            EnsureNonNegative(surfacePressure, nameof(surfacePressure));
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(gravity, nameof(gravity));
            EnsureNonNegative(depth, nameof(depth));
            return surfacePressure + density * gravity * depth;
        }

        /// <summary>
        /// Computes buoyant force from fluidDensity, gravity, and displacedVolume.
        /// </summary>
        public static double BuoyantForce(double fluidDensity, double gravity, double displacedVolume)
        {
            EnsurePositive(fluidDensity, nameof(fluidDensity));
            EnsureNonNegative(gravity, nameof(gravity));
            EnsureNonNegative(displacedVolume, nameof(displacedVolume));
            return fluidDensity * gravity * displacedVolume;
        }

        /// <summary>
        /// Computes mass flow rate from density and volumetricFlowRate.
        /// </summary>
        public static double MassFlowRate(double density, double volumetricFlowRate)
        {
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(volumetricFlowRate, nameof(volumetricFlowRate));
            return density * volumetricFlowRate;
        }

        /// <summary>
        /// Computes the outlet velocity from the continuity equation A1 v1 = A2 v2 for incompressible flow.
        /// </summary>
        /// <param name="inletArea">The inlet area.</param>
        /// <param name="inletVelocity">The inlet velocity.</param>
        /// <param name="outletArea">The outlet area.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of its valid range.</exception>
        public static double ContinuityVelocity(double inletArea, double inletVelocity, double outletArea)
        {
            EnsurePositive(inletArea, nameof(inletArea));
            EnsureFinite(inletVelocity, nameof(inletVelocity));
            EnsurePositive(outletArea, nameof(outletArea));
            return inletArea * inletVelocity / outletArea;
        }

        /// <summary>
        /// Computes the absolute pressure at a point from Bernoulli's equation relative to a reference point.
        /// </summary>
        /// <param name="referencePressure">The reference pressure.</param>
        /// <param name="density">The density.</param>
        /// <param name="referenceSpeed">The reference speed.</param>
        /// <param name="speed">The speed.</param>
        /// <param name="elevationChange">The elevation change.</param>
        /// <param name="gravity">The gravity.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of its valid range.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the computed pressure is negative or non-finite.</exception>
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

        /// <summary>
        /// Computes Reynolds number from density, speed, characteristicLength, and dynamicViscosity.
        /// </summary>
        public static double ReynoldsNumber(double density, double speed, double characteristicLength, double dynamicViscosity)
        {
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(speed, nameof(speed));
            EnsurePositive(characteristicLength, nameof(characteristicLength));
            EnsurePositive(dynamicViscosity, nameof(dynamicViscosity));
            return density * speed * characteristicLength / dynamicViscosity;
        }

        /// <summary>
        /// Computes the volumetric flow rate of laminar (Hagen-Poiseuille) flow through a circular pipe.
        /// </summary>
        /// <param name="radius">The radius.</param>
        /// <param name="pressureDrop">The pressure drop.</param>
        /// <param name="pipeLength">The pipe length.</param>
        /// <param name="dynamicViscosity">The dynamic viscosity.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of its valid range.</exception>
        public static double PoiseuilleFlowRate(double radius, double pressureDrop, double pipeLength, double dynamicViscosity)
        {
            EnsurePositive(radius, nameof(radius));
            EnsureNonNegative(pressureDrop, nameof(pressureDrop));
            EnsurePositive(pipeLength, nameof(pipeLength));
            EnsurePositive(dynamicViscosity, nameof(dynamicViscosity));
            return Math.PI * Math.Pow(radius, 4.0) * pressureDrop / (8.0 * dynamicViscosity * pipeLength);
        }

        /// <summary>
        /// Computes the drag force from the drag equation F = 0.5 * Cd * rho * v^2 * A.
        /// </summary>
        /// <param name="dragCoefficient">The drag coefficient.</param>
        /// <param name="density">The density.</param>
        /// <param name="speed">The speed.</param>
        /// <param name="referenceArea">The reference area.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of its valid range.</exception>
        public static double DragForce(double dragCoefficient, double density, double speed, double referenceArea)
        {
            EnsureNonNegative(dragCoefficient, nameof(dragCoefficient));
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(speed, nameof(speed));
            EnsurePositive(referenceArea, nameof(referenceArea));
            return 0.5 * dragCoefficient * density * speed * speed * referenceArea;
        }

        /// <summary>
        /// Computes the frictional pressure drop along a pipe using the Darcy-Weisbach equation.
        /// </summary>
        /// <param name="frictionFactor">The friction factor.</param>
        /// <param name="density">The density.</param>
        /// <param name="speed">The speed.</param>
        /// <param name="pipeLength">The pipe length.</param>
        /// <param name="diameter">The diameter.</param>
        /// <returns>The result.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an argument is out of its valid range.</exception>
        public static double DarcyWeisbachPressureDrop(double frictionFactor, double density, double speed, double pipeLength, double diameter)
        {
            EnsureNonNegative(frictionFactor, nameof(frictionFactor));
            EnsurePositive(density, nameof(density));
            EnsureNonNegative(speed, nameof(speed));
            EnsureNonNegative(pipeLength, nameof(pipeLength));
            EnsurePositive(diameter, nameof(diameter));
            return frictionFactor * pipeLength / diameter * density * speed * speed / 2.0;
        }

        /// <summary>
        /// Throws if value is not finite or is not strictly positive.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="name">The parameter name used in the exception.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not finite or not positive.</exception>
        private static void EnsurePositive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
            }
        }

        /// <summary>
        /// Throws if value is not finite or is negative.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="name">The parameter name used in the exception.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not finite or is negative.</exception>
        private static void EnsureNonNegative(double value, string name)
        {
            if (!double.IsFinite(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be non-negative and finite.");
            }
        }

        /// <summary>
        /// Throws if value is not finite.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="name">The parameter name used in the exception.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not finite.</exception>
        private static void EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }
    }
}
