using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public static KnowledgeGraph FromSnapshot(KnowledgeGraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var graph = new Graph();
        foreach (var edge in snapshot.Edges)
        {
            if (!Uri.TryCreate(edge.PredicateId, UriKind.Absolute, out var predicateUri))
            {
                throw new InvalidOperationException(SnapshotPredicateMustBeUriMessagePrefix + edge.PredicateId);
            }

            graph.Assert(
                CreateSnapshotNode(graph, edge.SubjectId),
                graph.CreateUriNode(predicateUri),
                CreateSnapshotNode(graph, edge.ObjectId));
        }

        return new KnowledgeGraph(graph);
    }

    public static string SerializeTurtle(KnowledgeGraphSnapshot snapshot)
    {
        return FromSnapshot(snapshot).SerializeTurtle();
    }

    public static string SerializeJsonLd(KnowledgeGraphSnapshot snapshot)
    {
        return FromSnapshot(snapshot).SerializeJsonLd();
    }

    private static INode CreateSnapshotNode(Graph graph, string nodeId)
    {
        if (nodeId.StartsWith(LiteralNodePrefix, StringComparison.Ordinal))
        {
            return graph.CreateLiteralNode(nodeId[LiteralNodePrefix.Length..]);
        }

        if (nodeId.StartsWith(BlankNodePrefix, StringComparison.Ordinal))
        {
            return graph.CreateBlankNode(nodeId[BlankNodePrefix.Length..]);
        }

        if (Uri.TryCreate(nodeId, UriKind.Absolute, out var uri))
        {
            return graph.CreateUriNode(uri);
        }

        throw new InvalidOperationException(SnapshotNodeUnsupportedMessagePrefix + nodeId);
    }
}
