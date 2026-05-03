using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class MarkdownKnowledgeBankFacadeFlowTests
{
    private const string BaseUriText = "https://facade.example/";
    private const string CachePath = "content/runbooks/cache-restore.md";
    private const string NotificationsPath = "content/tools/notification-settings.md";
    private const string NormalizedCachePath = "runbooks/cache-restore.md";
    private const string NormalizedNotificationsPath = "tools/notification-settings.md";
    private const string CacheTitle = "Cache restore runbook";
    private const string Query = "cache restore";
    private const string AnswerText = "Use the cache restore runbook for cache restore work [1].";
    private const string CoverageQuestion = "How do I restore cache safely?";
    private const string CoverageAnswer = "cache restore verification";
    private const int SmallChunkTokenTarget = 5;

    private static readonly Uri BaseUri = new(BaseUriText);

    private const string CacheMarkdown = """
---
title: Cache restore runbook
summary: Cache restore cache manifest restore evidence and cache validation.
---
# Cache restore runbook

Use this runbook for cache restore verification and manifest checks.
""";

    private const string UpdatedCacheMarkdown = """
---
title: Cache restore runbook
summary: Cache restore cache manifest restore evidence and cache validation.
---
# Cache restore runbook

Use this runbook for cache restore verification, manifest checks, and rollback evidence.
""";

    private const string NotificationsMarkdown = """
---
title: Notification settings
summary: Manage notification delivery preferences and inbox alerts.
---
# Notification settings

Use this tool to review notification delivery preferences.
""";

    private const string MultiBlockMarkdown = """
# Chunk options

First block describes cache restore setup.

Second block describes cache restore validation.

Third block describes cache restore rollback.
""";

    [Test]
    public async Task Unified_api_builds_plans_evaluates_searches_indexes_and_answers_with_citations()
    {
        var chatClient = new TestChatClient((_, _) => AnswerText);
        var bank = CreateBank(chatClient);
        var sources = CreateSources(CacheMarkdown);

        var firstPlan = bank.PlanChanges(sources);
        firstPlan.ChangedPaths.ShouldBe([NormalizedCachePath, NormalizedNotificationsPath]);

        var secondPlan = bank.PlanChanges(CreateSources(UpdatedCacheMarkdown), firstPlan.Manifest);
        secondPlan.ChangedPaths.ShouldBe([NormalizedCachePath]);
        secondPlan.UnchangedPaths.ShouldBe([NormalizedNotificationsPath]);

        var evaluation = bank.EvaluateChunks(
            CacheMarkdown,
            CachePath,
            [new MarkdownChunkCoverageExpectation(CoverageQuestion, CoverageAnswer)]);
        evaluation.CoverageRate.ShouldBe(1d);
        evaluation.QualitySamples.ShouldNotBeEmpty();

        var build = await bank.BuildAsync(sources);
        var semanticIndex = await build.BuildSemanticIndexAsync();
        semanticIndex.ShouldBeSameAs(build.SemanticIndex);

        var search = await build.SearchAsync(
            Query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });
        search[0].Label.ShouldBe(CacheTitle);
        search[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);

        var answer = await build.AnswerAsync(
            Query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });
        answer.Answer.ShouldBe(AnswerText);
        answer.Citations.Single().SourcePath.ShouldBe(CachePath);
        answer.Citations.Single().SearchSource.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);

        var incremental = await bank.BuildIncrementalAsync(
            sources,
            firstPlan.Manifest,
            build.Graph);
        incremental.UnchangedPaths.ShouldBe([NormalizedCachePath, NormalizedNotificationsPath]);
        incremental.Build.Graph.TripleCount.ShouldBe(build.Graph.TripleCount);
    }

    [Test]
    public async Task Unified_api_fails_explicitly_when_optional_ai_services_are_missing()
    {
        var bank = new MarkdownKnowledgeBank(new MarkdownKnowledgeBankOptions
        {
            PipelineOptions = new MarkdownKnowledgePipelineOptions
            {
                BaseUri = BaseUri,
                ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            },
        });
        var build = await bank.BuildFromMarkdownAsync(CacheMarkdown, CachePath);

        var answerException = await Should.ThrowAsync<InvalidOperationException>(
            async () => await build.AnswerAsync(Query));
        answerException.Message.ShouldContain(nameof(Microsoft.Extensions.AI.IChatClient));

        var embeddingException = await Should.ThrowAsync<InvalidOperationException>(
            async () => await build.BuildSemanticIndexAsync());
        embeddingException.Message.ShouldContain(nameof(Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>));
    }

    [Test]
    public void Unified_api_chunk_evaluation_uses_pipeline_chunking_options_by_default()
    {
        var bank = new MarkdownKnowledgeBank(new MarkdownKnowledgeBankOptions
        {
            PipelineOptions = new MarkdownKnowledgePipelineOptions
            {
                BaseUri = BaseUri,
                ExtractionMode = MarkdownKnowledgeExtractionMode.None,
                ChunkingOptions = new MarkdownChunkingOptions
                {
                    ChunkTokenTarget = SmallChunkTokenTarget,
                },
            },
        });

        var evaluation = bank.EvaluateChunks(MultiBlockMarkdown, CachePath);

        evaluation.SizeDistribution.Total.ShouldBeGreaterThan(1);
    }

    private static MarkdownKnowledgeBank CreateBank(TestChatClient chatClient)
    {
        return new MarkdownKnowledgeBank(new MarkdownKnowledgeBankOptions
        {
            PipelineOptions = new MarkdownKnowledgePipelineOptions
            {
                BaseUri = BaseUri,
                ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            },
            ChatClient = chatClient,
            EmbeddingGenerator = new TestEmbeddingGenerator(),
        });
    }

    private static MarkdownSourceDocument[] CreateSources(string cacheMarkdown)
    {
        return
        [
            new MarkdownSourceDocument(CachePath, cacheMarkdown),
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown),
        ];
    }

}
