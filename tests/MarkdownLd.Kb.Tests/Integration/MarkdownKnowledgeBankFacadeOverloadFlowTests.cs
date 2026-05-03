using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class MarkdownKnowledgeBankFacadeOverloadFlowTests
{
    private const string BaseUriText = "https://facade.example/";
    private const string CachePath = "content/runbooks/cache-restore.md";
    private const string NotificationsPath = "content/tools/notification-settings.md";
    private const string NormalizedCachePath = "runbooks/cache-restore.md";
    private const string NormalizedNotificationsPath = "tools/notification-settings.md";
    private const string Query = "cache restore";
    private const string AnswerText = "Use the cache restore runbook for cache restore work [1].";
    private const string MediaType = "text/markdown";
    private const string CoverageQuestion = "How do I restore cache safely?";
    private const string CoverageAnswer = "cache restore verification";

    private static readonly Uri BaseUri = new(BaseUriText);

    private const string CacheMarkdown = """
---
title: Cache restore runbook
summary: Cache restore cache manifest restore evidence and cache validation.
---
# Cache restore runbook

Use this runbook for cache restore verification and manifest checks.
""";

    private const string NotificationsMarkdown = """
---
title: Notification settings
summary: Manage notification delivery preferences and inbox alerts.
---
# Notification settings

Use this tool to review notification delivery preferences.
""";

    [Test]
    public async Task Unified_api_supports_file_directory_document_and_knowledge_source_overloads()
    {
        var bank = CreateBank();
        var knowledgeSources = new[]
        {
            new KnowledgeSourceDocument(CachePath, CacheMarkdown, null, MediaType),
            new KnowledgeSourceDocument(NotificationsPath, NotificationsMarkdown, null, MediaType),
        };

        var sourceChangeSet = bank.PlanChanges(knowledgeSources);
        sourceChangeSet.RemovedPaths.ShouldBeEmpty();

        var sourceBuild = await bank.BuildAsync(knowledgeSources);
        sourceBuild.Result.ShouldNotBeNull();
        sourceBuild.Documents.Count.ShouldBe(2);
        sourceBuild.Facts.Entities.ShouldNotBeNull();
        sourceBuild.Contract.ShouldNotBeNull();
        sourceBuild.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
        sourceBuild.Diagnostics.ShouldNotBeEmpty();
        sourceBuild.ValidateShacl().Conforms.ShouldBeTrue();

        var requestAnswer = await sourceBuild.AnswerAsync(
            new KnowledgeAnswerRequest(Query)
            {
                SearchOptions = new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Bm25,
                    MaxResults = 2,
                },
            });
        requestAnswer.Citations.Single().SourcePath.ShouldBe(CachePath);

        var incremental = await bank.BuildIncrementalAsync(
            knowledgeSources,
            sourceChangeSet.Manifest,
            sourceBuild.Graph);
        incremental.Result.ShouldNotBeNull();
        incremental.Manifest.Entries.Select(static entry => entry.Path).ShouldBe(
            sourceChangeSet.Manifest.Entries.Select(static entry => entry.Path));
        incremental.ChangedPaths.ShouldBeEmpty();
        incremental.RemovedPaths.ShouldBeEmpty();
        incremental.UnchangedPaths.ShouldBe([NormalizedCachePath, NormalizedNotificationsPath]);
        incremental.Diff.AddedEdges.ShouldBeEmpty();

        var parsed = new ManagedCode.MarkdownLd.Kb.Parsing.MarkdownDocumentParser()
            .Parse(new MarkdownDocumentSource(CacheMarkdown, CachePath));
        var documentEvaluation = bank.EvaluateChunks(
            parsed,
            [new MarkdownChunkCoverageExpectation(CoverageQuestion, CoverageAnswer)]);
        documentEvaluation.CoverageRate.ShouldBe(1d);

        await VerifyFileAndDirectoryBuildsAsync(bank);
    }

    private static MarkdownKnowledgeBank CreateBank()
    {
        return new MarkdownKnowledgeBank(new MarkdownKnowledgeBankOptions
        {
            PipelineOptions = new MarkdownKnowledgePipelineOptions
            {
                BaseUri = BaseUri,
                ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            },
            ChatClient = new TestChatClient((_, _) => AnswerText),
            EmbeddingGenerator = new TestEmbeddingGenerator(),
        });
    }

    private static async Task VerifyFileAndDirectoryBuildsAsync(MarkdownKnowledgeBank bank)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var cacheFilePath = Path.Combine(directory, "cache-restore.md");
            var notificationFilePath = Path.Combine(directory, "notification-settings.md");
            await File.WriteAllTextAsync(cacheFilePath, CacheMarkdown);
            await File.WriteAllTextAsync(notificationFilePath, NotificationsMarkdown);

            var fileBuild = await bank.BuildFromFileAsync(cacheFilePath);
            fileBuild.Documents.Count.ShouldBe(1);

            var directoryBuild = await bank.BuildFromDirectoryAsync(directory, searchPattern: "*.md");
            directoryBuild.Documents.Count.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
