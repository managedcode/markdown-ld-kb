using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static IReadOnlyList<KnowledgeGraphSchemaSearchMatch> BuildExpansionMatches(
        SparqlQueryResult rows,
        KnowledgeGraphSchemaSearchPlan plan,
        IReadOnlyList<KnowledgeGraphSchemaSearchMatch> primary)
    {
        var primaryIds = primary.Select(static match => match.NodeId).ToHashSet(StringComparer.Ordinal);
        return rows.Rows
            .Where(row => !primaryIds.Contains(row.Values[SchemaSearchNodeKey]))
            .Select(row => CreateExpansionMatch(row, plan))
            .GroupBy(static match => (match.NodeId, match.Role))
            .Select(static group => MergeExpansionMatches(group))
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static KnowledgeGraphSchemaSearchMatch MergeExpansionMatches(
        IEnumerable<KnowledgeGraphSchemaSearchMatch> matches)
    {
        var best = matches.OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.ViaPredicateId, StringComparer.Ordinal)
            .ThenBy(static match => match.SourceNodeId, StringComparer.Ordinal)
            .ThenBy(static match => match.Label, StringComparer.Ordinal)
            .First();
        return best with
        {
            Types = matches.SelectMany(static match => match.Types)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
    }

    private static KnowledgeGraphSchemaSearchMatch CreateExpansionMatch(
        SparqlRow row,
        KnowledgeGraphSchemaSearchPlan plan)
    {
        // Generated mandatory triples, source VALUES and predicate BIND guarantee these bindings.
        var nodeId = row.Values[SchemaSearchNodeKey];
        var predicateId = row.Values[SchemaSearchPredicateKey];
        var sourceId = row.Values[SchemaSearchSourceKey];
        var expansion = plan.ExpansionPredicates.First(predicate => predicate.PredicateId == predicateId);

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

}
