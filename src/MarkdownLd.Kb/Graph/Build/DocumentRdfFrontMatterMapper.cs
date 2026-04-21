using System.Globalization;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class DocumentRdfFrontMatterMapper(DocumentRdfMappingOptions? options = null)
{
    private readonly DocumentRdfMappingOptions _options = options ?? DocumentRdfMappingOptions.Default;

    public IReadOnlyDictionary<string, Uri> RegisterPrefixes(IGraph graph, IReadOnlyDictionary<string, object?> frontMatter)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(frontMatter);

        var prefixes = CreateBuiltInPrefixes();
        foreach (var prefix in _options.Prefixes)
        {
            SetPrefix(prefixes, prefix.Key, ValidateNamespace(prefix.Key, prefix.Value));
        }

        if (_options.EnableFrontMatterMappings)
        {
            foreach (var prefix in ReadPrefixMappings(frontMatter))
            {
                SetPrefix(prefixes, prefix.Key, prefix.Value);
            }
        }

        foreach (var prefix in prefixes.Where(static prefix => !IsBuiltInPrefix(prefix.Key)))
        {
            graph.NamespaceMap.AddNamespace(prefix.Key, prefix.Value);
        }

        return prefixes;
    }

    public void Apply(
        Graph graph,
        INode subject,
        IReadOnlyDictionary<string, object?> frontMatter,
        IReadOnlyDictionary<string, Uri> prefixes)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(frontMatter);
        ArgumentNullException.ThrowIfNull(prefixes);

        if (!_options.EnableFrontMatterMappings)
        {
            return;
        }

        var rdfType = graph.CreateUriNode(RdfTypeUri);
        foreach (var typeValue in ReadStringValues(frontMatter, RdfTypesKey, RdfTypesCamelKey))
        {
            graph.Assert(new Triple(subject, rdfType, graph.CreateUriNode(ResolveRequiredIdentifier(typeValue, prefixes))));
        }

        if (!TryGetMapping(frontMatter, RdfPropertiesKey, RdfPropertiesCamelKey, out var properties))
        {
            return;
        }

        foreach (var property in properties)
        {
            var predicateUri = ResolveRequiredIdentifier(property.Key, prefixes);
            foreach (var objectNode in CreateObjectNodes(graph, property.Value, prefixes))
            {
                graph.Assert(new Triple(subject, graph.CreateUriNode(predicateUri), objectNode));
            }
        }
    }

    private static Dictionary<string, Uri> CreateBuiltInPrefixes()
    {
        return new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase)
        {
            [SchemaPrefix] = SchemaNamespaceUri,
            [KbPrefix] = KbNamespaceUri,
            [ProvPrefix] = ProvNamespaceUri,
            [RdfPrefix] = RdfNamespaceUri,
            [XsdPrefix] = XsdNamespaceUri,
        };
    }

    private static bool IsBuiltInPrefix(string prefix)
    {
        return string.Equals(prefix, SchemaPrefix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(prefix, KbPrefix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(prefix, ProvPrefix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(prefix, RdfPrefix, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(prefix, XsdPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetPrefix(Dictionary<string, Uri> prefixes, string prefix, Uri uri)
    {
        if (prefixes.TryGetValue(prefix, out var existing) && existing != uri)
        {
            throw new InvalidDataException(string.Concat(RdfPrefixMessagePrefix, prefix, ConflictingRdfPrefixMessageSuffix));
        }

        prefixes[prefix] = uri;
    }

    private static IEnumerable<KeyValuePair<string, Uri>> ReadPrefixMappings(IReadOnlyDictionary<string, object?> frontMatter)
    {
        if (!TryGetMapping(frontMatter, RdfPrefixesKey, RdfPrefixesCamelKey, out var prefixes))
        {
            yield break;
        }

        foreach (var prefix in prefixes)
        {
            yield return new KeyValuePair<string, Uri>(prefix.Key, ValidateNamespace(prefix.Key, prefix.Value?.ToString()));
        }
    }

    private static IEnumerable<string> ReadStringValues(
        IReadOnlyDictionary<string, object?> frontMatter,
        string snakeCaseKey,
        string camelCaseKey)
    {
        if (!frontMatter.TryGetValue(snakeCaseKey, out var value) && !frontMatter.TryGetValue(camelCaseKey, out value))
        {
            yield break;
        }

        if (value is IEnumerable<object?> sequence && value is not string)
        {
            foreach (var item in sequence)
            {
                var text = item?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }

            yield break;
        }

        var single = value?.ToString()?.Trim();
        if (!string.IsNullOrWhiteSpace(single))
        {
            yield return single;
        }
    }

    private static bool TryGetMapping(
        IReadOnlyDictionary<string, object?> frontMatter,
        string snakeCaseKey,
        string camelCaseKey,
        out IReadOnlyDictionary<string, object?> mapping)
    {
        if (!frontMatter.TryGetValue(snakeCaseKey, out var value) && !frontMatter.TryGetValue(camelCaseKey, out value))
        {
            mapping = default!;
            return false;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            mapping = dictionary;
            return true;
        }

        throw new InvalidDataException(string.Equals(snakeCaseKey, RdfPrefixesKey, StringComparison.Ordinal)
            ? InvalidRdfPrefixesMessage
            : InvalidRdfPropertiesMessage);
    }

    private static IEnumerable<INode> CreateObjectNodes(Graph graph, object? value, IReadOnlyDictionary<string, Uri> prefixes)
    {
        if (value is null)
        {
            yield break;
        }

        if (value is IEnumerable<object?> sequence && value is not string)
        {
            foreach (var item in sequence)
            {
                foreach (var node in CreateObjectNodes(graph, item, prefixes))
                {
                    yield return node;
                }
            }

            yield break;
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            if (dictionary.TryGetValue(IdKey, out var idValue) && !string.IsNullOrWhiteSpace(idValue?.ToString()))
            {
                yield return graph.CreateUriNode(ResolveRequiredIdentifier(idValue!.ToString()!, prefixes));
                yield break;
            }

            if (dictionary.TryGetValue(ValueKey, out var scalarValue))
            {
                var datatype = dictionary.TryGetValue(DatatypeKey, out var datatypeValue)
                    ? datatypeValue?.ToString()
                    : dictionary.TryGetValue(DatatypeCamelKey, out datatypeValue)
                        ? datatypeValue?.ToString()
                        : null;
                yield return CreateLiteralNode(graph, scalarValue, datatype, prefixes);
                yield break;
            }

            throw new InvalidDataException(InvalidRdfPropertyValueMessage);
        }

        if (value is string text && TryResolveOptionalIdentifier(text, prefixes, out var identifier))
        {
            yield return graph.CreateUriNode(identifier);
            yield break;
        }

        yield return CreateLiteralNode(graph, value, null, prefixes);
    }

    private static ILiteralNode CreateLiteralNode(Graph graph, object? value, string? datatype, IReadOnlyDictionary<string, Uri> prefixes)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(datatype))
        {
            return graph.CreateLiteralNode(text);
        }

        return graph.CreateLiteralNode(text, ResolveRequiredIdentifier(datatype, prefixes));
    }

    private static bool TryResolveOptionalIdentifier(string value, IReadOnlyDictionary<string, Uri> prefixes, out Uri uri)
    {
        var trimmed = value.Trim();
        var separatorIndex = trimmed.IndexOf(Colon, StringComparison.Ordinal);
        if (separatorIndex > 0 && separatorIndex < trimmed.Length - 1)
        {
            var prefix = trimmed[..separatorIndex];
            if (prefixes.TryGetValue(prefix, out var namespaceUri))
            {
                uri = CreateQualifiedUri(namespaceUri, trimmed[(separatorIndex + 1)..]);
                return true;
            }

            if (!LooksLikeAbsoluteUri(trimmed))
            {
                uri = default!;
                return false;
            }
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            uri = absolute;
            return true;
        }

        uri = default!;
        return false;
    }

    private static Uri ResolveRequiredIdentifier(string value, IReadOnlyDictionary<string, Uri> prefixes)
    {
        if (TryResolveOptionalIdentifier(value, prefixes, out var uri))
        {
            return uri;
        }

        var separatorIndex = value.IndexOf(Colon, StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            var prefix = value[..separatorIndex];
            throw new InvalidDataException(string.Concat(RdfPrefixMessagePrefix, prefix, UnknownRdfPrefixMessageSuffix));
        }

        throw new InvalidDataException(string.Concat(RdfPrefixMessagePrefix, value, UnknownRdfPrefixMessageSuffix));
    }

    private static Uri ValidateNamespace(string prefix, string? value)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidDataException(string.Concat(RdfPrefixMessagePrefix, prefix, InvalidRdfPrefixNamespaceMessageSuffix));
        }

        return uri;
    }

    private static bool LooksLikeAbsoluteUri(string value)
    {
        return value.Contains(UriAuthoritySeparator, StringComparison.Ordinal) ||
               value.StartsWith(UriSchemePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri CreateQualifiedUri(Uri namespaceUri, string localName)
    {
        return new Uri(namespaceUri.AbsoluteUri + localName, UriKind.Absolute);
    }
}
