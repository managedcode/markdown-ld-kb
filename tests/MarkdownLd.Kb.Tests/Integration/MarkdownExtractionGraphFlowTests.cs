using ManagedCode.MarkdownLd.Kb.Extraction;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using Shouldly;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class MarkdownExtractionGraphFlowTests
{
    private const string MarkdownComplex = """
---
title: Markdown Complex
summary: Markdown summary.
canonical_url: https://kb.example/markdown-complex
date_published: 2026-04-11
authors: Ada Lovelace
tags: graph
about: https://example.com/topics/RDF
entity_hints:
  - https://example.com/entities/SPARQL
  - name: Tool
    same_as: https://example.com/tool
    type: schema:SoftwareApplication
---
# Markdown Complex

Markdown Complex --mentions--> SPARQL
[Knowledge](https://example.com/knowledge)
[[RDF|Resource Description Framework]]
""";
    private const string MarkdownBroken = """
---
title: [unterminated
---
# Markdown Broken

Markdown Broken --mentions--> RDF
""";
    private const string MarkdownScalar = """
---
title: 123
summary: 456
canonical_url: https://kb.example/scalar-markdown
authors:
  -
  - 789
tags:
  -
  - {}
  - scalar
about:
  -
  - {}
  - 321
entity_hints:
  -
  - {}
  - 654
---
# 123

123 --mentions--> 654
""";
    private const string MetadataQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?summary ?keyword ?published WHERE {
  <https://kb.example/markdown-complex> schema:description ?summary ;
                                          schema:keywords ?keyword ;
                                          schema:datePublished ?published .
}
""";
    private const string AuthorAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/markdown-complex> schema:author <urn:managedcode:markdown-ld-kb:entity/ada-lovelace> .
  <urn:managedcode:markdown-ld-kb:entity/sparql> schema:name "SPARQL" .
  <urn:managedcode:markdown-ld-kb:entity/tool> schema:sameAs <https://example.com/tool> .
}
""";
    private const string BrokenTitleQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?article ?title WHERE {
  ?article a schema:Article ;
           schema:name ?title .
}
""";
    private const string ScalarAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/scalar-markdown> schema:name "123" ;
                                      schema:mentions <urn:managedcode:markdown-ld-kb:entity/654> .
}
""";
    private const string TitleBindingKey = "title";
    private const string SummaryBindingKey = "summary";
    private const string KeywordBindingKey = "keyword";
    private const string PublishedBindingKey = "published";
    private const string ArticleBindingKey = "article";
    private const string SchemaPrefix = "schema:";
    private const string KbPrefix = "kb:";
    private const string SchemaArticle = "schema:Article";
    private const string SchemaName = "schema:name";
    private const string InvalidInsertQuery = "INSERT DATA { <a> <b> <c> }";
    private const string TestUrnPrefix = "urn:managedcode:markdown-ld-kb:test/";
    private const string BrokenMarkdownSourcePath = "content/markdown-broken.md";
    private const string SearchArticlesTerm = "sparql";
    private const string SearchEntitiesTerm = "tool";
    private const string SummaryValue = "Markdown summary.";
    private const string GraphKeyword = "graph";
    private const string PublishedValue = "2026-04-11";
    private const string BrokenTitleValue = "Markdown Broken";
    private const string MarkdownComplexCanonicalUri = "https://kb.example/markdown-complex";
    private const string SearchEntitySameAsUri = "https://example.com/tool";

    [Test]
    public void Markdown_extraction_flow_loads_valid_markdown_into_queryable_rdf_graph()
    {
        var graph = BuildMarkdownExtractionGraph(MarkdownComplex);

        var executor = new SparqlQueryExecutor(graph);
        var metadata = executor.ExecuteReadOnly(MetadataQuery);

        metadata.Rows.Single().Bindings[SummaryBindingKey].Value.ShouldBe(SummaryValue);
        metadata.Rows.Single().Bindings[KeywordBindingKey].Value.ShouldBe(GraphKeyword);
        metadata.Rows.Single().Bindings[PublishedBindingKey].Value.ShouldBe(PublishedValue);

        var author = executor.ExecuteRawReadOnly(AuthorAskQuery);
        author.Result.ShouldBeTrue();

        var search = new KnowledgeSearchService(graph);
        search.SearchArticles(SearchArticlesTerm).Single().Id.AbsoluteUri.ShouldBe(MarkdownComplexCanonicalUri);
        search.SearchEntities(SearchEntitiesTerm).Single().SameAs.Single().AbsoluteUri.ShouldBe(SearchEntitySameAsUri);
    }

    [Test]
    public void Markdown_extraction_flow_keeps_broken_front_matter_queryable_and_rejects_bad_sparql()
    {
        var graph = BuildMarkdownExtractionGraph(MarkdownBroken, BrokenMarkdownSourcePath);

        var executor = new SparqlQueryExecutor(graph);
        var title = executor.ExecuteReadOnly(BrokenTitleQuery);
        title.Rows.Single().Bindings[TitleBindingKey].Value.ShouldBe(BrokenTitleValue);

        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteReadOnly(InvalidInsertQuery));
        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteRawReadOnly(InvalidInsertQuery));
    }

    [Test]
    public void Markdown_extraction_flow_loads_scalar_and_blank_yaml_items_into_graph()
    {
        var graph = BuildMarkdownExtractionGraph(MarkdownScalar);

        var executor = new SparqlQueryExecutor(graph);
        var ask = executor.ExecuteRawReadOnly(ScalarAskQuery);
        ask.Result.ShouldBeTrue();
    }

    private static Graph BuildMarkdownExtractionGraph(string markdown, string? sourcePath = null)
    {
        var extracted = new MarkdownKnowledgeExtractor().Extract(markdown, sourcePath);
        var article = new KnowledgeArticle(
            new Uri(extracted.Article.Id),
            extracted.Article.Title,
            ParseDate(extracted.Article.DatePublished),
            ParseDate(extracted.Article.DateModified),
            extracted.Article.Tags,
            extracted.Article.Summary);

        var entities = extracted.Entities
            .Select(entity => new KnowledgeEntity(
                new Uri(entity.Id),
                entity.Label,
                ExpandUri(entity.Type),
                entity.SameAs.Select(ExpandUri).ToArray()))
            .ToArray();

        var assertions = extracted.Assertions
            .Select(assertion => new KnowledgeAssertion(
                ExpandUri(assertion.SubjectId),
                ExpandUri(assertion.Predicate),
                ExpandUri(assertion.ObjectId),
                (decimal)assertion.Confidence))
            .ToArray();

        return new ManagedCode.MarkdownLd.Kb.Rdf.KnowledgeGraphBuilder()
            .Build(new KnowledgeGraphDocument(article, entities, assertions));
    }

    private static DateOnly? ParseDate(string? value)
    {
        return DateOnly.TryParse(value, out var date) ? date : null;
    }

    private static Uri ExpandUri(string value)
    {
        if (value.StartsWith(SchemaPrefix, StringComparison.Ordinal))
        {
            return new Uri(KbNamespaces.Schema + value[SchemaPrefix.Length..]);
        }

        if (value.StartsWith(KbPrefix, StringComparison.Ordinal))
        {
            return new Uri(KbNamespaces.Kb + value[KbPrefix.Length..]);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : new Uri(TestUrnPrefix + Uri.EscapeDataString(value));
    }
}
