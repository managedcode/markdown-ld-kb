using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownDocumentParser
{
    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
    private readonly Uri _baseUri;
    private readonly IDeserializer _yamlDeserializer;

    public MarkdownDocumentParser(Uri? baseUri = null)
    {
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri("https://example.com/", UriKind.Absolute));
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public MarkdownDocument Parse(MarkdownSourceDocument source)
    {
        var content = (source.Content ?? string.Empty).TrimStart('\uFEFF');
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
        if (TryGetString(frontMatter, "title", out var title) && !string.IsNullOrWhiteSpace(title))
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
        if (!content.StartsWith("---", StringComparison.Ordinal))
        {
            return (new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), content);
        }

        var lines = content.Split('\n');
        if (lines.Length < 3)
        {
            return (new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), content);
        }

        var endIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex < 0)
        {
            return (new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase), content);
        }

        var yaml = string.Join('\n', lines.Skip(1).Take(endIndex - 1));
        IReadOnlyDictionary<string, object?> frontMatter;
        try
        {
            var parsed = _yamlDeserializer.Deserialize<object>(yaml);
            frontMatter = NormalizeYamlObject(parsed);
        }
        catch
        {
            frontMatter = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var body = string.Join('\n', lines.Skip(endIndex + 1));
        return (frontMatter, body.TrimStart('\r', '\n'));
    }

    private static Dictionary<string, object?> NormalizeYamlObject(object? node)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (node is not IDictionary<object, object> dictionary)
        {
            return result;
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
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
        var matches = HeadingRegex.Matches(body);

        if (matches.Count == 0)
        {
            return string.IsNullOrWhiteSpace(body)
                ? []
                : [new MarkdownSection(0, string.Empty, [], body.Trim(), 0, body.Length)];
        }

        var currentStart = 0;
        for (var i = 0; i < matches.Count; i++)
        {
            var headingMatch = matches[i];
            var text = body.Substring(currentStart, headingMatch.Index - currentStart);
            AddSectionIfNeeded(sections, headingPath, text, currentStart, headingMatch.Index);

            var level = headingMatch.Groups[1].Value.Length;
            var heading = headingMatch.Groups[2].Value.Trim();

            while (headingPath.Count >= level)
            {
                headingPath.RemoveAt(headingPath.Count - 1);
            }

            headingPath.Add(heading);
            currentStart = headingMatch.Index + headingMatch.Length;
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
            headingPath.Count == 0 ? string.Empty : string.Join(" / ", headingPath),
            headingPath.ToArray(),
            trimmed,
            startOffset,
            endOffset));
    }
}
