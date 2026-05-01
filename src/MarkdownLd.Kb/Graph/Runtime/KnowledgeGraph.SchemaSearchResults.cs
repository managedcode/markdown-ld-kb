using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    private static IReadOnlyList<KnowledgeGraphSchemaSearchMatch> BuildPrimaryMatches(
        SparqlQueryResult rows,
        KnowledgeGraphSchemaSearchPlan plan)
    {
        var builders = new Dictionary<string, SchemaSearchMatchBuilder>(StringComparer.Ordinal);
        foreach (var row in rows.Rows)
        {
            if (!TryGetRowValue(row, SchemaSearchSubjectKey, out var subjectId) ||
                !TryGetRowValue(row, SchemaSearchEvidencePredicateKey, out var predicateId) ||
                !TryGetRowValue(row, SchemaSearchEvidenceValueKey, out var matchedText))
            {
                continue;
            }

            var builder = GetMatchBuilder(builders, subjectId, row);
            AddEvidence(builder, row, predicateId, matchedText, plan);
        }

        return builders.Values
            .Select(static builder => builder.ToPrimaryMatch())
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SchemaSearchMatchBuilder GetMatchBuilder(
        IDictionary<string, SchemaSearchMatchBuilder> builders,
        string subjectId,
        SparqlRow row)
    {
        if (builders.TryGetValue(subjectId, out var builder))
        {
            AddType(builder, row);
            return builder;
        }

        var label = TryGetRowValue(row, SchemaSearchLabelKey, out var rowLabel) ? rowLabel : subjectId;
        var description = TryGetRowValue(row, SchemaSearchDescriptionKey, out var rowDescription) ? rowDescription : null;
        builder = new SchemaSearchMatchBuilder(subjectId, label, description);
        AddType(builder, row);
        builders[subjectId] = builder;
        return builder;
    }

    private static void AddType(SchemaSearchMatchBuilder builder, SparqlRow row)
    {
        if (TryGetRowValue(row, SchemaSearchTypeKey, out var typeId))
        {
            builder.Types.Add(typeId);
        }
    }

    private static void AddEvidence(
        SchemaSearchMatchBuilder builder,
        SparqlRow row,
        string predicateId,
        string matchedText,
        KnowledgeGraphSchemaSearchPlan plan)
    {
        var kind = ResolveEvidenceKind(row);
        var viaPredicate = TryGetRowValue(row, SchemaSearchViaPredicateKey, out var via) ? via : null;
        var relatedNode = TryGetRowValue(row, SchemaSearchRelatedNodeKey, out var related) ? related : null;
        var relatedLabel = TryGetRowValue(row, SchemaSearchRelatedLabelKey, out var label) ? label : null;
        var serviceEndpoint = TryGetRowValue(row, SchemaSearchServiceEndpointKey, out var endpoint) ? endpoint : null;
        var sourceContexts = CreateSourceContexts(row);
        var score = CalculateEvidenceScore(predicateId, matchedText, kind, viaPredicate, plan);

        builder.AddEvidence(new KnowledgeGraphSchemaSearchEvidence(
            predicateId,
            matchedText,
            kind,
            score,
            relatedNode,
            relatedLabel,
            viaPredicate,
            serviceEndpoint)
        {
            SourceContexts = sourceContexts,
        });
    }

    private static IReadOnlyList<KnowledgeGraphSchemaSearchSourceContext> CreateSourceContexts(SparqlRow row)
    {
        var contexts = new Dictionary<string, KnowledgeGraphSchemaSearchSourceContext>(StringComparer.Ordinal);
        AddSourceContext(row, contexts, SchemaSearchSourceKey, SchemaSearchSourceLabelKey);
        AddSourceContext(row, contexts, SchemaSearchRelatedSourceKey, SchemaSearchRelatedSourceLabelKey);
        return contexts.Values
            .OrderBy(static context => context.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddSourceContext(
        SparqlRow row,
        IDictionary<string, KnowledgeGraphSchemaSearchSourceContext> contexts,
        string sourceKey,
        string labelKey)
    {
        if (!TryGetRowValue(row, sourceKey, out var source))
        {
            return;
        }

        var label = TryGetRowValue(row, labelKey, out var sourceLabel) ? sourceLabel : null;
        contexts[source] = new KnowledgeGraphSchemaSearchSourceContext(source, label);
    }

    private static KnowledgeGraphSchemaSearchEvidenceKind ResolveEvidenceKind(SparqlRow row)
    {
        if (TryGetRowValue(row, SchemaSearchEvidenceKindKey, out var kind) &&
            string.Equals(kind, SchemaSearchRelationshipEvidenceKind, StringComparison.Ordinal))
        {
            return KnowledgeGraphSchemaSearchEvidenceKind.Relationship;
        }

        return KnowledgeGraphSchemaSearchEvidenceKind.Direct;
    }

    private static double CalculateEvidenceScore(
        string predicateId,
        string matchedText,
        KnowledgeGraphSchemaSearchEvidenceKind kind,
        string? viaPredicate,
        KnowledgeGraphSchemaSearchPlan plan)
    {
        var weight = kind == KnowledgeGraphSchemaSearchEvidenceKind.Direct
            ? ResolveDirectWeight(predicateId, plan)
            : ResolveRelationshipWeight(viaPredicate, plan);
        return IsExactMatch(matchedText, plan.Query)
            ? weight
            : weight * SchemaSearchContainsScoreMultiplier;
    }

    private static double ResolveDirectWeight(string predicateId, KnowledgeGraphSchemaSearchPlan plan)
    {
        return plan.TextPredicates
            .FirstOrDefault(predicate => predicate.PredicateId == predicateId)
            ?.Weight ?? SchemaSearchDefaultTextWeight;
    }

    private static double ResolveRelationshipWeight(string? predicateId, KnowledgeGraphSchemaSearchPlan plan)
    {
        return plan.RelationshipPredicates
            .FirstOrDefault(predicate => predicate.PredicateId == predicateId)
            ?.Weight ?? SchemaSearchRelationshipWeight;
    }

    private static bool IsExactMatch(string matchedText, string query)
    {
        return string.Equals(matchedText.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetRowValue(SparqlRow row, string key, out string value)
    {
        return row.Values.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);
    }

    private sealed class SchemaSearchMatchBuilder(string nodeId, string label, string? description)
    {
        public string NodeId { get; } = nodeId;

        public string Label { get; } = label;

        public string? Description { get; } = description;

        public double Score { get; set; }

        public HashSet<string> Types { get; } = new(StringComparer.Ordinal);

        public List<KnowledgeGraphSchemaSearchEvidence> Evidence { get; } = [];

        public void AddEvidence(KnowledgeGraphSchemaSearchEvidence evidence)
        {
            for (var index = 0; index < Evidence.Count; index++)
            {
                if (!EvidenceMatches(Evidence[index], evidence))
                {
                    continue;
                }

                Evidence[index] = MergeEvidence(Evidence[index], evidence);
                return;
            }

            Evidence.Add(evidence);
            Score += evidence.Score;
        }

        public KnowledgeGraphSchemaSearchMatch ToPrimaryMatch()
        {
            return new KnowledgeGraphSchemaSearchMatch(
                NodeId,
                Label,
                KnowledgeGraphSchemaSearchRole.Primary,
                Score,
                Types.OrderBy(static type => type, StringComparer.Ordinal).ToArray(),
                Evidence.OrderByDescending(static evidence => evidence.Score).ToArray(),
                Description);
        }

        private static bool EvidenceMatches(
            KnowledgeGraphSchemaSearchEvidence left,
            KnowledgeGraphSchemaSearchEvidence right)
        {
            return string.Equals(left.PredicateId, right.PredicateId, StringComparison.Ordinal) &&
                   string.Equals(left.MatchedText, right.MatchedText, StringComparison.Ordinal) &&
                   left.Kind == right.Kind &&
                   string.Equals(left.RelatedNodeId, right.RelatedNodeId, StringComparison.Ordinal) &&
                   string.Equals(left.ViaPredicateId, right.ViaPredicateId, StringComparison.Ordinal) &&
                   string.Equals(left.ServiceEndpoint, right.ServiceEndpoint, StringComparison.Ordinal);
        }

        private static KnowledgeGraphSchemaSearchEvidence MergeEvidence(
            KnowledgeGraphSchemaSearchEvidence existing,
            KnowledgeGraphSchemaSearchEvidence next)
        {
            return existing with
            {
                SourceContexts = existing.SourceContexts
                    .Concat(next.SourceContexts)
                    .GroupBy(static source => source.SourceId, StringComparer.Ordinal)
                    .Select(static group => group.First())
                    .OrderBy(static source => source.SourceId, StringComparer.Ordinal)
                    .ToArray(),
            };
        }
    }
}
