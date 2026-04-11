using ManagedCode.MarkdownLd.Kb.Rdf;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public static class TestKnowledgeGraphFactory
{
    public static KnowledgeGraphDocument CreateDocument()
    {
        var article = new KnowledgeArticle(
            new Uri("https://example.com/articles/what-is-a-knowledge-graph/"),
            "What is a Knowledge Graph?",
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 2),
            ["knowledge-graph", "sparql", "linked-data", "markdown"],
            "An introduction to knowledge graphs and linked data.");

        var entities = new[]
        {
            new KnowledgeEntity(
                new Uri("https://example.com/id/rdf"),
                "RDF",
                KbNamespaces.SchemaThing,
                [new Uri("https://www.wikidata.org/entity/Q519"),
                 new Uri("https://dbpedia.org/resource/Resource_Description_Framework")]),
            new KnowledgeEntity(
                new Uri("https://example.com/id/sparql"),
                "SPARQL",
                KbNamespaces.SchemaThing,
                [new Uri("https://www.wikidata.org/entity/Q54837")]),
            new KnowledgeEntity(
                new Uri("https://example.com/id/google"),
                "Google",
                KbNamespaces.SchemaOrganization,
                [new Uri("https://www.wikidata.org/entity/Q95")]),
        };

        var assertions = new[]
        {
            new KnowledgeAssertion(
                article.Id,
                KbNamespaces.SchemaMentions,
                entities[0].Id,
                0.91m,
                new Uri("urn:kb:source:article-1"),
                new Uri("urn:kb:chunk:article-1"),
                "content/2026/04/what-is-a-knowledge-graph.md",
                15,
                42),
            new KnowledgeAssertion(
                article.Id,
                KbNamespaces.SchemaMentions,
                entities[1].Id,
                0.93m,
                new Uri("urn:kb:source:article-1"),
                new Uri("urn:kb:chunk:article-1"),
                "content/2026/04/what-is-a-knowledge-graph.md",
                43,
                68),
            new KnowledgeAssertion(
                entities[2].Id,
                KbNamespaces.KbRelatedTo,
                entities[1].Id,
                0.62m,
                new Uri("urn:kb:source:article-1"),
                new Uri("urn:kb:chunk:article-2"),
                "content/2026/04/what-is-a-knowledge-graph.md",
                120,
                165),
        };

        return new KnowledgeGraphDocument(article, entities, assertions);
    }

    public static KnowledgeGraphBuilder CreateBuilder() => new();

    public static Graph BuildGraph()
    {
        return CreateBuilder().Build(CreateDocument());
    }
}
