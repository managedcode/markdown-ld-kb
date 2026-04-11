using Microsoft.Extensions.AI;
using Shouldly;
using TUnit.Core;
using ManagedCode.MarkdownLd.Kb.Tests.Support;

namespace ManagedCode.MarkdownLd.Kb.Tests.Ai;

public sealed class ChatClientKnowledgeFactExtractorTests
{
    [Test]
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

        var chatClient = new TestChatClient((_, _) => payload);
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(request);

        result.DocumentId.ShouldBe(documentId);
        result.ChunkId.ShouldBe(chunkId);
        result.Entities.Count.ShouldBe(2);
        result.Assertions.Count.ShouldBe(2);

        var articleEntity = result.Entities.Single(entity =>
            entity.Id == "https://example.com/id/markdown-ld-knowledge-bank");
        articleEntity.Type.ShouldBe("schema:Article");
        articleEntity.SameAs.ShouldNotBeNull();
        articleEntity.SameAs.ShouldContain("https://kb.example.com/article");

        var sparqlEntity = result.Entities.Single(entity =>
            entity.Id == "https://example.com/id/sparql");
        sparqlEntity.Label.ShouldBe("SPARQL");

        var mentionedArticle = result.Assertions.Single(assertion =>
            assertion.ObjectId == "https://example.com/id/markdown-ld-knowledge-bank");
        mentionedArticle.SubjectId.ShouldBe(documentId);
        mentionedArticle.Predicate.ShouldBe("schema:mentions");
        mentionedArticle.Confidence.ShouldBe(0.91);
        mentionedArticle.Source.ShouldBe(request.ChunkSourceUri);

        var mentionedSparql = result.Assertions.Single(assertion =>
            assertion.ObjectId == "https://example.com/id/sparql");
        mentionedSparql.SubjectId.ShouldBe(documentId);
        mentionedSparql.Predicate.ShouldBe("schema:mentions");
        mentionedSparql.Confidence.ShouldBe(1);
        mentionedSparql.Source.ShouldBe(request.ChunkSourceUri);

        chatClient.CallCount.ShouldBe(1);
        chatClient.LastOptions.ShouldNotBeNull();
        chatClient.LastOptions!.Temperature.ShouldBe(0);
        chatClient.LastOptions.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
        chatClient.LastMessages.Count.ShouldBe(2);
        chatClient.LastMessages[0].Role.ShouldBe(ChatRole.System);
        chatClient.LastMessages[1].Role.ShouldBe(ChatRole.User);
        chatClient.LastMessages[0].Text.ShouldContain("schema:mentions");
        chatClient.LastMessages[1].Text.ShouldContain(documentId);
        chatClient.LastMessages[1].Text.ShouldContain(request.ChunkSourceUri);
        chatClient.LastMessages[1].Text.ShouldContain("Markdown-LD Knowledge Bank connects Markdown and RDF.");
        chatClient.LastMessages[1].Text.ShouldContain("FRONT_MATTER:");
        chatClient.LastMessages[1].Text.ShouldContain("- tags: knowledge-graph, rdf");
    }

    [Test]
    public async Task ExtractAsync_ReturnsEmptyResultForInvalidStructuredOutput()
    {
        var chatClient = new TestChatClient((_, _) => "not json");
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                "https://example.com/articles/empty/",
                "chunk-02",
                "Markdown only."));

        result.Entities.ShouldBeEmpty();
        result.Assertions.ShouldBeEmpty();
        result.RawResponse.ShouldBe("not json");
        chatClient.CallCount.ShouldBe(1);
    }

    [Test]
    public async Task ExtractAsync_SkipsBlankMarkdownWithoutCallingTheClient()
    {
        var chatClient = new TestChatClient((_, _) => "{}");
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                "https://example.com/articles/blank/",
                "chunk-03",
                "   "));

        result.Entities.ShouldBeEmpty();
        result.Assertions.ShouldBeEmpty();
        result.RawResponse.ShouldBeEmpty();
        chatClient.CallCount.ShouldBe(0);
    }

    [Test]
    public async Task ExtractAsync_RejectsMissingDocumentIdentity()
    {
        var extractor = new ChatClientKnowledgeFactExtractor(
            new TestChatClient((_, _) => "{}"));

        await Should.ThrowAsync<ArgumentException>(() => extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                "",
                "chunk-04",
                "Markdown")));
    }
}
