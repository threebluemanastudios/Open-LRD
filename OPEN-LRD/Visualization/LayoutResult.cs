using System;
using System.Collections.Generic;
using System.Numerics;

namespace OPEN_LRD.Visualization
{
    internal enum LayoutStage
    {
        Initializing,
        Relaxing,
        Packing,
        Finalizing,
        Completed,
        Cancelled
    }

    internal readonly record struct LayoutProgress(
        LayoutStage Stage,
        int Iteration,
        int MaxIterations,
        double Fraction,
        int CompletedComponents,
        int TotalComponents);

    internal sealed class LayoutResult
    {
        public LayoutResult(
            IReadOnlyList<Vector3> positions,
            Vector3 boundsMinimum,
            Vector3 boundsMaximum,
            Vector3 centroid,
            float radius,
            int iterations,
            bool converged,
            bool cancelled,
            double elapsedSeconds,
            IReadOnlyList<string> warnings,
            int linkCount)
        {
            ArgumentNullException.ThrowIfNull(positions);
            ArgumentNullException.ThrowIfNull(warnings);

            if (!float.IsFinite(radius) || radius < 0f)
                throw new ArgumentOutOfRangeException(nameof(radius));

            if (iterations < 0)
                throw new ArgumentOutOfRangeException(nameof(iterations));

            if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

            if (linkCount < 0)
                throw new ArgumentOutOfRangeException(nameof(linkCount));

            Positions = positions;
            BoundsMinimum = boundsMinimum;
            BoundsMaximum = boundsMaximum;
            Centroid = centroid;
            Radius = radius;
            Iterations = iterations;
            Converged = converged;
            Cancelled = cancelled;
            ElapsedSeconds = elapsedSeconds;
            Warnings = warnings;
            LinkCount = linkCount;
        }

        public IReadOnlyList<Vector3> Positions { get; }

        public Vector3 BoundsMinimum { get; }

        public Vector3 BoundsMaximum { get; }

        public Vector3 Centroid { get; }

        public float Radius { get; }

        public int Iterations { get; }

        public bool Converged { get; }

        public bool Cancelled { get; }

        public double ElapsedSeconds { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int NodeCount => Positions.Count;

        public int LinkCount { get; }

        public bool IsEmpty => NodeCount == 0;
    }
}
