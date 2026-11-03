using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static void AppendOptionalSource(
        StringBuilder builder,
        string subjectVariable,
        string sourceVariable,
        string sourceLabelVariable,
        string indent,
        bool relationshipOnly = false)
    {
        // Keep source-label lookup inside the provenance OPTIONAL. An unbound source
        // must not turn the label patterns into a scan of unrelated graph subjects.
        builder.Append(indent).Append(SparqlOptionalKeyword).Append(SpaceCharacter).AppendLine(SparqlOpenBrace);
        var sourceIndent = indent + SparqlIndent;
        AppendTriple(builder, subjectVariable, ProvPrefix + ColonCharacter + ProvWasDerivedFromSuffix, sourceVariable, sourceIndent);
        AppendOptionalLabel(builder, sourceVariable, sourceLabelVariable, sourceIndent);
        if (relationshipOnly)
        {
            builder.Append(sourceIndent).Append(SparqlFilterKeyword).Append(OpenParenthesisCharacter)
                .Append(SparqlEvidenceKindVariable).Append('=')
                .Append(DoubleQuoteCharacter).Append(SchemaSearchRelationshipEvidenceKind).Append(DoubleQuoteCharacter)
                .AppendLine(SparqlCloseParenthesis);
        }

        builder.Append(indent).AppendLine(SparqlCloseBrace);
    }
}
