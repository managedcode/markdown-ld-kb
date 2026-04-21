using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class LargeKnowledgeBankGraphQueryMatrixTests
{
    private const string ReliabilityGroupName = "Reliability Operations";
    private const string SearchSubjectKey = "subject";
    private const int QueryLimit = 1;
    private const string GraphIngestionDocumentUri = "https://large-fixture.example/runbooks/graph-ingestion-playbook/";
    private const string QueryFederationDocumentUri = "https://large-fixture.example/runbooks/query-federation-runbook/";
    private const string CacheRecoveryDocumentUri = "https://large-fixture.example/runbooks/cache-recovery-workflow/";
    private const string SemanticSearchDocumentUri = "https://large-fixture.example/runbooks/semantic-search-tuning/";
    private const string IncidentTriageDocumentUri = "https://large-fixture.example/runbooks/incident-triage-guide/";
    private const string ReleaseGateDocumentUri = "https://large-fixture.example/runbooks/release-gate-checklist/";
    private const string ObservabilityRegressionDocumentUri = "https://large-fixture.example/runbooks/observability-regression-workbook/";

    private const string DenseGraphAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://large-fixture.example/runbooks/graph-ingestion-playbook/> a schema:Article ;
    schema:name "Graph Ingestion Playbook" ;
    kb:memberOf ?graphGroup ;
    kb:relatedTo <https://large-fixture.example/runbooks/cache-recovery-workflow/> ;
    kb:nextStep <https://large-fixture.example/runbooks/query-federation-runbook/> .
  ?graphGroup schema:name "Graph Operations" .
  <https://large-fixture.example/runbooks/query-federation-runbook/> a schema:Article ;
    schema:name "Query Federation Runbook" .
  <https://large-fixture.example/runbooks/cache-recovery-workflow/> a schema:Article ;
    schema:name "Cache Recovery Workflow" ;
    kb:nextStep <https://large-fixture.example/runbooks/release-gate-checklist/> .
  <https://large-fixture.example/runbooks/semantic-search-tuning/> a schema:Article ;
    schema:name "Semantic Search Tuning" .
  <https://large-fixture.example/runbooks/incident-triage-guide/> a schema:Article ;
    schema:name "Incident Triage Guide" .
  <https://large-fixture.example/runbooks/release-gate-checklist/> a schema:Article ;
    schema:name "Release Gate Checklist" .
}
""";

    private const string ReliabilityGroupSelectQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
SELECT ?name WHERE {
  ?article a schema:Article ;
           schema:name ?name ;
           kb:memberOf ?group .
  ?group schema:name "Reliability Operations" .
}
ORDER BY ?name
""";

    [Test]
    public async Task Large_tiktoken_corpus_builds_dense_graph_with_many_documents_and_sections()
    {
        var result = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        result.Documents.Count.ShouldBe(LargeKnowledgeBankFixtureCatalog.GraphDocuments.Count);
        result.Documents.Sum(static document => document.Chunks.Count).ShouldBeGreaterThanOrEqualTo(12);
        result.Graph.TripleCount.ShouldBeGreaterThan(120);
        result.ExtractionMode.ShouldBe(MarkdownKnowledgeExtractionMode.None);
        result.Graph.CanSearchByTokenDistance.ShouldBeFalse();
    }

    [Test]
    public async Task Large_tiktoken_corpus_supports_dense_cross_document_sparql_assertions()
    {
        var result = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var graphHasExpectedEdges = await result.Graph.ExecuteAskAsync(DenseGraphAskQuery);

        graphHasExpectedEdges.ShouldBeTrue();
    }

    [Test]
    public async Task Large_tiktoken_corpus_returns_articles_from_reliability_group()
    {
        var result = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var rows = await result.Graph.ExecuteSelectAsync(ReliabilityGroupSelectQuery);
        var names = rows.Rows
            .Select(static row => row.Values["name"])
            .ToArray();

        names.ShouldContain(LargeKnowledgeBankFixtureCatalog.GraphIngestion.Title);
        names.ShouldContain(LargeKnowledgeBankFixtureCatalog.QueryFederation.Title);
        names.ShouldContain(LargeKnowledgeBankFixtureCatalog.CacheRecovery.Title);
        names.ShouldContain(LargeKnowledgeBankFixtureCatalog.IncidentTriage.Title);
    }

    [Test]
    [Arguments("graph ingestion playbook", "Graph Ingestion Playbook")]
    [Arguments("query federation runbook", "Query Federation Runbook")]
    [Arguments("cache recovery workflow", "Cache Recovery Workflow")]
    [Arguments("semantic search tuning", "Semantic Search Tuning")]
    [Arguments("incident triage guide", "Incident Triage Guide")]
    [Arguments("release gate checklist", "Release Gate Checklist")]
    [Arguments("histogram rebucketing", "Observability Regression Workbook")]
    public async Task Ranked_graph_search_returns_expected_primary_article_for_large_corpus(
        string query,
        string expectedTitle)
    {
        var result = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var matches = await result.Graph.SearchRankedAsync(
            query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Graph,
                MaxResults = 3,
            });

        matches.ShouldNotBeEmpty();
        matches[0].Label.ShouldBe(expectedTitle);
    }

    [Test]
    [Arguments("Graph Ingestion Playbook", GraphIngestionDocumentUri)]
    [Arguments("Query Federation Runbook", QueryFederationDocumentUri)]
    [Arguments("Cache Recovery Workflow", CacheRecoveryDocumentUri)]
    [Arguments("Release Gate Checklist", ReleaseGateDocumentUri)]
    [Arguments("sampling bias", ObservabilityRegressionDocumentUri)]
    public async Task Search_api_finds_large_corpus_articles_from_realistic_terms(
        string term,
        string expectedSubject)
    {
        var result = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var search = await result.Graph.SearchAsync(term);

        search.Rows.Any(row =>
            row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
            subject == expectedSubject).ShouldBeTrue();
    }

    [Test]
    [Arguments("chunk fingerprints model identity and document identity", CacheRecoveryDocumentUri, "chunk")]
    [Arguments("ukrainian queries and semantic fallback thresholds", SemanticSearchDocumentUri, "semantic")]
    [Arguments("rollback evidence ledger and prompt version gate", IncidentTriageDocumentUri, "prompt version gate")]
    [Arguments("deterministic snapshot report against the previous baseline", ReleaseGateDocumentUri, "deterministic snapshot report")]
    [Arguments("histogram rebucketing threshold after p99 latency drift", ObservabilityRegressionDocumentUri, "histogram rebucketing threshold")]
    public async Task Token_distance_search_uses_large_real_sections_instead_of_toy_snippets(
        string query,
        string expectedDocumentUri,
        string expectedText)
    {
        var result = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);

        var matches = await result.Graph.SearchByTokenDistanceAsync(query, QueryLimit);

        matches.Single().DocumentId.ShouldBe(expectedDocumentUri);
    }

    private static async Task<MarkdownKnowledgeBuildResult> BuildGraphAsync(MarkdownKnowledgeExtractionMode extractionMode)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            LargeKnowledgeBankFixtureCatalog.BaseUri,
            extractionMode: extractionMode);

        return await pipeline.BuildAsync(LargeKnowledgeBankFixtureCatalog.CreateGraphSources());
    }
}
