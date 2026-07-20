namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphNormalizationWarningFactory
{
    private const string WarningPrefix = "Graph normalization warning: ";
    private const string DuplicateReason = "duplicate edge removed";
    private const string InvalidReason = "invalid edge removed";
    private const string SelfLoopReason = "self-loop removed";
    private const string SymmetricDuplicateReason = "symmetric duplicate edge removed";
    private const string CycleReason = "cycle-causing edge removed";
    private const string InvalidProvenanceReason = "invalid provenance edge removed";
    private const string ConfidenceNormalizedReason = "fact confidence normalized to one";
    private const string InvalidNodeReason = "invalid node removed";
    private const string ReasonSeparator = ": ";
    private const string EdgeConnector = " --";
    private const string EdgeArrow = "--> ";
    private const string SentenceEnd = ".";

    public static KnowledgeGraphNormalizationWarning Create(
        KnowledgeGraphNormalizationWarningCode code,
        KnowledgeAssertionFact edge,
        IReadOnlyList<string>? cyclePath = null)
    {
        return new KnowledgeGraphNormalizationWarning(
            code,
            CreateMessage(code, edge),
            edge)
        {
            CyclePath = cyclePath ?? [],
        };
    }

    public static KnowledgeGraphNormalizationWarning CreateForEntity(
        KnowledgeGraphNormalizationWarningCode code,
        KnowledgeEntityFact entity)
    {
        return Create(code, new KnowledgeAssertionFact
        {
            SubjectId = entity.Id ?? entity.Label,
            Predicate = PipelineConstants.RdfTypeUri.AbsoluteUri,
            ObjectId = entity.Type,
            Confidence = entity.Confidence,
        });
    }

    private static string CreateMessage(
        KnowledgeGraphNormalizationWarningCode code,
        KnowledgeAssertionFact edge)
    {
        return WarningPrefix +
               ResolveReason(code) +
               ReasonSeparator +
               edge.SubjectId +
               EdgeConnector +
               edge.Predicate +
               EdgeArrow +
               edge.ObjectId +
               SentenceEnd;
    }

    private static string ResolveReason(KnowledgeGraphNormalizationWarningCode code)
    {
        return code switch
        {
            KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved => DuplicateReason,
            KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved => InvalidReason,
            KnowledgeGraphNormalizationWarningCode.SelfLoopRemoved => SelfLoopReason,
            KnowledgeGraphNormalizationWarningCode.SymmetricDuplicateEdgeRemoved => SymmetricDuplicateReason,
            KnowledgeGraphNormalizationWarningCode.CycleEdgeRemoved => CycleReason,
            KnowledgeGraphNormalizationWarningCode.InvalidProvenanceRemoved => InvalidProvenanceReason,
            KnowledgeGraphNormalizationWarningCode.ConfidenceNormalized => ConfidenceNormalizedReason,
            KnowledgeGraphNormalizationWarningCode.InvalidNodeRemoved => InvalidNodeReason,
            _ => InvalidReason,
        };
    }
}

internal sealed record KnowledgeFactMergeResult(
    KnowledgeExtractionResult Facts,
    IReadOnlyList<KnowledgeGraphNormalizationWarning> Warnings);
