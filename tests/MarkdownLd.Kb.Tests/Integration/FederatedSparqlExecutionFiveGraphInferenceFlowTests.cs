using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class FederatedSparqlExecutionFiveGraphInferenceFlowTests
{
    private static readonly Uri BaseUri = new("https://federated-inference-flow.example/");

    private const string PolicyEndpointText = "https://federated-inference-flow.example/services/policy";
    private const string RunbookEndpointText = "https://federated-inference-flow.example/services/runbook";
    private const string StorageEndpointText = "https://federated-inference-flow.example/services/storage";
    private const string OntologyEndpointText = "https://federated-inference-flow.example/services/ontology";
    private const string ReferenceEndpointText = "https://federated-inference-flow.example/services/reference";
    private const string WikidataGuideText = "https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries";
    private const string ReleaseCheckpointText = "https://federated-inference-flow.example/workflows/query-release-gate/";
    private const string SchemeLabelText = "Federated Query Governance Scheme";
    private const string PolicyTitleText = "Federation Governance Policy";
    private const string RunbookTitleText = "Federated Query Approval Runbook";
    private const string StorageTitleText = "Federated Storage Blueprint";
    private const string PolicyPath = "governance/federation-governance-policy.md";
    private const string RunbookPath = "operations/federated-query-approval-runbook.md";
    private const string StoragePath = "storage/federated-storage-blueprint.md";
    private const string OntologyPath = "ontology/federated-query-ontology.md";
    private const string ReferencePath = "reference/wikidata-federated-query-guide.md";
    private const string RootPath = "shell/root.md";
    private const string SchemaFileName = "federation-schema.ttl";
    private const string RulesFileName = "federation-rules.n3";
    private const string TempRootPrefix = "markdown-ld-kb-federated-inference-";
    private const string GuidFormat = "N";

    private const string PolicyMarkdown = """
---
title: Federation Governance Policy
rdf_prefixes:
  ex: https://federated-inference-flow.example/vocab/
rdf_types:
  - ex:PolicyMemo
rdf_properties:
  ex:governsConcept:
    id: https://federated-inference-flow.example/id/federated-query-governance
---
# Federation Governance Policy

This policy governs multi-graph SPARQL execution.
""";

    private const string RunbookMarkdown = """
---
title: Federated Query Approval Runbook
rdf_prefixes:
  ex: https://federated-inference-flow.example/vocab/
rdf_types:
  - ex:OperationalRunbook
rdf_properties:
  ex:operationallyAbout:
    id: https://federated-inference-flow.example/id/federated-query-governance
graph_next_steps:
  - https://federated-inference-flow.example/workflows/query-release-gate/
---
# Federated Query Approval Runbook

Operational steps for multi-graph review and release approval.
""";

    private const string StorageMarkdown = """
---
title: Federated Storage Blueprint
rdf_prefixes:
  ex: https://federated-inference-flow.example/vocab/
rdf_types:
  - ex:StorageBlueprint
rdf_properties:
  ex:indexesConcept:
    id: https://federated-inference-flow.example/id/federated-query-governance
  ex:persistsAs: Linked Data Fragments
---
# Federated Storage Blueprint

Storage guidance maps inferred graph assets into fragment-friendly output.
""";

    private const string OntologyMarkdown = """
---
title: Federated Query Ontology
about:
  - Federated Query Governance
rdf_prefixes:
  ex: https://federated-inference-flow.example/vocab/
rdf_types:
  - ex:FederationScheme
rdf_properties:
  ex:schemeDisplayLabel: Federated Query Governance Scheme
---
# Federated Query Ontology

The ontology aligns governance terms with a reusable concept scheme.
""";

    private const string ReferenceMarkdown = """
---
title: Wikidata Federated Query Guide
rdf_prefixes:
  ex: https://federated-inference-flow.example/vocab/
rdf_types:
  - ex:ReferenceDigest
rdf_properties:
  ex:tracksConcept:
    id: https://federated-inference-flow.example/id/federated-query-governance
graph_entities:
  - id: https://federated-inference-flow.example/id/federated-query-governance
    label: Federated Query Governance
    type: schema:DefinedTerm
    same_as: https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries
---
# Wikidata Federated Query Guide

This reference tracks the official Wikidata SERVICE documentation.
""";

    private const string SchemaText = """
@prefix ex: <https://federated-inference-flow.example/vocab/> .
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix schema: <https://schema.org/> .
@prefix skos: <http://www.w3.org/2004/02/skos/core#> .
ex:GovernanceArtifact a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .
ex:PolicyMemo a rdfs:Class ;
  rdfs:subClassOf ex:GovernanceArtifact .
ex:OperationalArtifact a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .
ex:OperationalRunbook a rdfs:Class ;
  rdfs:subClassOf ex:OperationalArtifact .
ex:PersistenceArtifact a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .
ex:StorageBlueprint a rdfs:Class ;
  rdfs:subClassOf ex:PersistenceArtifact .
ex:ExternalReferenceArtifact a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .
ex:ReferenceDigest a rdfs:Class ;
  rdfs:subClassOf ex:ExternalReferenceArtifact .
ex:FederationScheme a rdfs:Class ;
  rdfs:subClassOf skos:ConceptScheme .
ex:governsConcept a rdf:Property ;
  rdfs:subPropertyOf schema:about .
ex:operationallyAbout a rdf:Property ;
  rdfs:subPropertyOf schema:about .
ex:indexesConcept a rdf:Property ;
  rdfs:subPropertyOf schema:about .
ex:tracksConcept a rdf:Property ;
  rdfs:subPropertyOf schema:about .
ex:schemeDisplayLabel a rdf:Property ;
  rdfs:subPropertyOf skos:prefLabel .
""";

    private const string RulesText = """
@prefix ex: <https://federated-inference-flow.example/vocab/> .
@prefix kb: <urn:managedcode:markdown-ld-kb:vocab:> .

{ ?workflow kb:nextStep ?step } => { ?workflow ex:hasOperationalCheckpoint ?step } .
""";

    private const string SelectQuery = """
PREFIX ex: <https://federated-inference-flow.example/vocab/>
PREFIX schema: <https://schema.org/>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
SELECT ?policyTitle ?runbookTitle ?storageTitle ?schemeLabel ?guide ?checkpoint WHERE {
  SERVICE <https://federated-inference-flow.example/services/policy> {
    ?policy a ex:GovernanceArtifact ;
            schema:name ?policyTitle ;
            schema:about ?concept .
  }
  SERVICE <https://federated-inference-flow.example/services/runbook> {
    ?runbook a ex:OperationalArtifact ;
             schema:name ?runbookTitle ;
             schema:about ?concept ;
             ex:hasOperationalCheckpoint ?checkpoint .
  }
  SERVICE <https://federated-inference-flow.example/services/storage> {
    ?storage a ex:PersistenceArtifact ;
             schema:name ?storageTitle ;
             schema:about ?concept ;
             ex:persistsAs "Linked Data Fragments" .
  }
  SERVICE <https://federated-inference-flow.example/services/ontology> {
    ?scheme a skos:ConceptScheme ;
            skos:prefLabel ?schemeLabel .
    FILTER(?schemeLabel = "Federated Query Governance Scheme")
    ?concept a skos:Concept ;
             skos:prefLabel "Federated Query Governance" .
  }
  SERVICE <https://federated-inference-flow.example/services/reference> {
    ?reference a ex:ExternalReferenceArtifact ;
               schema:about ?concept .
    ?concept skos:exactMatch ?guide .
  }
}
""";

    private const string AskQuery = """
PREFIX ex: <https://federated-inference-flow.example/vocab/>
PREFIX schema: <https://schema.org/>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
ASK WHERE {
  SERVICE <https://federated-inference-flow.example/services/policy> {
    ?policy a ex:GovernanceArtifact ;
            schema:name "Federation Governance Policy" ;
            schema:about <https://federated-inference-flow.example/id/federated-query-governance> .
  }
  SERVICE <https://federated-inference-flow.example/services/runbook> {
    ?runbook a ex:OperationalArtifact ;
             schema:name "Federated Query Approval Runbook" ;
             schema:about <https://federated-inference-flow.example/id/federated-query-governance> ;
             ex:hasOperationalCheckpoint <https://federated-inference-flow.example/workflows/query-release-gate/> .
  }
  SERVICE <https://federated-inference-flow.example/services/storage> {
    ?storage a ex:PersistenceArtifact ;
             schema:name "Federated Storage Blueprint" ;
             schema:about <https://federated-inference-flow.example/id/federated-query-governance> ;
             ex:persistsAs "Linked Data Fragments" .
  }
  SERVICE <https://federated-inference-flow.example/services/ontology> {
    ?scheme a skos:ConceptScheme ;
            skos:prefLabel "Federated Query Governance Scheme" .
    <https://federated-inference-flow.example/id/federated-query-governance> a skos:Concept ;
                                                                           skos:prefLabel "Federated Query Governance" .
  }
  SERVICE <https://federated-inference-flow.example/services/reference> {
    ?reference a ex:ExternalReferenceArtifact ;
               schema:about <https://federated-inference-flow.example/id/federated-query-governance> .
    <https://federated-inference-flow.example/id/federated-query-governance> skos:exactMatch <https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries> .
  }
}
""";

    [Test]
    public async Task Federated_query_execution_does_not_unify_five_file_based_graphs_before_inference()
    {
        using var temp = CreateTempDirectory();
        var fixture = await BuildFixtureAsync(temp.RootPath, materializeInference: false);

        var select = await fixture.RootGraph.ExecuteFederatedSelectAsync(SelectQuery, fixture.Options);
        var ask = await fixture.RootGraph.ExecuteFederatedAskAsync(AskQuery, fixture.Options);

        select.Result.Rows.ShouldBeEmpty();
        ask.Result.ShouldBeFalse();
    }

    [Test]
    public async Task Federated_query_execution_can_join_five_file_based_graphs_after_inference()
    {
        using var temp = CreateTempDirectory();
        var fixture = await BuildFixtureAsync(temp.RootPath, materializeInference: true);

        var result = await fixture.RootGraph.ExecuteFederatedSelectAsync(SelectQuery, fixture.Options);

        result.ServiceEndpointSpecifiers.ShouldBe(CreateExpectedEndpoints());
        result.Result.Rows.Count.ShouldBe(1);
        result.Result.Rows[0].Values["policyTitle"].ShouldBe(PolicyTitleText);
        result.Result.Rows[0].Values["runbookTitle"].ShouldBe(RunbookTitleText);
        result.Result.Rows[0].Values["storageTitle"].ShouldBe(StorageTitleText);
        result.Result.Rows[0].Values["schemeLabel"].ShouldBe(SchemeLabelText);
        result.Result.Rows[0].Values["guide"].ShouldBe(WikidataGuideText);
        result.Result.Rows[0].Values["checkpoint"].ShouldBe(ReleaseCheckpointText);
    }

    [Test]
    public async Task Federated_query_execution_can_answer_ask_across_five_file_based_graphs_after_inference()
    {
        using var temp = CreateTempDirectory();
        var fixture = await BuildFixtureAsync(temp.RootPath, materializeInference: true);

        var result = await fixture.RootGraph.ExecuteFederatedAskAsync(AskQuery, fixture.Options);

        result.Result.ShouldBeTrue();
        result.ServiceEndpointSpecifiers.ShouldBe(CreateExpectedEndpoints());
    }

    private static IReadOnlyList<string> CreateExpectedEndpoints()
    {
        return [
            PolicyEndpointText,
            RunbookEndpointText,
            StorageEndpointText,
            OntologyEndpointText,
            ReferenceEndpointText,
        ];
    }

    private static async Task<FiveGraphInferenceFixture> BuildFixtureAsync(string rootPath, bool materializeInference)
    {
        var schemaPath = await WriteTextFileAsync(rootPath, SchemaFileName, SchemaText);
        var rulesPath = await WriteTextFileAsync(rootPath, RulesFileName, RulesText);
        var policyPath = await WriteTextFileAsync(rootPath, PolicyPath, PolicyMarkdown);
        var runbookPath = await WriteTextFileAsync(rootPath, RunbookPath, RunbookMarkdown);
        var storagePath = await WriteTextFileAsync(rootPath, StoragePath, StorageMarkdown);
        var ontologyPath = await WriteTextFileAsync(rootPath, OntologyPath, OntologyMarkdown);
        var referencePath = await WriteTextFileAsync(rootPath, ReferencePath, ReferenceMarkdown);
        var rootFilePath = await WriteTextFileAsync(rootPath, RootPath, string.Empty);
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);

        return new FiveGraphInferenceFixture(
            await BuildGraphAsync(pipeline, policyPath, materializeInference, schemaPath, rulesPath),
            await BuildGraphAsync(pipeline, runbookPath, materializeInference, schemaPath, rulesPath),
            await BuildGraphAsync(pipeline, storagePath, materializeInference, schemaPath, rulesPath),
            await BuildGraphAsync(pipeline, ontologyPath, materializeInference, schemaPath, rulesPath),
            await BuildGraphAsync(pipeline, referencePath, materializeInference, schemaPath, rulesPath),
            (await pipeline.BuildFromFileAsync(rootFilePath)).Graph);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(
        MarkdownKnowledgePipeline pipeline,
        string filePath,
        bool materializeInference,
        string schemaPath,
        string rulesPath)
    {
        var built = await pipeline.BuildFromFileAsync(filePath);
        if (!materializeInference)
        {
            return built.Graph;
        }

        var inference = await built.Graph.MaterializeInferenceAsync(new KnowledgeGraphInferenceOptions
        {
            AdditionalSchemaFilePaths = [schemaPath],
            AdditionalN3RuleFilePaths = [rulesPath],
        });
        return inference.Graph;
    }

    private static async Task<string> WriteTextFileAsync(string rootPath, string relativePath, string content)
    {
        var fullPath = Path.Combine(rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
        return fullPath;
    }

    private static TempDirectory CreateTempDirectory()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            string.Concat(TempRootPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(rootPath);
        return new TempDirectory(rootPath);
    }

    private sealed record FiveGraphInferenceFixture(
        KnowledgeGraph PolicyGraph,
        KnowledgeGraph RunbookGraph,
        KnowledgeGraph StorageGraph,
        KnowledgeGraph OntologyGraph,
        KnowledgeGraph ReferenceGraph,
        KnowledgeGraph RootGraph)
    {
        public FederatedSparqlExecutionOptions Options =>
            new()
            {
                AllowedServiceEndpoints =
                [
                    new Uri(PolicyEndpointText),
                    new Uri(RunbookEndpointText),
                    new Uri(StorageEndpointText),
                    new Uri(OntologyEndpointText),
                    new Uri(ReferenceEndpointText),
                ],
                LocalServiceBindings =
                [
                    new FederatedSparqlLocalServiceBinding(new Uri(PolicyEndpointText), PolicyGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(RunbookEndpointText), RunbookGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(StorageEndpointText), StorageGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(OntologyEndpointText), OntologyGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(ReferenceEndpointText), ReferenceGraph),
                ],
            };
    }

    private sealed class TempDirectory(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
