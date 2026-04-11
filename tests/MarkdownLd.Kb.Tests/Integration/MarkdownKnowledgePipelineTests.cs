using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;
using TUnit.Core;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownKnowledgePipelineTests
{
    [Test]
    public async Task BuildAsync_merges_markdown_and_chat_facts_into_a_searchable_graph()
    {
        var articleUri = "https://example.com/2026/04/markdown-ld-knowledge-bank/";
        var chatResponse = $$"""
        {
          "entities": [
            {
              "id": "https://example.com/id/rdf",
              "type": "schema:Thing",
              "label": "RDF",
              "sameAs": [
                "https://www.w3.org/RDF/"
              ]
            }
          ],
          "assertions": [
            {
              "s": "{{articleUri}}",
              "p": "schema:mentions",
              "o": "https://example.com/id/rdf",
              "confidence": 0.99,
              "source": "urn:kb:chunk:{{articleUri}}:markdown-ld-knowledge-bank"
            }
          ]
        }
        """;

        var chatClient = new TestChatClient((_, _) => chatResponse);
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://example.com/"), chatClient);
        var source = new MarkdownSourceDocument(
            "content/2026/04/markdown-ld-knowledge-bank.md",
            FixtureLoader.Read("knowledge-graph.md"));

        var result = await pipeline.BuildAsync([source]);

        result.Documents.Count.ShouldBe(1);
        result.Documents[0].Title.ShouldBe("Markdown-LD Knowledge Bank");
        result.Facts.Entities.Count(entity => entity.Label == "RDF").ShouldBe(1);
        result.Facts.Assertions.Count(assertion =>
            assertion.SubjectId == articleUri &&
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == "https://example.com/id/rdf").ShouldBe(1);
        result.Facts.Assertions.Single(assertion =>
            assertion.SubjectId == articleUri &&
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == "https://example.com/id/rdf").Confidence.ShouldBe(0.99);

        var searchResults = await result.Graph.SearchAsync("rdf");
        searchResults.Rows.Count.ShouldBeGreaterThan(0);
        searchResults.Rows.Any(row => row.Values.TryGetValue("subject", out var value) && value == "https://example.com/id/rdf").ShouldBeTrue();

        var titleResult = await result.Graph.ExecuteSelectAsync("""
PREFIX schema: <https://schema.org/>
SELECT ?title WHERE {
  <https://example.com/2026/04/markdown-ld-knowledge-bank/> schema:name ?title
}
""");
        titleResult.Rows.Count.ShouldBe(1);
        titleResult.Rows[0].Values["title"].ShouldBe("Markdown-LD Knowledge Bank");

        var mentionsResult = await result.Graph.ExecuteSelectAsync("""
PREFIX schema: <https://schema.org/>
SELECT ?mention WHERE {
  <https://example.com/2026/04/markdown-ld-knowledge-bank/> schema:mentions ?mention
}
ORDER BY ?mention
""");
        mentionsResult.Rows.Count.ShouldBeGreaterThan(0);
        mentionsResult.Rows.Any(row => row.Values.TryGetValue("mention", out var value) && value == "https://example.com/id/rdf").ShouldBeTrue();

        var turtle = result.Graph.SerializeTurtle();
        turtle.ShouldContain("Markdown-LD Knowledge Bank");
        turtle.ShouldContain("schema:Article");

        var jsonLd = result.Graph.SerializeJsonLd();
        jsonLd.ShouldContain("Markdown-LD Knowledge Bank");
        jsonLd.ShouldContain("RDF");

        chatClient.CallCount.ShouldBe(1);
        chatClient.LastMessages.Count.ShouldBe(2);
        chatClient.LastMessages[0].Role.ShouldBe(ChatRole.System);
        chatClient.LastMessages[1].Role.ShouldBe(ChatRole.User);
        chatClient.LastOptions.ShouldNotBeNull();
        chatClient.LastOptions!.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
        chatClient.LastMessages[0].Text.ShouldContain("structured output schema");
        chatClient.LastMessages[1].Text.ShouldContain(articleUri);
    }

    [Test]
    public async Task BuildAsync_handles_duplicate_facts_and_ignores_malformed_assertions()
    {
        var articleUri = "https://example.com/2026/04/duplicate-facts/";
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://example.com/"));
        var source = new MarkdownSourceDocument(
            "content/2026/04/duplicate-facts.md",
            FixtureLoader.Read("duplicate-facts.md"));

        var result = await pipeline.BuildAsync([source]);

        result.Facts.Entities.Count(entity => entity.Label == "RDF").ShouldBe(1);
        result.Facts.Assertions.Count(assertion =>
            assertion.SubjectId == articleUri &&
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == "https://example.com/id/rdf").ShouldBe(1);
        result.Facts.Assertions.Count(assertion =>
            assertion.SubjectId == "https://example.com/id/rdf" &&
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == "https://example.com/id/rdf").ShouldBe(1);

        var graphResults = await result.Graph.ExecuteSelectAsync("""
PREFIX schema: <https://schema.org/>
SELECT ?mention WHERE {
  <https://example.com/2026/04/duplicate-facts/> schema:mentions ?mention
}
""");
        graphResults.Rows.Count.ShouldBe(1);
        graphResults.Rows[0].Values["mention"].ShouldBe("https://example.com/id/rdf");
    }

    [Test]
    public async Task BuildAsync_returns_no_matches_for_empty_documents_and_rejects_mutating_sparql()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://example.com/"));
        var result = await pipeline.BuildAsync([new MarkdownSourceDocument("content/empty.md", string.Empty)]);

        result.Documents.Count.ShouldBe(1);
        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();
        result.Graph.TripleCount.ShouldBe(0);

        var searchResults = await result.Graph.SearchAsync("rdf");
        searchResults.Rows.ShouldBeEmpty();

        await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteAskAsync("INSERT DATA { <a> <b> <c> }"));
    }
}
