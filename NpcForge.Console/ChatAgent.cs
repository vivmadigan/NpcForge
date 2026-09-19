using Microsoft.Extensions.AI;

namespace NpcForge;

// Build the loop with the tool source it is given, run it, print the answer. It never
// knows whether the tools are fake or MCP; that is IToolSource's job.

public static class ChatAgent
{
    public static async Task RunAsync(IChatClient client, ChatOptions options, IToolSource tools)
    {
        var loop = new AgentLoop(client, tools, options);

        // The caller owns the history. Step 6 puts the brief in this message; step 7 adds a
        // system message above it.
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "How does an innkeeper usually behave? Use the tool."),
        };

        var answer = await loop.RunAsync(history, CancellationToken.None);
        Console.WriteLine(answer);
    }
}
