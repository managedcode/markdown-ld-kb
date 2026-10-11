using VDS.RDF.Ontology;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    /// <summary>Creates an independent OWL/RDFS snapshot. The caller owns and disposes it; edits do not change the live graph.</summary>
    public OntologyGraph ToOntologySnapshot()
    {
        using var snapshot = CreateSnapshot();
        var ontology = new OntologyGraph(graphName: snapshot.Name);
        ontology.Merge(snapshot);
        return ontology;
    }
}
