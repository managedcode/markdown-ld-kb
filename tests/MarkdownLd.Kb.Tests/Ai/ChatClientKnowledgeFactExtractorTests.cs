using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Ai;

public sealed class ChatClientKnowledgeFactExtractorTests
{
    private const string DocumentId = "https://example.com/articles/markdown-ld/";
    private const string ChunkId = "chunk-01";
    private const string MarkdownText = """
        Markdown-LD Knowledge Bank connects Markdown and RDF.
        It mentions SPARQL and JSON-LD.
        """;
    private const string Title = "Markdown-LD Knowledge Bank";
    private const string SectionPath = "Introduction";
    private const string FrontMatterTitleKey = "title";
    private const string FrontMatterTagsKey = "tags";
    private const string FrontMatterTitleValue = "Markdown-LD Knowledge Bank";
    private const string FrontMatterTagsValue = "knowledge-graph, rdf";
    private const string ArticleEntityId = "https://example.com/id/markdown-ld-knowledge-bank";
    private const string ArticleEntityType = "schema:Article";
    private const string ArticleEntitySameAs = "https://kb.example.com/article";
    private const string SparqlEntityId = "https://example.com/id/sparql";
    private const string SparqlEntityLabel = "SPARQL";
    private const string MentionsPredicate = "schema:mentions";
    private const string SourceUri = "urn:kb:chunk:https://example.com/articles/markdown-ld/:chunk-01";
    private const string FrontMatterHeader = "FRONT_MATTER:";
    private const string FrontMatterTagsLine = "- tags: knowledge-graph, rdf";
    private const string StructuredOutputInvalidJson = "not json";
    private const string BlankMarkdown = "   ";
    private const string EmptyJson = "{}";
    private const string EmptyResultDocumentId = "https://example.com/articles/empty/";
    private const string EmptyResultChunkId = "chunk-02";
    private const string EmptyResultMarkdown = "Markdown only.";
    private const string BlankResultDocumentId = "https://example.com/articles/blank/";
    private const string BlankResultChunkId = "chunk-03";
    private const string MissingDocumentId = "";
    private const string MissingDocumentChunkId = "chunk-04";
    private const string MissingDocumentMarkdown = "Markdown";
    private const string StructuredOutputPrefix = "https://example.com/id/";
    private const string LiteralSchemaMentions = "schema:mentions";
    private const string MarkdownMentionsText = "Markdown-LD Knowledge Bank connects Markdown and RDF.";
    private const string SystemPromptToken = "schema:mentions";

    private static readonly IReadOnlyDictionary<string, string?> FrontMatter = new Dictionary<string, string?>
    {
        [FrontMatterTitleKey] = FrontMatterTitleValue,
        [FrontMatterTagsKey] = FrontMatterTagsValue,
    };

    private const string Payload = """
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

    [Test]
    public async Task ExtractAsync_UsesStructuredOutputAndNormalizesFacts()
    {
        var request = new KnowledgeFactExtractionRequest(
            DocumentId,
            ChunkId,
            MarkdownText,
            Title: Title,
            SectionPath: SectionPath,
            FrontMatter: new Dictionary<string, string?>(FrontMatter));

        var chatClient = new TestChatClient((_, _) => Payload);
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(request);

        result.DocumentId.ShouldBe(DocumentId);
        result.ChunkId.ShouldBe(ChunkId);
        result.Entities.Count.ShouldBe(2);
        result.Assertions.Count.ShouldBe(2);

        var articleEntity = result.Entities.Single(entity =>
            entity.Id == ArticleEntityId);
        articleEntity.Type.ShouldBe(ArticleEntityType);
        articleEntity.SameAs.ShouldNotBeNull();
        articleEntity.SameAs.ShouldContain(ArticleEntitySameAs);

        var sparqlEntity = result.Entities.Single(entity =>
            entity.Id == SparqlEntityId);
        sparqlEntity.Label.ShouldBe(SparqlEntityLabel);

        var mentionedArticle = result.Assertions.Single(assertion =>
            assertion.ObjectId == ArticleEntityId);
        mentionedArticle.SubjectId.ShouldBe(DocumentId);
        mentionedArticle.Predicate.ShouldBe(MentionsPredicate);
        mentionedArticle.Confidence.ShouldBe(0.91);
        mentionedArticle.Source.ShouldBe(request.ChunkSourceUri);

        var mentionedSparql = result.Assertions.Single(assertion =>
            assertion.ObjectId == SparqlEntityId);
        mentionedSparql.SubjectId.ShouldBe(DocumentId);
        mentionedSparql.Predicate.ShouldBe(MentionsPredicate);
        mentionedSparql.Confidence.ShouldBe(1);
        mentionedSparql.Source.ShouldBe(request.ChunkSourceUri);

        chatClient.CallCount.ShouldBe(1);
        chatClient.LastOptions.ShouldNotBeNull();
        chatClient.LastOptions!.Temperature.ShouldBe(0);
        chatClient.LastOptions.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
        chatClient.LastMessages.Count.ShouldBe(2);
        chatClient.LastMessages[0].Role.ShouldBe(ChatRole.System);
        chatClient.LastMessages[1].Role.ShouldBe(ChatRole.User);
        chatClient.LastMessages[0].Text.ShouldContain(SystemPromptToken);
        chatClient.LastMessages[1].Text.ShouldContain(DocumentId);
        chatClient.LastMessages[1].Text.ShouldContain(request.ChunkSourceUri);
        chatClient.LastMessages[1].Text.ShouldContain(MarkdownMentionsText);
        chatClient.LastMessages[1].Text.ShouldContain(FrontMatterHeader);
        chatClient.LastMessages[1].Text.ShouldContain(FrontMatterTagsLine);
    }

    [Test]
    public async Task ExtractAsync_ReturnsEmptyResultForInvalidStructuredOutput()
    {
        var chatClient = new TestChatClient((_, _) => StructuredOutputInvalidJson);
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                EmptyResultDocumentId,
                EmptyResultChunkId,
                EmptyResultMarkdown));

        result.Entities.ShouldBeEmpty();
        result.Assertions.ShouldBeEmpty();
        result.RawResponse.ShouldBe(StructuredOutputInvalidJson);
        chatClient.CallCount.ShouldBe(1);
    }

    [Test]
    public async Task ExtractAsync_SkipsBlankMarkdownWithoutCallingTheClient()
    {
        var chatClient = new TestChatClient((_, _) => EmptyJson);
        var extractor = new ChatClientKnowledgeFactExtractor(chatClient);

        var result = await extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                BlankResultDocumentId,
                BlankResultChunkId,
                BlankMarkdown));

        result.Entities.ShouldBeEmpty();
        result.Assertions.ShouldBeEmpty();
        result.RawResponse.ShouldBeEmpty();
        chatClient.CallCount.ShouldBe(0);
    }

    [Test]
    public async Task ExtractAsync_RejectsMissingDocumentIdentity()
    {
        var extractor = new ChatClientKnowledgeFactExtractor(
            new TestChatClient((_, _) => EmptyJson));

        await Should.ThrowAsync<ArgumentException>(() => extractor.ExtractAsync(
            new KnowledgeFactExtractionRequest(
                MissingDocumentId,
                MissingDocumentChunkId,
                MissingDocumentMarkdown)));
    }
}
