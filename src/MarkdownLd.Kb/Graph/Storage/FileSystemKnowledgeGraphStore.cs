using ManagedCode.Storage.Core;
using ManagedCode.Storage.FileSystem;
using ManagedCode.Storage.FileSystem.Options;
using static ManagedCode.MarkdownLd.Kb.Pipeline.KnowledgeGraphStoreConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class FileSystemKnowledgeGraphStore : IKnowledgeGraphStore
{
    private const bool CreateContainerIfNotExists = true;

    public static FileSystemKnowledgeGraphStore Default { get; } = new();

    public async Task SaveAsync(
        KnowledgeGraph graph,
        string location,
        KnowledgeGraphFilePersistenceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLocation(location);

        var binding = KnowledgeGraphStorageLocation.BindFileSystemPath(location);
        using var storage = CreateStorage(binding.BaseFolder);
        var store = new StorageKnowledgeGraphStore(storage);
        await store.SaveAsync(graph, binding.StorageLocation, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<KnowledgeGraph> LoadAsync(
        string location,
        KnowledgeGraphLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureLocation(location);

        var binding = KnowledgeGraphStorageLocation.BindFileSystemPath(location);
        using var storage = CreateStorage(binding.BaseFolder);
        var store = new StorageKnowledgeGraphStore(storage);
        return await store.LoadAsync(binding.StorageLocation, options, cancellationToken).ConfigureAwait(false);
    }

    internal static void EnsureLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            throw new ArgumentException(GraphLocationRequiredMessage, nameof(location));
        }
    }

    private static IStorage CreateStorage(string baseFolder)
    {
        return new FileSystemStorage(new FileSystemStorageOptions
        {
            BaseFolder = baseFolder,
            CreateContainerIfNotExists = CreateContainerIfNotExists,
        });
    }
}
