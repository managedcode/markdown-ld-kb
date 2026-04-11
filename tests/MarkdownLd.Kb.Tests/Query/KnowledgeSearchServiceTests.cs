using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using ManagedCode.MarkdownLd.Kb.Tests.Rdf;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class KnowledgeSearchServiceTests
{
    private const string RdfTerm = "rdf";
    private const string NoSuchTerm = "no-such-term";
    private const string NoSuchEntityTerm = "no-such-entity";
    private const string KnowledgeGraphTerm = "knowledge graph";
    private const string SparqlTerm = "sparql";
    private const string RdfLabel = "RDF";
    private const string KnowledgeGraphTitle = "What is a Knowledge Graph?";
    private const string RdfEntityUriText = "https://example.com/id/rdf";
    private const string KnowledgeGraphArticleUriText = "https://example.com/articles/what-is-a-knowledge-graph/";
    private static readonly Uri RdfEntityUri = new(RdfEntityUriText);
    private static readonly Uri KnowledgeGraphArticleUri = new(KnowledgeGraphArticleUriText);

    [Test]
    public void SearchEntitiesFindsMatchingEntity()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchEntities(RdfTerm);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(RdfEntityUri);
        results[0].Label.ShouldBe(RdfLabel);
        results[0].Type.ShouldBe(KbNamespaces.SchemaThing.AbsoluteUri);
    }

    [Test]
    public void SearchEntitiesReturnsEmptyListWhenNoMatch()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchEntities(NoSuchTerm);

        results.ShouldBeEmpty();
    }

    [Test]
    public void SearchArticlesFindsByTitleAndMentionedEntityLabel()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var byTitle = service.SearchArticles(KnowledgeGraphTerm);
        byTitle.Count.ShouldBe(1);
        byTitle[0].Title.ShouldBe(KnowledgeGraphTitle);

        var byEntity = service.SearchArticles(SparqlTerm);
        byEntity.Count.ShouldBe(1);
        byEntity[0].Title.ShouldBe(KnowledgeGraphTitle);
    }

    [Test]
    public void SearchArticlesReturnsEmptyListWhenNoMatch()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchArticles(NoSuchTerm);

        results.ShouldBeEmpty();
    }

    [Test]
    public void SearchArticlesByEntityLabelFindsMentionedArticle()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchArticlesByEntityLabel(SparqlTerm);

        results.Count.ShouldBe(1);
        results[0].Id.ShouldBe(KnowledgeGraphArticleUri);
    }

    [Test]
    public void SearchArticlesByEntityLabelReturnsEmptyListWhenNoMatch()
    {
        var service = new KnowledgeSearchService(TestKnowledgeGraphFactory.BuildGraph());

        var results = service.SearchArticlesByEntityLabel(NoSuchEntityTerm);

        results.ShouldBeEmpty();
    }
}
