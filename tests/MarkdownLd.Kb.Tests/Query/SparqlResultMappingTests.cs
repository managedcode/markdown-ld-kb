using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Rdf;
using ManagedCode.MarkdownLd.Kb.Tests.Rdf;
using Shouldly;
using VDS.RDF;

namespace ManagedCode.MarkdownLd.Kb.Tests.Query;

public sealed class SparqlResultMappingTests
{
    private const string SchemaPrefixQuery = """
PREFIX schema: <https://schema.org/>
SELECT ?entity ?label WHERE {
  ?entity schema:name ?label .
  FILTER(?entity = <https://example.com/id/rdf>)
}
""";
    private const string AskQuery = """
ASK WHERE { <https://example.com/articles/what-is-a-knowledge-graph/> ?p ?o }
""";
    private const string EntityVariable = "entity";
    private const string LabelVariable = "label";
    private const string RdfEntityUri = "https://example.com/id/rdf";
    private const string RdfLabel = "RDF";
    private const string UriNodeKind = "uri";
    private const string LiteralNodeKind = "literal";
    private const string LiteralValue = "42";
    private const string BlankNodeKind = "blank";

    [Test]
    public void MapsSelectResultSetToRows()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);
        var resultSet = executor.ExecuteRawReadOnly(SchemaPrefixQuery);

        var mapped = SparqlResultMapper.Map(resultSet);

        mapped.ResultType.ShouldBe(VDS.RDF.Query.SparqlResultsType.VariableBindings);
        mapped.BooleanValue.ShouldBeNull();
        mapped.Variables.Count.ShouldBe(2);
        mapped.Variables[0].ShouldBe(EntityVariable);
        mapped.Variables[1].ShouldBe(LabelVariable);
        mapped.Rows.Count.ShouldBe(1);

        var row = mapped.Rows[0];
        row.Bindings[EntityVariable].NodeKind.ShouldBe(UriNodeKind);
        row.Bindings[EntityVariable].Value.ShouldBe(RdfEntityUri);
        row.Bindings[LabelVariable].NodeKind.ShouldBe(LiteralNodeKind);
        row.Bindings[LabelVariable].Value.ShouldBe(RdfLabel);
    }

    [Test]
    public void MapsAskResultSetToBoolean()
    {
        var graph = TestKnowledgeGraphFactory.BuildGraph();
        var executor = new SparqlQueryExecutor(graph);
        var resultSet = executor.ExecuteRawReadOnly(AskQuery);

        var mapped = SparqlResultMapper.Map(resultSet);

        mapped.ResultType.ShouldBe(VDS.RDF.Query.SparqlResultsType.Boolean);
        mapped.BooleanValue.ShouldBe(true);
        mapped.Rows.ShouldBeEmpty();
    }

    [Test]
    public void MapsLiteralAndBlankNodes()
    {
        var graph = new Graph();
        var literal = graph.CreateLiteralNode(LiteralValue, KbNamespaces.XsdInteger);
        var blank = graph.CreateBlankNode();

        var literalMapped = SparqlResultMapper.MapNode(literal);
        var blankMapped = SparqlResultMapper.MapNode(blank);

        literalMapped.NodeKind.ShouldBe(LiteralNodeKind);
        literalMapped.Value.ShouldBe(LiteralValue);
        literalMapped.Datatype.ShouldBe(KbNamespaces.XsdInteger.AbsoluteUri);

        blankMapped.NodeKind.ShouldBe(BlankNodeKind);
        blankMapped.Value.ShouldNotBeNullOrWhiteSpace();
    }
}
