using ManagedCode.MarkdownLd.Kb.Query;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Writing;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;
using StringWriter = System.IO.StringWriter;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph : IDisposable
{
    private readonly Graph _graph;
    private readonly ReaderWriterLockSlim _graphLock = new();
    private TokenizedKnowledgeIndex? _tokenIndex;

    internal KnowledgeGraph(Graph graph, TokenizedKnowledgeIndex? tokenIndex = null)
    {
        _graph = graph;
        _tokenIndex = tokenIndex;
    }

    public bool CanSearchByTokenDistance => _tokenIndex is not null;

    public int TripleCount
    {
        get
        {
            _graphLock.EnterReadLock();
            try
            {
                return _graph.Triples.Count;
            }
            finally
            {
                _graphLock.ExitReadLock();
            }
        }
    }

    public Task MergeAsync(KnowledgeGraph graph, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = graph.CreateSnapshot();
        return Task.Run(() => MergeSnapshot(snapshot, cancellationToken), cancellationToken);
    }

    public async Task<SparqlQueryResult> ExecuteSelectAsync(string sparql, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteQueryAsync(
                sparql,
                cancellationToken,
                SparqlSafety.IsSelectQuery,
                ExecuteSelectRequiresSelectQueryMessage)
            .ConfigureAwait(false);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(ExpectedResultSetMessage);
        }

        return ToResult(resultSet);
    }

    public async Task<bool> ExecuteAskAsync(string sparql, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteQueryAsync(
                sparql,
                cancellationToken,
                static queryType => queryType == SparqlQueryType.Ask,
                ExecuteAskRequiresAskQueryMessage)
            .ConfigureAwait(false);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(ExpectedResultSetMessage);
        }

        return resultSet.Result;
    }

    public Task<SparqlQueryResult> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var searchQuery = SearchQueryTemplate.Replace(SearchTermToken, EscapeSparqlLiteral(term), StringComparison.Ordinal);
        return ExecuteSelectAsync(searchQuery, cancellationToken);
    }

    public Task<IReadOnlyList<TokenDistanceSearchResult>> SearchByTokenDistanceAsync(
        string query,
        int limit = DefaultMaxRelatedTokenSegments,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_tokenIndex is null)
        {
            throw new InvalidOperationException(TokenDistanceSearchUnavailableMessage);
        }

        return Task.FromResult(_tokenIndex.Search(query, limit));
    }

    public Task<IReadOnlyList<TokenDistanceSearchResult>> SearchByTokenDistanceAsync(
        string query,
        TokenDistanceSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_tokenIndex is null)
        {
            throw new InvalidOperationException(TokenDistanceSearchUnavailableMessage);
        }

        return Task.FromResult(_tokenIndex.Search(query, options));
    }

    public KnowledgeGraphSnapshot ToSnapshot()
    {
        _graphLock.EnterReadLock();
        try
        {
            return CreateGraphSnapshot(_graph.Triples);
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    public string SerializeMermaidFlowchart()
    {
        _graphLock.EnterReadLock();
        try
        {
            return BuildMermaidFlowchart(CreateGraphSnapshot(_graph.Triples));
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    public string SerializeDotGraph()
    {
        _graphLock.EnterReadLock();
        try
        {
            return BuildDotGraph(CreateGraphSnapshot(_graph.Triples));
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    public string SerializeTurtle()
    {
        _graphLock.EnterReadLock();
        try
        {
            using var writer = new StringWriter();
            var turtleWriter = new CompressingTurtleWriter();
            turtleWriter.Save(_graph, writer);
            return writer.ToString();
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    public string SerializeJsonLd()
    {
        _graphLock.EnterReadLock();
        try
        {
            using var writer = new StringWriter();
            var store = new TripleStore();
            store.Add(_graph);
            var jsonLdWriter = new JsonLdWriter();
            jsonLdWriter.Save(store, writer, false);
            return writer.ToString();
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private async Task<object> ExecuteQueryAsync(
        string sparql,
        CancellationToken cancellationToken,
        Func<SparqlQueryType, bool>? expectedQueryType = null,
        string? expectedQueryTypeMessage = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safety = SparqlSafety.EnforceReadOnly(sparql);
        if (!safety.IsAllowed)
        {
            throw new ReadOnlySparqlQueryException(safety.ErrorMessage ?? ReadOnlySparqlQueryMessage);
        }

        var parser = new SparqlQueryParser();
        var query = parser.ParseFromString(safety.Query);
        if (!SparqlSafety.IsReadOnlyQuery(query.QueryType))
        {
            throw new ReadOnlySparqlQueryException(SelectAskOnlyMessagePrefix + query.QueryType);
        }

        EnsureExpectedQueryType(query.QueryType, expectedQueryType, expectedQueryTypeMessage);

        return await Task.Run(() => ProcessQuery(query, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureExpectedQueryType(
        SparqlQueryType queryType,
        Func<SparqlQueryType, bool>? expectedQueryType,
        string? expectedQueryTypeMessage)
    {
        if (expectedQueryType is not null && !expectedQueryType(queryType))
        {
            throw new ReadOnlySparqlQueryException(expectedQueryTypeMessage ?? ReadOnlySparqlQueryMessage);
        }
    }

    private void MergeSnapshot(Graph graph, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _graphLock.EnterWriteLock();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _graph.Merge(graph);
            _tokenIndex = null;
        }
        finally
        {
            _graphLock.ExitWriteLock();
        }
    }

    internal Graph CreateSnapshot()
    {
        _graphLock.EnterReadLock();
        try
        {
            var snapshot = new Graph();
            snapshot.Merge(_graph);
            return snapshot;
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private object ProcessQuery(
        SparqlQuery query,
        CancellationToken cancellationToken,
        int? queryExecutionTimeoutMilliseconds = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _graphLock.EnterReadLock();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dataset = new InMemoryDataset(_graph);
            var processor = queryExecutionTimeoutMilliseconds is null
                ? new LeviathanQueryProcessor(dataset)
                : new LeviathanQueryProcessor(dataset, options =>
                {
                    options.QueryExecutionTimeout = queryExecutionTimeoutMilliseconds.Value;
                });
            return processor.ProcessQuery(query);
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    internal object ProcessFederatedLocalQuery(
        SparqlQuery query,
        KnowledgeGraphFederatedLocalServiceRegistry localServices,
        CancellationToken cancellationToken,
        int queryExecutionTimeoutMilliseconds)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _graphLock.EnterReadLock();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dataset = new InMemoryDataset(_graph);
            var processor = new KnowledgeGraphFederatedQueryProcessor(
                dataset,
                localServices,
                options => options.QueryExecutionTimeout = queryExecutionTimeoutMilliseconds);
            return processor.ProcessQuery(query);
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private static SparqlQueryResult ToResult(SparqlResultSet resultSet)
    {
        var variables = resultSet.Variables.Select(variable => variable.ToString()).ToArray();
        var rows = new List<SparqlRow>();

        foreach (var result in resultSet)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in resultSet.Variables)
            {
                if (result.TryGetBoundValue(variable, out var node) && node is not null)
                {
                    values[variable] = RenderNode(node);
                }
            }

            rows.Add(new SparqlRow(values));
        }

        return new SparqlQueryResult(variables, rows);
    }

    internal static string RenderNode(INode node)
    {
        return node switch
        {
            IUriNode uriNode => uriNode.Uri.AbsoluteUri,
            ILiteralNode literalNode => literalNode.Value,
            IBlankNode blankNode => BlankNodePrefix + blankNode.InternalID,
            _ => node.ToString(),
        };
    }

    internal static string EscapeSparqlLiteral(string value)
    {
        return value.Replace(BackslashText, EscapedBackslashText, StringComparison.Ordinal)
            .Replace(QuoteText, EscapedQuoteText, StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }

    public void Dispose()
    {
        _graphLock.Dispose();
    }
}
