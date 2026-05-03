namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBm25SearchResults
{
    internal static void AddBoundedMatch(
        List<KnowledgeGraphRankedSearchMatch> matches,
        KnowledgeGraphRankedSearchMatch match,
        int maxResults)
    {
        var insertIndex = FindInsertIndex(matches, match);
        if (insertIndex < 0)
        {
            AddTailMatch(matches, match, maxResults);
            return;
        }

        matches.Insert(insertIndex, match);
        if (matches.Count > maxResults)
        {
            matches.RemoveAt(matches.Count - 1);
        }
    }

    internal static KnowledgeGraphRankedSearchMatch[] ToArray(IReadOnlyList<KnowledgeGraphRankedSearchMatch> matches)
    {
        var results = new KnowledgeGraphRankedSearchMatch[matches.Count];
        for (var index = 0; index < matches.Count; index++)
        {
            results[index] = matches[index];
        }

        return results;
    }

    private static void AddTailMatch(
        List<KnowledgeGraphRankedSearchMatch> matches,
        KnowledgeGraphRankedSearchMatch match,
        int maxResults)
    {
        if (matches.Count < maxResults)
        {
            matches.Add(match);
        }
    }

    private static int FindInsertIndex(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> matches,
        KnowledgeGraphRankedSearchMatch match)
    {
        for (var index = 0; index < matches.Count; index++)
        {
            if (CompareMatches(match, matches[index]) < 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int CompareMatches(KnowledgeGraphRankedSearchMatch left, KnowledgeGraphRankedSearchMatch right)
    {
        var scoreComparison = right.Score.CompareTo(left.Score);
        return scoreComparison != 0
            ? scoreComparison
            : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
    }
}
