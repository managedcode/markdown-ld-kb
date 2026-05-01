using System.Text;
using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed partial class KnowledgeGraph
{
    public async Task<KnowledgeGraphSchemaSearchResult> SearchBySchemaAsync(
        string query,
        KnowledgeGraphSchemaSearchProfile? profile = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = CreateSchemaSearchPlan(query, profile ?? KnowledgeGraphSchemaSearchProfile.Default, federated: false);
        var rows = await ExecuteSelectAsync(plan.GeneratedSparql, cancellationToken).ConfigureAwait(false);
        var primary = BuildPrimaryMatches(rows, plan).Take(plan.Profile.MaxResults).ToArray();
        var expansion = await ResolveSchemaSearchExpansionAsync(primary, plan, cancellationToken).ConfigureAwait(false);

        return new KnowledgeGraphSchemaSearchResult(
            primary,
            expansion.Related,
            expansion.NextSteps,
            BuildSchemaSearchFocusedGraph(primary, expansion.Related, expansion.NextSteps),
            plan.GeneratedSparql,
            expansion.GeneratedSparql,
            [],
            CreateSchemaSearchExplain(plan, expansion.GeneratedSparql, []));
    }

    public async Task<KnowledgeGraphSchemaSearchResult> SearchBySchemaFederatedAsync(
        string query,
        KnowledgeGraphSchemaSearchProfile profile,
        FederatedSparqlExecutionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = CreateSchemaSearchPlan(query, profile, federated: true);
        var federated = await ExecuteFederatedSelectAsync(plan.GeneratedSparql, options, cancellationToken).ConfigureAwait(false);
        var primary = BuildPrimaryMatches(federated.Result, plan).Take(plan.Profile.MaxResults).ToArray();

        return new KnowledgeGraphSchemaSearchResult(
            primary,
            [],
            [],
            KnowledgeGraphSnapshot.Empty,
            plan.GeneratedSparql,
            GeneratedExpansionSparql: null,
            ServiceEndpointSpecifiers: federated.ServiceEndpointSpecifiers,
            Explain: CreateSchemaSearchExplain(plan, null, federated.ServiceEndpointSpecifiers));
    }

    private static KnowledgeGraphSchemaSearchPlan CreateSchemaSearchPlan(
        string query,
        KnowledgeGraphSchemaSearchProfile profile,
        bool federated)
    {
        ValidateSchemaSearchProfile(profile, federated);

        var prefixes = CreateSchemaSearchPrefixes(profile);
        var textPredicates = profile.TextPredicates
            .Select(predicate => new ResolvedSchemaTextPredicate(ResolveSchemaSearchIri(predicate.Predicate, prefixes), predicate.Weight))
            .ToArray();
        var relationships = profile.RelationshipPredicates
            .Select(predicate => ResolveRelationshipPredicate(predicate, prefixes))
            .ToArray();
        var expansion = profile.ExpansionPredicates
            .Select(predicate => new ResolvedSchemaExpansionPredicate(
                ResolveSchemaSearchIri(predicate.Predicate, prefixes),
                predicate.Role,
                predicate.Score))
            .ToArray();
        var typeFilters = profile.TypeFilters.Select(type => ResolveSchemaSearchIri(type, prefixes)).ToArray();
        var excludedTypes = profile.ExcludedTypes.Select(type => ResolveSchemaSearchIri(type, prefixes)).ToArray();
        var facetFilters = profile.FacetFilters.Select(filter => new ResolvedSchemaFacetFilter(
            ResolveSchemaSearchIri(filter.Predicate, prefixes),
            ResolveSchemaSearchIri(filter.Object, prefixes))).ToArray();
        var sparql = BuildSchemaSearchSparql(query, prefixes, textPredicates, relationships, typeFilters, excludedTypes, facetFilters, profile, federated);

        return new KnowledgeGraphSchemaSearchPlan(
            profile,
            query.Trim(),
            sparql,
            textPredicates,
            relationships,
            expansion,
            typeFilters,
            excludedTypes,
            facetFilters);
    }

    private static void ValidateSchemaSearchProfile(KnowledgeGraphSchemaSearchProfile profile, bool federated)
    {
        if (profile.TextPredicates.Count == 0)
        {
            throw new InvalidOperationException(SchemaSearchTextPredicatesRequiredMessage);
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profile.MaxResults);
        ArgumentOutOfRangeException.ThrowIfNegative(profile.MaxRelatedResults);
        ArgumentOutOfRangeException.ThrowIfNegative(profile.MaxNextStepResults);

        if (federated && profile.FederatedServiceEndpoints.Count == 0)
        {
            throw new InvalidOperationException(SchemaSearchFederatedEndpointsRequiredMessage);
        }
    }

    private static Dictionary<string, string> CreateSchemaSearchPrefixes(KnowledgeGraphSchemaSearchProfile profile)
    {
        return CreateSchemaSearchPrefixes(profile.Prefixes);
    }

    private static Dictionary<string, string> CreateSchemaSearchPrefixes(IReadOnlyDictionary<string, string> callerPrefixes)
    {
        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [SchemaPrefix] = SchemaNamespaceText,
            [KbPrefix] = KbNamespaceText,
            [ProvPrefix] = ProvNamespaceText,
            [RdfPrefix] = RdfNamespaceText,
            [RdfsPrefix] = RdfsNamespaceText,
            [OwlPrefix] = OwlNamespaceText,
            [SkosPrefix] = SkosNamespaceText,
            [XsdPrefix] = XsdNamespaceText,
        };

        foreach (var pair in callerPrefixes)
        {
            if (prefixes.TryGetValue(pair.Key, out var existing) && !string.Equals(existing, pair.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(SchemaSearchConflictingPrefixMessagePrefix + pair.Key);
            }

            prefixes[pair.Key] = pair.Value;
        }

        return prefixes;
    }

    private static string ResolveSchemaSearchIri(string value, IReadOnlyDictionary<string, string> prefixes)
    {
        var separatorIndex = value.IndexOf(Colon, StringComparison.Ordinal);
        if (separatorIndex > 0)
        {
            var prefix = value[..separatorIndex];
            if (prefixes.TryGetValue(prefix, out var namespaceText))
            {
                return namespaceText + value[(separatorIndex + 1)..];
            }

            if (IsAbsoluteSchemaSearchIri(value))
            {
                return value;
            }

            throw new InvalidOperationException(SchemaSearchUnknownPrefixMessagePrefix + prefix);
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return uri.AbsoluteUri;
        }

        throw new InvalidOperationException(SchemaSearchUnknownPrefixMessagePrefix + value);
    }

    private static bool IsAbsoluteSchemaSearchIri(string value)
    {
        return value.Contains(UriAuthoritySeparator, StringComparison.Ordinal) ||
               value.StartsWith(UrnSchemePrefix, StringComparison.Ordinal);
    }

    private static ResolvedSchemaRelationshipPredicate ResolveRelationshipPredicate(
        KnowledgeGraphSchemaRelationshipPredicate predicate,
        IReadOnlyDictionary<string, string> prefixes)
    {
        return new ResolvedSchemaRelationshipPredicate(
            ResolveSchemaSearchPredicatePath(predicate, prefixes),
            predicate.Direction,
            predicate.TargetTextPredicates.Select(target => ResolveSchemaSearchIri(target, prefixes)).ToArray(),
            predicate.Weight);
    }

    private static IReadOnlyList<string> ResolveSchemaSearchPredicatePath(
        KnowledgeGraphSchemaRelationshipPredicate predicate,
        IReadOnlyDictionary<string, string> prefixes)
    {
        var path = predicate.PredicatePath.Count == 0 ? [predicate.Predicate] : predicate.PredicatePath;
        return path.Select(item => ResolveSchemaSearchIri(item, prefixes)).ToArray();
    }

    private static string BuildSchemaSearchSparql(
        string query,
        IReadOnlyDictionary<string, string> prefixes,
        IReadOnlyList<ResolvedSchemaTextPredicate> textPredicates,
        IReadOnlyList<ResolvedSchemaRelationshipPredicate> relationships,
        IReadOnlyList<string> typeFilters,
        IReadOnlyList<string> excludedTypes,
        IReadOnlyList<ResolvedSchemaFacetFilter> facetFilters,
        KnowledgeGraphSchemaSearchProfile profile,
        bool federated)
    {
        var builder = new StringBuilder();
        AppendPrefixLines(builder, prefixes);
        AppendSchemaSearchSelect(builder);
        builder.AppendLine(SparqlWhereKeyword + SpaceText + SparqlOpenBrace);

        if (federated)
        {
            AppendFederatedSearchBody(builder, query, profile, textPredicates, relationships, typeFilters, excludedTypes, facetFilters);
        }
        else
        {
            AppendSchemaSearchBody(builder, query, profile.TermMode, textPredicates, relationships, typeFilters, excludedTypes, facetFilters, SparqlIndent);
        }

        builder.AppendLine(SparqlCloseBrace);
        AppendLimit(builder, profile.MaxResults * SchemaSearchEvidenceRowMultiplier);
        return builder.ToString();
    }

    private static void AppendPrefixLines(StringBuilder builder, IReadOnlyDictionary<string, string> prefixes)
    {
        foreach (var pair in prefixes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            builder
                .Append(SparqlPrefixKeyword)
                .Append(SpaceCharacter)
                .Append(pair.Key)
                .Append(ColonCharacter)
                .Append(SpaceCharacter)
                .Append(LessThanCharacter)
                .Append(pair.Value)
                .Append(GreaterThanCharacter)
                .AppendLine();
        }
    }

    private static void AppendSchemaSearchSelect(StringBuilder builder)
    {
        builder
            .Append(SparqlSelectKeyword)
            .Append(SpaceCharacter)
            .Append(SparqlDistinctKeyword)
            .Append(SpaceCharacter)
            .Append(SparqlSubjectVariable)
            .Append(SpaceCharacter)
            .Append(SparqlTypeVariable)
            .Append(SpaceCharacter)
            .Append(SparqlLabelVariable)
            .Append(SpaceCharacter)
            .Append(SparqlDescriptionVariable)
            .Append(SpaceCharacter)
            .Append(SparqlEvidencePredicateVariable)
            .Append(SpaceCharacter)
            .Append(SparqlEvidenceValueVariable)
            .Append(SpaceCharacter)
            .Append(SparqlEvidenceKindVariable)
            .Append(SpaceCharacter)
            .Append(SparqlRelatedNodeVariable)
            .Append(SpaceCharacter)
            .Append(SparqlRelatedLabelVariable)
            .Append(SpaceCharacter)
            .Append(SparqlViaPredicateVariable)
            .Append(SpaceCharacter)
            .Append(SparqlSourceVariable)
            .Append(SpaceCharacter)
            .Append(SparqlSourceLabelVariable)
            .Append(SpaceCharacter)
            .Append(SparqlRelatedSourceVariable)
            .Append(SpaceCharacter)
            .Append(SparqlRelatedSourceLabelVariable)
            .Append(SpaceCharacter)
            .Append(SparqlServiceEndpointVariable)
            .AppendLine();
    }

    private static KnowledgeGraphSchemaSearchExplain CreateSchemaSearchExplain(
        KnowledgeGraphSchemaSearchPlan plan,
        string? expansionSparql,
        IReadOnlyList<string> serviceEndpointSpecifiers)
    {
        return new KnowledgeGraphSchemaSearchExplain(
            plan.Query,
            plan.Profile.TermMode,
            plan.Profile.TypeFilters,
            plan.Profile.TextPredicates,
            plan.Profile.RelationshipPredicates,
            plan.Profile.ExpansionPredicates,
            plan.Profile.FacetFilters,
            plan.GeneratedSparql,
            expansionSparql,
            serviceEndpointSpecifiers);
    }
}

internal sealed record KnowledgeGraphSchemaSearchPlan(
    KnowledgeGraphSchemaSearchProfile Profile,
    string Query,
    string GeneratedSparql,
    IReadOnlyList<ResolvedSchemaTextPredicate> TextPredicates,
    IReadOnlyList<ResolvedSchemaRelationshipPredicate> RelationshipPredicates,
    IReadOnlyList<ResolvedSchemaExpansionPredicate> ExpansionPredicates,
    IReadOnlyList<string> TypeFilters,
    IReadOnlyList<string> ExcludedTypes,
    IReadOnlyList<ResolvedSchemaFacetFilter> FacetFilters);

internal sealed record ResolvedSchemaTextPredicate(string PredicateId, double Weight);

internal sealed record ResolvedSchemaRelationshipPredicate(
    IReadOnlyList<string> PredicatePathIds,
    KnowledgeGraphSchemaRelationshipDirection Direction,
    IReadOnlyList<string> TargetTextPredicateIds,
    double Weight)
{
    public string PredicateId { get; } = string.Join(SchemaSearchPredicatePathSeparator, PredicatePathIds);
}

internal sealed record ResolvedSchemaExpansionPredicate(
    string PredicateId,
    KnowledgeGraphSchemaSearchRole Role,
    double Score);

internal sealed record ResolvedSchemaFacetFilter(
    string PredicateId,
    string ObjectId);
