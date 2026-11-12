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


Module 3: The NLP Pipeline (Ingestion/Output)

This module will take raw text input, tokenize it, and translate the grammatical structures into temporary chains to pass to the Reasoning Engine. It will also take response chains from the Reasoning Engine and convert them back to user readable text.


Module 4: Upper Ontology (Base Knowledge)

A startup script that populates the empty graph with hardcoded reality rules, base categories, and structural links.

