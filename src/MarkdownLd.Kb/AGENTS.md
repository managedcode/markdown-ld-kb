# AGENTS.md

Project: ManagedCode.MarkdownLd.Kb
Purpose: Production library for Markdown-LD Knowledge Bank.

## Entry Points

- `ManagedCode.MarkdownLd.Kb` namespace
- `Pipeline/MarkdownKnowledgePipeline` for end-to-end Markdown-to-graph orchestration
- `Documents/*` for Markdown models, parsing, and chunking
- `Extraction/*` for `IChatClient` contracts, cache, and fact processing
- `Graph/*` for graph building, metadata mapping, runtime graph APIs, and SHACL-facing graph operations
- `Tokenization/*` for explicit experimental Tiktoken local corpus graph extraction
- `Query/*` for search, SPARQL, and NL-to-SPARQL
- `Rdf/*` for the lower-level RDF helper and serialization surface

## Boundaries

- The production library may reference `Microsoft.Extensions.AI` abstractions, Markdig, YamlDotNet, and dotNetRDF.
- Do not add provider-specific LLM SDKs here.
- Do not add Microsoft Agent Framework packages here without a new ADR.
- Do not add localhost, HTTP server, background service, database server, or hosted API dependencies. The production pipeline must build, store, query, and serialize graphs in memory.
- Do not add embedding/vector provider dependencies to the core pipeline. Future semantic/vector search must be optional and provider-neutral.
- Keep the root namespace, assembly name, and package ID as `ManagedCode.MarkdownLd.Kb`.
- Do not implement silent substitution paths. Invalid parsing, extraction, graph, or query inputs must fail explicitly or be skipped by a documented validation rule with tests.
- Do not keep old leftover implementation paths in this project. Remove obsolete code instead of wrapping or preserving it.

## Commands

- build: `dotnet build ../../MarkdownLd.Kb.slnx --no-restore`
- test: `(cd ../.. && dotnet test --solution MarkdownLd.Kb.slnx --configuration Release)`
- coverage: `(cd ../.. && dotnet test --solution MarkdownLd.Kb.slnx --configuration Release -- --coverage --coverage-output-format cobertura --coverage-output "$PWD/TestResults/TUnitCoverage/coverage.cobertura.xml" --coverage-settings "$PWD/CodeCoverage.runsettings")`

## Local Risks

- RDF/SPARQL behaviour must be tested through real dotNetRDF execution.
- LLM extraction must use `IChatClient`; provider SDKs belong in host apps.
- Do not use inline string literals in production code. Put regex patterns, RDF/SPARQL tokens, namespace URIs, YAML keys, prompt fragments, and error messages in named constants/static readonly fields.
- Keep public contracts compact and stable.
