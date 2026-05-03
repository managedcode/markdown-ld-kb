using ManagedCode.MarkdownLd.Kb.Parsing;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownChunkEvaluator(IMarkdownChunker? chunker = null)
{
    private static readonly char[] EndPunctuation =
    [
        '.', '!', '?', ':', ';', ')', ']', '}', '`',
        '\u3002', '\uFF01', '\uFF1F', '\uFF1A', '\uFF1B', '\uFF09', '\u3011', '\u300D',
    ];

    private readonly MarkdownDocumentParser _parser = new(chunker);

    public MarkdownChunkEvaluationResult Evaluate(
        string markdown,
        string? path = null,
        IEnumerable<MarkdownChunkCoverageExpectation>? coverageExpectations = null,
        MarkdownChunkEvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var effectiveOptions = options ?? MarkdownChunkEvaluationOptions.Default;
        ValidateOptions(effectiveOptions);

        var document = _parser.Parse(
            new MarkdownDocumentSource(markdown, path),
            effectiveOptions.ParsingOptions);
        return AnalyzeDocument(document, coverageExpectations, effectiveOptions);
    }

    public MarkdownChunkEvaluationResult AnalyzeDocument(
        MarkdownDocument document,
        IEnumerable<MarkdownChunkCoverageExpectation>? coverageExpectations = null,
        MarkdownChunkEvaluationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var effectiveOptions = options ?? MarkdownChunkEvaluationOptions.Default;
        ValidateOptions(effectiveOptions);

        var chunks = document.Chunks;
        return new MarkdownChunkEvaluationResult(
            AnalyzeSizeDistribution(chunks, effectiveOptions),
            EvaluateCoverage(chunks, coverageExpectations, effectiveOptions.PreviewCharacterLimit),
            CreateQualitySamples(chunks, effectiveOptions));
    }

    private static void ValidateOptions(MarkdownChunkEvaluationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.ParsingOptions);
        ArgumentOutOfRangeException.ThrowIfNegative(options.SmallTokenThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(options.LargeTokenThreshold);
        ArgumentOutOfRangeException.ThrowIfNegative(options.QualitySampleSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PreviewCharacterLimit);
        if (options.LargeTokenThreshold < options.SmallTokenThreshold)
        {
            throw new ArgumentException(MarkdownChunkEvaluationDefaults.InvalidThresholdRangeMessage, nameof(options));
        }
    }

    private static MarkdownChunkSizeDistribution AnalyzeSizeDistribution(
        IReadOnlyList<MarkdownChunk> chunks,
        MarkdownChunkEvaluationOptions options)
    {
        if (chunks.Count == 0)
        {
            return new MarkdownChunkSizeDistribution(
                MarkdownChunkEvaluationDefaults.ZeroTokenCount,
                MarkdownChunkEvaluationDefaults.ZeroTokenCount,
                MarkdownChunkEvaluationDefaults.ZeroTokenCount,
                MarkdownChunkEvaluationDefaults.ZeroTokenCount,
                MarkdownChunkEvaluationDefaults.ZeroTokenCount,
                MarkdownChunkEvaluationDefaults.ZeroTokenCount,
                MarkdownChunkEvaluationDefaults.ZeroRate,
                MarkdownChunkEvaluationDefaults.ZeroTokenCount);
        }

        var tokens = chunks
            .Select(static chunk => chunk.EstimatedTokenCount)
            .Order()
            .ToArray();

        return new MarkdownChunkSizeDistribution(
            chunks.Count,
            tokens.Count(token => token < options.SmallTokenThreshold),
            tokens.Count(token => token >= options.SmallTokenThreshold && token <= options.LargeTokenThreshold),
            tokens.Count(token => token > options.LargeTokenThreshold),
            tokens[0],
            tokens[^1],
            tokens.Average(),
            CalculateMedian(tokens));
    }

    private static double CalculateMedian(IReadOnlyList<int> sortedTokens)
    {
        var middle = sortedTokens.Count / 2;
        if (sortedTokens.Count % 2 == 1)
        {
            return sortedTokens[middle];
        }

        return (sortedTokens[middle - 1] + sortedTokens[middle]) / 2d;
    }

    private static IReadOnlyList<MarkdownChunkCoverageResult> EvaluateCoverage(
        IReadOnlyList<MarkdownChunk> chunks,
        IEnumerable<MarkdownChunkCoverageExpectation>? coverageExpectations,
        int previewCharacterLimit)
    {
        return coverageExpectations?
                   .Select(expectation => EvaluateCoverageExpectation(chunks, expectation, previewCharacterLimit))
                   .ToArray() ??
               [];
    }

    private static MarkdownChunkCoverageResult EvaluateCoverageExpectation(
        IReadOnlyList<MarkdownChunk> chunks,
        MarkdownChunkCoverageExpectation expectation,
        int previewCharacterLimit)
    {
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectation.Question);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectation.ExpectedAnswer);

        var hit = chunks.FirstOrDefault(chunk =>
            chunk.Markdown.Contains(expectation.ExpectedAnswer, StringComparison.OrdinalIgnoreCase));

        return new MarkdownChunkCoverageResult(
            expectation.Question,
            expectation.ExpectedAnswer,
            hit is not null,
            hit?.ChunkId,
            hit?.Order,
            hit?.HeadingPath ?? [],
            hit is null ? null : CreatePreview(hit.Markdown, previewCharacterLimit));
    }

    private static IReadOnlyList<MarkdownChunkQualitySample> CreateQualitySamples(
        IReadOnlyList<MarkdownChunk> chunks,
        MarkdownChunkEvaluationOptions options)
    {
        if (chunks.Count == 0 || options.QualitySampleSize == 0)
        {
            return [];
        }

        var random = new Random(options.QualitySampleSeed);
        return chunks
            .OrderBy(_ => random.Next())
            .Take(options.QualitySampleSize)
            .OrderBy(static chunk => chunk.Order)
            .Select(chunk => CreateQualitySample(chunk, options.PreviewCharacterLimit))
            .ToArray();
    }

    private static MarkdownChunkQualitySample CreateQualitySample(
        MarkdownChunk chunk,
        int previewCharacterLimit)
    {
        var trimmed = chunk.Markdown.Trim();
        return new MarkdownChunkQualitySample(
            chunk.ChunkId,
            chunk.Order,
            chunk.HeadingPath,
            chunk.EstimatedTokenCount,
            StartsAbruptly(trimmed),
            EndsAbruptly(trimmed),
            CreatePreview(trimmed, previewCharacterLimit));
    }

    private static bool StartsAbruptly(string text)
    {
        return text.Length > 0 &&
               char.IsAsciiLetterLower(text[0]) &&
               !text.StartsWith(MarkdownChunkEvaluationDefaults.CodeFencePrefix, StringComparison.Ordinal);
    }

    private static bool EndsAbruptly(string text)
    {
        return text.Length > 0 &&
               !EndPunctuation.Contains(text[^1]);
    }

    private static string CreatePreview(string text, int previewCharacterLimit)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= previewCharacterLimit
            ? trimmed
            : trimmed[..previewCharacterLimit].TrimEnd();
    }
}
