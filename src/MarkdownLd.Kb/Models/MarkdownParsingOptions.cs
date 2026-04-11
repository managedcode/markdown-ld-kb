namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownParsingOptions
{
    public int ChunkTokenTarget { get; init; } = 750;

    public string DefaultBaseUrl { get; init; } = ManagedCode.MarkdownLd.Kb.Parsing.MarkdownTextConstants.DefaultBaseUrl;
}
