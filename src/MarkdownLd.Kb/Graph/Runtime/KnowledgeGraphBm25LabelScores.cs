using System.Buffers;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBm25LabelScores
{
    private const double LabelWeight = 3d;

    public static double[] Calculate(IReadOnlyList<KnowledgeGraphSearchCandidate> candidates, string[] queryTerms)
    {
        var scores = new double[candidates.Count];
        using var statistics = KnowledgeGraphBm25TermStatistics.Rent(candidates.Count, queryTerms.Length);
        statistics.Clear();
        var documentLengths = ArrayPool<int>.Shared.Rent(candidates.Count);
        try
        {
            var termIndexes = new Dictionary<string, int>(queryTerms.Length, StringComparer.Ordinal);
            for (var termIndex = 0; termIndex < queryTerms.Length; termIndex++)
            {
                termIndexes.Add(queryTerms[termIndex], termIndex);
            }

            var totalLength = 0;
            for (var index = 0; index < candidates.Count; index++)
            {
                var frequencies = statistics.GetDocumentTermFrequencies(index);
                var length = KnowledgeGraphSearchTokenizer.CountSelectedTermFrequencies(
                    candidates[index].Label,
                    termIndexes,
                    frequencies);
                documentLengths[index] = length;
                totalLength += length;
                for (var termIndex = 0; termIndex < queryTerms.Length; termIndex++)
                {
                    if (frequencies[termIndex] > ZeroConfidence)
                    {
                        statistics.IncrementDocumentFrequency(termIndex);
                    }
                }
            }

            var averageLength = (double)totalLength / candidates.Count;
            for (var index = 0; index < candidates.Count; index++)
            {
                for (var termIndex = 0; termIndex < queryTerms.Length; termIndex++)
                {
                    scores[index] += LabelWeight * KnowledgeGraphBm25Scoring.ScoreTerm(
                        documentLengths[index],
                        statistics.GetTermFrequency(index, termIndex),
                        statistics.GetDocumentFrequency(termIndex),
                        candidates.Count,
                        averageLength);
                }
            }

            return scores;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(documentLengths);
        }
    }
}
