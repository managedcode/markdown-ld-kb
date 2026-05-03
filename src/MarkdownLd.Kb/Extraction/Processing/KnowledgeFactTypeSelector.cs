using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

internal static class KnowledgeFactTypeSelector
{
    private static readonly IReadOnlyDictionary<string, int> TypePriorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [SchemaPersonTypeText] = 5,
        [SchemaOrganizationTypeText] = 5,
        [SchemaSoftwareApplicationTypeText] = 5,
        [SchemaTechArticleTypeText] = 4,
        [SchemaScholarlyArticleTypeText] = 4,
        [SchemaBlogPostingTypeText] = 4,
        [SchemaCreativeWorkTypeText] = 4,
        [SchemaArticleTypeText] = 4,
        [SkosConceptTypeText] = 4,
        [SkosConceptSchemeTypeText] = 4,
        [KbKnowledgeConceptTypeText] = 4,
        [KbKnowledgeConceptSchemeTypeText] = 4,
        [KbMarkdownDocumentTypeText] = 4,
        [KbKnowledgeAssertionTypeText] = 4,
        [SchemaDefinedTermTypeText] = 3,
        [SchemaThingTypeText] = 1,
    };

    public static string PreferHigherPriority(string left, string right)
    {
        return TypePriority(right) > TypePriority(left) ? right : left;
    }

    private static int TypePriority(string type)
    {
        return TypePriorities.TryGetValue(type, out var priority) ? priority : 0;
    }
}
