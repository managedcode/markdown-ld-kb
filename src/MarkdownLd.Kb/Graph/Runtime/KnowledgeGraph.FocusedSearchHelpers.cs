using System.Runtime.InteropServices;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static KnowledgeGraphSnapshot BuildFocusedGraph(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> primary,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> related,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> nextSteps)
    {
        var selectedMatchIds = CreateFocusedMatchIdSet(primary, related, nextSteps);
        var explanatoryGroupIds = SelectExplanatoryGroupIds(snapshot, selectedMatchIds);
        var includedNodeIds = CreateIncludedFocusedNodeIds(selectedMatchIds, explanatoryGroupIds);
        var includedEdges = SelectFocusedEdges(snapshot, selectedMatchIds, explanatoryGroupIds);
        var nodes = new List<KnowledgeGraphNode>(includedNodeIds.Count);
        foreach (var node in snapshot.Nodes)
        {
            if (includedNodeIds.Contains(node.Id))
            {
                nodes.Add(node);
            }
        }

        nodes.Sort(static (left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));

        return new KnowledgeGraphSnapshot(nodes.ToArray(), includedEdges);
    }

    private static IReadOnlySet<string> SelectExplanatoryGroupIds(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlySet<string> selectedMatchIds)
    {
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in snapshot.Edges)
        {
            if (selectedMatchIds.Contains(edge.SubjectId) && edge.PredicateLabel == KbMemberOf)
            {
                groupIds.Add(edge.ObjectId);
            }
        }

        return groupIds;
    }

    private static KnowledgeGraphEdge[] SelectFocusedEdges(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlySet<string> selectedMatchIds,
        IReadOnlySet<string> explanatoryGroupIds)
    {
        var edges = new List<KnowledgeGraphEdge>();
        foreach (var edge in snapshot.Edges)
        {
            if (selectedMatchIds.Contains(edge.SubjectId) &&
                (selectedMatchIds.Contains(edge.ObjectId) ||
                 (explanatoryGroupIds.Contains(edge.ObjectId) && edge.PredicateLabel == KbMemberOf)))
            {
                edges.Add(edge);
            }
        }

        edges.Sort(CompareGraphEdges);
        return edges.ToArray();
    }

    private static HashSet<string> CreateArticleNodeIds(KnowledgeGraphSnapshot snapshot)
    {
        var articleNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in snapshot.Edges)
        {
            if (edge.PredicateId == RdfTypeText && edge.ObjectId == SchemaArticleText)
            {
                articleNodeIds.Add(edge.SubjectId);
            }
        }

        return articleNodeIds;
    }

    private static HashSet<string> CreateFocusedMatchIdSet(
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> matches)
    {
        var ids = new HashSet<string>(matches.Count, StringComparer.Ordinal);
        foreach (var match in matches)
        {
            ids.Add(match.NodeId);
        }

        return ids;
    }

    private static HashSet<string> CreateFocusedMatchIdSet(
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> primary,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> related,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> nextSteps)
    {
        var ids = new HashSet<string>(primary.Count + related.Count + nextSteps.Count, StringComparer.Ordinal);
        AddFocusedMatchIds(ids, primary);
        AddFocusedMatchIds(ids, related);
        AddFocusedMatchIds(ids, nextSteps);
        return ids;
    }

    private static void AddFocusedMatchIds(
        ISet<string> ids,
        IReadOnlyList<KnowledgeGraphFocusedSearchMatch> matches)
    {
        foreach (var match in matches)
        {
            ids.Add(match.NodeId);
        }
    }

    private static HashSet<string> CreateIncludedFocusedNodeIds(
        IReadOnlySet<string> selectedMatchIds,
        IReadOnlySet<string> explanatoryGroupIds)
    {
        var includedNodeIds = new HashSet<string>(selectedMatchIds.Count + explanatoryGroupIds.Count, StringComparer.Ordinal);
        foreach (var id in selectedMatchIds)
        {
            includedNodeIds.Add(id);
        }

        foreach (var id in explanatoryGroupIds)
        {
            includedNodeIds.Add(id);
        }

        return includedNodeIds;
    }

    private static KnowledgeGraphFocusedSearchMatch[] SortFocusedMatches(
        IEnumerable<KnowledgeGraphFocusedSearchMatch> matches,
        int maxResults)
    {
        if (maxResults == 0)
        {
            return [];
        }

        var sorted = new List<KnowledgeGraphFocusedSearchMatch>(maxResults);
        foreach (var match in matches)
        {
            AddBoundedFocusedMatch(sorted, match, maxResults);
        }

        return sorted.ToArray();
    }

    private static void AddBoundedFocusedMatch(
        List<KnowledgeGraphFocusedSearchMatch> matches,
        KnowledgeGraphFocusedSearchMatch match,
        int maxResults)
    {
        var matchSpan = CollectionsMarshal.AsSpan(matches);
        for (var index = 0; index < matchSpan.Length; index++)
        {
            if (CompareFocusedMatches(match, matchSpan[index]) < 0)
            {
                matches.Insert(index, match);
                if (matches.Count > maxResults)
                {
                    matches.RemoveAt(matches.Count - 1);
                }

                return;
            }
        }

        if (matches.Count < maxResults)
        {
            matches.Add(match);
        }
    }

    private static int CompareFocusedMatches(KnowledgeGraphFocusedSearchMatch left, KnowledgeGraphFocusedSearchMatch right)
    {
        var scoreComparison = right.Score.CompareTo(left.Score);
        return scoreComparison != 0
            ? scoreComparison
            : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareGraphEdges(KnowledgeGraphEdge left, KnowledgeGraphEdge right)
    {
        var subjectComparison = string.Compare(left.SubjectId, right.SubjectId, StringComparison.Ordinal);
        if (subjectComparison != 0)
        {
            return subjectComparison;
        }

        var predicateComparison = string.Compare(left.PredicateId, right.PredicateId, StringComparison.Ordinal);
        return predicateComparison != 0
            ? predicateComparison
            : string.Compare(left.ObjectId, right.ObjectId, StringComparison.Ordinal);
    }
}
