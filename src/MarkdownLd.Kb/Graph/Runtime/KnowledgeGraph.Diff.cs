using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public KnowledgeGraphDiff Diff(KnowledgeGraph other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return KnowledgeGraphDiffComparer.Compare(ToSnapshot(), other.ToSnapshot());
    }

    public static KnowledgeGraphDiff Diff(KnowledgeGraphSnapshot previous, KnowledgeGraphSnapshot current)
    {
        return KnowledgeGraphDiffComparer.Compare(previous, current);
    }
}

internal static class KnowledgeGraphDiffComparer
{
    public static KnowledgeGraphDiff Compare(KnowledgeGraphSnapshot previous, KnowledgeGraphSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var previousNodeIds = previous.Nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        var currentNodeIds = current.Nodes.Select(static node => node.Id).ToHashSet(StringComparer.Ordinal);
        var previousEdges = previous.Edges.Select(static edge => new EdgeIdentity(edge)).ToHashSet();
        var currentEdges = current.Edges.Select(static edge => new EdgeIdentity(edge)).ToHashSet();
        var addedEdges = current.Edges.Where(edge => !previousEdges.Contains(new EdgeIdentity(edge))).ToArray();
        var removedEdges = previous.Edges.Where(edge => !currentEdges.Contains(new EdgeIdentity(edge))).ToArray();

        return new KnowledgeGraphDiff(
            current.Nodes.Where(node => !previousNodeIds.Contains(node.Id)).ToArray(),
            previous.Nodes.Where(node => !currentNodeIds.Contains(node.Id)).ToArray(),
            addedEdges,
            removedEdges,
            CreateChangedLiteralEdges(removedEdges, addedEdges));
    }

    private static IReadOnlyList<KnowledgeGraphChangedLiteralEdge> CreateChangedLiteralEdges(
        IReadOnlyList<KnowledgeGraphEdge> removedEdges,
        IReadOnlyList<KnowledgeGraphEdge> addedEdges)
    {
        var addedBySubjectPredicate = addedEdges
            .Where(static edge => edge.ObjectId.StartsWith(LiteralNodePrefix, StringComparison.Ordinal))
            .GroupBy(static edge => new LiteralEdgeKey(edge.SubjectId, edge.PredicateId))
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var changed = new List<KnowledgeGraphChangedLiteralEdge>();

        foreach (var removed in removedEdges.Where(static edge => edge.ObjectId.StartsWith(LiteralNodePrefix, StringComparison.Ordinal)))
        {
            var key = new LiteralEdgeKey(removed.SubjectId, removed.PredicateId);
            if (!addedBySubjectPredicate.TryGetValue(key, out var candidates))
            {
                continue;
            }

            foreach (var added in candidates)
            {
                changed.Add(new KnowledgeGraphChangedLiteralEdge(
                    removed.SubjectId,
                    removed.PredicateId,
                    RemoveLiteralPrefix(removed.ObjectId),
                    RemoveLiteralPrefix(added.ObjectId)));
            }
        }

        return changed
            .OrderBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.PredicateId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.NewValue, StringComparer.Ordinal)
            .ToArray();
    }

    private static string RemoveLiteralPrefix(string value)
    {
        return value[LiteralNodePrefix.Length..];
    }

    private sealed record EdgeIdentity(string SubjectId, string PredicateId, string ObjectId)
    {
        public EdgeIdentity(KnowledgeGraphEdge edge)
            : this(edge.SubjectId, edge.PredicateId, edge.ObjectId)
        {
        }
    }

    private sealed record LiteralEdgeKey(string SubjectId, string PredicateId);
}
