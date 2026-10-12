using System.Net;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shouldly;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Writing;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class FederatedRemoteHttpBindingTests
{
    [Test]
    public async Task Explicit_HTTP_binding_executes_real_remote_SPARQL_and_preserves_caller_client_ownership()
    {
        using var remote = KnowledgeGraph.LoadJsonLd("""{ "@id":"https://remote.example/weather", "https://schema.org/name":"Weather" }""");
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using var server = builder.Build();
        var requests = 0;
        server.MapMethods("/sparql", ["GET", "POST"], async (HttpContext context) =>
        {
            Interlocked.Increment(ref requests);
            var query = context.Request.Query["query"].ToString();
            if (string.IsNullOrEmpty(query))
            {
                query = (await context.Request.ReadFormAsync())["query"].ToString();
            }
            using var graph = remote.CreateSnapshot();
            var processor = new LeviathanQueryProcessor(new InMemoryDataset(graph), options => options.UsePLinqEvaluation = false);
            var result = (SparqlResultSet)processor.ProcessQuery(new SparqlQueryParser().ParseFromString(query));
            using var writer = new System.IO.StringWriter();
            new SparqlJsonWriter().Save(result, writer);
            context.Response.ContentType = "application/sparql-results+json";
            await context.Response.WriteAsync(writer.ToString());
        });
        await server.StartAsync();
        var endpoint = new Uri(server.Urls.Single() + "/sparql");
        using var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false });
        using var local = KnowledgeGraph.LoadJsonLd("""{ "@id":"https://local.example/base", "https://schema.org/name":"Base" }""");
        var query = $"SELECT ?name WHERE {{ SERVICE <{endpoint}> {{ ?s <https://schema.org/name> ?name }} }}";
        var missing = new FederatedSparqlExecutionOptions { AllowedServiceEndpoints = [endpoint], RequireExplicitRemoteServiceBindings = true };
        await Should.ThrowAsync<FederatedSparqlQueryException>(() => local.ExecuteFederatedSelectAsync(query, missing));
        requests.ShouldBe(0);
        var configured = missing with { RemoteServiceBindings = [new(endpoint, client)] };
        var result = await local.ExecuteFederatedSelectAsync(query, configured);
        result.Result.Rows.Single().Values["name"].ShouldBe("Weather");
        requests.ShouldBe(1);
        (await local.ExecuteFederatedSelectAsync(query, configured)).Result.Rows.Count.ShouldBe(1);
        requests.ShouldBe(2);
        await server.StopAsync();
    }

    [Test]
    public async Task Duplicate_local_and_remote_bindings_fail_without_contacting_the_endpoint()
    {
        using var graph = KnowledgeGraph.LoadJsonLd("""{ "@id":"https://local.example/base", "https://schema.org/name":"Base" }""");
        var endpoint = new Uri("https://remote.example/sparql");
        using var client = new HttpClient();
        var options = new FederatedSparqlExecutionOptions
        {
            AllowedServiceEndpoints = [endpoint],
            LocalServiceBindings = [new(endpoint, graph)],
            RemoteServiceBindings = [new(endpoint, client)]
        };
        await Should.ThrowAsync<InvalidOperationException>(() => graph.ExecuteFederatedSelectAsync(
            $"SELECT ?name WHERE {{ SERVICE <{endpoint}> {{ ?s <https://schema.org/name> ?name }} }}", options));
    }
}
