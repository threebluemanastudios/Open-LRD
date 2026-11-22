# OPEN-LRD

OPEN-LRD is a .NET 10 relational knowledge graph and
computational engine built with C# and WPF. It stores
lexical, structural, and functional knowledge; links
trusted data with provenance; persists graph state in
partitioned binary checkpoints; and includes mathematics,
physics, statistics, and graph algorithms.

## Contents

1. [Build](#1-build)
2. [Architecture](#2-architecture)
3. [Core lifecycle](#3-core-lifecycle)
4. [Foundations](#4-foundations)
5. [Node graph](#5-node-graph)
6. [Persistence](#6-persistence)
7. [Tools and UI](#7-tools-and-ui)
8. [Skills library](#8-skills-library)
9. [Core API](#9-core-api)
10. [Smoke tests](#10-smoke-tests)

---

## 1. Build

- Runtime: .NET 10.
- WPF application target: `net10.0-windows`.
- Smoke test target: `net10.0`.
- External packages: none.
- Nullable analysis and warnings-as-errors are enabled.

Build the solution:

```bash
dotnet build OPEN-LRD.slnx
```

On non-Windows hosts, enable Windows targeting:

```bash
dotnet build OPEN-LRD.slnx \
  -p:EnableWindowsTargeting=true
```

Run the cross-platform smoke tests:

```bash
dotnet run --project \
  OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj
```

---

## 2. Architecture

The solution contains two projects:

- `OPEN-LRD` is the WPF application and engine.
- `OPEN-LRD.SmokeTests` is a dependency-free
  console verification suite.

The application project contains:

- `LRD_Core.cs`: trusted and untrusted registries,
  graph indexes, lifecycle, and persistence APIs.
- `Foundations/`: lexical boxes, functions, nodes,
  conversations, expressions, and value types.
- `Persistence/`: binary serializers and partitioned
  checkpoint storage.
- `Skills/`: computational, mathematical, and
  physical function libraries.
- `Tools/`: lossless character-grid file parsing and
  writing.
- `Reasoning/`, `IngestionPipeline/`, and
  `ExGenPipeline/`: currently empty expansion scaffolds.

`GraphSynchronization` protects core state and mutable
foundation objects with a shared synchronization lock.

---

## 3. Core lifecycle

Access the singleton through `LRD_Core.Instance`.
Registry and query operations require initialization.

### Initialization and reset

- `InitializeCore()` creates an empty ready core and is
  idempotent.
- Opening or streaming a validated checkpoint also
  initializes the core.
- `ResetCore()` clears registries, graph indexes,
  presentations, caches, and disk handles.

### Trusted and untrusted data

- Symbols, words, sentences, functions, and nodes may be
  staged as untrusted data.
- Incomplete or conflicting identities cannot be
  promoted.
- Sentence subgraphs can be prevalidated and promoted
  atomically with their untrusted words and symbols.
- Canonical resolution can rebind duplicate staged
  lexical items to an existing trusted identity.
- Trusted foundation identities and memberships are
  append-only. Reset clears trusted state.

### Storage modes

- **In-memory mutable:** the default initialized mode.
  Trusted additions and link mutations are enabled.
- **Disk-paged read-only:** records are loaded on demand
  through bounded per-partition LRU caches. Indexes and
  graph links are built when the store is opened.
  Call `MaterializeDiskStore()` before mutation.
- **Legacy read-only:** format-version 1 stores cannot
  be changed until `UpgradeLegacyDiskStore()` upgrades
  them to the current format.

---

## 4. Foundations

Every foundation box uses a non-empty `Guid` identity.
Trusted boxes freeze identity fields.

### Lexical and structural boxes

- **`SymbolBox`:** one UTF-16 code unit, including NUL
  and surrogate values, plus a sense ID. A separate
  assigned-state flag distinguishes an unset value from
  the assigned NUL character.
- **`WordBox`:** ordered symbol IDs, sense and usage IDs,
  and part of speech. Identity includes all these fields.
- **`SentenceBox`:** ordered word IDs and a sense ID.
  Dictionary-based construction sorts positions.
- **`SenseBox`:** sentence membership, leading and
  trailing words, and finite position metrics.
- **`UsageBox`:** ordered, deduplicated sentence IDs.
- **`GroupBox`:** normalized optional name and ordered
  sentence membership.
- **`SourceBox`:** normalized optional name and ordered
  group membership.

Trusted group and source membership changes go through
`LRD_Core` so indexes and structural graph links remain
consistent.

### Function definitions

`FunctionBox` stores a function key, typed variables,
expected result type and unit, and the last result.

- Variable names are unique and ordinal-sensitive.
- Definitions allow at most 256 variables.
- Declared types must be closed runtime types.
- Values must match their declared type and nullability.
- Definitions can be edited only while untrusted.
- A trusted definition requires a valid dispatched
  result with matching type and unit metadata.
- Dispatch uses an immutable definition snapshot and
  revision check. Failed calls preserve the last
  successful result; stale results cannot be promoted.
- Reusing a staged function ID for a different box is
  rejected.

### Conversation containers

- `ConversationText` maps `UInt128` IDs to strings.
- `ConversationSymbolic` maps `UInt128` IDs to symbol
  ID sequences.
- Range additions snapshot and validate all input
  before mutation.
- Sequential ID allocation is checked for overflow.
- Symbol sequences reject `Guid.Empty` and are copied
  defensively.

### Mathematical and symbolic primitives

- **`Fraction`:** immutable, reduced `BigInteger`
  rational with arithmetic, comparison, parsing, and
  invariant formatting.
- **`Vector2` and `Vector3`:** immutable finite vectors
  with arithmetic, dot/cross products, normalization,
  angles, distances, interpolation, and formatting.
- **Expression AST:** immutable nodes for constants,
  variables, unary and binary operations, calls, and
  conditional expressions.
- **Expression parser:** bounded input and nesting;
  arithmetic, comparisons, boolean operators,
  conditionals, factorial, implicit multiplication,
  scientific notation, function calls, constants, and
  diagnostic locations.
- Built-in expression calls include trigonometry,
  logarithms, special functions, statistics, activations,
  rounding, unit-free constants, and registered scalar
  skill functions.
- Indexed `sum` and `prod` expressions are expanded with
  a bounded iteration count. `let` bindings and common
  mathematical and physical constants are supported.

---

## 5. Node graph

Each `Node` wraps a foundation target and has its own
identity. A null label identifies the canonical node for
that target; additional labeled nodes may coexist.

### Structural and semantic links

Foundation registration automatically wires canonical
nodes with structural links:

- `HasSense`, `HasUsage`, and `ContainsSymbol`.
- `ContainsWord`, `LeadingWord`, and `TrailingWord`.
- `ContainsSentence` and `ContainsGroup`.

Structural links are derived from foundation membership
and cannot be manually changed. Semantic relations include
`Synonym`, `Antonym`, `IsA`, `PartOf`, `Causes`, `Implies`,
`EvaluatesWithFunction`, and `Custom`. Endpoint kinds
are validated for typed relations.

### Provenance and confidence

Each `NodeLink` has a confidence weight in `[0, 1]` and
immutable provenance:

- assertion ID and origin;
- asserted time and optional author;
- optional trusted source and evidence target IDs;
- optional confidence and validity window.

Synonym and antonym assertions maintain a pair of
directed links with one shared assertion ID. Link changes
keep outgoing, incoming, key, and graph indexes aligned.

`AddReasoningLink` creates deterministic assertion IDs.
`UpdateReasoningLinkWeight` only changes reasoning-origin
assertions. Validity windows are inclusive through
`NodeLinkProvenance.IsActiveAt(...)`.

Link queries and graph snapshots can filter by relation,
origin, author, minimum confidence, and active time.
`GetTrustedLinksByProvenanceTarget` returns distinct
assertions for a trusted source or evidence target.
`FindTrustedNodes` filters by target kind, label,
canonical status, net trust weight, and incident links.

### Trust and presentation

Nodes track verified and invalid reference counts with
overflow checks. The core derives node class, weight,
trust rating, color, label, and size from target data,
references, and graph evidence.

Presentation positions use deterministic 3D layouts with
separate trusted and untrusted regions. Layout biases can
be configured. Presentation IDs, positions, and layout
settings are persisted with checkpoints.

### Indexes and graph algorithms

The core maintains ordered O(1) reverse indexes for
senses, usages, symbols, words, groups, and sources.
Helpers include lexical text reconstruction and node
summaries.

`BuildSkillsGraphSnapshot(...)` returns an isolated graph
for algorithms including traversal, shortest paths,
connected components, topological sorting, minimum
spanning trees, maximum flow, and PageRank.

---

## 6. Persistence

`PartitionedBinaryStore` and
`FoundationBinarySerializer` persist trusted state in
versioned binary records.

### Checkpoint contents

The store has ten trusted data partitions:

- symbols, words, sentences, senses, usages;
- groups, sources, functions, nodes, and links.

Two additional partitions store node presentations and
layout settings. Current checkpoints contain a manifest
and data/index pairs for these partitions.

Each data record stores a GUID key, payload length,
payload, and CRC32 checksum. Fixed-size index entries
store keys, offsets, and payload lengths. The manifest
contains partition counts and SHA-256 hashes.

Read and write limits include:

- 5 million records per partition;
- 64 MiB maximum record payload;
- 1 MiB maximum strict UTF-8 string;
- 1 million maximum collection items.

Writes stage checkpoint files in a sibling directory,
validate the output, and then replace the destination.

### Streaming and merge

- `StreamTrustedDictionariesToDisk(...)` writes a
  checkpoint.
- `OpenDiskStore(...)` validates and pages records on
  demand with bounded LRU caches.
- `MaterializeDiskStore()` loads paged records into
  mutable in-memory dictionaries.
- `FlushTrustedDiskStore()`, `CompactTrustedDiskStore()`,
  and `ValidateDiskStore()` manage attached stores.
- Imports can replace current state or merge into it.
  Merge validates identities, reconciles container
  memberships, references, nodes, links, and layouts
  before installing state.
- Format version 1 loads read-only and can be upgraded
  atomically to version 2.

---

## 7. Tools and UI

### Character-grid file tool

`Tool_1` reads files into a rectangular character grid
and writes them back through `FileGridDocument`.

- Supported encodings: UTF-8, UTF-16 LE/BE, UTF-32
  LE/BE, and Latin-1.
- BOMs, strict UTF validation, binary fallback, and
  explicit encoding overrides are supported.
- Text rows preserve line-ending characters and logical
  row lengths. Binary fallback preserves byte values.
- Writers validate before replacing the destination and
  use a temporary sibling file for safe replacement.

### WPF launcher

`MainWindow` provides the top-level launcher. The
**Launch Core** action initializes `LRD_Core` and updates
the running status.

---

## 8. Skills library

Skills are implemented in native C# under `OPEN_LRD.Skills`.
Methods validate domains, bounds, and finite values.

### Function dispatcher

`MathFunctionDispatcher` routes `FunctionBox` definitions
and direct calls to registered functions.

- Supports canonical keys and unqualified method names.
- Matches parameter types, argument counts, result
  types, and unit metadata.
- Infers a function only when a signature is unique;
  ambiguity errors list matching keys.
- `ResolveFunctionKey(...)` returns the stable registry
  key for an alias or inferred signature.
- Unit mismatches identify cases requiring conversion.

### Core mathematics

- **`Math_1`:** numeric and `BigInteger` arithmetic,
  fractions, number theory, combinatorics, statistics,
  sequences, ratios, roots, powers, and logarithms.
  Includes `Gcd`, `Lcm`, `Factorial`, permutations,
  combinations, quartiles, variance, correlation,
  Fibonacci, and progression helpers.
- **`Math_2`:** trigonometry, hyperbolic functions,
  geometry, triangle solving, mensuration, and vector
  calculations. Includes inverse functions, line and
  plane geometry, Heron's formula, and 2D/3D shapes.
- **`Math_3`:** expression parsing, simplification,
  differentiation, integration, evaluation, series,
  limits, quadrature, root finding, and extrema.
  Includes symbolic Taylor series, Riemann sums,
  Newton-Raphson, and bisection.
- **`Math_4`:** multivariable calculus, numerical vector
  calculus, line/surface/multiple integrals, and ODE
  solvers for scalar and system equations. Includes
  gradients, divergence, curl, Jacobians, Euler, and RK4.
- **`Algebra`:** polynomial, matrix, complex, quaternion,
  inequality, and symbolic manipulation operations.
  Includes polynomial division/factorization, root
  solvers, row reduction, linear systems, and rational
  and trigonometric simplification.
- **`SymbolicCalculus`:** simplification rules,
  differentiation, integration, coefficient extraction,
  and expression evaluation.

### Advanced mathematics and algorithms

- **`LinearAlgebraAdvanced`:** LU, QR, Cholesky,
  eigenvalue and singular-value decompositions,
  pseudoinverse, null/column spaces, least squares,
  matrix norms, and condition numbers.
- **`NumericalMethods`:** interpolation, curve fitting,
  root solvers, quadrature, iterative linear solvers,
  adaptive ODE solvers, and convergence acceleration.
  Includes cubic splines, PCHIP, Brent, Romberg,
  Gauss-Legendre, and Dormand-Prince methods.
- **`Optimization`:** scalar and multivariable
  minimizers, constrained optimization, linear
  programming, and assignment. Includes Nelder-Mead,
  BFGS, simplex, and Hungarian assignment.
- **`ProbabilityAndStatistics`:** special functions,
  distributions, quantiles, intervals, tests, Bayesian
  updates, regression, and seeded sampling. Includes
  Normal, t, Chi-Square, F, Gamma, Beta, Binomial, and
  Poisson distributions.
- **`DiscreteMath`:** modular arithmetic, congruences,
  sieves, primality, factorization, combinatorics,
  number sequences, and base conversion.
- **`InformationTheoryAndMLMath`:** entropy, divergence,
  activations, losses, classification metrics, distance
  measures, standardization, PCA, and k-means. Includes
  ROC/AUC, confusion matrices, Softmax, and cross-entropy.
- **`Geometry3DAndGraphics`:** hulls, polygons,
  triangulation, barycentric coordinates, rays,
  intersections, curves, and 3D transforms.
- **`TransformsAndSignals`:** DFT, FFT, convolution,
  correlation, windows, FIR filters, and Fourier series.
- **`FinancialMath`:** interest, present/future values,
  annuities, amortization, investment, depreciation,
  bonds, yield, and duration.
- **`TimeAndDate`:** calendar, ISO week, business-day,
  age, timestamp, time-zone, and ISO-8601 utilities.
- **`GraphAlgorithms`:** traversal, shortest paths,
  topological sort, components, spanning trees, flow,
  and PageRank.

### Units, measurement, and physics

- **`UnitsAndMeasurement`:** SI dimensions, SI and US
  customary units, prefixes, conversions, measurements,
  uncertainty propagation, and significant figures.
  Supports affine temperature and scale conversions.
- **`BasicPhysics`:** physical constants, mechanics,
  gravitation, and projectile trajectories. Includes
  force, momentum, energy, orbital speed, and drag.
- **`Thermodynamics`:** gas laws, heat transfer,
  expansion, entropy, efficiency, and radiation.
  Includes adiabatic state relations and Carnot limits.
- **`FluidMechanics`:** hydrostatics, buoyancy, flow,
  Bernoulli relations, drag, and pressure loss.
  Includes Reynolds and Poiseuille calculations.
- **`Electromagnetism`:** electric and magnetic fields,
  circuits, transients, induction, and wave power.
  Includes Coulomb, Lorentz, Biot-Savart, RC, and RL
  calculations.
- **`CondensedMatter`:** crystal lattices, diffraction,
  tight-binding models, phonons, and Fermi quantities.
- **`ManyBodyInteractions`:** pair potentials,
  N-body forces and energies, and velocity-Verlet
  simulation.
- **`CurvedSpacetime`:** metrics, Schwarzschild values,
  curvature tensors, and geodesic integration.
- **`ExtremeScalars`:** extended-exponent scalar
  arithmetic and scalar-field simulation.

---

## 9. Core API

```csharp
LRD_Core core = LRD_Core.Instance;
core.InitializeCore();

core.AddTrustedSense(sense);
core.AddTrustedSymbol(symbol);
core.AddTrustedWord(word);
core.AddTrustedSentence(sentence);

core.AddReasoningLink(
    sourceNodeId,
    targetNodeId,
    NodeRelationType.Implies,
    confidence: 0.9,
    evidenceTargetId: evidenceId);

Graph<Guid> snapshot = core.BuildSkillsGraphSnapshot(
    NodeRelationType.Implies,
    NodeLinkOrigin.Reasoning,
    minimumConfidence: 0.7);
```

Use `GetTrustedNodeByTarget(...)` to resolve canonical
nodes and `FindTrustedNodes(...)` for filtered queries.
Use `OpenDiskStore(...)` and
`MaterializeDiskStore()` to switch between paged and
mutable storage.

### Core invariants

1. IDs are non-empty and identities complete before
   promotion.
2. Trusted foundation identities and memberships are
   append-only.
3. Structural links match foundation membership and
   cannot be edited through semantic-link APIs.
4. Trusted node labels and function definitions are
   immutable.
5. Link mutations preserve adjacency and graph
   consistency, including symmetric assertions.
6. Checkpoint imports validate staged state before
   replacing or merging current state.

---

## 10. Smoke tests

`OPEN-LRD.SmokeTests` is a console runner with no external
test framework. It covers:

- core identity, lifecycle, concurrency, and graph
  invariants;
- node layout, provenance, and presentation behavior;
- checkpoint paging, integrity, merge, and migration;
- foundation validation, conversations, functions, and
  expression parsing;
- lossless file parsing and writing;
- mathematics, numerical methods, statistics,
  optimization, and graph algorithms;
- units, measurement, physics, and extreme scalars.

Run the suite with:

```bash
dotnet run --project \
  OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj
```
