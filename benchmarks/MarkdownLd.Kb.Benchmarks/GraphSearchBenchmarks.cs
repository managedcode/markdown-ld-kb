using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Graph, BenchmarkCategories.Search)]
public class GraphSearchBenchmarks
{
    private MarkdownKnowledgeBuildResult _build = null!;
    private KnowledgeGraphRankedSearchOptions _graphOptions = null!;
    private KnowledgeGraphRankedSearchOptions _bm25Options = null!;
    private KnowledgeGraphRankedSearchOptions _bm25FuzzyOptions = null!;
    private KnowledgeGraphSchemaSearchProfile _federatedProfile = null!;
    private FederatedSparqlExecutionOptions _federatedOptions = null!;
    private string _query = string.Empty;

    [Params(
        BenchmarkCorpusProfile.ShortDocuments,
        BenchmarkCorpusProfile.LongDocuments,
        BenchmarkCorpusProfile.FederatedRunbooks)]
    public BenchmarkCorpusProfile CorpusProfile { get; set; }

    [Params(BenchmarkQueryScenario.Exact, BenchmarkQueryScenario.Typo, BenchmarkQueryScenario.NoMatch)]
    public BenchmarkQueryScenario QueryScenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sources = BenchmarkCorpusFactory.CreateSources(CorpusProfile);
        _build = BenchmarkCorpusFactory.BuildNone(sources);
        _query = BenchmarkCorpusFactory.GetQuery(CorpusProfile, QueryScenario);
        _graphOptions = BenchmarkCorpusFactory.CreateRankedOptions(KnowledgeGraphSearchMode.Graph);
        _bm25Options = BenchmarkCorpusFactory.CreateRankedOptions(KnowledgeGraphSearchMode.Bm25);
        _bm25FuzzyOptions = BenchmarkCorpusFactory.CreateRankedOptions(KnowledgeGraphSearchMode.Bm25, fuzzy: true);
        _federatedProfile = BenchmarkCorpusFactory.CreateFederatedProfile();
        _federatedOptions = BenchmarkCorpusFactory.CreateFederatedOptions(_build.Graph);
    }

    [Benchmark(Baseline = true)]
    public int RankedGraphSearch()
    {
        return _build.SearchRankedAsync(_query, _graphOptions)
            .GetAwaiter()
            .GetResult()
            .Count;
    }

    [Benchmark]
    public int Bm25Search()
    {
        return _build.SearchRankedAsync(_query, _bm25Options)
            .GetAwaiter()
            .GetResult()
            .Count;
    }

    [Benchmark]
    public int Bm25FuzzySearch()
    {
        return _build.SearchRankedAsync(_query, _bm25FuzzyOptions)
            .GetAwaiter()
            .GetResult()
            .Count;
    }

    [Benchmark]
    public int SchemaSearch()
    {
        return _build.Graph.SearchBySchemaAsync(_query)
            .GetAwaiter()
            .GetResult()
            .Matches
            .Count;
    }

    [Benchmark]
    public int FocusedSearch()
    {
        return _build.Graph.SearchFocusedAsync(_query)
            .GetAwaiter()
            .GetResult()
            .PrimaryMatches
            .Count;
    }

    [BenchmarkCategory(BenchmarkCategories.Federation)]
    [Benchmark]
    public int LocalFederatedSchemaSearch()
    {
        return _build.Graph.SearchBySchemaFederatedAsync(_query, _federatedProfile, _federatedOptions)
            .GetAwaiter()
            .GetResult()
            .Matches
            .Count;
    }
}
