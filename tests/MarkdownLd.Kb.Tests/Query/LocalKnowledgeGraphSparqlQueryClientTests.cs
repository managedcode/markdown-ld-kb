using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;
using VDS.RDF.Parsing;
using VDS.RDF.Parsing.Handlers;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class FederatedLocalSparqlQueryClientFlowTests
{
    private static readonly Uri BaseUri = new("https://local-client.example/");

    private const string EndpointText = "https://local-client.example/services/local";
    private const string MissingEndpointText = "https://local-client.example/services/missing";
    private const string SourcePath = "docs/local-client.md";
    private const string SourceMarkdown = """
---
title: Local Client Source
---
# Local Client Source

This graph is used to exercise the local federated SPARQL client.
""";

    private const string SelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?subject WHERE {
  ?subject a schema:Article .
}
""";

    private const string AskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  ?subject a schema:Article .
}
""";

    private const string MutatingQuery = "INSERT DATA { <a> <b> <c> }";
    private const string ConstructQuery = "CONSTRUCT { ?s ?p ?o } WHERE { ?s ?p ?o }";
    private const string MissingEndpointServiceQuery = """
SELECT ?s WHERE {
  SERVICE <https://local-client.example/services/missing> {
    ?s ?p ?o
  }
}
""";

    [Test]
    public async Task Local_client_can_stream_select_results_into_a_builtin_result_set_handler()
    {
        var client = await CreateClientAsync();
        var resultSet = new SparqlResultSet();

        await client.StreamResultSetAsync(SelectQuery, new ResultSetHandler(resultSet), CancellationToken.None);

        resultSet.Count.ShouldBe(1);
        resultSet.Variables.ShouldContain("subject");
    }

    [Test]
    public async Task Local_client_can_stream_ask_results_into_a_builtin_result_set_handler()
    {
        var client = await CreateClientAsync();
        var resultSet = new SparqlResultSet();

        await client.StreamResultSetAsync(AskQuery, new ResultSetHandler(resultSet), CancellationToken.None);

        resultSet.Result.ShouldBeTrue();
    }

    [Test]
    public async Task Local_client_rejects_non_result_set_and_mutating_queries_explicitly()
    {
        var client = await CreateClientAsync();

        await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await client.ExecuteResultSetAsync(ConstructQuery, CancellationToken.None));
        await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await client.ExecuteResultSetAsync(MutatingQuery, CancellationToken.None));
    }

    [Test]
    public async Task Local_service_registry_rejects_duplicate_endpoints_and_returns_false_for_unknown_endpoint_uris()
    {
        var graph = await BuildGraphAsync();
        var endpoint = new Uri(EndpointText);
        var duplicateException = Should.Throw<InvalidOperationException>(() =>
            KnowledgeGraphFederatedLocalServiceRegistry.Create(
                [
                    new FederatedSparqlLocalServiceBinding(endpoint, graph),
                    new FederatedSparqlLocalServiceBinding(endpoint, graph),
                ],
                1000));

        duplicateException.Message.ShouldContain(endpoint.AbsoluteUri);

        var registry = KnowledgeGraphFederatedLocalServiceRegistry.Create(
            [new FederatedSparqlLocalServiceBinding(endpoint, graph)],
            1000);
        var parsed = new SparqlQueryParser().ParseFromString(MissingEndpointServiceQuery);
        var context = new SparqlEvaluationContext(
            parsed,
            new InMemoryDataset(graph.CreateSnapshot()),
            new LeviathanQueryOptions());

        registry.ShouldNotBeNull();
        registry.TryResolve(parsed.RootGraphPattern.ChildGraphPatterns[0].GraphSpecifier!, context, out _).ShouldBeFalse();
    }

    private static async Task<LocalKnowledgeGraphSparqlQueryClient> CreateClientAsync()
    {
        var graph = await BuildGraphAsync();
        var registry = KnowledgeGraphFederatedLocalServiceRegistry.Create(
            [new FederatedSparqlLocalServiceBinding(new Uri(EndpointText), graph)],
            1000);

        registry.ShouldNotBeNull();
        return new LocalKnowledgeGraphSparqlQueryClient(graph, registry, 1000);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        return (await pipeline.BuildAsync([
            new MarkdownSourceDocument(SourcePath, SourceMarkdown),
        ])).Graph;
    }
}
