# Markdown-LD Knowledge Bank

Markdown-LD Knowledge Bank is a .NET 10 library for turning Markdown knowledge-base files into an in-memory RDF graph that can be searched and queried with read-only SPARQL.

It ports the core idea from [lqdev/markdown-ld-kb](https://github.com/lqdev/markdown-ld-kb) into a C# library package. The runtime is local and in-memory: no localhost server, no Azure Functions host, no database server, and no hosted graph service are required.

## What It Does

```mermaid
flowchart LR
    Source["Markdown, MDX, text, JSON, YAML, CSV"] --> Converter["KnowledgeSourceDocumentConverter"]
    Converter --> Parser["MarkdownDocumentParser"]
    Parser --> Deterministic["Deterministic fact extraction"]
    Parser --> Chat["Optional IChatClient extraction"]
    Deterministic --> Merge["Fact merge and canonicalization"]
    Chat --> Merge
    Merge --> Graph["dotNetRDF in-memory graph"]
    Graph --> Search["SearchAsync"]
    Graph --> Sparql["SELECT and ASK SPARQL"]
    Graph --> Export["Turtle and JSON-LD"]
```

The pipeline extracts:

- article identity, title, summary, dates, tags, authors, and topics from YAML front matter
- heading sections and document identity from Markdown
- Markdown links such as `[SPARQL](https://www.w3.org/TR/sparql11-query/)`
- optional wikilinks such as `[[RDF]]`
- optional assertion arrows such as `article --mentions--> RDF`
- optional LLM-produced entities and assertions through `Microsoft.Extensions.AI.IChatClient`

## Install

```bash
dotnet add package ManagedCode.MarkdownLd.Kb --version 0.0.1
```

For local repository development:

```bash
dotnet add reference ./src/MarkdownLd.Kb/MarkdownLd.Kb.csproj
```

## Minimal Example

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class MinimalGraphDemo
{
    private const string SearchTerm = "rdf";
    private const string NameKey = "name";
    private const string RdfLabel = "RDF";

    private const string ArticleMarkdown = """
---
title: Zero Cost Knowledge Graph
description: Markdown notes can become a queryable graph.
tags:
  - markdown
  - rdf
author:
  - Ada Lovelace
---
# Zero Cost Knowledge Graph

Markdown-LD Knowledge Bank links [RDF](https://www.w3.org/RDF/) and [SPARQL](https://www.w3.org/TR/sparql11-query/).
""";

    private const string SelectFactsQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?article ?entity ?name WHERE {
  ?article a schema:Article ;
           schema:name "Zero Cost Knowledge Graph" ;
           schema:keywords "markdown" ;
           schema:mentions ?entity .
  ?entity schema:name ?name ;
          schema:sameAs <https://www.w3.org/RDF/> .
}
""";

    public static async Task RunAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline();

        var result = await pipeline.BuildFromMarkdownAsync(ArticleMarkdown);

        var graphRows = await result.Graph.ExecuteSelectAsync(SelectFactsQuery);
        var search = await result.Graph.SearchAsync(SearchTerm);

        Console.WriteLine(graphRows.Rows.Count);
        Console.WriteLine(search.Rows.Any(row =>
            row.Values.TryGetValue(NameKey, out var name) &&
            name == RdfLabel));
    }
}
```

## Build From Files

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class FileGraphDemo
{
    private const string FilePath = "/absolute/path/to/content/article.md";
    private const string DirectoryPath = "/absolute/path/to/content";
    private const string MarkdownSearchPattern = "*.md";

    public static async Task RunAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline();

        var singleFile = await pipeline.BuildFromFileAsync(FilePath);
        var directory = await pipeline.BuildFromDirectoryAsync(
            DirectoryPath,
            searchPattern: MarkdownSearchPattern);

        Console.WriteLine(singleFile.Graph.TripleCount);
        Console.WriteLine(directory.Documents.Count);
    }
}
```

`KnowledgeSourceDocumentConverter` supports Markdown and other text-like knowledge inputs: `.md`, `.markdown`, `.mdx`, `.txt`, `.text`, `.log`, `.csv`, `.json`, `.jsonl`, `.yaml`, and `.yml`.

You do not need to pass a base URI for normal use. Document identity is resolved in this order:

- `canonicalUrl` or `canonical_url` in Markdown front matter
- the file path, normalized the same way as the upstream project: `content/notes/rdf.md` becomes a stable document IRI
- the generated inline document path when `BuildFromMarkdownAsync` is called without a path

The library uses `urn:managedcode:markdown-ld-kb:/` as an internal default base URI only to create valid RDF IRIs when the Markdown does not provide a canonical URL. Pass `new MarkdownKnowledgePipeline(new Uri("https://your-domain/"))` only when you want generated document/entity IRIs to live under your own domain.

## Optional AI Extraction

Optional AI extraction enriches the deterministic Markdown graph with entities and assertions returned by an injected `Microsoft.Extensions.AI.IChatClient`. The package stays provider-neutral: it does not reference OpenAI, Azure OpenAI, Anthropic, or any other model-specific SDK. If no chat client is provided, the pipeline still runs fully locally and builds the graph from Markdown/front matter/link extraction only.

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Microsoft.Extensions.AI;

internal static class AiGraphDemo
{
    private const string ArticlePath = "content/entity-extraction.md";

    private const string ArticleMarkdown = """
---
title: Entity Extraction RDF Pipeline
---
# Entity Extraction RDF Pipeline

The article mentions Markdown-LD Knowledge Bank, SPARQL, RDF, and entity extraction.
""";

    private const string AskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  ?article a schema:Article ;
           schema:name "Entity Extraction RDF Pipeline" ;
           schema:mentions ?entity .
  ?entity schema:name ?name .
}
""";

    public static async Task RunAsync(IChatClient chatClient)
    {
        var pipeline = new MarkdownKnowledgePipeline(chatClient: chatClient);

        var result = await pipeline.BuildFromMarkdownAsync(
            ArticleMarkdown,
            path: ArticlePath);

        var hasAiFacts = await result.Graph.ExecuteAskAsync(AskQuery);
        Console.WriteLine(hasAiFacts);
    }
}
```

The built-in chat extractor requests structured output through `GetResponseAsync<T>()`, normalizes the returned entity/assertion payload, merges it with deterministic facts, and then builds the same in-memory RDF graph used by search and SPARQL. Tests use one local non-network `IChatClient` implementation so the full extraction-to-graph flow is covered without a live model.

## Query And Export

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class QueryDemo
{
    private const string SelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?article ?title WHERE {
  ?article a schema:Article ;
           schema:name ?title ;
           schema:mentions ?entity .
  ?entity schema:name "RDF" .
}
LIMIT 100
""";

    private const string SearchTerm = "sparql";
    private const string ArticleKey = "article";
    private const string TitleKey = "title";

    public static async Task RunAsync(MarkdownKnowledgeBuildResult result)
    {
        var rows = await result.Graph.ExecuteSelectAsync(SelectQuery);
        foreach (var row in rows.Rows)
        {
            Console.WriteLine(row.Values[ArticleKey]);
            Console.WriteLine(row.Values[TitleKey]);
        }

        var search = await result.Graph.SearchAsync(SearchTerm);
        var turtle = result.Graph.SerializeTurtle();
        var jsonLd = result.Graph.SerializeJsonLd();

        Console.WriteLine(search.Rows.Count);
        Console.WriteLine(turtle.Length);
        Console.WriteLine(jsonLd.Length);
    }
}
```

SPARQL execution is intentionally read-only. `SELECT` and `ASK` are allowed; mutation forms such as `INSERT`, `DELETE`, `LOAD`, `CLEAR`, `DROP`, and `CREATE` are rejected before execution.

## Thread Safety

`KnowledgeGraph` is safe for shared in-memory read/write use through its public API. Search, read-only SPARQL, and serialization run under a read lock; `MergeAsync` snapshots a built graph and merges it under a write lock.

Use this when many workers convert Markdown independently and publish their results into one graph:

```csharp
var shared = await pipeline.BuildFromMarkdownAsync(string.Empty);
var next = await pipeline.BuildFromMarkdownAsync(markdown, path: "content/note.md");

await shared.Graph.MergeAsync(next.Graph);
var rows = await shared.Graph.SearchAsync("rdf");
```

## Markdown Conventions

```markdown
---
title: Markdown-LD Knowledge Bank
description: A Markdown knowledge graph note.
datePublished: 2026-04-11
tags:
  - markdown
  - rdf
author:
  - Ada Lovelace
about:
  - Knowledge Graph
---
# Markdown-LD Knowledge Bank

Use [RDF](https://www.w3.org/RDF/) and [SPARQL](https://www.w3.org/TR/sparql11-query/).
```

Optional advanced predicate forms:

- `mentions` becomes `schema:mentions`
- `about` becomes `schema:about`
- `author` becomes `schema:author`
- `creator` becomes `schema:creator`
- `sameas` becomes `schema:sameAs`
- `relatedTo` becomes `kb:relatedTo`
- prefixed predicates such as `schema:mentions`, `kb:relatedTo`, `prov:wasDerivedFrom`, and `rdf:type` are preserved
- absolute predicate URIs are preserved when valid

## Architecture Choices

- `Markdig` parses Markdown structure.
- `YamlDotNet` parses front matter.
- `dotNetRDF` builds the RDF graph, runs local SPARQL, and serializes Turtle/JSON-LD.
- `Microsoft.Extensions.AI.IChatClient` is the only AI boundary in the core pipeline.
- Embeddings are not required for the current graph/search flow.
- Microsoft Agent Framework is treated as host-level orchestration for future workflows, not a core package dependency.

See [docs/Architecture.md](docs/Architecture.md), [ADR-0001](docs/ADR/ADR-0001-rdf-sparql-library.md), and [ADR-0002](docs/ADR/ADR-0002-llm-extraction-ichatclient.md).

## Inspiration And Attribution

This project is inspired by Luis Quintanilla's Markdown-LD / AI Memex work:

- [lqdev/markdown-ld-kb](https://github.com/lqdev/markdown-ld-kb) - upstream Python reference repository
- [Zero-Cost Knowledge Graph from Markdown](https://lqdev.me/resources/ai-memex/blog-post-zero-cost-knowledge-graph-from-markdown/) - core idea for using Markdown, YAML front matter, LLM extraction, RDF, JSON-LD, Turtle, and SPARQL
- [Project Report: Entity Extraction & RDF Pipeline](https://lqdev.me/resources/ai-memex/project-report-entity-extraction-rdf-pipeline/) - extraction and RDF pipeline context
- [W3C SPARQL Federated Query](https://github.com/w3c/sparql-federated-query) - SPARQL federation reference material
- [dotNetRDF](https://github.com/dotnetrdf/dotnetrdf) - RDF/SPARQL engine used by this C# implementation

The original repository is kept as a read-only submodule under `external/lqdev-markdown-ld-kb`. This package ports the technology and API direction into a reusable .NET library instead of copying the Python repository layout.

## Development

```bash
dotnet restore MarkdownLd.Kb.slnx
dotnet build MarkdownLd.Kb.slnx --configuration Release --no-restore
dotnet test --solution MarkdownLd.Kb.slnx --configuration Release
dotnet format MarkdownLd.Kb.slnx --verify-no-changes
dotnet test --solution MarkdownLd.Kb.slnx --configuration Release -- --coverage --coverage-output-format cobertura --coverage-output "$PWD/TestResults/TUnitCoverage/coverage.cobertura.xml" --coverage-settings "$PWD/CodeCoverage.runsettings"
```

Current verification baseline:

- tests: 69 passed, 0 failed
- line coverage: 96.06%
- branch coverage: 85.22%
- target framework: .NET 10
- package version: 0.0.1
