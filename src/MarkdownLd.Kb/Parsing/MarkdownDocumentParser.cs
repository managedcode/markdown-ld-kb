using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using YamlDotNet.RepresentationModel;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownDocumentParser
{
    private static readonly MarkdownPipeline LinkPipeline = new MarkdownPipelineBuilder()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UsePipeTables()
        .Build();

    public MarkdownDocument Parse(MarkdownDocumentSource source, MarkdownParsingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        options ??= new MarkdownParsingOptions();

        var (rawFrontMatter, bodyMarkdown, frontMatterValues) = ParseFrontMatter(source.ContentMarkdown);
        var frontMatter = BuildFrontMatter(rawFrontMatter, frontMatterValues);
        var documentId = BuildDocumentId(source.ContentPath, source.BaseUrl, bodyMarkdown, options);
        var baseUri = BuildBaseUri(source.ContentPath, source.BaseUrl, options);

        var sections = BuildSections(
            documentId,
            bodyMarkdown,
            baseUri,
            options.ChunkTokenTarget);

        var chunks = sections.SelectMany(section => section.Chunks).ToArray();
        var links = sections.SelectMany(section => section.Links).ToArray();

        return new MarkdownDocument(
            DocumentId: documentId,
            ContentPath: source.ContentPath,
            BaseUri: baseUri,
            FrontMatter: frontMatter,
            SourceMarkdown: source.ContentMarkdown,
            BodyMarkdown: bodyMarkdown,
            Sections: sections,
            Chunks: chunks,
            Links: links);
    }

    private static MarkdownFrontMatter BuildFrontMatter(
        string rawYaml,
        IReadOnlyDictionary<string, object?> values)
    {
        var title = GetString(values, "title");
        var summary = GetString(values, "summary", "description");
        var about = GetString(values, "about");
        var datePublished = GetString(values, "date_published", "datePublished");
        var dateModified = GetString(values, "date_modified", "dateModified");
        var authors = GetStringList(values, "authors", "author");
        var tags = GetStringList(values, "tags");
        var entityHints = GetEntityHints(values);

        return new MarkdownFrontMatter
        {
            RawYaml = rawYaml,
            Values = values,
            Title = title,
            Summary = summary,
            About = about,
            DatePublished = datePublished,
            DateModified = dateModified,
            Authors = authors,
            Tags = tags,
            EntityHints = entityHints,
        };
    }

    private static IReadOnlyList<MarkdownSection> BuildSections(
        string documentId,
        string bodyMarkdown,
        Uri? baseUri,
        int tokenTarget)
    {
        if (string.IsNullOrWhiteSpace(bodyMarkdown))
        {
            return Array.Empty<MarkdownSection>();
        }

        var headings = DiscoverHeadings(bodyMarkdown);
        var sections = new List<MarkdownSection>();

        if (headings.Count == 0)
        {
            var content = bodyMarkdown.Trim();
            if (content.Length == 0)
            {
                return Array.Empty<MarkdownSection>();
            }

            var sectionId = ComputeSectionId(documentId, Array.Empty<string>(), content);
            var links = DiscoverLinks(content, baseUri, null);
            var chunks = BuildChunks(documentId, sectionId, Array.Empty<string>(), content, baseUri, Array.Empty<MarkdownLinkReference>(), tokenTarget);
            sections.Add(
                new MarkdownSection(
                    SectionId: sectionId,
                    Order: 0,
                    HeadingLevel: 0,
                    HeadingMarkdown: null,
                    HeadingText: null,
                    HeadingPath: Array.Empty<string>(),
                    Markdown: content,
                    Chunks: chunks,
                    Links: links));
            return sections;
        }

        var stack = new List<string>();
        for (var index = 0; index < headings.Count; index++)
        {
            var heading = headings[index];

            while (stack.Count >= heading.Level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            stack.Add(heading.Text);
            var headingPath = stack.ToArray();
            var nextStart = index + 1 < headings.Count ? headings[index + 1].StartIndex : bodyMarkdown.Length;
            var sectionMarkdown = bodyMarkdown.Substring(
                Math.Min(bodyMarkdown.Length, heading.EndIndex + 1),
                Math.Max(0, nextStart - (heading.EndIndex + 1))).Trim();

            var headingLinks = DiscoverLinks(heading.Markdown, baseUri, null);
            var contentLinks = DiscoverLinks(sectionMarkdown, baseUri, null, headingLinks.Count);
            var links = headingLinks.Concat(contentLinks).ToArray();
            var sectionId = ComputeSectionId(documentId, headingPath, sectionMarkdown);
            var chunks = BuildChunks(documentId, sectionId, headingPath, sectionMarkdown, baseUri, headingLinks, tokenTarget);

            sections.Add(
                new MarkdownSection(
                    SectionId: sectionId,
                    Order: index,
                    HeadingLevel: heading.Level,
                    HeadingMarkdown: heading.Markdown,
                    HeadingText: heading.Text,
                    HeadingPath: headingPath,
                    Markdown: sectionMarkdown,
                    Chunks: chunks,
                    Links: links));
        }

        return sections;
    }

    private static IReadOnlyList<MarkdownChunk> BuildChunks(
        string documentId,
        string sectionId,
        IReadOnlyList<string> headingPath,
        string markdown,
        Uri? baseUri,
        IReadOnlyList<MarkdownLinkReference> headingLinks,
        int tokenTarget)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Array.Empty<MarkdownChunk>();
        }

        var paragraphBlocks = SplitParagraphBlocks(markdown);
        var chunks = new List<MarkdownChunk>();
        var buffer = new StringBuilder();
        var chunkOrder = 0;

        void Flush()
        {
            var content = buffer.ToString().Trim();
            buffer.Clear();

            if (content.Length == 0)
            {
                return;
            }

            var chunkLinks = headingLinks
                .Concat(DiscoverLinks(content, baseUri, null, headingLinks.Count))
                .ToArray();
            var estimatedTokens = EstimateTokens(content);
            var chunkId = ComputeChunkId(documentId, content);

            chunks.Add(
                new MarkdownChunk(
                    ChunkId: chunkId,
                    SectionId: sectionId,
                    Order: chunkOrder++,
                    HeadingPath: headingPath.ToArray(),
                    Markdown: content,
                    EstimatedTokenCount: estimatedTokens,
                    Links: chunkLinks));
        }

        foreach (var paragraph in paragraphBlocks)
        {
            if (buffer.Length == 0)
            {
                buffer.Append(paragraph);
                continue;
            }

            var candidate = $"{buffer}{Environment.NewLine}{Environment.NewLine}{paragraph}";
            if (EstimateTokens(candidate) <= tokenTarget)
            {
                buffer.Clear();
                buffer.Append(candidate);
                continue;
            }

            Flush();
            buffer.Append(paragraph);
        }

        Flush();
        return chunks;
    }

    private static IReadOnlyList<string> SplitParagraphBlocks(string markdown)
    {
        var parts = Regex.Split(markdown.Trim(), @"(?:\r?\n){2,}")
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();

        return parts.Length == 0 ? Array.Empty<string>() : parts;
    }

    private static string BuildDocumentId(
        string? contentPath,
        string? baseUrl,
        string bodyMarkdown,
        MarkdownParsingOptions options)
    {
        if (string.IsNullOrWhiteSpace(contentPath))
        {
            var hash = ComputeSha256Hex(NormalizeWhitespace(bodyMarkdown));
            return $"urn:markdown-ld-kb:{hash}";
        }

        if (Uri.TryCreate(contentPath, UriKind.Absolute, out var absoluteContentPath))
        {
            var normalizedAbsolute = NormalizeDocumentUri(absoluteContentPath);
            return normalizedAbsolute.AbsoluteUri;
        }

        var root = BuildBaseUri(contentPath, baseUrl, options) ?? new Uri(options.DefaultBaseUrl, UriKind.Absolute);
        var relativePath = NormalizeContentPath(contentPath);
        return new Uri(root, relativePath).AbsoluteUri;
    }

    private static Uri? BuildBaseUri(
        string? contentPath,
        string? baseUrl,
        MarkdownParsingOptions options)
    {
        var candidate = baseUrl ?? options.DefaultBaseUrl;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        if (!candidate.EndsWith('/'))
        {
            candidate += "/";
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out var baseUri) ? baseUri : null;
    }

    private static string NormalizeContentPath(string contentPath)
    {
        var normalized = contentPath.Replace('\\', '/').Trim();
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized.StartsWith("content/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["content/".Length..];
        }

        normalized = Path.ChangeExtension(normalized, null) ?? normalized;
        normalized = normalized.Trim('/');
        return normalized.Length == 0 ? string.Empty : $"{normalized}/";
    }

    private static Uri NormalizeDocumentUri(Uri uri)
    {
        var builder = new UriBuilder(uri);
        builder.Path = Path.ChangeExtension(builder.Path, null)?.TrimEnd('/') + "/";
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }

    private static string ComputeSectionId(string documentId, IReadOnlyList<string> headingPath, string markdown)
    {
        var path = string.Join("\u001F", headingPath);
        return ComputeSha256Hex($"{documentId}\n{path}\n{NormalizeWhitespace(markdown)}");
    }

    private static string ComputeChunkId(string documentId, string markdown)
        => ComputeSha256Hex($"{documentId}\n{NormalizeWhitespace(markdown)}");

    private static int EstimateTokens(string text)
        => Math.Max(1, text.Length / 4);

    private static string NormalizeWhitespace(string text)
        => Regex.Replace(text, @"\s+", " ").Trim();

    private static string ComputeSha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IReadOnlyList<HeadingSlice> DiscoverHeadings(string bodyMarkdown)
    {
        var document = Markdown.Parse(bodyMarkdown, LinkPipeline);
        var headings = new List<HeadingSlice>();

        foreach (var block in document)
        {
            if (block is not HeadingBlock headingBlock)
            {
                continue;
            }

            var text = RenderInlineText(headingBlock.Inline).Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var start = headingBlock.Span.Start;
            var end = headingBlock.Span.End;
            headings.Add(
                new HeadingSlice(
                    Level: headingBlock.Level,
                    Text: text,
                    Markdown: bodyMarkdown.Substring(start, end - start + 1),
                    StartIndex: start,
                    EndIndex: end));
        }

        return headings;
    }

    private static IReadOnlyList<MarkdownLinkReference> DiscoverLinks(
        string markdown,
        Uri? baseUri,
        int orderOffset = 0)
    {
        var links = new List<MarkdownLinkReference>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return links;
        }

        var document = Markdown.Parse(markdown, LinkPipeline);
        var order = orderOffset;

        foreach (var block in document)
        {
            CollectLinks(block, baseUri, links, ref order);
        }

        foreach (var wikiLink in DiscoverWikiLinks(markdown))
        {
            links.Add(wikiLink with { Order = order++ });
        }

        return links;
    }

    private static void CollectLinks(
        Block block,
        Uri? baseUri,
        List<MarkdownLinkReference> links,
        ref int order)
    {
        if (block is ContainerBlock containerBlock)
        {
            foreach (var child in containerBlock)
            {
                if (child is Block childBlock)
                {
                    CollectLinks(childBlock, baseUri, links, ref order);
                }
            }
        }

        if (block is LeafBlock leafBlock && leafBlock.Inline is not null)
        {
            CollectLinks(leafBlock.Inline, baseUri, links, ref order);
        }
    }

    private static void CollectLinks(
        ContainerInline inline,
        Uri? baseUri,
        List<MarkdownLinkReference> links,
        ref int order)
    {
        for (var child = inline.FirstChild; child is not null; child = child.NextSibling)
        {
            switch (child)
            {
                case LinkInline linkInline:
                {
                    var text = RenderInlineText(linkInline).Trim();
                    var destination = linkInline.Url;
                    var isExternal = IsExternalDestination(destination);
                    var isDocumentLink = IsDocumentLink(destination);
                    var resolvedTarget = ResolveLinkTarget(destination, baseUri);

                    links.Add(
                        new MarkdownLinkReference(
                            Kind: MarkdownLinkKind.MarkdownLink,
                            Target: destination,
                            DisplayText: string.IsNullOrWhiteSpace(text) ? destination : text,
                            Destination: destination,
                            Title: string.IsNullOrWhiteSpace(linkInline.Title) ? null : linkInline.Title,
                            IsExternal: isExternal,
                            IsImage: linkInline.IsImage,
                            IsDocumentLink: isDocumentLink,
                            ResolvedTarget: resolvedTarget,
                            Order: order++));
                    break;
                }
                case ContainerInline nested:
                    CollectLinks(nested, baseUri, links, ref order);
                    break;
            }
        }
    }

    private static IReadOnlyList<MarkdownLinkReference> DiscoverWikiLinks(string markdown)
    {
        var matches = Regex.Matches(markdown, @"(?<!\\)\[\[([^\]\r\n]+?)\]\]");
        if (matches.Count == 0)
        {
            return Array.Empty<MarkdownLinkReference>();
        }

        var links = new List<MarkdownLinkReference>(matches.Count);
        var order = 0;

        foreach (Match match in matches)
        {
            var raw = match.Groups[1].Value;
            var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
            var target = parts[0].Trim();
            var label = parts.Length > 1 ? parts[1].Trim() : target;

            links.Add(
                new MarkdownLinkReference(
                    Kind: MarkdownLinkKind.WikiLink,
                    Target: target,
                    DisplayText: string.IsNullOrWhiteSpace(label) ? target : label,
                    Destination: target,
                    Title: null,
                    IsExternal: false,
                    IsImage: false,
                    IsDocumentLink: false,
                    ResolvedTarget: null,
                    Order: order++));
        }

        return links;
    }

    private static bool IsExternalDestination(string destination)
        => Uri.TryCreate(destination, UriKind.Absolute, out var uri)
           && (uri.Scheme is "http" or "https" or "mailto");

    private static bool IsDocumentLink(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            return false;
        }

        var path = destination.Split('#', '?')[0];
        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveLinkTarget(string destination, Uri? baseUri)
    {
        if (baseUri is null || !IsDocumentLink(destination))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUri, destination, out var resolved))
        {
            return null;
        }

        return NormalizeDocumentUri(resolved).AbsoluteUri;
    }

    private static string RenderInlineText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        AppendInlineText(inline, builder);
        return builder.ToString();
    }

    private static void AppendInlineText(ContainerInline inline, StringBuilder builder)
    {
        for (var child = inline.FirstChild; child is not null; child = child.NextSibling)
        {
            switch (child)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case LineBreakInline:
                    builder.Append(' ');
                    break;
                case ContainerInline nested:
                    AppendInlineText(nested, builder);
                    break;
            }
        }
    }

    private static (string RawYaml, string BodyMarkdown, IReadOnlyDictionary<string, object?> Values) ParseFrontMatter(string content)
    {
        if (!TrySplitFrontMatter(content, out var rawYaml, out var body))
        {
            return (string.Empty, content, new Dictionary<string, object?>());
        }

        try
        {
            var values = ParseYamlMapping(rawYaml);
            return (rawYaml, body, values);
        }
        catch
        {
            return (rawYaml, body, new Dictionary<string, object?>());
        }
    }

    private static bool TrySplitFrontMatter(string content, out string rawYaml, out string body)
    {
        rawYaml = string.Empty;
        body = content;

        using var reader = new StringReader(content);
        var firstLine = reader.ReadLine();
        if (firstLine is null || firstLine.Trim() != "---")
        {
            return false;
        }

        var yamlLines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Trim() == "---")
            {
                rawYaml = string.Join(Environment.NewLine, yamlLines);
                body = reader.ReadToEnd();
                if (body.StartsWith("\r\n", StringComparison.Ordinal))
                {
                    body = body[2..];
                }
                else if (body.StartsWith("\n", StringComparison.Ordinal))
                {
                    body = body[1..];
                }

                return true;
            }

            yamlLines.Add(line);
        }

        return false;
    }

    private static IReadOnlyDictionary<string, object?> ParseYamlMapping(string rawYaml)
    {
        var yamlStream = new YamlStream();
        yamlStream.Load(new StringReader(rawYaml));

        if (yamlStream.Documents.Count == 0 || yamlStream.Documents[0].RootNode is not YamlMappingNode mapping)
        {
            return new Dictionary<string, object?>();
        }

        return mapping.Children.ToDictionary(
            pair => ((YamlScalarNode)pair.Key).Value ?? string.Empty,
            pair => ConvertYamlNode(pair.Value),
            StringComparer.Ordinal);
    }

    private static object? ConvertYamlNode(YamlNode node)
    {
        return node switch
        {
            YamlScalarNode scalar => ConvertScalarValue(scalar.Value),
            YamlSequenceNode sequence => sequence.Children.Select(ConvertYamlNode).ToArray(),
            YamlMappingNode mapping => mapping.Children.ToDictionary(
                pair => ((YamlScalarNode)pair.Key).Value ?? string.Empty,
                pair => ConvertYamlNode(pair.Value),
                StringComparer.Ordinal),
            _ => null,
        };
    }

    private static object? ConvertScalarValue(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        if (long.TryParse(value, out var longValue))
        {
            return longValue;
        }

        if (decimal.TryParse(value, out var decimalValue))
        {
            return decimalValue;
        }

        if (DateOnly.TryParse(value, out var dateOnly))
        {
            return dateOnly;
        }

        if (DateTimeOffset.TryParse(value, out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        return value;
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            return value switch
            {
                string s => s,
                DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd"),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("yyyy-MM-dd"),
                _ => value.ToString(),
            };
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringList(IReadOnlyDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            return value switch
            {
                string s => new[] { s },
                object?[] array => array.Select(item => item?.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray(),
                IReadOnlyList<object?> list => list.Select(item => item?.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray(),
                IEnumerable<object?> enumerable => enumerable.Select(item => item?.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray(),
                _ => new[] { value.ToString() ?? string.Empty }.Where(item => item.Length > 0).ToArray(),
            };
        }

        return Array.Empty<string>();
    }

    private static IReadOnlyList<MarkdownEntityHint> GetEntityHints(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue("entity_hints", out var value) || value is null)
        {
            return Array.Empty<MarkdownEntityHint>();
        }

        var hints = new List<MarkdownEntityHint>();
        var items = value switch
        {
            object?[] array => array,
            IReadOnlyList<object?> list => list.ToArray(),
            IEnumerable<object?> enumerable => enumerable.ToArray(),
            _ => new object?[] { value },
        };

        foreach (var item in items)
        {
            if (item is not IReadOnlyDictionary<string, object?> mapping)
            {
                continue;
            }

            var label = GetString(mapping, "label") ?? string.Empty;
            if (label.Length == 0)
            {
                continue;
            }

            var type = GetString(mapping, "type");
            var sameAs = GetStringList(mapping, "sameAs");
            hints.Add(new MarkdownEntityHint(label, type, sameAs));
        }

        return hints;
    }

    private sealed record HeadingSlice(
        int Level,
        string Text,
        string Markdown,
        int StartIndex,
        int EndIndex);
}
