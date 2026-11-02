using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeGraphDisposalFlowTests
{
    [Test]
    public async Task Disposal_releases_token_search_capability_and_keeps_previous_immutable_snapshot_readable()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://dispose.example/"), extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        using var graph = (await pipeline.BuildAsync([new MarkdownSourceDocument("weather.md", "# Weather\nWeather forecast evidence and archive details.")])).Graph;
        (await graph.SearchByTokenDistanceAsync("Weather forecast")).ShouldNotBeEmpty();
        var snapshot = graph.ToSemanticSnapshot();
        var expected = snapshot.SerializeTurtle();

        graph.Dispose();

        graph.CanSearchByTokenDistance.ShouldBeFalse();
        await Should.ThrowAsync<InvalidOperationException>(() => graph.SearchByTokenDistanceAsync("Weather forecast"));
        Should.Throw<ObjectDisposedException>(graph.ToCompleteSnapshot);
        snapshot.SerializeTurtle().ShouldBe(expected);
        graph.Dispose();
    }
}
