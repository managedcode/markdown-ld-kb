using System.Buffers;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphExactBm25Search
{
    public static IReadOnlyList<KnowledgeGraphRankedSearchMatch> Search(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        string[] queryTerms,
        int maxResults)
    {
        using var statistics = KnowledgeGraphBm25TermStatistics.Rent(candidates.Count, queryTerms.Length);
        statistics.Clear();
        var documentLengths = ArrayPool<int>.Shared.Rent(candidates.Count);

        try
        {
            var averageDocumentLength = CreateTermStatistics(
                candidates,
                queryTerms,
                statistics,
                documentLengths);

            return CreateMatches(
                candidates,
                queryTerms.Length,
                statistics,
                documentLengths,
                averageDocumentLength,
                maxResults);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(documentLengths);
        }
    }

    private static double CreateTermStatistics(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        string[] queryTerms,
        KnowledgeGraphBm25TermStatistics statistics,
        int[] documentLengths)
    {
        var queryTermIndexes = CreateQueryTermIndexes(queryTerms);
        var totalLength = 0;
        for (var documentIndex = 0; documentIndex < candidates.Count; documentIndex++)
        {
            var termFrequencies = statistics.GetDocumentTermFrequencies(documentIndex);
            var documentLength = KnowledgeGraphSearchTokenizer.CountSelectedTermFrequencies(
                candidates[documentIndex].SearchText,
                queryTermIndexes,
                termFrequencies);
            documentLengths[documentIndex] = documentLength;
            totalLength += documentLength;
            AddDocumentFrequencies(statistics, termFrequencies);
        }

        return (double)totalLength / candidates.Count;
    }

    private static Dictionary<string, int> CreateQueryTermIndexes(string[] queryTerms)
    {
        var indexes = new Dictionary<string, int>(queryTerms.Length, StringComparer.Ordinal);
        for (var index = 0; index < queryTerms.Length; index++)
        {
            indexes[queryTerms[index]] = index;
        }

        return indexes;
    }

    private static void AddDocumentFrequencies(
        KnowledgeGraphBm25TermStatistics statistics,
        ReadOnlySpan<double> termFrequencies)
    {
        for (var termIndex = 0; termIndex < termFrequencies.Length; termIndex++)
        {
            if (termFrequencies[termIndex] > ZeroConfidence)
            {
                statistics.IncrementDocumentFrequency(termIndex);
            }
        }
    }

    private static KnowledgeGraphRankedSearchMatch[] CreateMatches(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        int termCount,
        KnowledgeGraphBm25TermStatistics statistics,
        IReadOnlyList<int> documentLengths,
        double averageDocumentLength,
        int maxResults)
    {
        var matches = new List<KnowledgeGraphRankedSearchMatch>(Math.Min(candidates.Count, maxResults));
        for (var documentIndex = 0; documentIndex < candidates.Count; documentIndex++)
        {
            var score = ScoreDocument(
                documentIndex,
                termCount,
                statistics,
                documentLengths[documentIndex],
                candidates.Count,
                averageDocumentLength);
            if (score <= ZeroConfidence)
            {
                continue;
            }

            var candidate = candidates[documentIndex];
            KnowledgeGraphBm25SearchResults.AddBoundedMatch(
                matches,
                new KnowledgeGraphRankedSearchMatch(
                    candidate.NodeId,
                    candidate.Label,
                    candidate.Description,
                    KnowledgeGraphRankedSearchSource.Bm25,
                    score),
                maxResults);
        }

        return KnowledgeGraphBm25SearchResults.ToArray(matches);
    }

    private static double ScoreDocument(
        int documentIndex,
        int termCount,
        KnowledgeGraphBm25TermStatistics statistics,
        int documentLength,
        int documentCount,
        double averageDocumentLength)
    {
        var score = ZeroConfidence;
        for (var termIndex = 0; termIndex < termCount; termIndex++)
        {
            score += KnowledgeGraphBm25Scoring.ScoreTerm(
                documentLength,
                statistics.GetTermFrequency(documentIndex, termIndex),
                statistics.GetDocumentFrequency(termIndex),
                documentCount,
                averageDocumentLength);
        }

        return score;
    }
}
