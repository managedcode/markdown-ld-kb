using VDS.RDF.Dynamic;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static readonly Uri DefaultDynamicSubjectBaseUri = new(DefaultBaseUriText, UriKind.Absolute);

    public DynamicGraph ToDynamicSnapshot(
        Uri? subjectBaseUri = null,
        Uri? predicateBaseUri = null)
    {
        var snapshot = CreateSnapshot();
        return snapshot.AsDynamic(
            subjectBaseUri ?? snapshot.BaseUri ?? DefaultDynamicSubjectBaseUri,
            predicateBaseUri ?? SchemaNamespaceUri);
    }
}
