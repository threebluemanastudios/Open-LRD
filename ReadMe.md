# OPEN-LRD (Open-Source Large Relational Database Model)

OPEN-LRD is a dependency-free .NET 10 relational knowledge graph and computational engine written in native C# and WPF. 


It manages lexical, structural, and functional knowledge foundations, connects them into a provenance-backed directed node graph, 
persists and pages trusted state from partitioned binary disk checkpoints, and provides an extensive library of deterministic 
mathematical, physical, statistical, and algorithmic skills.

---

## Table of Contents

1. [System Requirements & Build](#1-system-requirements--build)
2. [Architecture Overview](#2-architecture-overview)
3. [Core Lifecycle & Storage Modes (`LRD_Core`)](#3-core-lifecycle--storage-modes-lrd_core)
4. [Foundation Data Models (`OPEN_LRD.Foundations`)](#4-foundation-data-models-open_lrdfoundations)
   - [Lexical & Structural Boxes](#lexical--structural-boxes)
   - [Function Definitions (`FunctionBox`)](#function-definitions-functionbox)
   - [Conversation Containers (`ConversationText` & `ConversationSymbolic`)](#conversation-containers-conversationtext--conversationsymbolic)
   - [Mathematical & Symbolic Primitives](#mathematical--symbolic-primitives)
5. [Node Graph, Links, & Provenance](#5-node-graph-links--provenance)
   - [Node Identity & Canonical Nodes](#node-identity--canonical-nodes)
   - [Structural Auto-Wiring & Semantic Relations](#structural-auto-wiring--semantic-relations)
   - [Link Provenance, Confidence, & Symmetry](#link-provenance-confidence--symmetry)
   - [O(1) Reverse & Containment Indexes](#o1-reverse--containment-indexes)
6. [Partitioned Binary Disk Persistence & Streaming (`OPEN_LRD.Persistence`)](#6-partitioned-binary-disk-persistence--streaming-open_lrdpersistence)
   - [Store Layout & Integrity Verification](#store-layout--integrity-verification)
   - [Checkpoints, On-Demand LRU Paging, & Materialization](#checkpoints-on-demand-lru-paging--materialization)
   - [Merge Reconciliation & Legacy Format Migration](#merge-reconciliation--legacy-format-migration)
7. [File Character Grid Tooling (`OPEN_LRD.Tools`)](#7-file-character-grid-tooling-open_lrdtools)
8. [Computational & Domain Skills Library (`OPEN_LRD.Skills`)](#8-computational--domain-skills-library-open_lrdskills)
   - [Math Function Dispatcher (`MathFunctionDispatcher`)](#math-function-dispatcher-mathfunctiondispatcher)
   - [Core Mathematics (`Math_1` – `Math_4`, `Algebra`, `SymbolicCalculus`)](#core-mathematics-math_1--math_4-algebra-symboliccalculus)
   - [Advanced Mathematics & Algorithms](#advanced-mathematics--algorithms)
   - [Units, Measurement, & Physics Domains](#units-measurement--physics-domains)
9. [API & Invariant Reference](#9-api--invariant-reference)
10. [Smoke Test Suite (`OPEN-LRD.SmokeTests`)](#10-smoke-test-suite-open-lrdsmoketests)

---

## 1. System Requirements & Build

- **Runtime & SDK:** .NET 10.0 (`net10.0-windows` for the WPF application project; `net10.0` for the console smoke-test suite)
- **External Dependencies:** None (strictly standard .NET class libraries and WPF)
- **Compiler Policy:** Nullable reference types enabled (`<Nullable>enable</Nullable>`), XML documentation generation enabled, and warnings treated 
	as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).

### Building the Solution

```bash
dotnet build OPEN-LRD.slnx
```

### Running the Smoke Tests

```bash
dotnet run --project OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj
```

---

## 2. Architecture Overview

The repository is organized into two projects inside `OPEN-LRD.slnx`:

- **`OPEN-LRD`**: The main WPF application (`MainWindow`) and core engine libraries:
  - `LRD_Core.cs`: Thread-safe singleton managing all trusted/untrusted registries, identity/reverse indexes, graph adjacency, and disk-store lifecycle.
  - `Foundations/`: Core entity models (`SymbolBox`, `WordBox`, `SentenceBox`, `SenseBox`, `UsageBox`, `GroupBox`, `SourceBox`, `FunctionBox`, `Node`, 
		`NodeLink`, `ConversationText`, `ConversationSymbolic`, `Fraction`, `Vector2`, `Vector3`, `Expression`, `ExpressionParser`, `GraphSynchronization`).
  - `Persistence/`: Binary serialization (`FoundationBinarySerializer`) and indexed multi-partition disk storage (`PartitionedBinaryStore`).
  - `Skills/`: Domain computation engines and `MathFunctionDispatcher`.
  - `Tools/`: Lossless multi-encoding 2D character grid file parser and writer (`Tool_1`, `FileGridDocument`).
  - `IngestionPipeline/`, `Reasoning/`, `ExGenPipeline/`: Namespace scaffolds reserved for pipeline expansion.
- **`OPEN-LRD.SmokeTests`**: Cross-platform verification suite testing core invariants, persistence, file parsing, and all mathematical/physical skill modules.

All operations across `LRD_Core` and mutable foundation objects are synchronized through `GraphSynchronization`, a centralized lock helper ensuring thread 
safety across concurrent reads and mutations.

---

## 3. Core Lifecycle & Storage Modes (`LRD_Core`)

`LRD_Core` is accessed via `LRD_Core.Instance` (or `GetInstance()`). All registry, query, and mutation entry points enforce strict lifecycle and storage-mode preconditions:

1. **Uninitialized vs. Initialized:**
   - The core starts uninitialized (`initialized == false`). Calling registry getters or mutators before initialization throws `InvalidOperationException`.
   - `InitializeCore()` is idempotent and transitions the singleton into an empty, ready state (`initialized == true`).
   - `ResetCore()` and `ClearAllDictionaries()` clear all registries, reverse indexes, graph adjacency, paged caches, and open disk handles, and reset `initialized` to `false`.
   - Successfully opening or streaming a disk checkpoint via `OpenDiskStore` or `StreamTrustedDictionariesFromDisk` also marks the core initialized after validation succeeds.
   - In the WPF launcher (`MainWindow`), clicking **Launch Core** invokes `InitializeCore()`, updates the status display to `"Core Running"`, and hides the launch button.

2. **Untrusted Staging vs. Trusted Append-Only State:**
   - Symbols, words, sentences, functions, and nodes can be staged in untrusted dictionaries, modified while untrusted, and promoted to trusted status once their identities are complete.
   - Once an object is added or promoted to a trusted registry, its identity is frozen (`FreezeIdentity()` / `MarkTrusted()`).
   - Trusted Foundation boxes and their membership sets are **append-only**. There are no individual removal APIs for trusted boxes or trusted memberships; only `ResetCore()` or 
		`ClearAllDictionaries()` clears trusted Foundation state.

3. **In-Memory Mutable vs. Disk-Paged Read-Only vs. Legacy Read-Only:**
   - **In-Memory Mutable Mode:** Default mode after `InitializeCore()` or `MaterializeDiskStore()`. All trusted append and link mutation APIs are enabled.
   - **Disk-Paged Read-Only Mode (`IsDiskPagedReadOnly == true`):** Entered when opening a store via `OpenDiskStore(directoryPath)` or `StreamTrustedDictionariesFromDisk(..., 
		preloadAllIntoMemory: false)`. In-memory reverse indexes and the node/link graph are populated immediately, while individual Foundation boxes are paged on demand into a bounded LRU cache as read-only instances. Calling mutation APIs throws `InvalidOperationException` until `MaterializeDiskStore()` is called.
   - **Legacy Disk Read-Only Mode (`IsLegacyDiskStoreReadOnly == true`):** Entered when loading a format-version 1 checkpoint. All writes, flushes, merges, and compactions are blocked 
		until `UpgradeLegacyDiskStore()` atomically rewrites the checkpoint to format version 2.

---

## 4. Foundation Data Models (`OPEN_LRD.Foundations`)

Every Foundation box requires a non-empty `Guid` identifier (`Guid.Empty` is rejected with `ArgumentException`).

### Lexical & Structural Boxes

| Type | Target Kind | Identity & Mutability Rules | Key Properties & Methods |
| :--- | :--- | :--- | :--- |
| **`SymbolBox`** | `NodeTargetKind.Symbol` | Wraps a single UTF-16 code unit (`char`, including `'\0'` and surrogates) and a `SenseID`. Complete when `HasSymbolChar` is true and `SenseID` is set. Unique by `SymbolIdentityKey(SymbolChar, SenseId)`. | `GetSymbolID()`, `HasSymbolChar`, `GetSymbolChar()`, `SetSymbolChar(char)`, `GetSenseID()`, `SetSenseID(Guid)`, `GetSymbolType()` (`UnicodeCategory`). |
| **`WordBox`** | `NodeTargetKind.Word` | Represents an ordered sequence of `SymbolBox` IDs, a `SenseID`, a `UsageID`, and a `PartOfSpeech` (`Noun`, `Verb`, `Adjective`, `Adverb`, `Pronoun`, `Preposition`, `Conjunction`, `Interjection`, `Function`, `Value`). Unique by `WordIdentityKey(Symbols, SenseId, UsageId, PartOfSpeech)`. | `GetWordID()`, `GetSenseID()`, `SetSenseID(Guid)`, `GetUsageID()`, `SetUsageID(Guid)`, `GetSymbols()`, `SetSymbols(List<Guid>)`, `GetPartOfSpeech()`, `SetPartOfSpeech(PartOfSpeech)`. |
| **`SentenceBox`** | `NodeTargetKind.Sentence` | Represents an ordered sequence of `WordBox` IDs and a `SenseID`. Dictionary constructors sort entries deterministically by non-negative integer position key. Unique by `SentenceIdentityKey(Words, SenseId)`. | `GetSentenceID()`, `GetSenseID()`, `SetSenseID(Guid)`, `GetWords()`, `SetWords(List<Guid>)`. |
| **`SenseBox`** | `NodeTargetKind.Sense` | Tracks sentences, leading words, trailing words, and finite `float` positional metrics (`AverageWordPosition`, `AverageSentencePosition`, `AverageSpread`). Direct membership additions are restricted once trusted; post-trust additions occur via `LRD_Core`. | `GetSenseID()`, `GetSentences()`, `GetLeadingWords()`, `GetTrailingWords()`, `GetSentenceCount()`, `GetLeadingWordCount()`, `GetTrailingWordCount()`, `ContainsSentence(Guid)`, `ContainsLeadingWord(Guid)`, `ContainsTrailingWord(Guid)`, `UpdateAverageMetrics(float, float, float)`, `ResetAverageMetrics()`. |
| **`UsageBox`** | `NodeTargetKind.Usage` | Tracks ordered, deduplicated sentence IDs demonstrating word usage. Direct `AddSentence` is blocked after trust; automatically updated by `LRD_Core` when trusted sentences are registered. | `GetUsageID()`, `GetSentences()`, `GetSentenceCount()`, `ContainsSentence(Guid)`, `AddSentence(Guid)`. |
| **`GroupBox`** | `NodeTargetKind.Group` | Stores an optional normalized `GroupingName` (trimmed; whitespace-only becomes `null`) and an ordered, deduplicated list of sentence IDs (`List<Guid>`). Post-trust additions require `LRD_Core.AddSentenceToTrustedGroup`. | `GetGroupingID()`, `GetGroupingName()`, `GetSentenceIDs()`, `GetSentences()`, `GetSentenceCount()`, `ContainsSentence(Guid)`, `AddSentence(Guid)`. |
| **`SourceBox`** | `NodeTargetKind.Source` | Stores an optional normalized `SourceName` and an ordered, deduplicated list of `GroupBox` IDs (`List<Guid>`). Post-trust additions require `LRD_Core.AddGroupToTrustedSource`. | `GetSourceID()`, `GetSourceName()`, `SetSourceName(string?)`, `GetGroupingIDs()`, `GetGroupings()`, `GetGroupingCount()`, `ContainsGrouping(Guid)`, `AddGrouping(Guid)`. |

#### Atomic Promotion & Canonical Deduplication
- **Single-Box Promotion:** `ConvertUntrustedSymbolToTrusted`, `ConvertUntrustedWordToTrusted`, `ConvertUntrustedSentenceToTrusted`, and `ConvertUntrustedFunctionToTrusted` validate 
		that all referenced dependencies (senses, usages, symbols, words) are already trusted and that no identical identity key exists in the trusted store.
- **Atomic Subgraph Promotion (`ConvertUntrustedSentenceSubgraphToTrusted`):** Prevalidates an untrusted sentence junto with all of its untrusted words and symbols—including checking 
		for intra-batch identity collisions—before promoting the entire subgraph in a single atomic operation.
- **Canonical Deduplication (`ResolveOrPromoteUntrustedSymbol`, `ResolveOrPromoteUntrustedWord`, `ResolveOrPromoteUntrustedSentenceSubgraph`):** If an untrusted box matches an identity 
		already present in the trusted store, these methods rebind parent references to the existing canonical trusted ID and remove the redundant untrusted box and its untrusted node.

---

### Function Definitions (`FunctionBox`)

`FunctionBox` encapsulates a typed mathematical or domain function call:

- **Identity & Limits:** Each `FunctionBox` has a non-empty `FunctionID`, an `ExpectedResultType`, an optional `FunctionKey`, an optional `ExpectedResultUnitTitle`, up to 
		`MaximumVariableCount` (`256`) unique ordinal-named `FunctionVariable` entries, and a `FunctionResult`.
- **Type & Nullability Validation (`FunctionValueValidation`):**
  - Declared types must be closed, non-void, non-pointer, non-byref runtime `Type` instances.
  - Non-null values must be assignable to the declared type (or its underlying type if `Nullable<T>`).
  - `null` values are permitted only for reference types or `Nullable<T>` value types.
- **Lifecycle:**
  - Staged via `LRD_Core.SetUntrustedFunction(FunctionBox)`, retrieved via `GetUntrustedFunctionBox(Guid)`, and removed via `RemoveUntrustedFunction(Guid)`.
  - Editable (`AddVariable`, `SetVariable`, `RemoveVariable`, `FunctionKey`, `ExpectedResultUnitTitle`) only while untrusted.
  - Before a `FunctionBox` can be trusted (`AddTrustedFunction` or `ConvertUntrustedFunctionToTrusted`), it must carry a non-null `Result` whose type, value, and unit satisfy `MathFunctionDispatcher.ValidateDefinition`.
  - Once trusted, the `FunctionBox` is marked read-only; dispatching a trusted `FunctionBox` through `MathFunctionDispatcher.Dispatch` computes and returns a new `FunctionResult` without mutating the trusted definition.

---

### Conversation Containers (`ConversationText` & `ConversationSymbolic`)

Registered in `LRD_Core` via `AddTextConversation` / `GetTextConversation` / `RemoveTextConversation` and `AddSymbolicConversation` / `GetSymbolicConversation` / `RemoveSymbolicConversation`:

- **`ConversationText`**: Maps `UInt128` sequential or explicit keys to non-null `string` entries.
- **`ConversationSymbolic`**: Maps `UInt128` sequential or explicit keys to defensive copies of `List<Guid>` symbol sequences (rejecting `Guid.Empty`).
- **Atomic Range & Overflow Guarantees:**
  - `AddTextRange` and `AddSymbolicRange` accept either a sequential `List<...>` or an explicit `Dictionary<UInt128, ...>` (which upserts existing keys).
  - Inputs are snapshotted and validated in full before any state change.
  - `UInt128` ID allocation is preflighted with checked arithmetic. When `UInt128.MaxValue` is assigned, `IsIdSpaceExhausted` becomes `true`, and subsequent sequential additions throw 
		`OverflowException` without partial mutation.
  - Both types expose `GetConversationID()`, `GetEntryCount()`, `ContainsId(UInt128)`, `Remove(UInt128)`, `Clear()`, `GetCurrentTextIndex()` / `SetCurrentTextIndex(UInt128)` 
		(or symbolic equivalents), and `GetCurrentTextCount()` / `GetCurrentSymbolicCount()`.

---

### Mathematical & Symbolic Primitives

- **`Fraction` (`Foundations/Fractions.cs`)**:
  - Immutable rational number backed by arbitrary-precision `BigInteger` `Numerator` and `Denominator`.
  - Automatically canonicalized at construction: positive denominator, reduced by `BigInteger.GreatestCommonDivisor`, and zero normalized to `0/1`.
  - Implements `IEquatable<Fraction>`, `IComparable<Fraction>`, arithmetic operators (`+`, `-`, `*`, `/`, unary `-`), comparison operators (`==`, `!=`, `<`, `>`, `<=`, `>=`), `Invert()`, `ToDecimal()`, `ToFloat()`, and invariant-culture `Parse` / `TryParse` / `ToString()` (`"<Numerator>/<Denominator>"`).
- **`Vector2` & `Vector3` (`Foundations/Vectors.cs`)**:
  - Immutable `readonly struct` value types with `double` components.
  - Reject non-finite (`NaN`, `Infinity`) components or scalar operands in constructors and operators (`ArgumentOutOfRangeException`), and throw `ArithmeticException` if an operation 
		overflows to a non-finite result.
  - Provide `Zero`, `UnitX`, `UnitY`, `UnitZ` (3D), `IsFinite`, `+`, `-`, unary `-`, scalar `*` and `/`, `Dot`, `Cross` (3D), overflow-scaled `Length()`, `LengthSquared()`, `Normalize()` 
		(throws `InvalidOperationException` on zero vector), `Distance`, `Angle`, `Lerp`, value equality, and round-trip `"R"` invariant-culture formatting.
- **`Expression` AST & `ExpressionParser` (`Foundations/Expressions.cs`, `Foundations/ExpressionParser.cs`)**:
  - Immutable record hierarchy: `ConstantExpression` (finite `double`), `VariableExpression` (trimmed non-empty identifier), `NegateExpression`, `BinaryExpression` (`Add`, `Subtract`, 
		`Multiply`, `Divide`, `Power`), and `FunctionExpression` (`Sin`, `Cos`, `Tan`, `Cot`, `Sec`, `Csc`, `Asin`, `Acos`, `Atan`, `Sinh`, `Cosh`, `Tanh`, `Asinh`, `Acosh`, `Atanh`, `Exp`,
		`Ln`, `Log10`, `Sqrt`, `Cbrt`, `Abs`, `Sign`).
  - `ExpressionParser.Parse(string)` enforces `MaximumInputLength = 16,384` and `MaximumNestingDepth = 256`, supports standard operator precedence, right-associative exponentiation (`^`), 
		implicit multiplication (e.g., `2x`, `(x+1)(x-1)`), scientific notation, constants (`pi`, `e`), and function aliases (`arcsin`, `arccos`, `arctan`, `log`).

---

## 5. Node Graph, Links, & Provenance

### Node Identity & Canonical Nodes

`Node` (`Foundations/Node.cs`) wraps any of the eight Foundation box types (`NodeTargetKind`: `Symbol`, `Word`, `Sentence`, `Sense`, `Usage`, `Source`, `Group`, `Function`) with its own distinct `NodeID` (`Guid`):

- **Full Node Identity (`NodeIdentityKey`):** Defined by the tuple `(NodeTargetKind TargetKind, Guid TargetId, string? NormalizedLabel)`. Labels are trimmed, with whitespace-only strings normalized to `null`.
- **Canonical vs. Labeled Nodes:**
  - The node with a `null` label (`Label == null`) is the **canonical node** for `(TargetKind, TargetID)`.
  - Additional labeled nodes (`Label != null`) for the same target can coexist in `LRD_Core` with distinct `NodeID`s.
  - Adding or promoting any Foundation box to trusted status automatically creates (or promotes) its canonical null-label trusted `Node`.
  - A `Node` cannot be promoted to trusted (`ConvertUntrustedNodeToTrusted`) unless its underlying target box is already trusted (`HasTrustedTarget`).
  - Once trusted (`IsTrusted == true`), a node's label is frozen (`SetLabel` throws `InvalidOperationException`).
- **Trust Reference Counters:**
  - Each `Node` tracks `VerifiedReferences` (`WeightUp`) and `InvalidReferences` (`WeightDown`) with checked overflow protection.
  - `GetNetTrustWeight()` computes `(long)VerifiedReferences - InvalidReferences` safely clamped to `int` bounds.

### Structural Auto-Wiring & Semantic Relations

Edges between trusted nodes are represented by `NodeLink` and categorized by `NodeRelationType`:

1. **Structural Relations (Auto-Wired & Protected):**
   - Automatically wired between canonical (`Label == null`) trusted nodes whenever Foundation boxes are added or promoted to trusted, or when memberships are appended via `AddSentenceToTrustedGroup` / `AddGroupToTrustedSource`:
     - `HasSense`: `Symbol -> Sense`, `Word -> Sense`, `Sentence -> Sense`
     - `HasUsage`: `Word -> Usage`
     - `ContainsSymbol`: `Word -> Symbol`
     - `ContainsWord`: `Sentence -> Word`
     - `LeadingWord` / `TrailingWord`: `Sentence -> first Word` / `Sentence -> last Word`
     - `ContainsSentence`: `Group -> Sentence`
     - `ContainsGroup`: `Source -> Group`
   - Structural links are stamped with `NodeLinkOrigin.Structural`, unit weight (`1.0`), and `null` label.
   - External calls to `AddTrustedLink`, `UpdateTrustedLinkWeight`, or `RemoveTrustedLink` with a structural `NodeRelationType` are rejected so the graph cannot diverge from Foundation box memberships.

2. **Semantic & Reasoning Relations (Caller-Managed):**
   - Managed via `AddTrustedLink`, `UpdateTrustedLinkWeight`, and `RemoveTrustedLink` with strict endpoint `NodeTargetKind` validation:
     - `Synonym`, `Antonym`, `IsA`: Require distinct `Word -> Word` nodes.
     - `PartOf`: Requires distinct nodes of matching kind (`Word -> Word`, `Sentence -> Sentence`, or `Group -> Group`).
     - `Causes`, `Implies`: Require distinct endpoints where each endpoint is a `Word`, `Sentence`, or `Function` node.
     - `EvaluatesWithFunction`: Requires a non-`Function` source node targeting a `Function` node.
     - `Custom`: Connects any existing trusted source and target nodes (including self-links).

### Link Provenance, Confidence, & Symmetry

- **`NodeLink` & `NodeLinkProvenance`:**
  - `NodeLink.Weight` represents assertion confidence in the closed interval `[0.0, 1.0]`.
  - Every `NodeLink` carries an immutable `NodeLinkProvenance` containing:
    - `AssertionId` (`Guid`, non-empty; deterministically derived via SHA-256 when not explicitly supplied)
    - `Origin` (`NodeLinkOrigin`: `Structural`, `User`, `Import`, `Reasoning`, `LegacyImport`)
    - `AssertedAt` (`DateTimeOffset`)
    - `Author` (optional trimmed string)
    - `SourceTargetId` and `EvidenceTargetId` (optional `Guid`s referencing trusted Foundation boxes)
    - `Confidence` (optional `double` in `[0.0, 1.0]`; when present, must equal `NodeLink.Weight`)
    - `ValidFrom` and `ValidUntil` (optional `DateTimeOffset` validity window where `ValidFrom <= ValidUntil`)
  - Multiple evidence-backed assertions between the same endpoints with distinct `AssertionId`s can coexist.
- **Symmetric Relations & Atomic Link Mutations:**
  - `Synonym` and `Antonym` are symmetric: inserting, re-weighting, or removing an assertion automatically writes, updates, or removes both directed edges (`A -> B` and `B -> A`) under a shared `AssertionId`.
  - All link mutations are transactional across `TrustedOutgoingLinks`, `TrustedIncomingLinks`, `TrustedLinkKeys`, and `TrustedSkillsGraph`. If any step fails, the mutation rolls back completely.
- **`Skills.Graph<Guid>` Integration:**
  - `LRD_Core` maintains a live directed `Graph<Guid>` (`TrustedSkillsGraph`) updated incrementally on node and link insertion, and provides `BuildSkillsGraphSnapshot(NodeRelationType? relationFilter = null)` 
		for isolated execution of graph algorithms (`BreadthFirstTraversal`, `DepthFirstTraversal`, `Dijkstra`, `BellmanFord`, `FloydWarshall`, `PageRank`, etc.).

### O(1) Reverse & Containment Indexes

`LRD_Core` maintains synchronized `OrderedGuidIndexList` reverse indexes (pairing an insertion-ordered `List<Guid>` with an O(1) `HashSet<Guid>`) for all trusted boxes:

- `GetTrustedSymbolID(char)` / `GetTrustedSymbolIDs(char)`
- `GetTrustedSymbolsBySenseID(Guid)`
- `GetTrustedWordsBySenseID(Guid)`
- `GetTrustedSentencesBySenseID(Guid)`
- `GetTrustedWordsByUsageID(Guid)`
- `GetTrustedWordsContainingSymbol(Guid)`
- `GetTrustedSentencesContainingWord(Guid)`
- `GetTrustedGroupsContainingSentence(Guid)`
- `GetTrustedSourcesContainingGroup(Guid)`
- Text reconstruction helpers: `GetTextFromWordID(Guid)` and `GetTextFromSentenceID(Guid)`
- Node summary helper: `GetTrustedNodeSummary(Node)` / `GetTrustedNodeSummary(NodeTargetKind, Guid, string?)` (works seamlessly across both in-memory and disk-paged modes).

---

## 6. Partitioned Binary Disk Persistence & Streaming (`OPEN_LRD.Persistence`)

`PartitionedBinaryStore` and `FoundationBinarySerializer` provide deterministic, checksum-verified binary storage and on-demand paging for all ten trusted partitions.

### Store Layout & Integrity Verification

A checkpoint directory contains 21 files:

- **`manifest.lrdmeta`**: Store header (`Magic = 0x4D524C4F`, `CurrentFormatVersion = 2`), partition count, and per-partition record counts, byte lengths, and 32-byte SHA-256 hashes for both `.lrdpart` and `.lrdidx` files.
- **10 Partition Pairs (`<name>.lrdpart` and `<name>.lrdidx`)**:
  1. `symbols` (`DiskPartition.Symbols`)
  2. `words` (`DiskPartition.Words`)
  3. `sentences` (`DiskPartition.Sentences`)
  4. `senses` (`DiskPartition.Senses`)
  5. `usages` (`DiskPartition.Usages`)
  6. `groups` (`DiskPartition.Groups`)
  7. `sources` (`DiskPartition.Sources`)
  8. `functions` (`DiskPartition.Functions`)
  9. `nodes` (`DiskPartition.Nodes`)
  10. `links` (`DiskPartition.Links`)

- **Binary Format & Limits:**
  - `.lrdpart` data files start with a 16-byte header (`Magic = 0x44524C4F`, format version, partition ID, reserved bytes) followed by sequential records: `[16-byte Guid Key][4-byte PayloadLength][Payload][4-byte CRC32]`.
  - `.lrdidx` index files start with a 16-byte header (`Magic = 0x49524C4F`, format version, partition ID, record count) followed by fixed 28-byte entries: `[16-byte Guid Key][8-byte Offset][4-byte PayloadLength]`.
  - Safety ceilings enforced on read and write: `MaximumRecordsPerPartition = 5,000,000`, `MaximumRecordPayloadLength = 64 MiB`, `MaximumStringByteLength = 1 MiB` (strict UTF-8), and `MaximumCollectionCount = 1,000,000`.
  - Supported `FunctionBox` scalar types for binary persistence: `object`, `string`, `bool`, `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `char`, `Guid`, `DateTime`,
		`DateTimeOffset`, and `TimeSpan`.
  - Writes stage all files in a sibling `.staging-<guid>` directory and atomically swap into the target path only after all partition SHA-256 digests and record CRC32 checksums validate.

### Checkpoints, On-Demand LRU Paging, & Materialization

- **`StreamTrustedDictionariesToDisk(string directoryPath)`**: Writes all ten trusted partitions and `manifest.lrdmeta` atomically.
- **`OpenDiskStore(string directoryPath, int maxCachedRecordsPerPartition = 128)`**: Validates the store, builds identity and reverse indexes plus the `TrustedSkillsGraph` and link adjacency in a streaming pass, and 
		serves individual Foundation boxes on demand via a bounded LRU cache. Paged boxes are marked read-only.
- **`StreamTrustedDictionariesFromDisk(string directoryPath, bool replaceExisting = true, bool preloadAllIntoMemory = false, int maxCachedRecordsPerPartition = 128)`**: Validates the incoming store before mutating 
		core state. When `preloadAllIntoMemory` is `true`, all partitions are immediately materialized into mutable in-memory dictionaries.
- **`MaterializeDiskStore()`**: Stages all paged records into temporary dictionaries, marks them trusted/frozen, and swaps the core back into mutable in-memory mode.
- **`FlushTrustedDiskStore()` / `CompactTrustedDiskStore()` / `ValidateDiskStore()` / `CloseDiskStore(bool materialize = true)`**: Manage attached disk store lifecycle and integrity verification.

### Merge Reconciliation & Legacy Format Migration

- **Merge Imports (`replaceExisting: false`):**
  - Stages existing state and incoming store records into an isolated merge core before installing.
  - Reconciles existing `SenseBox`, `UsageBox`, `GroupBox`, and `SourceBox` containers by appending new sentence/word/group memberships while rejecting conflicting sense metrics or container names.
  - Verifies identity equality for matching `SymbolBox`, `WordBox`, `SentenceBox`, and `FunctionBox` IDs.
  - Reconciles `Node` `VerifiedReferences` and `InvalidReferences` for matching `(TargetKind, TargetId, Label)` identities, remaps colliding node GUIDs for distinct labeled nodes, and updates link weights when an 
		existing `AssertionId` is re-imported with a new weight.
- **Legacy Format v1 Migration:**
  - Format version 1 stores open in read-only legacy mode (`IsLegacyDiskStoreReadOnly == true`), synthesizing deterministic `NodeLinkOrigin.LegacyImport` provenance and treating legacy `SymbolBox` characters as assigned.
  - Calling `UpgradeLegacyDiskStore()` materializes the store, rewrites all partitions in format version 2, validates the upgraded checkpoint, and restores normal mutable mode.

---

## 7. File Character Grid Tooling (`OPEN_LRD.Tools`)

`Tool_1` and `FileGridDocument` (`OPEN-LRD/Tools/Tool_1.cs`) provide lossless 2D character grid parsing and reconstruction for text and binary files:

- **Supported Encodings (`FileGridEncoding`):** `Utf8`, `Utf16LittleEndian`, `Utf16BigEndian`, `Utf32LittleEndian`, `Utf32BigEndian`, and `Latin1` (byte-preserving fallback for arbitrary binary files or non-UTF8 8-bit text).
- **`Tool_1.ReadFileToCharGridDocument(string filePath, int? maxColumns = null, FileGridEncoding? encodingOverride = null)`**:
  - Detects BOM preambles and validates strict UTF encodings; falls back to `Latin1` if binary data or invalid UTF sequences (including embedded NULs in UTF-8) are encountered.
  - Splits text rows on `\r\n`, `\r`, or `\n` (preserving line-ending characters inside the row) and wraps rows at `maxColumns` (or `ceil(sqrt(byteLength))` for binary fallback when `maxColumns` is omitted).
  - Returns a `FileGridDocument` containing the rectangular `char[,]` grid, detected `FileGridEncoding`, `HasPreamble` flag, and exact per-row logical lengths (`RowLengths`) so trailing padding cells are distinguished from content.
- **`Tool_1.ParseFileToCharGrid(string filePath, int? maxColumns = null)`**: Convenience helper returning the `char[,]` array directly.
- **`Tool_1.WriteFileGridDocument(FileGridDocument document, string filePath)` & `Tool_1.WriteCharGridToFile(...)`**:
  - Encodes logical cells in row-major order using the stored encoding and preamble metadata, writing to a temporary sibling file before atomically replacing the destination file.

---

## 8. Computational & Domain Skills Library (`OPEN_LRD.Skills`)

All computational skills are implemented in native C# under `OPEN_LRD.Skills` with strict domain and finiteness validation.

### Math Function Dispatcher (`MathFunctionDispatcher`)

`MathFunctionDispatcher` routes structured `FunctionBox` definitions or direct keyed/signature calls to public static methods on `Math_1`, `Math_2`, `Math_3`, and `Math_4`:

- **Overload & Signature Resolution:** Matches candidate functions by optional `FunctionKey` (e.g., `"Math_2.CalculateHypotenuse"` or `"CalculateHypotenuse"`, case-insensitive), parameter count, exact `FunctionVariable.DataType` sequence, runtime value compatibility, and assignable `ExpectedResultType`.
- **Unique Signature Inference:** When `FunctionKey` is omitted (`null` or whitespace), automatically selects the function if exactly one registered method matches the supplied variable types, units, and expected return type; throws an ambiguity exception listing all matching keys if multiple methods match.
- **Unit Validation:** Enforces unit requirements on unit-annotated functions (e.g., `Math_2.CalculateHypotenuse` requiring `"m"` inputs and returning `"m"`, `Math_2.CalculateRectangleArea` and `Math_2.CalculateCircleArea` requiring `"m"` and returning `"m²"`). Throws `UnitConversionRequiredException` when a supplied unit has the same physical dimension (e.g., `"cm"` instead of `"m"`) so the caller can convert before dispatching.

### Core Mathematics (`Math_1` – `Math_4`, `Algebra`, `SymbolicCalculus`)

| Class | Core Capabilities & Functions |
| :--- | :--- |
| **`Math_1`** | Integer, `float`, `double`, `decimal`, and `BigInteger` arithmetic; `Fraction` simplification, addition, subtraction, multiplication, division, and conversions; GCD, LCM, factorial, permutations, combinations; descriptive statistics (sum, mean, weighted average, median, mode, variance, standard deviation, quartiles, IQR, percentile, covariance, Pearson correlation, z-score); sequences and series (arithmetic/geometric progression, Fibonacci, harmonic); percentage/ratio/proportion helpers; base roots, powers, logarithms, and clamping. |
| **`Math_2`** | Trigonometric, reciprocal (`Sec`, `Csc`, `Cot`), and inverse trigonometric functions; hyperbolic and inverse hyperbolic functions; angle normalization and radian/degree conversions; 2D and 3D analytic geometry (distance, midpoint, slope, line intersections, point-to-line and point-to-plane distances); triangle solvers (Heron's area, Law of Cosines, Law of Sines); 2D/3D mensuration (circle, sector, annulus, ellipse, polygon, sphere, cylinder, cone, frustum, torus, prism, pyramid); `Vector2` and `Vector3` magnitude, addition, subtraction, scaling, dot/cross products, normalization, angle, and projection. |
| **`Math_3`** | Symbolic parsing (`Parse`), simplification (`Simplify`), symbolic higher-order differentiation (`Differentiate`, `DifferentiateToString`), symbolic antiderivatives (`Integrate`, `TryIntegrate`, `IntegrateToString`), expression evaluation (`Evaluate`), symbolic Taylor/Maclaurin series expansion, numeric limits (`Left`, `Right`, `Both`), finite-difference derivatives, Riemann sums (`Left`, `Right`, `Midpoint`, `Trapezoid`), Simpson's rule, arc length, surface area of revolution, Newton-Raphson and bisection root finding, critical point and extremum classification (`Minimum`, `Maximum`, `Inflection`). |
| **`Math_4`** | Multivariable symbolic calculus (`PartialDerivative`, `Gradient`, `Laplacian`, `Hessian`, `DirectionalDerivative`, `Jacobian`); numerical vector calculus on 2D/3D scalar and vector fields (`Gradient`, `Divergence`, `Curl`, `Laplacian`, `DirectionalDerivative`, `JacobianMatrix`, `HessianMatrix`); line integrals over scalar/vector fields and parametric curves; double and triple integrals; surface flux integrals; Euler, RK4, and system ODE solvers (`OdeSolution`, `OdeSystemSolution`). |
| **`Algebra`** | Polynomial normalization, degree, evaluation (real and `Complex`), arithmetic (`Add`, `Subtract`, `Multiply`, `Scale`, `Compose`, `DividePolynomials`, `TrySyntheticDivision`), polynomial calculus, `PolynomialGCD` / `PolynomialLCM`, rational root finder, square-free and rational factorization (`FactorPolynomial`), linear/quadratic/general polynomial root solvers (`SolvePolynomial` via Durand-Kerner, `SolvePolynomialReal`), linear/quadratic/polynomial inequality interval solvers, radical simplification, matrix operations (`Identity`, `Transpose`, `Add`, `Subtract`, `Scale`, `Multiply`, `Power`, `Trace`, `Determinant`, `Inverse`, `Rank`, `RowReduce`, `SolveLinearSystem`), `Complex` transcendental functions and roots of unity, `Quaternion` algebra and axis-angle/vector rotation (`Slerp`, `Lerp`), symbolic expansion, substitution, term collection, and factoring. |
| **`SymbolicCalculus`** | Core symbolic engine backing `Math_3`, `Math_4`, and `Algebra`: algebraic/trigonometric simplification rules, product/quotient/chain-rule differentiation, table and substitution/by-parts symbolic integration, polynomial coefficient extraction, and variable environment evaluation. |

### Advanced Mathematics & Algorithms

| Class | Core Capabilities & Functions |
| :--- | :--- |
| **`LinearAlgebraAdvanced`** | Symmetry detection, `LuDecomposition` (with partial pivoting), `QrDecomposition` (Householder reflections), `CholeskyDecomposition`, `SymmetricEigenDecomposition` (Jacobi iteration), general `Eigenvalues` (QR algorithm with shifts), full `SingularValueDecomposition` (square, tall, wide, and rank-deficient matrices), Moore-Penrose `PseudoInverse`, `NullSpace`, `ColumnSpace`, `LeastSquares`, matrix norms (`Frobenius`, `One`, `Infinity`, `Spectral`), and `ConditionNumber`. |
| **`NumericalMethods`** | Lagrange, Newton divided-difference, natural/clamped/not-a-knot `CubicSpline`, and shape-preserving `PchipInterpolate` interpolation; polynomial, exponential, power, and logarithmic least-squares curve fitting (`CurveFitResult`); `Secant`, `FalsePosition`, `FixedPoint`, `Brent`, and multidimensional `NewtonSystem` root finding; `Romberg`, `AdaptiveSimpson`, `GaussLegendre`, and `MonteCarloIntegral` quadrature; `Jacobi`, `GaussSeidel`, and `ConjugateGradient` iterative linear solvers; adaptive `RungeKutta45` and `DormandPrince` ODE integrators; `RichardsonExtrapolation` and `AitkenDeltaSquared` convergence acceleration. |
| **`Optimization`** | 1D `GoldenSectionSearch` and `BrentMinimize`; multidimensional unconstrained `GradientDescent`, `NelderMead`, `ConjugateGradientMinimize`, and quasi-Newton `BfgsMinimize`; penalty/projected box-constrained and equality/inequality constrained optimizers; two-phase simplex `LinearProgramming` solver (`LinearProgrammingResult`); and Kuhn-Munkres `HungarianAssignment` (`AssignmentResult`). |
| **`ProbabilityAndStatistics`** | Special functions (`Erf`, `Erfc`, `LogGamma`, `Gamma`, `RegularizedGammaP/Q`, `Beta`, `RegularizedIncompleteBeta`); PDF, CDF, and quantile/inverse-CDF for Normal, Student's t, Chi-Square, F, Exponential, Uniform, Gamma, Beta, Binomial, Poisson, Geometric, and Hypergeometric distributions; z/t/proportion confidence intervals; one-sample, two-sample, paired t-tests, z-tests, chi-square goodness-of-fit/independence tests, and one-way `Anova`; discrete Bayesian posterior updates; simple, multiple, and weighted linear regression (`RegressionResult`); and seeded random sampling. |
| **`DiscreteMath`** | Positive modular arithmetic (`Mod`, `ModPow`, `ModInverse`, `ExtendedGcd`, `SolveLinearCongruence`, `ChineseRemainderTheorem` for `long` and `BigInteger`); segmented and standard prime sieves, deterministic Miller-Rabin primality testing (`IsPrime`), Pollard's rho prime factorization (`Factorize`), Euler's totient ($\phi$), divisor count/sum, Möbius ($\mu$), Catalan, Stirling, Bell, and partition numbers; base conversion and digit sums. |
| **`InformationTheoryAndMLMath`** | Shannon `Entropy`, `JointEntropy`, `ConditionalEntropy`, `MutualInformation`, `KlDivergence`, `JensenShannonDivergence`, `CrossEntropy`; activation functions (`Sigmoid`, `Tanh`, `Relu`, `LeakyRelu`, `Elu`, `Softplus`, `Gelu`, `Softmax`, `LogSoftmax`); loss functions (`MeanSquaredError`, `MeanAbsoluteError`, `HuberLoss`, `BinaryCrossEntropy`, `CategoricalCrossEntropy`, `HingeLoss`, `CosineSimilarity`); binary and multi-class `ConfusionMatrix`, `ClassificationMetrics`, `RocCurve`, and `RocAuc`; `DistanceMetric` (`Euclidean`, `Manhattan`, `Chebyshev`, `Minkowski`, `Cosine`, `Jaccard`); z-score/min-max standardization, `Pca` (`PcaResult`), and seeded `KMeans` clustering (`KMeansResult`). |
| **`Geometry3DAndGraphics`** | 2D Andrew's monotone chain `ConvexHull`, point-in-polygon, segment intersection, polygon area/centroid, ear-clipping `TriangulatePolygon`, 2D/3D `BarycentricCoordinates`; 3D triangle normal/area, plane construction and signed distance, `BoundingBox` merging/containment; `Ray` intersections with planes, triangles (Möller-Trumbore), spheres, and axis-aligned bounding boxes (`RayHit`); `BezierPoint`/`BezierDerivative` and `CatmullRomPoint`/`CatmullRomTangent` curves; 4x4 homogeneous translation, scaling, rotation, `LookAt`, orthographic, and perspective projection matrices. |
| **`TransformsAndSignals`** | `Dft`, `InverseDft`, radix-2 Cooley-Tukey `Fft`, `InverseFft`, `RealFft`, `FftShift`, `InverseFftShift`; time-domain and FFT-accelerated linear/circular `Convolve`, `CrossCorrelate`, and `AutoCorrelate`; window functions (`Rectangular`, `Hann`, `Hamming`, `Blackman`); windowed-sinc low-pass, high-pass, band-pass, and band-stop `FirFilter` design and application, single-pole recursive filtering, and `FourierSeries` synthesis/analysis. |
| **`FinancialMath`** | Discrete (`Annual`, `Semiannual`, `Quarterly`, `Monthly`, `Daily`) and `Continuous` compound interest, `EffectiveAnnualRate`, `PresentValue`, `FutureValue`, ordinary and due annuity PV/FV, `LoanPayment`, full `AmortizationSchedule` (`AmortizationEntry`), `NetPresentValue`, `InternalRateOfReturn` (`InvestmentResult`), depreciation schedules (`StraightLine`, `DecliningBalance`, `SumOfYearsDigits`), and `BondPrice`, `BondYieldToMaturity`, `MacaulayDuration`, `ModifiedDuration`, and `Convexity` (`BondResult`). |
| **`TimeAndDate`** | Leap-year and days-in-month queries, `DayOfYear`, `IsoWeek`, `DaysBetween`, holiday-aware `AddBusinessDays` and `BusinessDaysBetween`, exact `Age` calculation, Unix timestamp conversions, `TimeZoneInfo` conversions, midnight-crossing `ElapsedTime`, and invariant ISO-8601 formatting/parsing. |
| **`GraphAlgorithms`** | Generic directed/undirected `Graph<TNode>` and `GraphEdge<TNode>`; `BreadthFirstTraversal`, `DepthFirstTraversal`, `Dijkstra`, `BellmanFord`, `FloydWarshall`, Kahn's `TopologicalSort`, `HasCycle`, `ConnectedComponents`, `IsBipartite`, Tarjan's `StronglyConnectedComponents`, Kruskal's `MinimumSpanningTree`, Edmonds-Karp `MaximumFlow`, and iterative `PageRank`. |

### Units, Measurement, & Physics Domains

| Class | Core Capabilities & Functions |
| :--- | :--- |
| **`UnitsAndMeasurement`** | 7-base SI `UnitDimension` (`Length`, `Mass`, `Time`, `Current`, `Temperature`, `Amount`, `LuminousIntensity`), `Unit` and `UnitCatalog` (SI and US Customary units for length, mass, time, temperature, volume, force, energy, power, pressure, charge, potential, resistance, capacitance, magnetic flux density, frequency), SI prefix construction, affine/scale unit conversion, `Measurement` arithmetic with uncertainty propagation (`CombineAdditive`, `CombineMultiplicative`, `PropagatePower`, `PropagateFunction`), and significant-figure counting, rounding, and formatting. |
| **`BasicPhysics` & `PhysicalConstants`** | CODATA SI physical constants (`SpeedOfLight`, `GravitationalConstant`, `PlanckConstant`, `ReducedPlanckConstant`, `ElementaryCharge`, `VacuumPermittivity`, `VacuumPermeability`, `BoltzmannConstant`, `AvogadroConstant`, `IdealGasConstant`, `StefanBoltzmannConstant`, `StandardGravity`, `StandardAtmosphere`, `ElectronMass`, `ProtonMass`); Newtonian mechanics (`Force`, `Acceleration`, `Weight`, `Momentum`, `Impulse`, `KineticEnergy`, `GravitationalPotentialEnergy`, `Work`, `Power`, `CentripetalAcceleration`, `GravitationalForce`, `OrbitalSpeed`, `EscapeSpeed`, `SimpleHarmonicPosition`), and drag-enabled 3D projectile trajectory integration (`SimulateProjectile`). |
| **`Thermodynamics`** | Ideal gas law solvers (`IdealGasPressure`, `IdealGasVolume`, `IdealGasTemperature`, `IdealGasMoles`), sensible and latent heat transfer, first-law internal energy change, linear/volumetric thermal expansion, `CarnotEfficiency`, reservoir and isothermal/isochoric/isobaric entropy changes, adiabatic state relations, and Stefan-Boltzmann radiative power. |
| **`FluidMechanics`** | `HydrostaticPressure`, Archimedes `BuoyantForce`, mass/volumetric flow rates, `ContinuityVelocity`, `BernoulliPressure`, `ReynoldsNumber`, Hagen-Poiseuille laminar pipe flow (`PoiseuilleFlowRate`), aerodynamic/hydrodynamic `DragForce`, and `DarcyWeisbachPressureDrop`. |
| **`Electromagnetism`** | Vector `CoulombForce` and `ElectricFieldPointCharge`, `ElectricPotentialPointCharge`, Ohm's law solvers, `CapacitanceCharge`, `CapacitorEnergy`, transient `RcVoltage` and `RlCurrent`, vector `LorentzForce`, `MagneticForceOnWire`, Biot-Savart `MagneticFieldLongStraightWire` and `SolenoidMagneticField`, series/parallel resistance and capacitance, Faraday `InducedEmf`, and plane-wave `PoyntingVectorMagnitude`. |
| **`CondensedMatter`** | `UnitCellMassDensity`, `CubicInterplanarSpacing`, `BraggAngle`, 3D `ReciprocalLatticeVectors`, 1D `TightBindingDispersion` and `TightBindingGroupVelocity`, `MonatomicChainPhononFrequency`, `FermiWaveNumber`, `FreeElectronFermiEnergy`, `DebyeThermalEnergy`, and tight-binding `HamiltonianEigensystem`. |
| **`ManyBodyInteractions`** | `GravitationalPairPotential`, `CoulombPairPotential`, and `LennardJonesPairPotential`; pairwise and softened N-body potential/kinetic/total energy, force, and acceleration calculations; and symplectic `SimulateVelocityVerlet` trajectory evolution (`ManyBodySimulationResult`). |
| **`CurvedSpacetime`** | 4D `MinkowskiMetric` and `SchwarzschildMetric`, `SchwarzschildRadius`, `SchwarzschildMassLength`, `MetricInterval`, gravitational time dilation and redshift, numerical finite-difference `ChristoffelSymbols`, `RiemannTensor`, `RicciTensor`, `RicciScalar`, and RK4 `IntegrateGeodesic` (`GeodesicSolution`). |
| **`ExtremeScalars`** | Extended-exponent floating-point representation `ScaledScalar(double Significand, long BinaryExponent)` preventing IEEE-754 `double` overflow/underflow across extreme magnitudes; log-magnitude conversions, arithmetic (`Add`, `Subtract`, `Multiply`, `Divide`, `Power`, `Sqrt`, `Exp`), and coupled Klein-Gordon-like scalar field evolution (`SimulateScalarField`). |

---

## 9. API & Invariant Reference

### `LRD_Core` Key Operations Summary

```csharp
// 1. Lifecycle
LRD_Core core = LRD_Core.Instance;
core.InitializeCore();
bool isReady = core.initialized;
core.ResetCore();

// 2. Trusted Foundation Registration (auto-creates canonical Node & structural NodeLinks)
core.AddTrustedSense(senseBox);
core.AddTrustedUsage(usageBox);
core.AddTrustedSymbol(symbolBox);
core.AddTrustedWord(wordBox);
core.AddTrustedSentence(sentenceBox);
core.AddTrustedGroup(groupBox);
core.AddTrustedSource(sourceBox);
core.AddTrustedFunction(functionBox);

// 3. Post-Trust Append-Only Container Membership Extensions
core.AddSentenceToTrustedGroup(groupId, sentenceId);
core.AddGroupToTrustedSource(sourceId, groupId);

// 4. Untrusted Staging & Atomic Promotion
core.SetUntrustedSymbol(untrustedSymbol);
core.SetUntrustedWord(untrustedWord);
core.SetUntrustedSentence(untrustedSentence);
core.SetUntrustedFunction(untrustedFunction);

core.ConvertUntrustedSymbolToTrusted(symbolId);
core.ConvertUntrustedWordToTrusted(wordId);
core.ConvertUntrustedSentenceToTrusted(sentenceId);
core.ConvertUntrustedSentenceSubgraphToTrusted(sentenceId);
core.ConvertUntrustedFunctionToTrusted(functionId);

// 5. Canonical Deduplication (rebinds references if identity already exists in trusted store)
Guid? canonicalSymbolId = core.ResolveOrPromoteUntrustedSymbol(symbolId);
Guid? canonicalWordId = core.ResolveOrPromoteUntrustedWord(wordId);
Guid? canonicalSentenceId = core.ResolveOrPromoteUntrustedSentenceSubgraph(sentenceId);

// 6. Node & Provenance-Backed Link Operations
Node? canonicalNode = core.GetTrustedNodeByTarget(NodeTargetKind.Word, wordId);
IReadOnlyList<Node> allTargetNodes = core.GetTrustedNodesByTarget(NodeTargetKind.Word, wordId);
string? summary = core.GetTrustedNodeSummary(canonicalNode!);

core.AddTrustedLink(sourceNodeId, targetNodeId, NodeRelationType.Synonym, weight: 0.95, label: "lexical");
core.UpdateTrustedLinkWeight(assertionId, newWeight: 0.85);
core.RemoveTrustedLink(assertionId);

Graph<Guid> graphSnapshot = core.BuildSkillsGraphSnapshot();

// 7. Partitioned Binary Disk Persistence & Paging
core.StreamTrustedDictionariesToDisk(storeDirectory);
core.OpenDiskStore(storeDirectory, maxCachedRecordsPerPartition: 128);
core.MaterializeDiskStore();
core.StreamTrustedDictionariesFromDisk(otherStoreDirectory, replaceExisting: false, preloadAllIntoMemory: true);
```

### Core Invariants

1. **Non-Empty Identifiers:** `Guid.Empty` is rejected across all Foundation boxes, nodes, links, provenance targets, and symbolic conversations.
2. **Complete Identity Before Trust:** A `SymbolBox`, `WordBox`, `SentenceBox`, or `FunctionBox` cannot be trusted until all required fields and referenced target IDs are populated and already trusted.
3. **Append-Only Trusted State:** Trusted Foundation boxes and their membership lists never shrink or mutate identity in place.
4. **Canonical Structural Wiring:** Structural `NodeLink` edges (`HasSense`, `HasUsage`, `ContainsSymbol`, `ContainsWord`, `LeadingWord`, `TrailingWord`, `ContainsSentence`, `ContainsGroup`) always connect canonical (`Label == null`) nodes and cannot be added, re-weighted, or removed through external link APIs.
5. **Transactional Link & Disk Mutations:** Symmetric link updates and disk checkpoint imports/merges/materializations validate completely before committing and leave existing state untouched on failure.

---

## 10. Smoke Test Suite (`OPEN-LRD.SmokeTests`)

The `OPEN-LRD.SmokeTests` console runner executes 28 test modules (174 test cases) with zero external test framework dependencies:

- **Core & Persistence Tests:**
  - `CoreIdentityTests`: Core initialization lifecycle, identity assignment/freezing, word uniqueness, duplicate/incomplete promotion rejection, semantic indexes, and concurrent graph mutations.
  - `CoreNodeGraphTests`: `Guid.Empty` guards, `GroupBox`/`SourceBox` ID storage and snapshots, O(1) reverse/containment indexes, automatic node creation and structural link wiring, atomic sentence-subgraph promotion, `Skills.Graph<Guid>` integration, and link lifecycle hardening.
  - `CoreDiskStreamingTests`: Partitioned binary checkpoint save/load, on-demand LRU paging, corruption rejection, legacy v1 read-only loading and atomic v2 upgrade, and container/node/link merge reconciliation.
  - `FoundationHardeningTests`: Value validation, trusted `FunctionBox` freezing, node label identity and canonical lookup, `BigInteger` `Fraction` semantics, finite `Vector2`/`Vector3` boundaries, `SymbolBox` assigned-NUL tracking, atomic `UInt128` conversation ranges, and `ExpressionParser` limits.
  - `ToolFileParsingTests`: Multi-encoding character grid parsing, binary square-root wrapping, Latin-1 preservation, and atomic round-trip file writing.
- **Mathematics, Physics, & Algorithm Tests:**
  - `AlgebraTests`, `MathHelperReuseTests`, `MathFunctionDispatcherTests`, `TimeAndDateTests`, `ProbabilityAndStatisticsTests`, `NumericalMethodsTests`, `OptimizationTests`, `DiscreteMathTests`, `InformationTheoryAndMLMathTests`, `Geometry3DAndGraphicsTests`, `TransformsAndSignalsTests`, `FinancialMathTests`, `UnitsAndMeasurementTests`, `BasicPhysicsTests`, `ThermodynamicsTests`, `FluidMechanicsTests`, `ElectromagnetismTests`, `CondensedMatterTests`, `ManyBodyInteractionsTests`, `CurvedSpacetimeTests`, `ExtremeScalarsTests`, `GraphAlgorithmsTests`, and `LinearAlgebraAdvancedTests`.
