using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeGraphPredicateResolver
{
    public static Uri? Resolve(string predicate)
    {
        if (predicate.Contains(':', StringComparison.Ordinal))
        {
            return ResolvePrefixedOrAbsolute(predicate);
        }

        return predicate.ToLowerInvariant() switch
        {
            MentionPredicateKey => SchemaMentionsUri,
            AboutPredicateKey => SchemaAboutUri,
            AuthorPredicateKey => SchemaAuthorUri,
            CreatorPredicateKey => SchemaCreatorUri,
            HasPartPredicateKey => SchemaHasPartUri,
            SameAsPredicateKey => SchemaSameAsUri,
            RelatedToPredicateKey => KbRelatedToUri,
            MemberOfPredicateKey => KbMemberOfUri,
            NextStepPredicateKey => KbNextStepUri,
            _ => null,
        };
    }

    private static Uri? ResolvePrefixedOrAbsolute(string predicate)
    {
        var separatorIndex = predicate.IndexOf(':');
        var prefix = predicate[..separatorIndex];
        var local = predicate[(separatorIndex + 1)..];
        return prefix.ToLowerInvariant() switch
        {
            SchemaPrefix => new Uri(SchemaNamespaceText + local),
            KbPrefix => new Uri(KbNamespaceText + local),
            ProvPrefix => new Uri(ProvNamespaceText + local),
            RdfPrefix => new Uri(RdfNamespaceText + local),
            RdfsPrefix => new Uri(RdfsNamespaceText + local),
            OwlPrefix => new Uri(OwlNamespaceText + local),
            SkosPrefix => new Uri(SkosNamespaceText + local),
            XsdPrefix => new Uri(XsdNamespaceText + local),
            _ => Uri.TryCreate(predicate, UriKind.Absolute, out var absolute) ? absolute : null,
        };
    }
}
