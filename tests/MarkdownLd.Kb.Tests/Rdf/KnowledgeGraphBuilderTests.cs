using Shouldly;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public sealed class KnowledgeGraphBuilderTests
{
    [Test]
    public void BuildRegistersNamespacesAndCoreTriples()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var article = TestKnowledgeGraphFactory.CreateDocument().Article.Id;

        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.SchemaPrefix).ShouldBe(KbNamespaces.SchemaUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.ProvPrefix).ShouldBe(KbNamespaces.ProvUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.RdfPrefix).ShouldBe(KbNamespaces.RdfUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.XsdPrefix).ShouldBe(KbNamespaces.XsdUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.KbPrefix).ShouldBe(KbNamespaces.KbUri);

        var executor = new SparqlQueryExecutor(graph);

        var titleResult = executor.ExecuteRawReadOnly($$"""
PREFIX schema: <{{KbNamespaces.Schema}}>
SELECT ?title WHERE {
  <{{article.AbsoluteUri}}> schema:name ?title
}
""");

        titleResult.Results.Count.ShouldBe(1);
        ((ILiteralNode)titleResult.Results[0]["title"]).Value.ShouldBe("What is a Knowledge Graph?");

        var mentionsResult = executor.ExecuteRawReadOnly($$"""
PREFIX schema: <{{KbNamespaces.Schema}}>
SELECT ?mention WHERE {
  <{{article.AbsoluteUri}}> schema:mentions ?mention
}
ORDER BY ?mention
""");

        mentionsResult.Results.Count.ShouldBe(2);
        mentionsResult.Results.Any(row => ((IUriNode)row["mention"]).Uri.AbsoluteUri.EndsWith("/rdf", StringComparison.Ordinal)).ShouldBeTrue();
        mentionsResult.Results.Any(row => ((IUriNode)row["mention"]).Uri.AbsoluteUri.EndsWith("/sparql", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Test]
    public void BuildAddsAssertionProvenanceAndTypedValues()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var document = TestKnowledgeGraphFactory.CreateDocument();
        var article = document.Article.Id;
        var rdfEntity = document.Entities[0].Id;

        var executor = new SparqlQueryExecutor(graph);
        var ask = executor.ExecuteRawReadOnly($$"""
PREFIX schema: <{{KbNamespaces.Schema}}>
PREFIX kb: <{{KbNamespaces.Kb}}>
PREFIX prov: <{{KbNamespaces.Prov}}>
PREFIX xsd: <{{KbNamespaces.Xsd}}>
ASK WHERE {
  ?statement a kb:Assertion ;
             schema:subjectOf <{{article.AbsoluteUri}}> ;
             schema:mentions <{{rdfEntity.AbsoluteUri}}> ;
             kb:confidence "0.91"^^xsd:decimal ;
             prov:wasDerivedFrom <urn:kb:source:article-1> ;
             kb:chunk <urn:kb:chunk:article-1> ;
             kb:docPath "content/2026/04/what-is-a-knowledge-graph.md" ;
             kb:charStart "15"^^xsd:integer ;
             kb:charEnd "42"^^xsd:integer .
}
""");

        ask.Result.ShouldBeTrue();
    }
}
