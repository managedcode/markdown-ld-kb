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
        if (maxPerSegment == 0)
        {
            return [];
        }

        var capacity = (int)Math.Min((long)segments.Count * maxPerSegment, int.MaxValue);
        var relations = new List<TokenizedKnowledgeRelation>(capacity);
        var relatedBySegment = CreateRelatedLists(segments.Count, maxPerSegment);
        AddRelatedSegmentCandidates(segments, relatedBySegment);
        var relationPairs = new HashSet<(string Left, string Right)>(capacity);
        for (var sourceIndex = 0; sourceIndex < segments.Count; sourceIndex++)
        {
            AddRelations(relations, relationPairs, segments[sourceIndex], relatedBySegment[sourceIndex]);
        }

        return relations.ToArray();
    }

    private static List<RelatedSegmentCandidate>[] CreateRelatedLists(int count, int capacity)
    {
        var related = new List<RelatedSegmentCandidate>[count];
        for (var index = 0; index < count; index++)
        {
            related[index] = new List<RelatedSegmentCandidate>(capacity);
        }

        return related;
    }

    private void AddRelatedSegmentCandidates(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        IReadOnlyList<List<RelatedSegmentCandidate>> relatedBySegment)
    {
        var ordered = Enumerable.Range(0, segments.Count)
            .OrderBy(index => segments[index].Id, StringComparer.Ordinal).ToArray();
        var nextUnresolved = Enumerable.Range(1, ordered.Length).ToArray();
        var firstUnresolved = 0;
        var distances = RelatedSegmentDistanceCache.Create(segments);
        for (var position = 0; position < ordered.Length; position++)
        {
            AddLaterPairs(segments, relatedBySegment, ordered, distances, position, nextUnresolved, ref firstUnresolved);
        }
    }

    private void AddLaterPairs(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        IReadOnlyList<List<RelatedSegmentCandidate>> relatedBySegment,
        int[] ordered,
        RelatedSegmentDistanceCache? distances,
        int sourcePosition,
        int[] nextUnresolved,
        ref int firstUnresolved)
    {
        var sourceIndex = ordered[sourcePosition];
        var sourceComplete = firstUnresolved != sourcePosition;
        if (!sourceComplete)
        {
            firstUnresolved = nextUnresolved[sourcePosition];
        }

        // The remaining unresolved positions are always in ascending ID order.
        var unresolved = firstUnresolved;
        var previousUnresolved = -1;
        var position = sourceComplete ? unresolved : sourcePosition + 1;
        while (position < ordered.Length)
        {
            var candidateIndex = ordered[position];
            var candidateComplete = position != unresolved;
            AddPair(segments, relatedBySegment, distances, sourceIndex, candidateIndex, sourceComplete, candidateComplete);
            if (!candidateComplete)
            {
                unresolved = nextUnresolved[position];
                if (!HasExactNeighbors(relatedBySegment[candidateIndex]))
                {
                    previousUnresolved = position;
                }
                else if (previousUnresolved >= 0)
                {
                    nextUnresolved[previousUnresolved] = unresolved;
                }
                else
                {
                    firstUnresolved = unresolved;
                }
            }

            sourceComplete |= HasExactNeighbors(relatedBySegment[sourceIndex]);
            position = sourceComplete ? unresolved : position + 1;
        }
    }

    private bool HasExactNeighbors(List<RelatedSegmentCandidate> related) =>
        related.Count == _options.MaxRelatedSegments && related[^1].Distance == 0d;

    private void AddPair(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        IReadOnlyList<List<RelatedSegmentCandidate>> relatedBySegment,
        RelatedSegmentDistanceCache? distances,
        int sourceIndex,
        int candidateIndex,
        bool sourceComplete,
        bool candidateComplete)
    {
        var source = segments[sourceIndex];
        var candidate = segments[candidateIndex];
        // Preserve the original operand order, including floating-point accumulation.
        var distance = distances?.GetDistance(sourceIndex, candidateIndex) ??
            (sourceIndex < candidateIndex
                ? source.Vector.EuclideanDistanceTo(candidate.Vector)
                : candidate.Vector.EuclideanDistanceTo(source.Vector));
        if (distance > _options.MaximumRelatedDistance)
        {
            return;
        }

        if (!sourceComplete)
        {
            AddBoundedRelatedCandidate(relatedBySegment[sourceIndex], new RelatedSegmentCandidate(candidate, distance));
        }

        if (!candidateComplete)
        {
            AddBoundedRelatedCandidate(relatedBySegment[candidateIndex], new RelatedSegmentCandidate(source, distance));
        }
    }

    private static void AddRelations(
        ICollection<TokenizedKnowledgeRelation> relations,
        ISet<(string Left, string Right)> relationPairs,
        TokenizedKnowledgeSegment source,
        IReadOnlyList<RelatedSegmentCandidate> related)
    {
        foreach (var candidate in related)
        {
            if (!relationPairs.Add(CreateRelationPair(source.Id, candidate.Segment.Id)))
            {
                continue;
            }

            relations.Add(new TokenizedKnowledgeRelation(
                source.Id,
                candidate.Segment.Id,
                candidate.Distance));
        }
    }

    private static (string Left, string Right) CreateRelationPair(string left, string right)
    {
        return string.Compare(left, right, StringComparison.Ordinal) <= 0
            ? (left, right)
            : (right, left);
    }

    private void AddBoundedRelatedCandidate(
        List<RelatedSegmentCandidate> related,
        RelatedSegmentCandidate candidate)
    {
        if (related.Count == _options.MaxRelatedSegments && CompareRelatedCandidates(candidate, related[^1]) >= 0)
        {
            return;
        }

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
