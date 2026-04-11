using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using Shouldly;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Rdf;

public sealed class KnowledgeGraphBuilderTests
{
    private const string TitleQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?title WHERE {
  <https://example.com/articles/what-is-a-knowledge-graph/> schema:name ?title
}
""";
    private const string MentionsQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?mention WHERE {
  <https://example.com/articles/what-is-a-knowledge-graph/> schema:mentions ?mention
}
ORDER BY ?mention
""";
    private const string AssertionQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <https://managedcode.dev/ns/markdown-ld-kb#>
PREFIX prov: <https://www.w3.org/ns/prov#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
ASK WHERE {
  ?statement a kb:Assertion ;
             schema:subjectOf <https://example.com/articles/what-is-a-knowledge-graph/> ;
             schema:mentions <https://example.com/id/rdf> ;
             kb:confidence "0.91"^^xsd:decimal ;
             prov:wasDerivedFrom <urn:kb:source:article-1> ;
             kb:chunk <urn:kb:chunk:article-1> ;
             kb:docPath "content/2026/04/what-is-a-knowledge-graph.md" ;
             kb:charStart "15"^^xsd:integer ;
             kb:charEnd "42"^^xsd:integer .
}
""";
    private const string TitleValue = "What is a Knowledge Graph?";
    private const string RdfSuffix = "/rdf";
    private const string SparqlSuffix = "/sparql";
    private const string TitleVariable = "title";
    private const string MentionVariable = "mention";

    [Test]
    public void BuildRegistersNamespacesAndCoreTriples()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();

        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.SchemaPrefix).ShouldBe(KbNamespaces.SchemaUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.ProvPrefix).ShouldBe(KbNamespaces.ProvUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.RdfPrefix).ShouldBe(KbNamespaces.RdfUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.XsdPrefix).ShouldBe(KbNamespaces.XsdUri);
        graph.NamespaceMap.GetNamespaceUri(KbNamespaces.KbPrefix).ShouldBe(KbNamespaces.KbUri);

        var executor = new SparqlQueryExecutor(graph);

        var titleResult = executor.ExecuteRawReadOnly(TitleQuery);

        titleResult.Results.Count.ShouldBe(1);
        ((ILiteralNode)titleResult.Results[0][TitleVariable]).Value.ShouldBe(TitleValue);

        var mentionsResult = executor.ExecuteRawReadOnly(MentionsQuery);

        mentionsResult.Results.Count.ShouldBe(2);
        mentionsResult.Results.Any(row => ((IUriNode)row[MentionVariable]).Uri.AbsoluteUri.EndsWith(RdfSuffix, StringComparison.Ordinal)).ShouldBeTrue();
        mentionsResult.Results.Any(row => ((IUriNode)row[MentionVariable]).Uri.AbsoluteUri.EndsWith(SparqlSuffix, StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Test]
    public void BuildAddsAssertionProvenanceAndTypedValues()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();

        var executor = new SparqlQueryExecutor(graph);
        var ask = executor.ExecuteRawReadOnly(AssertionQuery);

        ask.Result.ShouldBeTrue();
    }
}
