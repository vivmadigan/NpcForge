using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using NpcForge;
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonDocument

// UTF-8 out, so curly quotes and dashes reach a terminal or a test as they were written (lesson 006).
Console.OutputEncoding = Encoding.UTF8;

// UTF-8 in as well, so a curly apostrophe the GM types reaches the quote check as itself and not
// as a "?". Piped input too: a scene piped from a UTF-8 file is UTF-8 bytes, and without this they
// are decoded in the console's OEM code page, so "’" arrives as "ÔÇÖ".
Console.InputEncoding = Encoding.UTF8;

// No host, no DI container. Everything is wired by hand, in the order it happens:
// config, then connect, then the client, then the options, then interview, then CharacterRun's roll, name
// and write, then the question whether to keep it (save or leave). CharacterRun is the one step a later
// change repeats.
// --load comes straight after connect, before the client, and the model is never called.
// With --lab, the lab takes over after the client and writes a sheet instead of a save. With no answer flag,
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

// Connect: start the server, before any model client is built. Disposed when the run ends,
// which stops the server on purpose.
// A replay or a recording run saves to a temp file unless NPCFORGE_CHARACTERS says otherwise, so
// it never writes over the characters.json you keep (docs/architecture.md "Storage").
var charactersPath = run.CharactersPath(Environment.GetEnvironmentVariable("NPCFORGE_CHARACTERS"));
await using var tools = new McpToolSource(charactersPath, seed);
await tools.ConnectAsync(CancellationToken.None);

// List: one row per save, from the server, which reads only each save's name and brief. No model, so
// it comes before ModelClients.Create and needs no API key (ADR-013, decision 5). Rows go to stdout;
// the empty message goes to stderr with the other [app] lines, and an empty file is not a failure.
if (run.List)
{
    var listed = SaveList.Rows(await tools.CallDirectAsync("list_characters", new(), CancellationToken.None));
    if (listed.Count == 0)
        Console.Error.WriteLine("[app] nothing saved yet");
    foreach (var row in listed)
        Console.WriteLine(row);
    return;
}

// Load: a saved character comes back exactly as it was written. No roll and no model, so it
// costs nothing, and it comes before ModelClients.Create so it needs no API key (ADR-013,
// decision 5). A number that was never saved stops the run here with the server's reason, and
// exit code 1, not a stack trace (decision 6).
if (load is not null)
{
    string savedJson;
    try
    {
        savedJson = await tools.CallDirectAsync("load_character", new()
        {
            ["number"] = load,
        }, CancellationToken.None);
    }
    catch (ToolRefusedException ex)
    {
        Console.Error.WriteLine($"[app] not loaded: {ex.Reason}");
        Environment.ExitCode = 1;
        return;
    }

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

    // A null is a scene that left the field out (ADR-010), which is different from a key the save
    // never had: each field is in one of the two lines, never both.
    var notGiven = SavedView.NotGiven(RollField.ListFrom(tools.SchemaOf("roll_character")), saved.RootElement);
    if (notGiven.Count > 0)
        Console.Error.WriteLine($"[app] not given in save {load}: {string.Join(", ", notGiven)}");

    Console.WriteLine(saved.RootElement.GetProperty("text").GetString());
    return;
}

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
    // required field the scene never mentions is asked for, not filled from a flag default (ADR-010,
    // decision 4); flag runs keep their defaults, in the else branch. A copy of the options, so
    // the interview's tool list never reaches the writer.
    var told = await SceneInterview.RunAsync(
        client, options.Clone(), fields, Console.In, Console.Error, CancellationToken.None);

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

// Roll, name and write are one step (CharacterRun): the app calls roll_character itself, prints the
// dice, then the namer names the character and the writer writes it. The Ask is what the run is made
// from; the scene is the GM's, or null for a run with answer flags.
var ask = new Ask(rollArguments, scene, provider);
Draft draft;
try
{
    draft = await CharacterRun.RollAndWriteAsync(client, options, tools, ask, CancellationToken.None);
}
catch (NoNameException ex)
{
    // No name followed the dice, so the writer was never called: nothing is written or saved.
    Console.Error.WriteLine($"[app] not written: {ex.Message}");
    Environment.ExitCode = 1;
    return;
}
Console.WriteLine(draft.Written.Text);

// Keep: the GM chooses, in every run (ADR-013, decisions 1 and 2). Only "save" calls save_character;
// "leave" and end of input keep nothing, and anything else asks again.
await KeepQuestion.AskAsync(tools, draft, charactersPath, Console.In, Console.Error, CancellationToken.None);

