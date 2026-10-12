using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static void AppendSchemaSearchBody(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchProfile profile,
        IReadOnlyList<ResolvedSchemaTextPredicate> textPredicates,
        IReadOnlyList<ResolvedSchemaRelationshipPredicate> relationships,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent,
        bool useFixedPredicateBranches)
    {
        builder.AppendLine(indent + SparqlOpenBrace);
        var evidenceIndent = indent + SparqlIndent;
        var hasBranch = false;
        foreach (var predicateIds in CreateEvidencePredicateGroups(textPredicates.Select(static predicate => predicate.PredicateId), useFixedPredicateBranches))
        {
            AppendEvidenceBranchStart(builder, evidenceIndent, hasBranch);
            AppendDirectEvidencePattern(builder, query, profile, predicateIds, typeFilters, excludedTypes, facetFilters, evidenceIndent + SparqlIndent);
            builder.AppendLine(evidenceIndent + SparqlCloseBrace);
            hasBranch = true;
        }
        if (relationships.Count > 0)
        {
            AppendEvidenceBranchStart(builder, evidenceIndent, hasBranch);
            AppendRelationshipEvidenceGroup(builder, query, profile, relationships, typeFilters, excludedTypes, facetFilters, evidenceIndent + SparqlIndent, useFixedPredicateBranches);
            builder.AppendLine(evidenceIndent + SparqlCloseBrace);
        }
        builder.AppendLine(indent + SparqlCloseBrace);
        AppendSubjectMetadata(builder, indent);
    }

    private static void AppendRelationshipEvidenceGroup(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchProfile profile,
        IReadOnlyList<ResolvedSchemaRelationshipPredicate> relationships,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent,
        bool useFixedPredicateBranches)
    {
        builder.AppendLine(indent + SparqlOpenBrace);
        var hasBranch = false;
        foreach (var relationship in relationships)
        {
            foreach (var predicateIds in CreateEvidencePredicateGroups(relationship.TargetTextPredicateIds, useFixedPredicateBranches))
            {
                AppendEvidenceBranchStart(builder, indent + SparqlIndent, hasBranch);
                AppendRelationshipEvidencePattern(builder, query, profile, relationship, predicateIds, typeFilters, excludedTypes, facetFilters, indent + SparqlDoubleIndent);
                builder.AppendLine(indent + SparqlIndent + SparqlCloseBrace);
                hasBranch = true;
            }
        }
        builder.AppendLine(indent + SparqlCloseBrace);
        AppendOptionalSource(builder, SparqlRelatedNodeVariable, SparqlRelatedSourceVariable, SparqlRelatedSourceLabelVariable, indent);
        AppendOptionalLabel(builder, SparqlRelatedNodeVariable, SparqlRelatedLabelVariable, indent);
    }

    private static void AppendEvidenceBranchStart(StringBuilder builder, string indent, bool hasBranch)
    {
        if (hasBranch)
        {
            builder.AppendLine(indent + SparqlUnionKeyword);
        }
        builder.AppendLine(indent + SparqlOpenBrace);
    }

    private static void AppendDirectEvidencePattern(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchProfile profile,
        IReadOnlyList<string> predicateIds,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent)
    {
        builder.AppendLine(indent + SparqlOpenBrace);
        var evidenceIndent = indent + SparqlIndent;
        AppendSchemaCandidateValues(builder, profile.CandidateNodeIds, evidenceIndent);
        AppendFacetFilters(builder, facetFilters, evidenceIndent);
        AppendEvidenceTriple(builder, SparqlSubjectVariable, predicateIds, evidenceIndent);
        AppendLiteralFilter(builder, query, profile.TermMode, evidenceIndent);
        AppendTriple(builder, SparqlSubjectVariable, SparqlTypeShortcut, SparqlTypeVariable, evidenceIndent);
        AppendTypeFilters(builder, typeFilters, excludedTypes, evidenceIndent);
        builder.AppendLine(indent + SparqlCloseBrace);
        if (predicateIds.Count == 1)
        {
            AppendBindUri(builder, predicateIds[0], SparqlEvidencePredicateVariable, indent);
        }
        AppendBindText(builder, SchemaSearchDirectEvidenceKind, SparqlEvidenceKindVariable, indent);
    }

    private static void AppendRelationshipEvidencePattern(
        StringBuilder builder,
        string query,
        KnowledgeGraphSchemaSearchProfile profile,
        ResolvedSchemaRelationshipPredicate relationship,
        IReadOnlyList<string> predicateIds,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        string indent)
    {
        builder.AppendLine(indent + SparqlOpenBrace);
        var evidenceIndent = indent + SparqlIndent;
        AppendSchemaCandidateValues(builder, profile.CandidateNodeIds, evidenceIndent);
        AppendFacetFilters(builder, facetFilters, evidenceIndent);
        AppendRelationshipPathTriple(builder, relationship, evidenceIndent);
        AppendEvidenceTriple(builder, SparqlRelatedNodeVariable, predicateIds, evidenceIndent);
        AppendLiteralFilter(builder, query, profile.TermMode, evidenceIndent);
        AppendTriple(builder, SparqlSubjectVariable, SparqlTypeShortcut, SparqlTypeVariable, evidenceIndent);
        AppendTypeFilters(builder, typeFilters, excludedTypes, evidenceIndent);
        builder.AppendLine(indent + SparqlCloseBrace);
        if (predicateIds.Count == 1)
        {
            AppendBindUri(builder, predicateIds[0], SparqlEvidencePredicateVariable, indent);
        }
        AppendBindText(builder, relationship.PredicateId, SparqlViaPredicateVariable, indent);
        AppendBindText(builder, SchemaSearchRelationshipEvidenceKind, SparqlEvidenceKindVariable, indent);
    }

    private static void AppendSubjectMetadata(StringBuilder builder, string indent)
    {
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

}
