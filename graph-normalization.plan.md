# Graph Normalization Plan

Date: 2026-07-20

Chosen direction: [Graph Normalization Brainstorm](graph-normalization.brainstorm.md), Option B.

## Goal and scope

Deliver deterministic edge normalization, caller-visible warnings, bounded cycle search, and a semantic graph projection that excludes Tiktoken retrieval internals without changing complete RDF or token-distance behavior. Cover the full caller-visible flow, update architecture and feature documentation, bump the package patch version, and push the verified change to `origin/main`.

Out of scope: consumer UI click/focus implementation, removal of Tiktoken retrieval facts from the complete graph, provider integrations, hosted infrastructure, and generic removal of valid RDF/ontology cycles.

## Constraints and risks

- Preserve the library-first in-memory architecture and dotNetRDF as the RDF/SPARQL boundary.
- Keep existing public APIs source compatible; new public contracts are additive.
- Keep Tiktoken retrieval and complete snapshot behavior available.
- Normalize only extracted/authored assertion facts before materialization.
- Enforce acyclicity only for `kb:nextStep` by default.
- Preserve the highest confidence and all provenance when duplicate facts are merged.
- Keep files below 400 lines, types below 200 lines, methods below 50 lines, and nesting at three levels or less.
- Production string literals must be named constants or static fields.
- Tests use TUnit and Shouldly with real pipeline/dotNetRDF flows and no network.
- Changed production code and critical public contracts require at least 95% line coverage.
- Preserve pre-existing dependency-audit changes and verify them rather than overwriting them.

## Testing methodology

TDD drives the behavior. Flow tests first build realistic Markdown/front-matter and deterministic extraction payloads that contain exact duplicates, reverse `kb:relatedTo` duplicates, malformed edges, self-loops, and multi-node `kb:nextStep` cycles. Assertions inspect normalized facts, structured warnings, diagnostics, SPARQL results, cycle search results, complete snapshots, semantic snapshots, and token-distance availability. Focused logic tests cover iterative Kosaraju SCC boundaries and deterministic confidence/lexical cycle breaking. Existing Tiktoken structure/search tests prove that retrieval internals remain in the complete graph and token index. Coverage uses the repository Microsoft Testing Platform configuration and must remain at or above 95%.

## Ordered implementation and verification checklist

- [x] 1. Inspect architecture, local rules, source/test ownership, graph construction, Tiktoken fact shape, snapshot APIs, version metadata, and initial git state.
  - Done when the normalization boundary, retrieval projection boundary, existing dirty files, and test conventions are identified.
  - Verification: `git status --short --branch`; targeted `rg`; root/local `AGENTS.md`; `docs/Architecture.md`; relevant source and test files.
- [x] 2. Establish and record the full pre-change baseline after this plan exists.
  - Done when the full Release TUnit suite has completed and every existing failure is listed below with root-cause notes.
  - Verification: `dotnet test --solution MarkdownLd.Kb.slnx --configuration Release` in an interactive PTY with visible progress.
- [x] 3. Add failing normalization and semantic-projection flow tests.
  - Done when tests reproduce exact/reverse duplicates, invalid endpoints/predicates, self-loops, confidence-aware cycle breaking, clean DAG output, structured warnings, Tiktoken complete-versus-semantic scope, and stable node IDs for consumer navigation.
  - Verification: focused `--treenode-filter` TUnit lane; failures must be behavioral, not compilation-only placeholders.
- [x] 4. Implement canonical merge reporting and graph normalization.
  - Done when exact duplicates preserve confidence/provenance, invalid/self-loop/symmetric duplicates are removed, `kb:nextStep` is acyclic, and every removed edge produces a deterministic warning.
  - Verification: focused normalization tests followed by related pipeline tests.
- [x] 5. Implement cycle search and semantic snapshot projection.
  - Done when iterative Kosaraju SCC cycle search returns deterministic cyclic components for selected predicates, `ToSnapshot()` / `ToSemanticSnapshot()` exclude Tiktoken section/segment/topic nodes and incident edges, and `ToCompleteSnapshot()` plus token retrieval remain complete.
  - Verification: focused algorithm tests, Tiktoken projection flow, existing focused-search and Tiktoken structure tests.
- [x] 6. Update durable architecture and feature documentation.
  - Done when `docs/Architecture.md` maps normalization/projection contracts and a feature document contains a rendering Mermaid flow plus behavior/test methodology.
  - Verification: inspect diagrams and documentation links; keep one canonical fact source.
- [x] 7. Run layered verification and close coverage gaps.
  - Done when build, focused tests, related tests, full tests, and coverage are green with changed production code at least 95% covered.
  - Verification: repository-defined build/test/coverage commands and Cobertura inspection.
- [x] 8. Run final quality, dependency, security, and package gates.
  - Done when format, pack, outdated scan, vulnerability scan, and `git diff --check` pass; every outdated direct dependency is updated and no advisory remains.
  - Verification: `dotnet format MarkdownLd.Kb.slnx --verify-no-changes`; `dotnet package list --outdated`; `dotnet package list --vulnerable --include-transitive`; pack and diff checks.
- [x] 9. Bump release metadata, review, commit, and push.
  - Done when package/README versions are synchronized, the intended diff is committed, `origin/main` contains the commit, and the working tree has no unintended task changes.
  - Verification: evaluated package version, `git diff --cached`, `git status --short --branch`, `git log -1`, and `git push origin main`.

## Full-test baseline failures

- [x] Baseline is green: 322 passed, 0 failed, 0 skipped in 41 seconds. There are no pre-existing failing tests to track.

## Completion evidence

- Focused normalization, contract, chat, SHACL, Tiktoken, capability, export, and large-corpus lanes passed.
- Release build passed with zero warnings and zero errors.
- Full Release suite passed: 350 passed, 0 failed, 0 skipped.
- Repository line coverage is 96.88%; new normalization, cycle, and projection types are at least 97% covered, with the core normalizer/cycle/projection algorithms at 100% line coverage.
- Format/analyzer verification and `git diff --check` passed.
- NuGet outdated scan reports no direct updates for production, test, or benchmark projects.
- NuGet vulnerability scan reports no direct or transitive advisories.
- Package `ManagedCode.MarkdownLd.Kb.0.2.7.nupkg` was produced successfully.

## Final validation order

1. `dotnet-tunit` focused tests: quickest behavioral proof for the new caller flows.
2. Release build: validates all production, tests, and benchmark projects against the updated package graph.
3. Full Release TUnit suite: proves repository-wide behavior and catches graph/search regressions.
4. Coverage: proves the repository and changed-code 95% quality contract.
5. `dotnet format --verify-no-changes`: proves formatting/analyzer cleanliness after behavior is green.
6. Package outdated and vulnerability scans: proves dependency freshness and security ownership requirements.
7. Pack and metadata inspection: proves the bumped package artifact can be produced.
8. `git diff --check` and intentional staging review: prevents whitespace defects and scope leakage before commit/push.
