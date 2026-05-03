using System.Diagnostics;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class TiktokenFuzzyTokenDistanceSearchFlowTests
{
    private const string BaseUriText = "https://tiktoken-fuzzy.example/";
    private const string CachePath = "content/runbooks/cache-restore.md";
    private const string BillingPath = "content/runbooks/billing-export.md";
    private const string ReleasePath = "content/runbooks/release-cache.md";
    private const string PaymentPath = "content/runbooks/payment-checkpoint.md";
    private const string TypoCorpusPath = "content/runbooks/cache-typo.md";
    private const string CacheEvidenceText = "Cache manifest restore checkpoint verifies rollback evidence before publishing.";
    private const string BillingEvidenceText = "Billing invoice export checkpoint verifies payment evidence before publishing.";
    private const string ReleaseEvidenceText = "Cache manifest restore rollback evidence safeguards the release.";
    private const string PaymentEvidenceText = "Payment checkpoint publishing evidence records invoice approval.";
    private const string CacheTypoEvidenceText = "Cache manifst restore checkpoint verifies rollback evidence before publishing.";
    private const string TypoQuery = "cach manifst restor chekpoint rollback evidnce";
    private const string DistractorBiasedTypoQuery = "payment checkpoint cach manifst restor rollbak";
    private const string ExactQuery = "cache manifest restore checkpoint rollback evidence";
    private const string PerformanceIdentifierPrefix = "cachevalidationfingerprintcheckpointtoken";
    private const string PerformanceIdentifierSuffix = "manifestwindowrollbackevidence";
    private const int QueryLimit = 2;
    private const int PerformanceCandidateCount = 240;
    private const int PerformanceTargetIndex = 137;
    private const int PerformanceSearchIterations = 8;
    private static readonly Uri BaseUri = new(BaseUriText);
    private static readonly TimeSpan FuzzySearchBudget = TimeSpan.FromSeconds(4);

    private const string CacheMarkdown = """
---
title: Cache Restore
---
# Cache Restore

Cache manifest restore checkpoint verifies rollback evidence before publishing.
""";

    private const string BillingMarkdown = """
---
title: Billing Export
---
# Billing Export

Billing invoice export checkpoint verifies payment evidence before publishing.
""";

    private const string ReleaseMarkdown = """
---
title: Cache Release
---
# Cache Release

Cache manifest restore rollback evidence safeguards the release.
""";

    private const string PaymentMarkdown = """
---
title: Payment Checkpoint
---
# Payment Checkpoint

Payment checkpoint publishing evidence records invoice approval.
""";

    private const string CacheTypoMarkdown = """
---
title: Cache Restore
---
# Cache Restore

Cache manifst restore checkpoint verifies rollback evidence before publishing.
""";

    [Test]
    public async Task Tiktoken_fuzzy_query_correction_improves_typo_query_distance_over_plain_token_search()
    {
        var graph = await BuildGraphAsync();

        var plainMatches = await graph.SearchByTokenDistanceAsync(TypoQuery, QueryLimit);
        var fuzzyMatches = await graph.SearchByTokenDistanceAsync(
            TypoQuery,
            new TokenDistanceSearchOptions
            {
                Limit = QueryLimit,
                EnableFuzzyQueryCorrection = true,
            });
        var exactMatches = await graph.SearchByTokenDistanceAsync(ExactQuery, QueryLimit);

        var plainCache = plainMatches.Single(match => match.Text == CacheEvidenceText);
        var fuzzyCache = fuzzyMatches.Single(match => match.Text == CacheEvidenceText);

        fuzzyMatches[0].Text.ShouldBe(CacheEvidenceText);
        exactMatches[0].Text.ShouldBe(CacheEvidenceText);
        fuzzyCache.Distance.ShouldBeLessThan(plainCache.Distance);
        fuzzyCache.Distance.ShouldBeLessThan(fuzzyMatches.Single(match => match.Text == BillingEvidenceText).Distance);
    }

    [Test]
    public async Task Tiktoken_fuzzy_query_correction_recovers_target_when_plain_tokens_follow_distractor()
    {
        var graph = await BuildGraphAsync(
            new MarkdownSourceDocument(ReleasePath, ReleaseMarkdown),
            new MarkdownSourceDocument(PaymentPath, PaymentMarkdown));

        var plainMatches = await graph.SearchByTokenDistanceAsync(DistractorBiasedTypoQuery, QueryLimit);
        var fuzzyMatches = await graph.SearchByTokenDistanceAsync(
            DistractorBiasedTypoQuery,
            new TokenDistanceSearchOptions
            {
                Limit = QueryLimit,
                EnableFuzzyQueryCorrection = true,
            });

        var plainTarget = plainMatches.Single(match => match.Text == ReleaseEvidenceText);
        var fuzzyTarget = fuzzyMatches.Single(match => match.Text == ReleaseEvidenceText);

        plainMatches[0].Text.ShouldBe(PaymentEvidenceText);
        fuzzyMatches[0].Text.ShouldBe(ReleaseEvidenceText);
        fuzzyTarget.Distance.ShouldBeLessThan(plainTarget.Distance);
    }

    [Test]
    public async Task Tiktoken_fuzzy_query_correction_improves_distance_when_corpus_text_contains_typo()
    {
        var graph = await BuildGraphAsync(
            new MarkdownSourceDocument(TypoCorpusPath, CacheTypoMarkdown),
            new MarkdownSourceDocument(BillingPath, BillingMarkdown));

        var plainMatches = await graph.SearchByTokenDistanceAsync(ExactQuery, QueryLimit);
        var fuzzyMatches = await graph.SearchByTokenDistanceAsync(
            ExactQuery,
            new TokenDistanceSearchOptions
            {
                Limit = QueryLimit,
                EnableFuzzyQueryCorrection = true,
            });

        var plainTarget = plainMatches.Single(match => match.Text == CacheTypoEvidenceText);
        var fuzzyTarget = fuzzyMatches.Single(match => match.Text == CacheTypoEvidenceText);

        fuzzyMatches[0].Text.ShouldBe(CacheTypoEvidenceText);
        fuzzyTarget.Distance.ShouldBeLessThan(plainTarget.Distance);
    }

    [Test]
    public async Task Tiktoken_fuzzy_query_correction_remains_opt_in()
    {
        var graph = await BuildGraphAsync();

        var plainMatches = await graph.SearchByTokenDistanceAsync(TypoQuery, QueryLimit);
        var explicitPlainMatches = await graph.SearchByTokenDistanceAsync(
            TypoQuery,
            new TokenDistanceSearchOptions
            {
                Limit = QueryLimit,
                EnableFuzzyQueryCorrection = false,
            });

        explicitPlainMatches.Select(static match => match.Text).ShouldBe(plainMatches.Select(static match => match.Text));
        explicitPlainMatches.Select(static match => match.Distance).ShouldBe(plainMatches.Select(static match => match.Distance));
    }

    [Test]
    public async Task Tiktoken_fuzzy_query_correction_validates_options()
    {
        var graph = await BuildGraphAsync();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await graph.SearchByTokenDistanceAsync(
                TypoQuery,
                new TokenDistanceSearchOptions
                {
                    Limit = 0,
                    EnableFuzzyQueryCorrection = true,
                }));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await graph.SearchByTokenDistanceAsync(
                TypoQuery,
                new TokenDistanceSearchOptions
                {
                    EnableFuzzyQueryCorrection = true,
                    MaxFuzzyEditDistance = -1,
                }));
    }

    [Test]
    public async Task Tiktoken_fuzzy_query_correction_long_vocab_search_stays_under_regression_budget()
    {
        var graph = await BuildGraphAsync(
            CreatePerformanceSources().ToArray(),
            new TiktokenKnowledgeGraphOptions
            {
                BuildAutoRelatedSegmentRelations = false,
            });
        var query = CreateInsertedTypoIdentifier(PerformanceTargetIndex);
        var options = new TokenDistanceSearchOptions
        {
            Limit = 1,
            EnableFuzzyQueryCorrection = true,
        };

        var warmup = await graph.SearchByTokenDistanceAsync(query, options);
        warmup.Single().Text.ShouldContain(CreateIdentifier(PerformanceTargetIndex));

        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < PerformanceSearchIterations; iteration++)
        {
            var results = await graph.SearchByTokenDistanceAsync(query, options);
            results.Single().Text.ShouldContain(CreateIdentifier(PerformanceTargetIndex));
        }

        stopwatch.Stop();
        stopwatch.Elapsed.ShouldBeLessThan(FuzzySearchBudget);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync()
    {
        return await BuildGraphAsync(
            new MarkdownSourceDocument(BillingPath, BillingMarkdown),
            new MarkdownSourceDocument(CachePath, CacheMarkdown));
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(params MarkdownSourceDocument[] documents)
    {
        return await BuildGraphAsync(documents, null);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(
        IEnumerable<MarkdownSourceDocument> documents,
        TiktokenKnowledgeGraphOptions? options)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken,
            tiktokenOptions: options);
        var result = await pipeline.BuildAsync(documents);

        return result.Graph;
    }

    private static IEnumerable<MarkdownSourceDocument> CreatePerformanceSources()
    {
        for (var index = 0; index < PerformanceCandidateCount; index++)
        {
            yield return new MarkdownSourceDocument(
                $"content/perf/token-candidate-{index:D4}.md",
                CreatePerformanceMarkdown(index));
        }
    }

    private static string CreatePerformanceMarkdown(int index)
    {
        var identifier = CreateIdentifier(index);
        return $$"""
            ---
            title: Token candidate {{index}}
            ---
            # Token candidate {{index}}

            Use {{identifier}} when cache evidence must be restored.
            """;
    }

    private static string CreateIdentifier(int index)
    {
        return $"{PerformanceIdentifierPrefix}{index:D4}{PerformanceIdentifierSuffix}";
    }

    private static string CreateInsertedTypoIdentifier(int index)
    {
        return $"{PerformanceIdentifierPrefix}{index:D4}x{PerformanceIdentifierSuffix}";
    }
}
