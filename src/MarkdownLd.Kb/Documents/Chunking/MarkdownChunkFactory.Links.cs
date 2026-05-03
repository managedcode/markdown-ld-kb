using System.Text.RegularExpressions;
using ManagedCode.MarkdownLd.Kb.Parsing;

namespace ManagedCode.MarkdownLd.Kb;

internal static partial class MarkdownChunkFactory
{
    private const char HeadingPipeSeparator = '|';
    private const char QueryDelimiter = '?';
    private const char FragmentDelimiter = '#';
    private const string CurrentDirectorySegment = ".";
    private const string ParentDirectorySegment = "..";

    private static IReadOnlyList<MarkdownLinkReference> ExtractLinks(
        string markdown,
        Uri baseUri,
        string? contentPath,
        ref int linkOrder)
    {
        var links = new List<MarkdownLinkReference>();

        AddWikiLinks(links, markdown, ref linkOrder);
        AddMarkdownLinks(links, markdown, baseUri, contentPath, MarkdownImageLinkRegex(), true, ref linkOrder);
        AddMarkdownLinks(links, markdown, baseUri, contentPath, MarkdownLinkRegex(), false, ref linkOrder);

        return links;
    }

    private static void AddWikiLinks(
        ICollection<MarkdownLinkReference> links,
        string markdown,
        ref int linkOrder)
    {
        foreach (Match match in WikiLinkRegex().Matches(markdown))
        {
            var rawTarget = match.Groups[MarkdownTextConstants.GroupTarget].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawTarget))
            {
                continue;
            }

            var parts = rawTarget.Split(HeadingPipeSeparator, 2, StringSplitOptions.TrimEntries);
            var target = parts[0];
            var displayText = parts.Length == 2 ? parts[1] : parts[0];
            links.Add(new MarkdownLinkReference(
                MarkdownLinkKind.WikiLink,
                target,
                displayText,
                target,
                null,
                false,
                false,
                false,
                null,
                linkOrder++));
        }
    }

    private static void AddMarkdownLinks(
        ICollection<MarkdownLinkReference> links,
        string markdown,
        Uri baseUri,
        string? contentPath,
        Regex linkRegex,
        bool isImage,
        ref int linkOrder)
    {
        foreach (Match match in linkRegex.Matches(markdown))
        {
            var target = match.Groups[MarkdownTextConstants.GroupTarget].Value.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            links.Add(CreateMarkdownLink(
                target,
                match.Groups[MarkdownTextConstants.GroupLabel].Value.Trim(),
                GetLinkTitle(match),
                baseUri,
                contentPath,
                ref linkOrder,
                isImage));
        }
    }

    private static MarkdownLinkReference CreateMarkdownLink(
        string target,
        string label,
        string? title,
        Uri baseUri,
        string? contentPath,
        ref int linkOrder,
        bool isImage)
    {
        var isExternal = IsExternalTarget(target);
        var resolvedTarget = isExternal
            ? target
            : ResolveDocumentTarget(target, baseUri, contentPath);

        return new MarkdownLinkReference(
            MarkdownLinkKind.MarkdownLink,
            target,
            label,
            target,
            title,
            isExternal,
            isImage,
            !isExternal,
            resolvedTarget,
            linkOrder++);
    }

    private static bool IsExternalTarget(string target) =>
        Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
        (uri.Scheme.Equals(MarkdownTextConstants.HttpScheme, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(MarkdownTextConstants.HttpsScheme, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(MarkdownTextConstants.MailtoScheme, StringComparison.OrdinalIgnoreCase));

    private static string ResolveDocumentTarget(string target, Uri baseUri, string? contentPath)
    {
        if (Uri.TryCreate(target, UriKind.RelativeOrAbsolute, out var uri) && uri.IsAbsoluteUri)
        {
            return uri.AbsoluteUri;
        }

        var sourceRelativeTarget = ResolveSourceRelativeTarget(target, contentPath);
        if (!Uri.TryCreate(baseUri, sourceRelativeTarget, out var resolvedUri))
        {
            return target;
        }

        var uriBuilder = new UriBuilder(resolvedUri)
        {
            Fragment = resolvedUri.Fragment,
            Query = resolvedUri.Query,
        };

        var path = uriBuilder.Uri.AbsolutePath;
        if (path.EndsWith(MarkdownTextConstants.MarkdownExtension, StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(MarkdownTextConstants.MarkdownExtensionLong, StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(MarkdownTextConstants.MarkdownExtensionAlternate, StringComparison.OrdinalIgnoreCase))
        {
            path = Path.ChangeExtension(path, null);
            if (!path.EndsWith(MarkdownTextConstants.PathSeparator, StringComparison.Ordinal))
            {
                path = string.Concat(path, MarkdownTextConstants.PathSeparator);
            }
        }

        uriBuilder.Path = path;
        return uriBuilder.Uri.AbsoluteUri;
    }

    private static string ResolveSourceRelativeTarget(string target, string? contentPath)
    {
        if (string.IsNullOrWhiteSpace(contentPath) ||
            target.StartsWith(MarkdownTextConstants.PathSeparator, StringComparison.Ordinal))
        {
            return target;
        }

        var suffixIndex = target.IndexOfAny([QueryDelimiter, FragmentDelimiter]);
        var targetPath = suffixIndex < 0 ? target : target[..suffixIndex];
        var suffix = suffixIndex < 0 ? string.Empty : target[suffixIndex..];
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return string.Concat(NormalizeRelativePath(contentPath), suffix);
        }

        var sourceDirectory = GetSourceDirectory(contentPath);
        var combined = string.IsNullOrWhiteSpace(sourceDirectory)
            ? targetPath
            : string.Concat(sourceDirectory, MarkdownTextConstants.PathSeparator, targetPath);
        return string.Concat(NormalizeRelativePath(combined), suffix);
    }

    private static string GetSourceDirectory(string contentPath)
    {
        var normalized = contentPath.Replace('\\', '/').Trim('/');
        var lastSlash = normalized.LastIndexOf(MarkdownTextConstants.PathSeparator, StringComparison.Ordinal);
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }

    private static string NormalizeRelativePath(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split(MarkdownTextConstants.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(segment) ||
                string.Equals(segment, CurrentDirectorySegment, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, ParentDirectorySegment, StringComparison.Ordinal) && segments.Count > 0)
            {
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        var normalized = string.Join(MarkdownTextConstants.PathSeparator, segments);
        return normalized.StartsWith(MarkdownTextConstants.ContentPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[MarkdownTextConstants.ContentPrefix.Length..]
            : normalized;
    }

    private static string? GetLinkTitle(Match match)
    {
        var titleGroup = match.Groups[MarkdownTextConstants.GroupTitle];
        return titleGroup.Success ? titleGroup.Value.Trim() : null;
    }

    private static object GetLinkKey(MarkdownLinkReference link) =>
        (
            link.Kind,
            link.Target,
            link.DisplayText,
            link.Destination,
            link.Title,
            link.IsExternal,
            link.IsImage,
            link.IsDocumentLink,
            link.ResolvedTarget
        );

    [GeneratedRegex(MarkdownTextConstants.WikiLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.MarkdownImageLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownImageLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.MarkdownLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();
}
