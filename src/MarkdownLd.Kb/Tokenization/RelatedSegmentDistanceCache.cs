namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class RelatedSegmentDistanceCache
{
    private const int MaximumHotVectorCount = 32;
    private const int MaximumCachedVectorPairs = 1_000_000;
    private const int Orientations = 2;
    private const int NotCached = -1;

    private readonly IReadOnlyList<TokenizedKnowledgeSegment> _segments;
    private readonly int[] _groupsBySegment;
    private readonly int[] _rowsByGroup;
    private readonly double[] _distances;

    private RelatedSegmentDistanceCache(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        int[] groupsBySegment,
        int[] rowsByGroup,
        int rows)
    {
        _segments = segments;
        _groupsBySegment = groupsBySegment;
        _rowsByGroup = rowsByGroup;
        _distances = new double[rows * rowsByGroup.Length * Orientations];
        Array.Fill(_distances, double.NaN);
    }

    public static RelatedSegmentDistanceCache? Create(IReadOnlyList<TokenizedKnowledgeSegment> segments)
    {
        var uses = new Dictionary<TokenVector, VectorUse>(ReferenceEqualityComparer.Instance);
        var groupsBySegment = new int[segments.Count];
        for (var index = 0; index < segments.Count; index++)
        {
            var vector = segments[index].Vector;
            if (!uses.TryGetValue(vector, out var use))
            {
                use = new VectorUse(uses.Count, 0);
            }

            uses[vector] = use with { Count = use.Count + 1 };
            groupsBySegment[index] = use.Group;
        }

        var rowLimit = Math.Min(MaximumHotVectorCount, MaximumCachedVectorPairs / uses.Count);
        var rowsByGroup = new int[uses.Count];
        Array.Fill(rowsByGroup, NotCached);
        var row = 0;
        foreach (var use in uses.Values.Where(static use => use.Count > 1).OrderByDescending(static use => use.Count).Take(rowLimit))
        {
            rowsByGroup[use.Group] = row++;
        }

        return row == 0 ? null : new RelatedSegmentDistanceCache(segments, groupsBySegment, rowsByGroup, row);
    }

    public double GetDistance(int first, int second)
    {
        var earlier = Math.Min(first, second);
        var later = Math.Max(first, second);
        var earlierGroup = _groupsBySegment[earlier];
        var laterGroup = _groupsBySegment[later];
        var slot = FindSlot(earlierGroup, laterGroup);
        if (slot == NotCached)
        {
            return _segments[earlier].Vector.EuclideanDistanceTo(_segments[later].Vector);
        }

        ref var distance = ref _distances[slot];
        if (double.IsNaN(distance))
        {
            // Separate orientations preserve summation order when both vectors have the same size.
            distance = _segments[earlier].Vector.EuclideanDistanceTo(_segments[later].Vector);
        }

        return distance;
    }

    private int FindSlot(int earlierGroup, int laterGroup)
    {
        var earlierRow = _rowsByGroup[earlierGroup];
        if (earlierRow != NotCached)
        {
            return (earlierRow * _rowsByGroup.Length + laterGroup) * Orientations;
        }

        var laterRow = _rowsByGroup[laterGroup];
        return laterRow == NotCached
            ? NotCached
            : (laterRow * _rowsByGroup.Length + earlierGroup) * Orientations + 1;
    }

    private readonly record struct VectorUse(int Group, int Count);
}
