namespace ManagedCode.MarkdownLd.Kb;

public interface IMarkdownChunker
{
    string ProfileId { get; }

    IReadOnlyList<MarkdownSection> Chunk(
        MarkdownChunkingDocument document,
        MarkdownChunkingOptions options);
}
