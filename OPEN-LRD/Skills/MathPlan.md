# Math Helper Module Plan

## 1. Purpose

This document defines the implementation plan for the complete set of math helper
classes under `OPEN-LRD/Skills/`.

The goals are:

- Keep each topic in a separate file and class.
- Reuse the existing math modules instead of duplicating behavior.
- Use only the .NET base class library.
- Keep the modules UI-independent and usable from reasoning, tools, and tests.
- Maintain `0 warnings / 0 errors` with the project's nullable and
  warnings-as-errors settings.
- Add permanent smoke coverage for every new module.

This document covers completed modules, planned modules, implementation order,
API conventions, test requirements, and acceptance criteria.

---

## 2. Current Module Inventory

| Module | File | Status | Notes |
|---|---|---|---|
| Basic math | `Math_1.cs` | Complete | Arithmetic, reductions, descriptive statistics, fractions, basic number theory, logs, basic trig, percentages |
| Trigonometry and geometry | `Math_2.cs` | Complete | Inverse/hyperbolic trig, triangle solving, 2D/3D geometry, vectors, polyhedra |
| Single-variable calculus | `Math_3.cs` | Complete | Symbolic differentiation/integration facade, limits, series, numerical integration, root finding |
| Multivariable calculus | `Math_4.cs` | Complete | Partial derivatives, vector calculus, multiple integrals, ODE solvers |
| Algebra | `Algebra.cs` | Complete | Polynomials, matrices, complex numbers, quaternions, inequalities, symbolic wrappers |
| Symbolic calculus engine | `SymbolicCalculus.cs` | Complete | Expression simplification, differentiation, integration |
| Advanced linear algebra | `LinearAlgebraAdvanced.cs` | Complete | LU, QR, Cholesky, eigen, SVD, pseudoinverse, nullspace, least squares, condition number |
| Graph algorithms | `GraphAlgorithms.cs` | Complete | Graph model, traversal, shortest paths, components, MST, flow, PageRank |
| Probability and statistics | `ProbabilityAndStatistics.cs` | Planned | Distributions, inference, tests, Bayesian updates, regression |
| Numerical methods | `NumericalMethods.cs` | Planned | Interpolation, splines, curve fitting, advanced quadrature, iterative solvers, adaptive ODE |
| Optimization | `Optimization.cs` | Planned | 1D and multi-dimensional minimization, LP, assignment |
| Discrete math | `DiscreteMath.cs` | Planned | Modular arithmetic, CRT, primes, factorization, combinatorics |
| 3D geometry and graphics | `Geometry3DAndGraphics.cs` | Planned | Convex hull, clipping, triangulation, curves, ray intersection, transforms |
| Transforms and signals | `TransformsAndSignals.cs` | Planned | DFT/FFT, convolution, correlation, windows, filters |
| Information theory and ML math | `InformationTheoryAndMLMath.cs` | Planned | Entropy, divergences, losses, metrics, distances, PCA, k-means |
| Financial math | `FinancialMath.cs` | Planned | Interest, annuities, amortization, NPV/IRR, depreciation, bonds |
| Units and measurement | `UnitsAndMeasurement.cs` | Planned | Unit conversion, dimensions, significant figures, uncertainty propagation |

`GraphAlgorithms` replaces the graph portion of the earlier
`DiscreteMathAndGraphs` idea. The remaining graph-adjacent work is limited to
optional centrality metrics; the new discrete module should not duplicate the
existing graph algorithms.

---

## 3. Cross-Cutting Design Rules

### 3.1 File and type conventions

- Files live in `OPEN-LRD/Skills/`.
- Namespace is `OPEN_LRD.Skills`.
- Main modules are `internal static class` types.
- Multi-value outputs use `internal sealed record` result types.
- Small value outputs may use tuples only when the values are self-explanatory.
- Stateful helpers, such as splines and curves, are small `internal sealed class`
  types with immutable configuration after construction.
- No top-level or public types are required.

### 3.2 Numeric conventions

- `double` is the canonical numeric type for advanced modules.
- `Math_1` and `Math_2` retain their existing `double`/`float` parity.
- New advanced modules may add `float` overloads only where the API is small and
  the conversion is straightforward.
- Default numerical tolerance is `1e-9`, matching the current math modules.
- Probability and statistical functions use log-space internally where needed to
  avoid overflow.
- `System.Numerics.Complex` is allowed through an alias:
  `using Complex = System.Numerics.Complex;`.
- Do not import `System.Numerics` directly because `Vector3` conflicts with
  `OPEN_LRD.Foundations.Vector3`.
- Use `System.Numerics.BigInteger` for exact modular arithmetic, large
  combinatorics, and factorization where `long` is insufficient.

### 3.3 Reuse rules

- Use `Math_1` for basic arithmetic, reductions, descriptive statistics,
  fractions, and simple number theory.
- Use `Math_2` for basic 2D/3D geometry and vector operations.
- Use `Math_3` and `Math_4` for existing calculus, root finding, integration, and
  ODE behavior.
- Use `Algebra` for polynomial and basic matrix operations.
- Use `LinearAlgebraAdvanced` for decompositions, eigen/SVD, least squares, and
  matrix conditioning.
- Use `GraphAlgorithms` for graph traversal and path problems.
- If a behavior already exists, call it or add a thin overload. Do not create a
  second implementation with different semantics.

### 3.4 Validation conventions

- Null arguments throw `ArgumentNullException`.
- Invalid dimensions, sizes, probabilities, degrees of freedom, and step counts
  throw `ArgumentException` or `ArgumentOutOfRangeException`.
- Numerical failure that is not a caller error throws
  `InvalidOperationException`.
- Non-finite inputs are rejected where they would produce meaningless results.
- `Try...` methods return `false` instead of throwing for expected domain
  failures.
- Randomized methods require a caller-supplied `Random` or an explicit seed.

### 3.5 Test conventions

- Each module gets `OPEN-LRD.SmokeTests/<Module>Tests.cs`.
- The smoke test project compiles the new source file directly.
- `Program.Main` calls every new `RunAll()`.
- Tests use fixed, analytically known values where possible.
- Randomized tests use fixed seeds.
- Every module includes validation tests for invalid inputs.
- Tests avoid depending on platform-specific floating-point formatting.

### 3.6 Verification commands

From the repository root:

```text
"/mnt/c/Program Files/dotnet/dotnet.exe" build OPEN-LRD.slnx --nologo
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj --no-build
```

The build must report `0 Warning(s)` and `0 Error(s)`.

---

## 4. Implementation Order

| Phase | Module | Priority | Reason |
|---|---|---|---|
| 0 | Shared test/project wiring | Required | Every module depends on it |
| 1 | `ProbabilityAndStatistics` | Highest | Largest missing area; provides special functions reused later |
| 2 | `NumericalMethods` | High | Builds on existing calculus and linear algebra |
| 3 | `Optimization` | High | Reuses numerical methods and linear algebra |
| 4 | `DiscreteMath` | High | Natural extension of `Math_1`; useful for graph-adjacent reasoning |
| 5 | `InformationTheoryAndMLMath` | Medium-high | Depends on statistics and linear algebra |
| 6 | `Geometry3DAndGraphics` | Medium | Extends `Math_2` into graphics-grade geometry |
| 7 | `TransformsAndSignals` | Medium | Independent numerical module |
| 8 | `FinancialMath` | Medium-low | Mostly formula-driven; lower dependency risk |
| 9 | `UnitsAndMeasurement` | Medium-low | Independent and broadly reusable |

Optional later work:

- Graph centrality extensions: degree, closeness, betweenness, eigenvector
  centrality, community detection.
- Symbolic transform engine for Laplace and Z transforms.
- Full unit-expression parser.
- Additional ML models beyond PCA and k-means.

---

## 5. Module: ProbabilityAndStatistics

### 5.1 File

`OPEN-LRD/Skills/ProbabilityAndStatistics.cs`

### 5.2 Purpose

Add inferential statistics and probability distributions. `Math_1` already covers
descriptive statistics, so this module should not duplicate mean, median,
variance, correlation, or regression error metrics.

### 5.3 Types

```csharp
internal enum TailType
{
    TwoSided,
    Upper,
    Lower
}

internal sealed record ConfidenceInterval(
    double Estimate,
    double Lower,
    double Upper,
    double ConfidenceLevel);

internal sealed record HypothesisTestResult(
    string Name,
    double Statistic,
    double PValue,
    double CriticalValue,
    double DegreesOfFreedom,
    bool RejectNull);

internal sealed record RegressionResult(
    double[] Coefficients,
    double RSquared,
    double AdjustedRSquared,
    double StandardError,
    double[,] Covariance);

internal sealed record AnovaResult(
    double BetweenVariance,
    double WithinVariance,
    double FStatistic,
    double PValue,
    bool RejectNull);
```

### 5.4 API surface

Special functions:

- `Erf(double x)`
- `Erfc(double x)`
- `LogGamma(double x)`
- `RegularizedGammaP(double a, double x)`
- `RegularizedGammaQ(double a, double x)`
- `IncompleteBeta(double a, double b, double x)`
- `RegularizedIncompleteBeta(double a, double b, double x)`

Normal distribution:

- `NormalPdf(double x, double mean, double standardDeviation)`
- `NormalCdf(double x, double mean, double standardDeviation)`
- `NormalQuantile(double probability, double mean, double standardDeviation)`
- `NormalSample(Random random, double mean, double standardDeviation)`
- `NormalConfidenceInterval(double sampleMean, double standardDeviation, int sampleSize, double confidenceLevel)`

Discrete distributions:

- `BinomialPmf(int successes, int trials, double probability)`
- `BinomialCdf(int successes, int trials, double probability)`
- `BinomialSample(Random random, int trials, double probability)`
- `PoissonPmf(int count, double rate)`
- `PoissonCdf(int count, double rate)`
- `PoissonSample(Random random, double rate)`

Continuous distributions:

- `ExponentialPdf(double x, double rate)`
- `ExponentialCdf(double x, double rate)`
- `ExponentialQuantile(double probability, double rate)`
- `ExponentialSample(Random random, double rate)`
- `UniformPdf(double x, double lower, double upper)`
- `UniformCdf(double x, double lower, double upper)`
- `BetaPdf(double x, double alpha, double beta)`
- `BetaCdf(double x, double alpha, double beta)`
- `GammaPdf(double x, double shape, double rate)`
- `GammaCdf(double x, double shape, double rate)`
- `StudentTPdf(double x, double degreesOfFreedom)`
- `StudentTCdf(double x, double degreesOfFreedom)`
- `ChiSquarePdf(double x, double degreesOfFreedom)`
- `ChiSquareCdf(double x, double degreesOfFreedom)`
- `FPdf(double x, double numeratorDegreesOfFreedom, double denominatorDegreesOfFreedom)`
- `FCdf(double x, double numeratorDegreesOfFreedom, double denominatorDegreesOfFreedom)`

Inference:

- `MeanConfidenceInterval(double[] sample, double confidenceLevel)`
- `ProportionConfidenceInterval(int successes, int trials, double confidenceLevel)`
- `DifferenceInMeansConfidenceInterval(double[] first, double[] second, double confidenceLevel)`
- `OneSampleZTest(double sampleMean, double populationStandardDeviation, int sampleSize, double nullMean, TailType tail)`
- `OneSampleTTest(double[] sample, double nullMean, TailType tail)`
- `PairedTTest(double[] before, double[] after, TailType tail)`
- `TwoSampleTTest(double[] first, double[] second, bool equalVariances, TailType tail)`
- `ProportionZTest(int successes, int trials, double nullProportion, TailType tail)`
- `ChiSquareGoodnessOfFit(int[] observed, double[] expectedProportions)`
- `ChiSquareIndependence(int[,] counts)`
- `OneWayAnova(params double[][] groups)`

Bayesian helpers:

- `BayesTheorem(double prior, double likelihood, double marginalLikelihood)`
- `BetaBinomialPosterior(int successes, int failures, double priorAlpha, double priorBeta)`
- `NormalNormalPosterior(double priorMean, double priorVariance, double[] observations, double observationVariance)`
- `GammaPoissonPosterior(int totalCount, double exposure, double priorShape, double priorRate)`

Regression:

- `SimpleLinearRegression(double[] x, double[] y)`
- `MultipleLinearRegression(double[,] designMatrix, double[] response)`
- `LogisticRegression(double[,] designMatrix, int[] labels, double learningRate, int maxIterations)` if time permits; otherwise mark as a stretch goal.

### 5.5 Algorithms

- Normal CDF via `Erf`.
- Normal quantile via a rational approximation followed by one Newton refinement.
- Incomplete gamma via series for small `x` and continued fraction for large `x`.
- Incomplete beta via continued fraction with symmetry handling.
- Student-t, chi-square, and F CDFs expressed through the incomplete beta or
  incomplete gamma functions.
- Hypothesis tests return both statistic and p-value; no table lookup is needed.
- Regression uses `LinearAlgebraAdvanced.LeastSquares` for multiple regression.
- PCA is not part of this module; it belongs to `InformationTheoryAndMLMath`.

### 5.6 Validation

- Standard deviations, rates, scales, and degrees of freedom must be positive.
- Probabilities must be in `[0, 1]`.
- Binomial successes must be in `[0, trials]`.
- Sample sizes must be positive.
- Confidence levels must be in `(0, 1)`.
- Two-sample tests require at least two observations per group.
- Chi-square expected counts must be positive.
- ANOVA requires at least two groups and at least one observation per group.

### 5.7 Tests

- `Erf(0) == 0`, `Erf` symmetry, known values at `1` and `2`.
- Normal PDF integrates approximately to `1`.
- Normal CDF known values and quantile inverse round-trip.
- Binomial PMF sums to `1`; CDF is monotone.
- Poisson PMF sums to `1` within tolerance.
- Exponential mean and CDF known values.
- Beta and gamma PDFs match known closed forms.
- Student-t, chi-square, and F CDFs match known values.
- Confidence interval contains the generating mean for deterministic samples.
- One-sample t-test, two-sample t-test, proportion test, chi-square, and ANOVA
  match known examples.
- Bayesian posterior updates match closed-form beta-binomial and normal-normal
  results.
- Seeded sampling is reproducible with the same seed.
- Simple and multiple regression recover exact linear coefficients.

### 5.8 Acceptance

- No external statistics package.
- All distribution CDFs return values in `[0, 1]`.
- No public API depends on `Random` shared state.
- All statistical tests have deterministic, analytically checkable cases.

---

## 6. Module: NumericalMethods

### 6.1 File

`OPEN-LRD/Skills/NumericalMethods.cs`

### 6.2 Purpose

Add interpolation, curve fitting, advanced quadrature, iterative linear solvers,
and adaptive ODE methods. Existing `Math_3` and `Math_4` already contain Newton,
bisection, trapezoidal integration, and RK4, so those should be reused rather
than reimplemented.

### 6.3 Types

```csharp
internal enum SplineBoundary
{
    Natural,
    Clamped
}

internal sealed record RootFindingResult(
    double Root,
    double Residual,
    int Iterations,
    bool Converged);

internal sealed record CurveFitResult(
    double[] Coefficients,
    double RSquared,
    double RootMeanSquaredError);

internal sealed record IntegrationResult(
    double Value,
    double EstimatedError,
    int FunctionEvaluations);

internal sealed class CubicSpline
{
    public double Evaluate(double x);
    public double Derivative(double x);
    public double SecondDerivative(double x);
}
```

Reuse `OdeSolution` and `OdeSystemSolution` from `Math_4` where possible. If a
new adaptive solver returns richer diagnostics, add a separate result record
instead of changing the existing types.

### 6.4 API surface

Interpolation:

- `LagrangeInterpolate(double[] x, double[] y, double value)`
- `NewtonDividedDifference(double[] x, double[] y, double value)`
- `BuildNewtonInterpolator(double[] x, double[] y)` returning `Func<double, double>`
- `CubicSplineInterpolate(double[] x, double[] y, SplineBoundary boundary, double? firstDerivative = null, double? lastDerivative = null)`
- `PchipInterpolate(double[] x, double[] y, double value)`

Curve fitting:

- `PolynomialLeastSquares(double[] x, double[] y, int degree)`
- `ExponentialFit(double[] x, double[] y)`
- `PowerFit(double[] x, double[] y)`
- `LogarithmicFit(double[] x, double[] y)`

Root finding and fixed points:

- `Secant(Func<double, double> function, double firstGuess, double secondGuess, double tolerance = 1e-9, int maxIterations = 100)`
- `FalsePosition(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxIterations = 100)`
- `FixedPoint(Func<double, double> iteration, double initialGuess, double tolerance = 1e-9, int maxIterations = 100)`
- `Brent(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxIterations = 100)`
- `NewtonSystem(Func<double[], double[]> function, Func<double[], double[,]> jacobian, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 100)`

Quadrature:

- `Romberg(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxLevels = 20)`
- `AdaptiveSimpson(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxDepth = 20)`
- `GaussLegendre(Func<double, double> function, double lower, double upper, int order)`
- `MonteCarloIntegral(Func<double, double> function, double lower, double upper, int samples, Random random)`

Iterative linear solvers:

- `Jacobi(double[,] matrix, double[] rightHandSide, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 1000)`
- `GaussSeidel(double[,] matrix, double[] rightHandSide, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 1000)`
- `ConjugateGradient(double[,] matrix, double[] rightHandSide, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 1000)` for symmetric positive definite systems.

Adaptive ODE:

- `RungeKutta45(Func<double, double, double> derivative, double start, double end, double initialValue, double tolerance = 1e-6)`
- `DormandPrince(Func<double, double, double> derivative, double start, double end, double initialValue, double tolerance = 1e-6)`
- `RungeKutta45System(Func<double, double[], double[]> derivative, double start, double end, double[] initialValues, double tolerance = 1e-6)`

Extrapolation and acceleration:

- `RichardsonExtrapolation(Func<double, double> estimate, double step, int levels)`
- `AitkenDeltaSquared(double first, double second, double third)`

### 6.5 Algorithms

- Newton interpolation builds divided-difference coefficients once.
- Natural cubic splines solve a tridiagonal system.
- Clamped splines use the caller-supplied endpoint derivatives.
- Polynomial fitting builds a Vandermonde system and solves with
  `LinearAlgebraAdvanced.LeastSquares`.
- Brent combines inverse quadratic interpolation, secant, and bisection.
- Romberg uses repeated trapezoidal refinement.
- Gauss-Legendre uses fixed nodes and weights for orders `2` through `10`.
- CG requires a symmetric positive definite matrix and validates that condition
  implicitly through breakdown detection.
- RK45 uses embedded error estimates and adaptive step-size control.

### 6.6 Validation

- Interpolation requires at least two distinct x-values.
- Splines require strictly increasing x-values.
- Polynomial degree must be non-negative and less than the point count.
- Root brackets must have opposite signs for bracketing methods.
- Quadrature bounds must be finite and ordered.
- Iterative solvers require square matrices and matching right-hand sides.
- CG rejects non-symmetric matrices.
- ODE intervals must be increasing; tolerances must be positive.

### 6.7 Tests

- Lagrange and Newton interpolation reproduce polynomials exactly.
- Cubic spline passes through all knots.
- Natural spline has zero second derivative at the endpoints.
- Polynomial fit recovers exact coefficients for a known polynomial.
- Exponential and power fits recover known parameters.
- Secant, false position, Brent, and fixed point converge on known roots.
- Newton system solves a two-variable nonlinear system.
- Romberg and Gauss-Legendre approximate `pi` or a known integral.
- Jacobi, Gauss-Seidel, and CG solve a small SPD system.
- RK45 matches the analytic solution of `y' = y`.
- Seeded Monte Carlo integration is reproducible.
- Invalid inputs produce the expected exceptions.

### 6.8 Acceptance

- No duplicate Newton/bisection implementations; call existing `Math_3` methods
  where the behavior is identical.
- All iterative methods have a maximum-iteration bound.
- No method returns `NaN` silently.

---

## 7. Module: Optimization

### 7.1 File

`OPEN-LRD/Skills/Optimization.cs`

### 7.2 Purpose

Add minimization, constrained optimization, linear programming, and assignment
algorithms. `Math_4` currently has a 2D gradient descent helper, so this module
should provide general-purpose versions and avoid duplicating that implementation
except as a thin wrapper if needed.

### 7.3 Types

```csharp
internal sealed record OptimizationResult(
    double[] Point,
    double Value,
    double GradientNorm,
    int Iterations,
    bool Converged);

internal sealed record LinearProgrammingResult(
    double ObjectiveValue,
    double[] Variables,
    bool Feasible);

internal sealed record AssignmentResult(
    int[] Assignment,
    double TotalCost);
```

### 7.4 API surface

One-dimensional minimization:

- `GoldenSectionSearch(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxIterations = 200)`
- `BrentMinimize(Func<double, double> function, double lower, double upper, double tolerance = 1e-9, int maxIterations = 200)`

Unconstrained multi-dimensional minimization:

- `GradientDescent(Func<double[], double> function, Func<double[], double[]> gradient, double[] initialGuess, double learningRate, double tolerance = 1e-9, int maxIterations = 10000)`
- `Bfgs(Func<double[], double> function, Func<double[], double[]> gradient, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 1000)`
- `ConjugateGradient(Func<double[], double> function, Func<double[], double[]> gradient, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 1000)`
- `NelderMead(Func<double[], double> function, double[] initialGuess, double tolerance = 1e-9, int maxIterations = 1000)`
- `SimulatedAnnealing(Func<double[], double> function, double[] initialGuess, double initialTemperature, double coolingRate, int maxIterations, Random random)`

Constrained minimization:

- `ProjectedGradient(Func<double[], double> function, Func<double[], double[]> gradient, double[] initialGuess, double[] lowerBounds, double[] upperBounds, double learningRate, double tolerance = 1e-9, int maxIterations = 10000)`
- `PenaltyMethod(Func<double[], double> objective, Func<double[], double[]> equalityConstraints, double[] initialGuess, double penaltyWeight, double tolerance = 1e-9, int maxIterations = 1000)`

Linear programming and assignment:

- `Simplex(double[,] constraintMatrix, double[] rightHandSide, double[] objective)` for maximization with `Ax <= b`, `x >= 0`.
- `TwoPhaseSimplex(...)` if artificial variables are needed.
- `HungarianAssignment(double[,] costMatrix)` for square or rectangular cost matrices.

### 7.5 Algorithms

- Gradient descent uses normalized or optionally backtracking line search.
- BFGS maintains an approximate inverse Hessian.
- Conjugate gradient uses Polak-Ribiere or Fletcher-Reeves updates.
- Nelder-Mead maintains a simplex and handles reflection, expansion, contraction,
  and shrink.
- Simulated annealing uses a caller-supplied `Random`.
- Simplex uses tableau form with Bland's rule or a deterministic pivot rule.
- Hungarian assignment uses the shortest augmenting path variant.

### 7.6 Validation

- Initial points must have the correct dimension.
- Bounds must satisfy `lower <= upper`.
- Learning rates, temperatures, and tolerances must be positive.
- LP dimensions must match; right-hand sides must be non-negative for standard
  form.
- Assignment matrices must be non-empty.
- Non-finite objective values reject the candidate step.

### 7.7 Tests

- Golden-section and Brent minimize a convex quadratic exactly.
- Gradient descent, BFGS, and Nelder-Mead solve a quadratic bowl.
- BFGS and Nelder-Mead make progress on Rosenbrock from a fixed start.
- Projected gradient respects box bounds.
- Simulated annealing is reproducible with a fixed seed.
- Simplex solves a small two-variable LP.
- Hungarian assignment solves a known cost matrix.
- Invalid dimensions and bounds throw.

### 7.8 Acceptance

- All stochastic methods accept a `Random`.
- Every optimizer returns convergence status and iteration count.
- No optimizer runs without an iteration cap.

---

## 8. Module: DiscreteMath

### 8.1 File

`OPEN-LRD/Skills/DiscreteMath.cs`

### 8.2 Purpose

Extend `Math_1` with exact modular arithmetic, primality, factorization, CRT,
arithmetic functions, and advanced combinatorics.

Graph algorithms are already implemented in `GraphAlgorithms.cs` and must not be
duplicated here.

### 8.3 Types

```csharp
internal sealed record PrimeFactorization(
    long Value,
    IReadOnlyList<(long Prime, int Exponent)> Factors);

internal sealed record CongruenceSolution(
    bool Exists,
    long Solution,
    long Modulus);

internal sealed record CrtResult(
    bool Exists,
    long Solution,
    long Modulus);
```

For values beyond `long`, use `BigInteger` overloads and `BigInteger` results.

### 8.4 API surface

Modular arithmetic:

- `Mod(long value, long modulus)`
- `ModPow(long value, long exponent, long modulus)`
- `ModInverse(long value, long modulus)`
- `TryModInverse(long value, long modulus, out long inverse)`
- `ExtendedGcd(long a, long b, out long x, out long y)`
- `IsCoprime(long a, long b)`
- `SolveLinearCongruence(long a, long b, long modulus)`

Primes and factorization:

- `Sieve(int limit)`
- `SegmentedSieve(long lower, long upper)`
- `IsPrime(long value)`
- `IsProbablePrime(long value, int rounds, Random random)`
- `Factorize(long value)` returning `PrimeFactorization`
- `PollardRho(long value)`
- `EulerPhi(long value)`
- `DivisorCount(long value)`
- `DivisorSum(long value)`
- `Moebius(long value)`
- `Liouville(long value)`

Chinese remainder:

- `ChineseRemainderTheorem(long[] remainders, long[] moduli)`
- `TryChineseRemainderTheorem(long[] remainders, long[] moduli, out long solution, out long modulus)`

Combinatorics:

- `Catalan(int n)`
- `Bell(int n)`
- `StirlingFirst(int n, int k)`
- `StirlingSecond(int n, int k)`
- `Partition(int n)`
- `Derangement(int n)`
- `Binomial(int n, int k)` using `BigInteger`
- `Multinomial(int[] counts)` using `BigInteger`

Base conversion and digits:

- `ToBase(BigInteger value, int radix)`
- `FromBase(string digits, int radix)`
- `DigitSum(long value, int radix)`

### 8.5 Algorithms

- Deterministic Miller-Rabin for 64-bit inputs using the standard witness set.
- Probabilistic Miller-Rabin for larger `BigInteger` inputs.
- Pollard rho for composite factorization.
- Sieve of Eratosthenes for small ranges.
- Segmented sieve for larger ranges.
- CRT with pairwise coprime moduli; general CRT only if implemented and tested.
- Dynamic programming for Bell numbers, partitions, and Stirling numbers.
- `BigInteger` for exact binomial and multinomial values.

### 8.6 Validation

- Modulus must be positive.
- Radix must be in `[2, 36]`.
- Exponents must be non-negative.
- Factorial/derangement/partition counts must be non-negative.
- Sieve limits and segment ranges must be bounded to prevent runaway memory.
- Factorization rejects zero and negative values unless explicitly documented.

### 8.7 Tests

- `ModPow` matches direct modular exponentiation for small values.
- `ModInverse` round-trips; non-coprime inputs fail.
- Extended GCD returns Bezout coefficients.
- CRT solves a known system and rejects inconsistent systems.
- Euler phi, divisor count/sum, Mobius, and Liouville match known values.
- `IsPrime` handles small primes, Carmichael numbers, and large 64-bit primes.
- Factorization reconstructs the original value.
- Catalan, Bell, Stirling, partition, and derangement match known sequences.
- BigInteger binomial values are exact.
- Base conversion round-trips.

### 8.8 Acceptance

- Exact integer results use `long` or `BigInteger`; no silent overflow.
- Prime and factorization tests include adversarial composites.
- No graph algorithm is duplicated.

---

## 9. Module: Geometry3DAndGraphics

### 9.1 File

`OPEN-LRD/Skills/Geometry3DAndGraphics.cs`

### 9.2 Purpose

Add graphics-grade geometry beyond the basic formulas in `Math_2`: hulls,
polygon operations, triangulation, curves, ray intersections, and 3D transforms.

### 9.3 Types

```csharp
internal readonly record struct Ray(Vector3 Origin, Vector3 Direction);

internal readonly record struct Plane(Vector3 Normal, double Offset);

internal readonly record struct Triangle(Vector3 A, Vector3 B, Vector3 C);

internal readonly record struct BoundingBox(Vector3 Minimum, Vector3 Maximum);

internal sealed record RayHit(
    bool Hit,
    double Distance,
    Vector3 Point,
    Vector3 Normal);

internal sealed record BarycentricCoordinates(
    double First,
    double Second,
    double Third);
```

Use `OPEN_LRD.Foundations.Vector2` and `Vector3`.

### 9.4 API surface

2D polygon and computational geometry:

- `ConvexHull(IReadOnlyList<Vector2> points)`
- `IsPointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)`
- `SegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 intersection)`
- `ClipPolygon(IReadOnlyList<Vector2> polygon, Vector2 clipMin, Vector2 clipMax)`
- `TriangulatePolygon(IReadOnlyList<Vector2> polygon)`
- `BarycentricCoordinates(Vector2 point, Vector2 a, Vector2 b, Vector2 c)`
- `IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)`

3D geometry and intersection:

- `TriangleNormal(Vector3 a, Vector3 b, Vector3 c)`
- `TriangleArea(Vector3 a, Vector3 b, Vector3 c)`
- `BarycentricCoordinates(Vector3 point, Triangle triangle)`
- `RayPlaneIntersection(Ray ray, Plane plane)`
- `RaySphereIntersection(Ray ray, Vector3 center, double radius)`
- `RayTriangleIntersection(Ray ray, Triangle triangle)`
- `RayBoxIntersection(Ray ray, BoundingBox box)`
- `BoundingBoxFromPoints(IReadOnlyList<Vector3> points)`

Curves:

- `BezierPoint(IReadOnlyList<Vector3> controlPoints, double t)`
- `BezierDerivative(IReadOnlyList<Vector3> controlPoints, double t)`
- `CatmullRomPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, double t)`
- `CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, double t)`

Transforms:

- `TranslationMatrix(double x, double y, double z)`
- `RotationXMatrix(double radians)`
- `RotationYMatrix(double radians)`
- `RotationZMatrix(double radians)`
- `ScaleMatrix(double x, double y, double z)`
- `MultiplyMatrices(double[,] left, double[,] right)`
- `TransformPoint(double[,] matrix, Vector3 point)`
- `TransformDirection(double[,] matrix, Vector3 direction)`
- `PerspectiveProjection(double fieldOfViewRadians, double aspectRatio, double nearPlane, double farPlane)`
- `LookAt(Vector3 eye, Vector3 target, Vector3 up)`

### 9.5 Algorithms

- Convex hull uses Andrew's monotone chain and removes duplicate/collinear points
  deterministically.
- Point-in-polygon uses the ray-crossing rule and handles boundary points
  explicitly.
- Polygon clipping uses Sutherland-Hodgman against an axis-aligned rectangle.
- Triangulation uses ear clipping for simple polygons.
- Ray-triangle uses Moller-Trumbore.
- Ray-box uses slab intersection.
- Bezier and Catmull-Rom use direct polynomial evaluation.
- Transform matrices are row-major `double[4,4]` matrices with a documented
  convention.

### 9.6 Validation

- Convex hull requires at least one point.
- Polygon operations require at least three vertices.
- Triangulation rejects self-intersecting polygons where detection is feasible.
- Ray directions are normalized internally or validated as non-zero.
- Sphere radius must be positive.
- Near and far planes must be positive and ordered.
- Matrix operations require `4x4` matrices.

### 9.7 Tests

- Convex hull returns the expected vertices for a square with interior points.
- Point-in-polygon handles inside, outside, edge, and vertex cases.
- Clipping a square produces the expected clipped polygon.
- Ear clipping returns `n - 2` triangles for a simple polygon.
- Barycentric coordinates sum to one and reconstruct the point.
- Ray-plane, ray-sphere, ray-triangle, and ray-box hits/misses are correct.
- Bezier endpoints and Catmull-Rom interpolation match known values.
- Transform composition and inverse transform round-trip.
- Perspective projection maps the near plane correctly.

### 9.8 Acceptance

- No dependency on WPF or rendering types.
- All geometry uses the project's `Vector2`/`Vector3`.
- Degenerate cases throw or return an explicit miss result.

---

## 10. Module: TransformsAndSignals

### 10.1 File

`OPEN-LRD/Skills/TransformsAndSignals.cs`

### 10.2 Purpose

Add numerical Fourier analysis, convolution, correlation, windows, and basic
digital filters.

This module is numeric, not a symbolic Laplace/Z-transform engine.

### 10.3 Types

```csharp
internal enum WindowType
{
    Rectangular,
    Hann,
    Hamming,
    Blackman
}

internal sealed record FirFilter(double[] Coefficients);
```

Use `System.Numerics.Complex` through an alias.

### 10.4 API surface

Fourier transforms:

- `Dft(IReadOnlyList<double> samples)`
- `InverseDft(IReadOnlyList<Complex> spectrum)`
- `Fft(IReadOnlyList<Complex> samples)`
- `InverseFft(IReadOnlyList<Complex> spectrum)`
- `RealFft(IReadOnlyList<double> samples)`
- `FftShift(Complex[] values)`
- `NextPowerOfTwo(int value)`

Convolution and correlation:

- `Convolve(double[] signal, double[] kernel)`
- `ConvolveFft(double[] signal, double[] kernel)`
- `CrossCorrelate(double[] first, double[] second)`
- `AutoCorrelate(double[] signal)`

Windows:

- `CreateWindow(int length, WindowType type)`
- `ApplyWindow(double[] signal, WindowType type)`

Filters:

- `ApplyFirFilter(double[] signal, FirFilter filter)`
- `MovingAverageFilter(double[] signal, int windowSize)`
- `OnePoleLowPass(double[] signal, double alpha)`
- `OnePoleHighPass(double[] signal, double alpha)`
- `BiquadFilter(double[] signal, double b0, double b1, double b2, double a1, double a2)`

Periodic analysis:

- `FourierSeriesCoefficients(Func<double, double> function, int terms, int samplesPerPeriod)`
- `FourierSeriesReconstruct(IReadOnlyList<Complex> coefficients, double x, double period)`

### 10.5 Algorithms

- FFT is radix-2 and requires or pads to a power-of-two length.
- DFT is the direct reference implementation used in tests.
- Convolution can run directly or through zero-padded FFT.
- Windows use standard cosine-sum formulas.
- Filters use direct-form difference equations.
- Fourier series coefficients are estimated by uniform sampling.

### 10.6 Validation

- FFT inputs must be non-empty and within a configured maximum size.
- `NextPowerOfTwo` rejects non-positive inputs.
- Convolution kernels must be non-empty.
- Window length must be positive.
- Filter alpha values must be in `(0, 1]` where required.
- Biquad coefficients must produce a stable filter when stability checking is
  implemented.

### 10.7 Tests

- DFT of a delta signal is all ones.
- FFT and DFT produce the same result for a small power-of-two input.
- Inverse FFT round-trips within tolerance.
- Parseval's theorem holds for FFT.
- A pure sinusoid produces the expected frequency peak.
- Direct and FFT convolution agree.
- Windows have the expected endpoints and symmetry.
- FIR and one-pole filters match hand-computed outputs.
- Fourier series reconstruction approximates a square wave with bounded error.

### 10.8 Acceptance

- No external FFT package.
- FFT size limits prevent accidental multi-gigabyte allocations.
- Complex-valued and real-valued convenience APIs are clearly separated.

---

## 11. Module: InformationTheoryAndMLMath

### 11.1 File

`OPEN-LRD/Skills/InformationTheoryAndMLMath.cs`

### 11.2 Purpose

Add information-theoretic quantities and the math needed for basic machine
learning: activations, losses, classification metrics, distances, PCA, and
k-means.

### 11.3 Types

```csharp
internal enum DistanceMetric
{
    Euclidean,
    Manhattan,
    Chebyshev,
    Minkowski,
    Cosine,
    Jaccard
}

internal sealed record ConfusionMatrix(
    int TruePositive,
    int TrueNegative,
    int FalsePositive,
    int FalseNegative);

internal sealed record ClassificationMetrics(
    double Accuracy,
    double Precision,
    double Recall,
    double F1,
    double Specificity,
    double BalancedAccuracy);

internal sealed record PcaResult(
    double[] Mean,
    double[,] Components,
    double[] ExplainedVariance,
    double[] ExplainedVarianceRatio,
    double[,] Transformed);

internal sealed record KMeansResult(
    double[,] Centroids,
    int[] Assignments,
    double Inertia,
    int Iterations);
```

### 11.4 API surface

Information theory:

- `Entropy(double[] probabilities)`
- `JointEntropy(double[,] jointProbabilities)`
- `ConditionalEntropy(double[,] jointProbabilities)`
- `MutualInformation(double[,] jointProbabilities)`
- `KlDivergence(double[] first, double[] second)`
- `JensenShannonDivergence(double[] first, double[] second)`
- `CrossEntropy(double[] actual, double[] predicted)`

Activations:

- `Sigmoid(double value)`
- `Sigmoid(double[] values)`
- `Softmax(double[] values)`
- `Relu(double value)`
- `LeakyRelu(double value, double alpha)`
- `Tanh(double value)`
- `Gelu(double value)`
- `Softplus(double value)`

Losses:

- `MeanSquaredError(double[] actual, double[] predicted)`
- `MeanAbsoluteError(double[] actual, double[] predicted)`
- `BinaryCrossEntropy(double actual, double predicted)`
- `CategoricalCrossEntropy(double[] actual, double[] predicted)`
- `HingeLoss(double margin, double actual, double predicted)`

Classification metrics:

- `BuildConfusionMatrix(int[] actual, int[] predicted, int positiveLabel)`
- `CalculateClassificationMetrics(int[] actual, int[] predicted, int positiveLabel)`
- `RocAuc(double[] scores, int[] labels)` if time permits.

Distances:

- `Distance(double[] first, double[] second, DistanceMetric metric, double p = 2.0)`
- `CosineSimilarity(double[] first, double[] second)`
- `JaccardSimilarity<T>(IReadOnlySet<T> first, IReadOnlySet<T> second)`

Dimensionality reduction and clustering:

- `Standardize(double[,] data)` returning standardized data and mean/scale.
- `Pca(double[,] data, int components)`
- `KMeans(double[,] data, int clusters, int maxIterations, int seed)`

### 11.5 Algorithms

- Entropy and divergences use log base 2 unless documented otherwise.
- KL divergence skips zero-probability terms where the corresponding predicted
  probability is also zero; otherwise it throws or clamps with explicit policy.
- Softmax subtracts the maximum for numerical stability.
- PCA centers the data, computes the covariance matrix, uses
  `LinearAlgebraAdvanced.SymmetricEigenDecomposition`, and projects onto the
  leading components.
- k-means uses k-means++ initialization and a caller-supplied seed.
- Distance functions use numerically stable accumulation.

### 11.6 Validation

- Probability vectors must be non-empty, finite, non-negative, and sum to one
  within tolerance.
- Actual and predicted arrays must have equal length.
- Percentages and probability inputs must be in `[0, 1]`.
- PCA requires at least two rows and at least one component.
- k-means requires at least one cluster and no more clusters than samples.
- Distance vectors must have equal dimensions.
- Jaccard similarity handles two empty sets explicitly.

### 11.7 Tests

- Entropy of a uniform distribution equals `log2(n)`.
- KL divergence is zero for identical distributions and positive otherwise.
- Softmax sums to one and is stable for large values.
- ReLU, leaky ReLU, sigmoid, and GELU match known values.
- Loss functions match hand-computed examples.
- Confusion matrix and classification metrics match a known example.
- Distance metrics satisfy expected identities.
- PCA recovers a known one-dimensional correlated data set.
- k-means separates two well-separated clusters with a fixed seed.
- Invalid probability vectors and mismatched arrays throw.

### 11.8 Acceptance

- PCA reuses the existing eigen decomposition.
- No ML training framework or external package is introduced.
- Random behavior is fully seed-controlled.

---

## 12. Module: FinancialMath

### 12.1 File

`OPEN-LRD/Skills/FinancialMath.cs`

### 12.2 Purpose

Add common time-value-of-money, amortization, investment, depreciation, and bond
calculations.

This module is optional for LRD, but it is formula-driven and low risk.

### 12.3 Types

```csharp
internal enum CompoundingFrequency
{
    Annual,
    Semiannual,
    Quarterly,
    Monthly,
    Daily,
    Continuous
}

internal enum DepreciationMethod
{
    StraightLine,
    DecliningBalance,
    SumOfYearsDigits
}

internal sealed record AmortizationEntry(
    int Period,
    double Payment,
    double Interest,
    double Principal,
    double Balance);

internal sealed record CashFlow(double Time, double Amount);

internal sealed record InvestmentResult(
    double NetPresentValue,
    double InternalRateOfReturn,
    bool Converged);

internal sealed record BondResult(
    double Price,
    double Yield,
    double MacaulayDuration,
    double ModifiedDuration,
    double Convexity);
```

### 12.4 API surface

Interest and time value:

- `CompoundInterest(double principal, double annualRate, double years, CompoundingFrequency frequency)`
- `ContinuousCompoundInterest(double principal, double annualRate, double years)`
- `EffectiveAnnualRate(double nominalRate, CompoundingFrequency frequency)`
- `PresentValue(double futureValue, double rate, double periods)`
- `FutureValue(double presentValue, double rate, double periods)`

Annuities and loans:

- `AnnuityPresentValue(double payment, double rate, int periods)`
- `AnnuityFutureValue(double payment, double rate, int periods)`
- `LoanPayment(double principal, double rate, int periods)`
- `AmortizationSchedule(double principal, double rate, int periods)`

Investment analysis:

- `NetPresentValue(IReadOnlyList<CashFlow> cashFlows, double discountRate)`
- `InternalRateOfReturn(IReadOnlyList<CashFlow> cashFlows, double tolerance = 1e-9, int maxIterations = 200)`
- `PaybackPeriod(IReadOnlyList<CashFlow> cashFlows)`
- `ProfitabilityIndex(IReadOnlyList<CashFlow> cashFlows, double discountRate)`

Depreciation:

- `DepreciationSchedule(double cost, double salvageValue, int life, DepreciationMethod method)`
- `StraightLineDepreciation(...)`
- `DecliningBalanceDepreciation(...)`
- `SumOfYearsDigitsDepreciation(...)`

Bonds:

- `BondPrice(double faceValue, double couponRate, double yield, int years, int paymentsPerYear)`
- `BondYield(double faceValue, double couponRate, double price, int years, int paymentsPerYear)`
- `MacaulayDuration(...)`
- `ModifiedDuration(...)`
- `Convexity(...)`

### 12.5 Algorithms

- IRR uses bracketed bisection or Brent search with explicit convergence status.
- Bond yield reuses the IRR-style root search.
- Amortization uses the standard fixed-payment formula and applies each payment
  to interest first, then principal.
- Depreciation schedules sum to the depreciable base within tolerance.

### 12.6 Validation

- Principal, payment, face value, and cost must be non-negative or positive as
  appropriate.
- Rates must be finite; periodic rates use explicit period conventions.
- Periods and life must be positive.
- Salvage value must not exceed cost.
- Cash flows must be time-ordered and non-empty.
- IRR must report failure when no sign change exists.

### 12.7 Tests

- Compound interest matches a known formula.
- Effective annual rate matches known values.
- Loan payment and amortization schedule match known values.
- Amortization schedule ends at a zero balance and sums correctly.
- NPV matches a hand-computed example.
- IRR finds a known rate and fails cleanly for invalid cash flows.
- Each depreciation method matches a known schedule.
- Bond price and yield round-trip.
- Duration and convexity match known examples.

### 12.8 Acceptance

- No accidental recursion between price and yield calculations.
- All financial results document whether rates are annual or periodic.
- No binary floating-point equality checks without tolerance.

---

## 13. Module: UnitsAndMeasurement

### 13.1 File

`OPEN-LRD/Skills/UnitsAndMeasurement.cs`

### 13.2 Purpose

Add unit conversion, dimensional analysis, significant figures, and uncertainty
propagation.

This is not a full unit-expression parser. It is a table-driven conversion and
measurement module.

### 13.3 Types

```csharp
internal readonly record struct UnitDimension(
    int Length,
    int Mass,
    int Time,
    int Current,
    int Temperature,
    int Amount,
    int LuminousIntensity);

internal sealed record Unit(
    string Symbol,
    double Scale,
    double Offset,
    UnitDimension Dimension);

internal readonly record struct Measurement(
    double Value,
    double Uncertainty,
    Unit Unit);

internal sealed record UncertaintyResult(
    double Value,
    double Uncertainty);
```

### 13.4 API surface

Dimensions and units:

- `UnitDimension Multiply(UnitDimension left, UnitDimension right)`
- `UnitDimension Divide(UnitDimension left, UnitDimension right)`
- `UnitDimension Power(UnitDimension dimension, int exponent)`
- `UnitCatalog.Length`, `Mass`, `Time`, `Temperature`, etc.
- `FindUnit(string symbol)`
- `TryFindUnit(string symbol, out Unit unit)`

Conversion:

- `Convert(double value, Unit from, Unit to)`
- `TryConvert(double value, Unit from, Unit to, out double converted)`
- `Convert(Measurement measurement, Unit to)`
- `Add(Measurement left, Measurement right)`
- `Subtract(Measurement left, Measurement right)`
- `Multiply(Measurement left, Measurement right)`
- `Divide(Measurement left, Measurement right)`
- `Power(Measurement measurement, int exponent)`

Significant figures:

- `CountSignificantFigures(string text)`
- `RoundToSignificantFigures(double value, int significantFigures)`
- `FormatSignificantFigures(double value, int significantFigures)`
- `FormatMeasurement(Measurement measurement)`

Uncertainty:

- `CombineAdditive(double firstValue, double firstUncertainty, double secondValue, double secondUncertainty)`
- `CombineMultiplicative(double firstValue, double firstUncertainty, double secondValue, double secondUncertainty)`
- `PropagatePower(double value, double uncertainty, int exponent)`
- `PropagateFunction(double value, double uncertainty, Func<double, double> function, Func<double, double> derivative)`

### 13.5 Algorithms

- Unit conversion uses `baseValue = value * scale + offset` and
  `targetValue = (baseValue - targetOffset) / targetScale`.
- Dimensional arithmetic adds exponents component-wise.
- Additive uncertainty uses root-sum-square.
- Multiplicative uncertainty uses relative uncertainties.
- Significant-figure formatting uses invariant culture and plain decimal
  notation where feasible.

### 13.6 Validation

- Incompatible dimensions reject conversion and arithmetic.
- Scale must be positive and finite.
- Offset is allowed only for affine units such as temperature.
- Uncertainty must be non-negative.
- Significant-figure count must be positive.
- Zero values with negative exponents or division by zero throw.

### 13.7 Tests

- Length, mass, time, and volume conversions match known values.
- Celsius/Fahrenheit/Kelvin conversions are correct.
- Incompatible units throw.
- Dimensional arithmetic combines exponents correctly.
- Additive and multiplicative uncertainty propagation match known examples.
- Significant-figure counting and formatting match examples.
- Measurement formatting includes units and uncertainty.

### 13.8 Acceptance

- Unit conversion is table-driven and deterministic.
- Temperature affine offsets are handled explicitly.
- No culture-dependent parsing or formatting.

---

## 14. Optional Extension: Graph Centrality

`GraphAlgorithms` already covers traversal, shortest paths, components, MST,
maximum flow, PageRank, and bipartite checks. If graph analysis expands, add a
separate extension rather than rewriting the class:

Possible additions:

- `DegreeCentrality`
- `ClosenessCentrality`
- `BetweennessCentrality` using Brandes' algorithm
- `EigenvectorCentrality`
- `CommunityDetection` using label propagation

These should be added only if LRD reasoning needs them.

---

## 15. Shared Test Project Updates

For every new module:

1. Add the source file to
   `OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj`:

   ```xml
   <Compile Include="../OPEN-LRD/Skills/ModuleName.cs" />
   ```

2. Add `OPEN-LRD.SmokeTests/ModuleNameTests.cs`.

3. Add `ModuleNameTests.RunAll();` to `OPEN-LRD.SmokeTests/Program.cs`.

4. Keep tests dependency-free and use the existing `Run`, `Assert`,
   `AssertEqual`, `AssertMatrixEqual`, and `AssertThrows` helpers.

5. Avoid comments in source files unless the repository style changes. The
   current math modules are self-documenting and do not carry comments.

---

## 16. Definition of Done

A module is complete only when all of the following are true:

- The implementation file exists under `OPEN-LRD/Skills/`.
- The module is `internal` and belongs to `OPEN_LRD.Skills`.
- No external NuGet dependency was added.
- Existing modules are reused where appropriate.
- Invalid inputs are handled with explicit exceptions or `Try...` results.
- Randomized methods accept a `Random` or seed.
- The smoke test project compiles the new source file.
- A dedicated test class covers normal behavior, edge cases, and validation.
- `Program.Main` runs the new test class.
- The full solution builds with `0 Warning(s)` and `0 Error(s)`.
- The complete smoke suite passes with `0 failed`.
- This plan's module status is updated.

---

## 17. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| Duplicating existing functions | Check `Math_1`–`Math_4`, `Algebra`, `LinearAlgebraAdvanced`, and `GraphAlgorithms` before adding APIs |
| Numerical instability | Use log-space, pivoting, stable summation, and explicit tolerances |
| Overflow in exact combinatorics | Use `BigInteger` |
| Float/double overload ambiguity | Keep advanced modules `double`-first; add float overloads only when required |
| Random test flakiness | Require seeded `Random`; avoid unseeded statistical assertions |
| Slow tests | Use small matrices, short signals, and bounded iteration counts |
| Scope creep into symbolic transforms or full unit parsing | Keep clear module boundaries and mark optional work separately |
| Warning-as-error failures | Build each module after adding tests; no unused public API warnings are expected for internal types |
| Large allocations | Cap FFT sizes, sieve ranges, sample counts, and solver iterations |
| Inconsistent matrix conventions | Document row-major `double[,]` and reuse `Algebra.Matrix*` helpers |

### Deferred optional numerical refinements

The following items are intentionally outside behavior-preserving helper reuse.
They require separate approval because they can change numerical results,
callback behavior, or tolerance-sensitive edge cases:

- **`Math_4.CurveDerivative`:** use Richardson-extrapolated component
  derivatives from `Math_3.NumericDerivative`. This may improve estimates for
  smooth curves, but changes the sampling step and number of curve evaluations.
- **`Math_1.CalculateR2Score`:** express the score through existing variance and
  mean-squared-error helpers. Although algebraically equivalent, the changed
  intermediate rounding and zero-variance behavior need explicit review.
- **`Algebra.MatrixInverse`:** consolidate elimination through
  `MatrixRowReduce`, retaining an explicit singularity check. Near-singular
  pivot decisions and floating-point results may differ.
- **`SymbolicCalculus` polynomial helpers:** delegate polynomial operations or
  linear solves to `Algebra` only after choosing a shared tolerance policy.
  Current coefficient trimming and pivot thresholds differ and can change
  small-coefficient or near-singular cases.

---

## 18. Recommended Next Step

Start with `ProbabilityAndStatistics` because it provides the widest missing
capability and supplies special functions that later modules can reuse.

The implementation sequence for that module is:

1. Add `ProbabilityAndStatistics.cs` with special functions and normal
   distribution.
2. Add discrete and continuous distributions.
3. Add confidence intervals and hypothesis tests.
4. Add Bayesian helpers.
5. Add regression wrappers over `LinearAlgebraAdvanced`.
6. Add `ProbabilityAndStatisticsTests.cs`, wire it into the smoke project, and
   run the full build and smoke suite.
