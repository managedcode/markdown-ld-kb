namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed record KnowledgeGraphBuildOptions
{
    public static KnowledgeGraphBuildOptions Default { get; } = new();

    public bool IncludeFrontMatterRules { get; init; } = true;

    public IReadOnlyList<KnowledgeGraphEntityRule> Entities { get; init; } = [];

    public IReadOnlyList<KnowledgeGraphEdgeRule> Edges { get; init; } = [];
}

public sealed record KnowledgeGraphEntityRule
{
    public string? Id { get; init; }

    public string Label { get; init; } = string.Empty;

    public string Type { get; init; } = PipelineConstants.DefaultSchemaThing;

    public IReadOnlyList<string> SameAs { get; init; } = [];

    public double Confidence { get; init; } = 1d;

    public string Source { get; init; } = string.Empty;
}

public sealed record KnowledgeGraphEdgeRule
{
    public string SubjectId { get; init; } = string.Empty;

    public string Predicate { get; init; } = PipelineConstants.KbRelatedTo;

    public string ObjectId { get; init; } = string.Empty;

    public double Confidence { get; init; } = 1d;

    public string Source { get; init; } = string.Empty;
}

public sealed record KnowledgeGraphFocusedSearchOptions
{
    public int MaxPrimaryResults { get; init; } = 3;

    public int MaxRelatedResults { get; init; } = 6;

    public int MaxNextStepResults { get; init; } = 6;

    public KnowledgeGraphSemanticIndex? SemanticIndex { get; init; }
}

public sealed record KnowledgeGraphFocusedSearchResult(
    IReadOnlyList<KnowledgeGraphFocusedSearchMatch> PrimaryMatches,
    IReadOnlyList<KnowledgeGraphFocusedSearchMatch> RelatedMatches,
    IReadOnlyList<KnowledgeGraphFocusedSearchMatch> NextStepMatches,
    KnowledgeGraphSnapshot FocusedGraph);

public sealed record KnowledgeGraphFocusedSearchMatch(
    string NodeId,
    string Label,
    KnowledgeGraphFocusedSearchRole Role,
    double Score,
    string? SourceNodeId = null,
    string? ViaPredicateLabel = null);

public enum KnowledgeGraphFocusedSearchRole
{
    Primary,
    Related,
    NextStep,
}
