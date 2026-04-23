using VDS.RDF;
using VDS.RDF.LDF.Client;
using VDS.RDF.Parsing;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public static Task<KnowledgeGraph> LoadFromLinkedDataFragmentsAsync(
        Uri endpointUri,
        KnowledgeGraphLinkedDataFragmentsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(
            () => LoadFromLinkedDataFragments(endpointUri, options ?? KnowledgeGraphLinkedDataFragmentsOptions.Default),
            cancellationToken);
    }

    private static KnowledgeGraph LoadFromLinkedDataFragments(
        Uri endpointUri,
        KnowledgeGraphLinkedDataFragmentsOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpointUri);
        ArgumentNullException.ThrowIfNull(options);

        var loader = CreateLoader(options);
        using var liveGraph = new TpfLiveGraph(endpointUri, options.Reader, loader);
        var materializedGraph = new Graph();
        materializedGraph.Merge(liveGraph);
        return new KnowledgeGraph(materializedGraph);
    }

    private static Loader? CreateLoader(KnowledgeGraphLinkedDataFragmentsOptions options)
    {
        return options.HttpClient is null
            ? null
            : new Loader(options.HttpClient);
    }
}
