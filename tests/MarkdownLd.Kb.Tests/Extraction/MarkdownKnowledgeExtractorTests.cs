using ManagedCode.MarkdownLd.Kb.Extraction;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Extraction;

public sealed class MarkdownKnowledgeExtractorTests
{
    private const string MarkdownWithFrontMatter = """
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

    private const string MarkdownWithBrokenFrontMatter = """
---
title: [broken
date_published: 2026-04-04
---

# Fallback Title

[[RDF]]
""";

    private const string SourcePathFallback = "docs/fallback-title.md";
    private const string SourcePathInput = "content/one.md";
    private const string ArticleId = "https://example.com/articles/markdown-ld-knowledge-bank";
    private const string ArticleTitle = "Markdown-LD Knowledge Bank";
    private const string ArticleSummary = "Deterministic extraction from Markdown.";
    private const string ArticlePublished = "2026-04-04";
    private const string ArticleModified = "2026-04-05";
    private const string AdaLovelace = "Ada Lovelace";
    private const string ManagedCode = "Managed Code";
    private const string KnowledgeGraphLabel = "Knowledge Graph";
    private const string RdfLabel = "RDF";
    private const string SparqlLabel = "SPARQL";
    private const string ResourceDescriptionFrameworkLabel = "Resource Description Framework";
    private const string SchemaThing = "schema:Thing";
    private const string SchemaPerson = "schema:Person";
    private const string SchemaAuthor = "schema:author";
    private const string SchemaAbout = "schema:about";
    private const string SchemaMentions = "schema:mentions";
    private const string SchemaUses = "schema:uses";
    private const string RdfId = "urn:managedcode:markdown-ld-kb:entity/rdf";
    private const string SparqlId = "urn:managedcode:markdown-ld-kb:entity/sparql";
    private const string AdaId = "urn:managedcode:markdown-ld-kb:entity/ada-lovelace";
    private const string KnowledgeGraphId = "urn:managedcode:markdown-ld-kb:entity/knowledge-graph";
    private const string IgnoredLinkId = "urn:managedcode:markdown-ld-kb:entity/ignored-link";
    private const string FooId = "urn:managedcode:markdown-ld-kb:entity/foo";
    private const string AliceId = "urn:managedcode:markdown-ld-kb:entity/alice";
    private const string Web3Title = "Web 3.0";
    private const string Web3Id = "urn:managedcode:markdown-ld-kb:article/web-30";
    private const string FallbackId = "urn:managedcode:markdown-ld-kb:article/fallback-title";
    private const string JsonLdId = "urn:managedcode:markdown-ld-kb:article/docs-mycoolfile";
    private const string MarkdownLdSlug = "markdown ld kb";
    private const string AdaSameAs = "https://example.com/authors/ada-lovelace";
    private const string RdfSameAs = "https://www.w3.org/RDF/";
    private const string SparqlSameAs = "https://www.wikidata.org/wiki/Q54872";
    private const string CanonicalUrl = "https://example.com/articles/markdown-ld-knowledge-bank";
    private const string FallbackTitle = "Fallback Title";
    private const string BodyText = "Body text.";
    private const string CppProgramming = "C++ Programming";
    private const string CppProgrammingId = "urn:managedcode:markdown-ld-kb:entity/c-programming";
    private const string MyCoolFilePath = "docs/MyCoolFile.md";
    private const string MarkdownKnowledgeBank = "markdown ld kb";

    private static readonly string[] ExpectedTags = ["knowledge-graph", "markdown"];
    private static readonly string[] ExpectedAuthors = [AdaLovelace, ManagedCode];
    private static readonly string[] ExpectedAbout = [KnowledgeGraphLabel, RdfLabel];
    private static readonly string[] ExpectedSameAs = [SparqlSameAs];
    private static readonly string[] ExpectedCanonicalAuthors = [AdaLovelace, "Grace Hopper"];
    private static readonly string[] ExpectedCanonicalTags = ["rdf", "markdown"];
    private static readonly string[] ExpectedCanonicalAbout = ["Graphs", RdfLabel];
    private static readonly string[] ExpectedFallbackHeadingPath = ["Heading"];

    private readonly MarkdownKnowledgeExtractor _extractor = new();

    [Test]
    public Task Extracts_metadata_entities_assertions_and_deduplicates_by_highest_confidence()
    {
        var result = _extractor.Extract(MarkdownWithFrontMatter);

        result.Article.Id.ShouldBe(ArticleId);
        result.Article.Title.ShouldBe(ArticleTitle);
        result.Article.Summary.ShouldBe(ArticleSummary);
        result.Article.DatePublished.ShouldBe(ArticlePublished);
        result.Article.DateModified.ShouldBe(ArticleModified);
        result.Article.Tags.ShouldBe(ExpectedTags);

        result.Article.Authors.Select(author => author.Name).ShouldBe(ExpectedAuthors);
        result.Article.About.Select(topic => topic.Label).ShouldBe(ExpectedAbout);

        var rdf = result.Entities.Single(entity => entity.Id == RdfId);
        rdf.Label.ShouldBe(RdfLabel);
        rdf.Type.ShouldBe(SchemaThing);
        rdf.SameAs.Any(value => value == ResourceDescriptionFrameworkLabel).ShouldBe(true);
        rdf.SameAs.Any(value => value == RdfSameAs).ShouldBe(true);

        var sparql = result.Entities.Single(entity => entity.Id == SparqlId);
        sparql.Label.ShouldBe(SparqlLabel);
        sparql.SameAs.ShouldBe(ExpectedSameAs);

        var ada = result.Entities.Single(entity => entity.Id == AdaId);
        ada.Type.ShouldBe(SchemaPerson);

        var knowledgeGraph = result.Entities.Single(entity => entity.Id == KnowledgeGraphId);
        knowledgeGraph.Label.ShouldBe(KnowledgeGraphLabel);

        result.Assertions
            .Single(assertion => assertion.SubjectId == result.Article.Id
                                 && assertion.Predicate == SchemaAuthor
                                 && assertion.ObjectId == AdaId)
            .Confidence.ShouldBe(1.0);

        result.Assertions
            .Single(assertion => assertion.SubjectId == result.Article.Id
                                 && assertion.Predicate == SchemaAbout
                                 && assertion.ObjectId == KnowledgeGraphId)
            .Confidence.ShouldBe(1.0);

        result.Assertions
            .Single(assertion => assertion.SubjectId == result.Article.Id
                                 && assertion.Predicate == SchemaMentions
                                 && assertion.ObjectId == RdfId)
            .Confidence.ShouldBe(0.95);

        result.Assertions.Any(assertion =>
            assertion.SubjectId == result.Article.Id &&
            assertion.Predicate == SchemaMentions &&
            assertion.ObjectId == SparqlId &&
            assertion.Confidence == 0.85).ShouldBe(true);

        result.Assertions
            .Single(assertion => assertion.SubjectId == AliceId
                                 && assertion.Predicate == SchemaUses
                                 && assertion.ObjectId == RdfId)
            .Confidence.ShouldBe(0.95);

        result.Entities.Any(entity => entity.Id == IgnoredLinkId).ShouldBe(false);
        result.Entities.Any(entity => entity.Id == FooId).ShouldBe(false);

        return Task.CompletedTask;
    }

    [Test]
    public Task Falls_back_to_source_path_title_when_front_matter_is_missing_or_invalid()
    {
        var result = _extractor.Extract(MarkdownWithBrokenFrontMatter, SourcePathFallback);

        result.Article.Title.ShouldBe(FallbackTitle);
        result.Article.Id.ShouldBe(FallbackId);
        result.Entities.Any(entity => entity.Id == RdfId).ShouldBe(true);
        result.Assertions.Any(assertion =>
            assertion.Predicate == SchemaMentions &&
            assertion.ObjectId == RdfId).ShouldBe(true);

        return Task.CompletedTask;
    }

    [Test]
    public Task Builds_deterministic_ids_from_titles_and_labels()
    {
        MarkdownKnowledgeIds.BuildEntityId(RdfLabel).ShouldBe(RdfId);
        MarkdownKnowledgeIds.BuildEntityId(CppProgramming).ShouldBe(CppProgrammingId);
        MarkdownKnowledgeIds.BuildArticleId(Web3Title).ShouldBe(Web3Id);
        MarkdownKnowledgeIds.BuildArticleId(null, MyCoolFilePath).ShouldBe(JsonLdId);
        MarkdownKnowledgeIds.HumanizeLabel(MarkdownLdSlug).ShouldBe(MarkdownKnowledgeBank);

        return Task.CompletedTask;
    }
}
