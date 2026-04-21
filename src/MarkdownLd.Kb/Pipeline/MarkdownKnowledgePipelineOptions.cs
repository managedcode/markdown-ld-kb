using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record MarkdownKnowledgePipelineOptions
{
    public Uri? BaseUri { get; init; }

    public IChatClient? ChatClient { get; init; }

    public ChatOptions? ChatOptions { get; init; }

    public string? ChatModelId { get; init; }

    public MarkdownKnowledgeExtractionMode ExtractionMode { get; init; } = MarkdownKnowledgeExtractionMode.Auto;

    public TiktokenKnowledgeGraphOptions? TiktokenOptions { get; init; }

    public KnowledgeGraphBuildOptions BuildOptions { get; init; } = KnowledgeGraphBuildOptions.Default;

    public DocumentRdfMappingOptions DocumentRdfMapping { get; init; } = DocumentRdfMappingOptions.Default;

    public IMarkdownChunker? MarkdownChunker { get; init; }

    public MarkdownChunkingOptions? ChunkingOptions { get; init; }

    public IKnowledgeExtractionCache? ExtractionCache { get; init; }
}
