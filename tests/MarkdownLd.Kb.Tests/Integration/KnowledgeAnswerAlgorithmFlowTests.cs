using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeAnswerAlgorithmFlowTests
{
    private const string BaseUriText = "https://answer-algorithm.example/";
    private const string LabelOverlapPath = "content/tools/label-description-overlap.md";
    private const string AssertionPrimaryPath = "content/tools/assertion-primary.md";
    private const string AssertionSecondaryPath = "content/tools/assertion-secondary.md";
    private const string AssertionPrimaryUri = "https://answer-algorithm.example/tools/assertion-primary/";
    private const string AssertionSecondaryUri = "https://answer-algorithm.example/tools/assertion-secondary/";
    private const string SharedAssertionToolUri = "https://answer-algorithm.example/id/shared-assertion-tool";
    private const string SharedAssertionTargetUri = "https://answer-algorithm.example/id/shared-assertion-target";
    private const string LabelOverlapQuestion = "Graph Label Overlap Evidence";
    private const string SharedAssertionToolQuestion = "Shared Assertion Tool";
    private const string AnswerText = "Algorithm answer [1].";

    private const string LabelOverlapMarkdown = """
---
title: Graph Label Overlap Evidence
summary: Operational guide reference.
---
# Body

This guide describes routine maintenance without the graph title phrase.
""";

    private const string AssertionPrimaryMarkdown = """
---
title: Assertion primary source
---
# Assertion primary source

The primary assertion source lists routine routing notes only.
""";

    private const string AssertionSecondaryMarkdown = """
---
title: Assertion secondary source
---
# Assertion secondary source

Shared Assertion Tool has cited evidence in the secondary assertion source.
""";

    [Test]
    public async Task Answer_service_keeps_strong_label_snippet_when_body_only_shares_weak_description_context()
    {
        var build = await BuildAsync(new MarkdownSourceDocument(LabelOverlapPath, LabelOverlapMarkdown));
        var service = new ChatClientKnowledgeAnswerService(new TestChatClient((_, _) => AnswerText));

        var result = await service.AnswerAsync(build, new KnowledgeAnswerRequest(LabelOverlapQuestion));

        result.Citations.Single().SourcePath.ShouldBe(LabelOverlapPath);
        result.Citations.Single().Snippet.ShouldBe(LabelOverlapQuestion);
        result.Citations.Single().Snippet.ShouldNotContain("routine maintenance");
    }

    [Test]
    public async Task Answer_service_selects_best_evidence_source_for_multi_source_assertion_provenance()
    {
        var build = await BuildAssertionSourceAsync();
        var service = new ChatClientKnowledgeAnswerService(new TestChatClient((_, _) => AnswerText));

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(SharedAssertionToolQuestion)
            {
                SearchOptions = new KnowledgeGraphRankedSearchOptions
                {
                    CandidateNodeIds = [SharedAssertionToolUri],
                },
            });

        result.Citations.Single().SourcePath.ShouldBe(AssertionSecondaryPath);
        result.Citations.Single().Snippet.ShouldContain(SharedAssertionToolQuestion);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAsync(params MarkdownSourceDocument[] documents)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            new Uri(BaseUriText),
            extractionMode: MarkdownKnowledgeExtractionMode.None);

        return pipeline.BuildAsync(documents);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAssertionSourceAsync()
    {
        var pipeline = new MarkdownKnowledgePipeline(
            new Uri(BaseUriText),
            extractionMode: MarkdownKnowledgeExtractionMode.None);

        return pipeline.BuildAsync(
            [
                new MarkdownSourceDocument(AssertionPrimaryPath, AssertionPrimaryMarkdown),
                new MarkdownSourceDocument(AssertionSecondaryPath, AssertionSecondaryMarkdown),
            ],
            new KnowledgeGraphBuildOptions
            {
                IncludeFrontMatterRules = false,
                Entities =
                [
                    new KnowledgeGraphEntityRule
                    {
                        Id = SharedAssertionToolUri,
                        Label = SharedAssertionToolQuestion,
                        Type = "schema:SoftwareApplication",
                    },
                    new KnowledgeGraphEntityRule
                    {
                        Id = SharedAssertionTargetUri,
                        Label = "Shared Assertion Target",
                        Type = "schema:Thing",
                    },
                ],
                Edges =
                [
                    new KnowledgeGraphEdgeRule
                    {
                        SubjectId = SharedAssertionToolUri,
                        Predicate = "relatedto",
                        ObjectId = SharedAssertionTargetUri,
                        Source = AssertionPrimaryUri,
                    },
                    new KnowledgeGraphEdgeRule
                    {
                        SubjectId = SharedAssertionToolUri,
                        Predicate = "relatedto",
                        ObjectId = SharedAssertionTargetUri,
                        Source = AssertionSecondaryUri,
                    },
                ],
            });
    }
}
