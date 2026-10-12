using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory("Tiktoken", "Search")]
public class TokenVectorDistanceBenchmarks
{
    private TokenVector _left = null!;
    private TokenVector _right = null!;

    [Params(16, 128)]
    public int WeightCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var left = new Dictionary<int, double>();
        var right = new Dictionary<int, double>();
        for (var index = 0; index < WeightCount; index++)
        {
            left[index] = index + 1d;
            right[index + WeightCount / 2] = WeightCount - index;
        }
        _left = TokenVector.Create(left);
        _right = TokenVector.Create(right);
    }

    [Benchmark]
    public double SparseVectorDistance() => _left.EuclideanDistanceTo(_right);
}
