using ManagedCode.MarkdownLd.Kb.Parsing;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Parsing;

public sealed class DeterministicSectionMarkdownChunkerTests
{
    private const string BaseUri = "https://chunking.example/";
    private const string SourcePath = "content/chunker.md";
    private const int TightChunkTarget = 5;

    private const string MermaidMarkdown = """
        # Diagram

        ```mermaid
        flowchart LR

            A["Start"] --> B["Middle"]
            B --> C["End"]
        ```

        Closing paragraph.
        """;

    private const string TableMarkdown = """
        # Table

        | Name | Value | Link |
        | --- | ---: | --- |
        | RDF | 1 | [Spec](https://www.w3.org/RDF/) |
        | SPARQL | 2 | [Query](https://www.w3.org/TR/sparql11-query/) |

        After table paragraph.
        """;

    private const string NestedMarkdown = """
        # Nested

        - First item
          - Nested item
          - Nested item with code

                SELECT * WHERE { ?s ?p ?o }

        > Blockquote line
        > continued line
        """;

    private const string GridTableMarkdown = """
        # Grid

        +----------+------------------+
        | Name     | Details          |
        +==========+==================+
        | Mermaid  | graph TD         |
        +----------+------------------+
        | Table    | Grid table cell  |
        +----------+------------------+

        Tail paragraph.
        """;

    private const string HtmlAndMermaidMarkdown = """
        # Html

        <table>
          <tr>
            <td>RDF</td>
            <td>
              <pre><code>SELECT * WHERE { ?s ?p ?o }</code></pre>
            </td>
          </tr>
        </table>

        ```mermaid
        graph TD
            KB[Knowledge Bank] --> RDF[RDF Graph]
            RDF --> Search[Search]
        ```
        """;

    private const string ListMermaidMarkdown = """
        # List Mermaid

        - Parent item
          - Nested insight

            ```mermaid
            flowchart TD
                A[Chunk] --> B[Entity]
                B --> C[Assertion]
            ```

        After list.
        """;

    private const string MixedMarkdown = """
        # Overview

        Intro paragraph.

        ## Table

        | Metric | Value |
        | --- | --- |
        | precision | 0.99 |

        ## Graph

        RDF --mentions--> SPARQL

        ```mermaid
        graph TD
            A[Markdown] --> B[RDF]
        ```
        """;

    [Test]
    public async Task Chunker_keeps_mermaid_fence_intact_when_blank_lines_exist_inside_block()
    {
        var document = Parse(MermaidMarkdown);

        var mermaidChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("```mermaid", StringComparison.Ordinal));

        mermaidChunk.Markdown.ShouldContain("flowchart LR");
        mermaidChunk.Markdown.ShouldContain("B --> C");
        mermaidChunk.Markdown.ShouldContain("```");
        document.Chunks.Any(chunk => chunk.Markdown == "Closing paragraph.").ShouldBeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Chunker_keeps_markdown_table_rows_in_one_chunk()
    {
        var document = Parse(TableMarkdown);

        var tableChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("| Name | Value | Link |", StringComparison.Ordinal));

        tableChunk.Markdown.ShouldContain("| --- | ---: | --- |");
        tableChunk.Markdown.ShouldContain("| SPARQL | 2 |");
        tableChunk.Links.Count.ShouldBe(2);
        document.Chunks.Any(chunk => chunk.Markdown == "After table paragraph.").ShouldBeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Chunker_keeps_nested_lists_and_indented_code_together_and_preserves_following_blockquote()
    {
        var document = Parse(NestedMarkdown);

        var listChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("- First item", StringComparison.Ordinal));
        var blockquoteChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("> Blockquote line", StringComparison.Ordinal));

        listChunk.Markdown.ShouldContain("- Nested item");
        listChunk.Markdown.ShouldContain("SELECT * WHERE");
        blockquoteChunk.Markdown.ShouldContain("> continued line");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Chunker_preserves_complex_block_boundaries_across_sections()
    {
        var document = Parse(MixedMarkdown);

        var tableChunk = document.Chunks.Single(chunk => chunk.HeadingPath.SequenceEqual(["Overview", "Table"]));
        var graphChunk = document.Chunks.Single(chunk => chunk.HeadingPath.SequenceEqual(["Overview", "Graph"]) &&
                                                       chunk.Markdown.Contains("```mermaid", StringComparison.Ordinal));
        var graphDslChunk = document.Chunks.Single(chunk => chunk.HeadingPath.SequenceEqual(["Overview", "Graph"]) &&
                                                          chunk.Markdown.Contains("RDF --mentions--> SPARQL", StringComparison.Ordinal));

        tableChunk.Markdown.ShouldContain("| Metric | Value |");
        graphChunk.Markdown.ShouldContain("graph TD");
        graphDslChunk.Markdown.ShouldContain("RDF --mentions--> SPARQL");

        await Task.CompletedTask;
    }

    [Test]
    public async Task Chunker_keeps_grid_table_intact_even_with_tight_chunk_target()
    {
        var document = Parse(GridTableMarkdown);

        var tableChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("+----------+------------------+", StringComparison.Ordinal));

        tableChunk.Markdown.ShouldContain("| Mermaid  | graph TD         |");
        tableChunk.Markdown.ShouldContain("| Table    | Grid table cell  |");
        document.Chunks.Any(chunk => chunk.Markdown == "Tail paragraph.").ShouldBeTrue();

        await Task.CompletedTask;
    }

    [Test]
    public async Task Chunker_keeps_html_table_and_mermaid_blocks_separate_without_splitting_inside_each_block()
    {
        var document = Parse(HtmlAndMermaidMarkdown);

        var htmlChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("<table>", StringComparison.Ordinal));
        var mermaidChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("```mermaid", StringComparison.Ordinal));

        htmlChunk.Markdown.ShouldContain("<pre><code>SELECT * WHERE");
        mermaidChunk.Markdown.ShouldContain("KB[Knowledge Bank] --> RDF[RDF Graph]");
        document.Chunks.Count(chunk => chunk.HeadingPath.SequenceEqual(["Html"])).ShouldBe(2);

        await Task.CompletedTask;
    }

    [Test]
    public async Task Chunker_keeps_mermaid_inside_nested_list_item_in_one_chunk()
    {
        var document = Parse(ListMermaidMarkdown);

        var listChunk = document.Chunks.Single(chunk => chunk.Markdown.Contains("- Parent item", StringComparison.Ordinal));

        listChunk.Markdown.ShouldContain("```mermaid");
        listChunk.Markdown.ShouldContain("A[Chunk] --> B[Entity]");
        document.Chunks.Any(chunk => chunk.Markdown == "After list.").ShouldBeTrue();

        await Task.CompletedTask;
    }

    private static MarkdownDocument Parse(string markdown)
    {
        var parser = new MarkdownDocumentParser(new DeterministicSectionMarkdownChunker());
        return parser.Parse(
            new MarkdownDocumentSource(markdown, SourcePath, BaseUri),
            new MarkdownParsingOptions
            {
                Chunking = new MarkdownChunkingOptions { ChunkTokenTarget = TightChunkTarget },
            });
    }
}
