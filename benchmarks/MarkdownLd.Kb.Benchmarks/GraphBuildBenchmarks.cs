using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Graph, BenchmarkCategories.Build)]
public class GraphBuildBenchmarks
{
    private MarkdownSourceDocument[] _sources = [];

    [Params(25, 250, 1000)]
    public int DocumentCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sources = BenchmarkCorpusFactory.CreateSources(DocumentCount);
    }

    [Benchmark]
    public int BuildGraph()
    {
        return BenchmarkCorpusFactory.BuildNone(_sources).Graph.TripleCount;
    }
}
