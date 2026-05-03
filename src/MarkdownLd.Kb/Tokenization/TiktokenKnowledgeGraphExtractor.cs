using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TiktokenKnowledgeGraphExtractor
{
    private readonly Uri _baseUri;
    private readonly TiktokenKnowledgeGraphOptions _options;
    private readonly TokenVectorizer _vectorizer;
    private readonly TiktokenSegmentCandidateBuilder _segmentBuilder;
    private readonly TiktokenRelatedSegmentBuilder _relationBuilder;
    private readonly TokenKeyphraseExtractor _topicExtractor;
    private readonly TokenizedEntityHintExtractor _entityHintExtractor;

    public TiktokenKnowledgeGraphExtractor(Uri? baseUri = null, TiktokenKnowledgeGraphOptions? options = null)
    {
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));
        _options = ValidateOptions(options ?? new TiktokenKnowledgeGraphOptions());
        _vectorizer = new TokenVectorizer(_options.ModelName);
        _segmentBuilder = new TiktokenSegmentCandidateBuilder(_baseUri, _options, _vectorizer);
        _relationBuilder = new TiktokenRelatedSegmentBuilder(_options);
        _topicExtractor = new TokenKeyphraseExtractor(_baseUri, _options);
        _entityHintExtractor = new TokenizedEntityHintExtractor(_baseUri);
    }

    public TokenizedKnowledgeExtractionResult Extract(IReadOnlyList<MarkdownDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var sections = _segmentBuilder.BuildSections(documents);
        var candidates = _segmentBuilder.BuildSegmentCandidates(documents);
        var vectorSpace = TokenVectorSpace.Fit(CreateCandidateTokenIds(candidates), _options.Weighting);
        var segments = CreateSegments(candidates, vectorSpace);
        var topics = _topicExtractor.Extract(candidates);
        var entityHints = _entityHintExtractor.Extract(documents);
        var relations = _options.BuildAutoRelatedSegmentRelations
            ? _relationBuilder.BuildRelations(segments)
            : [];
        var facts = TokenizedKnowledgeFactFactory.Build(sections, segments, topics, entityHints, relations);
        return new TokenizedKnowledgeExtractionResult(facts, segments, vectorSpace);
    }

    internal TokenizedKnowledgeIndex CreateIndex(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        TokenVectorSpace vectorSpace)
    {
        return new TokenizedKnowledgeIndex(_vectorizer, vectorSpace, segments);
    }

    private static TiktokenKnowledgeGraphOptions ValidateOptions(TiktokenKnowledgeGraphOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModelName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxRelatedSegments);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MinimumTokenCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumRelatedDistance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxTopicLabelsPerSegment);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxTopicPhraseWords);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MinimumTopicWordLength);
        return options;
    }

    private static IReadOnlyList<int>[] CreateCandidateTokenIds(IReadOnlyList<TokenizedSegmentCandidate> candidates)
    {
        var tokenIds = new IReadOnlyList<int>[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            tokenIds[index] = candidates[index].TokenIds;
        }

        return tokenIds;
    }

    private static TokenizedKnowledgeSegment[] CreateSegments(
        IReadOnlyList<TokenizedSegmentCandidate> candidates,
        TokenVectorSpace vectorSpace)
    {
        var segments = new TokenizedKnowledgeSegment[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            segments[index] = CreateSegment(candidates[index], vectorSpace);
        }

        return segments;
    }

    private static TokenizedKnowledgeSegment CreateSegment(
        TokenizedSegmentCandidate candidate,
        TokenVectorSpace vectorSpace)
    {
        return new TokenizedKnowledgeSegment(
            candidate.Id,
            candidate.DocumentId,
            candidate.ParentId,
            candidate.Text,
            candidate.LineNumber,
            vectorSpace.CreateVector(candidate.TokenIds));
    }

}

internal sealed record TokenizedKnowledgeExtractionResult(
    KnowledgeExtractionResult Facts,
    IReadOnlyList<TokenizedKnowledgeSegment> Segments,
    TokenVectorSpace VectorSpace);
