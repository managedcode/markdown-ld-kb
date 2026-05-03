using System.Text;
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
        var candidatesById = filteredCandidates.ToDictionary(static candidate => candidate.NodeId, StringComparer.Ordinal);
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

        var semanticMatches = (await semanticIndex
                .SearchAsync(
                    query,
                    effectiveOptions.MaxSemanticResults,
                    effectiveOptions.MinimumSemanticScore,
                    cancellationToken)
                .ConfigureAwait(false))
            .Where(match => candidatesById.ContainsKey(match.NodeId))
            .Select(match => OverrideCandidateMetadata(match, candidatesById[match.NodeId]))
            .ToArray();

        return effectiveOptions.Mode == KnowledgeGraphSearchMode.Semantic
            ? semanticMatches.Take(effectiveOptions.MaxResults).ToArray()
            : MergeHybridMatches(canonicalMatches, semanticMatches, effectiveOptions);
    }

    private static IReadOnlyList<KnowledgeGraphRankedSearchMatch> SearchCanonical(
        IReadOnlyList<KnowledgeGraphSearchCandidate> candidates,
        string query,
        int limit)
    {
        var normalizedQuery = query.Trim();
        return candidates
            .Select(candidate => CreateCanonicalMatch(candidate, normalizedQuery))
            .Where(match => match.Score > ZeroConfidence)
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    private static void ValidateCandidateNodeIds(IReadOnlyCollection<string>? candidateNodeIds)
    {
        if (candidateNodeIds?.Any(static id => string.IsNullOrWhiteSpace(id)) == true)
        {
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

        var allowedNodeIds = candidateNodeIds.ToHashSet(StringComparer.Ordinal);
        return candidates
            .Where(candidate => allowedNodeIds.Contains(candidate.NodeId))
            .ToArray();
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
        var relatedScore = candidate.RelatedLabels.Any(label => label.Contains(query, StringComparison.OrdinalIgnoreCase))
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
        var semanticByNodeId = semanticMatches.ToDictionary(static match => match.NodeId, StringComparer.Ordinal);

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

        return accumulators.Values
            .Select(static accumulator => accumulator.ToMatch())
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxResults)
            .ToArray();
    }

    private static void AddReciprocalRankScores(
        IDictionary<string, ReciprocalRankFusionAccumulator> accumulators,
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> matches,
        int rankOffset,
        bool isCanonical)
    {
        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            if (!accumulators.TryGetValue(match.NodeId, out var accumulator))
            {
                accumulator = new ReciprocalRankFusionAccumulator(match);
                accumulators.Add(match.NodeId, accumulator);
            }

            accumulator.Add(match, 1d / (rankOffset + index + 1), isCanonical);
        }
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

    internal static IReadOnlyList<KnowledgeGraphSearchCandidate> CreateSearchCandidates(KnowledgeGraphSnapshot snapshot)
    {
        var nodesById = snapshot.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var edgesBySubject = snapshot.Edges
            .GroupBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.Ordinal);
        var candidateNodeIds = snapshot.Edges
            .Where(edge => edge.PredicateId == RdfTypeText && nodesById.TryGetValue(edge.SubjectId, out var node) && node.Kind == KnowledgeGraphNodeKind.Uri)
            .Select(static edge => edge.SubjectId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal);
        var candidates = new List<KnowledgeGraphSearchCandidate>();

        foreach (var nodeId in candidateNodeIds)
        {
            if (!edgesBySubject.TryGetValue(nodeId, out var edges))
            {
                continue;
            }

            var label = ResolvePrimaryText(edges, nodesById, SchemaNameText);
            var description = ResolvePrimaryText(edges, nodesById, SchemaDescriptionText);
            var relatedLabels = ResolveSearchContextLabels(edges, nodesById);
            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(description) && relatedLabels.Count == 0)
            {
                continue;
            }

            candidates.Add(new KnowledgeGraphSearchCandidate(
                nodeId,
                label ?? nodesById[nodeId].Label,
                description,
                relatedLabels,
                ComposeSearchText(label ?? nodesById[nodeId].Label, description, relatedLabels)));
        }

        return candidates;
    }

    private static string? ResolvePrimaryText(
        IEnumerable<KnowledgeGraphEdge> edges,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        string predicateId)
    {
        foreach (var edge in edges.Where(edge => edge.PredicateId == predicateId))
        {
            if (nodesById.TryGetValue(edge.ObjectId, out var node) && !string.IsNullOrWhiteSpace(node.Label))
            {
                return node.Label;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveSearchContextLabels(
        IEnumerable<KnowledgeGraphEdge> edges,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById)
    {
        return edges
            .Where(edge => SearchContextPredicateIds.Contains(edge.PredicateId, StringComparer.Ordinal))
            .Select(edge => nodesById.TryGetValue(edge.ObjectId, out var node) ? node.Label : string.Empty)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static label => label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ComposeSearchText(
        string label,
        string? description,
        IReadOnlyList<string> relatedLabels)
    {
        var builder = new StringBuilder();
        AppendSearchText(builder, label);
        AppendSearchText(builder, description);

        foreach (var relatedLabel in relatedLabels)
        {
            AppendSearchText(builder, relatedLabel);
        }

        return builder.ToString();
    }

    private static void AppendSearchText(StringBuilder builder, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(text.Trim());
    }
}

internal sealed record KnowledgeGraphSearchCandidate(
    string NodeId,
    string Label,
    string? Description,
    IReadOnlyList<string> RelatedLabels,
    string SearchText);
