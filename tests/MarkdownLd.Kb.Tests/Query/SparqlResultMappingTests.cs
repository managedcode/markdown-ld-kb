using ManagedCode.MarkdownLd.Kb.Rdf;
using ManagedCode.MarkdownLd.Kb.Query;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class SparqlResultMappingTests
{
    [Fact]
    public void MapsSelectResultSetToRows()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);
        var resultSet = executor.ExecuteRawReadOnly($"""
PREFIX schema: <{KbNamespaces.Schema}>
SELECT ?entity ?label WHERE {{
  ?entity schema:name ?label .
  FILTER(?entity = <https://example.com/id/rdf>)
}}
""");

        var mapped = SparqlResultMapper.Map(resultSet);

        Assert.Equal(VDS.RDF.Query.SparqlResultsType.VariableBindings, mapped.ResultType);
        Assert.Null(mapped.BooleanValue);
        Assert.Equal(["entity", "label"], mapped.Variables);
        Assert.Single(mapped.Rows);

        var row = mapped.Rows[0];
        Assert.Equal("uri", row.Bindings["entity"].NodeKind);
        Assert.Equal("https://example.com/id/rdf", row.Bindings["entity"].Value);
        Assert.Equal("literal", row.Bindings["label"].NodeKind);
        Assert.Equal("RDF", row.Bindings["label"].Value);
    }

    [Fact]
    public void MapsAskResultSetToBoolean()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);
        var resultSet = executor.ExecuteRawReadOnly("""
ASK WHERE { <https://example.com/articles/what-is-a-knowledge-graph/> ?p ?o }
""");

        var mapped = SparqlResultMapper.Map(resultSet);

        Assert.Equal(VDS.RDF.Query.SparqlResultsType.Boolean, mapped.ResultType);
        Assert.True(mapped.BooleanValue);
        Assert.Empty(mapped.Rows);
    }

    [Fact]
    public void MapsLiteralAndBlankNodes()
    {
        var graph = new Graph();
        var literal = graph.CreateLiteralNode("42", KbNamespaces.XsdInteger);
        var blank = graph.CreateBlankNode();

        var literalMapped = SparqlResultMapper.MapNode(literal);
        var blankMapped = SparqlResultMapper.MapNode(blank);

        Assert.Equal("literal", literalMapped.NodeKind);
        Assert.Equal("42", literalMapped.Value);
        Assert.Equal(KbNamespaces.XsdInteger.AbsoluteUri, literalMapped.Datatype);

        Assert.Equal("blank", blankMapped.NodeKind);
        Assert.False(string.IsNullOrWhiteSpace(blankMapped.Value));
    }
}
