using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(
    BenchmarkCategories.Graph,
    BenchmarkCategories.Build,
    BenchmarkCategories.Search,
    BenchmarkCategories.Persistence,
    BenchmarkCategories.Export,
    BenchmarkCategories.Lifecycle)]
public class GraphLifecycleBenchmarks
{
    private const BenchmarkCorpusProfile CorpusProfile = BenchmarkCorpusProfile.ShortDocuments;
    private const string StoreLocation = "lifecycle/graph.ttl";
    private const string FileName = "graph.ttl";
    private static readonly KnowledgeGraphFilePersistenceOptions TurtlePersistenceOptions = new()
    {
        Format = KnowledgeGraphFileFormat.Turtle,
    };
    private static readonly KnowledgeGraphLoadOptions TurtleLoadOptions = new()
    {
        Format = KnowledgeGraphFileFormat.Turtle,
    };

    private MarkdownSourceDocument[] _sources = [];
    private KnowledgeGraphRankedSearchOptions _searchOptions = null!;
    private string _temporaryDirectory = string.Empty;
    private string _filePath = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _sources = BenchmarkCorpusFactory.CreateSources(CorpusProfile);
        _searchOptions = BenchmarkCorpusFactory.CreateRankedOptions(KnowledgeGraphSearchMode.Bm25);
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"{nameof(GraphLifecycleBenchmarks)}-{Guid.NewGuid():N}");
        _filePath = Path.Combine(_temporaryDirectory, FileName);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Benchmark(Baseline = true)]
    public async Task<int> BuildSearchSaveLoadAndExport()
    {
        var build = BenchmarkCorpusFactory.BuildNone(_sources);
        var query = BenchmarkCorpusFactory.GetQuery(CorpusProfile, BenchmarkQueryScenario.Exact);
        var searchMatches = await build.SearchRankedAsync(query, _searchOptions).ConfigureAwait(false);
        var store = new InMemoryKnowledgeGraphStore();

        await build.Graph.SaveToStoreAsync(store, StoreLocation, TurtlePersistenceOptions).ConfigureAwait(false);
        var storeGraph = await KnowledgeGraph.LoadFromStoreAsync(store, StoreLocation, TurtleLoadOptions)
            .ConfigureAwait(false);
        await build.Graph.SaveToFileAsync(_filePath, TurtlePersistenceOptions).ConfigureAwait(false);
        var fileGraph = await KnowledgeGraph.LoadFromFileAsync(_filePath, TurtleLoadOptions).ConfigureAwait(false);

        return build.Graph.TripleCount
            + searchMatches.Count
            + storeGraph.TripleCount
            + fileGraph.TripleCount
            + build.Graph.SerializeMermaidFlowchart().Length
            + build.Graph.SerializeDotGraph().Length;
    }
}
