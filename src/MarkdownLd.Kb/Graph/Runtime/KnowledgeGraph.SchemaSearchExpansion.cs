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
        builder.AppendLine(SparqlIndent + SparqlOpenBrace);
        var hasBranch = false;
        foreach (var predicateId in plan.ExpansionPredicates.Select(static predicate => predicate.PredicateId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            AppendEvidenceBranchStart(builder, SparqlDoubleIndent, hasBranch);
            AppendExpansionBranch(builder, primary, predicateId);
            hasBranch = true;
        }
        builder.AppendLine(SparqlIndent + SparqlCloseBrace);
        builder.Append(SparqlIndent).Append(SparqlOptionalKeyword).Append(SpaceCharacter).AppendLine(SparqlOpenBrace);
        AppendTriple(builder, SparqlNodeVariable, SparqlTypeShortcut, SparqlTypeVariable, SparqlDoubleIndent);
        builder.Append(SparqlIndent).AppendLine(SparqlCloseBrace);
        AppendOptionalLabel(builder, SparqlNodeVariable, SparqlLabelVariable, SparqlIndent);
        builder.AppendLine(SparqlCloseBrace);
        AppendLimit(builder, (plan.Profile.MaxRelatedResults + plan.Profile.MaxNextStepResults) * SchemaSearchEvidenceRowMultiplier);
        return builder.ToString();
    }

    private static void AppendExpansionBranch(StringBuilder builder, IReadOnlyList<KnowledgeGraphSchemaSearchMatch> primary, string predicateId)
    {
        var branchIndent = SparqlDoubleIndent + SparqlIndent;
        AppendValues(builder, SparqlSourceVariable, primary.Select(static match => match.NodeId), branchIndent);
        AppendTriple(builder, SparqlSourceVariable, SparqlUriOpen + predicateId + SparqlUriClose, SparqlNodeVariable, branchIndent);
        AppendBindUri(builder, predicateId, SparqlPredicateVariable, branchIndent);
        builder.AppendLine(SparqlDoubleIndent + SparqlCloseBrace);
    }
}

internal sealed record SchemaSearchExpansionResult(
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> Related,
    IReadOnlyList<KnowledgeGraphSchemaSearchMatch> NextSteps,
    string? GeneratedSparql);
