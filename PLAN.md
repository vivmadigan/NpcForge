# Plan

Steps 9 to 13, from plan v04. [v04](docs/plans/v04-2026-09-26-checks-floor-checkpoint.md) says why
the steps come in this order. This file says what to do in each one and what the code looks like.
Steps 1 to 8 are done, and they are in [docs/plans/steps-1-to-8.md](docs/plans/steps-1-to-8.md)
with all their notes. Tick things off here.

Tags: 🧩 you write · 📖 reference code from Claude, you adapt it · 🤝 pair on it in chat

Code blocks paste in. Each one says which file it goes in, starts with its `using` lines when it is
a whole file, and names the code it replaces when it is an edit. Step 9's blocks were built and run
in a throwaway copy of the repo on 2026-10-01, including the checks that should make them fail. The
compiler is still the final word.

## Decisions already made

The live list. A line links to its full record in `docs/decisions/` when one exists.

| Decision | Choice | Why |
| --- | --- | --- |
| Model access | `IChatClient` (Microsoft.Extensions.AI), both providers behind `--provider` | The loop gets written once and tested against two vendors. That is what makes the abstraction real rather than theoretical. |
| `IChatModel` from BUILD.md | Not written. `IChatClient` *is* it. | BUILD.md allows this. `Message` → `ChatMessage`, `ModelReply` → `ChatResponse`. |
| `IToolSource` from BUILD.md | Still yours to write | It is the seam that swaps a fake tool (step 4) for MCP (step 5) without touching the loop. `ToolDefinition` → `AITool`, `ToolCall` → `FunctionCallContent`. |
| `UseFunctionInvocation` | Off until step 4 is done | It runs the tool loop for you. Steps 2–4 are about writing it. |
| Host / DI container | None. `Program.cs` wires by hand: config → client → options → run. Stripped 2026-09-12. | Step 5's `ConnectAsync` is async setup, and step 6's "roll before the model's first turn" has to be visible, in order. A container hides both. |
| `AgentLoop.RunAsync` | Takes `List<ChatMessage>`, not `string opening` as in BUILD.md. `ChatOptions` comes in through the constructor. | Step 7 puts a system message at index 0. The caller builds the history; the loop never changes. |
| Skills *(assumed)* | `SKILL.md` files in the repo, read by the app into the system prompt | Works with both providers. Anthropic's server-side Skills feature would tie the app to one. |
| Storage | One JSON file in the server, `characters.json`, holding every run's brief and written character, numbered in order. Decided 2026-09-22. | BUILD.md allows file or SQLite; a file is less to learn and the brief is already JSON. The text is saved as well as the brief because the brief has no name and the model writes differently every run. A database later changes only the server: the app never sees the file. Departs from README build order 4 and BUILD.md step 8 ("load them again by name"): every run is saved, not only the ones worth keeping, and a save is loaded by number, because the name lives inside the model's text. See [ADR-003](docs/decisions/ADR-003-saved-character-is-brief-and-text.md). |
| Tests | Small xUnit project, first appears at step 4 | The cap and the exit condition are the first things worth a test. Nothing before that is testable without spending money. |
| OpenAI endpoint | Responses (`OpenAI.Responses.ResponsesClient`), not Chat Completions. Decided 2026-09-13. | `gpt-5.6-terra` rejects function tools on Chat Completions when reasoning is on (HTTP 400: "use /v1/responses or set reasoning_effort to 'none'"). Forcing effort to none would put a vendor workaround on options shared with Claude and switch off reasoning. Responses is marked experimental (`OPENAI001`), suppressed in `ModelClients.cs`. |
| Traces | stderr, with a bracketed prefix: `[tool]` from the app's tool source, `[server]` from the MCP server. Decided 2026-09-13. Step 7 adds three more on the same channel: `[app]` for the provider, model and the rolled brief, `[skill]` for which file was loaded, `[loop]` for one line per model turn. Step 8 adds `[app] saved as N` after a run and `[app] loaded N` on a load. Step 9 adds `[server] dice: seed N` or `dice: random`. | Stdout is the answer, and on the server it is the transport. Same channel and a prefix per side, so the two read as one trace. With the step 7 lines a plain run explains itself: every trait in the writing can be checked against the `[app] brief` line without a debugger. |
| Starting the server | `dotnet run --project <server> --no-build`, the path built from `AppContext.BaseDirectory`, the build order set by a `ProjectReference` with `ReferenceOutputAssembly="false"`. Decided 2026-09-19. | Works the same from the app and the tests, never starts a stale server, and shares no types. Dropping `--no-build` would rebuild on every launch and risk build output on stdout, which is the wire. |
| Recording paid runs | One file per run in `docs/runs/`, captured by pasting the console, each standing on its own: commit before the run, and the `[skill]` line carries a content fingerprint. No index; observations live in `PLAN.md`'s step notes. Decided 2026-09-20. See [docs/runs/README.md](docs/runs/README.md) "Why not". From step 9, a run made with `--record` also has its transcript file, kept beside the paste, not instead of it. Decided 2026-10-01. | Runs cost money and are the only evidence for whether a skill edit helped. Redirecting reorders the trace against the answer, and a `git diff` between two SHAs is empty in the normal case, because tuning happens against a dirty tree. The transcript holds the model's side byte for byte but none of the stderr trace, so the paste is still needed. |
| Occupation | Supplied like the setting: `--occupation`, default `"innkeeper"` next to the setting default in `Program.cs`. Not rolled. Decided 2026-09-19. See [ADR-002](docs/decisions/ADR-002-occupation-in-the-brief.md). | Three live runs let the model choose who the character is (an innkeeper twice, a patron once), and the README's success test asks for innkeepers. A table on the server cannot see the setting, so a rolled default would put "innkeeper" into a farm's brief as settled fact (the challenger). Departs from the README ("not one of the questions") and from BUILD.md's brief, which is step 8's save format. |
| Replay | Seeded dice (`NPCFORGE_SEED`, never a `roll_character` parameter), `--record` to a JSONL file, `--provider replay` answers from it, and a free test runs the whole app against it. Re-recorded only in feature steps, never in refactor steps. Decided 2026-10-01. See [ADR-005](docs/decisions/ADR-005-replay-a-recorded-run.md). | Steps 10 and 12 restructure a whole process each. "Behaviour unchanged" has to be a test result, not a paid run judged by eye. |

## Names you will meet

| Name | What it is | Step |
| --- | --- | --- |
| `DelegatingChatClient` | A chat client that wraps another one and passes every call through. Override `GetResponseAsync` to do something on the way. This is middleware. | 9 |
| `AIJsonUtilities.DefaultOptions` | The library's JSON settings. They know every kind of message content, and write a `$type` so it comes back as the same kind. They indent by default. | 9 |
| JSONL | A text file with one JSON value per line. | 9 |
| `ProcessStartInfo.ArgumentList` | A process's arguments one at a time, so a value with spaces needs no quoting. | 9 |
| `JsonNode.DeepEquals` | Compares two pieces of JSON by what they say, not how they are laid out. | 9 |
| Launch profile | A named way to start the app from the IDE, with its own arguments and environment. Lives in `Properties/launchSettings.json`. | 9 |

## The steps at a glance

| Step | What | Kind | Paid runs |
| --- | --- | --- | --- |
| 9 | A run you can replay for free | harness | 1, the recording |
| 10 | The server in layers | refactor | none |
| — | Checkpoint: did the server layers earn their place? | review, no code | none |
| 11 | Tone, importance and name dice | feature | about 7 |
| 12 | The console in layers, and a harder harness | refactor + harness | 1 |
| 13 | The scene interview | feature | about 7, on two vendors |

Two rules hold for every step:
- **A refactor and a feature never share a step.** A step that moves code *and* adds tone cannot tell
  a refactoring bug from a tone bug.
- **Refactor steps pass the replay test unchanged and never re-record.** Feature steps re-record
  once, after the change has settled, in the same commit as the change.

Each step runs the same way: a sketch, `crew:challenger` before you type, you write it,
`crew:test-runner`, the debugger, then `/crew:step-done`.

## Steps

### 9. A run you can replay for free — harness

**What**
- `--seed <number>` fixes the dice. The same seed rolls the same character every time.
- `--record <file>` makes a normal paid run, and also writes every message sent to the model, and
  every reply, to that file.
- `--provider replay --recording <file>` answers from the file instead of calling OpenAI. It is
  free. If the app sends anything different from what was recorded, it stops with an error naming
  the line that differs.
- A test runs the whole app against the recording, free, and checks what it saved.

**Why** — Steps 10 and 12 move every class in each process. Today the only proof that a run still
behaves is a paid run judged by eye. A recorded run you can replay for free turns "behaviour
unchanged" into a test result.

**Learning goal** — How an LLM app gets tested without paying: dice you can fix, a model you can
record and replay, and `IChatClient` middleware. Step 10's MediatR behaviours have the same shape.

**Decided** (full account in [ADR-005](docs/decisions/ADR-005-replay-a-recorded-run.md))
- The seed reaches the server through its environment, like the save path. It is never a
  `roll_character` parameter: that tool's parameters are only the brief fields a DM may set.
- The replay compares every message in the history, in order, with the model id, the output cap,
  and each tool's name, description and schema. The model's own messages are included: they came
  out of the file on both sides, so they match whenever the app puts them back. The challenger
  (WEAKENED, 2026-10-01) showed that leaving them out let `AgentLoop` drop the model's turn from the
  history with every test still passing. ADR-005 still says they are left out; that line is wrong.
- The test checks the save file, not what was printed (lesson 006).
- The test's save file reaches the server by inheritance: the test sets `NPCFORGE_CHARACTERS` on the
  console, and the server inherits it. No new flag. Decided 2026-10-01.
- What the test expects comes from the recording itself: the text of the last reply and the brief
  in the first message. Decided 2026-10-01.
- `--record` needs `--seed`, and line 1 of the file holds the values the run actually used, without
  `--provider` or `--record`. Found while checking the sketch, 2026-10-01: a replay that reused
  `--record` would empty the file it was reading.

#### 9a. Seeded dice — free

**`NpcForge.Server/CharacterTools.cs`** — replace `Pick` and the two comment lines above it with:

```csharp
        // The dice: the whole of the variety, and the model cannot reach them. An empty table
        // throws here, and the app's CallDirectAsync turns that into a stopped run.
        private static T Pick<T>(IReadOnlyList<T> table) => table[Dice.Next(table.Count)];

        // With NPCFORGE_SEED set, the same seed rolls the same brief every time, because
        // RollCharacter always picks in the same order. Without it, Random.Shared, as before.
        // Made once per server process, and the console starts a new server for every run.
        private static readonly Random Dice = CreateDice();

        private static Random CreateDice()
        {
            var seed = Environment.GetEnvironmentVariable("NPCFORGE_SEED");

            // stderr, never stdout: stdout is the wire. The console shows it as [server] dice ...
            Console.Error.WriteLine(seed is null ? "dice: random" : $"dice: seed {seed}");

            return seed is null ? Random.Shared : new Random(int.Parse(seed));
        }
```

**`NpcForge.Console/McpToolSource.cs`** — three edits.

Replace `public McpToolSource(string? charactersPath = null) => _charactersPath = charactersPath;` with:

```csharp
        // The seed for the server's dice. Null leaves them random.
        private readonly int? _seed;

        public McpToolSource(string? charactersPath = null, int? seed = null)
        {
            _charactersPath = charactersPath;
            _seed = seed;
        }
```

In `ConnectAsync`, just above `// Nothing starts yet: this only describes the process.`, add:

```csharp
            // What the server is told, through its environment. Only the settings that were given:
            // the server inherits everything this process has, and a null value here would not
            // mean "leave it", it would delete the variable. The replay test sets
            // NPCFORGE_CHARACTERS on the console and relies on the server inheriting it.
            var environment = new Dictionary<string, string?>();

            if (_charactersPath is not null)
                environment["NPCFORGE_CHARACTERS"] = _charactersPath;

            if (_seed is not null)
                environment["NPCFORGE_SEED"] = _seed.ToString();

```

In the transport options, replace the `EnvironmentVariables = _charactersPath is null ...` lines and
the two comment lines above them with:

```csharp
                // Storage.cs and the dice read these. An environment variable reaches the child
                // process before any MCP message does, and survives the hop through dotnet run.
                EnvironmentVariables = environment,
```

**`NpcForge.Tests/McpToolSourceTests.cs`** — add above `// A fresh file per test`:

```csharp
    [Fact]
    public async Task The_same_seed_rolls_the_same_brief()
    {
        // Two servers, one after the other, both given seed 1. If the seed never reached the
        // dice, two briefs would match about one time in six million.
        var first = await RollWithSeed(1);
        var second = await RollWithSeed(1);

        Assert.Equal(first, second);
    }

    // A server of its own each time, so the second roll cannot be the first one's dice carrying on.
    private static async Task<string> RollWithSeed(int seed)
    {
        await using var tools = new McpToolSource(seed: seed);
        await tools.ConnectAsync(CancellationToken.None);
        return await tools.CallDirectAsync("roll_character", RollArgs(), CancellationToken.None);
    }
```

**Check it** — `dotnet test`: nine tests pass. Then put `//` in front of
`environment["NPCFORGE_SEED"] = _seed.ToString();` and run it again: the new test fails with
"Strings differ". Remove the `//`.

**In the debugger** — run the new test with Debug from Test Explorer. Break on
`Assert.Equal(first, second)` and look at `first` and `second` in Locals: two separate servers, and
the same brief, trait for trait.

#### 9b. Record and replay — code only, free

Three new files in `NpcForge.Console`, then edits to `ModelClients.cs` and `Program.cs`.

**`NpcForge.Console/Recording.cs`** — the file format, shared by the recorder and the replayer:

```csharp
using Microsoft.Extensions.AI;
using System.Text;                      // StringBuilder
using System.Text.Json;                 // JsonElement, JsonSerializer, JsonSerializerOptions

namespace NpcForge;

// The file --record writes and --provider replay reads. Line 1 is the run's flags. Every line
// after it is one model call: what the app sent, and what came back.
public sealed record RecordedCall(RecordedRequest Request, ChatResponse Response);

// What the app sent on one call.
public sealed record RecordedRequest(
    List<ChatMessage> Messages,
    string? ModelId,
    int? MaxOutputTokens,
    List<RecordedTool> Tools)
{
    // Copied out of the live call. The tools are written out by hand because the library never
    // serialises ChatOptions.Tools: left to it, a lost tool list would replay as if nothing changed.
    public static RecordedRequest From(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        var tools = new List<RecordedTool>();
        foreach (var tool in (options?.Tools ?? []).OfType<AIFunction>())
            tools.Add(new RecordedTool(tool.Name, tool.Description, tool.JsonSchema));

        return new RecordedRequest([.. messages], options?.ModelId, options?.MaxOutputTokens, tools);
    }
}

// One tool as the model was shown it: everything the model knows about a tool.
public sealed record RecordedTool(string Name, string Description, JsonElement Schema);

public static class Recording
{
    // The library's own JSON settings, which know every kind of message content. Its default is
    // indented, and a JSONL file needs one call per line, so this copy writes on one line.
    public static readonly JsonSerializerOptions Json = new(AIJsonUtilities.DefaultOptions)
    {
        WriteIndented = false,
    };

    // The part of a request a replay compares, as text you can read in the debugger and in a
    // failed test: the model id, the output cap, the tools, and every message in order.
    public static string Describe(RecordedRequest request)
    {
        var text = new StringBuilder();
        text.AppendLine($"model: {request.ModelId}, max output tokens: {request.MaxOutputTokens}");

        foreach (var tool in request.Tools)
            text.AppendLine($"tool: {tool.Name}, {tool.Description}, {JsonSerializer.Serialize(tool.Schema, Json)}");

        // The model's messages too. On both sides they came out of the recording, so they match
        // whenever the app puts them back in the history. The point is to fail when it does not:
        // "Skip one and the next request is malformed" (BUILD.md "The loop").
        foreach (var message in request.Messages)
        {
            foreach (var content in message.Contents)
            {
                text.AppendLine(content switch
                {
                    TextContent t => $"{message.Role}: {t.Text}",
                    FunctionCallContent c => $"{message.Role} {c.CallId}: asks for {c.Name}",
                    FunctionResultContent r => $"{message.Role} {r.CallId}: {r.Result}",
                    _ => $"{message.Role}: {content.GetType().Name}",
                });
            }
        }

        // SKILL.md is LF in git and CRLF on a Windows disk. Without this, a fresh checkout on
        // another machine would fail the replay on line endings alone.
        return text.ToString().ReplaceLineEndings("\n");
    }
}
```

**`NpcForge.Console/RecordingChatClient.cs`** — the middleware:

```csharp
using Microsoft.Extensions.AI;
using System.Text.Json;                 // JsonSerializer

namespace NpcForge;

// Middleware. Every call passes through to the real client unchanged, and what the app sent
// and what came back are written to a file, one line per model call. Everything around it still
// sees one IChatClient, so neither ChatAgent nor AgentLoop knows it is there.
public sealed class RecordingChatClient : DelegatingChatClient
{
    private readonly string _path;

    // flags: this run's answers as command-line flags. They go on line 1, so the replay test can
    // make the same run again without anyone typing them twice.
    public RecordingChatClient(IChatClient inner, string path, string[] flags) : base(inner)
    {
        _path = path;

        // A new recording replaces the old one: WriteAllText creates the file or empties it.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(_path, JsonSerializer.Serialize(flags, Recording.Json) + "\n");
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Copied before the call: AgentLoop goes on adding to the same history afterwards.
        var request = RecordedRequest.From(messages, options);

        // The real model call. The base class hands it to the client this one wraps.
        var response = await base.GetResponseAsync(messages, options, cancellationToken);

        // Written as soon as it arrives, so a run that fails later still keeps what it paid for.
        var line = JsonSerializer.Serialize(new RecordedCall(request, response), Recording.Json);
        File.AppendAllText(_path, line + "\n");

        return response;
    }
}
```

**`NpcForge.Console/ReplayChatClient.cs`** — the free model:

```csharp
using Microsoft.Extensions.AI;
using System.Text.Json;                 // JsonSerializer

namespace NpcForge;

// A model that answers from a recording instead of a provider: free, offline, and the same every
// time. Each call first checks that the app sent what it sent when the recording was made, and
// stops the run if not. That stop is the regression test.
public sealed class ReplayChatClient : IChatClient
{
    private readonly List<RecordedCall> _calls = [];
    private int _next;

    public ReplayChatClient(string path)
    {
        // Line 1 is the flags, which only the test reads. Every line after it is one model call.
        foreach (var line in File.ReadLines(path).Skip(1))
            _calls.Add(JsonSerializer.Deserialize<RecordedCall>(line, Recording.Json)!);
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (_next == _calls.Count)
            throw new InvalidOperationException($"The recording has {_calls.Count} model calls and the app made another.");

        var recorded = _calls[_next];
        _next++;

        // The same description of both sides, so the two strings are what you compare in the debugger.
        var expected = Recording.Describe(recorded.Request);
        var actual = Recording.Describe(RecordedRequest.From(messages, options));

        if (actual != expected)
        {
            // Name the first line that differs, so a one-word change in SKILL.md is easy to find.
            var was = expected.Split('\n');
            var now = actual.Split('\n');
            var at = 0;
            while (at < was.Length && at < now.Length && was[at] == now[at])
                at++;

            throw new InvalidOperationException(
                $"Model call {_next} differs from the recording at line {at + 1}.\n" +
                $"  recorded: {(at < was.Length ? was[at] : "(nothing)")}\n" +
                $"  now:      {(at < now.Length ? now[at] : "(nothing)")}");
        }

        return Task.FromResult(recorded.Response);
    }

    // The interface has four members. Only GetResponseAsync matters here; these exist because it says so.
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

**`NpcForge.Console/ModelClients.cs`** — two edits.

Replace the `Create` signature line with:

```csharp
    public static IChatClient Create(string provider, string model, IConfiguration config, string? recording = null) => provider switch
```

Just above `_ => throw new ArgumentException($"Unknown provider: {provider}"),` add:

```csharp
        // Free: answers from a file made with --record. No key and no network.
        "replay" => new ReplayChatClient(recording
            ?? throw new ArgumentException("--provider replay needs --recording <file>.")),

```

**`NpcForge.Console/Program.cs`** — five edits, top to bottom.

1. Replace `using System.Text.Json;                 // JsonDocument` with:

```csharp
using System.Text;                      // Encoding
using System.Text.Json;                 // JsonDocument

// UTF-8 out, so curly quotes and dashes reach a terminal or a test as they were written (lesson 006).
Console.OutputEncoding = Encoding.UTF8;
```

2. Replace `int? load = null;`, the blank line and the `// Flags, read by hand: ...` comment with:

```csharp
int? load = null;

// --seed <number> fixes the dice: the same seed rolls the same brief. It goes to the server, never
// to roll_character, whose parameters are only the brief fields a DM may set.
int? seed = null;

// --record <file> also writes every model call to a file. --recording <file> is the file the
// replay provider answers from.
string? recordTo = null;
string? recording = null;

// Flags, read by hand: --provider openai|anthropic|replay, --model <id>, the four answers, and the
// ones above.
```

3. Replace the end of the `for` loop, from `load = int.Parse(args[i + 1]);` to the loop's closing
   brace, with:

```csharp
        load = int.Parse(args[i + 1]);
    }
    if (args[i] == "--seed" && i + 1 < args.Length)
        seed = int.Parse(args[i + 1]);
    if (args[i] == "--record" && i + 1 < args.Length)
        recordTo = args[i + 1];
    if (args[i] == "--recording" && i + 1 < args.Length)
        recording = args[i + 1];
}

// A recording without a seed could never replay: the dice would roll a different brief, so the
// first message would differ. Stop before anything is paid for.
if (recordTo is not null && seed is null)
    throw new ArgumentException("--record needs --seed, for example --seed 1.");
```

4. Replace `IChatClient client = ModelClients.Create(provider, model, config);` with:

```csharp
IChatClient client = ModelClients.Create(provider, model, config, recording);

// --record wraps the real client, and everything after this still sees one IChatClient. Line 1 of
// the file is this run's answers as flags: the values actually used, not what was typed, so a
// default that changes later cannot change what the replay test runs. No --provider and no
// --record: the test adds its own, and a replay must never write into the file it is reading.
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
```

5. Replace `await using var tools = new McpToolSource();` with:

```csharp
await using var tools = new McpToolSource(seed: seed);
```

**Check it, free** — `dotnet build` is clean and `dotnet test` still passes nine. Then two runs that
stop before anything is paid for:
- `dotnet run --project NpcForge.Console -- --record x.jsonl` stops with "--record needs --seed".
- `dotnet run --project NpcForge.Console -- --provider replay` stops with "--provider replay needs
  --recording".

#### 9c. The recording — one paid run

1. Commit first (`docs/runs/README.md`: a run's SHA only means something on a clean tree).
2. From the repo root, in a terminal rather than F5, because the path is relative to where you start:

   ```text
   dotnet run --project NpcForge.Console -- --record NpcForge.Tests/Fixtures/replay.jsonl --seed 1 --provider openai --model gpt-6-luna --setting "an inn in a major city" --want "the name of a fence" --difficulty Wall --occupation innkeeper
   ```

3. In the trace: `[server] dice: seed 1`, then the run as usual. It also saves to your
   `characters.json` as every run does.
4. Open `NpcForge.Tests/Fixtures/replay.jsonl`. Line 1 is the flags, then one line per `[loop]` turn.
5. Paste the console into `docs/runs/` as usual, and name the fixture in it as the run's transcript.
6. Add the command above to `.claude/rules/team.md` under "Commands", marked paid. It is the only
   place it is typed from now on.
7. Commit the fixture with the run file.

If the run fails partway, the fixture is already emptied: `--record` starts a fresh file before the
model is called. `git restore NpcForge.Tests/Fixtures/replay.jsonl` brings the last good one back,
which is one more reason to commit first.

#### 9d. The replay test — free

**`NpcForge.Tests/ReplayTests.cs`** — new file:

```csharp
using Microsoft.Extensions.AI;
using System.Diagnostics;               // Process, ProcessStartInfo
using System.Text.Json;
using System.Text.Json.Nodes;           // JsonNode

namespace NpcForge.Tests;

// The whole app, run as a process against a recorded model: free, and no network. A black box on
// purpose, so it still holds when step 12 moves every class in the console. It checks the save
// file, not what was printed: the save is exact, and a child process's stdout is not (lesson 006).
public class ReplayTests
{
    [Fact]
    public async Task Replays_the_recorded_run_and_saves_the_same_character()
    {
        // Four folders up from this test's bin is the repo, as McpToolSource finds the server.
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var fixture = Path.Combine(repo, "NpcForge.Tests", "Fixtures", "replay.jsonl");
        var saveFile = Path.Combine(Path.GetTempPath(), $"npcforge-replay-{Guid.NewGuid():N}.json");

        // Line 1 is the flags the run was recorded with. The test adds the replay provider.
        var lines = File.ReadAllLines(fixture);
        var flags = JsonSerializer.Deserialize<string[]>(lines[0])!;

        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(Path.Combine(repo, "NpcForge.Console"));
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--no-launch-profile");
        start.ArgumentList.Add("--");
        foreach (var flag in flags)
            start.ArgumentList.Add(flag);
        start.ArgumentList.Add("--provider");
        start.ArgumentList.Add("replay");
        start.ArgumentList.Add("--recording");
        start.ArgumentList.Add(fixture);

        // The console never names a save file, so the server inherits this one from it.
        start.Environment["NPCFORGE_CHARACTERS"] = saveFile;

        try
        {
            using var process = Process.Start(start)!;

            // Read both streams while it runs. Left unread, the [server] and [loop] lines fill
            // the pipe and the app stops, waiting for someone to read them.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            // A minute is far more than a replay needs. A hang fails the test instead of the test
            // run never ending.
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }

            // On failure, the app's own trace is the message: the replay names the line that differs.
            Assert.True(process.ExitCode == 0, await stderr);

            // What the recorded run saved: the text of the model's last reply, and the brief from
            // the first message. Read with the library's own types, not the console's, so step 12
            // can move every console class without touching this test.
            var lastCall = JsonNode.Parse(lines[^1])!;
            var lastReply = lastCall["response"].Deserialize<ChatResponse>(AIJsonUtilities.DefaultOptions)!;

            var firstCall = JsonNode.Parse(lines[1])!;
            var firstMessages = firstCall["request"]!["messages"].Deserialize<List<ChatMessage>>(AIJsonUtilities.DefaultOptions)!;
            var userText = firstMessages.First(m => m.Role == ChatRole.User).Text;
            var recordedBrief = JsonNode.Parse(userText[userText.IndexOf('{')..]);   // the brief is the JSON at its end

            var saved = JsonNode.Parse(File.ReadAllText(saveFile))!["1"]!;

            Assert.Equal(lastReply.Text, saved["text"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(recordedBrief, saved["brief"]), $"Saved brief: {saved["brief"]}");
        }
        finally
        {
            File.Delete(saveFile);
        }
    }
}
```

**Check it** — `dotnet test`: ten tests pass, with no network. Then the two changes that must make
it fail, one at a time, undoing each after:
- Change one word in `SKILL.md`. The test fails with "Model call 1 differs from the recording at
  line N" and shows the recorded line and the new one.
- In `AgentLoop.cs`, change `options.Tools = [.. await tools.ListAsync(ct)];` to
  `options.Tools = [];`. The test fails at line 2: the recorded `tool: lookup_archetype` line,
  against nothing.
- In `AgentLoop.cs`, put `//` in front of `history.AddRange(response.Messages);`. The test fails at
  model call 2: the recorded model's turn, against the tool result that now follows the brief.

**In the debugger** — the test starts the app as a separate process, so a breakpoint in
`ReplayChatClient` never hits from Test Explorer. Run the app itself against the recording instead.
It is free. Add **`NpcForge.Console/Properties/launchSettings.json`**:

```json
{
  "profiles": {
    "NpcForge.Console": {
      "commandName": "Project"
    },
    "Replay (free)": {
      "commandName": "Project",
      "commandLineArgs": "--provider replay --recording ..\\..\\..\\..\\NpcForge.Tests\\Fixtures\\replay.jsonl --seed 1 --model gpt-6-luna --setting \"an inn in a major city\" --want \"the name of a fence\" --difficulty Wall --occupation innkeeper",
      "environmentVariables": {
        "NPCFORGE_CHARACTERS": "replay\\characters.json"
      }
    }
  }
}
```

The first profile is the normal run, so F5 and `dotnet run` behave as before. The profile types the
fixture's flags a second time. After a re-record with different flags (step 11 adds `--tone` and
`--importance`), copy line 1 of the fixture into it, or it stops at model call 1. Pick "Replay (free)"
from the dropdown next to the Start button. The app starts in `bin\Debug\net10.0`, which is what the
four `..` count from, and its saves go to `bin\Debug\net10.0\replay\`, not into your characters.
The save path needs a folder in it: a bare file name makes `Storage.Save` fail on
`Directory.CreateDirectory("")`.

Breakpoints, and what you should see:
- `ReplayChatClient.GetResponseAsync`, on `if (actual != expected)`: open `expected` and `actual`
  with the Text Visualizer. They hold the same text: the model line, the tool line, the skill, the
  brief, and on the second call the model's turn and the tool result as well. `_next` is 1 on the
  first call and 2 on the second.
- `AgentLoop`, after `var calls = ...` on turn 1: a `FunctionCallContent` that came out of the
  file, not from OpenAI. `McpToolSource` still runs it against the real server.
- The trace says `[app] replay gpt-6-luna`, and the `[loop]` token counts are the recorded ones.

**Done when** — the replay test passes with no network, and each of these makes it fail: one word
changed in `SKILL.md`, the tool list emptied before the model call, and the model's turn left out of
the history. The seed test fails without the seed line. `dotnet test` is still free.

**Watch for**
- `new Random(seed)` is not promised to give the same numbers on a new .NET major version. After a
  runtime upgrade, a red replay on the brief means re-record.
- The recording is written after the model has been paid for. If a real OpenAI reply ever fails to
  turn into JSON, the run stops after `[loop] turn 1`, paid. A fake reply with reasoning content
  round-tripped in the throwaway copy. The 9c run is the first real one.
- Every test that starts the server already takes about 7 seconds. The seed test starts two and
  the replay test starts the app, so `dotnet test` gets about 25 seconds slower.
- A build fails while the app is running or paused in the debugger (AGENTS.md).

**Changed** — filled in when the step is done.

**Seen in the debugger, worth remembering** — filled in when the step is done.

### 10. The server in layers — refactor

**What** — three slices, each one runnable:
- **10a. The roll.** New projects `NpcForge.Server.Domain` (the brief as a value object,
  `Difficulty`, `ObstacleKind`, the tables, the roll rules) and `NpcForge.Server.Application` (the
  handler). `NpcForge.Server` stays the host: each MCP tool method shrinks to "map the arguments,
  call the handler, return". `Random` is passed in. There are already two kinds, shared and seeded,
  so no `IDice` interface is needed.
- **10b. Save and load.** `NpcForge.Server.Infrastructure` holds the JSON file (today's
  `Storage.cs`) behind `ICharacterRepository`. `SavedCharacter` becomes the write-side aggregate,
  with its number as identity.
  - The file gets a stored type of its own, so the save format can outlive changes to the brief.
  - `load_character` becomes a query that never builds a domain brief. It returns
    `SavedCharacterView`, a read model in Application, filled by a reader in Infrastructure through
    `ISavedCharacterReader`. Application never names the stored type: the compiler would refuse it,
    and that refusal is the point.
  - The view's brief serialises exactly as a rolled brief does: same names, same order, every value
    a string. The round-trip test (`McpToolSourceTests.cs`) is not edited.
  - A save appends the new entry and writes the old ones back as stored.
- **10c. MediatR.** Three handlers and one behaviour that earns the pipeline: a `[server]` line per
  request with its name and how long it took. Current MediatR with a Community licence key. A new
  `NpcForge.Server.Tests` project holds domain and handler tests with a seeded `Random`: fast, with no
  process. `NpcForge.Tests` stays the console's.

**Why** — The roll rules live in the MCP tool method today: a presentation adapter holding domain
rules. Step 11 is nearly all server work, so the server is layered first. This is the dependency
rule enforced by the compiler, DDD's building blocks on a small real domain, and CQRS as separate
command and query handlers.

**Before it starts** — ADR-004 (Clean Architecture, DDD and CQRS, server first). It replaces
AGENTS.md "Code"'s "No Clean Architecture layering", "No host or DI container" and "Two interfaces
earn their place", and BUILD.md's dependency-rule paragraph. Write the `tools/list` snapshot test
(names and input schemas) first. ADR-008 (the stored shape) at 10b, ADR-006 (MediatR) at 10c.

**Decide in the sketch** — is rolling a command or a query? It changes nothing, so by CQRS's own
definition it is a query, even though asking twice gives two answers. Write down why.

**Done when**
- every existing test and the replay test pass with no test edits;
- the `tools/list` snapshot still matches: that JSON is the only contract between the processes;
- the committed `characters.json` loads through the new query, and a save after it leaves the old
  entries byte-identical;
- `NpcForge.Server.Domain.csproj` has no package or project references;
- the console has no diff.

**Verify** — Paid: none. Debugger: break in the `roll_character` tool method and step into the
handler, then the domain roll: three projects, one call stack. In 10c, break in the behaviour and
watch every request pass through it.

**Watch for** — nothing may write to stdout on the server. MediatR warns when there is no licence
key: check it goes through the logger (stderr), then set the key. The server has no user-secrets
yet, and the Host adds them only in Development. Lesson 003 applies to every new `.csproj`: no
comment that mentions a CLI flag.

### Checkpoint after step 10: did the server layers earn their place?

A short review before step 11 builds on the layers. No code. "No, not all of them" is a valid
answer. Answer each from what happened in step 10, not from what the layers promise:
1. **Did the dependency rule catch anything?** A reference the compiler refused that the old single
   project would have allowed.
2. **Did testing get easier?** How many domain tests run with no process, and how fast, against the
   tests that need the server.
3. **Is step 11 easy to place?** Name where the tone tags, the tone filter, the name dice and the
   new stored fields go. Does each have one obvious home?
4. **Can you follow one roll** from the tool method to the domain in the debugger, and explain the
   call stack a day later?
5. **Did MediatR pay for itself?** One behaviour over three handlers, against three direct calls.

Outcomes: keep everything; keep the projects but drop MediatR; fold Application and Infrastructure
back into folders in the host; or go back to the step 9 shape. `crew:challenger` argues the other
side of whatever you conclude. Anything but "keep everything" is recorded as ADR-010 before step 11,
and step 12 is re-planned to match.

### 11. Tone, importance and name dice — feature

**What**
- **Brief.** `Tone` (`Serious`, `Comic`, `Heartfelt`; set by the DM, rolled if left out) and
  `Importance` (`Throwaway`, `Recurring`; set by the DM, default `Throwaway`, never rolled).
- **Tone on the server.** Table entries that clash with a tone are tagged `NotFor` and left out of
  that tone's roll. Most traits fit any tone, so only the clashing ones need tags.
- **Name dice, for every character.** An initial and a syllable count: "a name starting with K, two
  syllables". Like every rolled trait, either can be set by hand. The syllable count is an enum
  (`One`, `Two`, `Three`), so every field in the brief travels as a string.
- **Importance on the server.** A `Recurring` character rolls one extra field; a `Throwaway` does
  not. Which field is open question 1.
- **Skill.** `SKILL.md` stays the core, plus one line: the name starts with the rolled initial and
  has the rolled syllable count. New files `tones/serious.md`, `tones/comic.md`,
  `tones/heartfelt.md`, `importance/throwaway.md`, `importance/recurring.md`. The app loads the core
  plus the one tone file and the one importance file the brief names: code chooses, not the model.
  The `[skill]` line lists the files and fingerprints the combined text.
- **Console.** `--tone` and `--importance`. The app reads two properties from the brief's JSON to
  choose the files. Still no `CharacterBrief` type on the console.
- **Re-record the fixture once**, after the skill files have settled, with `--tone` and
  `--importance` added to the command.

**Variety floor: six.** Every table keeps at least six entries in every difficulty × tone
combination, after the difficulty filter and the tone's exclusions.
- Six because the success test draws three: with six, three runs all differ about 56% of the time.
- Wall has five wants today, so step 11 adds at least one, plus one more for each Wall want the
  most-excluding tone removes. Tag the existing entries first, then count, and make the new Wall
  wants fit every tone.
- `obstacle`, `tone` and the syllable count are small fixed kinds, not tables. The floor test names
  all three as exempt. The initial is a table and meets the floor.
- The test calls the same filter code the roll uses, not a copy.

**Old saves** — fields added after a save was written read as "unknown":
- The domain brief keeps tone, importance and the name dice required. The stored type has them
  optional: absent means saved before the field existed.
- The Infrastructure reader writes `"unknown"` into `SavedCharacterView` for each absent field. A
  save writes old entries back untouched, and "unknown" is never written into the file.
- An old save is read-only history. It can never become the write-side `SavedCharacter`.

**Before it starts** — ADR-007 (tone, importance and name dice join the brief).

**Done when**, free:
- the variety floor test passes for every difficulty × tone;
- unit tests show a given tone is kept and a missing one is rolled; clashing entries never roll for
  their tone; every character gets name dice; `Recurring` rolls its extra field and `Throwaway`
  does not;
- a copy of the committed `characters.json` loads with tone, importance and the name dice as
  "unknown", and a save after it succeeds and leaves the old entries byte-identical;
- the replay test passes against the re-recorded fixture.

And paid, on OpenAI, about six runs plus the re-record:
- three runs at the same answers and one tone give three different characters with levers that
  differ, the names follow their dice, and the second loads back unchanged;
- one run at each of the other two tones reads as that tone (your eyes);
- one `Recurring` run shows the extra field in the writing.

**Verify** — Debugger: break in the roll handler and look at the filtered table sizes for each
tone. Break where the app builds the system message and read the combined skill text.

### 12. The console in layers, and a harder harness — refactor + harness

**What** — three slices. If the checkpoint changed the server's shape, this step follows it.
- **12a. Write a character.** `NpcForge.Console.Application` holds `AgentLoop`, `IToolSource` and
  the `WriteCharacter` use case (roll, run, save). `NpcForge.Console.Infrastructure` holds
  `McpToolSource`, `ModelClients`, and the record and replay clients. `NpcForge.Console` keeps
  `Program.cs`, the flags and the console output. Application cannot reference
  `ModelContextProtocol`, OpenAI or Anthropic. `ServiceCollection` with no Host, so `Program.cs`
  still reads in order.
- **12b. Load a character.** The second use case. MediatR on the console once there are two.
- **12c. Harness.** Ctrl+C wired to the cancellation token, cancelling the run rather than the app.
  A session token budget as `IChatClient` middleware, which counts across every call where the turn
  cap cannot. Each `AgentLoop` gets its own `ChatOptions` (`Clone()`), so step 13's interviewer
  cannot overwrite the writer's tools.
- No console Domain project. `SceneDraft` (step 13) lives in Application until there is a second
  console domain type.

**Done when**
- the replay test passes unchanged. It compares the tool list as well as the messages, so a
  `Clone()` that loses the tools fails it;
- a fake client that waits on the token shows Ctrl+C ends the run and stops the server;
- a fake client that reports usage shows the budget stops the run;
- the Application project has no reference to the MCP or vendor packages.

**Verify** — Paid: one OpenAI smoke run, because replay swaps the client out and cannot prove the
real provider is still wired up. Debugger: break in `WriteCharacter` and walk the call stack from
`Program.cs`. Press Ctrl+C at a breakpoint in the loop and watch the token flip.

### 13. The scene interview — feature

**What**
- **The model talks; code keeps the draft.** `SceneDraft` holds each answer with the words you
  typed that support it.
- **The questions come from `roll_character`'s input schema**, which the console already has from
  `tools/list`. A field added on the server reaches the interview with no console change. Each
  `roll_character` parameter gets a `[Description]`, the one server change here. It updates step
  10's `tools/list` snapshot on purpose and does not touch the replay.
- **The model sees one tool, `set_answer(field, value, quote)`,** handled in the app, never on the
  server. Every call goes through the checks below. A rejection goes back to the model as a tool
  result.
- **The model cannot end the interview.** When every required answer is filled, code prints the
  draft and asks you to confirm. Only then does the app roll. Anything left unfilled is rolled.
- `roll_character` stays app-only, and a test proves the interview cannot reach it.
- **A new skill, `skills/scene-interviewer/SKILL.md`:** ask for what is missing, never offer a
  value, quote exactly.
- **Flags stay.** With every required answer on the command line there is no interview, so the
  replay test is unchanged.

**The answer checks** — one normalising function, used the same way on your messages and on the
model's quote and value: lowercase; curly quotes and apostrophes made straight; every punctuation
character becomes a space, except an apostrophe between two letters (`won't` stays one word); runs
of spaces collapsed, and trimmed.
- **Quote check (rejects)**, in this order, each with its own reason: an unknown field; an enum
  value not in the list; a quote that is empty after normalising (its own explicit check); a quote
  that is not in any of your messages as whole words (pad both with a space, so `inn` does not
  match inside `dinner`).
- **Drift flag (warns, never rejects)**, free-text fields only: if a word of the value is not in
  the quote, the value is kept and marked at the confirmation, like
  `setting: pub  ←  "the setting is an inn"  (not in your words)`. It cannot see a dropped word
  (`a castle ← "the setting is not a castle"`); the quote shown beside the value is where you catch
  that. Enum values are never flagged.

**Before it starts** — ADR-009 (the interview).

**Done when**, free (lesson 004: each test gives arguments that would otherwise succeed, and every
rejection asserts its reason):
- an honest quote differing only by case, a full stop, a curly apostrophe and extra spaces is
  accepted (typed `We're at an inn.`, quoted `we’re at an  inn`), and so is one typed inside single
  quotation marks (typed `'an inn'`, quoted `an inn`);
- a quote you never typed is rejected; a quote found only inside a longer word is rejected;
- an empty quote and a quote of only `...` are rejected, with a bare Enter and a lone "?" among your
  messages;
- an unknown field and an enum value outside the list are each rejected, with a quote you typed;
- a value with a word not in its quote is accepted and flagged; a value that reorders or drops words
  (including `a castle`) is accepted and not flagged; an enum value is never flagged;
- the model cannot reach `roll_character`, and the roll waits for your confirmation, which shows
  every value beside its quote and any flag;
- the replay test passes unchanged. A failure here means the interview leaked into the writer's
  path: a finding, not a reason to re-record.

And paid, on OpenAI and Anthropic: three interviews each with vague and incomplete answers (the
model asks for what is missing, every value that reached the roll has a quote you typed, the trace
shows each rejection and flag), and one interview through to a written, saved character.

**Verify** — Debugger: break in the `set_answer` handler and watch the draft fill. Step into the
normalising function with a quote that has a capital and a curly apostrophe. Break before the
confirmation and read the draft against what you typed.

## Finish line

The README's success test keeps passing: the same answers, three runs, three genuinely different
characters, levers that differ, and the second one loaded back unchanged.
- Refactor steps (10, 12) prove it with the replay test. No paid runs for the success test itself.
- Feature steps (11, 13) prove it with three paid runs judged by eye, then a `--load`.
- "Levers differ" stays a human check. Step 7 found the markup differs by model, so a parser would
  test formatting, not levers.
- From step 11, the variety floor test guards the dice for free.

## Open questions

1. **A recurring character's extra field:** a tie to the place, a secret, or a want beyond this
   scene? Needed by step 11.
2. **Occupation in the interview:** a required question, or default to "innkeeper" as today? Needed
   by step 13.

## Documents owed

From v04 "Documents to update", each done in the step that makes it true:
- README: status, the second purpose, the brief, the architecture, the build order. After step 9.
- AGENTS.md "Build and run": the test count (eight today, ten after step 9). "Code": after ADR-004.
- BUILD.md "Shape", "The dependency rule" and "Build steps": with step 10.
- `.claude/rules/team.md` "Commands": the re-record command, in step 9c.
