using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;
using RootMarkdownDocumentParser = ManagedCode.MarkdownLd.Kb.Parsing.MarkdownDocumentParser;
using RootMarkdownDocumentSource = ManagedCode.MarkdownLd.Kb.MarkdownDocumentSource;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeFlowEdgeCaseTests
{
    private const string BaseUrl = "https://kb.example/";
    private static readonly Uri BaseUri = new(BaseUrl);
    private const string TempRootPrefix = "markdown-ld-kb-flow-";
    private const string GuidFormat = "N";
    private const string CustomMediaType = "application/custom-markdown";

    private const string ContentRootFlowPath = "content/root-flow.md";
    private const string ContentInvalidRootFlowPath = "content/invalid-root-flow.md";
    private const string ContentScalarRootPath = "content/scalar-root.md";
    private const string ContentMergePath = "content/merge.md";
    private const string ContentEmptyChatPath = "content/empty-chat.md";
    private const string ContentEdgePath = "content/edge.md";
    private const string ComplexFileName = "complex.md";
    private const string InvalidYamlFileName = "invalid-yaml.md";
    private const string PlainMarkdownFileName = "plain.md";
    private const string MarkerOnlyFileName = "marker-only.md";
    private const string UnclosedFileName = "unclosed.md";
    private const string ListYamlFileName = "list-yaml.md";
    private const string BrokenBinFileName = "broken.bin";

    private const string RootFlowTitle = "Root Flow";
    private const string RootFlowSummary = "Root summary.";
    private const string RootFlowCanonicalUrl = "https://kb.example/root-flow/";
    private const string RootFlowDatePublished = "2026-04-11T12:30:00Z";
    private const string RootFlowDateModified = "2026-04-12";
    private const string RootFlowAuthorAda = "Ada Lovelace";
    private const string RootFlowAuthorValue = "Value Author";
    private const string KnowledgeGraphLabel = "Knowledge Graph";
    private const string RootToolLabel = "Root Tool";
    private const string RootToolType = "SoftwareApplication";
    private const string RootToolSameAs = "https://example.com/root-tool";
    private const string ValueHintLabel = "Value Hint";
    private const string RootFlowAskRoot = "https://kb.example/root-flow/";
    private const string RootToolEntityId = "https://kb.example/id/root-tool";

    private const string InvalidRootFlowTitle = "Invalid Root Flow";
    private const string InvalidRootFlowAskRoot = "https://kb.example/invalid-root-flow/";
    private const string InvalidRootFlowEntityId = "https://kb.example/id/rdf";

    private const string ScalarRootTitle = "123";
    private const string ScalarRootDescription = "456";
    private const string ScalarRootAuthor = "789";
    private const string ScalarRootTag = "scalar";
    private const string ScalarRootAbout = "321";
    private const string ScalarRootEntityHint = "654";
    private const string ScalarRootAskRoot = "https://kb.example/scalar-root/";
    private const string ScalarRootEntityId = "https://kb.example/id/654";

    private const string ComplexTitle = "Complex YAML";
    private const string ComplexSummary = "Complex summary.";
    private const string ComplexAuthorLabel = "Label Author";
    private const string ComplexAuthorValue = "Value Author";
    private const string ComplexHintLabel = "Hint Entity";
    private const string ComplexHintSameAs = "https://example.com/hint";
    private const string ComplexKeywordAlpha = "alpha";
    private const string ComplexKeywordBeta = "beta";
    private const string ComplexMetadataRoot = "https://kb.example/complex/";
    private const string ComplexAuthorId = "https://kb.example/id/label-author";
    private const string ComplexHintId = "https://kb.example/id/hint-entity";
    private const string ComplexSummaryKey = "summary";
    private const string ComplexKeywordKey = "keyword";

    private const string BrokenYamlTitle = "Still Parsed";
    private const string BrokenYamlAskRoot = "https://kb.example/invalid-yaml/";
    private const string BrokenYamlEntityId = "https://kb.example/id/rdf";
    private const string BrokenYamlInsertQuery = "INSERT DATA { <a> <b> <c> }";
    private const string BrokenYamlMalformedSelectQuery = "SELECT ?s WHERE { ?s ?p }";

    private const string EdgeTitle = "Edge Flow";
    private const string EdgeAdaLabel = "Ada Lovelace";
    private const string EdgeAdaSameAs = "https://example.com/ada";
    private const string EdgeToolLabel = "Tool";
    private const string EdgeToolSameAs = "https://example.com/tool";
    private const string EdgeToolType = "schema:SoftwareApplication";
    private const string EdgeMarkdownRoot = "https://kb.example/edge/";
    private const string EdgeAbsolutePredicate = "https://example.com/predicate/absolute";
    private const string EdgeAbsoluteObject = "https://example.com/object";
    private const string EdgeCustomObject = "https://example.com/custom";
    private const string EdgeRelativeId = "https://kb.example/id/relative";
    private const string EdgeAbsoluteId = "https://kb.example/id/absolute";
    private const string EdgeToolEntityId = "https://kb.example/id/tool";
    private const string EdgeOpinionPredicate = "custom:predicate";

    private const string MergeTitle = "Merge Flow";
    private const string MergeEntityLabel = "RDF";
    private const string MergeEntitySameAs = "https://example.com/rdf";
    private const string MergeEntityW3CSameAs = "https://www.w3.org/RDF/";
    private const string MergeChatSource = "https://kb.example/source/chat";
    private const string MergeSubjectId = "https://kb.example/merge/";
    private const string MergeEntityId = "https://kb.example/id/rdf";
    private const string MergeAskObject = "https://kb.example/id/rdf";
    private const string MergeGraphToolLabel = "Graph Tool";
    private const string MergeSchemaThingType = "schema:Thing";
    private const string MergeSchemaOrganizationType = "schema:Organization";
    private const string MergeMentionPredicateValue = "mentions";
    private const string MergeSameAsPredicateValue = "sameas";
    private const string MergeAboutPredicateValue = "about";
    private const string MergeAuthorPredicateValue = "author";
    private const string MergeCreatorPredicateValue = "creator";
    private const string MergeRelatedToPredicateValue = "relatedTo";
    private const string MergePlainUnknownPredicateValue = "plain unknown";
    private const string MergeAbsolutePredicateValue = "https://example.com/predicate/absolute";
    private const string MergeNotAUriValue = "not a uri";
    private const string MergeSkippedObjectValue = "https://example.com/skipped";
    private const string MergeDirectMentionObjectValue = "https://example.com/direct-mention";
    private const string MergeDirectAboutObjectValue = "https://example.com/direct-about";
    private const string MergeDirectAuthorObjectValue = "https://example.com/direct-author";
    private const string MergeDirectCreatorObjectValue = "https://example.com/direct-creator";
    private const string MergeDirectSameAsObjectValue = "https://example.com/direct-same-as";
    private const string MergeDirectIgnoredObjectValue = "https://example.com/ignored";
    private const string MergeUnprefixedTypeLabel = "Unprefixed Type";
    private const string EmptyJsonPayload = "{}";

    private const string EmptyChatTitle = "Empty Chat";
    private const string EmptyChatRoot = "https://kb.example/empty-chat/";
    private const string EmptyChatEntityId = "https://kb.example/id/rdf";

    private const string ConverterTitle = "Converted Content";
    private const string ConverterRelatedPredicate = "https://example.com/predicate/related";
    private const string ConverterRelatedObject = "https://example.com/object";
    private const string ConverterUnknownObject = "https://example.com/unknown";
    private const string ConverterAboutObject = "https://example.com/about";
    private const string ConverterAuthorObject = "https://example.com/author";
    private const string ConverterDescriptionObject = "https://example.com/description";
    private const string ConverterKeywordsObject = "https://example.com/keywords";
    private const string ConverterIgnoredObject = "https://example.com/ignored";
    private const string DirectoryPlainRoot = "https://kb.example/plain/";

    private const string FactMergerSubjectRoot = "https://kb.example/fact-flow/";
    private const string FactMergerExternalSubject = "urn:external:subject";
    private const string FactMergerToolId = "https://kb.example/id/tool";
    private const string FactMergerAboutObject = "https://example.com/about";
    private const string FactMergerAuthorObject = "https://example.com/author";
    private const string FactMergerCreatorObject = "https://example.com/creator";
    private const string FactMergerRelatedObject = "https://example.com/related";
    private const string FactMergerUnknownObject = "https://example.com/unknown";
    private const string FactMergerAbsolutePredicateObject = "https://example.com/absolute";
    private const string FactMergerDirectMentionObject = "https://example.com/direct-mention";
    private const string FactMergerDirectAboutObject = "https://example.com/direct-about";
    private const string FactMergerDirectAuthorObject = "https://example.com/direct-author";
    private const string FactMergerDirectCreatorObject = "https://example.com/direct-creator";
    private const string FactMergerDirectSameAsObject = "https://example.com/direct-same-as";
    private const string FactMergerDirectUnknownObject = "https://example.com/unknown";
    private const string FactMergerOrgId = "https://kb.example/id/org-tool";
    private const string FactMergerUnprefixedId = "https://kb.example/id/unprefixed-type";
    private const string FactMergerUnprefixedType = "schema:SoftwareApplication";

    private const string SummaryDictionaryKey = "summary";
    private const string KeywordDictionaryKey = "keyword";
    private const string TitleDictionaryKey = "title";
    private const string MentionDictionaryKey = "mention";
    private const string ObjectDictionaryKey = "object";

    private const string SchemaMentionsPredicate = "schema:mentions";
    private const string SchemaNamePredicate = "schema:name";
    private const string SchemaDescriptionPredicate = "schema:description";
    private const string SchemaKeywordsPredicate = "schema:keywords";
    private const string SchemaAuthorPredicate = "schema:author";
    private const string SchemaSameAsPredicate = "schema:sameAs";
    private const string SchemaCreatorPredicate = "schema:creator";
    private const string SchemaAboutPredicate = "schema:about";
    private const string RdfTypePredicate = "rdf:type";
    private const string KbRelatedToPredicate = "kb:relatedTo";
    private const string KbPlainUnknownPredicate = "kb:plain-unknown";
    private const string AbsolutePredicate = "<https://example.com/predicate/absolute>";
    private const string CustomPredicate = "<custom:predicate>";

    private const string RootFlowMarkdown = """
---
title: Root Flow
canonicalUrl: https://kb.example/root-flow/
description: Root summary.
datePublished: 2026-04-11T12:30:00Z
dateModified: 2026-04-12
authors:
  - label: Ada Lovelace
  - value: Value Author
  - 9001
tags:
  - true
  - 42
about:
  - name: Knowledge Graph
  - value: RDF
entityHints:
  - label: Root Tool
    type: SoftwareApplication
    sameAs:
      - https://example.com/root-tool
  - value: Value Hint
  - 777
---
# Root Flow

Root Flow --mentions--> Root Tool
""";

    private const string RootFlowAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/root-flow/> schema:mentions <https://kb.example/id/root-tool> .
  <https://kb.example/id/root-tool> schema:name "Root Tool" .
}
""";

    private const string InvalidRootFlowMarkdown = """
---
title: [unterminated
---
# Invalid Root Flow

Invalid Root Flow --mentions--> RDF
""";

    private const string ScalarRootMarkdown = """
---
title: 123
description: 456
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
entityHints:
  -
  - {}
  - 654
---
# 123

123 --mentions--> 654
""";

    private const string ScalarRootAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/scalar-root/> schema:name "123" ;
                                      schema:mentions <https://kb.example/id/654> .
}
""";

    private const string ComplexMarkdown = """
---
title: Complex YAML
description: Complex summary.
datePublished: 2026-04-11T12:30:00Z
dateModified: 2026-04-12
author:
  - label: Label Author
  - value: Value Author
  - 9001
tags:
  - alpha
  - beta
about:
  - name: Name Topic
  - value: Value Topic
entity_hints:
  - label: Hint Entity
    type: SoftwareApplication
    sameAs: https://example.com/hint
  - value: Value Hint
  - 777
---
# Complex YAML

Complex YAML --mentions--> Hint Entity
""";

    private const string ComplexSelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?summary ?keyword WHERE {
  <https://kb.example/complex/> schema:description ?summary ;
                                    schema:keywords ?keyword .
}
ORDER BY ?keyword
""";

    private const string ComplexAuthorAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/complex/> schema:author <https://kb.example/id/label-author> .
  <https://kb.example/id/label-author> schema:name "Label Author" .
}
""";

    private const string ComplexHintAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/complex/> schema:mentions <https://kb.example/id/hint-entity> .
  <https://kb.example/id/hint-entity> schema:sameAs <https://example.com/hint> .
}
""";

    private const string BrokenYamlMarkdown = """
---
title: [unterminated
---
# Still Parsed

Still Parsed --mentions--> RDF
""";

    private const string EdgeMarkdown = """
---
title: Edge Flow
author:
  - label:
  - label: Ada Lovelace
    sameAs:
      - https://example.com/ada
    type: schema:Person
about:
  -
  - RDF
entity_hints:
  - label: Ada Lovelace
    type: schema:Thing
  - Scalar Deterministic Hint
  - label:
  - label: Tool
    sameAs:
      - https://example.com/tool
    type: schema:SoftwareApplication
---
Intro mentions [[   ]] and [   ](https://example.com/blank) and [Relative](./relative.md) and [Absolute](https://example.com/absolute).

article --mentions--> Tool
   --mentions--> Tool
https://example.com/subject --creator--> https://example.com/object
Tool --rdf:type--> https://schema.org/SoftwareApplication
Tool --custom:predicate--> https://example.com/custom
""";

    private const string EdgePositiveAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://kb.example/edge/> schema:mentions <https://kb.example/id/tool> .
  <https://kb.example/edge/> schema:author <https://kb.example/id/ada-lovelace> .
  <https://kb.example/id/ada-lovelace> rdf:type <https://schema.org/Person> .
  <https://kb.example/id/tool> schema:sameAs <https://example.com/tool> ;
                               rdf:type <https://schema.org/SoftwareApplication> .
  <https://example.com/subject> schema:creator <https://example.com/object> .
  <https://kb.example/id/absolute> schema:sameAs <https://example.com/absolute> .
}
""";

    private const string EdgeNegativeAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/id/relative> schema:sameAs <https://kb.example/relative.md> .
}
""";

    private const string EdgeCustomSelectQuery = """
PREFIX custom: <custom:>
SELECT ?object WHERE {
  <https://kb.example/id/tool> <custom:predicate> ?object .
}
""";

    private const string MergeMarkdown = """
---
title: Merge Flow
tags:
  - rdf
---
Merge Flow --mentions--> RDF
""";

    private const string MergePayload = """
{
  "entities": [
    {
      "id": "",
      "type": "schema:SoftwareApplication",
      "label": "RDF",
      "sameAs": [
        "https://example.com/rdf",
        "https://www.w3.org/RDF/"
      ]
    }
  ],
  "assertions": [
    {
      "s": "ARTICLE",
      "p": "schema:mentions",
      "o": "https://kb.example/id/rdf",
      "confidence": 0.99,
      "source": "https://kb.example/source/chat"
    },
    {
      "s": "ARTICLE",
      "p": "sameas",
      "o": "https://example.com/rdf",
      "confidence": 0.6,
      "source": ""
    }
  ]
}
""";

    private const string MergeAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://kb.example/merge/> schema:mentions <https://kb.example/id/rdf> ;
                                schema:sameAs <https://example.com/rdf> .
  <https://kb.example/id/rdf> rdf:type <https://schema.org/SoftwareApplication> ;
                              schema:sameAs <https://www.w3.org/RDF/> .
}
""";

    private const string EmptyChatMarkdown = """
---
title: Empty Chat
---
Empty Chat --mentions--> RDF
""";

    private const string EmptyChatAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/empty-chat/> schema:mentions <https://kb.example/id/rdf> .
}
""";

    private const string ConverterMarkdown = """
# Converted Content

Converted Content --https://example.com/predicate/related--> https://example.com/object
Converted Content --unknown predicate--> https://example.com/unknown
Converted Content --about--> https://example.com/about
Converted Content --author--> https://example.com/author
Converted Content --description--> https://example.com/description
Converted Content --keywords--> https://example.com/keywords
Converted Content --   --> https://example.com/ignored
""";

    private const string ConverterRelatedAskQuery = """
ASK WHERE {
  <https://kb.example/document/> <https://example.com/predicate/related> <https://example.com/object> .
}
""";

    private const string ConverterUnknownPredicateAskQuery = """
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://kb.example/document/> kb:unknown-predicate <https://example.com/unknown> .
}
""";

    private const string ConverterNormalizedPredicatesAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/document/> schema:about <https://example.com/about> ;
                                 schema:author <https://example.com/author> ;
                                 schema:description <https://example.com/description> ;
                                 schema:keywords <https://example.com/keywords> .
}
""";

    private const string DirectoryPlainMarkdown = """
plain --mentions--> RDF
""";

    private const string DirectoryMarkerOnlyMarkdown = """
---
""";

    private const string DirectoryUnclosedMarkdown = """
---
title: Not Closed

# Unclosed Heading

Unclosed Heading --mentions--> SPARQL
""";

    private const string DirectoryListMarkdown = """
---
- list
- frontmatter
---
# List YAML

List YAML --mentions--> Graph
""";

    private const string DirectoryPositiveAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/plain/> schema:mentions <https://kb.example/id/rdf> .
}
""";

    private const string FactMergerAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <urn:external:subject> schema:sameAs <https://example.com/root-tool> .
  <https://kb.example/fact-flow/> schema:about <https://example.com/about> ;
                                 schema:author <https://example.com/author> ;
                                 schema:creator <https://example.com/creator> ;
                                 schema:mentions <https://example.com/direct-mention> ;
                                 schema:about <https://example.com/direct-about> ;
                                 schema:author <https://example.com/direct-author> ;
                                 schema:creator <https://example.com/direct-creator> ;
                                 schema:sameAs <https://example.com/direct-same-as> ;
                                 kb:relatedTo <https://example.com/related> ;
                                 <https://example.com/predicate/absolute> <https://example.com/absolute> .
  <https://kb.example/id/graph-tool> rdf:type <https://schema.org/Organization> ;
                                     schema:sameAs <https://example.com/root-tool> ;
                                     schema:sameAs <https://kb.example/root-flow/> .
  <https://kb.example/id/unprefixed-type> rdf:type <https://schema.org/SoftwareApplication> .
}
""";

    private const string FactMergerUnknownPredicateAskQuery = """
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://kb.example/fact-flow/> kb:plain-unknown <https://example.com/unknown> .
}
""";

    private static readonly string[] ExpectedComplexKeywords = [ComplexKeywordAlpha, ComplexKeywordBeta];
    private static readonly byte[] BrokenBinaryContent = [9, 8, 7];
    private static readonly string[] ExpectedMergeFactKeywords = [ComplexKeywordAlpha, ComplexKeywordBeta];

    [Test]
    public async Task Root_document_parser_rejects_invalid_yaml_output()
    {
        var rootParser = new RootMarkdownDocumentParser();
        Should.Throw<InvalidDataException>(() =>
            rootParser.Parse(new RootMarkdownDocumentSource(InvalidRootFlowMarkdown, ContentInvalidRootFlowPath, BaseUrl)));

        await Task.CompletedTask;
    }

    [Test]
    public async Task Valid_markdown_file_flow_converts_yaml_to_queryable_graph()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempRootPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, ComplexFileName);
            await File.WriteAllTextAsync(filePath, ComplexMarkdown);

            var pipeline = new MarkdownKnowledgePipeline(BaseUri);
            var result = await pipeline.BuildFromFileAsync(filePath);

            var metadata = await result.Graph.ExecuteSelectAsync(ComplexSelectQuery);
            metadata.Rows.Count.ShouldBe(2);
            metadata.Rows.All(row => row.Values[SummaryDictionaryKey] == ComplexSummary).ShouldBeTrue();
            metadata.Rows.Select(row => row.Values[KeywordDictionaryKey]).ShouldBe(ExpectedComplexKeywords);

            result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
            result.Facts.Entities.ShouldBeEmpty();
            result.Facts.Assertions.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Broken_markdown_file_flow_rejects_bad_yaml_and_bad_queries()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempRootPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, InvalidYamlFileName);
            await File.WriteAllTextAsync(filePath, BrokenYamlMarkdown);

            var pipeline = new MarkdownKnowledgePipeline(BaseUri);
            await Should.ThrowAsync<Exception>(async () => await pipeline.BuildFromFileAsync(filePath));

            var result = await pipeline.BuildAsync([
                new MarkdownSourceDocument(ContentEdgePath, EdgeMarkdown),
            ]);

            await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
                await result.Graph.ExecuteSelectAsync(BrokenYamlInsertQuery));
            await Should.ThrowAsync<Exception>(async () =>
                await result.Graph.ExecuteSelectAsync(BrokenYamlMalformedSelectQuery));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Pipeline_keeps_document_metadata_queryable_when_chat_returns_empty_payload()
    {
        var chatClient = new TestChatClient((_, _) => EmptyJsonPayload);
        var pipeline = new MarkdownKnowledgePipeline(BaseUri, chatClient);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ContentEmptyChatPath, EmptyChatMarkdown),
        ]);

        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();
        var rows = await result.Graph.SearchAsync(EmptyChatTitle);
        rows.Rows.Count.ShouldBe(1);
        chatClient.CallCount.ShouldBe(1);
    }

    [Test]
    public async Task Converter_content_flow_builds_graph_with_media_override_and_generated_document_path()
    {
        var converter = new KnowledgeSourceDocumentConverter();
        var document = converter.ConvertContent(
            ConverterMarkdown,
            options: new KnowledgeDocumentConversionOptions
            {
                MediaType = CustomMediaType,
            });

        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([document]);

        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
        result.Facts.Assertions.ShouldBeEmpty();
        var rows = await result.Graph.SearchAsync(ConverterTitle);
        rows.Rows.Count.ShouldBe(1);
    }

    [Test]
    public async Task Directory_flow_handles_parser_edge_cases_and_unsupported_file_policy()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempRootPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, PlainMarkdownFileName), DirectoryPlainMarkdown);
            await File.WriteAllBytesAsync(Path.Combine(root, BrokenBinFileName), BrokenBinaryContent);

            var pipeline = new MarkdownKnowledgePipeline(BaseUri);
            var result = await pipeline.BuildFromDirectoryAsync(root);

            result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
            result.Documents.Single().Title.ShouldBe(PlainMarkdownFileName.Replace(".md", string.Empty, StringComparison.Ordinal));

            await File.WriteAllTextAsync(Path.Combine(root, MarkerOnlyFileName), DirectoryMarkerOnlyMarkdown);
            await File.WriteAllTextAsync(Path.Combine(root, UnclosedFileName), DirectoryUnclosedMarkdown);
            await File.WriteAllTextAsync(Path.Combine(root, ListYamlFileName), DirectoryListMarkdown);
            await Should.ThrowAsync<Exception>(async () => await pipeline.BuildFromDirectoryAsync(root));

            var converter = new KnowledgeSourceDocumentConverter();
            await Should.ThrowAsync<NotSupportedException>(async () =>
            {
                await foreach (var _ in converter.ConvertDirectoryAsync(
                                   root,
                                   new KnowledgeDocumentConversionOptions { SkipUnsupportedFiles = false }))
                {
                }
            });
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Fact_merger_and_graph_builder_flow_loads_public_fact_batches_into_queryable_graph()
    {
        var baseUri = BaseUri;
        var merger = new KnowledgeFactMerger(baseUri);
        var merged = merger.Merge(
            new KnowledgeExtractionResult
            {
                Entities =
                [
                    new KnowledgeEntityFact
                    {
                        Label = MergeGraphToolLabel,
                        Type = MergeSchemaThingType,
                        SameAs = [RootToolSameAs],
                    },
                    new KnowledgeEntityFact
                    {
                        Label = MergeGraphToolLabel,
                        Type = MergeSchemaOrganizationType,
                        SameAs = [RootToolSameAs, RootFlowCanonicalUrl],
                        Confidence = 0.95,
                    },
                ],
                Assertions =
                [
                    new KnowledgeAssertionFact
                    {
                        SubjectId = string.Empty,
                        Predicate = MergeMentionPredicateValue,
                        ObjectId = MergeSkippedObjectValue,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerExternalSubject,
                        Predicate = MergeSameAsPredicateValue,
                        ObjectId = RootToolSameAs,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeAboutPredicateValue,
                        ObjectId = FactMergerAboutObject,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeAuthorPredicateValue,
                        ObjectId = FactMergerAuthorObject,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeCreatorPredicateValue,
                        ObjectId = FactMergerCreatorObject,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeRelatedToPredicateValue,
                        ObjectId = FactMergerRelatedObject,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergePlainUnknownPredicateValue,
                        ObjectId = FactMergerUnknownObject,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeAbsolutePredicateValue,
                        ObjectId = FactMergerAbsolutePredicateObject,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = MergeNotAUriValue,
                        Predicate = MergeMentionPredicateValue,
                        ObjectId = MergeDirectIgnoredObjectValue,
                    },
                ],
            });

        var graph = new KnowledgeGraphBuilder(baseUri).Build(
            [],
            merged with
            {
                Entities =
                [
                    .. merged.Entities,
                    new KnowledgeEntityFact
                    {
                        Label = MergeUnprefixedTypeLabel,
                        Type = FactMergerUnprefixedType,
                    },
                ],
                Assertions =
                [
                    .. merged.Assertions,
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergePlainUnknownPredicateValue,
                        ObjectId = FactMergerUnknownObject,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeMentionPredicateValue,
                        ObjectId = MergeDirectMentionObjectValue,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeAboutPredicateValue,
                        ObjectId = MergeDirectAboutObjectValue,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeAuthorPredicateValue,
                        ObjectId = MergeDirectAuthorObjectValue,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeCreatorPredicateValue,
                        ObjectId = MergeDirectCreatorObjectValue,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = FactMergerSubjectRoot,
                        Predicate = MergeSameAsPredicateValue,
                        ObjectId = MergeDirectSameAsObjectValue,
                    },
                    new KnowledgeAssertionFact
                    {
                        SubjectId = MergeNotAUriValue,
                        Predicate = MergeMentionPredicateValue,
                        ObjectId = MergeDirectIgnoredObjectValue,
                    },
                ],
            },
            KnowledgeGraphBuildOptions.Default);

        var ask = await graph.ExecuteAskAsync(FactMergerAskQuery);
        ask.ShouldBeTrue();

        var unknown = await graph.ExecuteAskAsync(FactMergerUnknownPredicateAskQuery);
        unknown.ShouldBeFalse();
    }
}
