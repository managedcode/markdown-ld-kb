using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class MarkdownDocumentParser(Uri? baseUri = null)
{
    private static readonly Regex HeadingRegex = new(HeadingPattern, RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly MarkdownPipeline MarkdigPipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseYamlFrontMatter()
        .UseDefinitionLists()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseGenericAttributes()
        .Build();

    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));
    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    public MarkdownDocument Parse(MarkdownSourceDocument source)
    {
        var content = source.Content.TrimStart('\uFEFF');
        var (frontMatter, body) = ParseFrontMatter(content);
        var documentUri = source.CanonicalUri ?? DeriveDocumentUri(source.Path);
        var title = ResolveTitle(frontMatter, body, source.Path);
        var sections = ParseSections(body);

        return new MarkdownDocument(
            documentUri,
            source.Path,
            title,
            frontMatter,
            body,
            sections);
    }

    private Uri DeriveDocumentUri(string sourcePath)
    {
        return KnowledgeNaming.CreateDocumentUri(_baseUri, sourcePath);
    }

    private static string ResolveTitle(IReadOnlyDictionary<string, object?> frontMatter, string body, string sourcePath)
    {
        if (TryGetString(frontMatter, TitleKey, out var title) && !string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        var firstHeading = HeadingRegex.Match(body);
        if (firstHeading.Success)
        {
            return firstHeading.Groups[2].Value.Trim();
        }

        return Path.GetFileNameWithoutExtension(sourcePath).Replace('-', ' ').Replace('_', ' ');
    }

    private (IReadOnlyDictionary<string, object?> FrontMatter, string Body) ParseFrontMatter(string content)
    {
        if (!content.StartsWith(FrontMatterMarker, StringComparison.Ordinal))
        {
            return (new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), content);
        }

        var lines = content.Split('\n');
        if (lines.Length < 3)
        {
            throw new InvalidDataException(MissingFrontMatterTerminatorMessage);
        }

        var endIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == FrontMatterMarker)
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex < 0)
        {
            throw new InvalidDataException(MissingFrontMatterTerminatorMessage);
        }

        var yaml = string.Join(NewLineDelimiter, lines.Skip(1).Take(endIndex - 1));
        var parsed = _yamlDeserializer.Deserialize<object>(yaml);
        var frontMatter = NormalizeYamlObject(parsed)
            ?? throw new InvalidDataException(FrontMatterMappingExpectedMessage);

        var body = string.Join(NewLineDelimiter, lines.Skip(endIndex + 1));
        return (frontMatter, body.TrimStart('\r', '\n'));
    }

    private static Dictionary<string, object?>? NormalizeYamlObject(object? node)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (node is null)
        {
            return result;
        }

        if (node is not IDictionary<object, object> dictionary)
        {
            return null;
        }

        foreach (var entry in dictionary)
        {
            result[entry.Key.ToString() ?? string.Empty] = NormalizeYamlValue(entry.Value);
        }

        return result;
    }

    private static object? NormalizeYamlValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IDictionary<object, object> dictionary)
        {
            var converted = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in dictionary)
            {
                converted[entry.Key.ToString() ?? string.Empty] = NormalizeYamlValue(entry.Value);
            }

            return converted;
        }

        if (value is IEnumerable<object> sequence && value is not string)
        {
            return sequence.Select(NormalizeYamlValue).ToList();
        }

        return value switch
        {
            DateTime dateTime => dateTime.ToString(DotNetDateFormat, CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString(DotNetDateFormat, CultureInfo.InvariantCulture),
            _ => value,
        };
    }

    private static bool TryGetString(IReadOnlyDictionary<string, object?> frontMatter, string key, out string? value)
    {
        if (frontMatter.TryGetValue(key, out var raw) && raw is string text)
        {
            value = text;
            return true;
        }

        value = null;
        return false;
    }

    private static IReadOnlyList<MarkdownSection> ParseSections(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var sections = new List<MarkdownSection>();
        var headingPath = new List<string>();
        var headings = Markdown.Parse(body, MarkdigPipeline)
            .Descendants<HeadingBlock>()
            .Where(static heading => heading.Span.Start >= 0)
            .OrderBy(static heading => heading.Span.Start)
            .ToArray();

        if (headings.Length == 0)
        {
            return string.IsNullOrWhiteSpace(body)
                ? []
                : [new MarkdownSection(0, string.Empty, [], body.Trim(), 0, body.Length)];
        }

        var currentStart = 0;
        for (var i = 0; i < headings.Length; i++)
        {
            var heading = headings[i];
            var headingStart = Math.Clamp(heading.Span.Start, 0, body.Length);
            var text = body.Substring(currentStart, headingStart - currentStart);
            AddSectionIfNeeded(sections, headingPath, text, currentStart, headingStart);

            var level = heading.Level;
            var headingText = ExtractHeadingText(body, heading);

            while (headingPath.Count >= level)
            {
                headingPath.RemoveAt(headingPath.Count - 1);
            }

            headingPath.Add(headingText);
            currentStart = Math.Clamp(heading.Span.End + 1, 0, body.Length);
        }

        AddSectionIfNeeded(sections, headingPath, body.Substring(currentStart), currentStart, body.Length);
        return sections;
    }

    private static void AddSectionIfNeeded(
        ICollection<MarkdownSection> sections,
        IReadOnlyList<string> headingPath,
        string text,
        int startOffset,
        int endOffset)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        sections.Add(new MarkdownSection(
            headingPath.Count,
            headingPath.Count == 0 ? string.Empty : string.Join(PathSeparator, headingPath),
            headingPath.ToArray(),
            trimmed,
            startOffset,
            endOffset));
    }

    private static string ExtractHeadingText(string body, HeadingBlock heading)
    {
        var start = Math.Clamp(heading.Span.Start, 0, body.Length);
        var end = Math.Clamp(heading.Span.End + 1, start, body.Length);
        var markdown = body[start..end].Trim();
        var firstLine = markdown.Split('\n', 2)[0].Trim();
        var atxMatch = HeadingRegex.Match(firstLine);
        return atxMatch.Success
            ? atxMatch.Groups[2].Value.Trim()
            : firstLine;
    }
}
