using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ManagedCode.MarkdownLd.Kb.Tests.Parsing;

public sealed class MarkdownDocumentParserTests
{
    [Fact]
    public void Parse_builds_sections_chunks_links_and_front_matter()
    {
        var parser = new MarkdownDocumentParser();
        var source = new MarkdownDocumentSource(
            ContentMarkdown: """
            ---
            title: Markdown-LD Knowledge Bank
            summary: A knowledge graph from Markdown
            about: Graphs from Markdown
            date_published: 2026-04-11
            date_modified: 2026-04-12
            authors:
              - Ada Lovelace
              - Grace Hopper
            tags:
              - graphs
              - markdown
            entity_hints:
              - label: RDF
                type: schema:Thing
                sameAs:
                  - https://www.wikidata.org/entity/Q54872
            ---

            # Overview

            Intro text with [[Knowledge Graph|knowledge graph]] and [spec](https://example.com/spec).

            More text with [reference](../reference.md).

            ## Details

            Child text.
            """,
            ContentPath: "content/2026/04/markdown-ld-knowledge-bank.md",
            BaseUrl: "https://kb.example");

        var document = parser.Parse(source, new MarkdownParsingOptions { ChunkTokenTarget = 20 });

        Assert.Equal("https://kb.example/2026/04/markdown-ld-knowledge-bank/", document.DocumentId);
        Assert.Equal("Markdown-LD Knowledge Bank", document.FrontMatter.Title);
        Assert.Equal("A knowledge graph from Markdown", document.FrontMatter.Summary);
        Assert.Equal("Graphs from Markdown", document.FrontMatter.About);
        Assert.Equal("2026-04-11", document.FrontMatter.DatePublished);
        Assert.Equal("2026-04-12", document.FrontMatter.DateModified);
        Assert.Equal(new[] { "Ada Lovelace", "Grace Hopper" }, document.FrontMatter.Authors);
        Assert.Equal(new[] { "graphs", "markdown" }, document.FrontMatter.Tags);
        Assert.Single(document.FrontMatter.EntityHints);
        Assert.Equal("RDF", document.FrontMatter.EntityHints[0].Label);
        Assert.Equal("schema:Thing", document.FrontMatter.EntityHints[0].Type);

        Assert.Equal(2, document.Sections.Count);
        Assert.Equal(3, document.Chunks.Count);
        Assert.Equal(3, document.Links.Count);

        var overview = document.Sections[0];
        Assert.Equal(1, overview.HeadingLevel);
        Assert.Equal(new[] { "Overview" }, overview.HeadingPath);
        Assert.Contains("knowledge graph", overview.Markdown);
        Assert.Equal(2, overview.Chunks.Count);
        Assert.Equal(3, overview.Links.Count);

        var details = document.Sections[1];
        Assert.Equal(2, details.HeadingLevel);
        Assert.Equal(new[] { "Overview", "Details" }, details.HeadingPath);
        Assert.Equal("Child text.", details.Markdown);
        Assert.Single(details.Chunks);
        Assert.Empty(details.Links);

        var wikiLink = document.Links.Single(link => link.Kind == MarkdownLinkKind.WikiLink);
        Assert.Equal("Knowledge Graph", wikiLink.Target);
        Assert.Equal("knowledge graph", wikiLink.DisplayText);
        Assert.False(wikiLink.IsExternal);
        Assert.False(wikiLink.IsDocumentLink);

        var documentLink = document.Links.Single(link => link.IsDocumentLink);
        Assert.Equal("../reference.md", documentLink.Target);
        Assert.Equal("https://kb.example/2026/04/reference/", documentLink.ResolvedTarget);

        var externalLink = document.Links.Single(link => link.IsExternal);
        Assert.Equal("https://example.com/spec", externalLink.Target);
        Assert.Equal("spec", externalLink.DisplayText);

        var firstChunk = document.Chunks[0];
        Assert.Equal(64, firstChunk.ChunkId.Length);
        Assert.All(firstChunk.ChunkId, ch => Assert.True(Uri.IsHexDigit(ch)));
        Assert.Equal(0, firstChunk.Order);
        Assert.Equal(new[] { "Overview" }, firstChunk.HeadingPath);
        Assert.StartsWith("Intro text", firstChunk.Markdown);
        Assert.Equal(ExpectedChunkId(document.DocumentId, firstChunk.Markdown), firstChunk.ChunkId);
    }

    [Fact]
    public void Parse_is_stable_for_repeated_input_and_changes_with_document_id()
    {
        var parser = new MarkdownDocumentParser();
        var markdown = """
            # Heading

            Same content.
            """;

        var first = parser.Parse(new MarkdownDocumentSource(markdown, "content/one.md", "https://kb.example"));
        var second = parser.Parse(new MarkdownDocumentSource(markdown, "content/one.md", "https://kb.example"));
        var moved = parser.Parse(new MarkdownDocumentSource(markdown, "content/two.md", "https://kb.example"));

        Assert.Equal(first.DocumentId, second.DocumentId);
        Assert.Equal(first.Chunks.Select(chunk => chunk.ChunkId), second.Chunks.Select(chunk => chunk.ChunkId));
        Assert.NotEqual(first.DocumentId, moved.DocumentId);
        Assert.NotEqual(first.Chunks[0].ChunkId, moved.Chunks[0].ChunkId);
    }

    [Fact]
    public void Parse_ignores_malformed_front_matter_but_still_builds_sections()
    {
        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(new MarkdownDocumentSource(
            ContentMarkdown: """
            ---
            title: [broken
            tags:
              - still
              - parses
            ---

            # Heading

            Body text.
            """));

        Assert.StartsWith("urn:markdown-ld-kb:", document.DocumentId);
        Assert.Empty(document.FrontMatter.Values);
        Assert.Contains("title: [broken", document.FrontMatter.RawYaml);
        Assert.Single(document.Sections);
        Assert.Equal(new[] { "Heading" }, document.Sections[0].HeadingPath);
        Assert.Single(document.Chunks);
        Assert.Equal("Body text.", document.Chunks[0].Markdown);
    }

    private static string ExpectedChunkId(string documentId, string markdown)
    {
        var normalized = Regex.Replace(markdown, @"\s+", " ").Trim();
        var payload = $"{documentId}\n{normalized}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
