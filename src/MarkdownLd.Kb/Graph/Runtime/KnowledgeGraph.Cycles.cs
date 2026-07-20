namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public IReadOnlyList<KnowledgeGraphCycle> FindCycles(KnowledgeGraphCycleSearchOptions? options = null)
    {
        var effectiveOptions = options ?? KnowledgeGraphCycleSearchOptions.Default;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveOptions.MaxComponents);
        ArgumentNullException.ThrowIfNull(effectiveOptions.PredicateIds);

        var predicateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var predicate in effectiveOptions.PredicateIds)
        {
            var predicateUri = KnowledgeGraphPredicateResolver.Resolve(predicate)
                ?? throw new ArgumentException(
                    PipelineConstants.CycleSearchPredicateInvalidMessagePrefix + predicate,
                    nameof(options));

            predicateIds.Add(predicateUri.AbsoluteUri);
        }

        return KnowledgeGraphCycleAnalyzer.FindCycles(ToSnapshot(), predicateIds, effectiveOptions.MaxComponents);
    }
}
