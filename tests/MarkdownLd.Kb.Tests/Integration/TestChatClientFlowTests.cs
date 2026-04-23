using ManagedCode.MarkdownLd.Kb.Tests.Support;
using Microsoft.Extensions.AI;
using Shouldly;

namespace ManagedCode.MarkdownLd.Kb.Tests.Integration;

public sealed class TestChatClientFlowTests
{
    private const string PromptText = "Extract the graph facts.";
    private const string ResponseText = "{\"entities\":[],\"assertions\":[]}";
    private const string ModelId = "test-model";

    [Test]
    public async Task Streaming_client_returns_a_single_assistant_update_and_tracks_request_state()
    {
        var client = new TestChatClient((_, _) => ResponseText);
        var options = new ChatOptions { ModelId = ModelId };
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync(
                           [new ChatMessage(ChatRole.User, PromptText)],
                           options))
        {
            updates.Add(update);
        }

        updates.Count.ShouldBe(1);
        updates[0].Role.ShouldBe(ChatRole.Assistant);
        updates[0].Text.ShouldBe(ResponseText);
        client.CallCount.ShouldBe(1);
        client.LastOptions.ShouldNotBeNull();
        client.LastOptions.ModelId.ShouldBe(ModelId);
        client.LastMessages.Single().Text.ShouldBe(PromptText);
    }
}
