# MathFunctionDispatcher Coverage, Unit Metadata, and Exclusion Matrix

**Status:** Resolved (2026-11-22)  
**Scope:** `OPEN-LRD/Skills/MathFunctionDispatcher.cs`, `OPEN-LRD/Skills/*.cs`, `OPEN-LRD/Persistence/FoundationBinarySerializer.cs`, and `OPEN-LRD.SmokeTests/MathFunctionDispatcherTests.cs`

---

## 1. Global Dispatchability Contract

`"All Skill libraries"` is implemented via the explicit allowlist `MathFunctionDispatcher.DispatchSkillTypes`. A method on an allowlisted skill class is registered as **dispatchable** if and only if:

1. It is `public static` and declared directly on the skill class (`BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly`).
2. It is not generic (`!IsGenericMethod`), not a property accessor or operator (`!IsSpecialName`), and does not return `void`.
3. Neither its return type nor any parameter type is `byref` or `byref-like` (`!IsOut`, `!ParameterType.IsByRef`, `!ParameterType.IsByRefLike`).
4. It has no optional/default parameters (`!IsOptional`) and no `params` arrays (`ParamArrayAttribute`).
5. Every parameter type and the return type belong to the Format-v3 persistable `DispatchValueTypes` supported by `FoundationBinarySerializer`:
   - **Primitives & System Scalars:** `string`, `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `char`, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`
   - **Extended Math, Vector & Array Types:** `BigInteger`, `Fraction`, `OPEN_LRD.Foundations.Vector2`, `OPEN_LRD.Foundations.Vector3`, `Complex`, `double[]`, `int[]`

Excluded methods on allowlisted types are deterministically recorded in `MathFunctionDispatcher.GetDiscoveryDiagnostics()`.

### Exclusion Categories

- **`E-TYPE` (Non-persistable domain/container/tuple/record/matrix type):** Uses `List<T>`, `IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>`, `IReadOnlySet<T>`, `double[,]`, `double[,,]`, `int[,]`, `Complex[]`, `Vector3[]`, `Expression`, `Unit`, `UnitDimension`, `Measurement`, `Quaternion`, `ScaledScalar`, `DateOnly`, `TimeOnly`, `TimeZoneInfo`, `ValueTuple`, or domain result records/enums.
- **`E-DELEGATE` (Higher-order callback):** Accepts `Func<...>`, `ScalarFunction2D/3D`, `VectorField2D/3D`, `ParametricCurve`, `ParametricSurface`, or `SpacetimeMetric` delegates.
- **`E-NONDET` (Non-deterministic RNG):** Accepts `System.Random` (non-deterministic results cannot be promoted to trusted).
- **`E-SIG` (Unsupported C# signature modifier):** Uses `out`/`ref` parameters, optional/default arguments, `params` arrays, or generic `<T>` type parameters.
- **`E-INTERNAL` (Non-allowlisted internal/generic class):** Internal AST engine (`SymbolicCalculus`) or generic structural graph algorithm library (`GraphAlgorithms`, invoked via `LRD_Core.BuildSkillsGraphSnapshot`).

---

## 2. Canonical Key Stability & Cross-Library Overload Ambiguity Policy

1. **Canonical Key Format:**
   - Every registered method has a stable canonical key `<DeclaringType.Name>.<Method.Name>` (e.g., `BasicPhysics.Force`, `Math_1.IsPrime`, `DiscreteMath.IsPrime`).
   - Descriptors are registered in deterministic ordinal order: `(DeclaringType.FullName, Method.Name, ParameterTypes.FullName, ReturnType.FullName)`.
   - Persisted `FunctionBox` provenance (`FunctionDispatchProvenance.FunctionKey`) always stores the canonical `<DeclaringType.Name>.<Method.Name>` key.
2. **Within-Library Overload Disambiguation:**
   - Overloads sharing a canonical key disambiguate by exact `(ParameterTypes, ExpectedResultType, Units)` signature (e.g., `BasicPhysics.Force(double, double)` vs `BasicPhysics.Force(double, Vector3)`).
3. **Cross-Library Unqualified Name Collisions:**
   - Unqualified method names or keyless `FunctionBox` definitions resolve only when `(ParameterTypes, ExpectedResultType, Units)` matches **exactly one** registered descriptor across all libraries (e.g., `IsPrime(int)` resolves to `Math_1.IsPrime`, whereas `IsPrime(long)` resolves to `DiscreteMath.IsPrime`).
   - If multiple libraries share the same unqualified name, parameter/return types, and units (e.g., `Simplify(string)` in `Algebra.Simplify` and `Math_3.Simplify`, or `Tanh(double)` in `Math_2` and `InformationTheoryAndMLMath`), `SelectFunction` throws `InvalidOperationException` listing the colliding canonical keys; callers must supply the qualified `Class.Method` key.

---

## 3. Per-Library Dispatch, Unit Metadata, and Exclusion Matrix

### 3.1 Pure & Symbolic Math Libraries

| Library | Dispatchable Count & Methods | Unit Metadata Policy | Excluded Count, Methods & Reasons |
| :--- | :--- | :--- | :--- |
| **`Math_1`** | **96 methods:**<br>- Scalar `int`/`double`/`float` arithmetic: `Add*`, `Subtract*`, `Multiply*`, `Divide*`, `Modulus*`, `Abs*`, `Negate*`, `Min*`, `Max*`, `Clamp*`, `Square*`, `Sqrt*`, `Cbrt*`, `Pow*`, `Floor*`, `Ceiling*`, `Round*` (1- & 2-arg), `Truncate*`, `Sign*`, `Log*`, `Log10*`, `LogBase*`, `Exp*`, `Sin*`, `Cos*`, `Tan*`, `Atan2*`, `DegreesToRadians`, `RadiansToDegrees`<br>- `Fraction` helpers: `AddFraction`, `SubtractFraction`, `MultiplyFraction`, `DivideFraction`, `SimplifyFraction` (`Fraction`, `BigInteger,BigInteger`, `int,int`), `InvertFraction`, `NegateFraction`, `CompareFractions`, `ConvertToMixedNumber`<br>- Number theory & percentages: `CalculateGCD` (`int`, `BigInteger`), `CalculateLCM`, `CalculateLCD`, `IsPrime`, `IsEven`, `IsOdd`, `IsPerfectSquare`, `CalculateFactorial`, `CalculateCombinations`, `CalculatePermutations`, `SumOfDigits`, `ReverseInt`, `CalculatePowerInt`, `CalculatePercentage`, `CalculatePercentageChange`, `CalculatePercentageIncrease`, `CalculatePercentageDecrease` | **Dimensionless** (no unit metadata) | **49 methods (`E-TYPE`):**<br>All `List<int>`, `List<double>`, `List<float>`, and `List<Fraction>` collection/statistical helpers (`AddRange*`, `SubtractRange*`, `MultiplyRange*`, `DivideRange*`, `CalculateAverage`, `CalculateStandardDeviation`, `CalculateVariance`, `CalculateZScore`, `CalculatePercentile`, `CalculateMedian`, `CalculateMode`, `CalculateRange`, `CalculateCoefficientOfVariation`, `CalculateSkewness`, `CalculateKurtosis`, `CalculateCovariance`, `CalculateCorrelationCoefficient`, `CalculateLinearRegression*`, `CalculateExponentialMovingAverage`, `CalculateWeightedAverage`, `CalculateGeometricMean`, `CalculateHarmonicMean`, `CalculateRootMeanSquare`, `CalculateMeanAbsolute*`, `CalculateInterquartileRange`, `CalculateMeanSquaredError`, `CalculateRootMeanSquaredError`, `CalculateR2Score`, `CalculateAdjustedR2Score`, `CalculateMeanDirectionalAccuracy`, `AddFractions`, `CalculateMin/Max/Sum/Product`, `CalculateCumulativeSum`, `CalculateMovingAverage`, `CalculateSimpleMovingAverage`, `CalculateMedianAbsoluteDeviation`, `CalculateTrimmedMean`, `CalculateSampleVariance`, `CalculateSampleStandardDeviation`, `CalculatePrimeFactors`). |
| **`Math_2`** | **148 methods:**<br>- `double`/`float` inverse/reciprocal trig & hyperbolic: `Asin*`, `Acos*`, `Atan*`, `Sec*`, `Csc*`, `Cot*`, `Sinh*`, `Cosh*`, `Tanh*`, `Asinh*`, `Acosh*`, `Atanh*`<br>- Angle helpers: `NormalizeRadians`, `NormalizeDegrees`, `CalculateAngleDifference*`<br>- 2D/3D mensuration & analytic geometry: `CalculateHypotenuse`, `CalculateLeg`, `CalculateTriangle*`, `CalculateCircle*`, `CalculateAnnulusArea`, `CalculateRectangle*`, `CalculateSquare*`, `CalculateParallelogram*`, `CalculateTrapezoidArea`, `CalculateRhombus*`, `CalculateRegularPolygon*`, `CalculateEllipse*`, `CalculateCyclicQuadrilateralArea`, `CalculateDistance2D/3D`, `CalculateMidpoint2D/3D`, `CalculateSlope`, `CalculateLineIntercept`, `CalculatePointLineDistance`, `CalculateSectionPoint`, `CalculateAngleBetweenLines*`, `RotatePoint*`, `TranslatePoint`, `ScalePoint`, `CalculateSphere*`, `CalculateHemisphere*`, `CalculateCube*`, `CalculateCuboid*`, `CalculateCylinder*`, `CalculateCone*`, `CalculateSquarePyramid*`, `CalculateTorus*`, `CalculateDistancePointToPlane`<br>- `Vector2`/`Vector3` operations: `CalculateVectorMagnitude`, `AddVectors`, `SubtractVectors`, `ScaleVector`, `CalculateDotProduct`, `CalculateCrossProduct`, `NormalizeVector`, `CalculateAngleBetweenVectors*`, `CalculateVectorProjection`, `CalculateVectorDistance` | **Tagged (6 overloads):**<br>- `CalculateHypotenuse` (`double`/`float`): `["m", "m"] -> "m"`<br>- `CalculateRectangleArea` (`double`/`float`): `["m", "m"] -> "m²"`<br>- `CalculateCircleArea` (`double`/`float`): `["m"] -> "m²"`<br>**Dimensionless (remaining 142):** Coordinate, vector, and abstract geometric helpers do not require `"m"`. | **12 methods:**<br>- `IsRightTriangle` (`double`/`float`), `AreLinesParallel` (`double`/`float`), `AreLinesPerpendicular` (`double`/`float`): **`E-SIG`** (optional `tolerance` parameter).<br>- `CalculateLineEquation` (`double`/`float`), `CalculateCircleThroughPoints`: **`E-TYPE`** (`ValueTuple` return).<br>- `CalculatePolygonAreaShoelace`, `CalculatePolygonPerimeter`, `CalculatePolygonCentroid`: **`E-TYPE`** (`List<Vector2>` parameter). |
| **`Math_3`** | **12 methods:**<br>`Simplify(string)`, `DifferentiateToString(string, string, int)`, `IntegrateToString(string, string)`, `Evaluate(string, string, double)`, `EvaluateFloat(string, string, float)`, `DefiniteIntegral(string, string, double, double)`, `DefiniteIntegralFloat(string, string, float, float)`, `GeometricSeriesSum(double, double)`, `ArcLength(string, string, double, double)`, `VolumeOfRevolutionDisk(string, string, double, double)`, `AverageValue(string, string, double, double)`, `TaylorSeriesToString(string, string, double, int)` | **Dimensionless** | **47 methods:**<br>- `Parse`, `Simplify(Expression)`, `Differentiate(...)`, `Integrate(...)`, `Evaluate(Expression, ...)`, `DefiniteIntegral(Expression, ...)`, `Limit*`, `TaylorSeries(Expression/string, ...)`, `MaclaurinSeries`, `TaylorSeriesValue`, `TaylorCoefficient`, `ArcLength(Expression, ...)`, `VolumeOfRevolution*(Expression, ...)`, `SurfaceAreaOfRevolution`, `AverageValue(Expression, ...)`, `MeanValueTheoremPoint`, `FindCriticalPoints`, `FindInflectionPoints`, `ClassifyCriticalPoint`: **`E-TYPE`** (`Expression`, `IReadOnlyDictionary`, `LimitDirection`, `ExtremumType`) and/or **`E-SIG`** (optional parameters).<br>- `TryIntegrate` (2 overloads): **`E-SIG`** (`out Expression?`).<br>- `NumericDerivative*`, `NumericSecondDerivative`, `NumericIntegral`, `AdaptiveIntegral`, `TrapezoidalIntegral`, `InfiniteSeriesSum`, `RiemannSum`, `ArcLengthParametric`, `NewtonRaphson`, `Bisection`, `FindRoot`, `FindRoots`: **`E-DELEGATE`** (`Func<...>`) and/or **`E-SIG`** (optional parameters). |
| **`Math_4`** | **0 methods** | N/A | **43 methods:**<br>- `PartialDerivative(Expression/string, string)`, `GradientAtPoint(Expression, ...)`, `Hessian(Expression, ...)`, `Evaluate`: **`E-TYPE`** (`Expression`, `Expression[,]`, `IReadOnlyDictionary`).<br>- `Gradient(Expression/string, params string[])`, `Laplacian(Expression/string, params string[])`: **`E-SIG`** (`params`) & **`E-TYPE`** (`Expression[]`).<br>- All numerical multivariable/vector-calculus & ODE solvers (`PartialDerivative`, `Gradient`, `SecondPartialDerivative`, `MixedPartialDerivative`, `Hessian`, `DirectionalDerivative`, `Laplacian`, `Divergence`, `Curl`, `Jacobian`, `DoubleIntegral*`, `TripleIntegral*`, `ScalarLineIntegral`, `VectorLineIntegral`, `FluxLineIntegral`, `SurfaceArea*`, `ScalarSurfaceIntegral`, `FluxIntegral`, `ArcLength*`, `AreaOfPolarRegion`, `CenterOfMass`, `MomentOfInertia*`, `AverageValue`, `WorkDoneByField`, `GradientDescent`, `Euler`, `ImprovedEuler`, `RungeKutta4*`): **`E-DELEGATE`** (`ScalarFunction2D/3D`, `VectorField2D/3D`, `ParametricCurve/Surface`, `Func<...>`) and/or **`E-TYPE`** (`double[,]`, `OdeSolution`, `OdeSystemSolution`). |
| **`Algebra`** | **42 methods:**<br>- Polynomials (`double[]`): `NormalizePolynomial`, `PolynomialDegree`, `PolynomialLeadingCoefficient`, `PolynomialEvaluate` (`double`/`Complex`), `AddPolynomials`, `SubtractPolynomials`, `MultiplyPolynomials`, `ScalePolynomial`, `ComposePolynomials`, `PolynomialDerivative`, `PolynomialIntegral`, `PolynomialGCD`, `PolynomialLCM`, `FindRationalRoots`, `FormatPolynomial`, `CalculateDiscriminant`, `CalculateQuadraticAxisOfSymmetry`, `SolveQuadraticReal`, `SolvePolynomialReal`<br>- Radicals & equations: `NthRoot`, `RationalPower`, `SolveExponentialEquation`, `SolveLogarithmicEquation`<br>- Complex numbers: `AddComplex`, `SubtractComplex`, `MultiplyComplex`, `DivideComplex`, `NegateComplex`, `ConjugateComplex`, `ComplexModulus`, `ComplexArgument`, `ComplexPower`, `ComplexSquareRoot`, `ComplexExponential`, `ComplexLogarithm`, `ComplexSine`, `ComplexCosine`, `ComplexTangent`, `ComplexFromPolar`<br>- Symbolic string helpers: `Simplify(string)`, `ExpandToString(string)`, `FactorToString(string, string)` | **Dimensionless** | **56 methods:**<br>- `DividePolynomials`, `FactorPolynomial`, `SolveLinear`, `CalculateQuadraticVertex`, `SolveQuadraticComplex`, `SolvePolynomial`, `SimplifySquareRoot`, `ComplexToPolar`, `ComplexRootsOfUnity`: **`E-TYPE`** (`PolynomialDivisionResult`, `PolynomialFactor[]`, `LinearSolution`, `ValueTuple`, `Complex[]`, `SquareRootSimplification`).<br>- `TrySyntheticDivision`, `TryGetPolynomial`, `TrySolveLinearSystem`, `QuaternionToAxisAngle`: **`E-SIG`** (`out` parameters).<br>- `MatrixIdentity`, `MatrixTranspose`, `MatrixAdd`, `MatrixSubtract`, `MatrixScale`, `MatrixMultiply`, `MatrixPower`, `MatrixTrace`, `MatrixDeterminant`, `MatrixInverse`, `MatrixRank`, `MatrixRowReduce`, `SolveLinearSystem`: **`E-TYPE`** (`double[,]`).<br>- All `Quaternion` methods (`AddQuaternions`, `SubtractQuaternions`, `NegateQuaternion`, `ScaleQuaternion`, `MultiplyQuaternions`, `ConjugateQuaternion`, `QuaternionNorm*`, `NormalizeQuaternion`, `InverseQuaternion`, `DotQuaternions`, `QuaternionFromAxisAngle`, `RotateVector`, `QuaternionLerp`, `QuaternionSlerp`): **`E-TYPE`** (`Quaternion`).<br>- `SolveLinearInequality`, `SolveQuadraticInequality`, `SolvePolynomialInequality`: **`E-TYPE`** (`Interval[]`, `InequalityRelation`).<br>- `PolynomialToExpression`, `Simplify(Expression)`, `Expand(Expression/string)`, `Substitute(...)`, `CollectPolynomialTerms`, `Factor(Expression/string, string)`: **`E-TYPE`** (`Expression`). |
| **`SymbolicCalculus`** | **0 methods (Not in `DispatchSkillTypes`)** | N/A | **`E-INTERNAL`:** Internal AST simplification, differentiation, integration, and evaluation engine backing `Math_3`, `Math_4`, and `Algebra`. |

---

### 3.2 Applied Math, Statistics, ML, Numerics & Discrete Libraries

| Library | Dispatchable Count & Methods | Unit Metadata Policy | Excluded Count, Methods & Reasons |
| :--- | :--- | :--- | :--- |
| **`ProbabilityAndStatistics`** | **30 methods:**<br>- Special functions: `Erf`, `Erfc`, `LogGamma`, `RegularizedGammaP`, `RegularizedGammaQ`, `IncompleteBeta`, `RegularizedIncompleteBeta`<br>- Distributions: `NormalPdf`, `NormalCdf`, `NormalQuantile`, `BinomialPmf`, `BinomialCdf`, `PoissonPmf`, `PoissonCdf`, `ExponentialPdf`, `ExponentialCdf`, `ExponentialQuantile`, `UniformPdf`, `UniformCdf`, `BetaPdf`, `BetaCdf`, `GammaPdf`, `GammaCdf`, `StudentTPdf`, `StudentTCdf`, `ChiSquarePdf`, `ChiSquareCdf`, `FPdf`, `FCdf`<br>- Bayesian: `BayesTheorem` | **Dimensionless** | **24 methods:**<br>- `NormalSample`, `BinomialSample`, `PoissonSample`, `ExponentialSample`: **`E-NONDET`** & **`E-TYPE`** (`Random`).<br>- `NormalConfidenceInterval`, `MeanConfidenceInterval`, `ProportionConfidenceInterval`, `DifferenceInMeansConfidenceInterval`: **`E-TYPE`** (`ConfidenceInterval`).<br>- `OneSampleZTest`, `OneSampleTTest`, `PairedTTest`, `TwoSampleTTest`, `ProportionZTest`, `ChiSquareGoodnessOfFit`, `ChiSquareIndependence`: **`E-TYPE`** (`HypothesisTestResult`, `TailType`, `int[,]`).<br>- `OneWayAnova`: **`E-SIG`** (`params`) & **`E-TYPE`** (`AnovaResult`, `double[][]`).<br>- `BetaBinomialPosterior`, `NormalNormalPosterior`, `GammaPoissonPosterior`: **`E-TYPE`** (`ValueTuple`).<br>- `SimpleLinearRegression`, `MultipleLinearRegression`, `LogisticRegression`: **`E-TYPE`** (`RegressionResult`, `double[,]`). |
| **`DiscreteMath`** | **23 methods:**<br>`Mod` (`long`, `BigInteger`), `ModPow` (`long`, `BigInteger`), `ModInverse` (`long`, `BigInteger`), `IsCoprime`, `Sieve`, `IsPrime`, `PollardRho`, `EulerPhi`, `DivisorCount`, `DivisorSum`, `Moebius`, `Liouville`, `Catalan`, `Bell`, `StirlingFirst`, `StirlingSecond`, `Partition`, `Derangement`, `Binomial`, `Multinomial`, `ToBase`, `FromBase`, `DigitSum` | **Dimensionless** | **12 methods:**<br>- `TryModInverse` (2 overloads), `ExtendedGcd` (2 overloads), `TryChineseRemainderTheorem`: **`E-SIG`** (`out` parameters).<br>- `IsProbablePrime` (2 overloads): **`E-NONDET`** & **`E-TYPE`** (`Random`).<br>- `SolveLinearCongruence`, `SegmentedSieve`, `Factorize` (2 overloads), `ChineseRemainderTheorem`: **`E-TYPE`** (`CongruenceSolution`, `long[]`, `PrimeFactorization`, `BigIntegerPrimeFactorization`, `CrtResult`). |
| **`InformationTheoryAndMLMath`** | **18 methods:**<br>`Entropy`, `KlDivergence`, `JensenShannonDivergence`, `CrossEntropy`, `Sigmoid` (`double`, `double[]`), `Softmax`, `Relu`, `LeakyRelu`, `Tanh`, `Gelu`, `Softplus`, `MeanSquaredError`, `MeanAbsoluteError`, `BinaryCrossEntropy`, `CategoricalCrossEntropy`, `HingeLoss`, `RocAuc`, `CosineSimilarity` | **Dimensionless** | **10 methods:**<br>- `JointEntropy`, `ConditionalEntropy`, `MutualInformation`, `Standardize`, `Pca`, `KMeans`: **`E-TYPE`** (`double[,]`, `StandardizationResult`, `PcaResult`, `KMeansResult`).<br>- `BuildConfusionMatrix`, `CalculateClassificationMetrics`: **`E-TYPE`** (`ConfusionMatrix`, `ClassificationMetrics`).<br>- `Distance`: **`E-TYPE`** (`DistanceMetric`) & **`E-SIG`** (optional `p = 2.0`).<br>- `JaccardSimilarity<T>`: **`E-SIG`** (generic `<T>`) & **`E-TYPE`** (`IReadOnlySet<T>`). |
| **`FinancialMath`** | **14 methods:**<br>`ContinuousCompoundInterest`, `PresentValue`, `FutureValue`, `AnnuityPresentValue`, `AnnuityFutureValue`, `LoanPayment`, `StraightLineDepreciation`, `DecliningBalanceDepreciation`, `SumOfYearsDigitsDepreciation`, `BondPrice`, `BondYield`, `MacaulayDuration`, `ModifiedDuration`, `Convexity` | **Dimensionless / Currency-Agnostic** | **9 methods:**<br>- `CompoundInterest`, `EffectiveAnnualRate`, `DepreciationSchedule`: **`E-TYPE`** (`CompoundingFrequency`, `DepreciationMethod`).<br>- `AmortizationSchedule`, `NetPresentValue`, `InternalRateOfReturn`, `PaybackPeriod`, `ProfitabilityIndex`, `CalculateBondMetrics`: **`E-TYPE`** (`IReadOnlyList<CashFlow>`, `IReadOnlyList<AmortizationEntry>`, `InvestmentResult`, `BondResult`) and/or **`E-SIG`** (optional parameters). |
| **`LinearAlgebraAdvanced`** | **0 methods** | N/A | **15 methods (`E-TYPE` / `E-SIG`):**<br>All methods take or return `double[,]` (`IsSymmetric`, `LuDecomposition`, `QrDecomposition`, `TryCholeskyDecomposition`, `CholeskyDecomposition`, `SymmetricEigenDecomposition`, `Eigenvalues`, `SingularValueDecomposition`, `PseudoInverse`, `NullSpace`, `ColumnSpace`, `LeastSquares`, `MatrixNorm`, `ConditionNumber`, `MatrixVectorMultiply`). |
| **`NumericalMethods`** | **4 methods:**<br>`LagrangeInterpolate(double[], double[], double)`, `NewtonDividedDifference(double[], double[], double)`, `PchipInterpolate(double[], double[], double)`, `AitkenDeltaSquared(double, double, double)` | **Dimensionless** | **23 methods:**<br>- `BuildNewtonInterpolator`, `Secant`, `FalsePosition`, `FixedPoint`, `Brent`, `NewtonSystem`, `Romberg`, `AdaptiveSimpson`, `GaussLegendre`, `MonteCarloIntegral`, `RungeKutta45*`, `DormandPrince`, `RichardsonExtrapolation`: **`E-DELEGATE`** (`Func<...>`) and/or **`E-NONDET`** (`Random`) / **`E-SIG`** (optional parameters).<br>- `BuildCubicSpline`, `CubicSplineInterpolate`, `PolynomialLeastSquares`, `ExponentialFit`, `PowerFit`, `LogarithmicFit`, `Jacobi`, `GaussSeidel`, `ConjugateGradient`: **`E-TYPE`** (`CubicSpline`, `SplineBoundary`, `CurveFitResult`, `IterativeSolverResult`, `double[,]`) and/or **`E-SIG`** (optional parameters). |
| **`Optimization`** | **0 methods** | N/A | **12 methods:**<br>- `GoldenSectionSearch`, `BrentMinimize`, `GradientDescent`, `Bfgs`, `ConjugateGradient`, `NelderMead`, `SimulatedAnnealing`, `ProjectedGradient`, `PenaltyMethod`: **`E-DELEGATE`** (`Func<...>`) and/or **`E-SIG`** (optional parameters) / **`E-NONDET`** (`Random`).<br>- `Simplex`, `TwoPhaseSimplex`, `HungarianAssignment`: **`E-TYPE`** (`double[,]`, `LinearProgrammingResult`, `AssignmentResult`). |
| **`Geometry3DAndGraphics`** | **5 methods:**<br>`IsPointInTriangle(Vector2, Vector2, Vector2, Vector2)`, `TriangleNormal(Vector3, Vector3, Vector3)`, `TriangleArea(Vector3, Vector3, Vector3)`, `CatmullRomPoint(Vector3, Vector3, Vector3, Vector3, double)`, `CatmullRomTangent(Vector3, Vector3, Vector3, Vector3, double)` | **Dimensionless** (mesh/spline coordinates) | **22 methods:**<br>- `ConvexHull`, `IsPointInPolygon`, `ClipPolygon`, `TriangulatePolygon`, `BoundingBoxFromPoints`, `BezierPoint`, `BezierDerivative`: **`E-TYPE`** (`IReadOnlyList<Vector2/3>`, `Triangle2D`, `BoundingBox`).<br>- `SegmentIntersection`: **`E-SIG`** (`out Vector2`).<br>- `BarycentricCoordinates` (2 overloads), `RayPlaneIntersection`, `RaySphereIntersection`, `RayTriangleIntersection`, `RayBoxIntersection`: **`E-TYPE`** (`BarycentricCoordinates`, `Triangle`, `Ray`, `Plane`, `BoundingBox`, `RayHit`).<br>- `TranslationMatrix`, `RotationX/Y/ZMatrix`, `ScaleMatrix`, `MultiplyMatrices`, `TransformPoint`, `TransformDirection`, `PerspectiveProjection`, `LookAt`: **`E-TYPE`** (`double[,]`). |
| **`TransformsAndSignals`** | **9 methods:**<br>`NextPowerOfTwo`, `Convolve`, `ConvolveFft`, `CrossCorrelate`, `AutoCorrelate`, `MovingAverageFilter`, `OnePoleLowPass`, `OnePoleHighPass`, `BiquadFilter` | **Dimensionless** (discrete sample arrays) | **11 methods:**<br>- `Dft`, `InverseDft`, `Fft`, `InverseFft`, `RealFft`, `FftShift`, `FourierSeriesReconstruct`: **`E-TYPE`** (`IReadOnlyList<double/Complex>`, `Complex[]`).<br>- `CreateWindow`, `ApplyWindow`, `ApplyFirFilter`: **`E-TYPE`** (`WindowType`, `FirFilter`).<br>- `FourierSeriesCoefficients`: **`E-DELEGATE`** (`Func<double, double>`) & **`E-TYPE`** (`Complex[]`). |
| **`TimeAndDate`** | **4 methods:**<br>`IsLeapYear(int)`, `DaysInMonth(int, int)`, `ToUnixTimeSeconds(DateTimeOffset)`, `FromUnixTimeSeconds(long)` | **Dimensionless** (calendar/epoch values) | **10 methods:**<br>- `DayOfYear`, `IsoWeek`, `DaysBetween`, `AddBusinessDays`, `BusinessDaysBetween`, `Age`, `FormatDate`: **`E-TYPE`** (`DateOnly`, `ValueTuple`, `IReadOnlySet<DateOnly>`).<br>- `ConvertTime`: **`E-TYPE`** (`TimeZoneInfo`).<br>- `ElapsedTime`: **`E-TYPE`** (`TimeOnly`) & **`E-SIG`** (optional `crossesMidnight`).<br>- `TryParseDateExact`: **`E-SIG`** (`out DateOnly`). |
| **`UnitsAndMeasurement`** | **3 methods:**<br>`CountSignificantFigures(string)`, `RoundToSignificantFigures(double, int)`, `FormatSignificantFigures(double, int)` | **Dimensionless** | **17 methods:**<br>- `Multiply`, `Divide`, `Power`, `FindUnit`, `Convert`, `ConvertToSI`, `ConvertFromSI`, `Add`, `Subtract`, `FormatMeasurement`: **`E-TYPE`** (`UnitDimension`, `Unit`, `Measurement`).<br>- `TryFindUnit`, `TryConvert`: **`E-SIG`** (`out` parameters).<br>- `CombineAdditive`, `CombineMultiplicative`, `PropagatePower`, `PropagateFunction`: **`E-TYPE`** (`UncertaintyResult`) & **`E-DELEGATE`** (`Func<double, double>`). |
| **`GraphAlgorithms`** | **0 methods (Not in `DispatchSkillTypes`)** | N/A | **`E-INTERNAL` / `E-SIG`:** All 12 methods are generic (`<TNode>`) operating on `Graph<TNode>`. Invoked directly via `LRD_Core.BuildSkillsGraphSnapshot()`. |

---

### 3.3 Physics & Engineering Libraries (SI Unit Metadata Required)

Every dispatchable method in these libraries represents a physical law in SI units and carries explicit input and output unit metadata in `MathFunctionDispatcher` and `UnitCatalog`.

| Library | Dispatchable Method & Signature | Required Input Units -> Output Unit | Excluded Methods & Reasons |
| :--- | :--- | :--- | :--- |
| **`BasicPhysics`** (25 dispatchable) | `Force(double, double)`<br>`Force(double, Vector3)`<br>`MassFromForce(double, double)`<br>`Acceleration(double, double)`<br>`Acceleration(Vector3, double)`<br>`Weight(double)`<br>`Weight(double, Vector3)`<br>`Momentum(double, Vector3)`<br>`Impulse(Vector3, double)`<br>`Distance(Vector3)`<br>`Distance(double, double)`<br>`TravelTime(double, double)`<br>`KineticEnergy(double, double)`<br>`GravitationalPotentialEnergy(double, double, double)`<br>`Work(Vector3, Vector3)`<br>`Power(double, double)`<br>`Power(Vector3, Vector3)`<br>`Torque(Vector3, Vector3)`<br>`RotationalPower(Vector3, Vector3)`<br>`PositionAtTime(Vector3, Vector3, Vector3, double)`<br>`VelocityAtTime(Vector3, Vector3, double)`<br>`GravitationalForce(double, double, Vector3)`<br>`EscapeVelocity(double, double)`<br>`CircularOrbitSpeed(double, double)`<br>`ProjectilePosition(Vector3, Vector3, Vector3, double)` | `["kg", "m/s²"] -> "N"`<br>`["kg", "m/s²"] -> "N"`<br>`["N", "m/s²"] -> "kg"`<br>`["N", "kg"] -> "m/s²"`<br>`["N", "kg"] -> "m/s²"`<br>`["kg"] -> "N"`<br>`["kg", "m/s²"] -> "N"`<br>`["kg", "m/s"] -> "kg·m/s"`<br>`["N", "s"] -> "N·s"`<br>`["m"] -> "m"`<br>`["m/s", "s"] -> "m"`<br>`["m", "m/s"] -> "s"`<br>`["kg", "m/s"] -> "J"`<br>`["kg", "m/s²", "m"] -> "J"`<br>`["N", "m"] -> "J"`<br>`["J", "s"] -> "W"`<br>`["N", "m/s"] -> "W"`<br>`["m", "N"] -> "N·m"`<br>`["N·m", "rad/s"] -> "W"`<br>`["m", "m/s", "m/s²", "s"] -> "m"`<br>`["m/s", "m/s²", "s"] -> "m/s"`<br>`["kg", "kg", "m"] -> "N"`<br>`["kg", "m"] -> "m/s"`<br>`["kg", "m"] -> "m/s"`<br>`["m", "m/s", "m/s²", "s"] -> "m"` | **2 methods (`E-TYPE`):**<br>- `ProjectileTrajectory`: takes `IReadOnlyList<double>`, returns `TrajectoryResult`.<br>- `FlatGroundImpact`: returns `(double Time, double Range)` `ValueTuple`. |
| **`Thermodynamics`** (13 dispatchable) | `IdealGasPressure(double, double, double)`<br>`IdealGasVolume(double, double, double)`<br>`IdealGasTemperature(double, double, double)`<br>`IdealGasMoles(double, double, double)`<br>`HeatTransfer(double, double, double)`<br>`LatentHeat(double, double)`<br>`InternalEnergyChange(double, double)`<br>`ThermalExpansion(double, double, double)`<br>`CarnotEfficiency(double, double)`<br>`EntropyChange(double, double)`<br>`IdealGasIsothermalEntropyChange(double, double, double)`<br>`IdealGasConstantVolumeEntropyChange(double, double, double, double)`<br>`AdiabaticFinalTemperature(double, double, double, double)` | `["mol", "m³", "K"] -> "Pa"`<br>`["mol", "Pa", "K"] -> "m³"`<br>`["mol", "Pa", "m³"] -> "K"`<br>`["Pa", "m³", "K"] -> "mol"`<br>`["kg", "J/(kg·K)", "K"] -> "J"`<br>`["kg", "J/kg"] -> "J"`<br>`["J", "J"] -> "J"`<br>`["m", "1/K", "K"] -> "m"`<br>`["K", "K"] -> null`<br>`["J", "K"] -> "J/K"`<br>`["mol", "m³", "m³"] -> "J/K"`<br>`["mol", "K", "K", "J/(mol·K)"] -> "J/K"`<br>`["K", "m³", "m³", "1"] -> "K"` | **0 methods** |
| **`FluidMechanics`** (9 dispatchable) | `HydrostaticPressure(double, double, double, double)`<br>`BuoyantForce(double, double, double)`<br>`MassFlowRate(double, double)`<br>`ContinuityVelocity(double, double, double)`<br>`BernoulliPressure(double, double, double, double, double, double)`<br>`ReynoldsNumber(double, double, double, double)`<br>`PoiseuilleFlowRate(double, double, double, double)`<br>`DragForce(double, double, double, double)`<br>`DarcyWeisbachPressureDrop(double, double, double, double, double)` | `["Pa", "kg/m³", "m/s²", "m"] -> "Pa"`<br>`["kg/m³", "m/s²", "m³"] -> "N"`<br>`["kg/m³", "m³/s"] -> "kg/s"`<br>`["m²", "m/s", "m²"] -> "m/s"`<br>`["Pa", "kg/m³", "m/s", "m/s", "m", "m/s²"] -> "Pa"`<br>`["kg/m³", "m/s", "m", "Pa·s"] -> null`<br>`["m", "Pa", "m", "Pa·s"] -> "m³/s"`<br>`["1", "kg/m³", "m/s", "m²"] -> "N"`<br>`["1", "kg/m³", "m/s", "m", "m"] -> "Pa"` | **0 methods** |
| **`Electromagnetism`** (12 dispatchable) | `CoulombForce(double, double, Vector3)`<br>`ElectricFieldPointCharge(double, Vector3)`<br>`ElectricPotentialPointCharge(double, double)`<br>`OhmsLawCurrent(double, double)`<br>`OhmsLawVoltage(double, double)`<br>`OhmsLawResistance(double, double)`<br>`CapacitanceCharge(double, double)`<br>`CapacitorEnergy(double, double)`<br>`RcVoltage(double, double, double, double, double)`<br>`LorentzForce(double, Vector3, Vector3, Vector3)`<br>`MagneticForceOnWire(double, Vector3, Vector3)`<br>`MagneticFieldLongStraightWire(double, double)` | `["C", "C", "m"] -> "N"`<br>`["C", "m"] -> "N/C"`<br>`["C", "m"] -> "V"`<br>`["V", "Ω"] -> "A"`<br>`["A", "Ω"] -> "V"`<br>`["V", "A"] -> "Ω"`<br>`["F", "V"] -> "C"`<br>`["F", "V"] -> "J"`<br>`["V", "V", "s", "Ω", "F"] -> "V"`<br>`["C", "N/C", "m/s", "T"] -> "N"`<br>`["A", "m", "T"] -> "N"`<br>`["A", "m"] -> "T"` | **4 methods (`E-SIG`):**<br>`EquivalentSeriesResistance`, `EquivalentParallelResistance`, `EquivalentParallelCapacitance`, `EquivalentSeriesCapacitance` (`params double[]`). |
| **`CondensedMatter`** (10 dispatchable) | `UnitCellMassDensity(double, double)`<br>`CubicInterplanarSpacing(double, int, int, int)`<br>`BraggAngle(double, double, int)`<br>`ReciprocalLatticeVector(Vector3, Vector3, Vector3)`<br>`ReciprocalLatticeVector(Vector3, Vector3, Vector3, int, int, int)`<br>`TightBindingDispersion(double, double, double, double)`<br>`TightBindingGroupVelocity(double, double, double)`<br>`MonatomicChainPhononFrequency(double, double, double, double)`<br>`FermiWaveNumber(double)`<br>`FreeElectronFermiEnergy(double, double)` | `["kg", "m³"] -> "kg/m³"`<br>`{ [0] = "m" } -> "m"`<br>`{ [0] = "m", [1] = "m" } -> "rad"`<br>`["m", "m", "m"] -> "1/m"`<br>`{ [0] = "m", [1] = "m", [2] = "m" } -> "1/m"`<br>`["J", "J", "m", "1/m"] -> "J"`<br>`["J", "m", "1/m"] -> "m/s"`<br>`["kg", "N/m", "m", "1/m"] -> "rad/s"`<br>`["1/m³"] -> "1/m"`<br>`["1/m³", "kg"] -> "J"` | **2 methods (`E-TYPE`):**<br>- `ReciprocalLatticeVectors`: returns `ValueTuple` of 3 `Vector3`.<br>- `HamiltonianEigensystem`: takes `double[,]`, returns `SymmetricEigenResult`. |
| **`CurvedSpacetime`** (4 dispatchable) | `SchwarzschildRadius(double)`<br>`SchwarzschildMassLength(double)`<br>`SchwarzschildGravitationalRedshift(double, double, double)`<br>`SchwarzschildKretschmannScalar(double, double)` | `["kg"] -> "m"`<br>`["kg"] -> "m"`<br>`["m", "m", "m"] -> null`<br>`["m", "m"] -> "1/m⁴"` | **5 methods:**<br>- `MinkowskiMetric`, `SchwarzschildMetric`: **`E-TYPE`** (`double[,]`).<br>- `MetricInterval`, `ChristoffelSymbols`, `IntegrateGeodesic`: **`E-DELEGATE`** (`SpacetimeMetric`) & **`E-TYPE`** (`double[,,]`, `GeodesicSolution`). |
| **`ManyBodyInteractions`** (0 dispatchable) | None | N/A | **13 methods (`E-TYPE` / `E-DELEGATE` / `E-SIG`):**<br>`GravitationalPairPotential`, `CoulombPairPotential`, `LennardJonesPairPotential`, `PairPotentialEnergy` (2), `PairForce` (2), `TotalPotentialEnergy`, `Accelerations`, `GravitationalAccelerations`, `CoulombAccelerations`, `VelocityVerletStep`, `Simulate`. |
| **`ExtremeScalars`** (0 dispatchable) | None | N/A | **18 methods (`E-TYPE` / `E-DELEGATE` / `E-SIG`):**<br>`FromDouble`, `FromLogMagnitude`, `LogMagnitude`, `ToDouble`, `TryToDouble`, `Add`, `Subtract`, `Multiply`, `Divide`, `Power`, `Negate`, `Abs`, `QuadraticPotential*`, `QuarticPotential*`, `DoubleWellPotential*`, `CanonicalEnergyDensityAndPressure`, `ScalarFieldAcceleration`, `Evolve`. |

---

## 4. Required Test Coverage Matrix per Library

Verified in `OPEN-LRD.SmokeTests/MathFunctionDispatcherTests.cs` (`TestSkillLibraryCoverageMatrix` and companion tests):

1. **End-to-End Dispatch, Provenance, Trust Promotion, and Format-v3 Serialization Round-Trip (All 19 Dispatchable Libraries):**
   - `Math_1` (`AddInt`, `AddDouble`, `CalculateGCD(BigInteger, BigInteger)`)
   - `Math_2` (`CalculateHypotenuse`, `CalculateCircleArea`, `CalculateRectangleArea`, `CalculateCrossProduct`)
   - `Math_3` (`Evaluate(string, string, double)`)
   - `Algebra` (`MultiplyComplex`)
   - `ProbabilityAndStatistics` (`BayesTheorem`)
   - `DiscreteMath` (`Catalan`)
   - `InformationTheoryAndMLMath` (`Entropy`)
   - `FinancialMath` (`PresentValue`)
   - `BasicPhysics` (`Force`, `PositionAtTime`, `EscapeVelocity`)
   - `Thermodynamics` (`HeatTransfer`, `CarnotEfficiency`)
   - `FluidMechanics` (`MassFlowRate`, `ReynoldsNumber`)
   - `Electromagnetism` (`LorentzForce`)
   - `CondensedMatter` (`UnitCellMassDensity`, `BraggAngle`, `MonatomicChainPhononFrequency`, `FreeElectronFermiEnergy`)
   - `CurvedSpacetime` (`SchwarzschildRadius`, `SchwarzschildKretschmannScalar`)
   - `NumericalMethods` (`LagrangeInterpolate`)
   - `Geometry3DAndGraphics` (`IsPointInTriangle`)
   - `TransformsAndSignals` (`Convolve`)
   - `TimeAndDate` (`ToUnixTimeSeconds`)
   - `UnitsAndMeasurement` (`RoundToSignificantFigures`)
2. **SI Unit Metadata Enforcement:**
   - Exact SI input/output unit validation and result unit preservation.
   - Missing unit rejection (`ArgumentException`).
   - Compatible non-SI unit rejection (`UnitConversionRequiredException`, e.g., `cm -> m`, `g -> kg`, `ft² -> m²`).
   - Incompatible unit dimension rejection (`ArgumentException`, e.g., `s -> m`).
3. **Cross-Library Collision & Overload Disambiguation:**
   - Unqualified `"Simplify"` with `(string) -> string` throws `InvalidOperationException` naming `Algebra.Simplify` and `Math_3.Simplify`.
   - Unqualified `"IsPrime"` disambiguates `int` (`Math_1.IsPrime`) vs `long` (`DiscreteMath.IsPrime`).
4. **Deterministic Discovery Rejection Diagnostics:**
   - Excluded methods across `Math_4`, `LinearAlgebraAdvanced`, `Optimization`, `ManyBodyInteractions`, `ExtremeScalars`, RNG methods (`NormalSample`, `IsProbablePrime`), `params` methods (`EquivalentSeriesResistance`), and matrix/delegate methods (`HamiltonianEigensystem`, `IntegrateGeodesic`, `ProjectileTrajectory`) are present in `MathFunctionDispatcher.GetDiscoveryDiagnostics()` and excluded from callable dispatch.

## 3.4 Math Comments Integration

**Date:** 2026-11-22

The XML summary comment headers in all but 4 `Skills/*.cs` files have been updated with consolidated descriptions. 
This integration adds refined function summary descriptions across all Math library files.

### Coverage Statistics:
- **23 out of 27** Skill library files updated with new XML comments
- **851 methods** received updated XML summary descriptions

### Updated Libraries:
- `Math_1.cs` - 173 methods: arithmetic, fraction, and number theory operations
- `Math_2.cs` - 125 methods: inverse trig, hyperbolic, triangle, and circle methods
- `Math_3.cs` - 88 methods: parse, simplify, differentiate, integrate, Taylor series
- `Math_4.cs` - 48 methods: partial derivatives, gradients, Laplacian, Hessian
- `Algebra.cs` - 15 methods: polynomials, radicals, complex numbers
- `BasicPhysics.cs` - 12 methods: force, mass, momentum, energy, projectile motion
- `CondensedMatter.cs` - 10 methods: crystal structure, phonon, Fermi level
- `CurvedSpacetime.cs` - 9 methods: Minkowski/schwarzschild metrics, geodesics
- `DiscreteMath.cs` - 21 methods: modular arithmetic, primality, factorization
- `Electromagnetism.cs` - 17 methods: Coulomb's law, electric field, Ohm's law
- `MathFunctionDispatcher.cs` - 137 methods: method dispatching and utilities
- **ExtremeScalars.cs** - 17 methods: ScaledScalar operations, potentials, field acceleration, evolution
- **FinancialMath.cs** - 22 methods: compound interest, annuities, bond metrics, depreciation methods
- **FluidMechanics.cs** - 9 methods: hydrostatic pressure, buoyant force, mass flow rate, Bernoulli equation
- **Geometry3DAndGraphics.cs** - 21 methods: convex hull, polygon intersection, triangle geometry, transforms
- **InformationTheoryAndMLMath.cs** - 27 methods: entropy, divergence, cross-entropy, activation functions, PCA, KMeans
- **LinearAlgebraAdvanced.cs** - 8 methods: LU decomposition, QR, Cholesky, SVD, matrix norm, condition number, matrix-vector multiply
- **ManyBodyInteractions.cs** - 9 methods: pair potentials, forces, accelerations, Verlet integration, simulation
- **NumericalMethods.cs** - 11 methods: interpolation, root-finding, splines, numerical integration
- **Optimization.cs** - 4 methods: Golden section search, Brent minimization, gradient descent, BFGS
- **ProbabilityAndStatistics.cs** - 40 methods: special functions, distributions, confidence intervals, regression
- **TimeAndDate.cs** - 9 methods: leap year, date calculations, Unix time, timezone conversion
- **TransformsAndSignals.cs** - 19 methods: Fourier transforms, convolution, filtering, window functions


### Description Format:
Each method's `/// <summary>` comment follows the pattern:
```
/// <summary>
/// [Description]
/// </summary>
```

### Example Descriptions:
- `AddInt(int a, int b)`: Computes the sum of two 32-bit signed integers.
- `AddDouble(double a, double b)`: Computes the sum of two double-precision floating-point values.
- `SubtractInt(int a, int b)`: Computes the difference between two integers (a - b).
- `MultiplyInt(int a, int b)`: Computes the product of two integers.
- `DivideInt(int a, int b)`: Divides the first integer by the second integer (a / b).
- `AsinDouble(double value)`: Computes the inverse sine (arcsine) in radians for a double-precision input in [-1, 1].
- `Force(double mass, double acceleration)`: Computes scalar force (F = m * a) from mass and scalar acceleration.
- `CalculateGCD(int a, int b)`: Computes the greatest common divisor of two integers.
- `FromDouble(double value)`: Constructs a ScaledScalar representation from a standard double value.
- `PresentValue(double futureValue, double rate, double periods)`: Computes the present value of future value at rate over periods.
- `ConvexHull(IReadOnlyList<Vector2> points)`: Computes the 2D convex hull enclosing points.
- `Dft(IReadOnlyList<double> samples)`: Computes the Discrete Fourier Transform of samples.
- `Erf(double value)`: Evaluates the error function at value.


### Remaining Files:
4 of 27 files remaining.
- **GraphAlgorithms.cs**
- **SymbolicCalculus.cs**
- **Thermodynamics.cs**
- **UnitsAndMeasurement.cs**
---

This integration ensures that all dispatchable methods have consolidated, detailed documentation, improving maintainability and documentation consistency across the Skill libraries.

