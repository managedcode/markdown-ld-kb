using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphNamespaces
{
    internal static void Register(IGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        graph.NamespaceMap.AddNamespace(SchemaPrefix, SchemaNamespaceUri);
        graph.NamespaceMap.AddNamespace(KbPrefix, KbNamespaceUri);
        graph.NamespaceMap.AddNamespace(ProvPrefix, ProvNamespaceUri);
        graph.NamespaceMap.AddNamespace(RdfPrefix, RdfNamespaceUri);
        graph.NamespaceMap.AddNamespace(RdfsPrefix, RdfsNamespaceUri);
        graph.NamespaceMap.AddNamespace(OwlPrefix, OwlNamespaceUri);
        graph.NamespaceMap.AddNamespace(SkosPrefix, SkosNamespaceUri);
        graph.NamespaceMap.AddNamespace(XsdPrefix, XsdNamespaceUri);
    }
}
