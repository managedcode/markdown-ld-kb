using System.Text.Json;
using Microsoft.Extensions.AI;
using ManagedCode.MarkdownLd.Kb.Tests.Support;

namespace ManagedCode.MarkdownLd.Kb.Tests.Ai;

public sealed class ChatClientKnowledgeFactExtractorTests
{
    [Fact]
    public async Task ExtractAsync_UsesStructuredOutputAndNormalizesFacts()
    {
        var documentId = "https://example.com/articles/markdown-ld/";
        var chunkId = "chunk-01";
        var request = new KnowledgeFactExtractionRequest(
            documentId,
            chunkId,
            """
            Markdown-LD Knowledge Bank connects Markdown and RDF.
            It mentions SPARQL and JSON-LD.
            """,
            Title: "Markdown-LD Knowledge Bank",
            SectionPath: "Introduction",
            FrontMatter: new Dictionary<string, string?>
            {
                ["title"] = "Markdown-LD Knowledge Bank",
                ["tags"] = "knowledge-graph, rdf",
            });

        var payload = """
        {
          "entities": [
            {
              "id": "",
              "type": "schema:Thing",
              "label": "Markdown-LD Knowledge Bank",
              "sameAs": []
            },
            {
              "id": "https://example.com/id/markdown-ld-knowledge-bank",
              "type": "schema:Article",
              "label": "Markdown-LD Knowledge Bank",
              "sameAs": ["https://kb.example.com/article"]
            },
            {
              "id": "https://example.com/id/sparql",
              "type": "schema:Thing",
              "label": "SPARQL"
            }
          ],
          "assertions": [
            {
              "s": "<ARTICLE_ID>",
              "p": "schema:mentions",
              "o": "https://example.com/id/markdown-ld-knowledge-bank",
              "confidence": 0.72,
              "source": ""
            },
            {
              "s": "<ARTICLE_ID>",
              "p": "schema:mentions",
              "o": "https://example.com/id/markdown-ld-knowledge-bank",
              "confidence": 0.91,
              "source": "urn:kb:chunk:https://example.com/articles/markdown-ld/:chunk-01"
            },
            {
              "s": "<ARTICLE_ID>",
              "p": "schema:mentions",
              "o": "https://example.com/id/sparql",
              "confidence": 1.2,
              "source": "urn:kb:chunk:https://example.com/articles/markdown-ld/:chunk-01"
            }
          ]
        }
        """;

        var chatClient = new TestChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, payload)));
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(request);

        Assert.Equal(documentId, result.DocumentId);
        Assert.Equal(chunkId, result.ChunkId);
        Assert.Equal(2, result.Entities.Count);
        Assert.Equal(2, result.Assertions.Count);
        var articleEntity = Assert.Single(result.Entities, entity =>
            entity.Id == "https://example.com/id/markdown-ld-knowledge-bank");
        Assert.Equal("schema:Article", articleEntity.Type);
        Assert.NotNull(articleEntity.SameAs);
        Assert.Contains("https://kb.example.com/article", articleEntity.SameAs);

        var sparqlEntity = Assert.Single(result.Entities, entity =>
            entity.Id == "https://example.com/id/sparql");
        Assert.Equal("SPARQL", sparqlEntity.Label);
        Assert.Contains(result.Assertions, assertion =>
            assertion.SubjectId == documentId &&
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == "https://example.com/id/markdown-ld-knowledge-bank" &&
            assertion.Confidence == 0.91 &&
            assertion.Source == request.ChunkSourceUri);
        Assert.Contains(result.Assertions, assertion =>
            assertion.SubjectId == documentId &&
            assertion.Predicate == "schema:mentions" &&
            assertion.ObjectId == "https://example.com/id/sparql" &&
            assertion.Confidence == 1);
        Assert.Equal(1, chatClient.CallCount);
        Assert.NotNull(chatClient.LastOptions);
        Assert.IsType<ChatResponseFormatJson>(chatClient.LastOptions!.ResponseFormat);
        Assert.Equal(ChatRole.System, chatClient.LastMessages[0].Role);
        Assert.Equal(ChatRole.User, chatClient.LastMessages[1].Role);
        Assert.Contains("schema:mentions", chatClient.LastMessages[0].Text);
        Assert.Contains(documentId, chatClient.LastMessages[1].Text);
        Assert.Contains(request.ChunkSourceUri, chatClient.LastMessages[1].Text);
        Assert.Contains("Markdown-LD Knowledge Bank connects Markdown and RDF.", chatClient.LastMessages[1].Text);
    }

    [Fact]
    public async Task ExtractAsync_ReturnsEmptyResultForInvalidStructuredOutput()
    {
        var chatClient = new TestChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "not json")));
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                "https://example.com/articles/empty/",
                "chunk-02",
                "Markdown only."));

        Assert.Empty(result.Entities);
        Assert.Empty(result.Assertions);
        Assert.Equal("not json", result.RawResponse);
        Assert.Equal(1, chatClient.CallCount);
    }

    [Fact]
    public async Task ExtractAsync_SkipsBlankMarkdownWithoutCallingTheClient()
    {
        var chatClient = new TestChatClient(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "{}")));
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                "https://example.com/articles/blank/",
                "chunk-03",
                "   "));

        Assert.Empty(result.Entities);
        Assert.Empty(result.Assertions);
        Assert.Equal(string.Empty, result.RawResponse);
        Assert.Equal(0, chatClient.CallCount);
    }
}
