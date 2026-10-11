using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaCandidateFilterFlowTests
{
    private const string ToolId = "https://candidate.example/tools/one";
    private const string SecondId = "https://candidate.example/tools/two";
    private const string EvidenceId = "https://candidate.example/evidence";
    private const string GraphJson = """
        { "@context": { "schema": "https://schema.org/", "kb": "https://markdownld.org/vocab#" }, "@graph": [
          { "@id": "https://candidate.example/tools/one", "@type": "schema:SoftwareApplication", "schema:name": "Weather forecast", "schema:about": { "@id": "https://candidate.example/evidence" } },
          { "@id": "https://candidate.example/tools/two", "@type": "schema:SoftwareApplication", "schema:name": "Weather forecast sibling" },
          { "@id": "https://candidate.example/evidence", "@type": "schema:DefinedTerm", "schema:name": "Rainfall evidence" }
        ] }
        """;

    [Test]
    [Arguments(false, "Weather")]
    [Arguments(false, "Rainfall")]
    [Arguments(true, "Weather")]
    [Arguments(true, "Rainfall")]
    public async Task Candidate_values_filter_primary_subjects_and_preserve_relationship_evidence(bool federated, string query)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        using var root = KnowledgeGraph.LoadJsonLd("{\"@graph\": []}");
        var count = graph.TripleCount;
        var endpoint = new Uri("https://candidate.example/sparql");
        var profile = CreateProfile() with { CandidateNodeIds = [ToolId, ToolId], FederatedServiceEndpoints = [endpoint] };
        var result = federated
            ? await root.SearchBySchemaFederatedAsync(query, profile, new FederatedSparqlExecutionOptions
            {
                AllowedServiceEndpoints = [endpoint],
                LocalServiceBindings = [new(endpoint, graph)]
            })
            : await graph.SearchBySchemaAsync(query, profile);
        result.Matches.Single().NodeId.ShouldBe(ToolId);
        result.GeneratedSparql.ShouldContain("VALUES ?subject");
        graph.TripleCount.ShouldBe(count);
        if (query == "Rainfall")
        {
            result.Matches.Single().Evidence.Single().RelatedNodeId.ShouldBe(EvidenceId);
        }

        if (!federated)
        {
            result.RelatedMatches.Single().NodeId.ShouldBe(EvidenceId);
        }
    }

    [Test]
    public async Task Empty_and_unknown_candidates_return_no_matches_without_broadening_search()
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        var empty = await graph.SearchBySchemaAsync("Weather", CreateProfile() with { CandidateNodeIds = [] });
        var unknown = await graph.SearchBySchemaAsync("Weather", CreateProfile() with { CandidateNodeIds = ["https://candidate.example/missing"] });
        empty.Matches.ShouldBeEmpty();
        unknown.Matches.ShouldBeEmpty();
        var unrestricted = await graph.SearchBySchemaAsync("Weather", CreateProfile());
        unrestricted.Matches.Count.ShouldBe(2);
    }

    [Test]
    public async Task Concurrent_profiles_do_not_share_candidate_state()
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        var results = await Task.WhenAll(
            graph.SearchBySchemaAsync("Weather", CreateProfile() with { CandidateNodeIds = [ToolId] }),
            graph.SearchBySchemaAsync("Weather", CreateProfile() with { CandidateNodeIds = [SecondId] }));
        results[0].Matches.Single().NodeId.ShouldBe(ToolId);
        results[1].Matches.Single().NodeId.ShouldBe(SecondId);
    }

    [Test]
    [Arguments("")]
    [Arguments("relative")]
    [Arguments("https://candidate.example/> } UNION { ?s ?p ?o")]
    [Arguments("https://candidate.example/\ninvalid")]
    public async Task Invalid_candidate_iris_fail_explicitly(string nodeId)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        await Should.ThrowAsync<ArgumentException>(() => graph.SearchBySchemaAsync("Weather", CreateProfile() with { CandidateNodeIds = [nodeId] }));
    }

    [Test]
    [Arguments(KnowledgeGraphSchemaSearchTermMode.AnyTerm, "Weather absent", 1)]
    [Arguments(KnowledgeGraphSchemaSearchTermMode.AllTerms, "Weather absent", 0)]
    [Arguments(KnowledgeGraphSchemaSearchTermMode.AllTerms, "Weather forecast", 1)]
    [Arguments(KnowledgeGraphSchemaSearchTermMode.ExactPhrase, "Weather forecast", 1)]
    public async Task Candidate_values_preserve_multi_term_match_semantics(KnowledgeGraphSchemaSearchTermMode mode, string query, int expected)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        var result = await graph.SearchBySchemaAsync(query, CreateProfile() with { CandidateNodeIds = [ToolId], TermMode = mode });
        result.Matches.Count.ShouldBe(expected);
        if (expected > 0)
        {
            result.Matches.Single().NodeId.ShouldBe(ToolId);
        }
    }

    private static KnowledgeGraphSchemaSearchProfile CreateProfile() => new()
    {
        TypeFilters = ["schema:SoftwareApplication"],
        TextPredicates = [new("schema:name")],
        RelationshipPredicates = [new("schema:about", ["schema:name"])],
        ExpansionPredicates = [new("schema:about", KnowledgeGraphSchemaSearchRole.Related, 1d)]
    };
}
