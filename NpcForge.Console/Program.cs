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

// The answers and every flag come from one table (RunOptions), which also writes line 1 of a
// recording. A bad combination of flags throws there, before anything is paid for.
var run = RunOptions.Parse(args);
string provider = run.Provider;
string model = run.Model;
int? load = run.Load;
int? seed = run.Seed;
string? recordTo = run.RecordTo;

// Where the recording will land, shown before anything is paid for, so a wrong working directory
// shows here and not after the run.
if (recordTo is not null)
    Console.Error.WriteLine($"[app] recording to {Path.GetFullPath(recordTo)}");

// The only line that knows which vendor is behind the model. Everything after it sees IChatClient.
IChatClient client = ModelClients.Create(provider, model, config, run.Recording);

// --record wraps the real client, and everything after this still sees one IChatClient. Line 1
// of the file is this run's answers as flags, from the same table the parser reads.
if (recordTo is not null)
    client = new RecordingChatClient(client, recordTo, run.RecordedFlags());

// Outermost, so every model call this session makes, interview and writer alike, counts against
// one allowance, and a refused call is never recorded or sent.
client = new SpendingCap(client, SpendingCap.TokensPerSession);

// Shared by every request. Step 4 fills in Tools from the tool source before the first turn.
var options = new ChatOptions
{
    ModelId = model,
    // No Temperature: claude-opus-5 and claude-sonnet-5 reject it with a 400.
    MaxOutputTokens = 5000,
};

// Connect: start the server, before the model's first turn. Disposed when the run ends,
// which stops the server on purpose.
// A replay or a recording run saves to a temp file unless NPCFORGE_CHARACTERS says otherwise, so
// it never writes over the characters.json you keep (docs/architecture.md "Storage").
var charactersPath = run.CharactersPath(Environment.GetEnvironmentVariable("NPCFORGE_CHARACTERS"));
await using var tools = new McpToolSource(charactersPath, seed);
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
// it does not know stops the run here, before any model is called. The parameter names come
// from the flag table (RunOptions.RollArguments), not from here.
var briefJson = await tools.CallDirectAsync("roll_character", run.RollArguments(), CancellationToken.None);

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

// Where, when this run chose the file, so a save in a temp file can be found.
Console.Error.WriteLine(charactersPath is null
    ? $"[app] saved as {savedAs}"
    : $"[app] saved as {savedAs} in {charactersPath}");

