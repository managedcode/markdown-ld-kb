using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class Bm25GraphSearchFlowTests
{
    private const string BaseUriText = "https://bm25-search.example/";
    private const string CachePath = "content/runbooks/cache-restore.md";
    private const string ArchivePath = "content/runbooks/archive-policy.md";
    private const string CacheTitle = "Cache restore runbook";
    private const string Query = "cache restore";
    private const string MissingCandidateNodeId = "https://bm25-search.example/missing";
    private const string DeletionTypoQuery = "cach restre";
    private const string SubstitutionTypoQuery = "kache restpre";
    private const string InsertionTypoQuery = "ccache restorre";
    private const string LongCacheToken = "cachevalidationfingerprintcheckpointtokenabcdefghijklmnopqrstuvwx";
    private const string LongCacheTokenTypo = "cachevalidationfingerprintcheckpointtokenabcdefghijklmnopqrstuvwz";
    private const string LongCacheTokenInsertionTypo = "cachevalidationfingerprintcheckpointtokenabcdefghijklmnopqrstuvwxy";
    private const string ExactFuzzyBoundaryQuery = "alpha";
    private const string ExactFuzzyBoundaryPath = "content/runbooks/alpha.md";
    private const string ZeroSimilarityFuzzyBoundaryPath = "content/runbooks/zzzzz.md";
    private const string SharedTermQuery = "sharedneedle";
    private const string SharedTermFirstPath = "content/runbooks/shared-first.md";
    private const string SharedTermSecondPath = "content/runbooks/shared-second.md";
    private const string CjkPath = "content/runbooks/cjk.md";
    private const string CjkTitle = "CJK recovery runbook";
    private const string CjkExactQuery = "知識庫圖譜";
    private const string CjkTypoQuery = "知識庫圖普";
    private const string ExactFuzzyBoundaryMarkdown = """
---
title: Alpha
---
# Alpha
""";
    private const string ZeroSimilarityFuzzyBoundaryMarkdown = """
---
title: Zzzzz
---
# Zzzzz
""";
    private const string SharedTermFirstMarkdown = """
---
title: Shared first
summary: sharedneedle first routing notes.
---
# Shared first
""";
    private const string SharedTermSecondMarkdown = """
---
title: Shared second
summary: sharedneedle second routing notes.
---
# Shared second
""";
    private const string CjkMarkdown = """
---
title: CJK recovery runbook
summary: 知識庫圖譜 restore evidence.
---
# CJK recovery runbook
""";
    private const string AnswerText = "Use the cache restore runbook for cache restore work [1].";
    private static readonly Uri BaseUri = new(BaseUriText);

    private static readonly string CacheMarkdown = $"""
---
title: Cache restore runbook
summary: Cache restore cache manifest restore evidence and cache validation.
---
# Cache restore runbook

Use this runbook for cache restore verification and manifest checks.
Long-token marker: {LongCacheToken}.
""";

    private const string ArchiveMarkdown = """
---
title: Archive policy
summary: Archive retention policy for legal records and release evidence.
---
# Archive policy

Use this policy for archive retention reviews.
""";

    [Test]
    public async Task Bm25_mode_ranks_graph_candidate_text_without_semantic_index()
    {
        var build = await BuildAsync();

        var results = await build.Graph.SearchRankedAsync(
            Query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });

        results.ShouldNotBeEmpty();
        results[0].Label.ShouldBe(CacheTitle);
        results[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);
        results[0].Score.ShouldBeGreaterThan(0d);
    }

    [Test]
    public async Task Answer_service_builds_citations_from_bm25_ranked_matches()
    {
        var build = await BuildAsync();
        var chatClient = new TestChatClient((_, _) => AnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(Query)
            {
                SearchOptions = new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Bm25,
                    MaxResults = 2,
                },
            });

        result.Answer.ShouldBe(AnswerText);
        result.Citations.Single().SourcePath.ShouldBe(CachePath);
        result.Citations.Single().SearchSource.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);
        result.Citations.Single().Snippet.ShouldContain("cache restore verification");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Bm25_mode_returns_empty_when_candidate_filter_matches_no_nodes(bool fuzzy)
    {
        var build = await BuildAsync();

        var results = await build.SearchRankedAsync(
            Query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                EnableFuzzyTokenMatching = fuzzy,
                CandidateNodeIds = [MissingCandidateNodeId],
            });

        results.ShouldBeEmpty();
    }

    [Test]
    [Arguments(DeletionTypoQuery)]
    [Arguments(SubstitutionTypoQuery)]
    [Arguments(InsertionTypoQuery)]
    public async Task Bm25_fuzzy_token_matching_is_opt_in_for_one_edit_typo_queries(string typoQuery)
    {
        var build = await BuildAsync();

        var exactResults = await build.SearchRankedAsync(
            typoQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });
        var fuzzyResults = await build.SearchRankedAsync(
            typoQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                EnableFuzzyTokenMatching = true,
                MaxResults = 2,
            });

        exactResults.ShouldBeEmpty();
        fuzzyResults.ShouldNotBeEmpty();
        fuzzyResults[0].Label.ShouldBe(CacheTitle);
        fuzzyResults[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);
    }

    [Test]
    [Arguments(LongCacheTokenTypo)]
    [Arguments(LongCacheTokenInsertionTypo)]
    public async Task Bm25_fuzzy_token_matching_handles_long_terms_through_fallback_distance(string longTypoQuery)
    {
        var build = await BuildAsync();

        var exactResults = await build.SearchRankedAsync(
            longTypoQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });
        var fuzzyResults = await build.SearchRankedAsync(
            longTypoQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                EnableFuzzyTokenMatching = true,
                MaxResults = 2,
            });

        exactResults.ShouldBeEmpty();
        fuzzyResults.ShouldNotBeEmpty();
        fuzzyResults[0].Label.ShouldBe(CacheTitle);
        fuzzyResults[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);
    }

    [Test]
    public async Task Bm25_fuzzy_token_matching_validates_threshold_options()
    {
        var build = await BuildAsync();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await build.SearchRankedAsync(
                DeletionTypoQuery,
                new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Bm25,
                    EnableFuzzyTokenMatching = true,
                    MaxFuzzyEditDistance = -1,
                }));

        await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
            await build.SearchRankedAsync(
                DeletionTypoQuery,
                new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Bm25,
                    EnableFuzzyTokenMatching = true,
                    MinimumFuzzyTokenLength = 0,
                }));
    }

    [Test]
    public async Task Bm25_fuzzy_token_matching_ignores_zero_similarity_terms_for_document_frequency()
    {
        var build = await BuildAsync(
            new MarkdownSourceDocument(ExactFuzzyBoundaryPath, ExactFuzzyBoundaryMarkdown),
            new MarkdownSourceDocument(ZeroSimilarityFuzzyBoundaryPath, ZeroSimilarityFuzzyBoundaryMarkdown));

        var exactResults = await build.SearchRankedAsync(
            ExactFuzzyBoundaryQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 1,
            });
        var fuzzyResults = await build.SearchRankedAsync(
            ExactFuzzyBoundaryQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                EnableFuzzyTokenMatching = true,
                MaxFuzzyEditDistance = ExactFuzzyBoundaryQuery.Length,
                MaxResults = 1,
            });

        fuzzyResults.Single().Label.ShouldBe(exactResults.Single().Label);
        Math.Abs(fuzzyResults.Single().Score - exactResults.Single().Score).ShouldBeLessThan(0.000001d);
    }

    [Test]
    public async Task Bm25_idf_remains_positive_when_query_term_appears_in_every_document()
    {
        var build = await BuildAsync(
            new MarkdownSourceDocument(SharedTermFirstPath, SharedTermFirstMarkdown),
            new MarkdownSourceDocument(SharedTermSecondPath, SharedTermSecondMarkdown));

        var results = await build.SearchRankedAsync(
            SharedTermQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });

        results.Count.ShouldBe(2);
        results.ShouldAllBe(result => result.Score > 0d);
    }

    [Test]
    public async Task Bm25_fuzzy_token_matching_handles_cjk_one_edit_terms()
    {
        var build = await BuildAsync(new MarkdownSourceDocument(CjkPath, CjkMarkdown));

        var exactResults = await build.SearchRankedAsync(
            CjkTypoQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 1,
            });
        var fuzzyResults = await build.SearchRankedAsync(
            CjkTypoQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                EnableFuzzyTokenMatching = true,
                MaxResults = 1,
            });
        var exactCjkResults = await build.SearchRankedAsync(
            CjkExactQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 1,
            });

        exactResults.ShouldBeEmpty();
        fuzzyResults.Single().Label.ShouldBe(CjkTitle);
        exactCjkResults.Single().Label.ShouldBe(CjkTitle);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAsync(params MarkdownSourceDocument[]? sources)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.None);

        return pipeline.BuildAsync(sources is null || sources.Length == 0
            ?
        [
            new MarkdownSourceDocument(CachePath, CacheMarkdown),
            new MarkdownSourceDocument(ArchivePath, ArchiveMarkdown),
        ]
            : sources);
    }
}
