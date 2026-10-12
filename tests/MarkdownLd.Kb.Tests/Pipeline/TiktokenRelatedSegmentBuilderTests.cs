using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Pipeline;

public sealed class TiktokenRelatedSegmentBuilderTests
{
    private const string DocumentId = "https://relations.example/document/";
    private const string ParentId = "https://relations.example/section/";

    [Test]
    public void Mutual_nearest_neighbors_emit_one_symmetric_relation()
    {
        var segments = new[]
        {
            Segment("a", (1, 1d)),
            Segment("b", (1, 1d)),
        };
        var builder = CreateBuilder(maxRelatedSegments: 1);

        var relations = builder.BuildRelations(segments);

        relations.ShouldBe([
            new TokenizedKnowledgeRelation(segments[0].Id, segments[1].Id, 0d),
        ]);
    }

    [Test]
    public void Related_pairs_preserve_the_union_of_each_segments_top_k_choices()
    {
        var segments = new[]
        {
            Segment("a", (1, 1d)),
            Segment("b", (1, 0.8d), (2, 0.2d)),
            Segment("c", (2, 1d)),
            Segment("d", (3, 1d)),
        };
        var builder = CreateBuilder(maxRelatedSegments: 1);

        var relations = builder.BuildRelations(segments);
        var actualPairs = relations.Select(CanonicalPair).ToArray();
        var expectedPairs = BuildCurrentTopKUnion(segments, maxRelatedSegments: 1)
            .Select(CanonicalPair)
            .ToArray();

        actualPairs.ShouldBe(expectedPairs);
        actualPairs.Distinct().Count().ShouldBe(actualPairs.Length);
    }

    [Test]
    [Arguments(1)]
    [Arguments(4)]
    [Arguments(12)]
    public void Shuffled_repeated_vectors_preserve_exact_exhaustive_top_k(int count)
    {
        var random = new Random(42);
        var segments = Enumerable.Range(0, 80)
            .Select(index => Segment(index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture), (index % 5, 1d)))
            .OrderBy(_ => random.Next()).ToArray();

        CreateBuilder(count).BuildRelations(segments)
            .ShouldBe(BuildCurrentTopKUnion(segments, count));
    }

    [Test]
    public void Mixed_sparse_weights_preserve_original_distances_and_order()
    {
        var random = new Random(73);
        var segments = Enumerable.Range(0, 100).Select(index => Segment(
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            (index % 7, random.NextDouble()), (10 + index % 3, random.NextDouble())))
            .OrderBy(_ => random.Next()).ToArray();

        CreateBuilder(5).BuildRelations(segments).ShouldBe(BuildCurrentTopKUnion(segments, 5));
    }

    [Test]
    public void Large_repeated_vectors_retain_every_source_and_the_distinct_tail()
    {
        const int count = 35_000;
        var segments = Enumerable.Range(0, count)
            .Select(index => Segment(index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture), (1, 1d)))
            .Append(Segment("tail", (2, 1d))).ToArray();

        var relations = CreateBuilder(4).BuildRelations(segments);

        relations.SelectMany(relation => new[] { relation.SubjectId, relation.ObjectId })
            .Distinct().Count().ShouldBe(count + 1);
        relations.Where(relation => relation.ObjectId == segments[^1].Id || relation.SubjectId == segments[^1].Id)
            .Count().ShouldBe(4);
    }

    private static TiktokenRelatedSegmentBuilder CreateBuilder(int maxRelatedSegments)
    {
        return new TiktokenRelatedSegmentBuilder(new TiktokenKnowledgeGraphOptions
        {
            MaxRelatedSegments = maxRelatedSegments,
            MaximumRelatedDistance = 2d,
        });
    }

    private static TokenizedKnowledgeSegment Segment(
        string suffix,
        params (int Token, double Weight)[] weights)
    {
        return new TokenizedKnowledgeSegment(
            $"https://relations.example/token-segment/{suffix}",
            DocumentId,
            ParentId,
            suffix,
            1,
            TokenVector.Create(weights.ToDictionary(static pair => pair.Token, static pair => pair.Weight)));
    }

    private static TokenizedKnowledgeRelation[] BuildCurrentTopKUnion(
        IReadOnlyList<TokenizedKnowledgeSegment> segments,
        int maxRelatedSegments)
    {
        var pairs = new HashSet<(string Left, string Right)>();
        var relations = new List<TokenizedKnowledgeRelation>();
        for (var sourceIndex = 0; sourceIndex < segments.Count; sourceIndex++)
        {
            var source = segments[sourceIndex];
            var candidates = segments
                .Where((_, index) => index != sourceIndex)
                .Select(candidate => new
                {
                    Segment = candidate,
                    Distance = source.Vector.EuclideanDistanceTo(candidate.Vector),
                })
                .OrderBy(static candidate => candidate.Distance)
                .ThenBy(static candidate => candidate.Segment.Id, StringComparer.Ordinal)
                .Take(maxRelatedSegments);
            foreach (var candidate in candidates)
            {
                var pair = CanonicalPair(source.Id, candidate.Segment.Id);
                if (pairs.Add(pair))
                {
                    relations.Add(new TokenizedKnowledgeRelation(
                        source.Id,
                        candidate.Segment.Id,
                        candidate.Distance));
                }
            }
        }

        return relations.ToArray();
    }

    private static (string Left, string Right) CanonicalPair(TokenizedKnowledgeRelation relation)
    {
        return CanonicalPair(relation.SubjectId, relation.ObjectId);
    }

    private static (string Left, string Right) CanonicalPair(string left, string right)
    {
        return string.Compare(left, right, StringComparison.Ordinal) <= 0
            ? (left, right)
            : (right, left);
    }
}
