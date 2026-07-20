# Graph Normalization Brainstorm

Date: 2026-07-20

## Problem framing

Markdown-LD Knowledge Bank currently canonicalizes and merges exact assertion keys, but malformed model output can still be dropped without a caller-visible warning, self-loops are not rejected, reverse duplicates of symmetric relationships can survive, and directed workflow cycles are not prevented. A Tiktoken build also intentionally materializes section, segment, topic, and retrieval-neighbor facts in the complete RDF graph. Exposing that complete retrieval graph as an operator-facing knowledge graph can produce more than one thousand apparent connections for a single document even when the authored knowledge graph is small.

The required result is a deterministic library-first graph boundary that preserves retrieval behavior while guaranteeing a clean authored/semantic projection and reporting every normalization decision that changes extracted or authored edges.

## Scope

### In scope

- Normalize assertion edges before RDF materialization.
- Merge exact duplicate edges while preserving confidence and all provenance.
- Treat `kb:relatedTo` as symmetric and keep one deterministic orientation for reverse duplicates.
- Reject malformed edge endpoints, unsupported predicates, and self-loops with structured warnings.
- Keep configured directed predicates acyclic; `kb:nextStep` is acyclic by default.
- Prefer higher-confidence directed edges, then deterministic lexical order, when breaking cycles.
- Expose cycle search through bounded strongly connected components instead of enumerating an exponential number of simple cycles.
- Expose a semantic snapshot that excludes Tiktoken section, segment, topic, and retrieval-neighbor internals while preserving the complete graph for token-distance retrieval and RDF/SPARQL callers.
- Add caller-visible normalization warnings to the build result and its existing diagnostics stream.
- Document and fully test the real Markdown -> extraction/rules -> normalization -> graph -> semantic projection flow.
- Bump the package patch version and deliver the verified repository state.

### Out of scope

- Removing Tiktoken internals from the complete RDF graph or changing token-distance ranking.
- UI implementation such as click handlers, card focus state, or node highlighting; this repository provides the stable node and edge IDs that a consumer UI uses for that behavior.
- Treating every RDF cycle as invalid. Ontology, SKOS, provenance, and symmetric semantic relationships may legitimately contain cycles.
- Hosted services, databases, provider SDKs, or network-dependent validation.

## Options

### Option A: run generic graph cleanup over every RDF triple

This would deduplicate and cycle-check the materialized dotNetRDF graph.

Trade-offs:

- Sees every final triple.
- Cannot distinguish authored facts from ontology, SKOS, provenance, or Tiktoken retrieval internals.
- Would incorrectly remove valid RDF cycles and cannot preserve assertion confidence/provenance cleanly after materialization.

### Option B: normalize extracted assertion facts before materialization and add a separate semantic projection

Canonicalize entities and assertions, report duplicate inputs, validate graph invariants, break cycles only for declared directed predicates, then build the complete graph. Derive the semantic snapshot by excluding known Tiktoken-internal node ID namespaces and every incident edge.

Trade-offs:

- Preserves the existing complete RDF and token-distance contracts.
- Keeps confidence and provenance available when selecting duplicate or cycle-causing edges.
- Requires a small normalization report contract and a projection API.
- Tiktoken internal IDs are repository-owned stable namespaces, so projection can filter them deterministically without guessing from generic RDF types such as `schema:CreativeWork`.

### Option C: stop materializing Tiktoken retrieval facts

Keep only the in-memory token index and authored facts in the RDF graph.

Trade-offs:

- Produces a smaller complete graph.
- Breaks the documented and tested Tiktoken graph-structure contract, persistence/export behavior, and callers that query retrieval facts through SPARQL.
- Conflates retrieval storage policy with the operator-facing projection bug.

## Recommended direction

Use Option B.

The normalization pipeline will compose the existing `KnowledgeFactMerger` with a dedicated `KnowledgeGraphNormalizer`. Exact duplicates are detected during canonical merging so confidence and provenance remain merged. The normalizer then validates URI/predicate materializability, removes self-loops, canonicalizes symmetric `kb:relatedTo` pairs, and greedily builds an acyclic edge set for configured predicates in descending confidence order. Before adding a directed edge, a reachability search checks whether the object already reaches the subject; if so, the edge is rejected and the discovered path is reported.

General cycle search will use iterative Kosaraju strongly connected components. A cyclic component is a compact, deterministic representation of one or more cycles, avoids recursive stack growth, and avoids exponential simple-cycle enumeration.

`KnowledgeGraph.ToSemanticSnapshot()` will filter repository-owned `token-section/`, `token-segment/`, and `token-topic/` node namespaces plus all incident edges. `ToSnapshot()` becomes the safe semantic/operator default so existing UI callers cannot accidentally expose retrieval internals. `ToCompleteSnapshot()` is the explicit complete graph for retrieval diagnostics.

## Risks and controls

- Legitimate cyclic predicates: default acyclic enforcement is limited to `kb:nextStep`; callers may add other directed predicates explicitly.
- Symmetric direction handling: the first authored orientation is retained for RDF compatibility, while focused search supports both inbound and outbound `kb:relatedTo` traversal.
- Warning volume: warnings are structured and deterministic; diagnostics remain caller-visible summaries/messages.
- Retrieval regression: complete snapshot, SPARQL graph, and token-distance index stay intact and existing Tiktoken flow tests must remain green.
- Projection false positives: filter exact repository-owned relative path segments, not RDF types or broad string contains checks.
- Public API drift: changes are additive and keep existing build, merge, snapshot, and serialization contracts.
- Performance: iterative Kosaraju is `O(V + E)`; normalization reachability is bounded to configured acyclic predicates and deterministic in-memory graphs.

## Open questions

No user decision is required. The feedback explicitly identifies Tiktoken internals as retrieval-only for operator visualization, and `kb:nextStep` is the only current built-in relationship whose semantics require a directed acyclic workflow.
