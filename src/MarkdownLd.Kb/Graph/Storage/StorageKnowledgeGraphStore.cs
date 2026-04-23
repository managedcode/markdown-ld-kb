using ManagedCode.Communication;
using ManagedCode.Storage.Core;
using ManagedCode.Storage.Core.Models;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.KnowledgeGraphStoreConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class StorageKnowledgeGraphStore(IStorage storage) : IKnowledgeGraphStore
{
    private readonly IStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    public async Task SaveAsync(
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
        var content = KnowledgeGraphTextCodec.Serialize(graph, format);
        var saveResult = await _storage.UploadAsync(
            content,
            uploadOptions =>
            {
                ApplyLocation(uploadOptions, normalizedLocation);
                uploadOptions.MimeType = KnowledgeGraphFileFormatResolver.GetMimeType(format);
            },
            cancellationToken).ConfigureAwait(false);

        ThrowIfFailed(saveResult, StorageSaveFailedMessagePrefix + normalizedLocation);
    }

    public async Task<KnowledgeGraph> LoadAsync(
        string location,
        KnowledgeGraphLoadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileSystemKnowledgeGraphStore.EnsureLocation(location);

        var normalizedLocation = KnowledgeGraphStorageLocation.Normalize(location);
        var effectiveOptions = options ?? KnowledgeGraphLoadOptions.Default;
        var existsResult = await _storage.ExistsAsync(normalizedLocation, cancellationToken).ConfigureAwait(false);
        ThrowIfFailed(existsResult, StorageLoadFailedMessagePrefix + normalizedLocation);

        if (existsResult.Value)
        {
            return await LoadSingleAsync(normalizedLocation, effectiveOptions, cancellationToken).ConfigureAwait(false);
        }

        return await LoadDirectoryAsync(normalizedLocation, effectiveOptions, cancellationToken).ConfigureAwait(false);
    }

    private async Task<KnowledgeGraph> LoadSingleAsync(
        string location,
        KnowledgeGraphLoadOptions options,
        CancellationToken cancellationToken)
    {
        var graph = new Graph();
        await MergeLocationAsync(graph, location, options.Format, cancellationToken).ConfigureAwait(false);
        return new KnowledgeGraph(graph);
    }

    private async Task<KnowledgeGraph> LoadDirectoryAsync(
        string location,
        KnowledgeGraphLoadOptions options,
        CancellationToken cancellationToken)
    {
        var graph = new Graph();
        var matchedAnyLocation = false;
        var mergedAnySupportedLocation = false;

        foreach (var blob in await ListDirectoryBlobsAsync(location, cancellationToken).ConfigureAwait(false))
        {
            if (!KnowledgeGraphStorageLocation.TryGetDirectoryRelativePath(location, blob.FullName, options.SearchOption, out var relativePath))
            {
                continue;
            }

            matchedAnyLocation = true;
            if (!KnowledgeGraphStorageLocation.MatchesPattern(relativePath, options.SearchPattern))
            {
                continue;
            }

            var format = options.Format ?? KnowledgeGraphFileFormatResolver.TryInfer(blob.FullName);
            if (format is null)
            {
                if (!options.SkipUnsupportedFiles)
                {
                    throw new InvalidDataException(UnsupportedGraphFileExtensionMessagePrefix + blob.FullName);
                }

                continue;
            }

            await MergeLocationAsync(graph, blob.FullName, format, cancellationToken).ConfigureAwait(false);
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

    private async Task MergeLocationAsync(
        Graph graph,
        string location,
        KnowledgeGraphFileFormat? format,
        CancellationToken cancellationToken)
    {
        var effectiveFormat = KnowledgeGraphFileFormatResolver.ResolveForLoad(location, format);
        var streamResult = await _storage.GetStreamAsync(location, cancellationToken).ConfigureAwait(false);
        ThrowIfFailed(streamResult, StorageLoadFailedMessagePrefix + location);
        if (streamResult.Value is null)
        {
            throw new InvalidOperationException(StorageLoadFailedMessagePrefix + location);
        }

        await using var stream = streamResult.Value;
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        KnowledgeGraphTextCodec.MergeInto(graph, content, effectiveFormat);
    }

    private static void ApplyLocation(BaseOptions options, string location)
    {
        var normalized = KnowledgeGraphStorageLocation.Normalize(location);
        var directory = Path.GetDirectoryName(normalized);

        options.FileName = Path.GetFileName(normalized);
        options.Directory = string.IsNullOrWhiteSpace(directory)
            ? string.Empty
            : KnowledgeGraphStorageLocation.Normalize(directory);
    }

    private async Task<IReadOnlyList<BlobMetadata>> ListDirectoryBlobsAsync(string location, CancellationToken cancellationToken)
    {
        var blobs = new List<BlobMetadata>();
        await foreach (var blob in _storage.GetBlobMetadataListAsync(location, cancellationToken))
        {
            blobs.Add(blob);
        }

        blobs.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.FullName, right.FullName));
        return blobs;
    }

    private static void ThrowIfFailed<T>(Result<T> result, string message)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(message + Environment.NewLine + result.ToDisplayMessage());
        }
    }
}
