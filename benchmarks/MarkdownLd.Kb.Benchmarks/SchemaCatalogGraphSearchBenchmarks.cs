using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Graph, BenchmarkCategories.Search)]
public class SchemaCatalogGraphSearchBenchmarks
{
    private const string MatchQuery = "weather";
    private const string MissingQuery = "satellite coffee roasting";
    private const int ToolCount = 120;
    private MarkdownKnowledgeBuildResult _build = null!;
    private KnowledgeGraphSchemaSearchProfile _all = null!;
    private KnowledgeGraphSchemaSearchProfile _candidates = null!;
    private string _query = string.Empty;

    [Params(BenchmarkQueryScenario.Exact, BenchmarkQueryScenario.NoMatch)]
    public BenchmarkQueryScenario QueryScenario { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var sources = Enumerable.Range(1, ToolCount).Select(index => new MarkdownSourceDocument($"tools/tool-{index:D3}.md", index == 81
            ? "# Weather forecast specialist\n\nGet weather forecast, rain, wind, umbrella planning, and precipitation details for a city or region.\n\n## Arguments\nQuery: string."
            : $"# Archive lookup {index:D3}\n\nHandle archive lookup workflow number {index:D3} for genealogy records.\n\n## Arguments\nQuery: string.")).ToArray();
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://catalog.bench.example/"), extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        _build = await pipeline.BuildAsync(sources);
        _all = KnowledgeGraphSchemaSearchProfile.Default with
        {
            TypeFilters = ["schema:Article"],
            TextPredicates = [new("schema:name"), new("schema:description"), new("schema:keywords"), new("skos:prefLabel")],
            RelationshipPredicates = [new("schema:about", ["schema:name", "skos:prefLabel"]), new("schema:mentions", ["schema:name"]), new("skos:memberOf", ["schema:name", "skos:prefLabel"]), new("prov:wasDerivedFrom", ["schema:name", "skos:prefLabel"]) { Direction = KnowledgeGraphSchemaRelationshipDirection.Inbound }],
            ExpansionPredicates = [new("kb:relatedTo", KnowledgeGraphSchemaSearchRole.Related, 0.7), new("kb:nextStep", KnowledgeGraphSchemaSearchRole.NextStep, 0.9), new("schema:mentions", KnowledgeGraphSchemaSearchRole.Related, 0.7), new("schema:hasPart", KnowledgeGraphSchemaSearchRole.Related, 0.6)],
            TermMode = KnowledgeGraphSchemaSearchTermMode.AnyTerm,
            MaxResults = 5
        };
        var seed = await _build.Graph.SearchBySchemaAsync(MatchQuery, _all);
        if (seed.Matches.Count != 1)
        {
            throw new InvalidOperationException($"Expected one specialist, found {seed.Matches.Count}.");
        }

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
