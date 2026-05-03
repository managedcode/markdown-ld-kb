using System.Security.Cryptography;
using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TokenKeyphraseExtractor
{
    private readonly Uri _baseUri;
    private readonly TiktokenKnowledgeGraphOptions _options;
    private readonly TokenKeyphraseCandidateBuilder _candidateBuilder;

    public TokenKeyphraseExtractor(Uri baseUri, TiktokenKnowledgeGraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(options);

        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri);
        _options = options;
        _candidateBuilder = new TokenKeyphraseCandidateBuilder(options);
    }

    public IReadOnlyList<TokenizedKnowledgeTopic> Extract(IReadOnlyList<TokenizedSegmentCandidate> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var candidates = _candidateBuilder.CreateCandidateGroups(segments);
        var documentFrequencies = TokenKeyphraseScorer.CountDocumentFrequencies(candidates);
        var topics = new List<TokenizedKnowledgeTopic>(segments.Count * _options.MaxTopicLabelsPerSegment);
        foreach (var group in candidates)
        {
            AddTopics(topics, group.Segment, group.Candidates, documentFrequencies, segments.Count);
        }

        return topics.ToArray();
    }

    private void AddTopics(
        ICollection<TokenizedKnowledgeTopic> topics,
        TokenizedSegmentCandidate segment,
        IReadOnlyList<KeyphraseCandidate> candidates,
        IReadOnlyDictionary<string, double> documentFrequencies,
        int segmentCount)
    {
        var scored = TokenKeyphraseScorer.ScoreCandidates(candidates, documentFrequencies, segmentCount);
        scored.Sort(TokenKeyphraseScorer.CompareScoredCandidates);
        var topicCount = Math.Min(_options.MaxTopicLabelsPerSegment, scored.Count);
        for (var index = 0; index < topicCount; index++)
        {
            var candidate = scored[index];
            topics.Add(new TokenizedKnowledgeTopic(
                CreateTopicId(candidate.Key),
                segment.DocumentId,
                segment.Id,
                candidate.Label,
                candidate.Score));
        }
    }

    private string CreateTopicId(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        return new Uri(_baseUri, TokenTopicIdPrefix + hash[..TopicHashLength]).AbsoluteUri;
    }

}
