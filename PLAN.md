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

Settled in the step 1 and 2 sessions so they are not re-decided later. The one marked
*assumed* is Claude's reading of the README — say so if wrong.

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
| Traces | stderr, with a bracketed prefix: `[tool]` from the app's tool source, `[server]` from the MCP server. Decided 2026-09-13. Step 7 adds three more on the same channel: `[app]` for the provider, model and the rolled brief, `[skill]` for which file was loaded, `[loop]` for one line per model turn. Step 8 adds `[app] saved as N` after a run and `[app] loaded N` on a load. | Stdout is the answer, and on the server it is the transport. Same channel and a prefix per side, so the two read as one trace. With the step 7 lines a plain run explains itself: every trait in the writing can be checked against the `[app] brief` line without a debugger. |
| Starting the server | `dotnet run --project <server> --no-build`, the path built from `AppContext.BaseDirectory`, the build order set by a `ProjectReference` with `ReferenceOutputAssembly="false"`. Decided 2026-09-19. | Works the same from the app and the tests, never starts a stale server, and shares no types. Dropping `--no-build` would rebuild on every launch and risk build output on stdout, which is the wire. |
| Recording paid runs | One file per run in `docs/runs/`, captured by pasting the console, each standing on its own: commit before the run, and the `[skill]` line carries a content fingerprint. No index; observations live in `PLAN.md`'s step notes. Decided 2026-09-20. See [docs/runs/README.md](docs/runs/README.md) "Why not". | Runs cost money and are the only evidence for whether a skill edit helped. Redirecting reorders the trace against the answer, and a `git diff` between two SHAs is empty in the normal case, because tuning happens against a dirty tree. No record in `docs/decisions/`: this shapes a working practice rather than the code, so the reasoning lives with the practice. |
| Occupation | Supplied like the setting: `--occupation`, default `"innkeeper"` next to the setting default in `Program.cs`. Not rolled. Decided 2026-09-19. See [ADR-002](docs/decisions/ADR-002-occupation-in-the-brief.md). | Three live runs let the model choose who the character is (an innkeeper twice, a patron once), and the README's success test asks for innkeepers. A table on the server cannot see the setting, so a rolled default would put "innkeeper" into a farm's brief as settled fact (the challenger). Departs from the README ("not one of the questions") and from BUILD.md's brief, which is step 8's save format. |

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

### ✅ 5. Replace the stub with MCP — done 2026-09-19, both providers

The loop ran unchanged against a real server in a second process. `NpcForge.Server`
exposes `lookup_archetype` over stdio. `McpToolSource` in the console app starts it with
`dotnet run`, shakes hands, lists its tools and forwards each call. `Program.cs` connects
before the model's first turn and hands the source to `ChatAgent`. `AgentLoop.cs` has no
diff since `step-4`. A new free test runs a tool call through the real server with a
scripted fake model, and both paid runs answered with the server's sentence.

**Changed**
- `NpcForge.Server` — new project from the MCP Server App template, trimmed of its NuGet
  packing and self-contained settings. `Program.cs` logs to stderr and registers tools with
  `WithToolsFromAssembly()`. `CharacterTools.cs` holds `lookup_archetype`, same sentence
  as step 2.
- `McpToolSource.cs` — the MCP `IToolSource`. The server path is built from
  `AppContext.BaseDirectory`, four folders up, so the app and the tests find it the same
  way. `InvokeAsync` keeps `FakeToolSource`'s contract: one `[tool]` trace line, failures
  as text.
- `NpcForge.Console.csproj` — `ModelContextProtocol` 2.2.0, the same version as the
  server, and a build-order `ProjectReference` to the server with
  `ReferenceOutputAssembly="false"`, so the no-build launch never starts a stale server.
- `ChatAgent.cs` — takes an `IToolSource` instead of creating the fake itself.
- `Program.cs` — creates and connects `McpToolSource` before the run.
- `NpcForge.Tests` — `CallsToolOnceChatClient` and `Runs_a_tool_call_through_the_real_server`:
  the loop, a scripted fake model, the real server. Three tests, about 2 s, still free.
- Before any code was typed, the challenger found that the first `InvokeAsync` sketch had
  no catch, which broke `IToolSource`'s contract, and that `--no-build` would run a stale
  server. Both were fixed in the plan first.

**Seen in the debugger, worth remembering**
- Lesson: A csproj comment that mentions a CLI flag stops the project loading (docs/lessons/003-csproj-comment-double-hyphen-load-failed.md)
- The handshake is `server/discover`, not `initialize`: that is what the server logs on
  protocol version `2026-07-28` (`_client.NegotiatedProtocolVersion`).
- The server's content root is whatever folder the app was started from: the console's `bin`
  under F5, the repo root from a terminal. The child process inherits the parent's working
  directory. Why `serverProject` is built from `AppContext.BaseDirectory`, and why step 8's
  save path must not be relative.
- First real run, OpenAI, 2026-09-19: the whole trip in one console. `[server]` lines for
  `server/discover` and `tools/list`, then `[tool] lookup_archetype {"occupation":"innkeeper"}`,
  then `[server]` `tools/call` and `"lookup_archetype" completed. IsError = False.` The answer
  was the sentence verbatim, as in step 3.
- Claude (`claude-sonnet-5`), same day, from a terminal: the same trace, line for line. Sonnet
  bolded the three traits and expanded on them, exactly as it did in step 3. Both providers
  now run against the real server.
- `calls.Count` was 1, then 0: the same exit as step 4, through a different tool source.
- On the client, `tool.UnderlyingMethod` is `null`. The client has no code for the tool, only
  what came over the wire: `Name`, `Description` and `JsonSchema`, built from the server's
  attributes.
- `First` throws `InvalidOperationException` on an unknown tool name, the same type the
  loop's cap throws. Without the catch, a runaway test against `McpToolSource` would pass
  without the cap ever firing. Found by the challenger before any code was typed.
- `ReferenceOutputAssembly="false"` still copies `NpcForge.Server.exe`, `.deps.json` and
  `.runtimeconfig.json` into the console's and the tests' `bin`, but not the `.dll`. That
  copy cannot run and is never used: the server that runs is the one in
  `NpcForge.Server\bin`, through `dotnet run --project`. Found by the reviewer.

### ✅ 6. Rolling up characters — done 2026-09-19, OpenAI

The app rolls the character in code before the model's first turn. The server holds the
brief, the trait tables and `roll_character`, and the model never sees that tool. `Program.cs`
connects, rolls through `McpToolSource.CallDirectAsync`, and hands the brief's JSON to
`ChatAgent` as the first message. After the occupation change, three live runs at `Wall` gave
three different innkeepers (the user's eyes, 2026-09-19). `IToolSource` and `AgentLoop.cs` did
not change. A fourth free test checks the two doors against the real server.

**Changed**
- `McpToolSource.cs` — the two carry-overs from the step 5 review: `IAsyncDisposable`, created
  with `await using` in `Program.cs` and the real-server test, so the server stops on purpose;
  and a failure inside the server reaches the model as `Tool failed:`, like one in the app.
  New: `AppOnly` keeps `roll_character` off the model's list, and `CallDirectAsync` is the
  app's own door, which throws on `IsError` instead of returning text.
- `NpcForge.Server` — `CharacterBrief.cs` (BUILD.md's record plus `Occupation`; both enums
  carry `JsonStringEnumConverter`), `Tables.cs` (wants tagged with the difficulties they fit,
  six tables), and `roll_character` in `CharacterTools.cs`: the difficulty filter in code,
  Easy never rolls `Wont`, and any rolled trait can be handed in instead.
- `Program.cs` — connect, roll, run, in that order. Flags `--setting`, `--want`,
  `--difficulty` and `--occupation`, with defaults so a bare run works.
- `ChatAgent.cs` — takes the brief's JSON and puts it in the first user message. No
  `CharacterBrief` type on the console side.
- `NpcForge.Tests` — `McpToolSourceTests.Rolls_a_brief_the_model_cannot_see`: the model's list
  leaves out `roll_character`, and the direct call returns a brief at `Wall`. Four tests, still
  free. With the `AppOnly` filter removed, it fails.
- `Occupation` joined the brief after run 3 came back a patron. Supplied, not rolled: see the
  decisions table.
- Before any code was typed, the challenger found that the first sketch handed a failed roll
  to the model as the brief, and rolled inside `ChatAgent`, which only has an `IToolSource`.
  Both were fixed in the plan first. On the occupation sketch, it found that a rolled default
  of "innkeeper or tavern owner" was a constant dressed as dice, and wrong for any setting but
  an inn.

**Seen in the debugger, worth remembering**
- First live run, OpenAI, `Wall`. The roll shows only as a `[server]` `tools/call` with no
  `[tool]` line: that trace lives in `InvokeAsync`, the model's door, and the app's own call has
  none. The model then called `lookup_archetype` for "innkeeper" unprompted; it was the only
  tool it could see.
- Every rolled trait reached the writing: the want (avoiding someone owed a favour) became why
  he will not give the name, and the wrong-about became rats as "venomous tunnel-wolves". Some
  traits were stated rather than shown ("In truth, he has a soft spot for animals"). That is
  step 7's "show the trait, never state it".
- Run 2, `Wall`: a different innkeeper, but he gave the fence's name ("ask after Pell") in his
  first reply. The brief says `"difficulty":"Wall"` and nothing tells the model what that means
  for the players. Step 7's skill has to.
- Names converge across runs: Merrit Vane, Sella Quill, Vell Marrow, Sella Vane. The name is
  not in the brief, so the model picks it and falls back on favourites, which is the reason the
  traits are rolled. Run 3 was a patron, not an innkeeper, for the same reason; that is why
  `Occupation` joined the brief. The name is still the model's.
  **Corrected at step 7:** "falls back on favourites" was drawn from OpenAI runs only and does
  not generalise. Eight OpenAI-family runs across three models (`terra`, `sol`, `luna`) all gave
  a `Mar-`/`Mer-` first name, with Venn, Pell and Voss recurring; four Anthropic runs gave Bartho
  Quill, Mira Colbeck, Hessa Dunmore and Halbrecht Onnow, sharing nothing. Anthropic converges
  too, but on backstory — both Sonnet runs gave eleven years at the inn and a brother-in-law. Each
  vendor has its own unrolled field it falls back on, so the argument for rolling holds while the
  evidence for it is vendor-shaped. See `docs/runs/2026-09-20-05-eight-models.md`.
- To stop inside the server: stop the app on the `CallDirectAsync` line, then Debug → Attach to
  Process → `NpcForge.Server.exe`. In time means the log shows `tools/list` completed and no
  `tools/call` yet. Attach after the roll and a bound breakpoint in `RollCharacter` never hits.
  Every run starts a new server process, so attach every run (Shift+Alt+P reattaches).
- A tool parameter typed `string` takes JSON `null` without complaint: `string` against
  `string?` is only checked by the compiler. `roll_character` then returned a brief with no
  `occupation` in it. The enum parameter `difficulty` does fail on null. Found on a scratch
  copy; the app always sends a value.

### ✅ 7. Skills — done 2026-09-20, OpenAI and Anthropic

**What**
- First, a carry-over from the step 6 review. `McpToolSource.InvokeAsync` finds the tool in the unfiltered `_tools`, so the model could still run `roll_character` by naming it, even though `ListAsync` hides it. Look the name up in the same filtered list `ListAsync` returns. An app-only name then fails as `Tool failed:` in the existing catch. It matters more at step 8, when `save_character` and `load_character` join `AppOnly`.
- `skills/npc-writer/SKILL.md`: the fixed output order from the README ("What comes back"), the rule that levers and anti-levers trace back to the brief, and "show the trait, never state it".
- The app reads the file at startup and puts it in a `ChatRole.System` message at index 0 of the history. It is never repeated.

**Why** — Separates writing guidance from facts. Facts come from the server; guidance comes from the file. The test of whether it is really a skill: editing the file changes the output with no code change.

**Shape — `skills/npc-writer/SKILL.md`**

```markdown
---
name: npc-writer
description: Writes one non-player character from a settled character brief, in a fixed order.
---

You are writing one character for a tabletop game from the brief you have been given.
The brief is settled. Do not add, drop or soften a trait.

## What the difficulty means for the scene

`difficulty` is how far `characterWants` pulls against `playersWant`. It is not
how rude they are.

- **Easy** — it costs them nothing. They give it.
- **SomeWork** — there is a price or a hesitation to talk past first.
- **Wall** — the two wants are in direct opposition. Nothing in the opening
  hands it over. The players have to work a lever, and it costs the character
  something to yield.

`obstacle` says what kind of hard it is, and it changes what yielding means:

- **Wont** — they could and they will not. Distrust, fear, or loyalty to someone else.
- **Cant** — they do not know, or may not say. No lever produces the answer; a
  lever gets the players what this character *can* give instead.
- **ForAPrice** — they will, once the price in `characterWants` is met. Name the price.

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

**Done when**, in one run: the six sections appear in order, every lever and anti-lever names the
entry in the brief it came from, and at `Wall` nothing is handed over until a lever lands and costs
the character something. Then, across three runs, the levers differ — generic levers mean the brief
is not reaching the writing.

**Watch for** — `response.Usage.CachedInputTokenCount`, the number step 2 flagged for this step. The
loop only ever appends, so the system message cannot move or be edited: this is a number to look at,
not a rule you could break.

**Before the skill — the baseline run.** OpenAI, `Wall`, 2026-09-20, with the carry-over in and
no SKILL.md yet. Every rolled trait reached the writing: the mannerism (nods before people have
finished speaking), the wrong-about (believes they are an excellent liar), the weakness (easily
impressed by confident people), the want (out of an arrangement with the fence). The difficulty
did not. Merrin Voss gave up the fence's name — *"It's Sella Marrow — 'Silk,' they call her"* — to
"little more than an authoritative assumption". The lever was drawn from the brief; it simply cost
nothing. And with no skill there was no fixed order at all: no Levers, Anti-levers or Afterwards
sections. Every run since has had all six, so the difference is the whole of what follows.

**Changed**
- `NpcForge.Console/skills/npc-writer/SKILL.md` — new. The fixed order from the README, the rule
  that levers trace back to the brief, "show the trait, never state it", and a section on what
  `difficulty` and `obstacle` oblige the writer to do. The user added the line saying a Games
  Master will role-play the character, which nothing else told the model.
- `NpcForge.Console.csproj` — `<None Include="skills\**" CopyToOutputDirectory="PreserveNewest" />`,
  so the file travels to `bin` and the app reads it from beside the exe.
- `ChatAgent.cs` — reads the file at startup and puts it in a `ChatRole.System` message at index 0,
  above the brief. No parsing: whatever the file says is what the model is told.
- `McpToolSource.cs` — the carry-over from the step 6 review. `ModelTools` is the filtered list, and
  both `ListAsync` and `InvokeAsync` read it, so an app-only name cannot be reached by naming it.
  The lookup throws with the name in the message rather than `First`'s "sequence contains no
  matching element".
- `Program.cs`, `AgentLoop.cs` — three more trace prefixes, see the decisions table.
- `NpcForge.Tests` — `Refuses_an_app_only_tool_the_model_names`. Five tests, still free.
- `docs/runs/` — new. One file per paid run, plus `README.md` on how a run gets there.
- PLAN.md — before any code was typed, the challenger found that the plan had dropped a carry-over
  step 6 had explicitly assigned to step 7 (what a `Wall` obliges the writer to do), and that the
  "Done when" could not have caught it. Both were fixed in the plan first, as at steps 5 and 6.

**Seen in the debugger, worth remembering**
- The skill works, and the evidence is the `Wall`. Before it, a `Wall` innkeeper handed over the
  fence's name in his first reply. After it, across three runs, none did. See `docs/runs/`.
- **The rule had to be per-obstacle, not per-difficulty.** Runs 02–04 drew `Wont`, `ForAPrice` and
  `Cant` in turn, and each produced a structurally different scene: refuses it, sells it, does not
  have it. A rule saying "at a `Wall`, withhold what the players want" would have broken two of the
  three — `Cant` worst of all, since that character stonewalls over information she never had.
  This is README "How difficulty works" seen for real.
- **Editing the file changes the output with no code change.** That is the test of whether it is a
  skill rather than a prompt, and it passes.
- **Formatting is a tendency, not a guarantee.** All eleven runs *with* the skill produced the six
  sections in order with levers traced to brief entries — run 01, before it, produced none of them
  — but the markup differed every way it could: numbered
  headings on OpenAI, inline bold labels on Sonnet, renamed sections on Opus, `[field: value]`
  brackets on luna. If anything ever parses this output, that is what will break it.
- **A model may skip a tool it can see.** `gpt-5.6-luna` returned on turn 1 with no tool call at
  all, writing the character straight from the brief; every other run called `lookup_archetype`
  once. Nothing broke — the loop's exit is the absence of calls — but it is the first time the
  single-turn path has been seen against a real model rather than the step 4 fake. It is also the
  stated reason `roll_character` is kept off the model's list entirely.
- **Prompt caching never engages at this size.** No `CachedInputTokenCount` appears in
  `AdditionalCounts`: input is about 680 tokens, under the threshold. The step 2 note pointed here;
  the answer is that there is nothing to see until the prompt is much bigger.
- `CachedInputTokenCount` is not a property on `UsageDetails`. It arrives in `AdditionalCounts`,
  the per-vendor dictionary, because `UsageDetails` only names the counts every vendor shares.
- The server's content root is the folder the app was started from: `bin` under F5, the repo root
  from a terminal. Confirmed again across twelve runs. Step 8's save path must not be relative.
- **`SKILL.md` changed after the twelve runs, at the step-done review.** Its `Easy` bullet said
  both "it costs them nothing" and "if there is a cost it is small", and nothing parses the file,
  so the model would have resolved that silently and differently per run. `Easy` and `SomeWork`
  also had no evidence at all: every one of the twelve runs was `Wall`. Both bullets were rewritten
  onto the axis that actually matters — not what it costs the *character*, but how hard the ask is
  for the *players* to clear. At `Easy` the character still asks for something, because nobody
  hands things to strangers for free; the ask is just small enough that any reasonable attempt
  passes. An `Easy` that simply hands the thing over is not a scene at all, and a GM gets nothing
  to run. README's ladder was changed to match. So the `2097 chars` recorded in the run headers is
  the version those runs used, not the one at HEAD. Run 06 then covered the whole ladder for the
  first time: at `Easy` the character asks the players to buy an unwanted warming-pan and the
  lever says "even a modest one" clears it; at `SomeWork` the ask is a real task a wrong approach
  would fail; at `Wall` only a brief-derived lever moves her. Three rungs that read differently.
- **A char count is not a fingerprint.** The `[skill]` line now carries a short SHA-256 of the
  text the model is sent as well as the length, because the twelve runs and run 06 could
  otherwise have been told apart only by luck — the rewrite happened to change the length.
  Swapping `Wont` for `Cant` in the obstacle bullet is `2239 chars, 70057667` against the real
  `2239 chars, 6b58d7c2`: same count, different skill. Comparing output across skill versions is
  the whole premise of `docs/runs/`, and it rested on a number that cannot see half the edits.

### ✅ 8. Saving — done 2026-09-22, OpenAI

Every run saves its brief and its written character, numbered, and `--load <number>` prints one
back with no roll and no model call. Run 3 (`ForAPrice`) and its `--load 3` printed the same
character (the user's eyes, 2026-09-22; `docs/runs/2026-09-22-01-save-and-load.md`), and the free
round-trip test proves it byte for byte. Eight tests, still free.

**What**
- Every run is saved: the brief and the written character together, numbered in order (1, 2, 3...).
  Nothing is chosen or named at save time. Keeping only some can come later.
- Server tools `save_character(brief, text)`, which returns the new number, and
  `load_character(number)`, which returns the brief and the text. Both go in `AppOnly`: only the
  app calls them.
- One file, `characters.json`. The server takes its path from the environment variable
  `NPCFORGE_CHARACTERS` if set, otherwise `NpcForge.Server\characters.json`: the server's
  project folder, three up from its `bin\Debug\net10.0`, where you can find it and where
  deleting `bin` does not touch it. (First `%LOCALAPPDATA%\NpcForge`, moved 2026-09-22 because
  it was hard to find.) Never a relative path: the server's working folder is wherever the app
  was started from (step 7).
- Console: after the run, the app saves and prints `[app] saved as 3`. `--load 3` skips the roll
  and the model: it prints the saved character exactly as it was. `ChatAgent.RunAsync` returns the
  answer so `Program.cs` can save it.
- A number that was never saved stops the run with an error. `load_character` throws
  `McpException`, the one exception whose message reaches the app, and `CallDirectAsync` already
  throws on `IsError`. Returning `null` would not do it: the SDK sends that back as empty content,
  not an error, and the model would be handed an empty brief.
- Tests: `McpToolSource` takes an optional file path and hands it to the server as
  `NPCFORGE_CHARACTERS`. Each test uses its own temp file, so no test touches your saves.

**Why** — README build order 4: store the character "along with the brief that produced them".
The brief alone is not the character. It has no name, and the model writes differently every run,
so reloading only the brief gives the same traits and a new innkeeper. Saving the text is what
makes "load the second one back unchanged" true, and it is what someone working on a character
later would open.

**Shape** — sketched one change at a time, compiled and challenged before you type it.

**Tests**, all free, against the real server:
- Save, then load: the brief and the text come back identical.
- Loading a number that was never saved throws from `CallDirectAsync`.
- The model cannot reach `save_character` or `load_character` by naming them. Lesson 004: give
  arguments that would otherwise succeed. The temp file makes a real save harmless if the filter
  is ever removed.

**Done when** a reloaded character is identical to the original, brief and text: the round-trip
test passes, and one paid run followed by `--load` of its number prints the same character.

**Before any code** — the first plan saved only the brief, under a name. The challenger
(WEAKENED, 2026-09-22) found four things: the brief is not the whole character, nothing ever
called `save_character`, a mistyped name would reach the model as an empty brief, and tests would
share your save file. The decisions above answer all four.

**Changed**
- `NpcForge.Server/Storage.cs` — new. `SavedCharacter(Brief, Text)`, and a static `Storage` with
  `Load` and `Save` on one indented, camelCase file keyed by number. A file that will not parse
  comes back as an `McpException` naming the path.
- `CharacterTools.cs` — `save_character` (the next number is one past the highest) and
  `load_character` (an unknown number throws `McpException`).
- `McpToolSource.cs` — save and load join `AppOnly`. An optional save-file path goes to the
  server as `NPCFORGE_CHARACTERS`.
- `Program.cs` — `--load <number>`: connect, load, print, return. A bare `--load` stops before
  anything runs, instead of falling through to a paid run. After a run: save, then
  `[app] saved as N`. `Program.cs` prints the answer itself, so a run and a load print the same
  way. The default model is `gpt-6-luna` since `5157aae` (it was `gpt-5.6-terra`).
- `ChatAgent.cs` — returns the answer instead of printing it.
- `NpcForge.Tests` — `Loads_a_saved_character_back_unchanged`, `Stops_on_a_number_that_was_never_saved`,
  `Refuses_save_and_load_when_the_model_names_them`, each on its own temp file. Eight tests. Each
  new one fails with the code it guards removed: without the filter the model's save returns
  `"2"`, and without the path the save lands in the server's own file.
- The user wrote the server and console code from sketches. The lead wrote the tests and the
  save-file path, at the user's request.
- The challenger ran three times: on the plan (above), on the server sketch (WEAKENED: a brief
  sent as text fails with no reason, and a broken save file says nothing), and on the console
  sketch (HOLDS, but a bare `--load` fell through to a paid run).

**Seen in the debugger, worth remembering**
- Lesson: A run and its --load look different in a console paste when the save is exact (docs/lessons/006-console-paste-not-byte-exact.md)
- **A brief sent as text fails with no reason.** `save_character` takes a `CharacterBrief`, and
  the app holds the brief as the text the roll returned. Sent as that text, it comes back as
  `save_character failed: An error occurred invoking 'save_character'.` Sent as
  `JsonDocument.Parse(briefJson).RootElement`, it saves. The challenger's probe found this
  before anyone hit it.
- **Only `McpException` carries its message to the app.** Any other exception thrown in a tool
  arrives as `An error occurred invoking '<tool>'.`, and a `null` return arrives as empty content
  with `IsError` false. Both are in the MCP 2.2.0 docs, and both were confirmed on a scratch copy.
- **Everything over MCP is text.** `save_character` returns an `int`, and `savedAs` in
  `Program.cs` is the string `"1"`.
- **The app's own calls leave no `[tool]` line.** Save and load, like the roll, show only
  `[server]` lines. `[tool]` lives in `InvokeAsync`, the model's door.
- **A number means something only inside one file.** Moving the save file started again at 1:
  Merrit Vale is number 1 in `%LOCALAPPDATA%\NpcForge`, and Mara Venn is number 1 in the project folder.
- **A load still builds the model client,** so it needs the provider's API key even though it
  never calls the model. Left as it is.
- Names converge on `gpt-6-luna` as they did on the step 7 OpenAI models: Merrit Vale, Mara Venn,
  Mara Venn.

## Finish line

The success test is in the README ("Success test"): same three answers, three runs, three genuinely different characters, load the second one back unchanged. Out of scope is listed there too — don't start any of it.

## After the finish line — not planned yet

Talked through on 2026-09-22, at the start of step 8. None of this is a step yet. Planning it
changes the project's direction, so it gets a new plan version in `docs/plans/` and a challenge
first.

- **Saving is a proof of concept.** The long-term idea: a user logs in, sees the characters and
  dialogue they made, and works on them further. The JSON file stays for now. A database later
  changes only the server's storage, because the app only ever calls `save_character` and
  `load_character`. That's the same kind of swap `IToolSource` allowed at step 5. README lists a real
  database and any interface beyond the console as out of scope for this plan.
- **Next: talk to the app instead of passing flags.** It covers two separate ideas:
  - *Asking for the answers* (setting, want, difficulty, occupation). README says the console
    app asks them. Plain `Console.ReadLine` does it with no model. If the model ever gathers
    them, the app still does the roll: `roll_character` stays out of the model's reach.
  - *Carrying on the conversation* after the character is written ("make him warier"). New for
    the loop: a history that keeps growing over many turns. It is also where prompt caching should
    finally show up, since step 7 found the prompt too small for it.
- **Saving every run stands in for choosing what to keep.** Revisit it with the interactive
  console, which is where a "Save as?" question or naming a character belongs.
