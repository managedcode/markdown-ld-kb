using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;
using RootMarkdownDocumentParser = ManagedCode.MarkdownLd.Kb.Parsing.MarkdownDocumentParser;
using RootMarkdownDocumentSource = ManagedCode.MarkdownLd.Kb.MarkdownDocumentSource;
using RootMarkdownParsingOptions = ManagedCode.MarkdownLd.Kb.MarkdownParsingOptions;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeFlowEdgeCaseTests
{
    [Test]
    public async Task Root_document_parser_output_feeds_pipeline_graph_queries()
    {
        var rootParser = new RootMarkdownDocumentParser();
        var parsed = rootParser.Parse(
            new RootMarkdownDocumentSource("""
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
    type: schema:SoftwareApplication
    sameAs:
      - https://example.com/root-tool
  - value: Value Hint
  - 777
---
# Root Flow

Root Flow --mentions--> Root Tool
""", "content/root-flow.md", "https://kb.example/"),
            new RootMarkdownParsingOptions { ChunkTokenTarget = 3 });

        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
        var graph = await pipeline.BuildAsync([
            new MarkdownSourceDocument(parsed.ContentPath!, parsed.BodyMarkdown, new Uri(parsed.DocumentId)),
        ]);

        var ask = await graph.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/root-flow/> schema:mentions <https://kb.example/id/root-tool> .
  <https://kb.example/id/root-tool> schema:name "Root Tool" .
}
""");
        ask.ShouldBeTrue();
    }

    [Test]
    public async Task Root_document_parser_invalid_yaml_output_still_feeds_graph_queries()
    {
        var rootParser = new RootMarkdownDocumentParser();
        var parsed = rootParser.Parse(new RootMarkdownDocumentSource("""
---
title: [unterminated
---
# Invalid Root Flow

Invalid Root Flow --mentions--> RDF
""", "content/invalid-root-flow.md", "https://kb.example/"));

        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
        var graph = await pipeline.BuildAsync([
            new MarkdownSourceDocument(parsed.ContentPath!, parsed.BodyMarkdown, new Uri(parsed.DocumentId)),
        ]);

        var ask = await graph.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/invalid-root-flow/> schema:name "Invalid Root Flow" ;
                                           schema:mentions <https://kb.example/id/rdf> .
}
""");
        ask.ShouldBeTrue();
    }

    [Test]
    public async Task Root_document_parser_scalar_and_blank_yaml_items_feed_graph_queries()
    {
        var rootParser = new RootMarkdownDocumentParser();
        var parsed = rootParser.Parse(new RootMarkdownDocumentSource("""
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
""", "content/scalar-root.md", "https://kb.example/"));

        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
        var graph = await pipeline.BuildAsync([
            new MarkdownSourceDocument(parsed.ContentPath!, parsed.BodyMarkdown, new Uri(parsed.DocumentId)),
        ]);

        var ask = await graph.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/scalar-root/> schema:name "123" ;
                                      schema:mentions <https://kb.example/id/654> .
}
""");
        ask.ShouldBeTrue();
    }

    [Test]
    public async Task Valid_markdown_file_flow_converts_yaml_to_queryable_graph()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat("markdown-ld-kb-flow-", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, "complex.md");
            await File.WriteAllTextAsync(filePath, """
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
    type: schema:SoftwareApplication
    sameAs: https://example.com/hint
  - value: Value Hint
  - 777
---
# Complex YAML

Complex YAML --mentions--> Hint Entity
""");

            var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
            var result = await pipeline.BuildFromFileAsync(filePath);

            var metadata = await result.Graph.ExecuteSelectAsync("""
PREFIX schema: <https://schema.org/>
SELECT ?summary ?keyword WHERE {
  <https://kb.example/complex/> schema:description ?summary ;
                                    schema:keywords ?keyword .
}
ORDER BY ?keyword
""");
            metadata.Rows.Count.ShouldBe(2);
            metadata.Rows.All(row => row.Values["summary"] == "Complex summary.").ShouldBeTrue();
            metadata.Rows.Select(row => row.Values["keyword"]).ShouldBe(new[] { "alpha", "beta" });

            var author = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/complex/> schema:author <https://kb.example/id/label-author> .
  <https://kb.example/id/label-author> schema:name "Label Author" .
}
""");
            author.ShouldBeTrue();

            var hint = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/complex/> schema:mentions <https://kb.example/id/hint-entity> .
  <https://kb.example/id/hint-entity> schema:sameAs <https://example.com/hint> .
}
""");
            hint.ShouldBeTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Broken_markdown_file_flow_preserves_body_and_rejects_bad_queries()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat("markdown-ld-kb-flow-", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);

        try
        {
            var filePath = Path.Combine(root, "invalid-yaml.md");
            await File.WriteAllTextAsync(filePath, """
---
title: [unterminated
---
# Still Parsed

Still Parsed --mentions--> RDF
""");

            var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
            var result = await pipeline.BuildFromFileAsync(filePath);

            var title = await result.Graph.ExecuteSelectAsync("""
PREFIX schema: <https://schema.org/>
SELECT ?title WHERE {
  <https://kb.example/invalid-yaml/> schema:name ?title .
}
""");
            title.Rows.Single().Values["title"].ShouldBe("Still Parsed");

            var mention = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/invalid-yaml/> schema:mentions <https://kb.example/id/rdf> .
}
""");
            mention.ShouldBeTrue();

            await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
                await result.Graph.ExecuteSelectAsync("INSERT DATA { <a> <b> <c> }"));
            await Should.ThrowAsync<Exception>(async () =>
                await result.Graph.ExecuteSelectAsync("SELECT ?s WHERE { ?s ?p }"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Pipeline_deterministic_extraction_handles_front_matter_and_link_edge_cases()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument("content/edge.md", """
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
  - text hint ignored by deterministic map reader
  - label:
  - label: Tool
    sameAs:
      - https://example.com/tool
    type: schema:SoftwareApplication
---
Intro mentions [[   ]] and [   ](https://example.com/blank) and [Relative](./relative.md) and [Absolute](https://example.com/absolute).

article --mentions--> Tool
https://example.com/subject --creator--> https://example.com/object
Tool --rdf:type--> https://schema.org/SoftwareApplication
Tool --custom:predicate--> https://example.com/custom
"""),
        ]);

        var positive = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://kb.example/edge/> schema:mentions <https://kb.example/id/tool> .
  <https://kb.example/edge/> schema:author <https://kb.example/id/ada-lovelace> .
  <https://kb.example/id/tool> schema:sameAs <https://example.com/tool> ;
                               rdf:type <https://schema.org/SoftwareApplication> .
  <https://example.com/subject> schema:creator <https://example.com/object> .
  <https://kb.example/id/absolute> schema:sameAs <https://example.com/absolute> .
}
""");
        positive.ShouldBeTrue();

        var negative = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/id/relative> schema:sameAs <https://kb.example/relative.md> .
}
""");
        negative.ShouldBeFalse();

        var custom = await result.Graph.ExecuteSelectAsync("""
PREFIX custom: <custom:>
SELECT ?object WHERE {
  <https://kb.example/id/tool> <custom:predicate> ?object .
}
""");
        custom.Rows.Single().Values["object"].ShouldBe("https://example.com/custom");
    }

    [Test]
    public async Task Pipeline_merges_deterministic_and_chat_duplicates_into_queryable_rdf()
    {
        var payload = """
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

        var chatClient = new TestChatClient((_, _) => payload);
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"), chatClient);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument("content/merge.md", """
---
title: Merge Flow
tags:
  - rdf
---
Merge Flow --mentions--> RDF
"""),
        ]);

        var merged = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
ASK WHERE {
  <https://kb.example/merge/> schema:mentions <https://kb.example/id/rdf> ;
                                schema:sameAs <https://example.com/rdf> .
  <https://kb.example/id/rdf> rdf:type <https://schema.org/SoftwareApplication> ;
                              schema:sameAs <https://www.w3.org/RDF/> .
}
""");
        merged.ShouldBeTrue();

        var rows = await result.Graph.SearchAsync("rdf");
        rows.Rows.Count.ShouldBeGreaterThan(0);
        chatClient.LastOptions.ShouldNotBeNull();
        chatClient.LastOptions!.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
    }

    [Test]
    public async Task Converter_content_flow_builds_graph_with_media_override_and_generated_document_path()
    {
        var converter = new KnowledgeSourceDocumentConverter();
        var document = converter.ConvertContent(
            """
# Converted Content

Converted Content --https://example.com/predicate/related--> https://example.com/object
Converted Content --unknown predicate--> https://example.com/unknown
Converted Content --about--> https://example.com/about
Converted Content --author--> https://example.com/author
Converted Content --description--> https://example.com/description
Converted Content --keywords--> https://example.com/keywords
Converted Content --   --> https://example.com/ignored
""",
            options: new KnowledgeDocumentConversionOptions
            {
                MediaType = "application/custom-markdown",
            });

        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
        var result = await pipeline.BuildAsync([document]);

        var related = await result.Graph.ExecuteAskAsync("""
ASK WHERE {
  <https://kb.example/document/> <https://example.com/predicate/related> <https://example.com/object> .
}
""");
        related.ShouldBeTrue();

        var fallback = await result.Graph.ExecuteAskAsync("""
PREFIX kb: <https://example.com/vocab/kb#>
ASK WHERE {
  <https://kb.example/document/> kb:relatedTo <https://example.com/unknown> .
}
""");
        fallback.ShouldBeTrue();

        var normalizedPredicates = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/document/> schema:about <https://example.com/about> ;
                                 schema:author <https://example.com/author> ;
                                 schema:description <https://example.com/description> ;
                                 schema:keywords <https://example.com/keywords> .
}
""");
        normalizedPredicates.ShouldBeTrue();
    }

    [Test]
    public async Task Directory_flow_handles_parser_edge_cases_and_unsupported_file_policy()
    {
        var root = Path.Combine(Path.GetTempPath(), string.Concat("markdown-ld-kb-flow-", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "plain.md"), """
plain --mentions--> RDF
""");
            await File.WriteAllTextAsync(Path.Combine(root, "marker-only.md"), """
---
""");
            await File.WriteAllTextAsync(Path.Combine(root, "unclosed.md"), """
---
title: Not Closed

# Unclosed Heading

Unclosed Heading --mentions--> SPARQL
""");
            await File.WriteAllTextAsync(Path.Combine(root, "list-yaml.md"), """
---
- list
- frontmatter
---
# List YAML

List YAML --mentions--> Graph
""");
            await File.WriteAllBytesAsync(Path.Combine(root, "broken.bin"), [9, 8, 7]);

            var pipeline = new MarkdownKnowledgePipeline(new Uri("https://kb.example/"));
            var result = await pipeline.BuildFromDirectoryAsync(root);

            var positive = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/plain/> schema:mentions <https://kb.example/id/rdf> .
  <https://kb.example/unclosed/> schema:mentions <https://kb.example/id/sparql> .
  <https://kb.example/list-yaml/> schema:mentions <https://kb.example/id/graph> .
}
""");
            positive.ShouldBeTrue();

            var titleRows = await result.Graph.ExecuteSelectAsync("""
PREFIX schema: <https://schema.org/>
SELECT ?title WHERE {
  <https://kb.example/marker-only/> schema:name ?title .
}
""");
            titleRows.Rows.Single().Values["title"].ShouldBe("marker only");

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
}
