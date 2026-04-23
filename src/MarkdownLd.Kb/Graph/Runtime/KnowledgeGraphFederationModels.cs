using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record FederatedSparqlExecutionOptions
{
    public static FederatedSparqlExecutionOptions Default { get; } = new();

    public IReadOnlyList<Uri> AllowedServiceEndpoints { get; init; } = [];

    public IReadOnlyList<FederatedSparqlLocalServiceBinding> LocalServiceBindings { get; init; } = [];

    public int QueryExecutionTimeoutMilliseconds { get; init; } = DefaultFederatedSparqlTimeoutMilliseconds;
}

public static class FederatedSparqlProfiles
{
    public static FederatedSparqlExecutionOptions WikidataMain { get; } = Create(WikidataMainSparqlEndpointUri);

    public static FederatedSparqlExecutionOptions WikidataScholarly { get; } = Create(WikidataScholarlySparqlEndpointUri);

    public static FederatedSparqlExecutionOptions WikidataMainAndScholarly { get; } =
        Create(WikidataMainSparqlEndpointUri, WikidataScholarlySparqlEndpointUri);

    private static FederatedSparqlExecutionOptions Create(params Uri[] endpoints)
    {
        return new FederatedSparqlExecutionOptions
        {
            AllowedServiceEndpoints = endpoints,
        };
    }
}

public sealed record FederatedSparqlSelectResult(
    SparqlQueryResult Result,
    IReadOnlyList<string> ServiceEndpointSpecifiers);

public sealed record FederatedSparqlAskResult(
    bool Result,
    IReadOnlyList<string> ServiceEndpointSpecifiers);

public sealed record FederatedSparqlLocalServiceBinding(
    Uri EndpointUri,
    KnowledgeGraph Graph);

public sealed class FederatedSparqlQueryException(string message, IReadOnlyList<string> serviceEndpointSpecifiers)
    : InvalidOperationException(message)
{
    public IReadOnlyList<string> ServiceEndpointSpecifiers { get; } = serviceEndpointSpecifiers;
}
