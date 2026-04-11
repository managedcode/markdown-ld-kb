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
- wikilinks such as `[[RDF]]`
- Markdown links such as `[SPARQL](https://www.w3.org/TR/sparql11-query/)`
- assertion arrows such as `article --mentions--> RDF`
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
    private const string BaseUriText = "https://kb.example/";
    private const string ArticlePath = "content/zero-cost-knowledge-graph.md";
    private const string SearchTerm = "rdf";
    private const string SubjectKey = "subject";
    private const string RdfEntityUri = "https://kb.example/id/rdf";

    private const string ArticleMarkdown = """
---
title: Zero Cost Knowledge Graph
description: Markdown notes can become a queryable graph.
tags:
  - markdown
  - rdf
author:
  - label: Ada Lovelace
    type: schema:Person
entity_hints:
  - label: RDF
    type: schema:Thing
    sameAs:
      - https://www.w3.org/RDF/
---
# Zero Cost Knowledge Graph

Markdown-LD Knowledge Bank links [[RDF]] and [SPARQL](https://www.w3.org/TR/sparql11-query/).
article --mentions--> RDF
RDF --sameas--> https://www.w3.org/RDF/
""";

    private const string AskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/zero-cost-knowledge-graph/> schema:name "Zero Cost Knowledge Graph" ;
                                                    schema:keywords "markdown" ;
                                                    schema:mentions <https://kb.example/id/rdf> .
  <https://kb.example/id/rdf> schema:sameAs <https://www.w3.org/RDF/> .
}
""";

    public static async Task RunAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ArticlePath, ArticleMarkdown),
        ]);

        var graphHasExpectedFacts = await result.Graph.ExecuteAskAsync(AskQuery);
        var search = await result.Graph.SearchAsync(SearchTerm);

        Console.WriteLine(graphHasExpectedFacts);
        Console.WriteLine(search.Rows.Any(row =>
            row.Values.TryGetValue(SubjectKey, out var subject) &&
            subject == RdfEntityUri));
    }
}
```

## Build From Files

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class FileGraphDemo
{
    private const string BaseUriText = "https://kb.example/";
    private const string FilePath = "/absolute/path/to/content/article.md";
    private const string DirectoryPath = "/absolute/path/to/content";
    private const string MarkdownSearchPattern = "*.md";

    public static async Task RunAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));

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

## Optional AI Extraction

The core library depends on `Microsoft.Extensions.AI.IChatClient`, not on a provider-specific SDK. Your host application owns the concrete provider, credentials, model choice, and optional Microsoft Agent Framework orchestration.

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Microsoft.Extensions.AI;

internal static class AiGraphDemo
{
    private const string BaseUriText = "https://kb.example/";
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
  <https://kb.example/entity-extraction/> schema:mentions ?entity .
  ?entity schema:name ?name .
}
""";

    public static async Task RunAsync(IChatClient chatClient)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            new Uri(BaseUriText),
            chatClient);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ArticlePath, ArticleMarkdown),
        ]);

        var hasAiFacts = await result.Graph.ExecuteAskAsync(AskQuery);
        Console.WriteLine(hasAiFacts);
    }
}
```

The built-in chat extractor requests structured output through `GetResponseAsync<T>()` and normalizes the returned entity/assertion payload before graph construction. Tests use one local non-network `IChatClient` implementation so the full flow is covered without a live model.

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
           schema:mentions <https://kb.example/id/rdf> .
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
  - label: Ada Lovelace
    type: schema:Person
about:
  - Knowledge Graph
entity_hints:
  - label: SPARQL
    type: schema:Thing
    sameAs:
      - https://www.w3.org/TR/sparql11-query/
---
# Markdown-LD Knowledge Bank

Use [[RDF]] and [SPARQL](https://www.w3.org/TR/sparql11-query/).
article --mentions--> RDF
RDF --sameas--> https://www.w3.org/RDF/
SPARQL --relatedTo--> RDF
```

Useful predicate forms:

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
dotnet test --solution MarkdownLd.Kb.slnx --configuration Release --no-build
dotnet format MarkdownLd.Kb.slnx --verify-no-changes
dotnet test --solution MarkdownLd.Kb.slnx --configuration Release --no-build --coverlet --coverlet-output-format cobertura --coverlet-include '[ManagedCode.MarkdownLd.Kb]*' --results-directory TestResults/CoverletMtpFiltered
```

Current verification baseline:

- tests: 67 passed, 0 failed
- line coverage: 95.83%
- target framework: .NET 10
- package version: 0.0.1
