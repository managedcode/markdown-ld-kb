using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class GraphShaclValidationFlowTests
{
    private static readonly Uri BaseUri = new("https://kb.example/");

    private const string SourcePath = "tools/shacl-source.md";
    private const string SourceUri = "https://kb.example/tools/shacl-source/";
    private const string TargetUri = "https://kb.example/tools/shacl-target/";
    private const string DuplicateEntityUri = "https://kb.example/entities/dotnetrdf-duplicate/";
    private const string CanonicalEntityUri = "https://kb.example/entities/dotnet-rdf/";
    private const string ExternalSameAsUri = "https://dotnetrdf.org/";
    private const string RdfQueryingUri = "https://kb.example/entities/rdf-querying/";

    [Test]
    public async Task Default_SHACL_shapes_conform_for_valid_capability_graph()
    {
        var result = await BuildValidGraphAsync();

        var report = result.ValidateShacl();

        report.Conforms.ShouldBeTrue();
        report.Results.ShouldBeEmpty();
        report.ReportTurtle.ShouldContain("ValidationReport");
    }

    [Test]
    public async Task Default_SHACL_shapes_report_invalid_assertion_confidence_and_sameAs()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync(
            [
                new MarkdownSourceDocument(SourcePath, ValidMarkdown),
            ],
            new KnowledgeGraphBuildOptions
            {
                IncludeAssertionReification = true,
                Entities =
                [
                    new KnowledgeGraphEntityRule
                    {
                        Id = TargetUri,
                        Label = "Target Tool",
                        Type = "schema:SoftwareApplication",
                        SameAs = ["not a uri"],
                    },
                ],
                Edges =
                [
                    new KnowledgeGraphEdgeRule
                    {
                        SubjectId = SourceUri,
                        Predicate = "relatedto",
                        ObjectId = TargetUri,
                        Confidence = 1.25d,
                        Source = "not a provenance uri",
                    },
                ],
            });

        var report = result.Graph.ValidateShacl();

        report.Conforms.ShouldBeFalse();
        report.Results.Select(static issue => issue.Message).ShouldContain("schema:sameAs values must be IRIs.");
        report.Results.Select(static issue => issue.Message).ShouldContain("kb:confidence must be a decimal from 0 through 1.");
        report.Results.Select(static issue => issue.Message).ShouldContain("prov:wasDerivedFrom values must be IRIs.");

        var assertionMetadataExists = await result.Graph.ExecuteAskAsync("""
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
ASK WHERE {
  ?assertion a rdf:Statement ;
    rdf:subject <https://kb.example/tools/shacl-source/> ;
    rdf:predicate kb:relatedTo ;
    rdf:object <https://kb.example/tools/shacl-target/> ;
    kb:confidence "1.25"^^xsd:decimal .
}
""");
        assertionMetadataExists.ShouldBeTrue();
    }

    [Test]
    public async Task Caller_supplied_SHACL_shapes_validate_the_built_graph()
    {
        var result = await BuildValidGraphAsync();

        var report = result.ValidateShacl(DatePublishedRequiredShapes);

        report.Conforms.ShouldBeFalse();
        report.Results.Single().Message.ShouldBe("Every Article must have a schema:datePublished.");
        report.Results.Single().FocusNode.ShouldBe(SourceUri);
    }

    [Test]
    public async Task SameAs_first_merge_rewrites_assertions_before_SHACL_validation()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync(
            [
                new MarkdownSourceDocument(SourcePath, ValidMarkdown),
            ],
            new KnowledgeGraphBuildOptions
            {
                IncludeAssertionReification = true,
                IncludeFrontMatterRules = false,
                Entities =
                [
                    new KnowledgeGraphEntityRule
                    {
                        Id = CanonicalEntityUri,
                        Label = ".NET RDF",
                        Type = "schema:Thing",
                        SameAs = [ExternalSameAsUri],
                    },
                    new KnowledgeGraphEntityRule
                    {
                        Id = DuplicateEntityUri,
                        Label = "dotNetRDF",
                        Type = "schema:SoftwareApplication",
                        SameAs = [ExternalSameAsUri],
                    },
                    new KnowledgeGraphEntityRule
                    {
                        Id = ExternalSameAsUri,
                        Label = "RDF",
                        Type = "schema:Thing",
                    },
                    new KnowledgeGraphEntityRule
                    {
                        Id = RdfQueryingUri,
                        Label = "RDF Querying",
                        Type = "schema:DefinedTerm",
                    },
                ],
                Edges =
                [
                    new KnowledgeGraphEdgeRule
                    {
                        SubjectId = DuplicateEntityUri,
                        Predicate = "relatedto",
                        ObjectId = RdfQueryingUri,
                    },
                ],
            });

        var merged = result.Facts.Entities.Single(entity => entity.SameAs.Contains(ExternalSameAsUri));
        merged.Id.ShouldBe(CanonicalEntityUri);
        merged.Type.ShouldBe("schema:SoftwareApplication");
        result.Facts.Entities.Select(static entity => entity.Id).ShouldNotContain(DuplicateEntityUri);
        result.Facts.Entities.Select(static entity => entity.Id).ShouldNotContain(ExternalSameAsUri);
        result.Facts.Assertions.Single().SubjectId.ShouldBe(CanonicalEntityUri);

        var canonicalGraphExists = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://kb.example/entities/dotnet-rdf/> a schema:SoftwareApplication ;
    schema:name "dotNetRDF" ;
    schema:sameAs <https://dotnetrdf.org/> ;
    kb:relatedTo <https://kb.example/entities/rdf-querying/> .
  FILTER NOT EXISTS {
    <https://kb.example/entities/dotnetrdf-duplicate/> kb:relatedTo <https://kb.example/entities/rdf-querying/> .
  }
}
""");
        canonicalGraphExists.ShouldBeTrue();
        result.ValidateShacl().Conforms.ShouldBeTrue();
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildValidGraphAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        return pipeline.BuildAsync(
            [
                new MarkdownSourceDocument(SourcePath, ValidMarkdown),
            ],
            new KnowledgeGraphBuildOptions
            {
                IncludeAssertionReification = true,
                Edges =
                [
                    new KnowledgeGraphEdgeRule
                    {
                        SubjectId = SourceUri,
                        Predicate = "relatedto",
                        ObjectId = TargetUri,
                        Source = SourceUri,
                    },
                ],
                Entities =
                [
                    new KnowledgeGraphEntityRule
                    {
                        Id = TargetUri,
                        Label = "Target Tool",
                        Type = "schema:SoftwareApplication",
                        SameAs = ["https://external.example/target-tool"],
                        Source = SourceUri,
                    },
                ],
            });
    }

    private const string ValidMarkdown = """
---
title: SHACL Source Tool
summary: Tool used to prove SHACL validation over the built graph.
---
# SHACL Source Tool

This document builds a graph that can be validated through SHACL.
""";

    private const string DatePublishedRequiredShapes = """
@prefix sh: <http://www.w3.org/ns/shacl#> .
@prefix schema: <https://schema.org/> .
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .

<urn:test:shape:ArticleDatePublishedShape> a sh:NodeShape ;
  sh:targetClass schema:Article ;
  sh:property [
    sh:path schema:datePublished ;
    sh:minCount 1 ;
    sh:datatype xsd:string ;
    sh:message "Every Article must have a schema:datePublished." ;
  ] .
""";
}
