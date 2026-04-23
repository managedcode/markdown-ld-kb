using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class FederatedSparqlExecutionFiveGraphFlowTests
{
    private static readonly Uri BaseUri = new("https://federation-flow.example/");

    private const string PolicyEndpointText = "https://federation-flow.example/services/policy";
    private const string RunbookEndpointText = "https://federation-flow.example/services/runbook";
    private const string StorageEndpointText = "https://federation-flow.example/services/storage";
    private const string OntologyEndpointText = "https://federation-flow.example/services/ontology";
    private const string ReferenceEndpointText = "https://federation-flow.example/services/reference";
    private const string NestedUnboundEndpointText = "https://unbound.federation-flow.example/sparql";
    private const string ReferenceTargetText = "https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries";
    private const string NextStepText = "https://federation-flow.example/workflows/query-approval-checklist/";
    private const string PolicyDocumentPath = "catalog/federation-policy.md";
    private const string RunbookDocumentPath = "runbooks/federated-query-runbook.md";
    private const string StorageDocumentPath = "storage/storage-knowledge-graph.md";
    private const string OntologyDocumentPath = "ontology/federated-query-scheme.md";
    private const string ReferenceDocumentPath = "references/wikidata-federated-queries.md";
    private const string EmptyRootDocumentPath = "scratch/empty.md";

    private const string PolicyMarkdown = """
---
title: Federation Policy
about:
  - Federated Queries
---
# Federation Policy

Explicit federation remains opt-in.
""";

    private const string RunbookMarkdown = """
---
title: Federated Query Runbook
about:
  - Federated Queries
graph_next_steps:
  - https://federation-flow.example/workflows/query-approval-checklist/
rdf_types:
  - schema:HowTo
---
# Federated Query Runbook

Operational guidance for multi-graph SERVICE queries.
""";

    private const string StorageMarkdown = """
---
title: Storage Knowledge Graph
rdf_types:
  - schema:TechArticle
rdf_properties:
  schema:mentions:
    id: https://federation-flow.example/id/federated-queries
---
# Storage Knowledge Graph

Storage guidance links graph persistence to federated queries.
""";

    private const string OntologyMarkdown = """
---
title: Federated Query Scheme
about:
  - Federated Queries
rdf_prefixes:
  skos: http://www.w3.org/2004/02/skos/core#
rdf_types:
  - skos:ConceptScheme
rdf_properties:
  skos:prefLabel: Federated Query Scheme
---
# Federated Query Scheme

The ontology graph defines the scheme for federated query concepts.
""";

    private const string ReferenceMarkdown = """
---
title: Wikidata Federation Reference
about:
  - Federated Queries
graph_entities:
  - label: Federated Queries
    type: schema:DefinedTerm
    same_as: https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries
---
# Wikidata Federation Reference

Reference material for the Wikidata federated query guide.
""";

    private const string SelectQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
SELECT ?policyTitle ?runbookTitle ?storageTitle ?schemeLabel ?referenceTarget ?nextStep WHERE {
  SERVICE <https://federation-flow.example/services/policy> {
    ?policy a schema:Article ;
            schema:name ?policyTitle ;
            schema:about ?concept .
  }

  SERVICE <https://federation-flow.example/services/runbook> {
    ?runbook a schema:HowTo ;
             schema:name ?runbookTitle ;
             schema:about ?concept ;
             kb:nextStep ?nextStep .
  }

  SERVICE <https://federation-flow.example/services/storage> {
    ?storage a schema:TechArticle ;
             schema:name ?storageTitle ;
             schema:mentions ?concept .
  }

  SERVICE <https://federation-flow.example/services/ontology> {
    <https://federation-flow.example/ontology/federated-query-scheme/> a skos:ConceptScheme ;
                                                                  skos:prefLabel ?schemeLabel .
    ?concept a skos:Concept ;
             skos:prefLabel "Federated Queries" .
  }

  SERVICE <https://federation-flow.example/services/reference> {
    ?reference a schema:Article ;
               schema:about ?concept .
    ?concept skos:exactMatch ?referenceTarget .
  }
}
""";

    private const string AskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
ASK WHERE {
  SERVICE <https://federation-flow.example/services/policy> {
    ?policy a schema:Article ;
            schema:name "Federation Policy" ;
            schema:about ?concept .
  }

  SERVICE <https://federation-flow.example/services/runbook> {
    ?runbook a schema:HowTo ;
             schema:name "Federated Query Runbook" ;
             schema:about ?concept ;
             kb:nextStep <https://federation-flow.example/workflows/query-approval-checklist/> .
  }

  SERVICE <https://federation-flow.example/services/storage> {
    ?storage a schema:TechArticle ;
             schema:name "Storage Knowledge Graph" ;
             schema:mentions ?concept .
  }

  SERVICE <https://federation-flow.example/services/ontology> {
    <https://federation-flow.example/ontology/federated-query-scheme/> a skos:ConceptScheme ;
                                                                  skos:prefLabel "Federated Query Scheme" .
    ?concept a skos:Concept ;
             skos:prefLabel "Federated Queries" .
  }

  SERVICE <https://federation-flow.example/services/reference> {
    ?reference a schema:Article ;
               schema:about ?concept .
    ?concept skos:exactMatch <https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries> .
  }
}
""";

    private const string BindingsSelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?policyTitle WHERE {
  SERVICE <https://federation-flow.example/services/policy> {
    ?policy a schema:Article ;
            schema:name ?policyTitle ;
            schema:about ?concept .
  }
  VALUES ?concept { <https://federation-flow.example/id/federated-queries> }
}
""";

    private const string SilentNestedServiceQuery = """
SELECT ?status WHERE {
  BIND("suppressed" AS ?status)
  SERVICE SILENT <https://federation-flow.example/services/policy> {
    SERVICE <https://unbound.federation-flow.example/sparql> {
      ?s ?p ?o
    }
  }
}
""";

    [Test]
    public async Task Federated_query_execution_can_join_five_different_local_graphs_with_one_service_query()
    {
        var fixture = await BuildFixtureAsync();

        var result = await fixture.RootGraph.ExecuteFederatedSelectAsync(SelectQuery, fixture.Options);

        result.ServiceEndpointSpecifiers.ShouldBe([
            PolicyEndpointText,
            RunbookEndpointText,
            StorageEndpointText,
            OntologyEndpointText,
            ReferenceEndpointText,
        ]);
        result.Result.Rows.Count.ShouldBe(1);
        result.Result.Rows[0].Values["policyTitle"].ShouldBe("Federation Policy");
        result.Result.Rows[0].Values["runbookTitle"].ShouldBe("Federated Query Runbook");
        result.Result.Rows[0].Values["storageTitle"].ShouldBe("Storage Knowledge Graph");
        result.Result.Rows[0].Values["schemeLabel"].ShouldBe("Federated Query Scheme");
        result.Result.Rows[0].Values["referenceTarget"].ShouldBe(ReferenceTargetText);
        result.Result.Rows[0].Values["nextStep"].ShouldBe(NextStepText);
    }

    [Test]
    public async Task Federated_query_execution_can_answer_ask_across_five_different_local_graphs()
    {
        var fixture = await BuildFixtureAsync();

        var result = await fixture.RootGraph.ExecuteFederatedAskAsync(AskQuery, fixture.Options);

        result.Result.ShouldBeTrue();
        result.ServiceEndpointSpecifiers.ShouldBe([
            PolicyEndpointText,
            RunbookEndpointText,
            StorageEndpointText,
            OntologyEndpointText,
            ReferenceEndpointText,
        ]);
    }

    [Test]
    public async Task Federated_query_execution_keeps_local_service_bindings_behind_the_allowlist_boundary()
    {
        var fixture = await BuildFixtureAsync();

        var exception = await Should.ThrowAsync<FederatedSparqlQueryException>(async () =>
            await fixture.RootGraph.ExecuteFederatedSelectAsync(
                SelectQuery,
                fixture.Options with
                {
                    AllowedServiceEndpoints =
                    [
                        new Uri(PolicyEndpointText),
                        new Uri(RunbookEndpointText),
                    ],
                }));

        exception.ServiceEndpointSpecifiers.ShouldContain(StorageEndpointText);
        exception.Message.ShouldContain("allowlisted");
    }

    [Test]
    public async Task Federated_query_execution_can_use_bindings_to_drive_local_service_subqueries()
    {
        var fixture = await BuildFixtureAsync();

        var result = await fixture.RootGraph.ExecuteFederatedSelectAsync(BindingsSelectQuery, fixture.Options);

        result.Result.Rows.Count.ShouldBe(1);
        result.Result.Rows[0].Values["policyTitle"].ShouldBe("Federation Policy");
    }

    [Test]
    public async Task Federated_query_execution_supports_silent_local_service_failures()
    {
        var fixture = await BuildFixtureAsync(
            additionalAllowedEndpoints: [new Uri(NestedUnboundEndpointText)]);

        var result = await fixture.RootGraph.ExecuteFederatedSelectAsync(SilentNestedServiceQuery, fixture.Options);

        result.Result.Rows.Count.ShouldBe(1);
        result.Result.Rows[0].Values["status"].ShouldBe("suppressed");
    }

    private static async Task<FiveGraphFederationFixture> BuildFixtureAsync(
        IReadOnlyList<Uri>? additionalAllowedEndpoints = null)
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var policyGraph = (await pipeline.BuildAsync([new MarkdownSourceDocument(PolicyDocumentPath, PolicyMarkdown)])).Graph;
        var runbookGraph = (await pipeline.BuildAsync([new MarkdownSourceDocument(RunbookDocumentPath, RunbookMarkdown)])).Graph;
        var storageGraph = (await pipeline.BuildAsync([new MarkdownSourceDocument(StorageDocumentPath, StorageMarkdown)])).Graph;
        var ontologyGraph = (await pipeline.BuildAsync([new MarkdownSourceDocument(OntologyDocumentPath, OntologyMarkdown)])).Graph;
        var referenceGraph = (await pipeline.BuildAsync([new MarkdownSourceDocument(ReferenceDocumentPath, ReferenceMarkdown)])).Graph;
        var rootGraph = (await pipeline.BuildAsync([new MarkdownSourceDocument(EmptyRootDocumentPath, string.Empty)])).Graph;
        return new FiveGraphFederationFixture(
            rootGraph,
            new FederatedSparqlExecutionOptions
            {
                AllowedServiceEndpoints = CreateEndpoints(additionalAllowedEndpoints),
                LocalServiceBindings =
                [
                    new FederatedSparqlLocalServiceBinding(new Uri(PolicyEndpointText), policyGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(RunbookEndpointText), runbookGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(StorageEndpointText), storageGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(OntologyEndpointText), ontologyGraph),
                    new FederatedSparqlLocalServiceBinding(new Uri(ReferenceEndpointText), referenceGraph),
                ],
            });
    }

    private static IReadOnlyList<Uri> CreateEndpoints(IReadOnlyList<Uri>? additionalAllowedEndpoints)
    {
        List<Uri> endpoints =
        [
            new Uri(PolicyEndpointText),
            new Uri(RunbookEndpointText),
            new Uri(StorageEndpointText),
            new Uri(OntologyEndpointText),
            new Uri(ReferenceEndpointText),
        ];
        if (additionalAllowedEndpoints is not null)
        {
            endpoints.AddRange(additionalAllowedEndpoints);
        }

        return endpoints;
    }

    private sealed record FiveGraphFederationFixture(
        KnowledgeGraph RootGraph,
        FederatedSparqlExecutionOptions Options);
}
