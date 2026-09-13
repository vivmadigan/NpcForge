using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace NpcForge;

public static class ChatAgent
{
    [Description("Look up how a given kind of person usually behaves.")]
    static string LookupArchetype(
        [Description("An occupation, such as innkeeper or farmer")] string occupation)
        => $"A typical {occupation} is busy, watchful, and knows everyone's business.";

    public static async Task RunAsync(IChatClient client, ChatOptions options)
    {
        options.Tools = [AIFunctionFactory.Create(LookupArchetype)];

        var response = await client.GetResponseAsync(
            "How does an innkeeper usually behave? Use the tool.", options);

        Console.WriteLine($"FinishReason: {response.FinishReason}");
        foreach (var message in response.Messages)
        {
            Console.WriteLine($"-- {message.Role}");
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent text:
                        Console.WriteLine($"text: {text.Text}");
                        break;
                    case FunctionCallContent call:
                        Console.WriteLine($"tool call: {call.Name} id={call.CallId} " +
                                          $"args={JsonSerializer.Serialize(call.Arguments)}");
                        break;
                    default:
                        Console.WriteLine($"other: {content.GetType().Name}");
                        break;
                }
            }
        }
    }
}
