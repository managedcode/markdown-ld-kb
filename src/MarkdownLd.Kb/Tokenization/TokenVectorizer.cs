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
        foreach (var tokenId in tokenIds)
        {
            weights[tokenId] = FullConfidence;
        }

        return weights;
    }

    private static Dictionary<int, double> BuildTermFrequencyWeights(IReadOnlyList<int> tokenIds)
    {
        var weights = new Dictionary<int, double>(tokenIds.Count);
        foreach (var tokenId in tokenIds)
        {
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
        return documentFrequencies.ToDictionary(
            static pair => pair.Key,
            pair => CalculateIdfWeight(corpusTokenIds.Count, pair.Value));
    }

    private static Dictionary<int, double> CountDocumentFrequencies(IReadOnlyList<IReadOnlyList<int>> corpusTokenIds)
    {
        var frequencies = new Dictionary<int, double>();
        var seen = new HashSet<int>();
        foreach (var tokenIds in corpusTokenIds)
        {
            seen.Clear();
            foreach (var tokenId in tokenIds)
            {
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

internal sealed record TokenVector(IReadOnlyDictionary<int, double> Weights)
{
    public static TokenVector Create(IReadOnlyDictionary<int, double> weights)
    {
        if (weights.Count == 0)
        {
            return new TokenVector(new Dictionary<int, double>(0));
        }

        var magnitude = Math.Sqrt(CalculateSquaredMagnitude(weights));
        return new TokenVector(NormalizeWeights(weights, magnitude));
    }

    public double EuclideanDistanceTo(TokenVector other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var sum = ZeroConfidence;
        foreach (var pair in Weights)
        {
            var difference = pair.Value - other.Weights.GetValueOrDefault(pair.Key);
            sum += difference * difference;
        }

        foreach (var pair in other.Weights)
        {
            if (Weights.ContainsKey(pair.Key))
            {
                continue;
            }

            sum += pair.Value * pair.Value;
        }

        return Math.Sqrt(sum);
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
