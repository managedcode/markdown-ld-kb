namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public enum KnowledgeGraphNormalizationWarningCode
{
    DuplicateEdgeRemoved,
    InvalidEdgeRemoved,
    SelfLoopRemoved,
    SymmetricDuplicateEdgeRemoved,
    CycleEdgeRemoved,
    InvalidProvenanceRemoved,
    ConfidenceNormalized,
    InvalidNodeRemoved,
}

public sealed record KnowledgeGraphNormalizationWarning(
    KnowledgeGraphNormalizationWarningCode Code,
    string Message,
    KnowledgeAssertionFact Edge)
{
    public IReadOnlyList<string> CyclePath { get; init; } = [];
}

public sealed record KnowledgeGraphNormalizationReport(
    IReadOnlyList<KnowledgeGraphNormalizationWarning> Warnings)
{
    public static KnowledgeGraphNormalizationReport Empty { get; } = new([]);

    public bool HasWarnings => Warnings.Count > 0;
}

public sealed record KnowledgeGraphNormalizationResult(
    KnowledgeExtractionResult Facts,
    KnowledgeGraphNormalizationReport Report);

public sealed record KnowledgeGraphMaterializationResult(
    KnowledgeExtractionResult Facts,
    KnowledgeGraph Graph,
    KnowledgeGraphNormalizationReport Normalization);

public sealed record KnowledgeGraphNormalizationOptions
{
    public static KnowledgeGraphNormalizationOptions Default { get; } = new();

    public IReadOnlyList<string> AcyclicPredicates { get; init; } = [PipelineConstants.KbNextStep];
}
