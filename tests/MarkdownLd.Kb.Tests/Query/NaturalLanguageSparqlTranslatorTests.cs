using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class NaturalLanguageSparqlTranslatorTests
{
    private const string BaseUriText = "https://nl.example/";
    private const string DocumentPath = "content/nl-query.md";
    private const string DocumentTitle = "Natural Language Query";
    private const string TitleVariable = "title";
    private const string Question = "What is the article title?";
    private const string MutatingQuestion = "Delete everything.";
    private const string SelectQuery = """
    ```sparql
    PREFIX schema: <https://schema.org/>
    SELECT ?title WHERE {
      <https://nl.example/nl-query/> schema:name ?title .
    }
    ```
    """;
    private const string SelectQueryWithLeadingText = """
    Here is the safest query:

    ```sparql
    PREFIX schema: <https://schema.org/>
    SELECT ?title WHERE {
      <https://nl.example/nl-query/> schema:name ?title .
    }
    ```
    """;
    private const string MutatingQuery = "DELETE WHERE { ?s ?p ?o }";

    private const string Markdown = """
---
title: Natural Language Query
---
# Natural Language Query

Plain content.
""";

    [Test]
    public async Task Translator_executes_read_only_select_query_over_built_graph()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var graph = (await pipeline.BuildAsync([
            new MarkdownSourceDocument(DocumentPath, Markdown),
        ])).Graph;
        var translator = new ChatClientNaturalLanguageSparqlTranslator(new TestChatClient((_, _) => SelectQuery));

        var result = await translator.ExecuteAsync(graph, Question);

        result.Translation.QueryKind.ShouldBe(NaturalLanguageSparqlQueryKind.Select);
        result.SelectResult.ShouldNotBeNull();
        result.SelectResult.Rows.Single().Values[TitleVariable].ShouldBe(DocumentTitle);
        result.AskResult.ShouldBeNull();
    }

    [Test]
    public async Task Translator_rejects_mutating_query_output()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var graph = (await pipeline.BuildAsync([
            new MarkdownSourceDocument(DocumentPath, Markdown),
        ])).Graph;
        var translator = new ChatClientNaturalLanguageSparqlTranslator(new TestChatClient((_, _) => MutatingQuery));

        await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await translator.TranslateAsync(graph, MutatingQuestion));
    }

    [Test]
    public async Task Translator_extracts_fenced_query_when_model_prepends_explanatory_text()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));
        var graph = (await pipeline.BuildAsync([
            new MarkdownSourceDocument(DocumentPath, Markdown),
        ])).Graph;
        var translator = new ChatClientNaturalLanguageSparqlTranslator(new TestChatClient((_, _) => SelectQueryWithLeadingText));

        var result = await translator.ExecuteAsync(graph, Question);

        result.Translation.QueryKind.ShouldBe(NaturalLanguageSparqlQueryKind.Select);
        result.SelectResult.ShouldNotBeNull();
        result.SelectResult.Rows.Single().Values[TitleVariable].ShouldBe(DocumentTitle);
    }
}
