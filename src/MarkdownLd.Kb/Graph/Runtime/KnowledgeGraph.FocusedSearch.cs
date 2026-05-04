using System.Runtime.InteropServices;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public async Task<KnowledgeGraphFocusedSearchResult> SearchFocusedAsync(
        string query,
        KnowledgeGraphFocusedSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();

        var effectiveOptions = options ?? new KnowledgeGraphFocusedSearchOptions();
        if (effectiveOptions.SchemaSearchProfile is not null)
        {
            return await SearchFocusedBySchemaAsync(query, effectiveOptions, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = ToSnapshot();
        var nodesById = CreateNodesById(snapshot.Nodes);
        var primary = await ResolvePrimaryMatchesAsync(query, effectiveOptions, nodesById, cancellationToken)
            .ConfigureAwait(false);
        var related = ResolveRelatedMatches(snapshot, primary, effectiveOptions);
        var nextSteps = ResolveNextStepMatches(snapshot, primary, effectiveOptions);
        var focusedGraph = BuildFocusedGraph(snapshot, primary, related, nextSteps);

        return new KnowledgeGraphFocusedSearchResult(primary, related, nextSteps, focusedGraph);
    }

    private async Task<KnowledgeGraphFocusedSearchResult> SearchFocusedBySchemaAsync(
        string query,
        KnowledgeGraphFocusedSearchOptions options,
        CancellationToken cancellationToken)
    {
        var schemaResult = await SearchBySchemaAsync(
                query,
                options.SchemaSearchProfile! with
                {
                    MaxResults = Math.Max(1, options.MaxPrimaryResults),
                    MaxRelatedResults = Math.Max(0, options.MaxRelatedResults),
                    MaxNextStepResults = Math.Max(0, options.MaxNextStepResults),
                },
                cancellationToken)
            .ConfigureAwait(false);

        return new KnowledgeGraphFocusedSearchResult(
            ConvertSchemaSearchMatches(schemaResult.Matches),
            ConvertSchemaSearchMatches(schemaResult.RelatedMatches),
            ConvertSchemaSearchMatches(schemaResult.NextStepMatches),
            schemaResult.FocusedGraph);
    }

    private static KnowledgeGraphFocusedSearchMatch[] ConvertSchemaSearchMatches(
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> matches)
    {
        var converted = new KnowledgeGraphFocusedSearchMatch[matches.Count];
        for (var index = 0; index < matches.Count; index++)
        {
            converted[index] = ConvertSchemaSearchMatch(matches[index]);
        }

        return converted;
    }

    private static KnowledgeGraphFocusedSearchMatch ConvertSchemaSearchMatch(KnowledgeGraphSchemaSearchMatch match)
    {
        return new KnowledgeGraphFocusedSearchMatch(
            match.NodeId,
            match.Label,
            ConvertSchemaSearchRole(match.Role),
            match.Score,
            match.SourceNodeId,
            match.ViaPredicateId);
    }

    private static KnowledgeGraphFocusedSearchRole ConvertSchemaSearchRole(KnowledgeGraphSchemaSearchRole role)
    {
        return role switch
        {
            KnowledgeGraphSchemaSearchRole.Related => KnowledgeGraphFocusedSearchRole.Related,
            KnowledgeGraphSchemaSearchRole.NextStep => KnowledgeGraphFocusedSearchRole.NextStep,
            _ => KnowledgeGraphFocusedSearchRole.Primary,
        };
    }

    private async Task<IReadOnlyList<KnowledgeGraphFocusedSearchMatch>> ResolvePrimaryMatchesAsync(
        string query,
        KnowledgeGraphFocusedSearchOptions options,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        CancellationToken cancellationToken)
    {
        if (options.SemanticIndex is not null)
        {
            var rankedMatches = await SearchRankedAsync(
                    query,
                    new KnowledgeGraphRankedSearchOptions
                    {
                        Mode = KnowledgeGraphSearchMode.Hybrid,
                        MaxResults = Math.Max(1, options.MaxPrimaryResults),
                    },
                    options.SemanticIndex,
                    cancellationToken)
                .ConfigureAwait(false);

            return CreatePrimaryMatchesFromRanked(rankedMatches, nodesById, options.MaxPrimaryResults);
        }

        if (_tokenIndex is not null)
        {
            var limit = Math.Max(options.MaxPrimaryResults * 4, options.MaxPrimaryResults);
            var tokenResults = await SearchByTokenDistanceAsync(query, limit, cancellationToken).ConfigureAwait(false);
            return CreatePrimaryMatchesFromTokenDistance(tokenResults, nodesById, options.MaxPrimaryResults);
        }

        var rankedGraphMatches = await SearchRankedAsync(
                query,
                new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Graph,
                    MaxResults = Math.Max(1, options.MaxPrimaryResults),
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return CreatePrimaryMatchesFromRanked(rankedGraphMatches, nodesById, options.MaxPrimaryResults);
    }

    private static KnowledgeGraphFocusedSearchMatch[] CreatePrimaryMatchesFromRanked(
        IReadOnlyList<KnowledgeGraphRankedSearchMatch> rankedMatches,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        int maxResults)
    {
        var limit = Math.Max(1, maxResults);
        var matches = new List<KnowledgeGraphFocusedSearchMatch>(Math.Min(rankedMatches.Count, limit));
        foreach (var rankedMatch in rankedMatches)
        {
            if (!nodesById.ContainsKey(rankedMatch.NodeId))
            {
                continue;
            }

            matches.Add(CreatePrimaryMatch(rankedMatch));
            if (matches.Count >= limit)
            {
                break;
            }
        }

        return matches.ToArray();
    }

    private static KnowledgeGraphFocusedSearchMatch[] CreatePrimaryMatchesFromTokenDistance(
        IReadOnlyList<TokenDistanceSearchResult> tokenResults,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        int maxResults)
    {
        var bestDistances = CreateBestTokenDistances(tokenResults, nodesById);
        var limit = Math.Max(1, maxResults);
        var matches = new List<KnowledgeGraphFocusedSearchMatch>(Math.Min(bestDistances.Count, limit));
        foreach (var pair in bestDistances)
        {
            if (nodesById.TryGetValue(pair.Key, out var node))
            {
                AddBoundedFocusedMatch(matches, CreatePrimaryMatch(node, pair.Value), limit);
            }
        }

        return matches.ToArray();
    }

    private static Dictionary<string, double> CreateBestTokenDistances(
        IReadOnlyList<TokenDistanceSearchResult> tokenResults,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById)
    {
        var bestDistances = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var result in tokenResults)
        {
            if (!nodesById.ContainsKey(result.DocumentId))
            {
                continue;
            }

            ref var distance = ref CollectionsMarshal.GetValueRefOrAddDefault(
                bestDistances,
                result.DocumentId,
                out var exists);
            if (!exists || result.Distance < distance)
            {
                distance = result.Distance;
            }
        }

        return bestDistances;
    }

    private static KnowledgeGraphFocusedSearchMatch CreatePrimaryMatch(
        KnowledgeGraphRankedSearchMatch match)
    {
        return new KnowledgeGraphFocusedSearchMatch(
            match.NodeId,
            match.Label,
            KnowledgeGraphFocusedSearchRole.Primary,
            match.Score);
    }

    private static KnowledgeGraphFocusedSearchMatch CreatePrimaryMatch(
        KnowledgeGraphNode node,
        double distance)
    {
        return new KnowledgeGraphFocusedSearchMatch(
            node.Id,
            node.Label,
            KnowledgeGraphFocusedSearchRole.Primary,
            ConvertDistanceToFocusedScore(distance));
    }

    private static IReadOnlyList<KnowledgeGraphFocusedSearchMatch> ResolveRelatedMatches(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> primary,
        KnowledgeGraphFocusedSearchOptions options)
    {
        var nodesById = CreateNodesById(snapshot.Nodes);
        var primaryIds = CreateFocusedMatchIdSet(primary);
        var matches = new Dictionary<string, KnowledgeGraphFocusedSearchMatch>(StringComparer.Ordinal);
        var articleNodeIds = CreateArticleNodeIds(snapshot);

        AddDirectRelatedMatches(snapshot, nodesById, primaryIds, matches);
        AddSharedGroupMatches(snapshot, nodesById, primaryIds, articleNodeIds, matches);

        return SortFocusedMatches(matches.Values, Math.Max(0, options.MaxRelatedResults));
    }

    private static void AddDirectRelatedMatches(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        IReadOnlySet<string> primaryIds,
        IDictionary<string, KnowledgeGraphFocusedSearchMatch> matches)
    {
        foreach (var edge in snapshot.Edges)
        {
            if (!primaryIds.Contains(edge.SubjectId) || edge.PredicateLabel != KbRelatedTo)
            {
                continue;
            }

            AddMatch(nodesById, matches, edge.ObjectId, KnowledgeGraphFocusedSearchRole.Related, edge.SubjectId, edge.PredicateLabel, 0.9d);
        }
    }

    private static void AddSharedGroupMatches(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        IReadOnlySet<string> primaryIds,
        IReadOnlySet<string> articleNodeIds,
        IDictionary<string, KnowledgeGraphFocusedSearchMatch> matches)
    {
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in snapshot.Edges)
        {
            if (primaryIds.Contains(edge.SubjectId) && edge.PredicateLabel == KbMemberOf)
            {
                groupIds.Add(edge.ObjectId);
            }
        }

        if (groupIds.Count == 0)
        {
            return;
        }

        foreach (var edge in snapshot.Edges)
        {
            if (!groupIds.Contains(edge.ObjectId) ||
                edge.PredicateLabel != KbMemberOf ||
                primaryIds.Contains(edge.SubjectId) ||
                !articleNodeIds.Contains(edge.SubjectId))
            {
                continue;
            }

            AddMatch(nodesById, matches, edge.SubjectId, KnowledgeGraphFocusedSearchRole.Related, edge.ObjectId, edge.PredicateLabel, 0.7d);
        }
    }

    private static IReadOnlyList<KnowledgeGraphFocusedSearchMatch> ResolveNextStepMatches(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> primary,
        KnowledgeGraphFocusedSearchOptions options)
    {
        var nodesById = CreateNodesById(snapshot.Nodes);
        var primaryIds = CreateFocusedMatchIdSet(primary);
        var matches = new Dictionary<string, KnowledgeGraphFocusedSearchMatch>(StringComparer.Ordinal);

        foreach (var edge in snapshot.Edges)
        {
            if (!primaryIds.Contains(edge.SubjectId) || edge.PredicateLabel != KbNextStep)
            {
                continue;
            }

            AddMatch(nodesById, matches, edge.ObjectId, KnowledgeGraphFocusedSearchRole.NextStep, edge.SubjectId, edge.PredicateLabel, 0.8d);
        }

        return SortFocusedMatches(matches.Values, Math.Max(0, options.MaxNextStepResults));
    }

    private static void AddMatch(
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        IDictionary<string, KnowledgeGraphFocusedSearchMatch> matches,
        string nodeId,
        KnowledgeGraphFocusedSearchRole role,
        string sourceNodeId,
        string predicateLabel,
        double score)
    {
        if (!nodesById.TryGetValue(nodeId, out var node))
        {
            return;
        }

        matches.TryAdd(nodeId, new KnowledgeGraphFocusedSearchMatch(
            node.Id,
            node.Label,
            role,
            score,
            sourceNodeId,
            predicateLabel));
    }

    private static double ConvertDistanceToFocusedScore(double distance)
        => distance <= 0 ? FullConfidence : FullConfidence / (FullConfidence + distance);
}
