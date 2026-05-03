# Performance Benchmarks

Markdown-LD Knowledge Bank keeps correctness tests and performance measurements separate. TUnit flow tests prove behaviour; BenchmarkDotNet measures runtime, allocation, scaling, and profiler traces for the hot paths.

## Benchmark Boundaries

```mermaid
flowchart LR
    Developer["Developer"] --> Runner["BenchmarkDotNet runner"]
    Runner --> Config["jobs, exporters, diagnosers"]
    Config --> Results["artifacts/benchmarks"]
    Config --> Profilers["optional EventPipe profiler"]
    Runner --> Corpus["deterministic Markdown workload profiles"]
    Corpus --> Pipeline["MarkdownKnowledgePipeline"]
    Runner --> Fuzzy["bounded fuzzy distance"]
    Runner --> Graph["ranked graph and BM25 search"]
    Runner --> Tokens["Tiktoken token-distance search"]
    Runner --> Federation["local federated SPARQL binding"]
    Runner --> Persistence["snapshot, serialization, save, load, export"]
    Pipeline --> Graph
    Pipeline --> Tokens
    Graph --> Persistence
```

The benchmark project is `benchmarks/MarkdownLd.Kb.Benchmarks`. It references the production library, but production and test projects do not reference it.

## Suites

| Suite | Measures |
| --- | --- |
| `FuzzyEditDistanceBenchmarks` | Bounded bit-vector/banded edit distance against a naive Levenshtein baseline for short typo and long affix-heavy tokens. |
| `GraphBuildBenchmarks` | Markdown source to in-memory graph build time across named workload profiles. |
| `GraphSearchBenchmarks` | Graph-ranked search, BM25, BM25 fuzzy matching, schema search, focused search, and local federated schema search. |
| `TiktokenSearchBenchmarks` | Exact token-distance search and fuzzy query correction over long-document and multilingual token-heavy graphs. |
| `GraphPersistenceBenchmarks` | Snapshot creation, Turtle/JSON-LD serialization, Mermaid/DOT export, in-memory store save/load, and file save/load. |
| `GraphLifecycleBenchmarks` | One broad build/search/save/load/export lifecycle benchmark for the complete suite. |

## Workload Profiles

Benchmark parameters use named workload profiles instead of raw document-count ranges.

| Profile | Shape | Why it exists |
| --- | --- | --- |
| `ShortDocuments` | 250 compact runbook-like Markdown documents. | Normal knowledge-base retrieval and persistence pressure. |
| `LongDocuments` | 80 long recovery playbooks with repeated sections. | Long body and chunk-scan pressure without pretending the main variable is file count. |
| `LargeCorpus` | 1000 compact documents. | Scale pressure for graph build, snapshot, serialization, save, and load paths. |
| `TokenizedMultilingual` | 250 token-heavy multilingual/CJK documents. | Tiktoken and fuzzy query-correction behaviour on non-trivial tokenization input. |
| `FederatedRunbooks` | 250 SPARQL/service/runbook documents. | Local federated schema-search and service-binding query plans. |

## Commands

```bash
dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --list flat
dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*FuzzyEditDistanceBenchmarks*"
dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphBuildBenchmarks*"
dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphSearchBenchmarks*"
dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*TiktokenSearchBenchmarks*"
dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphPersistenceBenchmarks*"
dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphLifecycleBenchmarks*"
```

`MarkdownLdBenchmarkConfig` writes Markdown, CSV, and full JSON reports to `artifacts/benchmarks/results`. Those files are machine-specific and intentionally ignored by git. If the command does not pass a BenchmarkDotNet job option, the config adds one `Default` job.

## Measured Metrics

The benchmark configuration is intentionally diagnostic, not just a stopwatch. The default reports collect:

| Metric group | BenchmarkDotNet data | Why it matters |
| --- | --- | --- |
| Latency | `Mean`, `Error`, `StdDev`, `Ratio`, `RatioSD`; full JSON also keeps min, quartiles, max, percentiles, and raw measurements | Shows the cost and distribution of each public path under the same deterministic workload. |
| Allocation and GC | `Allocated`, `Alloc Ratio`, `Gen0`, `Gen1`, `Gen2` | Catches search paths that look acceptable once but become expensive under repeated calls. |
| Threading and contention | `Completed Work Items`, `Lock Contentions` | Highlights SPARQL and federation paths that schedule work or contend while executing query plans. |
| Benchmark shape | corpus profile, query scenario, runtime, platform, JIT, job, iteration counts | Keeps runs explainable and comparable without turning local numbers into a cross-machine contract. |
| Optional profiler traces | EventPipe CPU, GC, or JIT files | Gives the next level of evidence when a benchmark result points at a hot path. |

The PR validation workflow and the dedicated benchmark workflow both run the complete benchmark suite: fuzzy edit distance, graph build, graph search, Tiktoken search, graph persistence, and graph lifecycle. Both workflows upload `artifacts/benchmarks/results` as the `benchmarkdotnet-results` artifact so CI always keeps the same performance evidence shape.

Optional EventPipe profiling is opt-in:

```bash
MARKDOWN_LD_KB_BENCHMARK_PROFILE=cpu dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*FuzzyEditDistanceBenchmarks*"
MARKDOWN_LD_KB_BENCHMARK_PROFILE=gc dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphSearchBenchmarks*"
MARKDOWN_LD_KB_BENCHMARK_PROFILE=jit dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*TiktokenSearchBenchmarks*"
```

## Current Results

On May 3, 2026, local BenchmarkDotNet runs on Apple M2 Pro with .NET 10.0.5 wrote Markdown, CSV, and JSON reports to `artifacts/benchmarks/results`.

| Suite | Job | Cases | Result files |
| --- | --- | ---: | --- |
| `FuzzyEditDistanceBenchmarks` | Default | 8 | Markdown, CSV, JSON |
| `GraphBuildBenchmarks` | Default | 4 | Markdown, CSV, JSON |
| `GraphSearchBenchmarks` | Default | 54 | Markdown, CSV, JSON |
| `TiktokenSearchBenchmarks` | Default | 12 | Markdown, CSV, JSON |
| `GraphPersistenceBenchmarks` | Default | 39 | Markdown, CSV, JSON |
| `GraphLifecycleBenchmarks` | Default | 1 | Markdown, CSV, JSON |

The full local pass executed 118 BenchmarkDotNet cases.

Graph build now reports named workload profiles:

| Profile | Mean | StdDev | Allocated |
| --- | ---: | ---: | ---: |
| `ShortDocuments` | 9.462 ms | 0.0324 ms | 14.61 MB |
| `LongDocuments` | 7.509 ms | 0.0127 ms | 14.35 MB |
| `LargeCorpus` | 45.457 ms | 0.5488 ms | 57.74 MB |
| `TokenizedMultilingual` | 12.206 ms | 0.2035 ms | 17.77 MB |

Graph search exact-query mean time:

| Profile | Ranked graph | BM25 | BM25 fuzzy | Focused | Schema SPARQL | Local federated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `ShortDocuments` | 1.195 ms | 1.659 ms | 1.979 ms | 2.036 ms | 41.078 ms | 39.410 ms |
| `LongDocuments` | 0.460 ms | 1.989 ms | 1.984 ms | 0.634 ms | 13.007 ms | 14.030 ms |
| `FederatedRunbooks` | 1.317 ms | 2.022 ms | 2.041 ms | 2.244 ms | 41.528 ms | 44.219 ms |

Graph search exact-query allocated memory per operation:

| Profile | Ranked graph | BM25 | BM25 fuzzy | Focused | Schema SPARQL | Local federated |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `ShortDocuments` | 2.37 MB | 3.07 MB | 3.07 MB | 3.27 MB | 60.33 MB | 62.31 MB |
| `LongDocuments` | 1.91 MB | 3.46 MB | 3.46 MB | 1.21 MB | 20.22 MB | 22.22 MB |
| `FederatedRunbooks` | 2.54 MB | 3.52 MB | 3.52 MB | 3.48 MB | 61.10 MB | 62.65 MB |

The `ShortDocuments` exact-query diagnostic slice shows the current hot paths:

| Method | Mean | Allocated | Alloc ratio | Gen0 | Gen1 | Gen2 | Work items | Lock contentions |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Ranked graph | 1.195 ms | 2.37 MB | 1.00x | 296.8750 | 101.5625 | 0 | 0 | 0 |
| BM25 | 1.659 ms | 3.07 MB | 1.29x | 384.7656 | 142.5781 | 0 | 0 | 0 |
| BM25 fuzzy | 1.979 ms | 3.07 MB | 1.29x | 375.0000 | 125.0000 | 0 | 0 | 0 |
| Focused | 2.036 ms | 3.27 MB | 1.38x | 406.2500 | 179.6875 | 0 | 0 | 0 |
| Schema SPARQL | 41.078 ms | 60.33 MB | 25.43x | 8400.0000 | 1800.0000 | 400.0000 | 551 | 300.6000 |
| Local federated | 39.410 ms | 62.31 MB | 26.27x | 8600.0000 | 1800.0000 | 400.0000 | 552 | 326.0000 |

Allocation, GC, work-item, and lock-contention columns come directly from BenchmarkDotNet diagnosers. Treat ratios and relative pressure inside the same run as the useful signal; local numbers are diagnostics, not release-grade SLA measurements.

Persistence and export on the `LargeCorpus` profile:

| Method | Mean | StdDev | Allocated |
| --- | ---: | ---: | ---: |
| `CreateSnapshot` | 4.494 ms | 0.0045 ms | 5.18 MB |
| `SerializeTurtle` | 9.249 ms | 0.0436 ms | 18.07 MB |
| `SerializeJsonLd` | 12.371 ms | 0.0586 ms | 20.31 MB |
| `ExportMermaidFlowchart` | 5.884 ms | 0.0899 ms | 7.15 MB |
| `ExportDotGraph` | 6.039 ms | 0.0050 ms | 7.55 MB |
| `SaveTurtleToFile` | 29.641 ms | 0.1868 ms | 34.74 MB |
| `SaveJsonLdToFile` | 38.491 ms | 1.5349 ms | 37.02 MB |
| `LoadTurtleFromFile` | 35.708 ms | 0.8051 ms | 28.10 MB |
| `LoadJsonLdFromFile` | 90.663 ms | 2.9780 ms | 75.32 MB |

Broad graph lifecycle:

| Method | Mean | StdDev | Allocated | Gen0 | Gen1 | Gen2 | Work items |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `BuildSearchSaveLoadAndExport` | 55.35 ms | 3.571 ms | 54.44 MB | 6750.0000 | 2250.0000 | 750.0000 | 52.0000 |

Tiktoken token-distance search over the semantic profiles:

| Profile | Query | Exact | Fuzzy-corrected | Exact allocated | Fuzzy allocated |
| --- | --- | ---: | ---: | ---: | ---: |
| `LongDocuments` | Exact | 298.1 us | 300.2 us | 212.24 KB | 213.16 KB |
| `LongDocuments` | Typo | 334.8 us | 391.5 us | 212.88 KB | 216.30 KB |
| `LongDocuments` | NoMatch | 254.1 us | 257.1 us | 212.19 KB | 213.49 KB |
| `TokenizedMultilingual` | Exact | 219.8 us | 221.4 us | 139.18 KB | 140.30 KB |
| `TokenizedMultilingual` | Typo | 245.2 us | 267.6 us | 139.59 KB | 142.20 KB |
| `TokenizedMultilingual` | NoMatch | 182.7 us | 183.1 us | 138.91 KB | 140.15 KB |

Interpretation: ranked graph, BM25, BM25 fuzzy, focused search, and Tiktoken token-distance search are the low-latency retrieval paths. The current BM25 implementation keeps exact and fuzzy allocation close by sharing the same tokenizer, dictionary shape, bounded top-N match retention, stack-backed short-token edit-distance masks, and pooled long-token fallback rows. Tiktoken search keeps bounded top-N candidates and updates TF-IDF dictionary values without temporary key arrays. Fuzzy BM25 still costs more CPU on typo-heavy queries and should stay opt-in. Schema-aware SPARQL and local federation are explainable RDF query paths, but dotNetRDF query-plan execution keeps them materially heavier for repeated low-latency calls. JSON-LD load is the highest persistence cost in the current local run; Turtle load and snapshot/serialization are cheaper. Use ranked graph or BM25 search when the caller needs low-latency retrieval, and use schema/federation when caller-visible evidence and graph-shape constraints matter more than raw latency.

The fuzzy edit-distance suite measured the bounded bit-vector/banded path with zero allocated bytes and faster than the naive Levenshtein baseline in every measured scenario, including 376.58x faster for the long-insertion case and 172.88x faster for the long no-match case.
