using ManagedCode.MarkdownLd.Kb.Pipeline;
using ManagedCode.MarkdownLd.Kb.Query;
using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;
using static ManagedCode.MarkdownLd.Kb.Tests.Integration.KnowledgeAnswerServiceTestFixtures;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class KnowledgeAnswerServiceFlowTests
{
    [Test]
    public async Task Answer_service_builds_cited_context_from_ranked_graph_matches()
    {
        var build = await BuildAsync();
        var chatClient = new TestChatClient((_, _) => AnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(build, new KnowledgeAnswerRequest(OriginalQuestion));

        result.Answer.ShouldBe(AnswerText);
        result.Question.ShouldBe(OriginalQuestion);
        result.SearchQuery.ShouldBe(OriginalQuestion);
        result.Citations.Count.ShouldBe(1);
        result.Citations[0].Index.ShouldBe(1);
        result.Citations[0].SourcePath.ShouldBe(NotificationsPath);
        result.Citations[0].DocumentUri.ShouldBe("https://answer.example/tools/notification-settings/");
        result.Citations[0].Snippet.ShouldContain("notification delivery preferences");
        chatClient.LastMessages.Single(message => message.Role == ChatRole.User).Text.ShouldContain("[1]");
        chatClient.LastMessages.Single(message => message.Role == ChatRole.User).Text.ShouldContain(NotificationsPath);
    }

    [Test]
    public async Task Answer_service_rewrites_follow_up_query_before_searching()
    {
        var build = await BuildAsync();
        var callIndex = 0;
        var chatClient = new TestChatClient((_, _) => callIndex++ == 0 ? RewrittenQuery : AnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(FollowUpQuestion)
            {
                ConversationHistory =
                [
                    new KnowledgeAnswerConversationMessage(KnowledgeAnswerConversationRole.User, OriginalQuestion),
                    new KnowledgeAnswerConversationMessage(KnowledgeAnswerConversationRole.Assistant, AssistantHistoryAnswer),
                ],
            });

        result.SearchQuery.ShouldBe(RewrittenQuery);
        result.Citations.Single().SourcePath.ShouldBe(NotificationsPath);
        chatClient.CallCount.ShouldBe(2);
        chatClient.Requests[0].Single(message => message.Role == ChatRole.User).Text.ShouldContain(FollowUpQuestion);
        chatClient.Requests[1].Single(message => message.Role == ChatRole.User).Text.ShouldContain(RewrittenQuery);
    }

    [Test]
    public async Task Answer_service_resolves_entity_match_citation_through_provenance()
    {
        var build = await BuildAsync(new MarkdownSourceDocument(ProvenancePath, ProvenanceMarkdown));
        var chatClient = new TestChatClient((_, _) => ProvenanceAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(ProvenanceQuestion)
            {
                MaxSnippetLength = 40,
            });

        result.Citations.Count.ShouldBe(1);
        result.Citations[0].SourcePath.ShouldBe(ProvenancePath);
        result.Citations[0].MatchLabel.ShouldBe(ProvenanceQuestion);
        result.Citations[0].Snippet.ShouldEndWith("...");
        result.Citations[0].Snippet.Length.ShouldBeLessThanOrEqualTo(43);
    }

    [Test]
    public async Task Answer_service_limits_citations_and_uses_front_matter_summary_when_chunks_are_absent()
    {
        var build = await BuildAsync(
            new MarkdownSourceDocument(SummaryOnlyPath, SummaryOnlyMarkdown),
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown));
        var chatClient = new TestChatClient((_, _) => EdgeAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(SettingsQuestion)
            {
                MaxCitations = 1,
            });

        result.Citations.Count.ShouldBe(1);
        result.Citations[0].SourcePath.ShouldBe(SummaryOnlyPath);
        result.Citations[0].Snippet.ShouldBe("Audit settings summarize retention controls without body chunks.");
    }

    [Test]
    public async Task Answer_service_scopes_retrieval_to_allowed_source_paths()
    {
        var build = await BuildAsync(
            new MarkdownSourceDocument(SummaryOnlyPath, SummaryOnlyMarkdown),
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown));
        var chatClient = new TestChatClient((_, _) => AnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(SettingsQuestion)
            {
                AllowedSourcePaths = [NotificationsPath],
            });

        result.Citations.Single().SourcePath.ShouldBe(NotificationsPath);
        result.Citations.Single().Snippet.ShouldContain("notification delivery preferences");
        chatClient.LastMessages.Single(message => message.Role == ChatRole.User).Text.ShouldNotContain(SummaryOnlyPath);

        var uriScoped = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(SettingsQuestion)
            {
                AllowedDocumentUris = [NotificationsDocumentUri],
            });

        uriScoped.Citations.Single().SourcePath.ShouldBe(NotificationsPath);
    }

    [Test]
    public async Task Answer_service_intersects_source_scope_with_existing_candidate_filter()
    {
        var build = await BuildAsync(
            new MarkdownSourceDocument(SummaryOnlyPath, SummaryOnlyMarkdown),
            new MarkdownSourceDocument(NotificationsPath, NotificationsMarkdown));
        var chatClient = new TestChatClient((_, _) => EdgeAnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var result = await service.AnswerAsync(
            build,
            new KnowledgeAnswerRequest(SettingsQuestion)
            {
                AllowedSourcePaths = [NotificationsPath],
                SearchOptions = new KnowledgeGraphRankedSearchOptions
                {
                    CandidateNodeIds = [SummaryOnlyDocumentUri],
                },
            });

        result.Citations.ShouldBeEmpty();
        chatClient.LastMessages.Single(message => message.Role == ChatRole.User).Text.ShouldContain("No relevant documents were found.");
    }

    [Test]
    public async Task Answer_service_fails_explicitly_for_empty_source_filters()
    {
        var build = await BuildAsync();
        var chatClient = new TestChatClient((_, _) => AnswerText);
        var service = new ChatClientKnowledgeAnswerService(chatClient);

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.AnswerAsync(
                build,
                new KnowledgeAnswerRequest(SettingsQuestion)
                {
                    AllowedSourcePaths = [string.Empty],
                }));

        exception.Message.ShouldContain("source path");

        var documentUriException = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.AnswerAsync(
                build,
                new KnowledgeAnswerRequest(SettingsQuestion)
                {
                    AllowedDocumentUris = [string.Empty],
                }));

        documentUriException.Message.ShouldContain("document URI");
    }

}
