# AGENTS.md

Project: ManagedCode.MarkdownLd.Kb.Benchmarks
Purpose: BenchmarkDotNet performance suite for Markdown-LD Knowledge Bank.

## Entry Points

- `Program.cs` for BenchmarkDotNet discovery and execution.
- `MarkdownLdBenchmarkConfig` for jobs, exporters, diagnosers, and optional profiling.
- `*Benchmarks.cs` for benchmark scenarios.
- `BenchmarkCorpusFactory` for deterministic generated Markdown corpora.

## Boundaries

- This project is a benchmark adapter only. It must not be referenced by production or test projects.
- Benchmarks must remain deterministic and network-free by default.
- Remote federation benchmarks are not allowed here; use local federated service bindings.
- BenchmarkDotNet artifacts are written under `artifacts/benchmarks`.
- Benchmarks are not correctness tests and must not replace TUnit flow tests.

## Commands

- build: `(cd ../.. && dotnet build MarkdownLd.Kb.slnx --no-restore)`
- fuzzy: `(cd ../.. && dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*FuzzyEditDistanceBenchmarks*")`
- graph-build: `(cd ../.. && dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphBuildBenchmarks*")`
- graph-search: `(cd ../.. && dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphSearchBenchmarks*")`
- tiktoken-search: `(cd ../.. && dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*TiktokenSearchBenchmarks*")`
- persistence: `(cd ../.. && dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphPersistenceBenchmarks*")`
- lifecycle: `(cd ../.. && dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*GraphLifecycleBenchmarks*")`
- profile: `(cd ../.. && MARKDOWN_LD_KB_BENCHMARK_PROFILE=cpu dotnet run --project benchmarks/MarkdownLd.Kb.Benchmarks -c Release -- --filter "*FuzzyEditDistanceBenchmarks*")`

## Applicable Skills

- `dotnet-tunit` remains the repository test runner skill; use it for correctness verification after benchmark changes.

## Local Risks

- Benchmark results are machine- and load-dependent. Do not hard-code performance assertions into TUnit tests.
- Profiler output can be large. Keep profiling opt-in through `MARKDOWN_LD_KB_BENCHMARK_PROFILE=cpu`, `gc`, or `jit`.
- Keep benchmark files under the repository LOC limits just like production and test code.
