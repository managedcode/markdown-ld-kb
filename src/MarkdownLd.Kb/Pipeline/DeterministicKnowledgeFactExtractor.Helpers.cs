using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class DeterministicKnowledgeFactExtractor
{
    private static int EntityTypePriority(string type)
    {
        return type switch
        {
            SchemaPersonTypeText => 5,
            SchemaOrganizationTypeText => 5,
            SchemaSoftwareApplicationTypeText => 5,
            SchemaCreativeWorkTypeText => 4,
            SchemaArticleTypeText => 4,
            SchemaThingTypeText => 1,
            _ => 0,
        };
    }

    private static IEnumerable<object?> ReadFrontMatterSequence(IReadOnlyDictionary<string, object?> frontMatter, string key)
    {
        if (!TryGetValue(frontMatter, key, out var value))
        {
            return [];
        }

        if (value is IEnumerable<object?> sequence && value is not string)
        {
            return sequence;
        }

        return [value];
    }

    private static (string Label, IEnumerable<string> SameAs, string Type) ReadNamedEntity(object? node, string defaultType)
    {
        if (node is string text)
        {
            return (text.Trim(), [], defaultType);
        }

        if (node is not IDictionary<string, object?> map)
        {
            return (string.Empty, [], defaultType);
        }

        var label = ReadString(map, LabelKey) ?? ReadString(map, NameKey) ?? string.Empty;
        var type = NormalizeEntityTypeText(ReadString(map, TypeKey), defaultType);
        var sameAs = ReadStringSequence(map, SameAsKey);
        return (label, sameAs, type);
    }

    private static string NormalizeEntityTypeText(string? type, string defaultType)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return defaultType;
        }

        var trimmed = type.Trim();
        return trimmed.Contains(Colon, StringComparison.Ordinal)
            ? trimmed
            : string.Concat(SchemaPrefix, Colon, trimmed);
    }

    private static string? ReadScalarLabel(object? node)
    {
        if (node is string text)
        {
            return text.Trim();
        }

        if (node is IDictionary<string, object?> map)
        {
            return ReadString(map, LabelKey) ?? ReadString(map, NameKey);
        }

        return null;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> frontMatter, string key, out object? value)
    {
        foreach (var entry in frontMatter)
        {
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetValue(IDictionary<string, object?> frontMatter, string key, out object? value)
    {
        foreach (var entry in frontMatter)
        {
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static string? ReadString(IDictionary<string, object?> map, string key)
    {
        return TryGetValue(map, key, out var value) ? value?.ToString() : null;
    }

    private static IEnumerable<string> ReadStringSequence(IDictionary<string, object?> map, string key)
    {
        if (!TryGetValue(map, key, out var value) || value is null)
        {
            return [];
        }

        if (value is IEnumerable<object?> sequence && value is not string)
        {
            return sequence
                .Select(item => item?.ToString()?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!);
        }

        var scalar = value.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(scalar) ? [] : [scalar];
    }

    [GeneratedRegex(WikiLinkPattern)]
    private static partial Regex WikiLinkRegex();

    [GeneratedRegex(MarkdownLinkPattern)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(ArrowPattern, RegexOptions.Multiline)]
    private static partial Regex ArrowRegex();
}
