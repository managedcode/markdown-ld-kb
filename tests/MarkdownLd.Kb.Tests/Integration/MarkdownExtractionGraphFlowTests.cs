using ManagedCode.MarkdownLd.Kb.Extraction;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using Shouldly;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class MarkdownExtractionGraphFlowTests
{
    [Test]
    public void Markdown_extraction_flow_loads_valid_markdown_into_queryable_rdf_graph()
    {
        var graph = BuildMarkdownExtractionGraph("""
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
""");

        var executor = new SparqlQueryExecutor(graph);
        var metadata = executor.ExecuteReadOnly("""
PREFIX schema: <https://schema.org/>
SELECT ?summary ?keyword ?published WHERE {
  <https://kb.example/markdown-complex> schema:description ?summary ;
                                          schema:keywords ?keyword ;
                                          schema:datePublished ?published .
}
""");

        metadata.Rows.Single().Bindings["summary"].Value.ShouldBe("Markdown summary.");
        metadata.Rows.Single().Bindings["keyword"].Value.ShouldBe("graph");
        metadata.Rows.Single().Bindings["published"].Value.ShouldBe("2026-04-11");

        var author = executor.ExecuteRawReadOnly("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/markdown-complex> schema:author <urn:managedcode:markdown-ld-kb:entity/ada-lovelace> .
  <urn:managedcode:markdown-ld-kb:entity/sparql> schema:name "SPARQL" .
  <urn:managedcode:markdown-ld-kb:entity/tool> schema:sameAs <https://example.com/tool> .
}
""");
        author.Result.ShouldBeTrue();

        var search = new KnowledgeSearchService(graph);
        search.SearchArticles("sparql").Single().Id.AbsoluteUri.ShouldBe("https://kb.example/markdown-complex");
        search.SearchEntities("tool").Single().SameAs.Single().AbsoluteUri.ShouldBe("https://example.com/tool");
    }

    [Test]
    public void Markdown_extraction_flow_keeps_broken_front_matter_queryable_and_rejects_bad_sparql()
    {
        var graph = BuildMarkdownExtractionGraph("""
---
title: [unterminated
---
# Markdown Broken

Markdown Broken --mentions--> RDF
""", "content/markdown-broken.md");

        var executor = new SparqlQueryExecutor(graph);
        var title = executor.ExecuteReadOnly("""
PREFIX schema: <https://schema.org/>
SELECT ?article ?title WHERE {
  ?article a schema:Article ;
           schema:name ?title .
}
""");
        title.Rows.Single().Bindings["title"].Value.ShouldBe("Markdown Broken");

        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteReadOnly("INSERT DATA { <a> <b> <c> }"));
        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteRawReadOnly("INSERT DATA { <a> <b> <c> }"));
    }

    [Test]
    public void Markdown_extraction_flow_loads_scalar_and_blank_yaml_items_into_graph()
    {
        var graph = BuildMarkdownExtractionGraph("""
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
""");

        var executor = new SparqlQueryExecutor(graph);
        var ask = executor.ExecuteRawReadOnly("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/scalar-markdown> schema:name "123" ;
                                      schema:mentions <urn:managedcode:markdown-ld-kb:entity/654> .
}
""");
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
        if (value.StartsWith("schema:", StringComparison.Ordinal))
        {
            return new Uri(KbNamespaces.Schema + value["schema:".Length..]);
        }

        if (value.StartsWith("kb:", StringComparison.Ordinal))
        {
            return new Uri(KbNamespaces.Kb + value["kb:".Length..]);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : new Uri("urn:managedcode:markdown-ld-kb:test/" + Uri.EscapeDataString(value));
    }
}
