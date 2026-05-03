using System.Numerics;
using System.Runtime.InteropServices;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphCommonAffixTrimmer
{
    internal static TrimmedTokenPair Trim(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var prefixLength = CountCommonPrefix(left, right);
        left = left[prefixLength..];
        right = right[prefixLength..];
        if (left.Length == 0 || right.Length == 0)
        {
            return new TrimmedTokenPair(left, right);
        }

        var suffixLength = CountCommonSuffix(left, right);
        left = left[..(left.Length - suffixLength)];
        right = right[..(right.Length - suffixLength)];

        return new TrimmedTokenPair(left, right);
    }

    private static int CountCommonPrefix(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var length = Math.Min(left.Length, right.Length);
        var index = 0;
        if (Vector.IsHardwareAccelerated && length >= Vector<ushort>.Count)
        {
            var leftUnits = MemoryMarshal.Cast<char, ushort>(left[..length]);
            var rightUnits = MemoryMarshal.Cast<char, ushort>(right[..length]);
            var vectorSize = Vector<ushort>.Count;
            while (index <= length - vectorSize)
            {
                if (!VectorsMatch(leftUnits, rightUnits, index, vectorSize))
                {
                    break;
                }

                index += vectorSize;
            }
        }

        while (index < length && left[index] == right[index])
        {
            index++;
        }

        return index;
    }

    private static int CountCommonSuffix(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var length = Math.Min(left.Length, right.Length);
        var suffixLength = 0;
        if (Vector.IsHardwareAccelerated && length >= Vector<ushort>.Count)
        {
            var leftUnits = MemoryMarshal.Cast<char, ushort>(left[(left.Length - length)..]);
            var rightUnits = MemoryMarshal.Cast<char, ushort>(right[(right.Length - length)..]);
            var vectorSize = Vector<ushort>.Count;
            while (suffixLength <= length - vectorSize)
            {
                var start = length - suffixLength - vectorSize;
                if (!VectorsMatch(leftUnits, rightUnits, start, vectorSize))
                {
                    break;
                }

                suffixLength += vectorSize;
            }
        }

        while (suffixLength < length &&
               left[left.Length - suffixLength - 1] == right[right.Length - suffixLength - 1])
        {
            suffixLength++;
        }

        return suffixLength;
    }

    private static bool VectorsMatch(
        ReadOnlySpan<ushort> left,
        ReadOnlySpan<ushort> right,
        int start,
        int vectorSize)
    {
        var leftVector = new Vector<ushort>(left.Slice(start, vectorSize));
        var rightVector = new Vector<ushort>(right.Slice(start, vectorSize));
        return Vector.EqualsAll(leftVector, rightVector);
    }
}

internal readonly ref struct TrimmedTokenPair(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
{
    public ReadOnlySpan<char> Left { get; } = left;

    public ReadOnlySpan<char> Right { get; } = right;
}
