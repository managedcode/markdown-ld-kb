using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed class SparqlQueryExecutor
{
    private const string QueryRejectedMessage = "Query rejected";
    private const string OnlySelectAndAskQueriesAllowedMessage = "Only SELECT and ASK queries are allowed";
    private const string ReadOnlyExecutorExpectedResultsSetMessage = "Read-only executor expected a SPARQL results set";
    private const string UnexpectedSparqlResultTypeMessage = "Unexpected SPARQL result type";

    private readonly SparqlQueryParser _parser;
    private readonly LeviathanQueryProcessor _processor;

    public SparqlQueryExecutor(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var store = CreateStore(graph);
        _parser = new SparqlQueryParser();
        _processor = new LeviathanQueryProcessor(store, options =>
        {
            options.QueryExecutionTimeout = 30_000;
        });
    }

    public SparqlQueryResult ExecuteReadOnly(string query)
    {
        var safety = SparqlSafety.EnforceReadOnly(query);
        if (!safety.IsAllowed)
        {
            throw new InvalidOperationException(safety.ErrorMessage ?? QueryRejectedMessage);
        }

        var parsed = _parser.ParseFromString(safety.Query);
        if (!SparqlSafety.IsReadOnlyQuery(parsed.QueryType))
        {
            throw new InvalidOperationException(OnlySelectAndAskQueriesAllowedMessage);
        }

        var result = _processor.ProcessQuery(parsed);
        return result switch
        {
            SparqlResultSet resultSet => SparqlResultMapper.Map(resultSet),
            IGraph => throw new InvalidOperationException(ReadOnlyExecutorExpectedResultsSetMessage),
            _ => throw new InvalidOperationException(UnexpectedSparqlResultTypeMessage)
        };
    }

    public SparqlResultSet ExecuteRawReadOnly(string query)
    {
        var safety = SparqlSafety.EnforceReadOnly(query);
        if (!safety.IsAllowed)
        {
            throw new InvalidOperationException(safety.ErrorMessage ?? QueryRejectedMessage);
        }

        var parsed = _parser.ParseFromString(safety.Query);
        if (!SparqlSafety.IsReadOnlyQuery(parsed.QueryType))
        {
            throw new InvalidOperationException(OnlySelectAndAskQueriesAllowedMessage);
        }

        if (_processor.ProcessQuery(parsed) is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(ReadOnlyExecutorExpectedResultsSetMessage);
        }

        return resultSet;
    }

    private static IInMemoryQueryableStore CreateStore(IGraph graph)
    {
        var store = new TripleStore();
        store.Add(graph);
        return store;
    }
}
