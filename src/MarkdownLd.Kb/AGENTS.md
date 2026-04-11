# AGENTS.md

Project: ManagedCode.MarkdownLd.Kb
Purpose: Production library for Markdown-LD Knowledge Bank.

## Entry Points

- `ManagedCode.MarkdownLd.Kb` namespace
- `Pipeline/MarkdownKnowledgePipeline` for end-to-end Markdown-to-graph orchestration
- `Parsing/*` for Markdown and YAML front matter parsing
- `Extraction/*` for deterministic and `IChatClient` fact extraction
- `Rdf/*` and `Query/*` for RDF graph construction, serialization, SPARQL, and search

## Boundaries

- The production library may reference `Microsoft.Extensions.AI` abstractions, Markdig, YamlDotNet, and dotNetRDF.
- Do not add provider-specific LLM SDKs here.
- Do not add Microsoft Agent Framework packages here without a new ADR.
- Do not add localhost, HTTP server, background service, database server, or hosted API dependencies. The production pipeline must build, store, query, and serialize graphs in memory.
- Do not add embedding/vector provider dependencies to the core pipeline. Future semantic/vector search must be optional and provider-neutral.
- Keep the root namespace, assembly name, and package ID as `ManagedCode.MarkdownLd.Kb`.

## Commands

- build: `dotnet build ../../MarkdownLd.Kb.slnx --no-restore`
- test: `dotnet test ../../MarkdownLd.Kb.slnx --no-build`
- coverage: `dotnet test ../../MarkdownLd.Kb.slnx --no-build --coverlet --coverlet-output-format cobertura --coverlet-include '[ManagedCode.MarkdownLd.Kb]*' --results-directory ../../TestResults/CoverletMtpFiltered`

## Local Risks

- RDF/SPARQL behaviour must be tested through real dotNetRDF execution.
- LLM extraction must use `IChatClient`; provider SDKs belong in host apps.
- Do not use inline string literals in production code. Put regex patterns, RDF/SPARQL tokens, namespace URIs, YAML keys, prompt fragments, and error messages in named constants/static readonly fields.
- Keep public contracts compact and stable.
