using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeGraphRuntimeExpansionFlowTests
{
    private const string BaseUriText = "https://graph-runtime.example/";
    private const string QueryRunbookPath = "operations/query-federation-runbook.md";
    private const string ReleaseGatePath = "release/release-gate-checklist.md";
    private const string CatalogPath = "catalog/query-workflows-catalog.md";
    private const string QueryRunbookUri = "https://graph-runtime.example/operations/query-federation-runbook/";
    private const string ReleaseGateUri = "https://graph-runtime.example/release/release-gate-checklist/";
    private const string QueryWorkflowConceptUri = "https://graph-runtime.example/id/query-workflows";
    private const string SemanticOperationsConceptUri = "https://graph-runtime.example/id/semantic-operations";
    private const string SchemaFileName = "runtime-schema.ttl";
    private const string RulesFileName = "runtime-rules.n3";
    private const string TurtleFileName = "runtime-graph.ttl";
    private const string JsonLdFileName = "runtime-graph.jsonld";
    private const string NTriplesFileName = "runtime-graph.nt";
    private const string LdfFileName = "runtime-fragment.ttl";
    private const string TempRootPrefix = "markdown-ld-kb-runtime-";
    private const string GuidFormat = "N";
    private const string FullTextQuery = "federated wikidata workflow";

    private const string QueryRunbookMarkdown = """
---
title: Query Federation Runbook
about:
  - Query Workflows
graph_groups:
  - Semantic Operations
graph_next_steps:
  - https://graph-runtime.example/release/release-gate-checklist/
rdf_prefixes:
  ex: https://graph-runtime.example/vocab/
rdf_types:
  - ex:Runbook
rdf_properties:
  ex:ownedBy:
    id: https://graph-runtime.example/id/platform-team
  ex:capability:
    - value: federation
    - value: wikidata
---
# Query Federation Runbook

This runbook coordinates federated queries across Markdown corpora, RDF graphs, and Wikidata endpoints.
""";

    private const string ReleaseGateMarkdown = """
---
title: Release Gate Checklist
about:
  - Release Workflows
graph_groups:
  - Semantic Operations
rdf_prefixes:
  ex: https://graph-runtime.example/vocab/
rdf_types:
  - ex:ReleaseGate
---
# Release Gate Checklist

The release gate validates query evidence, graph integrity, and workflow readiness.
""";

    private const string CatalogMarkdown = """
---
title: Query Workflows Catalog
about:
  - Query Workflows
graph_groups:
  - Semantic Operations
graph_related:
  - https://graph-runtime.example/operations/query-federation-runbook/
rdf_prefixes:
  ex: https://graph-runtime.example/vocab/
rdf_types:
  - ex:CapabilityCatalog
---
# Query Workflows Catalog

The catalog indexes query workflows, semantic operations, and release controls.
""";

    private const string RuntimeSchemaTurtle = """
@prefix ex: <https://graph-runtime.example/vocab/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix schema: <https://schema.org/> .
@prefix skos: <http://www.w3.org/2004/02/skos/core#> .

ex:WorkflowArtifact a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .

ex:Runbook a rdfs:Class ;
  rdfs:subClassOf ex:WorkflowArtifact .

ex:ReleaseGate a rdfs:Class ;
  rdfs:subClassOf ex:WorkflowArtifact .

ex:CapabilityCatalog a rdfs:Class ;
  rdfs:subClassOf ex:WorkflowArtifact .

<https://graph-runtime.example/id/query-workflows> a skos:Concept ;
  skos:broader <https://graph-runtime.example/id/semantic-operations> .

<https://graph-runtime.example/id/semantic-operations> a skos:Concept .
""";

    private const string RuntimeRules = """
@prefix ex: <https://graph-runtime.example/vocab/> .
@prefix kb: <urn:managedcode:markdown-ld-kb:vocab:> .

{ ?workflow kb:nextStep ?step } => { ?workflow ex:hasOperationalSuccessor ?step } .
""";

    private const string BaseAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX ex: <https://graph-runtime.example/vocab/>
ASK WHERE {
  <https://graph-runtime.example/operations/query-federation-runbook/> a kb:MarkdownDocument ;
    a ex:Runbook ;
    schema:name "Query Federation Runbook" ;
    schema:about <https://graph-runtime.example/id/query-workflows> ;
    kb:memberOf <https://graph-runtime.example/id/semantic-operations> ;
    kb:nextStep <https://graph-runtime.example/release/release-gate-checklist/> .

  <https://graph-runtime.example/release/release-gate-checklist/> a ex:ReleaseGate .
  <https://graph-runtime.example/catalog/query-workflows-catalog/> a ex:CapabilityCatalog .
}
""";

    private const string InferenceAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX ex: <https://graph-runtime.example/vocab/>
ASK WHERE {
  <https://graph-runtime.example/operations/query-federation-runbook/> a ex:WorkflowArtifact ;
    a schema:CreativeWork ;
    schema:about <https://graph-runtime.example/id/semantic-operations> ;
    ex:hasOperationalSuccessor <https://graph-runtime.example/release/release-gate-checklist/> .

  <https://graph-runtime.example/release/release-gate-checklist/> a ex:WorkflowArtifact ;
    a schema:CreativeWork .
}
""";

    private const string SingleFileAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX ex: <https://graph-runtime.example/vocab/>
ASK WHERE {
  <https://graph-runtime.example/operations/query-federation-runbook/> a kb:MarkdownDocument ;
    a ex:Runbook ;
    schema:name "Query Federation Runbook" ;
    schema:about <https://graph-runtime.example/id/query-workflows> ;
    kb:memberOf <https://graph-runtime.example/id/semantic-operations> ;
    kb:nextStep <https://graph-runtime.example/release/release-gate-checklist/> .
}
""";

    private const string LinkedDataFragmentsAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX ex: <https://graph-runtime.example/vocab/>
ASK WHERE {
  <https://graph-runtime.example/operations/query-federation-runbook/> a kb:MarkdownDocument ;
    a ex:Runbook ;
    schema:name "Query Federation Runbook" ;
    ex:hasOperationalSuccessor <https://graph-runtime.example/release/release-gate-checklist/> .

  <https://graph-runtime.example/release/release-gate-checklist/> a ex:ReleaseGate .
}
""";

    [Test]
    public async Task Single_markdown_file_builds_a_queryable_graph_from_disk()
    {
        using var temp = CreateTempDirectory();
        var filePath = await WriteTextFileAsync(temp.RootPath, QueryRunbookPath, QueryRunbookMarkdown);

        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var result = await pipeline.BuildFromFileAsync(
            filePath,
            new KnowledgeDocumentConversionOptions
            {
                CanonicalUri = new Uri(QueryRunbookUri),
            });

        result.Documents.Count.ShouldBe(1);
        (await result.Graph.ExecuteAskAsync(SingleFileAskQuery)).ShouldBeTrue();
    }

    [Test]
    public async Task Markdown_directory_dataset_builds_a_queryable_graph_and_round_trips_through_file_store_formats()
    {
        using var temp = CreateTempDirectory();
        await WriteMarkdownDatasetAsync(temp.RootPath);

        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var result = await pipeline.BuildFromDirectoryAsync(temp.RootPath);

        (await result.Graph.ExecuteAskAsync(BaseAskQuery)).ShouldBeTrue();

        var store = FileSystemKnowledgeGraphStore.Default;
        var turtlePath = Path.Combine(temp.RootPath, TurtleFileName);
        var jsonLdPath = Path.Combine(temp.RootPath, JsonLdFileName);
        await store.SaveAsync(result.Graph, turtlePath);
        await store.SaveAsync(result.Graph, jsonLdPath);

        var turtleGraph = await store.LoadAsync(turtlePath);
        var jsonLdGraph = await store.LoadAsync(jsonLdPath);
        var mergedDirectoryGraph = await KnowledgeGraph.LoadFromDirectoryAsync(
            temp.RootPath,
            new KnowledgeGraphLoadOptions
            {
                SearchPattern = "*.ttl",
                SearchOption = SearchOption.TopDirectoryOnly,
            });

        (await turtleGraph.ExecuteAskAsync(BaseAskQuery)).ShouldBeTrue();
        (await jsonLdGraph.ExecuteAskAsync(BaseAskQuery)).ShouldBeTrue();
        mergedDirectoryGraph.TripleCount.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Runtime_expansion_supports_inference_full_text_dynamic_snapshots_and_file_reload()
    {
        using var temp = CreateTempDirectory();
        await WriteMarkdownDatasetAsync(temp.RootPath);
        var schemaPath = await WriteTextFileAsync(temp.RootPath, SchemaFileName, RuntimeSchemaTurtle);
        var rulesPath = await WriteTextFileAsync(temp.RootPath, RulesFileName, RuntimeRules);

        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var built = await pipeline.BuildFromDirectoryAsync(temp.RootPath);
        var inference = await built.Graph.MaterializeInferenceAsync(new KnowledgeGraphInferenceOptions
        {
            AdditionalSchemaFilePaths = [schemaPath],
            AdditionalN3RuleFilePaths = [rulesPath],
        });

        (await inference.Graph.ExecuteAskAsync(InferenceAskQuery)).ShouldBeTrue();
        inference.InferredTripleCount.ShouldBeGreaterThan(0);

        var nTriplesPath = Path.Combine(temp.RootPath, NTriplesFileName);
        await inference.Graph.SaveToFileAsync(nTriplesPath);
        var reloaded = await KnowledgeGraph.LoadFromFileAsync(nTriplesPath);
        reloaded.TripleCount.ShouldBe(inference.Graph.TripleCount);

        using var fullTextIndex = await inference.Graph.BuildFullTextIndexAsync();
        var matches = await fullTextIndex.SearchAsync(FullTextQuery);
        matches.Any(match => match.NodeId == QueryRunbookUri).ShouldBeTrue();

        dynamic dynamicGraph = inference.Graph.ToDynamicSnapshot();
        dynamic dynamicDocument = dynamicGraph[QueryRunbookUri];
        ((object?)dynamicDocument).ShouldNotBeNull();
        dynamic dynamicNames = dynamicDocument["https://schema.org/name"];
        ((int)dynamicNames.Count).ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Linked_data_fragments_source_materializes_into_a_queryable_knowledge_graph()
    {
        using var temp = CreateTempDirectory();
        var ldfPath = Path.Combine(temp.RootPath, LdfFileName);
        var ldfUri = new Uri(ldfPath);
        await File.WriteAllTextAsync(ldfPath, CreateLinkedDataFragmentsDataset(ldfUri));

        using var httpClient = new HttpClient();
        var options = KnowledgeGraphLinkedDataFragmentsOptions.Default with
        {
            HttpClient = httpClient,
        };
        var graph = await KnowledgeGraph.LoadFromLinkedDataFragmentsAsync(
            ldfUri,
            options);
        var defaultGraph = await KnowledgeGraph.LoadFromLinkedDataFragmentsAsync(ldfUri);

        (await graph.ExecuteAskAsync(LinkedDataFragmentsAskQuery)).ShouldBeTrue();
        graph.TripleCount.ShouldBeGreaterThan(0);
        defaultGraph.TripleCount.ShouldBe(graph.TripleCount);
    }

    private static async Task WriteMarkdownDatasetAsync(string rootPath)
    {
        await WriteTextFileAsync(rootPath, QueryRunbookPath, QueryRunbookMarkdown);
        await WriteTextFileAsync(rootPath, ReleaseGatePath, ReleaseGateMarkdown);
        await WriteTextFileAsync(rootPath, CatalogPath, CatalogMarkdown);
    }

    private static async Task<string> WriteTextFileAsync(string rootPath, string relativePath, string content)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content);
        return fullPath;
    }

    private static TempDirectory CreateTempDirectory()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), TempRootPrefix + Guid.NewGuid().ToString(GuidFormat));
        Directory.CreateDirectory(rootPath);
        return new TempDirectory(rootPath);
    }

    private static string CreateLinkedDataFragmentsDataset(Uri sourceUri)
    {
        return $$"""
@prefix hydra: <http://www.w3.org/ns/hydra/core#> .
@prefix void: <http://rdfs.org/ns/void#> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix schema: <https://schema.org/> .
@prefix kb: <urn:managedcode:markdown-ld-kb:vocab:> .
@prefix ex: <https://graph-runtime.example/vocab/> .

<{{sourceUri}}#dataset> void:subset <{{sourceUri}}#fragment> ;
  hydra:search <{{sourceUri}}#search> .

<{{sourceUri}}#fragment> void:subset <{{sourceUri}}#page> .

<{{sourceUri}}#page> void:triples 4 .

<{{sourceUri}}#search> hydra:template "{{sourceUri.AbsoluteUri}}{?subject,predicate,object}" ;
  hydra:mapping <{{sourceUri}}#subjectMapping> ,
    <{{sourceUri}}#predicateMapping> ,
    <{{sourceUri}}#objectMapping> .

<{{sourceUri}}#subjectMapping> hydra:property rdf:subject ;
  hydra:variable "subject" .

<{{sourceUri}}#predicateMapping> hydra:property rdf:predicate ;
  hydra:variable "predicate" .

<{{sourceUri}}#objectMapping> hydra:property rdf:object ;
  hydra:variable "object" .

<https://graph-runtime.example/operations/query-federation-runbook/> a kb:MarkdownDocument ,
    ex:Runbook ;
  schema:name "Query Federation Runbook" ;
  ex:hasOperationalSuccessor <https://graph-runtime.example/release/release-gate-checklist/> .

<https://graph-runtime.example/release/release-gate-checklist/> a ex:ReleaseGate .
""";
    }

    private sealed class TempDirectory(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, true);
            }
        }
    }
}
