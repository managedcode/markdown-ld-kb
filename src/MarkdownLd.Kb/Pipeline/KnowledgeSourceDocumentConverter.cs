using System.Runtime.CompilerServices;
using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeDocumentConversionOptions
{
    public Encoding? Encoding { get; init; }
    public Uri? CanonicalUri { get; init; }
    public string? MediaType { get; init; }
    public bool SkipUnsupportedFiles { get; init; } = true;
}

public sealed class KnowledgeSourceDocumentConverter
{
    private static readonly IReadOnlyDictionary<string, string> SupportedMediaTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [MarkdownExtension] = MarkdownMediaType,
            [MarkdownLongExtension] = MarkdownMediaType,
            [MdxExtension] = MdxMediaType,
            [TextExtension] = PlainTextMediaType,
            [TextLongExtension] = PlainTextMediaType,
            [LogExtension] = PlainTextMediaType,
            [CsvExtension] = CsvMediaType,
            [JsonExtension] = JsonMediaType,
            [JsonLinesExtension] = JsonMediaType,
            [YamlExtension] = YamlMediaType,
            [YmlExtension] = YamlMediaType,
        };

    public KnowledgeSourceDocument ConvertContent(
        string? content,
        string? path = null,
        KnowledgeDocumentConversionOptions? options = null)
    {
        var sourcePath = string.IsNullOrWhiteSpace(path) ? ConverterDefaultPath : path;
        return new KnowledgeSourceDocument(
            sourcePath,
            content ?? string.Empty,
            options?.CanonicalUri,
            ResolveMediaType(sourcePath, options?.MediaType));
    }

    public async Task<KnowledgeSourceDocument> ConvertFileAsync(
        string filePath,
        KnowledgeDocumentConversionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return await ConvertFileCoreAsync(filePath, null, options, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<KnowledgeSourceDocument> ConvertDirectoryAsync(
        string directoryPath,
        KnowledgeDocumentConversionOptions? options = null,
        string? searchPattern = null,
        SearchOption searchOption = SearchOption.AllDirectories,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException(string.Concat(DirectoryNotFoundMessagePrefix, directoryPath));
        }

        var effectiveSearchPattern = string.IsNullOrWhiteSpace(searchPattern)
            ? AllFilesSearchPattern
            : searchPattern;

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, effectiveSearchPattern, searchOption)
                     .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await TryConvertDirectoryEntryAsync(filePath, directoryPath, options, cancellationToken).ConfigureAwait(false);
            if (document is not null)
            {
                yield return document;
            }
        }
    }

    public static bool IsSupportedTextFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrWhiteSpace(extension) && SupportedMediaTypes.ContainsKey(extension);
    }

    private static async Task<KnowledgeSourceDocument> ConvertFileCoreAsync(
        string filePath,
        string? rootDirectory,
        KnowledgeDocumentConversionOptions? options,
        CancellationToken cancellationToken)
    {
        var content = await KnowledgeSourceDocumentTextLoader
            .ReadTextAsync(filePath, options?.Encoding, cancellationToken)
            .ConfigureAwait(false);
        var sourcePath = NormalizeSourcePath(filePath, rootDirectory);

        return new KnowledgeSourceDocument(
            sourcePath,
            content,
            options?.CanonicalUri,
            ResolveMediaType(filePath, options?.MediaType));
    }

    private static async Task<KnowledgeSourceDocument?> TryConvertDirectoryEntryAsync(
        string filePath,
        string directoryPath,
        KnowledgeDocumentConversionOptions? options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ConvertFileCoreAsync(filePath, directoryPath, options, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException) when (options?.SkipUnsupportedFiles ?? true)
        {
            return null;
        }
    }

    private static string NormalizeSourcePath(string filePath, string? rootDirectory)
    {
        var sourcePath = string.IsNullOrWhiteSpace(rootDirectory)
            ? Path.GetFileName(filePath)
            : Path.GetRelativePath(rootDirectory, filePath);

        return sourcePath.Replace('\\', '/');
    }

    private static string ResolveMediaType(string path, string? mediaTypeOverride)
    {
        if (!string.IsNullOrWhiteSpace(mediaTypeOverride))
        {
            return mediaTypeOverride.Trim();
        }

        var extension = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(extension) && SupportedMediaTypes.TryGetValue(extension, out var mediaType)
            ? mediaType
            : PlainTextMediaType;
    }
}
