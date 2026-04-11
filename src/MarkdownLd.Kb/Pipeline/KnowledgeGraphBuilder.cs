using System.Globalization;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeGraphBuilder(Uri? baseUri = null)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));

    public KnowledgeGraph Build(IReadOnlyList<MarkdownDocument> documents, KnowledgeExtractionResult facts)
    {
        var graph = new Graph();
        RegisterNamespaces(graph);

        foreach (var document in documents)
        {
            AddDocument(graph, document);
        }

        foreach (var entity in facts.Entities)
        {
            AddEntity(graph, entity);
        }

        foreach (var assertion in facts.Assertions)
        {
            AddAssertion(graph, assertion);
        }

        return new KnowledgeGraph(graph);
    }

    private static void RegisterNamespaces(IGraph graph)
    {
        graph.NamespaceMap.AddNamespace(SchemaPrefix, SchemaNamespaceUri);
        graph.NamespaceMap.AddNamespace(KbPrefix, KbNamespaceUri);
        graph.NamespaceMap.AddNamespace(ProvPrefix, ProvNamespaceUri);
        graph.NamespaceMap.AddNamespace(RdfPrefix, RdfNamespaceUri);
        graph.NamespaceMap.AddNamespace(XsdPrefix, XsdNamespaceUri);
    }

    private void AddDocument(Graph graph, MarkdownDocument document)
    {
        if (document.Sections.Count == 0 && document.FrontMatter.Count == 0 && string.IsNullOrWhiteSpace(document.Body))
        {
            return;
        }

        var article = graph.CreateUriNode(document.DocumentUri);
        var schemaArticle = graph.CreateUriNode(SchemaArticleUri);
        var schemaName = graph.CreateUriNode(SchemaNameUri);
        var schemaDescription = graph.CreateUriNode(SchemaDescriptionUri);
        var schemaDatePublished = graph.CreateUriNode(SchemaDatePublishedUri);
        var schemaDateModified = graph.CreateUriNode(SchemaDateModifiedUri);
        var schemaKeywords = graph.CreateUriNode(SchemaKeywordsUri);
        var schemaAbout = graph.CreateUriNode(SchemaAboutUri);
        var schemaAuthor = graph.CreateUriNode(SchemaAuthorUri);
        var provWasDerivedFrom = graph.CreateUriNode(ProvWasDerivedFromUri);

        graph.Assert(new Triple(article, graph.CreateUriNode(RdfTypeUri), schemaArticle));
        graph.Assert(new Triple(article, schemaName, graph.CreateLiteralNode(document.Title)));
        graph.Assert(new Triple(article, provWasDerivedFrom, graph.CreateUriNode(document.DocumentUri)));

        if (TryGetString(document.FrontMatter, SummaryKey, out var summary) ||
            TryGetString(document.FrontMatter, DescriptionKey, out summary))
        {
            graph.Assert(new Triple(article, schemaDescription, graph.CreateLiteralNode(summary ?? string.Empty)));
        }

        if (TryGetString(document.FrontMatter, DatePublishedKey, out var datePublished) ||
            TryGetString(document.FrontMatter, DatePublishedCamelKey, out datePublished))
        {
            graph.Assert(new Triple(article, schemaDatePublished, CreateDateLiteral(graph, datePublished)));
        }

        if (TryGetString(document.FrontMatter, DateModifiedKey, out var dateModified) ||
            TryGetString(document.FrontMatter, DateModifiedCamelKey, out dateModified))
        {
            graph.Assert(new Triple(article, schemaDateModified, CreateDateLiteral(graph, dateModified)));
        }

        foreach (var tag in ReadStrings(document.FrontMatter, TagsKey).Concat(ReadStrings(document.FrontMatter, KeywordsKey)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            graph.Assert(new Triple(article, schemaKeywords, graph.CreateLiteralNode(tag)));
        }

        foreach (var about in ReadStrings(document.FrontMatter, AboutKey))
        {
            var id = KnowledgeNaming.CreateEntityId(_baseUri, about);
            graph.Assert(new Triple(article, schemaAbout, graph.CreateUriNode(new Uri(id))));
        }

        foreach (var author in ReadAuthors(document.FrontMatter))
        {
            var id = KnowledgeNaming.CreateEntityId(_baseUri, author.Label);
            graph.Assert(new Triple(article, schemaAuthor, graph.CreateUriNode(new Uri(id))));
        }
    }

    private void AddEntity(Graph graph, KnowledgeEntityFact entity)
    {
        var entityId = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label);
        var subject = graph.CreateUriNode(new Uri(entityId));
        var rdfType = graph.CreateUriNode(RdfTypeUri);
        var schemaName = graph.CreateUriNode(SchemaNameUri);
        var schemaSameAs = graph.CreateUriNode(SchemaSameAsUri);

        graph.Assert(new Triple(subject, rdfType, graph.CreateUriNode(NormalizeTypeUri(entity.Type))));
        graph.Assert(new Triple(subject, schemaName, graph.CreateLiteralNode(entity.Label)));
        foreach (var sameAs in entity.SameAs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(sameAs, UriKind.Absolute, out var absolute))
            {
                graph.Assert(new Triple(subject, schemaSameAs, graph.CreateUriNode(absolute)));
            }
        }
    }

    private static void AddAssertion(Graph graph, KnowledgeAssertionFact assertion)
    {
        if (!Uri.TryCreate(assertion.SubjectId, UriKind.Absolute, out var subjectUri) ||
            !Uri.TryCreate(assertion.ObjectId, UriKind.Absolute, out var objectUri))
        {
            return;
        }

        var predicateUri = ResolvePredicate(assertion.Predicate);
        graph.Assert(
            new Triple(
                graph.CreateUriNode(subjectUri),
                graph.CreateUriNode(predicateUri),
                graph.CreateUriNode(objectUri)));

        if (!string.IsNullOrWhiteSpace(assertion.Source))
        {
            graph.Assert(new Triple(graph.CreateUriNode(subjectUri), graph.CreateUriNode(ProvWasDerivedFromUri), graph.CreateUriNode(new Uri(assertion.Source))));
        }
    }

    private static Uri ResolvePredicate(string predicate)
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
                XsdPrefix => new Uri(XsdNamespaceText + local),
                _ => Uri.TryCreate(predicate, UriKind.Absolute, out var prefixedAbsolute)
                    ? prefixedAbsolute
                    : new Uri(KbNamespaceText + KnowledgeNaming.Slugify(predicate)),
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
            SameAsPredicateKey => SchemaSameAsUri,
            _ => new Uri(KbNamespaceText + KnowledgeNaming.Slugify(predicate)),
        };
    }

    private static Uri NormalizeTypeUri(string type)
    {
        if (type.Contains(':', StringComparison.Ordinal))
        {
            return ResolvePredicate(type);
        }

        return new Uri(SchemaNamespaceText + KnowledgeNaming.Slugify(type));
    }

    private static ILiteralNode CreateDateLiteral(Graph graph, string? value)
    {
        if (DateOnly.TryParse(value, out var dateOnly))
        {
            return graph.CreateLiteralNode(dateOnly.ToString(DotNetDateFormat, CultureInfo.InvariantCulture), XsdDateUri);
        }

        return graph.CreateLiteralNode(value ?? string.Empty);
    }

    private static bool TryGetString(IReadOnlyDictionary<string, object?> frontMatter, string key, out string? value)
    {
        foreach (var entry in frontMatter)
        {
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Value?.ToString();
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = null;
        return false;
    }

    private static IEnumerable<string> ReadStrings(IReadOnlyDictionary<string, object?> frontMatter, string key)
    {
        foreach (var entry in frontMatter)
        {
            if (!entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Value is IEnumerable<object?> list && entry.Value is not string)
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
            else
            {
                var text = entry.Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return text;
                }
            }
        }
    }

    private static IEnumerable<(string Label, string? Type)> ReadAuthors(IReadOnlyDictionary<string, object?> frontMatter)
    {
        foreach (var entry in frontMatter)
        {
            if (!entry.Key.Equals(AuthorKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Value is IEnumerable<object?> list && entry.Value is not string)
            {
                foreach (var item in list)
                {
                    if (item is IReadOnlyDictionary<string, object?> map)
                    {
                        yield return ((map.TryGetValue(LabelKey, out var label) ? label?.ToString() : null)
                            ?? (map.TryGetValue(NameKey, out var name) ? name?.ToString() : null)
                            ?? string.Empty, map.TryGetValue(TypeKey, out var type) ? type?.ToString() : null);
                    }
                    else
                    {
                        var text = item?.ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            yield return (text, SchemaPersonTypeText);
                        }
                    }
                }
            }
            else
            {
                var text = entry.Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return (text, SchemaPersonTypeText);
                }
            }
        }
    }
}
