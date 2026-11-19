using System;
using OPEN_LRD.Skills;
using static OPEN_LRD.SmokeTests.Program;

namespace OPEN_LRD.SmokeTests
{
    internal static class OptimizationTests
    {
        public static void RunAll()
        {
            Run("One-dimensional minimizers", TestOneDimensionalMinimizers);
            Run("Unconstrained optimizers", TestUnconstrainedOptimizers);
            Run("Constrained optimizers", TestConstrainedOptimizers);
            Run("Linear programming", TestLinearProgramming);
            Run("Hungarian assignment", TestAssignment);
            Run("Optimization validation", TestValidation);
        }

        private static void TestOneDimensionalMinimizers()
        {
            double golden = Optimization.GoldenSectionSearch(value => (value - 3.0) * (value - 3.0), 0.0, 5.0);
            AssertEqual(golden, 3.0, "golden-section minimum", 1e-6);
            double brent = Optimization.BrentMinimize(value => (value + 2.0) * (value + 2.0), -5.0, 4.0);
            AssertEqual(brent, -2.0, "Brent minimum", 1e-6);
        }

        private static void TestUnconstrainedOptimizers()
        {
            double Objective(double[] point) => point[0] * point[0] + 2.0 * point[1] * point[1];
            double[] Gradient(double[] point) => new[] { 2.0 * point[0], 4.0 * point[1] };
            double[] initial = { 3.0, -2.0 };

            OptimizationResult descent = Optimization.GradientDescent(Objective, Gradient, initial, 0.2, 1e-8, 10000);
            Assert(descent.Converged, "gradient descent should converge");
            AssertEqual(descent.Value, 0.0, "gradient descent minimum", 1e-8);

            OptimizationResult bfgs = Optimization.Bfgs(Objective, Gradient, initial, 1e-8, 1000);
            Assert(bfgs.Converged, "BFGS should converge");
            AssertEqual(bfgs.Value, 0.0, "BFGS minimum", 1e-8);

            OptimizationResult conjugate = Optimization.ConjugateGradient(Objective, Gradient, initial, 1e-8, 1000);
            Assert(conjugate.Converged, "nonlinear conjugate gradient should converge");
            AssertEqual(conjugate.Value, 0.0, "conjugate-gradient minimum", 1e-8);

            OptimizationResult simplex = Optimization.NelderMead(Objective, initial, 1e-7, 2000);
            Assert(simplex.Converged, "Nelder-Mead should converge");
            AssertEqual(simplex.Value, 0.0, "Nelder-Mead minimum", 1e-7);

            OptimizationResult annealingA = Optimization.SimulatedAnnealing(
                point => (point[0] - 1.0) * (point[0] - 1.0),
                new[] { 0.0 },
                1.0,
                0.99,
                1000,
                new Random(99));
            OptimizationResult annealingB = Optimization.SimulatedAnnealing(
                point => (point[0] - 1.0) * (point[0] - 1.0),
                new[] { 0.0 },
                1.0,
                0.99,
                1000,
                new Random(99));
            AssertEqual(annealingA.Point[0], annealingB.Point[0], "seeded annealing reproducibility");
            Assert(annealingA.Value < 0.1, "annealing should approach the minimum");
        }

        private static void TestConstrainedOptimizers()
        {
            OptimizationResult projected = Optimization.ProjectedGradient(
                point => (point[0] - 2.0) * (point[0] - 2.0),
                point => new[] { 2.0 * (point[0] - 2.0) },
                new[] { 0.0 },
                new[] { -1.0 },
                new[] { 1.0 },
                0.5,
                1e-8,
                1000);
            Assert(projected.Converged, "projected gradient should converge");
            AssertEqual(projected.Point[0], 1.0, "projected gradient respects upper bound");

            OptimizationResult penalty = Optimization.PenaltyMethod(
                point => (point[0] - 2.0) * (point[0] - 2.0),
                point => new[] { point[0] - 1.0 },
                new[] { 0.0 },
                1000.0,
                1e-6,
                1000);
            Assert(penalty.Converged, "penalty method should converge");
            Assert(Math.Abs(penalty.Point[0] - 1.0) < 0.01, "penalty method should satisfy equality constraint");
        }

        private static void TestLinearProgramming()
        {
            double[,] matrix = { { 1.0, 1.0 }, { 1.0, 0.0 }, { 0.0, 1.0 } };
            LinearProgrammingResult simplex = Optimization.Simplex(matrix, new[] { 4.0, 2.0, 3.0 }, new[] { 3.0, 2.0 });
            Assert(simplex.Feasible, "simplex solution should be feasible");
            AssertEqual(simplex.ObjectiveValue, 10.0, "simplex objective");
            AssertEqual(simplex.Variables[0], 2.0, "simplex x");
            AssertEqual(simplex.Variables[1], 2.0, "simplex y");

            LinearProgrammingResult twoPhase = Optimization.TwoPhaseSimplex(
                new[,] { { -1.0 }, { 1.0 } },
                new[] { -1.0, 3.0 },
                new[] { 1.0 });
            Assert(twoPhase.Feasible, "two-phase simplex should find feasible point");
            AssertEqual(twoPhase.ObjectiveValue, 3.0, "two-phase simplex objective");
            AssertEqual(twoPhase.Variables[0], 3.0, "two-phase simplex variable");

            LinearProgrammingResult infeasible = Optimization.TwoPhaseSimplex(
                new[,] { { 1.0 }, { -1.0 } },
                new[] { 0.0, -1.0 },
                new[] { 1.0 });
            Assert(!infeasible.Feasible, "two-phase simplex should reject infeasible constraints");
        }

        private static void TestAssignment()
        {
            AssignmentResult square = Optimization.HungarianAssignment(new[,] { { 4.0, 1.0, 3.0 }, { 2.0, 0.0, 5.0 }, { 3.0, 2.0, 2.0 } });
            AssertEqual(square.TotalCost, 5.0, "square assignment cost");
            Assert(square.Assignment[0] == 1 && square.Assignment[1] == 0 && square.Assignment[2] == 2, "square assignment mapping");

            AssignmentResult rectangular = Optimization.HungarianAssignment(new[,] { { 1.0, 5.0 }, { 2.0, 3.0 }, { 4.0, 1.0 } });
            AssertEqual(rectangular.TotalCost, 2.0, "rectangular assignment cost");
            Assert(rectangular.Assignment[0] == 0 && rectangular.Assignment[2] == 1, "rectangular assignment mapping");
            Assert(rectangular.Assignment[1] == -1, "unassigned row should be marked -1");
        }

        private static void TestValidation()
        {
            AssertThrows<ArgumentOutOfRangeException>(
                () => Optimization.GoldenSectionSearch(Math.Sin, 2.0, 1.0),
                "minimization bounds must be ordered");
            AssertThrows<ArgumentException>(
                () => Optimization.ProjectedGradient(
                    point => point[0] * point[0],
                    point => new[] { 2.0 * point[0] },
                    new[] { 0.0 },
                    new[] { 2.0 },
                    new[] { 1.0 },
                    0.1),
                "lower bound must not exceed upper bound");
            AssertThrows<ArgumentOutOfRangeException>(
                () => Optimization.SimulatedAnnealing(point => point[0] * point[0], new[] { 0.0 }, 1.0, 1.0, 10, new Random(1)),
                "annealing cooling rate");
            AssertThrows<ArgumentOutOfRangeException>(
                () => Optimization.Simplex(new[,] { { 1.0 } }, new[] { -1.0 }, new[] { 1.0 }),
                "simplex standard-form right-hand side");
            AssertThrows<ArgumentException>(
                () => Optimization.HungarianAssignment(new double[0, 2]),
                "empty assignment matrix");
        }
    }
}
