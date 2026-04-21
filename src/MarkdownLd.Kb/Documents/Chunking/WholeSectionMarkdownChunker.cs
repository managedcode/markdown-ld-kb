namespace ManagedCode.MarkdownLd.Kb;

public sealed class WholeSectionMarkdownChunker : IMarkdownChunker
{
    public const string DefaultProfileId = "whole-section-v1";

    public string ProfileId => DefaultProfileId;

    public IReadOnlyList<MarkdownSection> Chunk(MarkdownChunkingDocument document, MarkdownChunkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        return document.Sections
            .Select(section => BuildSection(document, section))
            .ToArray();
    }

    private static MarkdownSection BuildSection(
        MarkdownChunkingDocument document,
        MarkdownChunkingSection section)
    {
        var chunks = MarkdownChunkFactory.CreateWholeSectionChunks(document, section);
        return MarkdownChunkFactory.BuildSection(section, chunks);
    }
}
