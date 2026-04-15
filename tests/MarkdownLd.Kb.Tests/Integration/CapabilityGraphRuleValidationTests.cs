using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class CapabilityGraphRuleValidationTests
{
    private static readonly Uri BaseUri = new("https://kb.example/");

    private const string AdvancedRulesPath = "tools/advanced-rules.md";
    private const string ConfiguredPath = "tools/configured.md";
    private const string ConfiguredTargetUri = "https://kb.example/configured/target/";
    private const string ConfiguredTargetTitle = "Configured Target";
    private const string StoryToolsGroup = "Story tools";

    [Test]
    public async Task Graph_rule_front_matter_supports_entities_edges_and_validation_diagnostics()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(AdvancedRulesPath, AdvancedRulesMarkdown),
        ]);

        result.Diagnostics.ShouldContain("Graph rule skipped: graph_entities[2] requires a node label or id.");
        result.Diagnostics.ShouldContain("Graph rule skipped: graph_groups[1] requires a node label or id.");
        result.Diagnostics.ShouldContain("Graph rule skipped: graph_edges[1] requires a supported predicate.");

        var graphRulesExist = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://kb.example/tools/advanced-rules/> kb:relatedTo <urn:capability:manual> ;
    kb:nextStep <https://kb.example/id/follow-up-step> .
  <urn:capability:manual> a schema:SoftwareApplication ;
    schema:name "Manual Capability" ;
    schema:sameAs <https://external.example/manual> .
  <https://kb.example/entities/id-only/> schema:name "https://kb.example/entities/id-only/" .
}
""");
        graphRulesExist.ShouldBeTrue();

        var focused = await result.Graph.SearchFocusedAsync("Advanced Rules Source");
        focused.NextStepMatches.Single().Label.ShouldBe("Follow Up Step");
    }

    [Test]
    public async Task Configured_rules_build_edges_when_front_matter_rules_are_disabled()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync(
            [
                new KnowledgeSourceDocument(ConfiguredPath, ConfiguredRulesMarkdown, null, "text/markdown"),
            ],
            CreateConfiguredRulesOptions());

        var configuredRuleExists = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
PREFIX kb: <urn:managedcode:markdown-ld-kb:vocab:>
ASK WHERE {
  <https://kb.example/tools/configured/> kb:relatedTo <https://kb.example/configured/target/> .
  <https://kb.example/configured/target/> a schema:SoftwareApplication ;
    schema:name "Configured Target" ;
    schema:sameAs <https://external.example/configured-target> .
}
""");
        configuredRuleExists.ShouldBeTrue();

        AssertConfiguredRuleDiagnostics(result);
        result.Graph.ToSnapshot().Nodes.Select(static node => node.Label).ShouldNotContain(StoryToolsGroup);
    }

    private static KnowledgeGraphBuildOptions CreateConfiguredRulesOptions()
    {
        return new KnowledgeGraphBuildOptions
        {
            IncludeFrontMatterRules = false,
            Entities = CreateConfiguredEntityRules(),
            Edges = CreateConfiguredEdgeRules(),
        };
    }

    private static KnowledgeGraphEntityRule[] CreateConfiguredEntityRules()
    {
        return
        [
            new KnowledgeGraphEntityRule
            {
                Label = " ",
            },
            new KnowledgeGraphEntityRule
            {
                Id = ConfiguredTargetUri,
                Label = ConfiguredTargetTitle,
                Type = "schema:SoftwareApplication",
                SameAs =
                [
                    "",
                    "https://external.example/configured-target",
                ],
            },
        ];
    }

    private static KnowledgeGraphEdgeRule[] CreateConfiguredEdgeRules()
    {
        return
        [
            new KnowledgeGraphEdgeRule
            {
                SubjectId = " ",
                Predicate = "relatedto",
                ObjectId = ConfiguredTargetUri,
            },
            new KnowledgeGraphEdgeRule
            {
                SubjectId = "https://kb.example/tools/configured/",
                Predicate = "unsupported",
                ObjectId = ConfiguredTargetUri,
            },
            new KnowledgeGraphEdgeRule
            {
                SubjectId = "https://kb.example/tools/configured/",
                Predicate = "relatedto",
                ObjectId = " ",
            },
            new KnowledgeGraphEdgeRule
            {
                SubjectId = "https://kb.example/tools/configured/",
                Predicate = "relatedto",
                ObjectId = ConfiguredTargetUri,
            },
        ];
    }

    private static void AssertConfiguredRuleDiagnostics(MarkdownKnowledgeBuildResult result)
    {
        result.Diagnostics.ShouldContain("Graph rule skipped: options.Entities[0] requires a label.");
        result.Diagnostics.ShouldContain("Graph rule skipped: options.Edges[0] requires a subject.");
        result.Diagnostics.ShouldContain("Graph rule skipped: options.Edges[1] requires a supported predicate.");
        result.Diagnostics.ShouldContain("Graph rule skipped: options.Edges[2] requires an object.");
    }

    private const string AdvancedRulesMarkdown = """
---
title: Advanced Rules Source
summary: Exercises graph rule validation and explicit front matter graph construction.
graph_entities:
  - id: urn:capability:manual
    label: Manual Capability
    type: schema:SoftwareApplication
    same_as: https://external.example/manual
  - id: https://kb.example/entities/id-only/
  - {}
graph_groups:
  - Operations Group
  - {}
graph_next_steps: Follow Up Step
graph_edges:
  - subject: article
    predicate: relatedto
    object: urn:capability:manual
  - subject: article
    predicate: unsupported
    object: urn:capability:manual
---
# Advanced Rules Source

Explicit rules should build graph facts and diagnostics through the public pipeline.
""";

    private const string ConfiguredRulesMarkdown = """
---
title: Configured Rules Source
graph_groups:
  - Story tools
---
# Configured Rules Source

Front matter rules should be ignored for this build.
""";
}
