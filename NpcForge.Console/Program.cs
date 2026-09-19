using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using NpcForge;

// No host, no DI container. Everything is wired by hand, in the order it happens:
// config, then the client, then the options, then connect to the server, then the run.
// Step 6 adds "roll the character" between connect and run, also before the model's
// first turn, which is why the order has to stay visible.

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()          // API keys live here, never in appsettings or the repo
    .Build();

string provider = "openai";
string model = "gpt-5.6-terra";

// Two flags, read by hand: --provider openai|anthropic and --model <id>.
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--provider" && i + 1 < args.Length)
        provider = args[i + 1].ToLower();
    if (args[i] == "--model" && i + 1 < args.Length)
        model = args[i + 1].ToLower();
}

// The only line that knows which vendor is behind the model. Everything after it sees IChatClient.
IChatClient client = ModelClients.Create(provider, model, config);

// Shared by every request. Step 4 fills in Tools from the tool source before the first turn.
var options = new ChatOptions
{
    ModelId = model,
    // No Temperature: claude-opus-5 and claude-sonnet-5 reject it with a 400.
    MaxOutputTokens = 5000,
};

// Start the server and connect, before the model's first turn.
await using var tools = new McpToolSource();
await tools.ConnectAsync(CancellationToken.None);

await ChatAgent.RunAsync(client, options, tools);
