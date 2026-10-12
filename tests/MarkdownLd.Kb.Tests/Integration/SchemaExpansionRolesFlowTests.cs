using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaExpansionRolesFlowTests
{
    private const string RootId = "https://expansion.example/tools/search/";
    private const string DetailId = "https://expansion.example/tools/detail/";
    private const string GraphJson = """
        { "@context": { "s": "https://schema.org/" }, "@graph": [
          { "@id": "https://expansion.example/tools/search/", "@type": "s:SoftwareApplication", "s:name": "Fetch", "s:about": { "@id": "https://expansion.example/tools/detail/" }, "s:mentions": { "@id": "https://expansion.example/tools/detail/" }, "s:subjectOf": { "@id": "https://expansion.example/tools/detail/" } },
          { "@id": "https://expansion.example/tools/detail/", "@type": ["s:CreativeWork", "s:Dataset"], "s:name": "Record" }
        ] }
        """;

    [Test]
    public async Task Generated_markdown_neighbor_keeps_both_related_and_next_step_roles()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://expansion.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        var build = await pipeline.BuildAsync([
            new MarkdownSourceDocument("tools/search.md", """
                ---
                title: Fetch
                graph_related:
                  - https://expansion.example/tools/detail/
                graph_next_steps:
                  - https://expansion.example/tools/detail/
                ---
                # Fetch
                Find records.
                """),
            new MarkdownSourceDocument("tools/detail.md", "# Record\n\nInspect a found record.")]);
        using var graph = build.Graph;
        var profile = KnowledgeGraphSchemaSearchProfile.Default with
        {
            CandidateNodeIds = [RootId],
            ExpansionPredicates = [new("kb:relatedTo", KnowledgeGraphSchemaSearchRole.Related, 0.7), new("kb:nextStep", KnowledgeGraphSchemaSearchRole.NextStep, 0.9)]
        };
        var result = await graph.SearchBySchemaAsync("Fetch", profile);
        result.Matches.Single().NodeId.ShouldBe(RootId);
        result.RelatedMatches.Single().NodeId.ShouldBe(DetailId);
        result.NextStepMatches.Single().NodeId.ShouldBe(DetailId);
        result.RelatedMatches.Single().Score.ShouldBe(0.7);
        result.NextStepMatches.Single().Score.ShouldBe(0.9);
        result.FocusedGraph.Nodes.Count(node => node.Id == DetailId).ShouldBe(1);
        (await graph.SearchBySchemaAsync("satellite coffee", profile)).Matches.ShouldBeEmpty();
    }

    [Test]
    public async Task One_expansion_role_retains_all_neighbor_rdf_types()
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        var profile = new KnowledgeGraphSchemaSearchProfile
        {
            TypeFilters = ["schema:SoftwareApplication"],
            TextPredicates = [new("schema:name")],
            RelationshipPredicates = [],
            CandidateNodeIds = [RootId],
            ExpansionPredicates = [new("schema:about", KnowledgeGraphSchemaSearchRole.Related, 0.7)]
        };
        var result = await graph.SearchBySchemaAsync("Fetch", profile);
        result.RelatedMatches.Single().Types.ShouldBe(["https://schema.org/CreativeWork", "https://schema.org/Dataset"]);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Multiple_expansion_edges_preserve_each_role_all_types_and_the_best_binding(bool reverse)
    {
        using var graph = KnowledgeGraph.LoadJsonLd(GraphJson);
        KnowledgeGraphSchemaExpansionPredicate[] predicates = [new("schema:about", KnowledgeGraphSchemaSearchRole.Related, 0.7), new("schema:mentions", KnowledgeGraphSchemaSearchRole.Related, 0.4), new("schema:subjectOf", KnowledgeGraphSchemaSearchRole.NextStep, 0.9)];
        if (reverse)
        {
            Array.Reverse(predicates);
        }
        var profile = new KnowledgeGraphSchemaSearchProfile
        {
            TypeFilters = ["schema:SoftwareApplication"],
            TextPredicates = [new("schema:name")],
            RelationshipPredicates = [],
            ExpansionPredicates = predicates,
            CandidateNodeIds = [RootId]
        };
        var count = graph.TripleCount;
        var results = await Task.WhenAll(graph.SearchBySchemaAsync("Fetch", profile), graph.SearchBySchemaAsync("Fetch", profile));
        foreach (var result in results)
        {
            var related = result.RelatedMatches.Single();
            var next = result.NextStepMatches.Single();
            related.NodeId.ShouldBe(DetailId);
            next.NodeId.ShouldBe(DetailId);
            related.Score.ShouldBe(0.7);
            next.Score.ShouldBe(0.9);
            related.ViaPredicateId.ShouldBe("https://schema.org/about");
            next.ViaPredicateId.ShouldBe("https://schema.org/subjectOf");
            related.SourceNodeId.ShouldBe(RootId);
            next.SourceNodeId.ShouldBe(RootId);
            related.Types.ShouldBe(["https://schema.org/CreativeWork", "https://schema.org/Dataset"]);
            next.Types.ShouldBe(related.Types);
            result.FocusedGraph.Nodes.Count(node => node.Id == DetailId).ShouldBe(1);
        }
        graph.TripleCount.ShouldBe(count);
    }
}
