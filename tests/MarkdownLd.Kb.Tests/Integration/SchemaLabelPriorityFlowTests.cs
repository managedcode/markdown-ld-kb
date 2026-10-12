using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaLabelPriorityFlowTests
{
    private const string NodeId = "https://labels.example/tool";
    private const string TopicId = "https://labels.example/topic";
    private const string SourceId = "https://labels.example/source";
    private const string GraphTemplate = """
        { "@context": { "s": "https://schema.org/", "r": "http://www.w3.org/2000/01/rdf-schema#", "k": "http://www.w3.org/2004/02/skos/core#", "p": "http://www.w3.org/ns/prov#" }, "@graph": [
          { "@id": "https://labels.example/tool", "@type": "s:SoftwareApplication", "s:description": "weather", "s:about": { "@id": "https://labels.example/topic" }, "p:wasDerivedFrom": { "@id": "https://labels.example/source" }, LABELS },
          { "@id": "https://labels.example/topic", "@type": "s:DefinedTerm", "s:description": "rainfall", "p:wasDerivedFrom": { "@id": "https://labels.example/source" }, LABELS },
          { "@id": "https://labels.example/source", "@type": "s:CreativeWork", LABELS },
          { "@id": "https://labels.example/unrelated", "s:name": "Must never become a source" }
        ] }
        """;

    [Test]
    [Arguments(false, 0)]
    [Arguments(false, 1)]
    [Arguments(false, 2)]
    [Arguments(false, 3)]
    [Arguments(true, 0)]
    [Arguments(true, 1)]
    [Arguments(true, 2)]
    [Arguments(true, 3)]
    public async Task Preferred_labels_preserve_all_values_and_scoped_provenance(bool federated, int mode)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(CreateGraphJson(mode));
        using var root = KnowledgeGraph.LoadJsonLd("""{"@graph": []}""");
        var endpoint = new Uri("https://labels.example/sparql");
        var profile = new KnowledgeGraphSchemaSearchProfile
        {
            CandidateNodeIds = [NodeId],
            TypeFilters = ["schema:SoftwareApplication"],
            TextPredicates = [new("schema:description")],
            RelationshipPredicates = [new("schema:about", ["schema:description"])],
            ExpansionPredicates = [new("schema:about", KnowledgeGraphSchemaSearchRole.Related, 0.7)],
            FederatedServiceEndpoints = [endpoint]
        };
        var options = new FederatedSparqlExecutionOptions { AllowedServiceEndpoints = [endpoint], LocalServiceBindings = [new(endpoint, graph)] };
        foreach (var query in new[] { "weather", "rainfall" })
        {
            var result = federated ? await root.SearchBySchemaFederatedAsync(query, profile, options) : await graph.SearchBySchemaAsync(query, profile);
            AssertMatch(result, mode, query);
            var rows = federated ? (await root.ExecuteFederatedSelectAsync(result.GeneratedSparql, options)).Result.Rows : (await graph.ExecuteSelectAsync(result.GeneratedSparql)).Rows;
            rows.Where(row => row.Values.ContainsKey("label")).Select(row => row.Values["label"]).Distinct().Order().ShouldBe(mode == 3 ? [] : new[] { "Alpha", "Zulu" });
            rows.Where(row => row.Values.ContainsKey("sourceLabel")).Select(row => row.Values["sourceLabel"]).Distinct().Order().ShouldBe(mode == 3 ? [] : new[] { "Alpha", "Zulu" });
            if (!federated)
            {
                AssertExpansion(result, mode);
            }
        }
    }
    private static string CreateGraphJson(int mode)
    {
        var labels = mode switch
        {
            0 => "\"s:name\": [\"Alpha\", \"Zulu\"], \"r:label\": \"Lower RDFS\", \"k:prefLabel\": \"Lower SKOS\"",
            1 => "\"r:label\": [\"Alpha\", \"Zulu\"], \"k:prefLabel\": \"Lower SKOS\"",
            2 => "\"k:prefLabel\": [\"Alpha\", \"Zulu\"]",
            _ => "\"s:url\": \"https://labels.example/unlabelled\""
        };
        return GraphTemplate.Replace("LABELS", labels, StringComparison.Ordinal);
    }

    private static void AssertMatch(KnowledgeGraphSchemaSearchResult result, int mode, string query)
    {
        result.Matches.Single().NodeId.ShouldBe(NodeId);
        var evidence = result.Matches.Single().Evidence.Single();
        evidence.SourceContexts.Select(source => source.SourceId).Distinct().ShouldBe([SourceId]);
        if (mode == 3)
        {
            evidence.SourceContexts.Single().SourceLabel.ShouldBeNull();
        }
        else
        {
            new[] { "Alpha", "Zulu" }.ShouldContain(evidence.SourceContexts.Single().SourceLabel!);
        }

        if (query == "rainfall")
        {
            evidence.RelatedNodeId.ShouldBe(TopicId);
            if (mode != 3)
            {
                new[] { "Alpha", "Zulu" }.ShouldContain(evidence.RelatedNodeLabel!);
            }
        }
    }

    private static void AssertExpansion(KnowledgeGraphSchemaSearchResult result, int mode)
    {
        result.RelatedMatches.Single().NodeId.ShouldBe(TopicId);
        if (mode != 3)
        {
            new[] { "Alpha", "Zulu" }.ShouldContain(result.RelatedMatches.Single().Label);
        }
    }

}
