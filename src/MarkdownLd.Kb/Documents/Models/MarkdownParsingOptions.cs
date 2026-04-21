using ManagedCode.MarkdownLd.Kb.Parsing;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownParsingOptions
{
    public string DefaultBaseUrl { get; init; } = MarkdownTextConstants.DefaultBaseUrl;

    public MarkdownChunkingOptions Chunking { get; init; } = MarkdownChunkingOptions.Default;
}
