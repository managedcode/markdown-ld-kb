using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class JsonLdRoundTripFlowTests
{
    private const string BaseUrlText = "https://jsonld-roundtrip.example/";
    private const string MarkdownPath = "operations/jsonld-round-trip.md";
    private const string ArticleUri = "https://jsonld-roundtrip.example/operations/jsonld-round-trip/";
    private const string SearchSubjectKey = "subject";
    private const string SearchTerm = "portable jsonld";
    private const string JsonLdFileName = "round-trip.payload";
    private const string JsonLdStoreLocation = "graphs/runtime/round-trip.payload";
    private const string JsonLdTitle = "JSON-LD Round Trip";
    private const string JsonLdMarkdown = """
---
title: JSON-LD Round Trip
summary: Portable JSON-LD export keeps graph search available after reload.
keywords:
  - portable jsonld
  - graph search
---
# JSON-LD Round Trip

JSON-LD export should preserve enough RDF graph detail for search and SPARQL after reload.
""";

    private const string RoundTripAskQuery = """
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://jsonld-roundtrip.example/operations/jsonld-round-trip/> a schema:Article ;
    schema:name "JSON-LD Round Trip" ;
    schema:keywords "portable jsonld" .
}
""";

    [Test]
    public async Task GeneratedJsonLdTextLoadsBackIntoSearchableGraph()
    {
        var source = new MarkdownSourceDocument(MarkdownPath, JsonLdMarkdown);
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUrlText));
        var result = await pipeline.BuildAsync([source]);

        var jsonLd = result.Graph.SerializeJsonLd();
        jsonLd.ShouldContain(JsonLdTitle);

        var loaded = KnowledgeGraph.LoadJsonLd(jsonLd);

        (await loaded.ExecuteAskAsync(RoundTripAskQuery)).ShouldBeTrue();
        await AssertSearchFindsRoundTripArticleAsync(loaded);
    }

    [Test]
    public async Task ExplicitJsonLdFileAndStoreHelpersRoundTripSearchableGraphWithoutExtensionInference()
    {
        using var temp = CreateTempDirectory();
        var source = new MarkdownSourceDocument(MarkdownPath, JsonLdMarkdown);
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUrlText));
        var result = await pipeline.BuildAsync([source]);
        var filePath = Path.Combine(temp.RootPath, JsonLdFileName);

        await result.Graph.SaveJsonLdToFileAsync(filePath);
        var loadedFromFile = await KnowledgeGraph.LoadJsonLdFromFileAsync(filePath);

        (await loadedFromFile.ExecuteAskAsync(RoundTripAskQuery)).ShouldBeTrue();
        await AssertSearchFindsRoundTripArticleAsync(loadedFromFile);

        var store = new InMemoryKnowledgeGraphStore();
        await result.Graph.SaveJsonLdToStoreAsync(store, JsonLdStoreLocation);
        var loadedFromStore = await KnowledgeGraph.LoadJsonLdFromStoreAsync(store, JsonLdStoreLocation);

        (await loadedFromStore.ExecuteAskAsync(RoundTripAskQuery)).ShouldBeTrue();
        await AssertSearchFindsRoundTripArticleAsync(loadedFromStore);
    }

    [Test]
    public void LoadJsonLdRejectsEmptyContentExplicitly()
    {
        var exception = Should.Throw<ArgumentException>(() => KnowledgeGraph.LoadJsonLd(string.Empty));

        exception.Message.ShouldContain("JSON-LD content is required.");
    }

    private static async Task AssertSearchFindsRoundTripArticleAsync(KnowledgeGraph graph)
    {
        var search = await graph.SearchAsync(SearchTerm);

        search.Rows.Any(row =>
            row.Values.TryGetValue(SearchSubjectKey, out var subject) &&
            string.Equals(subject, ArticleUri, StringComparison.Ordinal)).ShouldBeTrue();
    }

    private static TempDirectory CreateTempDirectory()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "markdown-ld-kb-jsonld-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        return new TempDirectory(rootPath);
    }

    private sealed class TempDirectory(string rootPath) : IDisposable
    {
        public string RootPath { get; } = rootPath;

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
