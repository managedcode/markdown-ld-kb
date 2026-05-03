using ManagedCode.MarkdownLd.Kb.Query;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private const string UnsupportedServiceSpecifierMessagePrefix = "Federated SERVICE specifiers must be absolute endpoint URIs at the local execution boundary: ";
    private const string UnallowlistedServiceEndpointMessagePrefix = "Federated SERVICE endpoint is not allowlisted: ";

    public async Task<FederatedSparqlSelectResult> ExecuteFederatedSelectAsync(
        string sparql,
        FederatedSparqlExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prepared = PrepareFederatedQuery(
            sparql,
            options,
            SparqlSafety.IsSelectQuery,
            ExecuteFederatedSelectRequiresSelectQueryMessage);
        var localServices = KnowledgeGraphFederatedLocalServiceRegistry.Create(
            prepared.Options.LocalServiceBindings,
            prepared.Options.QueryExecutionTimeoutMilliseconds);
        var result = await Task.Run(
                () => ProcessFederatedQuery(prepared.Query, localServices, cancellationToken, prepared.Options.QueryExecutionTimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(ExpectedResultSetMessage);
        }

        return new FederatedSparqlSelectResult(ToResult(resultSet), prepared.ServiceEndpointSpecifiers);
    }

    public async Task<FederatedSparqlAskResult> ExecuteFederatedAskAsync(
        string sparql,
        FederatedSparqlExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prepared = PrepareFederatedQuery(
            sparql,
            options,
            static queryType => queryType == SparqlQueryType.Ask,
            ExecuteFederatedAskRequiresAskQueryMessage);
        var localServices = KnowledgeGraphFederatedLocalServiceRegistry.Create(
            prepared.Options.LocalServiceBindings,
            prepared.Options.QueryExecutionTimeoutMilliseconds);
        var result = await Task.Run(
                () => ProcessFederatedQuery(prepared.Query, localServices, cancellationToken, prepared.Options.QueryExecutionTimeoutMilliseconds),
                cancellationToken)
            .ConfigureAwait(false);
        if (result is not SparqlResultSet resultSet)
        {
            throw new InvalidOperationException(ExpectedResultSetMessage);
        }

        return new FederatedSparqlAskResult(resultSet.Result, prepared.ServiceEndpointSpecifiers);
    }

    private static FederatedPreparedQuery PrepareFederatedQuery(
        string sparql,
        FederatedSparqlExecutionOptions? options,
        Func<SparqlQueryType, bool>? expectedQueryType,
        string? expectedQueryTypeMessage)
    {
        var effectiveOptions = options ?? FederatedSparqlExecutionOptions.Default;
        var safety = SparqlSafety.EnforceReadOnly(sparql, allowFederatedService: true);
        if (!safety.IsAllowed)
        {
            throw new ReadOnlySparqlQueryException(safety.ErrorMessage ?? ReadOnlySparqlQueryMessage);
        }

        var parser = new SparqlQueryParser();
        var query = parser.ParseFromString(safety.Query);
        EnsureExpectedQueryType(query.QueryType, expectedQueryType, expectedQueryTypeMessage);
        var serviceClauses = SparqlSafety.GetAllServiceClauses(query);

        EnsureSupportedServiceSpecifiers(serviceClauses);
        EnsureAllowlistedEndpoints(serviceClauses, effectiveOptions);
        return new FederatedPreparedQuery(
            query,
            effectiveOptions,
            serviceClauses
                .Select(static clause => clause.SpecifierText)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private static void EnsureSupportedServiceSpecifiers(IReadOnlyList<SparqlServiceClause> serviceClauses)
    {
        var unsupportedSpecifiers = serviceClauses
            .Where(static clause => clause.IsVariableSpecifier || clause.ServiceEndpointUri is null)
            .Select(static clause => clause.SpecifierText)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unsupportedSpecifiers.Length == 0)
        {
            return;
        }

        throw new FederatedSparqlQueryException(
            UnsupportedServiceSpecifierMessagePrefix + string.Join(SupportedExtensionsSeparator, unsupportedSpecifiers),
            unsupportedSpecifiers);
    }

    private static void EnsureAllowlistedEndpoints(
        IReadOnlyList<SparqlServiceClause> serviceClauses,
        FederatedSparqlExecutionOptions options)
    {
        var allowlist = options.AllowedServiceEndpoints
            .Select(static endpoint => endpoint.AbsoluteUri)
            .ToHashSet(StringComparer.Ordinal);
        var unallowlistedEndpoints = serviceClauses
            .Where(static clause => clause.ServiceEndpointUri is not null)
            .Select(static clause => clause.ServiceEndpointUri!.AbsoluteUri)
            .Where(endpoint => !allowlist.Contains(endpoint))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (unallowlistedEndpoints.Length == 0)
        {
            return;
        }

        throw new FederatedSparqlQueryException(
            UnallowlistedServiceEndpointMessagePrefix + string.Join(SupportedExtensionsSeparator, unallowlistedEndpoints),
            unallowlistedEndpoints);
    }

    private object ProcessFederatedQuery(
        SparqlQuery query,
        KnowledgeGraphFederatedLocalServiceRegistry? localServices,
        CancellationToken cancellationToken,
        int queryExecutionTimeoutMilliseconds)
    {
        if (localServices is null)
        {
            return ProcessQuery(query, cancellationToken, queryExecutionTimeoutMilliseconds);
        }

        return ProcessFederatedLocalQuery(
            query,
            localServices,
            cancellationToken,
            queryExecutionTimeoutMilliseconds);
    }

    private sealed record FederatedPreparedQuery(
        SparqlQuery Query,
        FederatedSparqlExecutionOptions Options,
        IReadOnlyList<string> ServiceEndpointSpecifiers);
}
