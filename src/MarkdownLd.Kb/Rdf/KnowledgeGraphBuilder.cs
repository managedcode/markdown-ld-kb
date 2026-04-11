using System.Globalization;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Rdf;

public sealed class KnowledgeGraphBuilder
{
    public Graph Build(KnowledgeGraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var graph = new Graph();
        KbNamespaces.Register(graph);

        AddArticle(graph, document.Article);
        foreach (var entity in document.Entities)
        {
            AddEntity(graph, entity);
        }

        foreach (var assertion in document.Assertions)
        {
            AddAssertion(graph, assertion);
        }

        return graph;
    }

    private static void AddArticle(IGraph graph, KnowledgeArticle article)
    {
        var articleNode = graph.CreateUriNode(article.Id);
        graph.Assert(new Triple(articleNode, graph.CreateUriNode(KbNamespaces.RdfType), graph.CreateUriNode(KbNamespaces.SchemaArticle)));
        graph.Assert(new Triple(articleNode, graph.CreateUriNode(KbNamespaces.SchemaName), graph.CreateLiteralNode(article.Title)));

        if (article.DatePublished is not null)
        {
            graph.Assert(new Triple(
                articleNode,
                graph.CreateUriNode(KbNamespaces.SchemaDatePublished),
                graph.CreateLiteralNode(article.DatePublished.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), KbNamespaces.XsdDate)));
        }

        if (article.DateModified is not null)
        {
            graph.Assert(new Triple(
                articleNode,
                graph.CreateUriNode(KbNamespaces.SchemaDateModified),
                graph.CreateLiteralNode(article.DateModified.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), KbNamespaces.XsdDate)));
        }

        if (article.Tags is { Count: > 0 })
        {
            graph.Assert(new Triple(articleNode, graph.CreateUriNode(KbNamespaces.SchemaKeywords), graph.CreateLiteralNode(string.Join(", ", article.Tags))));
        }

        if (!string.IsNullOrWhiteSpace(article.Summary))
        {
            graph.Assert(new Triple(articleNode, graph.CreateUriNode(KbNamespaces.SchemaDescription), graph.CreateLiteralNode(article.Summary)));
        }
    }

    private static void AddEntity(IGraph graph, KnowledgeEntity entity)
    {
        var entityNode = graph.CreateUriNode(entity.Id);
        graph.Assert(new Triple(entityNode, graph.CreateUriNode(KbNamespaces.RdfType), graph.CreateUriNode(entity.Type)));
        graph.Assert(new Triple(entityNode, graph.CreateUriNode(KbNamespaces.SchemaName), graph.CreateLiteralNode(entity.Label)));

        if (entity.SameAs is { Count: > 0 })
        {
            foreach (var sameAs in entity.SameAs)
            {
                graph.Assert(new Triple(entityNode, graph.CreateUriNode(KbNamespaces.SchemaSameAs), graph.CreateUriNode(sameAs)));
            }
        }
    }

    private static void AddAssertion(IGraph graph, KnowledgeAssertion assertion)
    {
        graph.Assert(new Triple(graph.CreateUriNode(assertion.Subject), graph.CreateUriNode(assertion.Predicate), graph.CreateUriNode(assertion.Object)));

        var assertionNode = graph.CreateBlankNode();
        graph.Assert(new Triple(assertionNode, graph.CreateUriNode(KbNamespaces.RdfType), graph.CreateUriNode(KbNamespaces.KbAssertion)));
        graph.Assert(new Triple(assertionNode, graph.CreateUriNode(KbNamespaces.SchemaSubjectOf), graph.CreateUriNode(assertion.Subject)));
        graph.Assert(new Triple(assertionNode, graph.CreateUriNode(assertion.Predicate), graph.CreateUriNode(assertion.Object)));

        if (assertion.Confidence is not null)
        {
            graph.Assert(new Triple(
                assertionNode,
                graph.CreateUriNode(KbNamespaces.KbConfidence),
                graph.CreateLiteralNode(assertion.Confidence.Value.ToString(CultureInfo.InvariantCulture), KbNamespaces.XsdDecimal)));
        }

        if (assertion.Source is not null)
        {
            graph.Assert(new Triple(assertionNode, graph.CreateUriNode(new Uri($"{KbNamespaces.Prov}wasDerivedFrom")), graph.CreateUriNode(assertion.Source)));
        }

        if (assertion.Chunk is not null)
        {
            graph.Assert(new Triple(assertionNode, graph.CreateUriNode(KbNamespaces.KbChunk), graph.CreateUriNode(assertion.Chunk)));
        }

        if (!string.IsNullOrWhiteSpace(assertion.DocPath))
        {
            graph.Assert(new Triple(assertionNode, graph.CreateUriNode(KbNamespaces.KbDocPath), graph.CreateLiteralNode(assertion.DocPath)));
        }

        if (assertion.CharStart is not null)
        {
            graph.Assert(new Triple(
                assertionNode,
                graph.CreateUriNode(KbNamespaces.KbCharStart),
                graph.CreateLiteralNode(assertion.CharStart.Value.ToString(CultureInfo.InvariantCulture), KbNamespaces.XsdInteger)));
        }

        if (assertion.CharEnd is not null)
        {
            graph.Assert(new Triple(
                assertionNode,
                graph.CreateUriNode(KbNamespaces.KbCharEnd),
                graph.CreateLiteralNode(assertion.CharEnd.Value.ToString(CultureInfo.InvariantCulture), KbNamespaces.XsdInteger)));
        }
    }
}
