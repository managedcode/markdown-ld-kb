namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBitVectorEditDistance
{
    private const int UnitDistance = 1;
    private const int BitVectorPatternLimit = sizeof(ulong) * 8;
    private const int AsciiMaskCount = 128;
    private const ulong LowBit = 1UL;

    internal static int Compute(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text, int maxDistance)
    {
        Span<ulong> asciiMasks = stackalloc ulong[AsciiMaskCount];
        if (TryFillAsciiPatternMasks(pattern, asciiMasks))
        {
            return Compute(pattern.Length, text, maxDistance, new BitVectorPatternMasks(asciiMasks));
        }

        Span<char> patternChars = stackalloc char[BitVectorPatternLimit];
        Span<ulong> patternMasks = stackalloc ulong[BitVectorPatternLimit];
        var maskCount = FillLinearPatternMasks(pattern, patternChars, patternMasks);
        return Compute(
            pattern.Length,
            text,
            maxDistance,
            new BitVectorPatternMasks(patternChars[..maskCount], patternMasks[..maskCount]));
    }

    private static int Compute(
        int patternLength,
        ReadOnlySpan<char> text,
        int maxDistance,
        BitVectorPatternMasks patternMasks)
    {
        var activeMask = CreateActiveMask(patternLength);
        var topBit = LowBit << (patternLength - 1);
        var positive = activeMask;
        var negative = 0UL;
        var distance = patternLength;

        for (var index = 0; index < text.Length; index++)
        {
            var equality = patternMasks.Get(text[index]);
            var vertical = equality | negative;
            var horizontal = (((equality & positive) + positive) ^ positive) | equality;
            var positiveHorizontal = negative | ~(horizontal | positive);
            var negativeHorizontal = positive & horizontal;

            distance = AdjustDistance(distance, positiveHorizontal, negativeHorizontal, topBit);
            if (CannotReachThreshold(distance, text.Length - index - 1, maxDistance))
            {
                return KnowledgeGraphBoundedEditDistance.NoMatchDistance;
            }

            var shiftedPositive = ((positiveHorizontal << 1) | LowBit) & activeMask;
            var shiftedNegative = (negativeHorizontal << 1) & activeMask;
            positive = (shiftedNegative | ~(vertical | shiftedPositive)) & activeMask;
            negative = shiftedPositive & vertical;
        }

        return distance <= maxDistance ? distance : KnowledgeGraphBoundedEditDistance.NoMatchDistance;
    }

    private static bool TryFillAsciiPatternMasks(ReadOnlySpan<char> pattern, Span<ulong> masks)
    {
        for (var index = 0; index < pattern.Length; index++)
        {
            if (pattern[index] >= AsciiMaskCount)
            {
                return false;
            }

            masks[pattern[index]] |= LowBit << index;
        }

        return true;
    }

    private static int FillLinearPatternMasks(
        ReadOnlySpan<char> pattern,
        Span<char> patternChars,
        Span<ulong> patternMasks)
    {
        var maskCount = 0;
        for (var index = 0; index < pattern.Length; index++)
        {
            var maskIndex = patternChars[..maskCount].IndexOf(pattern[index]);
            if (maskIndex < 0)
            {
                patternChars[maskCount] = pattern[index];
                maskIndex = maskCount;
                maskCount++;
            }

            patternMasks[maskIndex] |= LowBit << index;
        }

        return maskCount;
    }

    private static ulong CreateActiveMask(int patternLength)
    {
        return patternLength == BitVectorPatternLimit
            ? ulong.MaxValue
            : (LowBit << patternLength) - LowBit;
    }

    private static int AdjustDistance(int distance, ulong positiveHorizontal, ulong negativeHorizontal, ulong topBit)
    {
        if ((positiveHorizontal & topBit) != 0UL)
        {
            return distance + UnitDistance;
        }

        return (negativeHorizontal & topBit) != 0UL ? distance - UnitDistance : distance;
    }

    private static bool CannotReachThreshold(int distance, int remainingTextLength, int maxDistance)
    {
        return distance - remainingTextLength > maxDistance;
    }
}

internal readonly ref struct BitVectorPatternMasks
{
    private readonly ReadOnlySpan<ulong> _asciiMasks;
    private readonly ReadOnlySpan<char> _patternChars;
    private readonly ReadOnlySpan<ulong> _patternMasks;

    public BitVectorPatternMasks(ReadOnlySpan<ulong> asciiMasks)
    {
        _asciiMasks = asciiMasks;
        _patternChars = [];
        _patternMasks = [];
    }

    public BitVectorPatternMasks(ReadOnlySpan<char> patternChars, ReadOnlySpan<ulong> patternMasks)
    {
        _asciiMasks = [];
        _patternChars = patternChars;
        _patternMasks = patternMasks;
    }

    public ulong Get(char value)
    {
        if (!_asciiMasks.IsEmpty)
        {
            return value < _asciiMasks.Length ? _asciiMasks[value] : 0UL;
        }

        var index = _patternChars.IndexOf(value);
        return index >= 0 ? _patternMasks[index] : 0UL;
    }
}
