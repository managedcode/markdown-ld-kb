using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TokenKeyphraseExtractor
{
    private static readonly Regex WordRegex = new(TopicWordPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly Uri _baseUri;
    private readonly TiktokenKnowledgeGraphOptions _options;

    public TokenKeyphraseExtractor(Uri baseUri, TiktokenKnowledgeGraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(options);

        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri);
        _options = options;
    }

    public IReadOnlyList<TokenizedKnowledgeTopic> Extract(IReadOnlyList<TokenizedSegmentCandidate> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var candidates = segments
            .Select(segment => (Segment: segment, Candidates: CreateCandidates(segment).ToArray()))
            .ToArray();
        var documentFrequencies = CountDocumentFrequencies(candidates.Select(static group => group.Candidates).ToArray());

        return candidates
            .SelectMany(group => SelectTopics(group.Segment, group.Candidates, documentFrequencies, segments.Count))
            .ToArray();
    }

    private IEnumerable<TokenizedKnowledgeTopic> SelectTopics(
        TokenizedSegmentCandidate segment,
        IReadOnlyList<KeyphraseCandidate> candidates,
        IReadOnlyDictionary<string, double> documentFrequencies,
        int segmentCount)
    {
        return candidates
            .GroupBy(static candidate => candidate.Key, StringComparer.Ordinal)
            .Select(group => ScoreCandidate(group, documentFrequencies, segmentCount))
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.WordCount)
            .ThenBy(static candidate => candidate.Label, StringComparer.OrdinalIgnoreCase)
            .Take(_options.MaxTopicLabelsPerSegment)
            .Select(candidate => new TokenizedKnowledgeTopic(
                CreateTopicId(candidate.Key),
                segment.DocumentId,
                segment.Id,
                candidate.Label,
                candidate.Score));
    }

    private IEnumerable<KeyphraseCandidate> CreateCandidates(TokenizedSegmentCandidate segment)
    {
        var words = WordRegex.Matches(segment.Text)
            .Select(static match => match.Value.Trim())
            .Where(word => word.Length > 0)
            .ToArray();

        for (var start = 0; start < words.Length; start++)
        {
            var maxLength = Math.Min(_options.MaxTopicPhraseWords, words.Length - start);
            for (var length = 1; length <= maxLength; length++)
            {
                var phraseWords = words[start..(start + length)];
                if (phraseWords.All(word => word.Length < _options.MinimumTopicWordLength))
                {
                    continue;
                }

                var label = string.Join(SpaceText, phraseWords);
                yield return new KeyphraseCandidate(
                    NormalizeKey(label),
                    label,
                    length,
                    label.Length);
            }
        }
    }

    private static Dictionary<string, double> CountDocumentFrequencies(IReadOnlyList<IReadOnlyList<KeyphraseCandidate>> candidates)
    {
        var frequencies = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var segmentCandidates in candidates)
        {
            foreach (var key in segmentCandidates.Select(static candidate => candidate.Key).Distinct(StringComparer.Ordinal))
            {
                frequencies[key] = frequencies.GetValueOrDefault(key) + TokenCountIncrement;
            }
        }

        return frequencies;
    }

    private static ScoredKeyphraseCandidate ScoreCandidate(
        IEnumerable<KeyphraseCandidate> candidates,
        IReadOnlyDictionary<string, double> documentFrequencies,
        int segmentCount)
    {
        var candidateArray = candidates.ToArray();
        var first = candidateArray[0];
        var idf = Math.Log((segmentCount + IdfSmoothingIncrement) /
                           (documentFrequencies[first.Key] + IdfSmoothingIncrement)) + IdfWeightOffset;
        var phraseBoost = Math.Sqrt(first.WordCount);
        var lengthBoost = Math.Log(first.CharacterCount + IdfWeightOffset);
        var score = candidateArray.Length * idf * phraseBoost * lengthBoost;
        return new ScoredKeyphraseCandidate(first.Key, first.Label, first.WordCount, score);
    }

    private string CreateTopicId(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return new Uri(_baseUri, TokenTopicIdPrefix + hash[..TopicHashLength]).AbsoluteUri;
    }

    private static string NormalizeKey(string label)
    {
        return label.Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
    }

    private sealed record KeyphraseCandidate(
        string Key,
        string Label,
        int WordCount,
        int CharacterCount);

    private sealed record ScoredKeyphraseCandidate(
        string Key,
        string Label,
        int WordCount,
        double Score);
}
