using System.Collections.Immutable;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private KnowledgeGraphSnapshot? _semanticSnapshot;

    public KnowledgeGraphSnapshot ToSemanticSnapshot()
    {
        _graphLock.EnterReadLock();
        try
        {
            if (Volatile.Read(ref _semanticSnapshot) is { } cached)
            {
                return cached;
            }
            var snapshot = BuildSemanticSnapshot();
            return Interlocked.CompareExchange(ref _semanticSnapshot, snapshot, null) ?? snapshot;
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private KnowledgeGraphSnapshot BuildSemanticSnapshot()
    {
        var complete = CreateGraphSnapshot(_graph.Triples);
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in complete.Nodes)
        {
            if (IsRetrievalInternalNode(node.Id))
            {
                excluded.Add(node.Id);
            }
        }
        var keys = new HashSet<(string, string, string)>();
        var included = new HashSet<string>(StringComparer.Ordinal);
        var edges = ImmutableArray.CreateBuilder<KnowledgeGraphEdge>();
        foreach (var edge in complete.Edges)
        {
            if (!excluded.Contains(edge.SubjectId) && !excluded.Contains(edge.ObjectId)
                && keys.Add((edge.SubjectId, edge.PredicateId, edge.ObjectId)))
            {
                edges.Add(edge);
                included.Add(edge.SubjectId);
                included.Add(edge.ObjectId);
            }
        }
        var nodes = complete.Nodes.Where(node => included.Contains(node.Id)).ToImmutableArray();
        return new KnowledgeGraphSnapshot(nodes, edges.ToImmutable());
    }

    private static bool IsRetrievalInternalNode(string nodeId)
    {
        if (!Uri.TryCreate(nodeId, UriKind.Absolute, out var uri))
        {
            return false;
        }

        foreach (var segment in uri.Segments)
        {
            if (segment.Equals(PipelineConstants.TokenSectionIdPrefix, StringComparison.Ordinal) ||
                segment.Equals(PipelineConstants.TokenSegmentIdPrefix, StringComparison.Ordinal) ||
                segment.Equals(PipelineConstants.TokenTopicIdPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
