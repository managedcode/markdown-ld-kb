using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public sealed class KnowledgeFactMerger(Uri? baseUri = null)
{
    private readonly KnowledgeFactCanonicalizer _canonicalizer = new(baseUri);

    public KnowledgeExtractionResult Merge(params KnowledgeExtractionResult[] results)
    {
        return MergeWithReport(results).Facts;
    }

    internal KnowledgeFactMergeResult MergeWithReport(params KnowledgeExtractionResult[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var entities = new Dictionary<string, KnowledgeEntityFact>(StringComparer.Ordinal);
        var assertions = new Dictionary<(string SubjectId, string PredicateId, string ObjectId), KnowledgeAssertionFact>();
        var aliases = new KnowledgeFactAliasIndex();
        var pendingAssertions = new List<KnowledgeAssertionFact>();
        var warnings = new List<KnowledgeGraphNormalizationWarning>();

        foreach (var result in results)
        {
            ArgumentNullException.ThrowIfNull(result);

            foreach (var entity in result.Entities)
            {
                var canonical = _canonicalizer.CanonicalizeEntity(entity);
                if (!KnowledgeFactCanonicalizer.IsValidEntity(canonical))
                {
                    warnings.Add(KnowledgeGraphNormalizationWarningFactory.CreateForEntity(
                        KnowledgeGraphNormalizationWarningCode.InvalidNodeRemoved,
                        canonical));
                    continue;
                }

                if (canonical.Confidence > FullConfidence)
                {
                    warnings.Add(KnowledgeGraphNormalizationWarningFactory.CreateForEntity(
                        KnowledgeGraphNormalizationWarningCode.ConfidenceNormalized,
                        canonical));
                    canonical = canonical with { Confidence = FullConfidence };
                }

                var normalized = KnowledgeGraphEntityRelationshipNormalizer.Normalize([canonical], warnings).Single();
                UpsertEntity(entities, aliases, normalized);
            }

            pendingAssertions.AddRange(result.Assertions);
        }

        foreach (var assertion in pendingAssertions)
        {
            if (KnowledgeFactCanonicalizer.HasMalformedAbsoluteIdentifier(assertion.SubjectId) ||
                KnowledgeFactCanonicalizer.HasMalformedAbsoluteIdentifier(assertion.ObjectId))
            {
                warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                    KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved,
                    assertion));
                continue;
            }

            var canonical = RewriteAssertionAliases(_canonicalizer.CanonicalizeAssertion(assertion), aliases.EntityAliases);
            if (KnowledgeFactCanonicalizer.IsValidAssertion(canonical))
            {
                UpsertAssertion(assertions, canonical, warnings);
            }
            else
            {
                warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
                    KnowledgeGraphNormalizationWarningCode.InvalidEdgeRemoved,
                    canonical));
            }
        }

        var normalizedEntities = KnowledgeGraphEntityRelationshipNormalizer.Normalize(
            entities.Values.ToList(),
            warnings);
        var facts = new KnowledgeExtractionResult
        {
            Entities = normalizedEntities.OrderBy(entity => entity.Label, StringComparer.OrdinalIgnoreCase).ToList(),
            Assertions = assertions.Values.ToList(),
        };

        return new KnowledgeFactMergeResult(facts, warnings);
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
            SameAs = existing.SameAs.Concat(entity.SameAs).ToList(),
            Confidence = Math.Max(existing.Confidence, entity.Confidence),
            Source = string.IsNullOrWhiteSpace(existing.Source) ? entity.Source : existing.Source,
            Sources = KnowledgeFactSourceCollector.MergeEntitySources(existing, entity),
        };
        aliases.Index(key, entities[key]);
        aliases.Index(key, entity);
    }

    private static void UpsertAssertion(
        IDictionary<(string SubjectId, string PredicateId, string ObjectId), KnowledgeAssertionFact> assertions,
        KnowledgeAssertionFact assertion,
        ICollection<KnowledgeGraphNormalizationWarning> warnings)
    {
        var predicateId = KnowledgeGraphPredicateResolver.Resolve(assertion.Predicate)?.AbsoluteUri ?? assertion.Predicate;
        var key = (assertion.SubjectId, predicateId, assertion.ObjectId);
        if (!assertions.TryGetValue(key, out var existing))
        {
            assertions[key] = assertion;
            return;
        }

        warnings.Add(KnowledgeGraphNormalizationWarningFactory.Create(
            KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved,
            assertion));
        assertions[key] = existing with
        {
            Confidence = Math.Max(existing.Confidence, assertion.Confidence),
            Source = KnowledgeFactSourceCollector.SelectPrimaryAssertionSource(existing, assertion),
            Sources = KnowledgeFactSourceCollector.MergeAssertionSources(existing, assertion),
        };
    }
}
