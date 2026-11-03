using System.Globalization;
using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private const string EvidencePriorityVariable = "?evidencePriority";
    private const string ExactEvidencePriorityOpen = "BIND(IF(LCASE(STR(";
    private const string ExactEvidencePriorityComparison = ")) = LCASE(\"";
    private const string PriorityIfOpen = "IF(";
    private const string PriorityIfClose = ", 1, 0)";
    private const string EvidenceOrder = "ORDER BY DESC(" + EvidencePriorityVariable + ") " + SparqlEvidenceKindVariable
        + " " + SparqlSubjectVariable + " " + SparqlEvidencePredicateVariable
        + " " + SparqlEvidenceValueVariable + " " + SparqlRelatedNodeVariable + " " + SparqlViaPredicateVariable
        + " " + SparqlTypeVariable + " " + SparqlLabelVariable + " " + SparqlDescriptionVariable
        + " " + SparqlRelatedLabelVariable;

    private static void AppendEvidencePriority(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchTermMode termMode)
    {
        var terms = termMode == KnowledgeGraphSchemaSearchTermMode.AnyTerm
            ? CreateSchemaSearchTerms(query, termMode)
            : [];
        builder.Append(SparqlIndent).Append(ExactEvidencePriorityOpen).Append(SparqlEvidenceValueVariable)
            .Append(ExactEvidencePriorityComparison).Append(EscapeSparqlLiteral(query.Trim()))
            .Append(DoubleQuoteCharacter).Append(CloseParenthesisCharacter).Append(SparqlCommaSpace)
            .Append((terms.Count + 1).ToString(CultureInfo.InvariantCulture))
            .Append(SparqlCommaSpace).Append('0').Append(CloseParenthesisCharacter);
        if (terms.Count > 1)
        {
            foreach (var term in terms)
            {
                builder.Append('+').Append(PriorityIfOpen);
                AppendContainsExpression(builder, term);
                builder.Append(PriorityIfClose);
            }
        }

        builder.Append(SpaceCharacter).Append(SparqlAsKeyword).Append(SpaceCharacter)
            .Append(EvidencePriorityVariable).AppendLine(SparqlCloseParenthesis);
    }
}
