using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using NpcForge;
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonDocument

// UTF-8 out, so curly quotes and dashes reach a terminal or a test as they were written (lesson 006).
Console.OutputEncoding = Encoding.UTF8;

// UTF-8 in as well, so a curly apostrophe the GM types reaches the quote check as itself and not
// as a "?". Only at a real console: piped input is already bytes in whatever encoding it was sent.
if (!Console.IsInputRedirected)
    Console.InputEncoding = Encoding.UTF8;

// No host, no DI container. Everything is wired by hand, in the order it happens:
// config, then the client, then the options, then connect, interview, roll, name, write, save. With
// --lab, the lab takes over after connect and writes a sheet instead of a save. With --load,
// the load comes straight after connect and the model is never called. With no answer flag,
// the interview reads the GM's scene, and the roll waits for the GM's yes. The roll comes
// before the writer's first turn, which is why the order has to stay visible.

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

// --score reads a finished blind lab and calls no model, so it runs before a client or a server
// exists, and needs no key.
if (run.Score is not null)
{
    await Lab.ScoreAsync(run.Score, Console.In, Console.Error, Console.Out, CancellationToken.None);
    return;
}

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

// The lab is one visible branch: it rolls once and writes every variant through the same
// ChatAgent.WriteAsync a normal run uses. It saves nothing, and asks before any model call.
if (run.Lab is not null)
{
    var ended = await Lab.RunAsync(client, options, tools, run, Console.In, CancellationToken.None);

    // As a normal run does when no name follows the dice: the lab has said why, and it is a failure.
    if (ended.NoName)
        Environment.ExitCode = 1;
    return;
}

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
    if (SavedView.NameOf(saved.RootElement) is { } savedName)
        Console.Error.WriteLine($"[app] name {savedName.Full}");

    // The brief line above is the save exactly as it was written. This one says what a save made
    // before a field existed does not have, so a missing tone is not read as a plain one.
    var unknown = SavedView.Unknown(RollField.ListFrom(tools.SchemaOf("roll_character")), saved.RootElement);
    if (unknown.Count > 0)
        Console.Error.WriteLine($"[app] unknown in save {load}: {string.Join(", ", unknown)}");

    Console.WriteLine(saved.RootElement.GetProperty("text").GetString());
    return;
}

// Every tone the server can roll needs a file in the skill folder. A gap is found now, before the
// interview and before the writer is paid for, and not when a roll lands on that tone (ADR-009).
ToneFiles.Require(RollField.ListFrom(tools.SchemaOf("roll_character")), [WriterInput.DefaultSkillDir]);

// The answers: from the flags, or from the GM's scene when no answer flag was given (ADR-007).
// Either way a plain set of roll_character arguments. The interview never rolls; it only says
// what to roll with, and only after the GM's yes.
Dictionary<string, object?> rollArguments;
// The GM's scene, for the writer (ADR-008). Null for a run with answer flags, which has none.
string? scene = null;
if (run.Interview)
{
    // The fields come from the server's own schema, so the console keeps no copy of them.
    var fields = RollField.ListFrom(tools.SchemaOf("roll_character"));

    // A scene may leave a field out, and the writer then needs that field's not-given file. Only this
    // path can produce a null, so a flag run and a lab are not checked (ADR-010, decision 5). Found
    // now, before the greeting, so a gap costs nothing.
    NotGivenFiles.Require(fields, [WriterInput.DefaultSkillDir]);

    // The dialogue goes to stderr, so stdout stays the character text alone (lesson 007). The
    // flag defaults fill a required field the scene never mentions. A copy of the options, so
    // the interview's tool list never reaches the writer.
    var told = await SceneInterview.RunAsync(
        client, options.Clone(), fields, run.RollArguments(), Console.In, Console.Error, CancellationToken.None);

    // Anything but yes, end of input included: nothing is rolled, written or saved.
    if (told is null)
    {
        Console.Error.WriteLine("[app] not rolled: the GM did not say yes");
        return;
    }
    rollArguments = told.RollArguments;
    scene = told.Scene;
}
else
{
    // The parameter names come from the flag table (RunOptions.RollArguments), not from here.
    rollArguments = run.RollArguments();
}

// Roll: the app calls roll_character itself. The model never sees this tool, so it cannot
// skip the roll. The difficulty goes as text and the server turns it into the enum; a value
// it does not know stops the run here, before the writer is called. A trait the GM described
// goes in through the parameter roll_character already has, and only the rest is rolled.
var briefJson = await tools.CallDirectAsync("roll_character", rollArguments, CancellationToken.None);

// The dice, made visible. Every trait in the writing should trace back to a value on this line,
// and any that does not was invented by the model.
Console.Error.WriteLine($"[app] {provider} {model}");
Console.Error.WriteLine($"[app] brief {briefJson}");

// Shows in every run's record that the writer was given the scene. A count, not the text: the GM
// typed it, and it is on the screen above.
if (scene is not null)
    Console.Error.WriteLine($"[app] scene sent ({scene.Length} chars)");

// Run: the namer names the character inside its dice, then the writer writes it.
Written written;
try
{
    written = await ChatAgent.WriteAsync(
        client, options, tools, new WriterInput(briefJson, scene, WriterInput.DefaultSkillDir), CancellationToken.None);
}
catch (NoNameException ex)
{
    // No name followed the dice, so the writer was never called: nothing is written or saved.
    Console.Error.WriteLine($"[app] not written: {ex.Message}");
    Environment.ExitCode = 1;
    return;
}
var answer = written.Text;
Console.WriteLine(answer);

// Save: every run is kept, the brief with the character written from it. The brief goes as a
// JSON object, not as the text the roll returned: save_character takes a CharacterBrief.
using var brief = JsonDocument.Parse(briefJson);
var savedAs = await tools.CallDirectAsync("save_character", new()
{
    ["brief"] = brief.RootElement,
    // As the namer split it, so the save shows the count the name was checked on.
    ["name"] = JsonSerializer.SerializeToElement(new { syllables = written.Name.Syllables, family = written.Name.Family }),
    ["text"] = answer,
}, CancellationToken.None);

// Where, when this run chose the file, so a save in a temp file can be found.
Console.Error.WriteLine(charactersPath is null
    ? $"[app] saved as {savedAs}"
    : $"[app] saved as {savedAs} in {charactersPath}");

