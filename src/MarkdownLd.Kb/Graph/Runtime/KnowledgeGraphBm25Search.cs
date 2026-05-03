using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static partial class KnowledgeGraphBm25Search
{
    private const double K1 = 1.2d;
    private const double B = 0.75d;
    private const double Half = 0.5d;
    private const double IdfOffset = 1d;
    private const string TokenPattern = @"[\p{L}\p{N}]+";

    public static IReadOnlyList<KnowledgeGraphRankedSearchMatch> Search(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        string query,
        KnowledgeGraphRankedSearchOptions options)
    {
        var queryTerms = Tokenize(query).Distinct(StringComparer.Ordinal).ToArray();
        if (queryTerms.Length == 0 || candidates.Count == 0)
        {
            return [];
        }

        var documents = candidates.Select(CreateDocument).ToArray();
        var averageDocumentLength = documents.Average(static document => document.Length);
        var fuzzyOptions = KnowledgeGraphFuzzyTokenMatchingOptions.FromRankedSearch(options);
        var documentFrequency = CreateDocumentFrequency(documents, queryTerms, fuzzyOptions);

        return documents
            .Select(document => CreateMatch(document, queryTerms, documentFrequency, candidates.Count, averageDocumentLength, fuzzyOptions))
            .Where(static match => match.Score > ZeroConfidence)
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxResults)
            .ToArray();
    }

    private static Bm25Document CreateDocument(KnowledgeGraphSearchCandidate candidate)
    {
        var terms = Tokenize(candidate.SearchText).ToArray();
        var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            frequencies[term] = frequencies.GetValueOrDefault(term) + 1;
        }

        return new Bm25Document(candidate, frequencies, terms.Length);
    }

    private static IReadOnlyDictionary<string, int> CreateDocumentFrequency(
        IReadOnlyList<Bm25Document> documents,
        IReadOnlyList<string> queryTerms,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var term in queryTerms)
        {
            frequency[term] = documents.Count(document => TryFindTermFrequency(document, term, fuzzyOptions, out _));
        }

        return frequency;
    }

    private static KnowledgeGraphRankedSearchMatch CreateMatch(
        Bm25Document document,
        IReadOnlyList<string> queryTerms,
        IReadOnlyDictionary<string, int> documentFrequency,
        int documentCount,
        double averageDocumentLength,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        var score = queryTerms.Sum(term => ScoreTerm(
            document,
            term,
            documentFrequency.GetValueOrDefault(term),
            documentCount,
            averageDocumentLength,
            fuzzyOptions));
        return new KnowledgeGraphRankedSearchMatch(
            document.Candidate.NodeId,
            document.Candidate.Label,
            document.Candidate.Description,
            KnowledgeGraphRankedSearchSource.Bm25,
            score);
    }

    private static double ScoreTerm(
        Bm25Document document,
        string term,
        int documentFrequency,
        int documentCount,
        double averageDocumentLength,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
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

    private static IEnumerable<string> Tokenize(string text)
    {
        return TokenRegex()
            .Matches(text)
            .Select(static match => match.Value.ToLowerInvariant());
    }

    [GeneratedRegex(TokenPattern, RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    private sealed record Bm25Document(
        KnowledgeGraphSearchCandidate Candidate,
        IReadOnlyDictionary<string, int> TermFrequency,
        int Length);
}
