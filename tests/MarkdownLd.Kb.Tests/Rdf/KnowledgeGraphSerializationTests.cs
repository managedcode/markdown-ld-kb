using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Rdf;
using Shouldly;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public sealed class KnowledgeGraphSerializationTests
{
    private const string SchemaPrefixQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?entity WHERE {
  <https://example.com/articles/what-is-a-knowledge-graph/> schema:mentions ?entity
}
""";
    private const string SchemaPrefix = "@prefix schema:";
    private const string KnowledgeGraphTitle = "What is a Knowledge Graph?";
    private const string RdfLabel = "RDF";
    private const string SparqlLabel = "SPARQL";
    private const string SchemaArticleType = "schema:Article";

    [Test]
    public void SerializeTurtleRoundTripsThroughParser()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var turtle = KnowledgeGraphSerialization.SerializeTurtle(graph);

        turtle.ShouldContain(SchemaPrefix);
        turtle.ShouldContain(KnowledgeGraphTitle);

        var parsed = new Graph();
        new TurtleParser().Load(parsed, new StringReader(turtle));

        var executor = new ManagedCode.MarkdownLd.Kb.Query.SparqlQueryExecutor(parsed);
        var result = executor.ExecuteRawReadOnly(SchemaPrefixQuery);

        result.Results.Count.ShouldBe(2);
    }

    [Test]
    public void SerializeJsonLdProducesReadableJsonDocument()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var jsonLd = KnowledgeGraphSerialization.SerializeJsonLd(graph);

        using var document = JsonDocument.Parse(jsonLd);
        document.RootElement.ValueKind.ShouldNotBe(JsonValueKind.Undefined);

        ContainsStringValue(document.RootElement, KnowledgeGraphTitle).ShouldBeTrue();
        ContainsStringValue(document.RootElement, RdfLabel).ShouldBeTrue();
        ContainsStringValue(document.RootElement, SparqlLabel).ShouldBeTrue();
    }

    [Test]
    public void WriteMethodsProduceOutput()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();

        using var turtleWriter = new StringWriter();
        KnowledgeGraphSerialization.WriteTurtle(graph, turtleWriter);
        turtleWriter.ToString().ShouldContain(SchemaArticleType);

        using var jsonWriter = new StringWriter();
        KnowledgeGraphSerialization.WriteJsonLd(graph, jsonWriter);
        jsonWriter.ToString().ShouldContain(KnowledgeGraphTitle);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void High_fanout_and_cross_batch_blank_nodes_roundtrip_without_lost_triples(bool named)
    {
        using var graph = new Graph(named ? new UriNode(new Uri("https://example.com/graph")) : null);
        var subject = graph.CreateUriNode(new Uri("https://example.com/root"));
        var predicate = graph.CreateUriNode(new Uri("https://example.com/contains"));
        var head = graph.CreateBlankNode("head");
        var tail = graph.CreateBlankNode("tail");
        var first = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfListFirst));
        var rest = graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfListRest));
        graph.Assert(subject, predicate, head);
        graph.Assert(head, first, graph.CreateLiteralNode("bonjour", "fr"));
        graph.Assert(head, rest, tail);
        graph.Assert(tail, first, graph.CreateLiteralNode("42", new Uri(XmlSpecsHelper.XmlSchemaDataTypeInteger)));
        graph.Assert(tail, rest, graph.CreateUriNode(new Uri(RdfSpecsHelper.RdfListNil)));
        for (var index = 0; index < 4096; index++)
        {
            graph.Assert(subject, predicate, graph.CreateUriNode(new Uri($"https://example.com/item/{index}")));
        }

        graph.Assert(graph.CreateUriNode(new Uri("https://example.com/other")), predicate, tail);
        graph.Triples.Count.ShouldBe(4102);
        var json = KnowledgeGraphSerialization.SerializeJsonLd(graph);
        using var store = new TripleStore();
        new JsonLdParser().Load(store, new StringReader(json));
        store.Graphs.Single().Name.ShouldBe(graph.Name);
        store.Graphs.Single().Equals(graph).ShouldBeTrue();
    }

    private static bool ContainsStringValue(JsonElement element, string expected)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => string.Equals(element.GetString(), expected, StringComparison.Ordinal),
            JsonValueKind.Array => element.EnumerateArray().Any(child => ContainsStringValue(child, expected)),
            JsonValueKind.Object => element.EnumerateObject().Any(property => ContainsStringValue(property.Value, expected)),
            _ => false
        };
    }
}
