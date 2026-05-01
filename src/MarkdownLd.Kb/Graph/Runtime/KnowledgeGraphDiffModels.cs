namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphDiff(
    IReadOnlyList<KnowledgeGraphNode> AddedNodes,
    IReadOnlyList<KnowledgeGraphNode> RemovedNodes,
    IReadOnlyList<KnowledgeGraphEdge> AddedEdges,
    IReadOnlyList<KnowledgeGraphEdge> RemovedEdges,
    IReadOnlyList<KnowledgeGraphChangedLiteralEdge> ChangedLiteralEdges)
{
    public static KnowledgeGraphDiff Empty { get; } = new([], [], [], [], []);

    public static KnowledgeGraphDiff Compare(KnowledgeGraphSnapshot previous, KnowledgeGraphSnapshot current)
    {
        return KnowledgeGraphDiffComparer.Compare(previous, current);
    }
}

public sealed record KnowledgeGraphChangedLiteralEdge(
    string SubjectId,
    string PredicateId,
    string OldValue,
    string NewValue);
