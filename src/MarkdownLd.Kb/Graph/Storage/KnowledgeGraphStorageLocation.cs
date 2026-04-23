using System.IO.Enumeration;
using static ManagedCode.MarkdownLd.Kb.Pipeline.KnowledgeGraphStoreConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal readonly record struct KnowledgeGraphFileSystemBinding(string BaseFolder, string StorageLocation);

internal static class KnowledgeGraphStorageLocation
{
    public static KnowledgeGraphFileSystemBinding BindFileSystemPath(string location)
    {
        FileSystemKnowledgeGraphStore.EnsureLocation(location);
        return !Path.IsPathRooted(location)
            ? new KnowledgeGraphFileSystemBinding(Directory.GetCurrentDirectory(), Normalize(location))
            : CreateAbsoluteBinding(location);
    }

    public static string Normalize(string location)
    {
        return location
            .Replace(Path.DirectorySeparatorChar.ToString(), PathSeparator, StringComparison.Ordinal)
            .Replace(Path.AltDirectorySeparatorChar.ToString(), PathSeparator, StringComparison.Ordinal)
            .Trim();
    }

    public static bool TryGetDirectoryRelativePath(
        string directoryLocation,
        string candidateLocation,
        SearchOption searchOption,
        out string relativePath)
    {
        var normalizedDirectory = Normalize(directoryLocation).TrimEnd(PathSeparator[0]);
        var normalizedCandidate = Normalize(candidateLocation);
        var prefix = string.IsNullOrEmpty(normalizedDirectory)
            ? string.Empty
            : normalizedDirectory + PathSeparator;

        var isPrefixMatch = string.IsNullOrEmpty(prefix) || normalizedCandidate.StartsWith(prefix, StringComparison.Ordinal);
        relativePath = !isPrefixMatch
            ? string.Empty
            : string.IsNullOrEmpty(prefix)
            ? normalizedCandidate
            : normalizedCandidate[prefix.Length..];
        return !string.IsNullOrEmpty(relativePath)
            && (searchOption == SearchOption.AllDirectories || relativePath.IndexOf(PathSeparator, StringComparison.Ordinal) < 0);
    }

    public static bool MatchesPattern(string relativePath, string searchPattern)
    {
        var fileName = Path.GetFileName(relativePath);
        return FileSystemName.MatchesSimpleExpression(searchPattern, fileName, ignoreCase: false);
    }

    private static KnowledgeGraphFileSystemBinding CreateAbsoluteBinding(string location)
    {
        var root = Path.GetPathRoot(location) ?? throw new InvalidOperationException(GraphLocationNotFoundMessagePrefix + location);
        return new KnowledgeGraphFileSystemBinding(root, Normalize(Path.GetRelativePath(root, location)));
    }
}
