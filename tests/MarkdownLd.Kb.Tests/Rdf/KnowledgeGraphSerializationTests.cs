using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Rdf;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public sealed class KnowledgeGraphSerializationTests
{
    [Fact]
    public void SerializeTurtleRoundTripsThroughParser()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var turtle = KnowledgeGraphSerialization.SerializeTurtle(graph);

        Assert.Contains("@prefix schema:", turtle);
        Assert.Contains("What is a Knowledge Graph?", turtle);

        var parsed = new Graph();
        new TurtleParser().Load(parsed, new StringReader(turtle));

        var executor = new ManagedCode.MarkdownLd.Kb.Query.SparqlQueryExecutor(parsed);
        var result = executor.ExecuteRawReadOnly($"""
PREFIX schema: <{KbNamespaces.Schema}>
SELECT ?entity WHERE {{
  <https://example.com/articles/what-is-a-knowledge-graph/> schema:mentions ?entity
}}
""");

        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public void SerializeJsonLdProducesReadableJsonDocument()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var jsonLd = KnowledgeGraphSerialization.SerializeJsonLd(graph);

        using var document = JsonDocument.Parse(jsonLd);
        Assert.NotNull(document.RootElement);

        Assert.True(ContainsStringValue(document.RootElement, "What is a Knowledge Graph?"));
        Assert.True(ContainsStringValue(document.RootElement, "RDF"));
        Assert.True(ContainsStringValue(document.RootElement, "SPARQL"));
    }

    [Fact]
    public void WriteMethodsProduceOutput()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();

        using var turtleWriter = new StringWriter();
        KnowledgeGraphSerialization.WriteTurtle(graph, turtleWriter);
        Assert.Contains("schema:Article", turtleWriter.ToString());

        using var jsonWriter = new StringWriter();
        KnowledgeGraphSerialization.WriteJsonLd(graph, jsonWriter);
        Assert.Contains("What is a Knowledge Graph?", jsonWriter.ToString());
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
