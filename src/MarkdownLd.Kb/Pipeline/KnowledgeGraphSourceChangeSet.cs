namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphSourceChangeSet(
    KnowledgeGraphSourceManifest Manifest,
    IReadOnlyList<string> ChangedPaths,
    IReadOnlyList<string> UnchangedPaths,
    IReadOnlyList<string> RemovedPaths);
