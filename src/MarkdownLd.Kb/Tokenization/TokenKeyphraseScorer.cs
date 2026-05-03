using System.Runtime.InteropServices;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class TokenKeyphraseScorer
{
    public static Dictionary<string, double> CountDocumentFrequencies(IReadOnlyList<KeyphraseCandidateGroup> candidates)
    {
        var frequencies = new Dictionary<string, double>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in candidates)
        {
            seen.Clear();
            seen.EnsureCapacity(group.Candidates.Count);
            foreach (var candidate in group.Candidates)
            {
                if (!seen.Add(candidate.Key))
                {
                    continue;
                }

                ref var frequency = ref CollectionsMarshal.GetValueRefOrAddDefault(frequencies, candidate.Key, out _);
                frequency += TokenCountIncrement;
            }
        }

        return frequencies;
    }

    public static List<ScoredKeyphraseCandidate> ScoreCandidates(
        IReadOnlyList<KeyphraseCandidate> candidates,
        IReadOnlyDictionary<string, double> documentFrequencies,
        int segmentCount)
    {
        var groups = new Dictionary<string, KeyphraseAccumulator>(candidates.Count, StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            ref var accumulator = ref CollectionsMarshal.GetValueRefOrAddDefault(groups, candidate.Key, out var exists);
            if (!exists)
            {
                accumulator = new KeyphraseAccumulator(candidate.Label, candidate.WordCount, candidate.CharacterCount);
            }

            accumulator.Count++;
        }

        var scored = new List<ScoredKeyphraseCandidate>(groups.Count);
        foreach (var pair in groups)
        {
            scored.Add(ScoreCandidate(pair.Key, pair.Value, documentFrequencies, segmentCount));
        }

        return scored;
    }

    public static int CompareScoredCandidates(ScoredKeyphraseCandidate left, ScoredKeyphraseCandidate right)
    {
        var scoreComparison = right.Score.CompareTo(left.Score);
        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        var wordCountComparison = right.WordCount.CompareTo(left.WordCount);
        return wordCountComparison != 0
            ? wordCountComparison
            : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
    }

    private static ScoredKeyphraseCandidate ScoreCandidate(
        string key,
        KeyphraseAccumulator accumulator,
        IReadOnlyDictionary<string, double> documentFrequencies,
        int segmentCount)
    {
        var idf = Math.Log((segmentCount + IdfSmoothingIncrement) /
                           (documentFrequencies[key] + IdfSmoothingIncrement)) + IdfWeightOffset;
        var phraseBoost = Math.Sqrt(accumulator.WordCount);
        var lengthBoost = Math.Log(accumulator.CharacterCount + IdfWeightOffset);
        var score = accumulator.Count * idf * phraseBoost * lengthBoost;
        return new ScoredKeyphraseCandidate(key, accumulator.Label, accumulator.WordCount, score);
    }

    private struct KeyphraseAccumulator(string label, int wordCount, int characterCount)
    {
        public readonly string Label = label;
        public readonly int WordCount = wordCount;
        public readonly int CharacterCount = characterCount;
        public int Count;
    }
}

internal sealed record ScoredKeyphraseCandidate(
    string Key,
    string Label,
    int WordCount,
    double Score);
