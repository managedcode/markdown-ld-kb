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
        readers[0].Nodes.Select(node => node.Id).ShouldNotContain(id =>
            id.Contains("/token-section/", StringComparison.Ordinal) ||
            id.Contains("/token-segment/", StringComparison.Ordinal) ||
            id.Contains("/token-topic/", StringComparison.Ordinal));
        var query = await graph.SearchFocusedAsync("satellite coffee roasting");
        query.PrimaryMatches.ShouldNotBeEmpty();
        query.FocusedGraph.Nodes.ShouldNotBeEmpty();
    }

    [Test]
    [Arguments(MarkdownKnowledgeExtractionMode.None)]
    [Arguments(MarkdownKnowledgeExtractionMode.Tiktoken)]
    public async Task Concurrent_cold_readers_after_merge_share_fresh_snapshot_and_preserve_previous_results(MarkdownKnowledgeExtractionMode mode)
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://snapshot.example/"), extractionMode: mode);
        using var graph = (await pipeline.BuildAsync([new MarkdownSourceDocument("weather.md", "# Weather\nForecast details.")])).Graph;
        var before = await ReadConcurrentlyAsync(graph);
        var original = before[0].SerializeTurtle();
        using var addition = KnowledgeGraph.LoadJsonLd("""
            { "@id":"https://snapshot.example/added", "https://schema.org/name":"Added satellite evidence" }
            """);

        await graph.MergeAsync(addition);
        var after = await ReadConcurrentlyAsync(graph);

        before.ShouldAllBe(snapshot => ReferenceEquals(snapshot, before[0]));
        after.ShouldAllBe(snapshot => ReferenceEquals(snapshot, after[0]));
        after[0].ShouldNotBeSameAs(before[0]);
        before[0].SerializeTurtle().ShouldBe(original);
        before[0].Nodes.ShouldNotContain(node => node.Id == "https://snapshot.example/added");
        after[0].Nodes.ShouldContain(node => node.Id == "https://snapshot.example/added");
        after[0].Nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(after[0].Nodes.Count);
        (await graph.ExecuteAskAsync("ASK WHERE { <https://snapshot.example/added> <https://schema.org/name> \"Added satellite evidence\" }")).ShouldBeTrue();
    }

    private static Task<KnowledgeGraphSnapshot[]> ReadConcurrentlyAsync(KnowledgeGraph graph)
    {
        return Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(graph.ToSemanticSnapshot)));
    }

    [Test]
    [Arguments(MarkdownKnowledgeExtractionMode.None)]
    [Arguments(MarkdownKnowledgeExtractionMode.Tiktoken)]
    public async Task Repeated_merges_and_racing_readers_preserve_committed_snapshot_boundaries(MarkdownKnowledgeExtractionMode mode)
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://merge-snapshot.example/"), extractionMode: mode);
        using var graph = (await pipeline.BuildAsync([new MarkdownSourceDocument("seed.md", "# Seed\nWeather evidence.")])).Graph;
        for (var round = 0; round < 8; round++)
        {
            var before = graph.ToSemanticSnapshot();
            var original = before.SerializeTurtle();
            var id = $"https://merge-snapshot.example/added-{round}";
            using var addition = KnowledgeGraph.LoadJsonLd($$"""
                { "@id": "{{id}}", "https://schema.org/name": "Added evidence {{round}}" }
                """);
            var readers = ReadConcurrentlyAsync(graph);
            var merge = graph.MergeAsync(addition);
            await Task.WhenAll(readers, merge);
            var after = graph.ToSemanticSnapshot();

            after.Nodes.ShouldContain(node => node.Id == id);
            readers.Result.ShouldAllBe(snapshot => ReferenceEquals(snapshot, before) || ReferenceEquals(snapshot, after));
            before.SerializeTurtle().ShouldBe(original);
            after.ShouldNotBeSameAs(before);
        }
        var query = await graph.ExecuteSelectAsync("SELECT ?s WHERE { ?s <https://schema.org/name> ?name FILTER(STRSTARTS(STR(?s), 'https://merge-snapshot.example/added-')) }");
        query.Rows.Count.ShouldBe(8);
        graph.CanSearchByTokenDistance.ShouldBeFalse();
    }
}
