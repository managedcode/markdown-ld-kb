using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TokenizedEntityHintExtractor
{
    private readonly Uri _baseUri;

    public TokenizedEntityHintExtractor(Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri);
    }

    public IReadOnlyList<TokenizedKnowledgeEntityHint> Extract(IReadOnlyList<MarkdownDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        return documents.SelectMany(Extract).ToArray();
    }

    private IEnumerable<TokenizedKnowledgeEntityHint> Extract(MarkdownDocument document)
    {
        foreach (var hint in ReadEntityHints(document.FrontMatter))
        {
            yield return new TokenizedKnowledgeEntityHint(
                CreateEntityHintId(hint.Label),
                document.DocumentUri.AbsoluteUri,
                hint.Label,
                hint.Type,
                hint.SameAs);
        }
    }

    private static IEnumerable<FrontMatterEntityHint> ReadEntityHints(IReadOnlyDictionary<string, object?> frontMatter)
    {
        if (!TryGetFrontMatterValue(frontMatter, EntityHintsKey, out var value) &&
            !TryGetFrontMatterValue(frontMatter, EntityHintsCamelKey, out value))
        {
            yield break;
        }

        foreach (var item in ReadSequence(value))
        {
            var hint = ReadEntityHint(item);
            if (hint is not null)
            {
                yield return hint;
            }
        }
    }

    private static FrontMatterEntityHint? ReadEntityHint(object? item)
    {
        if (item is null)
        {
            return null;
        }

        if (item is IReadOnlyDictionary<string, object?> map)
        {
            return ReadEntityHintMap(map);
        }

        if (item is IDictionary<object, object?> dynamicMap)
        {
            return ReadEntityHintMap(dynamicMap.ToDictionary(
                entry => entry.Key.ToString() ?? string.Empty,
                entry => entry.Value,
                StringComparer.OrdinalIgnoreCase));
        }

        var label = ConvertFrontMatterString(item);
        return string.IsNullOrWhiteSpace(label)
            ? null
            : new FrontMatterEntityHint(label, SchemaThingTypeText, []);
    }

    private static FrontMatterEntityHint? ReadEntityHintMap(IReadOnlyDictionary<string, object?> map)
    {
        var label = ReadString(map, LabelKey) ?? ReadString(map, NameKey) ?? ReadString(map, ValueKey);
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        return new FrontMatterEntityHint(
            label,
            ReadString(map, TypeKey) ?? SchemaThingTypeText,
            ReadStrings(map, SameAsKey, SameAsSnakeKey));
    }

    private static IReadOnlyList<string> ReadStrings(
        IReadOnlyDictionary<string, object?> map,
        string firstKey,
        string secondKey)
    {
        if (!TryGetFrontMatterValue(map, firstKey, out var value) &&
            !TryGetFrontMatterValue(map, secondKey, out value))
        {
            return [];
        }

        return ReadSequence(value)
            .Select(ConvertFrontMatterString)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Select(static text => text!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> map, string key)
    {
        return TryGetFrontMatterValue(map, key, out var value)
            ? ConvertFrontMatterString(value)
            : null;
    }

    private static bool TryGetFrontMatterValue(
        IReadOnlyDictionary<string, object?> map,
        string key,
        out object? value)
    {
        foreach (var entry in map)
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

    private static IEnumerable<object?> ReadSequence(object? value)
    {
        return value switch
        {
            null => [],
            string => [value],
            IEnumerable<object?> sequence => sequence,
            IEnumerable sequence => sequence.Cast<object?>(),
            _ => [value],
        };
    }

    private static string? ConvertFrontMatterString(object? value)
    {
        return value switch
        {
            null => null,
            string text => text.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture).Trim(),
            _ => value.ToString()?.Trim(),
        };
    }

    private string CreateEntityHintId(string label)
    {
        var key = label.Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return new Uri(_baseUri, TokenEntityHintIdPrefix + hash[..TopicHashLength]).AbsoluteUri;
    }

    private sealed record FrontMatterEntityHint(
        string Label,
        string Type,
        IReadOnlyList<string> SameAs);
}
