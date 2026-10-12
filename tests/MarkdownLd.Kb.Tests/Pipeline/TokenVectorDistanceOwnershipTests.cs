using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Pipeline;

public sealed class TokenVectorDistanceOwnershipTests
{
    [Test]
    [Arguments(1, 16)]
    [Arguments(16, 1)]
    [Arguments(16, 128)]
    [Arguments(128, 16)]
    public void Unequal_sparse_vector_distances_match_dense_reference_in_both_directions(int leftCount, int rightCount)
    {
        var leftWeights = new Dictionary<int, double>();
        var rightWeights = new Dictionary<int, double>();
        for (var index = 0; index < leftCount; index++)
        {
            leftWeights[index] = index + 1;
        }
        for (var index = 0; index < rightCount; index++)
        {
            rightWeights[index + 1] = rightCount - index;
        }
        var left = TokenVector.Create(leftWeights);
        var right = TokenVector.Create(rightWeights);
        var expected = Math.Sqrt(left.Weights.Keys.Union(right.Weights.Keys).Sum(key =>
            Math.Pow(left.Weights.GetValueOrDefault(key) - right.Weights.GetValueOrDefault(key), 2)));
        left.EuclideanDistanceTo(right).ShouldBe(expected, 0.000000001);
        right.EuclideanDistanceTo(left).ShouldBe(expected, 0.000000001);
        leftWeights.Clear();
        rightWeights.Clear();
        left.EuclideanDistanceTo(right).ShouldBe(expected, 0.000000001);
    }
}
