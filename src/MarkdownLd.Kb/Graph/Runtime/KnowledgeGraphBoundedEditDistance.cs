namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBoundedEditDistance
{
    internal const int NoMatchDistance = int.MaxValue;
    private const int ExactDistance = 0;
    private const int UnitDistance = 1;
    private const int BitVectorPatternLimit = sizeof(ulong) * 8;
    private const ulong LowBit = 1UL;

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
            ? ComputeBitVectorDistance(pattern, text, maxDistance)
            : ComputeDynamicProgrammingDistance(trimmed.Left, trimmed.Right, maxDistance);
    }

    private static int ComputeBitVectorDistance(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text, int maxDistance)
    {
        var patternMasks = CreatePatternMasks(pattern);
        var activeMask = CreateActiveMask(pattern.Length);
        var topBit = LowBit << (pattern.Length - 1);
        var positive = activeMask;
        var negative = 0UL;
        var distance = pattern.Length;

        for (var index = 0; index < text.Length; index++)
        {
            patternMasks.TryGetValue(text[index], out var equality);
            var vertical = equality | negative;
            var horizontal = (((equality & positive) + positive) ^ positive) | equality;
            var positiveHorizontal = negative | ~(horizontal | positive);
            var negativeHorizontal = positive & horizontal;

            distance = AdjustBitVectorDistance(distance, positiveHorizontal, negativeHorizontal, topBit);
            if (CannotReachThreshold(distance, text.Length - index - 1, maxDistance))
            {
                return NoMatchDistance;
            }

            var shiftedPositive = ((positiveHorizontal << 1) | LowBit) & activeMask;
            var shiftedNegative = (negativeHorizontal << 1) & activeMask;
            positive = (shiftedNegative | ~(vertical | shiftedPositive)) & activeMask;
            negative = shiftedPositive & vertical;
        }

        return distance <= maxDistance ? distance : NoMatchDistance;
    }

    private static Dictionary<char, ulong> CreatePatternMasks(ReadOnlySpan<char> pattern)
    {
        var masks = new Dictionary<char, ulong>(pattern.Length);
        for (var index = 0; index < pattern.Length; index++)
        {
            masks[pattern[index]] = masks.GetValueOrDefault(pattern[index]) | (LowBit << index);
        }

        return masks;
    }

    private static ulong CreateActiveMask(int patternLength)
    {
        return patternLength == BitVectorPatternLimit
            ? ulong.MaxValue
            : (LowBit << patternLength) - LowBit;
    }

    private static int AdjustBitVectorDistance(
        int distance,
        ulong positiveHorizontal,
        ulong negativeHorizontal,
        ulong topBit)
    {
        if ((positiveHorizontal & topBit) != 0UL)
        {
            return distance + UnitDistance;
        }

        return (negativeHorizontal & topBit) != 0UL
            ? distance - UnitDistance
            : distance;
    }

    private static bool CannotReachThreshold(int distance, int remainingTextLength, int maxDistance)
    {
        return distance - remainingTextLength > maxDistance;
    }

    private static int ComputeDynamicProgrammingDistance(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int maxDistance)
    {
        var sentinel = maxDistance + UnitDistance;
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        Array.Fill(previous, sentinel);
        for (var column = 0; column <= Math.Min(right.Length, maxDistance); column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            Array.Fill(current, sentinel);
            if (row <= maxDistance)
            {
                current[0] = row;
            }

            if (!TryFillDynamicProgrammingRow(left, right, maxDistance, row, previous, current))
            {
                return NoMatchDistance;
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length] <= maxDistance
            ? previous[right.Length]
            : NoMatchDistance;
    }

    private static bool TryFillDynamicProgrammingRow(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int maxDistance,
        int row,
        int[] previous,
        int[] current)
    {
        var startColumn = Math.Max(1, row - maxDistance);
        var endColumn = Math.Min(right.Length, row + maxDistance);
        if (startColumn > endColumn)
        {
            return false;
        }

        var bestInRow = current[0];
        for (var column = startColumn; column <= endColumn; column++)
        {
            var cost = left[row - 1] == right[column - 1] ? ExactDistance : UnitDistance;
            current[column] = Math.Min(
                Math.Min(current[column - 1] + UnitDistance, previous[column] + UnitDistance),
                previous[column - 1] + cost);
            bestInRow = Math.Min(bestInRow, current[column]);
        }

        return bestInRow <= maxDistance;
    }
}
