using System.Globalization;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace ManagedCode.MarkdownLd.Kb.Extraction;

internal static class MarkdownFrontMatterParser
{
    public static MarkdownFrontMatterParseResult Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarkdownFrontMatterParseResult(new MarkdownFrontMatter(), string.Empty, false);
        }

        var normalized = markdown.TrimStart('\uFEFF').Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return new MarkdownFrontMatterParseResult(new MarkdownFrontMatter(), normalized, false);
        }

        var (frontMatterBlock, body, hasFrontMatter) = SplitFrontMatter(normalized);
        if (!hasFrontMatter)
        {
            return new MarkdownFrontMatterParseResult(new MarkdownFrontMatter(), normalized, false);
        }

        var frontMatter = TryParseFrontMatter(frontMatterBlock, out var parsed)
            ? parsed
            : new MarkdownFrontMatter();

        return new MarkdownFrontMatterParseResult(frontMatter, body, true);
    }

    private static bool TryParseFrontMatter(string frontMatterBlock, out MarkdownFrontMatter frontMatter)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();

            var values = deserializer.Deserialize<Dictionary<string, object?>>(new StringReader(frontMatterBlock))
                ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            frontMatter = new MarkdownFrontMatter
            {
                Title = ReadString(values, "title"),
                Summary = ReadString(values, "summary"),
                CanonicalUrl = ReadString(values, "canonical_url"),
                DatePublished = ReadString(values, "date_published"),
                DateModified = ReadString(values, "date_modified"),
                Authors = ReadAuthors(values),
                Tags = ReadStringList(values, "tags"),
                About = ReadTopics(values, "about"),
                EntityHints = ReadEntityHints(values),
            };
            return true;
        }
        catch (YamlException)
        {
            frontMatter = new MarkdownFrontMatter();
            return false;
        }
        catch (FormatException)
        {
            frontMatter = new MarkdownFrontMatter();
            return false;
        }
    }

    private static (string FrontMatter, string Body, bool HasFrontMatter) SplitFrontMatter(string markdown)
    {
        using var reader = new StringReader(markdown);
        var firstLine = reader.ReadLine();
        if (!string.Equals(firstLine?.Trim(), "---", StringComparison.Ordinal))
        {
            return (string.Empty, markdown, false);
        }

        var frontMatter = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
            {
                var body = reader.ReadToEnd();
                return (frontMatter.ToString(), body.TrimStart('\n'), true);
            }

            frontMatter.AppendLine(line);
        }

        return (string.Empty, markdown, false);
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string s => s.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim(),
            _ => value.ToString()?.Trim(),
        };
    }

    private static IReadOnlyList<string> ReadStringList(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        return NormalizeSequence(value)
            .Select(item => item switch
            {
                string s => s.Trim(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim(),
                _ => item.ToString()?.Trim(),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<MarkdownAuthor> ReadAuthors(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue("authors", out var value) || value is null)
        {
            return [];
        }

        return NormalizeSequence(value)
            .Select(ReadAuthor)
            .Where(author => author is not null && !string.IsNullOrWhiteSpace(author.Name))
            .Select(author => author!)
            .DistinctBy(author => author.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static MarkdownAuthor? ReadAuthor(object? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item is string name)
        {
            return new MarkdownAuthor { Name = NormalizeLabel(name) };
        }

        if (item is IReadOnlyDictionary<string, object?> map)
        {
            return new MarkdownAuthor
            {
                Name = ReadString(map, "name") ?? ReadString(map, "label") ?? string.Empty,
                SameAs = ReadString(map, "sameAs") ?? ReadString(map, "same_as"),
                Type = ReadString(map, "type"),
            };
        }

        if (item is IDictionary<object, object?> dynamicMap)
        {
            var map = dynamicMap.ToDictionary(entry => entry.Key.ToString() ?? string.Empty, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            return ReadAuthor(map);
        }

        return new MarkdownAuthor { Name = item.ToString()?.Trim() ?? string.Empty };
    }

    private static IReadOnlyList<MarkdownTopic> ReadTopics(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        return NormalizeSequence(value)
            .Select(ReadTopic)
            .Where(topic => topic is not null && !string.IsNullOrWhiteSpace(topic.Label))
            .Select(topic => topic!)
            .DistinctBy(topic => $"{topic.Label}|{topic.SameAs}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static MarkdownTopic? ReadTopic(object? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item is string label)
        {
            return new MarkdownTopic { Label = NormalizeLabel(label) };
        }

        if (item is IReadOnlyDictionary<string, object?> map)
        {
            return new MarkdownTopic
            {
                Label = ReadString(map, "label") ?? ReadString(map, "name") ?? ReadString(map, "value") ?? string.Empty,
                SameAs = ReadString(map, "sameAs") ?? ReadString(map, "same_as"),
            };
        }

        if (item is IDictionary<object, object?> dynamicMap)
        {
            var map = dynamicMap.ToDictionary(entry => entry.Key.ToString() ?? string.Empty, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            return ReadTopic(map);
        }

        return new MarkdownTopic { Label = NormalizeLabel(item.ToString() ?? string.Empty) };
    }

    private static IReadOnlyList<MarkdownEntityHint> ReadEntityHints(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue("entity_hints", out var value) || value is null)
        {
            return [];
        }

        return NormalizeSequence(value)
            .Select(ReadEntityHint)
            .Where(hint => hint is not null && !string.IsNullOrWhiteSpace(hint.Label))
            .Select(hint => hint!)
            .DistinctBy(hint => $"{hint.Label}|{hint.SameAs}|{hint.Type}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static MarkdownEntityHint? ReadEntityHint(object? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item is string label)
        {
            return new MarkdownEntityHint { Label = NormalizeLabel(label) };
        }

        if (item is IReadOnlyDictionary<string, object?> map)
        {
            return new MarkdownEntityHint
            {
                Label = ReadString(map, "label") ?? ReadString(map, "name") ?? string.Empty,
                SameAs = ReadString(map, "sameAs") ?? ReadString(map, "same_as"),
                Type = ReadString(map, "type"),
            };
        }

        if (item is IDictionary<object, object?> dynamicMap)
        {
            var map = dynamicMap.ToDictionary(entry => entry.Key.ToString() ?? string.Empty, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            return ReadEntityHint(map);
        }

        return new MarkdownEntityHint { Label = NormalizeLabel(item.ToString() ?? string.Empty) };
    }

    private static string NormalizeLabel(string value)
    {
        var text = value.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            var segment = uri.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrWhiteSpace(segment))
            {
                return MarkdownKnowledgeIds.HumanizeLabel(Uri.UnescapeDataString(segment));
            }
        }

        return MarkdownKnowledgeIds.HumanizeLabel(text);
    }

    private static IEnumerable<object?> NormalizeSequence(object value)
    {
        return value switch
        {
            IEnumerable<object?> enumerable => enumerable,
            IEnumerable enumerable => enumerable.Cast<object?>(),
            _ => [value],
        };
    }
}

internal sealed record MarkdownFrontMatterParseResult(
    MarkdownFrontMatter FrontMatter,
    string Body,
    bool HasFrontMatter);
