using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class MarkdownKnowledgePipelineTests
{
    private const string BaseUrlText = "https://example.com/";
    private static readonly Uri BaseUri = new(BaseUrlText);
    private const string ArticleUri = "https://example.com/2026/04/markdown-ld-knowledge-bank/";
    private const string EntityRdfUri = "https://example.com/id/rdf";
    private const string EntityRdfSameAs = "https://www.w3.org/RDF/";
    private const string SchemaMentions = "schema:mentions";
    private const string SchemaArticle = "schema:Article";
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
    private const string InlineMarkdownTitle = "Inline Markdown Knowledge";
    private const string InlineRdfLabel = "RDF";
    private const string InlineSearchTerm = "rdf";
    private const string ArticleBindingKey = "article";
    private const string EntityBindingKey = "entity";
    private const string NameBindingKey = "name";
    private const string RepositoryReadmeFileName = "README.md";
    private const string RepositoryReadmeTitle = "Markdown-LD Knowledge Bank";
    private const string RepositoryReadmeDocumentUri = "https://example.com/README/";
    private const string RepositoryReadmeTokenQuery = "RDF SPARQL Markdown graph";
    private const string RepositoryReadmeTitleBindingKey = "name";
    private const string RepositoryReadmeInferenceTypeBindingKey = "type";
    private const string RepositoryReadmeInferenceSchemaText = """
@prefix ex: <https://example.com/vocab/> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix schema: <https://schema.org/> .

ex:QueryableKnowledgeAsset a rdfs:Class ;
  rdfs:subClassOf schema:CreativeWork .

ex:RepositoryReadme a rdfs:Class ;
  rdfs:subClassOf ex:QueryableKnowledgeAsset .
""";
    private const string RepositoryReadmeInferenceRulesText = """
@prefix ex: <https://example.com/vocab/> .
@prefix schema: <https://schema.org/> .

{ ?doc a schema:Article ; schema:name "Markdown-LD Knowledge Bank" . } =>
{ ?doc a ex:RepositoryReadme ;
       a ex:QueryableKnowledgeAsset ;
       ex:hasIndexedTitle "Markdown-LD Knowledge Bank" } .
""";
    private const string InlineMarkdown = """
---
title: Inline Markdown Knowledge
---
# Inline Markdown Knowledge

This note mentions [RDF](https://www.w3.org/RDF/).
""";
    private const string SelectInlineMarkdownFactsQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?article ?entity WHERE {
  ?article a schema:Article ;
           schema:name "Inline Markdown Knowledge" ;
           schema:mentions ?entity .
  ?entity schema:name "RDF" ;
          schema:sameAs <https://www.w3.org/RDF/> .
}
""";
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
    private const string RepositoryReadmeAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://example.com/README/> a schema:Article ;
                               schema:name "Markdown-LD Knowledge Bank" .
}
""";
    private const string RepositoryReadmeSelectQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?name WHERE {
  <https://example.com/README/> a schema:Article ;
                               schema:name ?name .
}
""";
    private const string RepositoryReadmeInferenceAskQuery = """
PREFIX ex: <https://example.com/vocab/>
ASK WHERE {
  <https://example.com/README/> a ex:RepositoryReadme ;
                               a ex:QueryableKnowledgeAsset ;
                               ex:hasIndexedTitle "Markdown-LD Knowledge Bank" .
}
""";
    private const string RepositoryReadmeInferenceSelectQuery = """
PREFIX ex: <https://example.com/vocab/>
SELECT ?type WHERE {
  <https://example.com/README/> a ?type .
  FILTER(?type IN (ex:QueryableKnowledgeAsset, ex:RepositoryReadme))
}
ORDER BY ?type
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
        RdfLabel,
        EntityRdfSameAs,
    };
    private static readonly Uri ArticleGraphUri = new(GraphUri);

    [Test]
    public async Task BuildFromMarkdownAsync_uses_library_defaults_for_inline_markdown_metadata_without_fact_extraction()
    {
        var pipeline = new MarkdownKnowledgePipeline();

        var result = await pipeline.BuildFromMarkdownAsync(InlineMarkdown);

        result.Documents.Count.ShouldBe(1);
        result.Documents[0].Title.ShouldBe(InlineMarkdownTitle);

        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
        result.Diagnostics.ShouldNotBeEmpty();
        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();

        var searchRows = await result.Graph.SearchAsync(InlineMarkdownTitle);
        searchRows.Rows.Count.ShouldBe(1);
    }

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
    public async Task BuildAsync_uses_chat_facts_into_a_searchable_graph()
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

        chatClient.CallCount.ShouldBe(result.Documents.Single().Chunks.Count);
    }

    [Test]
    public async Task BuildAsync_returns_no_matches_for_empty_documents_and_rejects_mutating_sparql()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([new MarkdownSourceDocument(ContentPathEmpty, string.Empty)]);

        result.Documents.Count.ShouldBe(1);
        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();
        result.Graph.TripleCount.ShouldBe(0);

        var searchResults = await result.Graph.SearchAsync(SearchTermRdf);
        searchResults.Rows.ShouldBeEmpty();

        await Should.ThrowAsync<ReadOnlySparqlQueryException>(async () =>
            await result.Graph.ExecuteAskAsync(MutatingInsertDataQuery));
    }

    [Test]
    public async Task BuildAsync_does_not_materialize_empty_documents_when_other_documents_exist()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ContentPathKnowledgeGraph, FixtureLoader.Read(FixtureKnowledgeGraph)),
            new MarkdownSourceDocument(ContentPathEmpty, string.Empty),
        ]);

        var emptyDocumentUri = result.Documents.Single(document => document.SourcePath == ContentPathEmpty).DocumentUri.AbsoluteUri;
        var emptyDocumentExists = await result.Graph.ExecuteAskAsync(
            $@"ASK WHERE {{
  <{emptyDocumentUri}> ?predicate ?object .
}}");

        emptyDocumentExists.ShouldBeFalse();
    }

    [Test]
    public async Task BuildFromFileAsync_builds_the_repository_readme_into_a_queryable_rdf_graph()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var readmePath = GetRepositoryRootFilePath(RepositoryReadmeFileName);

        var result = await pipeline.BuildFromFileAsync(readmePath);

        result.Documents.Count.ShouldBe(1);
        result.Documents[0].SourcePath.ShouldBe(RepositoryReadmeFileName);
        result.Documents[0].Title.ShouldBe(RepositoryReadmeTitle);
        result.Documents[0].DocumentUri.AbsoluteUri.ShouldBe(RepositoryReadmeDocumentUri);
        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.Tiktoken);
        result.Graph.CanSearchByTokenDistance.ShouldBeTrue();

        (await result.Graph.ExecuteAskAsync(RepositoryReadmeAskQuery)).ShouldBeTrue();
        var titleRows = await result.Graph.ExecuteSelectAsync(RepositoryReadmeSelectQuery);
        titleRows.Rows.Count.ShouldBe(1);
        titleRows.Rows[0].Values[RepositoryReadmeTitleBindingKey].ShouldBe(RepositoryReadmeTitle);

        var tokenMatches = await result.Graph.SearchByTokenDistanceAsync(RepositoryReadmeTokenQuery, 3);
        tokenMatches.ShouldNotBeEmpty();
        tokenMatches.Any(match =>
                match.Text.Contains("RDF", StringComparison.OrdinalIgnoreCase) &&
                match.Text.Contains("SPARQL", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue();
    }

    [Test]
    public async Task BuildFromFileAsync_materializes_inference_over_the_repository_readme_graph()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var readmePath = GetRepositoryRootFilePath(RepositoryReadmeFileName);

        var result = await pipeline.BuildFromFileAsync(readmePath);
        var inference = await result.Graph.MaterializeInferenceAsync(new KnowledgeGraphInferenceOptions
        {
            AdditionalSchemaTexts = [RepositoryReadmeInferenceSchemaText],
            AdditionalN3RuleTexts = [RepositoryReadmeInferenceRulesText],
        });

        inference.InferredTripleCount.ShouldBeGreaterThan(0);
        inference.AppliedReasoners.ShouldContain("RDFS");
        inference.AppliedReasoners.ShouldContain("SKOS");
        inference.AppliedReasoners.ShouldContain("N3Rules");
        (await inference.Graph.ExecuteAskAsync(RepositoryReadmeInferenceAskQuery)).ShouldBeTrue();

        var inferredTypes = await inference.Graph.ExecuteSelectAsync(RepositoryReadmeInferenceSelectQuery);
        inferredTypes.Rows.Count.ShouldBe(2);
        inferredTypes.Rows.Select(row => row.Values[RepositoryReadmeInferenceTypeBindingKey]).ShouldBe([
            "https://example.com/vocab/QueryableKnowledgeAsset",
            "https://example.com/vocab/RepositoryReadme",
        ]);
    }

    private static string GetRepositoryRootFilePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "MarkdownLd.Kb.slnx");
            if (File.Exists(solutionPath))
            {
                var filePath = Path.Combine(directory.FullName, fileName);
                File.Exists(filePath).ShouldBeTrue();
                return filePath;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be located from the test base directory.");
    }
}
