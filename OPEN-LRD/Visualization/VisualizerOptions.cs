using System;

namespace OPEN_LRD.Visualization
{
    /// <summary>Controls whether labels focus on current interaction or a bounded visible subset.</summary>
    internal enum VisualizerLabelMode
    {
        Focused = 0,
        AllVisible = 1
    }

    /// <summary>Configures graph layout, rendering limits, camera behavior, and label presentation.</summary>
    internal sealed class VisualizerOptions
    {
        public const double MinimumSpread = 10.0;
        public const double MaximumSpread = 100.0;
        public const double DefaultSpread = 20.0;
        public const double DefaultCubeSize = 1.0;
        public const double DefaultLineWidth = 0.15;
        public const int DefaultMaxIterations = 300;
        public const double DefaultConvergenceTolerance = 1e-3;
        public const double DefaultMinSpacingFactor = 1.75;
        public const double DefaultGravity = 0.002;
        public const int DefaultMaxNodes = 20000;
        public const int DefaultMaxLinks = 50000;
        public const int MaximumMaxNodes = 100000;
        public const int MaximumMaxLinks = 250000;
        public const int MinimumMaxLabels = 2;
        public const int DefaultMaxLabels = 50;
        public const int MaximumMaxLabels = 50;
        public const int MaxLabelCandidatesPerFrame = 250;
        public const int SpatialIndexLeafCapacity = 32;
        public const int DefaultLabelCharacterLimit = 48;
        public const double DefaultNearPlaneDistance = 0.05;
        public const double MinimumSpacing = 0.05;

        public double Spread { get; set; } = DefaultSpread;

        public double CubeSize { get; set; } = DefaultCubeSize;

        public double LineWidth { get; set; } = DefaultLineWidth;

        public int MaxIterations { get; set; } = DefaultMaxIterations;

        public double ConvergenceTolerance { get; set; } = DefaultConvergenceTolerance;

        public double MinSpacingFactor { get; set; } = DefaultMinSpacingFactor;

        public double Gravity { get; set; } = DefaultGravity;

        public int MaxNodes { get; set; } = DefaultMaxNodes;

        public int MaxLinks { get; set; } = DefaultMaxLinks;

        public int MaxLabels { get; set; } = DefaultMaxLabels;

        public VisualizerLabelMode LabelMode { get; set; } = VisualizerLabelMode.Focused;

        public int LabelCharacterLimit { get; set; } = DefaultLabelCharacterLimit;

        public bool ShowLabels { get; set; } = true;

        public bool InvertMouseX { get; set; } = true;

        public bool InvertMouseY { get; set; } = false;

        public double NearPlaneDistance { get; set; } = DefaultNearPlaneDistance;

        public double MinSpacing => Math.Max(CubeSize * MinSpacingFactor, MinimumSpacing);

        /// <summary>Clamps and rounds a spread value to a valid whole-number setting.</summary>
        public static double NormalizeSpread(double value)
        {
            if (!double.IsFinite(value))
                return DefaultSpread;

            double clamped = Math.Clamp(value, MinimumSpread, MaximumSpread);
            return Math.Round(clamped, MidpointRounding.AwayFromZero);
        }

        /// <summary>Creates a shallow copy of the current visualizer options.</summary>
        public VisualizerOptions Clone()
        {
            return (VisualizerOptions)MemberwiseClone();
        }

        /// <summary>Validates that all option values fall within their supported ranges.</summary>
        public void Validate()
        {
            if (!double.IsFinite(Spread)
                || Spread < MinimumSpread
                || Spread > MaximumSpread
                || Spread != Math.Truncate(Spread))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Spread),
                    Spread,
                    $"Spread must be a whole number between {MinimumSpread} and {MaximumSpread}.");
            }
            ValidatePositiveFinite(CubeSize, nameof(CubeSize));
            ValidatePositiveFinite(LineWidth, nameof(LineWidth));
            ValidatePositiveFinite(ConvergenceTolerance, nameof(ConvergenceTolerance));
            ValidatePositiveFinite(MinSpacingFactor, nameof(MinSpacingFactor));
            ValidatePositiveInteger(MaxIterations, nameof(MaxIterations));
            ValidatePositiveInteger(MaxNodes, nameof(MaxNodes));
            ValidatePositiveInteger(MaxLinks, nameof(MaxLinks));
            if (MaxLabels < MinimumMaxLabels || MaxLabels > MaximumMaxLabels)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxLabels),
                    MaxLabels,
                    $"MaxLabels must be between {MinimumMaxLabels} and {MaximumMaxLabels}.");
            }
            ValidatePositiveInteger(LabelCharacterLimit, nameof(LabelCharacterLimit));

            if (!Enum.IsDefined(LabelMode))
                throw new ArgumentOutOfRangeException(nameof(LabelMode), LabelMode, "LabelMode is invalid.");

            if (MaxNodes > MaximumMaxNodes)
                throw new ArgumentOutOfRangeException(nameof(MaxNodes), MaxNodes, $"MaxNodes cannot exceed {MaximumMaxNodes}.");
            if (MaxLinks > MaximumMaxLinks)
                throw new ArgumentOutOfRangeException(nameof(MaxLinks), MaxLinks, $"MaxLinks cannot exceed {MaximumMaxLinks}.");

            if (!double.IsFinite(NearPlaneDistance) || NearPlaneDistance < 0.01 || NearPlaneDistance > 100.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(NearPlaneDistance),
                    NearPlaneDistance,
                    "NearPlaneDistance must be finite and between 0.01 and 100.0.");
            }

            if (!double.IsFinite(Gravity) || Gravity < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Gravity),
                    Gravity,
                    "Gravity must be finite and cannot be negative.");
            }
        }

        /// <summary>Throws when a floating-point option is not finite and greater than zero.</summary>
        private static void ValidatePositiveFinite(double value, string parameterName)
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be finite and greater than zero.");
            }
        }

        /// <summary>Throws when an integer option is not greater than zero.</summary>
        private static void ValidatePositiveInteger(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be greater than zero.");
            }
        }
    }
}
