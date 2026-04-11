using System.Globalization;
using VDS.RDF;
using VDS.RDF.Writing;

namespace ManagedCode.MarkdownLd.Kb.Rdf;

public static class KnowledgeGraphSerialization
{
    public static string SerializeTurtle(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var writer = new CompressingTurtleWriter();
        using var stringWriter = new System.IO.StringWriter(CultureInfo.InvariantCulture);
        writer.Save(graph, stringWriter);
        return stringWriter.ToString();
    }

    public static string SerializeJsonLd(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var writer = new JsonLdWriter();
        return writer.SerializeStore(CreateStore(graph)).ToString();
    }

    public static void WriteTurtle(IGraph graph, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(writer);

        var turtleWriter = new CompressingTurtleWriter();
        turtleWriter.Save(graph, writer);
    }

    public static void WriteJsonLd(IGraph graph, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(writer);

        var jsonWriter = new JsonLdWriter();
        jsonWriter.Save(CreateStore(graph), writer, false);
    }

    private static TripleStore CreateStore(IGraph graph)
    {
        var store = new TripleStore();
        store.Add(graph);
        return store;
    }
}
