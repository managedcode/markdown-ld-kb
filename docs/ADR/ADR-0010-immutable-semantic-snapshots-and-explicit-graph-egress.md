# ADR-0010: Immutable semantic snapshots and explicit graph egress

Status: Accepted

The gateway's real allocation profile attributed the no-match fallback predominantly to repeated semantic snapshot materialization. Separate uninstrumented BenchmarkDotNet workloads reproduced 4.71 MB per search at 120 documents and 44.48 MB at 1000 documents. Distance profiling also identified boxed dictionary enumeration, verified separately at 56 B per distance.

Retain exactly one immutable semantic snapshot per `KnowledgeGraph`. Build it under the graph read lock, publish through atomic reference exchange and invalidate under the merge write lock. Immutable node/edge collections keep previous readers safe. Graph disposal releases the cached reference. This preserves complete semantic roles, object-only nodes, RDF identity, shared groups, ordering and no-match fallback rather than guessing a truncated retrieval neighborhood.

Internally owned token vectors retain normalized dictionaries and enumerate their concrete dictionary type. No public search scoring or ownership contract changes. Unequal-density/symmetry tests match a dense reference and confirm input dictionary mutations cannot alter owned vectors. Distance allocation becomes zero in both measured densities.

`Project` returns an independent graph with explicit subject, resource-object and optional predicate allowlists. Literal metadata belongs only to approved subjects/predicates. No automatic traversal imports unapproved evidence or blank-node chains.

Federated SPARQL supports explicit caller-owned `HttpClient` bindings through the official `SparqlQueryClient`. Hosts can require a configured HTTP client for every non-local endpoint, enabling destination/address/redirect policies without a substitute RDF engine. Local bindings stay network-free; clients remain owned by their caller.

SPIN 3.5.2 is retained as an upstream dependency, but its RDF-command and explicit rule/constraint execution surfaces are internal. No reflection bridge or duplicate engine is introduced. The verified public-API limitation is tracked in [dotnetRDF issue 911](https://github.com/dotnetrdf/dotnetrdf/issues/911).

Measured warm no-match results: 120 documents 38.27 KB and 58.23 us; 1000 documents 269.85 KB and 458.22 us. Cold graph construction and snapshot residence remain separate costs. CI/release matrices cover all benchmark suites, including both new suites. The full suite and coverage gates remain required.
