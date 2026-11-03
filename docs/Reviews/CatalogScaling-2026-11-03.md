# Catalog scaling review

The MCPGateway Linux benchmark run reached its 180-minute limit after 10 of 23 cases. The ordinary 10,000-tool scenario took 32.5 minutes to initialize one catalog. Its first 16 measured iterations took 20–24 seconds each. Initialization allocated 93.6 GB cumulatively and retained 7.27 GB of managed memory after collection. The run kept the first eight complete CSV results and two further measured cases in its log. [Linux benchmark log](https://github.com/managedcode/MCPGateway/actions/runs/41549054292/job/125634627244)

Ordinary test and capacity work have different purposes. The gateway's functional 5,000-tool case covers serialized discovery budgets and continuation with embedding search. BenchmarkDotNet exercises graph initialization and discovery cycles at 1,000, 5,000, and 10,000 tools. The matrix in [MCPGateway PR 4](https://github.com/managedcode/MCPGateway/pull/4) isolates these six scaling cases from ordinary benchmark classes and verifies each planned timing and allocation result.

## Cold graph construction

EventPipe sampled the actual 1,000-tool fixture during gateway setup. The dominant build paths were related-segment distance selection and RDF materialization. The fixture has 20,500 segments and 3,046 distinct texts. With automatic segment links enabled, an isolated graph build took 16.5 seconds and allocated 6.22 GB. The diagnostic switch reduced this to 8.0 seconds and 4.78 GB while removing 138,020 RDF triples. The switch isolated a cost and would change the exported graph.

The repair reuses fitted vectors for identical text, caches the two exact operand orders for the most frequent repeated vectors and tracks unresolved positions in an indexed chain. This avoids repeated distance calculations and per-source copies while preserving the full top-k relation union. The cache is local to a build and caps distance storage at 16 MB. Diverse corpora can still require quadratic pair evaluation. [ADR-0011](../ADR/ADR-0011-exact-neighbor-pruning-for-repeated-segments.md)

Matched fresh-process probes used the same generated tool schema fixture and the published 0.3.0 assembly as the control. The candidate was an isolated local build for measurement. Numbers below are decimal bytes and describe the measured fixture.

| Workload | Control | Candidate |
| --- | ---: | ---: |
| 2,000 tools, graph build elapsed | 46.90 s | 22.35 s |
| 2,000 tools, graph build process CPU | 48.28 s | 23.73 s |
| 2,000 tools, graph build allocations | 12,811,569,312 B | 12,321,869,112 B |
| 2,000 tools, complete gateway index elapsed | 48.34 s | 25.23 s |
| 2,000 tools, complete gateway index allocations | 14,262,542,936 B | 13,774,569,968 B |
| 2,000 tools, managed memory retained after gateway setup GC | 1,535,089,840 B | 1,518,205,208 B |

Both builds had equal triple counts and byte-identical ordered relation facts on the 1,000- and 2,000-tool fixtures. The latter contained 1,510,449 triples and 245,513 related facts. An intermediate graph-only sample retaining the build result showed higher managed memory. The separate public gateway probe above measures the catalog lifetime after that build result is released. Neither probe establishes retained process RSS or a 10,000-tool result.

## Warm exact discovery

One warm 1,000-tool discovery/schema/invocation cycle took 2.5–2.7 seconds and allocated approximately 4.12 GB locally. The primary schema SELECT alone allocated about 4.12 GB. The same query without related metadata allocated 12.0 MB, with different result rows. One exact subject produced thousands of intermediate rows as evidence combined with provenance. The expanded diagnostic selected three bound related nodes, 1,000 related sources and five primary source bindings.

Grounding the singleton subject, changing label fallback to MINUS and scoping related metadata with subqueries did not yield a useful result-preserving improvement. The remaining query work needs a separate evidence and provenance design that preserves label priority, caller-visible context and deterministic result budgets. The cold build repair keeps the query path intact.

## Verification

Focused TUnit covers exhaustive distance and tie parity, actual generated Markdown RDF, repeated content and distinct tails. The full suite passed 451 tests with no skips. Overall line coverage was 96.63%, and all three changed production files reached 100%. Formatting, strict build and package audits passed. The existing AngleSharp 1.9.0 transitive pin is retained. The local gateway receipts under `artifacts/benchmark-ci-repair` contain the captured SPARQL, raw relation digests, and control/candidate measurements. Complete Linux benchmark qualification remains separate from the library and gateway releases.
