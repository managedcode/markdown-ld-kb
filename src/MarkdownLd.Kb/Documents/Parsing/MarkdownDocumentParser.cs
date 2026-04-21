using System.Globalization;

namespace ManagedCode.MarkdownLd.Kb.Parsing;

public sealed partial class MarkdownDocumentParser(IMarkdownChunker? chunker = null)
{
    private readonly IMarkdownChunker _chunker = chunker ?? DeterministicSectionMarkdownChunker.Default;

    public MarkdownDocument Parse(MarkdownDocumentSource source, MarkdownParsingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= new MarkdownParsingOptions();

        var normalizedSource = NormalizeMarkdown(source.ContentMarkdown);
        var frontMatterResult = MarkdownFrontMatterParser.Parse(normalizedSource);
        var baseUri = CreateBaseUri(source.BaseUrl ?? options.DefaultBaseUrl);
        var documentId = ResolveDocumentId(frontMatterResult.FrontMatter, source, baseUri, normalizedSource);
        var chunkingDocument = BuildChunkingDocument(frontMatterResult.Body, documentId, source.ContentPath, baseUri);
        var sections = _chunker.Chunk(chunkingDocument, options.Chunking);
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

        var withoutExtension = Path.ChangeExtension(normalizedPath, null);
        withoutExtension = withoutExtension.Trim('/');

        return string.Concat(baseUrl.TrimEnd('/'), MarkdownTextConstants.PathSeparator, withoutExtension, MarkdownTextConstants.PathSeparator);
    }

    public static string ComputeChunkId(string markdown) =>
        ComputeHash(NormalizeWhitespace(markdown));

    public static string ComputeChunkId(string documentId, string markdown) =>
        ComputeHash(string.Concat(documentId, MarkdownTextConstants.LineFeed, NormalizeWhitespace(markdown)));

    public static string NormalizeWhitespace(string text) =>
        WhitespaceRegex().Replace(text, MarkdownTextConstants.Space).Trim();

    private static MarkdownChunkingDocument BuildChunkingDocument(
        string body,
        string documentId,
        string? contentPath,
        Uri baseUri)
    {
        return new MarkdownChunkingDocument(
            documentId,
            contentPath,
            baseUri,
            body,
            BuildSectionOutlines(body, documentId));
    }

    private static IReadOnlyList<MarkdownChunkingSection> BuildSectionOutlines(string body, string documentId)
    {
        var matches = HeadingRegex().Matches(body);
        if (matches.Count == 0)
        {
            return BuildLooseSection(body, documentId, 0);
        }

        var sections = new List<MarkdownChunkingSection>();
        var headingStack = new List<HeadingFrame>();
        var order = 0;

        if (!string.IsNullOrWhiteSpace(body[..matches[0].Index]))
        {
            var looseSectionMarkdown = body[..matches[0].Index].Trim();
            if (!string.IsNullOrWhiteSpace(looseSectionMarkdown))
            {
                sections.Add(BuildSectionOutline(looseSectionMarkdown, string.Empty, [], 0, documentId, order++));
            }
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var current = matches[index];
            var nextIndex = index + 1 < matches.Count ? matches[index + 1].Index : body.Length;
            var sectionMarkdown = body[current.Index..nextIndex].Trim();
            var headingLevel = current.Groups[MarkdownTextConstants.GroupLevel].Value.Length;
            var headingText = current.Groups[MarkdownTextConstants.GroupHeading].Value.Trim();

            while (headingStack.Count >= headingLevel)
            {
                headingStack.RemoveAt(headingStack.Count - 1);
            }

            headingStack.Add(new HeadingFrame(headingText));
            var headingPath = headingStack.Select(static frame => frame.Text).ToArray();
            sections.Add(BuildSectionOutline(sectionMarkdown, headingText, headingPath, headingLevel, documentId, order++));
        }

        return sections;
    }

    private static IReadOnlyList<MarkdownChunkingSection> BuildLooseSection(string body, string documentId, int order)
    {
        var markdown = body.Trim();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        return [BuildSectionOutline(markdown, string.Empty, [], 0, documentId, order)];
    }

    private static MarkdownChunkingSection BuildSectionOutline(
        string sectionMarkdown,
        string headingText,
        IReadOnlyList<string> headingPath,
        int headingLevel,
        string documentId,
        int order)
    {
        var sectionId = ComputeChunkId(
            documentId,
            string.Concat(
                headingLevel.ToString(CultureInfo.InvariantCulture),
                MarkdownTextConstants.Colon,
                string.Join(MarkdownTextConstants.PathSeparator, headingPath),
                MarkdownTextConstants.LineFeed,
                sectionMarkdown));

        return new MarkdownChunkingSection(
            sectionId,
            order,
            headingLevel,
            headingLevel > 0 ? GetHeadingLine(sectionMarkdown) : null,
            headingLevel > 0 ? headingText : null,
            headingPath.ToArray(),
            sectionMarkdown);
    }

    private static string ResolveDocumentId(
        MarkdownFrontMatter frontMatter,
        MarkdownDocumentSource source,
        Uri baseUri,
        string sourceMarkdown)
    {
        if (!string.IsNullOrWhiteSpace(source.DocumentIdOverride))
        {
            return source.DocumentIdOverride.Trim();
        }

        if (!string.IsNullOrWhiteSpace(frontMatter.CanonicalUrl))
        {
            var canonicalUrl = frontMatter.CanonicalUrl.Trim();
            if (Uri.TryCreate(canonicalUrl, UriKind.Absolute, out var canonicalUri))
            {
                return canonicalUri.AbsoluteUri;
            }

            return canonicalUrl;
        }

        if (!string.IsNullOrWhiteSpace(source.ContentPath))
        {
            return DocumentIdFromPath(source.ContentPath, baseUri.AbsoluteUri);
        }

        return string.Concat(MarkdownTextConstants.DocumentIdPrefix, ComputeChunkId(sourceMarkdown)[..16]);
    }

    private static Uri CreateBaseUri(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        throw new ArgumentException(MarkdownTextConstants.InvalidBaseUrlMessage, nameof(baseUrl));
    }

    private static string NormalizeMarkdown(string markdown)
    {
        var normalized = markdown.TrimStart('\uFEFF');
        normalized = normalized.Replace(MarkdownTextConstants.CarriageReturnLineFeed, MarkdownTextConstants.LineFeed, StringComparison.Ordinal);
        return normalized.Replace(MarkdownTextConstants.CarriageReturn, MarkdownTextConstants.LineFeed, StringComparison.Ordinal);
    }

    private static string GetHeadingLine(string sectionMarkdown)
    {
        var newlineIndex = sectionMarkdown.IndexOf('\n');
        return newlineIndex < 0 ? sectionMarkdown.Trim() : sectionMarkdown[..newlineIndex].TrimEnd();
    }
}
