namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record DocumentRdfMappingOptions
{
    public static DocumentRdfMappingOptions Default { get; } = new();

    public bool EnableFrontMatterMappings { get; init; } = true;

    public IReadOnlyDictionary<string, string> Prefixes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
