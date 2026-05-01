using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class GraphProductionPipelineFlowTests
{
    private const string PrefixEx = "ex";
    private const string ExNamespace = "https://graph-production.example/vocab/";
    private const string CapabilityType = "ex:Capability";
    private const string CapabilityUri = "https://graph-production.example/tools/cache-recovery";
    private const string SystemUri = "https://graph-production.example/systems/blob-cache";
    private const string SourceUri = "https://graph-production.example/docs/cache-recovery.md";
    private const string SystemSourceUri = "https://graph-production.example/docs/blob-cache.md";
    private const string ReleaseDocumentUri = "https://graph-production.example/docs/release/";
    private const string CapabilityLabel = "Cache Recovery Capability";
    private const string IntentPredicate = "https://graph-production.example/vocab/intent";
    private const string DependsOnPredicate = "https://graph-production.example/vocab/dependsOn";
    private const string SearchTerm = "restore cache";

    private const string ValidGraphJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "prov": "http://www.w3.org/ns/prov#",
    "ex": "https://graph-production.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://graph-production.example/tools/cache-recovery",
      "@type": "ex:Capability",
      "schema:name": "Cache Recovery Capability",
      "ex:intent": "Restore cache after failed release",
      "ex:requires": { "@id": "https://graph-production.example/systems/blob-cache" },
      "prov:wasDerivedFrom": { "@id": "https://graph-production.example/docs/cache-recovery.md" }
    },
    {
      "@id": "https://graph-production.example/systems/blob-cache",
      "@type": "ex:System",
      "schema:name": "Blob cache",
      "ex:symptom": "Stale shard checksum",
      "prov:wasDerivedFrom": { "@id": "https://graph-production.example/docs/blob-cache.md" }
    },
    {
      "@id": "https://graph-production.example/docs/cache-recovery.md",
      "@type": "schema:Article",
      "schema:name": "Cache recovery source document"
    },
    {
      "@id": "https://graph-production.example/docs/blob-cache.md",
      "@type": "schema:Article",
      "schema:name": "System source document"
    }
  ]
}
""";

    private const string InvalidGraphJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "ex": "https://graph-production.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://graph-production.example/tools/cache-recovery",
      "@type": "ex:Capability",
      "schema:name": "Cache Recovery Capability"
    }
  ]
}
""";

    private const string OldDiffGraphJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "ex": "https://graph-production.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://graph-production.example/tools/cache-recovery",
      "@type": "ex:Capability",
      "schema:name": "Cache Recovery Capability",
      "ex:intent": "Restore cache after failed release",
      "ex:dependsOn": { "@id": "https://graph-production.example/systems/blob-cache" }
    }
  ]
}
""";

    private const string NewDiffGraphJsonLd = """
{
  "@context": {
    "schema": "https://schema.org/",
    "ex": "https://graph-production.example/vocab/"
  },
  "@graph": [
    {
      "@id": "https://graph-production.example/tools/cache-recovery",
      "@type": "ex:Capability",
      "schema:name": "Cache Recovery Capability",
      "ex:intent": "Restore cache with ledger verification",
      "ex:dependsOn": { "@id": "https://graph-production.example/systems/ledger" }
    }
  ]
}
""";

    private const string DocumentationMarkdown = """
---
title: Cache Recovery Documentation
summary: Restore cache using graph-backed evidence and source links.
keywords:
  - restore cache
---
# Cache Recovery Documentation

Restore cache using graph-backed evidence and source links.
""";

    private const string BaselineIncrementalMarkdown = """
---
title: Cache Recovery
summary: Restore cache after failed release.
keywords:
  - restore cache
---
# Cache Recovery

Restore cache after failed release.
""";

    private const string ChangedIncrementalMarkdown = """
---
title: Cache Recovery
summary: Restore cache with ledger verification.
keywords:
  - restore cache
---
# Cache Recovery

Restore cache with ledger verification.
""";

    private const string UnchangedIncrementalMarkdown = """
---
title: Release Gate
summary: Approve generated graph artifacts.
keywords:
  - graph artifacts
---
# Release Gate

Approve generated graph artifacts.
""";

    [Test]
    public async Task ContractArtifactsRoundTripThroughJsonAndYamlPreserveSearchProfile()
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            BuildProfile = new KnowledgeGraphBuildProfile
            {
                Name = "production-contract",
                SearchProfile = CreateCapabilityProfile(),
            },
        });
        var result = await pipeline.BuildAsync(
        [
            new MarkdownSourceDocument("docs/cache.md", CreateCapabilityMarkdown()),
        ]);

        var json = result.Contract.SerializeJson();
        var yaml = result.Contract.SerializeYaml();
        var fromJson = KnowledgeGraphContract.LoadJson(json);
        var fromYaml = KnowledgeGraphContract.LoadYaml(yaml);

        fromJson.Name.ShouldBe(result.Contract.Name);
        fromYaml.Name.ShouldBe(result.Contract.Name);
        fromJson.SearchProfile.TextPredicates.Select(static item => item.Predicate).ShouldContain("ex:intent");
        fromYaml.SearchProfile.TypeFilters.ShouldContain(CapabilityType);
        fromJson.Validation.IsValid.ShouldBeTrue();
        fromYaml.Validation.IsValid.ShouldBeTrue();
    }

    [Test]
    public void GeneratedShaclFromContractValidatesRequiredProfilePredicates()
    {
        var graph = KnowledgeGraph.LoadJsonLd(ValidGraphJsonLd);
        var contract = graph.CreateContract("capability-contract", CreateCapabilityProfile());
        var shapes = contract.GenerateShacl();
        var valid = graph.ValidateShacl(shapes);
        var invalid = KnowledgeGraph.LoadJsonLd(InvalidGraphJsonLd).ValidateShacl(shapes);

        shapes.ShouldContain("sh:targetClass ex:Capability");
        shapes.ShouldContain("sh:path ex:intent");
        valid.Conforms.ShouldBeTrue();
        invalid.Conforms.ShouldBeFalse();
        invalid.Results.Select(static issue => issue.ResultPath).ShouldContain("https://graph-production.example/vocab/intent");
    }

    [Test]
    public async Task SearchEvidenceIncludesSourceContextAndStructuredExplainPlan()
    {
        var graph = KnowledgeGraph.LoadJsonLd(ValidGraphJsonLd);

        var search = await graph.SearchBySchemaAsync(SearchTerm, CreateCapabilityProfile());

        var match = search.Matches.Single();
        match.NodeId.ShouldBe(CapabilityUri);
        match.Evidence.Single().SourceContexts.Single().SourceId.ShouldBe(SourceUri);
        match.Evidence.Single().SourceContexts.Single().SourceLabel.ShouldBe("Cache recovery source document");
        search.Explain.Query.ShouldBe(SearchTerm);
        search.Explain.TermMode.ShouldBe(KnowledgeGraphSchemaSearchTermMode.AllTerms);
        search.Explain.TypeFilters.ShouldContain(CapabilityType);
        search.Explain.TextPredicates.Select(static item => item.Predicate).ShouldContain("ex:intent");
        search.Explain.GeneratedSparql.ShouldContain("prov:wasDerivedFrom");
    }

    [Test]
    public async Task RelationshipEvidenceIncludesRelatedNodeSourceContext()
    {
        var graph = KnowledgeGraph.LoadJsonLd(ValidGraphJsonLd);
        var profile = CreateCapabilityProfile() with
        {
            RelationshipPredicates =
            [
                new KnowledgeGraphSchemaRelationshipPredicate("ex:requires", ["ex:symptom"]),
            ],
        };

        var search = await graph.SearchBySchemaAsync("stale shard", profile);

        var evidence = search.Matches.Single().Evidence.Single();
        evidence.Kind.ShouldBe(KnowledgeGraphSchemaSearchEvidenceKind.Relationship);
        evidence.RelatedNodeId.ShouldBe(SystemUri);
        evidence.SourceContexts.Select(static source => source.SourceId).ShouldContain(SourceUri);
        evidence.SourceContexts.Select(static source => source.SourceId).ShouldContain(SystemSourceUri);
        evidence.SourceContexts.Single(source => source.SourceId == SystemSourceUri)
            .SourceLabel.ShouldBe("System source document");
    }

    [Test]
    public void GraphDiffReportsAddedRemovedAndChangedEdges()
    {
        var oldGraph = KnowledgeGraph.LoadJsonLd(OldDiffGraphJsonLd);
        var newGraph = KnowledgeGraph.LoadJsonLd(NewDiffGraphJsonLd);

        var diff = oldGraph.Diff(newGraph);

        diff.AddedEdges.ShouldContain(edge => edge.PredicateId == DependsOnPredicate &&
                                             edge.ObjectId == "https://graph-production.example/systems/ledger");
        diff.RemovedEdges.ShouldContain(edge => edge.PredicateId == DependsOnPredicate &&
                                               edge.ObjectId == "https://graph-production.example/systems/blob-cache");
        diff.ChangedLiteralEdges.Single().PredicateId.ShouldBe(IntentPredicate);
        diff.ChangedLiteralEdges.Single().OldValue.ShouldBe("Restore cache after failed release");
        diff.ChangedLiteralEdges.Single().NewValue.ShouldBe("Restore cache with ledger verification");
    }

    [Test]
    public async Task BuildProfilePresetSearchesDocumentationWithoutCustomProfile()
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            BuildProfile = KnowledgeGraphBuildProfiles.Documentation,
        });
        var result = await pipeline.BuildAsync(
        [
            new MarkdownSourceDocument("docs/cache.md", DocumentationMarkdown),
        ]);

        var search = await result.Graph.SearchBySchemaAsync(SearchTerm, result.Contract.SearchProfile);

        result.Contract.Name.ShouldBe(KnowledgeGraphBuildProfiles.Documentation.Name);
        result.Contract.Validation.IsValid.ShouldBeTrue();
        search.Matches.Single().Label.ShouldBe("Cache Recovery Documentation");
        search.Explain.TextPredicates.Select(static item => item.Predicate).ShouldContain("schema:keywords");
    }

    [Test]
    public async Task IncrementalBuildReportsChangedSourcesAndGraphDiff()
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            BaseUri = new Uri("https://graph-production.example/"),
            ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            BuildProfile = KnowledgeGraphBuildProfiles.Documentation,
        });
        var baseline = await pipeline.BuildIncrementalAsync(
        [
            new MarkdownSourceDocument("docs/cache.md", BaselineIncrementalMarkdown),
            new MarkdownSourceDocument("docs/release.md", UnchangedIncrementalMarkdown),
        ]);
        var next = await pipeline.BuildIncrementalAsync(
        [
            new MarkdownSourceDocument("docs/cache.md", ChangedIncrementalMarkdown),
            new MarkdownSourceDocument("docs/release.md", UnchangedIncrementalMarkdown),
        ], baseline.Manifest, baseline.BuildResult.Graph);

        baseline.ChangedPaths.ShouldBe(["docs/cache.md", "docs/release.md"], ignoreOrder: true);
        next.ChangedPaths.ShouldBe(["docs/cache.md"]);
        next.RemovedPaths.ShouldBeEmpty();
        next.Diff.ChangedLiteralEdges.ShouldContain(edge => edge.PredicateId == "https://schema.org/description");
        next.BuildResult.Contract.Validation.IsValid.ShouldBeTrue();

        var manifestJson = next.Manifest.SerializeJson();
        var loadedManifest = KnowledgeGraphSourceManifest.LoadJson(manifestJson);
        var manifestPath = Path.Combine(Path.GetTempPath(), "markdown-ld-kb-manifest-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await next.Manifest.SaveJsonToFileAsync(manifestPath);
            var loadedFromFile = await KnowledgeGraphSourceManifest.LoadJsonFromFileAsync(manifestPath);

            loadedManifest.Entries.ShouldBe(next.Manifest.Entries);
            loadedFromFile.Entries.ShouldBe(next.Manifest.Entries);
        }
        finally
        {
            File.Delete(manifestPath);
        }

        var removed = await pipeline.BuildIncrementalAsync(
        [
            new MarkdownSourceDocument("docs/cache.md", ChangedIncrementalMarkdown),
        ], next.Manifest, next.BuildResult.Graph);

        removed.ChangedPaths.ShouldBeEmpty();
        removed.RemovedPaths.ShouldBe(["docs/release.md"]);
        removed.Diff.RemovedNodes.ShouldContain(node => node.Id == ReleaseDocumentUri);
    }

    private static KnowledgeGraphSchemaSearchProfile CreateCapabilityProfile()
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
            TermMode = KnowledgeGraphSchemaSearchTermMode.AllTerms,
        };
    }

    private static string CreateCapabilityMarkdown()
    {
        return """
---
title: Cache Recovery Capability
rdf_prefixes:
  ex: https://graph-production.example/vocab/
rdf_types:
  - ex:Capability
rdf_properties:
  ex:intent: Restore cache after failed release
---
# Cache Recovery Capability

Restore cache after failed release.
""";
    }
}
