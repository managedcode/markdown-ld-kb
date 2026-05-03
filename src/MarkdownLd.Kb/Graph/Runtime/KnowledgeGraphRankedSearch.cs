namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphRankedSearchOptions
{
    public KnowledgeGraphSearchMode Mode { get; init; } = KnowledgeGraphSearchMode.Graph;

    public int MaxResults { get; init; } = PipelineConstants.DefaultRankedSearchMaxResults;

    public int MaxSemanticResults { get; init; } = PipelineConstants.DefaultSemanticSearchMaxResults;

    public double MinimumSemanticScore { get; init; } = PipelineConstants.DefaultMinimumSemanticScore;

    public KnowledgeGraphHybridFusionStrategy HybridFusionStrategy { get; init; } = KnowledgeGraphHybridFusionStrategy.CanonicalFirst;

    public int ReciprocalRankFusionRankOffset { get; init; } = KnowledgeGraphRankedSearchDefaults.DefaultReciprocalRankFusionRankOffset;

    public IReadOnlyCollection<string>? CandidateNodeIds { get; init; }

    public bool EnableFuzzyTokenMatching { get; init; }

    public int MaxFuzzyEditDistance { get; init; } = KnowledgeGraphRankedSearchDefaults.DefaultMaxFuzzyEditDistance;

    public int MinimumFuzzyTokenLength { get; init; } = KnowledgeGraphRankedSearchDefaults.DefaultMinimumFuzzyTokenLength;
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
    Bm25,
    Semantic,
    Hybrid,
}

public enum KnowledgeGraphHybridFusionStrategy
{
    CanonicalFirst,
    ReciprocalRank,
}

public enum KnowledgeGraphRankedSearchSource
{
    Canonical,
    Bm25,
    Semantic,
    Merged,
}

internal static class KnowledgeGraphRankedSearchDefaults
{
    public const int DefaultReciprocalRankFusionRankOffset = 60;
    public const int DefaultMaxFuzzyEditDistance = 1;
    public const int DefaultMinimumFuzzyTokenLength = 4;
    public const string CandidateNodeIdsCannotContainEmptyMessage = "Ranked search candidate node filters cannot contain empty values.";
}
