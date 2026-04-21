using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class FileKnowledgeExtractionCache : IKnowledgeExtractionCache
{
    private const string TemporaryFileSuffix = ".tmp-";
    private const int CacheKeyHashLength = 24;

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
        KnowledgeExtractionCacheEntry entry;
        try
        {
            entry = await JsonSerializer.DeserializeAsync<KnowledgeExtractionCacheEntry>(
                stream,
                _serializerOptions,
                cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException(CacheEntryMissingMessage);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(CacheEntryMissingMessage, exception);
        }

        return entry.Key.Matches(key) ? entry : null;
    }

    public async Task SetAsync(
        KnowledgeExtractionCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Directory.CreateDirectory(_directoryPath);

        var path = GetCacheFilePath(entry.Key);
        var temporaryPath = path + TemporaryFileSuffix + Guid.NewGuid().ToString("N");

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, entry, _serializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetCacheFilePath(KnowledgeExtractionCacheKey key)
    {
        var slug = KnowledgeNaming.Slugify(string.IsNullOrWhiteSpace(key.SourcePath) ? key.DocumentId : key.SourcePath);
        var chunker = KnowledgeNaming.Slugify(key.ChunkerProfileId);
        var prompt = KnowledgeNaming.Slugify(key.PromptVersion);
        var model = KnowledgeNaming.Slugify(key.ModelId);
        var keyHash = CreateCacheKeyHash(key);
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
            DotSeparator,
            keyHash,
            CacheFileExtension);

        return Path.Combine(_directoryPath, fileName);
    }

    private string CreateCacheKeyHash(KnowledgeExtractionCacheKey key)
    {
        var keyJson = JsonSerializer.Serialize(key, _serializerOptions);
        var keyBytes = Encoding.UTF8.GetBytes(keyJson);
        var hashBytes = SHA256.HashData(keyBytes);
        var hash = Convert.ToHexString(hashBytes);

        return hash[..CacheKeyHashLength];
    }
}
