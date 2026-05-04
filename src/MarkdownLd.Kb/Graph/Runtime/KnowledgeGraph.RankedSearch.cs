using System.Runtime.InteropServices;
using Microsoft.Extensions.AI;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public async Task<KnowledgeGraphSemanticIndex> BuildSemanticIndexAsync(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(embeddingGenerator);
        cancellationToken.ThrowIfCancellationRequested();

        var candidates = CreateSearchCandidates(ToSnapshot());
        return await KnowledgeGraphSemanticIndex.CreateAsync(candidates, embeddingGenerator, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<KnowledgeGraphRankedSearchMatch>> SearchRankedAsync(
        string query,
        KnowledgeGraphRankedSearchOptions? options = null,
        KnowledgeGraphSemanticIndex? semanticIndex = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();

        return await SearchRankedCandidatesAsync(
                query,
                CreateSearchCandidates(ToSnapshot()),
                options,
                semanticIndex,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task<IReadOnlyList<KnowledgeGraphRankedSearchMatch>> SearchRankedCandidatesAsync(
        string query,
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        KnowledgeGraphRankedSearchOptions? options = null,
        KnowledgeGraphSemanticIndex? semanticIndex = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(candidates);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? new KnowledgeGraphRankedSearchOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveOptions.MaxResults);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveOptions.MaxSemanticResults);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveOptions.ReciprocalRankFusionRankOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(effectiveOptions.MaxFuzzyEditDistance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(effectiveOptions.MinimumFuzzyTokenLength);
        ValidateCandidateNodeIds(effectiveOptions.CandidateNodeIds);

        var filteredCandidates = FilterSearchCandidates(
            candidates,
            effectiveOptions.CandidateNodeIds);
        var canonicalMatches = effectiveOptions.Mode is KnowledgeGraphSearchMode.Graph or KnowledgeGraphSearchMode.Hybrid
            ? SearchCanonical(filteredCandidates, query, effectiveOptions.MaxResults)
            : [];
        if (effectiveOptions.Mode == KnowledgeGraphSearchMode.Graph)
        {
            return canonicalMatches;
        }

        if (effectiveOptions.Mode == KnowledgeGraphSearchMode.Bm25)
        {
            return KnowledgeGraphBm25Search.Search(filteredCandidates, query, effectiveOptions);
        }

        if (semanticIndex is null)
        {
            throw new InvalidOperationException(SemanticSearchRequiresIndexMessage);
        }

        var candidatesById = CreateCandidatesById(filteredCandidates);
        var searchedSemanticMatches = await semanticIndex
                .SearchAsync(
                    query,
                    effectiveOptions.MaxSemanticResults,
                    effectiveOptions.MinimumSemanticScore,
                    cancellationToken)
                .ConfigureAwait(false);
        var semanticMatches = CreateSemanticMatches(searchedSemanticMatches, candidatesById);

        return effectiveOptions.Mode == KnowledgeGraphSearchMode.Semantic
            ? TakeRankedMatches(semanticMatches, effectiveOptions.MaxResults)
            : MergeHybridMatches(canonicalMatches, semanticMatches, effectiveOptions);
    }

    private static IReadOnlyList<KnowledgeGraphRankedSearchMatch> SearchCanonical(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        string query,
        int limit)
    {
        var normalizedQuery = query.Trim();
        var matches = new List<KnowledgeGraphRankedSearchMatch>(Math.Min(candidates.Count, limit));
        foreach (var candidate in candidates)
        {
            var match = CreateCanonicalMatch(candidate, normalizedQuery);
            if (match.Score <= ZeroConfidence)
            {
                continue;
            }

            KnowledgeGraphBm25SearchResults.AddBoundedMatch(matches, match, limit);
        }

        return KnowledgeGraphBm25SearchResults.ToArray(matches);
    }

    private static void ValidateCandidateNodeIds(IReadOnlyCollection<string>? candidateNodeIds)
    {
        if (candidateNodeIds is null)
        {
            return;
        }

        foreach (var nodeId in candidateNodeIds)
        {
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                continue;
            }

            throw new ArgumentException(KnowledgeGraphRankedSearchDefaults.CandidateNodeIdsCannotContainEmptyMessage);
        }
    }

    private static IReadOnlyList<KnowledgeGraphSearchCandidate> FilterSearchCandidates(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        IReadOnlyCollection<string>? candidateNodeIds)
    {
        if (candidateNodeIds is null)
        {
            return candidates;
        }

        var allowedNodeIds = new HashSet<string>(candidateNodeIds.Count, StringComparer.Ordinal);
        foreach (var nodeId in candidateNodeIds)
        {
            allowedNodeIds.Add(nodeId);
        }

        var filtered = new List<KnowledgeGraphSearchCandidate>(Math.Min(candidates.Count, allowedNodeIds.Count));
        foreach (var candidate in candidates)
        {
            if (allowedNodeIds.Contains(candidate.NodeId))
            {
                filtered.Add(candidate);
            }
        }

        return filtered.Count == candidates.Count ? candidates : filtered.ToArray();
    }

    private static KnowledgeGraphRankedSearchMatch CreateCanonicalMatch(
        KnowledgeGraphSearchCandidate candidate,
        string query)
    {
        var score = ComputeCanonicalScore(candidate, query);
        return new KnowledgeGraphRankedSearchMatch(
            candidate.NodeId,
            candidate.Label,
            candidate.Description,
            KnowledgeGraphRankedSearchSource.Canonical,
            score,
            CanonicalScore: score);
    }

    private static double ComputeCanonicalScore(KnowledgeGraphSearchCandidate candidate, string query)
    {
        var labelScore = GetTextMatchScore(candidate.Label, query, CanonicalLabelContainsScore);
        var descriptionScore = GetTextMatchScore(candidate.Description, query, CanonicalDescriptionContainsScore);
        var relatedScore = ContainsRelatedLabel(candidate.RelatedLabels, query)
            ? CanonicalRelatedLabelContainsScore
            : ZeroConfidence;

        return Math.Max(labelScore, Math.Max(descriptionScore, relatedScore));
    }

    private static double GetTextMatchScore(string? text, string query, double containsScore)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ZeroConfidence;
        }

        var normalizedText = text.Trim();
        if (normalizedText.Equals(query, StringComparison.OrdinalIgnoreCase))
        {
            return FullConfidence;
        }

        return normalizedText.Contains(query, StringComparison.OrdinalIgnoreCase)
            ? containsScore
            : ZeroConfidence;
    }

    private static IReadOnlyList<KnowledgeGraphRankedSearchMatch> MergeHybridMatches(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> canonicalMatches,
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> semanticMatches,
        KnowledgeGraphRankedSearchOptions options)
    {
        return options.HybridFusionStrategy == KnowledgeGraphHybridFusionStrategy.ReciprocalRank
            ? MergeHybridMatchesByReciprocalRank(canonicalMatches, semanticMatches, options)
            : MergeHybridMatchesCanonicalFirst(canonicalMatches, semanticMatches, options.MaxResults);
    }

    private static IReadOnlyList<KnowledgeGraphRankedSearchMatch> MergeHybridMatchesCanonicalFirst(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> canonicalMatches,
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> semanticMatches,
        int maxResults)
    {
        var results = new List<KnowledgeGraphRankedSearchMatch>(maxResults);
        var seenNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var semanticByNodeId = CreateSemanticMatchesByNodeId(semanticMatches);

        foreach (var canonicalMatch in canonicalMatches)
        {
            if (results.Count >= maxResults || !seenNodeIds.Add(canonicalMatch.NodeId))
            {
                continue;
            }

            results.Add(
                semanticByNodeId.TryGetValue(canonicalMatch.NodeId, out var semanticMatch)
                    ? canonicalMatch with
                    {
                        Source = KnowledgeGraphRankedSearchSource.Merged,
                        Score = canonicalMatch.Score + semanticMatch.Score,
                        SemanticScore = semanticMatch.SemanticScore ?? semanticMatch.Score,
                    }
                    : canonicalMatch);
        }

        foreach (var semanticMatch in semanticMatches)
        {
            if (results.Count >= maxResults || !seenNodeIds.Add(semanticMatch.NodeId))
            {
                continue;
            }

            results.Add(semanticMatch);
        }

        return results;
    }

    private static IReadOnlyList<KnowledgeGraphRankedSearchMatch> MergeHybridMatchesByReciprocalRank(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> canonicalMatches,
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> semanticMatches,
        KnowledgeGraphRankedSearchOptions options)
    {
        var accumulators = new Dictionary<string, ReciprocalRankFusionAccumulator>(StringComparer.Ordinal);
        AddReciprocalRankScores(accumulators, canonicalMatches, options.ReciprocalRankFusionRankOffset, true);
        AddReciprocalRankScores(accumulators, semanticMatches, options.ReciprocalRankFusionRankOffset, false);

        return CreateReciprocalRankMatches(accumulators, options.MaxResults);
    }

    private static void AddReciprocalRankScores(
        Dictionary<string, ReciprocalRankFusionAccumulator> accumulators,
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> matches,
        int rankOffset,
        bool isCanonical)
    {
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            ref var accumulator = ref CollectionsMarshal.GetValueRefOrAddDefault(
                accumulators,
                match.NodeId,
                out _);
            accumulator.Add(match, 1d / (rankOffset + index + 1), isCanonical);
        }
    }

    private static Dictionary<string, KnowledgeGraphSearchCandidate> CreateCandidatesById(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates)
    {
        var candidatesById = new Dictionary<string, KnowledgeGraphSearchCandidate>(candidates.Count, StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            candidatesById[candidate.NodeId] = candidate;
        }

        return candidatesById;
    }

    private static KnowledgeGraphRankedSearchMatch[] CreateSemanticMatches(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> searchedSemanticMatches,
        IReadOnlyDictionary<string, KnowledgeGraphSearchCandidate> candidatesById)
    {
        var matches = new List<KnowledgeGraphRankedSearchMatch>(searchedSemanticMatches.Count);
        foreach (var match in searchedSemanticMatches)
        {
            if (candidatesById.TryGetValue(match.NodeId, out var candidate))
            {
                matches.Add(OverrideCandidateMetadata(match, candidate));
            }
        }

        return KnowledgeGraphBm25SearchResults.ToArray(matches);
    }

    private static KnowledgeGraphRankedSearchMatch[] TakeRankedMatches(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> matches,
        int maxResults)
    {
        var resultCount = Math.Min(matches.Count, maxResults);
        var results = new KnowledgeGraphRankedSearchMatch[resultCount];
        for (var index = 0; index < resultCount; index++)
        {
            results[index] = matches[index];
        }

        return results;
    }

    private static bool ContainsRelatedLabel(
        IReadOnlyList<string> labels,
        string query)
    {
        foreach (var label in labels)
        {
            if (label.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, KnowledgeGraphRankedSearchMatch> CreateSemanticMatchesByNodeId(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> matches)
    {
        var matchesByNodeId = new Dictionary<string, KnowledgeGraphRankedSearchMatch>(matches.Count, StringComparer.Ordinal);
        foreach (var match in matches)
        {
            matchesByNodeId[match.NodeId] = match;
        }

        return matchesByNodeId;
    }

    private static KnowledgeGraphRankedSearchMatch[] CreateReciprocalRankMatches(
        Dictionary<string, ReciprocalRankFusionAccumulator> accumulators,
        int maxResults)
    {
        var matches = new List<KnowledgeGraphRankedSearchMatch>(Math.Min(accumulators.Count, maxResults));
        foreach (var accumulator in accumulators.Values)
        {
            KnowledgeGraphBm25SearchResults.AddBoundedMatch(matches, accumulator.ToMatch(), maxResults);
        }

        return KnowledgeGraphBm25SearchResults.ToArray(matches);
    }

    private static KnowledgeGraphRankedSearchMatch OverrideCandidateMetadata(
        KnowledgeGraphRankedSearchMatch match,
        KnowledgeGraphSearchCandidate candidate)
    {
        return match with
        {
            Label = candidate.Label,
            Description = candidate.Description,
        };
    }

}
