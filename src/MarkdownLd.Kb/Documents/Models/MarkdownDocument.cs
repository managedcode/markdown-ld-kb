namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownDocument(
    string DocumentId,
    string? ContentPath,
    Uri? BaseUri,
    MarkdownFrontMatter FrontMatter,
    string SourceMarkdown,
    string BodyMarkdown,
    IReadOnlyList<MarkdownSection> Sections,
    IReadOnlyList<MarkdownChunk> Chunks,
    IReadOnlyList<MarkdownLinkReference> Links);
