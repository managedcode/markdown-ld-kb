using ManagedCode.MarkdownLd.Kb.Parsing;
using Markdig;
using Markdig.Syntax;

namespace ManagedCode.MarkdownLd.Kb;

internal static partial class MarkdownChunkFactory
{
    private const int InitialLinkOrder = 0;
    private const int MinimumTokenCount = 1;
    private const int TokenQuarterUnitsPerToken = 4;
    private const int NonCjkTokenQuarterUnits = 1;
    private const int CjkTokenQuarterUnits = 6;
    private const char CjkUnifiedIdeographsStart = '\u4E00';
    private const char CjkUnifiedIdeographsEnd = '\u9FFF';
    private const char CjkExtensionAStart = '\u3400';
    private const char CjkExtensionAEnd = '\u4DBF';
    private const char CjkCompatibilityStart = '\uF900';
    private const char CjkCompatibilityEnd = '\uFAFF';
    private const char CjkSymbolsStart = '\u3000';
    private const char CjkSymbolsEnd = '\u303F';
    private const char HiraganaStart = '\u3040';
    private const char HiraganaEnd = '\u309F';
    private const char KatakanaStart = '\u30A0';
    private const char KatakanaEnd = '\u30FF';
    private const char KatakanaPhoneticExtensionsStart = '\u31F0';
    private const char KatakanaPhoneticExtensionsEnd = '\u31FF';
    private const char HangulJamoStart = '\u1100';
    private const char HangulJamoEnd = '\u11FF';
    private const char HangulCompatibilityJamoStart = '\u3130';
    private const char HangulCompatibilityJamoEnd = '\u318F';
    private const char HangulSyllablesStart = '\uAC00';
    private const char HangulSyllablesEnd = '\uD7AF';
    private const char HangulJamoExtendedAStart = '\uA960';
    private const char HangulJamoExtendedAEnd = '\uA97F';
    private const char HangulJamoExtendedBStart = '\uD7B0';
    private const char HangulJamoExtendedBEnd = '\uD7FF';
    private const char FullwidthHalfwidthStart = '\uFF00';
    private const char FullwidthHalfwidthEnd = '\uFFEF';
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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ChunkTokenTarget);
        ArgumentOutOfRangeException.ThrowIfNegative(options.ChunkOverlapTokenTarget);

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
                currentBlocks = CreateOverlapBlocks(currentBlocks, options.ChunkOverlapTokenTarget);
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
        var links = ExtractLinks(markdown, document.BaseUri, document.ContentPath, ref linkOrder)
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

    private static List<string> CreateOverlapBlocks(
        IReadOnlyList<string> blocks,
        int overlapTokenTarget)
    {
        if (overlapTokenTarget <= 0 || blocks.Count == 0)
        {
            return [];
        }

        var overlapBlocks = new List<string>();
        var overlapTokens = 0;
        for (var index = blocks.Count - 1; index >= 0; index--)
        {
            var block = blocks[index];
            var blockTokens = EstimateTokens(block);
            if (overlapBlocks.Count > 0 && overlapTokens + blockTokens > overlapTokenTarget)
            {
                break;
            }

            overlapBlocks.Insert(0, block);
            overlapTokens += blockTokens;
            if (overlapTokens >= overlapTokenTarget)
            {
                break;
            }
        }

        return overlapBlocks;
    }

    private static string ExtractBlockMarkdown(string markdown, Block block)
    {
        if (block.Span.Start < 0 || block.Span.End < block.Span.Start || block.Span.End >= markdown.Length)
        {
            return string.Empty;
        }

        return markdown[block.Span.Start..(block.Span.End + 1)].Trim();
    }

    private static int EstimateTokens(string text)
    {
        var quarterUnits = 0;
        foreach (var character in text)
        {
            quarterUnits += IsCjkCharacter(character)
                ? CjkTokenQuarterUnits
                : NonCjkTokenQuarterUnits;
        }

        return Math.Max(MinimumTokenCount, quarterUnits / TokenQuarterUnitsPerToken);
    }

    private static bool IsCjkCharacter(char character)
    {
        return IsBetween(character, CjkUnifiedIdeographsStart, CjkUnifiedIdeographsEnd) ||
               IsBetween(character, CjkExtensionAStart, CjkExtensionAEnd) ||
               IsBetween(character, CjkCompatibilityStart, CjkCompatibilityEnd) ||
               IsBetween(character, CjkSymbolsStart, CjkSymbolsEnd) ||
               IsBetween(character, HiraganaStart, HiraganaEnd) ||
               IsBetween(character, KatakanaStart, KatakanaEnd) ||
               IsBetween(character, KatakanaPhoneticExtensionsStart, KatakanaPhoneticExtensionsEnd) ||
               IsBetween(character, HangulJamoStart, HangulJamoEnd) ||
               IsBetween(character, HangulCompatibilityJamoStart, HangulCompatibilityJamoEnd) ||
               IsBetween(character, HangulSyllablesStart, HangulSyllablesEnd) ||
               IsBetween(character, HangulJamoExtendedAStart, HangulJamoExtendedAEnd) ||
               IsBetween(character, HangulJamoExtendedBStart, HangulJamoExtendedBEnd) ||
               IsBetween(character, FullwidthHalfwidthStart, FullwidthHalfwidthEnd);
    }

    private static bool IsBetween(char character, char start, char end) => character >= start && character <= end;
}
