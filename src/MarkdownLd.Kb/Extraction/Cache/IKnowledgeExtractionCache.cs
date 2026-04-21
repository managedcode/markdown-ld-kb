namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public interface IKnowledgeExtractionCache
{
    Task<KnowledgeExtractionCacheEntry?> GetAsync(
        KnowledgeExtractionCacheKey key,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        KnowledgeExtractionCacheEntry entry,
        CancellationToken cancellationToken = default);
}
