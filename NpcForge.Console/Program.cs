using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using NpcForge;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

string provider = "openai";
string model = "gpt-5.6-terra";

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--provider" && i + 1 < args.Length)
        provider = args[i + 1].ToLower();
    if (args[i] == "--model" && i + 1 < args.Length)
        model = args[i + 1].ToLower();
}

IChatClient client = ModelClients.Create(provider, model, config);

var options = new ChatOptions
{
    ModelId = model,
    // No Temperature: claude-opus-5 and claude-sonnet-5 reject it with a 400.
    MaxOutputTokens = 5000,
};

await ChatAgent.RunAsync(client, options);
