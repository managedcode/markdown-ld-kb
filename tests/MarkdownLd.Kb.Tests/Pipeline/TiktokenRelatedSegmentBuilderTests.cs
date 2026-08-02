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
