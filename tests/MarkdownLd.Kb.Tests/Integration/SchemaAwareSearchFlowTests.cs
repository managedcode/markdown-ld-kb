using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaAwareSearchFlowTests
{
    private const string PrefixEx = "ex";
    private const string ExNamespace = "https://schema-search.example/vocab/";
    private const string CapabilityType = "ex:Capability";
    private const string SystemType = "ex:System";
    private const string DirectQuery = "restore cache";
    private const string RelationshipQuery = "stale shard checksum";
    private const string MissingQuery = "billing workflow";
    private const string CapabilityUri = "https://schema-search.example/tools/cache-recovery";
    private const string SystemUri = "https://schema-search.example/systems/blob-cache";
    private const string NextStepUri = "https://schema-search.example/tools/release-gate";
    private const string EvidencePredicateIntent = "https://schema-search.example/vocab/intent";
    private const string EvidencePredicateSymptom = "https://schema-search.example/vocab/symptom";
    private const string RelationshipPredicateRequires = "https://schema-search.example/vocab/requires";
    private const string NextStepPredicate = "https://schema-search.example/vocab/next";
    private const string CapabilityLabel = "Cache Recovery Runbook";
    private const string SystemLabel = "Blob Cache Ledger";
    private const string NextStepLabel = "Release Gate Checklist";
    private const string PolicyEndpoint = "https://schema-search.example/services/policy";
    private const string RunbookEndpoint = "https://schema-search.example/services/runbook";

    private const string SearchJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "skos": "http://www.w3.org/2004/02/skos/core#",
    "ex": "https://schema-search.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://schema-search.example/tools/cache-recovery",
      "@type": ["schema:Article", "ex:Capability"],
      "schema:name": "Cache Recovery Runbook",
      "ex:intent": "Restore cache after release incident",
      "ex:requires": { "@id": "https://schema-search.example/systems/blob-cache" },
      "ex:next": { "@id": "https://schema-search.example/tools/release-gate" }
    },
    {
      "@id": "https://schema-search.example/systems/blob-cache",
      "@type": "ex:System",
      "skos:prefLabel": "Blob Cache Ledger",
      "ex:symptom": "Stale shard checksum during corpus rebuild"
    },
    {
      "@id": "https://schema-search.example/tools/release-gate",
      "@type": "ex:Capability",
      "schema:name": "Release Gate Checklist",
      "ex:intent": "Approve rebuilt graph artifacts"
    }
  ]
}
""";

    private const string PolicyJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "ex": "https://schema-search.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://schema-search.example/policies/cache-recovery",
      "@type": "ex:Capability",
      "schema:name": "Cache Recovery Policy",
      "ex:intent": "restore cache safely after data drift"
    }
  ]
}
""";

    private const string RunbookJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "ex": "https://schema-search.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://schema-search.example/runbooks/cache-rebuild",
      "@type": "ex:Capability",
      "schema:name": "Cache Rebuild Runbook",
      "ex:intent": "restore cache safely after data drift"
    }
  ]
}
""";

    [Test]
    public async Task SchemaAwareSearchFindsCustomJsonLdPredicateWithExplainableSparqlEvidence()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);

        var legacy = await graph.SearchAsync(DirectQuery);
        legacy.Rows.ShouldBeEmpty();

        var search = await graph.SearchBySchemaAsync(DirectQuery, CreateProfile());

        search.GeneratedSparql.ShouldContain("SELECT");
        search.Matches.Count.ShouldBe(1);
        search.Matches.Single().NodeId.ShouldBe(CapabilityUri);
        search.Matches.Single().Label.ShouldBe(CapabilityLabel);
        search.Matches.Single().Evidence.Single().PredicateId.ShouldBe(EvidencePredicateIntent);
        search.Matches.Single().Evidence.Single().MatchedText.ShouldContain("Restore cache");
    }

    [Test]
    public async Task SchemaAwareSearchFindsSourceNodeThroughRelatedNodeEvidenceAndExpansion()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);

        var search = await graph.SearchBySchemaAsync(RelationshipQuery, CreateProfile());

        search.Matches.Count.ShouldBe(1);
        var match = search.Matches.Single();
        match.NodeId.ShouldBe(CapabilityUri);
        match.Evidence.Single().PredicateId.ShouldBe(EvidencePredicateSymptom);
        match.Evidence.Single().RelatedNodeId.ShouldBe(SystemUri);
        match.Evidence.Single().ViaPredicateId.ShouldBe(RelationshipPredicateRequires);
        search.RelatedMatches.Select(static item => item.NodeId).ShouldContain(SystemUri);
        search.NextStepMatches.Select(static item => item.NodeId).ShouldContain(NextStepUri);
        search.FocusedGraph.Nodes.Select(static node => node.Label).ShouldContain(SystemLabel);
        search.FocusedGraph.Nodes.Select(static node => node.Label).ShouldContain(NextStepLabel);
    }

    [Test]
    public async Task FocusedSearchCanUseSchemaAwareProfileForPrimaryMatches()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);

        var focused = await graph.SearchFocusedAsync(
            RelationshipQuery,
            new KnowledgeGraphFocusedSearchOptions
            {
                SchemaSearchProfile = CreateProfile(),
                MaxPrimaryResults = 1,
                MaxRelatedResults = 1,
                MaxNextStepResults = 1,
            });

        focused.PrimaryMatches.Single().NodeId.ShouldBe(CapabilityUri);
        focused.RelatedMatches.Single().NodeId.ShouldBe(SystemUri);
        focused.NextStepMatches.Single().NodeId.ShouldBe(NextStepUri);
    }

    [Test]
    public async Task SchemaAwareFederatedSearchRunsSameProfileThroughAllowlistedLocalServiceBindings()
    {
        var rootGraph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);
        var policyGraph = KnowledgeGraph.LoadJsonLd(PolicyJsonLd);
        var runbookGraph = KnowledgeGraph.LoadJsonLd(RunbookJsonLd);
        var profile = CreateProfile() with
        {
            FederatedServiceEndpoints = [new Uri(PolicyEndpoint), new Uri(RunbookEndpoint)],
        };
        var options = new FederatedSparqlExecutionOptions
        {
            AllowedServiceEndpoints = [new Uri(PolicyEndpoint), new Uri(RunbookEndpoint)],
            LocalServiceBindings =
            [
                new FederatedSparqlLocalServiceBinding(new Uri(PolicyEndpoint), policyGraph),
                new FederatedSparqlLocalServiceBinding(new Uri(RunbookEndpoint), runbookGraph),
            ],
        };

        var search = await rootGraph.SearchBySchemaFederatedAsync(DirectQuery, profile, options);

        search.GeneratedSparql.ShouldContain("SERVICE");
        search.ServiceEndpointSpecifiers.ShouldContain(PolicyEndpoint);
        search.ServiceEndpointSpecifiers.ShouldContain(RunbookEndpoint);
        search.Matches.Select(static match => match.Label).ShouldContain("Cache Recovery Policy");
        search.Matches.Select(static match => match.Label).ShouldContain("Cache Rebuild Runbook");
    }

    [Test]
    public async Task SchemaAwareSearchReturnsNoMatchesForProfileFilteredMisses()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);

        var search = await graph.SearchBySchemaAsync(MissingQuery, CreateProfile());

        search.Matches.ShouldBeEmpty();
        search.RelatedMatches.ShouldBeEmpty();
        search.NextStepMatches.ShouldBeEmpty();
        search.FocusedGraph.Nodes.ShouldBeEmpty();
    }

    [Test]
    public async Task SchemaAwareSearchRejectsUnknownPrefixesExplicitly()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);
        var profile = CreateProfile() with
        {
            TextPredicates = [new KnowledgeGraphSchemaTextPredicate("missing:intent")],
        };

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await graph.SearchBySchemaAsync(DirectQuery, profile));

        exception.Message.ShouldContain("missing");
    }

    private static KnowledgeGraphSchemaSearchProfile CreateProfile()
    {
        return new KnowledgeGraphSchemaSearchProfile
        {
            Prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PrefixEx] = ExNamespace,
            },
            TypeFilters = [CapabilityType],
            TextPredicates =
            [
                new KnowledgeGraphSchemaTextPredicate("schema:name", Weight: 1.2d),
                new KnowledgeGraphSchemaTextPredicate("ex:intent", Weight: 1.5d),
                new KnowledgeGraphSchemaTextPredicate("skos:prefLabel", Weight: 1.1d),
            ],
            RelationshipPredicates =
            [
                new KnowledgeGraphSchemaRelationshipPredicate(
                    "ex:requires",
                    ["ex:symptom", "skos:prefLabel"],
                    Weight: 0.9d),
            ],
            ExpansionPredicates =
            [
                new KnowledgeGraphSchemaExpansionPredicate(
                    "ex:requires",
                    KnowledgeGraphSchemaSearchRole.Related,
                    Score: 0.8d),
                new KnowledgeGraphSchemaExpansionPredicate(
                    "ex:next",
                    KnowledgeGraphSchemaSearchRole.NextStep,
                    Score: 0.7d),
            ],
            MaxResults = 5,
            MaxRelatedResults = 3,
            MaxNextStepResults = 3,
        };
    }
}
