using System.Runtime.InteropServices;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private KnowledgeGraphSnapshot? _semanticSnapshot;

    public KnowledgeGraphSnapshot ToSemanticSnapshot()
    {
        _graphLock.EnterReadLock();
        try
        {
            if (Volatile.Read(ref _semanticSnapshot) is { } cached)
            {
                return cached;
            }
        }
        finally
        {
            _graphLock.ExitReadLock();
        }

        return InitializeSemanticSnapshot();
    }

    private KnowledgeGraphSnapshot InitializeSemanticSnapshot()
    {
        _graphLock.EnterUpgradeableReadLock();
        try
        {
            if (Volatile.Read(ref _semanticSnapshot) is { } cached)
            {
                return cached;
            }

            var snapshot = BuildSemanticSnapshot();
            Volatile.Write(ref _semanticSnapshot, snapshot);
            return snapshot;
        }
        finally
        {
            _graphLock.ExitUpgradeableReadLock();
        }
    }

    private KnowledgeGraphSnapshot BuildSemanticSnapshot()
    {
        var complete = CreateGraphSnapshot(EnumerateSemanticTriples());
        var keys = new HashSet<(string, string, string)>();
        // This private projection owns fresh arrays that have never escaped to a caller.
        var nodes = (KnowledgeGraphNode[])complete.Nodes;
        var edges = (KnowledgeGraphEdge[])complete.Edges;
        var edgeCount = 0;
        foreach (var edge in edges)
        {
            if (keys.Add((edge.SubjectId, edge.PredicateId, edge.ObjectId)))
            {
                edges[edgeCount++] = edge;
            }
        }

        if (edgeCount != edges.Length)
        {
            Array.Resize(ref edges, edgeCount);
        }

        return new KnowledgeGraphSnapshot(
            ImmutableCollectionsMarshal.AsImmutableArray(nodes), ImmutableCollectionsMarshal.AsImmutableArray(edges));
    }

    private IEnumerable<Triple> EnumerateSemanticTriples()
    {
        var classifications = new Dictionary<INode, bool>();
        foreach (var triple in _graph.Triples)
        {
            var excludedSubject = IsRetrievalInternalNode(triple.Subject, classifications);
            var excludedObject = IsRetrievalInternalNode(triple.Object, classifications);
            if (!excludedSubject && !excludedObject)
            {
                yield return triple;
            }
        }
    }

    private static bool IsRetrievalInternalNode(INode node, Dictionary<INode, bool> classifications)
    {
        if (node is ILiteralNode or IBlankNode)
        {
            return false;
        }

        if (node is not IUriNode uriNode)
        {
            throw new InvalidOperationException(UnsupportedGraphNodeMessagePrefix + node.NodeType);
        }

        if (!classifications.TryGetValue(node, out var excluded))
        {
            excluded = IsRetrievalInternalUri(uriNode.Uri);
            classifications.Add(node, excluded);
        }

        return excluded;
    }

    private static bool IsRetrievalInternalUri(Uri uri)
    {
        foreach (var segment in uri.Segments)
        {
            if (segment.Equals(TokenSectionIdPrefix, StringComparison.Ordinal) ||
                segment.Equals(TokenSegmentIdPrefix, StringComparison.Ordinal) ||
                segment.Equals(TokenTopicIdPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
