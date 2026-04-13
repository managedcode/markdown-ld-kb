using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeSourceDocumentConverterFlowTests
{
    private const string TempDirectoryPrefix = "markdown-ld-kb-";
    private const string GuidFormat = "N";
    private const string DocsDirectoryName = "docs";
    private const string NoteFileName = "note.md";
    private const string NoteRelativePath = "docs/note.md";
    private const string PlainFileName = "plain.txt";
    private const string IgnoredBinaryFileName = "ignored.bin";
    private const string DirectFileName = "direct.md";
    private const string DefaultDocumentPath = "document.md";
    private const string NoExtensionFileName = "README";
    private const string MissingDirectoryName = "missing";
    private const string CustomMediaType = "application/x-custom-markdown";
    private const string CustomMediaTypeWithPadding = " application/x-custom-markdown ";
    private const string BoundaryCanonicalUriText = "https://kb.example/canonical/boundary/";
    private const string BaseUriText = "https://kb.example/";
    private const string TextMarkdownMediaType = "text/markdown";
    private const string TextPlainMediaType = "text/plain";
    private const string FileGraphTitle = "File Graph";
    private const string PlainTitle = "plain";
    private const string RdfSearchTerm = "rdf";
    private const string SparqlSearchTerm = "sparql";
    private const string SparqlEntityLabel = "SPARQL";
    private const string FactSearchTerm = "fact";
    private const string ManyMarkdownFileName = "01-markdown.md";
    private const string ManyLongMarkdownFileName = "02-long.markdown";
    private const string ManyMdxFileName = "03-component.mdx";
    private const string ManyPlainFileName = "04-plain.txt";
    private const string ManyLogFileName = "05-events.log";
    private const string ManyCsvFileName = "06-catalog.csv";
    private const string ManyJsonFileName = "07-data.json";
    private const string ManyJsonLinesFileName = "08-lines.jsonl";
    private const string ManyYamlFileName = "09-config.yaml";
    private const string ManyYmlFileName = "10-config.yml";
    private const string FileGraphMarkdown = """
---
title: File Graph
tags:
  - graph
---
File Graph --mentions--> RDF
""";
    private const string PlainTextFixture = """
plain --mentions--> RDF
""";
    private const string DirectMarkdownFixture = """
---
title: Direct File
---
Direct File --mentions--> SPARQL
""";
    private const string ManyMarkdownFixture = """
---
title: Markdown Shape
tags:
  - examples
---
# Markdown Shape

article --mentions--> Markdown Fact
[Markdown Link](https://example.com/markdown-link)
""";
    private const string ManyLongMarkdownFixture = """
# Long Markdown Shape

article --mentions--> Long Markdown Fact
""";
    private const string ManyMdxFixture = """
export const meta = { title: 'MDX Shape' }

# MDX Shape

article --mentions--> Mdx Fact
<MdxNote>noise</MdxNote>
""";
    private const string ManyPlainFixture = """
Plain text notes
article --mentions--> Plain Fact
""";
    private const string ManyLogFixture = """
2026-04-11T10:00:00Z boot
article --mentions--> Log Fact
""";
    private const string ManyCsvFixture = """
fact
article --mentions--> Csv Fact
Csv Fact --sameas--> https://example.com/csv-fact
""";
    private const string ManyJsonFixture = """
[
  "article --mentions--> Json Fact",
  "Json Fact --sameas--> https://example.com/json-fact"
]
""";
    private const string ManyJsonLinesFixture = """
"article --mentions--> Json Lines Fact"
"Json Lines Fact --sameas--> https://example.com/json-lines-fact"
""";
    private const string ManyYamlFixture = """
facts:
  - article --mentions--> Yaml Fact
  - Yaml Fact --sameas--> https://example.com/yaml-fact
""";
    private const string ManyYmlFixture = """
facts:
  - article --mentions--> Yml Fact
  - Yml Fact --sameas--> https://example.com/yml-fact
""";
    private const string ManyShapesAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/01-markdown/> schema:mentions <https://kb.example/id/markdown-fact> ;
                                      schema:keywords "examples" .
  <https://kb.example/01-markdown/> schema:mentions <https://kb.example/id/markdown-link> .
  <https://kb.example/id/markdown-link> schema:sameAs <https://example.com/markdown-link> .
  <https://kb.example/02-long/> schema:mentions <https://kb.example/id/long-markdown-fact> .
  <https://kb.example/03-component/> schema:mentions <https://kb.example/id/mdx-fact> .
  <https://kb.example/04-plain/> schema:mentions <https://kb.example/id/plain-fact> .
  <https://kb.example/05-events/> schema:mentions <https://kb.example/id/log-fact> .
  <https://kb.example/06-catalog/> schema:mentions <https://kb.example/id/csv-fact> .
  <https://kb.example/07-data/> schema:mentions <https://kb.example/id/json-fact> .
  <https://kb.example/08-lines/> schema:mentions <https://kb.example/id/json-lines-fact> .
  <https://kb.example/09-config/> schema:mentions <https://kb.example/id/yaml-fact> .
  <https://kb.example/10-config/> schema:mentions <https://kb.example/id/yml-fact> .
  <https://kb.example/id/csv-fact> schema:sameAs <https://example.com/csv-fact> .
  <https://kb.example/id/json-fact> schema:sameAs <https://example.com/json-fact> .
  <https://kb.example/id/json-lines-fact> schema:sameAs <https://example.com/json-lines-fact> .
  <https://kb.example/id/yaml-fact> schema:sameAs <https://example.com/yaml-fact> .
  <https://kb.example/id/yml-fact> schema:sameAs <https://example.com/yml-fact> .
}
""";

    private static readonly string[] ConverterExpectedPaths = [NoteRelativePath, PlainFileName];
    private static readonly string[] ConverterExpectedMediaTypes = [TextMarkdownMediaType, TextPlainMediaType];
    private static readonly byte[] BinaryFixture = [0, 1, 2, 3];
    private static readonly string[] ManyExpectedPaths =
    [
        ManyMarkdownFileName,
        ManyLongMarkdownFileName,
        ManyMdxFileName,
        ManyPlainFileName,
        ManyLogFileName,
        ManyCsvFileName,
        ManyJsonFileName,
        ManyJsonLinesFileName,
        ManyYamlFileName,
        ManyYmlFileName,
    ];

    private static readonly Uri BoundaryCanonicalUri = new(BoundaryCanonicalUriText);

    private static readonly (string FileName, string Content)[] ManyFileShapes =
    [
        (ManyMarkdownFileName, ManyMarkdownFixture),
        (ManyLongMarkdownFileName, ManyLongMarkdownFixture),
        (ManyMdxFileName, ManyMdxFixture),
        (ManyPlainFileName, ManyPlainFixture),
        (ManyLogFileName, ManyLogFixture),
        (ManyCsvFileName, ManyCsvFixture),
        (ManyJsonFileName, ManyJsonFixture),
        (ManyJsonLinesFileName, ManyJsonLinesFixture),
        (ManyYamlFileName, ManyYamlFixture),
        (ManyYmlFileName, ManyYmlFixture),
    ];

    [Test]
    public async Task Converter_reads_files_into_document_format_and_pipeline_builds_queryable_graph()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempDirectoryPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            var docs = Path.Combine(root, DocsDirectoryName);
            Directory.CreateDirectory(docs);

            var markdownPath = Path.Combine(docs, NoteFileName);
            var textPath = Path.Combine(root, PlainFileName);
            var binaryPath = Path.Combine(root, IgnoredBinaryFileName);

            await File.WriteAllTextAsync(markdownPath, FileGraphMarkdown);
            await File.WriteAllTextAsync(textPath, PlainTextFixture);
            await File.WriteAllBytesAsync(binaryPath, BinaryFixture);

            var converter = new KnowledgeSourceDocumentConverter();
            var documents = new List<KnowledgeSourceDocument>();

            await foreach (var document in converter.ConvertDirectoryAsync(root))
            {
                documents.Add(document);
            }

            documents.Select(document => document.Path).ShouldBe(ConverterExpectedPaths);
            documents.Select(document => document.MediaType).ShouldBe(ConverterExpectedMediaTypes);

            await Should.ThrowAsync<NotSupportedException>(async () => await converter.ConvertFileAsync(binaryPath));

            var pipeline = new MarkdownKnowledgePipeline(
                new Uri(BaseUriText),
                extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
            var result = await pipeline.BuildAsync(documents);

            result.Documents.Count.ShouldBe(2);
            result.Documents.Any(document => document.Title == FileGraphTitle).ShouldBeTrue();
            result.Documents.Any(document => document.Title == PlainTitle).ShouldBeTrue();

            var rows = await result.Graph.SearchAsync(RdfSearchTerm);
            rows.Rows.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Converter_handles_boundary_inputs_and_unsupported_directory_entries()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempDirectoryPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            var converter = new KnowledgeSourceDocumentConverter();
            var defaultDocument = converter.ConvertContent(
                null,
                options: new KnowledgeDocumentConversionOptions
                {
                    CanonicalUri = BoundaryCanonicalUri,
                    MediaType = CustomMediaTypeWithPadding,
                });

            defaultDocument.Path.ShouldBe(DefaultDocumentPath);
            defaultDocument.Content.ShouldBeEmpty();
            defaultDocument.CanonicalUri.ShouldBe(BoundaryCanonicalUri);
            defaultDocument.MediaType.ShouldBe(CustomMediaType);
            KnowledgeSourceDocumentConverter.IsSupportedTextFile(NoExtensionFileName).ShouldBeFalse();

            await Should.ThrowAsync<DirectoryNotFoundException>(async () =>
            {
                await foreach (var _ in converter.ConvertDirectoryAsync(Path.Combine(root, MissingDirectoryName)))
                {
                }
            });

            var unsupportedPath = Path.Combine(root, IgnoredBinaryFileName);
            await File.WriteAllBytesAsync(unsupportedPath, BinaryFixture);

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
    public async Task Pipeline_can_build_directly_from_file_and_directory_inputs()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempDirectoryPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, DirectFileName);
            await File.WriteAllTextAsync(filePath, DirectMarkdownFixture);

            var pipeline = new MarkdownKnowledgePipeline(
                new Uri(BaseUriText),
                extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
            var fileResult = await pipeline.BuildFromFileAsync(filePath);
            fileResult.Documents.Single().SourcePath.ShouldBe(DirectFileName);
            fileResult.Facts.Entities.Any(entity => entity.Label.Contains(SparqlEntityLabel, StringComparison.Ordinal)).ShouldBeTrue();

            var directoryResult = await pipeline.BuildFromDirectoryAsync(root);
            directoryResult.Documents.Single().SourcePath.ShouldBe(DirectFileName);
            var query = await directoryResult.Graph.SearchAsync(SparqlSearchTerm);
            query.Rows.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Converter_reads_many_supported_file_shapes_into_queryable_graph()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat(TempDirectoryPrefix, Guid.NewGuid().ToString(GuidFormat)));
        Directory.CreateDirectory(root);

        try
        {
            foreach (var (fileName, content) in ManyFileShapes)
            {
                await File.WriteAllTextAsync(Path.Combine(root, fileName), content);
            }

            var pipeline = new MarkdownKnowledgePipeline(
                new Uri(BaseUriText),
                extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
            var result = await pipeline.BuildFromDirectoryAsync(root);

            result.Documents.Select(document => document.SourcePath).ShouldBe(ManyExpectedPaths);

            var search = await result.Graph.SearchAsync(FactSearchTerm);
            search.Rows.Count.ShouldBeGreaterThanOrEqualTo(10);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
