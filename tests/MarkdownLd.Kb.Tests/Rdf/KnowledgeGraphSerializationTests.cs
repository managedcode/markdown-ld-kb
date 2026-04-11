using System.Text.Json;
using Shouldly;
using ManagedCode.MarkdownLd.Kb.Rdf;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public sealed class KnowledgeGraphSerializationTests
{
    [Test]
    public void SerializeTurtleRoundTripsThroughParser()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var turtle = KnowledgeGraphSerialization.SerializeTurtle(graph);

        turtle.ShouldContain("@prefix schema:");
        turtle.ShouldContain("What is a Knowledge Graph?");

        var parsed = new Graph();
        new TurtleParser().Load(parsed, new StringReader(turtle));

        var executor = new ManagedCode.MarkdownLd.Kb.Query.SparqlQueryExecutor(parsed);
        var result = executor.ExecuteRawReadOnly($$"""
PREFIX schema: <{{KbNamespaces.Schema}}>
SELECT ?entity WHERE {
  <https://example.com/articles/what-is-a-knowledge-graph/> schema:mentions ?entity
}
""");

        result.Results.Count.ShouldBe(2);
    }

    [Test]
    public void SerializeJsonLdProducesReadableJsonDocument()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var jsonLd = KnowledgeGraphSerialization.SerializeJsonLd(graph);

        using var document = JsonDocument.Parse(jsonLd);
        document.RootElement.ValueKind.ShouldNotBe(JsonValueKind.Undefined);

        ContainsStringValue(document.RootElement, "What is a Knowledge Graph?").ShouldBeTrue();
        ContainsStringValue(document.RootElement, "RDF").ShouldBeTrue();
        ContainsStringValue(document.RootElement, "SPARQL").ShouldBeTrue();
    }

    [Test]
    public void WriteMethodsProduceOutput()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();

        using var turtleWriter = new StringWriter();
        KnowledgeGraphSerialization.WriteTurtle(graph, turtleWriter);
        turtleWriter.ToString().ShouldContain("schema:Article");

        using var jsonWriter = new StringWriter();
        KnowledgeGraphSerialization.WriteJsonLd(graph, jsonWriter);
        jsonWriter.ToString().ShouldContain("What is a Knowledge Graph?");
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
