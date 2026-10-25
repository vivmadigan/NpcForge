using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// Done when: the spending cap stops a session at its limit. Free: the model is a stand-in that
// reports a fixed usage and counts how often it was asked.
public class SpendingCapTests
{
    [Fact]
    public async Task Refuses_the_call_after_the_session_reaches_its_cap()
    {
        var model = new CountingChatClient(tokensPerCall: 1_000);
        var cap = new SpendingCap(model, maxTokens: 2_500);

        for (var i = 0; i < 3; i++)
            await cap.GetResponseAsync([new(ChatRole.User, "hi")]);

        var error = await Assert.ThrowsAsync<SpendingCapReachedException>(
            () => cap.GetResponseAsync([new(ChatRole.User, "hi")]));

        // The fourth call never reached the model: the cap refused it first.
        Assert.Equal(3, model.Calls);
        Assert.Equal(3_000, cap.Spent);
        Assert.Contains("Spending cap reached", error.Message);
    }

    [Fact]
    public async Task Stops_an_agent_loop_before_its_turn_cap()
    {
        var model = new CountingChatClient(tokensPerCall: 1_000, callsTool: true);
        var loop = new AgentLoop(new SpendingCap(model, maxTokens: 2_000), new FakeToolSource(), new ChatOptions(), maxTurns: 8);

        await Assert.ThrowsAsync<SpendingCapReachedException>(
            () => loop.RunAsync([new(ChatRole.User, "hi")], CancellationToken.None));

        Assert.Equal(2, model.Calls);
    }

    [Fact]
    public void The_session_cap_is_200000_tokens()
    {
        Assert.Equal(200_000, SpendingCap.TokensPerSession);
    }

    // Reports the same usage on every call. Told to, it asks for a tool every time, so a loop
    // never ends on its own.
    private sealed class CountingChatClient(long tokensPerCall, bool callsTool = false) : IChatClient
    {
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            var message = callsTool
                ? new ChatMessage(ChatRole.Assistant, [new FunctionCallContent($"call-{Calls}", "LookupArchetype",
                    new Dictionary<string, object?> { ["occupation"] = "innkeeper" })])
                : new ChatMessage(ChatRole.Assistant, "ok");
            return Task.FromResult(new ChatResponse(message) { Usage = new UsageDetails { TotalTokenCount = tokensPerCall } });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
