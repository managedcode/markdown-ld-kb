using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static IEnumerable<IReadOnlyList<string>> CreateEvidencePredicateGroups(IEnumerable<string> predicateIds, bool useFixedPredicateBranches)
    {
        var distinct = predicateIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0 || !useFixedPredicateBranches)
        {
            yield return distinct;
            yield break;
        }
        foreach (var predicateId in distinct)
        {
            yield return new[] { predicateId };
        }
    }

    private static void AppendEvidenceTriple(StringBuilder builder, string subjectVariable, IReadOnlyList<string> predicateIds, string indent)
    {
        if (predicateIds.Count == 1)
        {
            AppendTriple(builder, subjectVariable, SparqlUriOpen + predicateIds[0] + SparqlUriClose, SparqlEvidenceValueVariable, indent);
            return;
        }
        AppendValues(builder, SparqlEvidencePredicateVariable, predicateIds, indent);
        AppendTriple(builder, subjectVariable, SparqlEvidencePredicateVariable, SparqlEvidenceValueVariable, indent);
    }

}
