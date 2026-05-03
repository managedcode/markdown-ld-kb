namespace ManagedCode.MarkdownLd.Kb;

public sealed record MarkdownChunkingOptions
{
    public static MarkdownChunkingOptions Default { get; } = new();

    public int ChunkTokenTarget { get; init; } = 750;

    public int ChunkOverlapTokenTarget { get; init; }
}
