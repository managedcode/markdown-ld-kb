using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class KnowledgeGraphBuilder
{
    private readonly Uri _baseUri;

    public KnowledgeGraphBuilder(Uri? baseUri = null)
    {
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri("https://example.com/", UriKind.Absolute));
    }

    public KnowledgeGraph Build(IReadOnlyList<MarkdownDocument> documents, KnowledgeExtractionResult facts)
    {
        var graph = new Graph();
        RegisterNamespaces(graph);

        foreach (var document in documents)
        {
            AddDocument(graph, document, facts);
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
        graph.NamespaceMap.AddNamespace("schema", new Uri("https://schema.org/"));
        graph.NamespaceMap.AddNamespace("kb", new Uri("https://example.com/vocab/kb#"));
        graph.NamespaceMap.AddNamespace("prov", new Uri("http://www.w3.org/ns/prov#"));
        graph.NamespaceMap.AddNamespace("rdf", new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#"));
        graph.NamespaceMap.AddNamespace("xsd", new Uri("http://www.w3.org/2001/XMLSchema#"));
    }

    private void AddDocument(Graph graph, MarkdownDocument document, KnowledgeExtractionResult facts)
    {
        if (document.Sections.Count == 0 && document.FrontMatter.Count == 0 && string.IsNullOrWhiteSpace(document.Body))
        {
            return;
        }

        var article = graph.CreateUriNode(document.DocumentUri);
        var schemaArticle = graph.CreateUriNode(new Uri("https://schema.org/Article"));
        var schemaName = graph.CreateUriNode(new Uri("https://schema.org/name"));
        var schemaDescription = graph.CreateUriNode(new Uri("https://schema.org/description"));
        var schemaDatePublished = graph.CreateUriNode(new Uri("https://schema.org/datePublished"));
        var schemaDateModified = graph.CreateUriNode(new Uri("https://schema.org/dateModified"));
        var schemaKeywords = graph.CreateUriNode(new Uri("https://schema.org/keywords"));
        var schemaAbout = graph.CreateUriNode(new Uri("https://schema.org/about"));
        var schemaAuthor = graph.CreateUriNode(new Uri("https://schema.org/author"));
        var provWasDerivedFrom = graph.CreateUriNode(new Uri("http://www.w3.org/ns/prov#wasDerivedFrom"));

        graph.Assert(new Triple(article, graph.CreateUriNode(new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type")), schemaArticle));
        graph.Assert(new Triple(article, schemaName, graph.CreateLiteralNode(document.Title)));
        graph.Assert(new Triple(article, provWasDerivedFrom, graph.CreateUriNode(document.DocumentUri)));

        if (TryGetString(document.FrontMatter, "summary", out var summary) ||
            TryGetString(document.FrontMatter, "description", out summary))
        {
            graph.Assert(new Triple(article, schemaDescription, graph.CreateLiteralNode(summary ?? string.Empty)));
        }

        if (TryGetString(document.FrontMatter, "date_published", out var datePublished) ||
            TryGetString(document.FrontMatter, "datePublished", out datePublished))
        {
            graph.Assert(new Triple(article, schemaDatePublished, CreateDateLiteral(graph, datePublished)));
        }

        if (TryGetString(document.FrontMatter, "date_modified", out var dateModified) ||
            TryGetString(document.FrontMatter, "dateModified", out dateModified))
        {
            graph.Assert(new Triple(article, schemaDateModified, CreateDateLiteral(graph, dateModified)));
        }

        foreach (var tag in ReadStrings(document.FrontMatter, "tags").Concat(ReadStrings(document.FrontMatter, "keywords")).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            graph.Assert(new Triple(article, schemaKeywords, graph.CreateLiteralNode(tag)));
        }

        foreach (var about in ReadStrings(document.FrontMatter, "about"))
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
        var rdfType = graph.CreateUriNode(new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type"));
        var schemaName = graph.CreateUriNode(new Uri("https://schema.org/name"));
        var schemaSameAs = graph.CreateUriNode(new Uri("https://schema.org/sameAs"));

        graph.Assert(new Triple(subject, rdfType, graph.CreateUriNode(new Uri(NormalizeTypeUri(entity.Type)))));
        graph.Assert(new Triple(subject, schemaName, graph.CreateLiteralNode(entity.Label)));
        foreach (var sameAs in entity.SameAs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(sameAs, UriKind.Absolute, out var absolute))
            {
                graph.Assert(new Triple(subject, schemaSameAs, graph.CreateUriNode(absolute)));
            }
        }
    }

    private void AddAssertion(Graph graph, KnowledgeAssertionFact assertion)
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

        var confidenceNode = graph.CreateUriNode(new Uri("https://example.com/vocab/kb#confidence"));
        var provWasDerivedFrom = graph.CreateUriNode(new Uri("http://www.w3.org/ns/prov#wasDerivedFrom"));
        if (!string.IsNullOrWhiteSpace(assertion.Source))
        {
            graph.Assert(new Triple(graph.CreateUriNode(subjectUri), provWasDerivedFrom, graph.CreateUriNode(new Uri(assertion.Source))));
        }
    }

    private static Uri ResolvePredicate(string predicate)
    {
        if (Uri.TryCreate(predicate, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (predicate.Contains(':', StringComparison.Ordinal))
        {
            var prefix = predicate[..predicate.IndexOf(':')];
            var local = predicate[(predicate.IndexOf(':') + 1)..];
            return prefix.ToLowerInvariant() switch
            {
                "schema" => new Uri($"https://schema.org/{local}"),
                "kb" => new Uri($"https://example.com/vocab/kb#{local}"),
                "prov" => new Uri($"http://www.w3.org/ns/prov#{local}"),
                "rdf" => new Uri($"http://www.w3.org/1999/02/22-rdf-syntax-ns#{local}"),
                "xsd" => new Uri($"http://www.w3.org/2001/XMLSchema#{local}"),
                _ => new Uri($"https://example.com/vocab/kb#{KnowledgeNaming.Slugify(predicate)}"),
            };
        }

        return predicate.ToLowerInvariant() switch
        {
            "mentions" => new Uri("https://schema.org/mentions"),
            "about" => new Uri("https://schema.org/about"),
            "author" => new Uri("https://schema.org/author"),
            "creator" => new Uri("https://schema.org/creator"),
            "sameas" => new Uri("https://schema.org/sameAs"),
            _ => new Uri($"https://example.com/vocab/kb#{KnowledgeNaming.Slugify(predicate)}"),
        };
    }

    private static Uri NormalizeTypeUri(string type)
    {
        if (type.Contains(':', StringComparison.Ordinal))
        {
            return ResolvePredicate(type);
        }

        return new Uri($"https://schema.org/{KnowledgeNaming.Slugify(type)}");
    }

    private static ILiteralNode CreateDateLiteral(Graph graph, string? value)
    {
        if (DateOnly.TryParse(value, out var dateOnly))
        {
            return graph.CreateLiteralNode(dateOnly.ToString("yyyy-MM-dd"), new Uri("http://www.w3.org/2001/XMLSchema#date"));
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
            if (!entry.Key.Equals("author", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.Value is IEnumerable<object?> list && entry.Value is not string)
            {
                foreach (var item in list)
                {
                    if (item is IReadOnlyDictionary<string, object?> map)
                    {
                        yield return ((map.TryGetValue("label", out var label) ? label?.ToString() : null)
                            ?? (map.TryGetValue("name", out var name) ? name?.ToString() : null)
                            ?? string.Empty, map.TryGetValue("type", out var type) ? type?.ToString() : null);
                    }
                    else
                    {
                        var text = item?.ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            yield return (text, "schema:Person");
                        }
                    }
                }
            }
            else
            {
                var text = entry.Value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    yield return (text, "schema:Person");
                }
            }
        }
    }
}
