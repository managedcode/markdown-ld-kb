using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory(BenchmarkCategories.Graph, BenchmarkCategories.Search)]
public class TokenFocusedSearchBenchmarks
{
    private KnowledgeGraph _graph = null!;
    [Params(120, 1000)] public int DocumentCount { get; set; }
    [Params(BenchmarkQueryScenario.Exact, BenchmarkQueryScenario.NoMatch)] public BenchmarkQueryScenario QueryScenario { get; set; }
    [GlobalSetup]
    public async Task Setup()
    {
        var documents = Enumerable.Range(0, DocumentCount).Select(index => new MarkdownSourceDocument(
            $"tool-{index}.md", $"# Tool {index}\nWeather forecast service and status details for operation {index}.",
            new Uri($"https://bench.example/tools/{index}"))).ToArray();
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://bench.example/"),
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken,
            tiktokenOptions: new TiktokenKnowledgeGraphOptions { BuildAutoRelatedSegmentRelations = false });
        _graph = (await pipeline.BuildAsync(documents)).Graph;
        // Steady-state measurements; graph construction and cold materialization are separate workloads.
        _ = _graph.ToSemanticSnapshot();
    }
    [Benchmark]
    public async Task<int> FocusedTokenSearch()
    {
        var result = await _graph.SearchFocusedAsync(QueryScenario == BenchmarkQueryScenario.Exact
            ? "weather forecast service" : "satellite coffee roasting");
        return result.PrimaryMatches.Count + result.RelatedMatches.Count + result.NextStepMatches.Count
            + result.FocusedGraph.Nodes.Count + result.FocusedGraph.Edges.Count;
    }
    [GlobalCleanup] public void Cleanup() => _graph.Dispose();
}
