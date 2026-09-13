using Microsoft.Extensions.AI;

namespace NpcForge;

// Step 4: build the loop, run it, print. The tool and the printing loop from steps 2 and 3
// have moved into FakeToolSource and AgentLoop; this file no longer knows about either.
public static class ChatAgent
{
    public static async Task RunAsync(IChatClient client, ChatOptions options)
    {
        var loop = new AgentLoop(client, new FakeToolSource(), options);

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
