namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphRankedSearchOptions
{
    public KnowledgeGraphSearchMode Mode { get; init; } = KnowledgeGraphSearchMode.Graph;

    public int MaxResults { get; init; } = PipelineConstants.DefaultRankedSearchMaxResults;

    public int MaxSemanticResults { get; init; } = PipelineConstants.DefaultSemanticSearchMaxResults;

    public double MinimumSemanticScore { get; init; } = PipelineConstants.DefaultMinimumSemanticScore;
}

public sealed record KnowledgeGraphRankedSearchMatch(
    string NodeId,
    string Label,
    string? Description,
    KnowledgeGraphRankedSearchSource Source,
    double Score,
    double? CanonicalScore = null,
    double? SemanticScore = null);

public enum KnowledgeGraphSearchMode
{
    Graph,
    Semantic,
    Hybrid,
}

public enum KnowledgeGraphRankedSearchSource
{
    Canonical,
    Semantic,
    Merged,
}
