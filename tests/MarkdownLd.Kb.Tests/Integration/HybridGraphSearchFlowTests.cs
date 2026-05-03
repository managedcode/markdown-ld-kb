using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class HybridGraphSearchFlowTests
{
    private const string BaseUriText = "https://hybrid-search.example/";
    private const string NotificationsPath = "content/tools/notification-settings.md";
    private const string AlertsGuidePath = "content/tools/inbox-alerts-guide.md";
    private const string TreePath = "content/tools/person-tree-parent-link.md";
    private const string KeywordNoisePath = "content/tools/account-overview.md";
    private const string NotificationsTitle = "Notification settings";
    private const string AlertsGuideTitle = "Inbox alerts guide";
    private const string TreeTitle = "Person tree parent link";
    private const string UkrainianNotificationsQuery = "а що у мене з нотифікейшенами";
    private const string GraphNotificationsQuery = "notifications";
    private const string CanonicalNotificationsQuery = "notification settings";
    private static readonly Uri BaseUri = new(BaseUriText);

    private const string NotificationsMarkdown = """
---
title: Notification settings
summary: Manage notification delivery preferences and inbox alerts for your account.
---
# Notification settings

Use this tool to review notification delivery preferences and inbox alerts.
""";

    private const string AlertsGuideMarkdown = """
---
title: Inbox alerts guide
summary: Configure app alerts and delivery preferences for messages and reminders.
---
# Inbox alerts guide

Use this guide to configure app alerts and delivery preferences.
""";

    private const string TreeMarkdown = """
---
title: Person tree parent link
summary: Add father or mother relationships for a person in a family tree.
---
# Person tree parent link

Use this tool to connect parent relationships in a genealogy tree.
""";

    private const string KeywordNoiseMarkdown = """
---
title: Account overview
summary: Review your account dashboard and profile details.
keywords:
  - notifications
---
# Account overview

Use this page to review account dashboard details.
""";

    [Test]
    public async Task Hybrid_mode_recovers_cross_language_match_when_graph_language_differs_from_query()
    {
        var graph = await BuildGraphAsync(
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown),
            new MarkdownSourceDocument(TreePath, TreeMarkdown));
        var semanticIndex = await graph.BuildSemanticIndexAsync(new TestEmbeddingGenerator());

        var graphResults = await graph.SearchRankedAsync(
            UkrainianNotificationsQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Graph,
                MaxResults = 3,
            });
        var hybridResults = await graph.SearchRankedAsync(
            UkrainianNotificationsQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Hybrid,
                MaxResults = 3,
                MaxSemanticResults = 3,
            },
            semanticIndex);

        graphResults.ShouldBeEmpty();
        hybridResults.ShouldNotBeEmpty();
        hybridResults[0].Label.ShouldBe(NotificationsTitle);
        hybridResults[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Semantic);
    }

    [Test]
    public async Task Graph_mode_ignores_keyword_only_hits()
    {
        var graph = await BuildGraphAsync(new MarkdownSourceDocument(KeywordNoisePath, KeywordNoiseMarkdown));

        var results = await graph.SearchRankedAsync(
            GraphNotificationsQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Graph,
                MaxResults = 3,
            });

        results.ShouldBeEmpty();
    }

    [Test]
    public async Task Hybrid_mode_keeps_canonical_graph_hit_ahead_of_semantic_only_match()
    {
        var graph = await BuildGraphAsync(
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown),
            new MarkdownSourceDocument(AlertsGuidePath, AlertsGuideMarkdown));
        var semanticIndex = await graph.BuildSemanticIndexAsync(new TestEmbeddingGenerator());

        var results = await graph.SearchRankedAsync(
            CanonicalNotificationsQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Hybrid,
                MaxResults = 3,
                MaxSemanticResults = 3,
            },
            semanticIndex);

        results.Count.ShouldBeGreaterThanOrEqualTo(2);
        results[0].Label.ShouldBe(NotificationsTitle);
        results[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Merged);
        results[1].Label.ShouldBe(AlertsGuideTitle);
        results[1].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Semantic);
    }

    [Test]
    public async Task Hybrid_mode_can_use_reciprocal_rank_fusion_without_changing_default_strategy()
    {
        var graph = await BuildGraphAsync(
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown),
            new MarkdownSourceDocument(AlertsGuidePath, AlertsGuideMarkdown));
        var semanticIndex = await graph.BuildSemanticIndexAsync(new TestEmbeddingGenerator());

        var defaultResults = await graph.SearchRankedAsync(
            "app alerts",
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Hybrid,
                MaxResults = 2,
                MaxSemanticResults = 2,
            },
            semanticIndex);
        var rrfResults = await graph.SearchRankedAsync(
            "app alerts",
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Hybrid,
                MaxResults = 2,
                MaxSemanticResults = 2,
                HybridFusionStrategy = KnowledgeGraphHybridFusionStrategy.ReciprocalRank,
            },
            semanticIndex);

        defaultResults[0].Label.ShouldBe(AlertsGuideTitle);
        defaultResults[0].Score.ShouldBeGreaterThan(1);
        rrfResults[0].Label.ShouldBe(AlertsGuideTitle);
        rrfResults[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Merged);
        rrfResults[0].CanonicalScore.ShouldNotBeNull();
        rrfResults[0].SemanticScore.ShouldNotBeNull();
        rrfResults[0].CanonicalScore.GetValueOrDefault().ShouldBeGreaterThan(0);
        rrfResults[0].SemanticScore.GetValueOrDefault().ShouldBeGreaterThan(0);
        rrfResults[0].Score.ShouldBeLessThan(defaultResults[0].Score);
    }

    [Test]
    public async Task Semantic_mode_requires_a_semantic_index()
    {
        var graph = await BuildGraphAsync(new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown));

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await graph.SearchRankedAsync(
                CanonicalNotificationsQuery,
                new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Semantic,
                    MaxResults = 3,
                }));

        exception.Message.ShouldContain("semantic index");
    }

    [Test]
    public async Task Focused_search_uses_hybrid_primary_matches_when_semantic_index_is_supplied()
    {
        var graph = await BuildGraphAsync(
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown),
            new MarkdownSourceDocument(TreePath, TreeMarkdown));
        var semanticIndex = await graph.BuildSemanticIndexAsync(new TestEmbeddingGenerator());

        var result = await graph.SearchFocusedAsync(
            UkrainianNotificationsQuery,
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                SemanticIndex = semanticIndex,
            });

        result.PrimaryMatches.Count.ShouldBe(1);
        result.PrimaryMatches[0].Label.ShouldBe(NotificationsTitle);
    }

    [Test]
    public async Task Build_result_creates_optional_semantic_index_with_embedding_generator_boundary()
    {
        var build = await BuildAsync(
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown),
            new MarkdownSourceDocument(TreePath, TreeMarkdown));

        var semanticIndex = await build.BuildSemanticIndexAsync(new TestEmbeddingGenerator());
        var results = await build.Graph.SearchRankedAsync(
            UkrainianNotificationsQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Semantic,
                MaxResults = 1,
            },
            semanticIndex);

        semanticIndex.Count.ShouldBeGreaterThan(0);
        results.Single().Label.ShouldBe(NotificationsTitle);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(params MarkdownSourceDocument[] documents)
    {
        return (await BuildAsync(documents)).Graph;
    }

    private static async Task<MarkdownKnowledgeBuildResult> BuildAsync(params MarkdownSourceDocument[] documents)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.None);
        var result = await pipeline.BuildAsync(documents);

        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
        return result;
    }
}
