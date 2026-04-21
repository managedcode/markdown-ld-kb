using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;
using RootMarkdownChunker = ManagedCode.MarkdownLd.Kb.IMarkdownChunker;
using RootMarkdownChunkingOptions = ManagedCode.MarkdownLd.Kb.MarkdownChunkingOptions;
using RootMarkdownDocument = ManagedCode.MarkdownLd.Kb.MarkdownDocument;
using RootMarkdownDocumentParser = ManagedCode.MarkdownLd.Kb.Parsing.MarkdownDocumentParser;
using RootMarkdownDocumentSource = ManagedCode.MarkdownLd.Kb.MarkdownDocumentSource;
using RootMarkdownParsingOptions = ManagedCode.MarkdownLd.Kb.MarkdownParsingOptions;
using RootMarkdownSection = ManagedCode.MarkdownLd.Kb.MarkdownSection;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class MarkdownDocumentParser
{
    private readonly Uri _baseUri;
    private readonly RootMarkdownDocumentParser _parser;
    private readonly RootMarkdownParsingOptions _parsingOptions;

    public MarkdownDocumentParser(
        Uri? baseUri = null,
        RootMarkdownChunker? chunker = null,
        RootMarkdownChunkingOptions? chunkingOptions = null)
    {
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));
        _parser = new RootMarkdownDocumentParser(chunker);
        _parsingOptions = new RootMarkdownParsingOptions
        {
            DefaultBaseUrl = _baseUri.AbsoluteUri,
            Chunking = chunkingOptions ?? RootMarkdownChunkingOptions.Default,
        };
    }

    public MarkdownDocument Parse(MarkdownSourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var parsed = _parser.Parse(
            new RootMarkdownDocumentSource(
                source.Content,
                source.Path,
                _baseUri.AbsoluteUri,
                source.CanonicalUri?.AbsoluteUri),
            _parsingOptions);

        return new MarkdownDocument(
            ResolveDocumentUri(parsed, source),
            source.Path,
            ResolveTitle(parsed, source.Path),
            ToFrontMatterDictionary(parsed),
            parsed.BodyMarkdown,
            parsed.Sections.Select(MapSection).ToArray(),
            parsed.Chunks);
    }

    private Uri ResolveDocumentUri(RootMarkdownDocument parsed, MarkdownSourceDocument source)
    {
        if (Uri.TryCreate(parsed.DocumentId, UriKind.Absolute, out var parsedUri))
        {
            return parsedUri;
        }

        return source.CanonicalUri ?? KnowledgeNaming.CreateDocumentUri(_baseUri, source.Path);
    }

    private static string ResolveTitle(RootMarkdownDocument parsed, string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(parsed.FrontMatter.Title))
        {
            return parsed.FrontMatter.Title.Trim();
        }

        var firstHeading = parsed.Sections.FirstOrDefault(static section => !string.IsNullOrWhiteSpace(section.HeadingText));
        if (firstHeading is not null && !string.IsNullOrWhiteSpace(firstHeading.HeadingText))
        {
            return firstHeading.HeadingText;
        }

        return Path.GetFileNameWithoutExtension(sourcePath).Replace(Hyphen, SpaceText).Replace('_', ' ');
    }

    private static IReadOnlyDictionary<string, object?> ToFrontMatterDictionary(RootMarkdownDocument parsed)
    {
        return parsed.FrontMatter.Values.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static MarkdownSection MapSection(RootMarkdownSection section)
    {
        return new MarkdownSection(
            section.HeadingLevel,
            section.HeadingPath.Count == 0 ? string.Empty : string.Join(PathSeparator, section.HeadingPath),
            section.HeadingPath.ToArray(),
            ExtractSectionText(section),
            0,
            0);
    }

    private static string ExtractSectionText(RootMarkdownSection section)
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
}
