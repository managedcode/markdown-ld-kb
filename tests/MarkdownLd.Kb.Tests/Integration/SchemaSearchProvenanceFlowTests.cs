using System.Text.Json;
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

    [Test]
    public async Task Large_source_sets_preserve_direct_and_relationship_evidence()
    {
        var primarySources = CreateSourceIds("manual");
        var relatedSources = CreateSourceIds("dataset");
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson.Replace("Weather forecast", "Rainfall forecast", StringComparison.Ordinal));
        using var provenance = KnowledgeGraph.LoadJsonLd($$"""
            { "@context": { "p": "http://www.w3.org/ns/prov#", "r": "http://www.w3.org/2000/01/rdf-schema#" }, "@graph": [
              { "@id": "https://provenance.example/tool", "p:wasDerivedFrom": {{RenderSourceIds(primarySources)}} },
              { "@id": "https://provenance.example/topic", "p:wasDerivedFrom": {{RenderSourceIds(relatedSources)}} },
              { "@id": "{{relatedSources[0]}}", "r:label": "Rainfall dataset" }
            ] }
            """);
        await graph.MergeAsync(provenance);

        var result = await graph.SearchBySchemaAsync("Rainfall", new KnowledgeGraphSchemaSearchProfile
        {
            TypeFilters = ["schema:SoftwareApplication"],
            TextPredicates = [new("schema:name")],
            RelationshipPredicates = [new("schema:about", ["rdfs:label"])],
            ExpansionPredicates = [],
            MaxResults = 1
        });

        var evidence = result.Matches.Single().Evidence;
        var direct = evidence.Single(item => item.Kind == KnowledgeGraphSchemaSearchEvidenceKind.Direct);
        var related = evidence.Single(item => item.Kind == KnowledgeGraphSchemaSearchEvidenceKind.Relationship);
        direct.SourceContexts.Select(source => source.SourceId).ShouldBe(primarySources);
        related.SourceContexts.Select(source => source.SourceId).ShouldBe(relatedSources.Concat(primarySources));
        related.SourceContexts[0].SourceLabel.ShouldBe("Rainfall dataset");

        var diagnostic = await graph.ExecuteSelectAsync(result.GeneratedSparql);
        var directRows = diagnostic.Rows.Where(row => row.Values["evidenceKind"] == "direct").ToArray();
        directRows.Select(row => row.Values["source"]).Distinct().Order(StringComparer.Ordinal).ShouldBe(primarySources);
        directRows.ShouldAllBe(row => !row.Values.ContainsKey("relatedSource"));
        diagnostic.Rows.Where(row => row.Values["evidenceKind"] == "relationship")
            .Select(row => row.Values["relatedSource"]).Distinct().Order(StringComparer.Ordinal).ShouldBe(relatedSources);
    }

    private static string[] CreateSourceIds(string kind) =>
        Enumerable.Range(0, 32).Select(index => $"https://provenance.example/{kind}/{index:D2}").ToArray();

    private static string RenderSourceIds(IEnumerable<string> ids) =>
        JsonSerializer.Serialize(ids.Select(id => new Dictionary<string, string> { ["@id"] = id }));

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Disabled_relationship_targets_do_not_emit_unbound_evidence_rows(bool federated)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        using var root = KnowledgeGraph.LoadJsonLd("""{"@graph": []}""");
        var endpoint = new Uri("https://provenance.example/sparql");
        var profile = new KnowledgeGraphSchemaSearchProfile
        {
            TypeFilters = ["schema:SoftwareApplication"],
            TextPredicates = [new("schema:name")],
            RelationshipPredicates = [new("schema:about", [])],
            ExpansionPredicates = [],
            FederatedServiceEndpoints = [endpoint]
        };
        var options = new FederatedSparqlExecutionOptions { AllowedServiceEndpoints = [endpoint], LocalServiceBindings = [new(endpoint, graph)] };
        var result = federated ? await root.SearchBySchemaFederatedAsync("Rainfall", profile, options) : await graph.SearchBySchemaAsync("Rainfall", profile);
        result.Matches.ShouldBeEmpty();
        var rows = federated ? (await root.ExecuteFederatedSelectAsync(result.GeneratedSparql, options)).Result.Rows : (await graph.ExecuteSelectAsync(result.GeneratedSparql)).Rows;
        rows.ShouldBeEmpty();
    }

}
