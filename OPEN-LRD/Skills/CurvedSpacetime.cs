using System;
using OPEN_LRD.Foundations;

namespace OPEN_LRD.Skills
{
    internal delegate double[,] SpacetimeMetric(double[] coordinates);

    internal sealed record GeodesicSolution(
        double[] AffineParameter,
        double[][] Coordinates,
        double[][] Tangents,
        bool Completed);

    internal static class CurvedSpacetime
    {
        private const int Dimension = 4;

        public static double[,] MinkowskiMetric(double[] coordinates)
        {
            ValidateCoordinates(coordinates);
            return new double[,]
            {
                { -1.0, 0.0, 0.0, 0.0 },
                { 0.0, 1.0, 0.0, 0.0 },
                { 0.0, 0.0, 1.0, 0.0 },
                { 0.0, 0.0, 0.0, 1.0 }
            };
        }

        public static double[,] SchwarzschildMetric(double massLength, double[] coordinates)
        {
            EnsurePositive(massLength, nameof(massLength));
            ValidateCoordinates(coordinates);
            double radius = coordinates[1];
            if (radius <= 2.0 * massLength)
            {
                throw new ArgumentOutOfRangeException(nameof(coordinates), "Schwarzschild coordinates require r > 2M.");
            }
            double sine = Math.Sin(coordinates[2]);
            if (Math.Abs(sine) < 1e-12)
            {
                throw new ArgumentOutOfRangeException(nameof(coordinates), "Schwarzschild polar coordinates are singular at the poles.");
            }
            double factor = 1.0 - 2.0 * massLength / radius;
            return new double[,]
            {
                { -factor, 0.0, 0.0, 0.0 },
                { 0.0, 1.0 / factor, 0.0, 0.0 },
                { 0.0, 0.0, radius * radius, 0.0 },
                { 0.0, 0.0, 0.0, radius * radius * sine * sine }
            };
        }

        public static double SchwarzschildRadius(double massKg)
        {
            EnsurePositive(massKg, nameof(massKg));
            double c = PhysicalConstants.SpeedOfLight.ValueSI;
            return 2.0 * PhysicalConstants.GravitationalConstant.ValueSI * massKg / (c * c);
        }

        public static double SchwarzschildMassLength(double massKg)
        {
            EnsurePositive(massKg, nameof(massKg));
            double c = PhysicalConstants.SpeedOfLight.ValueSI;
            return PhysicalConstants.GravitationalConstant.ValueSI * massKg / (c * c);
        }

        public static double MetricInterval(SpacetimeMetric metric, double[] point, double[] displacement)
        {
            if (metric == null)
            {
                throw new ArgumentNullException(nameof(metric));
            }
            ValidateCoordinates(point);
            ValidateCoordinates(displacement);
            double[,] tensor = ValidateMetric(metric(point));
            double interval = 0.0;
            for (int first = 0; first < Dimension; first++)
            {
                for (int second = 0; second < Dimension; second++)
                {
                    interval += tensor[first, second] * displacement[first] * displacement[second];
                }
            }
            return EnsureFinite(interval, nameof(metric));
        }

        public static double[,,] ChristoffelSymbols(SpacetimeMetric metric, double[] coordinates, double finiteDifferenceStep)
        {
            if (metric == null)
            {
                throw new ArgumentNullException(nameof(metric));
            }
            ValidateCoordinates(coordinates);
            EnsurePositive(finiteDifferenceStep, nameof(finiteDifferenceStep));
            double[,] tensor = ValidateMetric(metric(coordinates));
            double[,] inverse = Algebra.MatrixInverse(tensor);
            double[,,] derivatives = new double[Dimension, Dimension, Dimension];
            for (int coordinate = 0; coordinate < Dimension; coordinate++)
            {
                int variedCoordinate = coordinate;
                double derivativeStep = finiteDifferenceStep * Math.Max(1.0, Math.Abs(coordinates[coordinate]));
                for (int row = 0; row < Dimension; row++)
                {
                    for (int column = 0; column < Dimension; column++)
                    {
                        int metricRow = row;
                        int metricColumn = column;
                        double derivative = Math_3.NumericDerivative(
                            variedValue =>
                            {
                                double[] sample = (double[])coordinates.Clone();
                                sample[variedCoordinate] = variedValue;
                                return ValidateMetricShape(metric(sample))[metricRow, metricColumn];
                            },
                            coordinates[coordinate],
                            derivativeStep);
                        derivatives[coordinate, row, column] = EnsureFinite(derivative, nameof(metric));
                    }
                }
            }

            double[,,] symbols = new double[Dimension, Dimension, Dimension];
            for (int upper = 0; upper < Dimension; upper++)
            {
                for (int first = 0; first < Dimension; first++)
                {
                    for (int second = 0; second < Dimension; second++)
                    {
                        double sum = 0.0;
                        for (int contracted = 0; contracted < Dimension; contracted++)
                        {
                            sum += inverse[upper, contracted]
                                * (derivatives[first, contracted, second]
                                    + derivatives[second, contracted, first]
                                    - derivatives[contracted, first, second]);
                        }
                        symbols[upper, first, second] = EnsureFinite(0.5 * sum, nameof(metric));
                    }
                }
            }
            return symbols;
        }

        public static GeodesicSolution IntegrateGeodesic(
            SpacetimeMetric metric,
            double[] initialCoordinates,
            double[] initialTangent,
            double affineStart,
            double affineEnd,
            double tolerance,
            int maxSteps)
        {
            if (metric == null)
            {
                throw new ArgumentNullException(nameof(metric));
            }
            ValidateCoordinates(initialCoordinates);
            ValidateCoordinates(initialTangent);
            EnsureFinite(affineStart, nameof(affineStart));
            EnsureFinite(affineEnd, nameof(affineEnd));
            EnsurePositive(tolerance, nameof(tolerance));
            if (affineEnd <= affineStart)
            {
                throw new ArgumentOutOfRangeException(nameof(affineEnd), "Affine end must exceed affine start.");
            }
            if (maxSteps < 1 || maxSteps > 100000)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSteps), "Maximum steps must be in [1, 100000].");
            }
            _ = ValidateMetric(metric(initialCoordinates));
            double[] initialState = new double[Dimension * 2];
            Array.Copy(initialCoordinates, 0, initialState, 0, Dimension);
            Array.Copy(initialTangent, 0, initialState, Dimension, Dimension);

            OdeSystemSolution integration = NumericalMethods.RungeKutta45System(
                (affine, state) =>
                {
                    _ = affine;
                    double[] position = new double[Dimension];
                    Array.Copy(state, 0, position, 0, Dimension);
                    double[] tangent = new double[Dimension];
                    Array.Copy(state, Dimension, tangent, 0, Dimension);
                    double[,,] gamma = ChristoffelSymbols(metric, position, Math.Max(1e-7, tolerance * 0.1));
                    double[] derivative = new double[Dimension * 2];
                    Array.Copy(tangent, 0, derivative, 0, Dimension);
                    for (int upper = 0; upper < Dimension; upper++)
                    {
                        double acceleration = 0.0;
                        for (int first = 0; first < Dimension; first++)
                        {
                            for (int second = 0; second < Dimension; second++)
                            {
                                acceleration -= gamma[upper, first, second] * tangent[first] * tangent[second];
                            }
                        }
                        derivative[Dimension + upper] = EnsureFinite(acceleration, nameof(metric));
                    }
                    return derivative;
                },
                affineStart,
                affineEnd,
                initialState,
                tolerance,
                maxSteps);

            if (integration.X.Length - 1 > maxSteps)
            {
                throw new InvalidOperationException("Geodesic integration exceeded the requested accepted-step limit.");
            }
            double[][] positions = new double[integration.Y.Length][];
            double[][] tangents = new double[integration.Y.Length][];
            for (int index = 0; index < integration.Y.Length; index++)
            {
                positions[index] = new double[Dimension];
                tangents[index] = new double[Dimension];
                Array.Copy(integration.Y[index], 0, positions[index], 0, Dimension);
                Array.Copy(integration.Y[index], Dimension, tangents[index], 0, Dimension);
            }
            return new GeodesicSolution(integration.X, positions, tangents, true);
        }

        public static double SchwarzschildGravitationalRedshift(double massLength, double emitterRadius, double observerRadius)
        {
            EnsurePositive(massLength, nameof(massLength));
            EnsureGreaterThanHorizon(emitterRadius, massLength, nameof(emitterRadius));
            EnsureGreaterThanHorizon(observerRadius, massLength, nameof(observerRadius));
            double emitterFactor = 1.0 - 2.0 * massLength / emitterRadius;
            double observerFactor = 1.0 - 2.0 * massLength / observerRadius;
            return Math.Sqrt(observerFactor / emitterFactor) - 1.0;
        }

        public static double SchwarzschildKretschmannScalar(double massLength, double radius)
        {
            EnsurePositive(massLength, nameof(massLength));
            EnsureGreaterThanHorizon(radius, massLength, nameof(radius));
            return 48.0 * massLength * massLength / Math.Pow(radius, 6.0);
        }

        private static double[,] ValidateMetric(double[,] metric)
        {
            double[,] validated = ValidateMetricShape(metric);
            _ = Algebra.MatrixInverse(validated);
            return validated;
        }

        private static double[,] ValidateMetricShape(double[,] metric)
        {
            if (metric == null || metric.GetLength(0) != Dimension || metric.GetLength(1) != Dimension)
            {
                throw new ArgumentException("Spacetime metric must be a 4x4 matrix.", nameof(metric));
            }
            for (int row = 0; row < Dimension; row++)
            {
                for (int column = 0; column < Dimension; column++)
                {
                    EnsureFinite(metric[row, column], nameof(metric));
                    if (Math.Abs(metric[row, column] - metric[column, row]) > 1e-10 * (1.0 + Math.Abs(metric[row, column])))
                    {
                        throw new ArgumentException("Metric tensor must be symmetric.", nameof(metric));
                    }
                }
            }
            return metric;
        }

        private static void ValidateCoordinates(double[] coordinates)
        {
            if (coordinates == null)
            {
                throw new ArgumentNullException(nameof(coordinates));
            }
            if (coordinates.Length != Dimension)
            {
                throw new ArgumentException("Four coordinates or tangent components are required.", nameof(coordinates));
            }
            foreach (double coordinate in coordinates)
            {
                EnsureFinite(coordinate, nameof(coordinates));
            }
        }

        private static void EnsureGreaterThanHorizon(double radius, double massLength, string name)
        {
            EnsurePositive(radius, name);
            if (radius <= 2.0 * massLength)
            {
                throw new ArgumentOutOfRangeException(name, "Radius must be outside the Schwarzschild horizon.");
            }
        }

        private static double EnsureFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException($"Non-finite spacetime result for {name}.");
            }
            return value;
        }

        private static void EnsurePositive(double value, string name)
        {
            if (!double.IsFinite(value) || value <= 0.0)
            {
                throw new ArgumentOutOfRangeException(name, "Value must be positive and finite.");
            }
        }
    }
}
