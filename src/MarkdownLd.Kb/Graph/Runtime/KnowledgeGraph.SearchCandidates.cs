using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    internal static IReadOnlyList<KnowledgeGraphSearchCandidate> CreateSearchCandidates(KnowledgeGraphSnapshot snapshot)
    {
        var nodesById = CreateNodesById(snapshot.Nodes);
        var edgesBySubject = CreateEdgesBySubject(snapshot.Edges);
        var candidateNodeIds = CreateCandidateNodeIds(snapshot.Edges, nodesById);
        var candidates = new List<KnowledgeGraphSearchCandidate>(candidateNodeIds.Count);

        foreach (var nodeId in candidateNodeIds)
        {
            if (!edgesBySubject.TryGetValue(nodeId, out var edges))
            {
                continue;
            }

            var label = ResolvePrimaryText(edges, nodesById, SchemaNameText);
            var fallbackLabel = nodesById[nodeId].Label;
            var resolvedLabel = label ?? fallbackLabel;
            var description = ResolvePrimaryText(edges, nodesById, SchemaDescriptionText);
            var relatedLabels = ResolveSearchContextLabels(edges, nodesById);
            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(description) && relatedLabels.Count == 0)
            {
                continue;
            }

            candidates.Add(new KnowledgeGraphSearchCandidate(
                nodeId,
                resolvedLabel,
                description,
                relatedLabels,
                ComposeSearchText(resolvedLabel, description, relatedLabels)));
        }

        return candidates;
    }

    private static Dictionary<string, KnowledgeGraphNode> CreateNodesById(IReadOnlyList<KnowledgeGraphNode> nodes)
    {
        var nodesById = new Dictionary<string, KnowledgeGraphNode>(nodes.Count, StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            nodesById[node.Id] = node;
        }

        return nodesById;
    }

    private static Dictionary<string, List<KnowledgeGraphEdge>> CreateEdgesBySubject(IReadOnlyList<KnowledgeGraphEdge> edges)
    {
        var edgesBySubject = new Dictionary<string, List<KnowledgeGraphEdge>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (!edgesBySubject.TryGetValue(edge.SubjectId, out var subjectEdges))
            {
                subjectEdges = [];
                edgesBySubject.Add(edge.SubjectId, subjectEdges);
            }

            subjectEdges.Add(edge);
        }

        return edgesBySubject;
    }

    private static List<string> CreateCandidateNodeIds(
        IReadOnlyList<KnowledgeGraphEdge> edges,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var candidateNodeIds = new List<string>();
        foreach (var edge in edges)
        {
            if (edge.PredicateId != RdfTypeText ||
                !nodesById.TryGetValue(edge.SubjectId, out var node) ||
                node.Kind != KnowledgeGraphNodeKind.Uri ||
                !seen.Add(edge.SubjectId))
            {
                continue;
            }

            candidateNodeIds.Add(edge.SubjectId);
        }

        candidateNodeIds.Sort(StringComparer.Ordinal);
        return candidateNodeIds;
    }

    private static string? ResolvePrimaryText(
        IReadOnlyList<KnowledgeGraphEdge> edges,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById,
        string predicateId)
    {
        foreach (var edge in edges)
        {
            if (edge.PredicateId != predicateId ||
                !nodesById.TryGetValue(edge.ObjectId, out var node) ||
                string.IsNullOrWhiteSpace(node.Label))
            {
                continue;
            }

            return node.Label;
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveSearchContextLabels(
        IReadOnlyList<KnowledgeGraphEdge> edges,
        IReadOnlyDictionary<string, KnowledgeGraphNode> nodesById)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var labels = new List<string>();
        foreach (var edge in edges)
        {
            if (!IsSearchContextPredicate(edge.PredicateId) ||
                !nodesById.TryGetValue(edge.ObjectId, out var node) ||
                string.IsNullOrWhiteSpace(node.Label) ||
                !seen.Add(node.Label))
            {
                continue;
            }

            labels.Add(node.Label);
        }

        labels.Sort(StringComparer.OrdinalIgnoreCase);
        return labels.ToArray();
    }

    private static bool IsSearchContextPredicate(string predicateId)
    {
        foreach (var searchPredicateId in SearchContextPredicateIds)
        {
            if (predicateId.Equals(searchPredicateId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
