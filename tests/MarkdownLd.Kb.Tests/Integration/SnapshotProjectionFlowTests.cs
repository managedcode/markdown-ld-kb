using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SnapshotProjectionFlowTests
{
    [Test]
    public async Task Markdown_and_merged_rdf_projection_preserves_label_priority_node_kinds_and_literal_edges()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://snapshot.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        var build = await pipeline.BuildAsync([new MarkdownSourceDocument("root.md", "# Root\n\nSnapshot projection evidence.")]);
        using var graph = build.Graph;
        using var addition = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s": "https://schema.org/", "r": "http://www.w3.org/2000/01/rdf-schema#", "k": "http://www.w3.org/2004/02/skos/core#", "x": "http://www.w3.org/2001/XMLSchema#" },
              "@graph": [
                { "@id": "https://snapshot.example/z", "r:label": "RDFS label", "k:prefLabel": "SKOS label", "s:name": ["Zulu", "Alpha"],
                  "s:about": [{ "@id": "https://snapshot.example/a" }, { "@id": "_:detail" }, { "@id": "urn:snapshot:object-only" }],
                  "s:version": [{ "@value": "2", "@type": "x:string" }, { "@value": "2", "@type": "x:integer" }] },
                { "@id": "https://snapshot.example/a", "s:name": [" ", "Beta"] },
                { "@id": "_:detail", "s:name": " ", "k:prefLabel": "Blank detail", "r:label": "Blank fallback" }
              ] }
            """);
        await graph.MergeAsync(addition);

        var snapshot = graph.ToCompleteSnapshot();
        snapshot.Nodes.Single(node => node.Id == "https://snapshot.example/z").Label.ShouldBe("Alpha");
        snapshot.Nodes.Single(node => node.Id == "https://snapshot.example/a").Label.ShouldBe("Beta");
        var blank = snapshot.Nodes.Single(node => node.Kind == KnowledgeGraphNodeKind.Blank);
        blank.Label.ShouldBe("Blank detail");
        snapshot.Nodes.Single(node => node.Id == "urn:snapshot:object-only").Kind.ShouldBe(KnowledgeGraphNodeKind.Uri);
        snapshot.Nodes.Single(node => node.Id == "literal:2").Kind.ShouldBe(KnowledgeGraphNodeKind.Literal);
        snapshot.Edges.Count(edge => edge.PredicateId == "https://schema.org/version" && edge.ObjectId == "literal:2").ShouldBe(2);
        (await graph.ExecuteSelectAsync("SELECT ?value WHERE { <https://snapshot.example/z> <https://schema.org/version> ?value } ORDER BY STR(DATATYPE(?value))")).Rows.Count.ShouldBe(2);
        snapshot.Nodes.Select(node => node.Id).ShouldBe(snapshot.Nodes.Select(node => node.Id).Order(StringComparer.Ordinal));
        snapshot.Edges.ShouldBe(snapshot.Edges.OrderBy(edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(edge => edge.PredicateId, StringComparer.Ordinal).ThenBy(edge => edge.ObjectId, StringComparer.Ordinal));
        var repeated = graph.ToCompleteSnapshot();
        repeated.Nodes.ShouldBe(snapshot.Nodes);
        repeated.Edges.ShouldBe(snapshot.Edges);
    }

    [Test]
    public async Task Reversed_markdown_input_retains_the_same_complete_projection()
    {
        MarkdownSourceDocument[] sources =
        [
            new("z.md", "---\ntitle: Zulu\ntags: [shared, graph]\n---\n# Zulu\n\n[Alpha](a.md)"),
            new("a.md", "---\ntitle: Alpha\ntags: [graph, shared]\n---\n# Alpha\n\n[Zulu](z.md)"),
        ];
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://snapshot.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        var first = await pipeline.BuildAsync(sources);
        var second = await pipeline.BuildAsync(sources.Reverse());
        using var firstGraph = first.Graph;
        using var secondGraph = second.Graph;
        var expected = firstGraph.ToCompleteSnapshot();
        var actual = secondGraph.ToCompleteSnapshot();
        actual.Nodes.ShouldBe(expected.Nodes);
        actual.Edges.ShouldBe(expected.Edges);
        (await firstGraph.SearchBySchemaAsync("Alpha")).Matches.ShouldNotBeEmpty();
    }

    [Test]
    public async Task Empty_markdown_corpus_returns_an_empty_complete_projection_and_no_query_matches()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://snapshot.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        var build = await pipeline.BuildAsync(Array.Empty<MarkdownSourceDocument>());
        using var graph = build.Graph;
        var snapshot = graph.ToCompleteSnapshot();
        snapshot.Nodes.ShouldBeEmpty();
        snapshot.Edges.ShouldBeEmpty();
        (await graph.ExecuteSelectAsync("SELECT ?s WHERE { ?s ?p ?o }")).Rows.ShouldBeEmpty();
    }
}
