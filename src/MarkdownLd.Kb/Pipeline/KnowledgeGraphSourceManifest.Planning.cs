using System.Security.Cryptography;
using System.Text;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial record KnowledgeGraphSourceManifest
{
    private const string FingerprintSeparator = "\n";

    public static KnowledgeGraphSourceManifest Create(IEnumerable<KnowledgeSourceDocument> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return Create(sources.Select(static source => source.ToMarkdownSourceDocument()));
    }

    public static KnowledgeGraphSourceManifest Create(IEnumerable<MarkdownSourceDocument> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return new KnowledgeGraphSourceManifest(
            sources
                .Select(CreateSourceManifestEntry)
                .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
                .ToArray());
    }

    public static KnowledgeGraphSourceChangeSet CreateChangeSet(
        IEnumerable<KnowledgeSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return CreateChangeSet(
            sources.Select(static source => source.ToMarkdownSourceDocument()),
            previousManifest);
    }

    public static KnowledgeGraphSourceChangeSet CreateChangeSet(
        IEnumerable<MarkdownSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null)
    {
        var current = Create(sources);
        var previousByPath = CreateManifestLookup(previousManifest);
        var currentByPath = CreateManifestLookup(current);

        return new KnowledgeGraphSourceChangeSet(
            current,
            FindChangedPaths(previousByPath, current),
            FindUnchangedPaths(previousByPath, current),
            FindRemovedPaths(previousManifest, currentByPath));
    }

    private static KnowledgeGraphSourceManifestEntry CreateSourceManifestEntry(MarkdownSourceDocument source)
    {
        var path = KnowledgeNaming.NormalizeSourcePath(source.Path);
        return new KnowledgeGraphSourceManifestEntry(path, CreateSourceFingerprint(path, source));
    }

    private static string CreateSourceFingerprint(string normalizedPath, MarkdownSourceDocument source)
    {
        var canonical = source.CanonicalUri?.AbsoluteUri ?? string.Empty;
        var payload = normalizedPath +
                      FingerprintSeparator +
                      canonical +
                      FingerprintSeparator +
                      source.Content;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static IReadOnlyList<string> FindChangedPaths(
        IReadOnlyDictionary<string, string> previousByPath,
        KnowledgeGraphSourceManifest current)
    {
        return current.Entries
            .Where(entry => !previousByPath.TryGetValue(entry.Path, out var fingerprint) ||
                            !string.Equals(fingerprint, entry.Fingerprint, StringComparison.Ordinal))
            .Select(static entry => entry.Path)
            .ToArray();
    }

    private static IReadOnlyList<string> FindUnchangedPaths(
        IReadOnlyDictionary<string, string> previousByPath,
        KnowledgeGraphSourceManifest current)
    {
        return current.Entries
            .Where(entry => previousByPath.TryGetValue(entry.Path, out var fingerprint) &&
                            string.Equals(fingerprint, entry.Fingerprint, StringComparison.Ordinal))
            .Select(static entry => entry.Path)
            .ToArray();
    }

    private static IReadOnlyList<string> FindRemovedPaths(
        KnowledgeGraphSourceManifest? previous,
        IReadOnlyDictionary<string, string> currentByPath)
    {
        return previous?.Entries
                   .Where(entry => !currentByPath.ContainsKey(entry.Path))
                   .Select(static entry => entry.Path)
                   .OrderBy(static path => path, StringComparer.Ordinal)
                   .ToArray() ??
               [];
    }

    private static IReadOnlyDictionary<string, string> CreateManifestLookup(KnowledgeGraphSourceManifest? manifest)
    {
        return manifest?.Entries
                   .GroupBy(static entry => entry.Path, StringComparer.Ordinal)
                   .ToDictionary(
                       static group => group.Key,
                       static group => group.Last().Fingerprint,
                       StringComparer.Ordinal) ??
               new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
