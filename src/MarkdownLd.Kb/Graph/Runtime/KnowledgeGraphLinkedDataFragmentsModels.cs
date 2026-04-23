namespace ManagedCode.MarkdownLd.Kb.Pipeline;

/// <summary>
/// Configures read-only Linked Data Fragments materialization into a local <see cref="KnowledgeGraph" />.
/// </summary>
public sealed record KnowledgeGraphLinkedDataFragmentsOptions
{
    public static KnowledgeGraphLinkedDataFragmentsOptions Default { get; } = new();

    /// <summary>
    /// Gets an optional RDF reader override for fragment parsing.
    /// </summary>
    public VDS.RDF.IRdfReader? Reader { get; init; }

    /// <summary>
    /// Gets an optional caller-owned transport for fragment loading.
    /// Host applications may create it directly or obtain it from <c>IHttpClientFactory</c> and pass the configured instance here.
    /// </summary>
    public HttpClient? HttpClient { get; init; }
}
