using System.Buffers;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal readonly struct KnowledgeGraphBm25TermStatistics : IDisposable
{
    private readonly int _termCount;
    private readonly int _termFrequencyLength;
    private readonly int[] _documentFrequency;
    private readonly double[] _termFrequency;

    private KnowledgeGraphBm25TermStatistics(
        int termCount,
        int termFrequencyLength,
        int[] documentFrequency,
        double[] termFrequency)
    {
        _termCount = termCount;
        _termFrequencyLength = termFrequencyLength;
        _documentFrequency = documentFrequency;
        _termFrequency = termFrequency;
    }

    public static KnowledgeGraphBm25TermStatistics Rent(int documentCount, int termCount)
    {
        var termFrequencyLength = checked(documentCount * termCount);
        return new KnowledgeGraphBm25TermStatistics(
            termCount,
            termFrequencyLength,
            ArrayPool<int>.Shared.Rent(termCount),
            ArrayPool<double>.Shared.Rent(termFrequencyLength));
    }

    public int GetDocumentFrequency(int termIndex)
    {
        return _documentFrequency[termIndex];
    }

    public double GetTermFrequency(int documentIndex, int termIndex)
    {
        return _termFrequency[GetFrequencyIndex(documentIndex, termIndex)];
    }

    public Span<double> GetDocumentTermFrequencies(int documentIndex)
    {
        return _termFrequency.AsSpan(GetFrequencyIndex(documentIndex, 0), _termCount);
    }

    public void Clear()
    {
        Array.Clear(_documentFrequency, 0, _termCount);
        Array.Clear(_termFrequency, 0, _termFrequencyLength);
    }

    public void IncrementDocumentFrequency(int termIndex)
    {
        _documentFrequency[termIndex]++;
    }

    public void SetDocumentFrequency(int termIndex, int frequency)
    {
        _documentFrequency[termIndex] = frequency;
    }

    public void SetTermFrequency(int documentIndex, int termIndex, double frequency)
    {
        _termFrequency[GetFrequencyIndex(documentIndex, termIndex)] = frequency;
    }

    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_documentFrequency);
        ArrayPool<double>.Shared.Return(_termFrequency);
    }

    private int GetFrequencyIndex(int documentIndex, int termIndex)
    {
        return checked((documentIndex * _termCount) + termIndex);
    }
}
