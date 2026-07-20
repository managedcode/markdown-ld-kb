namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphCycleAnalyzer
{
    public static IReadOnlyList<KnowledgeGraphCycle> FindCycles(
        KnowledgeGraphSnapshot snapshot,
        IReadOnlySet<string> predicateIds,
        int maxComponents)
    {
        var nodeKinds = snapshot.Nodes.ToDictionary(static node => node.Id, static node => node.Kind, StringComparer.Ordinal);
        var edges = snapshot.Edges
            .Where(edge => predicateIds.Contains(edge.PredicateId))
            .Where(edge => IsUriEdge(edge, nodeKinds))
            .OrderBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.PredicateId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.ObjectId, StringComparer.Ordinal)
            .ToArray();
        var adjacency = CreateAdjacency(edges, reverse: false);
        var reverse = CreateAdjacency(edges, reverse: true);
        var finishOrder = CreateFinishOrder(adjacency);
        var components = CreateComponents(finishOrder, reverse);

        return components
            .Where(component => IsCyclic(component, edges))
            .Select(component => CreateCycle(component, edges))
            .OrderBy(static cycle => cycle.NodeIds[0], StringComparer.Ordinal)
            .Take(maxComponents)
            .ToArray();
    }

    private static bool IsUriEdge(
        KnowledgeGraphEdge edge,
        IReadOnlyDictionary<string, KnowledgeGraphNodeKind> nodeKinds)
    {
        return nodeKinds.TryGetValue(edge.SubjectId, out var subjectKind) &&
               subjectKind == KnowledgeGraphNodeKind.Uri &&
               nodeKinds.TryGetValue(edge.ObjectId, out var objectKind) &&
               objectKind == KnowledgeGraphNodeKind.Uri;
    }

    private static Dictionary<string, string[]> CreateAdjacency(
        IReadOnlyList<KnowledgeGraphEdge> edges,
        bool reverse)
    {
        var mutable = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            var source = reverse ? edge.ObjectId : edge.SubjectId;
            var target = reverse ? edge.SubjectId : edge.ObjectId;
            AddTarget(mutable, source, target);
            AddNode(mutable, target);
        }

        return mutable.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static void AddTarget(
        IDictionary<string, SortedSet<string>> adjacency,
        string source,
        string target)
    {
        AddNode(adjacency, source);
        adjacency[source].Add(target);
    }

    private static void AddNode(
        IDictionary<string, SortedSet<string>> adjacency,
        string nodeId)
    {
        adjacency.TryAdd(nodeId, new SortedSet<string>(StringComparer.Ordinal));
    }

    private static List<string> CreateFinishOrder(IReadOnlyDictionary<string, string[]> adjacency)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var finishOrder = new List<string>(adjacency.Count);
        foreach (var nodeId in adjacency.Keys.Order(StringComparer.Ordinal))
        {
            if (visited.Add(nodeId))
            {
                VisitForFinishOrder(nodeId, adjacency, visited, finishOrder);
            }
        }

        return finishOrder;
    }

    private static void VisitForFinishOrder(
        string start,
        IReadOnlyDictionary<string, string[]> adjacency,
        ISet<string> visited,
        ICollection<string> finishOrder)
    {
        var stack = new Stack<TraversalFrame>();
        stack.Push(new TraversalFrame(start, 0));
        while (stack.Count > 0)
        {
            var frame = stack.Pop();
            var targets = adjacency[frame.NodeId];
            if (frame.NextTargetIndex >= targets.Length)
            {
                finishOrder.Add(frame.NodeId);
                continue;
            }

            stack.Push(frame with { NextTargetIndex = frame.NextTargetIndex + 1 });
            var target = targets[frame.NextTargetIndex];
            if (visited.Add(target))
            {
                stack.Push(new TraversalFrame(target, 0));
            }
        }
    }

    private static IReadOnlyList<IReadOnlyList<string>> CreateComponents(
        IReadOnlyList<string> finishOrder,
        IReadOnlyDictionary<string, string[]> reverse)
    {
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<string>>();
        for (var index = finishOrder.Count - 1; index >= 0; index--)
        {
            if (assigned.Add(finishOrder[index]))
            {
                components.Add(CollectComponent(finishOrder[index], reverse, assigned));
            }
        }

        return components;
    }

    private static IReadOnlyList<string> CollectComponent(
        string start,
        IReadOnlyDictionary<string, string[]> reverse,
        ISet<string> assigned)
    {
        var component = new List<string>();
        var stack = new Stack<string>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            component.Add(current);
            foreach (var target in reverse[current])
            {
                if (assigned.Add(target))
                {
                    stack.Push(target);
                }
            }
        }

        component.Sort(StringComparer.Ordinal);
        return component;
    }

    private static bool IsCyclic(
        IReadOnlyList<string> component,
        IReadOnlyList<KnowledgeGraphEdge> edges)
    {
        return component.Count > 1 ||
               edges.Any(edge => edge.SubjectId == component[0] && edge.ObjectId == component[0]);
    }

    private static KnowledgeGraphCycle CreateCycle(
        IReadOnlyList<string> component,
        IReadOnlyList<KnowledgeGraphEdge> edges)
    {
        var nodeIds = component.ToHashSet(StringComparer.Ordinal);
        var componentEdges = edges
            .Where(edge => nodeIds.Contains(edge.SubjectId) && nodeIds.Contains(edge.ObjectId))
            .ToArray();
        return new KnowledgeGraphCycle(component, componentEdges);
    }

    private readonly record struct TraversalFrame(string NodeId, int NextTargetIndex);
}
