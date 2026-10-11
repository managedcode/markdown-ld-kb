using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private const string InvalidSchemaCandidateMessage = "Schema search candidate IDs must be absolute SPARQL-safe IRIs.";
    private const string ForbiddenSchemaCandidateCharacters = "<>\\\"{}|^`";

    private static void ValidateSchemaCandidateNodeIds(IReadOnlyCollection<string>? nodeIds)
    {
        if (nodeIds is null)
        {
            return;
        }

        foreach (var nodeId in nodeIds)
        {
            if (string.IsNullOrWhiteSpace(nodeId) ||
                !Uri.TryCreate(nodeId, UriKind.Absolute, out _) ||
                nodeId.Any(static character => character <= SpaceCharacter || ForbiddenSchemaCandidateCharacters.Contains(character)))
            {
                throw new ArgumentException(InvalidSchemaCandidateMessage, nameof(nodeIds));
            }
        }
    }

    private static void AppendSchemaCandidateValues(StringBuilder builder, IReadOnlyCollection<string>? nodeIds, string indent)
    {
        if (nodeIds is not null)
        {
            AppendValues(builder, SparqlSubjectVariable, nodeIds.Distinct(StringComparer.Ordinal), indent);
        }
    }
}
