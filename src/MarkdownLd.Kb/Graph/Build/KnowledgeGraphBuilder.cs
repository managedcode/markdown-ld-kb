using System.Diagnostics.CodeAnalysis;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraphBuilder(Uri? baseUri = null, DocumentRdfMappingOptions? documentRdfMappingOptions = null)
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
        var context = new KnowledgeGraphMaterializationContext(graph);

        foreach (var document in documents)
        {
            AddDocument(context, document);
        }

        foreach (var entity in facts.Entities)
        {
            AddEntity(context, entity);
        }

        foreach (var assertion in facts.Assertions)
        {
            AddAssertion(context, assertion, buildOptions.IncludeAssertionReification);
        }

        _semanticLayerBuilder.Apply(graph, documents, buildOptions);
        return new KnowledgeGraph(graph, tokenIndex);
    }

    private static void RegisterNamespaces(IGraph graph)
    {
        KnowledgeGraphNamespaces.Register(graph);
    }

    private void AddDocument(KnowledgeGraphMaterializationContext context, MarkdownDocument document)
    {
        if (!KnowledgeGraphDocumentMaterialization.ShouldMaterialize(document))
        {
            return;
        }

        var graph = context.Graph;
        var prefixes = _documentRdfMapper.RegisterPrefixes(graph, document.FrontMatter);
        var article = context.UriNode(document.DocumentUri);
        var schemaArticle = context.UriNode(SchemaArticleUri);
        var schemaName = context.UriNode(SchemaNameUri);
        var schemaDescription = context.UriNode(SchemaDescriptionUri);
        var schemaDatePublished = context.UriNode(SchemaDatePublishedUri);
        var schemaDateModified = context.UriNode(SchemaDateModifiedUri);
        var schemaKeywords = context.UriNode(SchemaKeywordsUri);
        var schemaAbout = context.UriNode(SchemaAboutUri);
        var schemaAuthor = context.UriNode(SchemaAuthorUri);
        var kbEntryType = context.UriNode(KbEntryTypeUri);
        var kbSourceProject = context.UriNode(KbSourceProjectUri);
        var provWasDerivedFrom = context.UriNode(ProvWasDerivedFromUri);
        var rdfType = context.UriNode(RdfTypeUri);

        graph.Assert(new Triple(article, rdfType, schemaArticle));
        graph.Assert(new Triple(article, schemaName, context.LiteralNode(document.Title)));
        graph.Assert(new Triple(article, provWasDerivedFrom, article));

        AddDocumentAdditionalTypes(context, article, rdfType, document.FrontMatter);
        AddDocumentEntryMetadata(graph, article, kbEntryType, document.FrontMatter, EntryTypeKey, EntryTypeCamelKey);
        AddDocumentEntryMetadata(graph, article, kbSourceProject, document.FrontMatter, SourceProjectKey, SourceProjectCamelKey);
        AddDocumentDescription(context, article, schemaDescription, document.FrontMatter);
        AddDocumentDate(context, article, schemaDatePublished, document.FrontMatter, DatePublishedKey, DatePublishedCamelKey);
        AddDocumentDate(context, article, schemaDateModified, document.FrontMatter, DateModifiedKey, DateModifiedCamelKey);
        AddDocumentKeywords(graph, article, schemaKeywords, document.FrontMatter);
        AddDocumentAbout(context, article, schemaAbout, document.FrontMatter, _baseUri);
        AddDocumentAuthors(context, article, schemaAuthor, document.FrontMatter, _baseUri);
        _documentRdfMapper.Apply(graph, article, document.FrontMatter, prefixes);
    }

    private static void AddDocumentAdditionalTypes(
        KnowledgeGraphMaterializationContext context,
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
                context.Graph.Assert(new Triple(article, rdfType, context.UriNode(typeUri)));
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
        KnowledgeGraphMaterializationContext context,
        INode article,
        INode schemaDescription,
        IReadOnlyDictionary<string, object?> frontMatter)
    {
        if (TryGetString(frontMatter, SummaryKey, out var summary) ||
            TryGetString(frontMatter, DescriptionKey, out summary))
        {
            context.Graph.Assert(new Triple(article, schemaDescription, context.LiteralNode(summary)));
        }
    }

    private static void AddDocumentDate(
        KnowledgeGraphMaterializationContext context,
        INode article,
        INode schemaDate,
        IReadOnlyDictionary<string, object?> frontMatter,
        string snakeCaseKey,
        string camelCaseKey)
    {
        if (TryGetString(frontMatter, snakeCaseKey, out var date) ||
            TryGetString(frontMatter, camelCaseKey, out date))
        {
            context.Graph.Assert(new Triple(article, schemaDate, context.DateLiteral(date)));
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
        KnowledgeGraphMaterializationContext context,
        INode article,
        INode schemaAbout,
        IReadOnlyDictionary<string, object?> frontMatter,
        Uri baseUri)
    {
        foreach (var about in ReadStrings(frontMatter, AboutKey))
        {
            var id = KnowledgeNaming.CreateEntityId(baseUri, about);
            context.Graph.Assert(new Triple(article, schemaAbout, context.UriNode(new Uri(id))));
        }
    }

    private static void AddDocumentAuthors(
        KnowledgeGraphMaterializationContext context,
        INode article,
        INode schemaAuthor,
        IReadOnlyDictionary<string, object?> frontMatter,
        Uri baseUri)
    {
        foreach (var author in ReadAuthors(frontMatter))
        {
            var id = KnowledgeNaming.CreateEntityId(baseUri, author.Label);
            context.Graph.Assert(new Triple(article, schemaAuthor, context.UriNode(new Uri(id))));
        }
    }

    private void AddEntity(KnowledgeGraphMaterializationContext context, KnowledgeEntityFact entity)
    {
        var entityId = entity.Id ?? KnowledgeNaming.CreateEntityId(_baseUri, entity.Label);
        var graph = context.Graph;
        var subject = context.UriNode(new Uri(entityId));
        var rdfType = context.UriNode(RdfTypeUri);
        var schemaName = context.UriNode(SchemaNameUri);
        var schemaSameAs = context.UriNode(SchemaSameAsUri);
        var kbConfidence = context.UriNode(KbConfidenceUri);
        var provWasDerivedFrom = context.UriNode(ProvWasDerivedFromUri);

        graph.Assert(new Triple(subject, rdfType, context.UriNode(context.ResolveTypeUri(entity.Type))));
        graph.Assert(new Triple(subject, schemaName, context.LiteralNode(entity.Label)));
        graph.Assert(new Triple(subject, kbConfidence, context.ConfidenceLiteral(entity.Confidence)));
        foreach (var sameAs in entity.SameAs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            graph.Assert(new Triple(subject, schemaSameAs, context.UriOrLiteralNode(sameAs)));
        }

        AddSourceTriples(context, subject, provWasDerivedFrom, entity);
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
