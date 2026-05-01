using System.Globalization;
using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static void AppendFederatedSearchBody(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchProfile profile,
        IReadOnlyList<ResolvedSchemaTextPredicate> textPredicates,
        IReadOnlyList<ResolvedSchemaRelationshipPredicate> relationships,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters)
    {
        for (var index = 0; index < profile.FederatedServiceEndpoints.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine(SparqlIndent + SparqlUnionKeyword);
            }

            var endpoint = profile.FederatedServiceEndpoints[index].AbsoluteUri;
            builder.AppendLine(SparqlIndent + SparqlOpenBrace);
            AppendBindUri(builder, endpoint, SparqlServiceEndpointVariable, SparqlDoubleIndent);
            builder
                .Append(SparqlDoubleIndent)
                .Append(SparqlServiceKeyword)
                .Append(SpaceCharacter)
                .Append(LessThanCharacter)
                .Append(endpoint)
                .Append(GreaterThanCharacter)
                .Append(SpaceCharacter)
                .AppendLine(SparqlOpenBrace);
            AppendSchemaSearchBody(builder, query, profile.TermMode, textPredicates, relationships, typeFilters, excludedTypes, facetFilters, SparqlDoubleIndent + SparqlIndent);
            builder.AppendLine(SparqlDoubleIndent + SparqlCloseBrace);
            builder.AppendLine(SparqlIndent + SparqlCloseBrace);
        }
    }

    private static void AppendSchemaSearchBody(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchTermMode termMode,
        IReadOnlyList<ResolvedSchemaTextPredicate> textPredicates,
        IReadOnlyList<ResolvedSchemaRelationshipPredicate> relationships,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent)
    {
        builder.AppendLine(indent + SparqlOpenBrace);
        AppendDirectEvidencePattern(builder, query, termMode, textPredicates, typeFilters, excludedTypes, facetFilters, indent + SparqlIndent);
        builder.AppendLine(indent + SparqlCloseBrace);

        foreach (var relationship in relationships)
        {
            builder.AppendLine(indent + SparqlUnionKeyword);
            builder.AppendLine(indent + SparqlOpenBrace);
            AppendRelationshipEvidencePattern(builder, query, termMode, relationship, typeFilters, excludedTypes, facetFilters, indent + SparqlIndent);
            builder.AppendLine(indent + SparqlCloseBrace);
        }
    }

    private static void AppendDirectEvidencePattern(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchTermMode termMode,
        IReadOnlyList<ResolvedSchemaTextPredicate> textPredicates,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent)
    {
        AppendSubjectTypeAndLabels(builder, indent);
        AppendFacetFilters(builder, facetFilters, indent);
        AppendValues(builder, SparqlEvidencePredicateVariable, textPredicates.Select(static predicate => predicate.PredicateId), indent);
        AppendTriple(builder, SparqlSubjectVariable, SparqlEvidencePredicateVariable, SparqlEvidenceValueVariable, indent);
        AppendLiteralFilter(builder, query, termMode, indent);
        AppendTypeFilters(builder, typeFilters, excludedTypes, indent);
        AppendBindText(builder, SchemaSearchDirectEvidenceKind, SparqlEvidenceKindVariable, indent);
    }

    private static void AppendRelationshipEvidencePattern(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchTermMode termMode,
        ResolvedSchemaRelationshipPredicate relationship,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent)
    {
        AppendSubjectTypeAndLabels(builder, indent);
        AppendFacetFilters(builder, facetFilters, indent);
        AppendValues(
            builder,
            SparqlEvidencePredicateVariable,
            relationship.TargetTextPredicateIds.Distinct(StringComparer.Ordinal),
            indent);
        AppendRelationshipPathTriple(builder, relationship, indent);
        AppendOptionalSource(builder, SparqlRelatedNodeVariable, SparqlRelatedSourceVariable, SparqlRelatedSourceLabelVariable, indent);
        AppendTriple(builder, SparqlRelatedNodeVariable, SparqlEvidencePredicateVariable, SparqlEvidenceValueVariable, indent);
        AppendLiteralFilter(builder, query, termMode, indent);
        AppendOptionalLabel(builder, SparqlRelatedNodeVariable, SparqlRelatedLabelVariable, indent);
        AppendTypeFilters(builder, typeFilters, excludedTypes, indent);
        AppendBindText(builder, relationship.PredicateId, SparqlViaPredicateVariable, indent);
        AppendBindText(builder, SchemaSearchRelationshipEvidenceKind, SparqlEvidenceKindVariable, indent);
    }

    private static void AppendSubjectTypeAndLabels(StringBuilder builder, string indent)
    {
        AppendTriple(builder, SparqlSubjectVariable, SparqlTypeShortcut, SparqlTypeVariable, indent);
        AppendOptionalLabel(builder, SparqlSubjectVariable, SparqlLabelVariable, indent);
        AppendOptionalPredicate(builder, SparqlSubjectVariable, SchemaDescriptionText, SparqlDescriptionVariable, indent);
        AppendOptionalSource(builder, SparqlSubjectVariable, SparqlSourceVariable, SparqlSourceLabelVariable, indent);
    }

    private static void AppendRelationshipPathTriple(
        StringBuilder builder,
        ResolvedSchemaRelationshipPredicate relationship,
        string indent)
    {
        var subject = relationship.Direction == KnowledgeGraphSchemaRelationshipDirection.Outbound
            ? SparqlSubjectVariable
            : SparqlRelatedNodeVariable;
        var graphObject = relationship.Direction == KnowledgeGraphSchemaRelationshipDirection.Outbound
            ? SparqlRelatedNodeVariable
            : SparqlSubjectVariable;
        AppendTriple(builder, subject, CreatePredicatePath(relationship.PredicatePathIds), graphObject, indent);
    }

    private static string CreatePredicatePath(IEnumerable<string> predicateIds)
    {
        return string.Join(
            SchemaSearchPredicatePathSeparator,
            predicateIds.Select(static predicate => SparqlUriOpen + predicate + SparqlUriClose));
    }

    private static void AppendFacetFilters(
        StringBuilder builder,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent)
    {
        foreach (var facet in facetFilters)
        {
            AppendTriple(
                builder,
                SparqlSubjectVariable,
                SparqlUriOpen + facet.PredicateId + SparqlUriClose,
                SparqlUriOpen + facet.ObjectId + SparqlUriClose,
                indent);
        }
    }

    private static void AppendOptionalLabel(StringBuilder builder, string subjectVariable, string targetVariable, string indent)
    {
        AppendOptionalPredicate(builder, subjectVariable, SchemaNameText, targetVariable, indent);
        AppendOptionalPredicate(builder, subjectVariable, RdfsLabelText, targetVariable, indent);
        AppendOptionalPredicate(builder, subjectVariable, SkosPrefLabelText, targetVariable, indent);
    }

    private static void AppendOptionalPredicate(
        StringBuilder builder,
        string subjectVariable,
        string predicateId,
        string targetVariable,
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
            .Append(LessThanCharacter)
            .Append(predicateId)
            .Append(GreaterThanCharacter)
            .Append(SpaceCharacter)
            .Append(targetVariable)
            .Append(SparqlStatementTerminator)
            .Append(SpaceCharacter)
            .Append(CloseBraceCharacter)
            .AppendLine();
    }

    private static void AppendValues(StringBuilder builder, string variable, IEnumerable<string> iris, string indent)
    {
        builder
            .Append(indent)
            .Append(SparqlValuesKeyword)
            .Append(SpaceCharacter)
            .Append(variable)
            .Append(SpaceCharacter)
            .Append(OpenBraceCharacter)
            .Append(SpaceCharacter);
        foreach (var iri in iris.OrderBy(static iri => iri, StringComparer.Ordinal))
        {
            builder.Append(LessThanCharacter).Append(iri).Append(GreaterThanCharacter).Append(SpaceCharacter);
        }

        builder.Append(CloseBraceCharacter).AppendLine();
    }

    private static void AppendTriple(StringBuilder builder, string subject, string predicate, string graphObject, string indent)
    {
        builder
            .Append(indent)
            .Append(subject)
            .Append(SpaceCharacter)
            .Append(predicate)
            .Append(SpaceCharacter)
            .Append(graphObject)
            .AppendLine(SparqlStatementTerminator);
    }

    private static void AppendLiteralFilter(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchTermMode termMode,
        string indent)
    {
        builder
            .Append(indent)
            .Append(SparqlFilterKeyword)
            .Append(OpenParenthesisCharacter)
            .Append(SparqlIsLiteralOpen)
            .Append(SparqlEvidenceValueVariable)
            .Append(CloseParenthesisCharacter)
            .Append(CloseParenthesisCharacter)
            .AppendLine();
        var terms = CreateSchemaSearchTerms(query, termMode);
        if (termMode == KnowledgeGraphSchemaSearchTermMode.AnyTerm && terms.Count > 1)
        {
            AppendAnyTermLiteralFilter(builder, terms, indent);
            return;
        }

        foreach (var term in terms)
        {
            AppendSingleTermLiteralFilter(builder, term, indent);
        }
    }

    private static IReadOnlyList<string> CreateSchemaSearchTerms(
        string query,
        KnowledgeGraphSchemaSearchTermMode termMode)
    {
        if (termMode == KnowledgeGraphSchemaSearchTermMode.ExactPhrase)
        {
            return [query];
        }

        return query.Split(SpaceCharacter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AppendAnyTermLiteralFilter(
        StringBuilder builder,
        IReadOnlyList<string> terms,
        string indent)
    {
        builder
            .Append(indent)
            .Append(SparqlFilterKeyword)
            .Append(OpenParenthesisCharacter);
        for (var index = 0; index < terms.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(SparqlOrOperator);
            }

            AppendContainsExpression(builder, terms[index]);
        }

        builder.Append(CloseParenthesisCharacter).AppendLine();
    }

    private static void AppendSingleTermLiteralFilter(StringBuilder builder, string term, string indent)
    {
        builder
            .Append(indent)
            .Append(SparqlFilterKeyword)
            .Append(OpenParenthesisCharacter);
        AppendContainsExpression(builder, term);
        builder.Append(CloseParenthesisCharacter).AppendLine();
    }

    private static void AppendContainsExpression(StringBuilder builder, string term)
    {
        builder
            .Append(SparqlContainsOpen)
            .Append(SparqlEvidenceValueVariable)
            .Append(SparqlContainsMiddle)
            .Append(EscapeSparqlLiteral(term))
            .Append(SparqlContainsClose);
    }

    private static void AppendTypeFilters(
        StringBuilder builder,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        string indent)
    {
        if (typeFilters.Count > 0)
        {
            AppendTypeFilter(builder, typeFilters, SparqlInOpen, indent);
        }

        if (excludedTypes.Count > 0)
        {
            AppendTypeFilter(builder, excludedTypes, SparqlNotInOpen, indent);
        }
    }

    private static void AppendTypeFilter(StringBuilder builder, IReadOnlyList<string> types, string operatorText, string indent)
    {
        builder
            .Append(indent)
            .Append(SparqlFilterKeyword)
            .Append(OpenParenthesisCharacter)
            .Append(SparqlTypeVariable)
            .Append(operatorText)
            .AppendJoin(SparqlCommaSpace, types.Select(static type => SparqlUriOpen + type + SparqlUriClose))
            .Append(CloseParenthesisCharacter)
            .Append(CloseParenthesisCharacter)
            .AppendLine();
    }

    private static void AppendBindText(StringBuilder builder, string value, string variable, string indent)
    {
        builder
            .Append(indent)
            .Append(SparqlBindKeyword)
            .Append(OpenParenthesisCharacter)
            .Append(DoubleQuoteCharacter)
            .Append(value)
            .Append(DoubleQuoteCharacter)
            .Append(SpaceCharacter)
            .Append(SparqlAsKeyword)
            .Append(SpaceCharacter)
            .Append(variable)
            .Append(CloseParenthesisCharacter)
            .AppendLine();
    }

    private static void AppendBindUri(StringBuilder builder, string value, string variable, string indent)
    {
        builder
            .Append(indent)
            .Append(SparqlBindKeyword)
            .Append(OpenParenthesisCharacter)
            .Append(LessThanCharacter)
            .Append(value)
            .Append(GreaterThanCharacter)
            .Append(SpaceCharacter)
            .Append(SparqlAsKeyword)
            .Append(SpaceCharacter)
            .Append(variable)
            .Append(CloseParenthesisCharacter)
            .AppendLine();
    }

    private static void AppendLimit(StringBuilder builder, int limit)
    {
        builder
            .Append(SparqlLimitKeyword)
            .Append(SpaceCharacter)
            .AppendLine(limit.ToString(CultureInfo.InvariantCulture));
    }
}
