using VDS.RDF;
using VDS.RDF.Parsing.Tokens;
using VDS.RDF.Query;

using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class KnowledgeGraphFederatedLocalServiceRegistry
{
    private readonly IReadOnlyDictionary<string, KnowledgeGraph> _graphs;
    private readonly IReadOnlyList<FederatedSparqlRemoteServiceBinding> _remote;
    private readonly Lazy<IReadOnlyDictionary<string, ILocalFederatedSparqlClient>> _clients;
    private readonly int _queryExecutionTimeoutMilliseconds;

    private KnowledgeGraphFederatedLocalServiceRegistry(
        IReadOnlyDictionary<string, KnowledgeGraph> graphs,
        int queryExecutionTimeoutMilliseconds,
        IReadOnlyList<FederatedSparqlRemoteServiceBinding> remote)
    {
        _remote = remote;
        _graphs = graphs;
        _queryExecutionTimeoutMilliseconds = queryExecutionTimeoutMilliseconds;
        _clients = new Lazy<IReadOnlyDictionary<string, ILocalFederatedSparqlClient>>(CreateClients, isThreadSafe: true);
    }

    public static KnowledgeGraphFederatedLocalServiceRegistry? Create(
        IReadOnlyList<FederatedSparqlLocalServiceBinding> bindings,
        int queryExecutionTimeoutMilliseconds,
        IReadOnlyList<FederatedSparqlRemoteServiceBinding>? remote = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        remote ??= [];

        if (bindings.Count == 0 && remote.Count == 0)
        {
            return null;
        }

        var graphs = new Dictionary<string, KnowledgeGraph>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            ArgumentNullException.ThrowIfNull(binding.EndpointUri);
            ArgumentNullException.ThrowIfNull(binding.Graph);

            if (!graphs.TryAdd(binding.EndpointUri.AbsoluteUri, binding.Graph))
            {
                throw new InvalidOperationException(
                    DuplicateFederatedLocalServiceBindingMessagePrefix + binding.EndpointUri.AbsoluteUri);
            }
        }

        var ids = graphs.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var binding in remote)
        {
            ArgumentNullException.ThrowIfNull(binding.EndpointUri);
            ArgumentNullException.ThrowIfNull(binding.HttpClient);
            if (!ids.Add(binding.EndpointUri.AbsoluteUri))
            {
                throw new InvalidOperationException(DuplicateFederatedLocalServiceBindingMessagePrefix + binding.EndpointUri.AbsoluteUri);
            }
        }
        return new KnowledgeGraphFederatedLocalServiceRegistry(graphs, queryExecutionTimeoutMilliseconds, remote.ToArray());
    }

    public bool TryResolve(
        IToken endpointSpecifier,
        SparqlEvaluationContext context,
        out ILocalFederatedSparqlClient client)
    {
        ArgumentNullException.ThrowIfNull(endpointSpecifier);
        ArgumentNullException.ThrowIfNull(context);

        client = default!;
        if (!TryResolveEndpointUri(endpointSpecifier, context, out var endpointUri))
        {
            return false;
        }

        if (_clients.Value.TryGetValue(endpointUri.AbsoluteUri, out var resolvedClient))
        {
            client = resolvedClient;
            return true;
        }

        return false;
    }

    private IReadOnlyDictionary<string, ILocalFederatedSparqlClient> CreateClients()
    {
        var clients = _graphs.ToDictionary(
            static pair => pair.Key,
            pair => (ILocalFederatedSparqlClient)new LocalKnowledgeGraphSparqlQueryClient(
                pair.Value,
                this,
                _queryExecutionTimeoutMilliseconds),
            StringComparer.Ordinal);
        foreach (var binding in _remote)
        {
            clients.Add(binding.EndpointUri.AbsoluteUri, new RemoteClient(new SparqlQueryClient(binding.HttpClient, binding.EndpointUri)));
        }
        return clients;
    }

    private sealed class RemoteClient(SparqlQueryClient client) : ILocalFederatedSparqlClient
    {
        public Task<SparqlResultSet> ExecuteResultSetAsync(string sparqlQuery, CancellationToken cancellationToken) =>
            client.QueryWithResultSetAsync(sparqlQuery, cancellationToken);
    }

    private static bool TryResolveEndpointUri(
        IToken endpointSpecifier,
        SparqlEvaluationContext context,
        out Uri endpointUri)
    {
        endpointUri = default!;
        if (endpointSpecifier.TokenType != Token.URI)
        {
            return false;
        }

        var baseUri = context.Query.BaseUri?.AbsoluteUri ?? string.Empty;
        endpointUri = context.UriFactory.Create(Tools.ResolveUri(endpointSpecifier.Value, baseUri));
        return true;
    }
}
