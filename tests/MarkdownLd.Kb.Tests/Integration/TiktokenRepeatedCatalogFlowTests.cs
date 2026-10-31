using System.Text;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class TiktokenRepeatedCatalogFlowTests
{
    private const string BaseUriText = "https://catalog-budget.example/";
    private const int DocumentCount = 24;
    private const int MaximumJsonLdBytes = 2_000_000;

    [Test]
    public async Task Built_in_tiktoken_facts_do_not_create_duplicate_normalization_warnings()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            new Uri(BaseUriText),
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken,
            tiktokenOptions: new TiktokenKnowledgeGraphOptions
            {
                MaxRelatedSegments = 2,
            });

        var result = await pipeline.BuildAsync(CreateDocuments());

        var duplicateWarnings = result.Normalization.Warnings
            .Where(static warning =>
                warning.Code == KnowledgeGraphNormalizationWarningCode.DuplicateEdgeRemoved ||
                warning.Code == KnowledgeGraphNormalizationWarningCode.SymmetricDuplicateEdgeRemoved)
            .ToArray();
        duplicateWarnings.ShouldBeEmpty();
        result.Facts.Entities.Select(static entity => entity.Id).ShouldBeUnique();
        result.Graph.CanSearchByTokenDistance.ShouldBeTrue();
        Encoding.UTF8.GetByteCount(result.Graph.SerializeJsonLd()).ShouldBeLessThan(MaximumJsonLdBytes);

        var matches = await result.Graph.SearchByTokenDistanceAsync("catalog authorization revision workflow", 3);
        matches.ShouldNotBeEmpty();
    }

    [Test]
    public async Task Large_repeated_document_builds_serializes_and_finds_its_final_record()
    {
        const string marker = "FINAL-SUPPLIER-RECORD";
        var markdown = string.Concat(Enumerable.Repeat(
            "Product reference includes complete supplier specifications.\n", 35_000)) + marker;
        var bank = new MarkdownKnowledgeBank(new MarkdownKnowledgeBankOptions
        {
            PipelineOptions = new MarkdownKnowledgePipelineOptions
            {
                BaseUri = new Uri(BaseUriText),
                ExtractionMode = MarkdownKnowledgeExtractionMode.Tiktoken,
            },
        });

        var build = await bank.BuildAsync([new MarkdownSourceDocument("large.md", markdown)]);
        using var graph = build.Graph;
        var json = graph.SerializeJsonLd();
        using var parsed = System.Text.Json.JsonDocument.Parse(json);
        parsed.RootElement.GetArrayLength().ShouldBeGreaterThan(35_000);
        json.ShouldContain(marker);
        var matches = await graph.SearchByTokenDistanceAsync(marker, 5);
        matches.ShouldContain(match => match.Text.Contains(marker, StringComparison.Ordinal));
    }

    private static MarkdownSourceDocument[] CreateDocuments()
    {
        return Enumerable.Range(0, DocumentCount)
            .Select(index => new MarkdownSourceDocument(
                $"catalog/tool-{index:D2}.md",
                $$"""
                ---
                title: Catalog Tool {{index:D2}}
                ---
                # Catalog authorization workflow

                Catalog authorization validates owner identity, optimistic revision, and bounded input.
                Catalog authorization validates owner identity before every mutation.

                ## Usage examples

                Use catalog tool {{index:D2}} to inspect an exact resource and return a typed result.
                Use the catalog workflow to validate authorization and revision requirements.
                """,
                new Uri($"{BaseUriText}catalog/tool-{index:D2}/")))
            .ToArray();
    }
}
