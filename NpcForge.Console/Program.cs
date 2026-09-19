using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using NpcForge;

// No host, no DI container. Everything is wired by hand, in the order it happens:
// config, then the client, then the options, then connect, roll, run. The roll comes
// before the model's first turn, which is why the order has to stay visible.

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()          // API keys live here, never in appsettings or the repo
    .Build();

string provider = "openai";
string model = "gpt-5.6-terra";

// The answers. Defaults so a bare run works; flags for now. Occupation sits with the setting
// because they change together: a farm run wants --occupation as well as --setting.
string setting = "an inn in a major city";
string occupation = "innkeeper";
string want = "the name of a fence";
string difficulty = "Wall";

// Flags, read by hand: --provider openai|anthropic, --model <id>, and the three answers.
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--provider" && i + 1 < args.Length)
        provider = args[i + 1].ToLower();
    if (args[i] == "--model" && i + 1 < args.Length)
        model = args[i + 1].ToLower();
    if (args[i] == "--setting" && i + 1 < args.Length)
        setting = args[i + 1];
    if (args[i] == "--want" && i + 1 < args.Length)
        want = args[i + 1];
    if (args[i] == "--difficulty" && i + 1 < args.Length)
        difficulty = args[i + 1];
    if (args[i] == "--occupation" && i + 1 < args.Length)
        occupation = args[i + 1];
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

// Connect: start the server, before the model's first turn. Disposed when the run ends,
// which stops the server on purpose.
await using var tools = new McpToolSource();
await tools.ConnectAsync(CancellationToken.None);

// Roll: the app calls roll_character itself. The model never sees this tool, so it cannot
// skip the roll. The difficulty goes as text and the server turns it into the enum; a value
// it does not know stops the run here, before any model is called.
var briefJson = await tools.CallDirectAsync("roll_character", new()
{
    ["setting"] = setting,
    ["playersWant"] = want,
    ["difficulty"] = difficulty,
    ["occupation"] = occupation,
}, CancellationToken.None);

// Run.
await ChatAgent.RunAsync(client, options, tools, briefJson);
