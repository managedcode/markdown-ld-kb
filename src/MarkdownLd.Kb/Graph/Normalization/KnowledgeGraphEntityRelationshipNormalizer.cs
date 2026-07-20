using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphEntityRelationshipNormalizer
{
    public static List<KnowledgeEntityFact> Normalize(
        IReadOnlyList<KnowledgeEntityFact> entities,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var normalized = new List<KnowledgeEntityFact>(entities.Count);
        foreach (var entity in entities)
        {
            var sameAs = NormalizeSameAs(entity, warnings);
            normalized.Add(entity with { SameAs = sameAs });
        }

        return normalized;
    }

    private static List<string> NormalizeSameAs(
        KnowledgeEntityFact entity,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var normalized = new List<string>(entity.SameAs.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sameAs in entity.SameAs)
        {
            var edge = CreateSameAsEdge(entity, sameAs);
            var code = ResolveWarningCode(entity.Id, sameAs, seen);
            if (code is not null)
            {
                warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(code.Value, edge));
                continue;
            }

            normalized.Add(sameAs);
        }

        normalized.Sort(StringComparer.Ordinal);
        return normalized;
    }

    private static KnowledgeGraphNormalizationWarningCode? ResolveWarningCode(
        string? entityId,
        string sameAs,
        ISet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(sameAs) || !Uri.TryCreate(sameAs, UriKind.Absolute, out _))
        {
            return KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved;
        }

        if (sameAs.Equals(entityId, StringComparison.Ordinal))
        {
            return KnowledgeGraphNormalizationWarningCode.SelfLoopRemoved;
        }

        return seen.Add(sameAs)
            ? null
            : KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved;
    }

    private static KnowledgeAssertionFact CreateSameAsEdge(
        KnowledgeEntityFact entity,
        string sameAs)
    {
        return new KnowledgeAssertionFact
        {
            SubjectId = entity.Id ?? string.Empty,
            Predicate = SchemaSameAsText,
            ObjectId = sameAs,
            Confidence = entity.Confidence,
            Source = entity.Source,
            Sources = entity.Sources,
        };
    }
}
