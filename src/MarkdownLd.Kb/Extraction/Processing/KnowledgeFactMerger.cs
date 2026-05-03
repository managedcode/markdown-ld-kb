using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeFactMerger(Uri? baseUri = null)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));

    public KnowledgeExtractionResult Merge(params KnowledgeExtractionResult[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var entities = new Dictionary<string, KnowledgeEntityFact>(StringComparer.OrdinalIgnoreCase);
        var assertions = new Dictionary<string, KnowledgeAssertionFact>(StringComparer.OrdinalIgnoreCase);
        var aliases = new KnowledgeFactAliasIndex();
        var pendingAssertions = new List<KnowledgeAssertionFact>();

        foreach (var result in results)
        {
            ArgumentNullException.ThrowIfNull(result);

            foreach (var entity in result.Entities)
            {
                UpsertEntity(entities, aliases, CanonicalizeEntity(entity));
            }

            pendingAssertions.AddRange(result.Assertions);
        }

        foreach (var assertion in pendingAssertions)
        {
            var canonical = RewriteAssertionAliases(CanonicalizeAssertion(assertion), aliases.EntityAliases);
            if (IsValidAssertion(canonical))
            {
                UpsertAssertion(assertions, canonical);
            }
        }

        return new KnowledgeExtractionResult
        {
            Entities = entities.Values.OrderBy(entity => entity.Label, StringComparer.OrdinalIgnoreCase).ToList(),
            Assertions = assertions.Values
                .OrderBy(assertion => assertion.SubjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(assertion => assertion.Predicate, StringComparer.OrdinalIgnoreCase)
                .ThenBy(assertion => assertion.ObjectId, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private KnowledgeEntityFact CanonicalizeEntity(KnowledgeEntityFact entity)
    {
        var label = entity.Label.Trim();
        var canonicalId = CanonicalizeNodeId(entity.Id ?? label);
        return entity with
        {
            Id = canonicalId,
            Label = label,
            Type = string.IsNullOrWhiteSpace(entity.Type) ? DefaultSchemaThing : entity.Type.Trim(),
            SameAs = entity.SameAs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Source = entity.Source,
            Sources = KnowledgeFactSourceCollector.MergeEntitySources(entity),
        };
    }

    private KnowledgeAssertionFact CanonicalizeAssertion(KnowledgeAssertionFact assertion)
    {
        return assertion with
        {
            SubjectId = CanonicalizeNodeId(assertion.SubjectId),
            ObjectId = CanonicalizeNodeId(assertion.ObjectId),
            Predicate = KnowledgeNaming.NormalizePredicate(assertion.Predicate),
            Sources = KnowledgeFactSourceCollector.MergeAssertionSources(assertion),
        };
    }

    private string CanonicalizeNodeId(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return nodeId;
        }

        if (Uri.TryCreate(nodeId, UriKind.Absolute, out var absolute))
        {
            return absolute.AbsoluteUri;
        }

        if (nodeId.StartsWith(UriSchemePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return nodeId;
        }

        return KnowledgeNaming.CreateEntityId(_baseUri, nodeId);
    }

    private static bool IsValidAssertion(KnowledgeAssertionFact assertion)
    {
        return !string.IsNullOrWhiteSpace(assertion.SubjectId) &&
               !string.IsNullOrWhiteSpace(assertion.Predicate) &&
               !string.IsNullOrWhiteSpace(assertion.ObjectId);
    }

    private static KnowledgeAssertionFact RewriteAssertionAliases(
        KnowledgeAssertionFact assertion,
        IReadOnlyDictionary<string, string> entityAliases)
    {
        return assertion with
        {
            SubjectId = ResolveEntityAlias(assertion.SubjectId, entityAliases),
            ObjectId = ShouldRewriteObject(assertion)
                ? ResolveEntityAlias(assertion.ObjectId, entityAliases)
                : assertion.ObjectId,
        };
    }

    private static bool ShouldRewriteObject(KnowledgeAssertionFact assertion)
    {
        return !assertion.Predicate.Equals(ExpectedSchemaSameAs, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveEntityAlias(string nodeId, IReadOnlyDictionary<string, string> entityAliases)
    {
        return entityAliases.TryGetValue(nodeId, out var canonicalId)
            ? canonicalId
            : nodeId;
    }

    private static void UpsertEntity(
        IDictionary<string, KnowledgeEntityFact> entities,
        KnowledgeFactAliasIndex aliases,
        KnowledgeEntityFact entity)
    {
        var key = aliases.ResolveEntityKey(entity);
        if (!entities.TryGetValue(key, out var existing))
        {
            entities[key] = entity with { Id = key };
            aliases.Index(key, entity);
            return;
        }

        entities[key] = existing with
        {
            Label = existing.Label.Length >= entity.Label.Length ? existing.Label : entity.Label,
            Type = KnowledgeFactTypeSelector.PreferHigherPriority(existing.Type, entity.Type),
            SameAs = existing.SameAs.Concat(entity.SameAs).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Confidence = Math.Max(existing.Confidence, entity.Confidence),
            Source = string.IsNullOrWhiteSpace(existing.Source) ? entity.Source : existing.Source,
            Sources = KnowledgeFactSourceCollector.MergeEntitySources(existing, entity),
        };
        aliases.Index(key, entities[key]);
        aliases.Index(key, entity);
    }

    private static void UpsertAssertion(IDictionary<string, KnowledgeAssertionFact> assertions, KnowledgeAssertionFact assertion)
    {
        var key = assertion.SubjectId + AssertionKeySeparator + assertion.Predicate + AssertionKeySeparator + assertion.ObjectId;
        if (!assertions.TryGetValue(key, out var existing))
        {
            assertions[key] = assertion;
            return;
        }

        assertions[key] = existing with
        {
            Confidence = Math.Max(existing.Confidence, assertion.Confidence),
            Source = string.IsNullOrWhiteSpace(existing.Source) ? assertion.Source : existing.Source,
            Sources = KnowledgeFactSourceCollector.MergeAssertionSources(existing, assertion),
        };
    }
}
