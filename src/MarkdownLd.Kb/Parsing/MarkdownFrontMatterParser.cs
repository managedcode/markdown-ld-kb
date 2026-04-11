using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace ManagedCode.MarkdownLd.Kb.Parsing;

internal static class MarkdownFrontMatterParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public static MarkdownFrontMatterParseResult Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new MarkdownFrontMatterParseResult(CreateEmptyFrontMatter(), string.Empty, false);
        }

        var normalized = NormalizeMarkdown(markdown);
        if (!TrySplitFrontMatter(normalized, out var rawYaml, out var body))
        {
            return new MarkdownFrontMatterParseResult(CreateEmptyFrontMatter(), normalized, false);
        }

        if (!TryParseFrontMatter(rawYaml, out var frontMatter))
        {
            throw new InvalidDataException(MarkdownTextConstants.InvalidFrontMatterMessage);
        }

        return new MarkdownFrontMatterParseResult(frontMatter, body, true);
    }

    private static bool TryParseFrontMatter(string rawYaml, out MarkdownFrontMatter frontMatter)
    {
        try
        {
            var values = Deserializer.Deserialize<Dictionary<object, object?>>(rawYaml);
            if (values is null)
            {
                values = new Dictionary<object, object?>();
            }

            var normalized = NormalizeDictionary(values);

            frontMatter = new MarkdownFrontMatter
            {
                RawYaml = rawYaml,
                Values = normalized,
                Title = GetString(normalized, MarkdownTextConstants.TitleKey),
                Summary = GetString(normalized, MarkdownTextConstants.SummaryKey) ?? GetString(normalized, MarkdownTextConstants.DescriptionKey),
                CanonicalUrl = GetString(normalized, MarkdownTextConstants.CanonicalUrlKey) ?? GetString(normalized, MarkdownTextConstants.CanonicalUrlCamelKey),
                About = ReadStringList(normalized, MarkdownTextConstants.AboutKey),
                DatePublished = GetString(normalized, MarkdownTextConstants.DatePublishedKey) ?? GetString(normalized, MarkdownTextConstants.DatePublishedCamelKey),
                DateModified = GetString(normalized, MarkdownTextConstants.DateModifiedKey) ?? GetString(normalized, MarkdownTextConstants.DateModifiedCamelKey),
                Authors = ReadAuthors(normalized),
                Tags = ReadStringList(normalized, MarkdownTextConstants.TagsKey),
                EntityHints = ReadEntityHints(normalized),
            };
            return true;
        }
        catch (YamlException)
        {
            frontMatter = default!;
            return false;
        }
        catch (FormatException)
        {
            frontMatter = default!;
            return false;
        }
        catch (InvalidOperationException)
        {
            frontMatter = default!;
            return false;
        }
    }

    private static MarkdownFrontMatter CreateEmptyFrontMatter()
    {
        return CreateEmptyFrontMatter(string.Empty);
    }

    private static MarkdownFrontMatter CreateEmptyFrontMatter(string rawYaml)
    {
        return new MarkdownFrontMatter
        {
            RawYaml = rawYaml,
            Values = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)),
            Title = null,
            Summary = null,
            CanonicalUrl = null,
            About = [],
            DatePublished = null,
            DateModified = null,
            Authors = [],
            Tags = [],
            EntityHints = [],
        };
    }

    private static bool TrySplitFrontMatter(string markdown, out string rawYaml, out string body)
    {
        rawYaml = string.Empty;
        body = markdown;

        using var reader = new StringReader(markdown);
        var firstLine = reader.ReadLine();
        if (!string.Equals(firstLine?.Trim(), MarkdownTextConstants.FrontMatterFence, StringComparison.Ordinal))
        {
            return false;
        }

        var yaml = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.Equals(line.Trim(), MarkdownTextConstants.FrontMatterFence, StringComparison.Ordinal))
            {
                rawYaml = yaml.ToString().TrimEnd('\n');
                body = reader.ReadToEnd();
                if (body.StartsWith('\n'))
                {
                    body = body[1..];
                }

                return true;
            }

            yaml.AppendLine(line);
        }

        throw new InvalidDataException(MarkdownTextConstants.MissingFrontMatterTerminatorMessage);
    }

    private static string NormalizeMarkdown(string markdown)
    {
        var normalized = markdown.TrimStart('\uFEFF');
        normalized = normalized.Replace(MarkdownTextConstants.CarriageReturnLineFeed, MarkdownTextConstants.LineFeed, StringComparison.Ordinal);
        return normalized.Replace(MarkdownTextConstants.CarriageReturn, MarkdownTextConstants.LineFeed, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, object?> NormalizeDictionary(IEnumerable<KeyValuePair<object, object?>> values)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            var stringKey = key.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(stringKey))
            {
                continue;
            }

            normalized[stringKey] = NormalizeValue(value);
        }

        return new ReadOnlyDictionary<string, object?>(normalized);
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
            DateTime dateTime => dateTime.ToString(MarkdownTextConstants.RoundTripDateFormat, CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString(MarkdownTextConstants.RoundTripDateFormat, CultureInfo.InvariantCulture),
            TimeOnly timeOnly => timeOnly.ToString(MarkdownTextConstants.RoundTripDateFormat, CultureInfo.InvariantCulture),
            IReadOnlyDictionary<object, object?> dictionary => NormalizeDictionary(dictionary),
            IDictionary<object, object?> dictionary => NormalizeDictionary(dictionary),
            IEnumerable<object> sequence when value is not string => sequence.Select(NormalizeValue).ToArray(),
            IEnumerable sequence when value is not string => sequence.Cast<object?>().Select(NormalizeValue).ToArray(),
            _ => value.ToString(),
        };
    }

    private static string? GetString(IReadOnlyDictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out var value) ? ConvertToString(value) : null;
    }

    private static IReadOnlyList<string> ReadStringList(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return [];
        }

        return ReadSequence(value)
            .SelectMany(ConvertListItems)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadAuthors(IReadOnlyDictionary<string, object?> values)
    {
        if (values.TryGetValue(MarkdownTextConstants.AuthorsKey, out var authors) || values.TryGetValue(MarkdownTextConstants.AuthorKey, out authors))
        {
            return ReadSequence(authors)
                .Select(ConvertPerson)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return [];
    }

    private static IReadOnlyList<MarkdownEntityHint> ReadEntityHints(IReadOnlyDictionary<string, object?> values)
    {
        if (!TryGetValue(values, MarkdownTextConstants.EntityHintsKey, out var value) && !TryGetValue(values, MarkdownTextConstants.EntityHintsCamelKey, out value))
        {
            return [];
        }

        return ReadSequence(value)
            .Select(ReadEntityHint)
            .Where(hint => hint is not null && !string.IsNullOrWhiteSpace(hint.Label))
            .Select(hint => hint!)
            .DistinctBy(hint => string.Concat(hint.Label, MarkdownTextConstants.Pipe, hint.Type ?? string.Empty, MarkdownTextConstants.Pipe, string.Join(MarkdownTextConstants.Comma, hint.SameAs ?? [])), StringComparer.OrdinalIgnoreCase)
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
            return new MarkdownEntityHint(NormalizeLabel(label));
        }

        if (item is IReadOnlyDictionary<string, object?> dictionary)
        {
            var sameAs = ReadStringList(dictionary, MarkdownTextConstants.SameAsKey);
            return new MarkdownEntityHint(
                NormalizeLabel(GetString(dictionary, MarkdownTextConstants.LabelKey) ?? GetString(dictionary, MarkdownTextConstants.NameKey) ?? GetString(dictionary, MarkdownTextConstants.ValueKey) ?? string.Empty),
                GetString(dictionary, MarkdownTextConstants.TypeKey),
                sameAs.Count == 0 ? [] : sameAs);
        }

        if (item is IDictionary<object, object?> dynamicDictionary)
        {
            var normalizedDictionary = NormalizeDictionary(dynamicDictionary);
            return ReadEntityHint(normalizedDictionary);
        }

        return new MarkdownEntityHint(NormalizeLabel(ConvertToString(item) ?? string.Empty));
    }

    private static IEnumerable<object?> ReadSequence(object? value)
    {
        if (value is null)
        {
            return [];
        }

        return value switch
        {
            string s => [s],
            IEnumerable<object?> sequence when value is not string => sequence,
            IEnumerable sequence when value is not string => sequence.Cast<object?>(),
            _ => [value],
        };
    }

    private static string? ConvertToString(object? value)
    {
        if (value is null)
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

    private static string? ConvertPerson(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return ConvertToString(GetString(dictionary, MarkdownTextConstants.NameKey) ?? GetString(dictionary, MarkdownTextConstants.LabelKey) ?? GetString(dictionary, MarkdownTextConstants.ValueKey));
        }

        if (value is IDictionary<object, object?> dynamicDictionary)
        {
            var normalizedDictionary = NormalizeDictionary(dynamicDictionary);
            return ConvertPerson(normalizedDictionary);
        }

        return ConvertToString(value);
    }

    private static string? ConvertListItem(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return ConvertToString(GetString(dictionary, MarkdownTextConstants.LabelKey) ?? GetString(dictionary, MarkdownTextConstants.NameKey) ?? GetString(dictionary, MarkdownTextConstants.ValueKey));
        }

        if (value is IDictionary<object, object?> dynamicDictionary)
        {
            var normalizedDictionary = NormalizeDictionary(dynamicDictionary);
            return ConvertListItem(normalizedDictionary);
        }

        return ConvertToString(value);
    }

    private static IEnumerable<string?> ConvertListItems(object? value)
    {
        var item = ConvertListItem(value);
        if (string.IsNullOrWhiteSpace(item))
        {
            return [];
        }

        return item.Split(MarkdownTextConstants.Comma, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> values, string key, out object? value)
    {
        if (values.TryGetValue(key, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static string NormalizeLabel(string value)
    {
        return ConvertToString(value) ?? string.Empty;
    }
}

internal sealed record MarkdownFrontMatterParseResult(
    MarkdownFrontMatter FrontMatter,
    string Body,
    bool HasFrontMatter);
