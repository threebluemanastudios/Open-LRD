# Open-LRD Project Outline

## Purpose

Open-LRD is a lightweight cognitive knowledge-graph project written in native C# with no external library dependencies. 


The goal is a self-organizing semantic memory that can ingest language, represent concepts and relationships as a directed graph, reason over temporary and permanent knowledge, and return explainable answers.


## Core Architecture

- **Nodes** represent words, symbols, ideas, images, events, numbers, or raw data streams.
- **Links** are directed, typed relationships between nodes.
- Link classes and forward/inverse link types are thread-safe runtime registries with stable integer IDs, so new types can be registered without changing consumers.
- Heavy payloads are stored outside the node; the node keeps metadata and a pointer to the data.
- Relationship types are themselves concepts, allowing the graph to define and extend its own vocabulary over time.
- Knowledge is split between **permanent core memory** and **temporary working memory**.
- Temporary nodes and links expire unless repeated use promotes them to permanent knowledge.
- Trust, provenance, confidence, and usage weight determine whether new knowledge survives.

## Module Order

| Order | Module | Purpose | Status |
|---|---|---|---|
| 1 | The Graph Backend (Core and Data Storage) | Nodes, links, storage, search, memory lifecycle | Complete |
| 2 | The Reasoning Engine | Subgraph matching, trust, contradiction resolution, response extraction | Complete |
| 3 | The NLP Pipeline (Ingestion/Output) | Text-to-graph ingestion and graph-to-text responses | Complete (Phases 1–12) |
| 4 | Upper Ontology (Base Knowledge) | Hardcoded foundational concepts, structural rules, and curated GCIDE lexical loading | Complete (Phases 1-7) |
| 5 | Maintenance Reasoning Engine | Optional graph consolidation, relation induction, superseded-link handling, and metadata-driven graph formation | Complete (Phases 1-2) |
| 6 | Conversational Ask and Natural-Language Responses | Social dialogue, contextual follow-ups, multi-answer synthesis, and natural evidence phrasing | Complete (Phases 1-4) |
| 7 | Event Representation and Chronological Questions | Reified Event nodes, temporal windows, variance, and date/time Ask searches | Complete |
| Tools | Additional Tools & 3D Visualizer | Log persistence, large text file import, and native 3D visualizer | Complete (Phases 1-7) |

## Module 1: The Graph Backend

**Purpose:** Provide the foundational data model and memory lifecycle.

### Responsibilities

- Define node and link types, identifiers, metadata, and storage.
- Store nodes in fast lookup structures and maintain directed connections.
- Support search, traversal, connection validation, and maintenance operations.
- Distinguish temporary nodes from permanent nodes.
- Track usage weight, time-to-live, and promotion state.
- Remove expired temporary knowledge that has not earned permanence.
- Keep link handling safe: no invalid endpoints, self-links, or accidental duplicates.

### Key Design Rules

- Node IDs must be globally unique; GUIDs avoid collision-prone sequential IDs.
- Nodes may store data directly or reference external data.
- Links are directed and carry a relationship type plus link classification.
- Base relationship types are predefined, but new relationship types can be introduced as concepts.
- Link-type registry lookups are case-insensitive, thread-safe, and preserve the IDs used by persisted and in-memory links.
- Permanent knowledge is not casually overwritten or deleted.
- Temporary knowledge acts as working memory and a cognitive cache.
- High-use knowledge can be promoted; unused temporary knowledge expires.

### Current Implementation

- Node, web-node, and chain-node models.
- Value-correct typed links with persisted active/inactive state.
- Graph dictionary, search APIs, connection management, maintenance, and TTL handling.
- Verified-reference promotion and invalid-reference disposal.
- Query and response chains.
- Thread-safe runtime registries for link classes and forward/inverse link types.

## Module 2: The Reasoning Engine

**Purpose:** Integrate new knowledge into the graph and extract answers without corrupting permanent facts.

### Responsibilities

- Convert incoming chains into validated query graphs.
- Match query graphs against the existing knowledge graph.
- Score matches using node accuracy, edge accuracy, coverage, and structure.
- Detect ambiguity when multiple plausible matches exist.
- Apply source trust and topic-specific trust to incoming knowledge.
- Resolve contradictions between proposed facts and existing functional links.
- Merge approved facts, create temporary nodes, and promote trusted temporary nodes.
- Preserve provenance and record verified or invalid evidence.
- Extract bounded, ranked response paths and build response chains.
- Prevent cycles, repeated traversal, and runaway inference.

### Key Design Rules

- Matching may be partial, but low-confidence or ambiguous matches must not mutate the graph.
- Contradictions are non-destructive: losing links are invalidated rather than erased from history.
- Functional relationships may have only one active value per source and relationship class.
- Trust is statistical and domain-specific; a trusted source is not automatically trusted in every topic.
- A source's trust grows with verified evidence and falls with invalid evidence.
- Inferred facts remain temporary until usage and trust justify promotion.
- Every answer should be explainable through its source and reasoning path.

### Current Implementation

- Query graph construction and validation.
- Candidate generation and linear-chain subgraph matching.
- Trust-scale math with source and topic fallback.
- Contradiction resolution and merge planning.
- Non-destructive link invalidation and provenance actions.
- Response extraction with cycle avoidance and bounded traversal.
- Reasoning facade for ingest, answer, combined ingest-and-answer, trust inspection, and response extraction.

### Future Reasoning Extensions

- General graph matching beyond linear chains.
- Spatiotemporal reasoning over event time and location.
- Abstraction and inductive generalization from conflicting examples.
- Explicit inferred-edge confidence and reasoning traces.
- Graph refactoring, relation extraction, and graph compression during maintenance.

## Module 3: The NLP Pipeline

**Purpose:** Translate human language into temporary graph chains and translate graph responses back into readable language.

### Ingestion Path

1. Tokenize raw text into words and symbols.
2. Assign part-of-speech tags.
3. Parse grammatical dependencies to identify subjects, verbs, objects, and modifiers.
4. Perform named-entity recognition and chunk proper names into single concepts.
5. Extract relationships and qualifiers from the sentence structure.
6. Convert the result into temporary nodes, links, and a query graph.
7. Use surrounding context to disambiguate entities and word senses.
8. Pass the temporary chain to the Reasoning Engine for matching and merging.

### Output Path

1. Convert the user's question into a query chain.
2. Let the Reasoning Engine find matching knowledge.
3. Rank candidate facts and response paths by relevance, trust, and usefulness.
4. Group broad results into categories when clarification is needed.
5. Generate a human-readable answer from the selected response chain.
6. Clearly distinguish explicit source-backed facts from inferred conclusions.

### Key Design Rules

- Lexical nodes and semantic nodes are separate; a name can refer to multiple concepts.
- Entity disambiguation uses the surrounding mini-graph, not just string equality.
- Selectional restrictions and type hierarchies reject nonsensical relationships.
- An event-centric structure handles facts with time, place, participants, and activities.
- The pipeline must not invent certainty; missing source support must be stated honestly.

### Current Status

Complete. Module 3 Phases 1–12 implement normalization, tokenization, POS
tagging, lemmatization, shallow parsing, named-entity recognition, relation
extraction, entity resolution, chain construction, trusted ingestion, question
analysis, answer generation, conversation context, pronoun resolution, session
provenance, and the NLP Workbench. Later Module 6 and 7 phases extend Ask with
social dialogue, conversational preambles, multi-answer synthesis, event
representation, temporal variance, and chronological queries.

`NlpPipeline.Analyze(...)` parses text into tokens, chunks, clauses, typed
mentions, and relation frames. `NlpPipeline.Ingest(...)` resolves entities,
builds temporary chains, applies the trust precheck, records per-chain session
provenance, updates conversation focus, and merges through the Reasoning Engine.
First-person forms resolve to the configured speaker node; ordinary graph
questions that require unsupported second-person coreference are still reported
as unsupported. `NlpPipeline.Ask(...)` resolves supported contextual pronouns,
builds bounded query variants, uses read-only graph reasoning, and returns
evidence-backed wh/yes-no answers. It also supports conversational and temporal
event queries as described in Modules 6 and 7. Subjectless Output/Print/Respond/
Reply commands remain read-only.

`NlpWorkbench` provides a UI-independent facade for conversation creation,
attachment by ID, reset, removal, clear-all, Ingest, Ask, and read-only state
snapshots. `NlpWorkbenchWindow` shows diagnostics, candidates, evidence, frames,
chains, mentions, focus, and session provenance. The Tools window handles log
import/export, large text-file ingestion, ontology loading, and maintenance; the
3D Visualizer is a separate fullscreen WPF window. Conversation state is held in
memory and can be persisted in `.lrdlog` files. See `Module_3_Plan.txt`,
`Module_6_Plan.txt`, `Module_7_Plan.txt`, and `AdditionalTools.txt` for details.

Large text-file import in Tools closes the per-call NLP limits gap. A file is
split paragraph-first, then by sentence, and finally by a safe hard split into
chunks bounded by the configurable character and sentence budgets. All chunks
share one `NlpPipeline` and conversation, so pronoun and focus resolution carry
across adjacent chunks. The importer validates optional Source/Topic GUIDs
before mutating the graph, creates or reuses a permanent `Source` node with a
file `DataPointer` when Source is blank, continues after unsupported or failing
chunks, reports per-chunk counts, and supports a strictly read-only dry run plus
configurable file-size and chunk-count safety limits. Limit failures are retried
by splitting the chunk at a sentence boundary up to a depth of three. Partial
knowledge is retained by design; cancellation stops after the current chunk.

### NLP Workbench Workflow

1. Launch Core. Tools and NLP Workbench remain disabled until the core is initialized.
2. Open NLP Workbench.
3. Ingest a fact, optionally supplying source and topic node IDs. First-person
   forms resolve to the configured speaker node. The input box clears after a
   successful ingest (status `Ingested`); it is kept on diagnostics or failures.
4. Ask a follow-up question.
5. Inspect answer evidence, diagnostics, candidates, frames, mentions, and
   session provenance.
6. Run read-only Ask commands such as "Print facts", "Output graph",
   "Respond summary", or "Reply where does Alice work".
7. Create or attach conversations to compare isolated and shared context.
8. Reset or remove sessions without changing graph data.

## Module 4: Upper Ontology

**Purpose:** Bootstrapping an empty graph with the minimum foundational knowledge needed to learn.

### Responsibilities

- Define base concepts such as Entity, Event, Time, Location, and other top-level categories.
- Define structural relationships such as IS_A, PART_OF, CAUSES, HAS_PROPERTY, PARTICIPATED_IN, HAS_TIME, HAS_LOCATION, and IS_NAMED.
- Establish type hierarchies and domain/range expectations for relationships.
- Provide rules that distinguish real, fictional, conditional, and hypothetical knowledge.
- Provide startup population routines for a new graph.
- Support curated loading from reputable sources through the NLP/ETL pipeline.
- Give new facts enough structure to attach to the graph without hardcoding specific world facts.

### Key Design Rules

- The Upper Ontology contains foundational rules, not an encyclopedia of facts.
- Base concepts and structural links are permanent.
- Dynamic concepts and relationship types can be learned above this foundation.
- The ontology should prevent obvious category errors and backward relationship chaining.
- Fictional and real knowledge should be separable through explicit type paths.

### Current Status

Complete. The hardcoded upper ontology (base concepts, structural relations,
type hierarchy, and reality rules) and the curated GCIDE ETL that loads the
bundled dictionary corpus are implemented. The foundation bootstrap is
permanent and idempotent; the GCIDE loader parses the `CIDE.*` files into
lexical word nodes and semantic sense nodes with synonym, antonym, hypernym,
part-of-speech, and definition links. Loading is on demand and persists through
the versioned `.lrdlog` export and import path. The Tools window exposes Seed
Upper Ontology, Load GCIDE Lexicon (subset, letter, and entry cap with progress
and cancel), and Export Ontology Log actions. Nineteen foundational structural
relations occupy reserved IDs 1-19; `antonym` stays at the Module 3 reservation
(2,000,000). The original Module 4 delivery added eleven ontology checks. The
current integrated suite has 167 smoke tests and 287 automated UI checks. The
Visualizer layout is deterministic and reports progress and cancellation. See
`Module_4_Plan.txt` for the ontology plan, `AdditionalTools.txt` for persistence
and text import, and `Visualizer_Plan.txt` for the visualizer implementation.

## Module 5: Maintenance Reasoning Engine

**Purpose:** Consolidate duplicate graph entities and apply bounded, explainable
relation inference during an explicitly enabled maintenance run.

- The no-options `LRD_Core.RunMaintenanceCycle()` preserves TTL/disposal-only
  behavior. `RunMaintenanceCycle(MaintenanceOptions)` enables selected reasoning
  stages after the regular lifecycle pass.
- Node consolidation uses normalized labels/aliases and explicit synonym,
  identity, and shared-name evidence. Equal non-null DataPointers force a merge
  even across node types; different non-null DataPointers prevent a merge.
- Relation induction uses a fixed composition rule set and repeated two-hop
  patterns above a configurable frequency threshold. New pattern relations are
  registered and represented by Relation nodes when enabled.
- Superseded generic links are marked inactive rather than deleted. Inactive
  edges are preserved in `.lrdlog` files but hidden from active reasoning and
  the 3D Visualizer.
- Per-cycle node, link, path, merge, relation-type, and pruning budgets prevent
  unbounded graph scans or growth. The Tools window provides explicit controls
  for each maintenance pass and displays a cycle report.
- The Node Metadata mode materializes label, aliases, NodeType, and DataPointer
  fields as reusable typed value nodes and keyed links. `IngestNodeMetadata`
  runs immediately; the optional maintenance rescan reconciles changed fields,
  keeping stale facts inactive for audit while preserving other link origins.
  Generated DataPointer resource values do not carry the owner's pointer, so
  they cannot trigger identity-based node merging.
- Modules 5-7 and NLP conversational coverage bring the complete suite to 167
  passing smoke checks and 287 passing automated UI checks.

See `Module_5_Plan.txt` for the detailed phased design and acceptance criteria.

## Module 6: Conversational Ask and Natural-Language Responses

**Purpose:** Make Ask more natural while retaining evidence-backed, read-only
question answering.

- Social dialogue handles greetings, well-being questions, thanks, farewells,
  identity, and capability requests without changing graph or conversation state.
- Conversational preambles such as "Can you tell me where Alice works?" and
  "Do you know if Alice is a doctor?" unwrap by default; punctuation-free,
  preposition-fronted wh, and inverted yes/no questions are also accepted.
- "Tell me about Alice" summarizes incoming and outgoing active graph facts.
  "What about Bob?" can reuse a prior subject-question pattern and that pattern
  survives conversation-log round trips.
- Distinct matching answers are synthesized into readable lists and evidence
  sentences (for example, "Alice and Bob work at Acme."). Candidate node details
  and evidence paths remain available in the Workbench's Evidence / Candidates
  table. Duplicate-label and ambiguous-anchor cases remain explicit.
- Evidence phrasing handles classifications, adjective properties, part/whole,
  location, and participation relations; no-match responses name the missing
  information when possible.
- Conversational behaviors are configurable through NlpOptions and new options
  round-trip through conversation persistence, with defaults preserved for old
  logs.
- At Module 6 completion, 165 smoke tests and 285 automated UI checks passed;
  current integrated verification is 167 smoke tests and 287 UI checks.

See `Module_6_Plan.txt` for design decisions, implementation details, and
acceptance criteria.

## Module 7: Event Representation and Chronological Questions

**Purpose:** Represent events as graph nodes with explicit participant/action
links and searchable time windows.

- Event start/end DateTimeOffset values are encoded in the existing Event node
  Label; optional event names are stored as Aliases. The base `Node` and
  `Web_Node` classes remain unchanged, and existing log persistence round-trips
  the data through Label/Aliases.
- `LRD_Core.CreateEvent` creates a reified Event and links its subject, action,
  object, optional location, participants, and time value. Normal NLP ingestion
  automatically creates/reuses Events when successful statements include
  parseable temporal markers, while retaining the existing binary facts.
- `LRD_Core.CalculateEventTimeVariance` reports past/active/future status,
  duration, endpoint variance, and friendly relative-time wording with correct
  singular/plural units.
- Ask answers `When was [event]?` and searches overlapping event windows with
  questions such as `What happened between [start] and [end]?` or `What happened
  on [date]?`. Matching Event nodes and their links populate Evidence / Candidates.
- `When did [subject] [verb] [object]?` correlates participants and action to
  matching reified events. Range parsing is restricted to event-occurrence
  questions, and intransitive events with locations do not acquire fake objects.
- `When did [subject] [verb] [object]?` correlates to matching Event nodes.
  Temporal range parsing is limited to event-occurrence questions, and normal
  temporal qualifiers remain with standard relation queries. Intransitive
  location events are extracted without inventing an object.
- Verification: 167 smoke tests and 287 automated UI checks pass.

See `Module_7_Plan.txt` for the complete design and acceptance criteria.

## Additional Tools & 3D Visualizer

- **Log Persistence (.lrdlog)**: Versioned JSON snapshots supporting separate
  core and transient query/response chains, with Merge and Replace import modes.
- **Large Text File Import**: Automated paragraph/sentence chunking, retry on
  overflow, shared conversational context, and automatic Source node provenance.
- **Native 3D Visualizer**: Standalone fullscreen 3D window rendering node
  cubes and colored link lines laid out by semantic link distance into an
  isotropic spherical volume cloud. Includes WASD/QE fly navigation, mouse look,
  nearest-label 2D overlay badges with distance culling, ray-AABB node picking
  and selection inspection, live palette material switching, and JSON settings
  persistence.

### Data Source

The bundled `gcide-0.54` dictionary corpus used by the Module 4 ETL is the GNU
Collaborative International Dictionary of English, sourced from
https://kaikki.org/dictionary/English/words/index.html and licensed under the
GNU General Public License v3.

## License

Open-LRD is released under the GNU General Public License v3 (GPLv3).

The bundled `gcide-0.54` dictionary corpus is the GNU Collaborative
International Dictionary of English, sourced from
https://kaikki.org/dictionary/English/words/index.html and licensed under the
GNU General Public License v3. See `gcide-0.54/gcide-0.54/COPYING` for its
full terms.

## Cross-Cutting Principles

- **README module order is authoritative.**
- **Graph first, reasoning second, language third, ontology fourth.**
- **Permanent knowledge is protected; temporary knowledge is disposable.**
- **All facts should carry provenance, trust, and confidence where possible.**
- **Traversal must be bounded and cycle-safe.**
- **Inference must remain explainable and must not be presented as source-backed fact.**
- **The system should be self-optimizing but not self-corrupting.**
- **Shared link-type registries support concurrent registration and lookup.**
- **The implementation remains native C# with no external libraries.**
- **The project and its bundled GCIDE corpus are GPLv3.**

## Suggested Delivery Roadmap

1. Maintain and extend the completed graph backend and reasoning engine.
2. Build the NLP ingestion path and response-generation path.
3. Add the Upper Ontology startup layer and curated knowledge-loading tools.
4. Connect the modules into an end-to-end ingest, reason, and answer loop.
5. Add stress tests for ambiguity, contradictions, cycles, trust loss, and large graphs.
6. Add maintenance passes for graph refactoring, graph compression, and inferred-memory cleanup.
