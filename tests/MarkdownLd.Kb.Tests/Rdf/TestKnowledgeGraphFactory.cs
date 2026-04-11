using ManagedCode.MarkdownLd.Kb.Rdf;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public static class TestKnowledgeGraphFactory
{
    private const string ArticleIdValue = "https://example.com/articles/what-is-a-knowledge-graph/";
    private const string ArticleTitle = "What is a Knowledge Graph?";
    private const string ArticleSummary = "An introduction to knowledge graphs and linked data.";
    private const string RdfEntityIdValue = "https://example.com/id/rdf";
    private const string RdfEntityLabel = "RDF";
    private const string SparqlEntityIdValue = "https://example.com/id/sparql";
    private const string SparqlEntityLabel = "SPARQL";
    private const string GoogleEntityIdValue = "https://example.com/id/google";
    private const string GoogleEntityLabel = "Google";
    private const string WikidataRdfValue = "https://www.wikidata.org/entity/Q519";
    private const string DbpediaRdfValue = "https://dbpedia.org/resource/Resource_Description_Framework";
    private const string WikidataSparqlValue = "https://www.wikidata.org/entity/Q54837";
    private const string WikidataGoogleValue = "https://www.wikidata.org/entity/Q95";
    private const string SourceUriValue = "urn:kb:source:article-1";
    private const string ChunkUriValue = "urn:kb:chunk:article-1";
    private const string DocumentPathValue = "content/2026/04/what-is-a-knowledge-graph.md";
    private const string RelatedChunkUriValue = "urn:kb:chunk:article-2";
    private const string KnowledgeGraphPath = "content/2026/04/what-is-a-knowledge-graph.md";
    private static readonly Uri ArticleId = new(ArticleIdValue);
    private static readonly Uri RdfEntityId = new(RdfEntityIdValue);
    private static readonly Uri SparqlEntityId = new(SparqlEntityIdValue);
    private static readonly Uri GoogleEntityId = new(GoogleEntityIdValue);
    private static readonly Uri WikidataRdf = new(WikidataRdfValue);
    private static readonly Uri DbpediaRdf = new(DbpediaRdfValue);
    private static readonly Uri WikidataSparql = new(WikidataSparqlValue);
    private static readonly Uri WikidataGoogle = new(WikidataGoogleValue);
    private static readonly Uri SourceUri = new(SourceUriValue);
    private static readonly Uri ChunkUri = new(ChunkUriValue);
    private static readonly Uri RelatedChunkUri = new(RelatedChunkUriValue);
    private static readonly string[] ArticleTags = ["knowledge-graph", "sparql", "linked-data", "markdown"];
    private static readonly Uri[] RdfSameAs = [WikidataRdf, DbpediaRdf];
    private static readonly Uri[] SparqlSameAs = [WikidataSparql];
    private static readonly Uri[] GoogleSameAs = [WikidataGoogle];

    public static KnowledgeGraphDocument CreateDocument()
    {
        var article = new KnowledgeArticle(
            ArticleId,
            ArticleTitle,
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 2),
            ArticleTags,
            ArticleSummary);

        var entities = new[]
        {
            new KnowledgeEntity(
                RdfEntityId,
                RdfEntityLabel,
                KbNamespaces.SchemaThing,
                RdfSameAs),
            new KnowledgeEntity(
                SparqlEntityId,
                SparqlEntityLabel,
                KbNamespaces.SchemaThing,
                SparqlSameAs),
            new KnowledgeEntity(
                GoogleEntityId,
                GoogleEntityLabel,
                KbNamespaces.SchemaOrganization,
                GoogleSameAs),
        };

        var assertions = new[]
        {
            new KnowledgeAssertion(
                article.Id,
                KbNamespaces.SchemaMentions,
                entities[0].Id,
                0.91m,
                SourceUri,
                ChunkUri,
                DocumentPathValue,
                15,
                42),
            new KnowledgeAssertion(
                article.Id,
                KbNamespaces.SchemaMentions,
                entities[1].Id,
                0.93m,
                SourceUri,
                ChunkUri,
                DocumentPathValue,
                43,
                68),
            new KnowledgeAssertion(
                entities[2].Id,
                KbNamespaces.KbRelatedTo,
                entities[1].Id,
                0.62m,
                SourceUri,
                RelatedChunkUri,
                KnowledgeGraphPath,
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
