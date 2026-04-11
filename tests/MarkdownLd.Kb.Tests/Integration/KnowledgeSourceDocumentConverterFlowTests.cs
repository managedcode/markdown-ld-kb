using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeSourceDocumentConverterFlowTests
{
    [Test]
    public async Task Converter_reads_files_into_document_format_and_pipeline_builds_queryable_graph()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat("markdown-ld-kb-", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);

        try
        {
            var docs = Path.Combine(root, "docs");
            Directory.CreateDirectory(docs);

            var markdownPath = Path.Combine(docs, "note.md");
            var textPath = Path.Combine(root, "plain.txt");
            var binaryPath = Path.Combine(root, "ignored.bin");

            await File.WriteAllTextAsync(markdownPath, """
---
title: File Graph
tags:
  - graph
---
File Graph --mentions--> RDF
""");
            await File.WriteAllTextAsync(textPath, """
plain --mentions--> RDF
""");
            await File.WriteAllBytesAsync(binaryPath, [0, 1, 2, 3]);

            var converter = new KnowledgeSourceDocumentConverter();
            var documents = new List<KnowledgeSourceDocument>();

            await foreach (var document in converter.ConvertDirectoryAsync(root))
            {
                documents.Add(document);
            }

            documents.Select(document => document.Path).ShouldBe(new[] { "docs/note.md", "plain.txt" });
            documents.Select(document => document.MediaType).ShouldBe(new[] { "text/markdown", "text/plain" });

            await Should.ThrowAsync<NotSupportedException>(async () => await converter.ConvertFileAsync(binaryPath));

            var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
            var result = await pipeline.BuildAsync(documents);

            result.Documents.Count.ShouldBe(2);
            result.Documents.Any(document => document.Title == "File Graph").ShouldBeTrue();
            result.Documents.Any(document => document.Title == "plain").ShouldBeTrue();

            var rows = await result.Graph.SearchAsync("rdf");
            rows.Rows.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Pipeline_can_build_directly_from_file_and_directory_inputs()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat("markdown-ld-kb-", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, "direct.md");
            await File.WriteAllTextAsync(filePath, """
---
title: Direct File
---
Direct File --mentions--> SPARQL
""");

            var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
            var fileResult = await pipeline.BuildFromFileAsync(filePath);
            fileResult.Documents.Single().SourcePath.ShouldBe("direct.md");
            fileResult.Facts.Entities.Any(entity => entity.Label == "SPARQL").ShouldBeTrue();

            var directoryResult = await pipeline.BuildFromDirectoryAsync(root);
            directoryResult.Documents.Single().SourcePath.ShouldBe("direct.md");
            var query = await directoryResult.Graph.SearchAsync("sparql");
            query.Rows.Count.ShouldBeGreaterThan(0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
