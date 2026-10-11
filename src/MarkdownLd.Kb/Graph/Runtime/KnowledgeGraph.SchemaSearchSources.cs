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
        string indent)
    {
        // Keep source-label lookup inside the provenance OPTIONAL. An unbound source
        // must not turn the label patterns into a scan of unrelated graph subjects.
        builder.Append(indent).Append(SparqlOptionalKeyword).Append(SpaceCharacter).AppendLine(SparqlOpenBrace);
        var sourceIndent = indent + SparqlIndent;
        AppendTriple(builder, subjectVariable, ProvPrefix + ColonCharacter + ProvWasDerivedFromSuffix, sourceVariable, sourceIndent);
        AppendOptionalLabel(builder, sourceVariable, sourceLabelVariable, sourceIndent);
        builder.Append(indent).AppendLine(SparqlCloseBrace);
    }
}
