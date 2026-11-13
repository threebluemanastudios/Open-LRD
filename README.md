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
| 3 | The NLP Pipeline (Ingestion/Output) | Text-to-graph ingestion and graph-to-text responses | In Progress (Phases 1–10 complete) |
| 4 | Upper Ontology (Base Knowledge) | Hardcoded foundational concepts, structural rules, and curated GCIDE lexical loading | Complete (Phases 1-7) |

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
- Immutable value-correct links.
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

In progress. Phases 1–7 of the Module 3 implementation plan are implemented:
normalization, tokenization, POS tagging, lemmatization, shallow parsing,
named-entity recognition, relation extraction, predicate link-type registration,
entity resolution, chain construction, Reasoning Engine ingestion, question
analysis, answer generation, yes/no answers, evidence text, ambiguity and
no-match diagnostics, conversation context, third-person pronoun resolution,
focus tracking, shared conversation IDs, session provenance, and the NLP
Workbench integration, with smoke tests for the workbench facade. Phase 8 adds
configurable first-person speaker identity, read-only Ask commands, and Tools
button gating until core initialization; its manual WPF verification is
complete. Phase 9 adds versioned .lrdlog export and import in Tools, separate
transient-chain companion logs, Merge/Replace conflict policies, and link
distance definitions for the future visualizer. Manual Phase 9 log checks are
complete for export and core import; transient import is verified as a valid
no-op when the companion contains no chains.

Relation extraction also handles coordination and negation (a post-completion
enhancement). "and" produces one frame per conjunct, with subject, verb, and
object coordination composing as a cross product; "or" ingests each alternative
as a separate positive fact that shares a disjunction group. Negated complements
become ingestible "not X" concept nodes and are marked with
`NegationKind.Complement`, while negated action verbs are parsed and reported but
not ingested. Coordination and negation markers are recorded per frame in session
provenance, and the `antonym` relation is reserved for Module 4.

Two later post-completion fixes harden question answering and ingestion. Copula
questions whose wh-word asks for the entity — "Who is a douche?" — are
classified as subject questions anchored on the complement, so the query runs
`?who --is--> douche` and matches "Jeff --is--> douche", while copula
"What is Alice?" stays object-anchored on Alice. Ingestion now builds and merges
each frame's chains before moving to the next frame, so repeated mentions of one
entity in a multi-sentence text reuse a single temporary node instead of
creating duplicates that make later frames ambiguous. Smoke tests cover
"Who is a douche?", "Who is married to Cody?", and a multi-sentence ingest that
asserts one node per entity.

`NlpPipeline.Analyze(...)` parses text into tokens, chunks, clauses, typed
mentions, and relation frames. `NlpPipeline.Ingest(...)` resolves entities,
builds temporary chains, provisions the required anchor, applies the trust
precheck, records per-chain session provenance, updates conversation focus, and
merges through the Reasoning Engine. First-person forms resolve to one
conversation speaker node whose label is configured by
`NlpOptions.FirstPersonSpeakerLabel` and defaults to `User`; second-person
pronouns remain unsupported. `NlpPipeline.Ask(...)` analyzes a question,
resolves contextual pronouns, builds bounded query variants, evaluates them
with the Reasoning Engine, and generates read-only wh or yes/no answers with
session-provenance caveats when applicable. Ask also recognizes subjectless
commands beginning with output, print, respond, or reply. Commands accept a
question or the fixed targets facts, graph, conversation, summary, or
node <id>; incomplete targets return a clarification question. Commands are
read-only and do not begin a turn or update mentions, focus, or provenance.
Conversation state is owned by `LRD_Core` and shared only by pipelines
constructed with the same conversation ID; it is bounded, resettable, and never
stored in graph nodes.

`NlpWorkbench` provides a UI-independent facade for conversation creation,
attachment by ID, reset, removal, clear-all, Ingest, Ask, and read-only state
snapshots. `NlpWorkbenchWindow` uses it for the end-to-end desktop workflow and
shows diagnostics, candidates, evidence, frames, chains, mentions, focus, and
session provenance. The existing Tools window handles log export and import,
large text-file ingestion, and ontology loading; it is not converted into the
workbench, and Visualizer rendering remains planned. The visualizer's pure
snapshot, distance-layout, color, projection, camera, mesh, and settings layer is
implemented and tested ahead of the WPF window.
Conversation state is held in memory for the active core session and can be
exported to or restored from .lrdlog files. Merging keeps existing data on ID
conflicts, while replacing core state invalidates open workbench sessions.
Transient companion logs are snapshots of the chains currently held by
`LRD_Core`, not accumulated workbench history. Ingest-only sessions normally
produce empty `queryChains` and `responseChains`; importing such a file succeeds
with zero chains added. To exercise transient persistence manually, run an Ask
that produces a response path before exporting. The current workbench path
records a response chain for a successful answer but does not populate the core
query chain, so `queryChains` may remain empty even after Ask. See
`Module_3_Plan.txt` and `AdditionalTools.txt` for the full phased plans.

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
and cancel), and Export Ontology Log actions. Nineteen structural relations
occupy reserved IDs 1-19; `antonym` stays at the Module 3 reservation
(2,000,000). The smoke suite adds eleven Module 4 checks, thirteen
text-import checks, and thirty-two visualizer model/math/layout checks, for
139 passing tests in total. The distance layout is deterministic and reports
progress and cancellation. See `Module_4_Plan.txt` for the full
plan, `AdditionalTools.txt` section 16 for the text import tool, and
`Visualizer_Plan.txt` for the 3D visualizer.

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
