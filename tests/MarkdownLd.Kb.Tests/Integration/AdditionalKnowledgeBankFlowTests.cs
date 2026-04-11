using System.Text;
using ManagedCode.MarkdownLd.Kb.Extraction;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Rdf;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;
using RootMarkdownDocumentParser = ManagedCode.MarkdownLd.Kb.Parsing.MarkdownDocumentParser;
using RootMarkdownDocumentSource = ManagedCode.MarkdownLd.Kb.MarkdownDocumentSource;
using RootMarkdownLinkKind = ManagedCode.MarkdownLd.Kb.MarkdownLinkKind;
using RootMarkdownParsingOptions = ManagedCode.MarkdownLd.Kb.MarkdownParsingOptions;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class AdditionalKnowledgeBankFlowTests
{
    private const string Bom = "\uFEFF";
    private const char BomCharacter = '\uFEFF';
    private const string RootParserIgnoredPath = "content/ignored.md";
    private const string RootParserInvalidBaseUri = "not a uri";
    private const string RootParserExpectedDocumentId = "notes/canonical-relative";
    private const string RootParserExpectedBaseUri = "urn:managedcode:markdown-ld-kb:/";
    private const string RootParserExpectedSummary = "Root parser summary.";
    private static readonly string[] RootParserExpectedAuthors = ["Ada Lovelace"];
    private static readonly string[] RootParserExpectedTags = ["alpha", "beta"];
    private static readonly string[] RootParserExpectedAbout = ["Knowledge Graph", "RDF"];
    private static readonly string[] RootParserExpectedEntityHints = ["RDF", "LLM"];
    private static readonly string[] RootParserTopHeadingPath = ["Top"];
    private static readonly string[] RootParserDeepHeadingPath = ["Top", "Deep"];
    private static readonly string[] RootParserSiblingHeadingPath = ["Top", "Sibling"];
    private const string RootParserWikiTarget = "RDF";
    private const string RootParserWikiDisplayText = "Resource Description Framework";
    private const string RootParserImageTitle = "Diagram title";
    private const string RootParserImageTarget = "urn:managedcode:markdown-ld-kb:/diagram.png";
    private const string RootParserGuideDisplayText = "Guide";
    private const string RootParserGuideTitle = "Guide title";
    private const string RootParserGuideTarget = "urn:managedcode:markdown-ld-kb:/guide/#part";
    private const string RootParserMailDisplayText = "Mail";
    private const string UntitledDocumentPrefix = "urn:markdown-ld-kb:document/";
    private const string DefaultUntitledDocumentId = "urn:markdown-ld-kb:document/untitled";
    private const string DefaultBaseUri = "urn:managedcode:markdown-ld-kb:/";
    private const string KbExampleBaseUri = "https://kb.example";
    private const string WhitespaceSample = "  Alpha\r\n  Beta\tGamma  ";
    private const string WhitespaceSampleExpected = "Alpha Beta Gamma";
    private const string AlphaValue = "Alpha";
    private const string BetaValue = "Beta";
    private const string TripleAsterisk = "***";
    private const string UnclosedContainsTitle = "Not Closed";
    private const string ScalarValuesDocumentPath = "content/scalar-values.mdx";
    private const string ScalarValuesBaseUri = "https://kb.example/root";
    private const string ScalarValuesDocumentId = "https://kb.example/root/scalar-values/";
    private const string ScalarValuesSummary = "Scalar summary.";
    private static readonly string[] ScalarValuesAuthors = ["Value Author", "123"];
    private static readonly string[] ScalarValuesTags = ["true", "42"];
    private static readonly string[] ScalarValuesAbout = ["Value Topic"];
    private const string ScalarValuesMarkdown = """
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
""";
    private const string ValueHintLabel = "Value Hint";
    private const string RichExtractionExpectedTitle = "Untitled";
    private const string RichExtractionExpectedEmptyArticleId = "urn:managedcode:markdown-ld-kb:article/untitled";
    private const string RichExtractionDocumentId = "https://kb.example/articles/rich";
    private static readonly string[] RichExtractionAuthors = ["Ada Lovelace"];
    private static readonly string[] RichExtractionTags = ["graph", "42"];
    private const string RichExtractionExpectedSoftwareLabel = "Large Language Model";
    private const string RichExtractionExpectedSoftwareType = "schema:SoftwareApplication";
    private const string RichExtractionExpectedRdfLabel = "Resource Description Framework";
    private const string RichExtractionIgnoredLabel = "Ignored";
    private const string RichExtractionPredicate = "http://example.com/predicate/relatedTo";
    private const string RichExtractionExpectedLargeLanguageModelEntity = "urn:managedcode:markdown-ld-kb:entity/large-language-model";
    private const string MissingFrontMatterMarkdown = """
# Source Path Title

[ ](https://example.com/blank-label)
[] (https://example.com/not-a-link)
Source Path Title --mentions--> Target
""";
    private const string MissingFrontMatterDocumentPath = "docs/SourcePathTitle.md";
    private const string MissingFrontMatterTitle = "Source Path Title";
    private const string MissingFrontMatterTargetEntity = "urn:managedcode:markdown-ld-kb:entity/target";
    private const string MissingFenceMarkdown = """
---
title: Missing Fence
""";
    private const string MissingFenceDocumentPath = "docs/missing-fence.md";
    private const string MissingFenceExpectedTitle = "missing fence";
    private const string MissingFenceExpectedArticleId = "urn:managedcode:markdown-ld-kb:article/missing-fence";
    private const string ScalarExtractionMarkdown = """
---
title: Scalar Extraction
authors: Ada Lovelace
tags: graph
about: RDF
entity_hints: SPARQL
---
Scalar Extraction --uses--> SPARQL
""";
    private static readonly string[] ScalarExtractionTags = ["graph"];
    private const string ScalarExtractionAbout = "RDF";
    private const string ScalarExtractionEntity = "SPARQL";
    private const string RootParserMarkdown = """
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
    private const string UnclosedMarkdown = """
---
title: Not Closed

# Body Is Still Source
""";
    private const string RichExtractionMarkdown = """
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
""";
    private const string ExpectedEmptyItemSlug = "item";
    private const string ExpectedUntitledArticleId = "urn:managedcode:markdown-ld-kb:article/untitled";
    private const string ChatExtractionPayload = """
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
      "o": "https://kb.example/id/rdf-core",
      "confidence": -4,
      "source": ""
    }
  ]
}
""";
    private const string ChatExtractionBaseUri = "https://kb.example/";
    private const string ChatExtractionDocumentId = "https://kb.example/article/";
    private const string ChatExtractionPath = "content/article.md";
    private const string ChatExtractionMarkdown = "RDF Core appears here.";
    private const string ChatExtractionExpectedEntityLabel = "RDF Core";
    private const string ChatExtractionExpectedEntityId = "https://kb.example/id/rdf-core";
    private const string ChatExtractionExpectedEntityType = "schema:Thing";
    private static readonly string[] ChatExtractionExpectedSameAs = ["https://example.com/rdf"];
    private const string ChatExtractionAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://kb.example/article/> schema:mentions <https://kb.example/id/rdf-core> .
  <https://kb.example/id/rdf-core> rdf:type <https://schema.org/Thing> ;
                                   schema:sameAs <https://example.com/rdf> .
}
""";
    private const string PipelineFlowPath = "content/pipeline-flow.md";
    private const string PipelineFlowMarkdown = """
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
""";
    private const string PipelineBaseUri = "https://kb.example/";
    private const string PipelineExpectedTitle = "Pipeline Article";
    private const string PipelineExpectedSoftwareLabel = "Pipeline Tool";
    private const string PipelineExpectedSoftwareType = "schema:SoftwareApplication";
    private const string PipelinePredicateCreator = "schema:creator";
    private const string PipelinePredicateRelatedTo = "kb:relatedTo";
    private const string PipelinePredicateWasDerivedFrom = "prov:wasDerivedFrom";
    private const string PipelinePredicateRdfType = "rdf:type";
    private const string PipelinePredicateXsdInteger = "xsd:integer";
    private const string PipelineQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?published ?modified WHERE {
  <https://kb.example/pipeline-flow/> schema:datePublished ?published ;
                                       schema:dateModified ?modified .
}
""";
    private const string PipelineExpectedPublished = "2026-04-08";
    private const string PipelineExpectedModified = "not-a-date";
    private const string PipelinePublishedKey = "published";
    private const string PipelineModifiedKey = "modified";
    private const string NamingCeskyGraph = "Český Graph!!";
    private const string NamingExpectedCeskyGraph = "cesky-graph";
    private const string NamingBaseUri = "https://kb.example/root";
    private const string NamingBaseUriNormalized = "https://kb.example/root/";
    private const string NamingDocumentBaseUri = "https://kb.example/";
    private const string NamingExpectedDocumentUri = "https://kb.example/document/";
    private const string NamingRelativeSourcePath = "/content/folder\\note.md";
    private const string NamingExpectedRelativeSourcePath = "folder/note.md";
    private const string ReadOnlyInsert = "INSERT DATA { <a> <b> <c> }";
    private const string ReadOnlySelect = "SELECT ?s WHERE { ?s ?p ?o }";
    private const string ReadOnlyEmptyReason = "SPARQL query is empty";
    private const string ReadOnlyInsertReasonFragment = "INSERT";
    private const string ReadOnlySchemaAbout = "https://schema.org/about";
    private const string ReadOnlySchemaAuthor = "https://schema.org/author";
    private const string ReadOnlySchemaCreator = "https://schema.org/creator";
    private const string ReadOnlySchemaDescription = "https://schema.org/description";
    private const string ReadOnlySchemaKeywords = "https://schema.org/keywords";
    private const string ReadOnlySchemaMentions = "https://schema.org/mentions";
    private const string ReadOnlySchemaName = "https://schema.org/name";
    private const string ReadOnlySchemaSameAs = "https://schema.org/sameAs";
    private const string ReadOnlyCustomPredicate = "custom:predicate";
    private const string ReadOnlyCustomPredicateExpected = "custom:predicate";
    private const string ReadOnlyUnknownPredicate = "unknown";
    private const string ReadOnlyKbRelatedTo = "kb:relatedTo";
    private const string SchemaAbout = "schema:about";
    private const string SchemaAuthor = "schema:author";
    private const string SchemaCreator = "schema:creator";
    private const string SchemaDescription = "schema:description";
    private const string SchemaKeywords = "schema:keywords";
    private const string SchemaMentions = "schema:mentions";
    private const string SchemaName = "schema:name";
    private const string SchemaSameAs = "schema:sameAs";
    private const string QueryInsert = "INSERT DATA { <a> <b> <c> }";
    private const string QueryOnlySelectAndAsk = "Only SELECT and ASK";
    private const string QuerySearchRdf = "rdf";
    private const string QuerySearchEscaped = "knowledge \"graph\"\nmissing";
    private const string QuerySafetyWhitespace = "   ";
    private const string QuerySafetyRequired = "SPARQL query is required";
    private const string ArticleNotClosed = "Not Closed";
    private const string RootParserCanonicalUrl = "notes/canonical-relative";
    private const string LargeMarkdownPath = "content/large-memex.md";
    private const string LargeMarkdownBaseUri = "https://kb.example/";
    private const string LargeMarkdownDocumentId = "https://kb.example/large-memex/";
    private const string LargeMarkdownSummary = "Large markdown extraction summary.";
    private const string LargeMarkdownEntityExtractorLabel = "Entity Extractor";
    private const string LargeMarkdownEntityExtractorType = "schema:SoftwareApplication";
    private const string LargeMarkdownEntityExtractorSameAs = "https://example.com/entity-extractor";
    private const string LargeMarkdownWikiTarget = "SPARQL";
    private const string LargeMarkdownDotNetRdfLabel = "dotNetRDF";
    private const string LargeMarkdownDotNetRdfUrl = "https://dotnetrdf.org/";
    private const string LargeMarkdownSearchTerm = "entity";
    private const int LargeMarkdownRepeatedSectionCount = 64;
    private const int LargeMarkdownExpectedSectionCount = 66;
    private const int LargeMarkdownMinimumChunkCount = 32;
    private const int LargeMarkdownMinimumAssertionCount = 4;
    private const int LargeMarkdownExpectedDocumentCount = 1;
    private const int LargeMarkdownRootParserChunkTarget = 16;
    private static readonly string[] LargeMarkdownExpectedTags = ["memex", "retrieval"];
    private const string LargeMarkdownHeader = """
---
title: Large Markdown Memex
canonicalUrl: https://kb.example/large-memex/
description: Large markdown extraction summary.
tags:
  - memex
  - retrieval
author:
  - label: Ada Lovelace
    type: schema:Person
about:
  - Knowledge Graph
entity_hints:
  - label: Entity Extractor
    type: SoftwareApplication
    sameAs:
      - https://example.com/entity-extractor
---
Intro before heading with [[SPARQL]] and [dotNetRDF](https://dotnetrdf.org/).

# Knowledge Graph Intake

Large Markdown Memex --mentions--> Entity Extractor
article --mentions--> RDF
Entity Extractor --kb:relatedTo--> SPARQL
""";
    private const string LargeMarkdownRepeatedSection = """

## Repeated Graph Section

This section keeps extraction busy with [[SPARQL]], [dotNetRDF](https://dotnetrdf.org/), and RDF facts.
article --mentions--> RDF
Entity Extractor --kb:relatedTo--> SPARQL
""";
    private const string LargeMarkdownDocumentAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/large-memex/> schema:name "Large Markdown Memex" ;
                                      schema:description "Large markdown extraction summary." ;
                                      schema:keywords "memex" ;
                                      schema:keywords "retrieval" ;
                                      schema:author <https://kb.example/id/ada-lovelace> ;
                                      schema:mentions <https://kb.example/id/entity-extractor> ;
                                      schema:mentions <https://kb.example/id/rdf> .
}
""";
    private const string LargeMarkdownEntityTypeAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://kb.example/id/entity-extractor> rdf:type <https://schema.org/SoftwareApplication> .
}
""";
    private const string LargeMarkdownEntitySameAsAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/id/entity-extractor> schema:sameAs <https://example.com/entity-extractor> .
}
""";
    private const string LargeMarkdownEntityRelatedAskQuery = """
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://kb.example/id/entity-extractor> kb:relatedTo <https://kb.example/id/sparql> .
}
""";
    private const string LargeMarkdownDotNetRdfAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/id/dotnetrdf> schema:sameAs <https://dotnetrdf.org/> .
}
""";

    [Test]
    public void Root_parser_handles_front_matter_links_loose_text_and_nested_heading_flow()
    {
        var markdown = Bom + RootParserMarkdown;

        var parser = new RootMarkdownDocumentParser();
        var document = parser.Parse(
            new RootMarkdownDocumentSource(markdown, RootParserIgnoredPath, RootParserInvalidBaseUri),
            new RootMarkdownParsingOptions { ChunkTokenTarget = 2 });

        document.DocumentId.ShouldBe(RootParserCanonicalUrl);
        document.BaseUri!.AbsoluteUri.ShouldBe(RootParserExpectedBaseUri);
        document.FrontMatter.Summary.ShouldBe(RootParserExpectedSummary);
        document.FrontMatter.Authors.ShouldBe(RootParserExpectedAuthors);
        document.FrontMatter.Tags.ShouldBe(RootParserExpectedTags);
        document.FrontMatter.About.ShouldBe(RootParserExpectedAbout);
        document.FrontMatter.EntityHints.Select(hint => hint.Label).ShouldBe(RootParserExpectedEntityHints);

        document.Sections.Count.ShouldBe(4);
        document.Sections[0].HeadingLevel.ShouldBe(0);
        document.Sections[1].HeadingPath.ShouldBe(RootParserTopHeadingPath);
        document.Sections[2].HeadingPath.ShouldBe(RootParserDeepHeadingPath);
        document.Sections[3].HeadingPath.ShouldBe(RootParserSiblingHeadingPath);
        document.Chunks.Count.ShouldBeGreaterThanOrEqualTo(4);

        var wiki = document.Links.Single(link => link.Kind == RootMarkdownLinkKind.WikiLink);
        wiki.Target.ShouldBe(RootParserWikiTarget);
        wiki.DisplayText.ShouldBe(RootParserWikiDisplayText);

        var image = document.Links.Single(link => link.IsImage);
        image.Title.ShouldBe(RootParserImageTitle);
        image.ResolvedTarget.ShouldBe(RootParserImageTarget);

        var guide = document.Links.Single(link => link.DisplayText == RootParserGuideDisplayText);
        guide.IsDocumentLink.ShouldBeTrue();
        guide.Title.ShouldBe(RootParserGuideTitle);
        guide.ResolvedTarget.ShouldBe(RootParserGuideTarget);

        var mail = document.Links.Single(link => link.DisplayText == RootParserMailDisplayText);
        mail.IsExternal.ShouldBeTrue();
    }

    [Test]
    public async Task Large_markdown_content_flow_extracts_document_format_and_queryable_graph()
    {
        var markdown = BuildLargeMarkdownFixture();
        var rootParser = new RootMarkdownDocumentParser();
        var parsed = rootParser.Parse(
            new RootMarkdownDocumentSource(markdown, LargeMarkdownPath, LargeMarkdownBaseUri),
            new RootMarkdownParsingOptions { ChunkTokenTarget = LargeMarkdownRootParserChunkTarget });

        parsed.DocumentId.ShouldBe(LargeMarkdownDocumentId);
        parsed.FrontMatter.Summary.ShouldBe(LargeMarkdownSummary);
        parsed.FrontMatter.Tags.ShouldBe(LargeMarkdownExpectedTags);
        parsed.Sections.Count.ShouldBe(LargeMarkdownExpectedSectionCount);
        parsed.Chunks.Count.ShouldBeGreaterThan(LargeMarkdownMinimumChunkCount);
        parsed.Links.Any(link => link.Kind == RootMarkdownLinkKind.WikiLink && link.Target == LargeMarkdownWikiTarget).ShouldBeTrue();
        parsed.Links.Any(link => link.DisplayText == LargeMarkdownDotNetRdfLabel && link.ResolvedTarget == LargeMarkdownDotNetRdfUrl).ShouldBeTrue();

        var converter = new KnowledgeSourceDocumentConverter();
        var source = converter.ConvertContent(markdown, LargeMarkdownPath);
        var pipeline = new MarkdownKnowledgePipeline(new Uri(LargeMarkdownBaseUri));
        var result = await pipeline.BuildAsync([source]);

        result.Documents.Count.ShouldBe(LargeMarkdownExpectedDocumentCount);
        result.Documents[0].Sections.Count.ShouldBe(LargeMarkdownExpectedSectionCount);
        result.Facts.Assertions.Count.ShouldBeGreaterThan(LargeMarkdownMinimumAssertionCount);
        var entityExtractor = result.Facts.Entities.Single(entity => entity.Label == LargeMarkdownEntityExtractorLabel);
        entityExtractor.Type.ShouldBe(LargeMarkdownEntityExtractorType);
        entityExtractor.SameAs.ShouldContain(LargeMarkdownEntityExtractorSameAs);

        var documentAsk = await result.Graph.ExecuteAskAsync(LargeMarkdownDocumentAskQuery);
        documentAsk.ShouldBeTrue();

        var entityTypeAsk = await result.Graph.ExecuteAskAsync(LargeMarkdownEntityTypeAskQuery);
        entityTypeAsk.ShouldBeTrue();

        var entitySameAsAsk = await result.Graph.ExecuteAskAsync(LargeMarkdownEntitySameAsAskQuery);
        entitySameAsAsk.ShouldBeTrue();

        var entityRelatedAsk = await result.Graph.ExecuteAskAsync(LargeMarkdownEntityRelatedAskQuery);
        entityRelatedAsk.ShouldBeTrue();

        var dotNetRdfAsk = await result.Graph.ExecuteAskAsync(LargeMarkdownDotNetRdfAskQuery);
        dotNetRdfAsk.ShouldBeTrue();

        var search = await result.Graph.SearchAsync(LargeMarkdownSearchTerm);
        search.Rows.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public void Root_parser_handles_empty_unclosed_and_static_identity_flows()
    {
        var parser = new RootMarkdownDocumentParser();
        var empty = parser.Parse(new RootMarkdownDocumentSource(string.Empty, null, RootParserInvalidBaseUri));
        empty.BaseUri!.AbsoluteUri.ShouldBe(DefaultBaseUri);
        empty.DocumentId.ShouldStartWith(UntitledDocumentPrefix);
        empty.Sections.ShouldBeEmpty();
        empty.Chunks.ShouldBeEmpty();

        RootMarkdownDocumentParser.DocumentIdFromPath(string.Empty, KbExampleBaseUri)
            .ShouldBe(DefaultUntitledDocumentId);
        RootMarkdownDocumentParser.NormalizeWhitespace(WhitespaceSample)
            .ShouldBe(WhitespaceSampleExpected);
        RootMarkdownDocumentParser.ComputeChunkId(AlphaValue)
            .ShouldNotBe(RootMarkdownDocumentParser.ComputeChunkId(BetaValue));

        var unclosed = parser.Parse(new RootMarkdownDocumentSource(UnclosedMarkdown, null, KbExampleBaseUri));

        unclosed.FrontMatter.Values.ShouldBeEmpty();
        unclosed.BodyMarkdown.ShouldContain(UnclosedContainsTitle);
        unclosed.DocumentId.ShouldStartWith(UntitledDocumentPrefix);
    }

    [Test]
    public void Root_parser_handles_scalar_and_typed_yaml_values_through_document_flow()
    {
        var parser = new RootMarkdownDocumentParser();
        var document = parser.Parse(new RootMarkdownDocumentSource(ScalarValuesMarkdown, ScalarValuesDocumentPath, ScalarValuesBaseUri));

        document.DocumentId.ShouldBe(ScalarValuesDocumentId);
        document.FrontMatter.Title.ShouldBeNull();
        document.FrontMatter.Summary.ShouldBe(ScalarValuesSummary);
        document.FrontMatter.Authors.ShouldContain(ScalarValuesAuthors[0]);
        document.FrontMatter.Authors.ShouldContain(ScalarValuesAuthors[1]);
        document.FrontMatter.Tags.ShouldContain(ScalarValuesTags[0]);
        document.FrontMatter.Tags.ShouldContain(ScalarValuesTags[1]);
        document.FrontMatter.About.ShouldBe(ScalarValuesAbout);
        document.FrontMatter.EntityHints.Any(hint => hint.Label == ScalarValuesAuthors[1]).ShouldBeTrue();
        document.FrontMatter.EntityHints.Any(hint => hint.Label == ValueHintLabel).ShouldBeTrue();
        document.Sections.Single().HeadingLevel.ShouldBe(0);
    }

    [Test]
    public void Extraction_flow_handles_url_labels_scalar_values_code_fences_and_canonical_ids()
    {
        var extractor = new MarkdownKnowledgeExtractor();
        var empty = extractor.Extract(string.Empty);
        empty.Article.Title.ShouldBe(RichExtractionExpectedTitle);
        empty.Article.Id.ShouldBe(RichExtractionExpectedEmptyArticleId);
        empty.Entities.ShouldBeEmpty();

        var result = extractor.Extract(RichExtractionMarkdown);

        result.Article.Id.ShouldBe(RichExtractionDocumentId);
        result.Article.Authors.Select(author => author.Name).ShouldContain(RichExtractionAuthors[0]);
        result.Article.Tags.ShouldBe(RichExtractionTags);
        result.Entities.Any(entity => entity.Label == RichExtractionExpectedSoftwareLabel && entity.Type == RichExtractionExpectedSoftwareType).ShouldBeTrue();
        result.Entities.Any(entity => entity.Label == RichExtractionExpectedRdfLabel).ShouldBeTrue();
        result.Entities.Any(entity => entity.Label == RichExtractionIgnoredLabel).ShouldBeFalse();
        result.Assertions.Any(assertion =>
            assertion.SubjectId == result.Article.Id &&
            assertion.Predicate == RichExtractionPredicate &&
            assertion.ObjectId == RichExtractionExpectedLargeLanguageModelEntity).ShouldBeTrue();
    }

    [Test]
    public void Extraction_flow_handles_missing_front_matter_unclosed_yaml_scalar_sequences_and_id_helpers()
    {
        var extractor = new MarkdownKnowledgeExtractor();
        var noFrontMatter = extractor.Extract(MissingFrontMatterMarkdown, MissingFrontMatterDocumentPath);

        noFrontMatter.Article.Title.ShouldBe(MissingFrontMatterTitle);
        noFrontMatter.Assertions.Any(assertion =>
            assertion.SubjectId == noFrontMatter.Article.Id &&
            assertion.ObjectId == MissingFrontMatterTargetEntity).ShouldBeTrue();

        var unclosed = extractor.Extract(MissingFenceMarkdown, MissingFenceDocumentPath);

        unclosed.Article.Title.ShouldBe(MissingFenceExpectedTitle);
        unclosed.Article.Id.ShouldBe(MissingFenceExpectedArticleId);

        var scalar = extractor.Extract(ScalarExtractionMarkdown);

        scalar.Article.Authors.Any(author => author.Name == RichExtractionAuthors[0]).ShouldBeTrue();
        scalar.Article.Tags.ShouldBe(ScalarExtractionTags);
        scalar.Article.About.Single().Label.ShouldBe(ScalarExtractionAbout);
        scalar.Entities.Any(entity => entity.Label == ScalarExtractionEntity).ShouldBeTrue();

        MarkdownKnowledgeIds.Slugify(string.Empty).ShouldBe(ExpectedEmptyItemSlug);
        MarkdownKnowledgeIds.Slugify(TripleAsterisk).ShouldBe(ExpectedEmptyItemSlug);
        MarkdownKnowledgeIds.HumanizeLabel(string.Empty).ShouldBeEmpty();
        MarkdownKnowledgeIds.BuildArticleId(null, null).ShouldBe(ExpectedUntitledArticleId);
    }

    [Test]
    public async Task Chat_extraction_flow_filters_null_blank_and_duplicate_structured_output()
    {
        var chatClient = new TestChatClient((_, _) => ChatExtractionPayload);
        var pipeline = new MarkdownKnowledgePipeline(new Uri(ChatExtractionBaseUri), chatClient);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ChatExtractionPath, ChatExtractionMarkdown),
        ]);

        result.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(ChatExtractionDocumentId);
        var entity = result.Facts.Entities.Single(item => item.Label == ChatExtractionExpectedEntityLabel);
        entity.Id.ShouldBe(ChatExtractionExpectedEntityId);
        entity.Type.ShouldBe(ChatExtractionExpectedEntityType);
        entity.SameAs.ShouldBe(ChatExtractionExpectedSameAs);

        var graphAsk = await result.Graph.ExecuteAskAsync(ChatExtractionAskQuery);
        graphAsk.ShouldBeTrue();

        chatClient.CallCount.ShouldBe(1);
    }

    [Test]
    public async Task Pipeline_flow_materializes_front_matter_predicates_dates_and_sources_into_queryable_graph()
    {
        var source = new MarkdownSourceDocument(
            PipelineFlowPath,
            PipelineFlowMarkdown);

        var pipeline = new MarkdownKnowledgePipeline(new Uri(PipelineBaseUri));
        var result = await pipeline.BuildAsync([source]);

        result.Documents[0].Title.ShouldBe(PipelineExpectedTitle);
        result.Facts.Entities.Any(entity => entity.Label == PipelineExpectedSoftwareLabel && entity.Type == PipelineExpectedSoftwareType).ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == PipelinePredicateCreator).ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == PipelinePredicateRelatedTo).ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == PipelinePredicateWasDerivedFrom).ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == PipelinePredicateRdfType).ShouldBeTrue();
        result.Facts.Assertions.Any(assertion => assertion.Predicate == PipelinePredicateXsdInteger).ShouldBeTrue();

        var dateResult = await result.Graph.ExecuteSelectAsync(PipelineQuery);
        dateResult.Rows.Count.ShouldBe(1);
        dateResult.Rows[0].Values[PipelinePublishedKey].ShouldBe(PipelineExpectedPublished);
        dateResult.Rows[0].Values[PipelineModifiedKey].ShouldBe(PipelineExpectedModified);
    }

    [Test]
    public void Pipeline_naming_and_query_safety_cover_readonly_and_normalization_flows()
    {
        KnowledgeNaming.Slugify(string.Empty).ShouldBe(ExpectedEmptyItemSlug);
        KnowledgeNaming.Slugify(NamingCeskyGraph).ShouldBe(NamingExpectedCeskyGraph);
        KnowledgeNaming.NormalizeBaseUri(new Uri(NamingBaseUri)).AbsoluteUri.ShouldBe(NamingBaseUriNormalized);
        KnowledgeNaming.CreateDocumentUri(new Uri(NamingDocumentBaseUri), string.Empty).AbsoluteUri.ShouldBe(NamingExpectedDocumentUri);
        KnowledgeNaming.NormalizeSourcePath(NamingRelativeSourcePath).ShouldBe(NamingExpectedRelativeSourcePath);

        KnowledgeNaming.IsReadOnlySparql(string.Empty, out var emptyReason).ShouldBeFalse();
        emptyReason.ShouldBe(ReadOnlyEmptyReason);
        KnowledgeNaming.IsReadOnlySparql(ReadOnlyInsert, out var mutatingReason).ShouldBeFalse();
        mutatingReason!.ShouldContain(ReadOnlyInsertReasonFragment);
        KnowledgeNaming.IsReadOnlySparql(ReadOnlySelect, out var readOnlyReason).ShouldBeTrue();
        readOnlyReason.ShouldBeNull();

        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaAbout).ShouldBe(SchemaAbout);
        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaAuthor).ShouldBe(SchemaAuthor);
        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaCreator).ShouldBe(SchemaCreator);
        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaDescription).ShouldBe(SchemaDescription);
        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaKeywords).ShouldBe(SchemaKeywords);
        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaMentions).ShouldBe(SchemaMentions);
        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaName).ShouldBe(SchemaName);
        KnowledgeNaming.NormalizePredicate(ReadOnlySchemaSameAs).ShouldBe(SchemaSameAs);
        KnowledgeNaming.NormalizePredicate(ReadOnlyCustomPredicate).ShouldBe(ReadOnlyCustomPredicateExpected);
        KnowledgeNaming.NormalizePredicate(ReadOnlyUnknownPredicate).ShouldBe(ReadOnlyKbRelatedTo);
    }

    [Test]
    public void Query_flow_rejects_mutation_and_maps_duplicate_optional_rows()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);

        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteReadOnly(QueryInsert))
            .Message.ShouldContain(QueryOnlySelectAndAsk);
        Should.Throw<InvalidOperationException>(() =>
            executor.ExecuteRawReadOnly(QueryInsert))
            .Message.ShouldContain(QueryOnlySelectAndAsk);

        var service = new KnowledgeSearchService(graph);
        var entities = service.SearchEntities(QuerySearchRdf);
        entities.Single().SameAs.Count.ShouldBe(2);

        var escaped = service.SearchArticles(QuerySearchEscaped);
        escaped.ShouldBeEmpty();

        var safety = SparqlSafety.EnforceReadOnly(QuerySafetyWhitespace);
        safety.IsAllowed.ShouldBeFalse();
        safety.ErrorMessage.ShouldBe(QuerySafetyRequired);
    }

    private static string BuildLargeMarkdownFixture()
    {
        var builder = new StringBuilder();
        builder.Append(BomCharacter);
        builder.Append(LargeMarkdownHeader);

        for (var index = 0; index < LargeMarkdownRepeatedSectionCount; index++)
        {
            builder.Append(LargeMarkdownRepeatedSection);
        }

        return builder.ToString();
    }
}
