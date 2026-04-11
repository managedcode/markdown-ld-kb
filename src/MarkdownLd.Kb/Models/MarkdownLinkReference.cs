namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownLinkReference(
    MarkdownLinkKind Kind,
    string Target,
    string DisplayText,
    string? Destination,
    string? Title,
    bool IsExternal,
    bool IsImage,
    bool IsDocumentLink,
    string? ResolvedTarget,
    int Order);
