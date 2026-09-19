using Microsoft.Extensions.AI;

namespace NpcForge;

// Build the loop with the tool source it is given, run it, print the answer. It never
// knows whether the tools are fake or MCP; that is IToolSource's job. The brief arrives
// already rolled: rolling is Program.cs's business, not the loop's.

public static class ChatAgent
{
    public static async Task RunAsync(IChatClient client, ChatOptions options, IToolSource tools, string briefJson)
    {
        var loop = new AgentLoop(client, tools, options);

        // The caller owns the history. The brief goes in once, here, and is never repeated;
        // step 7 adds a system message above it.
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, $"Write this character. The brief is settled; do not change it.\n\n{briefJson}"),
        };

        var answer = await loop.RunAsync(history, CancellationToken.None);
        Console.WriteLine(answer);
    }
}
