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
