using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphBuildProfile
{
    public string Name { get; init; } = DefaultGraphBuildProfileName;

    public KnowledgeGraphBuildOptions BuildOptions { get; init; } = KnowledgeGraphBuildOptions.Default;

    public KnowledgeGraphSchemaSearchProfile SearchProfile { get; init; } = KnowledgeGraphSchemaSearchProfile.Default;

    public string? ShaclShapesTurtle { get; init; }
}

public sealed partial record KnowledgeGraphContract(
    string Name,
    KnowledgeGraphSchemaDescription Schema,
    KnowledgeGraphSchemaSearchProfile SearchProfile,
    KnowledgeGraphSchemaSearchProfileValidation Validation,
    string? ShaclShapesTurtle = null)
{
    public static KnowledgeGraphContract Empty { get; } = new(
        DefaultGraphBuildProfileName,
        KnowledgeGraphSchemaDescription.Empty,
        KnowledgeGraphSchemaSearchProfile.Default,
        KnowledgeGraphSchemaSearchProfileValidation.Empty);
}

public sealed record KnowledgeGraphSchemaDescription(
    IReadOnlyList<KnowledgeGraphSchemaTerm> RdfTypes,
    IReadOnlyList<KnowledgeGraphPredicateDescription> Predicates,
    IReadOnlyList<KnowledgeGraphPredicateDescription> LiteralPredicates,
    IReadOnlyList<KnowledgeGraphPredicateDescription> ResourcePredicates)
{
    public static KnowledgeGraphSchemaDescription Empty { get; } = new([], [], [], []);
}

public sealed record KnowledgeGraphSchemaTerm(
    string Iri,
    string CompactName,
    int Count);

public sealed record KnowledgeGraphPredicateDescription(
    string Iri,
    string CompactName,
    int TripleCount,
    int LiteralObjectCount,
    int ResourceObjectCount);

public sealed record KnowledgeGraphSchemaSearchProfileValidation(
    bool IsValid,
    IReadOnlyList<KnowledgeGraphSchemaSearchProfileIssue> Issues)
{
    public static KnowledgeGraphSchemaSearchProfileValidation Empty { get; } = new(false, []);
}

public sealed record KnowledgeGraphSchemaSearchProfileIssue(
    KnowledgeGraphSchemaSearchProfileIssueKind Kind,
    string Term,
    string? ResolvedIri,
    string Message);

public enum KnowledgeGraphSchemaSearchProfileIssueKind
{
    UnknownPrefix,
    MissingType,
    MissingTextPredicate,
    MissingRelationshipPredicate,
    MissingRelationshipTargetPredicate,
    MissingExpansionPredicate,
    MissingFacetPredicate,
}
