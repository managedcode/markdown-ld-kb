namespace ManagedCode.MarkdownLd.Kb;

public sealed class DeterministicSectionMarkdownChunker : IMarkdownChunker
{
    public const string DefaultProfileId = "deterministic-section-v1";

    public static DeterministicSectionMarkdownChunker Default { get; } = new();

    public string ProfileId => DefaultProfileId;

    public IReadOnlyList<MarkdownSection> Chunk(MarkdownChunkingDocument document, MarkdownChunkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);

        return document.Sections
            .Select(section => BuildSection(document, section, options))
            .ToArray();
    }

    private static MarkdownSection BuildSection(
        MarkdownChunkingDocument document,
        MarkdownChunkingSection section,
        MarkdownChunkingOptions options)
    {
        var chunks = MarkdownChunkFactory.CreateSplitChunks(document, section, options);
        return MarkdownChunkFactory.BuildSection(section, chunks);
    }
}
