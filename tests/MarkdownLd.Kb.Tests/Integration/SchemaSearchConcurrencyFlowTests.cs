using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaSearchConcurrencyFlowTests
{
    private const string ToolId = "https://concurrent-search.example/tools/inventory";
    private const string EvidenceId = "https://concurrent-search.example/formats/vendor";
    private const string DescriptionPredicate = "https://schema.org/description";
    private const string AboutPredicate = "https://schema.org/about";
    private const string DirectQuery = "workbook rows";
    private const string RelationshipQuery = "format checksum";
    private const string ServiceEndpoint = "https://concurrent-search.example/services/inventory";
    private const string EmptyJsonLd = """{"@graph": []}""";
    private const string SearchJsonLd = """
        {
          "@context": { "schema": "https://schema.org/" },
          "@graph": [
            {
              "@id": "https://concurrent-search.example/tools/inventory",
              "@type": "schema:SoftwareApplication",
              "schema:name": "Inventory parser",
              "schema:description": "Parse workbook rows",
              "schema:about": { "@id": "https://concurrent-search.example/formats/vendor" }
            },
            {
              "@id": "https://concurrent-search.example/formats/vendor",
              "@type": "schema:DefinedTerm",
              "schema:name": "Vendor format",
              "schema:description": "Verify vendor format checksum"
            }
          ]
        }
        """;

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Concurrent_searches_keep_direct_and_related_evidence_independent(bool federated)
    {
        using var localGraph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);
        using var rootGraph = KnowledgeGraph.LoadJsonLd(EmptyJsonLd);
        var endpoint = new Uri(ServiceEndpoint);
        var profile = CreateProfile() with
        {
            FederatedServiceEndpoints = federated ? [endpoint] : [],
        };
        var options = new FederatedSparqlExecutionOptions
        {
            AllowedServiceEndpoints = [endpoint],
            LocalServiceBindings = [new FederatedSparqlLocalServiceBinding(endpoint, localGraph)],
        };

        var queries = new[] { DirectQuery, RelationshipQuery };
        var results = await Task.WhenAll(queries.Select(query => federated
            ? rootGraph.SearchBySchemaFederatedAsync(query, profile, options)
            : localGraph.SearchBySchemaAsync(query, profile)));

        var direct = results[0].Matches.Single();
        direct.NodeId.ShouldBe(ToolId);
        direct.Evidence.Single().PredicateId.ShouldBe(DescriptionPredicate);
        direct.Evidence.Single().RelatedNodeId.ShouldBeNull();
        direct.Evidence.Single().MatchedText.ShouldBe("Parse workbook rows");

        var related = results[1].Matches.Single();
        related.NodeId.ShouldBe(ToolId);
        related.Evidence.Single().PredicateId.ShouldBe(DescriptionPredicate);
        related.Evidence.Single().RelatedNodeId.ShouldBe(EvidenceId);
        related.Evidence.Single().ViaPredicateId.ShouldBe(AboutPredicate);
        related.Evidence.Single().MatchedText.ShouldBe("Verify vendor format checksum");
        AssertExpansionContract(results[1], federated);
    }

    private static void AssertExpansionContract(KnowledgeGraphSchemaSearchResult search, bool federated)
    {
        if (federated)
        {
            search.RelatedMatches.ShouldBeEmpty();
            search.NextStepMatches.ShouldBeEmpty();
            search.FocusedGraph.Nodes.ShouldBeEmpty();
            search.GeneratedExpansionSparql.ShouldBeNull();
            search.ServiceEndpointSpecifiers.ShouldContain(ServiceEndpoint);
            return;
        }

        search.RelatedMatches.Single().NodeId.ShouldBe(EvidenceId);
        search.FocusedGraph.Nodes.Select(node => node.Label).ShouldContain("Vendor format");
    }

    private static KnowledgeGraphSchemaSearchProfile CreateProfile() => new()
    {
        TypeFilters = ["schema:SoftwareApplication"],
        TextPredicates = [new KnowledgeGraphSchemaTextPredicate("schema:description")],
        RelationshipPredicates =
        [
            new KnowledgeGraphSchemaRelationshipPredicate("schema:about", ["schema:description"]),
        ],
        ExpansionPredicates =
        [
            new KnowledgeGraphSchemaExpansionPredicate("schema:about", KnowledgeGraphSchemaSearchRole.Related, Score: 1d),
        ],
    };
}
