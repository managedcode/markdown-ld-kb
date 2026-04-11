using Microsoft.Extensions.AI;

namespace ManagedCode.MarkdownLd.Kb.Tests.Support;

public sealed class TestChatClient : IChatClient
{
    private readonly Func<IReadOnlyList<ChatMessage>, string> _responseFactory;
    private readonly List<IReadOnlyList<ChatMessage>> _requests = [];

    public TestChatClient(Func<IReadOnlyList<ChatMessage>, string> responseFactory)
    {
        _responseFactory = responseFactory;
    }

    public IReadOnlyList<IReadOnlyList<ChatMessage>> Requests => _requests;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialized = messages.ToArray();
        _requests.Add(materialized);
        var content = _responseFactory(materialized);
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, content)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming is not used in these tests.");
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
