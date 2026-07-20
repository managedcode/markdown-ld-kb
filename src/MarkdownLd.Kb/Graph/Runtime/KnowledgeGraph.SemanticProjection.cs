namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public KnowledgeGraphSnapshot ToSemanticSnapshot()
    {
        var complete = ToCompleteSnapshot();
        var excludedNodeIds = complete.Nodes
            .Where(static node => IsRetrievalInternalNode(node.Id))
            .Select(static node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var edgeKeys = new HashSet<(string SubjectId, string PredicateId, string ObjectId)>();
        var edges = complete.Edges
            .Where(edge => !excludedNodeIds.Contains(edge.SubjectId) && !excludedNodeIds.Contains(edge.ObjectId))
            .Where(edge => edgeKeys.Add((edge.SubjectId, edge.PredicateId, edge.ObjectId)))
            .ToArray();
        var includedNodeIds = edges
            .SelectMany(static edge => new[] { edge.SubjectId, edge.ObjectId })
            .ToHashSet(StringComparer.Ordinal);
        var nodes = complete.Nodes
            .Where(node => includedNodeIds.Contains(node.Id))
            .ToArray();
        return new KnowledgeGraphSnapshot(nodes, edges);
    }

    private static bool IsRetrievalInternalNode(string nodeId)
    {
        if (!Uri.TryCreate(nodeId, UriKind.Absolute, out var uri))
        {
            return false;
        }

        foreach (var segment in uri.Segments)
        {
            if (segment.Equals(PipelineConstants.TokenSectionIdPrefix, StringComparison.Ordinal) ||
                segment.Equals(PipelineConstants.TokenSegmentIdPrefix, StringComparison.Ordinal) ||
                segment.Equals(PipelineConstants.TokenTopicIdPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
