using ManagedCode.Storage.Core;
using ManagedCode.Storage.FileSystem.Extensions;
using ManagedCode.Storage.FileSystem.Options;
using ManagedCode.Storage.VirtualFileSystem.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public static class KnowledgeGraphStoreServiceCollectionExtensions
{
    public static IServiceCollection AddStorageBackedKnowledgeGraphStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IKnowledgeGraphStore>(static provider =>
            new StorageKnowledgeGraphStore(provider.GetRequiredService<IStorage>()));
    }

    public static IServiceCollection AddKeyedStorageBackedKnowledgeGraphStore<TStorage>(this IServiceCollection services, object serviceKey)
        where TStorage : class, IStorage
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);

        return services.AddKeyedSingleton<IKnowledgeGraphStore>(serviceKey, static (provider, key) =>
            new StorageKnowledgeGraphStore(provider.GetRequiredKeyedService<TStorage>(key)));
    }

    public static IServiceCollection AddVirtualFileSystemKnowledgeGraphStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLogging();
        services.AddMemoryCache();
        return services.AddSingleton<IKnowledgeGraphStore>(static provider =>
            new StorageKnowledgeGraphStore(provider.GetRequiredService<IVirtualFileSystem>().Storage));
    }

    public static IServiceCollection AddInMemoryKnowledgeGraphStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IKnowledgeGraphStore, InMemoryKnowledgeGraphStore>();
    }

    public static IServiceCollection AddFileSystemKnowledgeGraphStoreAsDefault(
        this IServiceCollection services,
        Action<FileSystemStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddFileSystemStorageAsDefault(configure);
        return services.AddStorageBackedKnowledgeGraphStore();
    }
}
