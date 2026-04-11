namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownChunk(
    string ChunkId,
    string SectionId,
    int Order,
    IReadOnlyList<string> HeadingPath,
    string Markdown,
    int EstimatedTokenCount,
    IReadOnlyList<MarkdownLinkReference> Links);
