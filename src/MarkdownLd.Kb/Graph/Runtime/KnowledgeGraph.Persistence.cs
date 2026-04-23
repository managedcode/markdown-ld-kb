namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public Task SaveToStoreAsync(
        IKnowledgeGraphStore store,
        string location,
        KnowledgeGraphFilePersistenceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.SaveAsync(this, location, options, cancellationToken);
    }

    public Task SaveToFileAsync(
        string filePath,
        KnowledgeGraphFilePersistenceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return SaveToStoreAsync(FileSystemKnowledgeGraphStore.Default, filePath, options, cancellationToken);
    }

    public static Task<KnowledgeGraph> LoadFromStoreAsync(
        IKnowledgeGraphStore store,
        string location,
        KnowledgeGraphLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.LoadAsync(location, options, cancellationToken);
    }

    public static Task<KnowledgeGraph> LoadFromFileAsync(
        string filePath,
        KnowledgeGraphLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return LoadFromStoreAsync(FileSystemKnowledgeGraphStore.Default, filePath, options, cancellationToken);
    }

    public static Task<KnowledgeGraph> LoadFromDirectoryAsync(
        string directoryPath,
        KnowledgeGraphLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return LoadFromStoreAsync(FileSystemKnowledgeGraphStore.Default, directoryPath, options, cancellationToken);
    }
}
