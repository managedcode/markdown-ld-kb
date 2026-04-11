using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeFlowEdgeCaseTests
{
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
}
