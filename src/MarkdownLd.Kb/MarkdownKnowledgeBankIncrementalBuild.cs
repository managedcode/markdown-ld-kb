using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownKnowledgeBankIncrementalBuild
{
    internal MarkdownKnowledgeBankIncrementalBuild(
        MarkdownKnowledgeIncrementalBuildResult result,
        MarkdownKnowledgeBankBuild build)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(build);

        Result = result;
        Build = build;
    }

    public MarkdownKnowledgeIncrementalBuildResult Result { get; }

    public MarkdownKnowledgeBankBuild Build { get; }

    public KnowledgeGraphSourceManifest Manifest => Result.Manifest;

    public IReadOnlyList<string> ChangedPaths => Result.ChangedPaths;

    public IReadOnlyList<string> UnchangedPaths => Result.UnchangedPaths;

    public IReadOnlyList<string> RemovedPaths => Result.RemovedPaths;

    public KnowledgeGraphDiff Diff => Result.Diff;
}
