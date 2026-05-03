using ManagedCode.MarkdownLd.Kb.Pipeline;
using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb.Query;

public sealed class ChatClientKnowledgeAnswerService
{
    private readonly IChatClient _chatClient;
    private readonly ChatOptions _chatOptions;
    private readonly string _answerSystemPrompt;
    private readonly string _rewriteSystemPrompt;

    public ChatClientKnowledgeAnswerService(
        IChatClient chatClient,
        ChatOptions? chatOptions = null,
        string? answerSystemPrompt = null,
        string? rewriteSystemPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        _chatClient = chatClient;
        _chatOptions = chatOptions?.Clone() ?? new ChatOptions();
        _answerSystemPrompt = answerSystemPrompt ?? KnowledgeAnsweringConstants.DefaultAnswerSystemPrompt;
        _rewriteSystemPrompt = rewriteSystemPrompt ?? KnowledgeAnsweringConstants.DefaultRewriteSystemPrompt;
    }

    public async Task<KnowledgeAnswerResult> AnswerAsync(
        MarkdownKnowledgeBuildResult buildResult,
        KnowledgeAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buildResult);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question);
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var searchQuery = await ResolveSearchQueryAsync(request, cancellationToken).ConfigureAwait(false);
        var snapshot = buildResult.Graph.ToSnapshot();
        var searchOptions = KnowledgeAnswerScopeBuilder.CreateSearchOptions(request, buildResult.Documents, snapshot);
        var matches = await buildResult
            .SearchRankedAsync(searchQuery, searchOptions, request.SemanticIndex, cancellationToken)
            .ConfigureAwait(false);
        var citations = KnowledgeAnswerCitationBuilder.Build(
            buildResult.Documents,
            snapshot,
            matches,
            searchQuery,
            request.MaxCitations,
            request.MaxSnippetLength);
        var answer = await CreateAnswerAsync(request, searchQuery, citations, cancellationToken).ConfigureAwait(false);

        return new KnowledgeAnswerResult(
            request.Question.Trim(),
            searchQuery,
            answer,
            citations);
    }

    private async Task<string> ResolveSearchQueryAsync(
        KnowledgeAnswerRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.RewriteQuestionWithHistory || request.ConversationHistory.Count == 0)
        {
            return request.Question.Trim();
        }

        var response = await _chatClient.GetResponseAsync(
                BuildRewriteMessages(request),
                BuildChatOptions(),
                cancellationToken)
            .ConfigureAwait(false);

        return NormalizeRequiredText(response.Text, KnowledgeAnsweringConstants.EmptyRewrittenQueryMessage);
    }

    private async Task<string> CreateAnswerAsync(
        KnowledgeAnswerRequest request,
        string searchQuery,
        IReadOnlyList<KnowledgeAnswerCitation> citations,
        CancellationToken cancellationToken)
    {
        var response = await _chatClient.GetResponseAsync(
                BuildAnswerMessages(request, searchQuery, citations),
                BuildChatOptions(),
                cancellationToken)
            .ConfigureAwait(false);

        return NormalizeRequiredText(response.Text, KnowledgeAnsweringConstants.EmptyAnswerMessage);
    }

    private ChatOptions BuildChatOptions()
    {
        var options = _chatOptions.Clone();
        options.Temperature = 0;
        return options;
    }

    private ChatMessage[] BuildRewriteMessages(KnowledgeAnswerRequest request)
    {
        return
        [
            new ChatMessage(ChatRole.System, _rewriteSystemPrompt),
            new ChatMessage(
                ChatRole.User,
                KnowledgeAnswerPromptBuilder.BuildRewritePrompt(
                    request.Question,
                    request.ConversationHistory)),
        ];
    }

    private ChatMessage[] BuildAnswerMessages(
        KnowledgeAnswerRequest request,
        string searchQuery,
        IReadOnlyList<KnowledgeAnswerCitation> citations)
    {
        return
        [
            new ChatMessage(ChatRole.System, _answerSystemPrompt),
            new ChatMessage(
                ChatRole.User,
                KnowledgeAnswerPromptBuilder.BuildAnswerPrompt(request, searchQuery, citations)),
        ];
    }

    private static void ValidateRequest(KnowledgeAnswerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.SearchOptions);
        ArgumentNullException.ThrowIfNull(request.ConversationHistory);
        ArgumentNullException.ThrowIfNull(request.AllowedSourcePaths);
        ArgumentNullException.ThrowIfNull(request.AllowedDocumentUris);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaxCitations);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaxSnippetLength);
        ValidateFilterEntries(
            request.AllowedSourcePaths,
            KnowledgeAnsweringConstants.EmptyAllowedSourcePathMessage);
        ValidateFilterEntries(
            request.AllowedDocumentUris,
            KnowledgeAnsweringConstants.EmptyAllowedDocumentUriMessage);
    }

    private static void ValidateFilterEntries(IReadOnlyList<string> values, string message)
    {
        if (values.Any(static value => string.IsNullOrWhiteSpace(value)))
        {
            throw new ArgumentException(message);
        }
    }

    private static string NormalizeRequiredText(string text, string emptyMessage)
    {
        var trimmed = text.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? throw new InvalidOperationException(emptyMessage)
            : trimmed;
    }
}
