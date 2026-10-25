# Architecture

How NpcForge fits together as it is now. Why it is shaped this way is in `README.md` and
`docs/decisions/`.

## Shape

```
NpcForge.slnx
├── NpcForge.Console   the app: flags, the scene interview, the agent loop, the model clients, the MCP client
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
          │ 1a. interview, only with no answer flag: read the scene, a model
          │     records the GM's words with record_answer, show them, wait for "yes"
          │ 2. roll: call roll_character directly ─────────► server: tables and dice
          │ ◄─────────────────────────────────────────────── the brief, as JSON
          │ 3. run: SKILL.md as the system message, the brief as the first user message
          │       AgentLoop ◄──► model, which may call lookup_archetype ◄──► server
          │ 4. save: save_character(brief, text) ──────────► characters.json
          ▼
     the character on stdout, the trace on stderr
```

Step 1a runs only when no answer flag and no `--load` is given ("The scene interview"). Its
answers go to the same `roll_character` call the flags fill. `--load <number>` replaces steps 2 to 4
with `load_character`: no roll, no model, no cost. It still
builds the model client, so it still needs an API key. `--seed`, `--record` and `--provider replay`
are under "Replay".

## Run options

Every flag is a row in one table in `RunOptions`, which says its default, how its value is read and
whether it goes on line 1 of a recording. The parser and the recording both read that table, so a
flag cannot be parsed and left off the recording. The same table names the `roll_character` parameter each answer fills
(`RollArguments()`).

| Flag | Default | Does |
| --- | --- | --- |
| `--provider` | `openai` | `openai`, `anthropic` or `replay` |
| `--model` | `gpt-6-luna` | The model id, lower-cased |
| `--setting`, `--want`, `--difficulty`, `--occupation` | an inn in a major city, the name of a fence, `Wall`, `innkeeper` | The GM's answers, sent to `roll_character`. Any one of them skips the interview |
| `--seed <n>` | none | Fixes the dice (see "Replay") |
| `--load <n>` | none | Prints saved character n instead of making one |
| `--record <file>` | none | Also writes every model call to a file. Needs `--seed` |
| `--recording <file>` | none | The file `--provider replay` answers from |

A flag the table does not know, a flag with no value (last, or before another flag) and a stray word
stop the run in `RunOptions.Parse`, before any model client is built. So do `--record` without
`--seed`, `--recording` without `--provider replay`, `--record` with it, and `--record` with no
answer flag (a recording replays flags, and a typed scene is not one). `RunOptions.Interview` is true
when no answer flag and no `--load` was typed; defaults do not count as typed.

## The server's tools

| Tool | Called by | Does |
| --- | --- | --- |
| `lookup_archetype` | the model | Says how a kind of person usually behaves |
| `roll_character` | the app only | Rolls the brief. Any rolled trait can be passed in instead. Each parameter has a `[Description]`, which the interview reads |
| `save_character` | the app only | Saves a brief and its text, returns the new number |
| `load_character` | the app only | Returns a saved brief and text by number |

`McpToolSource` has two doors:
- **The model's door**, `ListAsync` and `InvokeAsync`, sees only the tools not in `AppOnly`. Every
  failure comes back as text starting `Tool failed:`, so the model can read it and carry on.
- **The app's door**, `CallDirectAsync`, reaches the app-only tools. A failure throws, so a failed
  roll stops the run instead of reaching the model as a brief.
- **`SchemaOf(name)`** returns any tool's input schema from `tools/list`, app-only ones included. It
  shows the model nothing.

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

A trait handed in to `roll_character` is used as given, and only the missing ones are rolled. The
difficulty filter shapes the dice, not a handed-in trait: `Easy` with an obstacle of `Wont` the GM
described is kept (ADR-007). A handed-in trait also skips its pick, so the same seed rolls the other
traits differently from a plain roll.

## The scene interview

ADR-007 is the design. `SceneInterview.RunAsync` greets the GM, reads one line (asked again while it
is empty), and runs `AgentLoop` with the `scene-interviewer` skill and one tool, `record_answer(field,
value, quote)`. The tool lives in the console (`RecordAnswerTool`), and any other name, `roll_character`
included, fails as `Tool failed: No tool named '...'`. The interview holds no `McpToolSource`, so it
cannot roll, save or load. It returns `roll_character`'s arguments after a `yes`, or null after
anything else, end of input included, and `Program.cs` rolls only on the arguments. The loop gets
one turn per field plus three; out of turns (`TurnCapReachedException`) it goes on to the
confirmation with what was recorded, since every answer there already passed its checks.

- **The fields** come from `roll_character`'s schema (`RollField.ListFrom`): name, order,
  description, choices for an enum, and whether it is required. The SDK writes the nullable
  `ObstacleKind` as type `["string","null"]` with a null among its choices; the null is dropped. A
  trait added on the server reaches the interview with no console change.
- **The checks** are in `SceneDraft.Record`, pure, each with its own `Rejection`: an unknown field, an
  empty value, a choice not in the list, an empty quote, a quote longer than one sentence or 12 words
  (or running across two sentences of the scene), a quote not in the scene as whole words, and a
  free-text value that is not a run of whole words of its quote. `Words.Split` is the one normalising
  function: lower case, letters and digits, an apostrophe inside a word kept (curly written
  straight), everything else a separator. A rejection changes nothing; a second answer for a field
  replaces the first. The rejection's message goes back to the model. A choice is matched as
  letters only, so `Can't` finds `Cant`. A `.`, `!` or `?` ends a sentence only before a capital,
  an opening quote or the end, so `3.5` and `He...` stay in one; `Mr. Hobb` still splits, and a
  quote across it is rolled instead.
- **The labels** are decided by code, never claimed by the model: *my words* for a free-text field,
  stored as the GM typed it, cut from the scene; *inferred* for a choice, stored as the server spells
  it; *rolled* for an optional field nobody recorded, left out of the roll; *default* for a required
  field nobody recorded, which takes its flag default.
- **The confirmation** shows every field, its label, the quote as the GM typed it and the whole
  sentence it sits in. The greeting, the confirmation and the question go to stderr, so stdout stays
  the character text. Labels and quotes stop there: the writer's first message and the save are the
  same as for a flag run (ADR-003, ADR-005).
- **Untested here, needs a paid run:** whether a real model quotes the GM exactly and leaves
  unspoken fields alone. That is the skill's job and the epic's paid check.

## Spending

`SpendingCap` wraps the model client outermost, once per session, so the interview and the writer
draw on one allowance of 200,000 tokens. It adds each response's `Usage` and refuses the next call
once the total reaches the cap, before the model is asked; one call may overshoot. It refuses
streaming, which the app never uses and which it could not count. Below the cap it passes every call
through, so the replay sees nothing new.

## Writing guidance

`NpcForge.Console/skills/scene-interviewer/SKILL.md` is the interviewer's: quote the GM exactly, at
most one sentence and 12 words, never offer a value, free text only as the GM's words, a choice only
when the GM's words point to it, no follow-up questions. The interview appends the field list from
the server's schema to it.

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
| `[skill]` | `ChatAgent.cs`, `SceneInterview.cs` | Which `SKILL.md`, its length and fingerprint (the interviewer's with its field list) |
| `[loop]` | `AgentLoop.cs` | One line per model turn: tool calls, history size, tokens |
| `[tool]` | `McpToolSource`, `RecordAnswerTool` | Each call the model made, and for `record_answer` what it got back. The app's own calls have none |
| `[interview]` | `SceneInterview.cs` | The interviewer's closing line, as a trace; nothing reads it |
| `[server]` | the server | MCP requests and log lines, and `dice: seed N` or `dice: random` at the first roll |

## Starting the server

`dotnet run --project <server> --no-build`, with the path built from `AppContext.BaseDirectory`
(four folders up), so the app and the tests find it the same way. No build at launch, because build
output on stdout would corrupt the wire; the `ProjectReference` keeps the server from going stale.
Settings reach it as environment variables (`NPCFORGE_CHARACTERS`, `NPCFORGE_SEED`), which arrive
before any MCP message.

It is started in the configuration the console was built in. `dotnet run --no-build` with no `-c`
runs the Debug build, so a Release console would drive a Debug server that may be stale or missing.
`McpToolSource` reads the console assembly's `AssemblyConfigurationAttribute` and passes that as `-c`
(lesson 008).

## Storage

One JSON file, `NpcForge.Server/characters.json`: every run, numbered from 1, the brief and the text
together (ADR-003). `NPCFORGE_CHARACTERS` overrides the path. The path is always absolute, because
the server's working folder is wherever the app was started. Only the server touches the file, so a
database later changes the server and nothing else.

A replay or a recording run is not a real character, so it never uses the tracked file.
`RunOptions.CharactersPath` decides the file for every run: an `NPCFORGE_CHARACTERS` the caller set
wins; otherwise `--provider replay` and `--record` get a fresh `npcforge-replay-<guid>.json` or
`npcforge-record-<guid>.json` in the OS temp folder; a normal run and `--load` keep the tracked file.
The console passes the answer to the server and ends with `[app] saved as N in <path>` whenever it
chose the file, so the temp file can be found. The temp file is left in place on purpose.

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
  a replay that ever lost `--provider replay` fails instead of paying. A second test runs it with
  `NPCFORGE_CHARACTERS` removed and checks the tracked `characters.json` is byte-identical afterwards
  (putting it back if not) and the save is at the path the app printed.
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
- **By hand, free.** `docs/runs/README.md` has the replay command. Its save goes to a temp file,
  named on the `[app] saved as` line.
- **Build configuration.** A test that starts the app passes `-c <the configuration it was built in>`
  and checks both builds are newer than their source. The one helper that does it is
  `ConsoleProcess` (lesson 008).

## Tests

`dotnet test` is free: no network and no model.
- `AgentLoopTests`: the loop exits when the model sends no tool calls, throws at the cap, and runs a
  tool call through the real server. Fake models throughout.
- `McpToolSourceTests`, against the real server: the model can neither see nor name an app-only
  tool, a save loads back byte for byte, an unknown number stops the run, and the same seed rolls the
  same brief from two servers.
- `ReplayTests`: the whole app against the recorded run, as above, and the temp-file save.
- `ReplaySensitivityTests`: the replay fails on a changed system prompt, an emptied tool list, a
  changed model option and a dropped model turn.
- `OfflineTests`: `NPCFORGE_OFFLINE=1` refuses both paid providers, and replay still works.
- `RunOptionsTests` and `FlagRefusalTests`: the flag table, its refusals, when the interview runs,
  and which save file a run uses; and a typo'd flag next to the paid default stops at the parser,
  through the whole app.
- `RollFieldTests`, against the real server: the eleven fields in order, the four required, the two
  choice lists, a description on each, and a flag default for every required field.
- `SceneDraftTests`: every check of ADR-007 with its reason, the labels, the value and quote as the
  GM typed them, rolled and default fields, and the roll arguments. Pure.
- `SceneInterviewTests`: a scripted stand-in interviewer. The model is offered only `record_answer`
  and naming `roll_character` fails; anything but `yes`, end of input included, returns nothing to
  roll; `yes` returns the arguments only after the confirmation was shown; the tavern scene's
  confirmation; a described trait reaches the brief from the real server; and the app with no answer
  flag waits for a scene and, at end of input, rolls and saves nothing.
- `SpendingCapTests`: the cap refuses the call after the limit without asking the model, and stops
  an agent loop before its turn cap.
- `ConsoleProcess`: not a test. The one place a test starts the app as a process: the build
  configuration, a freshness check, the dead proxy, a timeout, both streams read, and stdin always
  its own (closed after any input given), so a run that reaches the interview never waits on a GM.

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
| Harden the safety net | A typo'd flag, a free run reaching a model, a replay writing over the owner's saves, and a Release test replaying the Debug build all stop or fail loudly | |
