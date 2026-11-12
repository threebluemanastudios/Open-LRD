# Open-LRD
Open source Large Relational Database Model.

Light Weight Model written in Native C# with no external library dependencies.

Still a major work-in-progress.


This project is currently divided into several "modules".


Module 1: The Graph Backend (Core and Data Storage) - Complete.

This module contains the definitions for the Node and Link classes as well as methods for creating nodes, linking them together, storing them in a dictionary, and searching through the dictionary.
It also manages the temporary Node Lifecyle, incrementing its promotion weight when a temporary node reference can be verified, and if the Time-To-Live (TTL) expires before that node reaches the promotion threshold it is purged.


Module 2: The Reasoning Engine - Complete.

This module runs trust scale calculations and performs the Subgraph matching to integrate new node chains into the node web dictionary and extract response node chains.


Module 3: The NLP Pipeline (Ingestion/Output) - In Progress (Phase 9 Additional Tools).

This module will take raw text input, tokenize it, and translate the grammatical structures into temporary chains to pass to the Reasoning Engine. It will also take response chains from the Reasoning Engine and convert them back to user readable text.

Parsing, named-entity recognition, relation extraction, chain construction, Reasoning Engine ingestion, question analysis, and answer generation are complete: normalization, tokenization, POS tagging, lemmatization, shallow parsing, semantic typing, predicate link-type registration, entity resolution, trust prechecks, idempotent chain ingestion, question variants, wh and yes/no answers, evidence text, ambiguity/no-match diagnostics, conversation context, third-person pronoun resolution, focus tracking, shared conversation IDs, and session provenance are implemented and covered by smoke tests.

Phase 7 added a dedicated NLP Workbench window for end-to-end Ingest and Ask workflows, conversation creation and attachment, reset/removal/clear controls, and read-only inspection of diagnostics, evidence, frames, mentions, focus, and session provenance. Phase 8 adds configurable first-person speaker identity, subjectless read-only commands in the Ask pane (output, print, respond, and reply), and Tools-button gating until Launch Core succeeds; its manual WPF verification is complete. Phase 9 adds versioned .lrdlog export and import in the Tools window, separate transient-chain companion logs, Merge/Replace conflict policies, and link distance definitions for the future visualizer. Manual Phase 9 log checks remain before Module 3 is marked complete.

NLP Workbench workflow:

1. Launch Core. Tools and NLP Workbench remain disabled until the core is initialized.
2. Open NLP Workbench.
3. Ingest a statement, optionally with source and topic node IDs. First-person forms resolve to the configured speaker node (default "User").
4. Ask a follow-up question and inspect the answer, diagnostics, evidence, and candidates.
5. Use read-only Ask commands such as "Print facts", "Output graph", "Respond summary", or "Reply where does Alice work".
6. Create or attach conversations to compare isolated and shared context.
7. Reset or remove sessions without changing graph data.

Conversation state is loaded into memory for the active core session and is lost when the application exits or the core is reinitialized. It can be exported and restored with the Tools log controls.

Tools log persistence:

- Export writes a versioned JSON snapshot named `selected-name.lrdlog`.
- With Include Transient Chains selected, query and response chains are written to `selected-name_trans.lrdlog`.
- Merge keeps existing nodes, links, conversations, and type definitions on ID conflicts and reports skipped items.
- Replace swaps the validated core or transient state after confirmation.
- Core and transient logs are imported separately. Pipeline attachments are never persisted.


Module 4: Upper Ontology (Base Knowledge)

A startup script that populates the empty graph with hardcoded reality rules, base categories, and structural links.

