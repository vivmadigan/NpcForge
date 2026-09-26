# Plan v04: v03 revised — layers only when they hold something, a re-record rule, two answer checks, a variety floor, and a checkpoint after the server layers

- **Date:** 2026-09-26
- **Supersedes:** v03
- **Plan file at this version:** `PLAN.md` at commit `2d0ad06`, unchanged. This version plans steps 9 to 13; they are not in `PLAN.md` yet.
- **Steps done at this version:** 1 to 8 (README build order 1 to 4)

## What this version believes

v02's finish line was reached: three runs give three different characters, and a save loads back
unchanged. The project now has two goals. The first is still the product: a DM describes a scene
and gets back a character whose variety comes from dice, with levers, anti-levers and consequences
that trace back to the brief. The second is new: the codebase becomes a portfolio-quality .NET app
that teaches DDD, Clean Architecture, CQRS and agent harness design.

The core principle does not move. **The model never guesses. It is either told or it rolls.**

Five steps and one checkpoint, in this order: a free safety net that replays a whole run; the server
in layers; a checkpoint on whether those layers earned their place; tone and importance; the console
in layers with a hardened harness; a scene interview. Each refactor comes straight before the feature
that uses it, and no step both restructures and changes behaviour. A layer or project appears only
when it has something to hold.

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
- The trait tables today: wants by difficulty are Easy 11, SomeWork 17, **Wall 5**; attitudes 20,
  mannerisms 20, weaknesses 10, things they will not discuss 10, things they are wrong about 10.
- Doc drift: README's status says "Nothing built yet"; AGENTS.md says the test project holds three
  tests (it holds eight).

## Recommended order, and why

| Step | What | Kind |
| --- | --- | --- |
| 9 | A run you can replay for free | harness |
| 10 | The server in layers | refactor |
| — | Checkpoint: did the server layers earn their place? | review, no code |
| 11 | Tone, importance and name dice | feature (goal 2) |
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
  re-record it on purpose (see "Re-recording" under step 9).
- **Each refactor comes right before the feature that uses it, so nothing is built twice.** Tone and
  importance are nearly all server work (the brief, the tables, the roll rules, the save format), so
  the server is layered first. The interview is nearly all console work, so the console is layered
  just before it. Doing both refactors up front would mean two steps with nothing new to run, and
  abstractions designed before the features that test them (AGENTS.md: "No abstraction until there
  are two things to abstract").
- **The checkpoint sits between the first refactor and the first feature built on it.** That is the
  cheapest moment to find out the layers cost more than they give: before step 11 builds on them, and
  before step 12 repeats them on the console.
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
- **New from step 11, free: a variety floor test** of at least six entries per table in every
  difficulty × tone combination (see step 11), so a filter can never quietly narrow the dice.

## The steps

Each step runs as the playbook describes: a sketch, `crew:challenger` before you type, you write it,
`crew:test-runner`, the debugger, then `/crew:step-done`. They are numbered after step 8, so the
crew's step tools and `git tag step-N` keep working. Paid runs are only what "done" needs, on OpenAI;
step 13 adds Anthropic because it is about model behaviour. About 16 paid runs across the plan,
counting the fixture recordings.

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
  writes every request and response to a JSONL file. Its first line holds the flags the run was made
  with. The file is a byte-exact record of the model's side (lesson 006). It holds none of the stderr
  trace, so it does not solve lesson 005; the console paste still carries the trace.
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
- **A test runs the console as a process**, the way the tests already run the server. It reads the
  flags from the fixture's first line, adds `--provider replay` and a temp save file, and runs. It is a
  black box on purpose, so it survives step 12 moving every class in the console. **It asserts on the
  save file, not on stdout.** The challenger's probe (2026-09-26): a child process here writes stdout in
  code page 850, which turned `won’t — “name”` into `won't - "name"`, and Test Explorer runs with no
  console at all. The save is byte-exact. Apply lesson 006's `Console.OutputEncoding = Encoding.UTF8`
  as well, but do not build the test on it.
- Considered and not chosen: a scripted fake with a golden file of the app's requests. It is just as
  refactor-proof and needs no paid run, but it is not a real provider's response shape, and the
  recording client is also the transcript.

**Re-recording.** One command, paid, kept in `.claude/rules/team.md` under "Commands":

```text
dotnet run --project NpcForge.Console -- --record NpcForge.Tests/Fixtures/replay.jsonl --seed 1 --provider openai --model gpt-6-luna --setting "an inn in a major city" --want "the name of a fence" --difficulty Wall --occupation innkeeper
```

- Every flag is spelled out, so a changed default in `Program.cs` cannot change the fixture silently.
  The fixture's first line stores them and the test replays with them, so this command is the only
  place they are typed. Step 11 adds `--tone` and `--importance` to it.
- **Re-recording after a deliberate behaviour change is expected, not a failure.** It happens only in a
  feature step, once the change has settled rather than after every tuning edit, and in the same
  commit as the change, so the fixture's diff shows what the model is now sent. While a skill is being
  tuned, the replay test is red on purpose.
- **Never re-record in a refactor step.** A replay failure there is the bug the net exists to catch.
- **In this plan:** step 11 re-records (new skill files, and tone, importance and name dice in the
  brief). Step 13 should need none. The fixture is recorded with flags, so the interview never runs in
  it, and the writer's skill, tools and brief do not change. A replay failure in step 13 means the
  interview leaked into the writer's path: a finding, not a reason to re-record.

**Done when** the replay test passes with no network, and each of these makes it fail: one word
changed in `SKILL.md`, and the tool list emptied before the model call. `dotnet test` is still free.

**Verify.** Paid: one OpenAI run with the re-record command, committed as the fixture. Debugger:
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
  behind `ICharacterRepository`. `SavedCharacter` becomes the aggregate on the write side, with its
  number as identity. It is where importance, "which ending happened" and revisions attach later.
  - **The file gets a stored type of its own** in Infrastructure, so the save format can outlive
    changes to the domain brief. That split is a restructure, so it belongs here, not in step 11.
  - **`load_character` is a query that never builds a domain brief.** Its handler lives in
    Application and returns a read model defined there, `SavedCharacterView`. A reader in
    Infrastructure maps the stored type to that view, so Application never names the stored type: the
    compiler would refuse it, and that refusal is this step's point. Two ports, both implemented by
    the one JSON file class: `ICharacterRepository` for the write side and `ISavedCharacterReader` for
    the query.
  - **The view's brief must serialise exactly as the rolled brief does**: the same property names, in
    the same order, and every value a string. Enums already travel as their names. The existing
    round-trip test (`McpToolSourceTests.cs:95`) compares the two byte for byte, and step 10 does not
    edit it.
  - **A save appends the new entry and writes the old ones back as stored,** without mapping them into
    the domain.
  - All of this matters from step 11, when old saves lack fields the domain brief requires (see
    ADR-008). In this step none of it changes anything you can see.
- **10c. MediatR.** With three handlers, one cross-cutting behaviour (a `[server]` trace line per
  request, with its name and duration) justifies the pipeline. Current MediatR, with a Community
  licence key in the server's configuration. A new `NpcForge.Server.Tests` project holds domain and
  handler unit tests with a seeded `Random`: fast, with no process. One test project per process:
  `NpcForge.Tests` stays the console's.

**For the sketch, not decided here.** Is rolling a command or a query? It changes nothing, so by
CQRS's own definition it is a query, even though asking twice gives two answers. Decide it in the
sketch and write down why.

**Done when:**
- every existing test and the replay test pass with no test edits;
- a snapshot test of `tools/list` (names and input schemas), written at the start of the step,
  still matches, since that JSON is the only contract between the processes;
- the committed `characters.json` loads through the new query, and saving after it works and leaves
  the old entries byte-identical;
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

### Checkpoint after step 10: did the server layers earn their place?

A short review before step 11 builds on the layers. "No, not all of them" is a valid result, not a
failure. It is what the checkpoint is for.

Answer each from what happened in step 10, not from what the layers promise:
1. **Did the dependency rule catch anything?** A reference the compiler refused, which the old
   single project would have allowed.
2. **Did testing get easier?** How many domain tests run with no process, and how fast, against the
   MCP-only tests before.
3. **Is step 11 easy to place?** Name where the tone tags, the tone filter, the name dice and the new
   stored fields go. Does each have one obvious home?
4. **Can you follow one roll** from the tool method to the domain in the debugger, and explain the
   call stack a day later?
5. **Did MediatR pay for itself?** One behaviour over three handlers, against three direct calls.

Outcomes: keep everything; keep the projects but drop MediatR; fold Application and Infrastructure
back into folders in the host; or return to the MVP shape. `crew:challenger` argues the other side of
whatever you conclude (one call). Anything but "keep everything" is recorded as ADR-010 before step 11
starts. It supersedes the matching part of ADR-004, and step 12 is re-planned to match, since it
repeats the same layering on the console.

### Step 11. Tone, importance and name dice — feature (goal 2)

**Learning goal.** The README's two-sides rule applied to a new idea; adding to a save format
without breaking old saves; code choosing which skill text the writer gets.

**Scope**
- **Brief.** `Tone` (enum `Serious`, `Comic`, `Heartfelt`; set by the DM, rolled if left out) and
  `Importance` (enum `Throwaway`, `Recurring`; set by the DM, default `Throwaway`, never rolled).
- **Server, tone.** Table entries that clash with a tone are tagged `NotFor` and left out of that
  tone's roll. Exclusion rather than inclusion: most traits fit any tone, so only the clashing ones
  need tags, and variety survives.
- **Server, name dice, for every character** (your answer, 2026-09-26: runs 05 and 08 show names
  converging on throwaways too). An initial and a syllable count, "a name starting with K, two
  syllables". Dice that cannot clash with the setting, where a table of names could not see it: the
  same argument ADR-002 used against a rolled occupation. Like every rolled trait, either can be set
  by hand instead. This meets ADR-002's own "revisit if" line, about name convergence needing the same
  treatment as occupation.
- **The syllable count is an enum (`One`, `Two`, `Three`), not a number,** like `Difficulty`. Every
  field in the brief then travels as a string, so an old save's view can say "unknown" in any field
  and still match a rolled brief byte for byte. An `int` could not hold "unknown", and a string
  holding `"2"` would not match the `2` the roll wrote (the challenger, 2026-09-26).
- **Server, importance.** A `Recurring` character rolls one extra field; a `Throwaway` does not.
  Which field is still open (question 1).
- **Skill.** `SKILL.md` stays the core, plus one line: the name must start with the rolled initial and
  have the rolled syllable count. Add `tones/serious.md`, `tones/comic.md`, `tones/heartfelt.md`,
  `importance/throwaway.md` and `importance/recurring.md`. The app loads the core plus the one tone
  file and the one importance file the brief names. Code chooses, not the model. The `[skill]` line
  lists the files and fingerprints the combined text.
- **Console.** `--tone` and `--importance`. The app reads two properties from the brief's JSON to
  choose the files; there is still no `CharacterBrief` type on the console.
- **The fixture is re-recorded once**, after the skill files have settled, with `--tone` and
  `--importance` added to the command.

**Variety floor: six.** Every table keeps **at least six entries** in every difficulty × tone
combination, after the difficulty filter and the tone's exclusions.
- **Why six.** The success test draws three characters, so fewer than three entries forces a repeat,
  and six is twice that. With six, three runs all get different entries about 56% of the time
  (6·5·4 / 6³). With today's five Wall wants it is 48%, and with three it is 22%. Today's narrowest
  cell, Wall wants at five, has passed the success test (step 7), so six is one above what has been
  seen to work, and nothing may drop below it.
- **What it means at Wall.** There are five Wall wants today, so step 11 adds **at least one** even
  before any tone excludes anything. It then adds **one more for each Wall want that the
  most-excluding tone removes**. If `Comic` tags two of today's five, Wall needs at least three new
  wants that fit every tone. Tag the existing entries first, then count, and make the new Wall wants
  tone-neutral so they count for all three tones.
- **Elsewhere.** Easy (11) and SomeWork (17) have room. The three ten-entry tables (weaknesses,
  things they will not discuss, things they are wrong about) can lose at most four entries to any one
  tone.
- **Kinds are not tables.** Three rolled fields come from a small fixed set of named values:
  `obstacle` (three by design, README "How difficulty works", and only two at Easy), `tone` (three,
  rolled when the DM leaves it out), and the syllable count (three). The floor test names all three
  as exempt, so the exemption is visible rather than silent. The initial is a table and meets the
  floor.
- The test calls the same filter code the roll uses, not a copy, so the two cannot drift apart.

**Old saves: tone, importance and the name dice read as "unknown".** Adding a `required` property
to the brief breaks every existing save. System.Text.Json throws "missing required properties
including: 'tone'" (checked on a scratch copy, 2026-09-26). And because `save_character` loads the
file before it writes, the first paid run after the change would fail at the save, after the model
has been paid for. Step 10b set up the fix, and this is its first use:
- The domain brief keeps tone, importance and the name dice **required**. A brief rolled now always
  has them.
- The stored type has them **optional**. Absent means the character was saved before the field
  existed.
- The Infrastructure reader writes `"unknown"` into `SavedCharacterView` for each absent field, and
  `load_character` returns that. Your answer, 2026-09-26: a default would state something nobody
  told or rolled.
- A save writes old entries back untouched. "unknown" is never written into the file.
- **An old save is read-only history.** It can never become the write-side `SavedCharacter`, because
  the domain brief requires what it lacks. A later plan that works on saved characters (revisions,
  which ending happened) must first decide what an old save's unknown fields become: supplied by the
  DM, or rolled at that moment.
- Rejected: nullable fields on the domain brief (every consumer would handle null for a case only
  history has); an `Unknown` enum member (the roll could pick it, and the interview would offer it
  as a choice); defaults (the reason above). Recorded in ADR-008.

**Done when**, free:
- the variety floor test passes for every difficulty × tone;
- unit tests show that a given tone is kept and a missing one is rolled; that clashing entries never
  roll for their tone; that every character gets name dice; and that `Recurring` rolls its extra field
  while `Throwaway` does not;
- a copy of the committed `characters.json` loads with tone, importance and the name dice as
  "unknown", and a save after it succeeds and leaves the old entries byte-identical;
- the replay test passes against the re-recorded fixture.

And paid, on OpenAI, about six runs plus the re-record:
- three runs at the same answers and one tone give three different characters with levers that
  differ (the success test), the names follow their dice, and the second loads back unchanged;
- one run at each of the other two tones reads as that tone (your eyes);
- one `Recurring` run shows the extra field in the writing.

**Verify.** Debugger: break in the roll handler and look at the filtered table sizes for each tone.
Break where the app builds the system message and read the combined skill text.

### Step 12. The console in layers, and a harder harness — refactor + harness (goals 3, 4)

**Learning goal.** What Clean Architecture means for an agent harness, which has almost no domain;
DI without a Host; the harness features a multi-turn conversation needs.

**Scope, as three slices.** If the checkpoint changed the server's shape, this step follows it.
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
- **No console Domain project in this plan.** The console has one domain type coming, `SceneDraft`
  in step 13, and one type is not a layer. It lives in `NpcForge.Console.Application` until a second
  console domain type exists (AGENTS.md: "No abstraction until there are two things to abstract").

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
- **The model talks; code keeps the draft.** `SceneDraft`, in `NpcForge.Console.Application`, holds
  each answer together with the words you typed that support it.
- **The slots come from `roll_character`'s input schema,** which the console already holds from
  `tools/list`: the required fields, the optional ones, the enum values. A field added on the server
  reaches the interview with no console change, and no type is shared (BUILD.md: JSON over the wire
  is the contract). This works because of step 9's rule: every `roll_character` parameter is a brief
  field a DM may set, so the slot list can take all of them. Today none has a `[Description]`, and the
  interviewer needs one per field to know what it is asking for. Adding them is the one server change
  here, and it updates step 10's `tools/list` snapshot on purpose. It does not touch the replay: the
  writer's tool list holds only `lookup_archetype`.
- **The model sees one tool, `set_answer(field, value, quote)`,** handled in the app, never on the
  server. Every call goes through the checks below. A rejection goes back to the model as a tool
  result, as failures do today.
- **The model cannot end the interview.** When every required slot is filled, code prints the draft
  and asks you to confirm. Only then does the app roll. Traits left unfilled are rolled by the server:
  told or it rolls.
- `roll_character` stays app-only. The interview's tool list never contains it, and a test proves it.
- **A new skill, `skills/scene-interviewer/SKILL.md`:** ask for what is missing, never offer a value,
  and quote exactly. "Never offer a value" is a prompt rule, and BUILD.md says those are suggestions.
  The checks and your confirmation are what hold.
- **Flags stay.** With every required answer on the command line there is no interview, so the replay
  test and scripted runs are unchanged.

**The answer checks.** One normalising function, applied the same way to your messages and to the
model's quote and value:
1. lowercase (invariant culture);
2. straighten curly quotes and apostrophes: `’` `‘` become `'`, and `“` `”` become `"`;
3. replace every punctuation character with a space, **except an apostrophe between two letters**,
   which stays. A space rather than nothing, so `inn—in` reads `inn in`, not `innin`. The apostrophe
   rule keeps `won’t` and `won't` as the same word `won't` (which is why step 2 matters), while
   single quotation marks around `'an inn'` still become spaces;
4. collapse runs of whitespace to one space, and trim.

- **Quote check (rejects).** In this order, each with its own reason in the rejection:
  1. an unknown field is rejected;
  2. an enum value not in the list is rejected;
  3. **a quote that is empty after normalising is rejected, by its own explicit check.** Do not leave
     it to the padding below: an empty quote matches any message that also normalises to empty, such
     as a bare Enter or a lone "?" (the challenger's probe, 2026-09-26);
  4. the normalised quote must appear in one of your normalised messages as whole words: pad both
     with a space and look for `" quote "` inside `" message "`. The padding stops `inn` matching
     inside `dinner`.

  An honest quote never fails on a capital letter, a full stop, extra spaces, a curly apostrophe or
  quotation marks.
- **Drift flag (warns, never rejects).** For free-text fields only: if any normalised word of the
  value is not among the normalised words of the quote, the value is kept and marked at the
  confirmation:

  ```text
  setting: pub  ←  "the setting is an inn"  (not in your words)
  ```

  Adding or changing a word raises the flag. Reordering or dropping words does not. The model may
  still tidy free text (your choice, 2026-09-26), and the flag makes every *added or changed* word
  visible. It will sometimes flag good tidying ("an inn" to "the inn"), and that is fine.
- **What the flag cannot see: a dropped word that changes the meaning.** `a castle ← "the setting is
  not a castle"` raises no flag, because every word of the value is in the quote (the challenger's
  probe). The quote is still printed beside the value, so the confirmation is where you catch it. No
  extra rule for this: a negation rule was offered and declined (your call, 2026-09-26).
- **Enum values are never flagged,** since they are list items, not your words. Their quote is shown
  beside them (`Wall ← "make it really tough"`), and your confirmation judges it.

**Done when**, free (lesson 004: every test gives arguments that would otherwise succeed, so the
check under test is the only thing that can decide the result):
- **Quote check.** Every rejection asserts its reason, not only that it was rejected:
  - an honest quote that differs from what you typed only by case, a full stop, a curly apostrophe
    and extra spaces is accepted (typed `We're at an inn.`, quoted `we’re at an  inn`);
  - an honest quote typed inside single quotation marks is accepted (typed `'an inn'`, quoted
    `an inn`);
  - a quote you never typed is rejected, with a known field and a valid value;
  - a quote that exists only inside a longer word is rejected (`inn` when you typed `dinner`);
  - an empty quote, and a quote of only punctuation (`...`), are rejected **with a bare Enter and a
    lone "?" in your messages**. Without those, the padding rejects an empty quote anyway, and the
    test would pass with the explicit check missing;
  - an unknown field is rejected, with a quote you typed and a value that would suit a real field;
  - an enum value outside the list is rejected, with a quote you typed.
- **Drift flag:**
  - a value with a word that is not in its quote is accepted and flagged. Assert both: it is in the
    draft, and it carries the flag;
  - a value that reorders or drops words from its quote is accepted and not flagged. This includes
    `a castle` from `the setting is not a castle`, so the test records the known limit;
  - an enum value is never flagged.
- **The interview:** the model cannot reach `roll_character`, and the roll waits for your
  confirmation, which shows every value beside its quote and any drift flag.
- The replay test passes unchanged.

And paid, on OpenAI and Anthropic, since this step is about model behaviour:
- three interviews each, with vague and incomplete answers. The model asks for what is missing, every
  value that reached the roll has a quote you typed, and the trace shows each rejection and flag;
- one interview through to a written, saved character.

**Verify.** Debugger: break in the `set_answer` handler and watch the draft fill. Step into the
normalising function with a quote that has a capital and a curly apostrophe and watch it come out
matching. Break before the confirmation and read the draft against what you typed.

## Goal 2 in detail: tone, importance and names against the README rule

"Could code produce it? Server. Does it only make sense as an instruction to a writer? Skill.
Anything that resists the question is usually two ideas under one name." All three resist it, so all
three split:

| | Server (code) | Skill (writer) |
| --- | --- | --- |
| Tone | which trait entries may roll (exclusion tags); rolled when the DM leaves it out | how to play serious, comic or heartfelt |
| Importance | the extra field a recurring character rolls | how much to write: short for a throwaway, fuller for a recurring one |
| Name | an initial and a syllable count, for every character | a name that fits the setting and the dice |

The options for tone:
- **Skill only.** Cheapest. But the brief is settled and `SKILL.md` says "Do not add, drop or soften
  a trait", so a serious scene with a rolled comic trait forces the writer to break one rule or the
  other. That is the model resolving a conflict silently, which step 7's `Easy` bullet showed goes
  differently every run.
- **Server only.** The traits fit, but the prose does not know what the tone asks of it.
- **The model picks the tone.** That is guessing. Out.
- **Both sides (chosen).** The costs: tagging work, tables that must grow (see "Variety floor" in
  step 11), and a floor test to guard them.

Importance keeps "every run is saved" (ADR-003). It changes what is rolled and written, not what is
kept.

## Goal 3 in detail: harness improvements, ranked

1. **Record and replay** (step 9). Free regression tests of whole runs, and a byte-exact transcript of
   the model's side. The most value, and every later step leans on it.
2. **Seeded dice** (step 9). A run can be reproduced.
3. **Guardrails in code on tool arguments** (step 13). The quote check rejects a value with no
   evidence, and the drift flag shows words the value added to its evidence, before either becomes a
   fact.
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

**Layers appear when they have something to hold.** A project exists when it has at least two things
to put in it, or when the compiler needs it to enforce a direction that already matters. That is
AGENTS.md "No abstraction until there are two things to abstract", applied to projects.

```text
NpcForge.slnx
├── NpcForge.Server.Domain          brief, enums, tables, roll rules, SavedCharacter   no references
├── NpcForge.Server.Application     handlers, SavedCharacterView, the two ports        → Domain
├── NpcForge.Server.Infrastructure  the JSON file class, its stored type, the reader   → Application
├── NpcForge.Server                 host, DI, MCP tools as thin adapters               → all three
├── NpcForge.Server.Tests           domain and handlers, no process
├── NpcForge.Console.Application    AgentLoop, IToolSource, use cases, SceneDraft      no references to MCP or vendors
├── NpcForge.Console.Infrastructure McpToolSource, ModelClients, record and replay     → Application
├── NpcForge.Console                Program.cs, flags, console I/O                     → both
└── NpcForge.Tests                  the console's tests: loop, MCP, replay, interview checks
```

- **The server has a domain and gets a Domain project.** The brief, the enums, the tables, the roll
  rules and `SavedCharacter` are more than enough to hold. The checkpoint after step 10 can still take
  it back.
- **The console has no Domain project in this plan.** `SceneDraft` would be its only type, so it sits
  in Application. A second console domain type is the trigger for the project.
- **Nothing is shared between the processes.** The console never references `NpcForge.Server.Domain`.
  The tempting shortcut, reusing `CharacterBrief` in the interview, is the shared contracts project
  AGENTS.md forbids.
- **One test project per process** (your answer, 2026-09-26): `NpcForge.Server.Tests` and
  `NpcForge.Tests`.
- **CQRS here means separate command and query handlers over one store, not separate stores.** The
  query side already earns something in step 11. `load_character` returns `SavedCharacterView`, a read
  model in Application, filled by a reader in Infrastructure from the stored type. It never has to
  build a domain brief that an old save cannot satisfy.
- **DDD where it earns it.** The vocabulary already exists: AGENTS.md "Vocabulary" is a ubiquitous
  language. The roll rules move out of an MCP adapter into the domain. `SavedCharacter` becomes the
  write-side aggregate that later things attach to. No domain events until two things need one.
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
  earn their place". Superseded by ADR-004 before step 10. "Plain over clever" and "No abstraction
  until there are two things to abstract" stay, and they decide when each project appears.
- BUILD.md "The dependency rule" ("The only bit of Clean Architecture worth the cost here") and
  "Shape" (two projects).
- README "The three questions" and "Who the character turns out to be is not one of the questions".
  ADR-002 already departed from these; tone, importance and name dice go further.
- Kept: two processes, no shared contracts project, stdout is the transport, and `roll_character` is
  never exposed to the model.

## Decisions worth recording as ADRs

- **ADR-004 Clean Architecture, DDD and CQRS, server first.** Layers and projects appear when they
  have something to hold: the server gets Domain, Application and Infrastructure in step 10; the
  console gets Application and Infrastructure in step 12, and no Domain project until it has a second
  domain type. Supersedes the AGENTS.md "Code" bullets above and BUILD.md's dependency-rule
  paragraph. Record before step 10.
- **ADR-005 Whole runs are regression-tested by replaying a recorded model.** The seed in the
  environment rather than on the tool, the replay provider, what the replay compares, why the test
  asserts on the save rather than stdout, and the one re-record command. **Re-recording after a
  deliberate behaviour change is expected, not a failure. It happens only in a feature step, in the
  same commit as the change, and never to make a refactor step pass.** It touches the "Recording paid
  runs" line in `PLAN.md`'s table. Record before step 9.
- **ADR-006 Handlers by hand, then MediatR under a Community licence.** The licence, the key, and why
  not 12.x or a source-generated mediator. Record at 10c.
- **ADR-007 Tone, importance and name dice join the brief.** Who sets each, the enums, exclusion
  tags, the variety floor of six, name dice for every character (meeting ADR-002's "revisit if"
  line), per-tone skill files, and importance not changing what is kept. Record before step 11.
- **ADR-008 A saved character's stored shape is separate from the domain brief.** It records:
  - the domain brief requires every field, and the stored type allows fields added after a save was
    written to be absent;
  - `load_character` is a query returning `SavedCharacterView` (Application), filled by a reader in
    Infrastructure that writes "unknown" for each absent field;
  - the view matches a rolled brief byte for byte for a new save, so every brief field must travel as
    a string or a named kind, which is why the syllable count is an enum;
  - a save writes old entries back untouched and never writes "unknown";
  - an old save is read-only history until a later plan decides what its unknown fields become;
  - the options rejected: nullable domain fields, an `Unknown` enum member, and defaults.

  Record in step 10b; step 11 is its first use.
- **ADR-009 The interview.** The model interprets; code owns the draft and the end. A quote check on
  normalised text rejects values with no words of yours behind them, with an explicit empty-quote
  rule. A drift flag marks free-text values that add or change words. It cannot see a dropped word,
  and no negation rule was added for that. Your confirmation judges the rest. Record before step 13.
- **ADR-010, only if the checkpoint says so.** What the server layers kept and dropped, and what that
  means for step 12. Record before step 11.
- A table line, not an ADR: `UseFunctionInvocation` stays off for good.

## Documents to update

README:
- **Status:** "Nothing built yet" becomes "proof of concept complete; next, plan v04".
- **Opening / "Why this exists":** add the second purpose, learning DDD, Clean Architecture and CQRS
  in a real app.
- **"The three questions":** becomes the conversation, and the answers now include occupation, tone
  and importance.
- **"The character brief":** add tone, importance and the name dice, and what a recurring character
  adds.
- **"Architecture":** layers inside each process. "Which part does a new idea belong to?" stays, with
  tone as a worked example.
- **"Build order":** the four steps are done; point at this plan.

Outside README:
- AGENTS.md "Code" (after ADR-004) and "Build and run" (eight tests, not three).
- BUILD.md "Shape", "The dependency rule" and "Build steps".
- `.claude/rules/team.md` "Commands": the re-record command, marked paid.

## What changed since v03

- **The console gets no Domain project in this plan.** `SceneDraft` lives in
  `NpcForge.Console.Application` until a second console domain type exists, and ADR-004 now says
  layers appear when they have something to hold.
  because: one type is not a layer; AGENTS.md "No abstraction until there are two things to abstract".
  driven by: user's feedback on v03, 2026-09-26
- **Re-recording has one command and a rule.** It is expected after a deliberate change in a feature
  step, and never done in a refactor step. The fixture carries its own flags.
  because: every writer skill change means a paid re-record, and without a rule a red replay test is
  ambiguous.
  driven by: user's feedback on v03. Correction: step 13 should need no re-record, because the
  interview is not in the replayed path.
- **Step 13's quote check is now two checks**: a quote check on normalised text that rejects, and a
  drift flag on free text that warns, each with tests.
  because: an honest quote must not fail on a capital letter or a full stop, and tidied free text
  should show what changed.
  driven by: user's feedback on v03; lesson 004 for the tests
- **The variety floor is six entries per table per difficulty × tone.** Wall needs at least one new
  want, plus one per want the most-excluding tone removes.
  because: v03 left N unset, and Wall already sits at five.
  driven by: user's feedback on v03
- **A checkpoint after step 10** asks whether the server layers earned their place, with ADR-010 if
  not.
  because: it is the cheapest moment to find out, before step 11 builds on them and step 12 repeats them.
  driven by: user's feedback on v03
- **Name dice for every character**, not only recurring ones.
  because: names converge on throwaways too (runs 05 and 08).
  driven by: user's answer to v03 question 2
- **Old saves read as "unknown"** for fields added after they were written. `load_character` is a
  query over the stored shape, set up in 10b.
  because: a default would state something nobody told or rolled, and the domain brief requires both
  fields.
  driven by: user's answer to v03 question 3
- **One test project per process.**
  driven by: user's answer to v03 question 6

v03's changes since v02 all stand: the second goal, Clean Architecture server first, the replay net,
tone and importance split between server and skill, the model-driven interview, and revision
deferred to its own plan.

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

From the 2026-09-26 interviews and feedback:
- Order: step 9, step 10, the checkpoint, steps 11, 12 and 13
- Layers and projects appear when they have something to hold; no console Domain project in this plan
- Tone: both sides; the DM sets it, else rolled; a fixed list
- Importance: changes what is written and what is rolled, not what is kept; the DM sets it, default throwaway
- Name dice (an initial and a syllable count) for every character; the syllable count is an enum,
  so every brief field travels as a string
- Variety floor: six entries per table per difficulty × tone; the three kinds (`obstacle`, `tone`,
  the syllable count) are exempt by name
- Old saves: fields added later read as "unknown", through a `load_character` query returning a read
  model filled from the stored shape; an old save is read-only history
- Interview: model-driven, with code owning the draft and the end. The model maps fixed-list answers
  and may tidy free text. A quote check on normalised text rejects (an apostrophe between letters
  stays; an empty quote is rejected explicitly); a drift flag on free text warns about added or
  changed words, with no negation rule; the confirmation shows each value beside its quote and is
  the guard
- Seed: `NPCFORGE_SEED` in the environment, never a `roll_character` parameter. That tool's parameters
  are exactly the brief fields a DM may set
- Replay fixture: one re-record command; re-recorded only in feature steps, never in refactor steps
- Clean Architecture: server first, a checkpoint, then the console just before the interview. The
  stored save shape is separate from the domain brief from step 10b
- Mediator: handlers by hand, then current MediatR
- Test projects: one per process
- Paid runs: only what "done" needs, on OpenAI; Anthropic added at step 13

## Lessons that shaped this version

- 003 A csproj comment that mentions a CLI flag stops the project loading. Steps 10 and 12 add six
  project files.
- 004 A test asserting on "Tool failed:" can pass for the wrong reason. It shapes step 13's check
  tests: arguments that would otherwise succeed, and a rejection that names its reason.
- 005 Redirecting a run to a file in PowerShell reorders the trace against the answer. The step 9
  transcript does not solve it: it holds the model's side, not the stderr trace, so the paste still
  carries the trace.
- 006 A run and its --load look different in a console paste when the save is exact. It is why the
  step 9 replay test asserts on the save file rather than stdout, and why the transcript is written
  by code.

## Challenged before saving

v03 was challenged on 2026-09-26 (WEAKENED); its findings and fixes are recorded in v03.

v04's changes were challenged on 2026-09-26: **WEAKENED**. It confirmed that the floor arithmetic,
the Wall consequence, the honest-quote normalisation and "step 13 needs no re-record" all hold. It
ran a throwaway C# probe of the checks outside the repository. What it found, all fixed above before
this file was written:
- **ADR-008 could not be built as drawn.** A query handler in Application cannot name the stored
  type in Infrastructure: v04's own dependency rule refuses it. The load now returns a read model,
  `SavedCharacterView`, defined in Application and filled by a reader in Infrastructure.
- **A numeric syllable count could not say "unknown"** and still match the rolled brief byte for byte
  in the existing round-trip test (`McpToolSourceTests.cs:95`). The syllable count is now an enum,
  so every brief field travels as a string.
- **ADR-008 left out a consequence:** an old save can never become the write-side aggregate. It is
  now stated as read-only history.
- **The drift flag cannot see a dropped word** (`a castle ← "the setting is not a castle"`), and
  v04 claimed it made every change visible. The claim is corrected, and the limit is written down and
  tested. A narrow negation rule was offered and you declined it; the confirmation is the guard.
- **An empty quote passes whole-word matching** whenever one of your messages normalises to empty
  (a bare Enter, a lone "?"). A naive test would pass with the rule missing (lesson 004). The rule is
  now explicit, and its test puts an empty message in the history. A test for an unknown field was
  added too.
- **`tone` was missing from the floor's exemptions,** although it is a rolled kind of three like
  `obstacle`. It is now named. And there are three ten-entry tables, not four.

## Open questions for the user

1. **A recurring character's extra field:** a tie to the place, a secret, or a want beyond this
   scene?
2. **Occupation in the interview:** a required question, or defaulting to "innkeeper" as today?
3. **The `--record` transcript:** does it replace the console paste in `docs/runs/`, or sit beside it?

Answered on 2026-09-26 and folded in above: name dice for every character; old saves read as
"unknown"; one test project per process. After the challenge, you accepted the syllable count as an
enum, the apostrophe staying inside a word, and `SavedCharacterView`, and declined a negation rule for
the drift flag.

Carried from v02: README's "Known gap" (the app has no world to ask about) is unchanged.
