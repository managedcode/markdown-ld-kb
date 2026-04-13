using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class GraphExportFlowTests
{
    private const string BaseUrl = "https://kb.example/";
    private const string ExportPath = "content/export.md";
    private const string GraphExportTitle = "Graph Export";
    private const string RdfLabel = "RDF";
    private const string GraphExportDocumentId = "https://kb.example/export/";
    private const string LiteralGraphExportId = "literal:Graph Export";
    private const string SchemaMentionsPredicateId = "https://schema.org/mentions";
    private const string SchemaMentionsPredicateLabel = "schema:mentions";
    private const string TokenSegmentIdPrefix = "https://kb.example/token-segment/";
    private const string MermaidHeader = "graph LR";
    private const string MermaidMentionEdge = "|\"schema:mentions\"|";
    private const string DotHeader = "digraph KnowledgeGraph";
    private const string DotMentionEdge = "[label=\"schema:mentions\"]";

    private static readonly Uri BaseUri = new(BaseUrl);

    private const string ExportMarkdown = """
---
title: Graph Export
tags:
  - graph
entity_hints:
  - label: RDF
    type: schema:Thing
    sameAs:
      - https://www.w3.org/RDF/
---
# Graph Export

Graph Export links [[RDF]] and [SPARQL](https://www.w3.org/TR/sparql11-query/).
Graph Export --mentions--> RDF
RDF --sameas--> https://www.w3.org/RDF/
""";

    [Test]
    public async Task Markdown_graph_export_flow_returns_snapshot_mermaid_and_dot()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(ExportPath, ExportMarkdown),
        ]);

        var snapshot = result.Graph.ToSnapshot();

        snapshot.Nodes.ShouldContain(node =>
            node.Id == GraphExportDocumentId &&
            node.Kind == KnowledgeGraphNodeKind.Uri);
        var rdfSegment = snapshot.Nodes.First(node =>
            node.Id.StartsWith(TokenSegmentIdPrefix, StringComparison.Ordinal) &&
            node.Label.Contains(RdfLabel, StringComparison.Ordinal) &&
            node.Kind == KnowledgeGraphNodeKind.Uri);
        snapshot.Nodes.ShouldContain(node =>
            node.Id == LiteralGraphExportId &&
            node.Label == GraphExportTitle &&
            node.Kind == KnowledgeGraphNodeKind.Literal);
        snapshot.Edges.ShouldContain(edge =>
            edge.SubjectId == GraphExportDocumentId &&
            edge.PredicateId == SchemaMentionsPredicateId &&
            edge.PredicateLabel == SchemaMentionsPredicateLabel &&
            edge.ObjectId == rdfSegment.Id);

        var mermaid = result.Graph.SerializeMermaidFlowchart();
        mermaid.ShouldContain(MermaidHeader);
        mermaid.ShouldContain(GraphExportTitle);
        mermaid.ShouldContain(RdfLabel);
        mermaid.ShouldContain(MermaidMentionEdge);

        var dot = result.Graph.SerializeDotGraph();
        dot.ShouldContain(DotHeader);
        dot.ShouldContain(GraphExportTitle);
        dot.ShouldContain(RdfLabel);
        dot.ShouldContain(DotMentionEdge);
    }
}
