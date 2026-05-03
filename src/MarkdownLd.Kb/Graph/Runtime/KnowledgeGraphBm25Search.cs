using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBm25Search
{
    public static IReadOnlyList<KnowledgeGraphRankedSearchMatch> Search(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        string query,
        KnowledgeGraphRankedSearchOptions options)
    {
        var queryTerms = KnowledgeGraphSearchTokenizer.TokenizeDistinct(query);
        if (queryTerms.Length == 0 || candidates.Count == 0)
        {
            return [];
        }

        var fuzzyOptions = KnowledgeGraphFuzzyTokenMatchingOptions.FromRankedSearch(options);
        if (!fuzzyOptions.Enabled)
        {
            return KnowledgeGraphExactBm25Search.Search(
                candidates,
                queryTerms,
                options.MaxResults);
        }

        var documents = CreateDocuments(candidates, out var averageDocumentLength);
        using var statistics = CreateTermStatistics(documents, queryTerms, fuzzyOptions);
        return CreateMatches(documents, queryTerms, statistics, averageDocumentLength, options.MaxResults);
    }

    private static Bm25Document[] CreateDocuments(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        out double averageDocumentLength)
    {
        var documents = new Bm25Document[candidates.Count];
        var totalLength = 0;
        for (var index = 0; index < candidates.Count; index++)
        {
            documents[index] = CreateDocument(candidates[index]);
            totalLength += documents[index].Length;
        }

        averageDocumentLength = (double)totalLength / documents.Length;
        return documents;
    }

    private static Bm25Document CreateDocument(KnowledgeGraphSearchCandidate candidate)
    {
        var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);
        var length = KnowledgeGraphSearchTokenizer.CountTermFrequencies(candidate.SearchText, frequencies);
        return new Bm25Document(candidate, frequencies, length);
    }

    private static KnowledgeGraphBm25TermStatistics CreateTermStatistics(
        IReadOnlyList<Bm25Document> documents,
        IReadOnlyList<string> queryTerms,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        var statistics = KnowledgeGraphBm25TermStatistics.Rent(documents.Count, queryTerms.Count);
        for (var termIndex = 0; termIndex < queryTerms.Count; termIndex++)
        {
            var term = queryTerms[termIndex];
            var matchingDocuments = 0;
            for (var documentIndex = 0; documentIndex < documents.Count; documentIndex++)
            {
                var matched = TryFindTermFrequency(documents[documentIndex], term, fuzzyOptions, out var frequency);
                statistics.SetTermFrequency(documentIndex, termIndex, matched ? frequency : ZeroConfidence);
                matchingDocuments += matched ? 1 : 0;
            }

            statistics.SetDocumentFrequency(termIndex, matchingDocuments);
        }

        return statistics;
    }

    private static KnowledgeGraphRankedSearchMatch[] CreateMatches(
        IReadOnlyList<Bm25Document> documents,
        IReadOnlyList<string> queryTerms,
        KnowledgeGraphBm25TermStatistics statistics,
        double averageDocumentLength,
        int maxResults)
    {
        var matches = new List<KnowledgeGraphRankedSearchMatch>(Math.Min(documents.Count, maxResults));
        for (var documentIndex = 0; documentIndex < documents.Count; documentIndex++)
        {
            var document = documents[documentIndex];
            var score = ScoreDocument(
                document,
                documentIndex,
                queryTerms.Count,
                statistics,
                documents.Count,
                averageDocumentLength);
            if (score <= ZeroConfidence)
            {
                continue;
            }

            KnowledgeGraphBm25SearchResults.AddBoundedMatch(
                matches,
                new KnowledgeGraphRankedSearchMatch(
                    document.Candidate.NodeId,
                    document.Candidate.Label,
                    document.Candidate.Description,
                    KnowledgeGraphRankedSearchSource.Bm25,
                    score),
                maxResults);
        }

        return KnowledgeGraphBm25SearchResults.ToArray(matches);
    }

    private static double ScoreDocument(
        Bm25Document document,
        int documentIndex,
        int termCount,
        KnowledgeGraphBm25TermStatistics statistics,
        int documentCount,
        double averageDocumentLength)
    {
        var score = ZeroConfidence;
        for (var termIndex = 0; termIndex < termCount; termIndex++)
        {
            score += KnowledgeGraphBm25Scoring.ScoreTerm(
                document.Length,
                statistics.GetTermFrequency(documentIndex, termIndex),
                statistics.GetDocumentFrequency(termIndex),
                documentCount,
                averageDocumentLength);
        }

        return score;
    }

    private static bool TryFindTermFrequency(
        Bm25Document document,
        string term,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions,
        out double frequency)
    {
        if (document.TermFrequency.TryGetValue(term, out var exactFrequency))
        {
            frequency = exactFrequency;
            return true;
        }

        return KnowledgeGraphFuzzyTokenMatcher.TryFindFuzzyFrequency(
            document.TermFrequency,
            term,
            fuzzyOptions,
            out frequency);
    }

    private readonly record struct Bm25Document(
        KnowledgeGraphSearchCandidate Candidate,
        Dictionary<string, int> TermFrequency,
        int Length);
}
