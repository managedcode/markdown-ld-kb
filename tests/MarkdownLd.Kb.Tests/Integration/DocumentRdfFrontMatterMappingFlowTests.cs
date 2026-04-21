using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class DocumentRdfFrontMatterMappingFlowTests
{
    private const string BaseUriText = "https://rdf-metadata.example/";
    private const string GlobalPrefixUri = "http://www.w3.org/2004/02/skos/core#";

    private const string FrontMatterDocumentPath = "content/flexible-graph-spec.md";
    private const string FrontMatterDocumentUri = "https://rdf-metadata.example/flexible-graph-spec/";
    private const string FrontMatterMarkdown = """
---
title: Flexible Graph Spec
rdf_prefixes:
  dcterms: http://purl.org/dc/terms/
  skos: http://www.w3.org/2004/02/skos/core#
rdf_types:
  - schema:HowTo
  - skos:ConceptScheme
rdf_properties:
  schema:isPartOf:
    id: https://rdf-metadata.example/projects/ai-memex
  dcterms:subject:
    - value: knowledge graph
    - value: rdf
  dcterms:issued:
    value: 2026-04-21
    datatype: xsd:date
  skos:prefLabel: Flexible Graph Spec
---
# Flexible Graph Spec

Body content.
""";

    private const string FrontMatterAskQuery = """
PREFIX schema: <https://schema.org/>
PREFIX dcterms: <http://purl.org/dc/terms/>
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
ASK WHERE {
  <https://rdf-metadata.example/flexible-graph-spec/> a schema:Article ;
                                                      a schema:HowTo ;
                                                      a skos:ConceptScheme ;
                                                      schema:isPartOf <https://rdf-metadata.example/projects/ai-memex> ;
                                                      dcterms:subject "knowledge graph" ;
                                                      dcterms:subject "rdf" ;
                                                      dcterms:issued "2026-04-21"^^xsd:date ;
                                                      skos:prefLabel "Flexible Graph Spec" .
}
""";

    private const string GlobalPrefixDocumentPath = "content/global-prefix-dataset.md";
    private const string GlobalPrefixDocumentUri = "https://rdf-metadata.example/global-prefix-dataset/";
    private const string GlobalPrefixMarkdown = """
---
title: Global Prefix Dataset
rdf_types:
  - skos:Concept
rdf_properties:
  skos:prefLabel: Global Prefix Dataset
---
# Global Prefix Dataset

Body content.
""";

    private const string GlobalPrefixAskQuery = """
PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
ASK WHERE {
  <https://rdf-metadata.example/global-prefix-dataset/> a skos:Concept ;
                                                        skos:prefLabel "Global Prefix Dataset" .
}
""";

    private const string InvalidPrefixDocumentPath = "content/invalid-prefix.md";
    private const string InvalidPrefixMarkdown = """
---
title: Invalid Prefix
rdf_types:
  - unknown:Thing
---
# Invalid Prefix

Body content.
""";

    [Test]
    public async Task Pipeline_materializes_generic_rdf_front_matter_types_properties_and_prefixes()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(FrontMatterDocumentPath, FrontMatterMarkdown),
        ]);

        result.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(FrontMatterDocumentUri);

        var ask = await result.Graph.ExecuteAskAsync(FrontMatterAskQuery);
        ask.ShouldBeTrue();
    }

    [Test]
    public async Task Pipeline_uses_configured_global_rdf_prefixes_when_front_matter_omits_them()
    {
        var pipeline = new MarkdownKnowledgePipeline(new MarkdownKnowledgePipelineOptions
        {
            BaseUri = new Uri(BaseUriText),
            DocumentRdfMapping = new DocumentRdfMappingOptions
            {
                Prefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["skos"] = GlobalPrefixUri,
                },
            },
        });

        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(GlobalPrefixDocumentPath, GlobalPrefixMarkdown),
        ]);

        result.Documents.Single().DocumentUri.AbsoluteUri.ShouldBe(GlobalPrefixDocumentUri);

        var ask = await result.Graph.ExecuteAskAsync(GlobalPrefixAskQuery);
        ask.ShouldBeTrue();
    }

    [Test]
    public async Task Pipeline_fails_explicitly_when_rdf_type_uses_unknown_prefix()
    {
        var pipeline = new MarkdownKnowledgePipeline(new Uri(BaseUriText));

        await Should.ThrowAsync<InvalidDataException>(async () =>
            await pipeline.BuildAsync([
                new MarkdownSourceDocument(InvalidPrefixDocumentPath, InvalidPrefixMarkdown),
            ]));
    }
}
