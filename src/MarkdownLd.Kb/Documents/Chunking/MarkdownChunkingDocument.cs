namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownChunkingDocument(
    string DocumentId,
    string? ContentPath,
    Uri BaseUri,
    string BodyMarkdown,
    IReadOnlyList<MarkdownChunkingSection> Sections);
