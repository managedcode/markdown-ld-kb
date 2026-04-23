using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb.Tests.Support;

public sealed class TestChatClient(Func<IReadOnlyList<ChatMessage>, ChatOptions?, string> responseFactory) : IChatClient
{
    private readonly Func<IReadOnlyList<ChatMessage>, ChatOptions?, string> _responseFactory = responseFactory;
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];

    public IReadOnlyList<IReadOnlyList<ChatMessage>> Requests => _requests;

    public ChatOptions? LastOptions { get; private set; }

    public IReadOnlyList<ChatMessage> LastMessages => _requests.Count == 0 ? Array.Empty<ChatMessage>() : _requests[^1];

    public int CallCount => _requests.Count;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var content = RecordRequest(messages, options);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, content)));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = RecordRequest(messages, options);
        yield return new ChatResponseUpdate(ChatRole.Assistant, content);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    private string RecordRequest(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var materialized = messages.ToArray();
        _requests.Add(materialized);
        LastOptions = options?.Clone();
        return _responseFactory(materialized, options);
    }

    public void Dispose()
    {
    }
}
