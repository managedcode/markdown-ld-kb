using ManagedCode.MarkdownLd.Kb.Query;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;

using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class LocalKnowledgeGraphSparqlQueryClient(
    KnowledgeGraph graph,
    KnowledgeGraphFederatedLocalServiceRegistry registry,
    int queryExecutionTimeoutMilliseconds)
    : ILocalFederatedSparqlClient
{
    private readonly KnowledgeGraph _graph = graph;
    private readonly KnowledgeGraphFederatedLocalServiceRegistry _registry = registry;
    private readonly int _queryExecutionTimeoutMilliseconds = queryExecutionTimeoutMilliseconds;

    public Task<SparqlResultSet> ExecuteResultSetAsync(string sparqlQuery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => ExecuteResultSet(sparqlQuery, cancellationToken), cancellationToken);
    }

    public async Task StreamResultSetAsync(
        string sparqlQuery,
        ISparqlResultsHandler resultsHandler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultsHandler);

        var resultSet = await ExecuteResultSetAsync(sparqlQuery, cancellationToken).ConfigureAwait(false);
        WriteResultSet(resultsHandler, resultSet);
    }

    private SparqlResultSet ExecuteResultSet(string sparqlQuery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var safety = SparqlSafety.EnforceReadOnly(sparqlQuery, allowFederatedService: true);
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

        var snapshot = _graph.CreateSnapshot();
        var processor = new KnowledgeGraphFederatedQueryProcessor(
            new InMemoryDataset(snapshot),
            _registry,
            options => options.QueryExecutionTimeout = _queryExecutionTimeoutMilliseconds);
        if (processor.ProcessQuery(query) is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(FederatedLocalResultSetExpectedMessage);
        }

        return resultSet;
    }

    private static void WriteResultSet(ISparqlResultsHandler resultsHandler, SparqlResultSet resultSet)
    {
        resultsHandler.StartResults();
        foreach (var variable in resultSet.Variables)
        {
            if (!resultsHandler.HandleVariable(variable))
            {
                resultsHandler.EndResults(ok: false);
                return;
            }
        }

        if (resultSet.ResultsType == SparqlResultsType.Boolean)
        {
            resultsHandler.HandleBooleanResult(resultSet.Result);
            resultsHandler.EndResults(ok: true);
            return;
        }

        foreach (var result in resultSet)
        {
            if (!resultsHandler.HandleResult(result))
            {
                resultsHandler.EndResults(ok: false);
                return;
            }
        }

        resultsHandler.EndResults(ok: true);
    }
}
