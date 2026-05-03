using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class TokenizedKnowledgeIndex
{
    private const string TokenPattern = @"[\p{L}\p{N}]+";

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
        return _segments
            .Select(segment => new TokenDistanceSearchResult(
                segment.Id,
                segment.DocumentId,
                segment.Text,
                queryVector.EuclideanDistanceTo(segment.Vector)))
            .OrderBy(result => result.Distance)
            .ThenBy(result => result.SegmentId, StringComparer.Ordinal)
            .Take(options.Limit)
            .ToArray();
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
        var corrections = EnumerateFuzzyCorrections(query, options)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return corrections.Length == 0
            ? query
            : string.Concat(query.Trim(), SpaceText, string.Join(SpaceText, corrections));
    }

    private IEnumerable<string> EnumerateFuzzyCorrections(string query, TokenDistanceSearchOptions options)
    {
        var fuzzyOptions = new KnowledgeGraphFuzzyTokenMatchingOptions(
            options.EnableFuzzyQueryCorrection,
            options.MaxFuzzyEditDistance,
            options.MinimumFuzzyTokenLength);

        foreach (var queryTerm in Tokenize(query).Distinct(StringComparer.Ordinal))
        {
            if (_corpusTermFrequency.ContainsKey(queryTerm))
            {
                continue;
            }

            foreach (var correction in FindCorrections(queryTerm, fuzzyOptions, options.MaxFuzzyCorrectionsPerToken))
            {
                yield return correction;
            }
        }
    }

    private IEnumerable<string> FindCorrections(
        string queryTerm,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions,
        int maxCorrections)
    {
        return CollectCorrectionCandidates(queryTerm, fuzzyOptions)
            .OrderByDescending(static candidate => candidate.Similarity)
            .ThenByDescending(static candidate => candidate.Term.Frequency)
            .ThenBy(static candidate => candidate.Term.Value, StringComparer.Ordinal)
            .Take(maxCorrections)
            .Select(static candidate => candidate.Term.Value);
    }

    private List<FuzzyCorrectionCandidate> CollectCorrectionCandidates(
        string queryTerm,
        KnowledgeGraphFuzzyTokenMatchingOptions fuzzyOptions)
    {
        var candidates = new List<FuzzyCorrectionCandidate>();
        foreach (var corpusTerm in EnumerateLengthCompatibleTerms(queryTerm.Length, fuzzyOptions.MaxEditDistance))
        {
            if (KnowledgeGraphFuzzyTokenMatcher.TryComputeSimilarity(
                    queryTerm,
                    corpusTerm.Value,
                    fuzzyOptions,
                    out var similarity))
            {
                candidates.Add(new FuzzyCorrectionCandidate(corpusTerm, similarity));
            }
        }

        return candidates;
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
        foreach (var term in segments.SelectMany(static segment => Tokenize(segment.Text)))
        {
            frequencies[term] = frequencies.GetValueOrDefault(term) + 1;
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

    private static IEnumerable<string> Tokenize(string text)
    {
        return TokenRegex()
            .Matches(text)
            .Select(static match => match.Value.ToLowerInvariant());
    }

    [GeneratedRegex(TokenPattern, RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    private readonly record struct FuzzyCorpusTerm(string Value, int Frequency);

    private readonly record struct FuzzyCorrectionCandidate(FuzzyCorpusTerm Term, double Similarity);
}
