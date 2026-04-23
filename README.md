# Markdown-LD Knowledge Bank

[![PR validation](https://github.com/managedcode/markdown-ld-kb/actions/workflows/validation.yml/badge.svg)](https://github.com/managedcode/markdown-ld-kb/actions/workflows/validation.yml)
[![Release](https://github.com/managedcode/markdown-ld-kb/actions/workflows/release.yml/badge.svg)](https://github.com/managedcode/markdown-ld-kb/actions/workflows/release.yml)
[![CodeQL](https://github.com/managedcode/markdown-ld-kb/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/managedcode/markdown-ld-kb/actions/workflows/codeql-analysis.yml)
[![NuGet](https://img.shields.io/nuget/v/ManagedCode.MarkdownLd.Kb.svg?logo=nuget)](https://www.nuget.org/packages/ManagedCode.MarkdownLd.Kb)
[![NuGet downloads](https://img.shields.io/nuget/dt/ManagedCode.MarkdownLd.Kb.svg?logo=nuget)](https://www.nuget.org/packages/ManagedCode.MarkdownLd.Kb)
[![GitHub release](https://img.shields.io/github/v/release/managedcode/markdown-ld-kb?include_prereleases&logo=github)](https://github.com/managedcode/markdown-ld-kb/releases)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Markdown-LD Knowledge Bank is a .NET 10 library for turning Markdown knowledge-base files into an in-memory RDF graph that can be searched, queried with read-only SPARQL, validated with SHACL, exported as RDF, and rendered as a diagram.

The package is a C# library implementation of the Markdown-LD knowledge graph workflow. The runtime is local and in-memory: no localhost server, no Azure Functions host, no database server, and no hosted graph service are required.

Use it when you want plain Markdown notes to become a queryable knowledge graph without making your application depend on a specific model provider, graph server, or hosted indexing service.

## What It Does

```mermaid
flowchart LR
    Source["Markdown / MDX / text\nJSON / YAML / CSV"] --> Converter["KnowledgeSourceDocumentConverter"]
    Converter --> Parser["MarkdownDocumentParser\n→ MarkdownDocument"]
    Parser --> Mode["Extraction mode\nAuto / None / ChatClient / Tiktoken"]
    Mode --> None["None\nmetadata only"]
    Mode --> Chat["ChatClientKnowledgeFactExtractor\nIChatClient"]
    Mode --> Token["Tiktoken token-distance extractor\nMicrosoft.ML.Tokenizers"]
    None --> Merge["KnowledgeFactMerger\n→ merged KnowledgeExtractionResult"]
    Chat --> Merge
    Token --> Merge
    Merge --> Builder["KnowledgeGraphBuilder\n→ RDF + ontology + SKOS graph"]
    Builder --> Search["SearchAsync"]
    Builder --> Sparql["ExecuteSelectAsync\nExecuteAskAsync"]
    Builder --> Shacl["ValidateShacl\nSHACL report"]
    Builder --> Snap["ToSnapshot"]
    Builder --> Diagram["SerializeMermaidFlowchart\nSerializeDotGraph"]
    Builder --> Export["SerializeTurtle\nSerializeJsonLd"]
```

Extraction is explicit:

- `Auto` uses `IChatClient` when one is supplied, otherwise extracts no facts and reports a diagnostic.
- `None` builds document metadata only.
- `ChatClient` builds facts only from structured `Microsoft.Extensions.AI.IChatClient` output.
- `Tiktoken` builds a local corpus graph from Tiktoken token IDs, section/segment structure, explicit front matter entity hints, and local keyphrase topics using `Microsoft.ML.Tokenizers`.

Tiktoken mode is deterministic and network-free. It uses lexical token-distance search rather than semantic embedding search. Its default local weighting is subword TF-IDF; raw term frequency and binary presence are also available. It creates `schema:DefinedTerm` topic nodes, explicit front matter hint entities, and `schema:hasPart` / `schema:about` / `schema:mentions` edges.

**Graph outputs:**

- `ToSnapshot()` — stable `KnowledgeGraphSnapshot` with `Nodes` and `Edges`
- `SerializeMermaidFlowchart()` — Mermaid `graph LR` diagram
- `SerializeDotGraph()` — Graphviz DOT diagram
- `SerializeTurtle()` — Turtle RDF serialization
- `SerializeJsonLd()` — JSON-LD serialization
- `SaveToStoreAsync(store, location, options)` — persist the graph through a graph-store abstraction
- `SaveToFileAsync(path, options)` — persist the graph as RDF
- `LoadFromStoreAsync(store, location, options)` — load a graph from a graph-store abstraction
- `LoadFromFileAsync(path, options)` — load a graph from one RDF file
- `LoadFromDirectoryAsync(path, options)` — load and merge RDF files from a directory
- `LoadFromLinkedDataFragmentsAsync(endpoint, options)` — materialize a Linked Data Fragments source into a local graph
- `ExecuteSelectAsync(sparql)` — read-only SPARQL SELECT returning `SparqlQueryResult`
- `ExecuteAskAsync(sparql)` — read-only SPARQL ASK returning `bool`
- `ExecuteFederatedSelectAsync(sparql, options)` — explicit federated read-only SPARQL SELECT with endpoint diagnostics
- `ExecuteFederatedAskAsync(sparql, options)` — explicit federated read-only SPARQL ASK with endpoint diagnostics
- `ValidateShacl()` — SHACL validation against the built-in Markdown-LD Knowledge Bank shapes
- `ValidateShacl(shapesTurtle)` — SHACL validation against caller-supplied Turtle shapes
- `SearchAsync(term)` — case-insensitive search across `schema:name`, `schema:description`, and `schema:keywords`, returning matching graph subjects as `SparqlQueryResult`
- `SearchFocusedAsync(term)` — sparse graph search that returns primary, related, and next-step matches plus a bounded focused graph snapshot
- `MaterializeInferenceAsync(options)` — explicit RDFS / SKOS / N3-rule materialization
- `BuildFullTextIndexAsync(options)` — optional Lucene-backed graph full-text index
- `ToDynamicSnapshot()` — optional dynamic graph access over dotNetRDF dynamic types

All async methods accept an optional `CancellationToken`.

## Install

```bash
dotnet add package ManagedCode.MarkdownLd.Kb --version 0.0.1
```

For local repository development:

```bash
dotnet add reference ./src/MarkdownLd.Kb/MarkdownLd.Kb.csproj
```

## Project Structure

The production source tree now follows feature-oriented slices instead of a mostly flat technical grouping:

- `src/MarkdownLd.Kb/Documents`
  `Models`, `Parsing`, and `Chunking`
- `src/MarkdownLd.Kb/Extraction`
  `Chat`, `Cache`, and `Processing`
- `src/MarkdownLd.Kb/Pipeline`
  orchestration-only files such as `MarkdownKnowledgePipeline`
- `src/MarkdownLd.Kb/Graph`
  `Build` and `Runtime`
- `src/MarkdownLd.Kb/Tokenization`
  local Tiktoken graph extraction
- `src/MarkdownLd.Kb/Query`
  `Search`, `Sparql`, and `NaturalLanguage`
- `src/MarkdownLd.Kb/Rdf`
  low-level RDF helpers and serialization

This layout mirrors [docs/Architecture.md](docs/Architecture.md) and keeps orchestration separate from parsing, extraction, graph runtime, and query capabilities.

## Minimal Example

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class MinimalGraphDemo
{
    private const string SearchTerm = "RDF SPARQL Markdown graph";

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

    public static async Task RunAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);

        var result = await pipeline.BuildFromMarkdownAsync(ArticleMarkdown);

        var search = await result.Graph.SearchByTokenDistanceAsync(SearchTerm);

        Console.WriteLine(search[0].Text);
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

`KnowledgeSourceDocumentConverter` supports Markdown and other text-like knowledge inputs: `.md`, `.markdown`, `.mdx`, `.txt`, `.text`, `.log`, `.csv`, `.json`, `.jsonl`, `.yaml`, and `.yml`. Files with unknown or missing extensions are still accepted when their bytes decode as text, and they are treated as `text/plain`. Truly unreadable binary files are either skipped during directory loads or fail explicitly with `InvalidDataException` when the caller disables skipping.

You do not need to pass a base URI for normal use. Document identity is resolved in this order:

- `KnowledgeDocumentConversionOptions.CanonicalUri` when you provide one
- the file path, normalized the same way as the upstream project: `content/notes/rdf.md` becomes a stable document IRI
- the generated inline document path when `BuildFromMarkdownAsync` is called without a path

The library uses `urn:managedcode:markdown-ld-kb:/` as an internal default base URI only to create valid RDF IRIs when the source does not provide `KnowledgeDocumentConversionOptions.CanonicalUri`. Pass `new MarkdownKnowledgePipeline(new Uri("https://your-domain/"))` only when you want generated document/entity IRIs to live under your own domain.

## Capability Graph Rules

Markdown can include deterministic graph rules in front matter. These rules are useful for capability catalogs, tool catalogs, workflow graphs, and any corpus where related and next-step nodes matter more than broad top-N search.

```markdown
---
title: Story Delete Tool
summary: Delete a story after the caller identifies the exact story item.
graph_groups:
  - Story tools
  - Delete operation
graph_related:
  - https://kb.example/tools/story-feed-detail/
graph_next_steps:
  - https://kb.example/tools/story-comments/
---
# Story Delete Tool

Use this capability to remove an existing story.
```

`graph_groups` creates `kb:memberOf` edges. `graph_related` creates `kb:relatedTo` edges. `graph_next_steps` creates `kb:nextStep` edges. For advanced graphs, use `graph_entities` and `graph_edges` to add explicit nodes and predicates. Absolute IRIs are preserved; plain labels become stable entity IRIs under the pipeline base URI.

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class CapabilityGraphDemo
{
    public static async Task RunAsync(IReadOnlyList<MarkdownSourceDocument> documents)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            new Uri("https://kb.example/"),
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);

        var result = await pipeline.BuildAsync(documents);
        var focused = await result.Graph.SearchFocusedAsync(
            "remove the selected story from the feed",
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 3,
                MaxNextStepResults = 3,
            });

        var primary = focused.PrimaryMatches[0];
        var mermaid = KnowledgeGraph.SerializeMermaidFlowchart(focused.FocusedGraph);

        Console.WriteLine(primary.Label);
        Console.WriteLine(mermaid);
    }
}
```

Use `BuildAsync(documents, KnowledgeGraphBuildOptions)` when graph rules are assembled by the host application instead of authored in Markdown front matter.

Entities with the same `schema:sameAs` target are merged before assertions are emitted, and assertion endpoints are rewritten to the chosen canonical entity IRI. This keeps the graph sparse and avoids duplicated workflow edges when callers provide multiple labels or IDs for the same outside resource.

## Ontology And SKOS Layers

`KnowledgeGraphBuilder` now builds one additive graph, not a flat triple dump:

- document and entity instance triples
- a SKOS concept scheme / concept layer for graph concepts
- repository-owned ontology declarations for `kb:` classes and properties

The implementation uses `dotNetRdf`, `dotNetRdf.Ontology`, and `dotNetRdf.Skos` as the semantic building blocks. Markdown remains the source of truth; the library owns the mapping from Markdown/front matter/rules into the RDF graph.

By default, semantic layers are enabled through `KnowledgeGraphBuildOptions.SemanticLayers`.

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

var result = await pipeline.BuildAsync(
    documents,
    new KnowledgeGraphBuildOptions
    {
        SemanticLayers = new KnowledgeGraphSemanticLayerOptions
        {
            IncludeOntologyLayer = true,
            IncludeSkosLayer = true,
            ConceptSchemeLabel = "Operations Capability Scheme",
        },
    });
```

## Graph Runtime Lifecycle

Once a Markdown file or directory has been built into a `KnowledgeGraph`, the same public runtime can persist it through a graph-store abstraction, reload it, materialize inference, expose a full-text index, expose a dynamic snapshot, or materialize a Linked Data Fragments source into the same local graph model.

The runtime now uses `dotNetRdf`, `dotNetRdf.Ontology`, `dotNetRdf.Skos`, `dotNetRdf.Inferencing`, `dotNetRdf.Dynamic`, `dotNetRdf.Query.FullText`, and `dotNetRdf.Ldf` through repository-owned adapters instead of a hand-rolled RDF stack. RDF serialization remains repository-owned; filesystem/blob access is delegated to `ManagedCode.Storage`.

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class GraphRuntimeLifecycleDemo
{
    private const string FilePath = "/absolute/path/to/content/query-federation-runbook.md";
    private const string TurtlePath = "/absolute/path/to/output/runtime-graph.ttl";
    private const string StorageLocation = "graphs/runtime/runtime-graph.ttl";
    private const string SchemaPath = "/absolute/path/to/runtime-schema.ttl";
    private const string RulesPath = "/absolute/path/to/runtime-rules.n3";

    public static async Task RunAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
        var built = await pipeline.BuildFromFileAsync(FilePath);
        var memoryStore = new InMemoryKnowledgeGraphStore();

        await built.Graph.SaveToStoreAsync(memoryStore, StorageLocation);
        var fromMemory = await KnowledgeGraph.LoadFromStoreAsync(memoryStore, StorageLocation);
        await built.Graph.SaveToFileAsync(TurtlePath);
        var reloaded = await KnowledgeGraph.LoadFromFileAsync(TurtlePath);

        var inference = await fromMemory.MaterializeInferenceAsync(new KnowledgeGraphInferenceOptions
        {
            AdditionalSchemaFilePaths = [SchemaPath],
            AdditionalN3RuleFilePaths = [RulesPath],
        });

        using var fullText = await inference.Graph.BuildFullTextIndexAsync();
        var matches = await fullText.SearchAsync("federated wikidata workflow");

        dynamic dynamicGraph = inference.Graph.ToDynamicSnapshot();
        dynamic dynamicDocument = dynamicGraph["https://kb.example/query-federation-runbook/"];

        Console.WriteLine(inference.InferredTripleCount);
        Console.WriteLine(matches.Count);
        Console.WriteLine(dynamicDocument["https://schema.org/name"].Count);
        Console.WriteLine(reloaded.TripleCount);
    }
}
```

The built-in graph-store implementations are:

- `FileSystemKnowledgeGraphStore` — local file paths, internally backed by `ManagedCode.Storage.FileSystem`
- `StorageKnowledgeGraphStore` — any configured `ManagedCode.Storage.Core.IStorage` backend, including blob/object providers
- `InMemoryKnowledgeGraphStore` — process-local graph persistence without files

DI helpers are available for hosts that want one or more configured stores:

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.Storage.FileSystem;
using ManagedCode.Storage.FileSystem.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddFileSystemKnowledgeGraphStoreAsDefault(options =>
{
    options.BaseFolder = "/absolute/path/to/storage-root";
    options.CreateContainerIfNotExists = true;
});

services.AddFileSystemStorage("archive", options =>
{
    options.BaseFolder = "/absolute/path/to/archive-root";
    options.CreateContainerIfNotExists = true;
});
services.AddKeyedStorageBackedKnowledgeGraphStore<IFileSystemStorage>("archive");
```

Use `new InMemoryKnowledgeGraphStore()` for process-local persistence, or `AddVirtualFileSystemKnowledgeGraphStore()` after `AddVirtualFileSystem(...)` when the host already standardizes on a VFS overlay.

The same runtime can also materialize a read-only Triple Pattern Fragments source into a local graph:

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

var ldfGraph = await KnowledgeGraph.LoadFromLinkedDataFragmentsAsync(
    new Uri("https://example.org/tpf"));
```

If the host needs custom transport settings, pass a caller-owned `HttpClient` through `KnowledgeGraphLinkedDataFragmentsOptions`. Host apps may source that client from `IHttpClientFactory`; the core library intentionally accepts the configured client instance instead of depending on `IHttpClientFactory`.

After materialization, callers use the normal local `ExecuteSelectAsync`, `ExecuteAskAsync`, `SearchAsync`, `ValidateShacl`, persistence, and inference APIs.

## Optional AI Extraction

AI extraction builds graph facts from entities and assertions returned by an injected `Microsoft.Extensions.AI.IChatClient`. The package stays provider-neutral: it does not reference OpenAI, Azure OpenAI, Anthropic, or any other model-specific SDK. If no chat client is provided, `Auto` mode extracts no facts and reports a diagnostic; choose `Tiktoken` mode explicitly for local token-distance extraction.

Chat extraction is chunk-based. The pipeline parses Markdown into deterministic chunks, sends each chunk through the structured extractor in order, and merges the resulting facts into one canonical graph. Optional cache reuse can be enabled through `MarkdownKnowledgePipelineOptions.ExtractionCache`.

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

The built-in chat extractor requests structured output through `GetResponseAsync<T>()`, normalizes the returned entity/assertion payload, and then builds the same in-memory RDF graph used by search and SPARQL. Tests use one local non-network `IChatClient` implementation so the full extraction-to-graph flow is covered without a live model. When cache reuse is enabled, the cache key includes document identity, chunk fingerprints, chunker profile, prompt version, and model identity so stale reuse stays explicit and controllable.

## Local Tiktoken Extraction

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class TiktokenGraphDemo
{
    private const string Markdown = """
The observatory stores telescope images in a cold archive near the mountain lab.
River sensors use cached forecasts to protect orchards from frost.
""";

    public static async Task RunAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);

        var result = await pipeline.BuildFromMarkdownAsync(Markdown);
        var matches = await result.Graph.SearchByTokenDistanceAsync("telescope image archive");

        Console.WriteLine(matches[0].Text);
    }
}
```

Tiktoken mode uses `Microsoft.ML.Tokenizers` to encode section/paragraph text into token IDs, builds normalized sparse vectors, and calculates Euclidean distance. The default weighting is `SubwordTfIdf`, fitted over the current build corpus and reused for query vectors. `TermFrequency` uses raw token counts, and `Binary` uses token presence/absence.

Tiktoken mode also builds a corpus graph:

- heading or loose document sections and paragraph/line segments become `schema:CreativeWork` nodes
- local Unicode word n-gram keyphrases become `schema:DefinedTerm` topic nodes
- explicit front matter `entity_hints` / `entityHints` become graph entities with stable hash IDs and preserved `sameAs` links
- containment uses `schema:hasPart`
- segment/topic membership uses `schema:about`
- document/entity-hint membership uses `schema:mentions`
- segment similarity uses `kb:relatedTo`

The local lexical design follows [Multilingual Search with Subword TF-IDF](https://arxiv.org/abs/2209.14281): use subword tokenization plus TF-IDF instead of manually curated tokenization, stop words, or stemming rules. It is designed for same-language lexical retrieval. Cross-language semantic retrieval requires a translation or embedding layer owned by the host application.

The current test corpus validates top-1 token-distance retrieval across English, Ukrainian, French, and German. Same-language queries hit the expected segment at `10/10` for each language in the test corpus. Sampled cross-language aligned hits stay low at `3/40`, which matches the lexical design.

## Query The Graph

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class QueryGraphDemo
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
        var search = await result.Graph.SearchAsync(SearchTerm);

        foreach (var row in rows.Rows)
        {
            Console.WriteLine(row.Values[ArticleKey]);
            Console.WriteLine(row.Values[TitleKey]);
        }

        Console.WriteLine(search.Rows.Count);
    }
}
```

SPARQL execution is intentionally read-only. `SELECT` and `ASK` are allowed; mutation forms such as `INSERT`, `DELETE`, `LOAD`, `CLEAR`, `DROP`, and `CREATE` are rejected before execution.

The supported query surface is intentionally narrow:

- local read-only queries: `ExecuteSelectAsync` for `SELECT` and `ExecuteAskAsync` for `ASK`
- explicit federated read-only queries: `ExecuteFederatedSelectAsync` for `SELECT` and `ExecuteFederatedAskAsync` for `ASK`
- unsupported query types: `CONSTRUCT`, `DESCRIBE`, and all mutation/update forms

The default public SPARQL contract remains local and in-memory. Local `ExecuteSelectAsync` / `ExecuteAskAsync` reject top-level `SERVICE` clauses. Federated queries are explicit through `ExecuteFederatedSelectAsync` / `ExecuteFederatedAskAsync`, require an allowlist or named profile, and currently ship caller-visible endpoint diagnostics through `FederatedSparqlSelectResult` / `FederatedSparqlAskResult`.

This follows the official Wikidata Query Service federation model, where cross-endpoint access is expressed with SPARQL `SERVICE` clauses and endpoint policy stays explicit at the caller boundary. The library ships ready-made profiles for the WDQS main/scholarly split introduced on 9 May 2025:

- `FederatedSparqlProfiles.WikidataMain` allowlists `https://query.wikidata.org/sparql`
- `FederatedSparqlProfiles.WikidataScholarly` allowlists `https://query-scholarly.wikidata.org/sparql`
- `FederatedSparqlProfiles.WikidataMainAndScholarly` allowlists both endpoints for multi-endpoint federated queries

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

var federated = await result.Graph.ExecuteFederatedSelectAsync(
    """
    SELECT ?item WHERE {
      SERVICE <https://query.wikidata.org/sparql> {
        ?item ?p ?o
      }
    }
    """,
    FederatedSparqlProfiles.WikidataMain);

Console.WriteLine(federated.ServiceEndpointSpecifiers[0]);
```

Use `ExecuteFederatedAskAsync` the same way when the caller needs a read-only federated `ASK` query instead of a result set.

For deterministic multi-graph federation inside the same process, bind allowlisted endpoint URIs to other in-memory `KnowledgeGraph` instances:

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

var localOptions = new FederatedSparqlExecutionOptions
{
    AllowedServiceEndpoints =
    [
        new Uri("https://kb.example/services/policy"),
        new Uri("https://kb.example/services/runbook"),
    ],
    LocalServiceBindings =
    [
        new FederatedSparqlLocalServiceBinding(
            new Uri("https://kb.example/services/policy"),
            policyGraph),
        new FederatedSparqlLocalServiceBinding(
            new Uri("https://kb.example/services/runbook"),
            runbookGraph),
    ],
};

var result = await rootGraph.ExecuteFederatedSelectAsync(
    """
    PREFIX schema: <https://schema.org/>
    SELECT ?policyTitle ?runbookTitle WHERE {
      SERVICE <https://kb.example/services/policy> {
        ?policy schema:name ?policyTitle .
      }
      SERVICE <https://kb.example/services/runbook> {
        ?runbook schema:name ?runbookTitle .
      }
    }
    """,
    localOptions);
```

This path still uses SPARQL `SERVICE` and the same allowlist checks, but it stays fully in-memory and network-free for test fixtures or host-managed multi-graph workflows.

For the external federation model and current WDQS endpoint split, see the official [Wikidata federated queries guide](https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries), the [WDQS graph split note](https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/WDQS_graph_split), and the [Wikidata Query Service user manual](https://www.mediawiki.org/wiki/Wikidata_Query_Service/User_Manual/en).

## Validate With SHACL

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class ShaclValidationDemo
{
    public static void Run(MarkdownKnowledgeBuildResult result)
    {
        KnowledgeGraphShaclValidationReport report = result.ValidateShacl();

        if (!report.Conforms)
        {
            foreach (var issue in report.Results)
            {
                Console.WriteLine(issue.FocusNode);
                Console.WriteLine(issue.Message);
            }
        }

        Console.WriteLine(report.ReportTurtle);
    }
}
```

`ValidateShacl()` uses default Markdown-LD Knowledge Bank shapes backed by `dotNetRdf.Shacl`. The default shapes validate article names, entity names, `schema:sameAs` IRIs, provenance IRIs, and assertion confidence metadata when reified assertion metadata is present.

Graph assertions always remain direct RDF edges for existing SPARQL and search callers. Reified assertion metadata is now an explicit throughput trade-off:

- default builds keep only the direct RDF edges, which is the fast path for large Markdown corpora and tokenized graphs
- opt in to RDF reification when the caller needs `rdf:Statement` metadata with `rdf:subject`, `rdf:predicate`, `rdf:object`, `kb:confidence`, and optional `prov:wasDerivedFrom`

Use `KnowledgeGraphBuildOptions.IncludeAssertionReification = true` when assertion-level provenance and confidence triples must be queryable:

```csharp
var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
{
    BaseUri = new Uri("https://kb.example/"),
    BuildOptions = new KnowledgeGraphBuildOptions
    {
        IncludeAssertionReification = true,
    },
});
```

Pass custom Turtle shapes when the host application needs stricter rules:

```csharp
const string Shapes = """
@prefix sh: <http://www.w3.org/ns/shacl#> .
@prefix schema: <https://schema.org/> .

<urn:shape:ArticleDatePublished> a sh:NodeShape ;
  sh:targetClass schema:Article ;
  sh:property [
    sh:path schema:datePublished ;
    sh:minCount 1 ;
    sh:message "Every Article must have a schema:datePublished." ;
  ] .
""";

var report = result.Graph.ValidateShacl(Shapes);
```

Invalid caller-authored `sameAs` or provenance values are kept as RDF literals so the SHACL report can expose the exact violation instead of silently dropping the malformed fact.

## Export The Graph

```csharp
using ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class ExportGraphDemo
{
    public static void Run(MarkdownKnowledgeBuildResult result)
    {
        KnowledgeGraphSnapshot snapshot = result.Graph.ToSnapshot();
        string mermaid = result.Graph.SerializeMermaidFlowchart();
        string dot = result.Graph.SerializeDotGraph();
        string turtle = result.Graph.SerializeTurtle();
        string jsonLd = result.Graph.SerializeJsonLd();

        Console.WriteLine(snapshot.Nodes.Count);
        Console.WriteLine(snapshot.Edges.Count);
        Console.WriteLine(mermaid);
        Console.WriteLine(dot);
        Console.WriteLine(turtle.Length);
        Console.WriteLine(jsonLd.Length);
    }
}
```

`ToSnapshot()` returns a stable object graph with `Nodes` and `Edges` so callers can build their own UI, JSON endpoint, or visualization layer without touching dotNetRDF internals. URI node labels are resolved from `schema:name` when available, so diagram output is readable by default.

Example Mermaid output shape:

```mermaid
graph LR
  n0["Zero Cost Knowledge Graph"]
  n1["RDF"]
  n0 -->|"schema:mentions"| n1
```

Example DOT output shape:

```dot
digraph KnowledgeGraph {
  rankdir=LR;
  "n0" [label="Zero Cost Knowledge Graph"];
  "n1" [label="RDF"];
  "n0" -> "n1" [label="schema:mentions"];
}
```

## Thread Safety

`KnowledgeGraph` is safe for shared in-memory read/write use through its public API. Search, read-only SPARQL, snapshot export, diagram serialization, and RDF serialization run under a read lock; `MergeAsync` snapshots a built graph and merges it under a write lock.

Use this when many workers convert Markdown independently and publish their results into one graph:

```csharp
var shared = await pipeline.BuildFromMarkdownAsync(string.Empty);
var next = await pipeline.BuildFromMarkdownAsync(markdown, path: "content/note.md");

await shared.Graph.MergeAsync(next.Graph);
var rows = await shared.Graph.SearchAsync("rdf");
```

## Key Types

| Type | Purpose |
|---|---|
| `MarkdownKnowledgePipeline` | Entry point. Orchestrates parsing, extraction, merge, and graph build. |
| `MarkdownKnowledgeBuildResult` | Holds `Documents`, `Facts`, and the built `Graph`. |
| `KnowledgeGraph` | In-memory dotNetRDF graph with query, search, SHACL validation, export, and merge. |
| `KnowledgeGraphSnapshot` | Immutable view with `Nodes` (`KnowledgeGraphNode`) and `Edges` (`KnowledgeGraphEdge`). |
| `KnowledgeGraphShaclValidationReport` | SHACL conformance result with flattened issues and Turtle report output. |
| `KnowledgeGraphShaclValidationIssue` | Caller-readable SHACL result fields such as focus node, path, value, severity, and message. |
| `MarkdownDocument` | Pipeline parsed document: `FrontMatter`, `Body`, and `Sections`. |
| `MarkdownFrontMatter` | Typed front matter model used by the low-level Markdown parser. |
| `KnowledgeExtractionResult` | Merged collection of `KnowledgeEntityFact` and `KnowledgeAssertionFact`. |
| `SparqlQueryResult` | Query result with `Variables` and `Rows` of `SparqlRow`. |
| `KnowledgeSourceDocumentConverter` | Converts files and directories into pipeline-ready source documents. |
| `ChatClientKnowledgeFactExtractor` | AI extraction adapter behind `IChatClient`. |
| `TiktokenKnowledgeGraphOptions` | Options for explicit Tiktoken token-distance extraction. |
| `TokenVectorWeighting` | Local token weighting mode: `SubwordTfIdf`, `TermFrequency`, or `Binary`. |
| `TokenDistanceSearchResult` | Search result returned by `SearchByTokenDistanceAsync`. |

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

Recognized front matter keys:

| Key | RDF property | Type |
|---|---|---|
| `title` | `schema:name` | string |
| `description` / `summary` | `schema:description` | string |
| `datePublished` | `schema:datePublished` | string (ISO date) |
| `dateModified` | `schema:dateModified` | string (ISO date) |
| `author` | `schema:author` | string or list |
| `tags` / `keywords` | `schema:keywords` | list |
| `about` | `schema:about` | list |
| `entryType` / `entry_type` | compatibility metadata plus optional additional `schema.org` article subtype typing | string or list |
| `sourceProject` / `source_project` | `kb:sourceProject` | string or list |
| `canonicalUrl` / `canonical_url` | low-level Markdown parser document identity; use `KnowledgeDocumentConversionOptions.CanonicalUri` for pipeline identity | string (URL) |
| `entity_hints` / `entityHints` | explicit graph entities in `Tiktoken` mode; parsed as front matter metadata otherwise | list of `{label, type, sameAs}` |

Generic RDF front matter mapping is also supported for richer document metadata beyond article-only defaults:

| Key | Purpose | Type |
|---|---|---|
| `rdf_prefixes` / `rdfPrefixes` | additional vocabulary prefixes | object |
| `rdf_types` / `rdfTypes` | additional RDF types for the document node | string or list |
| `rdf_properties` / `rdfProperties` | arbitrary predicate/value mappings for the document node | object |

Example:

```yaml
rdf_prefixes:
  dcterms: http://purl.org/dc/terms/
  skos: http://www.w3.org/2004/02/skos/core#
rdf_types:
  - schema:HowTo
  - skos:ConceptScheme
rdf_properties:
  schema:isPartOf:
    id: https://example.com/projects/ai-memex
  dcterms:issued:
    value: 2026-04-21
    datatype: xsd:date
  skos:prefLabel: Flexible Graph Spec
```

Scalar values become literals by default. Object values may use `id` to emit a URI node or `value` plus optional `datatype` to emit a typed literal. Unknown prefixes fail explicitly instead of being silently guessed.

Predicate normalization for explicit chat/token facts:

- `mentions` becomes `schema:mentions`
- `about` becomes `schema:about`
- `author` becomes `schema:author`
- `creator` becomes `schema:creator`
- `sameas` becomes `schema:sameAs`
- `relatedTo` becomes `kb:relatedTo`
- prefixed predicates such as `schema:mentions`, `kb:relatedTo`, `prov:wasDerivedFrom`, and `rdf:type` are preserved
- absolute predicate URIs are preserved when valid

Markdown links, wikilinks, and arrow assertions are not implicitly converted into graph facts. Use `IChatClient` extraction or explicit `Tiktoken` mode when you want body content to produce graph nodes and edges.

## Architecture Choices

- `Markdig` parses Markdown structure.
- `YamlDotNet` parses front matter.
- `dotNetRDF` builds the RDF graph, runs local SPARQL, and serializes Turtle/JSON-LD.
- `dotNetRdf.Shacl` validates built graphs with default or caller-supplied SHACL shapes.
- `Microsoft.Extensions.AI.IChatClient` is the only AI boundary in the core pipeline.
- The production source tree is organized by feature slices: Documents, Extraction, Pipeline, Graph, Tokenization, Query, and Rdf.
- `Microsoft.ML.Tokenizers` powers the explicit Tiktoken token-distance mode.
- Subword TF-IDF is the default local token weighting because it downweights corpus-common tokens without adding language-specific preprocessing or model runtime dependencies.
- Local topic graph construction uses Unicode word n-gram keyphrases and RDF `schema:DefinedTerm`, `schema:hasPart`, and `schema:about` edges.
- Embeddings are not required for the current graph/search flow; Tiktoken mode uses token IDs, not embedding vectors.
- Microsoft Agent Framework is treated as host-level orchestration, not a core package dependency.

See [docs/Architecture.md](docs/Architecture.md), [ADR-0001](docs/ADR/ADR-0001-rdf-sparql-library.md), [ADR-0002](docs/ADR/ADR-0002-llm-extraction-ichatclient.md), [ADR-0003](docs/ADR/ADR-0003-tiktoken-extraction-mode.md), [ADR-0006](docs/ADR/ADR-0006-federated-sparql-adapter.md), [Graph Runtime Lifecycle](docs/Features/GraphRuntimeLifecycle.md), [Graph SHACL Validation](docs/Features/GraphShaclValidation.md), and [Federated SPARQL Execution](docs/Features/FederatedSparqlExecution.md).

## Inspiration And Attribution

This project is inspired by Luis Quintanilla's Markdown-LD / AI Memex work:

- [lqdev/markdown-ld-kb](https://github.com/lqdev/markdown-ld-kb) - upstream Python reference repository
- [Zero-Cost Knowledge Graph from Markdown](https://lqdev.me/resources/ai-memex/blog-post-zero-cost-knowledge-graph-from-markdown/) - core idea for using Markdown, YAML front matter, LLM extraction, RDF, JSON-LD, Turtle, and SPARQL
- [Project Report: Entity Extraction & RDF Pipeline](https://lqdev.me/resources/ai-memex/project-report-entity-extraction-rdf-pipeline/) - extraction and RDF pipeline context
- [W3C SPARQL Federated Query](https://github.com/w3c/sparql-federated-query) - SPARQL federation reference material
- [Wikidata Federated Queries](https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries) - official WDQS `SERVICE` federation guide and examples
- [Wikidata Query Service User Manual](https://www.mediawiki.org/wiki/Wikidata_Query_Service/User_Manual/en) - official WDQS operational and usage guidance
- [WDQS Graph Split](https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/WDQS_graph_split) - official main/scholarly endpoint split and migration guidance
- [dotNetRDF](https://github.com/dotnetrdf/dotnetrdf) - RDF/SPARQL/SHACL engine used by this C# implementation

The upstream reference repository is kept as a read-only submodule under `external/lqdev-markdown-ld-kb`.

## Development

```bash
dotnet restore MarkdownLd.Kb.slnx
dotnet build MarkdownLd.Kb.slnx --configuration Release --no-restore
dotnet test --solution MarkdownLd.Kb.slnx --configuration Release
dotnet format MarkdownLd.Kb.slnx --verify-no-changes
dotnet test --solution MarkdownLd.Kb.slnx --configuration Release -- --coverage --coverage-output-format cobertura --coverage-output "$PWD/TestResults/TUnitCoverage/coverage.cobertura.xml" --coverage-settings "$PWD/CodeCoverage.runsettings"
```

Coverage is collected through `Microsoft.Testing.Extensions.CodeCoverage`. Cobertura is the XML output format used for line and branch reporting; the test project does not reference Coverlet.

Current verification:

- tests: 109 passed, 0 failed
- line coverage: 95.97%
- branch coverage: 84.01%
- target framework: .NET 10
- package version: 0.0.1
