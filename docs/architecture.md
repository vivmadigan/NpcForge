# Architecture

How NpcForge fits together now. This page is a map: how the parts connect, the rules that hold
across them, and what you cannot find by looking. The detail lives where it can be checked: the code,
the tests that hold it, and the decisions in `docs/decisions/`, whose `INDEX.md` lists every one in
force. Why it is shaped this way is in `README.md`.

## Shape

```
NpcForge.slnx
├── NpcForge.Core      every rule the front ends share: the brief, the tables, the dice, the namer,
│                      the writer, the interview, the loop, the save rules and the skills
├── NpcForge.Adapters  what two front ends share from the outside world: the model clients and their
│                      default provider and model, record and replay, and the file store
├── NpcForge.Console   the app: flags, the lab, the save question, the list and the owner's characters.json
├── NpcForge.Api       the website's API, a minimal API on the core: the saves, and the forge's interview, roll and save
├── NpcForge.AppHost   Aspire's AppHost: starts the API and the pages with one command
└── NpcForge.Tests     xUnit, free: fake models, the real dice and tools, the whole app against a recording,
                       and the API run in-process

NpcForge.Web           the website's pages, outside the solution: Vite, React and TypeScript, with npm
```

Each front end calls the core in-process and hands it what lives outside it: an `IChatClient` for the
model and an `ICharacterStore` for the saves, the core's two ports (ADR-014). Both front ends build
them from `NpcForge.Adapters`, which references the core and the vendor packages and never a front
end (ADR-019, `AdaptersReferenceTests`). An adapter only one front end uses stays in that front end.
Inside the core, folders follow the domain: `Brief`, `Interview`, `Writing`, `Loop`, `Saves`.
`CoreReferenceTests` and the core's `BannedSymbols.txt` hold the core's edge: it references only .NET
and Microsoft.Extensions.AI.Abstractions, writes only traces to the console, and reads no file but the
bundled skills, through `SkillFiles`. `NpcForge.Web` reaches the core only through `NpcForge.Api`.
Until epic 5 the dice and the saves lived in an MCP server the console started; ADR-016 records the
switch.

## One run, end to end

```
you ──► Program.cs
          │ 1. the saves: a FileCharacterStore on characters.json, or the file the run names;
          │    --list and --load stop here, with no client and no dice
          │ 2. the model client, the options and the run's one Dice
          │ 2a. interview, only with no answer flag: read the scene, a model
          │     records the GM's words with record_answer, show them, wait for "yes"
          │ 3. roll: dice.Roll(arguments), in-process ──────► core: tables and dice
          │ ◄─────────────────────────────────────────────── the brief, typed
          │ 4. name: the namer skill; the race, the gender, the name dice and the scene
          │       AgentLoop ◄──► model, which must call give_name (ConsoleTool),
          │       checked against the dice; no accepted name stops the run here
          │ 5. write: SKILL.md plus the brief's one tone file as the system message;
          │       the first user message holds the GM's scene (interview runs
          │       only), the name and the brief
          │       AgentLoop ◄──► model, which may call lookup_archetype (ConsoleTool)
          │ 6. show the character, then ask: save, again or leave
          │       save: the store keeps the brief, the name and the text
          │       again: back to 3, same answers, scene, dice and client
          │       leave, or end of input: nothing saved
          ▼
     each character on stdout, the trace on stderr
```

Steps 3 to 5 are one call, `CharacterRun.RollAndWriteAsync`, and `CharacterRun.SaveAsync` is the save
(ADR-013). The answers and the scene are one `Ask`, built once, so a roll again rolls only what was
rolled. Every roll uses the run's one `Dice`, so under `--seed` the next roll takes the next dice and a
run with roll agains gives the same characters every time. `KeepQuestion` reads `save`, `again` or
`leave`, trimmed and in any case, and asks again for anything else; only the character on the screen
when the GM types `save` is saved.

An interview's yes hands back the scene as the GM typed it, and the writer is sent it word for word
between `<scene>` tags, then the name, then the brief, which stays last (ADR-008,
`ChatAgent.FirstMessage`). Code never reads the scene, and it is not saved. The order at start-up is
config, flags, the saves, `--list` or `--load`, the model client, then the dice, so `--list` and
`--load` need no key and print no dice line.

The website runs the same core calls in the same order, one request per step ("The website").

## The website

### The pages

`NpcForge.Web` holds two pages. The forge page at `/` is where a GM describes a scene and gets a
character; the saved page lists the saves at `/saved` and opens one at `/saved/<number>`. A page gets
data only through the functions of `src/api.ts`, each one route of `NpcForge.Api`, called as
`/api` on the page's own address and forwarded by `vite.config.ts` to `API_HTTP` (set by the AppHost)
or `http://localhost:5180` (ADR-017, ADR-019 decision 8). The folder is not in `NpcForge.slnx`, so
`dotnet build` and `dotnet test` never start Node.

The forge page holds one `ForgeState` and draws it with one switch; one step replaces the last, and
the GM's scene stays above every step after the first. The confirmation is the scene and a few
sentences that `src/sentences.ts` writes from the fields the scene settled, and no page shows the
rolled brief (ADR-018). Colours, typefaces and type sizes live in `src/tokens.css`; the cavern, the
flame, the title and the pixel art are drawn in code, and no text sits on the cavern (lesson 019).
`Sheet.tsx` hands a sheet to `react-markdown` whole and never reads its meaning.

Every fault throws one `ApiError` with the API's message, and each page shows its one failure state
(ADR-019 decision 4). With no API behind the proxy the message is `502 Bad Gateway`; a request that
gets no answer at all has status 0 and `The API did not answer.` After a failed save the forge page
keeps the character and offers the save again first, because a new roll costs money and a second
save of one draft is safe. It does not say nothing was saved: a lost answer may hide a save that
landed. A 404 from `loadSave` is "no save numbered", not a fault.

Each state opens by name, as `?state=<name>` on its own path. `src/madeUp/states.ts` is the one table
of them, so a state that no longer fits its page fails `tsc`. `src/madeUp/` holds their data, and its
`saves.json` also seeds the browser tests' API. A
button on a named state that calls the API sends an interview id the API never held, such as
`bakery`, and so ends in the failure state on purpose.

### The API

`NpcForge.Api` is a minimal API on the core and the adapters (ADR-019). DI holds an `ICharacterStore`,
a `ModelChoice` and one `Forge`. Each route is one function of `src/api.ts`, and `ForgeApiTests` and
`SavesApiTests` hold each fault's status:

| Route | Answers |
| --- | --- |
| `GET /api/saves` | Every save in number order: number, joined name, occupation and setting, never the text |
| `GET /api/saves/{number}` | One save: number, joined name and sheet |
| `POST /api/interviews` | The scene in one go; the first step |
| `POST /api/interviews/{id}/answers` | One answer; the next step |
| `POST /api/interviews/{id}/characters` | A character; a second call rolls again on the interview's next dice |
| `PUT /api/drafts/{id}/save` | The save's number; a second save of one draft returns the first's |

A step is a 200 body with a `kind`: `question`, `confirm` (the settled fields, null for a want not
given, nothing for a field the dice will pick) or `stopped` (the cap). A roll is `written`, `no-name`
or `stopped`. The brief does not cross. The wire records (`ForgeBodies.cs`, `SaveBodies.cs`) are the
API's own, so a rename in the core cannot rename a field the page reads, and `ApiFieldNamesTests`
holds `briefFields` in `src/api.ts` equal to `Dice.Fields`.

`Forge` is one singleton holding every interview and kept draft in memory; nothing expires, so a
restart turns the page's ids into 404s. An interview holds its `SceneDraft`, one `Dice`, one
`SpendingCap` and a gate that lets one request at a time work on it, because the dice and the cap are
not safe across threads. Each model-calling step gets options of its own from `ModelOptions.New`
("The loop"). It runs `SceneInterview.ReadAsync` and
`CharacterRun.RollAndWriteAsync` as the console does, and `ForgeApiTests` holds every request it sends
a model equal to the console's for one seed. A kept draft holds the task of its first save, so a
double click saves once; a save that faulted is forgotten, so a retry can try again.

Its settings are in `ForgeSettings` and `SavesPath`, and AGENTS.md "Build and run" says how a free
run sets them. They carry the app's prefix because the host reads every environment variable as a
setting. The API has no default saves file. The AppHost checks the full-path rule ("Storage") itself
before it starts anything, because `dotnet run` starts it in its project folder and the store's
refusal would show only in the dashboard. A provider the API cannot build stops it at start-up, and
`replay` is the console's alone, because it needs a recording.

The model client is made at the first model call, not at start-up, so the saved page works with no
key, and a factory that throws is not kept, so a key set later is found without a restart. The API
carries the console's user-secrets id, so a Development run finds the console's keys. `ToneFiles` and
`NotGivenFiles` are checked at start-up.

Faults are problem details. A model that cannot be built (no key, or the offline stop) is a 503,
mapped around the client's creation alone, because its error type is one a bug elsewhere throws too.
A save file that will not parse is a 500 with the store's message; the routes catch
`InvalidDataException` only. A file held longer than the store waits ("Storage"), and any other
fault, reaches the host as the bug it is. The spending cap and no name are 200 bodies, because the page shows them
as states.

`StandInChatClient` is the free, scripted model for `stand-in`. It tells the namer by its system
message and the interviewer by the `record_answer` tool it is offered; the writer is the rest. It
records only choice fields, so the interview asks for the Ask fields and the want ends up not given.
It names inside the dice it is sent and returns one fixed sheet. Each response reports `TokensPerCall` tokens, so a test can reach
the cap.

### The AppHost

`dotnet run --project NpcForge.AppHost` starts the API and the pages; the dashboard's address and
login are in its output. The pages come in through `AddViteApp`. Aspire adds its own `--port` to the
dev script and Vite honours the last one, so under the AppHost the pages take a port Aspire picks,
while `npm start` keeps its fixed 5173. The API listens behind Aspire's proxy on its launch profile's
port. The AppHost's launch profile is http only, so no development certificate is
needed. The AppHost's environment passes through to the API, which is how AGENTS.md's free run works.
ADR-020 pins Aspire's version, and `Directory.Build.props` makes a NuGet audit advisory of
moderate or higher a build error in every project, so a vulnerable pin fails the build.

## Run options

Every flag is a row in one table in `RunOptions`: its default, how its value is read, which
`roll_character` parameter it fills, and whether it goes on line 1 of a recording. The parser and the
recording both read that table, so a flag cannot be parsed and left off a recording. `--provider` and
`--model` default to `ModelClients.DefaultProvider` and `DefaultModel`, which the API reads too.

An unknown flag, a flag with no value and a stray word stop the run in `RunOptions.Parse`, before any
model client is built. So do `--record` without `--seed`, with `--provider replay` or with no answer
flag (a recording replays flags, and a typed scene is not one), and `--recording` without
`--provider replay`.
`RunOptions.Interview` is true when no answer flag and no `--load` was typed; defaults do not count.

## The lab

`--lab` writes one brief several ways and lays the results out side by side, so a change to the writer
is judged on a sheet and not from memory (ADR-009). One axis per lab: `skill=<dir>,<dir>`,
`temperature=<t>,<t>` or `tone[=<tone>,<tone>]`. It needs `--seed` and the answers as flags. The lab
rolls once and names once, then writes every variant and take with that name through
`ChatAgent.WriteAsync`, the call a normal run makes. A tone variant is rolled again with every field
handed back in and only `tone` replaced (`BriefJson.WithField`), so the dice stay the only author of
briefs. Before any model call it prints an estimate against the session cap and waits for `yes`. Blind
mode shuffles from the seed and sends `Console.Error` to a null writer while it runs, so no trace gives
the key away; that is why `LabRunTests` runs alone. `--score <dir>` marks the guesses with no model,
no dice and no key. `Lab.cs` owns the run and `LabSheet.cs` the sheets, which are pure.

## What the model can reach

The writer is offered one tool, `lookup_archetype`, which says how a kind of person usually behaves:
`CharacterRun.WriterTools`, a `ConsoleTool` over `Archetypes.Lookup`. The lab writes through the same
list. The namer is offered `give_name` and the interviewer `record_answer`, each a `ConsoleTool` of its
own. A `ConsoleTool` answers only the name it was offered, and every failure comes back as text starting
`Tool failed:`, so the model can read it and carry on. After a malformed `lookup_archetype` call that
text is the binder's own reason (ADR-016).

Roll, save, load and list are plain methods the app calls (`Dice.Roll`, and `ICharacterStore`'s
`SaveAsync`, `LoadAsync` and `ListAsync`), never a tool in a list, so no model can see or name them
(ADR-014, decision 5).

`roll_character` is still the roll's name: `Dice` builds an `AIFunction` of that name over its typed
`Roll`, for the schema and the binding, and never offers it to a model.

## The loop

`AgentLoop.RunAsync` is the whole agent. Five rules:
- **The exit is the absence of tool calls.** Not `FinishReason`, not `Text`: each provider shapes
  those differently.
- **History is the state.** The model keeps nothing between calls, so every request resends the
  list. The caller builds it; the loop only appends.
- **Every tool call gets a result appended**, failures included. Skip one and the next request is
  malformed.
- **The turn cap is not optional.** Out of turns (`maxTurns`; the namer and the interview set their
  own), it throws.
- **The loop sets its tools on the options it is given** (`options.Tools`), so every loop needs
  options of its own: the namer runs on a copy, the console's interview gets a copy, and the API
  builds new options for each step.

`IToolSource` is the seam to where tools come from (`ConsoleTool` in the app, `FakeToolSource` in tests).
`IChatClient` from Microsoft.Extensions.AI is the seam to the vendor, and `ModelClients.Create` is
the only code that knows which vendor; the API picks its free stand-in before it. `UseFunctionInvocation` stays off: it would hide the loop.

## The brief

`CharacterBrief`, in the core. `Dice.Roll` is where loose arguments become one, and everything after it
trusts the type. Its fields, and who settles each, are `Dice.Roll`'s parameters: supplied (`setting`,
`occupation`, and `playersWant` or null for "not given", ADR-010), rolled inside what the difficulty
allows, and given, inferred or rolled (tone, race and the name dice, ADR-009; gender, age and look,
ADR-011). The difficulty filter is code: each want is tagged with the difficulties it fits, and `Easy`
never rolls `Wont`. The brief is settled once, never changes, and is half of the save format (ADR-003).

**Every die is cast, every time, in one fixed order**, and a handed-in value replaces its die's result
instead of skipping the die, so one seed rolls the same other traits whatever is handed in (ADR-009).
New dice go after the old ones, so no seed's older traits move. The wants and obstacle dice are cast
first as `NextDouble` values and read late, once the difficulty is settled (`Dice.ReadCast`), which
picks what `Next(n)` would have picked. `Looks.Read` is pure and reads the look from its casts and the
settled age. `rolls-seeds-1-to-12.json` and `looks-seeds-1-to-12.json` are golden briefs and are never
rewritten.

`Dice.Roll(arguments)` checks the arguments against `Dice.Fields` before it binds them (ADR-016). A
required field left out, a choice the binder cannot read, an undefined enum number and a blank want or
look are each a `RollRefusedException` that names the field and what it takes, and the console prints
`[app] not rolled: <reason>` with exit code 1. Every spelling the roll took before still binds
(`RollArgumentsTests`). A value of the wrong type stays the binder's own `ArgumentException`: a
caller's bug, not the GM's slip.

## The namer

ADR-009 decision 4. `Namer.RunAsync` runs before the writer, inside `ChatAgent.WriteAsync`, unless the
`WriterInput` already carries a name (the lab's variants do). It runs `AgentLoop` with the `namer`
skill, a first message that holds the race, the gender, the name dice in words and the scene, and one
console tool, `give_name(syllables, family)`, for example `["Kes", "sa"], "Vale"`. It runs on a copy
of the options, so the writer's tool list does not change.

- **The check** is `NameCheck.Check`, pure, on the call's arguments, each rule with its own
  `NameRejection` and reason. Letters ignore case as ordinal text, so a dotless `ı` is not `I`.
  `NameDice` reads the dice from the brief's JSON (`nameSyllables` is "1" to "3").
- **The first accepted name wins.** A later call is refused (`AlreadyNamed`).
- **No name, no writer.** At most `Namer.MaxTurns` turns. A name accepted before the cap
  stands; with none, `NoNameException` stops the run before the writer is paid for:
  `[app] not written: no name followed the dice (K, 2, V)`, exit code 1, nothing saved.
- **What is not checked:** that the name sounds like its race, and that the writer uses the name it
  was told. Only paid runs and the owner's eyes check those.

## The scene interview

ADR-007 is the design, ADR-010 the gaps. The interview comes in steps, so a front end that gets one
answer per request runs the same rules as the console (ADR-019, decision 2).
`SceneInterview.ReadAsync` is the model's half: `AgentLoop` with the `scene-interviewer` skill and one
tool, `record_answer(field, value, quote)`, one turn per field plus three. Out of turns, it goes on
with what was recorded, since every answer already passed its checks. It returns the `SceneDraft`,
which says what comes next: `NextQuestion`, `Answer`, then `Settled()` and `RollArguments()`.
`SceneInterview.RunAsync` drives those members on a `TextReader` and a `TextWriter` for the console.
The interview holds no dice and no store, so it cannot roll, save or load.

- **The fields** come from the roll's schema, `Dice.Fields` (`RollField.ListFrom`), and what a missing
  one gets comes from it too (`WhenMissing`): not required is *Roll*; required and nullable is
  *NotGiven*; any other required field is *Ask*. A trait added to the roll reaches the interview with
  no change here.
- **A long choice is never read from a scene.** A choice of more than
  `SceneDraft.MostChoicesFromAScene` values, such as a name letter, is left out of the interviewer's
  field list and refused by `record_answer`, so it is rolled or set by flag (ADR-009, amended
  2026-10-26). A tone list that grew past it would make tone dice-only.
- **The checks** are in `SceneDraft.Record`, pure, each with its own `Rejection`, and the rejection's
  message goes back to the model. A quote is one sentence of at most 12 words, found in the scene as
  whole words; a free-text value is a run of whole words of its quote. `Words.Split` is the one
  normalising function for the scene, the quote and the value.
- **The labels** are decided by code, never claimed by the model: *my words*, *inferred* (a choice),
  *rolled* and *not given*. An interview never uses a flag default.
- **The questions:** after the model's turns, code asks for each Ask field nobody recorded, in schema
  order, from the field's name and description. `SceneDraft.Lines` throws while an Ask field has no
  answer, so a roll with a hole cannot be built.
- **Labels and quotes stop at the confirmation.** They never reach the writer or the save.
- **Proved only by a paid run:** whether a real model quotes the GM exactly and leaves unspoken
  fields alone. That is the skill's job.

## Spending

`SpendingCap` wraps the model client outermost, once per session (in the API, once per interview), so
the interview and the writer draw on one allowance of `TokensPerSession`. It adds each response's
`Usage` and refuses the next call once the total reaches the cap, before the model is asked; one call
may overshoot. It refuses streaming, which it could not count. Below the cap it passes every call
through, so the replay sees nothing new. Every roll again draws on the same allowance, and the
console reports the stop as `[app] stopped: <the cap's message> Nothing was saved.` with exit code 1.

## Writing guidance

The skills in `NpcForge.Core/skills/` are product: what each model is told. Nothing parses
them, so editing one changes behaviour with no code change, and a change to what the model is sent
needs a re-record ("Replay").

- `scene-interviewer/SKILL.md` gets the field list from the roll's schema appended.
- `namer/SKILL.md` is sent as it is.
- `npc-writer/SKILL.md` is followed by one tone file from `tones/`, picked by the brief's `tone`, so
  the model never sees the other two. A field the brief holds as null adds
  `not-given/<field>.md` between them, so the tone still governs (ADR-010, decision 5). `ToneFiles`
  and `NotGivenFiles` stop a run, or the API, before anything is paid for when a file is missing.

The writer's and the namer's skills are sent with `\n` line endings, so a recording made on Windows
replays on Linux; the interviewer's is sent as checked out. The
`[skill]` line names the files and carries the length and a short SHA-256 of what was sent, so a run
names the guidance it was given, for example
`[skill] .../npc-writer/SKILL.md + tones/Comic.md (2871 chars, 3fa09c1e)`.

## Traces

Stdout is the answer. Everything else goes to stderr with a prefix, so one console reads as one
trace. On the website the same lines go to the API's log, where interviews interleave until epic 6
moves them (ADR-014).

| Prefix | From | Says |
| --- | --- | --- |
| `[app]` | `Program.cs`, `CharacterRun.cs`, `KeepQuestion.cs`, `Namer.cs` | What the app decided: the dice, the brief, the name, and each way a run ends (`saved as N`, `not saved`, `not rolled`, `not written`, `stopped`, `not loaded`) |
| `[skill]` | `ChatAgent.cs`, `Namer.cs`, `SceneInterview.cs` | Which skill files, their combined length and fingerprint |
| `[loop]` | `AgentLoop.cs` | One line per model turn: tool calls, history size, tokens |
| `[tool]` | `ConsoleTool` | Each call the model made, then what it got back |
| `[interview]` | `SceneInterview.cs` | The interviewer's closing line; nothing reads it |
| `[lab]`, `[score]` | `Lab.cs` | The lab's plan, estimate and refusals, and a blind lab's score |

## Storage

One JSON file, `NpcForge.Console/characters.json`, behind `ICharacterStore` (`FileCharacterStore`):
every character the GM chose to save, numbered from 1, the brief, the name and the text together
(ADR-003, ADR-009, ADR-013). A new save takes one past the highest number, which keeps the port's
promise of a number greater than every number held (ADR-015). An entry is `{ "brief": {...}, "name":
{ "syllables": ["Kes", "sa"], "family": "Vale" }, "text": "..." }`.

**An old save is never re-typed.** The store keeps each brief as the JSON it was saved as
(`SavedCharacter.Brief` is a `JsonElement`), so an old entry is written back byte for byte and never
gains a field added later (ADR-009); `SaveFileGuardTests` holds the owner's file to that. A save from
before the namer has no `name`. A missing brief key shows as unknown on `--load`; a key saved as null
is not given (`SavedView`). A new save is made from the typed brief (`SavedCharacter.Of`), so it has
every field. A file that will not parse throws `InvalidDataException` with its path; the console
reports it as one `[app]` line with exit code 1, and the API as a 500 with the same message. Every
store passes the same `CharacterStoreTests`, as epic 6's database store
must.

**The file is locked across processes.** The console and the API each hold a store on the same file
(ADR-019, decision 5). A save opens the file for itself alone across the read, the number and the
write, so two saves at the same moment get different numbers; it writes over the old bytes and then
cuts the file to length, so a save that stops part way never leaves an empty file. A list or a load
opens it shared with readers only. A store that finds the file held retries every `RetryEvery` until
`HeldFor` has passed, then throws the operating system's `IOException`, which no front end reports as one line yet.

**Which file.** `FileCharacterStore` owns the rule that a saves path is a full path with its drive,
because Windows would read any other from the current drive or folder. Both front ends build their
store before anything is paid for, so both stop there, and a new front end cannot forget it.
`RunOptions.CharactersPath` decides the console's file, so the rule has one owner: an
`NPCFORGE_CHARACTERS` the caller set wins; otherwise `--provider replay`, `--record` and `--lab` get a
fresh `npcforge-<kind>-<guid>.json` in the OS temp folder, so they never write over the characters.json
you keep, and a normal run and `--load` keep the tracked file. The console then ends with
`[app] saved as N in <path>`, so the temp file can be found. `FileCharacterStore.DefaultPath` is three
folders up from the console's build output (`bin/<config>/net10.0`), made absolute with the two-argument `GetFullPath` (lesson
015). It is the console's rule; the API never reads it.

## Replay

One real run is recorded once, and the whole app replays against it for free (ADR-005).
`docs/runs/README.md` has the commands.

- **The seed.** `--seed <n>` reaches the run's one `Dice` directly, and nothing else seeds it. The run
  says which: `[app] dice: seed N` or `[app] dice: random`.
- **Recording.** `--record <file>` wraps the real client in `RecordingChatClient`. The file is JSONL:
  line 1 is the run's answers as flags, then one line per model call with what the app sent (every
  message, every `ChatOptions` field the library serialises, each tool) and what came back.
- **Replaying.** `--provider replay --recording <file>` answers from the file, and stops the run at the
  first line where `Recording.Describe` of the app's request differs from the recorded one.
- **The tests.** `ReplayTests` runs the console as a process with line 1's flags and checks the save
  against the recording. It finds the writer's calls by their system message, not by index, because
  the namer's come first, and refuses a recording with fewer than two writer calls, because a dropped
  turn only shows on a later call. `RollAgainTests` replays `roll-again.jsonl` (`again`, then `save`) and
  also counts the `[loop]` lines, because a replay stops one call too many but not one left unused.
  `ReplaySensitivityTests` proves the replay fails on a changed prompt, tool list, option or turn.
- **Two stops on paying.** A test's child process gets a dead HTTP proxy, and `NPCFORGE_OFFLINE=1`
  makes `ModelClients.Create` refuse the paid providers before it reads a key.
- **What forces a re-record.** Anything that changes what the model is sent: a skill, a tool's
  description or schema, a `ChatOptions` field, the first message. Anything that changes the seeded
  brief: an entry in `Tables.cs`, the order of the picks, a .NET major version that changes
  `new Random(seed)`. And an upgrade of the AI or OpenAI libraries, because the recording is their
  JSON. Re-record after a deliberate behavior change or such an upgrade only, never for a restructure.
  Both recordings go stale together, and a re-record is paid, so ask the owner first.

## Tests

`dotnet test` is free: no network and no model. How to run it, and how long it takes, is in AGENTS.md
"Build and run". Each test class is named for what it guards; read it for the cases.

- **A test that starts the console** carries `[Trait("Category", "Process")]` and starts it only
  through `ConsoleProcess`: the build configuration it was built in (lesson 008), a check that the
  build is newer than its source (lesson 013; lesson 016 is the case it cannot see, a file put back
  with its old time), the dead proxy, a timeout, both streams read as
  UTF-8 (lesson 017), and stdin always its own, so a run that reaches the interview never waits on a GM.
- **The API's tests** run it in-process with `WebApplicationFactory` on a temp saves file, with no
  port. They key the factory on a type in the API (`SavesPath`, `Forge`), because the console has a
  `Program` too. No dead proxy guards them. They stay free because they run in Production, which
  loads no user secret, and the forge's tests put the stand-in under the API (`ForgeHost`).
- **A test that asserts on `Tool failed:`** must give arguments that would otherwise succeed, or it
  passes for the wrong reason (lesson 004).
- **`LabRunTests` runs alone**, because the lab swaps the process-wide `Console.Error`.

The pages have their own free suite, `npm --prefix NpcForge.Web test`: two `tsc --noEmit` checks,
`tests/checkSpecImports.mjs`, then Playwright at phone and desktop width (`playwright.config.ts`).

- **Every spec takes `test` and `expect` from `tests/fixture.ts`**, which fails a test whose page raised
  an uncaught error or left a rejected promise unhandled. The check script fails a spec that imports
  them from `@playwright/test`, which would skip that fixture.
- **Playwright starts its own API and dev server** and reuses neither (lesson 018); the ports are in
  `playwright.config.ts`. The API runs on the stand-in, seeded and with `NPCFORGE_OFFLINE=1`, on a
  temp copy of `src/madeUp/saves.json`. Tests run in parallel on that one copy, so a test checks the save it
  made, by its number, and never a total.
- **Contrast.** `tests/contrast.ts` runs axe's `color-contrast` rule on every named state and after
  each step of the flow, and a result axe cannot judge fails like a violation (lesson 019). The config
  asks for less motion, so the flame is still and the check is the same every run.
- **Screenshots.** `npm --prefix NpcForge.Web run screenshots` writes
  `docs/design/epic-13/<state>-<phone|desktop>.png`, to look at, not to compare. `npm test` never does.

## Behaviour worth knowing

Found in the debugger and in paid runs. Each one cost time once. Those that took a dead end are
lessons in `docs/lessons/`, whose INDEX is loaded into every session.
- **`FinishReason` is null on OpenAI and set on Anthropic.** A `CallId` is `call_…` on one and
  `toolu_…` on the other. Treat it as opaque and echo it exactly.
- **Never pass `ConversationId` back.** It lets OpenAI keep the history on its side. History stays
  in the app's list.
- **A `string` parameter accepts a null.** Nullability is only checked by the compiler, so
  `Dice.Roll(arguments)` binds a null `setting` or `occupation` and writes it into the brief. No
  caller sends one (ADR-016).
- **A model may skip a tool it can see.** One run wrote straight from the brief with no tool call.
  That is why the roll is never a tool.
- **Output formatting differs by model:** numbered headings, bold labels, renamed sections. Never
  parse it.
- **Names converge, differently per vendor.** OpenAI models kept reaching for `Mar-` and `Mer-`
  names; Anthropic converges on backstory instead. The name dice and the namer (ADR-009) are the
  answer: six seeds gave six names that follow their dice (`docs/runs/2026-10-27-01-epic3-checks.md`).
- **OpenAI caches the writer's prefix.** Nothing in the app asks for it; the replay fixture's writer
  calls report it as `cachedInputTokenCount`.
- **The save file is the exact record**, not a console paste (lessons 005 and 006).

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
| The writer reads the scene (epic 7) | The GM's scene reaches the writer, and the control scene gives a character who acts inside it on three seeds of three | |
| Talk to it (epic 2) | A bare run interviews the GM, every field traces to their words or the dice, nothing rolls before a yes, and a spending cap stops a session | |
| Better characters (epic 3) | Tone, race and name dice join the brief; one tone file reaches the writer; a namer names inside the dice and the save keeps it; a blind lab tells the tones apart 5 of 6 times | |
| Fill the gaps in a scene (epic 8) | A missing place or role is asked for, a missing want is not given and written for goodwill, a missing difficulty is rolled; the bakery scene passed on three seeds of three | |
| Read it to the table (epic 10) | A rolled gender, age and look join the brief with no older die moved; every sheet opens with the name and a passage to read aloud, which passed on seven runs of seven across the bakery and control scenes | |
| Faster tests (epic 9) | The free suite went from 12 m 19 s to about a minute with all 455 tests kept: a server closes at once on MCP 2.3.1, starts from its launcher, and the fast part runs on its own in 5 s | |
| Save what I like (epic 4) | Saving is the GM's choice after each character; `again` rolls new dice for the same scene on the same server, so a seeded run repeats; `--list` finds a save and `--load` opens it; the cap stops a run cleanly. The bakery scene rolled again twice and saved the third | |
| Ready to grow (epic 5) | One core, `NpcForge.Core`, called in-process by the console; saves behind `ICharacterStore`, with a file store and an in-memory store passing the same tests; the MCP server retired, with the replay and roll-again recordings passing unchanged and `--list` and `--load` printing what they printed | |
| A front end to click through (epic 12) | The website's two pages run on made-up data behind one seam, `src/api.ts`; browser tests walk the whole flow and a run that leaves, read every word of every sheet, and pass AA contrast on all 16 named states at phone and desktop width | |
| A forge in a cavern (epic 13) | The pages look like a forge in a cavern: a header, a cavern, a title drawn as a picture, a confirmation in sentences, a character page with save, roll again and leave at the top and no brief, and a black wait with a flame that holds still under less motion. Browser tests pass AA contrast on all 17 named states at both widths, with a screenshot of each in `docs/design/epic-13/` | |
| The website makes real characters (epic 14) | The pages call `NpcForge.Api` for everything, and the API calls the core in-process with the console's model clients and file store, moved to `NpcForge.Adapters`; one command starts both under Aspire. The interview takes one answer at a time, a second save of one draft saves nothing new, and the file store's lock holds between the console and the API. Free tests walk the API on a stand-in model and hold what it sends the model equal to the console's; the browser walks scene to reopened save on the stand-in, every fault ends in a failure state, a failed save keeps the character for a retry, and AA contrast passes on all 20 named states at both widths | |
