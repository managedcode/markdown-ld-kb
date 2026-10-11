using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using Shouldly;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class GraphFragmentIdentityFlowTests
{
    [Test]
    public async Task Markdown_semantic_layers_keep_fragment_concepts_labels_and_same_as_targets_distinct()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri("https://fragments.example/"), extractionMode: MarkdownKnowledgeExtractionMode.None);
        var result = await pipeline.BuildAsync([new MarkdownSourceDocument("concepts.md", "# Concept catalogue")], new KnowledgeGraphBuildOptions
        {
            Entities =
            [
                new() { Id = "https://fragments.example/concepts#alpha", Label = "Alpha", Type = "schema:DefinedTerm", SameAs = ["https://fragments.example/external#first", "https://fragments.example/external#second"] },
                new() { Id = "https://fragments.example/concepts#beta", Label = "Beta", Type = "schema:DefinedTerm", SameAs = ["https://fragments.example/external#third"] }
            ]
        });
        using var graph = result.Graph;
        (await graph.ExecuteAskAsync("""
            PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
            ASK {
              <https://fragments.example/concepts#alpha> skos:prefLabel "Alpha" ; skos:exactMatch <https://fragments.example/external#first>, <https://fragments.example/external#second> .
              <https://fragments.example/concepts#beta> skos:prefLabel "Beta" ; skos:exactMatch <https://fragments.example/external#third> .
            }
            """)).ShouldBeTrue();
        (await graph.ExecuteAskAsync("""
            PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
            ASK { <https://fragments.example/concepts#alpha> skos:exactMatch <https://fragments.example/external#third> }
            """)).ShouldBeFalse();
    }

    [Test]
    public void Entity_search_retains_fragment_ids_and_all_fragment_same_as_targets()
    {
        using var graph = new Graph();
        graph.LoadFromString("""
            @prefix s: <https://schema.org/> .
            <https://fragments.example/entity#one> a s:Thing ; s:name "Weather one" ; s:sameAs <https://fragments.example/link#one>, <https://fragments.example/link#two> .
            <https://fragments.example/entity#two> a s:Thing, s:DefinedTerm ; s:name "Weather two" .
            """, new TurtleParser());
        var results = new KnowledgeSearchService(graph).SearchEntities("Weather");
        results.Select(item => item.Id.AbsoluteUri).Order().ShouldBe(new[] { "https://fragments.example/entity#one", "https://fragments.example/entity#two" }.Order());
        results.Single(item => item.Id.Fragment == "#two").SameAs.ShouldBeEmpty();
        results.Single(item => item.Id.Fragment == "#one").SameAs.Select(uri => uri.AbsoluteUri).Order().ShouldBe(new[] { "https://fragments.example/link#one", "https://fragments.example/link#two" }.Order());
    }

    [Test]
    public void Article_search_retains_distinct_fragment_articles()
    {
        using var graph = new Graph();
        graph.LoadFromString("""
            @prefix s: <https://schema.org/> .
            <https://fragments.example/article#one> a s:Article ; s:name "Weather article one" .
            <https://fragments.example/article#two> a s:Article ; s:name "Weather article two" .
            """, new TurtleParser());
        var results = new KnowledgeSearchService(graph).SearchArticles("Weather");
        results.Select(item => item.Id.AbsoluteUri).Order().ShouldBe(new[] { "https://fragments.example/article#one", "https://fragments.example/article#two" }.Order());
    }
}
