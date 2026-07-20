using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal sealed class KnowledgeFactCanonicalizer(Uri? baseUri)
{
    private readonly Uri _baseUri = KnowledgeNaming.NormalizeBaseUri(
        baseUri ?? new Uri(DefaultBaseUriText, UriKind.Absolute));

    public KnowledgeEntityFact CanonicalizeEntity(KnowledgeEntityFact entity)
    {
        var label = entity.Label?.Trim() ?? string.Empty;
        return entity with
        {
            Id = CanonicalizeNodeId(entity.Id ?? label),
            Label = label,
            Type = string.IsNullOrWhiteSpace(entity.Type) ? DefaultSchemaThing : entity.Type.Trim(),
            SameAs = (entity.SameAs ?? []).Select(static value => value?.Trim() ?? string.Empty).ToList(),
            Source = entity.Source ?? string.Empty,
            Sources = KnowledgeFactSourceCollector.MergeEntitySources(entity),
        };
    }

    public KnowledgeAssertionFact CanonicalizeAssertion(KnowledgeAssertionFact assertion)
    {
        return assertion with
        {
            SubjectId = CanonicalizeNodeId(assertion.SubjectId),
            ObjectId = CanonicalizeNodeId(assertion.ObjectId),
            Predicate = string.IsNullOrWhiteSpace(assertion.Predicate)
                ? string.Empty
                : KnowledgeNaming.NormalizePredicate(assertion.Predicate),
            Source = assertion.Source ?? string.Empty,
            Sources = KnowledgeFactSourceCollector.MergeAssertionSources(assertion),
        };
    }

    public static bool IsValidEntity(KnowledgeEntityFact entity)
    {
        return !string.IsNullOrWhiteSpace(entity.Label) &&
               Uri.TryCreate(entity.Id, UriKind.Absolute, out _) &&
               double.IsFinite(entity.Confidence) &&
               entity.Confidence >= ZeroConfidence;
    }

    public static bool IsValidAssertion(KnowledgeAssertionFact assertion)
    {
        return !string.IsNullOrWhiteSpace(assertion.SubjectId) &&
               !string.IsNullOrWhiteSpace(assertion.Predicate) &&
               !string.IsNullOrWhiteSpace(assertion.ObjectId);
    }

    public static bool HasMalformedAbsoluteIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        var colonIndex = trimmed.IndexOf(ColonCharacter);
        return colonIndex > 0 && !Uri.TryCreate(trimmed, UriKind.Absolute, out _);
    }

    private string CanonicalizeNodeId(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(nodeId, UriKind.Absolute, out var absolute))
        {
            return absolute.AbsoluteUri;
        }

        return nodeId.StartsWith(UriSchemePrefix, StringComparison.OrdinalIgnoreCase)
            ? nodeId
            : KnowledgeNaming.CreateEntityId(_baseUri, nodeId);
    }
}
