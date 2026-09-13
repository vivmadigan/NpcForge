using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// Two tests, one per rule from BUILD.md "The loop": the exit is the absence of tool
// calls, and the cap is not optional. Neither touches the network; both fakes are local.
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => loop.RunAsync([new(ChatRole.User, "hi")], CancellationToken.None));
    }
}
