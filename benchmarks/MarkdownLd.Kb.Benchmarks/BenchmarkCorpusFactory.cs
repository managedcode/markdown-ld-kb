using ManagedCode.MarkdownLd.Kb.Pipeline;

namespace ManagedCode.MarkdownLd.Kb.Benchmarks;

internal static class BenchmarkCorpusFactory
{
    private const string BaseUriText = "https://bench.example/";
    private const string LocalFederatedEndpointText = "https://bench.example/sparql/local";
    private const string CacheQuery = "cache restore manifest evidence";
    private const string LongQuery = "incident escalation recovery dependency timeline checkpoint";
    private const string TokenizedQuery = "cache restore manifest token 実行 evidence";
    private const string FederatedQuery = "federated sparql service binding runbook evidence";
    private const string TypoQuery = "cach restre manifst evidnce";
    private const string LongTypoQuery = "incidnt escalaton recovry depndency chekpoint";
    private const string TokenizedTypoQuery = "cach restore manifst tokne 実行 evidnce";
    private const string FederatedTypoQuery = "federatd sparq servce bindng evidnce";
    private const string NoMatchQuery = "satellite coffee roasting";
    private static readonly Uri BaseUri = new(BaseUriText);
    private static readonly Uri LocalFederatedEndpoint = new(LocalFederatedEndpointText);

    public static MarkdownSourceDocument[] CreateSources(BenchmarkCorpusProfile profile)
    {
        return BenchmarkMarkdownCorpus.CreateSources(profile);
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

    public static string GetQuery(BenchmarkCorpusProfile profile, BenchmarkQueryScenario scenario)
    {
        return scenario switch
        {
            BenchmarkQueryScenario.Typo => GetTypoQuery(profile),
            BenchmarkQueryScenario.NoMatch => NoMatchQuery,
            _ => GetExactQuery(profile),
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

    private static string GetExactQuery(BenchmarkCorpusProfile profile)
    {
        return profile switch
        {
            BenchmarkCorpusProfile.LongDocuments => LongQuery,
            BenchmarkCorpusProfile.TokenizedMultilingual => TokenizedQuery,
            BenchmarkCorpusProfile.FederatedRunbooks => FederatedQuery,
            _ => CacheQuery,
        };
    }

    private static string GetTypoQuery(BenchmarkCorpusProfile profile)
    {
        return profile switch
        {
            BenchmarkCorpusProfile.LongDocuments => LongTypoQuery,
            BenchmarkCorpusProfile.TokenizedMultilingual => TokenizedTypoQuery,
            BenchmarkCorpusProfile.FederatedRunbooks => FederatedTypoQuery,
            _ => TypoQuery,
        };
    }
}
