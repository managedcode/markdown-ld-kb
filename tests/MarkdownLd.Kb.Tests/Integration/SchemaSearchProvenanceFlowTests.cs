using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaSearchProvenanceFlowTests
{
    private const string GraphJson = """
        { "@context": { "s": "https://schema.org/", "p": "http://www.w3.org/ns/prov#", "r": "http://www.w3.org/2000/01/rdf-schema#" }, "@graph": [
          { "@id": "https://provenance.example/tool", "@type": ["s:SoftwareApplication", "s:Action"], "s:name": "Weather forecast", "s:description": "Calculate rainfall", "s:about": { "@id": "https://provenance.example/topic" } },
          { "@id": "https://provenance.example/topic", "@type": "s:DefinedTerm", "r:label": "Rainfall measurements" },
          { "@id": "https://provenance.example/unrelated", "s:name": "Unrelated source label" }
        ] }
        """;

    [Test]
    [Arguments(false, "Weather")]
    [Arguments(false, "Rainfall")]
    [Arguments(true, "Weather")]
    [Arguments(true, "Rainfall")]
    public async Task Missing_provenance_does_not_bind_unrelated_nodes_as_sources(bool federated, string query)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        using var root = KnowledgeGraph.LoadJsonLd("""{"@graph": []}""");
        var endpoint = new Uri("https://provenance.example/sparql");
        var profile = new KnowledgeGraphSchemaSearchProfile
        {
            TypeFilters = ["schema:SoftwareApplication"],
            TextPredicates = [new("schema:name")],
            RelationshipPredicates = [new("schema:about", ["rdfs:label"])],
            FederatedServiceEndpoints = [endpoint],
            MaxRelatedResults = 0,
            MaxNextStepResults = 0
        };
        var result = federated
            ? await root.SearchBySchemaFederatedAsync(query, profile, new FederatedSparqlExecutionOptions
            {
                AllowedServiceEndpoints = [endpoint],
                LocalServiceBindings = [new(endpoint, graph)]
            })
            : await graph.SearchBySchemaAsync(query, profile);
        var match = result.Matches.Single();
        match.Label.ShouldBe("Weather forecast");
        match.Description.ShouldBe("Calculate rainfall");
        match.Evidence.Single().SourceContexts.ShouldBeEmpty();
        if (query == "Rainfall")
        {
            match.Evidence.Single().RelatedNodeLabel.ShouldBe("Rainfall measurements");
        }
    }
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Grouped_evidence_preserves_multiple_types_and_labelled_or_unlabelled_sources(bool federated)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        using var provenance = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "p": "http://www.w3.org/ns/prov#", "r": "http://www.w3.org/2000/01/rdf-schema#" }, "@graph": [
              { "@id": "https://provenance.example/tool", "p:wasDerivedFrom": [{ "@id": "https://provenance.example/manual" }, { "@id": "https://provenance.example/unlabelled" }] },
              { "@id": "https://provenance.example/topic", "p:wasDerivedFrom": { "@id": "https://provenance.example/dataset" } },
              { "@id": "https://provenance.example/manual", "r:label": "Manual" },
              { "@id": "https://provenance.example/dataset", "r:label": "Dataset" }
            ] }
            """);
        await graph.MergeAsync(provenance);
        using var root = KnowledgeGraph.LoadJsonLd("""{"@graph": []}""");
        var endpoint = new Uri("https://provenance.example/sparql");
        var profile = new KnowledgeGraphSchemaSearchProfile
        {
            ExcludedTypes = [],
            TextPredicates = [new("schema:name")],
            RelationshipPredicates = [new("schema:about", ["rdfs:label"])],
            FederatedServiceEndpoints = [endpoint],
            MaxRelatedResults = 0,
            MaxNextStepResults = 0
        };
        var result = federated
            ? await root.SearchBySchemaFederatedAsync("Rainfall", profile, new FederatedSparqlExecutionOptions
            {
                AllowedServiceEndpoints = [endpoint],
                LocalServiceBindings = [new(endpoint, graph)]
            })
            : await graph.SearchBySchemaAsync("Rainfall", profile);
        var match = result.Matches.Single();
        match.Types.Order().ShouldBe(new[] { "https://schema.org/Action", "https://schema.org/SoftwareApplication" }.Order());
        var sources = match.Evidence.Single().SourceContexts;
        sources.Count.ShouldBe(3);
        sources.Single(source => source.SourceId.EndsWith("/manual", StringComparison.Ordinal)).SourceLabel.ShouldBe("Manual");
        sources.Single(source => source.SourceId.EndsWith("/dataset", StringComparison.Ordinal)).SourceLabel.ShouldBe("Dataset");
        sources.Single(source => source.SourceId.EndsWith("/unlabelled", StringComparison.Ordinal)).SourceLabel.ShouldBeNull();
    }

}
