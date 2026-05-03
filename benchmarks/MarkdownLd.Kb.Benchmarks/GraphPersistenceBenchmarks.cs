using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Graph, BenchmarkCategories.Persistence)]
public class GraphPersistenceBenchmarks
{
    private const string InMemoryTurtleLocation = "bench/graph.ttl";
    private const string InMemoryJsonLdLocation = "bench/graph.jsonld";
    private const string TurtleFileName = "graph.ttl";
    private const string JsonLdFileName = "graph.jsonld";
    private static readonly KnowledgeGraphFilePersistenceOptions TurtlePersistenceOptions = new()
    {
        Format = KnowledgeGraphFileFormat.Turtle,
    };
    private static readonly KnowledgeGraphFilePersistenceOptions JsonLdPersistenceOptions = new()
    {
        Format = KnowledgeGraphFileFormat.JsonLd,
    };
    private static readonly KnowledgeGraphLoadOptions TurtleLoadOptions = new()
    {
        Format = KnowledgeGraphFileFormat.Turtle,
    };
    private static readonly KnowledgeGraphLoadOptions JsonLdLoadOptions = new()
    {
        Format = KnowledgeGraphFileFormat.JsonLd,
    };

    private KnowledgeGraph _graph = null!;
    private InMemoryKnowledgeGraphStore _inMemoryStore = null!;
    private string _temporaryDirectory = string.Empty;
    private string _turtleFilePath = string.Empty;
    private string _jsonLdFilePath = string.Empty;

    [Params(
        BenchmarkCorpusProfile.ShortDocuments,
        BenchmarkCorpusProfile.LongDocuments,
        BenchmarkCorpusProfile.LargeCorpus)]
    public BenchmarkCorpusProfile CorpusProfile { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var sources = BenchmarkCorpusFactory.CreateSources(CorpusProfile);
        _graph = BenchmarkCorpusFactory.BuildNone(sources).Graph;
        _inMemoryStore = new InMemoryKnowledgeGraphStore();
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"{nameof(GraphPersistenceBenchmarks)}-{Guid.NewGuid():N}");
        _turtleFilePath = Path.Combine(_temporaryDirectory, TurtleFileName);
        _jsonLdFilePath = Path.Combine(_temporaryDirectory, JsonLdFileName);
        SeedStores();
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
    [BenchmarkCategory(BenchmarkCategories.Serialization)]
    public int CreateSnapshot()
    {
        return _graph.ToSnapshot().Edges.Count;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Serialization)]
    public int SerializeTurtle()
    {
        return _graph.SerializeTurtle().Length;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Serialization)]
    public int SerializeJsonLd()
    {
        return _graph.SerializeJsonLd().Length;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Export)]
    public int ExportMermaidFlowchart()
    {
        return _graph.SerializeMermaidFlowchart().Length;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Export)]
    public int ExportDotGraph()
    {
        return _graph.SerializeDotGraph().Length;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Save)]
    public async Task<int> SaveTurtleToInMemoryStore()
    {
        await _graph.SaveToStoreAsync(_inMemoryStore, InMemoryTurtleLocation, TurtlePersistenceOptions).ConfigureAwait(false);
        return _graph.TripleCount;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Load)]
    public async Task<int> LoadTurtleFromInMemoryStore()
    {
        var graph = await KnowledgeGraph.LoadFromStoreAsync(_inMemoryStore, InMemoryTurtleLocation, TurtleLoadOptions)
            .ConfigureAwait(false);
        return graph.TripleCount;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Save)]
    public async Task<int> SaveJsonLdToInMemoryStore()
    {
        await _graph.SaveToStoreAsync(_inMemoryStore, InMemoryJsonLdLocation, JsonLdPersistenceOptions)
            .ConfigureAwait(false);
        return _graph.TripleCount;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Load)]
    public async Task<int> LoadJsonLdFromInMemoryStore()
    {
        var graph = await KnowledgeGraph.LoadFromStoreAsync(_inMemoryStore, InMemoryJsonLdLocation, JsonLdLoadOptions)
            .ConfigureAwait(false);
        return graph.TripleCount;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Save)]
    public async Task<int> SaveTurtleToFile()
    {
        await _graph.SaveToFileAsync(_turtleFilePath, TurtlePersistenceOptions).ConfigureAwait(false);
        return _graph.TripleCount;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Load)]
    public async Task<int> LoadTurtleFromFile()
    {
        var graph = await KnowledgeGraph.LoadFromFileAsync(_turtleFilePath, TurtleLoadOptions).ConfigureAwait(false);
        return graph.TripleCount;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Save)]
    public async Task<int> SaveJsonLdToFile()
    {
        await _graph.SaveToFileAsync(_jsonLdFilePath, JsonLdPersistenceOptions).ConfigureAwait(false);
        return _graph.TripleCount;
    }

    [Benchmark]
    [BenchmarkCategory(BenchmarkCategories.Load)]
    public async Task<int> LoadJsonLdFromFile()
    {
        var graph = await KnowledgeGraph.LoadFromFileAsync(_jsonLdFilePath, JsonLdLoadOptions).ConfigureAwait(false);
        return graph.TripleCount;
    }

    private void SeedStores()
    {
        _graph.SaveToStoreAsync(_inMemoryStore, InMemoryTurtleLocation, TurtlePersistenceOptions)
            .GetAwaiter()
            .GetResult();
        _graph.SaveToStoreAsync(_inMemoryStore, InMemoryJsonLdLocation, JsonLdPersistenceOptions)
            .GetAwaiter()
            .GetResult();
        _graph.SaveToFileAsync(_turtleFilePath, TurtlePersistenceOptions)
            .GetAwaiter()
            .GetResult();
        _graph.SaveToFileAsync(_jsonLdFilePath, JsonLdPersistenceOptions)
            .GetAwaiter()
            .GetResult();
    }
}
