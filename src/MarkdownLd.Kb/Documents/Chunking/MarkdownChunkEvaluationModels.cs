namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownChunkEvaluationOptions
{
    public static MarkdownChunkEvaluationOptions Default { get; } = new();

    public MarkdownParsingOptions ParsingOptions { get; init; } = new();

    public int SmallTokenThreshold { get; init; } = MarkdownChunkEvaluationDefaults.DefaultSmallTokenThreshold;

    public int LargeTokenThreshold { get; init; } = MarkdownChunkEvaluationDefaults.DefaultLargeTokenThreshold;

    public int QualitySampleSize { get; init; } = MarkdownChunkEvaluationDefaults.DefaultQualitySampleSize;

    public int QualitySampleSeed { get; init; } = MarkdownChunkEvaluationDefaults.DefaultQualitySampleSeed;

    public int PreviewCharacterLimit { get; init; } = MarkdownChunkEvaluationDefaults.DefaultPreviewCharacterLimit;
}

public sealed record MarkdownChunkCoverageExpectation(
    string Question,
    string ExpectedAnswer);

public sealed record MarkdownChunkEvaluationResult(
    MarkdownChunkSizeDistribution SizeDistribution,
    IReadOnlyList<MarkdownChunkCoverageResult> CoverageResults,
    IReadOnlyList<MarkdownChunkQualitySample> QualitySamples)
{
    public double CoverageRate =>
        CoverageResults.Count == 0
            ? MarkdownChunkEvaluationDefaults.ZeroRate
            : (double)CoverageResults.Count(static result => result.Found) / CoverageResults.Count;
}

public sealed record MarkdownChunkSizeDistribution(
    int Total,
    int TooSmall,
    int InRange,
    int TooLarge,
    int MinTokens,
    int MaxTokens,
    double AverageTokens,
    double MedianTokens);

public sealed record MarkdownChunkCoverageResult(
    string Question,
    string ExpectedAnswer,
    bool Found,
    string? FoundChunkId,
    int? FoundChunkOrder,
    IReadOnlyList<string> HeadingPath,
    string? Preview);

public sealed record MarkdownChunkQualitySample(
    string ChunkId,
    int Order,
    IReadOnlyList<string> HeadingPath,
    int EstimatedTokenCount,
    bool StartsAbruptly,
    bool EndsAbruptly,
    string Preview);

internal static class MarkdownChunkEvaluationDefaults
{
    public const int DefaultSmallTokenThreshold = 100;
    public const int DefaultLargeTokenThreshold = 600;
    public const int DefaultQualitySampleSize = 10;
    public const int DefaultQualitySampleSeed = 42;
    public const int DefaultPreviewCharacterLimit = 300;
    public const int ZeroTokenCount = 0;
    public const double ZeroRate = 0;
    public const string CodeFencePrefix = "```";
    public const string InvalidThresholdRangeMessage = "Large token threshold must be greater than or equal to small token threshold.";
}
