namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownChunkingSection(
    string SectionId,
    int Order,
    int HeadingLevel,
    string? HeadingMarkdown,
    string? HeadingText,
    IReadOnlyList<string> HeadingPath,
    string Markdown);
