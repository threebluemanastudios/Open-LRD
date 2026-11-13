using System;

namespace OPEN_LRD.Visualization
{
    internal sealed class VisualizerOptions
    {
        public const double DefaultSpread = 6.0;
        public const double DefaultCubeSize = 1.0;
        public const double DefaultLineWidth = 0.15;
        public const int DefaultMaxIterations = 300;
        public const double DefaultConvergenceTolerance = 1e-3;
        public const double DefaultMinSpacingFactor = 1.75;
        public const double DefaultGravity = 0.002;
        public const int DefaultMaxNodes = 20000;
        public const int DefaultMaxLinks = 50000;
        public const int DefaultMaxLabels = 1000;
        public const int DefaultLabelCharacterLimit = 48;
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

        public int LabelCharacterLimit { get; set; } = DefaultLabelCharacterLimit;

        public bool ShowLabels { get; set; } = true;

        public double MinSpacing => Math.Max(CubeSize * MinSpacingFactor, MinimumSpacing);

        public VisualizerOptions Clone()
        {
            return (VisualizerOptions)MemberwiseClone();
        }

        public void Validate()
        {
            ValidatePositiveFinite(Spread, nameof(Spread));
            ValidatePositiveFinite(CubeSize, nameof(CubeSize));
            ValidatePositiveFinite(LineWidth, nameof(LineWidth));
            ValidatePositiveFinite(ConvergenceTolerance, nameof(ConvergenceTolerance));
            ValidatePositiveFinite(MinSpacingFactor, nameof(MinSpacingFactor));
            ValidatePositiveInteger(MaxIterations, nameof(MaxIterations));
            ValidatePositiveInteger(MaxNodes, nameof(MaxNodes));
            ValidatePositiveInteger(MaxLinks, nameof(MaxLinks));
            ValidatePositiveInteger(MaxLabels, nameof(MaxLabels));
            ValidatePositiveInteger(LabelCharacterLimit, nameof(LabelCharacterLimit));

            if (!double.IsFinite(Gravity) || Gravity < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Gravity),
                    Gravity,
                    "Gravity must be finite and cannot be negative.");
            }
        }

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
