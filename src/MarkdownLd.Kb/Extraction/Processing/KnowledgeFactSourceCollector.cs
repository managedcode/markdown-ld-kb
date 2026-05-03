namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeFactSourceCollector
{
    public static List<string> MergeEntitySources(params KnowledgeEntityFact[] entities)
    {
        return entities
            .SelectMany(EnumerateEntitySources)
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static List<string> MergeAssertionSources(params KnowledgeAssertionFact[] assertions)
    {
        return assertions
            .SelectMany(EnumerateAssertionSources)
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static IEnumerable<string> EnumerateEntitySources(KnowledgeEntityFact entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Source))
        {
            yield return entity.Source;
        }

        foreach (var source in entity.Sources ?? [])
        {
            yield return source;
        }
    }

    public static IEnumerable<string> EnumerateAssertionSources(KnowledgeAssertionFact assertion)
    {
        if (!string.IsNullOrWhiteSpace(assertion.Source))
        {
            yield return assertion.Source;
        }

        foreach (var source in assertion.Sources ?? [])
        {
            yield return source;
        }
    }
}
