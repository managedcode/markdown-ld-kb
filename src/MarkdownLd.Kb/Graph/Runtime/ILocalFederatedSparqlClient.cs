using VDS.RDF;
using VDS.RDF.Query;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal interface ILocalFederatedSparqlClient
{
    Task<SparqlResultSet> ExecuteResultSetAsync(string sparqlQuery, CancellationToken cancellationToken);

    Task StreamResultSetAsync(
        string sparqlQuery,
        ISparqlResultsHandler resultsHandler,
        CancellationToken cancellationToken);
}
