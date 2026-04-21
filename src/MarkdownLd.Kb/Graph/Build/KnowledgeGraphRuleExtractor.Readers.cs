using System.Diagnostics.CodeAnalysis;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed partial class KnowledgeGraphRuleExtractor
{
    private string? ReadMapNodeId(
        IReadOnlyDictionary<string, object?> map,
        MarkdownDocument document,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryReadString(map, key, out var value))
            {
                return ResolveNodeId(document, value);
            }
        }

        return null;
    }

    private bool TryReadNodeReference(
        object? item,
        MarkdownDocument document,
        [NotNullWhen(true)] out GraphNodeReference? node)
    {
        if (item is IReadOnlyDictionary<string, object?> map)
        {
            return TryReadMapNodeReference(map, document, out node);
        }

        var text = item?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            node = null;
            return false;
        }

        node = new GraphNodeReference(
            ResolveNodeId(document, text),
            text,
            null,
            [],
            FullConfidence,
            !IsExternalIdentifier(text));
        return true;
    }

    private bool TryReadMapNodeReference(
        IReadOnlyDictionary<string, object?> map,
        MarkdownDocument document,
        [NotNullWhen(true)] out GraphNodeReference? node)
    {
        var label = ReadFirstString(map, LabelKey, NameKey, ValueKey, TargetKey, ObjectKey);
        var idText = ReadFirstString(map, IdKey, TargetIdKey, TargetIdSnakeKey, ObjectIdKey, ObjectIdSnakeKey);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = idText;
        }

        if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(idText))
        {
            node = null;
            return false;
        }

        var type = ReadFirstString(map, TypeKey);
        var sameAs = ReadStringList(map, SameAsKey, SameAsSnakeKey).ToList();
        node = new GraphNodeReference(
            string.IsNullOrWhiteSpace(idText)
                ? ResolveNodeId(document, label!)
                : ResolveNodeId(document, idText),
            label ?? idText!,
            type,
            sameAs,
            FullConfidence,
            ShouldAddEntity(label, idText, type, sameAs));
        return true;
    }

    private string ResolveNodeId(MarkdownDocument document, string? value)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) ||
            text.Equals(ArticleMarker, StringComparison.OrdinalIgnoreCase) ||
            text.Equals(ThisArticleMarker, StringComparison.OrdinalIgnoreCase) ||
            text.Equals(DefaultDocument, StringComparison.OrdinalIgnoreCase))
        {
            return document.DocumentUri.AbsoluteUri;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute))
        {
            return absolute.AbsoluteUri;
        }

        if (text.StartsWith(UriSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return KnowledgeNaming.CreateEntityId(_baseUri, text);
    }

    private static IEnumerable<GraphRuleFrontMatterItem> ReadFrontMatterItems(
        IReadOnlyDictionary<string, object?> frontMatter,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!frontMatter.TryGetValue(key, out var raw))
            {
                continue;
            }

            var index = 0;
            if (raw is IEnumerable<object?> list && raw is not string)
            {
                foreach (var item in list)
                {
                    yield return new GraphRuleFrontMatterItem(key, index, item);
                    index++;
                }
            }
            else
            {
                yield return new GraphRuleFrontMatterItem(key, index, raw);
            }
        }
    }

    private static string? ReadFirstString(IReadOnlyDictionary<string, object?> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (TryReadString(map, key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> ReadStringList(
        IReadOnlyDictionary<string, object?> map,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (raw is IEnumerable<object?> list && raw is not string)
            {
                foreach (var item in list)
                {
                    var text = item?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        yield return text;
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(raw?.ToString()))
            {
                yield return raw.ToString()!.Trim();
            }
        }
    }

    private static bool TryReadString(
        IReadOnlyDictionary<string, object?> map,
        string key,
        [NotNullWhen(true)] out string? value)
    {
        if (map.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw?.ToString()))
        {
            value = raw.ToString()!.Trim();
            return true;
        }

        value = null;
        return false;
    }

    private static bool ShouldAddEntity(
        string? label,
        string? idText,
        string? type,
        IReadOnlyCollection<string> sameAs)
    {
        if (!string.IsNullOrWhiteSpace(type) || sameAs.Count > 0)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(idText))
        {
            return !string.Equals(label, idText, StringComparison.OrdinalIgnoreCase);
        }

        return !IsExternalIdentifier(label);
    }

    private static bool IsExternalIdentifier(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               Uri.TryCreate(value.Trim(), UriKind.Absolute, out _);
    }

    private sealed record GraphRuleFrontMatterItem(
        string RuleName,
        int Index,
        object? Value);

    private sealed record GraphNodeReference(
        string Id,
        string Label,
        string? Type,
        List<string> SameAs,
        double Confidence,
        bool ShouldAddEntity);
}
