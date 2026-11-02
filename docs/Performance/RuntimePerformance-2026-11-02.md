# Snapshot memory and CPU investigation

Date: 2026-11-02

## Outcome and scope

Snapshot projection now renders sorting identities once, sorts compact indices with stable ties, shares predicate labels within a projection, and allocates the exact edge array size. Complete results remain byte-identical on the measured corpora. This is an internal materialization change: public APIs, RDF formats, engine packages, graph ownership, locks, and inference/search semantics remain unchanged.

Memory is the primary acceptance criterion. On these local workloads, allocation traffic falls 33–36% and retained managed memory per complete snapshot falls 39–54%. The larger cases also use 20–26% less process CPU per operation. The small metadata case trades about 0.2 ms of CPU for its memory reduction. These percentages apply to snapshot projection, not the entire application or canonical RDF graph.

The requested ManagedCode `blazing-fast-dotnet` skill is installed under `.codex/skills`, version `1.0.0+source.ab150faed96c3ae2`, catalog release `2026.11.2.0`. Roslynk 3.0.0 semantic inspection and dotnet-diagnostics-mcp 0.28.0 profiling were used. The configured Roslynk connector failed generically, so the installed documented stdio bridge ran on a dedicated port without changing user MCP configuration.

## Measurement contract

Host: macOS/arm64, SDK 10.0.403, runtime 10.0.14, Release builds. No new runtime switches, unsafe code, SIMD intrinsics, production dependencies, or persistent caches were added.

`scripts/profile-snapshot-projection.cs` builds realistic Markdown corpora with front matter, tags, headings, and body text. It performs ten snapshot warmups and five batches of twenty projections. Each operation checks cardinality; complete serialized JSON identity is checked outside each timed batch. The metadata-only mode uses extraction `None`; the token case explicitly uses `Tiktoken`.

One identical probe/dependency set runs with either the preserved original library DLL or the optimized DLL in separate repository-owned directories. Clean comparisons use fresh processes in A/B/B/A order. Tables use medians of the ten batch measurements per implementation, without an attached profiler. The second clean series also reads `Process.TotalProcessorTime` around each batch: aggregate process user/kernel CPU, including runtime/GC work, rather than elapsed time or sampled stack frequency. This is a repeated-counter investigation, not a BenchmarkDotNet statistical confidence claim.

| Library | Module version ID | DLL SHA-256 |
| --- | --- | --- |
| Original, captured before the projection edit | `0e501506-d38a-43ea-bcb9-cb0d654fc111` | `FF355208ECA75F6C25CB049B86F3E559CEF699348A1E69FDB62E6CD7EA9C77D2` |
| Optimized | `4b8d9876-bc8d-4146-86ef-d100b269dc29` | `BD229B88AA47BBD576814E9BFEBC2A37448A3992BC09564EA2E12BF483D0BFB5` |

The first prototype moved rendered records while sorting and increased small-case latency. It was replaced by sorting integer indices with readonly record comparisons. Only the final implementation remains. Earlier preliminary timings are superseded by the controlled comparisons below.

## Clean projection costs

Primary CPU/time evidence: `artifacts/runtime-performance-20261102/snapshot-cpu-comparison.json` and `cpu-clean-*/*.metrics.json`. Allocation agrees with the first independent clean series in `snapshot-comparison.json` (small differences include background runtime traffic).

| Corpus | Triples | Elapsed ms, before → after | CPU ms, before → after | Allocation bytes/op, before → after |
| --- | ---: | ---: | ---: | ---: |
| 250 metadata documents | 1,544 | 1.717 → 1.911 | 1.724 → 1.922 | 1,027,618 → 689,815 |
| 2,500 metadata documents | 15,044 | 13.013 → 10.521 | 14.309 → 11.449 | about 9.85–9.88 million → 6.63 million |
| 120 Tiktoken documents | 12,650 | 9.351 → 7.062 | 16.837 → 12.504 | 7,277,037 → 4,681,184 |

The first clean series observed elapsed medians of 1.769 → 2.015, 14.608 → 12.535, and 10.365 → 8.110 ms respectively. Absolute timing varies with host/runtime state; both series show the same memory improvement and small-versus-large timing trade-off. CPU can exceed elapsed time because it sums work across process threads.

Full snapshot SHA-256 values match across both implementations, clean series, and memory repetitions:

| Corpus | Serialized result SHA-256 |
| --- | --- |
| Metadata 250 | `EFE912F36A28D909172E0A1D4A45F6E972E8334DB7E922AF0610DD33090BD5C7` |
| Metadata 2,500 | `BA0FA737C5033362F59426F8B50B066FFD236D94E7A398E8C1816C588711D11E` |
| Tiktoken 120 | `65008858ED52A4D86BF32E41F4F9E20723A8940AB6DD95CB3C69422233D873A8` |

## Retained heap and peak RAM

Each memory experiment runs one case in a fresh process, warms up ten projections, then holds ten complete snapshots. A forced managed collection before/after isolates the additional retained snapshot heap; the already-live graph and reference snapshot are excluded from the delta. Forced GC is diagnostic-only. Two repetitions per implementation/case are retained. A fresh Python wrapper launches exactly one dotnet child and records macOS `getrusage(RUSAGE_CHILDREN).ru_maxrss` in bytes after exit, independently of the managed heap measurement.

| Corpus | Retained managed bytes/snapshot, before → after | Reduction | Median OS peak RSS MiB, before → after |
| --- | ---: | ---: | ---: |
| Metadata 250 | 582,629 → 269,350 | 53.8% | 79.53 → 79.39 |
| Metadata 2,500 | 5,145,891 → 2,420,012 | 53.0% | 150.77 → 141.26 |
| Tiktoken 120 | 3,867,894 → 2,356,016 | 39.1% | 169.21 → 162.00 |

Peak RSS includes runtime/JIT/tokenizer initialization and the graph. The small process peak is effectively unchanged. Metadata-2,500 samples were 145.63/155.91 MiB before and 141.98/140.53 MiB after. Tiktoken peak samples overlap substantially (162.19/176.23 before, 152.06/171.94 after), so its modest median peak difference is not a robust whole-process reduction claim. Retained snapshot heap and allocation traffic provide the clearer result.

Raw evidence: `memory-*/*.memory.json`, `memory-*/os-peak.json`, `snapshot-comparison.json`. The repository-owned `run-snapshot-matrix.py` records the process isolation and aggregation procedure. The probe's `memory` mode can also be run directly.

## Profiling and ownership

The fresh post-plan full baseline passed all 436 tests. A real Release TUnit host also passed 436 tests under bounded allocation/GC/counter capture. In its eight-second allocation sample, `CreateGraphSnapshot` accounted for 3.52% exclusive / 14.54% inclusive allocation-stack occurrences. Those are stack occurrence percentages, not CPU or allocated-byte percentages. Roslynk traced the projection callers and confirmed the removed private helpers belonged to that path.

Steady schema search attributed 94.91% inclusive allocation-stack occurrence to dotNetRDF. The main stacks include RDF set/string/trie work and DISTINCT. This does not establish an engine defect; the official engine and query semantics were retained. The real test-host sample also shows substantial Newtonsoft JSON-LD cloning cost, left unchanged.

Matched snapshot-only captures use the same metadata-2,500 case, explicit process identities, eight-second allocation/GC collectors, and unchanged complete result hash. Raw MCP requests/responses live in `diagnostics-session-3/`; the original and optimized library MVIDs are recorded with the workload. The original allocation sample had 51,440 events; the optimized sample 40,267. Throughput differs, so fixed-window aggregate bytes/event counts are not treated as per-operation savings. Clean counters above establish that comparison. `CompactUri` falls from 12.24% to 0.04% exclusive allocation-stack occurrence, consistent with predicate-label reuse. Official RDF enumeration becomes a larger share of the remaining sampled allocations.

GC captures observed 715 versus 553 completed collections, but both report unreliable suspension evidence and detail caps. The original has orphan boundaries; the optimized has right-censored boundaries. Only 200 event rows are retained per capture, with 515/353 later collection rows omitted and additional heap-stat omissions. No exact pause or normalized GC improvement is claimed.

### CPU collection limits

The MCP sampler misclassified these JIT targets as NativeAOT and explicitly refused EventPipe CPU sampling as `UnsupportedRuntime`. Runtime counters show active JIT compilation. Official dotnet-trace 10.0.750501 successfully recorded managed thread-time over another complete 436-test run, but that includes parked waits (notably semaphore/monitor waits) and is not kernel on-CPU attribution. It must not justify lock changes or a claim of zero CPU cost.

Native Xcode Time Profiler attach could not resolve the owned PID; an owned-process launch attempt then failed with `Error connecting to DTServiceHub`. These failed captures provide no CPU samples. Aggregate CPU comparisons above instead come from process CPU accounting in clean fresh processes. Raw failure logs and the managed thread-time trace/report are preserved.

Two old interactive probes subsequently failed on shutdown because their probe DLL was overwritten while they were still alive during the CPU-harness refresh. This was a diagnostic orchestration error, not a library regression. The allocation captures completed before the refresh. Both clean CPU lanes launched fresh processes after copying finished; all memory measurements had already completed in exited processes. Do not reuse a live probe binary when repeating the experiment.

## SIMD assessment

Existing `KnowledgeGraphCommonAffixTrimmer` already uses portable `Vector<ushort>` for common prefixes/suffixes, with scalar handling for short inputs and mismatches. Its loads stay inside the minimum input length. No new SIMD loop is justified by the measured projection path, which sorts string identities and materializes RDF objects. Framework string/span operations remain preferable where they preserve the contract; see the [official .NET SIMD guidance](https://learn.microsoft.com/en-us/dotnet/standard/simd).

`TokenVector.CalculateDotProduct` walks sparse dictionary entries and probes the other dictionary by token ID. A direct dense `TensorPrimitives.Dot` replacement would require a different representation, extra memory, and review of floating-point accumulation order/ranking ties. That does not fit the current memory-first change. No dense vectors or additional package were introduced. A future numeric optimization needs a representative sparse-vector BenchmarkDotNet comparison and exact ranking/threshold invariants before implementation.

All 439 tests pass in separate normal and `DOTNET_EnableHWIntrinsic=0` processes, including real fuzzy BM25 and token-distance flows. This verifies the existing scalar fallback on this arm64 host; it does not claim x64/AVX qualification.

## Verification and residual uncertainty

Three new integration flows passed on the original projection before editing, then on the optimized implementation. They prove label precedence/whitespace handling, blank and object-only nodes, typed-literal edge multiplicity, stable ordering, reversed Markdown input, empty input, and real query/search results. This is a performance-only change, so quantitative acceptance and existing-behavior invariants replace a deliberately failing behavioral test.

- Full coverage run: 439 passed, zero failures/skips. Library line coverage: **96.60%** (11,672/12,083); changed projection file: **100% line and branch coverage**.
- Separate final normal run: 439 passed; separate scalar run: 439 passed, no skipped tests.
- Roslynk final semantic/analyzer diagnostics: zero errors/warnings.
- Full format verification, final canonical build, and `git diff --check`: passed.
- Final outdated and direct/transitive vulnerability scans: no outdated or vulnerable packages in any of the three projects.

The first coverage validation had one `NullReferenceException` in `System.Uri.AbsoluteUri` while constructing a graph in `Bm25_fuzzy_token_matching_is_opt_in_for_one_edit_typo_queries(kache restpre)`, before snapshot projection. It did not recur in the complete coverage rerun or subsequent complete scalar/normal suites. Its cause is unconfirmed; it was not suppressed, retried inside a test, or attributed to a confirmed dependency defect. Preserve this residual uncertainty and the first failing log (`final-tests-coverage.log`), rather than claiming a proven repair for that event.

## Dependencies and delivery

The earlier authorized package update is committed as `aa6491b`:

| Dependency | Old → new |
| --- | --- |
| ManagedCode.Storage.Core | 10.1.7 → 10.1.10 |
| ManagedCode.Storage.FileSystem | 10.1.7 → 10.1.10 |
| ManagedCode.Storage.VirtualFileSystem | 10.1.7 → 10.1.10 |
| TUnit | 1.76.45 → 1.78.0 |

No additional NuGet dependencies were changed for this optimization. The existing intentional central transitive pin `AngleSharp` 1.9.0 remains; no new pin was introduced. Veritas evaluation source/artifacts were removed under the cleanup request. The owner requested committing the complete current working-tree scope, including the optimization, installed skill, tests, diagnostic probe, documentation, and existing tool-manifest/architecture edits. Raw profiling artifacts remain local and ignored by Git.

Further candidates are source-level query construction/parsing reuse and JSON-LD clone avoidance with explicit ownership guarantees. Neither is implemented or claimed as a measured improvement here. Existing focused/semantic projections remain the caller-controlled way to request a smaller view.
