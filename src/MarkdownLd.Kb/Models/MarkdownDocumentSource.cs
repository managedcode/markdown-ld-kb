namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownDocumentSource(
    string ContentMarkdown,
    string? ContentPath = null,
    string? BaseUrl = null);
