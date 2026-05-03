using System.Buffers;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphBandedEditDistance
{
    private const int ExactDistance = 0;
    private const int UnitDistance = 1;

    internal static int Compute(ReadOnlySpan<char> left, ReadOnlySpan<char> right, int maxDistance)
    {
        var sentinel = maxDistance + UnitDistance;
        var rowLength = right.Length + 1;
        var previous = ArrayPool<int>.Shared.Rent(rowLength);
        var current = ArrayPool<int>.Shared.Rent(rowLength);

        try
        {
            return ComputeWithRows(left, right, maxDistance, previous, current, sentinel);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(previous);
            ArrayPool<int>.Shared.Return(current);
        }
    }

    private static int ComputeWithRows(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int maxDistance,
        int[] previous,
        int[] current,
        int sentinel)
    {
        var rowLength = right.Length + 1;
        var previousRow = previous.AsSpan(0, rowLength);
        var currentRow = current.AsSpan(0, rowLength);
        FillInitialRow(previousRow, right.Length, maxDistance, sentinel);

        for (var row = 1; row <= left.Length; row++)
        {
            currentRow.Fill(sentinel);
            if (row <= maxDistance)
            {
                currentRow[0] = row;
            }

            if (!TryFillRow(left, right, maxDistance, row, previousRow, currentRow))
            {
                return KnowledgeGraphBoundedEditDistance.NoMatchDistance;
            }

            var rowSwap = previousRow;
            previousRow = currentRow;
            currentRow = rowSwap;
        }

        return previousRow[right.Length] <= maxDistance
            ? previousRow[right.Length]
            : KnowledgeGraphBoundedEditDistance.NoMatchDistance;
    }

    private static void FillInitialRow(Span<int> previous, int rightLength, int maxDistance, int sentinel)
    {
        previous.Fill(sentinel);
        for (var column = 0; column <= Math.Min(rightLength, maxDistance); column++)
        {
            previous[column] = column;
        }
    }

    private static bool TryFillRow(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right,
        int maxDistance,
        int row,
        ReadOnlySpan<int> previous,
        Span<int> current)
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
