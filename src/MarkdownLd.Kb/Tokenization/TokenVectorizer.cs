using System.Runtime.InteropServices;
using Microsoft.ML.Tokenizers;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class TokenVectorizer
{
    private readonly Tokenizer _tokenizer;
    private readonly object _sync = new();

    public TokenVectorizer(string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        _tokenizer = TiktokenTokenizer.CreateForModel(modelName);
    }

    public IReadOnlyList<int> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        lock (_sync)
        {
            return _tokenizer.EncodeToIds(text);
        }
    }
}

internal sealed class TokenVectorSpace
{
    private readonly TokenVectorWeighting _weighting;
    private readonly IReadOnlyDictionary<int, double> _idfWeights;
    private readonly double _unseenTokenIdfWeight;

    private TokenVectorSpace(
        TokenVectorWeighting weighting,
        IReadOnlyDictionary<int, double> idfWeights,
        double unseenTokenIdfWeight)
    {
        _weighting = weighting;
        _idfWeights = idfWeights;
        _unseenTokenIdfWeight = unseenTokenIdfWeight;
    }

    public static TokenVectorSpace Fit(
        IReadOnlyList<IReadOnlyList<int>> corpusTokenIds,
        TokenVectorWeighting weighting)
    {
        ArgumentNullException.ThrowIfNull(corpusTokenIds);

        var idfWeights = weighting == TokenVectorWeighting.SubwordTfIdf
            ? FitIdfWeights(corpusTokenIds)
            : new Dictionary<int, double>();
        var unseenIdfWeight = CalculateIdfWeight(corpusTokenIds.Count, ZeroConfidence);
        return new TokenVectorSpace(weighting, idfWeights, unseenIdfWeight);
    }

    public TokenVector CreateVector(IReadOnlyList<int> tokenIds)
    {
        ArgumentNullException.ThrowIfNull(tokenIds);

        return TokenVector.Create(BuildWeights(tokenIds));
    }

    private Dictionary<int, double> BuildWeights(IReadOnlyList<int> tokenIds)
    {
        return _weighting switch
        {
            TokenVectorWeighting.Binary => BuildBinaryWeights(tokenIds),
            TokenVectorWeighting.SubwordTfIdf => BuildTfIdfWeights(tokenIds),
            _ => BuildTermFrequencyWeights(tokenIds),
        };
    }

    private static Dictionary<int, double> BuildBinaryWeights(IReadOnlyList<int> tokenIds)
    {
        var weights = new Dictionary<int, double>(tokenIds.Count);
        for (var index = 0; index < tokenIds.Count; index++)
        {
            var tokenId = tokenIds[index];
            weights[tokenId] = FullConfidence;
        }

        return weights;
    }

    private static Dictionary<int, double> BuildTermFrequencyWeights(IReadOnlyList<int> tokenIds)
    {
        var weights = new Dictionary<int, double>(tokenIds.Count);
        for (var index = 0; index < tokenIds.Count; index++)
        {
            var tokenId = tokenIds[index];
            ref var weight = ref CollectionsMarshal.GetValueRefOrAddDefault(weights, tokenId, out _);
            weight += TokenCountIncrement;
        }

        return weights;
    }

    private Dictionary<int, double> BuildTfIdfWeights(IReadOnlyList<int> tokenIds)
    {
        var weights = BuildTermFrequencyWeights(tokenIds);
        foreach (var tokenId in weights.Keys)
        {
            ref var weight = ref CollectionsMarshal.GetValueRefOrNullRef(weights, tokenId);
            weight *= _idfWeights.GetValueOrDefault(tokenId, _unseenTokenIdfWeight);
        }

        return weights;
    }

    private static Dictionary<int, double> FitIdfWeights(IReadOnlyList<IReadOnlyList<int>> corpusTokenIds)
    {
        var documentFrequencies = CountDocumentFrequencies(corpusTokenIds);
        var weights = new Dictionary<int, double>(documentFrequencies.Count);
        foreach (var pair in documentFrequencies)
        {
            weights.Add(pair.Key, CalculateIdfWeight(corpusTokenIds.Count, pair.Value));
        }

        return weights;
    }

    private static Dictionary<int, double> CountDocumentFrequencies(IReadOnlyList<IReadOnlyList<int>> corpusTokenIds)
    {
        var frequencies = new Dictionary<int, double>();
        var seen = new HashSet<int>();
        foreach (var tokenIds in corpusTokenIds)
        {
            seen.Clear();
            seen.EnsureCapacity(tokenIds.Count);
            for (var index = 0; index < tokenIds.Count; index++)
            {
                var tokenId = tokenIds[index];
                if (!seen.Add(tokenId))
                {
                    continue;
                }

                ref var frequency = ref CollectionsMarshal.GetValueRefOrAddDefault(frequencies, tokenId, out _);
                frequency += TokenCountIncrement;
            }
        }

        return frequencies;
    }

    private static double CalculateIdfWeight(int documentCount, double documentFrequency)
    {
        return Math.Log((documentCount + IdfSmoothingIncrement) / (documentFrequency + IdfSmoothingIncrement)) + IdfWeightOffset;
    }
}

internal sealed record TokenVector
{
    private const double NormalizedSquaredDistanceScale = 2d;

    private TokenVector(IReadOnlyDictionary<int, double> weights, double squaredMagnitude)
    {
        Weights = weights;
        SquaredMagnitude = squaredMagnitude;
    }

    public IReadOnlyDictionary<int, double> Weights { get; }

    private double SquaredMagnitude { get; }

    public static TokenVector Create(IReadOnlyDictionary<int, double> weights)
    {
        if (weights.Count == 0)
        {
            return new TokenVector(new Dictionary<int, double>(0), ZeroConfidence);
        }

        var magnitude = Math.Sqrt(CalculateSquaredMagnitude(weights));
        var normalized = NormalizeWeights(weights, magnitude);
        return new TokenVector(normalized, CalculateSquaredMagnitude(normalized));
    }

    public double EuclideanDistanceTo(TokenVector other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (Weights.Count == 0 || other.Weights.Count == 0)
        {
            return Weights.Count == other.Weights.Count ? ZeroConfidence : FullConfidence;
        }

        var dotProduct = CalculateDotProduct(Weights, other.Weights);
        return Math.Sqrt(Math.Max(ZeroConfidence, SquaredMagnitude + other.SquaredMagnitude - (NormalizedSquaredDistanceScale * dotProduct)));
    }

    private static double CalculateDotProduct(
        IReadOnlyDictionary<int, double> left,
        IReadOnlyDictionary<int, double> right)
    {
        var smaller = left.Count <= right.Count ? left : right;
        var larger = left.Count <= right.Count ? right : left;
        var dotProduct = ZeroConfidence;
        foreach (var pair in smaller)
        {
            if (!larger.TryGetValue(pair.Key, out var otherWeight))
            {
                continue;
            }

            dotProduct += pair.Value * otherWeight;
        }

        return dotProduct;
    }

    private static double CalculateSquaredMagnitude(IReadOnlyDictionary<int, double> weights)
    {
        var sum = ZeroConfidence;
        foreach (var weight in weights.Values)
        {
            sum += weight * weight;
        }

        return sum;
    }

    private static Dictionary<int, double> NormalizeWeights(IReadOnlyDictionary<int, double> weights, double magnitude)
    {
        var normalized = new Dictionary<int, double>(weights.Count);
        foreach (var pair in weights)
        {
            normalized[pair.Key] = pair.Value / magnitude;
        }

        return normalized;
    }
}
