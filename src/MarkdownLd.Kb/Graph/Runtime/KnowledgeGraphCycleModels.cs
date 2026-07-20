namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphCycleSearchOptions
{
    public static KnowledgeGraphCycleSearchOptions Default { get; } = new();

    public IReadOnlyList<string> PredicateIds { get; init; } = [PipelineConstants.KbNextStepText];

    public int MaxComponents { get; init; } = 100;
}

public sealed record KnowledgeGraphCycle(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<KnowledgeGraphEdge> Edges);
