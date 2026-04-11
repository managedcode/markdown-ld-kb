using Shouldly;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using ManagedCode.MarkdownLd.Kb.Tests.Rdf;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class KnowledgeSearchServiceTests
{
    [Test]
    public void SearchEntitiesFindsMatchingEntity()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchEntities("rdf");

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(new Uri("https://example.com/id/rdf"));
        results[0].Label.ShouldBe("RDF");
        results[0].Type.ShouldBe(KbNamespaces.SchemaThing.AbsoluteUri);
    }

    [Test]
    public void SearchEntitiesReturnsEmptyListWhenNoMatch()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchEntities("no-such-term");

        results.ShouldBeEmpty();
    }

    [Test]
    public void SearchArticlesFindsByTitleAndMentionedEntityLabel()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var byTitle = service.SearchArticles("knowledge graph");
        byTitle.Count.ShouldBe(1);
        byTitle[0].Title.ShouldBe("What is a Knowledge Graph?");

        var byEntity = service.SearchArticles("sparql");
        byEntity.Count.ShouldBe(1);
        byEntity[0].Title.ShouldBe("What is a Knowledge Graph?");
    }

    [Test]
    public void SearchArticlesReturnsEmptyListWhenNoMatch()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchArticles("no-such-term");

        results.ShouldBeEmpty();
    }

    [Test]
    public void SearchArticlesByEntityLabelFindsMentionedArticle()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchArticlesByEntityLabel("sparql");

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(new Uri("https://example.com/articles/what-is-a-knowledge-graph/"));
    }

    [Test]
    public void SearchArticlesByEntityLabelReturnsEmptyListWhenNoMatch()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchArticlesByEntityLabel("no-such-entity");

        results.ShouldBeEmpty();
    }
}
