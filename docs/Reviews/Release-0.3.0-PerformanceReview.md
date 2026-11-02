# 0.3.0 review: thirteen performance and memory iterations

Date: 2026-11-02

## Scope and result

The review starts at `61a5024` and retains official dotNetRDF, public signatures, RDF identity/serialization formats, in-memory ownership, and existing validation gates. The owner extended three iterations to thirteen and superseded the uncommitted patch target with **0.3.0**. No intermediate version was published.

Confirmed repairs preserve authored literal metadata, prevent duplicate concurrent cold snapshot builds, remove avoidable label allocations, filter retrieval triples before semantic materialization, transfer exclusively owned arrays into immutable snapshots, and release owned RDF/token-index resources on disposal. The speculative span-sort rewrite was rejected after measurements worsened larger-case CPU.

## Iteration ledger

| Iteration | Check, repair and verification |
| --- | --- |
| 1 | Three real Markdown regressions reproduced disappearing literal values containing `token-section/`, `token-segment/`, or `token-topic/`. Apply retrieval namespace classification only to URI nodes; all snapshot flows pass. |
| 2 | Sixteen rendezvoused cold readers allocated sixteen projections before one cache winner was published. Recheck/build under the existing upgradeable read lock after the fast read-lock check; retain concurrent warm reads and write-lock merge invalidation. |
| 3 | Remove empty label dictionaries and reuse rendered IDs for preferred-label lookup. Related projection cases pass and clean allocations decrease on all three original corpora. Make reference-snapshot GC liveness explicit in the diagnostic probe. |
| 4 | Token-heavy semantic requests previously rendered and sorted retrieval nodes that were immediately discarded. Filter triples first, classify each URI once per build, and preserve explicit unsupported-node failure. Two mixed-RDF reference flows prove labels, blank/object-only nodes, typed-literal multiplicity, query-path lookalikes and complete graph preservation. |
| 5 | Avoid rebuilding/copying immutable collections. The private projection owns fresh arrays; compact duplicate rendered edges before transferring ownership through `ImmutableCollectionsMarshal.AsImmutableArray`. Tests reject mutation and prove complete-projection arrays cannot alias the semantic cache. |
| 6 | A failing real build/search/dispose regression proved token search remained active after disposal. Clear its index and dispose the owned official RDF graph, retaining earlier immutable snapshots and idempotent disposal. Measure released managed memory while the disposed wrapper remains live. |
| 7 | Compare portable span sorting with a value comparer against the existing delegate sort. Allocation is unchanged and larger-case CPU worsens about 9–11%; reject the rewrite. Stable ordinal ties remain unchanged. |
| 8 | Run 28 normalization/malformed-input cases and 16 projection/empty/mixed-node cases. All pass; no additional behavior change justified. |
| 9 | Run multilingual weighting (3), scalar fuzzy matching (7), and CJK-aware Markdown chunking (10). All pass; retain existing portable SIMD and scalar behavior without dense token vectors. |
| 10 | Compare five corpora: 250/2,500 unique metadata documents, 120 Tiktoken documents, 2,500 repeated-value documents, and 250 Ukrainian/CJK/emoji documents. All full serialized complete-result hashes match and allocations improve. |
| 11 | Six snapshot lifetime/cache tests plus two hundred-worker/index-invalidation flows pass. New repeated merge/read races accept only old or new committed immutable snapshots and preserve every previous reader result. |
| 12 | Three real persistence/inference/full-text flows, eight focused projection cases, and three JSON-LD round-trips pass. No consumer-side engine substitution or extra repair justified. |
| 13 | A 500-operation read-only soak preserves full result identity and one warm semantic cache reference. Final Debug coverage and actual profiled Release hosts each pass all 450 tests; final compiler, formatting, packaging and dependency gates pass. |

The baseline after planning was 439/439. Regression-first failures were limited to the three new literal cases and the new disposal case. An initially incorrect test used legacy search for an untyped merged RDF node; it was corrected to the explicit SPARQL ASK contract, preserving the legacy type requirement.

## Measurements and limits

All evidence is repository-owned under `artifacts/patch-review-20261102/`. The standalone `scripts/profile-snapshot-projection.cs` supports clean repeated-counter, concurrent cold-cache, semantic, retained-memory, disposal, and soak modes. Generated Markdown includes front matter, headings, tags and body evidence. Full snapshots and hashes are retained. No profiler is attached in clean comparison lanes.

Clean comparisons preserve separate DLLs, module IDs and serialized result identity. Five batches of twenty complete projections follow ten warmups. A/B/B/A process order is used where stated in the raw matrix logs. Process CPU is aggregate user/kernel time, including runtime/GC work, and differs from elapsed time. Local small samples are not statistical confidence claims or whole-application CPU percentages.

| Complete projection corpus | Allocation bytes/op before review → after label repair |
| --- | ---: |
| Metadata 250 | 689,787 → 669,175 |
| Metadata 2,500 | 6,632,766 → 6,450,129 |
| Tiktoken 120 | 4,681,255 → 4,620,932 |
| Repeated metadata 2,500 | 6,047,024 → 6,044,573 |
| Unicode metadata 250 | 697,786 → 673,146 |

The old-binary probe initially required assembly 0.2.21 and could not load 0.2.20. That failed harness run is excluded. The same source was compiled against the original minimum assembly version, then loaded with both libraries; all five hashes match. Neither temporary binaries nor local references are used as release/consumer delivery.

For sixteen simultaneous cold metadata-2,500 readers, the pre-review median was **177,561,812 allocated bytes**, **1,245 ms process CPU**, **233 ms elapsed**, and **200,073,216 bytes OS peak RSS**. After the first three repairs it was 10,050,032 allocated bytes, about 42 ms CPU and 39 ms elapsed. Final0.3.0 measures 8,926,624 allocated bytes, 33.4 ms CPU, 31.7 ms elapsed, and 141,271,040 bytes peak RSS; raw observations are in `final-cold-comparison.json`. Peak RSS is measured by a fresh Python wrapper launching exactly one dotnet child and reading macOS child `ru_maxrss`, twice per implementation.

Early token filtering reduces one cold Tiktoken semantic request from **5,178,656 to 3,564,920 allocated bytes** with identical semantic hash `056B4E7B62925C08A0C0B16EB194749BF233F653C17C53F82205F54A8B70D576`. Immutable ownership removes another **812,096 bytes** from the 2,500-document metadata semantic build (9,212,464 → 8,400,368). These are distinct scenarios and must not be added as one universal percentage.

With a disposed graph wrapper and caller-owned complete snapshot kept live, the token corpus released about **21.7 MB managed memory**, versus **59 KB** before the disposal repair. This is retained managed heap, not a claim that the OS immediately returns those pages. The official [BaseGraph disposal](https://github.com/dotnetrdf/dotnetrdf/blob/v3.5.2/Libraries/dotNetRdf.Core/Core/BaseGraph.cs#L907) delegates to its owned triple collection; the wrapper now calls that supported lifecycle.

The soak's live-heap observations were 54.9 MB initially, about 65.9 MB in intermediate batches, and 56.8 MB finally. Growth was not monotonic over the bounded run; five tiny log rows remain live. This does not prove absence of all possible leaks. Explicit `GC.KeepAlive` protects the reference snapshot in retained-memory diagnostics; no production forced GC or runtime tuning was added.

## Diagnostics and qualification

Applied skills: `blazing-fast-dotnet`, `dotnet-diagnostics-profiling`, Roslynk, `test-anti-patterns`, and `dotnet-tunit`. Host: macOS/arm64, SDK 10.0.403/runtime 10.0.14. Diagnostics MCP 0.28.0 ran through the official SDK stdio helper without global MCP configuration changes. Roslynk 3.0.0 inspected the owning solution and reports zero compiler/analyzer errors or warnings.

The pre-repair real Release host PID32171 passed all 439 original tests and failed exactly the three added literal regressions. Final version-0.3.0 Release host PID44993 passed **450/450**, no skips, under aligned eight-second allocation/GC/counter capture. Original requests/responses, process identities and binary SHA are retained in `diagnostics-1/` and `profiled-030-process.json`. Captures identify allocation call sites, not a controlled full-suite performance percentage: windows, scheduling and added tests differ.

The final collector observed 37,418 allocation events with sampled weighted bytes4,143,856,376. GC observed505 completed collections but marks suspension evidence unreliable (orphan/right-censored boundaries). Only200 detailed collection rows are retained, with305 later collection and306 heap-stat rows omitted. No exact GC pause reduction is claimed. Raw quality/loss/censoring fields remain in the saved responses.

Explicit EventPipe CPU sampling again returned `UnsupportedRuntime`: the tool classified this `dotnet ...Tests.dll` JIT host as NativeAOT. macOS has no supported OS backend in this installed MCP version. That refusal is preserved and is not zero CPU evidence. Clean process CPU counters provide the numerical CPU comparisons; no unavailable stack attribution is invented.

Final Debug line coverage is **96.61%** (11,700/12,111). Changed snapshot materialization is100% line/branch covered; semantic projection is97.14% line and96.43% branch covered. Critical immutable/data-preservation and lifecycle flows include positive and failure assertions. Formatting, final canonical build, Release build/test, version evaluation, package identity/content and diff checks pass. The real package ID/version is `ManagedCode.MarkdownLd.Kb` **0.3.0**.

No NuGet dependency versions changed in this review batch. The existing intentional central transitive pin `AngleSharp`1.9.0 remains; no new pins or advisories were introduced. Final outdated and direct/transitive vulnerability scans are clean for all three projects. The earlier Storage/TUnit dependency changes are recorded in the prior performance report and commit `aa6491b`.

The previous isolated `System.Uri.AbsoluteUri` failure did not recur in the fresh baseline or final full suites; its root cause remains unconfirmed and no speculative workaround is claimed. The release is delivered through the existing main workflow and verified on GitHub/NuGet before the dependent MCPGateway update begins.
