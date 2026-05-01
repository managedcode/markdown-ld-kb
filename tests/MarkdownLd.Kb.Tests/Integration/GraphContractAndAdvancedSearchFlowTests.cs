using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class GraphContractAndAdvancedSearchFlowTests
{
    private const string PrefixEx = "ex";
    private const string ExNamespace = "https://graph-contract.example/vocab/";
    private const string CapabilityType = "ex:Capability";
    private const string SystemType = "ex:System";
    private const string TeamType = "ex:Team";
    private const string CategoryType = "ex:Category";
    private const string CapabilityUri = "https://graph-contract.example/tools/cache-recovery";
    private const string SystemUri = "https://graph-contract.example/systems/blob-cache";
    private const string TeamUri = "https://graph-contract.example/teams/cache-platform";
    private const string NextStepUri = "https://graph-contract.example/tools/release-gate";
    private const string RunbookCategoryUri = "https://graph-contract.example/categories/runbook";
    private const string IntentPredicate = "https://graph-contract.example/vocab/intent";
    private const string SymptomPredicate = "https://graph-contract.example/vocab/symptom";
    private const string RequiresPredicate = "https://graph-contract.example/vocab/requires";
    private const string SupportsPredicate = "https://graph-contract.example/vocab/supports";
    private const string OwnerPredicate = "https://graph-contract.example/vocab/owner";
    private const string CategoryPredicate = "https://graph-contract.example/vocab/category";
    private const string CapabilityLabel = "Cache Recovery Runbook";

    private const string SearchJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "skos": "http://www.w3.org/2004/02/skos/core#",
    "ex": "https://graph-contract.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://graph-contract.example/tools/cache-recovery",
      "@type": ["ex:Capability"],
      "schema:name": "Cache Recovery Runbook",
      "ex:intent": "Restore cache after release incident",
      "ex:requires": { "@id": "https://graph-contract.example/systems/blob-cache" },
      "ex:next": { "@id": "https://graph-contract.example/tools/release-gate" },
      "ex:category": { "@id": "https://graph-contract.example/categories/runbook" }
    },
    {
      "@id": "https://graph-contract.example/systems/blob-cache",
      "@type": "ex:System",
      "skos:prefLabel": "Blob Cache Ledger",
      "ex:symptom": "Stale shard checksum during corpus rebuild",
      "ex:owner": { "@id": "https://graph-contract.example/teams/cache-platform" },
      "ex:supports": { "@id": "https://graph-contract.example/tools/cache-recovery" }
    },
    {
      "@id": "https://graph-contract.example/teams/cache-platform",
      "@type": "ex:Team",
      "skos:prefLabel": "Cache Platform Team"
    },
    {
      "@id": "https://graph-contract.example/categories/runbook",
      "@type": "ex:Category",
      "skos:prefLabel": "Runbook"
    },
    {
      "@id": "https://graph-contract.example/tools/release-gate",
      "@type": "ex:Capability",
      "schema:name": "Release Gate Checklist",
      "ex:intent": "Approve graph artifacts"
    }
  ]
}
""";

    private const string ContractMarkdown = """
---
title: Cache Recovery Runbook
rdf_prefixes:
  ex: https://graph-contract.example/vocab/
rdf_types:
  - ex:Capability
rdf_properties:
  ex:intent: Restore cache after release incident
  ex:category:
    id: https://graph-contract.example/categories/runbook
---
# Cache Recovery Runbook

Restore cache after release incident.
""";

    [Test]
    public async Task PipelineBuildProfileReturnsSearchReadyGraphContract()
    {
        var profile = CreateContractProfile();
        var buildProfile = new KnowledgeGraphBuildProfile
        {
            Name = "capability-workflow",
            BuildOptions = new KnowledgeGraphBuildOptions(),
            SearchProfile = profile,
        };
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            BuildProfile = buildProfile,
        });

        var result = await pipeline.BuildFromMarkdownAsync(ContractMarkdown, path: "runbooks/cache.md");

        result.Contract.Name.ShouldBe("capability-workflow");
        result.Contract.SearchProfile.ShouldBe(profile);
        result.Contract.Validation.IsValid.ShouldBeTrue();
        result.Contract.Schema.RdfTypes.Select(static item => item.CompactName).ShouldContain(CapabilityType);
        result.Contract.Schema.Predicates.Select(static item => item.CompactName).ShouldContain("ex:intent");

        var search = await result.Graph.SearchBySchemaAsync("restore cache", result.Contract.SearchProfile);
        search.Matches.Single().Label.ShouldBe(CapabilityLabel);
    }

    [Test]
    public void GraphSchemaIntrospectionAndProfileValidationDescribeTheRealRdfShape()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);
        var profile = CreateProfile();

        var schema = graph.DescribeSchema(profile.Prefixes);
        var validation = graph.ValidateSchemaSearchProfile(profile);

        schema.RdfTypes.Select(static item => item.CompactName).ShouldContain(CapabilityType);
        schema.RdfTypes.Select(static item => item.CompactName).ShouldContain(SystemType);
        schema.RdfTypes.Select(static item => item.CompactName).ShouldContain(TeamType);
        schema.RdfTypes.Select(static item => item.CompactName).ShouldContain(CategoryType);
        schema.LiteralPredicates.Select(static item => item.CompactName).ShouldContain("ex:intent");
        schema.LiteralPredicates.Select(static item => item.CompactName).ShouldContain("ex:symptom");
        schema.ResourcePredicates.Select(static item => item.CompactName).ShouldContain("ex:requires");
        validation.IsValid.ShouldBeTrue();

        var invalidProfile = profile with
        {
            TypeFilters = ["ex:MissingType"],
            TextPredicates = [new KnowledgeGraphSchemaTextPredicate("ex:unknownLiteral")],
        };

        var invalid = graph.ValidateSchemaSearchProfile(invalidProfile);

        invalid.IsValid.ShouldBeFalse();
        invalid.Issues.Select(static issue => issue.Term).ShouldContain("ex:MissingType");
        invalid.Issues.Select(static issue => issue.Term).ShouldContain("ex:unknownLiteral");
    }

    [Test]
    public async Task SearchBySchemaSupportsAllTermsModeWithoutFallingBackToKeywordSearch()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);
        var exactPhraseProfile = CreateProfile() with
        {
            RelationshipPredicates = [],
            TermMode = KnowledgeGraphSchemaSearchTermMode.ExactPhrase,
        };
        var allTermsProfile = exactPhraseProfile with
        {
            TermMode = KnowledgeGraphSchemaSearchTermMode.AllTerms,
        };

        var exact = await graph.SearchBySchemaAsync("cache restore", exactPhraseProfile);
        var allTerms = await graph.SearchBySchemaAsync("cache restore", allTermsProfile);

        exact.Matches.ShouldBeEmpty();
        allTerms.Matches.Single().NodeId.ShouldBe(CapabilityUri);
        allTerms.GeneratedSparql.ShouldContain("CONTAINS");
    }

    [Test]
    public async Task SearchBySchemaSupportsInboundRelationshipsPredicatePathsAndFacets()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);
        var inboundProfile = CreateProfile() with
        {
            TextPredicates = [new KnowledgeGraphSchemaTextPredicate("schema:name")],
            RelationshipPredicates =
            [
                new KnowledgeGraphSchemaRelationshipPredicate(
                    "ex:supports",
                    ["ex:symptom"],
                    Weight: 0.9d)
                {
                    Direction = KnowledgeGraphSchemaRelationshipDirection.Inbound,
                },
            ],
            FacetFilters =
            [
                new KnowledgeGraphSchemaFacetFilter("ex:category", RunbookCategoryUri),
            ],
        };
        var pathProfile = CreateProfile() with
        {
            TextPredicates = [new KnowledgeGraphSchemaTextPredicate("schema:name")],
            RelationshipPredicates =
            [
                new KnowledgeGraphSchemaRelationshipPredicate(
                    "ex:requires",
                    ["skos:prefLabel"],
                    Weight: 0.9d)
                {
                    PredicatePath = ["ex:requires", "ex:owner"],
                },
            ],
            FacetFilters =
            [
                new KnowledgeGraphSchemaFacetFilter("ex:category", RunbookCategoryUri),
            ],
        };

        var inbound = await graph.SearchBySchemaAsync("stale shard checksum", inboundProfile);
        var path = await graph.SearchBySchemaAsync("Cache Platform Team", pathProfile);

        inbound.Matches.Single().NodeId.ShouldBe(CapabilityUri);
        inbound.Matches.Single().Evidence.Single().ViaPredicateId.ShouldBe(SupportsPredicate);
        path.Matches.Single().NodeId.ShouldBe(CapabilityUri);
        var pathPredicate = path.Matches.Single().Evidence.Single().ViaPredicateId;
        pathPredicate.ShouldNotBeNull();
        pathPredicate.ShouldContain(RequiresPredicate);
        pathPredicate.ShouldContain(OwnerPredicate);
        path.GeneratedSparql.ShouldContain("/");
    }

    [Test]
    public async Task FocusedGraphSnapshotExportsToJsonLdTurtleMermaidAndDot()
    {
        var graph = KnowledgeGraph.LoadJsonLd(SearchJsonLd);
        var search = await graph.SearchBySchemaAsync("stale shard checksum", CreateProfile());

        var jsonLd = search.FocusedGraph.SerializeJsonLd();
        var turtle = search.FocusedGraph.SerializeTurtle();
        var mermaid = search.FocusedGraph.SerializeMermaidFlowchart();
        var dot = search.FocusedGraph.SerializeDotGraph();
        var reloaded = KnowledgeGraph.LoadJsonLd(jsonLd);
        var ask = "ASK { <" + CapabilityUri + "> <" + RequiresPredicate + "> <" + SystemUri + "> . }";
        var hasFocusedEdge = await reloaded.ExecuteAskAsync(ask);

        hasFocusedEdge.ShouldBeTrue();
        turtle.ShouldContain(CapabilityUri);
        mermaid.ShouldContain(CapabilityLabel);
        dot.ShouldContain(CapabilityLabel);
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

    private static KnowledgeGraphSchemaSearchProfile CreateContractProfile()
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
            ],
            RelationshipPredicates = [],
            ExpansionPredicates = [],
            FacetFilters =
            [
                new KnowledgeGraphSchemaFacetFilter("ex:category", RunbookCategoryUri),
            ],
            MaxResults = 5,
        };
    }
}
