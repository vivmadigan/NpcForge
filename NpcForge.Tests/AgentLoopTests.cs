using Microsoft.Extensions.AI;

namespace NpcForge.Tests;

// Two tests, one per rule from BUILD.md "The loop": the exit is the absence of tool
// calls, and the cap is not optional. A third runs the loop against the real MCP server
// (step 5). None touches the network: the models are local fakes, and the server is a
// child process on this machine.
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

    [Fact]
    public async Task Runs_a_tool_call_through_the_real_server()
    {
        await using var tools = new McpToolSource();
        await tools.ConnectAsync(CancellationToken.None);    // starts NpcForge.Server as a child process

        var loop = new AgentLoop(new CallsToolOnceChatClient(), tools, new ChatOptions());

        var text = await loop.RunAsync([new(ChatRole.User, "hi")], CancellationToken.None);

        // The server's sentence, word for word: the fake echoes whatever the tool returned.
        Assert.Equal("A typical innkeeper is busy, watchful, and knows everyone's business.", text);
    }
}
