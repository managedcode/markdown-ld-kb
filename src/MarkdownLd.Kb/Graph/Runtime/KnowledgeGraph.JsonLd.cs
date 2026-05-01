using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.KnowledgeGraphStoreConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static readonly KnowledgeGraphFilePersistenceOptions JsonLdPersistenceOptions = new()
    {
        Format = KnowledgeGraphFileFormat.JsonLd,
    };

    private static readonly KnowledgeGraphLoadOptions JsonLdLoadOptions = new()
    {
        Format = KnowledgeGraphFileFormat.JsonLd,
    };

    public Task SaveJsonLdToStoreAsync(
        IKnowledgeGraphStore store,
        string location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        return SaveToStoreAsync(store, location, JsonLdPersistenceOptions, cancellationToken);
    }

    public Task SaveJsonLdToFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return SaveToFileAsync(filePath, JsonLdPersistenceOptions, cancellationToken);
    }

    public static Task<KnowledgeGraph> LoadJsonLdFromStoreAsync(
        IKnowledgeGraphStore store,
        string location,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        return LoadFromStoreAsync(store, location, JsonLdLoadOptions, cancellationToken);
    }

    public static Task<KnowledgeGraph> LoadJsonLdFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return LoadFromFileAsync(filePath, JsonLdLoadOptions, cancellationToken);
    }

    public static KnowledgeGraph LoadJsonLd(string jsonLd)
    {
        if (string.IsNullOrWhiteSpace(jsonLd))
        {
            throw new ArgumentException(JsonLdContentRequiredMessage, nameof(jsonLd));
        }

        var graph = new Graph();
        KnowledgeGraphTextCodec.MergeInto(graph, jsonLd, KnowledgeGraphFileFormat.JsonLd);
        return new KnowledgeGraph(graph);
    }
}
