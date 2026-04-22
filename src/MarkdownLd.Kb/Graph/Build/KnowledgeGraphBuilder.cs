using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeGraphBuilder(Uri? baseUri = null, DocumentRdfMappingOptions? documentRdfMappingOptions = null)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));
    private readonly DocumentRdfFrontMatterMapper _documentRdfMapper = new(documentRdfMappingOptions);
    private readonly KnowledgeGraphSemanticLayerBuilder _semanticLayerBuilder = new(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));

    public KnowledgeGraph Build(
        IReadOnlyList<MarkdownDocument> documents,
        KnowledgeExtractionResult facts,
        KnowledgeGraphBuildOptions buildOptions,
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

        _semanticLayerBuilder.Apply(graph, documents, buildOptions);
        return new KnowledgeGraph(graph, tokenIndex);
    }

    private static void RegisterNamespaces(IGraph graph)
    {
        KnowledgeGraphNamespaces.Register(graph);
    }

    private void AddDocument(Graph graph, MarkdownDocument document)
    {
        if (document.Sections.Count == 0 && document.FrontMatter.Count == 0 && string.IsNullOrWhiteSpace(document.Body))
        {
            return;
        }

        var prefixes = _documentRdfMapper.RegisterPrefixes(graph, document.FrontMatter);
        var article = graph.CreateUriNode(document.DocumentUri);
        var schemaArticle = graph.CreateUriNode(SchemaArticleUri);
        var schemaName = graph.CreateUriNode(SchemaNameUri);
        var schemaDescription = graph.CreateUriNode(SchemaDescriptionUri);
        var schemaDatePublished = graph.CreateUriNode(SchemaDatePublishedUri);
        var schemaDateModified = graph.CreateUriNode(SchemaDateModifiedUri);
        var schemaKeywords = graph.CreateUriNode(SchemaKeywordsUri);
        var schemaAbout = graph.CreateUriNode(SchemaAboutUri);
        var schemaAuthor = graph.CreateUriNode(SchemaAuthorUri);
        var kbEntryType = graph.CreateUriNode(KbEntryTypeUri);
        var kbSourceProject = graph.CreateUriNode(KbSourceProjectUri);
        var provWasDerivedFrom = graph.CreateUriNode(ProvWasDerivedFromUri);
        var rdfType = graph.CreateUriNode(RdfTypeUri);

        graph.Assert(new Triple(article, rdfType, schemaArticle));
        graph.Assert(new Triple(article, schemaName, graph.CreateLiteralNode(document.Title)));
        graph.Assert(new Triple(article, provWasDerivedFrom, graph.CreateUriNode(document.DocumentUri)));

        AddDocumentAdditionalTypes(graph, article, rdfType, document.FrontMatter);
        AddDocumentEntryMetadata(graph, article, kbEntryType, document.FrontMatter, EntryTypeKey, EntryTypeCamelKey);
        AddDocumentEntryMetadata(graph, article, kbSourceProject, document.FrontMatter, SourceProjectKey, SourceProjectCamelKey);
        AddDocumentDescription(graph, article, schemaDescription, document.FrontMatter);
        AddDocumentDate(graph, article, schemaDatePublished, document.FrontMatter, DatePublishedKey, DatePublishedCamelKey);
        AddDocumentDate(graph, article, schemaDateModified, document.FrontMatter, DateModifiedKey, DateModifiedCamelKey);
        AddDocumentKeywords(graph, article, schemaKeywords, document.FrontMatter);
        AddDocumentAbout(graph, article, schemaAbout, document.FrontMatter, _baseUri);
        AddDocumentAuthors(graph, article, schemaAuthor, document.FrontMatter, _baseUri);
        _documentRdfMapper.Apply(graph, article, document.FrontMatter, prefixes);
    }

    private static void AddDocumentAdditionalTypes(
        Graph graph,
        INode article,
        INode rdfType,
        IReadOnlyDictionary<string, object?> frontMatter)
    {
        foreach (var entryType in ReadStrings(frontMatter, EntryTypeKey)
                     .Concat(ReadStrings(frontMatter, EntryTypeCamelKey))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var typeUri = ResolveArticleType(entryType);
            if (typeUri is not null)
            {
                graph.Assert(new Triple(article, rdfType, graph.CreateUriNode(typeUri)));
            }
        }
    }

    private static void AddDocumentEntryMetadata(
        Graph graph,
        INode article,
        INode predicate,
        IReadOnlyDictionary<string, object?> frontMatter,
        string snakeCaseKey,
        string camelCaseKey)
    {
        foreach (var value in ReadStrings(frontMatter, snakeCaseKey)
                     .Concat(ReadStrings(frontMatter, camelCaseKey))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            graph.Assert(new Triple(article, predicate, graph.CreateLiteralNode(value)));
        }
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
        var kbConfidence = graph.CreateUriNode(KbConfidenceUri);
        var provWasDerivedFrom = graph.CreateUriNode(ProvWasDerivedFromUri);

        graph.Assert(new Triple(subject, rdfType, graph.CreateUriNode(NormalizeTypeUri(entity.Type))));
        graph.Assert(new Triple(subject, schemaName, graph.CreateLiteralNode(entity.Label)));
        graph.Assert(new Triple(subject, kbConfidence, CreateConfidenceLiteral(graph, entity.Confidence)));
        foreach (var sameAs in entity.SameAs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            graph.Assert(new Triple(subject, schemaSameAs, CreateUriOrLiteralNode(graph, sameAs)));
        }

        AddSourceTriple(graph, subject, provWasDerivedFrom, entity.Source);
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
        AddReifiedAssertion(graph, subjectUri, predicateUri, objectUri, assertion);

        if (!string.IsNullOrWhiteSpace(assertion.Source))
        {
            AddSourceTriple(graph, graph.CreateUriNode(subjectUri), graph.CreateUriNode(ProvWasDerivedFromUri), assertion.Source);
        }
    }

    private static void AddReifiedAssertion(
        Graph graph,
        Uri subjectUri,
        Uri predicateUri,
        Uri objectUri,
        KnowledgeAssertionFact assertion)
    {
        var statement = graph.CreateBlankNode();
        graph.Assert(new Triple(statement, graph.CreateUriNode(RdfTypeUri), graph.CreateUriNode(RdfStatementUri)));
        graph.Assert(new Triple(statement, graph.CreateUriNode(RdfSubjectUri), graph.CreateUriNode(subjectUri)));
        graph.Assert(new Triple(statement, graph.CreateUriNode(RdfPredicateUri), graph.CreateUriNode(predicateUri)));
        graph.Assert(new Triple(statement, graph.CreateUriNode(RdfObjectUri), graph.CreateUriNode(objectUri)));
        graph.Assert(new Triple(statement, graph.CreateUriNode(KbConfidenceUri), CreateConfidenceLiteral(graph, assertion.Confidence)));
        AddSourceTriple(graph, statement, graph.CreateUriNode(ProvWasDerivedFromUri), assertion.Source);
    }

    private static void AddSourceTriple(Graph graph, INode subject, INode predicate, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        graph.Assert(new Triple(subject, predicate, CreateUriOrLiteralNode(graph, source)));
    }

    private static INode CreateUriOrLiteralNode(Graph graph, string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            ? graph.CreateUriNode(absolute)
            : graph.CreateLiteralNode(value);
    }

    private static ILiteralNode CreateConfidenceLiteral(Graph graph, double confidence)
    {
        return graph.CreateLiteralNode(confidence.ToString(CultureInfo.InvariantCulture), XsdDecimalUri);
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

    private static Uri? ResolveArticleType(string entryType)
    {
        if (string.IsNullOrWhiteSpace(entryType))
        {
            return null;
        }

        var trimmed = entryType.Trim();
        if (string.Equals(trimmed, SchemaTechArticleTypeText, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, SchemaTechArticleText, StringComparison.OrdinalIgnoreCase))
        {
            return SchemaTechArticleUri;
        }

        if (string.Equals(trimmed, SchemaScholarlyArticleTypeText, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, SchemaScholarlyArticleText, StringComparison.OrdinalIgnoreCase))
        {
            return SchemaScholarlyArticleUri;
        }

        if (string.Equals(trimmed, SchemaBlogPostingTypeText, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, SchemaBlogPostingText, StringComparison.OrdinalIgnoreCase))
        {
            return SchemaBlogPostingUri;
        }

        var normalized = NormalizeArticleTypeToken(trimmed);
        if (normalized == KnowledgeNaming.Slugify(SchemaTechArticleUri.Segments[^1]))
        {
            return SchemaTechArticleUri;
        }

        if (normalized == KnowledgeNaming.Slugify(SchemaScholarlyArticleUri.Segments[^1]))
        {
            return SchemaScholarlyArticleUri;
        }

        return normalized == KnowledgeNaming.Slugify(SchemaBlogPostingUri.Segments[^1])
            ? SchemaBlogPostingUri
            : null;
    }

    private static string NormalizeArticleTypeToken(string entryType)
    {
        var normalized = entryType.Trim();
        if (normalized.StartsWith(SchemaPrefix + Colon, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(SchemaPrefix.Length + Colon.Length)..];
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absolute) &&
            absolute.AbsoluteUri.StartsWith(SchemaNamespaceText, StringComparison.Ordinal))
        {
            normalized = absolute.Segments[^1];
        }

        return KnowledgeNaming.Slugify(normalized);
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
