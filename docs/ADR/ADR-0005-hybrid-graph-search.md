# ADR-0005: Hybrid Graph Search Boundary

Date: 2026-04-16

## Status

Accepted

## Context

The library already had two retrieval styles:

- graph-native lexical search over RDF metadata
- Tiktoken token-distance search for local lexical structure

Neither path solves cross-language mismatch between graph content and user queries. At the same time, the repository rules forbid turning the core library into a hosted vector-search subsystem or tying it to a provider-specific embedding SDK.

## Decision

Add an optional semantic ranked-search boundary that:

- builds an in-memory semantic index from graph-native candidate text
- supports in-memory BM25 lexical ranking over the same candidate boundary
- uses `Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>`
- keeps graph results canonical
- uses semantic results only as fallback or merge inputs
- supports opt-in reciprocal-rank fusion when callers want rank-fused graph and semantic evidence
- excludes `schema:keywords` from canonical ranking

Exact BM25 stays provider-neutral and in-memory. It counts selected query terms with span-based lookup and pooled term statistics. Optional fuzzy BM25 uses bounded edit distance for typo tolerance, remains opt-in, and builds full candidate term dictionaries only when typo enumeration is requested.

## Boundaries

```mermaid
flowchart LR
    Graph["KnowledgeGraph"] --> Canonical["Canonical graph ranking"]
    Graph --> Bm25["In-memory BM25 ranking"]
    Graph --> SemanticIndex["Optional semantic index"]
    Embedder["IEmbeddingGenerator"] --> SemanticIndex
    Canonical --> Hybrid["Hybrid merge"]
    Bm25 --> Results["Ranked results"]
    SemanticIndex --> Hybrid
    Hybrid --> Results
    Hybrid --> Gateway["Gateway or host app"]
```

## Consequences

Positive:

- cross-language queries have a provider-neutral recovery path
- graph-first explainability is preserved
- BM25 gives a local lexical ranking path without embeddings, Lucene, or a database
- fuzzy BM25 can recover insertion, deletion, and substitution typos while staying opt-in
- reciprocal-rank fusion is available without making it the default merge policy
- the host application keeps ownership of embedding-provider choice

Negative:

- the library now owns a small additional search boundary
- fuzzy BM25 costs more CPU than exact BM25 because it must enumerate candidate terms
- semantic tests need a deterministic non-network embedding adapter

## Rejected Alternatives

- Semantic-only ranking: rejected because graph must remain canonical.
- Always-on fuzzy BM25: rejected because exact lexical ranking should stay the cheaper default path.
- Provider-specific embedding package in the core library: rejected by repository rules.
- External vector database integration in the library: rejected because infra belongs in the host application.
