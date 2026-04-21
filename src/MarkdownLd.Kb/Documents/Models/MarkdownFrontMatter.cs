namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownFrontMatter
{
    public required string RawYaml { get; init; }

    public required IReadOnlyDictionary<string, object?> Values { get; init; }

    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? CanonicalUrl { get; init; }

    public IReadOnlyList<string> About { get; init; } = Array.Empty<string>();

    public string? DatePublished { get; init; }

    public string? DateModified { get; init; }

    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public IReadOnlyList<MarkdownEntityHint> EntityHints { get; init; } = Array.Empty<MarkdownEntityHint>();
}
