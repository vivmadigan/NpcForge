# Plan

Companion to BUILD.md. BUILD.md says what each step *proves*. This says what to
actually do in each one, why the piece exists, and what the code looks like.
Same step numbers, so the two line up. Tick things off here.

Tags: 🧩 you write · 📖 reference code from Claude, you adapt it · 🤝 pair on it in chat

Where a step is about *seeing* something happen, the code block is the whole
file and pastes in. Where it is about *writing* something, `...` marks the part
that is yours. Read it through either way. Type and member names were checked
against the installed packages, but the compiler is the final word — if a name
is off it will say so in seconds.

## Decisions already made

Settled in the step 1 and 2 sessions so they are not re-decided later. The two marked
*assumed* are Claude's reading of the README — say so if wrong.

| Decision | Choice | Why |
| --- | --- | --- |
| Model access | `IChatClient` (Microsoft.Extensions.AI), both providers behind `--provider` | The loop gets written once and tested against two vendors. That is what makes the abstraction real rather than theoretical. |
| `IChatModel` from BUILD.md | Not written. `IChatClient` *is* it. | BUILD.md allows this. `Message` → `ChatMessage`, `ModelReply` → `ChatResponse`. |
| `IToolSource` from BUILD.md | Still yours to write | It is the seam that swaps a fake tool (step 4) for MCP (step 5) without touching the loop. `ToolDefinition` → `AITool`, `ToolCall` → `FunctionCallContent`. |
| `UseFunctionInvocation` | Off until step 4 is done | It runs the tool loop for you. Steps 2–4 are about writing it. |
| Host / DI container | None. `Program.cs` wires by hand: config → client → options → run. Stripped 2026-09-12. | Step 5's `ConnectAsync` is async setup, and step 6's "roll before the model's first turn" has to be visible, in order. A container hides both. |
| `AgentLoop.RunAsync` | Takes `List<ChatMessage>`, not `string opening` as in BUILD.md. `ChatOptions` comes in through the constructor. | Step 7 puts a system message at index 0. The caller builds the history; the loop never changes. |
| Skills *(assumed)* | `SKILL.md` files in the repo, read by the app into the system prompt | Works with both providers. Anthropic's server-side Skills feature would tie the app to one. |
| Storage *(assumed)* | One JSON file, in the server | BUILD.md allows file or SQLite. A file is less to learn and the brief is already JSON. |
| Tests | Small xUnit project, first appears at step 4 | The cap and the exit condition are the first things worth a test. Nothing before that is testable without spending money. |
| OpenAI endpoint | Responses (`OpenAI.Responses.ResponsesClient`), not Chat Completions. Decided 2026-09-13. | `gpt-5.6-terra` rejects function tools on Chat Completions when reasoning is on (HTTP 400: "use /v1/responses or set reasoning_effort to 'none'"). Forcing effort to none would put a vendor workaround on options shared with Claude and switch off reasoning. Responses is marked experimental (`OPENAI001`), suppressed in `ModelClients.cs`. |
| Traces | stderr, with a bracketed prefix: `[tool]` from the app's tool source, `[server]` from the MCP server. Decided 2026-09-13. | Stdout is the answer, and on the server it is the transport. Same channel and a prefix per side, so the two read as one trace. |

## Names you will meet

| Name | What it is | Step |
| --- | --- | --- |
| `ChatMessage`, `ChatRole` | One entry in the history. Role is `User`, `Assistant`, `System` or `Tool`. | 2 |
| `ChatResponse.Messages` | Everything the model sent back this turn — text and tool requests together. | 2 |
| `TextContent`, `FunctionCallContent` | The two kinds of content you care about. A call has `CallId`, `Name`, `Arguments`. | 2 |
| `FunctionResultContent` | Your answer to one call: the same `CallId` plus the result. | 3 |
| `AIFunction`, `AIFunctionFactory.Create` | A C# method wrapped so the model can see its name, description and parameter schema. | 2 |
| `AIFunctionArguments` | The dictionary a call's `Arguments` become when you run the function yourself. | 3 |
| `ChatOptions.Tools` | The list of `AITool`s the model is allowed to ask for. `AIFunction` is one kind of `AITool`. | 2 |
| `McpClientTool` | An MCP tool as seen from the client. Also an `AIFunction`, so it fits the same list. | 5 |
| `[McpServerToolType]`, `[McpServerTool]` | Attributes that turn a static method into an MCP tool on the server. | 5 |

## Steps

### ✅ 1. Talk to the model — done 2026-09-12

OpenAI and both Claude models reply. `Program.cs` builds the client with `ModelClients.Create`; `ChatAgent.RunAsync(client, options)` sends one hardcoded message.

### ✅ 2. See a tool request — done 2026-09-13, both providers

The model returned a `FunctionCallContent` with a `CallId`, `Name` and `Arguments`
instead of prose, and `LookupArchetype` never ran. `ChatAgent.RunAsync` sends one
hardcoded question with the fake tool in `ChatOptions.Tools` and prints every
content item by type. The Anthropic run was done at the start of step 3, same
breakpoints: same call shape, a `toolu_` id, `FinishReason` set to `tool_calls`.

**Changed**
- `ChatAgent.cs` — the step 2 sketch, plus a `-- {message.Role}` line per message.
- `ModelClients.cs` — OpenAI now uses the Responses endpoint (`ResponsesClient`),
  not Chat Completions. See the decisions table. `#pragma warning disable OPENAI001`
  because the SDK marks Responses experimental.

**Seen in the debugger, worth remembering**
- `FinishReason` is `null` on the Responses endpoint, and `response.Text` is `""`
  when the model sends only a tool call. Neither is a signal. The loop's exit is
  the absence of `FunctionCallContent` — BUILD.md's rule, now seen for real.
- `response.ConversationId` comes back set. Do **not** pass it into
  `ChatOptions.ConversationId`: that lets OpenAI keep the history server-side.
  History stays in your `List<ChatMessage>`. Claude has no equivalent anyway.
- `CallId` is fresh every request (`call_...`). Step 3's `FunctionResultContent`
  must echo it exactly; it is how the model pairs answers to calls.
- `call.Arguments` is `IDictionary<string, object?>` with `JsonElement` values,
  not a typed object. `AIFunctionArguments` bridges it to the method in step 3.
- `FunctionCallContent.Exception` is where the adapter records a failure to parse
  the model's arguments. `InformationalOnly == false` means a real request, not
  a hosted tool the provider already ran itself.
- The tool name the model sees is the method name, `LookupArchetype`. Step 5's
  server calls it `lookup_archetype`. Expected mismatch, not a bug.
- `response.Usage` has the token counts. `CachedInputTokenCount` is the number to
  watch in step 7, where the system message has to stay first and unchanged.
- Swapping endpoints changed `client` from `OpenAIChatClient` to
  `OpenAIResponsesChatClient` with no edit to `ChatAgent.cs`. That is `IChatClient`
  earning its place.
- `AIFunctionFactory` is library code from Microsoft.Extensions.AI. Nothing to write.

### ✅ 3. Close the circle by hand — done 2026-09-13, both providers

The function ran in the app, never in the model. `ChatAgent.RunAsync` sends the
question, appends the assistant's messages to a `List<ChatMessage>` it owns, runs the
one `FunctionCallContent` through `tool.InvokeAsync`, appends a `ChatRole.Tool`
message carrying a `FunctionResultContent` with the same `CallId`, and calls
`GetResponseAsync` again with the whole list. Straight-line code, no loop. The second
reply used the tool's sentence on both providers.

**Changed**
- `ChatAgent.cs` — the step 3 sketch. `PrintResponse(ChatResponse)` extracted from
  step 2's printing loop, called once per turn. `LookupArchetype` reformatted: each
  `[Description]` on its own line, braces instead of `=>`, and a comment saying the
  two attributes are the only prose the model sees.

**Seen in the debugger, worth remembering**
- Both `[Description]` strings are visible on the wrapper: the method's in
  `tool.Description`, the parameter's inside `tool.JsonSchema`. That JSON is all the
  model knows about the tool. The same attribute comes back on the server in step 5.
- `result` from `tool.InvokeAsync` is a `JsonElement`, not a `string`.
  `AIFunctionFactory` serialises return values. Step 4's `?.ToString()` unwraps it.
- The reasoning-item 400 from the watch-for did not appear on `gpt-5.6-terra`.
  Adding `first.Messages` to the history unchanged was enough.
- `FinishReason` is `null` on both OpenAI responses, tool call and text alike, and
  set on both Anthropic ones: `tool_calls`, then `stop`. Provider-shaped. Never the
  exit.
- `CallId` is `call_...` on OpenAI and `toolu_...` on Anthropic. Opaque, echoed
  exactly, accepted both times.
- The `ChatRole.Tool` message is the one new shape this step. Both adapters mapped it
  with no vendor-specific code.
- Same tool result, different writing. OpenAI echoed the sentence verbatim; Sonnet
  bolded the three traits and expanded on each without adding a fourth. The facts
  held and only the prose moved. That is the README's split, seen before any of the
  machinery meant to produce it exists.
- `history.Count` went 2, then 3: question, assistant call, tool result. Every
  `GetResponseAsync` resent all of it.

### ✅ 4. Generalise into the loop — done 2026-09-13, both providers

Step 3's lines inside a `for`, behind `IToolSource`. `AgentLoop.RunAsync` asks the
source for tools, calls the model, appends its messages, and either returns the text
when there are no calls or runs every call and appends a `Tool` message per result.
Out of turns, it throws. `ChatAgent.RunAsync` is now three things: build the loop,
run it, print. The real run terminates on its own on both vendors and the runaway
test hits the cap at three turns.

**Changed**
- `AgentLoop.cs` — the loop, commented against the rules in BUILD.md "The loop".
- `IToolSource.cs` — the seam. `ListAsync` and `InvokeAsync`, nothing else.
- `FakeToolSource.cs` — `LookupArchetype` moved here from `ChatAgent`, wrapped once
  and shared by both methods. `InvokeAsync` catches everything and returns the error
  as text. One `[tool]` trace line on stderr per call, so a plain run shows the round
  trip without the debugger.
- `ChatAgent.cs` — shrunk to build the loop, run it, print. `PrintResponse` deleted.
- `Program.cs` — comments on the order of events. No code change.
- `NpcForge.Tests` — new xUnit project. Two fakes of `IChatClient`, one that always
  asks for a tool and one that never does, and two tests: the exit returns the text,
  the cap throws. No network; both run in under a second.

**Seen in the debugger, worth remembering**
- `this` in `AgentLoop` shows the four constructor parameters as fields. `model` is
  typed `IChatClient` with `OpenAIResponsesChatClient` behind it; `tools` is typed
  `IToolSource` with `FakeToolSource` behind it. The loop only ever sees the left
  half. Step 5 changes the right half of `tools` and nothing on the left moves.
- `result` in the loop is a `string`, not step 3's `JsonElement`. The unwrap moved
  into `FakeToolSource`; the loop never meets a `JsonElement`.
- `ct` is `CancellationToken.None` from `ChatAgent`: `CanBeCanceled == false`. Nothing
  stops a run but the cap. Wiring Ctrl+C to it is a later nicety, not a step.
- The exit check is false on turn 0 and true on turn 1. `history.Count` goes 2, 3, 4
  across the two turns: user, assistant call, tool result, assistant text.
- The tests compiled unchanged against the loop, the fake source and the interface.
  The first small proof the interface has the right shape.
- From outside, the tool round trip disappears: a plain run prints only what the
  loop returned, where step 3 printed both turns. That silence is what "generalise
  into the loop" looks like. The `[tool]` trace exists to make it visible again.
- Sonnet still expands on exactly the three traits and names the lookup as the
  source. Same behaviour as step 3, now through the loop.

### 5. Replace the stub with MCP 🧩 server · 🤝 client

**What**
- New project `NpcForge.Server`: console app, packages `ModelContextProtocol` and `Microsoft.Extensions.Hosting`. One tool, `lookup_archetype`, same hardcoded answer as step 2.
- All server logging to stderr.
- In the console app, `McpToolSource : IToolSource` — starts the server as a child process over stdio, lists its tools, forwards calls.
- `Program.cs`: `var tools = new McpToolSource(); await tools.ConnectAsync(ct);` then hand it to `ChatAgent`. Two lines, in the open — this is why there is no DI container.
- `FakeToolSource` stays for the tests.

**Why** — Two processes is the point. The operating system enforces the boundary, and the loop running unchanged is the proof that `IToolSource` was the right seam.

**Shape — `NpcForge.Server/Program.cs`** 🧩

```csharp
var builder = Host.CreateApplicationBuilder(args);

// stdout is the transport. Every log line goes to stderr instead.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

**Shape — `NpcForge.Server/CharacterTools.cs`** 🧩

```csharp
[McpServerToolType]
public static class CharacterTools
{
    [McpServerTool(Name = "lookup_archetype"), Description("Look up how a given kind of person usually behaves.")]
    public static string LookupArchetype(
        [Description("An occupation, such as innkeeper or farmer")] string occupation)
        => ...;   // same sentence as step 2
}
```

**Shape — `NpcForge.Console/McpToolSource.cs`** 🤝

```csharp
public sealed class McpToolSource : IToolSource
{
    private McpClient? _client;
    private IList<McpClientTool> _tools = [];

    public async Task ConnectAsync(CancellationToken ct)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "NpcForge.Server",
            Command = "dotnet",
            Arguments = ["run", "--project", "<path to NpcForge.Server>", "--no-build"],   // 🤝 path
            StandardErrorLines = line => Console.Error.WriteLine($"[server] {line}"),
        });

        _client = await McpClient.CreateAsync(transport, cancellationToken: ct);
        _tools = await _client.ListToolsAsync(cancellationToken: ct);
    }

    public Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AITool>>([.. _tools]);

    public async Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct)
    {
        var tool = _tools.First(t => t.Name == call.Name);
        var args = call.Arguments?.ToDictionary(kv => kv.Key, kv => kv.Value);

        var result = await tool.CallAsync(args, cancellationToken: ct);
        return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
    }
}
```

**New here**
- `StdioClientTransport` *launches* the server. The console app is the parent process; the server's stdin/stdout are the wire. `StandardErrorLines` is how you see the server's logs without them corrupting the wire.
- `McpClientTool` is an `AIFunction`, so `[.. _tools]` drops straight into `ChatOptions.Tools`. Nothing to translate.
- `CallAsync` returns MCP's own result shape — a list of content blocks. Text tools give one `TextContentBlock`. Joining them is enough for now.
- `WithToolsFromAssembly` finds every `[McpServerToolType]` class by reflection. Add a class, get a tool.

**Done when** the loop runs unchanged against the real server.

**Watch for** — No `Console.WriteLine` anywhere in the server. Stdout is the transport; one stray line produces baffling parse errors on the client side. `Console.Error.WriteLine` is fine.

### 6. Rolling up characters 🧩

**What**
- In the server: `CharacterBrief` and the two enums — copy them from BUILD.md "The brief as a type". Trait tables as static arrays, each entry tagged with the difficulties it fits. A `roll_character` tool that takes optionals and rolls only what is missing.
- In the console app: before the loop, call `roll_character` directly through the MCP client and put the returned JSON into the first user message with the three answers.
- `McpToolSource.ListAsync` filters `roll_character` *out* of what the model sees.
- The three questions become `--setting`, `--want`, `--difficulty` flags for now.

**Why** — The model never guesses; it is told or it rolls. Rolling in code gives variety the model cannot collapse. Keeping `roll_character` off the model's list means it cannot skip the roll. Same server, two audiences.

**Shape — `NpcForge.Server/Tables.cs`**

```csharp
public static class Tables
{
    public record Want(string Text, params Difficulty[] Fits);

    public static readonly Want[] Wants =
    [
        new("a bit of company",                Difficulty.Easy),
        new("to be paid what they are owed",   Difficulty.SomeWork),
        new("to be left alone to work",        Difficulty.SomeWork, Difficulty.Wall),
        new("to keep a secret buried",         Difficulty.Wall),
        ...
    ];

    public static readonly string[] Attitudes = ["greedy", "rushed off their feet", "distrustful", "frightened", ...];
    public static readonly string[] Mannerisms = [...];
    ...
}
```

**Shape — the tool, in `CharacterTools.cs`**

```csharp
[McpServerTool(Name = "roll_character"), Description("Roll up a character brief inside what the difficulty allows.")]
public static CharacterBrief RollCharacter(
    string setting,
    string playersWant,
    Difficulty difficulty,
    string? attitude = null,          // any rolled field can be handed in instead
    string? mannerism = null,
    ...)
{
    var wants = Tables.Wants.Where(w => w.Fits.Contains(difficulty)).ToArray();   // the difficulty filter, in code

    return new CharacterBrief
    {
        Setting = setting,
        PlayersWant = playersWant,
        Difficulty = difficulty,
        CharacterWants = Pick(wants).Text,
        Obstacle = ...,                                    // Easy never rolls Wont
        Attitude = attitude ?? Pick(Tables.Attitudes),
        Mannerism = mannerism ?? Pick(Tables.Mannerisms),
        ...
    };
}

static T Pick<T>(IReadOnlyList<T> table) => table[Random.Shared.Next(table.Count)];
```

Put `[JsonConverter(typeof(JsonStringEnumConverter))]` on both enums so the brief's JSON says `"Wall"`, not `2`. The model reads that JSON.

**Shape — the app-only door, in `McpToolSource.cs`**

```csharp
private static readonly string[] AppOnly = ["roll_character"];      // grows at step 8

public Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct)
    => Task.FromResult<IReadOnlyList<AITool>>([.. _tools.Where(t => !AppOnly.Contains(t.Name))]);

public async Task<string> CallDirectAsync(string name, Dictionary<string, object?> args, CancellationToken ct)
{
    var result = await _client!.CallToolAsync(name, args, cancellationToken: ct);
    return string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));
}
```

**Shape — the start of `ChatAgent.RunAsync`**

```csharp
var briefJson = await tools.CallDirectAsync("roll_character", new()
{
    ["setting"] = setting,
    ["playersWant"] = want,
    ["difficulty"] = difficulty,
}, ct);

var history = new List<ChatMessage>
{
    new(ChatRole.User, $"Write this character. The brief is settled; do not change it.\n\n{briefJson}"),
};
```

**New here**
- A tool that returns an object comes back as JSON text. The console app never deserialises it — it hands the text to the model. No `CharacterBrief` type on the console side, no shared project.
- `ListAsync` and `CallDirectAsync` are the two audiences from BUILD.md, in code. One list for the model, one door for the app.
- `Random.Shared` is the dice. That is the entire mechanism for variety, and the model cannot reach it.

**Done when** three runs at the same difficulty produce three different people.

### 7. Skills 🧩 the file · 🤝 loading it

**What**
- `skills/npc-writer/SKILL.md`: the fixed output order from the README ("What comes back"), the rule that levers and anti-levers trace back to the brief, and "show the trait, never state it".
- The app reads the file at startup and puts it in a `ChatRole.System` message at index 0 of the history. It is never repeated.

**Why** — Separates writing guidance from facts. Facts come from the server; guidance comes from the file. The test of whether it is really a skill: editing the file changes the output with no code change.

**Shape — `skills/npc-writer/SKILL.md`**

```markdown
---
name: npc-writer
description: Writes one non-player character from a settled character brief, in a fixed order.
---

You are writing one character for a tabletop game from the brief in the first message.
The brief is settled. Do not add, drop or soften a trait.

## Output, in this order
1. **Who they are** — a name and one line of history. A line, not a backstory.
2. **Where they stand** — what they know about what the players want, and their position on it.
3. **How they seem, and what is underneath** — the surface manner, then the real state of things.
4. **Levers** — two or three things that would move this person, each with a line of dialogue.
5. **Anti-levers** — two or three things that would harden them, each with a line.
6. **Afterwards** — what they do once the players leave: got it / didn't / made an enemy.

## Rules
- Every lever and anti-lever names the entry in the brief it comes from.
- Show traits in what the character does or says. The character never announces them.
```

**Shape — loading it**

```csharp
var skillPath = Path.Combine(AppContext.BaseDirectory, "skills", "npc-writer", "SKILL.md");
var skill = File.ReadAllText(skillPath);

var history = new List<ChatMessage>
{
    new(ChatRole.System, skill),          // index 0, once, never edited
    new(ChatRole.User, $"...{briefJson}"),
};
```

And in the console csproj, so the file travels to the output folder:

```xml
<ItemGroup>
  <None Include="skills\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

**New here**
- A `System` message is just another `ChatMessage`. Both providers map it to their system prompt.
- `ChatOptions.Instructions` does the same job as a property instead of a message. The message is used here so the guidance is visible in the history you already own.

**Done when** the levers differ between the three step 6 runs. Generic levers mean the brief is not reaching the writing.

**Watch for** — The system message stays first and unchanged across turns. Move it or edit it and prompt caching stops working.

### 8. Saving 🧩

**What**
- Server tools `save_character(name, brief)` and `load_character(name)`, backed by one JSON file next to the server.
- Add both names to `AppOnly`.
- Console: `--load <name>` skips rolling and uses the saved brief.

**Why** — The brief is the save format. Persist it and the character comes back identical.

**Shape — in `CharacterTools.cs`**

```csharp
[McpServerTool(Name = "save_character"), Description("Keep a character worth keeping, by name.")]
public static string SaveCharacter(string name, CharacterBrief brief)
{
    var all = Storage.Load();               // Dictionary<string, CharacterBrief> from the JSON file, or empty
    all[name] = brief;
    Storage.Save(all);
    return $"Saved '{name}'.";
}

[McpServerTool(Name = "load_character"), Description("Load a kept character by name.")]
public static CharacterBrief? LoadCharacter(string name)
    => Storage.Load().GetValueOrDefault(name);
```

`Storage` is a static class with `Load` and `Save`: `File.ReadAllText` / `WriteAllText` on `characters.json` next to the exe, through `JsonSerializer`. Ten lines.

**Done when** a reloaded character is identical to the original.

## Finish line

The success test is in the README ("Success test"): same three answers, three runs, three genuinely different characters, load the second one back unchanged. Out of scope is listed there too — don't start any of it.
