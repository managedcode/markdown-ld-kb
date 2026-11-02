using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static readonly string[] SnapshotLabelPredicates = [SchemaNameText, SkosPrefLabelText, RdfsLabelText];

    private static KnowledgeGraphSnapshot CreateGraphSnapshot(IEnumerable<Triple> triples)
    {
        var renderedTriples = triples.Select(static triple => new SnapshotProjectionTriple(
            triple, RenderGraphNodeId(triple.Subject), RenderGraphNodeId(triple.Predicate), RenderGraphNodeId(triple.Object))).ToArray();
        var orderedIndices = CreateOrderedSnapshotIndices(renderedTriples);
        var nodes = new Dictionary<string, KnowledgeGraphNode>(StringComparer.Ordinal);
        var labels = CreateGraphNodeLabels(renderedTriples, orderedIndices);
        var predicateLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var edges = new KnowledgeGraphEdge[renderedTriples.Length];

        for (var index = 0; index < orderedIndices.Length; index++)
        {
            var triple = renderedTriples[orderedIndices[index]];
            var subject = AddSnapshotNode(nodes, triple.Value.Subject, triple.SubjectId, labels);
            var graphObject = AddSnapshotNode(nodes, triple.Value.Object, triple.ObjectId, labels);
            if (!predicateLabels.TryGetValue(triple.PredicateId, out var predicateLabel))
            {
                predicateLabel = RenderGraphNodeLabel(triple.Value.Predicate);
                predicateLabels.Add(triple.PredicateId, predicateLabel);
            }

            edges[index] = new KnowledgeGraphEdge(subject.Id, triple.PredicateId, predicateLabel, graphObject.Id);
        }

        return new KnowledgeGraphSnapshot(nodes.Values.OrderBy(static node => node.Id, StringComparer.Ordinal).ToArray(), edges);
    }

    private static int[] CreateOrderedSnapshotIndices(SnapshotProjectionTriple[] triples)
    {
        var indices = new int[triples.Length];
        for (var index = 0; index < indices.Length; index++)
        {
            indices[index] = index;
        }

        Array.Sort(indices, (left, right) =>
        {
            var compared = CompareSnapshotTriples(in triples[left], in triples[right]);
            return compared != 0 ? compared : left.CompareTo(right);
        });
        return indices;
    }

    private static int CompareSnapshotTriples(in SnapshotProjectionTriple left, in SnapshotProjectionTriple right)
    {
        var compared = StringComparer.Ordinal.Compare(left.SubjectId, right.SubjectId);
        if (compared != 0)
        {
            return compared;
        }

        compared = StringComparer.Ordinal.Compare(left.PredicateId, right.PredicateId);
        return compared != 0 ? compared : StringComparer.Ordinal.Compare(left.ObjectId, right.ObjectId);
    }

    private static IReadOnlyDictionary<string, string> CreateGraphNodeLabels(SnapshotProjectionTriple[] triples, int[] orderedIndices)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var predicateId in SnapshotLabelPredicates)
        {
            foreach (var index in orderedIndices)
            {
                var triple = triples[index];
                if (triple.PredicateId != predicateId || triple.Value.Object is not ILiteralNode literalNode)
                {
                    continue;
                }

                if (!labels.ContainsKey(triple.SubjectId) && !string.IsNullOrWhiteSpace(literalNode.Value))
                {
                    labels[triple.SubjectId] = literalNode.Value;
                }
            }
        }

        return labels;
    }

    private static KnowledgeGraphNode AddSnapshotNode(
        Dictionary<string, KnowledgeGraphNode> nodes,
        INode node,
        string id,
        IReadOnlyDictionary<string, string> labels)
    {
        if (!nodes.TryGetValue(id, out var graphNode))
        {
            graphNode = new KnowledgeGraphNode(id, RenderGraphNodeLabel(node, labels), GetGraphNodeKind(node));
            nodes.Add(id, graphNode);
        }

        return graphNode;
    }

    private readonly record struct SnapshotProjectionTriple(Triple Value, string SubjectId, string PredicateId, string ObjectId);
}
