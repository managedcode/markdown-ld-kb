using System.Text.Json;
using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SchemaEvidenceBudgetFlowTests
{
    private const string ForecastId = "https://evidence-budget.example/forecast";
    private const string Query = "weather forecast Paris";

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Bounded_search_keeps_the_most_relevant_direct_evidence(bool reverse, bool exact)
    {
        var forecast = exact ? Query : "Weather forecast for Paris";
        var alert = exact ? "List weather forecast alerts for Paris" : "List weather alerts";
        var tools = Enumerable.Range(0, 128)
            .Select(index => CreateTool($"https://evidence-budget.example/alert/{index:D3}", $"Weather alert {index:D3}", alert))
            .Append(CreateTool(ForecastId, "Paris forecast", forecast))
            .ToArray();
        if (reverse)
        {
            Array.Reverse(tools);
        }

        using var graph = KnowledgeGraph.LoadJsonLd(JsonSerializer.Serialize(
            new Dictionary<string, object> { ["@graph"] = tools }));
        var result = await graph.SearchBySchemaAsync(Query, new KnowledgeGraphSchemaSearchProfile
        {
            TypeFilters = ["schema:SoftwareApplication"],
            TextPredicates = [new("schema:description")],
            ExpansionPredicates = [],
            TermMode = KnowledgeGraphSchemaSearchTermMode.AnyTerm,
            MaxResults = 1,
        });

        var match = result.Matches.Single();
        match.NodeId.ShouldBe(ForecastId);
        match.Evidence.Single().MatchedText.ShouldBe(forecast);
        var replay = await graph.ExecuteSelectAsync(result.GeneratedSparql);
        replay.Rows.ShouldContain(row => row.Values["subject"] == ForecastId);
    }

    private static Dictionary<string, string> CreateTool(string id, string name, string description) =>
        new()
        {
            ["@id"] = id,
            ["@type"] = "https://schema.org/SoftwareApplication",
            ["https://schema.org/name"] = name,
            ["https://schema.org/description"] = description,
        };
}
