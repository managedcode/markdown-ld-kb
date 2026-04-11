using Shouldly;
using ManagedCode.MarkdownLd.Kb.Parsing;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownDocumentParserTests
{
    [Test]
    public async Task Parse_reads_front_matter_sections_chunks_and_links_from_fixture()
    {
        const string markdown = """
            ---
            title: Markdown-LD Knowledge Bank
            description: A knowledge graph built from Markdown notes.
            date_published: 2026-04-11
            date_modified: 2026-04-11
            tags:
              - rdf
              - sparql
              - knowledge graph
            about:
              - knowledge graph
            author:
              - label: ManagedCode
                type: schema:Organization
            entity_hints:
              - label: RDF
                type: schema:Thing
                sameAs:
                  - https://www.w3.org/RDF/
              - label: SPARQL
                type: schema:Thing
                sameAs:
                  - https://www.w3.org/TR/sparql11-query/
            ---
            # Markdown-LD Knowledge Bank

            Markdown-LD Knowledge Bank uses [[RDF]] and [SPARQL](https://www.w3.org/TR/sparql11-query/).

            ## Graph

            RDF --mentions--> SPARQL
            RDF --schema:creator--> ManagedCode
            Malformed --schema:mentions-->
            """;

        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(new MarkdownDocumentSource(
            markdown,
            "content/2026/04/markdown-ld-knowledge-bank.md",
            "https://kb.example"),
            new MarkdownParsingOptions { ChunkTokenTarget = 20 });

        document.DocumentId.ShouldBe("https://kb.example/2026/04/markdown-ld-knowledge-bank/");
        document.ContentPath.ShouldBe("content/2026/04/markdown-ld-knowledge-bank.md");
        document.BaseUri.ShouldNotBeNull();
        document.BaseUri!.AbsoluteUri.ShouldBe("https://kb.example/");
        document.FrontMatter.Title.ShouldBe("Markdown-LD Knowledge Bank");
        document.FrontMatter.Summary.ShouldBe("A knowledge graph built from Markdown notes.");
        document.FrontMatter.About.ShouldBe(new[] { "knowledge graph" });
        document.FrontMatter.DatePublished.ShouldBe("2026-04-11");
        document.FrontMatter.DateModified.ShouldBe("2026-04-11");
        document.FrontMatter.Authors.ShouldBe(new[] { "ManagedCode" });
        document.FrontMatter.Tags.ShouldBe(new[] { "rdf", "sparql", "knowledge graph" });
        document.FrontMatter.EntityHints.Count.ShouldBe(2);
        document.FrontMatter.EntityHints[0].Label.ShouldBe("RDF");
        document.FrontMatter.EntityHints[0].Type.ShouldBe("schema:Thing");
        document.FrontMatter.EntityHints[0].SameAs.ShouldBe(new[] { "https://www.w3.org/RDF/" });
        document.FrontMatter.EntityHints[1].Label.ShouldBe("SPARQL");
        document.FrontMatter.EntityHints[1].SameAs.ShouldBe(new[] { "https://www.w3.org/TR/sparql11-query/" });

        document.Sections.Count.ShouldBe(2);
        document.Chunks.Count.ShouldBe(2);
        document.Links.Count.ShouldBe(2);

        document.Sections[0].HeadingLevel.ShouldBe(1);
        document.Sections[0].HeadingText.ShouldBe("Markdown-LD Knowledge Bank");
        document.Sections[0].HeadingPath.ShouldBe(new[] { "Markdown-LD Knowledge Bank" });
        document.Sections[1].HeadingLevel.ShouldBe(2);
        document.Sections[1].HeadingPath.ShouldBe(new[] { "Markdown-LD Knowledge Bank", "Graph" });

        document.Chunks[0].Markdown.ShouldContain("RDF");
        document.Chunks[0].Markdown.ShouldContain("SPARQL");
        document.Chunks[0].Links.Count.ShouldBe(2);
        document.Chunks[1].Links.Count.ShouldBe(0);

        var wikiLink = document.Links.Single(link => link.Kind == MarkdownLinkKind.WikiLink);
        wikiLink.Target.ShouldBe("RDF");
        wikiLink.DisplayText.ShouldBe("RDF");
        wikiLink.IsExternal.ShouldBeFalse();
        wikiLink.IsDocumentLink.ShouldBeFalse();

        var markdownLink = document.Links.Single(link => link.Kind == MarkdownLinkKind.MarkdownLink);
        markdownLink.Target.ShouldBe("https://www.w3.org/TR/sparql11-query/");
        markdownLink.IsExternal.ShouldBeTrue();
        markdownLink.ResolvedTarget.ShouldBe("https://www.w3.org/TR/sparql11-query/");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parse_stable_chunk_ids_follow_document_identity_and_path()
    {
        const string markdown = """
            # Heading

            First paragraph.

            Second paragraph with [reference](./reference.md).
            """;

        var parser = new MarkdownDocumentParser();
        var first = parser.Parse(new MarkdownDocumentSource(markdown, "content/one.md", "https://kb.example"), new MarkdownParsingOptions { ChunkTokenTarget = 1 });
        var second = parser.Parse(new MarkdownDocumentSource(markdown, "content/one.md", "https://kb.example"), new MarkdownParsingOptions { ChunkTokenTarget = 1 });
        var moved = parser.Parse(new MarkdownDocumentSource(markdown, "content/two.md", "https://kb.example"), new MarkdownParsingOptions { ChunkTokenTarget = 1 });

        first.DocumentId.ShouldBe(second.DocumentId);
        first.Chunks.Select(chunk => chunk.ChunkId).ShouldBe(second.Chunks.Select(chunk => chunk.ChunkId));
        first.DocumentId.ShouldBe(MarkdownDocumentParser.DocumentIdFromPath("content/one.md", "https://kb.example"));
        first.Chunks.Count.ShouldBe(2);
        first.Chunks[0].ChunkId.ShouldBe(MarkdownDocumentParser.ComputeChunkId(first.DocumentId, first.Chunks[0].Markdown));
        moved.DocumentId.ShouldNotBe(first.DocumentId);
        moved.Chunks[0].ChunkId.ShouldNotBe(first.Chunks[0].ChunkId);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parse_uses_canonical_url_and_parses_plural_authors()
    {
        const string markdown = """
            ---
            title: Canonical Article
            canonical_url: https://kb.example/articles/canonical/
            description: Canonical summary.
            authors:
              - Ada Lovelace
              - Grace Hopper
            tags: rdf, markdown
            about:
              - Graphs
              - RDF
            entity_hints:
              - label: RDF
                type: schema:Thing
                sameAs:
                  - https://www.w3.org/RDF/
            ---
            # Canonical Article

            Body text.
            """;

        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(new MarkdownDocumentSource(markdown, "content/ignored.md", "https://kb.example"));

        document.DocumentId.ShouldBe("https://kb.example/articles/canonical/");
        document.FrontMatter.Title.ShouldBe("Canonical Article");
        document.FrontMatter.Summary.ShouldBe("Canonical summary.");
        document.FrontMatter.Authors.ShouldBe(new[] { "Ada Lovelace", "Grace Hopper" });
        document.FrontMatter.Tags.ShouldBe(new[] { "rdf", "markdown" });
        document.FrontMatter.About.ShouldBe(new[] { "Graphs", "RDF" });
        document.FrontMatter.EntityHints.ShouldContain(hint => hint.Label == "RDF" && hint.SameAs!.Contains("https://www.w3.org/RDF/"));
        document.Sections.Count.ShouldBe(1);
        document.Chunks.Count.ShouldBe(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parse_preserves_malformed_front_matter_and_still_builds_sections()
    {
        const string markdown = """
            ---
            title: [broken
            tags:
              - still
              - parses
            ---

            # Heading

            Body text.
            """;

        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(new MarkdownDocumentSource(markdown, "content/broken.md", "https://kb.example"));

        document.DocumentId.ShouldBe("https://kb.example/broken/");
        document.FrontMatter.Values.Count.ShouldBe(0);
        document.FrontMatter.RawYaml.ShouldContain("title: [broken");
        document.Sections.Count.ShouldBe(1);
        document.Sections[0].HeadingPath.ShouldBe(new[] { "Heading" });
        document.Chunks.Count.ShouldBe(1);
        document.Chunks[0].Markdown.ShouldBe("Body text.");

        await Task.CompletedTask;
    }
}
