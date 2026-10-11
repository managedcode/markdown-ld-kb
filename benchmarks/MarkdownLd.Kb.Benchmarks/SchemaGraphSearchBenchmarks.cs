using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Graph, BenchmarkCategories.Search)]
public class SchemaGraphSearchBenchmarks
{
    private MarkdownKnowledgeBuildResult _build = null!;
    private KnowledgeGraphSchemaSearchProfile _all = null!;
    private KnowledgeGraphSchemaSearchProfile _candidates = null!;
    private string _query = string.Empty;
    private const string MatchQuery = "cache restore manifest evidence";
    private const string MissingQuery = "satellite coffee roasting";

    [Params(BenchmarkCorpusProfile.ShortDocuments, BenchmarkCorpusProfile.LongDocuments)]
    public BenchmarkCorpusProfile CorpusProfile { get; set; }

    [Params(BenchmarkQueryScenario.Exact, BenchmarkQueryScenario.NoMatch)]
    public BenchmarkQueryScenario QueryScenario { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://bench.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        _build = await pipeline.BuildAsync(BenchmarkCorpusFactory.CreateSources(CorpusProfile));
        _all = KnowledgeGraphSchemaSearchProfile.Default with { TermMode = KnowledgeGraphSchemaSearchTermMode.AnyTerm, MaxResults = 10 };
        var seed = await _build.Graph.SearchBySchemaAsync(MatchQuery, _all);
        _candidates = _all with { CandidateNodeIds = seed.Matches.Select(match => match.NodeId).ToArray() };
        _query = QueryScenario == BenchmarkQueryScenario.NoMatch ? MissingQuery : MatchQuery;
    }

    [Benchmark(Baseline = true)]
    public async Task<int> Unrestricted() => (await _build.Graph.SearchBySchemaAsync(_query, _all)).Matches.Count;

    [Benchmark]
    public async Task<int> CandidateValues() => (await _build.Graph.SearchBySchemaAsync(_query, _candidates)).Matches.Count;

    [GlobalCleanup]
    public void Cleanup() => _build.Graph.Dispose();
}
