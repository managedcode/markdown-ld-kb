using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphProvenanceNormalizer
{
    public static KnowledgeExtractionResult Normalize(
        KnowledgeExtractionResult facts,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        return facts with
        {
            Entities = facts.Entities.Select(entity => NormalizeEntity(entity, warnings)).ToList(),
            Assertions = facts.Assertions.Select(assertion => NormalizeAssertion(assertion, warnings)).ToList(),
        };
    }

    private static KnowledgeEntityFact NormalizeEntity(
        KnowledgeEntityFact entity,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var sources = NormalizeSources(
            entity.Id ?? string.Empty,
            entity.Source,
            entity.Sources,
            entity.Confidence,
            warnings);
        return entity with
        {
            Source = sources.FirstOrDefault() ?? string.Empty,
            Sources = sources,
        };
    }

    private static KnowledgeAssertionFact NormalizeAssertion(
        KnowledgeAssertionFact assertion,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var sources = NormalizeSources(
            assertion.SubjectId,
            assertion.Source,
            assertion.Sources,
            assertion.Confidence,
            warnings);
        return assertion with
        {
            Source = sources.FirstOrDefault() ?? string.Empty,
            Sources = sources,
        };
    }

    private static List<string> NormalizeSources(
        string subjectId,
        string? primarySource,
        IReadOnlyList<string>? additionalSources,
        double confidence,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var sources = EnumerateSources(primarySource, additionalSources)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        var valid = new List<string>(sources.Count);
        foreach (var source in sources)
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out _))
            {
                valid.Add(source);
                continue;
            }

            warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                KnowledgeGraphNormalizationWarningCode.InvalidProvenanceRemoved,
                CreateProvenanceEdge(subjectId, source, confidence)));
        }

        return valid;
    }

    private static IEnumerable<string> EnumerateSources(
        string? primarySource,
        IReadOnlyList<string>? additionalSources)
    {
        if (!string.IsNullOrWhiteSpace(primarySource))
        {
            yield return primarySource;
        }

        foreach (var source in additionalSources ?? [])
        {
            if (!string.IsNullOrWhiteSpace(source))
            {
                yield return source;
            }
        }
    }

    private static KnowledgeAssertionFact CreateProvenanceEdge(
        string subjectId,
        string source,
        double confidence)
    {
        return new KnowledgeAssertionFact
        {
            SubjectId = subjectId,
            Predicate = ProvWasDerivedFromUri.AbsoluteUri,
            ObjectId = source,
            Confidence = confidence,
        };
    }
}
