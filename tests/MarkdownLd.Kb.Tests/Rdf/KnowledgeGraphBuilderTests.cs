using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public sealed class KnowledgeGraphBuilderTests
{
    [Fact]
    public void BuildRegistersNamespacesAndCoreTriples()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var article = TestKnowledgeGraphFactory.CreateDocument().Article.Id;

        Assert.Equal(KbNamespaces.SchemaUri, graph.NamespaceMap.GetNamespaceUri(KbNamespaces.SchemaPrefix));
        Assert.Equal(KbNamespaces.ProvUri, graph.NamespaceMap.GetNamespaceUri(KbNamespaces.ProvPrefix));
        Assert.Equal(KbNamespaces.RdfUri, graph.NamespaceMap.GetNamespaceUri(KbNamespaces.RdfPrefix));
        Assert.Equal(KbNamespaces.XsdUri, graph.NamespaceMap.GetNamespaceUri(KbNamespaces.XsdPrefix));
        Assert.Equal(KbNamespaces.KbUri, graph.NamespaceMap.GetNamespaceUri(KbNamespaces.KbPrefix));

        var executor = new SparqlQueryExecutor(graph);

        var titleResult = executor.ExecuteRawReadOnly($"""
PREFIX schema: <{KbNamespaces.Schema}>
SELECT ?title WHERE {{
  <{article.AbsoluteUri}> schema:name ?title
}}
""");

        Assert.Single(titleResult.Results);
        Assert.Equal("What is a Knowledge Graph?", titleResult.Results[0]["title"].Value);

        var mentionsResult = executor.ExecuteRawReadOnly($"""
PREFIX schema: <{KbNamespaces.Schema}>
SELECT ?mention WHERE {{
  <{article.AbsoluteUri}> schema:mentions ?mention
}}
ORDER BY ?mention
""");

        Assert.Equal(2, mentionsResult.Results.Count);
        Assert.Contains(mentionsResult.Results, row => row["mention"].Value.EndsWith("/rdf", StringComparison.Ordinal));
        Assert.Contains(mentionsResult.Results, row => row["mention"].Value.EndsWith("/sparql", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildAddsAssertionProvenanceAndTypedValues()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var document = TestKnowledgeGraphFactory.CreateDocument();
        var article = document.Article.Id;
        var rdfEntity = document.Entities[0].Id;

        var executor = new SparqlQueryExecutor(graph);
        var ask = executor.ExecuteRawReadOnly($"""
PREFIX schema: <{KbNamespaces.Schema}>
PREFIX kb: <{KbNamespaces.Kb}>
PREFIX prov: <{KbNamespaces.Prov}>
PREFIX xsd: <{KbNamespaces.Xsd}>
ASK WHERE {{
  ?statement a kb:Assertion ;
             schema:subjectOf <{article.AbsoluteUri}> ;
             schema:mentions <{rdfEntity.AbsoluteUri}> ;
             kb:confidence "0.91"^^xsd:decimal ;
             prov:wasDerivedFrom <urn:kb:source:article-1> ;
             kb:chunk <urn:kb:chunk:article-1> ;
             kb:docPath "content/2026/04/what-is-a-knowledge-graph.md" ;
             kb:charStart "15"^^xsd:integer ;
             kb:charEnd "42"^^xsd:integer .
}}
""");

        Assert.True(ask.Result);
    }
}
