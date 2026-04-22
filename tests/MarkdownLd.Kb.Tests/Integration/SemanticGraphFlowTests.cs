using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class SemanticGraphFlowTests
{
    private static readonly Uri BaseUri = new("https://kb.example/");

    private const string SemanticPath = "docs/semantic-graph.md";
    private const string SemanticDocumentUri = "https://kb.example/docs/semantic-graph/";
    private const string KnowledgeGraphConceptUri = "https://kb.example/id/knowledge-graph";
    private const string SemanticOperationsConceptUri = "https://kb.example/id/semantic-operations";
    private const string FederatedQueriesConceptUri = "https://kb.example/id/federated-queries";
    private const string ConceptSchemeUri = "https://kb.example/id/markdown-ld-knowledge-bank-concepts";

    private const string SemanticMarkdown = """
---
title: Semantic Graph Spec
about:
  - Knowledge Graph
graph_groups:
  - Semantic Operations
graph_entities:
  - label: Federated Queries
    type: schema:DefinedTerm
    same_as: https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries
---
# Semantic Graph Spec

The document describes ontology-backed and SKOS-backed graph construction.
""";

    private const string SemanticAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
ASK WHERE {
  <https://kb.example/docs/semantic-graph/> a kb:MarkdownDocument .

  <https://kb.example/id/knowledge-graph> a skos:Concept ;
    skos:inScheme <https://kb.example/id/markdown-ld-knowledge-bank-concepts> ;
    skos:prefLabel "Knowledge Graph" ;
    rdfs:label "Knowledge Graph" .

  <https://kb.example/id/semantic-operations> a schema:DefinedTerm ;
    a skos:Concept ;
    a kb:KnowledgeConcept ;
    skos:prefLabel "Semantic Operations" .

  <https://kb.example/id/federated-queries> a schema:DefinedTerm ;
    a skos:Concept ;
    skos:exactMatch <https://www.wikidata.org/wiki/Wikidata:SPARQL_query_service/Federated_queries> .

  <https://kb.example/id/markdown-ld-knowledge-bank-concepts> a skos:ConceptScheme ;
    a kb:KnowledgeConceptScheme ;
    skos:prefLabel "Markdown-LD Knowledge Bank Concepts" .

  kb:MarkdownDocument rdfs:subClassOf schema:Article .
  kb:KnowledgeConcept rdfs:subClassOf schema:DefinedTerm .
  kb:KnowledgeConcept rdfs:subClassOf skos:Concept .
  kb:memberOf rdfs:domain schema:Thing ;
    rdfs:range kb:KnowledgeConcept .

  ?statement a kb:KnowledgeAssertion ;
    rdf:subject <https://kb.example/docs/semantic-graph/> ;
    rdf:predicate kb:memberOf ;
    rdf:object <https://kb.example/id/semantic-operations> .
}
""";

    [Test]
    public async Task Pipeline_builds_additive_ontology_and_skos_layers_from_markdown_graph_rules()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(SemanticPath, SemanticMarkdown),
        ]);

        result.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(SemanticDocumentUri);

        var ask = await result.Graph.ExecuteAskAsync(SemanticAskQuery);

        ask.ShouldBeTrue();

        var turtle = result.Graph.SerializeTurtle();
        turtle.ShouldContain("skos:ConceptScheme");
        turtle.ShouldContain("kb:KnowledgeConcept");
        turtle.ShouldContain("Knowledge Graph");
    }
}
