using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Text.Json;

namespace NpcForge;

public static class ChatAgent
{
    // The model never sees this C#. AIFunctionFactory reads the method by reflection and
    // builds the JSON it does see. The two [Description]s are the only prose in that JSON.
    [Description("Look up how a given kind of person usually behaves.")]
    static string LookupArchetype(
        [Description("An occupation, such as innkeeper or farmer")]
        string occupation)
    {
        return $"A typical {occupation} is busy, watchful, and knows everyone's business.";
    }

    public static async Task RunAsync(IChatClient client, ChatOptions options)
    {
        var tool = AIFunctionFactory.Create(LookupArchetype);
        options.Tools = [tool];

        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "How does an innkeeper usually behave? Use the tool."),
        };

        var first = await client.GetResponseAsync(history, options);
        history.AddRange(first.Messages);                   // the assistant's turn goes in as-is
        PrintResponse(first);

        var call = first.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .First();

        var result = await tool.InvokeAsync(new AIFunctionArguments(call.Arguments));   // you run it

        history.Add(new ChatMessage(ChatRole.Tool,
            [new FunctionResultContent(call.CallId, result)]));   // your answer, keyed by the call's id

        var second = await client.GetResponseAsync(history, options);
        PrintResponse(second);
    }

    // Step 2's printing loop, now called once per turn.
    static void PrintResponse(ChatResponse response)
    {
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
