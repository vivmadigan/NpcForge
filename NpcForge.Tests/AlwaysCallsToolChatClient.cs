using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// A model that asks for a tool every single time. Exists to prove the cap fires.
sealed class AlwaysCallsToolChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var call = new FunctionCallContent("call-1", "LookupArchetype",
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
