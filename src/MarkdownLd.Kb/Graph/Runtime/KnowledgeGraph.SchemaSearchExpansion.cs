using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
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

        var snapshot = ToSnapshot();
        var nodes = snapshot.Nodes
            .Where(node => selectedIds.Contains(node.Id))
            .OrderBy(static node => node.Id, StringComparer.Ordinal)
            .ToArray();
        var edges = snapshot.Edges
            .Where(edge => selectedIds.Contains(edge.SubjectId) && selectedIds.Contains(edge.ObjectId))
            .OrderBy(static edge => edge.SubjectId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.PredicateId, StringComparer.Ordinal)
            .ThenBy(static edge => edge.ObjectId, StringComparer.Ordinal)
            .ToArray();

        return new KnowledgeGraphSnapshot(nodes, edges);
    }
}

internal sealed record SchemaSearchExpansionResult(
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> Related,
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> NextSteps,
    string? GeneratedSparql);
