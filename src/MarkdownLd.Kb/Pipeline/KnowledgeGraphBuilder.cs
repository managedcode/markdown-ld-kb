using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeGraphBuilder(Uri? baseUri = null)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));

    public KnowledgeGraph Build(
        IReadOnlyList<MarkdownDocument> documents,
        KnowledgeExtractionResult facts,
        TokenizedKnowledgeIndex? tokenIndex = null)
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

        return new KnowledgeGraph(graph, tokenIndex);
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

        AddDocumentDescription(graph, article, schemaDescription, document.FrontMatter);
        AddDocumentDate(graph, article, schemaDatePublished, document.FrontMatter, DatePublishedKey, DatePublishedCamelKey);
        AddDocumentDate(graph, article, schemaDateModified, document.FrontMatter, DateModifiedKey, DateModifiedCamelKey);
        AddDocumentKeywords(graph, article, schemaKeywords, document.FrontMatter);
        AddDocumentAbout(graph, article, schemaAbout, document.FrontMatter, _baseUri);
        AddDocumentAuthors(graph, article, schemaAuthor, document.FrontMatter, _baseUri);
    }

    private static void AddDocumentDescription(
        Graph graph,
        INode article,
        INode schemaDescription,
        IReadOnlyDictionary<string, object?> frontMatter)
    {
        if (TryGetString(frontMatter, SummaryKey, out var summary) ||
            TryGetString(frontMatter, DescriptionKey, out summary))
        {
            graph.Assert(new Triple(article, schemaDescription, graph.CreateLiteralNode(summary)));
        }
    }

    private static void AddDocumentDate(
        Graph graph,
        INode article,
        INode schemaDate,
        IReadOnlyDictionary<string, object?> frontMatter,
        string snakeCaseKey,
        string camelCaseKey)
    {
        if (TryGetString(frontMatter, snakeCaseKey, out var date) ||
            TryGetString(frontMatter, camelCaseKey, out date))
        {
            graph.Assert(new Triple(article, schemaDate, CreateDateLiteral(graph, date)));
        }
    }

    private static void AddDocumentKeywords(
        Graph graph,
        INode article,
        INode schemaKeywords,
        IReadOnlyDictionary<string, object?> frontMatter)
    {
        foreach (var tag in ReadStrings(frontMatter, TagsKey).Concat(ReadStrings(frontMatter, KeywordsKey)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            graph.Assert(new Triple(article, schemaKeywords, graph.CreateLiteralNode(tag)));
        }
    }

    private static void AddDocumentAbout(
        Graph graph,
        INode article,
        INode schemaAbout,
        IReadOnlyDictionary<string, object?> frontMatter,
        Uri baseUri)
    {
        foreach (var about in ReadStrings(frontMatter, AboutKey))
        {
            var id = KnowledgeNaming.CreateEntityId(baseUri, about);
            graph.Assert(new Triple(article, schemaAbout, graph.CreateUriNode(new Uri(id))));
        }
    }

    private static void AddDocumentAuthors(
        Graph graph,
        INode article,
        INode schemaAuthor,
        IReadOnlyDictionary<string, object?> frontMatter,
        Uri baseUri)
    {
        foreach (var author in ReadAuthors(frontMatter))
        {
            var id = KnowledgeNaming.CreateEntityId(baseUri, author.Label);
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
            !Uri.TryCreate(assertion.ObjectId, UriKind.Absolute, out var objectUri) ||
            string.IsNullOrWhiteSpace(assertion.Predicate))
        {
            return;
        }

        var predicateUri = ResolvePredicate(assertion.Predicate);
        if (predicateUri is null)
        {
            return;
        }

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

    private static ILiteralNode CreateDateLiteral(Graph graph, string? value)
    {
        if (DateOnly.TryParse(value, out var dateOnly))
        {
            return graph.CreateLiteralNode(dateOnly.ToString(DotNetDateFormat, CultureInfo.InvariantCulture), XsdDateUri);
        }

        return graph.CreateLiteralNode(value ?? string.Empty);
    }

    private static bool TryGetString(
        IReadOnlyDictionary<string, object?> frontMatter,
        string key,
        [NotNullWhen(true)] out string? value)
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
