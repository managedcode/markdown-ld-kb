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
        var snapshot = ToSnapshot();
        var nodesById = snapshot.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var primary = await ResolvePrimaryMatchesAsync(query, effectiveOptions, nodesById, cancellationToken)
            .ConfigureAwait(false);
        var related = ResolveRelatedMatches(snapshot, primary, effectiveOptions);
        var nextSteps = ResolveNextStepMatches(snapshot, primary, effectiveOptions);
        var focusedGraph = BuildFocusedGraph(snapshot, primary, related, nextSteps);

        return new KnowledgeGraphFocusedSearchResult(primary, related, nextSteps, focusedGraph);
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

            return rankedMatches
                .Where(match => nodesById.ContainsKey(match.NodeId))
                .Select(CreatePrimaryMatch)
                .Take(Math.Max(1, options.MaxPrimaryResults))
                .ToArray();
        }

        if (_tokenIndex is not null)
        {
            var limit = Math.Max(options.MaxPrimaryResults * 4, options.MaxPrimaryResults);
            var tokenResults = await SearchByTokenDistanceAsync(query, limit, cancellationToken).ConfigureAwait(false);
            return tokenResults
                .Where(result => nodesById.ContainsKey(result.DocumentId))
                .GroupBy(result => result.DocumentId, StringComparer.Ordinal)
                .Select(group => CreatePrimaryMatch(nodesById[group.Key], group.Min(static item => item.Distance)))
                .OrderByDescending(static match => match.Score)
                .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, options.MaxPrimaryResults))
                .ToArray();
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

        return rankedGraphMatches
            .Where(match => nodesById.ContainsKey(match.NodeId))
            .Select(CreatePrimaryMatch)
            .Take(Math.Max(1, options.MaxPrimaryResults))
            .ToArray();
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
        var nodesById = snapshot.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var primaryIds = primary.Select(static match => match.NodeId).ToHashSet(StringComparer.Ordinal);
        var matches = new Dictionary<string, KnowledgeGraphFocusedSearchMatch>(StringComparer.Ordinal);

        AddDirectRelatedMatches(snapshot, nodesById, primaryIds, matches);
        AddSharedGroupMatches(snapshot, nodesById, primaryIds, matches);

        return matches.Values
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, options.MaxRelatedResults))
            .ToArray();
    }

    private static void AddDirectRelatedMatches(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        IReadOnlySet<string> primaryIds,
        IDictionary<string, KnowledgeGraphFocusedSearchMatch> matches)
    {
        foreach (var edge in snapshot.Edges.Where(edge => primaryIds.Contains(edge.SubjectId) && edge.PredicateLabel == KbRelatedTo))
        {
            AddMatch(nodesById, matches, edge.ObjectId, KnowledgeGraphFocusedSearchRole.Related, edge.SubjectId, edge.PredicateLabel, 0.9d);
        }
    }

    private static void AddSharedGroupMatches(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        IReadOnlySet<string> primaryIds,
        IDictionary<string, KnowledgeGraphFocusedSearchMatch> matches)
    {
        var groupIds = snapshot.Edges
            .Where(edge => primaryIds.Contains(edge.SubjectId) && edge.PredicateLabel == KbMemberOf)
            .Select(static edge => edge.ObjectId)
            .ToHashSet(StringComparer.Ordinal);
        if (groupIds.Count == 0)
        {
            return;
        }

        foreach (var edge in snapshot.Edges.Where(edge => groupIds.Contains(edge.ObjectId) && edge.PredicateLabel == KbMemberOf))
        {
            if (primaryIds.Contains(edge.SubjectId) || !IsArticleNode(snapshot, edge.SubjectId))
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
        var nodesById = snapshot.Nodes.ToDictionary(static node => node.Id, StringComparer.Ordinal);
        var primaryIds = primary.Select(static match => match.NodeId).ToHashSet(StringComparer.Ordinal);
        var matches = new Dictionary<string, KnowledgeGraphFocusedSearchMatch>(StringComparer.Ordinal);

        foreach (var edge in snapshot.Edges.Where(edge => primaryIds.Contains(edge.SubjectId) && edge.PredicateLabel == KbNextStep))
        {
            AddMatch(nodesById, matches, edge.ObjectId, KnowledgeGraphFocusedSearchRole.NextStep, edge.SubjectId, edge.PredicateLabel, 0.8d);
        }

        return matches.Values
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, options.MaxNextStepResults))
            .ToArray();
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

    private static KnowledgeGraphSnapshot BuildFocusedGraph(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> primary,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> related,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> nextSteps)
    {
        var selectedMatchIds = primary
            .Concat(related)
            .Concat(nextSteps)
            .Select(static match => match.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        var explanatoryGroupIds = SelectExplanatoryGroupIds(snapshot, selectedMatchIds);
        var includedNodeIds = selectedMatchIds
            .Concat(explanatoryGroupIds)
            .ToHashSet(StringComparer.Ordinal);
        var includedEdges = SelectFocusedEdges(snapshot, selectedMatchIds, explanatoryGroupIds).ToArray();

        return new KnowledgeGraphSnapshot(
            snapshot.Nodes
                .Where(node => includedNodeIds.Contains(node.Id))
                .OrderBy(static node => node.Id, StringComparer.Ordinal)
                .ToArray(),
            includedEdges);
    }

    private static IReadOnlySet<string> SelectExplanatoryGroupIds(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlySet<string> selectedMatchIds)
    {
        return snapshot.Edges
            .Where(edge => selectedMatchIds.Contains(edge.SubjectId) && edge.PredicateLabel == KbMemberOf)
            .Select(static edge => edge.ObjectId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<KnowledgeGraphEdge> SelectFocusedEdges(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlySet<string> selectedMatchIds,
        IReadOnlySet<string> explanatoryGroupIds)
    {
        return snapshot.Edges
            .Where(edge =>
                (selectedMatchIds.Contains(edge.SubjectId) && selectedMatchIds.Contains(edge.ObjectId)) ||
                (selectedMatchIds.Contains(edge.SubjectId) &&
                 explanatoryGroupIds.Contains(edge.ObjectId) &&
                 edge.PredicateLabel == KbMemberOf))
            .OrderBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.PredicateId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.ObjectId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsArticleNode(KnowledgeGraphSnapshot snapshot, string nodeId)
        => snapshot.Edges.Any(edge =>
            edge.SubjectId == nodeId &&
            edge.PredicateId == RdfTypeText &&
            edge.ObjectId == SchemaArticleText);

    private static double ConvertDistanceToFocusedScore(double distance)
        => distance <= 0 ? FullConfidence : FullConfidence / (FullConfidence + distance);
}
