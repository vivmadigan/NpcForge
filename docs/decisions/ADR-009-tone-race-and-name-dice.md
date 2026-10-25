# ADR-009: Tone, race and name dice join the brief, a namer names the character, and a lab compares writers

- **Status:** proposed
- **Date:** 2026-10-25
- **Supersedes:** none. ADR-003's save format gains a name, and its brief is kept as JSON.

## Context

Epic 3, "Better characters" in ROADMAP.md, adds three levers to the character brief (tone, race and
name dice) and a writers' lab to compare writing side by side. The owner settled these before the
design, and this record does not reopen them:
- Tone, race and the name dice are given by the GM, inferred where ADR-007 allows a choice, or rolled.
  The model never picks them.
- Tone (Serious, Comic, Heartfelt) changes only the writing, not which traits roll.
- Race is the GM's words when given, rolled from a table otherwise.
- Name dice are a starting letter and a syllable count.
- The lab comes first, with a blind mode. Recurring characters wait for epic 4.

Six things in the code make the shape less obvious than it looks:
- The server owns the brief. The console has no `CharacterBrief`, and builds the interview's fields
  from `roll_character`'s schema. `RollField.ListFrom` throws on a parameter that is not text, so an
  `int` syllable count would break the interview.
- Every property of `CharacterBrief` is `required`, and `save_character` reads every save and writes
  them all back through that type. A new `required` field makes the six real saves throw. A nullable
  one types every fresh brief as possibly missing its tone.
- A trait handed in to `roll_character` skips its die (`attitude ?? Pick(...)`), so every die after it
  moves. Under one seed, `--tone Comic` and `--tone Serious` would roll a different race and different
  name dice, which breaks "tone changes only the writing".
- The writer's system message is one file. "One tone reaches the writer, chosen by code" needs code to
  put one tone's guidance there and nothing else.
- Code may not read the model's prose, so a name inside the text cannot be checked. A name in a tool
  call's arguments can.
- The replay compares every request. A restructure must pass it unchanged. A behavior change
  re-records it, a paid run the owner approves.

## Options considered

Three design runners (Opus, Sonnet, Haiku) each sketched the whole epic without seeing the others. A
cross-judge (Sonnet) scored them against one rubric. Both the judge and the synthesis picked the Opus
sketch as the base. The real choices were these.

**Old saves.**
- **A** Nullable new fields, left out of the file when null (`WhenWritingNull`). Old saves round-trip,
  but a fresh brief is typed as possibly lacking a tone, and every reader must handle a state that only
  old saves have.
- **B** `SavedCharacter` keeps the brief as a `JsonElement`. An old entry is never re-typed, so it can
  never gain a field. `CharacterBrief` stays all-required, so every new brief carries its dice by type.
- **C** Migrate old saves to "unknown". Ruled out by the epic, and "unknown" would have to be a fourth
  tone.

**The name check.**
- **A** The writer calls a model-visible tool (`set_name` or `record_name`) in its own loop. The model can
  skip it. Code then holds text and no name, and must nudge or refuse to save. The tool's name and the
  prose's name can still differ.
- **B** A short namer turn before the writer. Its one tool, `give_name`, is its only way out with a name,
  and no accepted name stops the run before the writer is paid. The writer is then told the name, as it
  is told every trait.
- **C** No check. The owner reads the names.
- **D** The writer returns structured output with a name field. It changes `ChatOptions` on every run,
  and its markup differs by provider, which is the failure AGENTS.md warns about.
- **E** The server composes the name from syllable tables. Exact, but it overrules the decision that the
  model writes the name inside its dice, and it needs a table of sounds for every race.

**How a syllable count is checked.** Counting vowel groups in "Aeryn" or "Thaoile" is a guess, and a
tolerance of one makes it nearly empty. Having the namer hand back the name already split into its
syllables makes the count exact for what it declares.

**The lab's input.** A plan file (JSON) or flags. Flags win while a lab varies one thing at a time: the
lab's input is the run's own input plus one axis.

## Decision

Option B for old saves, option B for the name, the namer's own split for the count, and flags for the
lab. In detail:

### 1. The brief

Four flat fields, with the same names on `roll_character` and `CharacterBrief`:

| Field | Server type | In the interview | When not given | Flag |
| --- | --- | --- | --- | --- |
| `tone` | `Tone { Serious, Comic, Heartfelt }` | a choice, may be inferred | rolled | `--tone` |
| `race` | `string` | free text, the GM's words only | rolled from `Tables.Races` | `--race` |
| `nameLetter` | `NameLetter { A ... Z }` | a choice, may be inferred ("her name starts with K") | rolled from `Tables.NameLetters` | `--letter` |
| `nameSyllables` | `Syllables { One, Two, Three }` | a choice, may be inferred ("a short name") | rolled from `Tables.SyllableCounts` | `--syllables` |

- Every value is a string or a string enum, so `ListFrom` reads all four with no console change. The
  console maps `One`, `Two` and `Three` to numbers in one place, and a test fails if that map and the
  schema's enum disagree.
- None of the four has a default. The interview labels an unmentioned one rolled, and the four flags
  have no default in `RunOptions`. `RollArguments()` and `RecordedFlags()` leave out a flag that has
  no value. Today both would throw on one.
- The four flags are answer flags, like `--setting`, so any one of them skips the interview.
- **Every die is cast, every time, in one fixed order.** The order is the seven existing dice, then
  tone, race, letter and syllables. A given value replaces its die's result and never skips the die. One
  seed then rolls the same race and name dice whatever tone is given. The new dice come after the old
  ones, so a seed's old seven traits stay as they were.
- `Tables` gains `Races`, `NameLetters` and `SyllableCounts`, each a weighted table
  (`Weighted<T>(T Value, int Weight)`). Their rows are the owner's (open questions 1 to 3). The rule that
  every table keeps at least six entries covers the text tables in `Tables.cs`; the fixed enums `tone`
  and `nameSyllables` are exempt by name, as `obstacle` already is.

### 2. Old saves

```
SavedCharacter(JsonElement Brief, CharacterName? Name, string Text)
save_character(CharacterBrief brief, CharacterName name, string text)
```

- The save file keeps each brief as the JSON it was saved as. An old entry is read and written back
  without passing through `CharacterBrief`, so it can never gain `"tone": null` or `"tone": "unknown"`.
- `save_character` still binds its brief through `CharacterBrief`, so the server refuses a new brief
  missing any field. `Name` is left out of the file when null, so an old entry keeps its bytes.
- "Unknown" exists only on the screen. `--load` prints the saved brief line byte for byte, as today,
  then one more line when the save lacks fields: `[app] unknown in save 3: tone, race, nameLetter,
  nameSyllables, name`. One pure function, `SavedView.Unknown`, derives that list from the roll schema,
  so a field added later shows as unknown with no edit.

### 3. Tone reaches the writer

- Three files sit beside the skill: `skills/npc-writer/tones/Serious.md`, `Comic.md` and `Heartfelt.md`.
- The writer's system message is `SKILL.md`, a blank line, then the one tone file the brief's `tone`
  names. Code reads `tone` from the brief JSON, which is structured data. The model never sees the
  other two tones, so it cannot blend them.
- The `[skill]` line fingerprints the combined text and names the tone file:
  `[skill] .../npc-writer/SKILL.md + tones/Comic.md (2871 chars, 3fa09c1e)`.
- A brief whose tone has no file stops the run before any model call. A test lists the schema's `tone`
  enum and fails when a choice has no file.
- `SKILL.md` gains one line: the tone section sets how to write, and the brief still sets what is true.
- A lab skill variant is a folder with its own `SKILL.md` and `tones/`, so a skill and its tones are
  compared together.

### 4. The name

- `Namer.RunAsync` runs before the writer, with its own skill (`skills/namer/SKILL.md`) and one console
  tool, `give_name(syllables: string[], family: string?)`, for example `["Kes", "sa"], "Vale"`. It has
  the scene interview's shape: `record_answer` is a console tool, and so is this.
- The namer is told the race, the name dice and the scene. Code checks two things in the tool's
  arguments, each with its own rejection reason (lesson 004):
  - **The letter, exactly.** The first letter of the first syllable must be `nameLetter`, ignoring case.
  - **The count, exactly, as the namer split it.** The number of syllables handed back must equal
    `nameSyllables`. Code cannot hear the name, so a dishonest split still passes. That is a smaller
    and plainer gap than guessing the count from spelling.
- A rejected call says why, and the namer tries again. After four turns with no accepted name, the run
  stops before the writer is paid: `[app] not written: no name followed the dice (K, Two)`.
- The writer's first message gains one line, `Their name is Kessa Vale.`, before the brief, and the
  brief stays last (ADR-008). An `[app] name Kessa Vale (K, Two)` line shows on stderr.
- The family name is optional and not bound by dice.
- The save keeps the name (`syllables`, `family`). The Done when check "six different first names, each
  following its dice" then reads only structured data.
- What this cannot prove: that the writer's text uses the name it was told. The writer is told the
  name as it is told every trait, and only the owner's eyes check the text.

### 5. The lab

```
--lab tone | skill=<dir>,<dir> | temperature=<t>,<t>   --repeat <n>   --blind   --out <dir>
--score <dir>
```

- **Command.** One axis per lab, so each sheet shows one difference. `tone` with no values takes every
  choice in the schema. `--repeat` defaults to 1, and `--out` defaults to a new folder in temp, whose
  path is printed. `--lab` needs `--seed`, and refuses `--load`, `--record` and `--provider replay`.
  `--lab tone` refuses `--tone`. `--lab temperature` is refused for `--provider anthropic`, because
  claude-opus-5 and claude-sonnet-5 reject a temperature with a 400.
- **Input.** The run's own answers, from flags or from the interview. The lab rolls once. A tone
  variant's brief comes from `roll_character` with every field of the base brief handed back in and the
  tone replaced, so the server stays the only author of briefs and nothing is rolled twice.
- **One way to write.** Every variant goes through `ChatAgent.WriteAsync`, the call a normal run makes:
  the namer, then the writer. A variant changes one thing only: the skill folder, the brief's tone, or
  the temperature on a copy of `ChatOptions`. The lab saves nothing to `characters.json`. The sheet is
  the record.
- **It asks first.** Before any model call, the interview's included, the lab prints how many
  characters it will write and an estimated token count against the session cap, then waits for yes.
  Anything else, end of input included, stops before any model call. An estimate over the cap is refused
  outright. It shows tokens only: a price table would need hand upkeep.
- **Output.** `sheet.md` shows the brief once, then every character under its label and take, with its
  name and the fingerprint of the skill it was given. A variant that fails, a temperature 400 or a namer
  that never followed the dice, stays on the sheet with its reason, and the lab goes on.
- **Blind mode.** `blind.md` shows the characters shuffled as A, B, C and so on, with no labels and no
  fingerprints, and `key.json` maps each letter to its label. The shuffle is seeded from `--seed`, so a
  test can check it. While a blind lab runs, nothing that names a variant (its tone file, its name, its
  text) is printed to the screen, so the trace cannot give the key away. `--score <dir>`, run later with
  no model and no server, asks for a guess per letter, prints `n of m right` and writes `scored.md`.

### Signatures the build follows

```csharp
// Server
public required Tone Tone { get; init; }                     // CharacterBrief, with Race, NameLetter, NameSyllables
public sealed record CharacterName(IReadOnlyList<string> Syllables, string? Family);
public sealed record SavedCharacter(JsonElement Brief, CharacterName? Name, string Text);

// Console
public sealed record WriterInput(string BriefJson, string? Scene, string SkillDir);
public sealed record Written(NamedCharacter Name, string Text);
ChatAgent.WriteAsync(IChatClient client, ChatOptions options, IToolSource tools, WriterInput input, CancellationToken ct) -> Written
ChatAgent.SystemMessage(string skillDir, string briefJson) -> (string Text, string Trace)
ChatAgent.FirstMessage(string briefJson, string? scene, NamedCharacter? name) -> string
NameDice.Check(NameCheck dice, IReadOnlyList<string> syllables, string? family) -> (NameRejection, string)
Namer.RunAsync(IChatClient client, ChatOptions options, WriterInput input, CancellationToken ct) -> NamedCharacter
SavedView.Unknown(IReadOnlyList<RollField> fields, JsonElement saved) -> IReadOnlyList<string>
BriefJson.WithField(string briefJson, string field, string value) -> roll_character arguments
Lab.RunAsync(...) -> the output folder;  LabSheet.SideBySide / Blind / Score, all pure
```

A normal run reads in three files: `Program.cs`, `ChatAgent.WriteAsync`, then `Namer`. A lab run reads
in three: `Program.cs`, `Lab.RunAsync`, then `ChatAgent.WriteAsync`.

## Why

The README's case for the dice is that a model asked for a trait gives "one of about four answers,
forever". The saves show it for names: of six characters, three are Merrit Vale and two Mara Venn. Tone,
race and the name dice put that choice back with the dice.

- **Old saves as JSON** hides the old-save case inside storage instead of spreading it to every reader
  of a brief. A nullable brief would be easier to write and harder to trust, because it would let a
  fresh roll lack its dice.
- **Every die cast** is what "tone changes only the writing" means once a seed is fixed. Without it, the
  lab's tone sheet would compare three different characters, not one character in three moods.
- **One tone file** keeps the guidance for how to write in the skill, where ADR-008 put it, and keeps the
  first message to facts. The fingerprint then says which tone a run was told.
- **The namer** is the only option where no state exists in which the model skipped the check: no name,
  no writer. The cost is one small call per character, about 1,000 to 2,000 tokens.
- **The lab as flags on the existing run** gives one way to write a character, used by a run and by
  every variant, so a sheet compares what a real run would produce.

## Consequences

- Easier: a GM can ask for a mood and a people, and the name follows dice the saves can show. A
  creative change to the skill or a tone file is judged on a sheet, blind if wanted, instead of from
  memory. Old saves keep their bytes. A field added later shows as unknown with no edit.
- Harder: every character costs one more small model call. The replay fixture holds two exchanges per
  run, the namer's and the writer's. `ChatAgent` becomes the one entry point for writing, and its name
  changes from `RunAsync` to `WriteAsync`.
- `save_character` gains a `name` argument, which changes the server's `tools/list`. It stays app-only,
  so the model's tools do not change.
- The free tests prove the name follows its dice and the tone file is sent. Only paid runs show that a
  tone reads as itself, that a name sounds like its people, and that the text uses the name it was told.
- Revisit if: the lab needs two axes at once (a plan file then earns its place), a model refuses the
  namer often enough to cost real money, or epic 4 needs to type old briefs.

## Build order

Each PR starts from `master` and passes `dotnet test`. Each new test is shown failing on the code it
guards, then passing. A restructure passes the replay fixture unchanged.

1. **This ADR.**
2. **Restructure: storage keeps the brief as JSON.** *Free test:* a copy of the real `characters.json`
   goes through `Storage.Load` then `Storage.Save` and comes out byte-identical. If it does not, the
   design falls back to option A for old saves and this record is amended. The replay passes unchanged.
3. **Restructure: one way to write.** `ChatAgent.WriteAsync(WriterInput)` with a skill folder replaces
   `RunAsync`, and `RunOptions` leaves out answer flags that have no value. *Free tests:* the replay
   passes unchanged and the `[skill]` line is byte-identical; a flag with no default and no value stays
   out of the roll arguments and the recorded flags.
4. **The lab: skill and temperature axes, blind mode, `--score`.** *Free tests:* `Lab.RunAsync` with a
   stand-in model and the real server writes every variant and take to `sheet.md`; in blind mode each
   letter's text in `blind.md` matches its label in `key.json`, and the same seed gives the same
   letters; `LabSheet.Score` gives 5 of 6 from piped guesses; the confirmation refuses at end of input
   and over the cap; a failing variant stays on the sheet with its reason; a blind lab prints no
   variant's tone, name or text; the flag refusals each give their own reason. The replay passes
   unchanged. *Needs my eyes:* the confirmation and the sheet layout.
5. **Behavior: every die is cast.** *Free test:* seed 5 with `--attitude` given rolls the same brief as
   seed 5 without it, except for attitude. It fails on today's code. The fixture hands in no trait, so
   it passes unchanged.
6. **Behavior: the four fields join the brief.** Enums, tables with the owner's rows, flags, and
   `SavedView.Unknown` on `--load`. *Free tests:* a given tone or race is kept and a missing one is
   rolled; every brief carries `nameLetter` and `nameSyllables`; under one seed `--tone Comic` and `--tone
   Serious` roll the same brief except for tone; a stand-in interviewer recording `tone` gives `tone:
   Comic [inferred]`; an old save loads with the unknown line and the next save leaves its JSON
   byte-identical, with no "unknown" in the file; every text table has at least six rows; the brief's
   properties are the same set as `roll_character`'s parameters. *Paid:* re-record the fixture.
   *Needs my eyes:* the new `--load` line and the interview's confirmation.
7. **Behavior: one tone reaches the writer.** The tone files, `ChatAgent.SystemMessage`, the `SKILL.md`
   line, and the lab's `tone` axis. *Free tests:* the system message for a Comic brief holds `Comic.md`
   and neither other file; every tone in the schema has a file, and the test fails when one is deleted;
   a missing file stops the run before any model call; the lab's tone axis gives briefs that differ only
   in tone. *Paid:* re-record. *Needs my eyes:* the three tone files, which are product.
8. **Behavior: the namer and the name in the save.** *Free tests:* each rejection has its own reason; a
   stand-in namer that gives "Tobin" for K, then "Kessa", is rejected once and then accepted; a namer
   that never follows the dice stops the run and the writer is never called; the name reaches the
   writer's first message before the brief; `save_character` refuses a brief without `nameLetter`; the
   syllable map matches the schema. *Paid:* re-record, then the epic's paid checks, each asked for
   first: the blind tone check (`--lab tone --repeat 2 --blind`), six runs for names, and an elf and a
   dwarf under one seed. *Needs my eyes:* the namer's skill. This PR also closes the epic in the docs.

## Open questions for the owner

1. **The race table and its weights.** Proposed: human 6, dwarf 2, elf 2, halfling 2, gnome 1,
   half-orc 1, orc 1, goblin 1. Eight rows, and about a third of characters are not human.
2. **The letter table.** Proposed: every letter but Q, U, X, Y and Z, at equal weight, so 21 rows. A
   given `--letter Q` is still allowed.
3. **The syllable weights.** Proposed: One 1, Two 2, Three 1. Is Four wanted, for elves?
4. **One re-record or three.** As written, PRs 6, 7 and 8 each change what the model is sent, so each
   re-records the fixture: three small paid runs, about two characters' worth each. The other way is to
   build 6, 7 and 8 as one PR with one re-record, which is one larger review. Proposed: three, so each
   behavior is proved on its own.
5. **The namer's extra call.** About 1,000 to 2,000 tokens per character, interview and lab runs
   included. Proposed: accept it. The other way is option A, a tool inside the writer's own loop, which
   the model can skip.
6. **A name the GM gives** ("an innkeeper called Bram"). Proposed: not in this epic. The dice apply, and
   epic 4 can give a kept character a fixed name.
