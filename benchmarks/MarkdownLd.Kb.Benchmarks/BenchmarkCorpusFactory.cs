using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

internal static class BenchmarkCorpusFactory
{
    private const string BaseUriText = "https://bench.example/";
    private const string LocalFederatedEndpointText = "https://bench.example/sparql/local";
    private const string CacheTitlePrefix = "Cache restore runbook";
    private const string BillingTitlePrefix = "Billing export guide";
    private const string ReleaseTitlePrefix = "Release evidence checklist";
    private const string CacheQuery = "cache restore manifest evidence";
    private const string TypoQuery = "cach restre manifst evidnce";
    private const string NoMatchQuery = "satellite coffee roasting";
    private static readonly Uri BaseUri = new(BaseUriText);
    private static readonly Uri LocalFederatedEndpoint = new(LocalFederatedEndpointText);

    public static MarkdownSourceDocument[] CreateSources(int documentCount)
    {
        return Enumerable.Range(0, documentCount)
            .Select(index => new MarkdownSourceDocument(
                $"content/bench/doc-{index:D5}.md",
                CreateMarkdown(index)))
            .ToArray();
    }

    public static MarkdownKnowledgeBuildResult BuildNone(
        IReadOnlyList<MarkdownSourceDocument> sources)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.None);
        return pipeline.BuildAsync(sources).GetAwaiter().GetResult();
    }

    public static MarkdownKnowledgeBuildResult BuildTiktoken(
        IReadOnlyList<MarkdownSourceDocument> sources)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken,
            tiktokenOptions: new TiktokenKnowledgeGraphOptions
            {
                BuildAutoRelatedSegmentRelations = false,
            });
        return pipeline.BuildAsync(sources).GetAwaiter().GetResult();
    }

    public static string GetQuery(BenchmarkQueryScenario scenario)
    {
        return scenario switch
        {
            BenchmarkQueryScenario.Typo => TypoQuery,
            BenchmarkQueryScenario.NoMatch => NoMatchQuery,
            _ => CacheQuery,
        };
    }

    public static KnowledgeGraphRankedSearchOptions CreateRankedOptions(
        KnowledgeGraphSearchMode mode,
        bool fuzzy = false)
    {
        return new KnowledgeGraphRankedSearchOptions
        {
            Mode = mode,
            EnableFuzzyTokenMatching = fuzzy,
            MaxResults = 10,
        };
    }

    public static TokenDistanceSearchOptions CreateTokenDistanceOptions(bool fuzzy)
    {
        return new TokenDistanceSearchOptions
        {
            Limit = 10,
            EnableFuzzyQueryCorrection = fuzzy,
        };
    }

    public static KnowledgeGraphSchemaSearchProfile CreateFederatedProfile()
    {
        return KnowledgeGraphSchemaSearchProfile.Default with
        {
            FederatedServiceEndpoints = [LocalFederatedEndpoint],
            MaxResults = 10,
        };
    }

    public static FederatedSparqlExecutionOptions CreateFederatedOptions(KnowledgeGraph graph)
    {
        return new FederatedSparqlExecutionOptions
        {
            AllowedServiceEndpoints = [LocalFederatedEndpoint],
            LocalServiceBindings = [new FederatedSparqlLocalServiceBinding(LocalFederatedEndpoint, graph)],
        };
    }

    private static string CreateMarkdown(int index)
    {
        var family = index % 3;
        var title = CreateTitle(family, index);
        var topic = CreateTopic(family);
        var body = CreateBody(family, index);
        return $$"""
            ---
            title: {{title}}
            summary: {{topic}} summary for benchmark document {{index}}.
            tags:
              - benchmark
              - {{topic}}
            ---
            # {{title}}

            {{body}}
            """;
    }

    private static string CreateTitle(int family, int index)
    {
        return family switch
        {
            1 => $"{BillingTitlePrefix} {index:D5}",
            2 => $"{ReleaseTitlePrefix} {index:D5}",
            _ => $"{CacheTitlePrefix} {index:D5}",
        };
    }

    private static string CreateTopic(int family)
    {
        return family switch
        {
            1 => "billing invoice export payment checkpoint",
            2 => "release gate approval evidence checklist",
            _ => "cache restore manifest rollback evidence",
        };
    }

    private static string CreateBody(int family, int index)
    {
        var identifier = $"validationfingerprintcheckpointtoken{index:D5}manifestwindowrollbackevidence";
        return family switch
        {
            1 => $"Billing export verifies invoice payment checkpoint evidence with marker {identifier}.",
            2 => $"Release evidence checklist confirms approval gates and deployment notes with marker {identifier}.",
            _ => $"Cache restore validates manifest rollback evidence and runbook recovery with marker {identifier}.",
        };
    }
}
