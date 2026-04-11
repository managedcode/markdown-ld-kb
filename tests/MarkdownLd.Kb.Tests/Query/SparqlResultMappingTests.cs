using Shouldly;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using ManagedCode.MarkdownLd.Kb.Tests.Rdf;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class SparqlResultMappingTests
{
    [Test]
    public void MapsSelectResultSetToRows()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);
        var resultSet = executor.ExecuteRawReadOnly($$"""
PREFIX schema: <{{KbNamespaces.Schema}}>
SELECT ?entity ?label WHERE {
  ?entity schema:name ?label .
  FILTER(?entity = <https://example.com/id/rdf>)
}
""");

        var mapped = SparqlResultMapper.Map(resultSet);

        mapped.ResultType.ShouldBe(VDS.RDF.Query.SparqlResultsType.VariableBindings);
        mapped.BooleanValue.ShouldBeNull();
        mapped.Variables.Count.ShouldBe(2);
        mapped.Variables[0].ShouldBe("entity");
        mapped.Variables[1].ShouldBe("label");
        mapped.Rows.Count.ShouldBe(1);

        var row = mapped.Rows[0];
        row.Bindings["entity"].NodeKind.ShouldBe("uri");
        row.Bindings["entity"].Value.ShouldBe("https://example.com/id/rdf");
        row.Bindings["label"].NodeKind.ShouldBe("literal");
        row.Bindings["label"].Value.ShouldBe("RDF");
    }

    [Test]
    public void MapsAskResultSetToBoolean()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);
        var resultSet = executor.ExecuteRawReadOnly("""
ASK WHERE { <https://example.com/articles/what-is-a-knowledge-graph/> ?p ?o }
""");

        var mapped = SparqlResultMapper.Map(resultSet);

        mapped.ResultType.ShouldBe(VDS.RDF.Query.SparqlResultsType.Boolean);
        mapped.BooleanValue.ShouldBe(true);
        mapped.Rows.ShouldBeEmpty();
    }

    [Test]
    public void MapsLiteralAndBlankNodes()
    {
        var graph = new Graph();
        var literal = graph.CreateLiteralNode("42", KbNamespaces.XsdInteger);
        var blank = graph.CreateBlankNode();

        var literalMapped = SparqlResultMapper.MapNode(literal);
        var blankMapped = SparqlResultMapper.MapNode(blank);

        literalMapped.NodeKind.ShouldBe("literal");
        literalMapped.Value.ShouldBe("42");
        literalMapped.Datatype.ShouldBe(KbNamespaces.XsdInteger.AbsoluteUri);

        blankMapped.NodeKind.ShouldBe("blank");
        blankMapped.Value.ShouldNotBeNullOrWhiteSpace();
    }
}
