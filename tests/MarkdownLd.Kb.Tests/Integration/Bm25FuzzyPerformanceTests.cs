using System.Diagnostics;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class Bm25FuzzyPerformanceTests
{
    private const string BaseUriText = "https://bm25-performance.example/";
    private const string IdentifierPrefix = "cachevalidationfingerprintcheckpointtoken";
    private const string IdentifierSuffix = "manifestwindowrollbackevidence";
    private const int CandidateCount = 800;
    private const int TargetIndex = 420;
    private const int SearchIterations = 10;
    private static readonly TimeSpan SearchBudget = TimeSpan.FromSeconds(4);
    private static readonly Uri BaseUri = new(BaseUriText);

    [Test]
    public async Task Bm25_fuzzy_long_identifier_search_stays_under_regression_budget()
    {
        var build = await BuildAsync();
        var query = CreateInsertedTypoIdentifier(TargetIndex);
        var options = new KnowledgeGraphRankedSearchOptions
        {
            Mode = KnowledgeGraphSearchMode.Bm25,
            EnableFuzzyTokenMatching = true,
            MaxResults = 1,
        };

        var warmup = await build.SearchRankedAsync(query, options);
        warmup.Single().Label.ShouldBe(CreateTitle(TargetIndex));

        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < SearchIterations; iteration++)
        {
            var results = await build.SearchRankedAsync(query, options);
            results.Single().Label.ShouldBe(CreateTitle(TargetIndex));
        }

        stopwatch.Stop();
        stopwatch.Elapsed.ShouldBeLessThan(SearchBudget);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.None);
        return pipeline.BuildAsync(CreateSources());
    }

    private static IEnumerable<MarkdownSourceDocument> CreateSources()
    {
        for (var index = 0; index < CandidateCount; index++)
        {
            yield return new MarkdownSourceDocument(
                $"content/perf/candidate-{index:D4}.md",
                CreateMarkdown(index));
        }
    }

    private static string CreateMarkdown(int index)
    {
        var title = CreateTitle(index);
        var identifier = CreateIdentifier(index);
        return $$"""
            ---
            title: {{title}}
            summary: {{identifier}} restores cache manifest evidence.
            ---
            # {{title}}

            Use {{identifier}} when cache manifest evidence must be restored.
            """;
    }

    private static string CreateTitle(int index)
    {
        return $"Cache candidate {index:D4}";
    }

    private static string CreateIdentifier(int index)
    {
        return $"{IdentifierPrefix}{index:D4}{IdentifierSuffix}";
    }

    private static string CreateInsertedTypoIdentifier(int index)
    {
        return $"{IdentifierPrefix}{index:D4}x{IdentifierSuffix}";
    }
}
