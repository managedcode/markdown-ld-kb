using Lucene.Net.Analysis;
using VDS.RDF.Query.FullText.Indexing;
using VDS.RDF.Query.FullText.Search;
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeGraphFullTextIndex : IDisposable
{
    private const int DefaultFullTextResultLimit = 10;
    private readonly LuceneDirectory _directory;
    private readonly Analyzer _analyzer;
    private readonly IFullTextIndexer _indexer;
    private readonly IFullTextSearchProvider _provider;
    private readonly IReadOnlyDictionary<string, string> _labels;

    internal KnowledgeGraphFullTextIndex(
        LuceneDirectory directory,
        Analyzer analyzer,
        IFullTextIndexer indexer,
        IFullTextSearchProvider provider,
        IReadOnlyDictionary<string, string> labels)
    {
        _directory = directory;
        _analyzer = analyzer;
        _indexer = indexer;
        _provider = provider;
        _labels = labels;
    }

    public Task<IReadOnlyList<KnowledgeGraphFullTextMatch>> SearchAsync(
        string query,
        int limit = DefaultFullTextResultLimit,
        double minimumScore = 0,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<KnowledgeGraphFullTextMatch>>([]);
        }

        var results = _provider
            .Match(query, minimumScore, limit)
            .Select(result =>
            {
                var nodeId = KnowledgeGraph.RenderNode(result.Node);
                return new KnowledgeGraphFullTextMatch(
                    nodeId,
                    _labels.TryGetValue(nodeId, out var label) ? label : nodeId,
                    result.Score);
            })
            .ToArray();
        return Task.FromResult<IReadOnlyList<KnowledgeGraphFullTextMatch>>(results);
    }

    public void Dispose()
    {
        (_provider as IDisposable)?.Dispose();
        (_indexer as IDisposable)?.Dispose();
        _analyzer.Dispose();
        _directory.Dispose();
    }
}
