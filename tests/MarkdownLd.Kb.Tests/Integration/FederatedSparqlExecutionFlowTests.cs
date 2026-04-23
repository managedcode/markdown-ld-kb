using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class FederatedSparqlExecutionFlowTests
{
    private static readonly Uri BaseUri = new("https://kb.example/");

    private const string SourcePath = "docs/federation.md";
    private const string SourceMarkdown = """
---
title: Federation Policy
graph_groups:
  - Query Safety
---
# Federation Policy

The graph is local by default and federation must be explicit.
""";

    private const string ServiceQuery = """
SELECT ?s WHERE {
  SERVICE <https://example.com/sparql> {
    ?s ?p ?o
  }
}
""";

    private const string VariableServiceQuery = """
SELECT ?s WHERE {
  VALUES ?endpoint { <https://query.wikidata.org/sparql> }
  SERVICE ?endpoint {
    ?s ?p ?o
  }
}
""";

    private const string NestedServiceQuery = """
SELECT ?s WHERE {
  SERVICE <https://query.wikidata.org/sparql> {
    SERVICE <https://example.com/sparql> {
      ?s ?p ?o
    }
  }
}
""";

    private const string LocalSelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?subject WHERE {
  ?subject a schema:Article .
}
ORDER BY ?subject
""";

    private const string LocalAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  ?subject a schema:Article .
}
""";

    [Test]
    public async Task Local_query_execution_rejects_service_clauses()
    {
        var result = await BuildGraphAsync();

        var exception = await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteSelectAsync(ServiceQuery));

        exception.Message.ShouldContain("SERVICE");
    }

    [Test]
    public async Task Federated_query_execution_rejects_unallowlisted_service_endpoints_before_execution()
    {
        var result = await BuildGraphAsync();

        var exception = await Should.ThrowAsync<FederatedSparqlQueryException>(async () =>
            await result.Graph.ExecuteFederatedSelectAsync(ServiceQuery, FederatedSparqlProfiles.WikidataMain));

        exception.ServiceEndpointSpecifiers.ShouldContain("https://example.com/sparql");
        exception.Message.ShouldContain("allowlisted");
    }

    [Test]
    public async Task Federated_query_execution_rejects_variable_service_specifiers_at_the_local_boundary()
    {
        var result = await BuildGraphAsync();

        var exception = await Should.ThrowAsync<FederatedSparqlQueryException>(async () =>
            await result.Graph.ExecuteFederatedSelectAsync(
                VariableServiceQuery,
                FederatedSparqlProfiles.WikidataMainAndScholarly));

        exception.ServiceEndpointSpecifiers.ShouldContain("?endpoint");
        exception.Message.ShouldContain("absolute endpoint URIs");
    }

    [Test]
    public async Task Federated_query_execution_rejects_nested_unallowlisted_service_endpoints_before_execution()
    {
        var result = await BuildGraphAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Should.ThrowAsync<FederatedSparqlQueryException>(async () =>
            await result.Graph.ExecuteFederatedSelectAsync(
                NestedServiceQuery,
                FederatedSparqlProfiles.WikidataMain,
                cancellation.Token));

        exception.ServiceEndpointSpecifiers.ShouldContain("https://example.com/sparql");
        exception.Message.ShouldContain("allowlisted");
    }

    [Test]
    public async Task Federated_query_execution_can_run_local_read_only_queries_and_reports_empty_service_diagnostics()
    {
        var result = await BuildGraphAsync();

        var selectResult = await result.Graph.ExecuteFederatedSelectAsync(
            LocalSelectQuery,
            FederatedSparqlProfiles.WikidataMainAndScholarly);

        selectResult.ServiceEndpointSpecifiers.ShouldBeEmpty();
        selectResult.Result.Rows.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Local_ask_execution_rejects_select_queries()
    {
        var result = await BuildGraphAsync();

        var exception = await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteAskAsync(LocalSelectQuery));

        exception.Message.ShouldContain("ExecuteAskAsync");
    }

    [Test]
    public async Task Local_select_execution_rejects_ask_queries()
    {
        var result = await BuildGraphAsync();

        var exception = await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteSelectAsync(LocalAskQuery));

        exception.Message.ShouldContain("ExecuteSelectAsync");
    }

    [Test]
    public async Task Federated_ask_execution_rejects_select_queries()
    {
        var result = await BuildGraphAsync();

        var exception = await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteFederatedAskAsync(LocalSelectQuery, FederatedSparqlProfiles.WikidataMainAndScholarly));

        exception.Message.ShouldContain("ExecuteFederatedAskAsync");
    }

    [Test]
    public async Task Federated_select_execution_rejects_ask_queries()
    {
        var result = await BuildGraphAsync();

        var exception = await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteFederatedSelectAsync(LocalAskQuery, FederatedSparqlProfiles.WikidataMainAndScholarly));

        exception.Message.ShouldContain("ExecuteFederatedSelectAsync");
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildGraphAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        return pipeline.BuildAsync([
            new MarkdownSourceDocument(SourcePath, SourceMarkdown),
        ]);
    }
}
