using System;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class CurvedSpacetimeTests
    {
        public static void RunAll()
        {
            Run("Curved spacetime flat metric and geodesic", TestFlatSpace);
            Run("Curved spacetime Schwarzschild quantities", TestSchwarzschild);
            Run("Curved spacetime domain validation", TestValidation);
        }

        private static void TestFlatSpace()
        {
            double[] point = { 0.0, 1.0, Math.PI / 2.0, 0.0 };
            double interval = CurvedSpacetime.MetricInterval(CurvedSpacetime.MinkowskiMetric, point, new[] { 2.0, 1.0, 0.0, 0.0 });
            AssertEqual(interval, -3.0, "Minkowski interval");
            double[,,] gamma = CurvedSpacetime.ChristoffelSymbols(CurvedSpacetime.MinkowskiMetric, point, 1e-4);
            AssertEqual(gamma[0, 0, 0], 0.0, "flat-space connection");
            GeodesicSolution solution = CurvedSpacetime.IntegrateGeodesic(
                CurvedSpacetime.MinkowskiMetric,
                point,
                new[] { 1.0, 0.25, 0.0, 0.0 },
                0.0,
                2.0,
                1e-8,
                100);
            Assert(solution.Completed, "flat geodesic completes");
            AssertEqual(solution.Coordinates[^1][0], 2.0, "flat time coordinate");
            AssertEqual(solution.Coordinates[^1][1], 1.5, "flat radial coordinate");
            AssertEqual(solution.Tangents[^1][0], 1.0, "flat tangent conserved");
        }

        private static void TestSchwarzschild()
        {
            double mass = 1.98847e30;
            double radius = CurvedSpacetime.SchwarzschildRadius(mass);
            Assert(radius > 2900.0 && radius < 3000.0, "solar Schwarzschild radius");
            AssertEqual(CurvedSpacetime.SchwarzschildMassLength(mass), radius / 2.0, "geometrized mass length");
            double[,] metric = CurvedSpacetime.SchwarzschildMetric(1.0, new[] { 0.0, 10.0, Math.PI / 2.0, 0.0 });
            Assert(metric[0, 0] < 0.0 && metric[1, 1] > 0.0 && metric[2, 2] > 0.0 && metric[3, 3] > 0.0, "Schwarzschild signature");
            AssertEqual(CurvedSpacetime.SchwarzschildGravitationalRedshift(1.0, 4.0, 10.0), Math.Sqrt(0.8 / 0.5) - 1.0, "gravitational redshift");
            AssertEqual(CurvedSpacetime.SchwarzschildKretschmannScalar(1.0, 4.0), 48.0 / Math.Pow(4.0, 6.0), "Kretschmann scalar");
            double factor = 0.8;
            double[] coordinates = { 0.0, 10.0, Math.PI / 2.0, 0.0 };
            double[] tangent = { 1.0 / factor, -Math.Sqrt(0.2), 0.0, 0.0 };
            GeodesicSolution radial = CurvedSpacetime.IntegrateGeodesic(
                values => CurvedSpacetime.SchwarzschildMetric(1.0, values),
                coordinates,
                tangent,
                0.0,
                0.01,
                1e-8,
                100);
            double finalNorm = CurvedSpacetime.MetricInterval(
                values => CurvedSpacetime.SchwarzschildMetric(1.0, values),
                radial.Coordinates[^1],
                radial.Tangents[^1]);
            AssertEqual(finalNorm, -1.0, "geodesic normalization conservation", 1e-5);
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => CurvedSpacetime.SchwarzschildMetric(1.0, new[] { 0.0, 2.0, Math.PI / 2.0, 0.0 }), "horizon is excluded");
            AssertThrows<ArgumentOutOfRangeException>(() => CurvedSpacetime.SchwarzschildMetric(1.0, new[] { 0.0, 10.0, 0.0, 0.0 }), "polar singularity is excluded");
            AssertThrows<ArgumentException>(() => CurvedSpacetime.MetricInterval(CurvedSpacetime.MinkowskiMetric, new[] { 0.0 }, new[] { 0.0 }), "metric coordinates require four dimensions");
        }
    }
}
