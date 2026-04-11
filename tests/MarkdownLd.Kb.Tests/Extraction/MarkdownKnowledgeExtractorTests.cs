using ManagedCode.MarkdownLd.Kb.Extraction;

namespace ManagedCode.MarkdownLd.Kb.Tests.Extraction;

public sealed class MarkdownKnowledgeExtractorTests
{
    private readonly MarkdownKnowledgeExtractor _extractor = new();

    [Fact]
    public void Extracts_front_matter_metadata_entities_mentions_and_arrow_assertions()
    {
        var markdown = """
---
title: Markdown-LD Knowledge Bank
summary: Deterministic extraction from Markdown.
canonical_url: https://example.com/articles/markdown-ld-knowledge-bank
date_published: 2026-04-04
date_modified: 2026-04-05
authors:
  - name: Ada Lovelace
    sameAs: https://example.com/authors/ada-lovelace
  - name: Managed Code
tags:
  - knowledge-graph
  - markdown
about:
  - label: Knowledge Graph
    sameAs: https://schema.org/KnowledgeGraph
entity_hints:
  - label: RDF
    sameAs: https://www.w3.org/RDF/
  - label: SPARQL
    sameAs: https://www.wikidata.org/wiki/Q54872
---

# Markdown-LD Knowledge Bank

[[RDF]] and [RDF](https://www.w3.org/RDF/) appear in this article.
[SPARQL](https://www.wikidata.org/wiki/Q54872) is also referenced.
Alice --uses--> RDF
Alice --uses--> RDF

```text
[[Ignored Link]]
Foo --ignored--> Bar
```
""";

        var result = _extractor.Extract(markdown);

        Assert.Equal("https://example.com/articles/markdown-ld-knowledge-bank", result.Article.Id);
        Assert.Equal("Markdown-LD Knowledge Bank", result.Article.Title);
        Assert.Equal("Deterministic extraction from Markdown.", result.Article.Summary);
        Assert.Equal("2026-04-04", result.Article.DatePublished);
        Assert.Equal("2026-04-05", result.Article.DateModified);

        Assert.Collection(result.Article.Authors,
            author => Assert.Equal("Ada Lovelace", author.Name),
            author => Assert.Equal("Managed Code", author.Name));

        Assert.Equal(new[] { "knowledge-graph", "markdown" }, result.Article.Tags);
        Assert.Single(result.Article.About);
        Assert.Equal("Knowledge Graph", result.Article.About[0].Label);
        Assert.Equal("https://schema.org/KnowledgeGraph", result.Article.About[0].SameAs);

        Assert.Contains(result.Entities, entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/rdf" && entity.SameAs.Contains("https://www.w3.org/RDF/"));
        Assert.Contains(result.Entities, entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/sparql" && entity.SameAs.Contains("https://www.wikidata.org/wiki/Q54872"));
        Assert.Contains(result.Entities, entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/ada-lovelace" && entity.Type == "schema:Person");
        Assert.Contains(result.Entities, entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/alice");

        var mentions = result.Assertions.Where(assertion => assertion.Predicate == "schema:mentions").ToArray();
        Assert.Contains(mentions, assertion => assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/rdf" && assertion.Confidence == 0.95);
        Assert.DoesNotContain(mentions, assertion => assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/rdf" && assertion.Confidence == 0.85);

        Assert.Contains(result.Assertions, assertion =>
            assertion.SubjectId == result.Article.Id &&
            assertion.Predicate == "schema:author" &&
            assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/ada-lovelace");

        Assert.Contains(result.Assertions, assertion =>
            assertion.SubjectId == result.Article.Id &&
            assertion.Predicate == "schema:about" &&
            assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/knowledge-graph");

        Assert.Contains(result.Assertions, assertion =>
            assertion.SubjectId == "urn:managedcode:markdown-ld-kb:entity/alice" &&
            assertion.Predicate == "schema:uses" &&
            assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/rdf" &&
            assertion.Confidence == 0.95);
    }

    [Fact]
    public void Canonicalizes_entities_by_slug_and_same_as()
    {
        var markdown = """
---
title: Identity Notes
entity_hints:
  - label: JSON-LD
    sameAs: https://example.com/jsonld
  - label: JSON LD
    sameAs: https://example.com/jsonld
---

[[JSON-LD]] and [JSON LD](https://example.com/jsonld) describe the same concept.
""";

        var result = _extractor.Extract(markdown);
        var entity = Assert.Single(result.Entities, candidate => candidate.Id == "urn:managedcode:markdown-ld-kb:entity/json-ld");
        Assert.Equal("JSON-LD", entity.Label);
        Assert.Equal(new[] { "https://example.com/jsonld" }, entity.SameAs);

        var mentions = result.Assertions.Where(assertion => assertion.Predicate == "schema:mentions" && assertion.ObjectId == entity.Id).ToArray();
        Assert.Single(mentions);
        Assert.Equal(0.95, mentions[0].Confidence);
    }

    [Fact]
    public void Extracts_body_content_even_when_front_matter_is_malformed()
    {
        var markdown = """
---
title: [broken
date_published: 2026-04-04
---

# Fallback Title

[[RDF]]
""";

        var result = _extractor.Extract(markdown, "docs/fallback-title.md");

        Assert.Equal("Fallback Title", result.Article.Title);
        Assert.Equal("urn:managedcode:markdown-ld-kb:article/fallback-title", result.Article.Id);
        Assert.Contains(result.Entities, entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/rdf");
        Assert.Contains(result.Assertions, assertion => assertion.Predicate == "schema:mentions" && assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/rdf");
    }

    [Fact]
    public void Builds_canonical_slug_ids_deterministically()
    {
        Assert.Equal("urn:managedcode:markdown-ld-kb:entity/json-ld", MarkdownKnowledgeIds.BuildEntityId("JSON-LD"));
        Assert.Equal("urn:managedcode:markdown-ld-kb:entity/c-programming", MarkdownKnowledgeIds.BuildEntityId("C++ Programming"));
        Assert.Equal("urn:managedcode:markdown-ld-kb:article/web-30", MarkdownKnowledgeIds.BuildArticleId("Web 3.0"));
    }
}
