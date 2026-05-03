using System.Text;
using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private const int SchemaNameLabelPriority = 0;
    private const int SkosPrefLabelPriority = 1;
    private const int RdfsLabelPriority = 2;

    private async Task<SchemaSearchExpansionResult> ResolveSchemaSearchExpansionAsync(
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> primary,
        KnowledgeGraphSchemaSearchPlan plan,
        CancellationToken cancellationToken)
    {
        if (primary.Count == 0 || plan.ExpansionPredicates.Count == 0)
        {
            return new SchemaSearchExpansionResult([], [], null);
        }

        var sparql = BuildSchemaSearchExpansionSparql(primary, plan);
        var rows = await ExecuteSelectAsync(sparql, cancellationToken).ConfigureAwait(false);
        var matches = BuildExpansionMatches(rows, plan, primary);
        return new SchemaSearchExpansionResult(
            matches.Where(static match => match.Role == KnowledgeGraphSchemaSearchRole.Related)
                .Take(plan.Profile.MaxRelatedResults)
                .ToArray(),
            matches.Where(static match => match.Role == KnowledgeGraphSchemaSearchRole.NextStep)
                .Take(plan.Profile.MaxNextStepResults)
                .ToArray(),
            sparql);
    }

    private static string BuildSchemaSearchExpansionSparql(
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> primary,
        KnowledgeGraphSchemaSearchPlan plan)
    {
        var builder = new StringBuilder();
        AppendPrefixLines(builder, CreateSchemaSearchPrefixes(plan.Profile));
        builder
            .Append(SparqlSelectKeyword)
            .Append(SpaceCharacter)
            .Append(SparqlDistinctKeyword)
            .Append(SpaceCharacter)
            .Append(SparqlSourceVariable)
            .Append(SpaceCharacter)
            .Append(SparqlNodeVariable)
            .Append(SpaceCharacter)
            .Append(SparqlPredicateVariable)
            .Append(SpaceCharacter)
            .Append(SparqlTypeVariable)
            .Append(SpaceCharacter)
            .Append(SparqlLabelVariable)
            .AppendLine();
        builder.AppendLine(SparqlWhereKeyword + SpaceText + SparqlOpenBrace);
        AppendValues(builder, SparqlSourceVariable, primary.Select(static match => match.NodeId), SparqlIndent);
        AppendValues(builder, SparqlPredicateVariable, plan.ExpansionPredicates.Select(static predicate => predicate.PredicateId), SparqlIndent);
        AppendTriple(builder, SparqlSourceVariable, SparqlPredicateVariable, SparqlNodeVariable, SparqlIndent);
        builder.Append(SparqlIndent).Append(SparqlOptionalKeyword).Append(SpaceCharacter).AppendLine(SparqlOpenBrace);
        AppendTriple(builder, SparqlNodeVariable, SparqlTypeShortcut, SparqlTypeVariable, SparqlDoubleIndent);
        builder.Append(SparqlIndent).AppendLine(SparqlCloseBrace);
        AppendOptionalLabel(builder, SparqlNodeVariable, SparqlLabelVariable, SparqlIndent);
        builder.AppendLine(SparqlCloseBrace);
        AppendLimit(builder, (plan.Profile.MaxRelatedResults + plan.Profile.MaxNextStepResults) * SchemaSearchEvidenceRowMultiplier);
        return builder.ToString();
    }

    private static IReadOnlyList<KnowledgeGraphSchemaSearchMatch> BuildExpansionMatches(
        SparqlQueryResult rows,
        KnowledgeGraphSchemaSearchPlan plan,
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> primary)
    {
        var primaryIds = primary.Select(static match => match.NodeId).ToHashSet(StringComparer.Ordinal);
        return rows.Rows
            .Where(row => TryGetRowValue(row, SchemaSearchNodeKey, out var nodeId) && !primaryIds.Contains(nodeId))
            .Select(row => CreateExpansionMatch(row, plan))
            .Where(static match => match is not null)
            .GroupBy(match => match!.NodeId, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(match => match!.Score).First()!)
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static KnowledgeGraphSchemaSearchMatch? CreateExpansionMatch(
        SparqlRow row,
        KnowledgeGraphSchemaSearchPlan plan)
    {
        if (!TryGetRowValue(row, SchemaSearchNodeKey, out var nodeId) ||
            !TryGetRowValue(row, SchemaSearchPredicateKey, out var predicateId) ||
            !TryGetRowValue(row, SchemaSearchSourceKey, out var sourceId))
        {
            return null;
        }

        var expansion = plan.ExpansionPredicates.FirstOrDefault(predicate => predicate.PredicateId == predicateId);
        if (expansion is null)
        {
            return null;
        }

        var label = TryGetRowValue(row, SchemaSearchLabelKey, out var rowLabel) ? rowLabel : nodeId;
        var type = TryGetRowValue(row, SchemaSearchTypeKey, out var rowType) ? rowType : null;
        var types = type is null ? [] : new[] { type };
        return new KnowledgeGraphSchemaSearchMatch(
            nodeId,
            label,
            expansion.Role,
            expansion.Score,
            types,
            [],
            SourceNodeId: sourceId,
            ViaPredicateId: predicateId);
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
            return CreateFocusedGraphSnapshot(selectedIds, _graph.Triples);
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    private static KnowledgeGraphSnapshot CreateFocusedGraphSnapshot(
        ISet<string> selectedIds,
        IEnumerable<Triple> triples)
    {
        var nodes = new Dictionary<string, INode>(StringComparer.Ordinal);
        var labels = new Dictionary<string, FocusedGraphLabel>(StringComparer.Ordinal);
        var edges = new List<KnowledgeGraphEdge>();

        foreach (var triple in triples)
        {
            AddFocusedGraphTriple(selectedIds, nodes, labels, edges, triple);
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

internal sealed record SchemaSearchExpansionResult(
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> Related,
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> NextSteps,
    string? GeneratedSparql);
