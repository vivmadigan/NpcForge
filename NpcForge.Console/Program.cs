using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using NpcForge;
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonSerializer

// UTF-8 out, so curly quotes and dashes reach a terminal or a test as they were written (lesson 006).
Console.OutputEncoding = Encoding.UTF8;

// UTF-8 in as well, so a curly apostrophe the GM types reaches the quote check as itself and not
// as a "?". Piped input too: a scene piped from a UTF-8 file is UTF-8 bytes, and without this they
// are decoded in the console's OEM code page, so "’" arrives as "ÔÇÖ".
Console.InputEncoding = Encoding.UTF8;

// No host, no DI container. Everything is wired by hand, in the order it happens:
// config, then the saves, then the client, then the options, then the dice, then interview, then
// CharacterRun's roll, name and write, then the question whether to keep it (save, again or leave).
// "again" repeats CharacterRun with the same answers and scene. The core is called in-process
// (ADR-014): this file builds its dice and its store and hands them in.
// --list and --load come straight after the saves, before the client and the dice, and the model is
// never called. With --lab, the lab takes over after the dice and writes a sheet instead of a save. With
// no answer flag, the interview reads the GM's scene, and the roll waits for the GM's yes. The roll comes
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

// --score reads a finished blind lab and calls no model, so it runs before a client, the saves or the
// dice exist, and needs no key.
if (run.Score is not null)
{
    await Lab.ScoreAsync(run.Score, Console.In, Console.Error, Console.Out, CancellationToken.None);
    return;
}

// Where the recording will land, shown before anything is paid for, so a wrong working directory
// shows here and not after the run.
if (recordTo is not null)
    Console.Error.WriteLine($"[app] recording to {Path.GetFullPath(recordTo)}");

// The saves: one JSON file, the tracked NpcForge.Console/characters.json unless the run names another.
// A replay or a recording run saves to a temp file unless NPCFORGE_CHARACTERS says otherwise, so it
// never writes over the characters.json you keep (docs/architecture.md "Storage"). Nothing is read
// until a list, a load or a save asks.
var charactersPath = run.CharactersPath(Environment.GetEnvironmentVariable("NPCFORGE_CHARACTERS"));
ICharacterStore saves = new FileCharacterStore(charactersPath ?? FileCharacterStore.DefaultPath);

// List: one row per save, from the store, which hands over only each save's name and two fields of its
// brief. No model and no dice, so it comes before ModelClients.Create and needs no API key (ADR-013,
// decision 5). Rows go to stdout; the empty message goes to stderr with the other [app] lines, and an
// empty file is not a failure. A file that will not parse is (ADR-015, decision 4).
if (run.List)
{
    IReadOnlyList<ListedCharacter> listed;
    try
    {
        listed = await saves.ListAsync(CancellationToken.None);
    }
    catch (InvalidDataException ex)
    {
        Console.Error.WriteLine($"[app] not listed: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }

    var rows = SaveList.Rows(listed);
    if (rows.Count == 0)
        Console.Error.WriteLine("[app] nothing saved yet");
    foreach (var row in rows)
        Console.WriteLine(row);
    return;
}

// Load: a saved character comes back exactly as it was written. No roll and no model, so it
// costs nothing, and it comes before ModelClients.Create so it needs no API key (ADR-013,
// decision 5). A number that was never saved, or a file that will not parse, stops the run here
// with the reason and exit code 1, not a stack trace (decision 6).
if (load is not null)
{
    SavedCharacter? saved;
    try
    {
        saved = await saves.LoadAsync(load.Value, CancellationToken.None);
    }
    catch (InvalidDataException ex)
    {
        Console.Error.WriteLine($"[app] not loaded: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }

    if (saved is null)
    {
        Console.Error.WriteLine($"[app] not loaded: No character saved as {load}.");
        Environment.ExitCode = 1;
        return;
    }

    Console.Error.WriteLine($"[app] loaded {load}");
    // The brief as it was saved, every key and value in its order, written compact and with the
    // default escaping, the way the run that rolled it printed its own brief line. The file keeps it
    // indented and unescaped, so its raw text would read differently on screen (ADR-016).
    Console.Error.WriteLine($"[app] brief {JsonSerializer.Serialize(saved.Brief)}");
    if (SavedView.NameOf(saved) is { } savedName)
        Console.Error.WriteLine($"[app] name {savedName.Full}");

    // The brief line above is the save as it was written. This one says what a save made before a
    // field existed does not have, so a missing tone is not read as a plain one.
    var unknown = SavedView.Unknown(Dice.Fields, saved);
    if (unknown.Count > 0)
        Console.Error.WriteLine($"[app] unknown in save {load}: {string.Join(", ", unknown)}");

    // A null is a scene that left the field out (ADR-010), which is different from a key the save
    // never had: each field is in one of the two lines, never both.
    var notGiven = SavedView.NotGiven(Dice.Fields, saved);
    if (notGiven.Count > 0)
        Console.Error.WriteLine($"[app] not given in save {load}: {string.Join(", ", notGiven)}");

    Console.WriteLine(saved.Text);
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

// The dice: one Dice for the whole run, seeded by --seed or random. Every roll in the run casts from
// it, a lab's variants and each roll again included, so under --seed the next roll takes the next
// dice and a run with roll agains gives the same characters every time. A new Dice, or a re-seed,
// would roll the first character again (ADR-013, decision 3). Only --seed seeds it.
var dice = new Dice(seed);
Console.Error.WriteLine(seed is null ? "[app] dice: random" : $"[app] dice: seed {seed}");

// A roll the dice refuse and the spending cap each end the run here, once. The dice refuse a field they
// cannot read, from a flag, the interview or the lab, before any die is cast and before the writer is
// called. The cap refuses any call: an interview turn, or the namer or the writer on any roll again.
// Each roll again draws on the same SpendingCap, so a GM who keeps rolling reaches it. Nothing is saved
// either way, because only "save" saves and both come before the question (ADR-013, decision 7).
try
{
    // The lab is one visible branch: it rolls once and writes every variant through the same
    // ChatAgent.WriteAsync a normal run uses. It saves nothing, and asks before any model call.
    if (run.Lab is not null)
    {
        var ended = await Lab.RunAsync(client, options, dice, run, Console.In, CancellationToken.None);

        // As a normal run does when no name follows the dice: the lab has said why, and it is a failure.
        if (ended.NoName)
            Environment.ExitCode = 1;
        return;
    }

    // Every tone the dice can roll needs a file in the skill folder. A gap is found now, before the
    // interview and before the writer is paid for, and not when a roll lands on that tone (ADR-009).
    ToneFiles.Require(Dice.Fields, [WriterInput.DefaultSkillDir]);

    // The answers: from the flags, or from the GM's scene when no answer flag was given (ADR-007).
    // Either way a plain set of roll arguments. The interview never rolls; it only says what to roll
    // with, and only after the GM's yes.
    Dictionary<string, object?> rollArguments;
    // The GM's scene, for the writer (ADR-008). Null for a run with answer flags, which has none.
    string? scene = null;
    if (run.Interview)
    {
        // The fields come from the roll's own schema, so the console keeps no copy of them.
        var fields = Dice.Fields;

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

    // The Ask is what every roll in this run is made from: the same answers and the same scene each
    // time round. The scene is the GM's, or null for a run with answer flags.
    var ask = new Ask(rollArguments, scene, provider);

    // Roll, show, ask; "again" goes round. Every roll uses this run's one Dice and one client, so
    // under --seed the next roll takes the next dice and the characters come out the same every time.
    while (true)
    {
        // Roll, name and write are one step (CharacterRun): the app rolls the dice itself, prints
        // the brief, then the namer names the character and the writer writes it.
        Draft draft;
        try
        {
            draft = await CharacterRun.RollAndWriteAsync(client, options, dice, ask, CancellationToken.None);
        }
        catch (NoNameException ex)
        {
            // No name followed the dice, so the writer was never called: nothing is written or saved.
            Console.Error.WriteLine($"[app] not written: {ex.Message}");
            Environment.ExitCode = 1;
            return;
        }
        Console.WriteLine(draft.Written.Text);

        // Keep: the GM chooses, in every run (ADR-013, decisions 1 and 2). Only "save" saves, so only
        // the character on the screen when the GM types it is saved.
        switch (await KeepQuestion.AskAsync(Console.In, Console.Error, CancellationToken.None))
        {
            case Keep.Again:
                continue;
            case Keep.Save:
                int savedAs;
                try
                {
                    savedAs = await CharacterRun.SaveAsync(saves, draft, CancellationToken.None);
                }
                catch (InvalidDataException ex)
                {
                    // The character was written and shown, and the file it would join will not parse,
                    // so nothing is written over it (ADR-015, decision 4).
                    Console.Error.WriteLine($"[app] not saved: {ex.Message}");
                    Environment.ExitCode = 1;
                    return;
                }
                // Where, when this run chose the file, so a save in a temp file can be found.
                Console.Error.WriteLine(charactersPath is null
                    ? $"[app] saved as {savedAs}"
                    : $"[app] saved as {savedAs} in {charactersPath}");
                return;
            case Keep.Leave:
                Console.Error.WriteLine("[app] not saved");
                return;
            case Keep.EndOfInput:
                Console.Error.WriteLine("[app] not saved: end of input");
                return;
        }
    }
}
catch (RollRefusedException ex)
{
    // The reason names the field and what it takes, so the GM can mend the flag. Exit code 1 and no
    // stack trace: the dice refused, nothing faulted.
    Console.Error.WriteLine($"[app] not rolled: {ex.Reason}");
    Environment.ExitCode = 1;
}
catch (SpendingCapReachedException ex)
{
    // The cap's own message says how much was used. Exit code 1 and no stack trace: the run was
    // stopped on purpose, not by a fault.
    Console.Error.WriteLine($"[app] stopped: {ex.Message} Nothing was saved.");
    Environment.ExitCode = 1;
}
