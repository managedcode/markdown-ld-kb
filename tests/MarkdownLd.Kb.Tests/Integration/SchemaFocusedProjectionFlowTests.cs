using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaFocusedProjectionFlowTests
{
    private const string RootId = "https://focus.example/tools/search/";
    private const string DetailId = "https://focus.example/tools/detail/";
    private const string ObjectId = "https://focus.example/object-only";

    [Test]
    public async Task Generated_markdown_focus_keeps_selected_labels_and_each_induced_edge()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://focus.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        var build = await pipeline.BuildAsync([
            new MarkdownSourceDocument("tools/search.md", """
                ---
                title: Weather
                graph_related:
                  - https://focus.example/tools/detail/
                graph_next_steps:
                  - https://focus.example/tools/detail/
                ---
                # Weather
                Retrieve forecasts.
                """),
            new MarkdownSourceDocument("tools/detail.md", "# Details\n\nInspect forecasts."),
            new MarkdownSourceDocument("unrelated.md", "# Archives\n\nGenealogy evidence.")]);
        using var graph = build.Graph;
        var profile = KnowledgeGraphSchemaSearchProfile.Default with
        {
            CandidateNodeIds = [RootId],
            TextPredicates = [new("schema:name")],
            RelationshipPredicates = [],
            ExpansionPredicates = [new("kb:relatedTo", KnowledgeGraphSchemaSearchRole.Related, 0.7), new("kb:nextStep", KnowledgeGraphSchemaSearchRole.NextStep, 0.9)]
        };
        var result = await graph.SearchBySchemaAsync("Weather", profile);
        var focus = result.FocusedGraph;
        focus.Nodes.Select(node => node.Id).ShouldBe(new[] { DetailId, RootId });
        focus.Nodes.Single(node => node.Id == RootId).Label.ShouldBe("Weather");
        focus.Nodes.Single(node => node.Id == DetailId).Label.ShouldBe("Details");
        var selected = focus.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var expected = graph.ToCompleteSnapshot().Edges.Where(edge => selected.Contains(edge.SubjectId) && selected.Contains(edge.ObjectId));
        EdgeKeys(focus.Edges).ShouldBe(EdgeKeys(expected));
        focus.Edges.Select(edge => edge.PredicateId).ShouldContain(MarkdownKnowledgeDefaults.KbNamespaceText + "relatedTo");
        focus.Edges.Select(edge => edge.PredicateId).ShouldContain(MarkdownKnowledgeDefaults.KbNamespaceText + "nextStep");
        (await graph.SearchBySchemaAsync("satellite coffee", profile)).FocusedGraph.Nodes.ShouldBeEmpty();
    }

    [Test]
    public async Task Object_only_target_is_retained_without_importing_incoming_unselected_subjects()
    {
        using var graph = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s": "https://schema.org/" }, "@graph": [
              { "@id": "https://focus.example/tools/search/", "@type": "s:SoftwareApplication", "s:name": "Weather", "s:about": { "@id": "https://focus.example/object-only" } },
              { "@id": "https://focus.example/unrelated", "s:name": "Archives", "s:about": { "@id": "https://focus.example/object-only" } }
            ] }
            """);
        var result = await graph.SearchBySchemaAsync("Weather", CreateProfile());
        result.RelatedMatches.Single().NodeId.ShouldBe(ObjectId);
        result.FocusedGraph.Nodes.Select(node => node.Id).ShouldBe(new[] { ObjectId, RootId });
        result.FocusedGraph.Edges.Count.ShouldBe(1);
        result.FocusedGraph.Edges.Single().ObjectId.ShouldBe(ObjectId);
        result.FocusedGraph.Nodes.Single(node => node.Id == ObjectId).Kind.ShouldBe(KnowledgeGraphNodeKind.Uri);
    }

    [Test]
    public async Task Mixed_blank_uri_and_literal_expansion_preserves_general_rdf_projection()
    {
        using var graph = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s": "https://schema.org/" }, "@graph": [
              { "@id": "https://focus.example/tools/search/", "@type": "s:SoftwareApplication", "s:name": "Weather", "s:about": [{ "@id": "_:detail" }, { "@id": "https://focus.example/object-only" }, "rain"] },
              { "@id": "_:detail", "s:name": "Blank detail" }
            ] }
            """);
        var result = await graph.SearchBySchemaAsync("Weather", CreateProfile());
        result.RelatedMatches.Count.ShouldBe(3);
        result.RelatedMatches.Select(match => match.NodeId).ShouldContain("rain");
        var blank = result.RelatedMatches.Single(match => match.NodeId.StartsWith("_:", StringComparison.Ordinal));
        result.FocusedGraph.Nodes.Count.ShouldBe(3);
        result.FocusedGraph.Nodes.Single(node => node.Id == blank.NodeId).Label.ShouldBe("Blank detail");
        result.FocusedGraph.Nodes.Single(node => node.Id == blank.NodeId).Kind.ShouldBe(KnowledgeGraphNodeKind.Blank);
        result.FocusedGraph.Edges.Count.ShouldBe(2);
        var ids = result.FocusedGraph.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        EdgeKeys(result.FocusedGraph.Edges).ShouldBe(EdgeKeys(graph.ToCompleteSnapshot().Edges.Where(edge => ids.Contains(edge.SubjectId) && ids.Contains(edge.ObjectId))));
    }

    [Test]
    public async Task Repeated_search_observes_merged_target_metadata_without_stale_focus()
    {
        using var graph = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s": "https://schema.org/" }, "@id": "https://focus.example/tools/search/", "@type": "s:SoftwareApplication", "s:name": "Weather", "s:about": { "@id": "https://focus.example/object-only" } }
            """);
        var before = await graph.SearchBySchemaAsync("Weather", CreateProfile());
        before.FocusedGraph.Nodes.Single(node => node.Id == ObjectId).Label.ShouldNotBe("Ocean forecast");
        using var addition = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s": "https://schema.org/" }, "@id": "https://focus.example/object-only", "s:name": "Ocean forecast" }
            """);
        await graph.MergeAsync(addition);
        var results = await Task.WhenAll(graph.SearchBySchemaAsync("Weather", CreateProfile()), graph.SearchBySchemaAsync("Weather", CreateProfile()));
        foreach (var result in results)
        {
            result.FocusedGraph.Nodes.Single(node => node.Id == ObjectId).Label.ShouldBe("Ocean forecast");
            result.FocusedGraph.Edges.Count.ShouldBe(1);
        }
    }

    [Test]
    [Arguments("rain")]
    [Arguments("literal:rain")]
    [Arguments("https://focus.example/absent")]
    public async Task Literal_expansion_does_not_fabricate_a_graph_uri_node(string literal)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@context"] = new Dictionary<string, string> { ["s"] = "https://schema.org/" },
            ["@id"] = RootId,
            ["@type"] = "s:SoftwareApplication",
            ["s:name"] = "Weather",
            ["s:about"] = literal
        });
        using var graph = KnowledgeGraph.LoadJsonLd(json);
        var result = await graph.SearchBySchemaAsync("Weather", CreateProfile());
        result.RelatedMatches.Single().NodeId.ShouldBe(literal);
        result.FocusedGraph.Nodes.Single().Id.ShouldBe(RootId);
        result.FocusedGraph.Edges.ShouldBeEmpty();
    }

    [Test]
    public async Task Focus_retains_an_existing_object_only_uri_when_selected_by_its_rendered_identity()
    {
        using var graph = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s": "https://schema.org/" }, "@graph": [
              { "@id": "https://focus.example/tools/search/", "@type": "s:SoftwareApplication", "s:name": "Weather", "s:about": "https://focus.example/object-only" },
              { "@id": "https://focus.example/unrelated", "s:about": { "@id": "https://focus.example/object-only" } }
            ] }
            """);
        var result = await graph.SearchBySchemaAsync("Weather", CreateProfile());
        result.RelatedMatches.Single().NodeId.ShouldBe(ObjectId);
        result.FocusedGraph.Nodes.Select(node => node.Id).ShouldBe(new[] { ObjectId, RootId });
        result.FocusedGraph.Nodes.Single(node => node.Id == ObjectId).Kind.ShouldBe(KnowledgeGraphNodeKind.Uri);
        result.FocusedGraph.Edges.ShouldBeEmpty();
    }

    private static KnowledgeGraphSchemaSearchProfile CreateProfile() => new()
    {
        CandidateNodeIds = [RootId],
        TextPredicates = [new("schema:name")],
        RelationshipPredicates = [],
        ExpansionPredicates = [new("schema:about", KnowledgeGraphSchemaSearchRole.Related, 0.7)]
    };

    private static string[] EdgeKeys(IEnumerable<KnowledgeGraphEdge> edges) => edges
        .Select(edge => $"{edge.SubjectId}|{edge.PredicateId}|{edge.ObjectId}")
        .Order(StringComparer.Ordinal).ToArray();
}
