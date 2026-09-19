using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// A model that asks for lookup_archetype once, then replies with whatever the tool sent back.
// Exists to prove a call crosses to the real server and its result comes back.
sealed class CallsToolOnceChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var last = messages.Last();

        // Second turn: the loop has appended the tool's answer. Reply with it.
        if (last.Role == ChatRole.Tool)
        {
            var answer = last.Contents.OfType<FunctionResultContent>().Single().Result?.ToString();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)));
        }

        // First turn: ask for the server's tool, by the server's name.
        var call = new FunctionCallContent("call-1", "lookup_archetype",
            new Dictionary<string, object?> { ["occupation"] = "innkeeper" });
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
    }

    // The interface has four members. Only GetResponseAsync matters here; these exist because it says so.
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
