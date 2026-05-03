namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class MarkdownKnowledgePipeline
{
    public Task<MarkdownKnowledgeIncrementalBuildResult> BuildIncrementalAsync(
        IEnumerable<KnowledgeSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null,
        KnowledgeGraph? previousGraph = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return BuildIncrementalAsync(
            sources.Select(static source => source.ToMarkdownSourceDocument()),
            previousManifest,
            previousGraph,
            cancellationToken);
    }

    public async Task<MarkdownKnowledgeIncrementalBuildResult> BuildIncrementalAsync(
        IEnumerable<MarkdownSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null,
        KnowledgeGraph? previousGraph = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceList = sources.ToArray();
        var changeSet = KnowledgeGraphSourceManifest.CreateChangeSet(sourceList, previousManifest);
        var result = await BuildAsync(sourceList, cancellationToken).ConfigureAwait(false);
        var diff = previousGraph is null
            ? KnowledgeGraphDiff.Empty
            : previousGraph.Diff(result.Graph);

        return new MarkdownKnowledgeIncrementalBuildResult(
            result,
            changeSet.Manifest,
            changeSet.ChangedPaths,
            changeSet.RemovedPaths,
            diff)
        {
            UnchangedPaths = changeSet.UnchangedPaths,
        };
    }
}
