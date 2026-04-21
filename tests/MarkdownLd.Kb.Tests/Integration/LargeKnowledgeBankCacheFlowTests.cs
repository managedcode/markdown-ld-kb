using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class LargeKnowledgeBankCacheFlowTests
{
    private const string CacheDirectoryPrefix = "markdown-ld-kb-large-cache-";
    private const string FirstModelId = "large-chat-model-a";
    private const string SecondModelId = "large-chat-model-b";
    private const string AlternateCanonicalUrl = "https://large-fixture.example/playbooks/extraction-operations-workbook-alt/";
    private const string CanonicalUrlToReplace = "https://large-fixture.example/playbooks/extraction-operations-workbook/";
    private const string CorruptCacheText = "{ invalid json";
    private const string TemporarySuffix = ".tmp-";
    private const string AddedSentence = "The operator also records a second cache rewrite note for the regression test.";

    [Test]
    public async Task Large_chat_corpus_reuses_warm_file_cache_without_extra_chat_calls()
    {
        var cacheDirectory = CreateCacheDirectory();

        try
        {
            var cache = new FileKnowledgeExtractionCache(cacheDirectory);
            var chatClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();

            await BuildChatAsync(LargeKnowledgeBankFixtureCatalog.CreateChatSources(), chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());
            await BuildChatAsync(LargeKnowledgeBankFixtureCatalog.CreateChatSources(), chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());

            chatClient.CallCount.ShouldBe(8);
            Directory.GetFiles(cacheDirectory, "*.json", SearchOption.TopDirectoryOnly).Length.ShouldBe(2);
            Directory.GetFiles(cacheDirectory, "*" + TemporarySuffix + "*", SearchOption.TopDirectoryOnly).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Large_chat_cache_invalidates_when_document_content_changes()
    {
        var cacheDirectory = CreateCacheDirectory();

        try
        {
            var cache = new FileKnowledgeExtractionCache(cacheDirectory);
            var chatClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
            var original = LargeKnowledgeBankFixtureCatalog.CreateSource(LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook);
            var changed = new MarkdownSourceDocument(
                LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook.SourcePath,
                original.Content + Environment.NewLine + AddedSentence);

            await BuildChatAsync([original], chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());
            await BuildChatAsync([changed], chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());

            chatClient.CallCount.ShouldBe(8);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Large_chat_cache_invalidates_when_model_identity_changes()
    {
        var cacheDirectory = CreateCacheDirectory();

        try
        {
            var cache = new FileKnowledgeExtractionCache(cacheDirectory);
            var firstClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
            var secondClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
            IReadOnlyList<MarkdownSourceDocument> sources =
            [
                LargeKnowledgeBankFixtureCatalog.CreateSource(LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook),
            ];

            await BuildChatAsync(sources, firstClient, cache, FirstModelId, new WholeSectionMarkdownChunker());
            await BuildChatAsync(sources, secondClient, cache, SecondModelId, new WholeSectionMarkdownChunker());

            firstClient.CallCount.ShouldBe(4);
            secondClient.CallCount.ShouldBe(4);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Large_chat_cache_invalidates_when_chunker_profile_changes()
    {
        var cacheDirectory = CreateCacheDirectory();

        try
        {
            var cache = new FileKnowledgeExtractionCache(cacheDirectory);
            var firstClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
            var secondClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
            IReadOnlyList<MarkdownSourceDocument> sources =
            [
                LargeKnowledgeBankFixtureCatalog.CreateSource(LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook),
            ];

            await BuildChatAsync(sources, firstClient, cache, FirstModelId, new WholeSectionMarkdownChunker());
            await BuildChatAsync(sources, secondClient, cache, FirstModelId, DeterministicSectionMarkdownChunker.Default);

            firstClient.CallCount.ShouldBe(4);
            secondClient.CallCount.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task File_cache_keeps_distinct_entries_for_same_source_path_with_different_document_ids()
    {
        var cacheDirectory = CreateCacheDirectory();

        try
        {
            var cache = new FileKnowledgeExtractionCache(cacheDirectory);
            var chatClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
            var originalMarkdown = LargeKnowledgeBankFixtureCatalog.ReadFixture(LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook.FixturePath);
            var alternateMarkdown = originalMarkdown.Replace(CanonicalUrlToReplace, AlternateCanonicalUrl, StringComparison.Ordinal);
            var original = new MarkdownSourceDocument(LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook.SourcePath, originalMarkdown);
            var alternate = new MarkdownSourceDocument(LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook.SourcePath, alternateMarkdown);

            await BuildChatAsync([original], chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());
            await BuildChatAsync([alternate], chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());
            await BuildChatAsync([original], chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());

            chatClient.CallCount.ShouldBe(8);
            Directory.GetFiles(cacheDirectory, "*.json", SearchOption.TopDirectoryOnly).Length.ShouldBe(2);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Corrupt_large_cache_entry_throws_instead_of_silently_falling_back()
    {
        var cacheDirectory = CreateCacheDirectory();

        try
        {
            var cache = new FileKnowledgeExtractionCache(cacheDirectory);
            var chatClient = LargeKnowledgeBankFixtureCatalog.CreateChatClient();
            IReadOnlyList<MarkdownSourceDocument> sources =
            [
                LargeKnowledgeBankFixtureCatalog.CreateSource(LargeKnowledgeBankFixtureCatalog.ExtractionWorkbook),
            ];

            await BuildChatAsync(sources, chatClient, cache, FirstModelId, new WholeSectionMarkdownChunker());

            var cacheFile = Directory.GetFiles(cacheDirectory, "*.json", SearchOption.TopDirectoryOnly).Single();
            await File.WriteAllTextAsync(cacheFile, CorruptCacheText);

            await Should.ThrowAsync<InvalidDataException>(async () =>
                await BuildChatAsync(sources, LargeKnowledgeBankFixtureCatalog.CreateChatClient(), cache, FirstModelId, new WholeSectionMarkdownChunker()));
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    private static string CreateCacheDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), CacheDirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<MarkdownKnowledgeBuildResult> BuildChatAsync(
        IReadOnlyList<MarkdownSourceDocument> sources,
        TestChatClient chatClient,
        IKnowledgeExtractionCache cache,
        string modelId,
        IMarkdownChunker chunker)
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            BaseUri = LargeKnowledgeBankFixtureCatalog.BaseUri,
            ChatClient = chatClient,
            ChatModelId = modelId,
            MarkdownChunker = chunker,
            ExtractionCache = cache,
        });

        return await pipeline.BuildAsync(sources);
    }
}
