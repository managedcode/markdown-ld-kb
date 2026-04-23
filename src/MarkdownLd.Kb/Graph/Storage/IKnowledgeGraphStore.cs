namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public interface IKnowledgeGraphStore
{
    Task SaveAsync(
        KnowledgeGraph graph,
        string location,
        KnowledgeGraphFilePersistenceOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<KnowledgeGraph> LoadAsync(
        string location,
        KnowledgeGraphLoadOptions? options = null,
        CancellationToken cancellationToken = default);
}
