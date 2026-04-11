# AGENTS.md

Project: ManagedCode.MarkdownLd.Kb.Tests
Purpose: Flow-level verification for Markdown-LD Knowledge Bank.

## Entry Points

- `Integration/*` for Markdown -> graph -> SPARQL/search/serialization flows
- `Parsing/*`, `Extraction/*`, `Rdf/*`, `Query/*`, and `Ai/*` for focused contract tests
- `Fixtures/*` for realistic Markdown test data
- `Support/TestChatClient.cs` for the only allowed non-network `IChatClient` test implementation

## Boundaries

- Do not use mocks, stubs, or fakes.
- The only exception is a local test implementation of `Microsoft.Extensions.AI.IChatClient`.
- Use TUnit for tests and Shouldly for assertions.
- Tests must assert real caller-visible behaviour, not property assignment.
- Keep test namespaces under `ManagedCode.MarkdownLd.Kb.Tests`.
- Do not encode silent substitution expectations in tests. Invalid inputs should assert explicit failures or documented skip rules, not generic replacement behaviour.
- Do not keep tests for old leftover implementation paths.

## Commands

- focused tests: `(cd ../.. && dotnet test --solution MarkdownLd.Kb.slnx --configuration Release)`
- coverage: `(cd ../.. && dotnet test --solution MarkdownLd.Kb.slnx --configuration Release -- --coverage --coverage-output-format cobertura --coverage-output "$PWD/TestResults/TUnitCoverage/coverage.cobertura.xml" --coverage-settings "$PWD/CodeCoverage.runsettings")`

## Local Risks

- Coverage must remain 95%+ for changed production code.
- Core tests must be network-free.
