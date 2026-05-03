using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBm25Search
{
    private const double K1 = 1.2d;
    private const double B = 0.75d;
    private const double Half = 0.5d;
    private const double IdfOffset = 1d;

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

        var documents = CreateDocuments(candidates, out var averageDocumentLength);
        var fuzzyOptions = KnowledgeGraphFuzzyTokenMatchingOptions.FromRankedSearch(options);
        var documentFrequency = CreateDocumentFrequency(documents, queryTerms, fuzzyOptions);
        return CreateMatches(
            documents,
            queryTerms,
            documentFrequency,
            averageDocumentLength,
            options.MaxResults,
            fuzzyOptions);
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

    private static Dictionary<string, int> CreateDocumentFrequency(
        IReadOnlyList<Bm25Document> documents,
        IReadOnlyList<string> queryTerms,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in queryTerms)
        {
            var matchingDocuments = 0;
            foreach (var document in documents)
            {
                matchingDocuments += TryFindTermFrequency(document, term, fuzzyOptions, out _) ? 1 : 0;
            }

            frequency[term] = matchingDocuments;
        }

        return frequency;
    }

    private static KnowledgeGraphRankedSearchMatch[] CreateMatches(
        IReadOnlyList<Bm25Document> documents,
        IReadOnlyList<string> queryTerms,
        IReadOnlyDictionary<string, int> documentFrequency,
        double averageDocumentLength,
        int maxResults,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        var matches = new List<KnowledgeGraphRankedSearchMatch>(Math.Min(documents.Count, maxResults));
        foreach (var document in documents)
        {
            var score = ScoreDocument(
                document,
                queryTerms,
                documentFrequency,
                documents.Count,
                averageDocumentLength,
                fuzzyOptions);
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
        IReadOnlyList<string> queryTerms,
        IReadOnlyDictionary<string, int> documentFrequency,
        int documentCount,
        double averageDocumentLength,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        var score = ZeroConfidence;
        foreach (var term in queryTerms)
        {
            score += ScoreTerm(
                document,
                term,
                documentFrequency.GetValueOrDefault(term),
                documentCount,
                averageDocumentLength,
                fuzzyOptions);
        }

        return score;
    }

    private static double ScoreTerm(
        Bm25Document document,
        string term,
        int documentFrequency,
        int documentCount,
        double averageDocumentLength,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        if (documentFrequency == 0 || document.Length == 0)
        {
            return ZeroConfidence;
        }

        if (!TryFindTermFrequency(document, term, fuzzyOptions, out var frequency))
        {
            return ZeroConfidence;
        }

        var idf = Math.Log(IdfOffset + ((documentCount - documentFrequency + Half) / (documentFrequency + Half)));
        var denominator = frequency + K1 * (IdfOffset - B + (B * document.Length / averageDocumentLength));
        return idf * ((frequency * (K1 + IdfOffset)) / denominator);
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
