namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TiktokenRelatedSegmentBuilder
{
    private readonly TiktokenKnowledgeGraphOptions _options;

    public TiktokenRelatedSegmentBuilder(TiktokenKnowledgeGraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public TokenizedKnowledgeRelation[] BuildRelations(IReadOnlyList<TokenizedKnowledgeSegment> segments)
    {
        var maxPerSegment = Math.Min(_options.MaxRelatedSegments, Math.Max(0, segments.Count - 1));
        var capacity = (int)Math.Min((long)segments.Count * maxPerSegment, int.MaxValue);
        var relations = new List<TokenizedKnowledgeRelation>(capacity);
        var related = new List<RelatedSegmentCandidate>(maxPerSegment);
        for (var sourceIndex = 0; sourceIndex < segments.Count; sourceIndex++)
        {
            related.Clear();
            AddRelatedSegmentCandidates(segments, sourceIndex, related);
            AddRelations(relations, segments[sourceIndex], related);
        }

        return relations.ToArray();
    }

    private void AddRelatedSegmentCandidates(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        int sourceIndex,
        List<RelatedSegmentCandidate> related)
    {
        var source = segments[sourceIndex];
        for (var index = 0; index < segments.Count; index++)
        {
            if (index == sourceIndex)
            {
                continue;
            }

            var candidate = segments[index];
            var distance = source.Vector.EuclideanDistanceTo(candidate.Vector);
            if (distance <= _options.MaximumRelatedDistance)
            {
                AddBoundedRelatedCandidate(related, new RelatedSegmentCandidate(candidate, distance));
            }
        }
    }

    private static void AddRelations(
        ICollection<TokenizedKnowledgeRelation> relations,
        TokenizedKnowledgeSegment source,
        IReadOnlyList<RelatedSegmentCandidate> related)
    {
        foreach (var candidate in related)
        {
            relations.Add(new TokenizedKnowledgeRelation(
                source.Id,
                candidate.Segment.Id,
                candidate.Distance));
        }
    }

    private void AddBoundedRelatedCandidate(
        List<RelatedSegmentCandidate> related,
        RelatedSegmentCandidate candidate)
    {
        var insertIndex = FindRelatedInsertIndex(related, candidate);
        if (insertIndex >= 0)
        {
            related.Insert(insertIndex, candidate);
            if (related.Count > _options.MaxRelatedSegments)
            {
                related.RemoveAt(related.Count - 1);
            }

            return;
        }

        if (related.Count < _options.MaxRelatedSegments)
        {
            related.Add(candidate);
        }
    }

    private static int FindRelatedInsertIndex(
        IReadOnlyList<RelatedSegmentCandidate> related,
        RelatedSegmentCandidate candidate)
    {
        for (var index = 0; index < related.Count; index++)
        {
            if (CompareRelatedCandidates(candidate, related[index]) < 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int CompareRelatedCandidates(RelatedSegmentCandidate left, RelatedSegmentCandidate right)
    {
        var distanceComparison = left.Distance.CompareTo(right.Distance);
        return distanceComparison != 0
            ? distanceComparison
            : string.Compare(left.Segment.Id, right.Segment.Id, StringComparison.Ordinal);
    }
}

internal readonly record struct RelatedSegmentCandidate(TokenizedKnowledgeSegment Segment, double Distance);
