using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class KnowledgeSearchServiceTests
{
    [Fact]
    public void SearchEntitiesFindsMatchingEntity()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchEntities("rdf");

        Assert.Single(results);
        Assert.Equal(new Uri("https://example.com/id/rdf"), results[0].Id);
        Assert.Equal("RDF", results[0].Label);
        Assert.Equal(KbNamespaces.SchemaThing.AbsoluteUri, results[0].Type);
    }

    [Fact]
    public void SearchArticlesFindsByTitleAndMentionedEntityLabel()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var byTitle = service.SearchArticles("knowledge graph");
        Assert.Single(byTitle);
        Assert.Equal("What is a Knowledge Graph?", byTitle[0].Title);

        var byEntity = service.SearchArticles("sparql");
        Assert.Single(byEntity);
        Assert.Equal("What is a Knowledge Graph?", byEntity[0].Title);
    }

    [Fact]
    public void SearchArticlesByEntityLabelFindsMentionedArticle()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchArticlesByEntityLabel("sparql");

        Assert.Single(results);
        Assert.Equal(new Uri("https://example.com/articles/what-is-a-knowledge-graph/"), results[0].Id);
    }
}
