namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphAcyclicEdgeNormalizer
{
    public static IReadOnlyList<KnowledgeAssertionFact> Normalize(
        IReadOnlyList<KnowledgeAssertionFact> assertions,
        IReadOnlySet<string> acyclicPredicates,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var retained = assertions
            .Where(edge => !acyclicPredicates.Contains(KnowledgeGraphNormalizer.ResolvePredicateId(edge.Predicate)))
            .ToList();
        var candidates = assertions
            .Where(edge => acyclicPredicates.Contains(KnowledgeGraphNormalizer.ResolvePredicateId(edge.Predicate)))
            .OrderByDescending(static edge => edge.Confidence)
            .ThenBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(static edge => KnowledgeGraphNormalizer.ResolvePredicateId(edge.Predicate), StringComparer.Ordinal)
            .ThenBy(static edge => edge.ObjectId, StringComparer.Ordinal);
        var adjacency = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var cyclePath = FindPath(adjacency, candidate.ObjectId, candidate.SubjectId);
            if (cyclePath.Count > 0)
            {
                warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                    KnowledgeGraphNormalizationWarningCode.CycleEdgeRemoved,
                    candidate,
                    cyclePath));
                continue;
            }

            AddEdge(adjacency, candidate.SubjectId, candidate.ObjectId);
            retained.Add(candidate);
        }

        return retained;
    }

    private static void AddEdge(
        IDictionary<string, SortedSet<string>> adjacency,
        string subjectId,
        string objectId)
    {
        if (!adjacency.TryGetValue(subjectId, out var targets))
        {
            targets = new SortedSet<string>(StringComparer.Ordinal);
            adjacency.Add(subjectId, targets);
        }

        targets.Add(objectId);
    }

    private static IReadOnlyList<string> FindPath(
        IReadOnlyDictionary<string, SortedSet<string>> adjacency,
        string start,
        string target)
    {
        var queue = new Queue<string>();
        var parents = new Dictionary<string, string?>(StringComparer.Ordinal) { [start] = null };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Equals(target, StringComparison.Ordinal))
            {
                return ReconstructPath(parents, target);
            }

            EnqueueTargets(adjacency, current, parents, queue);
        }

        return [];
    }

    private static void EnqueueTargets(
        IReadOnlyDictionary<string, SortedSet<string>> adjacency,
        string current,
        IDictionary<string, string?> parents,
        Queue<string> queue)
    {
        if (!adjacency.TryGetValue(current, out var targets))
        {
            return;
        }

        foreach (var target in targets)
        {
            if (parents.TryAdd(target, current))
            {
                queue.Enqueue(target);
            }
        }
    }

    private static IReadOnlyList<string> ReconstructPath(
        IReadOnlyDictionary<string, string?> parents,
        string target)
    {
        var path = new List<string>();
        var current = (string?)target;
        while (current is not null)
        {
            path.Add(current);
            current = parents[current];
        }

        path.Reverse();
        return path;
    }
}
