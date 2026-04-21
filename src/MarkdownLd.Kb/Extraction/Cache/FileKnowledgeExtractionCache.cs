using System.Text.Json;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class FileKnowledgeExtractionCache : IKnowledgeExtractionCache
{
    private readonly string _directoryPath;
    private readonly JsonSerializerOptions _serializerOptions;

    public FileKnowledgeExtractionCache(
        string directoryPath,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        _directoryPath = directoryPath;
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
    }

    public async Task<KnowledgeExtractionCacheEntry?> GetAsync(
        KnowledgeExtractionCacheKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var path = GetCacheFilePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var entry = await JsonSerializer.DeserializeAsync<KnowledgeExtractionCacheEntry>(
            stream,
            _serializerOptions,
            cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException(CacheEntryMissingMessage);

        return entry.Key.Matches(key) ? entry : null;
    }

    public async Task SetAsync(
        KnowledgeExtractionCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Directory.CreateDirectory(_directoryPath);

        var path = GetCacheFilePath(entry.Key);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, entry, _serializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private string GetCacheFilePath(KnowledgeExtractionCacheKey key)
    {
        var slug = KnowledgeNaming.Slugify(string.IsNullOrWhiteSpace(key.SourcePath) ? key.DocumentId : key.SourcePath);
        var chunker = KnowledgeNaming.Slugify(key.ChunkerProfileId);
        var prompt = KnowledgeNaming.Slugify(key.PromptVersion);
        var model = KnowledgeNaming.Slugify(key.ModelId);
        var fileName = string.Concat(
            slug,
            DotSeparator,
            CacheFileVersion,
            DotSeparator,
            chunker,
            DotSeparator,
            prompt,
            DotSeparator,
            model,
            CacheFileExtension);

        return Path.Combine(_directoryPath, fileName);
    }
}
