using System.Text.RegularExpressions;
using ManagedCode.MarkdownLd.Kb.Parsing;
using Markdig;
using Markdig.Syntax;

namespace ManagedCode.MarkdownLd.Kb;

internal static partial class MarkdownChunkFactory
{
    private const int InitialLinkOrder = 0;
    private const int MinimumTokenCount = 1;
    private const int EstimatedCharactersPerToken = 4;
    private const char HeadingPipeSeparator = '|';
    private static readonly MarkdownPipeline BlockParsingPipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseDefinitionLists()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseGenericAttributes()
        .Build();

    public static MarkdownSection BuildSection(
        MarkdownChunkingSection section,
        IReadOnlyList<MarkdownChunk> chunks)
    {
        var links = chunks.SelectMany(static chunk => chunk.Links)
            .DistinctBy(GetLinkKey)
            .ToArray();

        return new MarkdownSection(
            section.SectionId,
            section.Order,
            section.HeadingLevel,
            section.HeadingMarkdown,
            section.HeadingText,
            section.HeadingPath.ToArray(),
            section.Markdown,
            chunks,
            links);
    }

    public static IReadOnlyList<MarkdownChunk> CreateSplitChunks(
        MarkdownChunkingDocument document,
        MarkdownChunkingSection section,
        MarkdownChunkingOptions options)
    {
        var sectionBody = ExtractSectionBody(section);
        var blocks = SplitBlocks(sectionBody);
        if (blocks.Count == 0)
        {
            return [];
        }

        var chunks = new List<MarkdownChunk>();
        var currentBlocks = new List<string>();
        var order = 0;
        var linkOrder = InitialLinkOrder;

        foreach (var block in blocks)
        {
            var candidate = currentBlocks.Count == 0
                ? block
                : string.Concat(
                    string.Join(MarkdownTextConstants.DoubleLineFeed, currentBlocks),
                    MarkdownTextConstants.DoubleLineFeed,
                    block);

            if (currentBlocks.Count > 0 && EstimateTokens(candidate) > options.ChunkTokenTarget)
            {
                chunks.Add(CreateChunk(document, section, currentBlocks, order++, ref linkOrder));
                currentBlocks.Clear();
            }

            currentBlocks.Add(block);
        }

        if (currentBlocks.Count > 0)
        {
            chunks.Add(CreateChunk(document, section, currentBlocks, order, ref linkOrder));
        }

        return chunks;
    }

    public static IReadOnlyList<MarkdownChunk> CreateWholeSectionChunks(
        MarkdownChunkingDocument document,
        MarkdownChunkingSection section)
    {
        var sectionBody = ExtractSectionBody(section);
        if (string.IsNullOrWhiteSpace(sectionBody))
        {
            return [];
        }

        var linkOrder = InitialLinkOrder;
        return [CreateChunk(document, section, [sectionBody.Trim()], 0, ref linkOrder)];
    }

    private static MarkdownChunk CreateChunk(
        MarkdownChunkingDocument document,
        MarkdownChunkingSection section,
        IReadOnlyList<string> blocks,
        int order,
        ref int linkOrder)
    {
        var markdown = string.Join(MarkdownTextConstants.DoubleLineFeed, blocks).Trim();
        var links = ExtractLinks(markdown, document.BaseUri, ref linkOrder)
            .DistinctBy(GetLinkKey)
            .ToArray();

        return new MarkdownChunk(
            Parsing.MarkdownDocumentParser.ComputeChunkId(document.DocumentId, markdown),
            section.SectionId,
            order,
            section.HeadingPath.ToArray(),
            markdown,
            EstimateTokens(markdown),
            links);
    }

    private static string ExtractSectionBody(MarkdownChunkingSection section)
    {
        if (section.HeadingLevel == 0 || string.IsNullOrWhiteSpace(section.HeadingMarkdown))
        {
            return section.Markdown.Trim();
        }

        var headingIndex = section.Markdown.IndexOf(section.HeadingMarkdown, StringComparison.Ordinal);
        if (headingIndex < 0)
        {
            return section.Markdown.Trim();
        }

        var body = section.Markdown[(headingIndex + section.HeadingMarkdown.Length)..];
        return body.TrimStart('\n').Trim();
    }

    private static IReadOnlyList<string> SplitBlocks(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var normalized = markdown.Trim();
        var document = Markdown.Parse(normalized, BlockParsingPipeline);
        var blocks = document
            .Select(block => ExtractBlockMarkdown(normalized, block))
            .Where(static block => !string.IsNullOrWhiteSpace(block))
            .ToArray();

        return blocks.Length == 0 ? [normalized] : blocks;
    }

    private static string ExtractBlockMarkdown(string markdown, Block block)
    {
        if (block.Span.Start < 0 || block.Span.End < block.Span.Start || block.Span.End >= markdown.Length)
        {
            return string.Empty;
        }

        return markdown[block.Span.Start..(block.Span.End + 1)].Trim();
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

        foreach (Match match in MarkdownImageLinkRegex().Matches(markdown))
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
                ref linkOrder,
                true));
        }

        foreach (Match match in MarkdownLinkRegex().Matches(markdown))
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
                ref linkOrder,
                false));
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
            path = Path.ChangeExtension(path, null);
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
        return titleGroup.Success ? titleGroup.Value.Trim() : null;
    }

    private static int EstimateTokens(string text) => Math.Max(MinimumTokenCount, text.Length / EstimatedCharactersPerToken);

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
