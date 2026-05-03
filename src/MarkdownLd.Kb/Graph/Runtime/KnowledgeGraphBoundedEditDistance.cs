namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBoundedEditDistance
{
    internal const int NoMatchDistance = int.MaxValue;
    private const int ExactDistance = 0;
    private const int BitVectorPatternLimit = sizeof(ulong) * 8;

    internal static int Compute(string left, string right, int maxDistance)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return ExactDistance;
        }

        if (Math.Abs(left.Length - right.Length) > maxDistance)
        {
            return NoMatchDistance;
        }

        var trimmed = KnowledgeGraphCommonAffixTrimmer.Trim(left.AsSpan(), right.AsSpan());
        if (trimmed.Left.Length == 0 || trimmed.Right.Length == 0)
        {
            var distance = Math.Max(trimmed.Left.Length, trimmed.Right.Length);
            return distance <= maxDistance ? distance : NoMatchDistance;
        }

        var pattern = trimmed.Left.Length <= trimmed.Right.Length ? trimmed.Left : trimmed.Right;
        var text = trimmed.Left.Length <= trimmed.Right.Length ? trimmed.Right : trimmed.Left;
        return pattern.Length <= BitVectorPatternLimit
            ? KnowledgeGraphBitVectorEditDistance.Compute(pattern, text, maxDistance)
            : KnowledgeGraphBandedEditDistance.Compute(trimmed.Left, trimmed.Right, maxDistance);
    }
}
