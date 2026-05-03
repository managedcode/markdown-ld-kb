using ManagedCode.MarkdownLd.Kb.Parsing;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Parsing;

public sealed class MarkdownChunkOverlapFlowTests
{
    private const string SourcePath = "content/chunk-overlap.md";

    private const string Markdown = """
# Recovery

Alpha alpha alpha alpha alpha alpha.

Beta beta beta beta beta beta.

Gamma gamma gamma gamma gamma gamma.
""";

    [Test]
    public void Chunker_has_no_overlap_by_default()
    {
        var document = Parse(new MarkdownChunkingOptions
        {
            ChunkTokenTarget = 5,
        });

        document.Chunks.Count.ShouldBeGreaterThan(1);
        document.Chunks[1].Markdown.ShouldNotContain("Alpha alpha");
    }

    [Test]
    public void Chunker_can_overlap_trailing_blocks_without_splitting_block_boundaries()
    {
        var document = Parse(new MarkdownChunkingOptions
        {
            ChunkTokenTarget = 5,
            ChunkOverlapTokenTarget = 2,
        });

        document.Chunks.Count.ShouldBeGreaterThan(1);
        document.Chunks[1].Markdown.ShouldStartWith("Alpha alpha");
        document.Chunks[1].Markdown.ShouldContain("Beta beta");
    }

    [Test]
    public void Chunker_rejects_invalid_chunk_budget_options()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Parse(new MarkdownChunkingOptions
            {
                ChunkTokenTarget = 0,
            }));

        Should.Throw<ArgumentOutOfRangeException>(() =>
            Parse(new MarkdownChunkingOptions
            {
                ChunkTokenTarget = 5,
                ChunkOverlapTokenTarget = -1,
            }));
    }

    private static MarkdownDocument Parse(MarkdownChunkingOptions options)
    {
        return new MarkdownDocumentParser().Parse(
            new MarkdownDocumentSource(Markdown, SourcePath),
            new MarkdownParsingOptions
            {
                Chunking = options,
            });
    }
}
