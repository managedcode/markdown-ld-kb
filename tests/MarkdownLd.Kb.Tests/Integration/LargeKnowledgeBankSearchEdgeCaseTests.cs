using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class LargeKnowledgeBankSearchEdgeCaseTests
{
    private const string QueryFederationDocumentUri = "https://large-fixture.example/runbooks/query-federation-runbook/";
    private const string SemanticSearchDocumentUri = "https://large-fixture.example/runbooks/semantic-search-tuning/";
    private const string SearchEdgeCaseLabDocumentUri = "https://large-fixture.example/runbooks/search-edge-case-lab/";
    private const string QuarterlyArchiveDigestDocumentUri = "https://large-fixture.example/runbooks/quarterly-archive-digest/";
    private const string IncidentTriageDocumentUri = "https://large-fixture.example/runbooks/incident-triage-guide/";
    private const string ObservabilityRegressionDocumentUri = "https://large-fixture.example/runbooks/observability-regression-workbook/";
    private const string SearchSubjectKey = "subject";

    [Test]
    public async Task Graph_search_ignores_body_only_noise_from_large_archive_digest()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var matches = await graph.SearchRankedAsync(
            "remote endpoints",
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Graph,
                MaxResults = 5,
            });

        matches.ShouldNotBeEmpty();
        matches[0].NodeId.ShouldBe(QueryFederationDocumentUri);
        matches.Select(static match => match.NodeId).ShouldNotContain(QuarterlyArchiveDigestDocumentUri);
    }

    [Test]
    public async Task Search_api_keeps_numeric_release_identifiers_exact()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var exact = await graph.SearchAsync("2.4.17");
        var nearMiss = await graph.SearchAsync("2.4.18");

        exact.Rows.Any(row =>
            row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
            subject == SearchEdgeCaseLabDocumentUri).ShouldBeTrue();
        nearMiss.Rows.Any(row =>
            row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
            subject == SearchEdgeCaseLabDocumentUri).ShouldBeFalse();
    }

    [Test]
    public async Task Search_api_keeps_short_acronym_queries_exact()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var exact = await graph.SearchAsync("VPN");
        var nearMiss = await graph.SearchAsync("VDN");

        exact.Rows.Any(row =>
            row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
            subject == SearchEdgeCaseLabDocumentUri).ShouldBeTrue();
        nearMiss.Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task Search_api_does_not_index_body_only_endpoint_uri_but_token_distance_search_does()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var canonical = await graph.SearchAsync("query.wikidata.org/sparql");

        canonical.Rows.ShouldBeEmpty();

        var tokenGraph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);
        var matches = await tokenGraph.SearchByTokenDistanceAsync("query.wikidata.org/sparql background vocabulary lookup", 1);

        matches.Single().DocumentId.ShouldBe(QueryFederationDocumentUri);
        matches.Single().Text.Contains("query.wikidata.org/sparql", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Test]
    public async Task Search_api_does_not_index_body_only_multilingual_phrase_but_token_distance_search_does()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var canonical = await graph.SearchAsync("українські запити про cache recovery");

        canonical.Rows.ShouldBeEmpty();

        var tokenGraph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);
        var matches = await tokenGraph.SearchByTokenDistanceAsync("українські запити про cache recovery", 1);

        matches.Single().DocumentId.ShouldBe(SemanticSearchDocumentUri);
        matches.Single().Text.Contains("українські запити про cache recovery", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Test]
    public async Task Search_api_treats_quoted_mutating_terms_as_body_only_data_until_deep_lexical_search_is_used()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var canonical = await graph.SearchAsync("\"DELETE\"");

        canonical.Rows.ShouldBeEmpty();

        var tokenGraph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);
        var matches = await tokenGraph.SearchByTokenDistanceAsync("\"DELETE\" can appear in a quoted support transcript", 1);

        matches.Single().DocumentId.ShouldBe(SearchEdgeCaseLabDocumentUri);
        matches.Single().Text.Contains("quoted support transcript", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Test]
    public async Task Hybrid_search_recovers_synonym_like_query_that_graph_mode_cannot_answer()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);
        var semanticIndex = await graph.BuildSemanticIndexAsync(new SearchEdgeEmbeddingGenerator());

        var graphMatches = await graph.SearchRankedAsync(
            "endpoint whitelist audit",
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Graph,
                MaxResults = 3,
            });
        var hybridMatches = await graph.SearchRankedAsync(
            "endpoint whitelist audit",
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Hybrid,
                MaxResults = 3,
                MaxSemanticResults = 3,
            },
            semanticIndex);

        graphMatches.ShouldBeEmpty();
        hybridMatches.ShouldNotBeEmpty();
        hybridMatches[0].NodeId.ShouldBe(QueryFederationDocumentUri);
        hybridMatches[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Semantic);
    }

    [Test]
    public async Task Graph_search_prefers_entity_rich_query_federation_document_over_archive_noise()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.None);

        var matches = await graph.SearchRankedAsync(
            "read-only SPARQL",
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Graph,
                MaxResults = 3,
            });

        matches.ShouldNotBeEmpty();
        matches[0].NodeId.ShouldBe(QueryFederationDocumentUri);
        matches.Select(static match => match.NodeId).ShouldNotContain(QuarterlyArchiveDigestDocumentUri);
    }

    [Test]
    public async Task Token_distance_search_finds_unique_phrase_from_late_appendix_in_large_document()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);

        var matches = await graph.SearchByTokenDistanceAsync("archive reconciliation checksum window approved release 2.4.17", 1);

        matches.Single().DocumentId.ShouldBe(SearchEdgeCaseLabDocumentUri);
        matches.Single().Text.Contains("archive reconciliation checksum window", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Test]
    public async Task Focused_search_uses_body_only_endpoint_uri_to_recover_query_federation_context()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);

        var focused = await graph.SearchFocusedAsync(
            "query.wikidata.org/sparql background vocabulary lookup provenance audit",
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 2,
                MaxNextStepResults = 2,
            });

        focused.PrimaryMatches.Single().NodeId.ShouldBe(QueryFederationDocumentUri);
        focused.NextStepMatches.Select(static match => match.NodeId).ShouldContain(IncidentTriageDocumentUri);
        focused.NextStepMatches.Select(static match => match.NodeId).ShouldContain(LargeKnowledgeBankFixtureCatalog.ReleaseGate.DocumentUri);
    }

    [Test]
    public async Task Focused_search_routes_release_identifier_query_to_edge_case_lab()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);

        var focused = await graph.SearchFocusedAsync(
            "vpn reset approved release 2.4.17 evidence bundle",
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 2,
                MaxNextStepResults = 2,
            });

        focused.PrimaryMatches.Single().NodeId.ShouldBe(SearchEdgeCaseLabDocumentUri);
        focused.NextStepMatches.Select(static match => match.NodeId)
            .ShouldContain(LargeKnowledgeBankFixtureCatalog.ReleaseGate.DocumentUri);
    }

    [Test]
    public async Task Focused_search_recovers_observability_workbook_from_body_heavy_regression_query()
    {
        var graph = await BuildGraphAsync(MarkdownKnowledgeExtractionMode.Tiktoken);

        var focused = await graph.SearchFocusedAsync(
            "histogram rebucketing threshold ledger p99 drift route calibration",
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 3,
                MaxNextStepResults = 2,
            });

        focused.PrimaryMatches.Single().NodeId.ShouldBe(ObservabilityRegressionDocumentUri);
        focused.RelatedMatches.Select(static match => match.NodeId).ShouldContain(IncidentTriageDocumentUri);
        focused.RelatedMatches.Select(static match => match.NodeId).ShouldContain(SemanticSearchDocumentUri);
        focused.NextStepMatches.Select(static match => match.NodeId)
            .ShouldContain(LargeKnowledgeBankFixtureCatalog.ReleaseGate.DocumentUri);
    }

    private static async Task<KnowledgeGraph> BuildGraphAsync(MarkdownKnowledgeExtractionMode mode)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            LargeKnowledgeBankFixtureCatalog.BaseUri,
            extractionMode: mode);
        var result = await pipeline.BuildAsync(LargeKnowledgeBankFixtureCatalog.CreateGraphSources());
        return result.Graph;
    }

    private sealed class SearchEdgeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private static readonly string[] QueryFederationTerms =
        [
            "query",
            "federation",
            "remote",
            "endpoint",
            "allowlist",
            "whitelist",
            "audit",
            "sparql",
            "read only",
            "provenance",
        ];

        private static readonly string[] EdgeCaseTerms =
        [
            "vpn",
            "release",
            "2.4.17",
            "checksum",
            "appendix",
            "acronym",
            "ledger",
        ];

        public object? GetService(Type serviceType, object? serviceKey)
        {
            return serviceType == typeof(IEmbeddingGenerator<string, Embedding<float>>) ? this : null;
        }

        public void Dispose()
        {
        }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embeddings = values
                .Select(CreateEmbedding)
                .ToArray();

            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        private static Embedding<float> CreateEmbedding(string value)
        {
            var normalized = value.ToLowerInvariant();
            var vector = new float[2];

            if (ContainsAny(normalized, QueryFederationTerms))
            {
                vector[0] = 1;
            }

            if (ContainsAny(normalized, EdgeCaseTerms))
            {
                vector[1] = 1;
            }

            return new Embedding<float>(vector);
        }

        private static bool ContainsAny(string value, IEnumerable<string> terms)
        {
            return terms.Any(term => value.Contains(term, StringComparison.Ordinal));
        }
    }
}
