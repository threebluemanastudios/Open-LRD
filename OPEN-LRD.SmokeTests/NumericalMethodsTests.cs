using System;
using OPEN_LRD.Foundations;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class NumericalMethodsTests
    {
        public static void RunAll()
        {
            Run("Interpolation and splines", TestInterpolation);
            Run("Curve fitting", TestCurveFits);
            Run("Scalar and system roots", TestRoots);
            Run("Numerical quadrature", TestQuadrature);
            Run("Iterative linear solvers", TestIterativeSolvers);
            Run("Adaptive Runge-Kutta", TestAdaptiveOde);
            Run("Extrapolation and acceleration", TestExtrapolation);
            Run("Numerical methods validation", TestValidation);
        }

        private static void TestInterpolation()
        {
            double[] x = { -1.0, 0.0, 1.0, 2.0 };
            double[] y = { 1.0, 0.0, 1.0, 4.0 };
            AssertEqual(NumericalMethods.LagrangeInterpolate(x, y, 1.5), 2.25, "Lagrange polynomial interpolation");
            AssertEqual(NumericalMethods.NewtonDividedDifference(x, y, 1.5), 2.25, "Newton divided differences");
            Func<double, double> newton = NumericalMethods.BuildNewtonInterpolator(x, y);
            AssertEqual(newton(1.5), 2.25, "built Newton interpolator");

            double[] knotX = { 0.0, 1.0, 2.0, 3.0 };
            double[] knotY = { 0.0, 1.0, 8.0, 27.0 };
            CubicSpline natural = NumericalMethods.BuildCubicSpline(knotX, knotY, SplineBoundary.Natural);
            foreach (int index in new[] { 0, 1, 2, 3 })
            {
                AssertEqual(natural.Evaluate(knotX[index]), knotY[index], "spline knot interpolation");
            }
            AssertEqual(natural.SecondDerivative(0.0), 0.0, "natural spline left boundary");
            AssertEqual(natural.SecondDerivative(3.0), 0.0, "natural spline right boundary");

            CubicSpline clamped = NumericalMethods.BuildCubicSpline(knotX, knotY, SplineBoundary.Clamped, 0.0, 27.0);
            AssertEqual(clamped.Derivative(0.0), 0.0, "clamped spline left derivative");
            AssertEqual(clamped.Derivative(3.0), 27.0, "clamped spline right derivative");
            AssertEqual(NumericalMethods.PchipInterpolate(new[] { 0.0, 1.0, 2.0, 3.0 }, new[] { 0.0, 1.0, 1.5, 2.0 }, 1.5), 1.2708333333333333, "PCHIP interpolation");
        }

        private static void TestCurveFits()
        {
            double[] x = { 0.0, 1.0, 2.0, 3.0 };
            double[] polynomialY = { 1.0, 6.0, 17.0, 34.0 };
            CurveFitResult polynomial = NumericalMethods.PolynomialLeastSquares(x, polynomialY, 2);
            AssertEqual(polynomial.Coefficients[0], 1.0, "polynomial fit constant");
            AssertEqual(polynomial.Coefficients[1], 2.0, "polynomial fit linear coefficient");
            AssertEqual(polynomial.Coefficients[2], 3.0, "polynomial fit quadratic coefficient");
            AssertEqual(polynomial.RSquared, 1.0, "polynomial fit R squared");
            CurveFitResult unsortedPolynomial = NumericalMethods.PolynomialLeastSquares(
                new[] { 2.0, 0.0, 3.0, 1.0 },
                new[] { 17.0, 1.0, 34.0, 6.0 },
                2);
            AssertEqual(unsortedPolynomial.Coefficients[2], 3.0, "polynomial fit accepts unsorted observations");

            double[] exponentialY = { 2.0, 2.0 * Math.Exp(0.5), 2.0 * Math.Exp(1.0), 2.0 * Math.Exp(1.5) };
            CurveFitResult exponential = NumericalMethods.ExponentialFit(x, exponentialY);
            AssertEqual(exponential.Coefficients[0], 2.0, "exponential fit scale");
            AssertEqual(exponential.Coefficients[1], 0.5, "exponential fit exponent");

            double[] positiveX = { 1.0, 2.0, 3.0, 4.0 };
            double[] powerY = { 3.0, 12.0, 27.0, 48.0 };
            CurveFitResult power = NumericalMethods.PowerFit(positiveX, powerY);
            AssertEqual(power.Coefficients[0], 3.0, "power fit scale");
            AssertEqual(power.Coefficients[1], 2.0, "power fit exponent");

            double[] logY = { 1.0, 1.0 + 2.0 * Math.Log(2.0), 1.0 + 2.0 * Math.Log(3.0), 1.0 + 2.0 * Math.Log(4.0) };
            CurveFitResult logarithmic = NumericalMethods.LogarithmicFit(positiveX, logY);
            AssertEqual(logarithmic.Coefficients[0], 1.0, "logarithmic fit constant");
            AssertEqual(logarithmic.Coefficients[1], 2.0, "logarithmic fit coefficient");
        }

        private static void TestRoots()
        {
            RootFindingResult secant = NumericalMethods.Secant(value => value * value - 2.0, 1.0, 2.0);
            Assert(secant.Converged, "secant should converge");
            AssertEqual(secant.Root, Math.Sqrt(2.0), "secant root", 1e-8);

            RootFindingResult falsePosition = NumericalMethods.FalsePosition(value => value * value - 2.0, 1.0, 2.0);
            Assert(falsePosition.Converged, "false position should converge");
            AssertEqual(falsePosition.Root, Math.Sqrt(2.0), "false-position root", 1e-8);

            RootFindingResult fixedPoint = NumericalMethods.FixedPoint(Math.Cos, 0.5);
            Assert(fixedPoint.Converged, "fixed point iteration should converge");
            AssertEqual(fixedPoint.Root, 0.7390851332151607, "cosine fixed point", 1e-8);

            RootFindingResult brent = NumericalMethods.Brent(value => value * value * value - value - 2.0, 1.0, 2.0);
            Assert(brent.Converged, "Brent method should converge");
            AssertEqual(brent.Root, 1.5213797068045676, "Brent root", 1e-8);

            SystemRootFindingResult system = NumericalMethods.NewtonSystem(
                point => new[] { point[0] * point[0] + point[1] * point[1] - 4.0, point[0] - point[1] },
                point => new[,] { { 2.0 * point[0], 2.0 * point[1] }, { 1.0, -1.0 } },
                new[] { 1.5, 1.0 });
            Assert(system.Converged, "Newton system should converge");
            AssertEqual(system.Root[0], Math.Sqrt(2.0), "system root x", 1e-8);
            AssertEqual(system.Root[1], Math.Sqrt(2.0), "system root y", 1e-8);
        }

        private static void TestQuadrature()
        {
            IntegrationResult romberg = NumericalMethods.Romberg(value => value * value, 0.0, 1.0);
            AssertEqual(romberg.Value, 1.0 / 3.0, "Romberg integral", 1e-9);
            Assert(romberg.FunctionEvaluations > 0, "Romberg should report evaluations");

            IntegrationResult adaptive = NumericalMethods.AdaptiveSimpson(Math.Exp, 0.0, 1.0, 1e-10);
            AssertEqual(adaptive.Value, Math.E - 1.0, "adaptive Simpson integral", 1e-9);
            Assert(adaptive.EstimatedError >= 0.0, "adaptive Simpson error estimate");

            IntegrationResult gauss = NumericalMethods.GaussLegendre(value => Math.Pow(value, 4.0), -1.0, 1.0, 4);
            AssertEqual(gauss.Value, 0.4, "Gauss-Legendre polynomial integral", 1e-12);
            Assert(gauss.FunctionEvaluations == 4, "Gauss-Legendre evaluation count");

            IntegrationResult monteCarloA = NumericalMethods.MonteCarloIntegral(value => value, 0.0, 1.0, 20000, new Random(7));
            IntegrationResult monteCarloB = NumericalMethods.MonteCarloIntegral(value => value, 0.0, 1.0, 20000, new Random(7));
            AssertEqual(monteCarloA.Value, monteCarloB.Value, "seeded Monte Carlo reproducibility");
            Assert(Math.Abs(monteCarloA.Value - 0.5) < 0.02, "Monte Carlo estimate should be close to exact integral");
        }

        private static void TestIterativeSolvers()
        {
            double[,] matrix = { { 4.0, 1.0 }, { 1.0, 3.0 } };
            double[] rightHandSide = { 1.0, 2.0 };
            double[] initial = { 0.0, 0.0 };
            foreach (IterativeSolverResult result in new[]
            {
                NumericalMethods.Jacobi(matrix, rightHandSide, initial),
                NumericalMethods.GaussSeidel(matrix, rightHandSide, initial),
                NumericalMethods.ConjugateGradient(matrix, rightHandSide, initial)
            })
            {
                Assert(result.Converged, "iterative solver should converge");
                AssertEqual(result.Solution[0], 1.0 / 11.0, "linear system x", 1e-8);
                AssertEqual(result.Solution[1], 7.0 / 11.0, "linear system y", 1e-8);
                Assert(result.ResidualNorm < 1e-8, "iterative solver residual");
            }
        }

        private static void TestAdaptiveOde()
        {
            OdeSolution scalar = NumericalMethods.RungeKutta45((_, value) => value, 0.0, 1.0, 1.0, 1e-8);
            AssertEqual(scalar.X[^1], 1.0, "adaptive scalar ODE endpoint");
            AssertEqual(scalar.Y[^1], Math.E, "adaptive scalar ODE solution", 1e-7);

            OdeSolution alias = NumericalMethods.DormandPrince((_, value) => value, 0.0, 1.0, 1.0, 1e-8);
            AssertEqual(alias.Y[^1], scalar.Y[^1], "Dormand-Prince delegates to embedded solver");

            OdeSystemSolution system = NumericalMethods.RungeKutta45System(
                (_, values) => new[] { values[1], -values[0] },
                0.0,
                Math.PI / 2.0,
                new[] { 1.0, 0.0 },
                1e-8);
            AssertEqual(system.X[^1], Math.PI / 2.0, "adaptive system endpoint");
            AssertEqual(system.Y[^1][0], 0.0, "harmonic oscillator position", 1e-7);
            AssertEqual(system.Y[^1][1], -1.0, "harmonic oscillator velocity", 1e-7);
        }

        private static void TestExtrapolation()
        {
            AssertEqual(NumericalMethods.RichardsonExtrapolation(step => 1.0 + step * step, 0.5, 3), 1.0, "Richardson extrapolation", 1e-12);
            AssertEqual(NumericalMethods.AitkenDeltaSquared(1.0, 1.5, 1.75), 2.0, "Aitken delta-squared");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentException>(
                () => NumericalMethods.LagrangeInterpolate(new[] { 0.0, 0.0 }, new[] { 1.0, 2.0 }, 0.5),
                "duplicate interpolation nodes");
            AssertThrows<ArgumentException>(
                () => NumericalMethods.BuildCubicSpline(new[] { 0.0, 1.0 }, new[] { 0.0, 1.0 }, SplineBoundary.Clamped),
                "clamped spline endpoint derivatives");
            AssertThrows<ArgumentException>(
                () => NumericalMethods.Brent(value => value * value + 1.0, -1.0, 1.0),
                "root bracket must change sign");
            AssertThrows<ArgumentOutOfRangeException>(
                () => NumericalMethods.GaussLegendre(Math.Sin, 0.0, 1.0, 11),
                "unsupported Gauss-Legendre order");
            AssertThrows<ArgumentException>(
                () => NumericalMethods.ConjugateGradient(new[,] { { 1.0, 2.0 }, { 0.0, 1.0 } }, new[] { 1.0, 1.0 }, new[] { 0.0, 0.0 }),
                "CG requires a symmetric matrix");
            AssertThrows<ArgumentOutOfRangeException>(
                () => NumericalMethods.RungeKutta45((_, value) => value, 1.0, 0.0, 1.0),
                "ODE interval must increase");
        }
    }
}
