using ManagedCode.MarkdownLd.Kb.Extraction;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Rdf;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;
using System.Text.Json;
using RootMarkdownDocumentParser = ManagedCode.MarkdownLd.Kb.Parsing.MarkdownDocumentParser;
using RootMarkdownDocumentSource = ManagedCode.MarkdownLd.Kb.MarkdownDocumentSource;
using RootMarkdownLinkKind = ManagedCode.MarkdownLd.Kb.MarkdownLinkKind;
using RootMarkdownParsingOptions = ManagedCode.MarkdownLd.Kb.MarkdownParsingOptions;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class AdditionalKnowledgeBankFlowTests
{
    [Test]
    public void Root_parser_handles_front_matter_links_loose_text_and_nested_heading_flow()
    {
        var markdown = "\uFEFF" + """
---
title: Root Parser Article
canonicalUrl: notes/canonical-relative
description: Root parser summary.
datePublished: 2026-04-06
dateModified: 2026-04-07
author:
  - name: Ada Lovelace
tags: alpha, beta
about:
  - label: Knowledge Graph
  - value: RDF
entityHints:
  - RDF
  - value: LLM
    sameAs:
      - https://example.com/llm
---
Intro before heading with [[RDF|Resource Description Framework]], ![Diagram](./diagram.png "Diagram title"), [Guide](./guide.markdown#part "Guide title"), and [Mail](mailto:team@example.com).

# Top

Top body.

### Deep

Deep body.

## Sibling

Sibling body.
""";

        var parser = new RootMarkdownDocumentParser();
        var document = parser.Parse(
            new RootMarkdownDocumentSource(markdown, "content/ignored.md", "not a uri"),
            new RootMarkdownParsingOptions { ChunkTokenTarget = 2 });

        document.DocumentId.ShouldBe("notes/canonical-relative");
        document.BaseUri!.AbsoluteUri.ShouldBe("https://example.com/");
        document.FrontMatter.Summary.ShouldBe("Root parser summary.");
        document.FrontMatter.Authors.ShouldBe(new[] { "Ada Lovelace" });
        document.FrontMatter.Tags.ShouldBe(new[] { "alpha", "beta" });
        document.FrontMatter.About.ShouldBe(new[] { "Knowledge Graph", "RDF" });
        document.FrontMatter.EntityHints.Select(hint => hint.Label).ShouldBe(new[] { "RDF", "LLM" });

        document.Sections.Count.ShouldBe(4);
        document.Sections[0].HeadingLevel.ShouldBe(0);
        document.Sections[1].HeadingPath.ShouldBe(new[] { "Top" });
        document.Sections[2].HeadingPath.ShouldBe(new[] { "Top", "Deep" });
        document.Sections[3].HeadingPath.ShouldBe(new[] { "Top", "Sibling" });
        document.Chunks.Count.ShouldBeGreaterThanOrEqualTo(4);

        var wiki = document.Links.Single(link => link.Kind == RootMarkdownLinkKind.WikiLink);
        wiki.Target.ShouldBe("RDF");
        wiki.DisplayText.ShouldBe("Resource Description Framework");

        var image = document.Links.Single(link => link.IsImage);
        image.Title.ShouldBe("Diagram title");
        image.ResolvedTarget.ShouldBe("https://example.com/diagram.png");

        var guide = document.Links.Single(link => link.DisplayText == "Guide");
        guide.IsDocumentLink.ShouldBeTrue();
        guide.Title.ShouldBe("Guide title");
        guide.ResolvedTarget.ShouldBe("https://example.com/guide/#part");

        var mail = document.Links.Single(link => link.DisplayText == "Mail");
        mail.IsExternal.ShouldBeTrue();
    }

    [Test]
    public void Root_parser_handles_empty_unclosed_and_static_identity_flows()
    {
        var parser = new RootMarkdownDocumentParser();
        var empty = parser.Parse(new RootMarkdownDocumentSource(string.Empty, null, "not a uri"));
        empty.BaseUri!.AbsoluteUri.ShouldBe("https://example.com/");
        empty.DocumentId.ShouldStartWith("urn:markdown-ld-kb:document/");
        empty.Sections.ShouldBeEmpty();
        empty.Chunks.ShouldBeEmpty();

        RootMarkdownDocumentParser.DocumentIdFromPath(string.Empty, "https://kb.example")
            .ShouldBe("urn:markdown-ld-kb:document/untitled");
        RootMarkdownDocumentParser.NormalizeWhitespace("  Alpha\r\n  Beta\tGamma  ")
            .ShouldBe("Alpha Beta Gamma");
        RootMarkdownDocumentParser.ComputeChunkId("Alpha")
            .ShouldNotBe(RootMarkdownDocumentParser.ComputeChunkId("Beta"));

        var unclosed = parser.Parse(new RootMarkdownDocumentSource("""
---
title: Not Closed

# Body Is Still Source
""", null, "https://kb.example"));

        unclosed.FrontMatter.Values.ShouldBeEmpty();
        unclosed.BodyMarkdown.ShouldContain("Not Closed");
        unclosed.DocumentId.ShouldStartWith("urn:markdown-ld-kb:document/");
    }

    [Test]
    public void Root_parser_handles_scalar_and_typed_yaml_values_through_document_flow()
    {
        var parser = new RootMarkdownDocumentParser();
        var document = parser.Parse(new RootMarkdownDocumentSource("""
---
description: Scalar summary.
date_published: 2026-04-10T12:13:14Z
dateModified: 2026-04-11
authors:
  - value: Value Author
  - 123
tags:
  - true
  - 42
about:
  - value: Value Topic
entityHints:
  - 123
  - name: Value Hint
  - label:
---
Body without headings.
""", "content/scalar-values.mdx", "https://kb.example/root"));

        document.DocumentId.ShouldBe("https://kb.example/root/scalar-values/");
        document.FrontMatter.Title.ShouldBeNull();
        document.FrontMatter.Summary.ShouldBe("Scalar summary.");
        document.FrontMatter.Authors.ShouldContain("Value Author");
        document.FrontMatter.Authors.ShouldContain("123");
        document.FrontMatter.Tags.ShouldContain("true");
        document.FrontMatter.Tags.ShouldContain("42");
        document.FrontMatter.About.ShouldBe(new[] { "Value Topic" });
        document.FrontMatter.EntityHints.Any(hint => hint.Label == "123").ShouldBeTrue();
        document.FrontMatter.EntityHints.Any(hint => hint.Label == "Value Hint").ShouldBeTrue();
        document.Sections.Single().HeadingLevel.ShouldBe(0);
    }

    [Test]
    public void Extraction_flow_handles_url_labels_scalar_values_code_fences_and_canonical_ids()
    {
        var extractor = new MarkdownKnowledgeExtractor();
        var empty = extractor.Extract(string.Empty);
        empty.Article.Title.ShouldBe("Untitled");
        empty.Article.Id.ShouldBe("urn:managedcode:markdown-ld-kb:article/untitled");
        empty.Entities.ShouldBeEmpty();

        var result = extractor.Extract("""
---
title: Rich Extraction
canonical_url: https://kb.example/articles/rich
summary: Rich summary.
date_published: 2026-04-06
date_modified: 2026-04-07
authors:
  - https://example.com/people/Ada_Lovelace
  - name: Managed Code
    same_as: https://example.com/orgs/managed-code
    type: schema:Organization
tags:
  - graph
  - Graph
  - 42
about:
  - https://example.com/topics/Resource_Description_Framework
  - label: Knowledge Graph
    same_as: https://example.com/topics/kg
entity_hints:
  - https://example.com/entities/SPARQL
  - name: Large Language Model
    same_as: https://example.com/entities/llm
    type: schema:SoftwareApplication
---
# Markdown Heading Does Not Override

[[RDF|Resource Description Framework]] and [SPARQL](https://www.wikidata.org/wiki/Q54872).
Rich Extraction --http://example.com/predicate/relatedTo--> Large Language Model.

~~~
[[Ignored]]
Foo --mentions--> Bar
~~~
""");

        result.Article.Id.ShouldBe("https://kb.example/articles/rich");
        result.Article.Authors.Select(author => author.Name).ShouldContain("Ada Lovelace");
        result.Article.Tags.ShouldBe(new[] { "graph", "42" });
        result.Entities.Any(entity => entity.Label == "Large Language Model" && entity.Type == "schema:SoftwareApplication").ShouldBeTrue();
        result.Entities.Any(entity => entity.Label == "Resource Description Framework").ShouldBeTrue();
        result.Entities.Any(entity => entity.Label == "Ignored").ShouldBeFalse();
        result.Assertions.Any(assertion =>
            assertion.SubjectId == result.Article.Id &&
            assertion.Predicate == "http://example.com/predicate/relatedTo" &&
            assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/large-language-model").ShouldBeTrue();
    }

    [Test]
    public void Extraction_flow_handles_missing_front_matter_unclosed_yaml_scalar_sequences_and_id_helpers()
    {
        var extractor = new MarkdownKnowledgeExtractor();
        var noFrontMatter = extractor.Extract("""
# Source Path Title

[ ](https://example.com/blank-label)
[] (https://example.com/not-a-link)
Source Path Title --mentions--> Target
""", "docs/SourcePathTitle.md");

        noFrontMatter.Article.Title.ShouldBe("Source Path Title");
        noFrontMatter.Assertions.Any(assertion =>
            assertion.SubjectId == noFrontMatter.Article.Id &&
            assertion.ObjectId == "urn:managedcode:markdown-ld-kb:entity/target").ShouldBeTrue();

        var unclosed = extractor.Extract("""
---
title: Missing Fence
""", "docs/missing-fence.md");

        unclosed.Article.Title.ShouldBe("missing fence");
        unclosed.Article.Id.ShouldBe("urn:managedcode:markdown-ld-kb:article/missing-fence");

        var scalar = extractor.Extract("""
---
title: Scalar Extraction
authors: Ada Lovelace
tags: graph
about: RDF
entity_hints: SPARQL
---
Scalar Extraction --uses--> SPARQL
""");

        scalar.Article.Authors.Any(author => author.Name == "Ada Lovelace").ShouldBeTrue();
        scalar.Article.Tags.ShouldBe(new[] { "graph" });
        scalar.Article.About.Single().Label.ShouldBe("RDF");
        scalar.Entities.Any(entity => entity.Label == "SPARQL").ShouldBeTrue();

        MarkdownKnowledgeIds.Slugify(string.Empty).ShouldBe("item");
        MarkdownKnowledgeIds.Slugify("***").ShouldBe("item");
        MarkdownKnowledgeIds.HumanizeLabel(string.Empty).ShouldBeEmpty();
        MarkdownKnowledgeIds.BuildArticleId(null, null).ShouldBe("urn:managedcode:markdown-ld-kb:article/untitled");
    }

    [Test]
    public async Task Chat_extraction_flow_filters_null_blank_and_duplicate_structured_output()
    {
        var payload = """
{
  "entities": [
    null,
    {
      "id": "ARTICLE",
      "type": "",
      "label": "  ",
      "sameAs": []
    },
    {
      "id": "",
      "type": "",
      "label": "RDF Core",
      "sameAs": [
        "https://example.com/rdf",
        "https://example.com/rdf",
        ""
      ]
    }
  ],
  "assertions": [
    null,
    {
      "s": "",
      "p": "",
      "o": ""
    },
    {
      "s": "ARTICLE",
      "p": "schema:mentions",
      "o": "https://example.com/id/rdf-core",
      "confidence": -4,
      "source": ""
    }
  ]
}
""";

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var chatClient = new TestChatClient((_, _) => payload);
        var extractor = new ChatClientKnowledgeFactExtractor(
            chatClient,
            "https://kb.example/",
            new ChatOptions { Temperature = 0.8f },
            serializerOptions);

        var request = new KnowledgeFactExtractionRequest(
            "https://kb.example/article/",
            "chunk-edge",
            "RDF Core appears here.");

        var result = await extractor.ExtractAsync(request);

        result.Entities.Single().Id.ShouldBe("https://kb.example/id/rdf-core");
        result.Entities.Single().Type.ShouldBe("schema:Thing");
        result.Entities.Single().SameAs.ShouldBe(new[] { "https://example.com/rdf" });
        result.Assertions.Single().SubjectId.ShouldBe(request.DocumentId);
        result.Assertions.Single().Confidence.ShouldBe(0);
        result.Assertions.Single().Source.ShouldBe(request.ChunkSourceUri);
        chatClient.LastOptions!.Temperature.ShouldBe(0);
    }

    [Test]
    public async Task Pipeline_flow_materializes_front_matter_predicates_dates_and_sources_into_queryable_graph()
    {
        var source = new MarkdownSourceDocument(
            "content/pipeline-flow.md",
            """
---
description: Pipeline summary.
datePublished: 2026-04-08
date_modified: not-a-date
tags:
  - graph
keywords: search
about:
  - RDF
author:
  - label: Ada Lovelace
    type: schema:Person
  - Managed Code
entity_hints:
  - label: Pipeline Tool
    type: schema:SoftwareApplication
    sameAs:
      - https://example.com/tools/pipeline
---
Intro without a heading links to [Relative](./relative.md) and [Absolute](https://example.com/id/absolute).

# Pipeline Article

article --creator--> https://example.com/id/absolute
this article --kb:relatedTo--> Pipeline Tool
Pipeline Tool --prov:wasDerivedFrom--> https://example.com/source/tool
Pipeline Tool --rdf:type--> https://schema.org/SoftwareApplication
Pipeline Tool --xsd:integer--> https://example.com/id/number
-- --bad--> Thing
""");

        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
        var result = await pipeline.BuildAsync([source]);

        result.Documents[0].Title.ShouldBe("Pipeline Article");
        result.Facts.Entities.Any(entity => entity.Label == "Pipeline Tool" && entity.Type == "schema:SoftwareApplication").ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == "schema:creator").ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == "kb:relatedTo").ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == "prov:wasDerivedFrom").ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == "rdf:type").ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == "xsd:integer").ShouldBeTrue();

        var dateResult = await result.Graph.ExecuteSelectAsync("""
PREFIX schema: <https://schema.org/>
SELECT ?published ?modified WHERE {
  <https://kb.example/pipeline-flow/> schema:datePublished ?published ;
                                       schema:dateModified ?modified .
}
""");
        dateResult.Rows.Count.ShouldBe(1);
        dateResult.Rows[0].Values["published"].ShouldBe("2026-04-08");
        dateResult.Rows[0].Values["modified"].ShouldBe("not-a-date");
    }

    [Test]
    public void Pipeline_naming_and_query_safety_cover_readonly_and_normalization_flows()
    {
        KnowledgeNaming.Slugify(string.Empty).ShouldBe("item");
        KnowledgeNaming.Slugify("Český Graph!!").ShouldBe("cesky-graph");
        KnowledgeNaming.NormalizeBaseUri(new Uri("https://kb.example/root")).AbsoluteUri.ShouldBe("https://kb.example/root/");
        KnowledgeNaming.CreateDocumentUri(new Uri("https://kb.example/"), string.Empty).AbsoluteUri.ShouldBe("https://kb.example/document/");
        KnowledgeNaming.NormalizeSourcePath("/content/folder\\note.md").ShouldBe("folder/note.md");

        KnowledgeNaming.IsReadOnlySparql(string.Empty, out var emptyReason).ShouldBeFalse();
        emptyReason.ShouldBe("SPARQL query is empty");
        KnowledgeNaming.IsReadOnlySparql("INSERT DATA { <a> <b> <c> }", out var mutatingReason).ShouldBeFalse();
        mutatingReason!.ShouldContain("INSERT");
        KnowledgeNaming.IsReadOnlySparql("SELECT ?s WHERE { ?s ?p ?o }", out var readOnlyReason).ShouldBeTrue();
        readOnlyReason.ShouldBeNull();

        KnowledgeNaming.NormalizePredicate("https://schema.org/about").ShouldBe("schema:about");
        KnowledgeNaming.NormalizePredicate("https://schema.org/author").ShouldBe("schema:author");
        KnowledgeNaming.NormalizePredicate("https://schema.org/creator").ShouldBe("schema:creator");
        KnowledgeNaming.NormalizePredicate("https://schema.org/description").ShouldBe("schema:description");
        KnowledgeNaming.NormalizePredicate("https://schema.org/keywords").ShouldBe("schema:keywords");
        KnowledgeNaming.NormalizePredicate("https://schema.org/mentions").ShouldBe("schema:mentions");
        KnowledgeNaming.NormalizePredicate("https://schema.org/name").ShouldBe("schema:name");
        KnowledgeNaming.NormalizePredicate("https://schema.org/sameAs").ShouldBe("schema:sameAs");
        KnowledgeNaming.NormalizePredicate("custom:predicate").ShouldBe("custom:predicate");
        KnowledgeNaming.NormalizePredicate("unknown").ShouldBe("kb:relatedTo");
    }

    [Test]
    public void Query_flow_rejects_mutation_and_maps_duplicate_optional_rows()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);

        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteReadOnly("INSERT DATA { <a> <b> <c> }"))
            .Message.ShouldContain("Only SELECT and ASK");
        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteRawReadOnly("INSERT DATA { <a> <b> <c> }"))
            .Message.ShouldContain("Only SELECT and ASK");

        var service = new KnowledgeSearchService(graph);
        var entities = service.SearchEntities("rdf");
        entities.Single().SameAs.Count.ShouldBe(2);

        var escaped = service.SearchArticles("knowledge \"graph\"\nmissing");
        escaped.ShouldBeEmpty();

        var safety = SparqlSafety.EnforceReadOnly("   ");
        safety.IsAllowed.ShouldBeFalse();
        safety.ErrorMessage.ShouldBe("SPARQL query is required");
    }
}
