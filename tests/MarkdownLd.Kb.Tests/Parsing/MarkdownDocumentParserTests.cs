using ManagedCode.MarkdownLd.Kb.Parsing;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests;

public sealed class MarkdownDocumentParserTests
{
    private const string MarkdownWithFrontMatter = """
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

    private const string MarkdownStableChunkIds = """
        # Heading

        First paragraph.

        Second paragraph with [reference](./reference.md).
        """;

    private const string MarkdownCanonicalUrl = """
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

    private const string MarkdownMalformedFrontMatter = """
        ---
        title: [broken
        tags:
          - still
          - parses
        ---

        # Heading

        Body text.
        """;

    private const string BaseUri = "https://kb.example";
    private const string BaseUriResolved = "https://kb.example/";
    private const string ContentPath = "content/2026/04/markdown-ld-knowledge-bank.md";
    private const string SourcePathInput = ContentPath;
    private const string OtherContentPath = "content/two.md";
    private const string DocumentId = "https://kb.example/2026/04/markdown-ld-knowledge-bank/";
    private const string ArticleTitle = "Markdown-LD Knowledge Bank";
    private const string ArticleSummary = "A knowledge graph built from Markdown notes.";
    private const string DatePublished = "2026-04-11";
    private const string DateModified = "2026-04-11";
    private const string ManagedCode = "ManagedCode";
    private const string ManagedCodeOrganization = "schema:Organization";
    private const string RdfLabel = "RDF";
    private const string SparqlLabel = "SPARQL";
    private const string KnowledgeGraphLabel = "knowledge graph";
    private const string RdfSameAs = "https://www.w3.org/RDF/";
    private const string SparqlSameAs = "https://www.w3.org/TR/sparql11-query/";
    private const string CanonicalContentPath = "content/ignored.md";
    private const string CanonicalDocumentId = "https://kb.example/articles/canonical/";
    private const string CanonicalArticleTitle = "Canonical Article";
    private const string CanonicalSummary = "Canonical summary.";
    private const string AdaLovelace = "Ada Lovelace";
    private const string GraceHopper = "Grace Hopper";
    private const string Graphs = "Graphs";
    private const string BodyText = "Body text.";
    private const string FallbackDocumentId = "https://kb.example/broken/";
    private const string FallbackTitle = "Fallback Title";
    private const string FallbackSourcePath = "content/broken.md";
    private const string BrokenTitle = "title: [broken";
    private const string Heading = "Heading";
    private const string MarkdownKnowledgeBankHeading = "Markdown-LD Knowledge Bank";
    private const string ReferenceLinkTarget = "https://www.w3.org/TR/sparql11-query/";
    private const string WikiLinkTarget = "RDF";
    private const string WikiLinkDisplay = "RDF";
    private const string ReferenceLinkDisplay = "SPARQL";
    private const string SchemaThing = "schema:Thing";

    private static readonly string[] ExpectedFrontMatterAbout = [KnowledgeGraphLabel];
    private static readonly string[] ExpectedFrontMatterAuthors = [ManagedCode];
    private static readonly string[] ExpectedFrontMatterTags = ["rdf", "sparql", "knowledge graph"];
    private static readonly string[] ExpectedEntityHintLabels = [RdfLabel, SparqlLabel];
    private static readonly string[] ExpectedEntityHintSameAs = [RdfSameAs, SparqlSameAs];
    private static readonly string[] ExpectedSection0HeadingPath = [MarkdownKnowledgeBankHeading];
    private static readonly string[] ExpectedSection1HeadingPath = [MarkdownKnowledgeBankHeading, "Graph"];
    private static readonly string[] ExpectedCanonicalAuthors = [AdaLovelace, GraceHopper];
    private static readonly string[] ExpectedCanonicalTags = ["rdf", "markdown"];
    private static readonly string[] ExpectedCanonicalAbout = [Graphs, RdfLabel];
    private static readonly string[] ExpectedFallbackHeadingPath = [Heading];

    [Test]
    public async Task Parse_reads_front_matter_sections_chunks_and_links_from_fixture()
    {
        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(
            new MarkdownDocumentSource(MarkdownWithFrontMatter, ContentPath, BaseUri),
            new MarkdownParsingOptions { ChunkTokenTarget = 20 });

        document.DocumentId.ShouldBe(DocumentId);
        document.ContentPath.ShouldBe(ContentPath);
        document.BaseUri.ShouldNotBeNull();
        document.BaseUri!.AbsoluteUri.ShouldBe(BaseUriResolved);
        document.FrontMatter.Title.ShouldBe(ArticleTitle);
        document.FrontMatter.Summary.ShouldBe(ArticleSummary);
        document.FrontMatter.About.ShouldBe(ExpectedFrontMatterAbout);
        document.FrontMatter.DatePublished.ShouldBe(DatePublished);
        document.FrontMatter.DateModified.ShouldBe(DateModified);
        document.FrontMatter.Authors.ShouldBe(ExpectedFrontMatterAuthors);
        document.FrontMatter.Tags.ShouldBe(ExpectedFrontMatterTags);
        document.FrontMatter.EntityHints.Count.ShouldBe(2);
        document.FrontMatter.EntityHints[0].Label.ShouldBe(RdfLabel);
        document.FrontMatter.EntityHints[0].Type.ShouldBe(SchemaThing);
        document.FrontMatter.EntityHints[0].SameAs.ShouldBe([RdfSameAs]);
        document.FrontMatter.EntityHints[1].Label.ShouldBe(SparqlLabel);
        document.FrontMatter.EntityHints[1].SameAs.ShouldBe([SparqlSameAs]);

        document.Sections.Count.ShouldBe(2);
        document.Chunks.Count.ShouldBe(2);
        document.Links.Count.ShouldBe(2);

        document.Sections[0].HeadingLevel.ShouldBe(1);
        document.Sections[0].HeadingText.ShouldBe(MarkdownKnowledgeBankHeading);
        document.Sections[0].HeadingPath.ShouldBe(ExpectedSection0HeadingPath);
        document.Sections[1].HeadingLevel.ShouldBe(2);
        document.Sections[1].HeadingPath.ShouldBe(ExpectedSection1HeadingPath);

        document.Chunks[0].Markdown.ShouldContain(RdfLabel);
        document.Chunks[0].Markdown.ShouldContain(SparqlLabel);
        document.Chunks[0].Links.Count.ShouldBe(2);
        document.Chunks[1].Links.Count.ShouldBe(0);

        var wikiLink = document.Links.Single(link => link.Kind == MarkdownLinkKind.WikiLink);
        wikiLink.Target.ShouldBe(WikiLinkTarget);
        wikiLink.DisplayText.ShouldBe(WikiLinkDisplay);
        wikiLink.IsExternal.ShouldBeFalse();
        wikiLink.IsDocumentLink.ShouldBeFalse();

        var markdownLink = document.Links.Single(link => link.Kind == MarkdownLinkKind.MarkdownLink);
        markdownLink.Target.ShouldBe(ReferenceLinkTarget);
        markdownLink.IsExternal.ShouldBeTrue();
        markdownLink.ResolvedTarget.ShouldBe(ReferenceLinkTarget);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parse_stable_chunk_ids_follow_document_identity_and_path()
    {
        var parser = new MarkdownDocumentParser();
        var first = parser.Parse(new MarkdownDocumentSource(MarkdownStableChunkIds, SourcePathInput, BaseUri), new MarkdownParsingOptions { ChunkTokenTarget = 1 });
        var second = parser.Parse(new MarkdownDocumentSource(MarkdownStableChunkIds, SourcePathInput, BaseUri), new MarkdownParsingOptions { ChunkTokenTarget = 1 });
        var moved = parser.Parse(new MarkdownDocumentSource(MarkdownStableChunkIds, OtherContentPath, BaseUri), new MarkdownParsingOptions { ChunkTokenTarget = 1 });

        first.DocumentId.ShouldBe(second.DocumentId);
        first.Chunks.Select(chunk => chunk.ChunkId).ShouldBe(second.Chunks.Select(chunk => chunk.ChunkId));
        first.DocumentId.ShouldBe(MarkdownDocumentParser.DocumentIdFromPath(SourcePathInput, BaseUri));
        first.Chunks.Count.ShouldBe(2);
        first.Chunks[0].ChunkId.ShouldBe(MarkdownDocumentParser.ComputeChunkId(first.DocumentId, first.Chunks[0].Markdown));
        moved.DocumentId.ShouldNotBe(first.DocumentId);
        moved.Chunks[0].ChunkId.ShouldNotBe(first.Chunks[0].ChunkId);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parse_uses_canonical_url_and_parses_plural_authors()
    {
        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(new MarkdownDocumentSource(MarkdownCanonicalUrl, CanonicalContentPath, BaseUri));

        document.DocumentId.ShouldBe(CanonicalDocumentId);
        document.FrontMatter.Title.ShouldBe(CanonicalArticleTitle);
        document.FrontMatter.Summary.ShouldBe(CanonicalSummary);
        document.FrontMatter.Authors.ShouldBe(ExpectedCanonicalAuthors);
        document.FrontMatter.Tags.ShouldBe(ExpectedCanonicalTags);
        document.FrontMatter.About.ShouldBe(ExpectedCanonicalAbout);
        document.FrontMatter.EntityHints.ShouldContain(hint => hint.Label == RdfLabel && hint.SameAs!.Contains(RdfSameAs));
        document.Sections.Count.ShouldBe(1);
        document.Chunks.Count.ShouldBe(1);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Parse_preserves_malformed_front_matter_and_still_builds_sections()
    {
        var parser = new MarkdownDocumentParser();
        var document = parser.Parse(new MarkdownDocumentSource(MarkdownMalformedFrontMatter, FallbackSourcePath, BaseUri));

        document.DocumentId.ShouldBe(FallbackDocumentId);
        document.FrontMatter.Values.Count.ShouldBe(0);
        document.FrontMatter.RawYaml.ShouldContain(BrokenTitle);
        document.Sections.Count.ShouldBe(1);
        document.Sections[0].HeadingPath.ShouldBe(ExpectedFallbackHeadingPath);
        document.Chunks.Count.ShouldBe(1);
        document.Chunks[0].Markdown.ShouldBe(BodyText);

        await Task.CompletedTask;
    }
}
