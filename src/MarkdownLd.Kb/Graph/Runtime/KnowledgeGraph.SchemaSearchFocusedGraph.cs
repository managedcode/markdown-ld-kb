using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private const int SchemaNameLabelPriority = 0;
    private const int SkosPrefLabelPriority = 1;
    private const int RdfsLabelPriority = 2;

    private static IReadOnlyList<Uri>? ResolveFocusedGraphSubjectUris(ISet<string> selectedIds)
    {
        var subjects = new List<Uri>(selectedIds.Count);
        foreach (var id in selectedIds)
        {
            if (id.StartsWith(LiteralNodePrefix, StringComparison.Ordinal)
                || id.StartsWith(BlankNodePrefix, StringComparison.Ordinal)
                || !Uri.TryCreate(id, UriKind.Absolute, out var uri))
            {
                // Rendered blank/literal identities require the general RDF projection.
                return null;
            }

            subjects.Add(uri);
        }

        return subjects;
    }

    private void AddObjectOnlyFocusedGraphNodes(
        IReadOnlyList<Uri> subjects,
        ISet<string> selectedIds,
        IDictionary<string, INode> nodes)
    {
        foreach (var subject in subjects)
        {
            if (nodes.ContainsKey(subject.AbsoluteUri))
            {
                continue;
            }

            var triple = _graph.GetTriplesWithObject(subject).FirstOrDefault();
            if (triple is not null)
            {
                var objectId = RenderGraphNodeId(triple.Object);
                if (selectedIds.Contains(objectId))
                {
                    nodes.TryAdd(objectId, triple.Object);
                }
            }
        }
    }

    private KnowledgeGraphSnapshot BuildSchemaSearchFocusedGraph(
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> primary,
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> related,
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> nextSteps)
    {
        var selectedIds = primary
            .Concat(related)
            .Concat(nextSteps)
            .Select(static match => match.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedIds.Count == 0)
        {
            return KnowledgeGraphSnapshot.Empty;
        }

        _graphLock.EnterReadLock();
        try
        {
            var subjects = ResolveFocusedGraphSubjectUris(selectedIds);
            var triples = subjects is null ? _graph.Triples : subjects.SelectMany(_graph.GetTriplesWithSubject);
            return CreateFocusedGraphSnapshot(selectedIds, triples, subjects);
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private KnowledgeGraphSnapshot CreateFocusedGraphSnapshot(
        ISet<string> selectedIds,
        IEnumerable<Triple> triples,
        IReadOnlyList<Uri>? subjects)
    {
        var nodes = new Dictionary<string, INode>(StringComparer.Ordinal);
        var labels = new Dictionary<string, FocusedGraphLabel>(StringComparer.Ordinal);
        var edges = new List<KnowledgeGraphEdge>();

        foreach (var triple in triples)
        {
            AddFocusedGraphTriple(selectedIds, nodes, labels, edges, triple);
        }

        if (subjects is not null)
        {
            AddObjectOnlyFocusedGraphNodes(subjects, selectedIds, nodes);
        }

        return CreateFocusedGraphSnapshot(nodes, labels, edges);
    }

    private static void AddFocusedGraphTriple(
        ISet<string> selectedIds,
        IDictionary<string, INode> nodes,
        IDictionary<string, FocusedGraphLabel> labels,
        ICollection<KnowledgeGraphEdge> edges,
        Triple triple)
    {
        var subjectId = RenderGraphNodeId(triple.Subject);
        var objectId = RenderGraphNodeId(triple.Object);
        var predicateId = RenderGraphNodeId(triple.Predicate);

        if (selectedIds.Contains(subjectId))
        {
            nodes.TryAdd(subjectId, triple.Subject);
            TryAddFocusedGraphLabel(labels, subjectId, predicateId, triple.Object);
        }

        if (selectedIds.Contains(objectId))
        {
            nodes.TryAdd(objectId, triple.Object);
        }

        if (!selectedIds.Contains(subjectId) || !selectedIds.Contains(objectId))
        {
            return;
        }

        edges.Add(new KnowledgeGraphEdge(
            subjectId,
            predicateId,
            RenderGraphNodeLabel(triple.Predicate),
            objectId));
    }

    private static void TryAddFocusedGraphLabel(
        IDictionary<string, FocusedGraphLabel> labels,
        string nodeId,
        string predicateId,
        INode graphObject)
    {
        if (graphObject is not ILiteralNode literalNode ||
            string.IsNullOrWhiteSpace(literalNode.Value) ||
            !TryGetFocusedGraphLabelPriority(predicateId, out var priority))
        {
            return;
        }

        if (labels.TryGetValue(nodeId, out var existing) &&
            (existing.Priority < priority ||
             existing.Priority == priority &&
             string.Compare(existing.Value, literalNode.Value, StringComparison.Ordinal) <= 0))
        {
            return;
        }

        labels[nodeId] = new FocusedGraphLabel(literalNode.Value, priority);
    }

    private static bool TryGetFocusedGraphLabelPriority(string predicateId, out int priority)
    {
        priority = predicateId switch
        {
            SchemaNameText => SchemaNameLabelPriority,
            SkosPrefLabelText => SkosPrefLabelPriority,
            RdfsLabelText => RdfsLabelPriority,
            _ => -1,
        };
        return priority >= 0;
    }

    private static KnowledgeGraphSnapshot CreateFocusedGraphSnapshot(
        IReadOnlyDictionary<string, INode> nodes,
        IReadOnlyDictionary<string, FocusedGraphLabel> labels,
        IReadOnlyList<KnowledgeGraphEdge> edges)
    {
        return new KnowledgeGraphSnapshot(
            nodes
                .Select(pair => CreateFocusedGraphNode(pair.Key, pair.Value, labels))
                .OrderBy(static node => node.Id, StringComparer.Ordinal)
                .ToArray(),
            edges
                .OrderBy(static edge => edge.SubjectId, StringComparer.Ordinal)
                .ThenBy(static edge => edge.PredicateId, StringComparer.Ordinal)
                .ThenBy(static edge => edge.ObjectId, StringComparer.Ordinal)
                .ToArray());
    }

    private static KnowledgeGraphNode CreateFocusedGraphNode(
        string nodeId,
        INode node,
        IReadOnlyDictionary<string, FocusedGraphLabel> labels)
    {
        var label = labels.TryGetValue(nodeId, out var focusedLabel)
            ? focusedLabel.Value
            : RenderGraphNodeLabel(node);
        return new KnowledgeGraphNode(nodeId, label, GetGraphNodeKind(node));
    }

    private sealed record FocusedGraphLabel(string Value, int Priority);
}
