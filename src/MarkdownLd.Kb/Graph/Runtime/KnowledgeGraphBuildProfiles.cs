using static ManagedCode.MarkdownLd.Kb.Pipeline.PipelineConstants;

namespace ManagedCode.MarkdownLd.Kb.Pipeline;

public static class KnowledgeGraphBuildProfiles
{
    private const string DocumentationProfileName = "documentation";
    private const string CapabilityWorkflowProfileName = "capability-workflow";
    private const string RunbookProfileName = "runbook";
    private const string DecisionLogProfileName = "decision-log";
    private const string ServiceCatalogProfileName = "service-catalog";
    private const int PresetMaxResults = 12;
    private const int PresetMaxRelatedResults = 8;
    private const int PresetMaxNextStepResults = 8;

    public static KnowledgeGraphBuildProfile Documentation { get; } = CreateDocumentationProfile(DocumentationProfileName);

    public static KnowledgeGraphBuildProfile CapabilityWorkflow { get; } = new()
    {
        Name = CapabilityWorkflowProfileName,
        SearchProfile = CreateWorkflowSearchProfile(),
    };

    public static KnowledgeGraphBuildProfile Runbook { get; } = new()
    {
        Name = RunbookProfileName,
        SearchProfile = CreateWorkflowSearchProfile(),
    };

    public static KnowledgeGraphBuildProfile DecisionLog { get; } = CreateDocumentationProfile(DecisionLogProfileName);

    public static KnowledgeGraphBuildProfile ServiceCatalog { get; } = CreateDocumentationProfile(ServiceCatalogProfileName);

    private static KnowledgeGraphBuildProfile CreateDocumentationProfile(string name)
    {
        return new KnowledgeGraphBuildProfile
        {
            Name = name,
            SearchProfile = CreateDocumentationSearchProfile(),
        };
    }

    private static KnowledgeGraphSchemaSearchProfile CreateDocumentationSearchProfile()
    {
        return new KnowledgeGraphSchemaSearchProfile
        {
            TextPredicates =
            [
                new KnowledgeGraphSchemaTextPredicate(ExpectedSchemaName, SchemaSearchDefaultTextWeight),
                new KnowledgeGraphSchemaTextPredicate(ExpectedSchemaDescription, SchemaSearchDescriptionWeight),
                new KnowledgeGraphSchemaTextPredicate(ExpectedSchemaKeywords, SchemaSearchKeywordWeight),
            ],
            RelationshipPredicates = [],
            ExpansionPredicates = [],
            TermMode = KnowledgeGraphSchemaSearchTermMode.AllTerms,
            MaxResults = PresetMaxResults,
            MaxRelatedResults = PresetMaxRelatedResults,
            MaxNextStepResults = PresetMaxNextStepResults,
        };
    }

    private static KnowledgeGraphSchemaSearchProfile CreateWorkflowSearchProfile()
    {
        return CreateDocumentationSearchProfile() with
        {
            RelationshipPredicates =
            [
                new KnowledgeGraphSchemaRelationshipPredicate(
                    ExpectedKbRelatedTo,
                    [ExpectedSchemaName, ExpectedSchemaDescription],
                    SchemaSearchRelationshipWeight),
                new KnowledgeGraphSchemaRelationshipPredicate(
                    ExpectedSchemaMentions,
                    [ExpectedSchemaName, ExpectedSchemaDescription],
                    SchemaSearchRelationshipWeight),
            ],
            ExpansionPredicates =
            [
                new KnowledgeGraphSchemaExpansionPredicate(
                    ExpectedKbRelatedTo,
                    KnowledgeGraphSchemaSearchRole.Related,
                    SchemaSearchRelatedExpansionScore),
                new KnowledgeGraphSchemaExpansionPredicate(
                    ExpectedKbNextStep,
                    KnowledgeGraphSchemaSearchRole.NextStep,
                    SchemaSearchNextStepExpansionScore),
            ],
        };
    }
}
