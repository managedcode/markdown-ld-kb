using VDS.RDF;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public KnowledgeGraphSchemaDescription DescribeSchema(IReadOnlyDictionary<string, string>? prefixes = null)
    {
        var effectivePrefixes = CreateSchemaSearchPrefixes(prefixes ?? new Dictionary<string, string>(StringComparer.Ordinal));
        _graphLock.EnterReadLock();
        try
        {
            var triples = _graph.Triples.ToArray();
            return new KnowledgeGraphSchemaDescription(
                DescribeTypes(triples, effectivePrefixes),
                DescribePredicates(triples, effectivePrefixes, SchemaPredicateObjectKind.Any),
                DescribePredicates(triples, effectivePrefixes, SchemaPredicateObjectKind.Literal),
                DescribePredicates(triples, effectivePrefixes, SchemaPredicateObjectKind.Resource));
        }
        finally
        {
            _graphLock.ExitReadLock();
        }
    }

    public KnowledgeGraphSchemaSearchProfileValidation ValidateSchemaSearchProfile(
        KnowledgeGraphSchemaSearchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var issues = new List<KnowledgeGraphSchemaSearchProfileIssue>();
        var schema = DescribeSchema(profile.Prefixes);
        var typeIds = schema.RdfTypes.Select(static item => item.Iri).ToHashSet(StringComparer.Ordinal);
        var allPredicates = schema.Predicates.Select(static item => item.Iri).ToHashSet(StringComparer.Ordinal);
        var literalPredicates = schema.LiteralPredicates.Select(static item => item.Iri).ToHashSet(StringComparer.Ordinal);
        var resourcePredicates = schema.ResourcePredicates.Select(static item => item.Iri).ToHashSet(StringComparer.Ordinal);
        var prefixes = CreateSchemaSearchPrefixes(profile);

        AddMissingTerms(profile.TypeFilters, typeIds, KnowledgeGraphSchemaSearchProfileIssueKind.MissingType, SchemaSearchProfileIssueMissingTypeMessage);
        AddMissingTerms(
            profile.TextPredicates.Select(static item => item.Predicate),
            literalPredicates,
            KnowledgeGraphSchemaSearchProfileIssueKind.MissingTextPredicate,
            SchemaSearchProfileIssueMissingTextPredicateMessage);
        AddRelationshipIssues(profile, prefixes, resourcePredicates, literalPredicates, issues);
        AddMissingTerms(
            profile.ExpansionPredicates.Select(static item => item.Predicate),
            resourcePredicates,
            KnowledgeGraphSchemaSearchProfileIssueKind.MissingExpansionPredicate,
            SchemaSearchProfileIssueMissingExpansionPredicateMessage);
        AddMissingTerms(
            profile.FacetFilters.Select(static item => item.Predicate),
            allPredicates,
            KnowledgeGraphSchemaSearchProfileIssueKind.MissingFacetPredicate,
            SchemaSearchProfileIssueMissingFacetPredicateMessage);

        return new KnowledgeGraphSchemaSearchProfileValidation(issues.Count == 0, issues);

        void AddMissingTerms(
            IEnumerable<string> terms,
            ISet<string> knownTerms,
            KnowledgeGraphSchemaSearchProfileIssueKind kind,
            string message)
        {
            foreach (var term in terms)
            {
                AddMissingTerm(term, knownTerms, prefixes, kind, message, issues);
            }
        }
    }

    public KnowledgeGraphContract CreateContract(
        string name,
        KnowledgeGraphSchemaSearchProfile searchProfile,
        string? shaclShapesTurtle = null)
    {
        return new KnowledgeGraphContract(
            name,
            DescribeSchema(searchProfile.Prefixes),
            searchProfile,
            ValidateSchemaSearchProfile(searchProfile),
            shaclShapesTurtle);
    }

    private static IReadOnlyList<KnowledgeGraphSchemaTerm> DescribeTypes(
        IEnumerable<Triple> triples,
        IReadOnlyDictionary<string, string> prefixes)
    {
        return triples
            .Where(static triple => RenderGraphNodeId(triple.Predicate) == RdfTypeText && triple.Object is IUriNode)
            .GroupBy(static triple => RenderGraphNodeId(triple.Object), StringComparer.Ordinal)
            .Select(group => new KnowledgeGraphSchemaTerm(group.Key, CompactUri(group.Key, prefixes), group.Count()))
            .OrderBy(static item => item.CompactName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<KnowledgeGraphPredicateDescription> DescribePredicates(
        IEnumerable<Triple> triples,
        IReadOnlyDictionary<string, string> prefixes,
        SchemaPredicateObjectKind objectKind)
    {
        return triples
            .Where(triple => PredicateObjectKindMatches(triple, objectKind))
            .GroupBy(static triple => RenderGraphNodeId(triple.Predicate), StringComparer.Ordinal)
            .Select(group => new KnowledgeGraphPredicateDescription(
                group.Key,
                CompactUri(group.Key, prefixes),
                group.Count(),
                group.Count(static triple => triple.Object is ILiteralNode),
                group.Count(static triple => triple.Object is IUriNode or IBlankNode)))
            .OrderBy(static item => item.CompactName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool PredicateObjectKindMatches(Triple triple, SchemaPredicateObjectKind objectKind)
    {
        return objectKind switch
        {
            SchemaPredicateObjectKind.Literal => triple.Object is ILiteralNode,
            SchemaPredicateObjectKind.Resource => triple.Object is IUriNode or IBlankNode,
            _ => true,
        };
    }

    private static void AddRelationshipIssues(
        KnowledgeGraphSchemaSearchProfile profile,
        IReadOnlyDictionary<string, string> prefixes,
        ISet<string> resourcePredicates,
        ISet<string> literalPredicates,
        ICollection<KnowledgeGraphSchemaSearchProfileIssue> issues)
    {
        foreach (var relationship in profile.RelationshipPredicates)
        {
            var path = relationship.PredicatePath.Count == 0 ? [relationship.Predicate] : relationship.PredicatePath;
            foreach (var predicate in path)
            {
                AddMissingTerm(
                    predicate,
                    resourcePredicates,
                    prefixes,
                    KnowledgeGraphSchemaSearchProfileIssueKind.MissingRelationshipPredicate,
                    SchemaSearchProfileIssueMissingRelationshipPredicateMessage,
                    issues);
            }

            foreach (var target in relationship.TargetTextPredicates)
            {
                AddMissingTerm(
                    target,
                    literalPredicates,
                    prefixes,
                    KnowledgeGraphSchemaSearchProfileIssueKind.MissingRelationshipTargetPredicate,
                    SchemaSearchProfileIssueMissingRelationshipTargetMessage,
                    issues);
            }
        }
    }

    private static void AddMissingTerm(
        string term,
        ISet<string> knownTerms,
        IReadOnlyDictionary<string, string> prefixes,
        KnowledgeGraphSchemaSearchProfileIssueKind kind,
        string message,
        ICollection<KnowledgeGraphSchemaSearchProfileIssue> issues)
    {
        if (!TryResolveSchemaSearchIri(term, prefixes, out var resolved, out var issue))
        {
            issues.Add(issue!);
            return;
        }

        if (!knownTerms.Contains(resolved))
        {
            issues.Add(new KnowledgeGraphSchemaSearchProfileIssue(kind, term, resolved, message));
        }
    }

    private static bool TryResolveSchemaSearchIri(
        string term,
        IReadOnlyDictionary<string, string> prefixes,
        out string resolved,
        out KnowledgeGraphSchemaSearchProfileIssue? issue)
    {
        try
        {
            resolved = ResolveSchemaSearchIri(term, prefixes);
            issue = null;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            resolved = string.Empty;
            issue = new KnowledgeGraphSchemaSearchProfileIssue(
                KnowledgeGraphSchemaSearchProfileIssueKind.UnknownPrefix,
                term,
                null,
                exception.Message);
            return false;
        }
    }

    private static string CompactUri(string iri, IReadOnlyDictionary<string, string> prefixes)
    {
        foreach (var pair in prefixes.OrderByDescending(static pair => pair.Value.Length))
        {
            if (iri.StartsWith(pair.Value, StringComparison.Ordinal))
            {
                return pair.Key + Colon + iri[pair.Value.Length..];
            }
        }

        return iri;
    }

    private enum SchemaPredicateObjectKind
    {
        Any,
        Literal,
        Resource,
    }
}
