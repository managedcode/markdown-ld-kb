using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class GraphNormalizationFlowTests
{
    private static readonly Uri BaseUri = new("https://normalization.example/");

    private const string NodeA = "https://normalization.example/nodes/A";
    private const string NodeALower = "https://normalization.example/nodes/a";
    private const string NodeB = "https://normalization.example/nodes/B";
    private const string NodeC = "https://normalization.example/nodes/C";
    private const string SourceOne = "https://normalization.example/sources/one";
    private const string SourceTwo = "https://normalization.example/sources/two";
    private const string RelatedTo = "kb:relatedTo";
    private const string NextStep = "kb:nextStep";
    private const string KbRelatedToUri = "urn:managedcode:markdown-ld-kb:vocab:relatedTo";
    private const string KbNextStepUri = "urn:managedcode:markdown-ld-kb:vocab:nextStep";
    private const string SemanticDocumentPath = "workflows/operator-graph.md";
    private const string SemanticDocumentUri = "https://normalization.example/workflows/operator-graph/";
    private const string SemanticTargetUri = "https://normalization.example/workflows/review/";

    [Test]
    public async Task Build_normalizes_duplicates_self_loops_and_workflow_cycles_with_warnings()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync(
            Array.Empty<MarkdownSourceDocument>(),
            CreateDirtyGraphOptions());

        result.Facts.Assertions.Count.ShouldBe(3);
        result.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved);
        result.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.SymmetricDuplicateEdgeRemoved);
        result.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.SelfLoopRemoved);
        result.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.CycleEdgeRemoved);
        result.Diagnostics.ShouldContain(static diagnostic => diagnostic.StartsWith("Graph normalization warning:", StringComparison.Ordinal));

        var duplicateWinner = result.Facts.Assertions.Single(assertion =>
            assertion.SubjectId == NodeA &&
            assertion.Predicate == NextStep &&
            assertion.ObjectId == NodeB);
        duplicateWinner.Confidence.ShouldBe(0.9d);
        duplicateWinner.Sources.ShouldContain(SourceOne);
        duplicateWinner.Sources.ShouldContain(SourceTwo);

        var topologyIsClean = await result.Graph.ExecuteAskAsync($$"""
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <{{NodeA}}> kb:relatedTo <{{NodeB}}> ;
    kb:nextStep <{{NodeB}}> .
  <{{NodeB}}> kb:nextStep <{{NodeC}}> .
  FILTER NOT EXISTS { <{{NodeB}}> kb:relatedTo <{{NodeA}}> }
  FILTER NOT EXISTS { <{{NodeA}}> kb:nextStep <{{NodeA}}> }
  FILTER NOT EXISTS { <{{NodeC}}> kb:nextStep <{{NodeA}}> }
}
""");
        topologyIsClean.ShouldBeTrue();
        result.Graph.FindCycles().ShouldBeEmpty();
    }

    [Test]
    public async Task Canonical_edge_identity_preserves_case_sensitive_uri_paths()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync(
            Array.Empty<MarkdownSourceDocument>(),
            new KnowledgeGraphBuildOptions
            {
                Edges =
                [
                    CreateEdge(NodeA, RelatedTo, NodeC, 0.8d, SourceOne),
                    CreateEdge(NodeALower, RelatedTo, NodeC, 0.7d, SourceTwo),
                ],
            });

        result.Facts.Assertions.Count.ShouldBe(2);
        result.Normalization.Warnings.ShouldBeEmpty();
    }

    [Test]
    public void Cycle_search_returns_bounded_strongly_connected_components_for_selected_predicates()
    {
        using var graph = KnowledgeGraph.FromSnapshot(new KnowledgeGraphSnapshot(
            [],
            [
                CreateSnapshotEdge(NodeA, KbNextStepUri, NextStep, NodeB),
                CreateSnapshotEdge(NodeB, KbNextStepUri, NextStep, NodeC),
                CreateSnapshotEdge(NodeC, KbNextStepUri, NextStep, NodeA),
                CreateSnapshotEdge(NodeA, KbRelatedToUri, RelatedTo, NodeC),
            ]));

        var cycles = graph.FindCycles();

        cycles.Count.ShouldBe(1);
        cycles.Single().NodeIds.ShouldBe([NodeA, NodeB, NodeC]);
        cycles.Single().Edges.Count.ShouldBe(3);
        cycles.Single().Edges.ShouldAllBe(static edge => edge.PredicateId == KbNextStepUri);
    }

    [Test]
    public async Task Semantic_snapshot_excludes_tiktoken_retrieval_internals_and_keeps_navigation_ids()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(SemanticDocumentPath, SemanticMarkdown),
        ]);

        var complete = result.Graph.ToCompleteSnapshot();
        var semantic = result.Graph.ToSnapshot();

        complete.Nodes.ShouldContain(static node => node.Id.Contains("/token-section/", StringComparison.Ordinal));
        complete.Nodes.ShouldContain(static node => node.Id.Contains("/token-segment/", StringComparison.Ordinal));
        complete.Nodes.ShouldContain(static node => node.Id.Contains("/token-topic/", StringComparison.Ordinal));
        semantic.Nodes.ShouldNotContain(static node => node.Id.Contains("/token-section/", StringComparison.Ordinal));
        semantic.Nodes.ShouldNotContain(static node => node.Id.Contains("/token-segment/", StringComparison.Ordinal));
        semantic.Nodes.ShouldNotContain(static node => node.Id.Contains("/token-topic/", StringComparison.Ordinal));
        semantic.Edges.ShouldContain(edge =>
            edge.SubjectId == SemanticDocumentUri &&
            edge.PredicateId == KbRelatedToUri &&
            edge.ObjectId == SemanticTargetUri);
        semantic.Edges.Select(static edge => (edge.SubjectId, edge.PredicateId, edge.ObjectId))
            .Distinct()
            .Count()
            .ShouldBe(semantic.Edges.Count);
        result.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved);
        result.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved);
        result.Graph.CanSearchByTokenDistance.ShouldBeTrue();
        (await result.Graph.SearchByTokenDistanceAsync("operator workflow validation")).ShouldNotBeEmpty();
    }

    [Test]
    public void Normalizer_rejects_malformed_incomplete_unsupported_and_negative_confidence_edges()
    {
        var normalizer = new KnowledgeGraphNormalizer(BaseUri);
        var result = normalizer.Normalize(new KnowledgeExtractionResult
        {
            Entities =
            [
                new KnowledgeEntityFact
                {
                    Id = null,
                    Label = null!,
                    SameAs = [null!],
                },
            ],
            Assertions =
            [
                CreateFact(string.Empty, NextStep, NodeB, 0.8d, SourceOne),
                CreateFact(NodeA, "unsupported predicate", NodeB, 0.8d, SourceOne),
                CreateFact("https://[invalid", NextStep, NodeB, 0.8d, SourceOne),
                CreateFact(NodeA, NextStep, NodeB, -0.1d, SourceOne),
                new KnowledgeAssertionFact
                {
                    SubjectId = null!,
                    Predicate = null!,
                    ObjectId = null!,
                },
            ],
        });

        result.Facts.Entities.ShouldBeEmpty();
        result.Facts.Assertions.ShouldBeEmpty();
        result.Report.Warnings.Count(static warning =>
            warning.Code == KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved).ShouldBe(5);
        result.Report.Warnings.Count(static warning =>
            warning.Code == KnowledgeGraphNormalizationWarningCode.InvalidNodeRemoved).ShouldBe(1);
    }

    [Test]
    public async Task Direct_builder_defensively_normalizes_before_reifying_assertions()
    {
        var builder = new KnowledgeGraphBuilder(BaseUri);
        using var graph = builder.Build(
            [],
            new KnowledgeExtractionResult
            {
                Entities =
                [
                    new KnowledgeEntityFact { Label = null! },
                ],
                Assertions =
                [
                    CreateFact(NodeA, NextStep, NodeB, 0.9d, SourceOne),
                    CreateFact(NodeA, NextStep, NodeB, 0.7d, SourceTwo),
                    CreateFact(NodeB, NextStep, NodeA, 0.1d, SourceTwo),
                ],
            },
            new KnowledgeGraphBuildOptions { IncludeAssertionReification = true });

        graph.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved);
        graph.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.CycleEdgeRemoved);
        graph.Normalization.Warnings.Select(static warning => warning.Code)
            .ShouldContain(KnowledgeGraphNormalizationWarningCode.InvalidNodeRemoved);
        var statements = await graph.ExecuteSelectAsync($$"""
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
SELECT ?statement WHERE {
  ?statement a rdf:Statement ;
    rdf:subject <{{NodeA}}> ;
    rdf:predicate kb:nextStep ;
    rdf:object <{{NodeB}}> .
}
""");
        statements.Rows.Count.ShouldBe(1);
        graph.FindCycles().ShouldBeEmpty();
    }

    [Test]
    public void Normalization_is_deterministic_across_input_permutations()
    {
        var assertions = new[]
        {
            CreateFact(NodeA, NextStep, NodeB, 0.9d, SourceTwo),
            CreateFact(NodeA, NextStep, NodeB, 0.4d, SourceOne),
            CreateFact(NodeB, NextStep, NodeC, 0.8d, SourceOne),
            CreateFact(NodeC, NextStep, NodeA, 0.1d, SourceTwo),
        };
        var normalizer = new KnowledgeGraphNormalizer(BaseUri);

        var forward = normalizer.Normalize(new KnowledgeExtractionResult { Assertions = assertions.ToList() });
        var reverse = normalizer.Normalize(new KnowledgeExtractionResult { Assertions = assertions.Reverse().ToList() });

        ProjectAssertions(forward.Facts.Assertions).ShouldBe(ProjectAssertions(reverse.Facts.Assertions));
        forward.Report.Warnings.Select(static warning => warning.Message)
            .ShouldBe(reverse.Report.Warnings.Select(static warning => warning.Message));
    }

    [Test]
    public void Cycle_search_can_include_symmetric_relationships_without_changing_default_scope()
    {
        using var graph = KnowledgeGraph.FromSnapshot(new KnowledgeGraphSnapshot(
            [],
            [
                CreateSnapshotEdge(NodeA, KbRelatedToUri, RelatedTo, NodeB),
                CreateSnapshotEdge(NodeB, KbRelatedToUri, RelatedTo, NodeA),
            ]));

        graph.FindCycles().ShouldBeEmpty();
        var cycles = graph.FindCycles(new KnowledgeGraphCycleSearchOptions
        {
            PredicateIds = [KbRelatedToUri],
            MaxComponents = 1,
        });
        cycles.Single().NodeIds.ShouldBe([NodeA, NodeB]);
    }

    [Test]
    public void Entity_same_as_relationships_remove_duplicates_invalid_targets_and_self_loops()
    {
        var normalizer = new KnowledgeGraphNormalizer(BaseUri);
        var result = normalizer.Normalize(new KnowledgeExtractionResult
        {
            Entities =
            [
                new KnowledgeEntityFact
                {
                    Id = NodeA,
                    Label = "Node A",
                    SameAs = [NodeA, string.Empty, null!, "not-a-uri", NodeB, NodeB, NodeALower],
                    Source = SourceOne,
                },
                new KnowledgeEntityFact
                {
                    Id = NodeB,
                    Label = "Node B",
                    SameAs = [NodeA],
                    Source = SourceTwo,
                },
            ],
        });

        result.Facts.Entities.Single().SameAs.ShouldBe([NodeB, NodeALower]);
        result.Report.Warnings.Count(static warning =>
            warning.Code == KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved).ShouldBe(3);
        result.Report.Warnings.Count(static warning =>
            warning.Code == KnowledgeGraphNormalizationWarningCode.SelfLoopRemoved).ShouldBe(2);
        result.Report.Warnings.Count(static warning =>
            warning.Code == KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved).ShouldBe(1);
    }

    private static KnowledgeGraphBuildOptions CreateDirtyGraphOptions()
    {
        return new KnowledgeGraphBuildOptions
        {
            Entities =
            [
                CreateEntity(NodeA, "Node A"),
                CreateEntity(NodeB, "Node B"),
                CreateEntity(NodeC, "Node C"),
            ],
            Edges =
            [
                CreateEdge(NodeA, RelatedTo, NodeB, 0.6d, SourceOne),
                CreateEdge(NodeB, RelatedTo, NodeA, 0.8d, SourceTwo),
                CreateEdge(NodeA, NextStep, NodeB, 0.9d, SourceOne),
                CreateEdge(NodeA, NextStep, NodeB, 0.4d, SourceTwo),
                CreateEdge(NodeB, NextStep, NodeC, 0.8d, SourceOne),
                CreateEdge(NodeC, NextStep, NodeA, 0.1d, SourceTwo),
                CreateEdge(NodeA, NextStep, NodeA, 1d, SourceOne),
            ],
        };
    }

    private static KnowledgeGraphEntityRule CreateEntity(string id, string label)
    {
        return new KnowledgeGraphEntityRule
        {
            Id = id,
            Label = label,
        };
    }

    private static KnowledgeGraphEdgeRule CreateEdge(
        string subjectId,
        string predicate,
        string objectId,
        double confidence,
        string source)
    {
        return new KnowledgeGraphEdgeRule
        {
            SubjectId = subjectId,
            Predicate = predicate,
            ObjectId = objectId,
            Confidence = confidence,
            Source = source,
        };
    }

    private static KnowledgeGraphEdge CreateSnapshotEdge(
        string subjectId,
        string predicateId,
        string predicateLabel,
        string objectId)
    {
        return new KnowledgeGraphEdge(subjectId, predicateId, predicateLabel, objectId);
    }

    private static KnowledgeAssertionFact CreateFact(
        string subjectId,
        string predicate,
        string objectId,
        double confidence,
        string source)
    {
        return new KnowledgeAssertionFact
        {
            SubjectId = subjectId,
            Predicate = predicate,
            ObjectId = objectId,
            Confidence = confidence,
            Source = source,
        };
    }

    private static IReadOnlyList<(string SubjectId, string Predicate, string ObjectId, double Confidence, string Sources)> ProjectAssertions(
        IReadOnlyList<KnowledgeAssertionFact> assertions)
    {
        return assertions
            .Select(static assertion => (
                assertion.SubjectId,
                assertion.Predicate,
                assertion.ObjectId,
                assertion.Confidence,
                string.Join('|', assertion.Sources)))
            .ToArray();
    }

    private const string SemanticMarkdown = """
---
title: Operator Graph Workflow
summary: Validate an authored workflow without exposing retrieval internals.
graph_related:
  - id: https://normalization.example/workflows/review/
    label: Review Workflow
    type: schema:SoftwareApplication
    sameAs:
      - https://external.example/review/
      - https://external.example/review/
      - ""
---
# Operator Graph Workflow

Operators validate graph relationships and review workflow connections before release.
""";
}
