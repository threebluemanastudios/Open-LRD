# OPEN-LRD: Current Plan

Combined plan for the first pass. Covers ReasoningLayer2 (Tracks A and B, excluding Phase 6) and PhysicsSim (Phases 0-2). Detailed references:

- `.plans/ReasoningLayer2.md` (full RC and BP design, decisions D1-D12)
- `.plans/PhysicsSim.md` (full physics engine design)
- `.plans/Benchmark.md` (RC Phase 0 measurements)

Phase 6 (answer promotion) of ReasoningLayer2 is out of scope for this pass. Its design is not settled.

## Progress

Status as of this pass. Tests: `dotnet run --project OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj`, 273 passed, 0 failed (unchanged in this pass; attention work is in `RCBench/`). `OPEN-LRD.csproj` builds with `-p:EnableWindowsTargeting=true` on WSL.

| Item | Status | Where |
|---|---|---|
| BP-3 pure math (path confidence, loss, logit gradients via prefix/suffix, sigmoid/logit) | Done. Finite-difference tests (T5.6) pass for lengths 1-3, near 0 and 1, and zero weights | `OPEN-LRD/Reasoning/LinkLearning.cs`, `OPEN-LRD.SmokeTests/LinkLearningTests.cs` |
| PS-0 foundation: `VecN` (3D fast path), velocity Verlet, RK4, fixed-step loop, CG, FFT (radix-2 and Bluestein) | Done. Integrator order, oscillator energy, CG, FFT vs naive DFT, FFT round-trip | `OPEN-LRD/PhysicsSim/Core`, `PhysicsSim/Numerics`, `OPEN-LRD.SmokeTests/PhysicsSimTests.cs` |
| PS-2 N-body: pair laws (gravity, Coulomb), softening, cutoff, diagnostics | Done. Two-body period, energy drift, momentum conservation, 2D/3D agreement, Coulomb sign, softening, validation | `OPEN-LRD/PhysicsSim/Laws/PairLaw.cs`, `PhysicsSim/Modules/NBody.cs` |
| A1.6-A1.10 attention kernel: native head-major KV cache, fused GQA kernel, chunked parallel attention with ordered merge, optional int8 KV, allocation-free parallel matvec | Done (scratch project) | `RCBench/Attention.cs`, `RCBench/Types.cs` |
| A1.9 numerical checks: fused fp32 and int8 vs scalar reference (tolerance 1e-5), parallel bit-identical to serial | Done. Errors at most 1.9e-7 | `RCBench/AttentionCheck.cs` (runs at every benchmark start) |
| A1.8 int8 KV quality | Measured: 0.6% relative RMS vs fp32 on random data. Output quality on real model not measured. Int8 gives no speed gain, halves KV memory only at 0.48 GB vs 1.88 GB | `.plans/Benchmark.md` |
| A1.11 16k gate (5 tok/s) | **Passes, with a smaller margin than first reported.** Earlier 8 to 9 tok/s figure used an unprimed KV cache. With primed KV: scratch fp32 about 6.8 tok/s, production int8 about 7.7, production fp32 about 5.3 (see Inference port) | `.plans/Benchmark.md` (correction and port sections) |
| Milestone 2 (A1.6-A1.9 checks pass) | Done | |
| Stream 1: BP-1, BP-2, Phase 3 (v4), Phase 4 hook, BP-4 to BP-6 | Not started. Requires `LRD_Core.cs` edits; each needs its own review | `OPEN-LRD/LRD_Core.cs` |
| PS-1: graph bridge, law nodes, run registration, JSON config, `--sim` | Not started. Depends on Stream 1 | `OPEN-LRD/App.xaml*` |
| Phase 1 remainder, Phase 2 RC decoder | Not started | `OPEN-LRD/Reasoning/` |
| Phase 5 RC and BP test suites | BP-3 math tests done. Remaining tests depend on Stream 1 | |

Notes from this pass:
- N-body uses a system-level velocity-Verlet loop, not `Integrators.VelocityVerletStep`. Forces couple all particles, so the single-body helper does not apply. The helper is tested on the oscillator.
- The fixed-step loop does not take a seed. Determinism comes from the caller (`System.Random(seed)` in test setup). A seeded RNG type is still to be decided for PS-1 run configs.
- With a cutoff, energy is not exactly conserved, because pairs switch on and off. No test checks conservation with a cutoff.
- Attention notes: on the earlier unprimed benchmark, int8 KV looked no faster than fp32. With primed KV, int8 is faster at 16k (see Inference port). The fused kernel runs one task per (KV head, 1024 positions). `RCBench` attention is not yet part of `OPEN-LRD/Reasoning/`; it must be ported with the Phase 1 model runtime (`Reasoning/Inference/`).
- Law expression parsing (`ExpressionParser`) is not used yet. Laws are C# objects. Graph-stored laws come with PS-1.

## Inference port (`OPEN-LRD/Reasoning/Inference/`)

Status: **implemented; int8 KV is the long-context default; prefix caching, two-tier KV storage, and paged KV with shared prefix blocks (steps 1-3), batched decoding over paged sequences, batched prefill, batched attention across tokens, and a disk tier for paged prefix blocks added.** Smoke tests: 307 passed, 0 failed (34 tests in `ReasoningInferenceTests.cs`). No `unsafe` code in the new files. WPF project builds.

| Item | Status |
|---|---|
| `ModelConfig`, `QMat`, `Kernels` (scalar, AVX2, parallel `MatVecEngine`, `MatMat` loop), `KvCache` (managed arrays, fp32 or int8), `Attention` (fused GQA, chunked merge, int8, scalar reference), `Decoder`, `Sampler` (xoshiro256**, temperature 0.7, top-p 0.8), `ILocalModel` | Done |
| Tests: kernels vs scalar, parallel and batch bit-identical, fused attention fp32 and int8 across chunks, KV round-trip, sampler determinism and distribution, decoder determinism and sequencing, fake `ILocalModel`, config validation, KV policy default | Done, 12 tests, all pass |
| KV precision default: int8 for long context (`KvPolicy`, threshold 4096 positions) | Done |
| Automatic prefix caching (`PrefixCache`, `Decoder.Prefill`): chained block hash over tokens, verified on every hit (tokens, parent, mode, shape) | Done. Hits are bit-identical to cold prefill (fp32 and int8) |
| Hierarchical KV tiers: tier 0 live cache, tier 1 RAM (LRU, byte budget), tier 2 disk directory. Eviction writes to disk, disk hits promote to RAM, corrupt files are misses | Done. Two tiers below tier 0. No compression. No size limit on disk |
| Prefix benchmark: 1024-token prompt, warm cache 0.01 s vs 112 s cold | Done (Benchmark.md) |
| Batched prefill through `MatMat` | Done (`PrefillBatched`, paged decoders). See the batched prefill section |
| RCBench compiles the production sources (`EnableDefaultCompileItems` off) | Done. Old scratch files kept as `*.old` for reference |
| Matvec performance parity with scratch kernels (FFN and LM) | Done, after `AggressiveOptimization` on hot kernels (JIT tiering was costing about 2x) |
| Decode performance at 16k, fp32 KV, within 5% of scratch | **Not met.** Attention kernel rewritten with array refs (no per-row spans, no bounds checks) and now at the fp32 bandwidth limit. Remaining gap in matvec under fp32 KV traffic: port median 5.3 vs scratch 6.8 tok/s (4 runs each). Session noise is high, so the gap is not settled |
| Decode performance at 16k, int8 KV | About 7.7 tok/s median. Meets the 5 tok/s gate |
| Benchmark methodology: prime KV before timing | Done. Earlier 16k figures (8 to 9 tok/s) were optimistic because the KV cache was not written. Corrected in `.plans/Benchmark.md` |
| Optimizations after the port (shared-input fusion, accumulators, balanced blocks, persistent pool) | Not started |

Decisions:
- **KV precision default (decided):** int8 for long-context RC sessions, fp32 for short context. `KvPolicy.DefaultFor(maxContext)` returns int8 when `MaxSeq >= 4096` (`KvPolicy.LongContextThreshold`). `Decoder` uses it when no `kvMode` is passed. Explicit `KvMode` still overrides. Fp32 remains for short-context and correctness testing.
- The fp32 gap at 16k (about 22%) is accepted for long context because int8 is the default there. Investigation (Benchmark.md): matvec is not slow in isolation, and serial attention does not help. At 16k both phases are DRAM-bandwidth bound, and fp32 reads about 3.9 GB per token against about 2.5 GB for int8. The gap can only be closed by moving fewer bytes, so it is accepted for explicit fp32 use only.

Earlier design notes follow.

Decisions: sampler and `ILocalModel` included now. No `unsafe` code: kernels use managed arrays with `Span`, `ref`, and `Vector256.LoadUnsafe`/`StoreUnsafe`. No `AllowUnsafeBlocks` in any csproj. Batched API signature defined now, batched path implemented in the optimization step. Benchmarks stay in `RCBench/`, which compiles the production sources. Batched decoding is accepted for Phase 6. Int8 weights are kept (D1).

Files (namespace `OPEN_LRD.Reasoning.Inference`):
- `ModelConfig.cs` record (hidden, layers, heads, KV heads, head dim, FFN, vocab, max context). Tests and RCBench use the benchmark shape until the 2B architecture is fixed.
- `QMat.cs` int8 matrix with per-row scales.
- `Kernels.cs` scalar, AVX2 serial, row-parallel matvec. `MatVec(QMat, ReadOnlySpan<float>, Span<float>)` and `MatMat(QMat, ReadOnlySpan<float> X, int batch, Span<float> Y)` signatures. Reduction order per output element is fixed, so output is identical for any batch size.
- `KvCache.cs` managed arrays per (layer, KV head), fp32 or int8 with per-row scales.
- `Attention.cs` fused GQA kernel, chunked tasks, ordered merge, fp32 and int8 paths, scalar reference.
- `Decoder.cs` single-sequence forward pass.
- `Sampler.cs` in-repo seeded PRNG (xoshiro256**), temperature 0.7, top-p 0.8.
- `ILocalModel.cs` interface for `RcDecoder` testing.

Tests (`OPEN-LRD.SmokeTests/ReasoningInferenceTests.cs`): kernels vs scalar within 1e-5; parallel bit-identical to serial; fused attention vs scalar at several positions (fp32 and int8); KV write/read round-trip; sampler determinism; fake `ILocalModel` sequencing.

Build changes: `RCBench` compiles `../OPEN-LRD/Reasoning/Inference/*.cs`. The smoke csproj gets matching `Compile Include` entries. `OPEN-LRD.csproj` is not changed.

Performance gate: production kernels must stay within 5% of the RCBench numbers recorded in `.plans/Benchmark.md` (16k sweep, fused fp32). If the gap is larger, stop and report.

Optimization steps after the port, each measured and checked against the scalar reference: bandwidth ceiling test, persistent worker pool, shared-input fusion (Wq/Wk/Wv and W1/W3), multiple accumulators per row, balanced row blocks.

## Recent changes (since the last plan update)

- **KV precision policy:** int8 KV is the default for long-context RC sessions (`KvPolicy`, MaxSeq >= 4096). Fp32 stays for short context and explicit use.
- **fp32 gap at 16k investigated, not closed.** Matvec is not slow in isolation, and serial attention does not help. Decode at 16k is DRAM-bandwidth bound. fp32 reads about 3.9 GB per token, int8 about 2.5 GB. Recorded in Benchmark.md.
- **Automatic prefix caching and two-tier KV storage:** `PrefixCache` (RAM LRU tier and disk tier) and `Decoder.Prefill`. Hits are bit-identical to cold prefill. Warm 1024-token prompt takes 0.01 s, against 112 s cold. Tests: 291 passed.
- **Limitation found:** KV storage is one contiguous array per layer sized to MaxSeq. Prefix blocks are copies, so sessions with a shared prefix store it more than once.

## Paged KV cache and block tables (done: steps 1-3)

Goal: fixed-size physical blocks in a shared pool, a block table per sequence, copy-on-write sharing for prefixes, and fused attention that reads through the table.

Design:
- `KvBlockPool` (`OPEN-LRD/Reasoning/Inference/KvPaging.cs`): one pool per model shape and KV precision. Block size is a power of two (default 256 positions). Each block holds all layers and all KV heads for its positions. Free list plus reference count per block.
- `PagedKvSequence`: block table (list of physical block ids), length, and operations `Write`, `Fork` (shares all blocks by reference), and `Clear` (releases blocks).
- Copy-on-write: writing into a block with reference count above 1 first copies the whole block to a fresh block, then releases the old one.
- Attention: `Attention.RunPaged` reads K and V rows through the block table. Same arithmetic order as the contiguous kernel, so output should be bit-identical.
- `Decoder`: a paged constructor binds a sequence. The contiguous path is unchanged.

Tests (planned):
- Paged logits bit-identical to contiguous, fp32 and int8.
- Fork: two sequences share a prefix, then diverge through copy-on-write. Each matches its own cold run.
- Pool exhaustion throws. Release returns blocks to the free list.

Status:
- Step 1, done: `KvBlockPool` (free list, reference counts, copy-on-write block copy) and `PagedKvSequence` (block table, `Write`, `Fork`, `Clear`) in `KvPaging.cs`. `Decoder` has a paged constructor. The contiguous path is unchanged.
- Step 2, done: `Attention.RunPaged` reads K and V rows through the block table. Output is bit-identical to the contiguous kernel.
- Step 3, done: `PagedPrefixIndex` (in `KvPaging.cs`). It maps chain hash to a physical pool block and holds one reference per entry, so prefixes are shared by reference, not copied. `Decoder.Prefill(tokens, index)` appends shared blocks with `PagedKvSequence.AppendShared`. Entries evict LRU and release their reference. `PrefixCache` (copy-based, RAM and disk) is unchanged for contiguous decoders.
- Measured: paged int8 prefill of 512 tokens takes 48.9 s against 48.0 s contiguous (within noise), and logits are identical (Benchmark.md).
- Not done: batched decoding over paged sequences, disk paging of pool blocks, and tuning the paged attention kernel (it still does a table lookup per position). The paged prefix index is not yet connected to the disk tier.

Tests (smoke suite, 298 passed, 0 failed; paged tests added in steps 1-3):
- Paged logits bit-identical to contiguous, fp32 and int8, across 40 positions in 16-position blocks.
- Fork and copy-on-write: shared partial block is copied on first write, and both branches match their own cold runs.
- Pool exhaustion throws; `Clear` and `Release` return blocks to the free list.
- Shape mismatch, non-power-of-two block size, zero blocks, and copy-based prefix-cache use on paged decoders are rejected.
- Step 3: a second prompt with the same prefix reuses the same physical blocks (reference count 3: two sequences plus the index). Logits are bit-identical to cold prefill. Reuse allocates only the private partial block. Eviction releases the index reference. Clearing sequences and then the index returns every block.

Not in this step: batched decoding over paged sequences, and disk paging of pool blocks.

## Batched decoding over paged sequences (done)

Advances B paged sequences by one token per step. Each weight matrix is read once per step for all B.

- `Kernels.MatMat` and `MatVecEngine.RunBatch`: rows are the outer loop. `DotRowAvx2Group4` converts each int8 weight chunk to float once and feeds it to up to four accumulators. Each accumulator reduces in the same order as the single-sequence kernel, so output is bit-identical for any batch size.
- `BatchDecoder` (`OPEN-LRD/Reasoning/Inference/BatchDecoder.cs`): B `PagedKvSequence`s on one pool. Sequences may differ in length and may share prefix blocks. Projections use `RunBatch`. Attention runs per sequence through `RunPaged`.
- `Decoder.RmsNorm` is now `internal` so both decoders share it.
- Sequences must share one pool. Pool exhaustion mid-step throws, and the caller clears the affected sequences.

Tests (smoke suite, 302 passed, 0 failed, 4 new):
- Batched logits bit-identical to single paged decoders for three sequences of different lengths, fp32 and int8.
- Two sequences sharing prefix blocks, then continuing with different tokens, match cold runs.
- `MatMat` and `RunBatch` match single matvecs bit for bit.
- Validation: mixed pools, empty batch, wrong token count, out-of-range token, shape mismatch.

Measured (`BATCH_BENCH=1`, int8, 512-position contexts, Benchmark.md): single sequence 9.5 tok/s; batch 4 about 30 tok/s; batch 8 about 35 tok/s.

Not done: batched prefill (prompts still run one sequence at a time), batched attention across sequences, and batched decode through the disk tier.

## Batched prefill over paged sequences (done)

- `Decoder.PrefillBatched(tokens, chunk = 64, index)` (`Decoder.cs`): paged decoders only. Reuses indexed prefix blocks first, then runs the uncached suffix in chunks. Per chunk and layer, projections use `MatVecEngine.RunBatch` (each weight read once per chunk). Per token: RoPE, `Write`, and `RunPaged` at that position (causal). Logits are computed only at the prompt end and at indexed block ends.
- Output is bit-identical to sequential `Prefill` (fp32 and int8, chunk sizes 1, 7, 16, 64).
- Measured (512 tokens, int8, Benchmark.md): sequential 55 s, batched chunk 64 18 s.

Tests: 305 passed, 0 failed (3 new). Not done: batched attention across tokens (attention is per position), and batched prefill through the disk tier.

## Batched attention across tokens and disk-tier batched prefill (done)

1. **Batched attention across tokens.** `Attention.RunPagedChunk` handles all tokens of a prefill chunk in one call. Tasks are (KV head, 1024-position chunk). Each K or V row is read once per task and applied to every chunk query that can see it. Per query the arithmetic and merge order match `RunPaged`, so output is bit-identical. `Decoder.ForwardChunk` writes the chunk's K and V for a layer first, then calls it once.
2. **Disk tier for paged prefix blocks.** `PagedPrefixIndex` takes an optional spill `PrefixCache`. Evicted entries are copied out of the pool (`KvBlockPool.CaptureBlock`) and spilled. A RAM miss checks the spill store; a hit allocates a pool block, restores it (`RestoreBlock`), and re-indexes it. `PrefillBatched` uses this through `TryGet`.
3. **Bug found and fixed in `PrefillBatched`.** Completed blocks were indexed with the last allocated block (`Table[Count - 1]`). When a chunk ran past a block boundary, that was the next block, so a hash pointed at the wrong physical block. The fix uses `Table[p / BlockSize]`. Chunk sizes aligned to the block size had hidden the bug. The disk-tier test (chunk 7) caught it.

Measured (`PREFILL_BENCH=1`, 512 tokens, int8): batched chunk 64 18.6 s (previously 18.2 s, within noise, so attention was not the bottleneck). Disk tier: first prefill 18.6 s; a second prefill with both blocks restored from disk takes 0.04 s with identical logits.

Tests: 307 passed, 0 failed. New: batched attention across the 1024-position boundary (fp32 and int8, plus decode afterwards); disk-tier batched prefill (fp32 and int8), with a disk hit, shared restored block, and every pool block returned.

Not done: batching attention across layers or across sequences; disk tier for `BatchDecoder`; compression of disk blocks.

## Constraints (shared)

- Pure C#, no external NuGet packages. `System.Runtime.Intrinsics` and `System.Text.Json` are allowed.
- Hardware: i7-11800H (8C/16T, AVX2), 16 GB RAM, WSL2 for benchmarks.
- Reproducibility: bit-identical output on the same machine.
- Single write path to `LRD_Core`: multi-object writes go through `ReasoningWriteBatch` or `ReasoningTransaction`.
- New code goes into new folders where possible, to limit merge conflicts.

## Shared-file hotspots

These files are touched by more than one track. Edits to them are serialized, one track at a time:

- `OPEN-LRD/LRD_Core.cs`
- `OPEN-LRD/Persistence/PartitionedBinaryStore.cs`
- `OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj`
- `OPEN-LRD.SmokeTests/Program.cs`
- `OPEN-LRD/App.xaml` and `App.xaml.cs` (PhysicsSim `--sim` path only)

## Execution order

```
Stream 1 (LRD_Core, serialized)     Stream 2 (independent)          Stream 3 (independent)
------------------------------      -------------------------       ----------------------
BP-1  maps and cleanup              BP-3  pure math                 PS-0  VecN, integrators,
BP-2  batch staging                 (LinkLearning.cs)                     FFT, CG solver
  |                                   |                             PS-2  N-body (needs PS-0)
  v                                   v                             
Phase 3  format v4                  A1.6-A1.11  attention kernel    
  |                                   |                             
  v                                   |                             
Phase 4  final-answer hook          Phase 2  RC decoder (needs A1.5)
  |                                   |
  v                                   |
BP-4..BP-6  path search, penalties,  |
            feedback call           |
  |                                   |
  v                                   v
PS-1  graph bridge, JSON config, --sim in App.xaml  (needs Stream 1 merged)
  |
  v
Phase 5  tests (RC and BP)
```

Rationale:
- Stream 2 and Stream 3 do not touch `LRD_Core.cs`, so they run while Stream 1 is in progress.
- BP-3 is pure math, so it is tested first, before any graph code exists.
- A1.6-A1.11 is gated on the 16k decode target (5 tok/s). It is the most urgent kernel work.
- PS-1 (graph bridge) waits for Stream 1, because it depends on `BeginReasoningWriteBatch` and the `LinkCosts` partition conventions.

## Stream 1: LRD_Core changes

### BP-1: Maps and cleanup
- Add `LinkLogits` (in-memory) and `LinkCosts` (persisted in v4) to `LRD_Core`.
- Clean up both maps on link removal. Verify `ShouldStoreReverse` and the removal path first.
- Clean up both maps in `ResetCore()` (D10).

### BP-2: Batch staging
- Add `StageLinkWeightUpdate` to `ReasoningTransaction` and `ReasoningWriteBatch`, with phase-1 validation.
- Commit applies weight, logit, and penalty changes, with rollback actions like existing boxes.
- `UpdateTrustedLinkWeight` writes immediately and cannot roll back. Feedback never calls it during a batch.

### Phase 3: Storage (format v4)
- Types: `RcSession`, `RcTurn` (Reasoning or Summary, text, token counts, finish reason, parent ID), `RcStore`.
- New `DiskPartition` values:

  | Partition | Value | Contents |
  |---|---|---|
  | `TextConversations` | 13 | `TextConversationDictionary` |
  | `SymbolicConversations` | 14 | `SymbolicConversationDictionary` |
  | `RcSessions` | 15 | `RcStore` sessions and turns |
  | `LinkCosts` | 16 | Per-assertion base cost and penalty |

- Bump `CurrentFormatVersion` 3 to 4. Readers accept v1-v3. New partitions load empty. Penalties reset once on upgrade. Document in the release notes.
- Update `StreamTrustedDictionariesToDisk`, `StreamTrustedDictionariesFromDisk`, and `OpenDiskStore`. Check whether the paged cache needs entries for the new partitions.
- `LinkLogits` is not persisted. It is re-derived from weights on load.

### Phase 4: Final-answer hook
- `StageRcAnswer(sessionId, boxedContent)` stages extracted content as an untrusted `SentenceBox`. Order: symbols, words, sentence. Uses `ReasoningWriteBatch`.
- Verify the existing untrusted staging method names before writing it.
- Reasoning and summary turns never enter the graph.

### BP-4 to BP-6: Path search, penalties, feedback
- Traversable edges: non-structural links only. Each assertion is used at most once per simple path.
- Edge score `s(e) = log w(e) - λ · cost(e)`. Skip `w = 0`.
- Dinkelbach iteration with Bellman-Ford. Ties within 1e-9. Tie cap 16, with a `Truncated` flag.
- Effective cost `min(base + penalty, maxCost)`.
- Penalties (Option A): if `y < threshold`, add `penaltyStep` to the path; if `y ≥ threshold`, multiply by `decay`. All parameters come from a required `LinkLearningOptions` record.
- Feedback call: `LinkFeedbackResult ApplyOutcomeFeedback(Guid sourceNodeId, Guid targetNodeId, double observedOutcome, double learningRate, LinkLearningOptions options)`.
  - Validate inputs. Tie-break: fewest hops, then lexicographic assertion IDs. Return all ties.
  - Stage all weight and penalty changes in one batch. Rebuild the Skills snapshot once at commit.
  - Targets: trusted, non-structural links only.

## Stream 2: Reasoning Cache inference

### BP-3: Pure math (`Reasoning/LinkLearning.cs`)
- Path confidence `c = Π wᵢ`. Prefix and suffix products give `P₋ᵢ` without division.
- Loss `L = ½(c - y)²`. Gradient `dL/dzᵢ = (c - y) · P₋ᵢ · wᵢ(1 - wᵢ)`.
- Parameterization `w = sigmoid(z)`. If stored weight disagrees, re-derive `z = logit(clamp(w, 1e-9, 1 - 1e-9))`.
- Test first (T5.6) with finite differences, before any graph code.

### A1.6 to A1.11: Attention kernel
- A1.6: contiguous per-layer KV buffer with precomputed offsets. Remove per-call `fixed` pinning. Block over positions for GQA (20/4 heads). Target about 5-8 ms per 1k positions (currently about 23).
- A1.7: parallel attention across heads with fixed reduction order.
- A1.8: KV-cache precision: int8 with per-position scales, or float16. Measure quality against float32 before adopting.
- A1.9: numerical check of vectorized and quantized attention against the scalar reference. Tolerance about 1e-5.
- A1.10: remove per-call allocations in the parallel matvec.
- A1.11: re-run the 16k gate. Pass: 5 tok/s at 16k. Record in `.plans/Benchmark.md`.
- Can proceed on the benchmark shape (hidden 2560, 28 layers, 20/4 heads, head dim 128). Blocked on the 2B architecture for format work only.

### Phase 1 remainder: Model runtime (`Reasoning/Inference/`)
- A1.1 byte-level BPE tokenizer. A1.2 versioned weight format. A1.3 forward pass (RMSNorm, RoPE, GQA, SwiGLU, KV cache). A1.4 sampler (temperature 0.7, top-p 0.8, in-repo seeded PRNG). A1.5 `ILocalModel` interface.

### Phase 2: RC decoder (`Reasoning/`)
- A2.1 Loop per paper Eq. 5.1-5.2: reasoning turn, then summary turn. Prompts I_R and I_S as versioned constants.
- A2.2 Config: T = 12, H_S = 2048, H_R = 16384. Stop on `\boxed{}`.
- A2.3 Answer extraction: last `\boxed{}` in the final reasoning turn.

## Stream 3: PhysicsSim

Full design in `.plans/PhysicsSim.md`. Code in `OPEN-LRD/PhysicsSim/`, namespace `OPEN_LRD.PhysicsSim`.

### PS-0: Foundation (no shared-file edits)
1. `VecN` with a 3D fast path. Finite-value and dimension checks.
2. Velocity-Verlet, RK4, fixed-step loop with a deterministic seed.
3. CG linear solver and FFT (radix-2, Bluestein fallback).
4. Tests: integrator order, harmonic oscillator, FFT round-trip.

### PS-2: N-body (no shared-file edits)
5. Particles in N dimensions. Pair force from a law object. Built-in gravity and Coulomb fallbacks.
6. Softening, optional cutoff, per-step energy and momentum diagnostics.
7. Tests: two-body period, energy drift bound, momentum conservation, consistent 2D and 3D results.

### PS-1: Graph bridge, config, headless mode (needs Stream 1 merged)
8. Law nodes: `Function`-type nodes with expression string, ordered variables, and units. Loaded by label via `GetTrustedNodeByTarget`. Parsed with `ExpressionParser`.
9. Run registration: one `Source` node per run. Parameters as `Custom` links (`Origin = Reasoning`). Summaries as link labels. Weights in [0,1].
10. Result files: `<run>.csv` or `.bin` in the configured output directory. Only paths go in the graph.
11. Writes batched through `BeginReasoningWriteBatch`. No per-step graph writes.
12. JSON run config (`System.Text.Json`) with validation. Laws referenced by label only; no expression text in the config.
13. `--sim <run.json>` mode: `App.xaml` drops `StartupUri`; `App.xaml.cs` opens `MainWindow` only without `--sim`. The sim path must not create a window or need a display.

## Phase 5: Tests (`OPEN-LRD.SmokeTests`)

Register new test files in `Program.cs` and the csproj. Run with `dotnet run --project OPEN-LRD.SmokeTests/OPEN-LRD.SmokeTests.csproj`.

- `ReasoningTests.cs`: fake-model turn sequencing, determinism, RC and conversation-map round-trips, v3 to v4 load, hook staging and atomicity.
- `LinkLearningTests.cs`: finite differences (path lengths 1-3), search optimality vs brute force, ties and cap, zero-weight edges, structural links skipped, reverse-stored links updated once, penalty behavior, convergence, atomicity, `ResetCore()` clearing.
- `PhysicsSimTests.cs`: `VecN`, integrators, FFT, N-body checks, law round-trip, run recorded and read back, config validation errors.

## Milestones

1. BP-3 math tests pass (Stream 2).
2. A1.6-A1.9 kernel and numerical checks pass (Stream 2).
3. PS-0 and PS-2 tests pass (Stream 3).
4. BP-1 and BP-2 merged (Stream 1).
5. Phase 3 format v4 with v3 to v4 round-trip test.
6. Phase 4 hook and BP-4 to BP-6 with feedback tests.
7. PS-1 graph bridge and `--sim` mode.
8. A1.11 16k gate passes (done). Phase 1 remainder (A1.1-A1.5) and Phase 2 RC decoder remain.
9. Phase 5 test suite complete.

## Open items

1. 2B architecture (blocks Track A format work).
2. Phase 6 (answer promotion) is out of scope; design not settled.
3. Decide whether `.plans/ReasoningCache.md` and `.plans/BackPropagation_Plan.md` are deleted or kept as history.
4. Physics Phase 3 modules (fields, rigid bodies, fluids, spacetime) need separate approval.
5. Release note required for the v3 to v4 penalty reset.

## Risks

- Stream 1 conflicts: serialize edits to the hotspot files listed above.
- The 16k gate passed in this pass (8 to 9 tok/s). It was measured with random weights and 4-step samples. Re-check on the real model once the Phase 1 forward pass is in place.
- A 2B model may not match the paper's quality. Speed is measured; quality is not yet evaluated.
- Dimension-generic physics costs speed. The 3D fast path is required.
- Graph writes are globally locked. Physics writes only summaries to the graph.
- Verify `ShouldStoreReverse`, the link removal path, and untrusted staging method names before writing code that depends on them.
