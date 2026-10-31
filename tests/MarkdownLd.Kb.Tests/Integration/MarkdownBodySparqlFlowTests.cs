using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class MarkdownBodySparqlFlowTests
{
    private const string BaseAddress = "https://body-sparql.example/";
    private const string SourcePath = "runbooks/body-only.md";
    private const string DocumentHeading = "# Operations handbook\n\n";
    private const string TextPredicate = "https://schema.org/text";
    private const string MissingQuery = "unrecorded satellite";

    [Test]
    [Arguments("citrine ledger review", true)]
    [Arguments("citrine ledger review", false)]
    [Arguments("brass \"sensor\" { checksum }", true)]
    public async Task PreparedJsonLd_ShouldPreserveBodyOnlyFactsForModelFreeSparql(string query, bool includeDocumentText)
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            BaseUri = new Uri(BaseAddress),
            ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            BuildOptions = includeDocumentText
                ? KnowledgeGraphBuildOptions.Default with { IncludeDocumentText = true }
                : KnowledgeGraphBuildOptions.Default
        });
        var built = await pipeline.BuildAsync([new MarkdownSourceDocument(SourcePath, DocumentHeading + query)]);
        var sourceUri = built.Documents.Single().DocumentUri.AbsoluteUri;
        string jsonLd;
        using (built.Graph)
        {
            jsonLd = built.Graph.SerializeJsonLd();
        }
        using var loaded = KnowledgeGraph.LoadJsonLd(jsonLd);
        var profile = KnowledgeGraphSchemaSearchProfile.Default with
        {
            TextPredicates = [new KnowledgeGraphSchemaTextPredicate(TextPredicate)],
            RelationshipPredicates = [],
            ExpansionPredicates = [],
            ExcludedTypes = [],
            MaxRelatedResults = 0,
            MaxNextStepResults = 0
        };
        var found = await loaded.SearchBySchemaAsync(query, profile);
        if (!includeDocumentText)
        {
            found.Matches.ShouldBeEmpty();
            return;
        }
        found.Matches.Count.ShouldBe(1);
        found.Matches[0].NodeId.ShouldBe(sourceUri);
        found.Matches[0].Evidence.Any(evidence => evidence.MatchedText.Contains(query, StringComparison.Ordinal)).ShouldBeTrue();
        (await loaded.SearchBySchemaAsync(MissingQuery, profile)).Matches.ShouldBeEmpty();
    }

    [Test]
    public async Task IncludingDocumentText_WithMetadataOnlySource_ShouldPreserveSearchableProvenance()
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            BaseUri = new Uri(BaseAddress),
            ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            BuildOptions = KnowledgeGraphBuildOptions.Default with { IncludeDocumentText = true }
        });
        var built = await pipeline.BuildAsync([new MarkdownSourceDocument(SourcePath, "---\ntitle: Sourcebook\n---\n")]);
        string jsonLd;
        using (built.Graph)
        {
            jsonLd = built.Graph.SerializeJsonLd();
        }
        using var loaded = KnowledgeGraph.LoadJsonLd(jsonLd);
        var profile = KnowledgeGraphSchemaSearchProfile.Default with { ExcludedTypes = [] };
        var found = await loaded.SearchBySchemaAsync("Sourcebook", profile);
        found.Matches.ShouldContain(match => match.NodeId == built.Documents.Single().DocumentUri.AbsoluteUri);
        (await loaded.SearchBySchemaAsync(MissingQuery, profile)).Matches.ShouldBeEmpty();
    }
}
