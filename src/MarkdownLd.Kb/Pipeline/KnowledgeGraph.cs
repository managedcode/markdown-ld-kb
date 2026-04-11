using ManagedCode.MarkdownLd.Kb.Query;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Writing;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;
using StringWriter = System.IO.StringWriter;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private readonly Graph _graph;
    private readonly ReaderWriterLockSlim _graphLock = new();

    internal KnowledgeGraph(Graph graph)
    {
        _graph = graph;
    }

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
        var result = await ExecuteQueryAsync(sparql, cancellationToken).ConfigureAwait(false);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(ExpectedResultSetMessage);
        }

        return ToResult(resultSet);
    }

    public async Task<bool> ExecuteAskAsync(string sparql, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteQueryAsync(sparql, cancellationToken).ConfigureAwait(false);
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

    private async Task<object> ExecuteQueryAsync(string sparql, CancellationToken cancellationToken)
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

        return await Task.Run(() => ProcessQuery(query, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private void MergeSnapshot(Graph graph, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _graphLock.EnterWriteLock();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _graph.Merge(graph);
        }
        finally
        {
            _graphLock.ExitWriteLock();
        }
    }

    private Graph CreateSnapshot()
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

    private object ProcessQuery(SparqlQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _graphLock.EnterReadLock();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dataset = new InMemoryDataset(_graph);
            var processor = new LeviathanQueryProcessor(dataset);
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
                var node = result.Value(variable);
                if (node is not null)
                {
                    values[variable] = RenderNode(node);
                }
            }

            rows.Add(new SparqlRow(values));
        }

        return new SparqlQueryResult(variables, rows);
    }

    private static string RenderNode(INode node)
    {
        return node switch
        {
            IUriNode uriNode => uriNode.Uri.AbsoluteUri,
            ILiteralNode literalNode => literalNode.Value,
            IBlankNode blankNode => BlankNodePrefix + blankNode.InternalID,
            _ => node.ToString(),
        };
    }

    private static string EscapeSparqlLiteral(string value)
    {
        return value.Replace(BackslashText, EscapedBackslashText, StringComparison.Ordinal)
            .Replace(QuoteText, EscapedQuoteText, StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
