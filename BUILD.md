# Build Guide

Companion to the README. The README covers what this is and why. This covers
how it is put together and what to do next. Nothing here repeats the design
rationale, so read that one first if the shape is unclear.

## How the code should read

The architecture is allowed to be interesting. The code is not.

This is a learning project, and the point is to understand it, including in
six months. Plain and obvious wins. A line that saves four lines but takes a
minute to parse is the wrong line, every time.

So: no dense chains where a loop reads better, no abstraction introduced
before there are two things to abstract, no pattern used because it is the
done thing on a real project. A technique earns its place by making the code
clearer, not shorter.

## Naming

One term worth fixing before any code is written.

A **character brief** is the short list of traits that settles who a person is
before any writing happens. Three answers you give, plus what the app rolls
up. The name is meant in the ordinary sense: the brief you hand a writer
before they start.

**Rolling up a character** is producing that brief. Partly your answers,
partly dice.

In code: a `CharacterBrief` record, a `RollCharacter` method, a
`roll_character` tool on the server.

## Shape

Two processes, not one app. MCP servers run separately and are reached over
stdio or HTTP, so the boundary is enforced by the operating system and no
project structure is going to make it tighter.

```
NpcForge.sln
├── NpcForge.Console    the loop, the questions, the MCP client
└── NpcForge.Server     tool definitions, trait tables, rolling, storage
```

No shared contracts project. JSON over the wire is the contract, and sharing
types would quietly recouple the two things MCP just separated.

## One run, end to end

```
you ──► console app
             │  1. roll up the character (your code calls the server, not the model)
             ├──────────────────────────► MCP server
             │ ◄──────────────────────────  the brief
             │
             │  2. brief + instructions + tool list
             ├──────────────────────────► model
             │ ◄──────────────────────────  "call lookup_archetype"
             │
             │  3. run the requested tool
             ├──────────────────────────► MCP server
             │ ◄──────────────────────────  result
             │
             │  4. same history, now with the result appended
             ├──────────────────────────► model
             │ ◄──────────────────────────  the finished character
             ▼
        printed to you
```

Step 1 is the one that is easy to get wrong. `roll_character` is a tool on the
server, but it is **not** in the list handed to the model. Your code calls it
directly and puts the brief into the first message. A tool the model can see
is a tool the model can decide to skip.

So the server ends up with two kinds of tool: ones you expose to the model,
and ones only your app calls. Same server, different audiences.

## The dependency rule

The only bit of Clean Architecture worth the cost here. Two interfaces, so the
loop knows neither who the model vendor is nor where tools come from.

```csharp
public interface IChatModel
{
    Task<ModelReply> CompleteAsync(
        IReadOnlyList<Message> history,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken ct);
}

public interface IToolSource
{
    Task<IReadOnlyList<ToolDefinition>> ListAsync(CancellationToken ct);
    Task<string> InvokeAsync(ToolCall call, CancellationToken ct);
}
```

This is what lets step 2 of the build below run against a hardcoded stub and
step 5 swap in real MCP without the loop changing.

Microsoft.Extensions.AI's `IChatClient` plays the `IChatModel` role if you
would rather not hand-roll it. Either way the loop depends on the abstraction.

## The loop

The whole thing. Everything else is detail hanging off this.

```csharp
public sealed class AgentLoop(IChatModel model, IToolSource tools, int maxTurns = 8)
{
    public async Task<string> RunAsync(string opening, CancellationToken ct)
    {
        var history = new List<Message> { Message.User(opening) };
        var definitions = await tools.ListAsync(ct);

        for (var turn = 0; turn < maxTurns; turn++)
        {
            var reply = await model.CompleteAsync(history, definitions, ct);
            history.Add(reply);

            if (reply.ToolCalls.Count == 0)
                return reply.Text;

            foreach (var call in reply.ToolCalls)
            {
                var result = await tools.InvokeAsync(call, ct);
                history.Add(Message.ToolResult(call.Id, result));
            }
        }

        throw new InvalidOperationException($"No answer after {maxTurns} turns.");
    }
}
```

Four things to notice:

- **The exit is the absence of tool calls.** Not a keyword, not a judgement.
- **History is the state.** There is no session on the model's side. Each call
  resends everything.
- **Every call gets a result appended**, including ones that failed. Skip one
  and the next request is malformed.
- **The cap is not optional.** A confused model will loop until your bill
  hurts.

## The brief as a type

```csharp
public sealed record CharacterBrief
{
    // you supply these three
    public required string Setting { get; init; }
    public required string PlayersWant { get; init; }
    public required Difficulty Difficulty { get; init; }

    // rolled on the server, inside what Difficulty allows
    public required string CharacterWants { get; init; }
    public required ObstacleKind Obstacle { get; init; }
    public required string Attitude { get; init; }
    public required string Mannerism { get; init; }
    public required string Weakness { get; init; }
    public required string WillNotDiscuss { get; init; }
    public required string WrongAbout { get; init; }
}

public enum Difficulty { Easy, SomeWork, Wall }
public enum ObstacleKind { Wont, Cant, ForAPrice }
```

Init properties rather than a positional record, because ten positional
parameters make the call site unreadable:

```csharp
// unreadable
new CharacterBrief(setting, want, Difficulty.Wall, "to be left alone", ...)

// says what it is
new CharacterBrief
{
    Setting = "an inn in a major city",
    PlayersWant = "the name of a fence",
    Difficulty = Difficulty.Wall,
    Obstacle = ObstacleKind.Wont,
    ...
};
```

`required` still forces every field to be set, so nothing is lost.

A record because it is settled once and never mutated. It is also your save
format: persist this and you can reproduce the character.

Any rolled field can be set by hand, so `RollCharacter` takes optionals and
only rolls what is missing.

## Server side

Tools should be narrow and named for what they are for. `roll_character`,
`lookup_archetype`, `save_character`, `load_character`. Never `run_sql`.

```csharp
[McpServerToolType]
public static class CharacterTools
{
    [McpServerTool, Description("Look up how a given kind of person usually behaves.")]
    public static Archetype LookupArchetype(
        IArchetypeStore store,
        [Description("An occupation, such as innkeeper or farmer")] string occupation)
        => store.Get(occupation);
}
```

The difficulty filter lives here, in code, not in a tool description. `Wall`
narrows the wants table down to entries that genuinely obstruct, and only then
rolls. Leave that to the model and it will pick something agreeable.

Storage is a JSON file or SQLite. Characters are the only thing that needs
saving.

## Build steps

Each has a goal and a stop condition. Do not start the next until the current
one passes.

**1. Talk to the model.**
Send one message, print the reply. No tools, no server.
*Done when:* configuration, secrets and the SDK all work.

**2. See a tool request.**
Define one fake tool. Ask something that needs it. Print the raw response and
do not execute anything.
*Done when:* you have seen the model return a structured call instead of
prose. This is the moment the rest of the project is built on.

**3. Close the circle by hand.**
Run the tool yourself, append the result, call again. No `while` yet.
*Done when:* a second response comes back that uses the tool's output.

**4. Generalise into the loop.**
The code above, with `IToolSource` stubbed to return the fake tool.
*Done when:* it terminates on its own, and the cap works when you force a
runaway.

**5. Replace the stub with MCP.**
Stand up the server, connect over stdio, implement `IToolSource` against the
MCP client.
*Done when:* the loop runs unchanged against real tools.

**6. Rolling up characters.**
Trait tables, the difficulty filter, `roll_character` called directly by the
app.
*Done when:* three runs at the same difficulty produce three different people.

**7. Skills.**
Shape the output into the order the README specifies.
*Done when:* the levers differ between those three. Generic levers mean the
brief is not reaching the writing.

**8. Saving.**
Persist a brief and load it by name.
*Done when:* a reloaded character is identical to the original.

Steps 1 to 4 are the agent loop, learnable in an evening and worth
understanding cold before MCP arrives. When something breaks in step 5 you
will then know the loop is not the culprit.

## Things that will bite

**A stdio server cannot write to the console.** Standard out *is* the
transport. A stray `Console.WriteLine` corrupts the protocol stream and
produces baffling parse errors. Log to stderr or a file.

**Tool failures are results, not exceptions.** Catch inside `InvokeAsync` and
return the error text as the tool result. The model can often recover from
that. An exception kills the run.

**History grows every turn.** Fine at this size, but it is why the cap
matters, and why the brief goes in once at the start rather than being
repeated.

**Secrets belong in user-secrets**, never appsettings.json, never the repo.

**The model will happily ignore an instruction it is given as a suggestion.**
Anything that must happen should happen in your code before the model's turn.
That is the whole reason `roll_character` is not exposed to it.

## Decisions worth remembering

- Two processes because MCP requires it, not as a design preference.
- No shared contracts project, to keep the boundary real.
- Stdio over HTTP, because it is local and one less thing to run.
- Server before Skills, so it stays clear which change produced the variety.
- Interfaces around the model and the tool source, so steps 4 and 5 are
  independent.
