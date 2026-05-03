using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record TokenDistanceSearchOptions
{
    public int Limit { get; init; } = DefaultMaxRelatedTokenSegments;

    public bool EnableFuzzyQueryCorrection { get; init; }

    public int MaxFuzzyEditDistance { get; init; } = KnowledgeGraphRankedSearchDefaults.DefaultMaxFuzzyEditDistance;

    public int MinimumFuzzyTokenLength { get; init; } = KnowledgeGraphRankedSearchDefaults.DefaultMinimumFuzzyTokenLength;

    public int MaxFuzzyCorrectionsPerToken { get; init; } = TokenDistanceSearchDefaults.DefaultMaxFuzzyCorrectionsPerToken;
}

internal static class TokenDistanceSearchDefaults
{
    public const int DefaultMaxFuzzyCorrectionsPerToken = 1;
}
