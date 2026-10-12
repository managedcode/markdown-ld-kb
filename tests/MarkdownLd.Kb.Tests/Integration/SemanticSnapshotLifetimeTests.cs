using System.Collections.Immutable;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SemanticSnapshotLifetimeTests
{
    [Test]
    public async Task Warm_snapshot_is_immutable_and_merge_invalidates_it_without_changing_previous_readers()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://snapshot.example/"),
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        using var graph = (await pipeline.BuildAsync([new MarkdownSourceDocument("weather.md", "# Weather\nForecast details.")])).Graph;
        var first = graph.ToSemanticSnapshot();
        graph.ToSemanticSnapshot().ShouldBeSameAs(first);
        first.Nodes.ShouldBeOfType<ImmutableArray<KnowledgeGraphNode>>();
        first.Edges.ShouldBeOfType<ImmutableArray<KnowledgeGraphEdge>>();
        var original = first.Nodes.Select(node => node.Id).ToArray();
        using var added = KnowledgeGraph.LoadJsonLd("""
            { "@id":"https://snapshot.example/new", "https://schema.org/name":"New evidence" }
            """);
        await graph.MergeAsync(added);
        var second = graph.ToSemanticSnapshot();
        second.ShouldNotBeSameAs(first);
        first.Nodes.Select(node => node.Id).ShouldBe(original);
        second.Nodes.Select(node => node.Id).ShouldContain("https://snapshot.example/new");
        graph.CanSearchByTokenDistance.ShouldBeFalse();
        graph.ToSemanticSnapshot().ShouldBeSameAs(second);
    }

    [Test]
    public async Task Concurrent_snapshot_readers_share_one_complete_semantic_projection()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://snapshot.example/"),
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        using var graph = (await pipeline.BuildAsync([new MarkdownSourceDocument("weather.md", "# Weather\nForecast details.")])).Graph;
        var readers = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(graph.ToSemanticSnapshot)));
        readers.All(snapshot => ReferenceEquals(snapshot, readers[0])).ShouldBeTrue();
        readers[0].Nodes.ShouldNotBeEmpty();
        readers[0].Nodes.Select(node => node.Id).ShouldNotContain(id => id.Contains("/tokens/", StringComparison.Ordinal));
        var query = await graph.SearchFocusedAsync("satellite coffee roasting");
        query.PrimaryMatches.ShouldNotBeEmpty();
        query.FocusedGraph.Nodes.ShouldNotBeEmpty();
    }
}
