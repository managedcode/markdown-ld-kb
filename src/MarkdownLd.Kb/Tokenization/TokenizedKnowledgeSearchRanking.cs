namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class TokenizedKnowledgeSearchRanking
{
    private static readonly Comparison<TokenDistanceCandidate> DistanceCandidateComparison = CompareDistanceCandidates;
    private static readonly Comparison<FuzzyCorrectionCandidate> CorrectionCandidateComparison = CompareCorrectionCandidates;

    internal static List<TokenDistanceCandidate> FindNearestSegments(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        TokenVector queryVector,
        int limit)
    {
        var candidates = new List<TokenDistanceCandidate>(limit);
        foreach (var segment in segments)
        {
            AddDistanceCandidate(
                candidates,
                new TokenDistanceCandidate(segment, queryVector.EuclideanDistanceTo(segment.Vector)),
                limit);
        }

        return candidates;
    }

    internal static TokenDistanceSearchResult[] CreateResults(IReadOnlyList<TokenDistanceCandidate> candidates)
    {
        var results = new TokenDistanceSearchResult[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            results[index] = new TokenDistanceSearchResult(
                candidate.Segment.Id,
                candidate.Segment.DocumentId,
                candidate.Segment.Text,
                candidate.Distance);
        }

        return results;
    }

    internal static void AddCorrectionCandidate(
        List<FuzzyCorrectionCandidate> candidates,
        FuzzyCorrectionCandidate candidate,
        int limit)
    {
        AddBoundedCandidate(candidates, candidate, limit, CorrectionCandidateComparison);
    }

    internal static string[] CreateCorrectionValues(IReadOnlyList<FuzzyCorrectionCandidate> candidates)
    {
        var values = new string[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            values[index] = candidates[index].Term.Value;
        }

        return values;
    }

    private static void AddDistanceCandidate(
        List<TokenDistanceCandidate> candidates,
        TokenDistanceCandidate candidate,
        int limit)
    {
        AddBoundedCandidate(candidates, candidate, limit, DistanceCandidateComparison);
    }

    private static void AddBoundedCandidate<T>(
        List<T> candidates,
        T candidate,
        int limit,
        Comparison<T> comparison)
    {
        var insertIndex = FindInsertIndex(candidates, candidate, comparison);
        if (insertIndex < 0)
        {
            AddTailCandidate(candidates, candidate, limit);
            return;
        }

        candidates.Insert(insertIndex, candidate);
        if (candidates.Count > limit)
        {
            candidates.RemoveAt(candidates.Count - 1);
        }
    }

    private static void AddTailCandidate<T>(List<T> candidates, T candidate, int limit)
    {
        if (candidates.Count < limit)
        {
            candidates.Add(candidate);
        }
    }

    private static int FindInsertIndex<T>(IReadOnlyList<T> candidates, T candidate, Comparison<T> comparison)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (comparison(candidate, candidates[index]) < 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int CompareDistanceCandidates(TokenDistanceCandidate left, TokenDistanceCandidate right)
    {
        var distanceComparison = left.Distance.CompareTo(right.Distance);
        return distanceComparison != 0
            ? distanceComparison
            : string.Compare(left.Segment.Id, right.Segment.Id, StringComparison.Ordinal);
    }

    private static int CompareCorrectionCandidates(FuzzyCorrectionCandidate left, FuzzyCorrectionCandidate right)
    {
        var similarityComparison = right.Similarity.CompareTo(left.Similarity);
        if (similarityComparison != 0)
        {
            return similarityComparison;
        }

        var frequencyComparison = right.Term.Frequency.CompareTo(left.Term.Frequency);
        return frequencyComparison != 0
            ? frequencyComparison
            : string.Compare(left.Term.Value, right.Term.Value, StringComparison.Ordinal);
    }
}

internal readonly record struct FuzzyCorpusTerm(string Value, int Frequency);

internal readonly record struct FuzzyCorrectionCandidate(FuzzyCorpusTerm Term, double Similarity);

internal readonly record struct TokenDistanceCandidate(TokenizedKnowledgeSegment Segment, double Distance);
