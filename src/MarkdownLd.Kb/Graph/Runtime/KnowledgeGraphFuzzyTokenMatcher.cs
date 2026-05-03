using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal readonly record struct KnowledgeGraphFuzzyTokenMatchingOptions(
    bool Enabled,
    int MaxEditDistance,
    int MinimumTokenLength)
{
    public static KnowledgeGraphFuzzyTokenMatchingOptions FromRankedSearch(
        KnowledgeGraphRankedSearchOptions options)
    {
        return new KnowledgeGraphFuzzyTokenMatchingOptions(
            options.EnableFuzzyTokenMatching,
            options.MaxFuzzyEditDistance,
            options.MinimumFuzzyTokenLength);
    }
}

internal static class KnowledgeGraphFuzzyTokenMatcher
{
    private const int ExactDistance = 0;

    public static bool TryFindFuzzyFrequency(
        IReadOnlyDictionary<string, int> candidateTermFrequency,
        string queryTerm,
        KnowledgeGraphFuzzyTokenMatchingOptions options,
        out double frequency)
    {
        frequency = ZeroConfidence;
        if (!CanFuzzyMatch(queryTerm, options))
        {
            return false;
        }

        var bestDistance = KnowledgeGraphBoundedEditDistance.NoMatchDistance;
        foreach (var (candidateTerm, candidateFrequency) in candidateTermFrequency)
        {
            if (!TryComputeSimilarityAndDistance(
                    queryTerm,
                    candidateTerm,
                    options,
                    out var similarityWeight,
                    out var distance) ||
                distance > bestDistance)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                frequency = ZeroConfidence;
            }

            frequency += candidateFrequency * similarityWeight;
        }

        return bestDistance != KnowledgeGraphBoundedEditDistance.NoMatchDistance;
    }

    public static bool TryComputeSimilarity(
        string queryTerm,
        string candidateTerm,
        KnowledgeGraphFuzzyTokenMatchingOptions options,
        out double similarity)
    {
        return TryComputeSimilarityAndDistance(queryTerm, candidateTerm, options, out similarity, out _);
    }

    private static bool TryComputeSimilarityAndDistance(
        string queryTerm,
        string candidateTerm,
        KnowledgeGraphFuzzyTokenMatchingOptions options,
        out double similarity,
        out int distance)
    {
        similarity = ZeroConfidence;
        distance = KnowledgeGraphBoundedEditDistance.NoMatchDistance;
        if (!CanFuzzyMatch(queryTerm, options) || !CanFuzzyMatch(candidateTerm, options))
        {
            return false;
        }

        distance = KnowledgeGraphBoundedEditDistance.Compute(queryTerm, candidateTerm, options.MaxEditDistance);
        if (distance == KnowledgeGraphBoundedEditDistance.NoMatchDistance)
        {
            return false;
        }

        similarity = CreateSimilarityWeight(queryTerm.Length, distance);
        return similarity > ZeroConfidence;
    }

    private static bool CanFuzzyMatch(string term, KnowledgeGraphFuzzyTokenMatchingOptions options)
    {
        return options.Enabled &&
               options.MaxEditDistance > ExactDistance &&
               term.Length >= options.MinimumTokenLength;
    }

    private static double CreateSimilarityWeight(int queryTermLength, int distance)
    {
        return FullConfidence - ((double)distance / queryTermLength);
    }
}
