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

    [GlobalSetup]
    public void Setup()
    {
        _sources = BenchmarkCorpusFactory.CreateSources(BenchmarkCorpusProfile.RepeatedCatalog);
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
