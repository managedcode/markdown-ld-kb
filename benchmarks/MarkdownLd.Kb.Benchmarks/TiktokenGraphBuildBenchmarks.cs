using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(
    BenchmarkCategories.Tiktoken,
    BenchmarkCategories.Graph,
    BenchmarkCategories.Build)]
public class TiktokenGraphBuildBenchmarks
{
    private MarkdownSourceDocument[] _sources = [];

    [Params(1, 8)]
    public int CorpusCopies { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var original = BenchmarkCorpusFactory.CreateSources(BenchmarkCorpusProfile.RepeatedCatalog);
        _sources = Enumerable.Range(0, CorpusCopies).SelectMany(copy => original.Select(source =>
            source with { Path = $"copy-{copy:D2}/{source.Path}" })).ToArray();
    }

    [Benchmark]
    public int BuildRepeatedCatalogGraph()
    {
        var build = BenchmarkCorpusFactory.BuildTiktoken(
            _sources,
            buildAutoRelatedSegmentRelations: true);
        try
        {
            return build.Graph.TripleCount
                + build.Facts.Entities.Count
                + build.Facts.Assertions.Count
                + build.Normalization.Warnings.Count;
        }
        finally
        {
            build.Graph.Dispose();
        }
    }
}
