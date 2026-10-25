# Architecture

How NpcForge fits together as it is now. Why it is shaped this way is in `README.md` and
`docs/decisions/`.

## Shape

```
NpcForge.slnx
├── NpcForge.Console   the app: flags, the agent loop, the model clients, the MCP client
├── NpcForge.Server    an MCP server over stdio: the brief, the trait tables, the dice, storage
└── NpcForge.Tests     xUnit, free: fake models, the real server, and the whole app against a recording
```

Two processes. The console starts the server as a child process and talks MCP over its stdin and
stdout. They share no types: JSON over the wire is the contract, and a shared project would quietly
recouple what MCP separates. The console's `ProjectReference` to the server only sets the build
order (`ReferenceOutputAssembly="false"`).

## One run, end to end

```
you ──► Program.cs
          │ 1. connect: start the server, list its tools
          │ 2. roll: call roll_character directly ─────────► server: tables and dice
          │ ◄─────────────────────────────────────────────── the brief, as JSON
          │ 3. run: SKILL.md as the system message, the brief as the first user message
          │       AgentLoop ◄──► model, which may call lookup_archetype ◄──► server
          │ 4. save: save_character(brief, text) ──────────► characters.json
          ▼
     the character on stdout, the trace on stderr
```

`--load <number>` replaces steps 2 to 4 with `load_character`: no roll, no model, no cost. It still
builds the model client, so it still needs an API key. `--seed`, `--record` and `--provider replay`
are under "Replay".

## The server's tools

| Tool | Called by | Does |
| --- | --- | --- |
| `lookup_archetype` | the model | Says how a kind of person usually behaves |
| `roll_character` | the app only | Rolls the brief. Any rolled trait can be passed in instead |
| `save_character` | the app only | Saves a brief and its text, returns the new number |
| `load_character` | the app only | Returns a saved brief and text by number |

`McpToolSource` has two doors:
- **The model's door**, `ListAsync` and `InvokeAsync`, sees only the tools not in `AppOnly`. Every
  failure comes back as text starting `Tool failed:`, so the model can read it and carry on.
- **The app's door**, `CallDirectAsync`, reaches the app-only tools. A failure throws, so a failed
  roll stops the run instead of reaching the model as a brief.

## The loop

`AgentLoop.RunAsync` is the whole agent. Four rules:
- **The exit is the absence of tool calls.** Not `FinishReason`, not `Text`: each provider shapes
  those differently.
- **History is the state.** The model keeps nothing between calls, so every request resends the
  list. The caller builds it; the loop only appends.
- **Every tool call gets a result appended**, failures included. Skip one and the next request is
  malformed.
- **The turn cap is not optional.** Eight turns, then it throws.

`IToolSource` is the seam to where tools come from (`FakeToolSource` or `McpToolSource`).
`IChatClient` from Microsoft.Extensions.AI is the seam to the vendor, and `ModelClients.Create` is
the only code that knows which one. `UseFunctionInvocation` stays off: it would hide the loop.

## The brief

`CharacterBrief`, on the server only.
- **Supplied:** `setting`, `playersWant`, `difficulty`, `occupation`.
- **Rolled, inside what the difficulty allows:** `characterWants`, `obstacle`, `attitude`,
  `mannerism`, `weakness`, `willNotDiscuss`, `wrongAbout`.

The difficulty filter is code: each want is tagged with the difficulties it fits, and `Easy` never
rolls `Wont`. Enums travel as their names. The brief is settled once and never changes, and it is
half of the save format (ADR-003).

## Writing guidance

`NpcForge.Console/skills/npc-writer/SKILL.md` is copied to `bin` and read into a system message at
index 0. It sets the output order, what each difficulty and obstacle oblige the writer to do, that
every lever names the brief entry it comes from, and "show the trait, never state it". Nothing
parses it, so editing it changes the writing with no code change. The `[skill]` trace line carries
its length and a short SHA-256, so a run names the guidance it was given.

## Traces

Stdout is the answer. Everything else goes to stderr with a prefix, so one console reads as one
trace.

| Prefix | From | Says |
| --- | --- | --- |
| `[app]` | `Program.cs` | `recording to <path>`, the provider and model, the rolled brief, `saved as N`, `loaded N` |
| `[skill]` | `ChatAgent.cs` | Which `SKILL.md`, its length and fingerprint |
| `[loop]` | `AgentLoop.cs` | One line per model turn: tool calls, history size, tokens |
| `[tool]` | `McpToolSource` | Each call the model made. The app's own calls have none |
| `[server]` | the server | MCP requests and log lines, and `dice: seed N` or `dice: random` at the first roll |

## Starting the server

`dotnet run --project <server> --no-build`, with the path built from `AppContext.BaseDirectory`
(four folders up), so the app and the tests find it the same way. No build at launch, because build
output on stdout would corrupt the wire; the `ProjectReference` keeps the server from going stale.
Settings reach it as environment variables (`NPCFORGE_CHARACTERS`, `NPCFORGE_SEED`), which arrive
before any MCP message.

## Storage

One JSON file, `NpcForge.Server/characters.json`: every run, numbered from 1, the brief and the text
together (ADR-003). `NPCFORGE_CHARACTERS` overrides the path, and the tests use temp files. The path
is always absolute, because the server's working folder is wherever the app was started. Only the
server touches the file, so a database later changes the server and nothing else.

## Replay

One real run is recorded once, and the whole app replays against it for free (ADR-005).

- **The seed.** `--seed <n>` reaches the server as `NPCFORGE_SEED`, never as a `roll_character`
  parameter, whose parameters stay the brief fields a GM may set. The same seed rolls the same brief.
  A run without `--seed` clears any `NPCFORGE_SEED` the shell holds, so only the flag seeds the dice.
- **Recording.** `--record <file>`, which needs `--seed`, wraps the real client in
  `RecordingChatClient`. The file is JSONL: line 1 is the run's answers as flags, then one line per
  model call with what the app sent (every message, every `ChatOptions` field the library
  serialises, each tool's name, description and schema) and what came back. The file is written at
  the first reply, so a run that stops before the model leaves the last recording alone.
  The flags and line 1 come from one table in `RunOptions`, so a new answer cannot be parsed but
  left off the recording. The same parser stops the run, before any model client is built, on a flag
  that is not in the table, a flag with no value (last, or before another flag) and a stray word.
- **Replaying.** `--provider replay --recording <file>` answers from the file. Before each answer it
  compares `Recording.Describe` of the recorded request with the app's, and stops the run at the
  first line that differs. Text is compared as lines; the model's reasoning and tool calls as the
  library's JSON.
- **The test.** `ReplayTests` runs the console as a process with line 1's flags and checks the save
  file against the recording. It refuses a recording with fewer than two calls or no tool call,
  because a dropped model turn only shows on a later call. The child's HTTP proxy is a dead port, so
  a replay that ever lost `--provider replay` fails instead of paying.
- **Offline.** `NPCFORGE_OFFLINE=1` makes `ModelClients.Create` refuse `openai` and `anthropic` before
  it builds a client or reads a key, so a free run cannot reach a model whatever order `Program.cs` runs
  in. `replay` still works. It is a second stop, independent of the dead proxy. `OfflineTests` runs both
  paid providers against the real code, in process with a dummy key and as the whole app.
- **What forces a re-record.** Anything that changes what the model is sent: `SKILL.md`, a tool's
  description or schema, a `ChatOptions` field, the first message. Anything that changes the seeded
  brief: an entry in `Tables.cs`, the order of the picks, a .NET major version that changes
  `new Random(seed)`. And an upgrade of the AI, OpenAI or MCP libraries, because the recording is
  their JSON. Re-record after a deliberate behavior change or such an upgrade only, never for a
  restructure. A re-record with no tool call fails the test; ask the owner before paying again. The
  command and the rules are in `docs/runs/README.md`.
- **By hand, free.** `docs/runs/README.md` has the replay command. Give it `NPCFORGE_CHARACTERS`, or
  its save lands in the tracked `characters.json`.

## Tests

`dotnet test` is free: no network and no model.
- `AgentLoopTests`: the loop exits when the model sends no tool calls, throws at the cap, and runs a
  tool call through the real server. Fake models throughout.
- `McpToolSourceTests`, against the real server: the model can neither see nor name an app-only
  tool, a save loads back byte for byte, an unknown number stops the run, and the same seed rolls the
  same brief from two servers.
- `ReplayTests`: the whole app against the recorded run, as above.
- `OfflineTests`: `NPCFORGE_OFFLINE=1` refuses both paid providers, and replay still works.

A test that asserts on `Tool failed:` must give arguments that would otherwise succeed, or it passes
for the wrong reason (lesson 004).

## Behaviour worth knowing

Found in the debugger and in paid runs. Each one cost time once.
- **OpenAI is on the Responses endpoint.** Chat Completions rejects function tools on reasoning
  models (lesson 001).
- **`FinishReason` is null on OpenAI and set on Anthropic.** A `CallId` is `call_…` on one and
  `toolu_…` on the other. Treat it as opaque and echo it exactly.
- **Never pass `ConversationId` back.** It lets OpenAI keep the history on its side. History stays
  in the app's list.
- **Only `McpException` carries its message to the app.** Any other exception in a tool arrives as
  "An error occurred invoking …", and a `null` return arrives as empty content with no error.
- **Everything over MCP is text.** `save_character` returns an int and the app receives `"1"`. A
  brief sent as text fails with no reason given; send it as a JSON object.
- **A `string` tool parameter accepts JSON `null`.** Nullability is only checked by the compiler.
- **A model may skip a tool it can see.** One run wrote straight from the brief with no tool call.
  That is why `roll_character` is never visible.
- **Output formatting differs by model:** numbered headings, bold labels, renamed sections. Never
  parse it.
- **Names converge, differently per vendor.** OpenAI models keep reaching for `Mar-` and `Mer-`
  names; Anthropic converges on backstory instead. Names are not rolled yet.
- **Prompt caching does not engage.** The prompt is about 700 tokens, under the threshold.
- **A console paste is not byte-exact** (lesson 006), and a PowerShell redirect reorders stdout
  against stderr (lesson 005). The save file is the exact record.
- **To debug the server**, stop the app on a `CallDirectAsync` line, then attach to
  `NpcForge.Server.exe`. Every run starts a new server, so attach every run.
- **There is no launch profile on purpose.** A `Properties/launchSettings.json` makes every
  `dotnet run` print "Using launch settings from ..." on stdout, above the answer.
  `"dotnetRunMessages": false` does not stop it.

## How it was built

Steps 1 to 8 were typed by hand, one proof at a time. Comments that mention a step mean these. Their
full notes are in `docs/plans/steps-1-to-8.md` at commit `cd20b60`. Epics since then are built by
agents and named, not numbered.

| Step | Proved | Tag |
| --- | --- | --- |
| 1 | Config, secrets and the SDK: the model replies | |
| 2 | The model returns a structured tool call, not prose | |
| 3 | Run the tool by hand, append the result, call again | |
| 4 | The loop stops on its own, and the cap works | `step-4` |
| 5 | The same loop runs against a real MCP server | `step-5` |
| 6 | Server dice roll the brief before the model's first turn | `step-6` |
| 7 | `SKILL.md` shapes the output, and levers differ across runs | `step-7` |
| 8 | Every run is saved, and a load is byte-identical | `step-8` |
| Safety net | A recorded run replays for free, and the same seed rolls the same brief | |
