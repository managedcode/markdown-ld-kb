using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Pipeline;

public sealed class KnowledgeGraphBoundedEditDistanceTests
{
    private const string LongSharedPrefix = "cachevalidationfingerprintcheckpointtoken";
    private const string LongSharedSuffix = "manifestwindowrollbackevidence";

    [Test]
    public void Bounded_distance_handles_long_token_insertion_after_common_affix_trimming()
    {
        var left = LongSharedPrefix + LongSharedSuffix;
        var right = LongSharedPrefix + "x" + LongSharedSuffix;

        var distance = KnowledgeGraphBoundedEditDistance.Compute(left, right, maxDistance: 1);

        distance.ShouldBe(1);
    }

    [Test]
    public void Bounded_distance_rejects_misaligned_suffix_for_different_length_tokens()
    {
        var left = "x" + LongSharedSuffix;
        var right = "y" + LongSharedSuffix + "z";

        var rejected = KnowledgeGraphBoundedEditDistance.Compute(left, right, maxDistance: 1);
        var accepted = KnowledgeGraphBoundedEditDistance.Compute(left, right, maxDistance: 2);

        rejected.ShouldBe(KnowledgeGraphBoundedEditDistance.NoMatchDistance);
        accepted.ShouldBe(2);
    }

    [Test]
    public void Bounded_distance_handles_long_residual_tokens_with_small_banded_rows()
    {
        var middle = new string('m', 70);
        var left = "A" + middle + "Z";
        var right = "B" + middle + "Y";

        var distance = KnowledgeGraphBoundedEditDistance.Compute(left, right, maxDistance: 2);

        distance.ShouldBe(2);
    }
}
