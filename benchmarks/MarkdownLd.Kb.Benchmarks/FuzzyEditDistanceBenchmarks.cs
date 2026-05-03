using BenchmarkDotNet.Attributes;
using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

[BenchmarkCategory(BenchmarkCategories.Algorithm, BenchmarkCategories.Fuzzy)]
public class FuzzyEditDistanceBenchmarks
{
    private const string LongPrefix = "cachevalidationfingerprintcheckpointtoken";
    private const string LongSuffix = "manifestwindowrollbackevidence";
    private string _left = string.Empty;
    private string _right = string.Empty;
    private int _maxDistance;

    [Params(
        FuzzyTokenScenario.ShortDeletion,
        FuzzyTokenScenario.ShortSubstitution,
        FuzzyTokenScenario.LongInsertion,
        FuzzyTokenScenario.LongNoMatch)]
    public FuzzyTokenScenario Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_left, _right, _maxDistance) = Scenario switch
        {
            FuzzyTokenScenario.ShortDeletion => ("manifest", "manifst", 1),
            FuzzyTokenScenario.ShortSubstitution => ("restore", "restpre", 1),
            FuzzyTokenScenario.LongInsertion => (LongPrefix + LongSuffix, LongPrefix + "x" + LongSuffix, 1),
            _ => (LongPrefix + "alpha" + LongSuffix, LongPrefix + "omega" + LongSuffix, 2),
        };
    }

    [Benchmark(Baseline = true)]
    public int BoundedBitVectorOrBanded()
    {
        return KnowledgeGraphBoundedEditDistance.Compute(_left, _right, _maxDistance);
    }

    [Benchmark]
    public int NaiveLevenshtein()
    {
        return NaiveLevenshteinDistance.Compute(_left, _right);
    }
}
