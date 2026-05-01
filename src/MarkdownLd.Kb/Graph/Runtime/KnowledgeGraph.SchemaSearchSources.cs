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
        builder
            .Append(indent)
            .Append(SparqlOptionalKeyword)
            .Append(SpaceCharacter)
            .Append(OpenBraceCharacter)
            .Append(SpaceCharacter)
            .Append(subjectVariable)
            .Append(SpaceCharacter)
            .Append(ProvPrefix)
            .Append(ColonCharacter)
            .Append(ProvWasDerivedFromSuffix)
            .Append(SpaceCharacter)
            .Append(sourceVariable)
            .Append(SparqlStatementTerminator)
            .Append(SpaceCharacter)
            .Append(CloseBraceCharacter)
            .AppendLine();
        AppendOptionalLabel(builder, sourceVariable, sourceLabelVariable, indent);
    }
}
