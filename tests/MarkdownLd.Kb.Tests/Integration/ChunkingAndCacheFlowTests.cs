using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class ChunkingAndCacheFlowTests
{
    private const string BaseUriText = "https://cache.example/";
    private const string DocumentPath = "content/cache-flow.md";
    private const string DocumentUri = "https://cache.example/cache-flow/";
    private const string SourcePlaceholder = "__SOURCE__";
    private const string ChunkSourceLabel = "CHUNK_SOURCE: ";
    private const string RdfEntityId = "https://cache.example/id/rdf";
    private const string SparqlEntityId = "https://cache.example/id/sparql";
    private const string SearchTerm = "sparql";
    private const string SearchSubjectKey = "subject";
    private const int ExpectedChunkCount = 2;
    private const int ExpectedChatCallsAfterWarmBuild = 2;
    private const int ExpectedChatCallsAfterCachedBuild = 2;

    private const string Markdown = """
---
title: Cache Flow
---
# RDF

RDF section content.

## SPARQL

SPARQL section content.
""";

    private const string FirstPayload = """
{
  "entities": [
    {
      "id": "https://cache.example/id/rdf",
      "type": "schema:Thing",
      "label": "RDF",
      "sameAs": []
    }
  ],
  "assertions": [
    {
      "s": "<ARTICLE_ID>",
      "p": "schema:mentions",
      "o": "https://cache.example/id/rdf",
      "confidence": 0.95,
      "source": "__SOURCE__"
    }
  ]
}
""";

    private const string SecondPayload = """
{
  "entities": [
    {
      "id": "https://cache.example/id/sparql",
      "type": "schema:Thing",
      "label": "SPARQL",
      "sameAs": []
    }
  ],
  "assertions": [
    {
      "s": "<ARTICLE_ID>",
      "p": "schema:mentions",
      "o": "https://cache.example/id/sparql",
      "confidence": 0.96,
      "source": "__SOURCE__"
    }
  ]
}
""";

    private const string AskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://cache.example/cache-flow/> a schema:Article ;
                                     schema:mentions <https://cache.example/id/rdf> ;
                                     schema:mentions <https://cache.example/id/sparql> .
}
""";

    [Test]
    public async Task Pipeline_reuses_file_cache_and_supports_swap_in_chunker_flow()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "markdown-ld-kb-cache-flow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cacheDirectory);

        try
        {
            var payloads = new Queue<string>([FirstPayload, SecondPayload]);
            var chatClient = new TestChatClient((messages, _) =>
                payloads.Dequeue().Replace(SourcePlaceholder, ExtractChunkSource(messages), StringComparison.Ordinal));
            var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
            {
                BaseUri = new Uri(BaseUriText),
                ChatClient = chatClient,
                ChatModelId = "test-cache-model",
                MarkdownChunker = new WholeSectionMarkdownChunker(),
                ExtractionCache = new FileKnowledgeExtractionCache(cacheDirectory),
            });
            var source = new MarkdownSourceDocument(DocumentPath, Markdown);

            var warmResult = await pipeline.BuildAsync([source]);
            var cachedResult = await pipeline.BuildAsync([source]);

            warmResult.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(DocumentUri);
            warmResult.Documents.Single().Chunks.Count.ShouldBe(ExpectedChunkCount);
            cachedResult.Documents.Single().Chunks.Count.ShouldBe(ExpectedChunkCount);
            chatClient.CallCount.ShouldBe(ExpectedChatCallsAfterCachedBuild);

            var warmGraphHasFacts = await warmResult.Graph.ExecuteAskAsync(AskQuery);
            warmGraphHasFacts.ShouldBeTrue();

            var cachedGraphHasFacts = await cachedResult.Graph.ExecuteAskAsync(AskQuery);
            cachedGraphHasFacts.ShouldBeTrue();

            var search = await cachedResult.Graph.SearchAsync(SearchTerm);
            search.Rows.Any(row =>
                row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
                subject == SparqlEntityId).ShouldBeTrue();

            payloads.Count.ShouldBe(0);
            chatClient.CallCount.ShouldBe(ExpectedChatCallsAfterWarmBuild);
        }
        finally
        {
            Directory.Delete(cacheDirectory, recursive: true);
        }
    }

    private static string ExtractChunkSource(IReadOnlyList<ChatMessage> messages)
    {
        var userPrompt = messages.Single(message => message.Role == ChatRole.User).Text;
        var sourceLine = userPrompt
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith(ChunkSourceLabel, StringComparison.Ordinal));

        return sourceLine[ChunkSourceLabel.Length..].Trim();
    }
}
