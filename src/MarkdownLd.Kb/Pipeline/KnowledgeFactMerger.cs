namespace ManagedCode.MarkdownLd.Kb;

public sealed class KnowledgeFactMerger
{
    private readonly Uri _baseUri;

    public KnowledgeFactMerger(Uri? baseUri = null)
    {
        _baseUri = KnowledgeNaming.NormalizeBaseUri(baseUri ?? new Uri("https://example.com/", UriKind.Absolute));
    }

    public KnowledgeExtractionResult Merge(params KnowledgeExtractionResult[] results)
    {
        var entities = new Dictionary<string, KnowledgeEntityFact>(StringComparer.OrdinalIgnoreCase);
        var assertions = new Dictionary<string, KnowledgeAssertionFact>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            if (result is null)
            {
                continue;
            }

            foreach (var entity in result.Entities)
            {
                UpsertEntity(entities, CanonicalizeEntity(entity));
            }

            foreach (var assertion in result.Assertions)
            {
                UpsertAssertion(assertions, CanonicalizeAssertion(assertion));
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
        var canonicalId = KnowledgeNaming.CreateEntityId(_baseUri, label);
        return entity with
        {
            Id = canonicalId,
            Label = label,
            Type = string.IsNullOrWhiteSpace(entity.Type) ? "schema:Thing" : entity.Type.Trim(),
            SameAs = entity.SameAs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Source = entity.Source,
        };
    }

    private KnowledgeAssertionFact CanonicalizeAssertion(KnowledgeAssertionFact assertion)
    {
        return assertion with
        {
            SubjectId = CanonicalizeNodeId(assertion.SubjectId),
            ObjectId = CanonicalizeNodeId(assertion.ObjectId),
            Predicate = KnowledgeNaming.NormalizePredicate(assertion.Predicate),
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

        if (nodeId.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
        {
            return nodeId;
        }

        return KnowledgeNaming.CreateEntityId(_baseUri, nodeId);
    }

    private static void UpsertEntity(IDictionary<string, KnowledgeEntityFact> entities, KnowledgeEntityFact entity)
    {
        var key = entity.Id ?? entity.Label;
        if (!entities.TryGetValue(key, out var existing))
        {
            entities[key] = entity;
            return;
        }

        entities[key] = existing with
        {
            Label = existing.Label.Length >= entity.Label.Length ? existing.Label : entity.Label,
            Type = PreferHigherPriority(existing.Type, entity.Type),
            SameAs = existing.SameAs.Concat(entity.SameAs).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Confidence = Math.Max(existing.Confidence, entity.Confidence),
            Source = string.IsNullOrWhiteSpace(existing.Source) ? entity.Source : existing.Source,
        };
    }

    private static void UpsertAssertion(IDictionary<string, KnowledgeAssertionFact> assertions, KnowledgeAssertionFact assertion)
    {
        var key = $"{assertion.SubjectId}||{assertion.Predicate}||{assertion.ObjectId}";
        if (!assertions.TryGetValue(key, out var existing))
        {
            assertions[key] = assertion;
            return;
        }

        assertions[key] = existing with
        {
            Confidence = Math.Max(existing.Confidence, assertion.Confidence),
            Source = string.IsNullOrWhiteSpace(existing.Source) ? assertion.Source : existing.Source,
        };
    }

    private static string PreferHigherPriority(string left, string right)
    {
        return TypePriority(right) > TypePriority(left) ? right : left;
    }

    private static int TypePriority(string type)
    {
        return type switch
        {
            "schema:Person" => 5,
            "schema:Organization" => 5,
            "schema:SoftwareApplication" => 5,
            "schema:CreativeWork" => 4,
            "schema:Article" => 4,
            "schema:Thing" => 1,
            _ => 0,
        };
    }
}
