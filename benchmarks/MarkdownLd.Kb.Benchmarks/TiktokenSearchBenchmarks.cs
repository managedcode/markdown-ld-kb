using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Tiktoken, BenchmarkCategories.Search)]
public class TiktokenSearchBenchmarks
{
    private MarkdownKnowledgeBuildResult _build = null!;
    private TokenDistanceSearchOptions _exactOptions = null!;
    private TokenDistanceSearchOptions _fuzzyOptions = null!;
    private string _query = string.Empty;

    [Params(25, 100, 250)]
    public int DocumentCount { get; set; }

    [Params(BenchmarkQueryScenario.Exact, BenchmarkQueryScenario.Typo, BenchmarkQueryScenario.NoMatch)]
    public BenchmarkQueryScenario QueryScenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sources = BenchmarkCorpusFactory.CreateSources(DocumentCount);
        _build = BenchmarkCorpusFactory.BuildTiktoken(sources);
        _query = BenchmarkCorpusFactory.GetQuery(QueryScenario);
        _exactOptions = BenchmarkCorpusFactory.CreateTokenDistanceOptions(fuzzy: false);
        _fuzzyOptions = BenchmarkCorpusFactory.CreateTokenDistanceOptions(fuzzy: true);
    }

    [Benchmark(Baseline = true)]
    public int ExactTokenDistanceSearch()
    {
        return _build.Graph.SearchByTokenDistanceAsync(_query, _exactOptions)
            .GetAwaiter()
            .GetResult()
            .Count;
    }

    [Benchmark]
    public int FuzzyCorrectedTokenDistanceSearch()
    {
        return _build.Graph.SearchByTokenDistanceAsync(_query, _fuzzyOptions)
            .GetAwaiter()
            .GetResult()
            .Count;
    }
}
