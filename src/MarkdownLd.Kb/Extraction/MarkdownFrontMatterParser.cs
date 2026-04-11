using System.Collections;
using System.Globalization;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using static ManagedCode.MarkdownLd.Kb.Extraction.MarkdownKnowledgeConstants;

namespace ManagedCode.MarkdownLd.Kb.Extraction;

internal static class MarkdownFrontMatterParser
{
    public static MarkdownFrontMatterParseResult Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarkdownFrontMatterParseResult(new MarkdownFrontMatter(), string.Empty, false);
        }

        var normalized = markdown.TrimStart('\uFEFF').Replace(CarriageReturnLineFeed, LineFeed);
        if (!normalized.StartsWith(FrontMatterStart, StringComparison.Ordinal))
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
            : throw new InvalidDataException(InvalidFrontMatterMessage);

        return new MarkdownFrontMatterParseResult(frontMatter, body, true);
    }

    private static bool TryParseFrontMatter(string frontMatterBlock, out MarkdownFrontMatter frontMatter)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();

            var values = deserializer.Deserialize<Dictionary<string, object?>>(new StringReader(frontMatterBlock));
            if (values is null)
            {
                values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            }

            frontMatter = new MarkdownFrontMatter
            {
                Title = ReadString(values, TitleKey),
                Summary = ReadString(values, SummaryKey),
                CanonicalUrl = ReadString(values, CanonicalUrlKey),
                DatePublished = ReadString(values, DatePublishedKey),
                DateModified = ReadString(values, DateModifiedKey),
                Authors = ReadAuthors(values),
                Tags = ReadStringList(values, TagsKey),
                About = ReadTopics(values, AboutKey),
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
        if (!string.Equals(firstLine?.Trim(), FrontMatterFence, StringComparison.Ordinal))
        {
            return (string.Empty, markdown, false);
        }

        var frontMatter = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.Equals(line.Trim(), FrontMatterFence, StringComparison.Ordinal))
            {
                var body = reader.ReadToEnd();
                return (frontMatter.ToString(), body.TrimStart('\n'), true);
            }

            frontMatter.AppendLine(line);
        }

        throw new InvalidDataException(MissingFrontMatterTerminatorMessage);
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
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Trim(),
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
                null => null,
                string s => s.Trim(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Trim(),
                _ => item.ToString()?.Trim(),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<MarkdownAuthor> ReadAuthors(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue(AuthorsKey, out var value) || value is null)
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
                Name = ReadString(map, NameKey) ?? ReadString(map, LabelKey) ?? string.Empty,
                SameAs = ReadString(map, SameAsKey) ?? ReadString(map, SameAsSnakeKey),
                Type = ReadString(map, TypeKey),
            };
        }

        if (item is IDictionary<object, object?> dynamicMap)
        {
            var dictionary = dynamicMap.ToDictionary(entry => entry.Key.ToString() ?? string.Empty, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            return ReadAuthor(dictionary);
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
            .DistinctBy(BuildTopicKey, StringComparer.OrdinalIgnoreCase)
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
                Label = ReadString(map, LabelKey) ?? ReadString(map, NameKey) ?? ReadString(map, ValueKey) ?? string.Empty,
                SameAs = ReadString(map, SameAsKey) ?? ReadString(map, SameAsSnakeKey),
            };
        }

        if (item is IDictionary<object, object?> dynamicMap)
        {
            var dictionary = dynamicMap.ToDictionary(entry => entry.Key.ToString() ?? string.Empty, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            return ReadTopic(dictionary);
        }

        return new MarkdownTopic { Label = NormalizeLabel(item.ToString() ?? string.Empty) };
    }

    private static IReadOnlyList<MarkdownEntityHint> ReadEntityHints(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue(EntityHintsKey, out var value) || value is null)
        {
            return [];
        }

        return NormalizeSequence(value)
            .Select(ReadEntityHint)
            .Where(hint => hint is not null && !string.IsNullOrWhiteSpace(hint.Label))
            .Select(hint => hint!)
            .DistinctBy(BuildHintKey, StringComparer.OrdinalIgnoreCase)
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
                Label = ReadString(map, LabelKey) ?? ReadString(map, NameKey) ?? string.Empty,
                SameAs = ReadString(map, SameAsKey) ?? ReadString(map, SameAsSnakeKey),
                Type = ReadString(map, TypeKey),
            };
        }

        if (item is IDictionary<object, object?> dynamicMap)
        {
            var dictionary = dynamicMap.ToDictionary(entry => entry.Key.ToString() ?? string.Empty, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
            return ReadEntityHint(dictionary);
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

    private static string BuildTopicKey(MarkdownTopic topic)
    {
        return string.Concat(topic.Label, SameAsKeySeparator, topic.SameAs);
    }

    private static string BuildHintKey(MarkdownEntityHint hint)
    {
        return string.Concat(hint.Label, SameAsKeySeparator, hint.SameAs, SameAsKeySeparator, hint.Type);
    }

    private static IEnumerable<object?> NormalizeSequence(object value)
    {
        return value switch
        {
            string => [value],
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
