using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class TokenizedKnowledgeIndex
{
    private readonly TokenVectorizer _vectorizer;
    private readonly TokenVectorSpace _vectorSpace;
    private readonly IReadOnlyList<TokenizedKnowledgeSegment> _segments;
    private readonly IReadOnlyDictionary<string, int> _corpusTermFrequency;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<FuzzyCorpusTerm>> _corpusTermsByLength;

    internal TokenizedKnowledgeIndex(
        TokenVectorizer vectorizer,
        TokenVectorSpace vectorSpace,
        IReadOnlyList<TokenizedKnowledgeSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(vectorizer);
        ArgumentNullException.ThrowIfNull(vectorSpace);
        ArgumentNullException.ThrowIfNull(segments);

        _vectorizer = vectorizer;
        _vectorSpace = vectorSpace;
        _segments = segments;
        _corpusTermFrequency = CreateCorpusTermFrequency(segments);
        _corpusTermsByLength = CreateCorpusTermsByLength(_corpusTermFrequency);
    }

    public IReadOnlyList<TokenDistanceSearchResult> Search(string query, int limit)
    {
        return Search(query, new TokenDistanceSearchOptions { Limit = limit });
    }

    public IReadOnlyList<TokenDistanceSearchResult> Search(string query, TokenDistanceSearchOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var effectiveQuery = options.EnableFuzzyQueryCorrection
            ? CreateFuzzyExpandedQuery(query, options)
            : query;
        var queryVector = _vectorSpace.CreateVector(_vectorizer.Tokenize(effectiveQuery));
        var candidates = TokenizedKnowledgeSearchRanking.FindNearestSegments(_segments, queryVector, options.Limit);
        return TokenizedKnowledgeSearchRanking.CreateResults(candidates);
    }

    private static void ValidateOptions(TokenDistanceSearchOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Limit);
        ArgumentOutOfRangeException.ThrowIfNegative(options.MaxFuzzyEditDistance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MinimumFuzzyTokenLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxFuzzyCorrectionsPerToken);
    }

    private string CreateFuzzyExpandedQuery(string query, TokenDistanceSearchOptions options)
    {
        var corrections = EnumerateFuzzyCorrections(query, options);
        return corrections.Length == 0
            ? query
            : string.Concat(query.Trim(), SpaceText, string.Join(SpaceText, corrections));
    }

    private string[] EnumerateFuzzyCorrections(string query, TokenDistanceSearchOptions options)
    {
        var fuzzyOptions = new KnowledgeGraphFuzzyTokenMatchingOptions(
            options.EnableFuzzyQueryCorrection,
            options.MaxFuzzyEditDistance,
            options.MinimumFuzzyTokenLength);
        var corrections = new HashSet<string>(StringComparer.Ordinal);

        foreach (var queryTerm in KnowledgeGraphSearchTokenizer.TokenizeDistinct(query))
        {
            if (_corpusTermFrequency.ContainsKey(queryTerm))
            {
                continue;
            }

            foreach (var correction in FindCorrections(queryTerm, fuzzyOptions, options.MaxFuzzyCorrectionsPerToken))
            {
                corrections.Add(correction);
            }
        }

        return corrections.ToArray();
    }

    private string[] FindCorrections(
        string queryTerm,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions,
        int maxCorrections)
    {
        var candidates = new List<FuzzyCorrectionCandidate>(maxCorrections);
        foreach (var corpusTerm in EnumerateLengthCompatibleTerms(queryTerm.Length, fuzzyOptions.MaxEditDistance))
        {
            if (KnowledgeGraphFuzzyTokenMatcher.TryComputeSimilarity(
                    queryTerm,
                    corpusTerm.Value,
                    fuzzyOptions,
                    out var similarity))
            {
                TokenizedKnowledgeSearchRanking.AddCorrectionCandidate(
                    candidates,
                    new FuzzyCorrectionCandidate(corpusTerm, similarity),
                    maxCorrections);
            }
        }

        return TokenizedKnowledgeSearchRanking.CreateCorrectionValues(candidates);
    }

    private IEnumerable<FuzzyCorpusTerm> EnumerateLengthCompatibleTerms(int queryTermLength, int maxEditDistance)
    {
        var minimumLength = Math.Max(0, queryTermLength - maxEditDistance);
        var maximumLength = queryTermLength + maxEditDistance;
        for (var length = minimumLength; length <= maximumLength; length++)
        {
            if (!_corpusTermsByLength.TryGetValue(length, out var terms))
            {
                continue;
            }

            foreach (var term in terms)
            {
                yield return term;
            }
        }
    }

    private static IReadOnlyDictionary<string, int> CreateCorpusTermFrequency(
        IEnumerable<TokenizedKnowledgeSegment> segments)
    {
        var frequencies = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var segment in segments)
        {
            KnowledgeGraphSearchTokenizer.CountTermFrequencies(segment.Text, frequencies);
        }

        return frequencies;
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<FuzzyCorpusTerm>> CreateCorpusTermsByLength(
        IReadOnlyDictionary<string, int> corpusTermFrequency)
    {
        return corpusTermFrequency
            .GroupBy(static pair => pair.Key.Length)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<FuzzyCorpusTerm>)group
                    .Select(static pair => new FuzzyCorpusTerm(pair.Key, pair.Value))
                    .OrderBy(static term => term.Value, StringComparer.Ordinal)
                    .ToArray());
    }
}
