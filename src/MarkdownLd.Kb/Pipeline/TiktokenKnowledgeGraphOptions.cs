using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record TiktokenKnowledgeGraphOptions
{
    public string ModelName { get; init; } = TiktokenModelName;

    public TokenVectorWeighting Weighting { get; init; } = DefaultTokenVectorWeighting;

    public int MaxRelatedSegments { get; init; } = DefaultMaxRelatedTokenSegments;

    public int MinimumTokenCount { get; init; } = DefaultMinimumTokenCount;

    public double MaximumRelatedDistance { get; init; } = MaximumNormalizedTokenDistance;

    public int MaxTopicLabelsPerSegment { get; init; } = DefaultMaxTopicLabelsPerSegment;

    public int MaxTopicPhraseWords { get; init; } = DefaultMaxTopicPhraseWords;

    public int MinimumTopicWordLength { get; init; } = DefaultMinimumTopicWordLength;
}
