using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SemanticProjectionBoundaryFlowTests
{
    [Test]
    public async Task Cached_snapshot_rejects_collection_mutation_and_does_not_alias_complete_projection_arrays()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://immutable.example/"), extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        using var graph = (await pipeline.BuildAsync([new MarkdownSourceDocument("guide.md", "# Immutable guide\nWeather forecast evidence.")])).Graph;
        var cached = graph.ToSemanticSnapshot();
        var original = cached.SerializeTurtle();

        Should.Throw<NotSupportedException>(() => ((IList<KnowledgeGraphNode>)cached.Nodes)[0] = new("urn:modified", "Modified", KnowledgeGraphNodeKind.Uri));
        Should.Throw<NotSupportedException>(() => ((IList<KnowledgeGraphEdge>)cached.Edges)[0] = new("urn:modified", "urn:p", "Modified", "urn:modified"));
        var complete = graph.ToCompleteSnapshot();
        ((KnowledgeGraphNode[])complete.Nodes)[0] = new("urn:modified", "Modified", KnowledgeGraphNodeKind.Uri);
        ((KnowledgeGraphEdge[])complete.Edges)[0] = new("urn:modified", "urn:p", "Modified", "urn:modified");

        graph.ToSemanticSnapshot().ShouldBeSameAs(cached);
        cached.SerializeTurtle().ShouldBe(original);
        (await graph.ExecuteAskAsync("ASK WHERE { ?s <https://schema.org/name> \"Immutable guide\" }")).ShouldBeTrue();
        graph.ToCompleteSnapshot().Nodes.ShouldNotContain(node => node.Id == "urn:modified");
    }

    [Test]
    [Arguments(MarkdownKnowledgeExtractionMode.None)]
    [Arguments(MarkdownKnowledgeExtractionMode.Tiktoken)]
    public async Task Semantic_projection_preserves_complete_graph_labels_and_induced_edges_when_removing_retrieval_nodes(MarkdownKnowledgeExtractionMode mode)
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://projection.example/"), extractionMode: mode);
        using var graph = (await pipeline.BuildAsync([new MarkdownSourceDocument("guide.md", "# Guide\nWeather forecast evidence and archive details.")])).Graph;
        using var addition = KnowledgeGraph.LoadJsonLd("""
            { "@context": { "s": "https://schema.org/", "r": "http://www.w3.org/2000/01/rdf-schema#", "x": "http://www.w3.org/2001/XMLSchema#" }, "@graph": [
              { "@id": "https://projection.example/kept", "s:name": ["Zulu", "Alpha"], "s:description": "guide/token-topic/literal",
                "s:about": [{ "@id": "https://projection.example/token-segment/internal" }, { "@id": "https://projection.example/object-only" }, { "@id": "_:detail" }],
                "s:version": [{ "@value": "2", "@type": "x:string" }, { "@value": "2", "@type": "x:integer" }] },
              { "@id": "https://projection.example/token-segment/internal", "s:name": "Internal", "s:about": { "@id": "https://projection.example/orphan" } },
              { "@id": "https://projection.example/resource?route=/token-topic/example", "s:name": "Query route" },
              { "@id": "_:detail", "r:label": "Blank detail" }
            ] }
            """);
        await graph.MergeAsync(addition);
        var complete = graph.ToCompleteSnapshot();
        var expected = ReferenceProjection(complete);

        var actual = graph.ToSemanticSnapshot();

        actual.Nodes.ShouldBe(expected.Nodes);
        actual.Edges.ShouldBe(expected.Edges);
        actual.Nodes.ShouldContain(node => node.Id == "https://projection.example/kept" && node.Label == "Alpha");
        actual.Nodes.ShouldContain(node => node.Id == "literal:guide/token-topic/literal");
        actual.Nodes.ShouldContain(node => node.Id == "https://projection.example/object-only");
        actual.Nodes.ShouldNotContain(node => node.Id == "https://projection.example/orphan");
        actual.Nodes.ShouldContain(node => node.Id == "https://projection.example/resource?route=/token-topic/example");
        actual.Edges.Count(edge => edge.PredicateId == "https://schema.org/version").ShouldBe(1);
        graph.ToCompleteSnapshot().Edges.ShouldBe(complete.Edges);
        (await graph.ExecuteSelectAsync("SELECT ?v WHERE { <https://projection.example/kept> <https://schema.org/version> ?v }")).Rows.Count.ShouldBe(2);
    }

    private static KnowledgeGraphSnapshot ReferenceProjection(KnowledgeGraphSnapshot complete)
    {
        var excluded = complete.Nodes.Where(node => node.Kind == KnowledgeGraphNodeKind.Uri &&
            new Uri(node.Id).Segments.Any(segment => segment is "token-section/" or "token-segment/" or "token-topic/"))
            .Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = complete.Edges.Where(edge => !excluded.Contains(edge.SubjectId) && !excluded.Contains(edge.ObjectId))
            .DistinctBy(edge => (edge.SubjectId, edge.PredicateId, edge.ObjectId)).ToArray();
        var included = edges.SelectMany(edge => new[] { edge.SubjectId, edge.ObjectId }).ToHashSet(StringComparer.Ordinal);
        return new KnowledgeGraphSnapshot(complete.Nodes.Where(node => included.Contains(node.Id)).ToArray(), edges);
    }
}
