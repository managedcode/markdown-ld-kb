using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaRichCatalogFlowTests
{
    [Test]
    public async Task Generated_rich_catalog_retains_primary_evidence_and_candidate_results()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://rich.example/"), extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var sources = Enumerable.Range(1, 120).Select(index => new MarkdownSourceDocument($"tools/tool-{index:D3}.md", index == 81
            ? "# Weather forecast specialist\n\nGet weather forecast, rain, wind, umbrella planning, and precipitation details for a city or region.\n\n## Arguments\nQuery: string."
            : $"# Archive lookup {index:D3}\n\nHandle archive lookup workflow number {index:D3} for genealogy records.\n\n## Arguments\nQuery: string.")).ToArray();
        var build = await pipeline.BuildAsync(sources);
        using var graph = build.Graph;
        graph.TripleCount.ShouldBeGreaterThan(8_192);
        var profile = KnowledgeGraphSchemaSearchProfile.Default with
        {
            TypeFilters = ["schema:Article"],
            TextPredicates = [new("schema:name"), new("schema:description"), new("schema:keywords")],
            RelationshipPredicates = [],
            TermMode = KnowledgeGraphSchemaSearchTermMode.AnyTerm,
            MaxResults = 5
        };
        var full = await graph.SearchBySchemaAsync("weather", profile);
        full.Matches.Single().NodeId.ShouldBe("https://rich.example/tools/tool-081/");
        full.Matches.Single().Evidence.Select(evidence => evidence.PredicateId).ShouldContain("https://schema.org/name");
        var bounded = await graph.SearchBySchemaAsync("weather", profile with { CandidateNodeIds = [full.Matches.Single().NodeId] });
        bounded.Matches.Single().NodeId.ShouldBe(full.Matches.Single().NodeId);
        bounded.Matches.Single().Evidence.Select(evidence => evidence.MatchedText).Order().ShouldBe(full.Matches.Single().Evidence.Select(evidence => evidence.MatchedText).Order());
        (await graph.SearchBySchemaAsync("satellite coffee roasting", profile)).Matches.ShouldBeEmpty();
        (await graph.SearchBySchemaAsync("weather", profile with { CandidateNodeIds = [] })).Matches.ShouldBeEmpty();
    }
}
