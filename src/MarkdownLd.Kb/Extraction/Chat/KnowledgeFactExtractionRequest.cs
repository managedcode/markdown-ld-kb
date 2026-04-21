using static ManagedCode.MarkdownLd.Kb.KnowledgeFactConstants;

namespace ManagedCode.MarkdownLd.Kb;

public sealed record KnowledgeFactExtractionRequest(
    string DocumentId,
    string ChunkId,
    string Markdown,
    string? Title = null,
    string? SectionPath = null,
    IReadOnlyDictionary<string, string?>? FrontMatter = null)
{
    public string ChunkSourceUri => string.Concat(ChunkSourcePrefix, DocumentId, ChunkSourceSeparator, ChunkId);
}
