using System.Globalization;
using System.Text;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public static string SerializeMermaidFlowchart(KnowledgeGraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return BuildMermaidFlowchart(snapshot);
    }

    public static string SerializeDotGraph(KnowledgeGraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return BuildDotGraph(snapshot);
    }

    private static KnowledgeGraphSnapshot CreateGraphSnapshot(IEnumerable<Triple> triples)
    {
        var nodes = new Dictionary<string, KnowledgeGraphNode>(StringComparer.Ordinal);
        var edges = new List<KnowledgeGraphEdge>();
        var orderedTriples = triples
            .OrderBy(static triple => RenderGraphNodeId(triple.Subject), StringComparer.Ordinal)
            .ThenBy(static triple => RenderGraphNodeId(triple.Predicate), StringComparer.Ordinal)
            .ThenBy(static triple => RenderGraphNodeId(triple.Object), StringComparer.Ordinal)
            .ToArray();
        var labels = CreateGraphNodeLabels(orderedTriples);

        foreach (var triple in orderedTriples)
        {
            var subject = AddSnapshotNode(nodes, triple.Subject, labels);
            var predicateId = RenderGraphNodeId(triple.Predicate);
            var predicateLabel = RenderGraphNodeLabel(triple.Predicate);
            var graphObject = AddSnapshotNode(nodes, triple.Object, labels);

            edges.Add(new KnowledgeGraphEdge(
                subject.Id,
                predicateId,
                predicateLabel,
                graphObject.Id));
        }

        return new KnowledgeGraphSnapshot(
            nodes.Values.OrderBy(static node => node.Id, StringComparer.Ordinal).ToArray(),
            edges);
    }

    private static IReadOnlyDictionary<string, string> CreateGraphNodeLabels(IEnumerable<Triple> triples)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var triple in triples)
        {
            if (RenderGraphNodeId(triple.Predicate) != SchemaNameText || triple.Object is not ILiteralNode literalNode)
            {
                continue;
            }

            var subjectId = RenderGraphNodeId(triple.Subject);
            if (!labels.ContainsKey(subjectId) && !string.IsNullOrWhiteSpace(literalNode.Value))
            {
                labels[subjectId] = literalNode.Value;
            }
        }

        return labels;
    }

    private static KnowledgeGraphNode AddSnapshotNode(
        IDictionary<string, KnowledgeGraphNode> nodes,
        INode node,
        IReadOnlyDictionary<string, string> labels)
    {
        var id = RenderGraphNodeId(node);
        if (!nodes.TryGetValue(id, out var graphNode))
        {
            graphNode = new KnowledgeGraphNode(id, RenderGraphNodeLabel(node, labels), GetGraphNodeKind(node));
            nodes[id] = graphNode;
        }

        return graphNode;
    }

    private static string BuildMermaidFlowchart(KnowledgeGraphSnapshot snapshot)
    {
        var nodeIds = CreateDiagramNodeIds(snapshot);
        var builder = new StringBuilder();
        builder.AppendLine(MermaidHeader);

        foreach (var node in snapshot.Nodes)
        {
            builder
                .Append(MermaidIndent)
                .Append(nodeIds[node.Id])
                .Append(MermaidNodeLabelOpen)
                .Append(EscapeDiagramLabel(node.Label))
                .Append(MermaidNodeLabelClose)
                .AppendLine();
        }

        foreach (var edge in snapshot.Edges)
        {
            builder
                .Append(MermaidIndent)
                .Append(nodeIds[edge.SubjectId])
                .Append(MermaidEdgeLabelOpen)
                .Append(EscapeDiagramLabel(edge.PredicateLabel))
                .Append(MermaidEdgeLabelClose)
                .Append(nodeIds[edge.ObjectId])
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildDotGraph(KnowledgeGraphSnapshot snapshot)
    {
        var nodeIds = CreateDiagramNodeIds(snapshot);
        var builder = new StringBuilder();
        builder.AppendLine(DotHeader);
        builder.AppendLine(DotRankDirection);

        foreach (var node in snapshot.Nodes)
        {
            builder
                .Append(MermaidIndent)
                .Append(DoubleQuoteCharacter)
                .Append(nodeIds[node.Id])
                .Append(DoubleQuoteCharacter)
                .Append(DotNodeLabelOpen)
                .Append(EscapeDiagramLabel(node.Label))
                .Append(DotLabelClose)
                .AppendLine();
        }

        foreach (var edge in snapshot.Edges)
        {
            builder
                .Append(MermaidIndent)
                .Append(DoubleQuoteCharacter)
                .Append(nodeIds[edge.SubjectId])
                .Append(DoubleQuoteCharacter)
                .Append(DotEdgeConnector)
                .Append(DoubleQuoteCharacter)
                .Append(nodeIds[edge.ObjectId])
                .Append(DoubleQuoteCharacter)
                .Append(DotEdgeLabelOpen)
                .Append(EscapeDiagramLabel(edge.PredicateLabel))
                .Append(DotLabelClose)
                .AppendLine();
        }

        builder.AppendLine(DotClose);
        return builder.ToString();
    }

    private static Dictionary<string, string> CreateDiagramNodeIds(KnowledgeGraphSnapshot snapshot)
    {
        var nodeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < snapshot.Nodes.Count; index++)
        {
            nodeIds[snapshot.Nodes[index].Id] = MermaidNodeIdPrefix + index.ToString(CultureInfo.InvariantCulture);
        }

        return nodeIds;
    }

    private static string RenderGraphNodeId(INode node)
    {
        return node switch
        {
            IUriNode uriNode => uriNode.Uri.AbsoluteUri,
            ILiteralNode literalNode => LiteralNodePrefix + literalNode.Value,
            IBlankNode blankNode => BlankNodePrefix + blankNode.InternalID,
            _ => throw new InvalidOperationException(UnsupportedGraphNodeMessagePrefix + node.NodeType),
        };
    }

    private static string RenderGraphNodeLabel(INode node)
    {
        return RenderGraphNodeLabel(node, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static string RenderGraphNodeLabel(INode node, IReadOnlyDictionary<string, string> labels)
    {
        var id = RenderGraphNodeId(node);
        if (labels.TryGetValue(id, out var label))
        {
            return label;
        }

        return node switch
        {
            IUriNode uriNode => CompactUri(uriNode.Uri),
            ILiteralNode literalNode => literalNode.Value,
            IBlankNode blankNode => BlankNodePrefix + blankNode.InternalID,
            _ => throw new InvalidOperationException(UnsupportedGraphNodeMessagePrefix + node.NodeType),
        };
    }

    private static KnowledgeGraphNodeKind GetGraphNodeKind(INode node)
    {
        return node switch
        {
            IUriNode => KnowledgeGraphNodeKind.Uri,
            ILiteralNode => KnowledgeGraphNodeKind.Literal,
            IBlankNode => KnowledgeGraphNodeKind.Blank,
            _ => throw new InvalidOperationException(UnsupportedGraphNodeMessagePrefix + node.NodeType),
        };
    }

    private static string CompactUri(Uri uri)
    {
        var text = uri.AbsoluteUri;
        if (text.StartsWith(SchemaNamespaceText, StringComparison.Ordinal))
        {
            return SchemaPrefix + Colon + text[SchemaNamespaceText.Length..];
        }

        if (text.StartsWith(KbNamespaceText, StringComparison.Ordinal))
        {
            return KbPrefix + Colon + text[KbNamespaceText.Length..];
        }

        if (text.StartsWith(ProvNamespaceText, StringComparison.Ordinal))
        {
            return ProvPrefix + Colon + text[ProvNamespaceText.Length..];
        }

        if (text.StartsWith(RdfNamespaceText, StringComparison.Ordinal))
        {
            return RdfPrefix + Colon + text[RdfNamespaceText.Length..];
        }

        if (text.StartsWith(RdfsNamespaceText, StringComparison.Ordinal))
        {
            return RdfsPrefix + Colon + text[RdfsNamespaceText.Length..];
        }

        if (text.StartsWith(OwlNamespaceText, StringComparison.Ordinal))
        {
            return OwlPrefix + Colon + text[OwlNamespaceText.Length..];
        }

        if (text.StartsWith(SkosNamespaceText, StringComparison.Ordinal))
        {
            return SkosPrefix + Colon + text[SkosNamespaceText.Length..];
        }

        if (text.StartsWith(XsdNamespaceText, StringComparison.Ordinal))
        {
            return XsdPrefix + Colon + text[XsdNamespaceText.Length..];
        }

        return text;
    }

    private static string EscapeDiagramLabel(string value)
    {
        return value.Replace(BackslashText, EscapedBackslashText, StringComparison.Ordinal)
            .Replace(QuoteText, EscapedQuoteText, StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');
    }
}
