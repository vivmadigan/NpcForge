using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace NpcForge;

public static class ChatAgent
{
    // Build step 1: one message out, one reply back. No tools, no loop.
    public static async Task RunAsync(IServiceProvider services)
    {
        var client = services.GetRequiredService<IChatClient>();
        var options = services.GetRequiredService<ChatOptions>();

        var response = await client.GetResponseAsync(
            "Introduce yourself in one sentence.", options);

        Console.WriteLine(response.Text);
    }
}
