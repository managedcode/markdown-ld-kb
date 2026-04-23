using System.Collections.Concurrent;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.KnowledgeGraphStoreConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class InMemoryKnowledgeGraphStore : IKnowledgeGraphStore
{
    private readonly ConcurrentDictionary<string, string> _documents = new(StringComparer.Ordinal);

    public Task SaveAsync(
        KnowledgeGraph graph,
        string location,
        KnowledgeGraphFilePersistenceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemKnowledgeGraphStore.EnsureLocation(location);

        var normalizedLocation = KnowledgeGraphStorageLocation.Normalize(location);
        var format = KnowledgeGraphFileFormatResolver.ResolveForSave(normalizedLocation, options?.Format);
        _documents[normalizedLocation] = KnowledgeGraphTextCodec.Serialize(graph, format);
        return Task.CompletedTask;
    }

    public Task<KnowledgeGraph> LoadAsync(
        string location,
        KnowledgeGraphLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemKnowledgeGraphStore.EnsureLocation(location);

        var normalizedLocation = KnowledgeGraphStorageLocation.Normalize(location);
        var effectiveOptions = options ?? KnowledgeGraphLoadOptions.Default;
        if (_documents.TryGetValue(normalizedLocation, out var content))
        {
            return Task.FromResult(LoadSingle(normalizedLocation, content, effectiveOptions));
        }

        return Task.FromResult(LoadDirectory(normalizedLocation, effectiveOptions));
    }

    private static KnowledgeGraph LoadSingle(string location, string content, KnowledgeGraphLoadOptions options)
    {
        var graph = new Graph();
        var format = KnowledgeGraphFileFormatResolver.ResolveForLoad(location, options.Format);
        KnowledgeGraphTextCodec.MergeInto(graph, content, format);
        return new KnowledgeGraph(graph);
    }

    private KnowledgeGraph LoadDirectory(string location, KnowledgeGraphLoadOptions options)
    {
        var graph = new Graph();
        var matchedAnyLocation = false;
        var mergedAnySupportedLocation = false;

        foreach (var pair in _documents.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!KnowledgeGraphStorageLocation.TryGetDirectoryRelativePath(location, pair.Key, options.SearchOption, out var relativePath))
            {
                continue;
            }

            matchedAnyLocation = true;
            if (!KnowledgeGraphStorageLocation.MatchesPattern(relativePath, options.SearchPattern))
            {
                continue;
            }

            var format = options.Format ?? KnowledgeGraphFileFormatResolver.TryInfer(pair.Key);
            if (format is null)
            {
                if (!options.SkipUnsupportedFiles)
                {
                    throw new InvalidDataException(UnsupportedGraphFileExtensionMessagePrefix + pair.Key);
                }

                continue;
            }

            KnowledgeGraphTextCodec.MergeInto(graph, pair.Value, format.Value);
            mergedAnySupportedLocation = true;
        }

        if (!matchedAnyLocation)
        {
            throw new FileNotFoundException(GraphLocationNotFoundMessagePrefix + location, location);
        }

        if (!mergedAnySupportedLocation)
        {
            throw new InvalidDataException(GraphDirectoryContainsNoSupportedFilesMessagePrefix + location);
        }

        return new KnowledgeGraph(graph);
    }
}
