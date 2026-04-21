namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownEntityHint(
    string Label,
    string? Type = null,
    IReadOnlyList<string>? SameAs = null);
