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

    private const string StoryDeleteUri = "https://kb.example/tools/story-delete/";
    private const string StoryFeedUri = "https://kb.example/tools/story-feed-detail/";
    private const string StoryCommentsUri = "https://kb.example/tools/story-comments/";
    private const string PeopleSearchUri = "https://kb.example/tools/people-search/";

    private const string StoryToolsGroup = "Story tools";
    private const string DeleteOperationGroup = "Delete operation";
    private const string RemoveStoryQuery = "remove the selected story from the feed";

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
}
