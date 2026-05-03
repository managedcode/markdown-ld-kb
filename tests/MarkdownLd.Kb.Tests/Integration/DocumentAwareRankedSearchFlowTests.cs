using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class DocumentAwareRankedSearchFlowTests
{
    private const string BaseUriText = "https://document-aware-search.example/";
    private const string BodyOnlyPath = "content/runbooks/body-only-restore.md";
    private const string DistractorPath = "content/runbooks/distractor.md";
    private const string BodyOnlyTitle = "Operational repair note";
    private const string Query = "orphan checksum replay";
    private const string LongEvidencePath = "content/runbooks/long-evidence.md";
    private const string DuplicatePrimaryPath = "content/runbooks/duplicate-primary.md";
    private const string DuplicateSecondaryPath = "content/runbooks/duplicate-secondary.md";
    private const string DuplicateCanonicalUriText = "https://document-aware-search.example/runbooks/duplicate-canonical/";
    private const string LongEvidenceQuery = "restore deepreplaytoken";
    private const string DuplicateCanonicalQuery = "secondaryduplicateanchor";
    private const string LongEvidenceAnchor = "deepreplaytoken";
    private const string AnswerText = "Use the operational repair note for orphan checksum replay [1].";
    private const string LongEvidenceAnswerText = "Use the deepreplaytoken runbook evidence [1].";
    private const int FocusedSnippetLength = 80;
    private static readonly Uri BaseUri = new(BaseUriText);

    private const string BodyOnlyMarkdown = """
---
title: Operational repair note
summary: General maintenance reference.
---
# Operational repair note

The maintenance team should run orphan checksum replay after the archive restore finishes.
""";

    private const string DistractorMarkdown = """
---
title: Restore overview
summary: General archive restore summary.
---
# Restore overview

This page lists storage responsibilities without repair evidence.
""";

    private const string LongEvidenceMarkdown = """
---
title: Deep recovery note
summary: General recovery note.
---
# Background

Background context describes ordinary restore handling, operator handoff, dry-run timing, and routine validation before any emergency branch is selected.

## Recovery action

The decisive instruction is to run the deepreplaytoken before publishing restore evidence.
""";

    private const string DuplicatePrimaryMarkdown = """
---
title: Duplicate canonical runbook
summary: Primary duplicate source.
---
# Duplicate canonical runbook

The primary source explains routine cache validation only.
""";

    private const string DuplicateSecondaryMarkdown = """
---
title: Duplicate canonical runbook
summary: Secondary duplicate source.
---
# Duplicate canonical runbook

The secondary source carries the secondaryduplicateanchor recovery evidence.
""";

    [Test]
    public async Task Build_result_bm25_search_uses_markdown_body_chunks_as_ranked_evidence()
    {
        var build = await BuildAsync();

        var results = await build.SearchRankedAsync(
            Query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 1,
            });

        results.ShouldNotBeEmpty();
        results[0].Label.ShouldBe(BodyOnlyTitle);
        results[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);
    }

    [Test]
    public async Task Facade_search_and_cited_answer_use_document_body_aware_bm25()
    {
        var chatClient = new TestChatClient((_, _) => AnswerText);
        var bank = new MarkdownKnowledgeBank(new MarkdownKnowledgeBankOptions
        {
            PipelineOptions = new MarkdownKnowledgePipelineOptions
            {
                BaseUri = BaseUri,
                ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            },
            ChatClient = chatClient,
        });
        var build = await bank.BuildAsync(CreateSources());

        var search = await build.SearchAsync(
            Query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });
        search[0].Label.ShouldBe(BodyOnlyTitle);

        var answer = await build.AnswerAsync(
            Query,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 2,
            });
        answer.Answer.ShouldBe(AnswerText);
        answer.Citations.Single().SourcePath.ShouldBe(BodyOnlyPath);
        answer.Citations.Single().Snippet.ShouldContain("orphan checksum replay");
    }

    [Test]
    public async Task Cited_answer_snippet_is_focused_on_body_only_evidence()
    {
        var chatClient = new TestChatClient((_, _) => LongEvidenceAnswerText);
        var bank = new MarkdownKnowledgeBank(new MarkdownKnowledgeBankOptions
        {
            PipelineOptions = new MarkdownKnowledgePipelineOptions
            {
                BaseUri = BaseUri,
                ExtractionMode = MarkdownKnowledgeExtractionMode.None,
            },
            ChatClient = chatClient,
        });
        var build = await bank.BuildAsync([new MarkdownSourceDocument(LongEvidencePath, LongEvidenceMarkdown)]);

        var answer = await build.AnswerAsync(
            new KnowledgeAnswerRequest(LongEvidenceQuery)
            {
                SearchOptions = new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Bm25,
                    MaxResults = 1,
                },
                MaxSnippetLength = FocusedSnippetLength,
            });

        var citation = answer.Citations.Single();
        citation.SourcePath.ShouldBe(LongEvidencePath);
        citation.HeadingPath.ShouldContain("Recovery action");
        citation.Snippet.ShouldContain(LongEvidenceAnchor);
        citation.Snippet.ShouldNotContain("ordinary restore handling");
        citation.Snippet.Length.ShouldBeLessThanOrEqualTo(FocusedSnippetLength);
    }

    [Test]
    public async Task Build_result_bm25_search_merges_duplicate_document_uri_sources_without_crashing()
    {
        var duplicateUri = new Uri(DuplicateCanonicalUriText);
        var build = await BuildAsync(
            new MarkdownSourceDocument(DuplicatePrimaryPath, DuplicatePrimaryMarkdown, duplicateUri),
            new MarkdownSourceDocument(DuplicateSecondaryPath, DuplicateSecondaryMarkdown, duplicateUri));

        var results = await build.SearchRankedAsync(
            DuplicateCanonicalQuery,
            new KnowledgeGraphRankedSearchOptions
            {
                Mode = KnowledgeGraphSearchMode.Bm25,
                MaxResults = 1,
            });

        results.ShouldNotBeEmpty();
        results[0].NodeId.ShouldBe(DuplicateCanonicalUriText);
        results[0].Source.ShouldBe(KnowledgeGraphRankedSearchSource.Bm25);
    }

    private static Task<MarkdownKnowledgeBuildResult> BuildAsync(params MarkdownSourceDocument[]? sources)
    {
        var pipeline = new MarkdownKnowledgePipeline(
            BaseUri,
            extractionMode: MarkdownKnowledgeExtractionMode.None);

        return pipeline.BuildAsync(sources is null || sources.Length == 0 ? CreateSources() : sources);
    }

    private static MarkdownSourceDocument[] CreateSources()
    {
        return
        [
            new MarkdownSourceDocument(BodyOnlyPath, BodyOnlyMarkdown),
            new MarkdownSourceDocument(DistractorPath, DistractorMarkdown),
        ];
    }
}
