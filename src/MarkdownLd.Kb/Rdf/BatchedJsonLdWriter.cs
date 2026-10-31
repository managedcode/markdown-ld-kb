using Newtonsoft.Json;
using VDS.RDF;
using VDS.RDF.Writing;

namespace ManagedCode.MarkdownLd.Kb.Rdf;

internal static class BatchedJsonLdWriter
{
    // A serialization batch bounds duplicate checks, never the number of graph triples.
    private const int TriplesPerBatch = 512;

    public static void Write(IGraph graph, TextWriter writer)
    {
        using var output = new JsonTextWriter(writer) { CloseOutput = false };
        output.WriteStartArray();
        var serializer = new JsonLdWriter();
        foreach (var triples in graph.Triples.Where(static triple => !HasBlankNode(triple)).Chunk(TriplesPerBatch))
        {
            WriteBatch(triples, graph.Name, serializer, output);
        }

        // Keep every reference to blank nodes together: JSON-LD list compaction needs
        // the complete reference set to preserve shared list nodes across subjects.
        WriteBatch(graph.Triples.Where(HasBlankNode), graph.Name, serializer, output);

        output.WriteEndArray();
    }
    private static bool HasBlankNode(Triple triple) =>
        triple.Subject.NodeType == NodeType.Blank || triple.Predicate.NodeType == NodeType.Blank ||
        triple.Object.NodeType == NodeType.Blank;

    private static void WriteBatch(IEnumerable<Triple> triples, IRefNode? name, JsonLdWriter serializer, JsonWriter output)
    {
        using var batch = new Graph(name);
        batch.Assert(triples);
        using var store = new TripleStore();
        store.Add(batch);
        foreach (var node in serializer.SerializeStore(store))
        {
            node.WriteTo(output);
        }
    }

}
