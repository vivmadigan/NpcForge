using Microsoft.Extensions.AI;

namespace NpcForge;

public static class ChatAgent
{
    // Build step 1: one message out, one reply back. No tools, no loop.
    public static async Task RunAsync(IChatClient client, ChatOptions options)
    {
        var response = await client.GetResponseAsync(
            "Introduce yourself in one sentence.", options);

        Console.WriteLine(response.Text);
    }
}
