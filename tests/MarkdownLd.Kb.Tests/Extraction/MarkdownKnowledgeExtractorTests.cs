using ManagedCode.MarkdownLd.Kb.Extraction;
using Shouldly;
using TUnit.Core;

namespace ManagedCode.MarkdownLd.Kb.Tests.Extraction;

public sealed class MarkdownKnowledgeExtractorTests
{
    private readonly MarkdownKnowledgeExtractor _extractor = new();

    [Test]
    public Task Extracts_metadata_entities_assertions_and_deduplicates_by_highest_confidence()
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
  - Managed Code
tags:
  - knowledge-graph
  - markdown
  - markdown
about:
  - label: Knowledge Graph
    sameAs: https://schema.org/KnowledgeGraph
  - RDF
entity_hints:
  - RDF
  - label: Resource Description Framework
    sameAs: https://www.w3.org/RDF/
  - SPARQL
---

# Markdown-LD Knowledge Bank

[[RDF|Resource Description Framework]] and [Resource Description Framework](https://www.w3.org/RDF/) appear in this article.
[SPARQL](https://www.wikidata.org/wiki/Q54872) is also referenced.
Alice --uses--> RDF
Alice --uses--> RDF

```text
[[Ignored Link]]
Foo --ignored--> Bar
```
""";

        var result = _extractor.Extract(markdown);

        result.Article.Id.ShouldBe("https://example.com/articles/markdown-ld-knowledge-bank");
        result.Article.Title.ShouldBe("Markdown-LD Knowledge Bank");
        result.Article.Summary.ShouldBe("Deterministic extraction from Markdown.");
        result.Article.DatePublished.ShouldBe("2026-04-04");
        result.Article.DateModified.ShouldBe("2026-04-05");
        result.Article.Tags.ShouldBe(new[] { "knowledge-graph", "markdown" });

        result.Article.Authors.Select(author => author.Name).ShouldBe(new[] { "Ada Lovelace", "Managed Code" });
        result.Article.About.Select(topic => topic.Label).ShouldBe(new[] { "Knowledge Graph", "RDF" });

        var rdf = result.Entities.Single(entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/rdf");
        rdf.Label.ShouldBe("RDF");
        rdf.Type.ShouldBe("schema:Thing");
        rdf.SameAs.Any(value => value == "Resource Description Framework").ShouldBe(true);
        rdf.SameAs.Any(value => value == "https://www.w3.org/RDF/").ShouldBe(true);

        var sparql = result.Entities.Single(entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/sparql");
        sparql.Label.ShouldBe("SPARQL");
        sparql.SameAs.ShouldBe(new[] { "https://www.wikidata.org/wiki/Q54872" });

        var ada = result.Entities.Single(entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/ada-lovelace");
        ada.Type.ShouldBe("schema:Person");

        var knowledgeGraph = result.Entities.Single(entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/knowledge-graph");
        knowledgeGraph.Label.ShouldBe("Knowledge Graph");

        result.Assertions
            .Single(assertion => assertion.SubjectId == result.Article.Id
                                 && assertion.Predicate == "schema:author"
                                 && assertion.ObjectId == ada.Id)
            .Confidence.ShouldBe(1.0);

        result.Assertions
            .Single(assertion => assertion.SubjectId == result.Article.Id
                                 && assertion.Predicate == "schema:about"
                                 && assertion.ObjectId == knowledgeGraph.Id)
            .Confidence.ShouldBe(1.0);

        result.Assertions
            .Single(assertion => assertion.SubjectId == result.Article.Id
                                 && assertion.Predicate == "schema:mentions"
                                 && assertion.ObjectId == rdf.Id)
            .Confidence.ShouldBe(0.95);

        result.Assertions.Any(assertion =>
            assertion.SubjectId == result.Article.Id &&
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == sparql.Id &&
            assertion.Confidence == 0.85).ShouldBe(true);

        result.Assertions
            .Single(assertion => assertion.SubjectId == "urn:managedcode:markdown-ld-kb:entity/alice"
                                 && assertion.Predicate == "schema:uses"
                                 && assertion.ObjectId == rdf.Id)
            .Confidence.ShouldBe(0.95);

        result.Entities.Any(entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/ignored-link").ShouldBe(false);
        result.Entities.Any(entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/foo").ShouldBe(false);

        return Task.CompletedTask;
    }

    [Test]
    public Task Falls_back_to_source_path_title_when_front_matter_is_missing_or_invalid()
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

        result.Article.Title.ShouldBe("Fallback Title");
        result.Article.Id.ShouldBe("urn:managedcode:markdown-ld-kb:article/fallback-title");
        result.Entities.Any(entity => entity.Id == "urn:managedcode:markdown-ld-kb:entity/rdf").ShouldBe(true);
        result.Assertions.Any(assertion =>
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/rdf").ShouldBe(true);

        return Task.CompletedTask;
    }

    [Test]
    public Task Builds_deterministic_ids_from_titles_and_labels()
    {
        MarkdownKnowledgeIds.BuildEntityId("JSON-LD").ShouldBe("urn:managedcode:markdown-ld-kb:entity/json-ld");
        MarkdownKnowledgeIds.BuildEntityId("C++ Programming").ShouldBe("urn:managedcode:markdown-ld-kb:entity/c-programming");
        MarkdownKnowledgeIds.BuildArticleId("Web 3.0").ShouldBe("urn:managedcode:markdown-ld-kb:article/web-30");
        MarkdownKnowledgeIds.BuildArticleId(null, "docs/MyCoolFile.md").ShouldBe("urn:managedcode:markdown-ld-kb:article/docs-mycoolfile");
        MarkdownKnowledgeIds.HumanizeLabel("markdown-ld-kb").ShouldBe("markdown ld kb");

        return Task.CompletedTask;
    }
}
