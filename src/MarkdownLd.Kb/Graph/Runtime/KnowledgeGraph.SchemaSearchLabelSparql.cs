using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static void AppendOptionalLabel(StringBuilder builder, string subjectVariable, string targetVariable, string indent)
    {
        builder.Append(indent).Append(SparqlOptionalKeyword).Append(SpaceCharacter).AppendLine(SparqlOpenBrace);
        var branchIndent = indent + SparqlIndent;
        AppendLabelBranch(builder, subjectVariable, targetVariable, SchemaNameText, [], branchIndent);
        builder.AppendLine(branchIndent + SparqlUnionKeyword);
        AppendLabelBranch(builder, subjectVariable, targetVariable, RdfsLabelText, [SchemaNameText], branchIndent);
        builder.AppendLine(branchIndent + SparqlUnionKeyword);
        AppendLabelBranch(builder, subjectVariable, targetVariable, SkosPrefLabelText, [SchemaNameText, RdfsLabelText], branchIndent);
        builder.AppendLine(indent + SparqlCloseBrace);
    }

    private static void AppendLabelBranch(
        StringBuilder builder,
        string subjectVariable,
        string targetVariable,
        string predicateId,
        IReadOnlyList<string> preferredPredicateIds,
        string indent)
    {
        builder.AppendLine(indent + SparqlOpenBrace);
        AppendTriple(builder, subjectVariable, SparqlUriOpen + predicateId + SparqlUriClose, targetVariable, indent + SparqlIndent);
        if (preferredPredicateIds.Count > 0)
        {
            builder.Append(indent).Append(SparqlIndent).Append(SparqlFilterKeyword).Append(SpaceCharacter)
                .Append(SparqlNotExistsKeyword).Append(SpaceCharacter).AppendLine(SparqlOpenBrace);
            var path = SparqlOpenParenthesis + string.Join(SchemaSearchAlternativePathSeparator, preferredPredicateIds.Select(static predicate => SparqlUriOpen + predicate + SparqlUriClose)) + SparqlCloseParenthesis;
            AppendTriple(builder, subjectVariable, path, targetVariable + SchemaSearchPreferredLabelVariableSuffix, indent + SparqlDoubleIndent);
            builder.AppendLine(indent + SparqlIndent + SparqlCloseBrace);
        }
        builder.AppendLine(indent + SparqlCloseBrace);
    }
}
