using ManagedCode.MarkdownLd.Kb.Pipeline;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class CapabilityGraphFlowTests
{
    private static readonly Uri BaseUri = new("https://kb.example/");

    private const string StoryDeletePath = "tools/story-delete.md";
    private const string StoryFeedPath = "tools/story-feed-detail.md";
    private const string StoryCommentsPath = "tools/story-comments.md";
    private const string PeopleSearchPath = "tools/people-search.md";
    private const string PrimaryBoundedPath = "tools/primary-bounded.md";
    private const string AlphaRelatedPath = "tools/alpha-related.md";
    private const string BetaRelatedPath = "tools/beta-related.md";
    private const string AlphaNextPath = "tools/alpha-next.md";
    private const string BetaNextPath = "tools/beta-next.md";
    private const string RuleTargetPath = "tools/rule-target.md";

    private const string StoryDeleteUri = "https://kb.example/tools/story-delete/";
    private const string StoryFeedUri = "https://kb.example/tools/story-feed-detail/";
    private const string StoryCommentsUri = "https://kb.example/tools/story-comments/";
    private const string PeopleSearchUri = "https://kb.example/tools/people-search/";
    private const string PrimaryBoundedUri = "https://kb.example/tools/primary-bounded/";
    private const string AlphaRelatedUri = "https://kb.example/tools/alpha-related/";
    private const string BetaRelatedUri = "https://kb.example/tools/beta-related/";
    private const string AlphaNextUri = "https://kb.example/tools/alpha-next/";
    private const string BetaNextUri = "https://kb.example/tools/beta-next/";
    private const string ReviewTargetUri = "https://kb.example/tools/review-target/";
    private const string EscalationTargetUri = "https://kb.example/id/escalation-step";
    private const string ExternalReviewTargetUri = "https://external.example/review-target";

    private const string StoryToolsGroup = "Story tools";
    private const string DeleteOperationGroup = "Delete operation";
    private const string RemoveStoryQuery = "remove the selected story from the feed";
    private const string PrimaryBoundedTitle = "Primary Delete Bounded Tool";
    private const string AlphaRelatedTitle = "Alpha Related Tool";
    private const string BetaRelatedTitle = "Beta Related Tool";
    private const string AlphaNextTitle = "Alpha Next Step Tool";
    private const string BetaNextTitle = "Beta Next Step Tool";
    private const string ReviewTargetTitle = "Review Target Tool";
    private const string EscalationTargetTitle = "Escalation Step";
    private const string MalformedRulePath = "tools/malformed-rules.md";

    private const string StoryDeleteMarkdown = """
---
title: Story Delete Tool
summary: Delete a story after the caller identifies the exact story item.
graph_groups:
  - Story tools
  - Delete operation
graph_related:
  - https://kb.example/tools/story-feed-detail/
graph_next_steps:
  - https://kb.example/tools/story-comments/
---
# Story Delete Tool

Use this capability to remove or delete an existing story. The story feed detail tool usually finds the selected story first.
""";

    private const string StoryFeedMarkdown = """
---
title: Story Feed Detail Tool
summary: Read a story feed item before updating, commenting, liking, or deleting it.
graph_groups:
  - Story tools
  - Read operation
graph_next_steps:
  - https://kb.example/tools/story-delete/
  - https://kb.example/tools/story-comments/
---
# Story Feed Detail Tool

Use this capability to inspect an exact story feed item and resolve the story before delete or comment actions.
""";

    private const string StoryCommentsMarkdown = """
---
title: Story Comments Tool
summary: Add or inspect comments on a story after the story is known.
graph_groups:
  - Story tools
  - Comment operation
---
# Story Comments Tool

Use this capability after a story has been found.
""";

    private const string PeopleSearchMarkdown = """
---
title: People Search Tool
summary: Find people in family trees by name or relationship clues.
graph_groups:
  - People tools
  - Search operation
---
# People Search Tool

Use this capability for genealogy people lookup, relatives, parents, and profile searches.
""";

    [Test]
    public async Task Capability_graph_front_matter_builds_focused_search_with_related_and_next_step_results()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.Tiktoken);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(StoryDeletePath, StoryDeleteMarkdown),
            new MarkdownSourceDocument(StoryFeedPath, StoryFeedMarkdown),
            new MarkdownSourceDocument(StoryCommentsPath, StoryCommentsMarkdown),
            new MarkdownSourceDocument(PeopleSearchPath, PeopleSearchMarkdown),
        ]);

        var focused = await result.Graph.SearchFocusedAsync(
            RemoveStoryQuery,
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 3,
                MaxNextStepResults = 3,
            });

        focused.PrimaryMatches.Count.ShouldBe(1);
        focused.PrimaryMatches.Single().NodeId.ShouldBe(StoryDeleteUri);
        focused.PrimaryMatches.Single().Label.ShouldBe("Story Delete Tool");

        focused.RelatedMatches.Select(match => match.NodeId).ShouldContain(StoryFeedUri);
        focused.RelatedMatches.Select(match => match.NodeId).ShouldNotContain(PeopleSearchUri);
        focused.NextStepMatches.Select(match => match.NodeId).ShouldContain(StoryCommentsUri);

        focused.FocusedGraph.Nodes.Select(node => node.Label).ShouldContain(StoryToolsGroup);
        focused.FocusedGraph.Nodes.Select(node => node.Label).ShouldContain(DeleteOperationGroup);
        focused.FocusedGraph.Edges.Select(edge => edge.PredicateLabel).ShouldContain("kb:memberOf");
        focused.FocusedGraph.Edges.Select(edge => edge.PredicateLabel).ShouldContain("kb:relatedTo");
        focused.FocusedGraph.Edges.Select(edge => edge.PredicateLabel).ShouldContain("kb:nextStep");

        var mermaid = KnowledgeGraph.SerializeMermaidFlowchart(focused.FocusedGraph);
        mermaid.ShouldContain(StoryToolsGroup);
        mermaid.ShouldContain("kb:nextStep");
        mermaid.ShouldNotContain("People Search Tool");
    }

    [Test]
    public async Task Focused_graph_snapshot_respects_related_and_next_step_limits()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(PrimaryBoundedPath, BoundedPrimaryMarkdown),
            new MarkdownSourceDocument(AlphaRelatedPath, CreateTitleOnlyMarkdown(AlphaRelatedTitle)),
            new MarkdownSourceDocument(BetaRelatedPath, CreateTitleOnlyMarkdown(BetaRelatedTitle)),
            new MarkdownSourceDocument(AlphaNextPath, CreateTitleOnlyMarkdown(AlphaNextTitle)),
            new MarkdownSourceDocument(BetaNextPath, CreateTitleOnlyMarkdown(BetaNextTitle)),
        ]);

        var focused = await result.Graph.SearchFocusedAsync(
            PrimaryBoundedTitle,
            new KnowledgeGraphFocusedSearchOptions
            {
                MaxPrimaryResults = 1,
                MaxRelatedResults = 1,
                MaxNextStepResults = 1,
            });

        focused.PrimaryMatches.Single().NodeId.ShouldBe(PrimaryBoundedUri);
        focused.RelatedMatches.Count.ShouldBe(1);
        focused.RelatedMatches.Single().NodeId.ShouldBe(AlphaRelatedUri);
        focused.NextStepMatches.Count.ShouldBe(1);
        focused.NextStepMatches.Single().NodeId.ShouldBe(AlphaNextUri);

        var focusedLabels = focused.FocusedGraph.Nodes.Select(static node => node.Label).ToArray();
        focusedLabels.ShouldContain(PrimaryBoundedTitle);
        focusedLabels.ShouldContain(AlphaRelatedTitle);
        focusedLabels.ShouldContain(AlphaNextTitle);
        focusedLabels.ShouldNotContain(BetaRelatedTitle);
        focusedLabels.ShouldNotContain(BetaNextTitle);

        var mermaid = KnowledgeGraph.SerializeMermaidFlowchart(focused.FocusedGraph);
        mermaid.ShouldContain(AlphaRelatedTitle);
        mermaid.ShouldContain(AlphaNextTitle);
        mermaid.ShouldNotContain(BetaRelatedTitle);
        mermaid.ShouldNotContain(BetaNextTitle);
    }

    [Test]
    public async Task Malformed_graph_rule_entries_are_reported_as_diagnostics()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(MalformedRulePath, MalformedRulesMarkdown),
        ]);

        result.Diagnostics.ShouldContain("Graph rule skipped: graph_edges[0] requires a mapping value.");
        result.Diagnostics.ShouldContain("Graph rule skipped: graph_edges[1] requires a predicate.");
        result.Diagnostics.ShouldContain("Graph rule skipped: graph_edges[2] requires an object.");
        result.Diagnostics.ShouldContain("Graph rule skipped: graph_related[0] requires a node label or id.");
    }

    [Test]
    public async Task Related_and_next_step_targets_preserve_authored_entity_metadata()
    {
        var pipeline = new MarkdownKnowledgePipeline(BaseUri);
        var result = await pipeline.BuildAsync([
            new MarkdownSourceDocument(RuleTargetPath, RuleTargetMarkdown),
        ]);

        var targetMetadataExists = await result.Graph.ExecuteAskAsync("""
PREFIX schema: <https://schema.org/>
ASK WHERE {
  <https://kb.example/tools/review-target/> a schema:SoftwareApplication ;
    schema:name "Review Target Tool" ;
    schema:sameAs <https://external.example/review-target> .
  <https://kb.example/id/escalation-step> a schema:DefinedTerm ;
    schema:name "Escalation Step" .
}
""");
        targetMetadataExists.ShouldBeTrue();

        var focused = await result.Graph.SearchFocusedAsync("Rule Target Source Tool");
        focused.RelatedMatches.Single().NodeId.ShouldBe(ReviewTargetUri);
        focused.NextStepMatches.Single().NodeId.ShouldBe(EscalationTargetUri);
        focused.FocusedGraph.Nodes.Select(static node => node.Label).ShouldContain(ReviewTargetTitle);
        focused.FocusedGraph.Nodes.Select(static node => node.Label).ShouldContain(EscalationTargetTitle);
    }

    private const string BoundedPrimaryMarkdown = """
---
title: Primary Delete Bounded Tool
summary: Primary capability used to delete a bounded story item.
graph_groups:
  - Story tools
graph_related:
  - https://kb.example/tools/alpha-related/
  - https://kb.example/tools/beta-related/
graph_next_steps:
  - https://kb.example/tools/alpha-next/
  - https://kb.example/tools/beta-next/
---
# Primary Delete Bounded Tool

Primary delete capability.
""";

    private const string MalformedRulesMarkdown = """
---
title: Malformed Rules Tool
graph_related:
  - ""
graph_edges:
  - broken scalar
  - object: Story tools
  - predicate: relatedto
---
# Malformed Rules Tool

This document keeps valid metadata while reporting invalid graph rules.
""";

    private const string RuleTargetMarkdown = """
---
title: Rule Target Source Tool
summary: Uses authored target metadata for graph construction.
graph_related:
  - id: https://kb.example/tools/review-target/
    label: Review Target Tool
    type: schema:SoftwareApplication
    sameAs:
      - https://external.example/review-target
graph_next_steps:
  - label: Escalation Step
    type: schema:DefinedTerm
---
# Rule Target Source Tool

This capability points to authored target nodes.
""";

    private static string CreateTitleOnlyMarkdown(string title)
        => $"""
---
title: {title}
summary: Supporting capability for bounded focused search.
---
# {title}

Supporting capability.
""";
}
