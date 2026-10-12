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
        var unresolved = new SortedSet<int>(Enumerable.Range(0, segments.Count));
        for (var position = 0; position < ordered.Length; position++)
        {
            unresolved.Remove(position);
            var sourceIndex = ordered[position];
            var candidates = HasExactNeighbors(relatedBySegment[sourceIndex])
                ? unresolved.ToArray()
                : Enumerable.Range(position + 1, ordered.Length - position - 1);
            foreach (var candidatePosition in candidates)
            {
                var candidateIndex = ordered[candidatePosition];
                AddPair(segments, relatedBySegment, sourceIndex, candidateIndex);
                if (HasExactNeighbors(relatedBySegment[candidateIndex]))
                {
                    unresolved.Remove(candidatePosition);
                }
            }
        }
    }

    private bool HasExactNeighbors(IReadOnlyList<RelatedSegmentCandidate> related) =>
        related.Count == _options.MaxRelatedSegments && related[^1].Distance == 0d;

    private void AddPair(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        IReadOnlyList<List<RelatedSegmentCandidate>> relatedBySegment,
        int sourceIndex,
        int candidateIndex)
    {
        var source = segments[sourceIndex];
        var candidate = segments[candidateIndex];
        // Preserve the original operand order, including floating-point accumulation.
        var distance = sourceIndex < candidateIndex
            ? source.Vector.EuclideanDistanceTo(candidate.Vector)
            : candidate.Vector.EuclideanDistanceTo(source.Vector);
        if (distance > _options.MaximumRelatedDistance)
        {
            return;
        }

        AddBoundedRelatedCandidate(relatedBySegment[sourceIndex], new RelatedSegmentCandidate(candidate, distance));
        AddBoundedRelatedCandidate(relatedBySegment[candidateIndex], new RelatedSegmentCandidate(source, distance));
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
