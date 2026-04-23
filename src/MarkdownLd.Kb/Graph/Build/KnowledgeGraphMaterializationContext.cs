using System.Globalization;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class KnowledgeGraphMaterializationContext(Graph graph)
{
    private readonly Dictionary<string, IUriNode> _uriNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ILiteralNode> _literalNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<TypedLiteralKey, ILiteralNode> _typedLiteralNodes = [];
    private readonly Dictionary<string, Uri?> _predicateUris = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Uri> _typeUris = new(StringComparer.OrdinalIgnoreCase);

    public Graph Graph { get; } = graph;

    public IUriNode UriNode(Uri uri)
    {
        var key = uri.AbsoluteUri;
        return _uriNodes.TryGetValue(key, out var node)
            ? node
            : _uriNodes[key] = Graph.CreateUriNode(uri);
    }

    public ILiteralNode LiteralNode(string value)
    {
        return _literalNodes.TryGetValue(value, out var node)
            ? node
            : _literalNodes[value] = Graph.CreateLiteralNode(value);
    }

    public ILiteralNode TypedLiteral(string value, Uri datatype)
    {
        var key = new TypedLiteralKey(value, datatype.AbsoluteUri);
        return _typedLiteralNodes.TryGetValue(key, out var node)
            ? node
            : _typedLiteralNodes[key] = Graph.CreateLiteralNode(value, datatype);
    }

    public INode UriOrLiteralNode(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            ? UriNode(absolute)
            : LiteralNode(value);
    }

    public ILiteralNode ConfidenceLiteral(double confidence)
    {
        return TypedLiteral(confidence.ToString(CultureInfo.InvariantCulture), XsdDecimalUri);
    }

    public ILiteralNode DateLiteral(string? value)
    {
        return DateOnly.TryParse(value, out var dateOnly)
            ? TypedLiteral(dateOnly.ToString(DotNetDateFormat, CultureInfo.InvariantCulture), XsdDateUri)
            : LiteralNode(value ?? string.Empty);
    }

    public Uri? ResolvePredicateUri(string predicate)
    {
        return _predicateUris.TryGetValue(predicate, out var cached)
            ? cached
            : _predicateUris[predicate] = ResolvePredicate(predicate);
    }

    public Uri ResolveTypeUri(string type)
    {
        return _typeUris.TryGetValue(type, out var cached)
            ? cached
            : _typeUris[type] = NormalizeTypeUri(type);
    }

    private static Uri? ResolvePredicate(string predicate)
    {
        if (predicate.Contains(':', StringComparison.Ordinal))
        {
            var separatorIndex = predicate.IndexOf(':');
            var prefix = predicate[..separatorIndex];
            var local = predicate[(separatorIndex + 1)..];
            return prefix.ToLowerInvariant() switch
            {
                SchemaPrefix => new Uri(SchemaNamespaceText + local),
                KbPrefix => new Uri(KbNamespaceText + local),
                ProvPrefix => new Uri(ProvNamespaceText + local),
                RdfPrefix => new Uri(RdfNamespaceText + local),
                RdfsPrefix => new Uri(RdfsNamespaceText + local),
                OwlPrefix => new Uri(OwlNamespaceText + local),
                SkosPrefix => new Uri(SkosNamespaceText + local),
                XsdPrefix => new Uri(XsdNamespaceText + local),
                _ => Uri.TryCreate(predicate, UriKind.Absolute, out var prefixedAbsolute)
                    ? prefixedAbsolute
                    : null,
            };
        }

        if (Uri.TryCreate(predicate, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        return predicate.ToLowerInvariant() switch
        {
            MentionPredicateKey => SchemaMentionsUri,
            AboutPredicateKey => SchemaAboutUri,
            AuthorPredicateKey => SchemaAuthorUri,
            CreatorPredicateKey => SchemaCreatorUri,
            HasPartPredicateKey => SchemaHasPartUri,
            SameAsPredicateKey => SchemaSameAsUri,
            _ => null,
        };
    }

    private static Uri NormalizeTypeUri(string type)
    {
        if (type.Contains(':', StringComparison.Ordinal))
        {
            return ResolvePredicate(type) ?? SchemaThingTypeUri();
        }

        return new Uri(SchemaNamespaceText + KnowledgeNaming.Slugify(type));
    }

    private static Uri SchemaThingTypeUri()
    {
        return new Uri(SchemaNamespaceText + KnowledgeNaming.Slugify(SchemaThingTypeText));
    }

    private readonly record struct TypedLiteralKey(string Value, string DatatypeUri);
}
