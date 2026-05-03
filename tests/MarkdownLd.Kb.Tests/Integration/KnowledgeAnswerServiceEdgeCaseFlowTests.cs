using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;
using static ManagedCode.MarkdownLd.Kb.Tests.Integration.KnowledgeAnswerServiceTestFixtures;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeAnswerServiceEdgeCaseFlowTests
{
    [Test]
    public async Task Answer_service_fails_explicitly_for_null_request_members()
    {
        var build = await BuildAsync();
        var chatClient = new TestChatClient((_, _) => AnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.AnswerAsync(
                build,
                new KnowledgeAnswerRequest(SettingsQuestion)
                {
                    SearchOptions = null!,
                }));

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.AnswerAsync(
                build,
                new KnowledgeAnswerRequest(SettingsQuestion)
                {
                    ConversationHistory = null!,
                }));

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.AnswerAsync(
                build,
                new KnowledgeAnswerRequest(SettingsQuestion)
                {
                    AllowedSourcePaths = null!,
                }));

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.AnswerAsync(
                build,
                new KnowledgeAnswerRequest(SettingsQuestion)
                {
                    AllowedDocumentUris = null!,
                }));
    }

    [Test]
    public async Task Answer_service_uses_match_label_when_document_has_no_chunk_or_description()
    {
        var build = await BuildAsync(new MarkdownSourceDocument(LabelOnlyPath, LabelOnlyMarkdown));
        var chatClient = new TestChatClient((_, _) => EdgeAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(build, new KnowledgeAnswerRequest(LabelOnlyQuestion));

        result.Citations.Single().SourcePath.ShouldBe(LabelOnlyPath);
        result.Citations.Single().Snippet.ShouldBe(LabelOnlyQuestion);
    }

    [Test]
    public async Task Answer_service_uses_match_label_instead_of_unrelated_body_when_chunks_lack_evidence()
    {
        var build = await BuildAsync(new MarkdownSourceDocument(LabelWithBodyPath, LabelWithBodyMarkdown));
        var chatClient = new TestChatClient((_, _) => EdgeAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(build, new KnowledgeAnswerRequest(LabelWithBodyQuestion));

        result.Citations.Single().SourcePath.ShouldBe(LabelWithBodyPath);
        result.Citations.Single().Snippet.ShouldBe(LabelWithBodyQuestion);
        result.Citations.Single().Snippet.ShouldNotContain("unrelated body text");
    }

    [Test]
    public async Task Answer_service_selects_evidence_source_when_duplicate_document_uris_exist()
    {
        var duplicateUri = new Uri(DuplicateCanonicalUriText);
        var build = await BuildAsync(
            new MarkdownSourceDocument(DuplicatePrimaryPath, DuplicatePrimaryMarkdown, duplicateUri),
            new MarkdownSourceDocument(DuplicateSecondaryPath, DuplicateSecondaryMarkdown, duplicateUri));
        var chatClient = new TestChatClient((_, _) => EdgeAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(DuplicateCanonicalQuestion)
            {
                SearchOptions = new KnowledgeGraphRankedSearchOptions
                {
                    Mode = KnowledgeGraphSearchMode.Bm25,
                    MaxResults = 1,
                },
            });

        result.Citations.Single().DocumentUri.ShouldBe(DuplicateCanonicalUriText);
        result.Citations.Single().SourcePath.ShouldBe(DuplicateSecondaryPath);
        result.Citations.Single().Snippet.ShouldContain(DuplicateCanonicalQuestion);
    }

    [Test]
    public async Task Answer_service_selects_best_evidence_source_for_multi_source_entity_matches()
    {
        var build = await BuildAsync(
            new MarkdownSourceDocument(MultiSourcePrimaryPath, MultiSourcePrimaryMarkdown),
            new MarkdownSourceDocument(MultiSourceSecondaryPath, MultiSourceSecondaryMarkdown));
        var chatClient = new TestChatClient((_, _) => EdgeAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(MultiSourceEntityQuestion)
            {
                SearchOptions = new KnowledgeGraphRankedSearchOptions
                {
                    CandidateNodeIds = [MultiSourceEntityUriText],
                },
            });

        result.Citations.Single().SourcePath.ShouldBe(MultiSourceSecondaryPath);
        result.Citations.Single().Snippet.ShouldContain(MultiSourceEntityQuestion);
    }

    [Test]
    public async Task Answer_service_skips_ranked_matches_that_have_no_source_document()
    {
        var graph = KnowledgeGraph.LoadJsonLd(OrphanJsonLd);
        var build = new MarkdownKnowledgeBuildResult([], new KnowledgeExtractionResult(), graph);
        var chatClient = new TestChatClient((_, _) => EdgeAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(build, new KnowledgeAnswerRequest(OrphanQuestion));

        result.Citations.ShouldBeEmpty();
        chatClient.LastMessages.Single(message => message.Role == ChatRole.User).Text.ShouldContain("No relevant documents were found.");
    }

    [Test]
    public async Task Answer_service_returns_empty_citations_for_no_match_context()
    {
        var build = await BuildAsync(new MarkdownSourceDocument(TreePath, TreeMarkdown));
        var chatClient = new TestChatClient((_, _) => NoMatchAnswer);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(build, new KnowledgeAnswerRequest(NoMatchQuestion));

        result.Answer.ShouldBe(NoMatchAnswer);
        result.Citations.ShouldBeEmpty();
        chatClient.LastMessages.Single(message => message.Role == ChatRole.User).Text.ShouldContain("No relevant documents were found.");
    }

    [Test]
    public async Task Answer_service_fails_explicitly_when_rewrite_returns_empty_query()
    {
        var build = await BuildAsync();
        var chatClient = new TestChatClient((_, _) => string.Empty);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.AnswerAsync(
                build,
                new KnowledgeAnswerRequest(EmptyRewriteQuestion)
                {
                    ConversationHistory =
                    [
                        new KnowledgeAnswerConversationMessage(KnowledgeAnswerConversationRole.User, OriginalQuestion),
                    ],
                }));

        exception.Message.ShouldContain("rewritten query");
    }

}
