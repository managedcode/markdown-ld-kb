using System.Security.Cryptography;
using System.Text;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class MarkdownKnowledgePipeline
{
    private const string ManifestFingerprintSeparator = "\n";

    public Task<MarkdownKnowledgeIncrementalBuildResult> BuildIncrementalAsync(
        IEnumerable<KnowledgeSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null,
        KnowledgeGraph? previousGraph = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return BuildIncrementalAsync(
            sources.Select(static source => source.ToMarkdownSourceDocument()),
            previousManifest,
            previousGraph,
            cancellationToken);
    }

    public async Task<MarkdownKnowledgeIncrementalBuildResult> BuildIncrementalAsync(
        IEnumerable<MarkdownSourceDocument> sources,
        KnowledgeGraphSourceManifest? previousManifest = null,
        KnowledgeGraph? previousGraph = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceList = sources.ToArray();
        var currentManifest = CreateSourceManifest(sourceList);
        var changedPaths = FindChangedPaths(previousManifest, currentManifest);
        var removedPaths = FindRemovedPaths(previousManifest, currentManifest);
        var result = await BuildAsync(sourceList, cancellationToken).ConfigureAwait(false);
        var diff = previousGraph is null
            ? KnowledgeGraphDiff.Empty
            : previousGraph.Diff(result.Graph);

        return new MarkdownKnowledgeIncrementalBuildResult(
            result,
            currentManifest,
            changedPaths,
            removedPaths,
            diff);
    }

    private static KnowledgeGraphSourceManifest CreateSourceManifest(IEnumerable<MarkdownSourceDocument> sources)
    {
        return new KnowledgeGraphSourceManifest(
            sources
                .Select(CreateSourceManifestEntry)
                .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
                .ToArray());
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
                      ManifestFingerprintSeparator +
                      canonical +
                      ManifestFingerprintSeparator +
                      source.Content;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static IReadOnlyList<string> FindChangedPaths(
        KnowledgeGraphSourceManifest? previous,
        KnowledgeGraphSourceManifest current)
    {
        var previousByPath = CreateManifestLookup(previous);
        return current.Entries
            .Where(entry => !previousByPath.TryGetValue(entry.Path, out var fingerprint) ||
                            !string.Equals(fingerprint, entry.Fingerprint, StringComparison.Ordinal))
            .Select(static entry => entry.Path)
            .ToArray();
    }

    private static IReadOnlyList<string> FindRemovedPaths(
        KnowledgeGraphSourceManifest? previous,
        KnowledgeGraphSourceManifest current)
    {
        var currentPaths = current.Entries.Select(static entry => entry.Path).ToHashSet(StringComparer.Ordinal);
        return previous?.Entries
                   .Where(entry => !currentPaths.Contains(entry.Path))
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
