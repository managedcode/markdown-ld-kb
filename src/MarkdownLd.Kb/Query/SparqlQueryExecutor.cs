using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Parsing;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed class SparqlQueryExecutor
{
    private readonly IInMemoryQueryableStore _store;
    private readonly SparqlQueryParser _parser;
    private readonly LeviathanQueryProcessor _processor;

    public SparqlQueryExecutor(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        _store = graph.AsTripleStore();
        _parser = new SparqlQueryParser();
        _processor = new LeviathanQueryProcessor(_store, options =>
        {
            options.QueryExecutionTimeout = 30_000;
        });
    }

    public SparqlQueryResult ExecuteReadOnly(string query)
    {
        var safety = SparqlSafety.EnforceReadOnly(query);
        if (!safety.IsAllowed)
        {
            throw new InvalidOperationException(safety.ErrorMessage ?? "Query rejected");
        }

        var parsed = _parser.ParseFromString(safety.Query);
        if (!SparqlSafety.IsReadOnlyQuery(parsed.QueryType))
        {
            throw new InvalidOperationException("Only SELECT and ASK queries are allowed");
        }

        var result = _processor.ProcessQuery(parsed);
        return result switch
        {
            SparqlResultSet resultSet => SparqlResultMapper.Map(resultSet),
            IGraph => throw new InvalidOperationException("Read-only executor expected a SPARQL results set"),
            _ => throw new InvalidOperationException("Unexpected SPARQL result type")
        };
    }

    public SparqlResultSet ExecuteRawReadOnly(string query)
    {
        var safety = SparqlSafety.EnforceReadOnly(query);
        if (!safety.IsAllowed)
        {
            throw new InvalidOperationException(safety.ErrorMessage ?? "Query rejected");
        }

        var parsed = _parser.ParseFromString(safety.Query);
        if (!SparqlSafety.IsReadOnlyQuery(parsed.QueryType))
        {
            throw new InvalidOperationException("Only SELECT and ASK queries are allowed");
        }

        if (_processor.ProcessQuery(parsed) is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException("Read-only executor expected a SPARQL results set");
        }

        return resultSet;
    }
}
