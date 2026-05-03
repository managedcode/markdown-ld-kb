using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeGraphSourceManifestPlannerFlowTests
{
    private const string CachePath = "docs/cache.md";
    private const string ReleasePath = "docs/release.md";
    private const string BaselineCacheMarkdown = "# Cache\n\nRestore cache manifests before deploy.";
    private const string ChangedCacheMarkdown = "# Cache\n\nVerify cache manifests before deploy.";
    private const string ReleaseMarkdown = "# Release\n\nShip release notes after validation.";
    private const string MarkdownMediaType = "text/markdown";

    [Test]
    public void Source_manifest_change_set_reports_changed_unchanged_and_removed_paths_without_building_graph()
    {
        var baseline = KnowledgeGraphSourceManifest.Create(
        [
            new MarkdownSourceDocument(CachePath, BaselineCacheMarkdown),
            new MarkdownSourceDocument(ReleasePath, ReleaseMarkdown),
        ]);

        var next = KnowledgeGraphSourceManifest.CreateChangeSet(
        [
            new MarkdownSourceDocument(CachePath, ChangedCacheMarkdown),
            new MarkdownSourceDocument(ReleasePath, ReleaseMarkdown),
        ], baseline);

        next.ChangedPaths.ShouldBe([CachePath]);
        next.UnchangedPaths.ShouldBe([ReleasePath]);
        next.RemovedPaths.ShouldBeEmpty();

        var removed = KnowledgeGraphSourceManifest.CreateChangeSet(
        [
            new MarkdownSourceDocument(CachePath, ChangedCacheMarkdown),
        ], next.Manifest);

        removed.ChangedPaths.ShouldBeEmpty();
        removed.UnchangedPaths.ShouldBe([CachePath]);
        removed.RemovedPaths.ShouldBe([ReleasePath]);
    }

    [Test]
    public async Task Source_manifest_planning_supports_knowledge_source_documents()
    {
        var source = new KnowledgeSourceDocument(CachePath, BaselineCacheMarkdown, null, MarkdownMediaType);
        var manifest = KnowledgeGraphSourceManifest.Create([source]);
        var changeSet = KnowledgeGraphSourceManifest.CreateChangeSet([source]);
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            ExtractionMode = MarkdownKnowledgeExtractionMode.None,
        });

        var incremental = await pipeline.BuildIncrementalAsync([source]);

        manifest.Entries.Single().Path.ShouldBe(CachePath);
        changeSet.ChangedPaths.ShouldBe([CachePath]);
        changeSet.UnchangedPaths.ShouldBeEmpty();
        changeSet.RemovedPaths.ShouldBeEmpty();
        incremental.ChangedPaths.ShouldBe([CachePath]);
        incremental.BuildResult.Documents.Single().SourcePath.ShouldBe(CachePath);
    }
}
