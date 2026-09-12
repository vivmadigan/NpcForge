# Plan

Companion to BUILD.md. BUILD.md says what each step *proves*. This says what to
actually do in each one, why the piece exists, and what the code looks like.
Same step numbers, so the two line up. Tick things off here.

Tags: 🧩 you write · 📖 reference code from Claude, you adapt it · 🤝 pair on it in chat

The code blocks are sketches of the shape, not files to paste. The lines that
matter are real; `...` is yours. Type and member names were checked against the
installed packages, but the compiler is the final word — if a name is off it
will say so in seconds.

## Decisions already made

Settled in the step 1 session so they are not re-decided later. The two marked
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

### 2. See a tool request 🧩

**What**
- One fake tool: a static method returning a hardcoded sentence. Wrap it with `AIFunctionFactory.Create` and put it in `ChatOptions.Tools`.
- Ask something that needs it.
- Print every content item the model sends back, by type. Print `FinishReason`.
- Do **not** execute anything.

**Why** — This is the moment the rest of the project rests on: the model returns a structured `FunctionCallContent` instead of prose. Until you have seen that with your own eyes, the loop is theory.

**Shape — `ChatAgent.cs`**

```csharp
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.AI;

public static class ChatAgent
{
    [Description("Look up how a given kind of person usually behaves.")]
    static string LookupArchetype(
        [Description("An occupation, such as innkeeper or farmer")] string occupation)
        => $"A typical {occupation} is busy, watchful, and knows everyone's business.";

    public static async Task RunAsync(IChatClient client, ChatOptions options)
    {
        options.Tools = [AIFunctionFactory.Create(LookupArchetype)];

        var response = await client.GetResponseAsync(
            "How does an innkeeper usually behave? Use the tool.", options);

        Console.WriteLine($"FinishReason: {response.FinishReason}");
        foreach (var message in response.Messages)
        {
            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent text:
                        Console.WriteLine($"text: {text.Text}");
                        break;
                    case FunctionCallContent call:
                        Console.WriteLine($"tool call: {call.Name} id={call.CallId} " +
                                          $"args={JsonSerializer.Serialize(call.Arguments)}");
                        break;
                    default:
                        Console.WriteLine($"other: {content.GetType().Name}");
                        break;
                }
            }
        }
    }
}
```

**New here**
- `[Description]` on the method and on the parameter is what the model reads. `AIFunctionFactory.Create` turns the method's signature into the JSON schema the model needs. No schema by hand.
- `response.Messages` is a list, not one message. A tool request is one content item inside one of those messages. The `switch` on type is the whole skill of this step.
- `FinishReason` will say `ToolCalls` when the model stopped to ask for a tool. Look at it.

**Done when** you see a `FunctionCallContent` with a `CallId`, `Name` and `Arguments` printed. Run it against both providers — the shape is the same, and that is the point of `IChatClient`.

**Watch for** — `UseFunctionInvocation` must stay out. With it in, this step is invisible.

### 3. Close the circle by hand 🧩

**What**
- Take the `FunctionCallContent` from step 2 and run the function yourself.
- Put the answer in a `ChatMessage` with `ChatRole.Tool` containing a `FunctionResultContent`.
- History is now: your question, the assistant's messages, your tool message. Call `GetResponseAsync` again with that list.
- Straight-line code. No `while` yet.

**Why** — Proves the three rules under "The loop" in BUILD.md: history is the state, every call gets a result, the model uses the result. Writing it flat first means you see each rule as a line of code before it disappears into a loop.

**Shape — inside `RunAsync`, replacing the printing from step 2**

```csharp
var tool = AIFunctionFactory.Create(LookupArchetype);
options.Tools = [tool];

var history = new List<ChatMessage>
{
    new(ChatRole.User, "How does an innkeeper usually behave? Use the tool."),
};

var first = await client.GetResponseAsync(history, options);
history.AddRange(first.Messages);                       // the assistant's turn goes in as-is

var call = first.Messages
    .SelectMany(m => m.Contents)
    .OfType<FunctionCallContent>()
    .First();

var result = await tool.InvokeAsync(new AIFunctionArguments(call.Arguments));   // you run it

history.Add(new ChatMessage(ChatRole.Tool,
    [new FunctionResultContent(call.CallId, result)]));  // your answer, keyed by the call's id

var second = await client.GetResponseAsync(history, options);
Console.WriteLine(second.Text);
```

**New here**
- `history` is a plain `List<ChatMessage>` and *you* own it. The model keeps nothing between calls. Every `GetResponseAsync` resends the whole list.
- You could call `LookupArchetype(...)` directly. `tool.InvokeAsync` is used instead because it works for *any* `AIFunction` — including the MCP ones in step 5 — which is exactly what step 4's `IToolSource` needs.
- The order matters: assistant messages first, then your `Tool` message. The `CallId` on the result must be the `CallId` on the call.

**Done when** the second reply clearly uses the tool's output.

### 4. Generalise into the loop 🧩 loop, interface, fake source, tests · 📖 the fake `IChatClient`

**What**
- `AgentLoop` — BUILD.md's loop in M.E.AI types. Step 3's lines inside a `for`.
- `IToolSource` — `ListAsync` gives the tools for `ChatOptions.Tools`; `InvokeAsync` runs one call and returns a string.
- `FakeToolSource` — returns the step 2 tool.
- `NpcForge.Tests` — two tests: the loop returns when the model sends no calls; the loop throws at `maxTurns` when a fake model asks for a tool every time.
- `ChatAgent.RunAsync` shrinks to: build the loop, call it, print.

**Where it's used** — `AgentLoop` is the whole app from here on. Everything later is either a tool source it is given or text it is given.

**Why** — `IToolSource` is what lets step 5 swap in MCP without the loop changing. The cap test is the one that saves your bill: a confused model loops until stopped.

**Shape — `IToolSource.cs`**

```csharp
public interface IToolSource
{
    Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct);
    Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct);
}
```

**Shape — `AgentLoop.cs`**

```csharp
public sealed class AgentLoop(IChatClient model, IToolSource tools, ChatOptions options, int maxTurns = 8)
{
    public async Task<string> RunAsync(List<ChatMessage> history, CancellationToken ct)
    {
        options.Tools = [.. await tools.ListAsync(ct)];

        for (var turn = 0; turn < maxTurns; turn++)
        {
            var response = await model.GetResponseAsync(history, options, ct);
            history.AddRange(response.Messages);

            var calls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .ToList();

            if (calls.Count == 0)
                return response.Text;                     // the exit: no tool calls

            foreach (var call in calls)
            {
                var result = await tools.InvokeAsync(call, ct);
                history.Add(new ChatMessage(ChatRole.Tool,
                    [new FunctionResultContent(call.CallId, result)]));
            }
        }

        throw new InvalidOperationException($"No answer after {maxTurns} turns.");
    }
}
```

**Shape — `FakeToolSource.cs`** 🧩

```csharp
public sealed class FakeToolSource : IToolSource
{
    private readonly AIFunction _lookup = AIFunctionFactory.Create(LookupArchetype);   // moved here from ChatAgent

    public Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct) => ...;   // just the one

    public async Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct)
    {
        try
        {
            ...   // step 3's InvokeAsync line, then ?.ToString() ?? ""
        }
        catch (Exception ex)
        {
            return $"Tool failed: {ex.Message}";          // a result, not an exception
        }
    }
}
```

**Shape — the fake model, in the test project** 📖

```csharp
// A model that asks for a tool every single time. Exists to prove the cap fires.
sealed class AlwaysCallsToolChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var call = new FunctionCallContent("call-1", "LookupArchetype",
            new Dictionary<string, object?> { ["occupation"] = "innkeeper" });
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

**Shape — the cap test** 🧩

```csharp
[Fact]
public async Task Throws_when_the_model_never_stops_asking_for_tools()
{
    var loop = new AgentLoop(new AlwaysCallsToolChatClient(), new FakeToolSource(), new ChatOptions(), maxTurns: 3);

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => loop.RunAsync([new(ChatRole.User, "hi")], CancellationToken.None));
}
```

The second test is the same shape with a fake that returns `new ChatMessage(ChatRole.Assistant, "Done.")` and asserts the text comes back.

**Setting up the test project** (one line each, from the repo root)

```
dotnet new xunit -n NpcForge.Tests -o NpcForge.Tests
dotnet add NpcForge.Tests reference NpcForge.Console
dotnet sln NpcForge.slnx add NpcForge.Tests
```

**New here**
- `IChatClient` is just an interface. A test fake that returns whatever you want is fifteen lines, and it costs nothing to run. That is the payoff of depending on the abstraction.
- Only `GetResponseAsync` matters for the fake. The other three members exist because the interface says so.
- `InvokeAsync` catches everything and returns the error as text. The model can often recover from "tool failed: ..."; it cannot recover from your process dying.

**Done when** the real run terminates on its own, and the runaway test hits the cap.

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
