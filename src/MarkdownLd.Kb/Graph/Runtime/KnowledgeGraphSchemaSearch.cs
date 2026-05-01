using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphSchemaSearchProfile
{
    public static KnowledgeGraphSchemaSearchProfile Default { get; } = new();

    public IReadOnlyDictionary<string, string> Prefixes { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<KnowledgeGraphSchemaTextPredicate> TextPredicates { get; init; } =
    [
        new(ExpectedSchemaName, SchemaSearchDefaultTextWeight),
        new(ExpectedRdfsLabel, SchemaSearchDefaultTextWeight),
        new(ExpectedSkosPrefLabel, SchemaSearchDefaultTextWeight),
        new(ExpectedSkosAltLabel, SchemaSearchDefaultTextWeight),
        new(ExpectedSchemaDescription, SchemaSearchDescriptionWeight),
        new(ExpectedRdfsComment, SchemaSearchDescriptionWeight),
    ];

    public IReadOnlyList<KnowledgeGraphSchemaRelationshipPredicate> RelationshipPredicates { get; init; } =
    [
        new(ExpectedSchemaMentions, [ExpectedSchemaName, ExpectedRdfsLabel, ExpectedSkosPrefLabel], SchemaSearchRelationshipWeight),
        new(ExpectedSchemaAbout, [ExpectedSchemaName, ExpectedRdfsLabel, ExpectedSkosPrefLabel], SchemaSearchRelationshipWeight),
        new(ExpectedKbRelatedTo, [ExpectedSchemaName, ExpectedRdfsLabel, ExpectedSkosPrefLabel], SchemaSearchRelationshipWeight),
    ];

    public IReadOnlyList<KnowledgeGraphSchemaExpansionPredicate> ExpansionPredicates { get; init; } =
    [
        new(ExpectedSchemaMentions, KnowledgeGraphSchemaSearchRole.Related, SchemaSearchRelatedExpansionScore),
        new(ExpectedSchemaAbout, KnowledgeGraphSchemaSearchRole.Related, SchemaSearchRelatedExpansionScore),
        new(ExpectedKbRelatedTo, KnowledgeGraphSchemaSearchRole.Related, SchemaSearchRelatedExpansionScore),
        new(ExpectedKbNextStep, KnowledgeGraphSchemaSearchRole.NextStep, SchemaSearchNextStepExpansionScore),
    ];

    public IReadOnlyList<string> TypeFilters { get; init; } = [];

    public IReadOnlyList<KnowledgeGraphSchemaFacetFilter> FacetFilters { get; init; } = [];

    public IReadOnlyList<string> ExcludedTypes { get; init; } =
    [
        KbMarkdownDocumentTypeText,
        KbKnowledgeConceptTypeText,
        KbKnowledgeConceptSchemeTypeText,
        KbKnowledgeAssertionTypeText,
    ];

    public IReadOnlyList<Uri> FederatedServiceEndpoints { get; init; } = [];

    public KnowledgeGraphSchemaSearchTermMode TermMode { get; init; } = KnowledgeGraphSchemaSearchTermMode.ExactPhrase;

    public int MaxResults { get; init; } = DefaultSchemaSearchMaxResults;

    public int MaxRelatedResults { get; init; } = DefaultSchemaSearchMaxRelatedResults;

    public int MaxNextStepResults { get; init; } = DefaultSchemaSearchMaxNextStepResults;
}

public sealed record KnowledgeGraphSchemaTextPredicate(
    string Predicate,
    double Weight = SchemaSearchDefaultTextWeight);

public sealed record KnowledgeGraphSchemaRelationshipPredicate(
    string Predicate,
    IReadOnlyList<string> TargetTextPredicates,
    double Weight = SchemaSearchRelationshipWeight)
{
    public IReadOnlyList<string> PredicatePath { get; init; } = [];

    public KnowledgeGraphSchemaRelationshipDirection Direction { get; init; } = KnowledgeGraphSchemaRelationshipDirection.Outbound;
}

public sealed record KnowledgeGraphSchemaExpansionPredicate(
    string Predicate,
    KnowledgeGraphSchemaSearchRole Role,
    double Score);

public sealed record KnowledgeGraphSchemaFacetFilter(
    string Predicate,
    string Object);

public sealed record KnowledgeGraphSchemaSearchResult(
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> Matches,
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> RelatedMatches,
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> NextStepMatches,
    KnowledgeGraphSnapshot FocusedGraph,
    string GeneratedSparql,
    string? GeneratedExpansionSparql,
    IReadOnlyList<string> ServiceEndpointSpecifiers,
    KnowledgeGraphSchemaSearchExplain Explain);

public sealed record KnowledgeGraphSchemaSearchMatch(
    string NodeId,
    string Label,
    KnowledgeGraphSchemaSearchRole Role,
    double Score,
    IReadOnlyList<string> Types,
    IReadOnlyList<KnowledgeGraphSchemaSearchEvidence> Evidence,
    string? Description = null,
    string? SourceNodeId = null,
    string? ViaPredicateId = null);

public sealed record KnowledgeGraphSchemaSearchEvidence(
    string PredicateId,
    string MatchedText,
    KnowledgeGraphSchemaSearchEvidenceKind Kind,
    double Score,
    string? RelatedNodeId = null,
    string? RelatedNodeLabel = null,
    string? ViaPredicateId = null,
    string? ServiceEndpoint = null)
{
    public IReadOnlyList<KnowledgeGraphSchemaSearchSourceContext> SourceContexts { get; init; } = [];
}

public sealed record KnowledgeGraphSchemaSearchSourceContext(
    string SourceId,
    string? SourceLabel = null);

public sealed record KnowledgeGraphSchemaSearchExplain(
    string Query,
    KnowledgeGraphSchemaSearchTermMode TermMode,
    IReadOnlyList<string> TypeFilters,
    IReadOnlyList<KnowledgeGraphSchemaTextPredicate> TextPredicates,
    IReadOnlyList<KnowledgeGraphSchemaRelationshipPredicate> RelationshipPredicates,
    IReadOnlyList<KnowledgeGraphSchemaExpansionPredicate> ExpansionPredicates,
    IReadOnlyList<KnowledgeGraphSchemaFacetFilter> FacetFilters,
    string GeneratedSparql,
    string? GeneratedExpansionSparql,
    IReadOnlyList<string> ServiceEndpointSpecifiers);

public enum KnowledgeGraphSchemaSearchRole
{
    Primary,
    Related,
    NextStep,
}

public enum KnowledgeGraphSchemaSearchEvidenceKind
{
    Direct,
    Relationship,
}

public enum KnowledgeGraphSchemaSearchTermMode
{
    ExactPhrase,
    AllTerms,
    AnyTerm,
}

public enum KnowledgeGraphSchemaRelationshipDirection
{
    Outbound,
    Inbound,
}
