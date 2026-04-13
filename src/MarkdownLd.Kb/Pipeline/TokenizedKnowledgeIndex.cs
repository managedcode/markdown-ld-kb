namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class TokenizedKnowledgeIndex
{
    private readonly TokenVectorizer _vectorizer;
    private readonly TokenVectorSpace _vectorSpace;
    private readonly IReadOnlyList<TokenizedKnowledgeSegment> _segments;

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
    }

    public IReadOnlyList<TokenDistanceSearchResult> Search(string query, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var queryVector = _vectorSpace.CreateVector(_vectorizer.Tokenize(query));
        return _segments
            .Select(segment => new TokenDistanceSearchResult(
                segment.Id,
                segment.DocumentId,
                segment.Text,
                queryVector.EuclideanDistanceTo(segment.Vector)))
            .OrderBy(result => result.Distance)
            .ThenBy(result => result.SegmentId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

}
