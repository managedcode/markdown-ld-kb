using System.Collections.Concurrent;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Tests.Support;

public static class LargeKnowledgeBankBuildCache
{
    private static readonly ConcurrentDictionary<MarkdownKnowledgeExtractionMode, Lazy<Task<MarkdownKnowledgeBuildResult>>> Cache = new();
    private static readonly TiktokenKnowledgeGraphOptions LargeFixtureTiktokenOptions = new()
    {
        BuildAutoRelatedSegmentRelations = false,
    };

    public static Task<MarkdownKnowledgeBuildResult> GetAsync(MarkdownKnowledgeExtractionMode extractionMode)
    {
        var lazyBuild = Cache.GetOrAdd(
            extractionMode,
            static mode => new Lazy<Task<MarkdownKnowledgeBuildResult>>(
                () => BuildAsync(mode),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazyBuild.Value;
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAsync(MarkdownKnowledgeExtractionMode extractionMode)
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            BaseUri = LargeKnowledgeBankFixtureCatalog.BaseUri,
            ExtractionMode = extractionMode,
            TiktokenOptions = ResolveTiktokenOptions(extractionMode),
            BuildOptions = new KnowledgeGraphBuildOptions
            {
                IncludeAssertionReification = false,
            },
        });

        return pipeline.BuildAsync(LargeKnowledgeBankFixtureCatalog.CreateGraphSources());
    }

    private static TiktokenKnowledgeGraphOptions? ResolveTiktokenOptions(MarkdownKnowledgeExtractionMode extractionMode)
    {
        return extractionMode == MarkdownKnowledgeExtractionMode.Tiktoken
            ? LargeFixtureTiktokenOptions
            : null;
    }
}
