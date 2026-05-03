using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Graph, BenchmarkCategories.Build)]
public class GraphBuildBenchmarks
{
    private MarkdownSourceDocument[] _sources = [];

    [Params(
        BenchmarkCorpusProfile.ShortDocuments,
        BenchmarkCorpusProfile.LongDocuments,
        BenchmarkCorpusProfile.LargeCorpus,
        BenchmarkCorpusProfile.TokenizedMultilingual)]
    public BenchmarkCorpusProfile CorpusProfile { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sources = BenchmarkCorpusFactory.CreateSources(CorpusProfile);
    }

    [Benchmark]
    public int BuildGraph()
    {
        return BenchmarkCorpusFactory.BuildNone(_sources).Graph.TripleCount;
    }
}
