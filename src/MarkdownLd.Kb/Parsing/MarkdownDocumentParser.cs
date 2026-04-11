using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb.Parsing;

public sealed partial class MarkdownDocumentParser
{
    public MarkdownDocument Parse(MarkdownDocumentSource source, MarkdownParsingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new MarkdownParsingOptions();

        var normalizedSource = NormalizeMarkdown(source.ContentMarkdown);
        var frontMatterResult = MarkdownFrontMatterParser.Parse(normalizedSource);
        var baseUri = CreateBaseUri(source.BaseUrl ?? options.DefaultBaseUrl);
        var documentId = ResolveDocumentId(frontMatterResult.FrontMatter, source.ContentPath, baseUri, normalizedSource);

        var sections = BuildSections(
            frontMatterResult.Body,
            documentId,
            baseUri,
            options.ChunkTokenTarget);

        var chunks = sections.SelectMany(static section => section.Chunks).ToArray();
        var links = sections.SelectMany(static section => section.Links)
            .DistinctBy(GetLinkKey)
            .ToArray();

        return new MarkdownDocument(
            documentId,
            source.ContentPath,
            baseUri,
            frontMatterResult.FrontMatter,
            normalizedSource,
            frontMatterResult.Body,
            sections,
            chunks,
            links);
    }

    public static string DocumentIdFromPath(string path, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return MarkdownTextConstants.DocumentIdUntitled;
        }

        var normalizedPath = path.Replace('\\', '/').Trim('/');
        if (normalizedPath.StartsWith(MarkdownTextConstants.ContentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath[MarkdownTextConstants.ContentPrefix.Length..];
        }

        var withoutExtension = Path.ChangeExtension(normalizedPath, null) ?? normalizedPath;
        withoutExtension = withoutExtension.Trim('/');

        return string.Concat(baseUrl.TrimEnd('/'), MarkdownTextConstants.PathSeparator, withoutExtension, MarkdownTextConstants.PathSeparator);
    }

    public static string ComputeChunkId(string markdown) =>
        ComputeHash(NormalizeWhitespace(markdown));

    public static string ComputeChunkId(string documentId, string markdown) =>
        ComputeHash(string.Concat(documentId, MarkdownTextConstants.LineFeed, NormalizeWhitespace(markdown)));

    public static string NormalizeWhitespace(string text) =>
        WhitespaceRegex().Replace(text, MarkdownTextConstants.Space).Trim();

    private static IReadOnlyList<MarkdownSection> BuildSections(
        string body,
        string documentId,
        Uri baseUri,
        int chunkTokenTarget)
    {
        var matches = HeadingRegex().Matches(body);
        if (matches.Count == 0)
        {
            return BuildLooseSection(body, documentId, baseUri, 0, chunkTokenTarget);
        }

        var sections = new List<MarkdownSection>();
        var headingStack = new List<HeadingFrame>();
        var order = 0;

        if (!string.IsNullOrWhiteSpace(body[..matches[0].Index]))
        {
            var looseSectionMarkdown = body[..matches[0].Index].Trim();
            if (!string.IsNullOrWhiteSpace(looseSectionMarkdown))
            {
                sections.Add(BuildSection(
                    looseSectionMarkdown,
                    string.Empty,
                    [],
                    0,
                    documentId,
                    baseUri,
                    order++,
                    chunkTokenTarget));
            }
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var current = matches[index];
            var nextIndex = index + 1 < matches.Count ? matches[index + 1].Index : body.Length;
            var sectionRaw = body[current.Index..nextIndex];
            var headingLevel = current.Groups[MarkdownTextConstants.GroupLevel].Value.Length;
            var headingText = current.Groups[MarkdownTextConstants.GroupHeading].Value.Trim();

            while (headingStack.Count >= headingLevel)
            {
                headingStack.RemoveAt(headingStack.Count - 1);
            }

            headingStack.Add(new HeadingFrame(headingLevel, headingText, current.Value.TrimEnd()));
            var sectionMarkdown = sectionRaw.Trim();
            var headingPath = headingStack.Select(static frame => frame.Text).ToArray();
            sections.Add(BuildSection(
                sectionMarkdown,
                headingText,
                headingPath,
                headingLevel,
                documentId,
                baseUri,
                order++,
                chunkTokenTarget));
        }

        return sections;
    }

    private static IReadOnlyList<MarkdownSection> BuildLooseSection(
        string body,
        string documentId,
        Uri baseUri,
        int order,
        int chunkTokenTarget)
    {
        var markdown = body.Trim();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        return [
            BuildSection(markdown, string.Empty, [], 0, documentId, baseUri, order, chunkTokenTarget),
        ];
    }

    private static MarkdownSection BuildSection(
        string sectionMarkdown,
        string headingText,
        IReadOnlyList<string> headingPath,
        int headingLevel,
        string documentId,
        Uri baseUri,
        int order,
        int chunkTokenTarget)
    {
        var headingMarkdown = headingLevel > 0 ? GetHeadingLine(sectionMarkdown) : null;
        var sectionBody = headingLevel > 0
            ? ExtractBodyAfterHeading(sectionMarkdown, headingMarkdown)
            : sectionMarkdown;

        var sectionId = ComputeChunkId(
            documentId,
            string.Concat(
                headingLevel.ToString(CultureInfo.InvariantCulture),
                MarkdownTextConstants.Colon,
                string.Join(MarkdownTextConstants.PathSeparator, headingPath),
                MarkdownTextConstants.LineFeed,
                sectionMarkdown));
        var chunks = BuildChunks(sectionId, documentId, headingPath, sectionBody, baseUri, chunkTokenTarget);
        var links = chunks.SelectMany(static chunk => chunk.Links)
            .DistinctBy(GetLinkKey)
            .ToArray();

        return new MarkdownSection(
            sectionId,
            order,
            headingLevel,
            headingMarkdown,
            headingLevel > 0 ? headingText : null,
            headingPath,
            sectionMarkdown,
            chunks,
            links);
    }

    private static IReadOnlyList<MarkdownChunk> BuildChunks(
        string sectionId,
        string documentId,
        IReadOnlyList<string> headingPath,
        string sectionBody,
        Uri baseUri,
        int chunkTokenTarget)
    {
        var blocks = SplitBlocks(sectionBody).ToArray();
        if (blocks.Length == 0)
        {
            return [];
        }

        var chunks = new List<MarkdownChunk>();
        var currentBlocks = new List<string>();
        var order = 0;
        var linkOrder = 0;

        foreach (var block in blocks)
        {
            var candidate = currentBlocks.Count == 0
                ? block
                : string.Concat(string.Join(MarkdownTextConstants.DoubleLineFeed, currentBlocks), MarkdownTextConstants.DoubleLineFeed, block);

            if (currentBlocks.Count > 0 && EstimateTokens(candidate) > chunkTokenTarget)
            {
                chunks.Add(CreateChunk(sectionId, documentId, headingPath, currentBlocks, baseUri, order++, ref linkOrder));
                currentBlocks.Clear();
            }

            currentBlocks.Add(block);
        }

        if (currentBlocks.Count > 0)
        {
            chunks.Add(CreateChunk(sectionId, documentId, headingPath, currentBlocks, baseUri, order++, ref linkOrder));
        }

        return chunks;
    }

    private static MarkdownChunk CreateChunk(
        string sectionId,
        string documentId,
        IReadOnlyList<string> headingPath,
        IReadOnlyList<string> blocks,
        Uri baseUri,
        int order,
        ref int linkOrder)
    {
        var markdown = string.Join(MarkdownTextConstants.DoubleLineFeed, blocks).Trim();
        var links = ExtractLinks(markdown, baseUri, ref linkOrder)
            .DistinctBy(GetLinkKey)
            .ToArray();

        return new MarkdownChunk(
            ComputeChunkId(documentId, markdown),
            sectionId,
            order,
            headingPath.ToArray(),
            markdown,
            EstimateTokens(markdown),
            links);
    }

    private static string ResolveDocumentId(MarkdownFrontMatter frontMatter, string? contentPath, Uri baseUri, string sourceMarkdown)
    {
        if (!string.IsNullOrWhiteSpace(frontMatter.CanonicalUrl))
        {
            var canonicalUrl = frontMatter.CanonicalUrl!.Trim();
            if (Uri.TryCreate(canonicalUrl, UriKind.Absolute, out var canonicalUri))
            {
                return canonicalUri.AbsoluteUri;
            }

            return canonicalUrl;
        }

        if (!string.IsNullOrWhiteSpace(contentPath))
        {
            return DocumentIdFromPath(contentPath, baseUri.AbsoluteUri);
        }

        return string.Concat(MarkdownTextConstants.DocumentIdPrefix, ComputeChunkId(sourceMarkdown)[..16]);
    }

    private static Uri CreateBaseUri(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        return new Uri(MarkdownTextConstants.DefaultBaseUrl, UriKind.Absolute);
    }

    private static string NormalizeMarkdown(string markdown)
    {
        var normalized = markdown.TrimStart('\uFEFF');
        normalized = normalized.Replace(MarkdownTextConstants.CarriageReturnLineFeed, MarkdownTextConstants.LineFeed, StringComparison.Ordinal);
        return normalized.Replace(MarkdownTextConstants.CarriageReturn, MarkdownTextConstants.LineFeed, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> SplitBlocks(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        return Regex.Split(markdown.Trim(), MarkdownTextConstants.SplitBlocksPattern)
            .Select(static block => block.Trim())
            .Where(static block => !string.IsNullOrWhiteSpace(block))
            .ToArray();
    }

    private static IReadOnlyList<MarkdownLinkReference> ExtractLinks(
        string markdown,
        Uri baseUri,
        ref int linkOrder)
    {
        var links = new List<MarkdownLinkReference>();

        foreach (Match match in WikiLinkRegex().Matches(markdown))
        {
            var rawTarget = match.Groups[MarkdownTextConstants.GroupTarget].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawTarget))
            {
                continue;
            }

            var parts = rawTarget.Split('|', 2, StringSplitOptions.TrimEntries);
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

        foreach (Match match in MarkdownImageLinkRegex().Matches(markdown))
        {
            var target = match.Groups[MarkdownTextConstants.GroupTarget].Value.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            var title = GetLinkTitle(match);
            links.Add(CreateMarkdownLink(target, match.Groups[MarkdownTextConstants.GroupLabel].Value.Trim(), title, baseUri, ref linkOrder, true));
        }

        foreach (Match match in MarkdownLinkRegex().Matches(markdown))
        {
            var target = match.Groups[MarkdownTextConstants.GroupTarget].Value.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            var title = GetLinkTitle(match);
            links.Add(CreateMarkdownLink(target, match.Groups[MarkdownTextConstants.GroupLabel].Value.Trim(), title, baseUri, ref linkOrder, false));
        }

        return links;
    }

    private static MarkdownLinkReference CreateMarkdownLink(
        string target,
        string label,
        string? title,
        Uri baseUri,
        ref int linkOrder,
        bool isImage)
    {
        var isExternal = IsExternalTarget(target);
        var resolvedTarget = isExternal
            ? target
            : ResolveDocumentTarget(target, baseUri);

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

    private static string ResolveDocumentTarget(string target, Uri baseUri)
    {
        if (Uri.TryCreate(target, UriKind.RelativeOrAbsolute, out var uri) && uri.IsAbsoluteUri)
        {
            return uri.AbsoluteUri;
        }

        if (!Uri.TryCreate(baseUri, target, out var resolvedUri))
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
            path = Path.ChangeExtension(path, null) ?? path;
            if (!path.EndsWith(MarkdownTextConstants.PathSeparator, StringComparison.Ordinal))
            {
                path = string.Concat(path, MarkdownTextConstants.PathSeparator);
            }
        }

        uriBuilder.Path = path;
        return uriBuilder.Uri.AbsoluteUri;
    }

    private static string? GetLinkTitle(Match match)
    {
        var titleGroup = match.Groups[MarkdownTextConstants.GroupTitle];
        if (titleGroup.Success)
        {
            return titleGroup.Value.Trim();
        }

        return null;
    }

    private static string ExtractBodyAfterHeading(string sectionMarkdown, string? headingLine)
    {
        if (string.IsNullOrWhiteSpace(sectionMarkdown) || string.IsNullOrWhiteSpace(headingLine))
        {
            return sectionMarkdown.Trim();
        }

        var headingIndex = sectionMarkdown.IndexOf(headingLine, StringComparison.Ordinal);
        if (headingIndex < 0)
        {
            return sectionMarkdown.Trim();
        }

        var body = sectionMarkdown[(headingIndex + headingLine.Length)..];
        return body.TrimStart('\n').Trim();
    }

    private static string GetHeadingLine(string sectionMarkdown)
    {
        var newlineIndex = sectionMarkdown.IndexOf('\n');
        return newlineIndex < 0 ? sectionMarkdown.Trim() : sectionMarkdown[..newlineIndex].TrimEnd();
    }

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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

    [GeneratedRegex(MarkdownTextConstants.HeadingPattern, RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(MarkdownTextConstants.WikiLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.MarkdownImageLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownImageLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.MarkdownLinkPattern, RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(MarkdownTextConstants.WhitespacePattern, RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record HeadingFrame(int Level, string Text, string Markdown);
}
