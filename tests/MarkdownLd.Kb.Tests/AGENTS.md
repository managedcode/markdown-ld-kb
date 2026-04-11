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
- Tests must assert real caller-visible behaviour, not property assignment.
- Keep test namespaces under `ManagedCode.MarkdownLd.Kb.Tests`.

## Commands

- focused tests: `dotnet test ../../MarkdownLd.Kb.slnx --no-build --filter FullyQualifiedName~ManagedCode.MarkdownLd.Kb.Tests`
- coverage: `dotnet test ../../MarkdownLd.Kb.slnx --collect:"XPlat Code Coverage"`

## Local Risks

- Coverage must remain 95%+ for changed production code.
- Core tests must be network-free.
