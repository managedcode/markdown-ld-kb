using RootKnowledgeFactExtractionResult = ManagedCode.MarkdownLd.Kb.KnowledgeFactExtractionResult;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeExtractionChunkFingerprint(
    string ChunkId,
    string ContentHash,
    int Order);

public sealed record KnowledgeExtractionCacheKey(
    string DocumentId,
    string SourcePath,
    string ChunkerProfileId,
    string PromptVersion,
    string ModelId,
    IReadOnlyList<KnowledgeExtractionChunkFingerprint> Chunks)
{
    public bool Matches(KnowledgeExtractionCacheKey other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(DocumentId, other.DocumentId, StringComparison.Ordinal) &&
               string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal) &&
               string.Equals(ChunkerProfileId, other.ChunkerProfileId, StringComparison.Ordinal) &&
               string.Equals(PromptVersion, other.PromptVersion, StringComparison.Ordinal) &&
               string.Equals(ModelId, other.ModelId, StringComparison.Ordinal) &&
               Chunks.SequenceEqual(other.Chunks);
    }
}

public sealed record KnowledgeExtractionCacheEntry(
    KnowledgeExtractionCacheKey Key,
    IReadOnlyList<RootKnowledgeFactExtractionResult> ChunkResults,
    DateTimeOffset CreatedAtUtc);
