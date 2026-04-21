namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownSection(
    string SectionId,
    int Order,
    int HeadingLevel,
    string? HeadingMarkdown,
    string? HeadingText,
    IReadOnlyList<string> HeadingPath,
    string Markdown,
    IReadOnlyList<MarkdownChunk> Chunks,
    IReadOnlyList<MarkdownLinkReference> Links);
