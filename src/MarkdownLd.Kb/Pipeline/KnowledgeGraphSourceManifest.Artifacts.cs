using System.Text.Json;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial record KnowledgeGraphSourceManifest
{
    private const string ManifestJsonRequiredMessage = "Knowledge graph source manifest JSON content is required.";
    private const string ManifestJsonParseMessage = "Knowledge graph source manifest JSON did not contain a manifest.";

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public string SerializeJson()
    {
        return JsonSerializer.Serialize(this, ManifestJsonOptions);
    }

    public static KnowledgeGraphSourceManifest LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(ManifestJsonRequiredMessage, nameof(json));
        }

        return JsonSerializer.Deserialize<KnowledgeGraphSourceManifest>(json, ManifestJsonOptions) ??
               throw new InvalidOperationException(ManifestJsonParseMessage);
    }

    public Task SaveJsonToFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return File.WriteAllTextAsync(filePath, SerializeJson(), cancellationToken);
    }

    public static async Task<KnowledgeGraphSourceManifest> LoadJsonFromFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return LoadJson(json);
    }
}
