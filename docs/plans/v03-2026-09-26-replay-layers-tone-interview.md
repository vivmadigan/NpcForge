# Plan v03: Past the finish line — a replay safety net, Clean Architecture, tone and importance, and a scene interview

- **Date:** 2026-09-26
- **Supersedes:** v02
- **Plan file at this version:** `PLAN.md` at commit `2d0ad06`, unchanged. This version plans steps 9 to 13; they are not in `PLAN.md` yet.
- **Steps done at this version:** 1 to 8 (README build order 1 to 4)

## What this version believes

v02's finish line was reached: three runs give three different characters, and a save loads back
unchanged. The project now has two goals. The first is still the product: a DM describes a scene
and gets back a character whose variety comes from dice, with levers, anti-levers and consequences
that trace back to the brief. The second is new: the codebase becomes a portfolio-quality .NET app
that teaches DDD, Clean Architecture, CQRS and agent harness design.

The core principle does not move. **The model never guesses. It is either told or it rolls.**

Five steps, in this order: a free safety net that replays a whole run; the server in layers; tone
and importance; the console in layers with a hardened harness; a scene interview. Each refactor
comes straight before the feature that uses it, and no step both restructures and changes behaviour.

## Where this starts from

Facts from reading the repository on 2026-09-26, not from the docs:

- Two processes, three projects, eight free tests. `Program.cs` holds the whole run in order:
  connect, roll, run, save, or connect, load, print.
- The roll rules (the difficulty filter, Easy never rolling `Wont`) live inside the MCP tool method
  `RollCharacter` in `CharacterTools.cs`. That is a presentation adapter holding domain rules, and it
  is the first thing Clean Architecture moves.
- The brief is the save format (ADR-003), and `NpcForge.Server/characters.json` is committed.
- `AgentLoop` sets `options.Tools` on the `ChatOptions` it is handed. There is one shared
  `ChatOptions` today. Two agents sharing it (an interviewer and a writer) would overwrite each
  other's tool list.
- Doc drift: README's status says "Nothing built yet"; AGENTS.md says the test project holds three
  tests (it holds eight).

## Recommended order, and why

| Step | What | Kind |
| --- | --- | --- |
| 9 | A run you can replay for free | harness |
| 10 | The server in layers | refactor |
| 11 | Tone and importance | feature (goal 2) |
| 12 | The console in layers, and a harder harness | refactor + harness (goals 3, 4) |
| 13 | The scene interview | feature (goal 1) |

- **The safety net comes first.** Two of the five steps restructure a whole process. Today the only
  proof that a run still behaves is a paid run judged by eye. A recorded run replayed for free turns
  "behaviour unchanged" into a test result instead of an impression. It is also the most valuable
  harness improvement (goal 3).
- **A refactor and a feature never share a step.** README "Build order": "The server comes before
  Skills on purpose ... Add Skills first and there is no way to tell which change made the
  difference." Same here: a step that moves code into layers *and* adds tone cannot tell a
  refactoring bug from a tone bug. Refactor steps pass the replay test unchanged; feature steps
  re-record it on purpose.
- **Each refactor comes right before the feature that uses it, so nothing is built twice.** Tone and
  importance are nearly all server work (the brief, the tables, the roll rules, the save format), so
  the server is layered first. The interview is nearly all console work, so the console is layered
  just before it. Doing both refactors up front would mean two steps with nothing new to run, and
  abstractions designed before the features that test them (AGENTS.md: "No abstraction until there
  are two things to abstract").
- **Tone and importance come before the interview**, so the interview is built once, with every
  answer it will ever ask for.
- **The interview comes last** because it is the only step whose behaviour depends mostly on the
  model. It gains most from a hardened harness (cancellation, a budget) and from the replay net.

## Keeping the success test passing

README "Success test": the same answers, three runs, three genuinely different characters, levers
that differ, and the second one loaded back unchanged.

- **Refactor steps (10, 12):** the replay test proves the model is sent the same words and the same
  tools as before, and the same character is saved. The dice live on the server and are covered by
  its tests. No paid runs are needed for the success test itself.
- **Feature steps (11, 13):** three paid runs at the same answers, judged by eye as today, then a
  `--load`.
- **"Levers differ" stays a human check.** Step 7 found the markup differs by model ("If anything
  ever parses this output, that is what will break it"), so a parser would test formatting, not
  levers.
- **New from step 11, free: a variety floor test.** Every difficulty × tone combination keeps at
  least N entries in every table, so a filter can never quietly narrow the dice to one or two answers.

## The steps

Each step runs as the playbook describes: a sketch, `crew:challenger` before you type, you write it,
`crew:test-runner`, the debugger, then `/crew:step-done`. They are numbered after step 8, so the
crew's step tools and `git tag step-N` keep working. Paid runs are only what "done" needs, on OpenAI;
step 13 adds Anthropic because it is about model behaviour. About 15 paid runs across the plan.

### Step 9. A run you can replay for free — harness

**Learning goal.** How an LLM app gets regression-tested without paying: deterministic dice, a
model you can record and replay, and `IChatClient` middleware (`DelegatingChatClient`). That is the
same pipeline shape MediatR's behaviours will have in step 10.

**Scope**
- **Seeded dice.** The server reads a seed from the environment variable `NPCFORGE_SEED`, the same
  way it reads `NPCFORGE_CHARACTERS`, and uses `new Random(seed)`; with no seed, `Random.Shared`. The
  console gets `--seed` and passes it on the way it passes the save path. The seed is deliberately
  *not* a `roll_character` parameter. That keeps a rule step 13 depends on: `roll_character`'s
  parameters are exactly the brief fields a DM may set, and any app-only setting lives in the
  environment. A seed reproduces the brief because the pick order in `RollCharacter` is fixed.
- **`--record <file>`** wraps the real client in a `RecordingChatClient : DelegatingChatClient`, which
  writes every request and response to a JSONL file. That file is a byte-exact record of the model's
  side (lesson 006). It holds none of the stderr trace, so it does not solve lesson 005; the console
  paste still carries the trace.
- **`--provider replay --recording <file>`**: `ModelClients.Create` returns a `ReplayChatClient` that
  answers from the file in order, and fails if the request differs from the recording. It compares:
  - the messages the app wrote (system, user, tool results), not the recorded assistant messages,
    which round-trip through JSON and may lose provider-specific parts;
  - the request's options: tool names and schemas, `ModelId` and `MaxOutputTokens`. Messages alone
    cannot see a lost tool list: the replayed `lookup_archetype` call would still succeed, because
    `McpToolSource.InvokeAsync` finds tools in its own list, not in `options.Tools`. Step 12 changes
    exactly this (`Clone()`);
  - text with line endings normalised. `SKILL.md` is LF in git and CRLF on disk here
    (`core.autocrlf=true`), so an exact comparison would fail on a fresh LF checkout.
- **A test runs the console as a process**, the way the tests already run the server, with `--provider
  replay`, the recorded seed and answers, and a temp save file. It is a black box on purpose, so it
  survives step 12 moving every class in the console. **It asserts on the save file, not on stdout.**
  The challenger's probe (2026-09-26): a child process here writes stdout in code page 850, which
  turned `won’t — “name”` into `won't - "name"`, and Test Explorer runs with no console at all. The
  save is byte-exact. Apply lesson 006's `Console.OutputEncoding = Encoding.UTF8` as well, but do not
  build the test on it.
- Considered and not chosen: a scripted fake with a golden file of the app's requests. It is just as
  refactor-proof and needs no paid run, but it is not a real provider's response shape, and the
  recording client is also the transcript.

**Done when** the replay test passes with no network, and each of these makes it fail: one word
changed in `SKILL.md`, and the tool list emptied before the model call. `dotnet test` is still free.

**Verify.** Paid: one OpenAI run with `--record` and `--seed`, committed as the fixture. Debugger:
break in `ReplayChatClient.GetResponseAsync` during the test and compare the incoming history with
the file's line. Break in `RollCharacter` twice with the same seed and see the same traits.

**Watch for.** `new Random(seed)` is not guaranteed to give the same sequence across .NET major
versions, so a runtime upgrade means re-recording. Check that the server, started by the console
inside the test, gets the temp save path and the seed: either it inherits them from the console's
environment, or the console passes them on.

### Step 10. The server in layers — refactor

**Learning goal.** The dependency rule enforced by the compiler; DDD's building blocks on a real,
small domain; CQRS as separate command and query handlers; handlers written by hand, then MediatR.

**Scope, as three runnable slices**
- **10a. The roll.** New projects: `NpcForge.Server.Domain` (the brief as a value object,
  `Difficulty`, `ObstacleKind`, the tables, the roll rules) and `NpcForge.Server.Application` (the
  handler). `NpcForge.Server` stays the host and becomes presentation: each MCP tool method shrinks to
  "map the arguments, call the handler, return". `Random` is injected. It already has two instances,
  shared and seeded, so no `IDice` interface is needed.
- **10b. Save and load.** `NpcForge.Server.Infrastructure` holds the JSON file (today's `Storage.cs`)
  behind `ICharacterRepository`. `SavedCharacter` becomes the aggregate, with its number as identity.
  It is where importance, "which ending happened" and revisions attach later. The file's shape
  becomes a stored type of its own in Infrastructure, mapped to and from the domain by the
  repository, so the save format can outlive changes to the domain brief. That split is a
  restructure, so it belongs here, not in step 11 (see "The save format" under step 11).
- **10c. MediatR.** With three handlers, one cross-cutting behaviour (a `[server]` trace line per
  request, with its name and duration) justifies the pipeline. Current MediatR, with a Community
  licence key in the server's configuration. A new `NpcForge.Server.Tests` project holds domain and
  handler unit tests with a seeded `Random`: fast, with no process.

**For the sketch, not decided here.** Is rolling a command or a query? It changes nothing, so by
CQRS's own definition it is a query, even though asking twice gives two answers. Decide it in the
sketch and write down why.

**Done when:**
- every existing test and the replay test pass with no test edits;
- a snapshot test of `tools/list` (names and input schemas), written at the start of the step,
  still matches, since that JSON is the only contract between the processes;
- the committed `characters.json` loads through the new repository and its stored type, and saving
  after it works;
- `NpcForge.Server.Domain.csproj` has no package or project references;
- the console has no diff.

**Verify.** Paid: none. Debugger: break in the `roll_character` tool method and step into the
handler and then the domain roll: three projects, one call stack. In 10c, break in the behaviour and
watch every request pass through it.

**Watch for.** Nothing may write to stdout on the server (BUILD.md "Things that will bite").
MediatR logs a warning when there is no licence key. Confirm it goes through the logger (stderr) and
not to the console, and set the key so it never appears. The server has no user-secrets yet, and the
Host adds them only in Development. Lesson 003 applies to every new `.csproj`: no comment that
mentions a CLI flag.

### Step 11. Tone and importance — feature (goal 2)

**Learning goal.** The README's two-sides rule applied to a new idea; changing a save format
without breaking old saves; code choosing which skill text the writer gets.

**Scope**
- **Brief.** `Tone` (enum `Serious`, `Comic`, `Heartfelt`; set by the DM, rolled if left out) and
  `Importance` (enum `Throwaway`, `Recurring`; set by the DM, default `Throwaway`, never rolled).
- **Server, tone.** Table entries that clash with a tone are tagged `NotFor` and left out of that
  tone's roll. Exclusion rather than inclusion: most traits fit any tone, so only the clashing ones
  need tags, and variety survives.
- **Server, importance.** A `Recurring` character rolls extra fields; a `Throwaway` does not.
  Recommended: name dice that cannot clash with the setting (an initial and a syllable count, "a name
  starting with K, two syllables"), plus one more field (see open questions). A table of names cannot
  see the setting: the same argument ADR-002 used against a rolled occupation.
- **Skill.** `SKILL.md` stays the core. Add `tones/serious.md`, `tones/comic.md`, `tones/heartfelt.md`,
  `importance/throwaway.md` and `importance/recurring.md`. The app loads the core plus the one tone
  file and the one importance file the brief names. Code chooses, not the model. The `[skill]` line
  lists the files and fingerprints the combined text.
- **Console.** `--tone` and `--importance`. The app reads two properties from the brief's JSON to
  choose the files; there is still no `CharacterBrief` type on the console.
- The replay fixture is re-recorded on purpose.

**The save format will break unless this is planned.** Adding a `required` property to the brief
breaks every existing save. System.Text.Json throws "missing required properties including: 'tone'"
(checked on a scratch copy, 2026-09-26). `Storage.Load` turns that into `McpException`, and
`save_character` loads the file before it writes. So the first paid run after the change would fail
at the save, after the model has been paid for. Old saves must load, and the next save must work.
Step 10b's stored type is the fix, and this is where it earns its keep. In step 11, tone and
importance are optional on the stored type ("saved before tone existed") and required on the domain
brief, and the repository maps between them. This step adds two fields; it does not restructure
anything.

**Done when**, free:
- the variety floor test passes for every difficulty × tone;
- unit tests show a given tone is kept and a missing one is rolled, clashing entries never roll for
  their tone, and `Recurring` rolls the extra fields while `Throwaway` does not;
- a copy of the committed `characters.json` loads, and a save after it succeeds.

And paid, on OpenAI, about six runs:
- three runs at the same answers and one tone give three different characters with levers that
  differ (the success test), and the second loads back unchanged;
- one run at each of the other two tones reads as that tone (your eyes);
- one `Recurring` run shows the extra fields in the writing.

**Verify.** Debugger: break in the roll handler and look at the filtered table sizes for each tone.
Break where the app builds the system message and read the combined skill text.

### Step 12. The console in layers, and a harder harness — refactor + harness (goals 3, 4)

**Learning goal.** What Clean Architecture means for an agent harness, which has almost no domain;
DI without a Host; the harness features a multi-turn conversation needs.

**Scope, as three slices**
- **12a. Write a character.** `NpcForge.Console.Application` holds `AgentLoop`, `IToolSource` and the
  `WriteCharacter` use case (roll, run, save: today's `Program.cs` and `ChatAgent` sequence).
  `NpcForge.Console.Infrastructure` holds `McpToolSource`, `ModelClients`, and the record and replay
  clients. `NpcForge.Console` keeps `Program.cs`, the flags and the console output. This is BUILD.md's
  "The dependency rule" enforced by the compiler: Application cannot reference `ModelContextProtocol`,
  OpenAI or Anthropic. `ServiceCollection` with no Host, so `Program.cs` still reads in order: build
  the services, connect, then load or write.
- **12b. Load a character.** The second use case. MediatR on the console once there are two.
- **12c. Harness.** Ctrl+C wired to the cancellation token (open since v01), cancelling the run
  rather than the app. A session token budget as `IChatClient` middleware: it counts across every
  call, which the per-run turn cap cannot. Each `AgentLoop` gets its own `ChatOptions` (`Clone()`), so
  the interviewer's tools cannot overwrite the writer's.
- No console Domain project yet. It appears with the interview's scene draft in step 13, when there
  is something to put in it.

**Done when:**
- the replay test passes unchanged, which is the proof the refactor kept behaviour. It compares the
  tool list and options as well as the messages, so a `Clone()` that loses the tools fails it;
- a fake client that waits on the token shows Ctrl+C ends the run and stops the server;
- a fake client that reports usage shows the budget stops the run;
- the Application project has no reference to the MCP or vendor packages.

**Verify.** Paid: one OpenAI smoke run. Replay swaps the client out, so it cannot prove the real
provider is still wired up through DI. Debugger: break in `WriteCharacter` and walk the call stack
from `Program.cs`. Press Ctrl+C at a breakpoint in the loop and watch the token flip.

### Step 13. The scene interview — feature (goal 1)

**Learning goal.** A second agent on the same `AgentLoop`, with a different job and a different end;
tool calls as structured output; guardrails enforced in code rather than in a prompt.

**Scope**
- **The model talks; code keeps the draft.** `SceneDraft`, the console's first Domain type, holds each
  answer together with the words you typed that support it.
- **The slots come from `roll_character`'s input schema,** which the console already holds from
  `tools/list`: the required fields, the optional ones, the enum values. A field added on the server
  reaches the interview with no console change, and no type is shared (BUILD.md: JSON over the wire
  is the contract). This works because of step 9's rule: every `roll_character` parameter is a brief
  field a DM may set, so the slot list can take all of them. Today none has a `[Description]`, and the
  interviewer needs one per field to know what it is asking for. Adding them is the one server change
  here, and it updates step 10's `tools/list` snapshot on purpose.
- **The model sees one tool, `set_answer(field, value, quote)`,** handled in the app, never on the
  server. The app rejects an unknown field, an enum value not in the list, and a quote that does not
  appear in what you typed. A rejection goes back to the model as a tool result, as failures do today.
- **What code guarantees, and what you guarantee.** Code guarantees that every value has evidence: a
  field you talked about, backed by words you typed. It cannot judge whether the value matches its
  evidence. The model may map "make it really tough" to `Wall`, and it may tidy free text, which is
  your choice (2026-09-26). So the confirmation is the guard for every field: each value is shown
  beside its quote. A model-offered value accepted with "yes, that" shows as `Wall ← "yes, that"`,
  which is visible, not hidden. "Never offer a value" goes in the skill, but it is a prompt rule, and
  BUILD.md says those are suggestions.
- **The model cannot end the interview.** When every required slot is filled, code prints the draft
  and asks you to confirm. Only then does the app roll. Traits left unfilled are rolled by the server:
  told or it rolls.
- `roll_character` stays app-only. The interview's tool list never contains it, and a test proves it.
- **A new skill, `skills/scene-interviewer/SKILL.md`:** ask for what is missing, never offer a value,
  and quote exactly.
- **Flags stay.** With every required answer on the command line there is no interview, so the replay
  test and scripted runs are unchanged.

**Done when**, free: scripted fake clients show that a value with an invented quote is rejected, an
enum value outside the list is rejected, the model cannot reach `roll_character`, and the roll waits
for your confirmation, which shows every value beside its quote (lesson 004: each rejection test uses
arguments that would otherwise succeed).

And paid, on OpenAI and Anthropic, since this step is about model behaviour:
- three interviews each, with vague and incomplete answers. The model asks for what is missing, every
  value that reached the roll has a quote you typed, and the trace shows each rejection;
- one interview through to a written, saved character.

**Verify.** Debugger: break in the `set_answer` handler and watch the draft fill. Break before the
confirmation and read the draft against what you typed.

## Goal 2 in detail: tone and importance against the README rule

"Could code produce it? Server. Does it only make sense as an instruction to a writer? Skill.
Anything that resists the question is usually two ideas under one name." Both resist it, so both
split:

| | Server (code) | Skill (writer) |
| --- | --- | --- |
| Tone | which trait entries may roll (exclusion tags); rolled when the DM leaves it out | how to play serious, comic or heartfelt |
| Importance | which extra fields roll for a recurring character | how much to write: short for a throwaway, fuller for a recurring one |

The options for tone:
- **Skill only.** Cheapest. But the brief is settled and `SKILL.md` says "Do not add, drop or soften
  a trait", so a serious scene with a rolled comic trait forces the writer to break one rule or the
  other. That is the model resolving a conflict silently, which step 7's `Easy` bullet showed goes
  differently every run.
- **Server only.** The traits fit, but the prose does not know what the tone asks of it.
- **The model picks the tone.** That is guessing. Out.
- **Both sides (chosen).** The costs: tagging work, tables that must grow (at `Wall` there are five
  wants today, before any tone filter), and a variety floor test to guard them.

Importance keeps "every run is saved" (ADR-003). It changes what is rolled and written, not what is
kept.

## Goal 3 in detail: harness improvements, ranked

1. **Record and replay** (step 9). Free regression tests of whole runs, and a byte-exact transcript.
   The most value, and every later step leans on it.
2. **Seeded dice** (step 9). A run can be reproduced.
3. **Guardrails in code on tool arguments** (step 13, the quote check). The model's output is checked
   before it becomes a fact.
4. **Cancellation** (step 12).
5. **A session token budget** (step 12). History grows in a conversation, and the turn cap cannot
   see it.
6. **One `ChatOptions` per loop** (step 12). A bug waiting for the moment there are two agents.

Not now, and why:
- `UseFunctionInvocation`: it would hide the loop, which is the thing being learned. Recommend
  closing v01's open question as "no".
- Retries: both vendor SDKs retry transient failures by default. Check that before writing any.
- Context compaction: the prompt is about 700 tokens (step 7). The budget trace will show when it is
  needed.
- Parallel tool calls and streaming: nothing here needs them yet.

## Goal 4 in detail: the target shape

```text
NpcForge.slnx
├── NpcForge.Server.Domain          brief, enums, tables, roll rules, SavedCharacter   no references
├── NpcForge.Server.Application     command and query handlers, ICharacterRepository   → Domain
├── NpcForge.Server.Infrastructure  the JSON repository                                → Application
├── NpcForge.Server                 host, DI, MCP tools as thin adapters               → all three
├── NpcForge.Server.Tests           domain and handlers, no process
├── NpcForge.Console.Domain         SceneDraft (step 13)                               no references
├── NpcForge.Console.Application    AgentLoop, IToolSource, use cases                  → Domain
├── NpcForge.Console.Infrastructure McpToolSource, ModelClients, record and replay     → Application
├── NpcForge.Console                Program.cs, flags, console I/O                     → all three
└── NpcForge.Tests                  loop, MCP and replay tests, as now
```

- **Nothing is shared between the processes.** The console never references `NpcForge.Server.Domain`.
  The tempting shortcut, reusing `CharacterBrief` in the interview, is the shared contracts project
  AGENTS.md forbids.
- **CQRS here means separate command and query handlers, not separate stores.** A read model earns
  its place with a database, later.
- **DDD where it earns it.** The vocabulary already exists: AGENTS.md "Vocabulary" is a ubiquitous
  language. The roll rules move out of an MCP adapter into the domain. `SavedCharacter` becomes the
  aggregate that later things attach to. No domain events until two things need one.
- **SOLID, concretely.** Roll rules out of the tool method (single responsibility). Handlers depending
  on `ICharacterRepository` (dependency inversion). A new tone as tags and a file rather than a change
  to the roller (open/closed). `FakeToolSource` and `McpToolSource` interchangeable under test
  (Liskov). `IToolSource`'s two members (interface segregation).

## What crosses a line

README "Out of scope":
- None of the five steps crosses it. The interview stays in the console, and storage stays a file.
- Importance comes close to "tracking how a character feels about the players over time": a
  recurring character implies a next meeting. This plan changes only what is rolled and written.
  Tracking stays out.
- The long-term vision (logins, browsing saved characters, a database) crosses two lines: "any
  interface beyond the console" and "a real database". It is not in this plan. Step 10's repository
  interface is where a database would plug in.

Project rules this plan changes, which matter more than README's list:
- AGENTS.md "Code": "No Clean Architecture layering", "No host or DI container", and "Two interfaces
  earn their place". Superseded by an ADR before step 10. "Plain over clever" and "No abstraction
  until there are two things to abstract" stay, and they decide when each project appears.
- BUILD.md "The dependency rule" ("The only bit of Clean Architecture worth the cost here") and
  "Shape" (two projects).
- README "The three questions" and "Who the character turns out to be is not one of the questions".
  ADR-002 already departed from these; tone and importance go further.
- Kept: two processes, no shared contracts project, stdout is the transport, and `roll_character` is
  never exposed to the model.

## Decisions worth recording as ADRs

- **ADR-004 Clean Architecture, DDD and CQRS, server first.** Supersedes the AGENTS.md "Code" bullets
  above and BUILD.md's dependency-rule paragraph. Record before step 10.
- **ADR-005 Whole runs are regression-tested by replaying a recorded model.** The seed in the
  environment rather than on the tool, the replay provider, what the replay compares, and why the test
  asserts on the save rather than stdout. It touches the "Recording paid runs" line in `PLAN.md`'s
  table. Record before step 9.
- **ADR-006 Handlers by hand, then MediatR under a Community licence.** The licence, the key, and why
  not 12.x or a source-generated mediator. Record at 10c.
- **ADR-007 Tone and importance join the brief.** Who sets each, the enums, exclusion tags, per-tone
  skill files, and importance not changing what is kept. Record before step 11.
- **ADR-008 A saved character's stored shape is separate from the domain brief.** How old saves load
  when the brief gains a field. Record in step 10b, once the sketch has been challenged.
- **ADR-009 The interview.** The model interprets; code owns the draft and the end; every value needs
  words you typed; your confirmation judges whether the value matches them. Record before step 13.
- A table line, not an ADR: `UseFunctionInvocation` stays off for good.

## README sections to update

- **Status:** "Nothing built yet" becomes "proof of concept complete; next, plan v03".
- **Opening / "Why this exists":** add the second purpose, learning DDD, Clean Architecture and CQRS
  in a real app.
- **"The three questions":** becomes the conversation, and the answers now include occupation, tone
  and importance.
- **"The character brief":** add tone and importance, and what a recurring character adds.
- **"Architecture":** layers inside each process. "Which part does a new idea belong to?" stays, with
  tone as a worked example.
- **"Build order":** the four steps are done; point at this plan.
- **Outside README:** AGENTS.md "Code" and "Build and run" (eight tests, not three), and BUILD.md
  "Shape", "The dependency rule" and "Build steps".

## What changed since v02

- A second goal joined the first: a portfolio-quality app that teaches DDD, Clean Architecture, CQRS
  and harness design.
  because: the proof of concept passed its success test, and the user wants the codebase to teach the
  next thing.
  driven by: user's call, 2026-09-26
- Clean Architecture on both processes, server first, reversing AGENTS.md's "No Clean Architecture
  layering" and "No host or DI container".
  because: the new goal; each project appears only when it has something to hold.
  driven by: user's call (interview, 2026-09-26); ADR-004 to be written
- A replay safety net comes before any refactor.
  because: two steps restructure a whole process, and a paid run judged by eye cannot show behaviour
  is unchanged.
  driven by: README "Build order" (one change at a time); lesson 006; the challenger (2026-09-26)
- Tone and importance join the brief, each split between server and skill.
  because: README "Which part does a new idea belong to?"; skill-only tone contradicts "do not
  soften a trait".
  driven by: user's call (interview, 2026-09-26)
- Asking for the answers moves from v02's "plain `Console.ReadLine`" idea to a model-driven interview
  with a draft kept in code.
  because: the user wants a conversation, not a form. Code makes sure every value has words you
  typed behind it, and your confirmation judges the rest, which keeps "the model never guesses".
  driven by: user's call (interview, 2026-09-26); the challenger found that the quote check alone
  cannot stop a model-offered value you accept
- "Carrying on the conversation after the character is written" is deferred to its own plan.
  because: a revision raises its own questions (does it change the brief? is it a new save?).
  driven by: user's call (interview, 2026-09-26)

## Decisions in force

v02's list, unchanged, except where noted:
- `IChatClient`, both providers behind `--provider`; a third, `replay`, from step 9
- `IToolSource` hand-written; MCP is one implementation, the fake the other
- `UseFunctionInvocation` off; recommend closing it as "no"
- No host or DI container on the console — **until step 12**, then `ServiceCollection` without a Host (ADR-004)
- `AgentLoop.RunAsync` takes `List<ChatMessage>`
- Skills as `SKILL.md` files read into a system message at index 0; from step 11, a core file plus files chosen by code from the brief
- Storage: one JSON file, every run saved, loaded by number (ADR-003)
- Occupation: supplied, default `"innkeeper"`, not rolled (ADR-002)
- OpenAI on the Responses endpoint (lesson 001)
- Traces on stderr with a prefix per side
- Starting the server: `dotnet run --project --no-build`, path from `AppContext.BaseDirectory`
- Recording paid runs: one file per run in `docs/runs/`; the `--record` transcript may join it (open question)
- ADR-001: the crew, versioned decisions, curated lessons

New, from the 2026-09-26 interview:
- Order: steps 9 to 13 as above
- Tone: both sides; the DM sets it, else rolled; a fixed list
- Importance: changes what is written and what is rolled, not what is kept; the DM sets it, default throwaway
- Interview: model-driven, with code owning the draft and the end. The model maps fixed-list answers
  and may tidy free text; every value needs a quote you typed; the confirmation shows each value beside
  its quote and is the guard
- Seed: `NPCFORGE_SEED` in the environment, never a `roll_character` parameter. That tool's parameters
  are exactly the brief fields a DM may set
- Clean Architecture: server first, console just before the interview. The stored save shape is
  separate from the domain brief from step 10b
- Mediator: handlers by hand, then current MediatR
- Paid runs: only what "done" needs, on OpenAI; Anthropic added at step 13

## Lessons that shaped this version

- 003 A csproj comment that mentions a CLI flag stops the project loading (steps 10 and 12 add
  six project files)
- 004 A test asserting on "Tool failed:" can pass for the wrong reason (step 13's rejection tests)
- 005 Redirecting a run to a file in PowerShell reorders the trace against the answer. The step 9
  transcript does not solve it: it holds the model's side, not the stderr trace, so the paste still
  carries the trace.
- 006 A run and its --load look different in a console paste when the save is exact. It is why the
  step 9 replay test asserts on the save file rather than stdout, and why the transcript is written
  by code.

## Challenged before saving

`crew:challenger`, 2026-09-26: **WEAKENED**. It confirmed every code fact in "Where this starts
from", and that a seed reproduces the brief. Everything it found was fixed above before this file
was written:
- Step 9 compared stdout, which a child process here writes in code page 850, flattening the curly
  quotes and dashes a real answer contains (its probe; lesson 006's mechanism). The test now asserts
  on the save.
- An exact comparison of the system message would fail on an LF checkout, because `SKILL.md` is
  CRLF on disk here. Line endings are normalised.
- The replay compared messages only, so step 12 could lose the tool list and still pass. It now
  compares tools and options too.
- In step 13, a model-offered fixed-list value accepted with "yes, that" passes every code check. The
  plan now says the confirmation is the guard, and you chose that (2026-09-26).
- Leaving `seed` out of the slots by name made the slot list default-open to every future app-only
  parameter. The seed moved to the environment.
- Step 11's stored save shape was a restructure inside a feature step, against this plan's own rule.
  It moved to 10b.
- The draft also claimed the transcript solves lesson 005. It does not, and the claim is corrected.

## Open questions for the user

1. **Name dice:** only for recurring characters, or for every character, since names converge on
   throwaways too (runs 05 and 08)?
2. **A recurring character's second extra field:** a tie to the place, a secret, or a want beyond
   this scene?
3. **An old save's tone and importance:** shown as "unknown", or given the defaults?
4. **Occupation in the interview:** a required question, or defaulting to "innkeeper" as today?
5. **The `--record` transcript:** does it replace the console paste in `docs/runs/`, or sit beside it?
6. **Test projects:** one per process (`NpcForge.Server.Tests` beside `NpcForge.Tests`, as planned),
   or one per layer? One per process is less to maintain; one per layer is what many portfolio
   solutions show.

Carried from v02: README's "Known gap" (the app has no world to ask about) is unchanged.
