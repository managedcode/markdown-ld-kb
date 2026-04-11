# AGENTS.md

Project: Markdown-LD Knowledge Bank
Stack: .NET 10, C#, RDF/SPARQL, Markdown knowledge graph extraction, Microsoft.Extensions.AI `IChatClient` extraction port

Follows [MCAF](https://mcaf.managed-code.com/).

---

## Purpose

This repository is a .NET 10 library port of the `lqdev/markdown-ld-kb` technology.
Its product goal is to turn Markdown knowledge-base files into an RDF/JSON-LD knowledge graph and provide graph search/query capabilities over that graph.

The implementation MUST be usable as a library first. CLI, app, or agent integrations are secondary adapters around the library contracts.

Target capabilities:

- discover and load Markdown documents from a folder tree
- parse Markdown into deterministic chunks with source metadata
- extract entities, relationships, and assertions from Markdown content
- provide an LLM extraction adapter through `Microsoft.Extensions.AI.IChatClient` from the first implementation slice
- emit RDF triples and JSON-LD/Turtle serializations
- query the graph with SPARQL and higher-level graph search APIs
- keep AI extraction behind explicit ports and use `Microsoft.Extensions.AI.IChatClient` as the LLM boundary from the start
- consider Microsoft Agent Framework only as a higher-level orchestration adapter around the `IChatClient` boundary, not as the core graph dependency

## Solution Topology

- Solution root: `/Users/ksemenenko/Developer/markdown-ld-kb`
- Upstream reference submodule: `external/lqdev-markdown-ld-kb`
- Planned solution file: `MarkdownLd.Kb.slnx`
- Planned production projects:
  - `src/MarkdownLd.Kb`
- Planned test projects:
  - `tests/MarkdownLd.Kb.Tests`
- Required architecture map:
  - `docs/Architecture.md`

## Rule Precedence

1. Read this solution-root `AGENTS.md` first.
2. Read the nearest local `AGENTS.md` for the area you will edit.
3. Apply the stricter rule when both files speak to the same topic.
4. Local `AGENTS.md` files may refine or tighten root rules, but they must not silently weaken them.
5. If a local rule needs an exception, document it explicitly in the nearest local `AGENTS.md`, ADR, or feature doc.

## Durable Project Rules

- Keep the core Markdown-to-graph pipeline deterministic and testable without network access.
- Treat LLM/entity extraction as an adapter behind a small interface and implement that adapter through `Microsoft.Extensions.AI.IChatClient` from the start.
- It is allowed for the production library to reference `Microsoft.Extensions.AI.Abstractions`; concrete OpenAI/Azure/Foundry providers must remain app-level dependencies unless an ADR says otherwise.
- The product/library name is `Markdown-LD Knowledge Bank`; do not rename it to a shorter marketing name.
- The C# root namespace, assembly identity, and package ID MUST be `ManagedCode.MarkdownLd.Kb`.
- Prefer RDF/SPARQL standards and proven .NET RDF libraries over hand-rolled RDF parsing or query engines.
- Keep the upstream Python/JavaScript/reference repository as a read-only submodule; do not patch it as part of the C# port.
- User-facing docs and public API comments are written in English.
- Every non-trivial feature must include flow-level integration tests, not property-only tests.
- Repository line coverage must be 95% or higher after production code exists.
- Tests must verify the real Markdown -> graph -> query/search flow, including success, malformed input, and empty/no-match paths where relevant.
- Tests must not use mocks, stubs, or fakes, except one local test implementation of `Microsoft.Extensions.AI.IChatClient` used to prove the LLM extraction boundary without network access.
- Use TUnit for tests and Shouldly for assertions.

## Global Skills

List only the skills this solution actually uses.

- `mcaf-testing` — use for test strategy, coverage gates, and flow-level verification.
- `mcaf-adr-writing` — use when adding or changing architecture decisions such as RDF library choice, AI adapter boundaries, or SPARQL execution strategy.

`.NET` skill-management rules:

- `.NET` skills are sourced from `https://skills.managed-code.com/`.
- `mcaf-dotnet` is the entry skill and routes to specialized `.NET` skills when installed.
- Keep exactly one framework skill when installed: `mcaf-dotnet-tunit`.
- Add tool-specific `.NET` skills only when the repository actually uses those tools in CI or local verification.
- Keep only `mcaf-*` skills in agent skill directories.
- When upgrading skills, recheck `build`, `test`, `format`, `analyze`, and `coverage` commands against the repo toolchain.

## Rules to Follow (Mandatory)

### Commands

- `restore`: `dotnet restore MarkdownLd.Kb.slnx`
- `build`: `dotnet build MarkdownLd.Kb.slnx --no-restore`
- `test`: `dotnet test MarkdownLd.Kb.slnx --no-build`
- `format`: `dotnet format MarkdownLd.Kb.slnx --verify-no-changes`
- `coverage`: `dotnet test MarkdownLd.Kb.slnx --collect:"XPlat Code Coverage"`

`.NET` runner policy:

- Target framework: `net10.0`.
- Use the SDK default C# language version unless an ADR documents a different value.
- Test framework: TUnit.
- Assertion library: Shouldly.
- Test runner model: TUnit through `dotnet test` / Microsoft.Testing.Platform-compatible execution.
- Coverage: Coverlet XPlat collector unless a later ADR moves coverage to `coverlet.MTP` or another .NET 10-compatible collector.

### Project AGENTS Policy

- Multi-project solutions MUST keep one root `AGENTS.md` plus one local `AGENTS.md` in each project or module root.
- Each local `AGENTS.md` MUST document:
  - project purpose
  - entry points
  - boundaries
  - project-local commands
  - applicable skills
  - local risks or protected areas
- If a project grows enough that the root file becomes vague, add or tighten the local `AGENTS.md` before continuing implementation.

### Maintainability Limits

These limits are repo-configured policy values. They live here so the solution can tune them over time.

- `file_max_loc`: `400`
- `type_max_loc`: `200`
- `function_max_loc`: `50`
- `max_nesting_depth`: `3`
- `exception_policy`: `Document any justified exception in the nearest ADR, feature doc, or local AGENTS.md with the reason, scope, and removal/refactor plan.`

Local `AGENTS.md` files may tighten these values, but they must not loosen them without an explicit root-level exception.

### Task Delivery

- Start from `docs/Architecture.md` and the nearest local `AGENTS.md` when they exist.
- Treat `docs/Architecture.md` as the architecture map for every non-trivial task.
- If the overview is missing, stale, or diagram-free, update it before implementation.
- Define scope before coding:
  - in scope
  - out of scope
- Keep context tight. Do not read the whole repo if the architecture map and local docs are enough.
- If the task matches a skill, use the skill instead of improvising.
- Analyze first:
  - current state
  - required change
  - constraints and risks
- For non-trivial work, create a root-level `<slug>.brainstorm.md` file before making code changes.
- Use `<slug>.brainstorm.md` to capture the problem framing, options, trade-offs, risks, open questions, and the recommended direction.
- After the brainstorm direction is chosen, create a root-level `<slug>.plan.md` file.
- Keep the `<slug>.plan.md` file as the working plan for the task until completion.
- The plan file MUST contain:
  - a link or reference to the chosen brainstorm
  - task goal and scope
  - detailed ordered implementation steps
  - constraints and risks
  - explicit test steps as part of the ordered plan
  - the test and verification strategy for each planned step
  - the testing methodology for the task
  - an explicit full-test baseline step after the plan is prepared
  - a tracked list of already failing tests, with one checklist item per failing test
  - root-cause notes and intended fix path for each failing test that must be addressed
  - a checklist with explicit done criteria for each step
  - ordered final validation skills and commands, with reason for each
- Use the Ralph Loop for every non-trivial task:
  - brainstorm before code changes
  - plan in detail before implementation
  - run the full relevant test suite after the initial plan to establish the real baseline
  - track any pre-existing failures in the plan file
  - execute one planned step at a time
  - mark checklist items in the plan as work progresses
  - review findings, apply fixes, and rerun relevant verification
  - update the plan and repeat until done criteria are met or an explicit exception is documented
- Implement code and tests together.
- Run verification in layers:
  - changed tests
  - related suite
  - broader required regressions
- If `build` is separate from `test`, run `build` before `test`.
- After tests pass, run `format`, then the final required verification commands.
- The task is complete only when every planned checklist item is done and all relevant tests are green.
- Summarize the change, risks, and verification before marking the task complete.

### Documentation

- All durable docs live in `docs/`.
- `docs/Architecture.md` is the required global map and the first stop for agents.
- `docs/Architecture.md` MUST contain Mermaid diagrams for:
  - system or module boundaries
  - interfaces or contracts between boundaries
  - key classes or types for the changed area
- Keep one canonical source for each important fact. Link instead of duplicating.
- Update feature docs when behaviour changes.
- Update ADRs when architecture, boundaries, or standards change.
- For non-trivial work, the plan file, feature doc, or ADR MUST document the testing methodology:
  - what flows are covered
  - how they are tested
  - which commands prove them
  - what quality and coverage requirements must hold
- Every feature doc under `docs/Features/` MUST contain at least one Mermaid diagram for the main behaviour or flow.
- Every ADR under `docs/ADR/` MUST contain at least one Mermaid diagram for the decision, boundaries, or interactions.
- Mermaid diagrams are mandatory in architecture docs, feature docs, and ADRs.
- Mermaid diagrams must render. Simplify them until they do.

### Testing

- TDD is the default for new behaviour and bug fixes: write the failing test first, make it pass, then refactor.
- Bug fixes start with a failing regression test that reproduces the issue.
- Every behaviour change needs new or updated automated tests with meaningful assertions.
- Tests must prove the real user flow or caller-visible system flow, not only internal implementation details.
- Prefer integration tests over isolated unit tests when behaviour crosses boundaries.
- Test names must describe the scenario and expected outcome.
- Do not write trivial tests that only prove a property can be set or a file can be opened.
- Use realistic Markdown fixtures that exercise front matter, headings, links, assertions, malformed data, and graph queries.
- Avoid mocks, fakes, stubs, or service doubles in final verification tests.
- Deterministic adapters are allowed only when they represent an explicit non-network test contract and the production boundary remains unchanged.
- Flaky tests are failures. Fix the cause.
- Changed production code MUST reach at least 95% line coverage.
- Critical flows and public contracts MUST reach at least 95% line coverage with explicit success and failure assertions.
- Repository or module coverage must not decrease without an explicit written exception.
- Coverage is for finding gaps, not gaming a number. Coverage numbers do not replace scenario coverage or user-flow verification.
- The task is not done until the full relevant test suite is green, not only the newly added tests.
- After changing production code, run the repo-defined quality pass: format, build, focused tests, broader tests, coverage, and any configured extra gates.

### Code and Design

- Everything in this solution MUST follow SOLID principles by default.
- Every class, object, module, and service MUST have a clear single responsibility and explicit boundaries.
- SRP and strong cohesion are mandatory for files, types, and functions.
- Prefer composition over inheritance unless inheritance is explicitly justified.
- Large files, types, functions, and deep nesting are design smells. Split them or document a justified exception under `exception_policy`.
- Avoid magic values. Extract shared values into constants, enums, configuration, or dedicated types.
- Production code MUST NOT use inline string literals, including regex patterns, RDF/SPARQL tokens, namespace URIs, YAML keys, prompt fragments, or error messages.
- Production code may use named `const string` or `static readonly` string fields and must reference those symbols everywhere else.
- Test code may use string literals only as test data, expected values, or fixture content.
- Keep public contracts small, named, and stable.
- Design boundaries so real behaviour can be tested through public interfaces.
- The repo-root `.editorconfig` is the source of truth for formatting, naming, style, and analyzer severity.
- Do not let IDE defaults, pipeline flags, and repo config disagree.

### Critical

- Never commit secrets, keys, or connection strings.
- Never skip tests to make a branch green.
- Never weaken a test or analyzer without explicit justification.
- Never introduce mocks, fakes, stubs, or service doubles to hide real behaviour in final verification.
- Never introduce a non-SOLID design unless the exception is explicitly documented under `exception_policy`.
- Never force-push to `main`.
- Never approve or merge on behalf of a human maintainer.

### Boundaries

Always:

- Read root and local `AGENTS.md` files before editing code.
- Read the relevant docs before changing behaviour or architecture.
- Run the required verification commands yourself.

Ask first:

- changing public API contracts after they are released
- modifying persistence formats after fixtures depend on them
- deleting code files

Allowed without asking when needed for this repository goal:

- adding NuGet dependencies for Markdown parsing, RDF/SPARQL, testing, coverage, or .NET library packaging
- creating solution, source, test, docs, and fixture files
- adding the upstream reference repository as a read-only Git submodule

## Preferences

### Likes

- English for project code, docs, test names, and public APIs.
- Flow-level tests that prove Markdown-to-graph-to-query behaviour.

### Dislikes

- Trivial tests that only check property assignment or raw file IO.
- Network-dependent core tests.
- Provider-specific LLM dependencies in the core library when `IChatClient` abstractions are enough.
