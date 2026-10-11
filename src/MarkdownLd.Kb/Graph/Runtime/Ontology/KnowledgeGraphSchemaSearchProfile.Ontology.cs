using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial record KnowledgeGraphSchemaSearchProfile
{
    private const string RdfsClass = RdfsNamespaceText + "Class";
    private const string RdfProperty = RdfNamespaceText + "Property";
    private const string OwlClass = OwlNamespaceText + "Class";
    private const string OwlObjectProperty = OwlNamespaceText + "ObjectProperty";
    private const string OwlDatatypeProperty = OwlNamespaceText + "DatatypeProperty";
    private const string OwlAnnotationProperty = OwlNamespaceText + "AnnotationProperty";
    private const string SubClassOf = RdfsNamespaceText + "subClassOf";
    private const string SubPropertyOf = RdfsNamespaceText + "subPropertyOf";
    private const string Domain = RdfsNamespaceText + "domain";
    private const string Range = RdfsNamespaceText + "range";
    private const string EquivalentClass = OwlNamespaceText + "equivalentClass";
    private const string EquivalentProperty = OwlNamespaceText + "equivalentProperty";

    /// <summary>OWL/RDFS class and property discovery, with labels/comments and hierarchy/domain/range evidence. Compatible with candidate VALUES and explicit federation.</summary>
    public static KnowledgeGraphSchemaSearchProfile Ontology { get; } = new()
    {
        TypeFilters = [OwlClass, RdfsClass, RdfProperty, OwlObjectProperty, OwlDatatypeProperty, OwlAnnotationProperty],
        ExcludedTypes = [],
        TextPredicates = [new(RdfsLabelText), new(RdfsCommentText, SchemaSearchDescriptionWeight), new(SkosPrefLabelText), new(SkosAltLabelText), new(SchemaNameText), new(SchemaDescriptionText, SchemaSearchDescriptionWeight)],
        RelationshipPredicates = CreateOntologyRelationships(),
        ExpansionPredicates = CreateOntologyExpansion()
    };

    private static IReadOnlyList<KnowledgeGraphSchemaRelationshipPredicate> CreateOntologyRelationships() =>
        new[] { SubClassOf, SubPropertyOf, Domain, Range, EquivalentClass, EquivalentProperty }
            .Select(static predicate => new KnowledgeGraphSchemaRelationshipPredicate(predicate, [RdfsLabelText, RdfsCommentText, SkosPrefLabelText, SchemaNameText], SchemaSearchRelationshipWeight))
            .ToArray();

    private static IReadOnlyList<KnowledgeGraphSchemaExpansionPredicate> CreateOntologyExpansion() =>
        new[] { SubClassOf, SubPropertyOf, Domain, Range, EquivalentClass, EquivalentProperty }
            .Select(static predicate => new KnowledgeGraphSchemaExpansionPredicate(predicate, KnowledgeGraphSchemaSearchRole.Related, SchemaSearchRelatedExpansionScore))
            .ToArray();
}
