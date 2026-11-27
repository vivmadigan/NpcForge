using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// Two tests, one per rule in docs/architecture.md "The loop": the exit is the absence of tool
// calls, and the cap is not optional. A third runs the loop against the writer's real tool, the
// ConsoleTool over Archetypes.Lookup that CharacterRun gives the writer (step 5, then ADR-014). None
// touches the network: the models are local fakes.
public class AgentLoopTests
{
    [Fact]
    public async Task Returns_the_text_when_the_model_sends_no_tool_calls()
    {
        var loop = new AgentLoop(new NeverCallsToolChatClient(), new FakeToolSource(), new ChatOptions());

        var text = await loop.RunAsync([new(ChatRole.User, "hi")], CancellationToken.None);

        Assert.Equal("Done.", text);
    }

    [Fact]
    public async Task Throws_when_the_model_never_stops_asking_for_tools()
    {
        var loop = new AgentLoop(new AlwaysCallsToolChatClient(), new FakeToolSource(), new ChatOptions(), maxTurns: 3);

        await Assert.ThrowsAsync<TurnCapReachedException>(
            () => loop.RunAsync([new(ChatRole.User, "hi")], CancellationToken.None));
    }

    [Fact]
    public async Task Runs_a_tool_call_through_the_writers_tool()
    {
        var tools = new ConsoleTool(Archetypes.Lookup);

        var loop = new AgentLoop(new CallsToolOnceChatClient(), tools, new ChatOptions());

        var text = await loop.RunAsync([new(ChatRole.User, "hi")], CancellationToken.None);

        // The tool's sentence, word for word: the fake echoes whatever the tool returned.
        Assert.Equal("A typical innkeeper is busy, watchful, and knows everyone's business.", text);
    }

    // The fifth rule in docs/architecture.md "The loop": the loop sets its tools on a copy, so one
    // options object can serve every loop of a run and comes back as it went in.
    [Fact]
    public async Task Offers_the_model_its_tools_and_leaves_the_callers_options_as_they_were()
    {
        var model = new RecordsToolsChatClient();
        var options = new ChatOptions { ModelId = "stand-in" };

        await new AgentLoop(model, new FakeToolSource(), options).RunAsync([new(ChatRole.User, "hi")], CancellationToken.None);

        Assert.Equal(["LookupArchetype"], model.Offered);
        Assert.Null(options.Tools);
        Assert.Equal("stand-in", model.ModelId);
    }

    // Answers at once with no tool call, and keeps what the request offered.
    private sealed class RecordsToolsChatClient : IChatClient
    {
        public IReadOnlyList<string> Offered { get; private set; } = [];
        public string? ModelId { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Offered = [.. (options?.Tools ?? []).Select(tool => tool.Name)];
            ModelId = options?.ModelId;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Done.")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
