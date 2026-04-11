using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb;

public sealed class MarkdownKnowledgePipelineTests
{
    private const string BaseUrlText = "https://example.com/";
    private static readonly Uri BaseUri = new(BaseUrlText);
    private const string ArticleUri = "https://example.com/2026/04/markdown-ld-knowledge-bank/";
    private const string EntityRdfUri = "https://example.com/id/rdf";
    private const string EntityRdfSameAs = "https://www.w3.org/RDF/";
    private const string SchemaMentions = "schema:mentions";
    private const string SchemaArticle = "schema:Article";
    private const string StructuredOutputSchema = "structured output schema";
    private const string ContentPathKnowledgeGraph = "content/2026/04/markdown-ld-knowledge-bank.md";
    private const string ContentPathDuplicateFacts = "content/2026/04/duplicate-facts.md";
    private const string ContentPathEmpty = "content/empty.md";
    private const string FixtureKnowledgeGraph = "knowledge-graph.md";
    private const string FixtureDuplicateFacts = "duplicate-facts.md";
    private const string SearchTermRdf = "rdf";
    private const string SearchSubjectKey = "subject";
    private const string SearchMentionKey = "mention";
    private const string GraphTitle = "Markdown-LD Knowledge Bank";
    private const string GraphUri = "https://example.com/2026/04/markdown-ld-knowledge-bank/";
    private const string DuplicateArticleUri = "https://example.com/2026/04/duplicate-facts/";
    private const string EmptyText = "";
    private const string RdfLabel = "RDF";
    private const string SourcePrefix = "urn:kb:chunk:";
    private const string ChunkSuffix = ":markdown-ld-knowledge-bank";
    private const string TitleBindingKey = "title";
    private const string SubjectBindingKey = "subject";
    private const string MentionBindingKey = "mention";
    private const string SummaryText = "Markdown summary.";
    private const string GraphKeyword = "graph";
    private const string PublishedDate = "2026-04-11";
    private const string SearchArticlesTerm = "sparql";
    private const string SearchEntitiesTerm = "tool";
    private const string SearchEntitySameAsUri = "https://example.com/tool";
    private const string SelectTitleQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?title WHERE {
  <https://example.com/2026/04/markdown-ld-knowledge-bank/> schema:name ?title
}
""";
    private const string SelectMentionsQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?mention WHERE {
  <https://example.com/2026/04/markdown-ld-knowledge-bank/> schema:mentions ?mention
}
ORDER BY ?mention
""";
    private const string SelectDuplicateMentionsQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?mention WHERE {
  <https://example.com/2026/04/duplicate-facts/> schema:mentions ?mention
}
""";
    private const string MutatingInsertDataQuery = "INSERT DATA { <a> <b> <c> }";
    private static readonly string[] MarkdownLiterals =
    {
        GraphTitle,
        SchemaArticle,
        StructuredOutputSchema,
        RdfLabel,
        EntityRdfSameAs,
    };
    private static readonly Uri ArticleGraphUri = new(GraphUri);

    private static readonly string ChatResponse = $$"""
    {
      "entities": [
        {
          "id": "{{EntityRdfUri}}",
          "type": "schema:Thing",
          "label": "{{RdfLabel}}",
          "sameAs": [
            "{{EntityRdfSameAs}}"
          ]
        }
      ],
      "assertions": [
        {
          "s": "{{ArticleUri}}",
          "p": "{{SchemaMentions}}",
          "o": "{{EntityRdfUri}}",
          "confidence": 0.99,
          "source": "{{SourcePrefix}}{{ArticleUri}}{{ChunkSuffix}}"
        }
      ]
    }
    """;

    [Test]
    public async Task BuildAsync_merges_markdown_and_chat_facts_into_a_searchable_graph()
    {
        var chatClient = new TestChatClient((_, _) => ChatResponse);
        var pipeline = new MarkdownKnowledgePipeline(BaseUri, chatClient);
        var source = new MarkdownSourceDocument(
            ContentPathKnowledgeGraph,
            FixtureLoader.Read(FixtureKnowledgeGraph));

        var result = await pipeline.BuildAsync([source]);

        result.Documents.Count.ShouldBe(1);
        result.Documents[0].Title.ShouldBe(GraphTitle);
        result.Facts.Entities.Count(entity => entity.Label == RdfLabel).ShouldBe(1);
        result.Facts.Assertions.Count(assertion =>
            assertion.SubjectId == ArticleUri &&
            assertion.Predicate == SchemaMentions &&
            assertion.ObjectId == EntityRdfUri).ShouldBe(1);
        result.Facts.Assertions.Single(assertion =>
            assertion.SubjectId == ArticleUri &&
            assertion.Predicate == SchemaMentions &&
            assertion.ObjectId == EntityRdfUri).Confidence.ShouldBe(0.99);

        var searchResults = await result.Graph.SearchAsync(SearchTermRdf);
        searchResults.Rows.Count.ShouldBeGreaterThan(0);
        searchResults.Rows.Any(row => row.Values.TryGetValue(SubjectBindingKey, out var value) && value == EntityRdfUri).ShouldBeTrue();

        var titleResult = await result.Graph.ExecuteSelectAsync(SelectTitleQuery);
        titleResult.Rows.Count.ShouldBe(1);
        titleResult.Rows[0].Values[TitleBindingKey].ShouldBe(GraphTitle);

        var mentionsResult = await result.Graph.ExecuteSelectAsync(SelectMentionsQuery);
        mentionsResult.Rows.Count.ShouldBeGreaterThan(0);
        mentionsResult.Rows.Any(row => row.Values.TryGetValue(MentionBindingKey, out var value) && value == EntityRdfUri).ShouldBeTrue();

        var turtle = result.Graph.SerializeTurtle();
        turtle.ShouldContain(MarkdownLiterals[0]);
        turtle.ShouldContain(MarkdownLiterals[1]);

        var jsonLd = result.Graph.SerializeJsonLd();
        jsonLd.ShouldContain(MarkdownLiterals[0]);
        jsonLd.ShouldContain(MarkdownLiterals[3]);

        chatClient.CallCount.ShouldBe(1);
        chatClient.LastMessages.Count.ShouldBe(2);
        chatClient.LastMessages[0].Role.ShouldBe(ChatRole.System);
        chatClient.LastMessages[1].Role.ShouldBe(ChatRole.User);
        chatClient.LastOptions.ShouldNotBeNull();
        chatClient.LastOptions!.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
        chatClient.LastMessages[0].Text.ShouldContain(StructuredOutputSchema);
        chatClient.LastMessages[1].Text.ShouldContain(ArticleUri);
    }

    [Test]
    public async Task BuildAsync_handles_duplicate_facts_and_ignores_malformed_assertions()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var source = new MarkdownSourceDocument(
            ContentPathDuplicateFacts,
            FixtureLoader.Read(FixtureDuplicateFacts));

        var result = await pipeline.BuildAsync([source]);

        result.Facts.Entities.Count(entity => entity.Label == RdfLabel).ShouldBe(1);
        result.Facts.Assertions.Count(assertion =>
            assertion.SubjectId == DuplicateArticleUri &&
            assertion.Predicate == SchemaMentions &&
            assertion.ObjectId == EntityRdfUri).ShouldBe(1);
        result.Facts.Assertions.Count(assertion =>
            assertion.SubjectId == EntityRdfUri &&
            assertion.Predicate == SchemaMentions &&
            assertion.ObjectId == EntityRdfUri).ShouldBe(1);

        var graphResults = await result.Graph.ExecuteSelectAsync(SelectDuplicateMentionsQuery);
        graphResults.Rows.Count.ShouldBe(1);
        graphResults.Rows[0].Values[MentionBindingKey].ShouldBe(EntityRdfUri);
    }

    [Test]
    public async Task BuildAsync_returns_no_matches_for_empty_documents_and_rejects_mutating_sparql()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([new MarkdownSourceDocument(ContentPathEmpty, EmptyText)]);

        result.Documents.Count.ShouldBe(1);
        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();
        result.Graph.TripleCount.ShouldBe(0);

        var searchResults = await result.Graph.SearchAsync(SearchTermRdf);
        searchResults.Rows.ShouldBeEmpty();

        await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteAskAsync(MutatingInsertDataQuery));
    }
}
