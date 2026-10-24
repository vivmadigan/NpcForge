using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using NpcForge;
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonDocument

// UTF-8 out, so curly quotes and dashes reach a terminal or a test as they were written (lesson 006).
Console.OutputEncoding = Encoding.UTF8;

// No host, no DI container. Everything is wired by hand, in the order it happens:
// config, then the client, then the options, then connect, roll, run, save. With --load,
// the load comes straight after connect and the model is never called. The roll comes
// before the model's first turn, which is why the order has to stay visible.

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()          // API keys live here, never in appsettings or the repo
    .Build();

string provider = "openai";
string model = "gpt-6-luna";

// The answers. Defaults so a bare run works; flags for now. Occupation sits with the setting
// because they change together: a farm run wants --occupation as well as --setting.
string setting = "an inn in a major city";
string occupation = "innkeeper";
string want = "the name of a fence";
string difficulty = "Wall";

// --load <number> prints a saved character instead of making a new one.
int? load = null;

// --seed <number> fixes the dice: the same seed rolls the same brief. It goes to the server, never
// to roll_character, whose parameters are only the brief fields a GM may set.
int? seed = null;

// --record <file> also writes every model call to a file. --recording <file> is the file the
// replay provider answers from, and goes with --provider replay only.
string? recordTo = null;
string? recording = null;

// Flags, read by hand: --provider openai|anthropic|replay, --model <id>, the four answers, and the
// ones above.
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
    if (args[i] == "--load")
    {
        // Without a number this would fall through to a paid run and a new save. Stop instead.
        if (i + 1 == args.Length)
            throw new ArgumentException("--load needs a number, for example --load 3.");
        load = int.Parse(args[i + 1]);
    }
    if (args[i] == "--seed" && i + 1 < args.Length)
        seed = int.Parse(args[i + 1]);
    if (args[i] == "--record")
    {
        // Without a file this would fall through to a paid run that records nothing. Stop instead.
        if (i + 1 == args.Length)
            throw new ArgumentException("--record needs a file, for example --record NpcForge.Tests/Fixtures/replay.jsonl.");
        recordTo = args[i + 1];
    }
    if (args[i] == "--recording" && i + 1 < args.Length)
        recording = args[i + 1];
}

// All of these stop the run before anything is paid for.
//
// A recording without a seed could never replay: the dice would roll a different brief, so the
// first message would differ.
if (recordTo is not null && seed is null)
    throw new ArgumentException("--record needs --seed, for example --seed 1.");

// --recording on its own would leave the provider on openai: a paid run that ignores the file.
if (recording is not null && provider != "replay")
    throw new ArgumentException("--recording only goes with --provider replay.");

// A replay must never write a recording, least of all over the one it is reading.
if (recordTo is not null && provider == "replay")
    throw new ArgumentException("--record cannot go with --provider replay: a replay never writes a recording.");

// Where the recording will land, shown before anything is paid for, so a wrong working directory
// shows here and not after the run.
if (recordTo is not null)
    Console.Error.WriteLine($"[app] recording to {Path.GetFullPath(recordTo)}");

// The only line that knows which vendor is behind the model. Everything after it sees IChatClient.
IChatClient client = ModelClients.Create(provider, model, config, recording);

// --record wraps the real client, and everything after this still sees one IChatClient. Line 1 of
// the file is this run's answers as flags: the values actually used, not what was typed, so a
// default that changes later cannot change what the replay test runs. No --provider and no
// --record among them: the replay test adds its own --provider.
if (recordTo is not null)
{
    string[] flags =
    [
        "--seed", $"{seed}",
        "--model", model,
        "--setting", setting,
        "--want", want,
        "--difficulty", difficulty,
        "--occupation", occupation,
    ];
    client = new RecordingChatClient(client, recordTo, flags);
}

// Shared by every request. Step 4 fills in Tools from the tool source before the first turn.
var options = new ChatOptions
{
    ModelId = model,
    // No Temperature: claude-opus-5 and claude-sonnet-5 reject it with a 400.
    MaxOutputTokens = 5000,
};

// Connect: start the server, before the model's first turn. Disposed when the run ends,
// which stops the server on purpose.
await using var tools = new McpToolSource(seed: seed);
await tools.ConnectAsync(CancellationToken.None);

// Load: a saved character comes back exactly as it was written. No roll and no model, so it
// costs nothing. A number that was never saved stops the run here, with the server's reason.
if (load is not null)
{
    var savedJson = await tools.CallDirectAsync("load_character", new()
    {
        ["number"] = load,
    }, CancellationToken.None);

    using var saved = JsonDocument.Parse(savedJson);
    Console.Error.WriteLine($"[app] loaded {load}");
    Console.Error.WriteLine($"[app] brief {saved.RootElement.GetProperty("brief").GetRawText()}");
    Console.WriteLine(saved.RootElement.GetProperty("text").GetString());
    return;
}

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

// The dice, made visible. Every trait in the writing should trace back to a value on this line,
// and any that does not was invented by the model.
Console.Error.WriteLine($"[app] {provider} {model}");
Console.Error.WriteLine($"[app] brief {briefJson}");

// Run.
var answer = await ChatAgent.RunAsync(client, options, tools, briefJson);
Console.WriteLine(answer);

// Save: every run is kept, the brief with the character written from it. The brief goes as a
// JSON object, not as the text the roll returned: save_character takes a CharacterBrief.
using var brief = JsonDocument.Parse(briefJson);
var savedAs = await tools.CallDirectAsync("save_character", new()
{
    ["brief"] = brief.RootElement,
    ["text"] = answer,
}, CancellationToken.None);

Console.Error.WriteLine($"[app] saved as {savedAs}");

