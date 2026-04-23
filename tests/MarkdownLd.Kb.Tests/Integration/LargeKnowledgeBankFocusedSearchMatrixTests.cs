using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class LargeKnowledgeBankFocusedSearchMatrixTests
{
    private const string CacheRecoveryDocumentUri = "https://large-fixture.example/runbooks/cache-recovery-workflow/";
    private const string QueryFederationDocumentUri = "https://large-fixture.example/runbooks/query-federation-runbook/";
    private const string SemanticSearchDocumentUri = "https://large-fixture.example/runbooks/semantic-search-tuning/";
    private const string ReleaseGateDocumentUri = "https://large-fixture.example/runbooks/release-gate-checklist/";

    [Test]
    [Arguments("mixed mdx jsonl yaml sources and chunk boundary ledger", "Graph Ingestion Playbook")]
    [Arguments("same source path different document id distinct slot required", "Cache Recovery Workflow")]
    [Arguments("cross-language recall map for ukrainian alerts", "Semantic Search Tuning")]
    [Arguments("collect rollback evidence and hotfix timeline during incident triage", "Incident Triage Guide")]
    [Arguments("prepare deterministic snapshot and coverage bundle before release", "Release Gate Checklist")]
    [Arguments("histogram rebucketing and p99 drift route calibration", "Observability Regression Workbook")]
    public async Task Focused_search_returns_expected_primary_article_for_large_natural_language_queries(
        string query,
        string expectedTitle)
    {
        var result = await BuildLargeGraphAsync();

        var focused = await result.Graph.SearchFocusedAsync(
            query,
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 3,
                MaxNextStepResults = 3,
            });

        focused.PrimaryMatches.Count.ShouldBe(1);
        focused.PrimaryMatches[0].Label.ShouldBe(expectedTitle);
    }

    [Test]
    public async Task Focused_search_explains_related_and_next_step_context_for_cache_recovery()
    {
        var result = await BuildLargeGraphAsync();

        var focused = await result.Graph.SearchFocusedAsync(
            "same source path different document id distinct slot required",
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 4,
                MaxNextStepResults = 2,
            });

        focused.PrimaryMatches.Single().NodeId.ShouldBe(LargeKnowledgeBankFixtureCatalog.CacheRecovery.DocumentUri);
        focused.RelatedMatches.Select(static match => match.NodeId).ShouldContain(LargeKnowledgeBankFixtureCatalog.GraphIngestion.DocumentUri);
        focused.RelatedMatches.Select(static match => match.NodeId).ShouldContain(LargeKnowledgeBankFixtureCatalog.IncidentTriage.DocumentUri);
        focused.NextStepMatches.Select(static match => match.NodeId).ShouldContain(LargeKnowledgeBankFixtureCatalog.ReleaseGate.DocumentUri);

        focused.FocusedGraph.Nodes.Select(static node => node.Label).ShouldContain("Reliability Operations");
        focused.FocusedGraph.Nodes.Select(static node => node.Label).ShouldContain("Graph Operations");
        focused.FocusedGraph.Edges.Select(static edge => edge.PredicateLabel).ShouldContain("kb:relatedTo");
        focused.FocusedGraph.Edges.Select(static edge => edge.PredicateLabel).ShouldContain("kb:nextStep");
    }

    [Test]
    public async Task Focused_search_respects_match_limits_on_large_corpus()
    {
        var result = await BuildLargeGraphAsync();

        var focused = await result.Graph.SearchFocusedAsync(
            "query safety review and cache recovery incident release",
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 1,
                MaxNextStepResults = 1,
            });

        focused.PrimaryMatches.Count.ShouldBe(1);
        focused.RelatedMatches.Count.ShouldBeLessThanOrEqualTo(1);
        focused.NextStepMatches.Count.ShouldBeLessThanOrEqualTo(1);
        focused.FocusedGraph.Nodes.Count.ShouldBeLessThanOrEqualTo(6);
    }

    [Test]
    [Arguments("old cache slot could not be reused after the prompt version gate changed", CacheRecoveryDocumentUri, "prompt version gate")]
    [Arguments("read only query safety while inspecting remote graphs", QueryFederationDocumentUri, "read-only")]
    [Arguments("ukrainian and english paraphrases to the same workflow title", SemanticSearchDocumentUri, "ukrainian")]
    [Arguments("coverage dashboard or cache directory audit is missing", ReleaseGateDocumentUri, "coverage dashboard")]
    [Arguments("histogram rebucketing threshold ledger after p99 drift", "https://large-fixture.example/runbooks/observability-regression-workbook/", "histogram rebucketing threshold ledger")]
    public async Task Token_distance_search_maps_real_body_queries_to_expected_large_documents(
        string query,
        string expectedDocumentUri,
        string expectedText)
    {
        var result = await BuildLargeGraphAsync();

        var matches = await result.Graph.SearchByTokenDistanceAsync(query, 1);

        matches.Single().DocumentId.ShouldBe(expectedDocumentUri);
    }

    private static async Task<MarkdownKnowledgeBuildResult> BuildLargeGraphAsync()
    {
        return await LargeKnowledgeBankBuildCache.GetAsync(MarkdownKnowledgeExtractionMode.Tiktoken);
    }
}
