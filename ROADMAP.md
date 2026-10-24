# Roadmap

The next tasks, in order, one card each: Task, Constraints, Done when. `AGENTS.md` says how a card is
worked. A finished card is deleted from here; what it decided goes into an ADR, and what it taught
goes into a lesson.

| Step | Task | Kind | Paid runs |
| --- | --- | --- | --- |
| 9 | A run you can replay for free | harness | 1, the recording |
| 10 | The server in layers | refactor | none |
| — | Checkpoint: did the server layers earn their place? | review | none |
| 11 | Tone, importance and name dice | feature | about 7 |
| 12 | The console in layers, and a harder harness | refactor | 1 |
| 13 | The scene interview | feature | about 7, on two vendors |

Two rules for every step:
- **A refactor and a feature never share a step.** Otherwise a refactoring bug and a feature bug
  cannot be told apart.
- **Refactor steps pass the replay test unchanged and never re-record.** Feature steps re-record once,
  after the change has settled, in the same commit as the change.

## 9. A run you can replay for free

**Task.** Make a whole run reproducible, and free to re-run.
- `--seed <number>` fixes the dice. The server reads the seed from `NPCFORGE_SEED` and says which
  it used on stderr: `[server] dice: seed N` or `[server] dice: random`.
- `--record <file>` makes a normal paid run and also writes every model request and reply to a JSONL
  file. Line 1 holds the flags the run actually used.
- `--provider replay --recording <file>` answers from the file, free. If the app sends anything
  different from what was recorded, it stops and names the first line that differs.
- A test runs the whole app as a process against the recording and checks what it saved.

Why: steps 10 and 12 each restructure a whole process, and "behaviour unchanged" has to be a test
result, not a paid run judged by eye. Design: ADR-005.

**Constraints**
- The seed is never a `roll_character` parameter. That tool's parameters are exactly the brief
  fields a GM may set, and step 13 builds its interview from them.
- The replay compares every message in order, the model's own included, plus the model id, the
  output cap, and each tool's name, description and schema, with line endings normalised. ADR-005
  says the model's messages are left out; that line is wrong, because leaving them out let a dropped
  model turn pass every test.
- Recording is `IChatClient` middleware (`DelegatingChatClient`), so nothing else knows it happens.
- `--record` needs `--seed`. A replay must never write to the file it reads.
- The test checks the save file, not stdout (lesson 006). What it expects comes from the recording.
- Making the recording is one paid run: ask first. Commit before it, and record it in `docs/runs/`.
- The fixture is `NpcForge.Tests/Fixtures/replay.jsonl`, committed. The run file in `docs/runs/`
  names it as the run's transcript; it is not copied there.
- A worked version of every file, built and run in a throwaway copy on 2026-10-01, is lines 70 to
  680 of the old plan: `git show cd20b60:PLAN.md`.

**Done when**
- `dotnet test` passes with no network, including a seed test (the same seed gives the same brief
  from two separate servers) and the replay test.
- Each of these makes the replay test fail, and is then undone: one word changed in `SKILL.md`; the
  tool list emptied before the model call; the model's turn left out of the history.
- The seed test fails without the line that passes the seed to the server.
- The fixture is committed with its run file in `docs/runs/`.

The recording command, paid. This is the only place it is written down:

```
dotnet run --project NpcForge.Console -- --record NpcForge.Tests/Fixtures/replay.jsonl --seed 1 --provider openai --model gpt-6-luna --setting "an inn in a major city" --want "the name of a fence" --difficulty Wall --occupation innkeeper
```

## 10. The server in layers

**Task.** Split the server into Domain, Application and Infrastructure projects behind the existing
host, in three slices that each build and pass:
- **10a. The roll.** `NpcForge.Server.Domain` holds the brief as a value object, the enums, the
  tables and the roll rules. `NpcForge.Server.Application` holds the handler. Each MCP tool method
  shrinks to "map the arguments, call the handler, return".
- **10b. Save and load.** `NpcForge.Server.Infrastructure` holds the JSON file behind
  `ICharacterRepository`, with a stored type of its own so the save format can outlive changes to the
  brief. `load_character` becomes a query returning `SavedCharacterView`, a read model in Application,
  filled through `ISavedCharacterReader`. A save writes the old entries back as stored.
- **10c. MediatR.** Three handlers and one behaviour that earns the pipeline: a `[server]` line per
  request with its name and duration. A new `NpcForge.Server.Tests` holds domain and handler tests
  with a seeded `Random`, with no process.

**Constraints**
- An ADR first: Clean Architecture, DDD and CQRS, server first. It replaces AGENTS.md's "No Clean
  Architecture layering" for the server. A second ADR at 10b for the stored shape, and one at 10c
  for MediatR and its licence.
- Write a `tools/list` snapshot test (names and input schemas) before moving anything. That JSON is
  the only contract between the processes.
- `Random` is passed in. Shared and seeded are the two kinds, so no `IDice` interface.
- The view's brief serialises exactly as a rolled brief does: same names, same order, every value a
  string. The round-trip test is not edited.
- Decide whether rolling is a command or a query, and write down why.
- Nothing on the server writes to stdout. MediatR's licence warning must go to stderr; then set the
  key. Lesson 003 applies to every new `.csproj`.

**Done when**
- Every existing test and the replay test pass with no test edits.
- The `tools/list` snapshot still matches.
- The committed `characters.json` loads through the new query, and a save after it leaves the old
  entries byte-identical.
- `NpcForge.Server.Domain.csproj` has no package or project references.
- The console has no diff.

## Checkpoint: did the server layers earn their place?

**Task.** A short review before step 11 builds on the layers. No code. Answer each from what
happened in step 10, not from what layers promise:
1. Did the dependency rule catch anything the old single project would have allowed?
2. Did testing get easier: how many domain tests run with no process, and how fast?
3. Does each part of step 11 (tone tags and filter, name dice, new stored fields) have one obvious
   home?
4. Can one roll be followed from the tool method to the domain in the debugger, and explained a day
   later?
5. Did MediatR pay for itself: one behaviour over three handlers, against three direct calls?

**Done when** I have chosen one: keep everything; keep the projects and drop MediatR; fold
Application and Infrastructure back into the host; or go back to the step 9 shape. Anything but
"keep everything" is an ADR, and step 12 is re-planned to match. *Needs my eyes.*

## 11. Tone, importance and name dice

**Task.**
- **Tone** (`Serious`, `Comic`, `Heartfelt`) joins the brief: set by the GM, rolled if left out.
  Table entries that clash with a tone are tagged `NotFor` and never roll for it.
- **Importance** (`Throwaway`, `Recurring`): set by the GM, default `Throwaway`, never rolled. A
  `Recurring` character rolls one extra field (open question 1).
- **Name dice** for every character: an initial and a syllable count (`One`, `Two`, `Three`), so
  names stop converging. Either can be set by hand.
- **Skill:** `SKILL.md` stays the core, plus a line that the name follows its dice. New files
  `tones/{serious,comic,heartfelt}.md` and `importance/{throwaway,recurring}.md`. The app loads the
  core plus the one tone and one importance file the brief names: code chooses, not the model.
- **Console:** `--tone` and `--importance`.

**Constraints**
- An ADR first: tone, importance and name dice join the brief.
- **Variety floor of six.** Every table keeps at least six entries in every difficulty × tone
  combination, after filtering. Wall has five wants today, so it needs at least one more, plus one for
  each Wall want the most-excluding tone removes. Tag first, then count. `obstacle`, `tone` and the
  syllable count are fixed kinds, exempt by name. The floor test calls the roll's own filter code.
- **Old saves read as "unknown"** for every field added here. The domain brief requires them; the
  stored type has them optional; the reader writes "unknown" into the view; a save writes old entries
  back untouched and never writes "unknown". An old save is read-only history.
- The console reads two properties from the brief's JSON to pick files. Still no `CharacterBrief`
  type there. The `[skill]` line lists the files and fingerprints the combined text.
- Re-record the fixture once, after the skill files settle, with `--tone` and `--importance` added.

**Done when**, free:
- The variety floor test passes for every difficulty × tone.
- Unit tests show a given tone is kept and a missing one rolled; clashing entries never roll for
  their tone; every character gets name dice; `Recurring` rolls its extra field and `Throwaway` does
  not.
- A copy of the committed `characters.json` loads with the new fields as "unknown", and a save after
  it leaves the old entries byte-identical.
- The replay test passes against the re-recorded fixture.

And paid, on OpenAI, *needs my eyes*: three runs at the same answers and one tone give three different
characters whose levers differ, the names follow their dice, and the second loads back unchanged; one
run at each other tone reads as that tone; one `Recurring` run shows the extra field.

## 12. The console in layers, and a harder harness

**Task.** If the checkpoint changed the server's shape, follow it here.
- **12a. Write a character.** `NpcForge.Console.Application` holds `AgentLoop`, `IToolSource` and the
  `WriteCharacter` use case (roll, run, save). `NpcForge.Console.Infrastructure` holds
  `McpToolSource`, `ModelClients` and the record and replay clients. `NpcForge.Console` keeps
  `Program.cs`, the flags and the console output.
- **12b. Load a character**, the second use case. MediatR on the console once there are two.
- **12c. Harness.** Ctrl+C cancels the run, not the app. A session token budget as `IChatClient`
  middleware, counting across every call. Each `AgentLoop` gets its own `ChatOptions` (`Clone()`), so
  step 13's interviewer cannot overwrite the writer's tools.

**Constraints**
- Application cannot reference `ModelContextProtocol`, OpenAI or Anthropic.
- `ServiceCollection` with no Host, so `Program.cs` still reads in order. This replaces AGENTS.md's
  "No host or DI container" for the console, in the step 10 ADR or one of its own.
- No console Domain project: `SceneDraft` (step 13) lives in Application until there is a second
  console domain type.

**Done when**
- The replay test passes unchanged. It compares the tool list, so a `Clone()` that loses the tools
  fails it.
- A fake client that waits on the token shows Ctrl+C ends the run and stops the server.
- A fake client that reports usage shows the budget stops the run.
- The Application project has no reference to the MCP or vendor packages.
- One paid OpenAI smoke run, because replay cannot prove the real provider is still wired up.

## 13. The scene interview

**Task.** The model asks for the scene in conversation instead of flags; code keeps the answers.
- `SceneDraft` holds each answer with the words I typed that support it.
- The questions come from `roll_character`'s input schema, which the console already has from
  `tools/list`, so a field added on the server reaches the interview with no console change. Each
  `roll_character` parameter gets a `[Description]`: the one server change, which updates the
  snapshot on purpose.
- The model sees one tool, `set_answer(field, value, quote)`, handled in the app. A rejection goes
  back to the model as a tool result.
- The model cannot end the interview. When every required answer is filled, code prints the draft and
  asks me to confirm. Only then does the app roll; anything unfilled is rolled.
- A new skill, `skills/scene-interviewer/SKILL.md`: ask for what is missing, never offer a value,
  quote exactly.
- With every required answer on the command line there is no interview, so the replay is unchanged.

**Constraints**
- An ADR first: the interview.
- `roll_character` stays app-only, and a test proves the interview cannot reach it.
- **One normalising function**, used the same way on my messages and on the model's quote and value:
  lowercase; curly quotes made straight; every punctuation character becomes a space, except an
  apostrophe between two letters; runs of spaces collapsed, and trimmed.
- **The quote check rejects**, in this order, each with its own reason: an unknown field; an enum value
  not in the list; a quote that is empty after normalising (its own check); a quote not found in any of
  my messages as whole words (pad both with a space, so `inn` does not match inside `dinner`).
- **The drift flag warns and never rejects**, on free-text fields only: a value with a word not in its
  quote is kept and marked at the confirmation. It cannot see a dropped word (`a castle` from
  `the setting is not a castle`); the quote printed beside the value is where I catch that. Enum
  values are never flagged.
- Tests follow lesson 004: arguments that would otherwise succeed, and every rejection asserts its
  reason.

**Done when**, free:
- An honest quote differing only by case, a full stop, a curly apostrophe and extra spaces is
  accepted (typed `We're at an inn.`, quoted `we’re at an  inn`), and so is one typed in single
  quotation marks (typed `'an inn'`, quoted `an inn`).
- A quote never typed is rejected, and so is one found only inside a longer word.
- An empty quote and a quote of only `...` are rejected, with a bare Enter and a lone "?" among my
  messages.
- An unknown field and an enum value outside the list are each rejected, with a quote I typed.
- A value with a word not in its quote is accepted and flagged. A value that reorders or drops words,
  including `a castle`, is accepted and not flagged. An enum value is never flagged.
- The model cannot reach `roll_character`, and the roll waits for my confirmation, which shows every
  value beside its quote and any flag.
- The replay test passes unchanged. A failure means the interview leaked into the writer's path: a
  finding, not a reason to re-record.

And paid, on OpenAI and Anthropic, *needs my eyes*: three interviews each with vague and incomplete
answers (the model asks for what is missing, every value that reached the roll has a quote I typed,
the trace shows each rejection and flag), and one interview through to a written, saved character.

## Open questions

1. **A recurring character's extra field:** a tie to the place, a secret, or a want beyond this
   scene? Needed by step 11.
2. **Occupation in the interview:** a required question, or default to "innkeeper" as today? Needed
   by step 13.
