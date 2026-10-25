# ADR-009: Tone, race and name dice join the brief, a namer names the character, and a lab compares writers

- **Status:** proposed
- **Date:** 2026-10-25
- **Supersedes:** three lines of earlier records, each named where it changes. ADR-003's save format
  gains a name and keeps its brief as JSON (Decision 2). ADR-007's rule that any choice may be inferred
  now holds only for a short choice (Decision 1). ADR-008's "with no scene, today's message exactly"
  holds until the namer, which adds a name line (Decision 4). The rest of each stands.

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

Seven things in the code make the shape less obvious than it looks:
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
- The interview accepts a choice on any real quote without checking that the quote supports it
  (`SceneDraft`). ADR-007 allowed that because a choice list is short and the GM can judge it at a
  glance. A list of 26 letters is neither.
- The replay compares every request, and `ReplayTests` reads the brief from the first model call. A
  restructure must pass it unchanged. A behavior change re-records it, a paid run the owner approves.

## Options considered

Three design runners (Opus, Sonnet, Haiku) each sketched the whole epic without seeing the others. A
cross-judge (Sonnet) scored them against one rubric. Both the judge and the synthesis picked the Opus
sketch as the base, with grafts from the other two. A fresh reviewer (Sonnet) then tried to break this
record against the source, and every finding is answered below. The real choices were these.

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
  skip it. Code then holds text and no name, and must nudge or refuse to save.
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

**The lab's input.** A plan file (JSON), or flags. Flags win while a lab varies one thing at a time: the
lab's input is the run's own input plus one axis.

## Decision

Option B for old saves, option B for the name, the namer's own split for the count, and flags for the
lab. In detail:

### 1. The brief

Five flat fields, with the same names on `roll_character` and `CharacterBrief`:

| Field | Server type | In the interview | When not given | Flag |
| --- | --- | --- | --- | --- |
| `tone` | `Tone { Serious, Comic, Heartfelt }` | a short choice, may be inferred | rolled | `--tone` |
| `race` | `string` | free text, the GM's words only | rolled from `Tables.Races` | `--race` |
| `nameLetter` | `NameLetter { A ... Z }` | a long choice: only when the letter is in the quote | rolled from `Tables.NameLetters` | `--letter` |
| `nameSyllables` | `Syllables`, written `"1"`, `"2"`, `"3"` | a short choice, may be inferred ("a short name") | rolled from `Tables.SyllableCounts` | `--syllables` |
| `familyLetter` | `NameLetter { A ... Z }` | a long choice: only when the letter is in the quote | rolled from `Tables.NameLetters` | `--family-letter` |

- Every value is a string or a string enum, so `ListFrom` reads all five with no console change.
- The family name has its own letter, rolled from the same table as the first name's (the owner's call,
  2026-10-25). The saves showed family names repeating as much as first names: Venn, Voss and Vale.
- **The syllable count is written as a number in a string.** Each `Syllables` member carries
  `[JsonStringEnumMemberName("1")]` and so on, so the schema's choices are `"1"`, `"2"`, `"3"` and the
  console reads the count with `int.Parse`. The console keeps no copy of the server's enum. If the MCP
  SDK's schema ignores the attribute, PR 6's schema test fails, and the fallback is a word-to-number map
  in the console, guarded by a test.
- **A long choice is not inferred.** A choice field with more than six values is recorded only when its
  value appears in the quote as a whole word ("her name starts with K"). The rule reads the field's
  choice count from the schema, so no field is named in the console. Tone and syllables stay short
  choices, inferable as ADR-007 allows. This replaces ADR-007's "a choice may be inferred" for long
  choices only.
- **The server refuses an enum value it does not define.** System.Text.Json's string-enum converter
  accepts integers, so `--tone 7` would bind today. `roll_character` checks `Enum.IsDefined` on every
  enum parameter, `difficulty` and `obstacle` included, and refuses with the field's name.
- None of the five has a default. The interview labels an unmentioned one rolled, and the five flags
  have no default in `RunOptions`. `RollArguments()` and `RecordedFlags()` leave out a flag that has
  no value. Today both would throw on one.
- The five flags are answer flags, like `--setting`, so any one of them skips the interview. `--tone
  Comic` alone gives a flag run with the default setting.
- **Every die is cast, every time, in one fixed order.** The order is the seven existing dice, then
  tone, race, letter, syllables and family letter. A given value replaces its die's result and never skips the die. One
  seed then rolls the same race and name dice whatever tone is given. The new dice come after the old
  ones, so a seed's old seven traits stay as they were.
- `Tables` gains `Races`, `NameLetters` and `SyllableCounts` as weighted tables
  (`Weighted<T>(T Value, int Weight)`), with a weighted pick. Both are new code; today's tables are
  plain arrays. Their rows are the owner's (open questions 1 to 3). A new test enforces at least six
  rows in every text table in `Tables.cs`; the fixed enums `tone` and `nameSyllables` are exempt by
  name, as `obstacle` is.

### 2. Old saves

```
SavedCharacter(JsonElement Brief, CharacterName? Name, string Text)    // Name: WhenWritingNull
save_character(CharacterBrief brief, CharacterName name, string text)
```

- The save file keeps each brief as the JSON it was saved as. An old entry is read and written back
  without passing through `CharacterBrief`, so it can never gain `"tone": null` or `"tone": "unknown"`.
- `save_character` still binds its brief through `CharacterBrief`, so the server refuses a new brief
  missing any field. `Name` carries `[JsonIgnore(Condition = WhenWritingNull)]`, because the Web
  defaults write nulls, so an old entry keeps its bytes.
- "Unknown" exists only on the screen. `--load` prints the saved brief line byte for byte, as today,
  then one more line when the save lacks something: `[app] unknown in save 3: tone, race, nameLetter,
  nameSyllables, familyLetter, name`. One pure function, `SavedView.Unknown`, lists the roll schema's fields that the
  brief lacks, then `name` when the entry has none, so a brief field added later shows as unknown with no
  edit. A save with a name prints it: `[app] name Kessa Vale`.

### 3. Tone reaches the writer

- Three files sit beside the skill: `skills/npc-writer/tones/Serious.md`, `Comic.md` and `Heartfelt.md`.
- The writer's system message is `SKILL.md`, a blank line, then the one tone file the brief's `tone`
  names. Code reads `tone` from the brief JSON, which is structured data. The model never sees the
  other two tones, so it cannot blend them.
- The `[skill]` line fingerprints the combined text and names the tone file:
  `[skill] .../npc-writer/SKILL.md + tones/Comic.md (2871 chars, 3fa09c1e)`.
- **Every tone file is checked before anything is paid for.** Straight after connect, before the
  interview and before the lab's confirmation, the console compares the schema's `tone` choices with the
  `tones/` files of every skill folder the run will use: the default one, and each `--lab skill=`
  folder. A missing file stops the run with its path. A test fails when the schema and the default
  folder disagree.
- `SKILL.md` gains one line: the tone section sets how to write, and the brief still sets what is true.
  The interviewer's skill gains a line on reading a tone from a scene. Both are product.
- A lab skill variant is a folder with its own `SKILL.md` and `tones/`, so a skill and its tones are
  compared together.

### 4. The name

- `Namer.RunAsync` runs before the writer, with its own skill (`skills/namer/SKILL.md`) and one console
  tool, `give_name(syllables: string[], family: string)`, for example `["Kes", "sa"], "Vale"`. It has
  the scene interview's shape: `record_answer` is a console tool, and so is this. The writer's tool list
  does not change, because each `AgentLoop` sets its own tools.
- The namer is told the race, the name dice and the scene. Code checks the tool's arguments, each check
  with its own rejection reason (lesson 004):
  - **No empty syllable.** An empty or blank syllable, or no syllables, is refused.
  - **The letter, exactly.** The first letter of the first syllable must be `nameLetter`, ignoring case.
  - **The family letter, exactly.** Every character has a family name, and its first letter must be
    `familyLetter`, ignoring case.
  - **The count, exactly, as the namer split it.** The number of syllables handed back must equal
    `nameSyllables`. Code cannot hear the name, so a dishonest split still passes. That is a smaller
    and plainer gap than guessing the count from spelling.
- A rejected call says why, and the namer tries again. The first accepted name wins, and a later call
  in the same loop is refused. The loop ends on the namer's first turn with no tool call, so a named
  character costs at least two model calls.
- `Namer` runs `AgentLoop` with `maxTurns: 4` and catches `TurnCapReachedException`. With no accepted
  name, the run stops before the writer is paid: `[app] not written: no name followed the dice (K, 2, V)`.
- The writer's first message gains one line, `Their name is Kessa Vale.`, before the brief, and the
  brief stays last (ADR-008). An `[app] name Kessa Vale (K, 2, V)` line shows on stderr.
- The save keeps the name. The Done when check "six different first names, each following its dice"
  then reads only structured data.
- What this cannot prove: that the writer's text uses the name it was told. If it does not, the save
  holds the namer's name beside a text with another one. Only the owner's eyes check the text.

```csharp
public sealed record CharacterName(IReadOnlyList<string> Syllables, string Family);              // server, saved
public sealed record NameDice(char Letter, int Syllables, char FamilyLetter);                     // console, read from the brief JSON
public sealed record NamedCharacter(IReadOnlyList<string> Syllables, string Family)               // console, accepted
{
    public string Given => string.Concat(Syllables);
    public string Full => $"{Given} {Family}";
}
public enum NameRejection { None, NoSyllables, EmptySyllable, WrongLetter, WrongSyllableCount, NoFamily, WrongFamilyLetter, AlreadyNamed }
```

### 5. The lab

```
--lab tone | skill=<dir>,<dir> | temperature=<t>,<t>   --repeat <n>   --mode side|blind   --out <dir>
--score <dir>
```

- **Command.** One axis per lab, so each sheet shows one difference. `tone` with no values takes every
  choice in the schema. `--repeat` defaults to 1, `--mode` to `side`, and `--out` to a new folder in
  temp, whose path is printed. Every flag takes a value, because `RunOptions` reads every flag that way.
  Paths are read as typed, not lower-cased.
- **Refusals,** each before any model client is built, each with its own reason:
  - `--lab` without `--seed`, or without an answer flag. An interview cannot be confirmed before the
    model is called, and cannot be repeated. A lab of a typed scene waits for a later epic.
  - `--lab` with `--load`, `--record` or `--provider replay`.
  - `--repeat`, `--mode` or `--out` without `--lab`. `--repeat` below 1.
  - The same skill folder twice. `--lab tone` with `--tone`.
  - `--lab temperature` with `--provider anthropic`, because claude-opus-5 and claude-sonnet-5 reject a
    temperature with a 400.
  - `--score` with any other flag. `--score` runs before a model client is built and before the server
    is started, so it needs no key.
- **Input.** The run's answer flags and its seed. The lab rolls once. A tone variant's brief comes from
  `roll_character` with every field of the base brief handed back in and the tone replaced, so the
  server stays the only author of briefs and nothing is rolled twice.
- **One name for the whole lab.** The namer runs once, on the base brief, and every variant is written
  with that name. Every sheet then shows one character, in different moods or by different writers.
- **One way to write.** Every variant goes through `ChatAgent.WriteAsync`, the call a normal run makes.
  It takes an optional name, and calls the namer only when it is given none. A variant changes one thing
  only: the skill folder, the brief's tone, or the temperature on a copy of `ChatOptions`. The lab saves
  nothing to `characters.json`. The sheet is the record.
- **It asks first, with an upper bound.** Before any model call, the lab prints how many characters it
  will write and the most tokens they can cost: each planned model call counted at its input (the
  skill, the tone file and the first message) plus `MaxOutputTokens`. Then it waits for yes. Anything
  else, end of input included, stops before any model call. A bound over the session cap is refused
  outright. It shows tokens, not money (open question 7).
- **The cap mid-lab.** If the spending cap stops a call anyway, the lab stops, and the sheet says where:
  `stopped at the spending cap after 4 of 6`. Every later call would be refused before it was sent.
- **Output, side mode.** `sheet.md` shows the brief once, then every character under its label and
  take, with its name and the fingerprint of the skill it was given. A variant that fails, a temperature
  400 say, stays on the sheet with its reason, and the lab goes on.
- **Output, blind mode.** No `sheet.md`. `blind.md` shows the characters as A, B, C and so on, with no
  labels and no fingerprints, and `key.json` maps each letter to its label. Both the order the variants
  run in and the letters are shuffled from `--seed`, so a test can check them. While the variants run,
  `Console.Error` goes to a null writer, so no `[skill]`, `[loop]`, `[app]` or `[server]` line can give
  the key away through a file name, a token count or the order. `--score <dir>` asks for a guess per
  letter, prints `n of m right`, and writes `scored.md`, the labelled sheet.

### Signatures the build follows

```csharp
// Server
public required Tone Tone { get; init; }                     // CharacterBrief, with Race, NameLetter, NameSyllables
public sealed record SavedCharacter(JsonElement Brief, CharacterName? Name, string Text);

// Console
public sealed record WriterInput(string BriefJson, string? Scene, string SkillDir, NamedCharacter? Name = null);
public sealed record Written(NamedCharacter Name, string Text);
ChatAgent.WriteAsync(IChatClient client, ChatOptions options, IToolSource tools, WriterInput input, CancellationToken ct) -> Written
ChatAgent.SystemMessage(string skillDir, string briefJson) -> (string Text, string Trace)
ChatAgent.FirstMessage(string briefJson, string? scene, NamedCharacter? name) -> string
ToneFiles.Missing(IReadOnlyList<RollField> fields, IEnumerable<string> skillDirs) -> IReadOnlyList<string>
NameCheck.Check(NameDice dice, IReadOnlyList<string> syllables, string family) -> (NameRejection, string)
Namer.RunAsync(IChatClient client, ChatOptions options, WriterInput input, CancellationToken ct) -> NamedCharacter
SavedView.Unknown(IReadOnlyList<RollField> fields, JsonElement savedEntry) -> IReadOnlyList<string>
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
  lab's tone sheet would compare three different characters, not one character in three moods. For the
  same reason the lab names its character once.
- **A long choice needs its word in the quote**, because a list the GM cannot judge at a glance is the
  model picking, which ADR-007 closed for free text and now closes for long choices.
- **One tone file** keeps the guidance for how to write in the skill, where ADR-008 put it, and keeps the
  first message to facts. The fingerprint then says which tone a run was told.
- **The namer** is the only option where no state exists in which the model skipped the check: no name,
  no writer. Rolling the family letter too means no part of the name is the model's free pick. The cost is two small calls per character, about 2,000 to 3,000 tokens.
- **The lab as flags on the existing run** gives one way to write a character, used by a run and by
  every variant, so a sheet compares what a real run would produce. A blind lab is silent while it runs,
  because the trace is the easiest place to read the key.

## Consequences

- Easier: a GM can ask for a mood and a people, and the name follows dice the saves can show. A
  creative change to the skill or a tone file is judged on a sheet, blind if wanted, instead of from
  memory. Old saves keep their bytes. A brief field added later shows as unknown with no edit.
- Harder: every character costs two more small model calls. The replay fixture grows from two calls to
  at least four, the namer's first. `ReplayTests` and `ReplaySensitivityTests` then find the writer's
  calls by their system message, not by index (PR 8). `ChatAgent` becomes the one entry point for
  writing, and its name changes from `RunAsync` to `WriteAsync`.
- `save_character` gains a `name` argument, which changes the server's `tools/list`. It stays app-only,
  so the model's tools do not change.
- The interview loses inference on `nameLetter`: a GM who says "a K name" is recorded, and a GM who
  hints at nothing gets a rolled letter.
- The free tests prove the name follows its dice and the tone file is sent. Only paid runs show that a
  tone reads as itself, that a name sounds like its people, and that the text uses the name it was told.
- Revisit if: the lab needs two axes at once, or a typed scene (a plan file then earns its place), a
  model refuses the namer often enough to cost real money, or epic 4 needs to type old briefs.

## Build order

Each PR starts from `master` and passes `dotnet test`. Each new test is shown failing on the code it
guards, then passing. A restructure passes the replay fixture unchanged.

1. **This ADR.**
2. **Restructure: storage keeps the brief as JSON.** `SavedCharacter.Brief` becomes a `JsonElement`, and
   nothing else changes; the name comes in PR 8. *Free test:* a copy of the real `characters.json` goes
   through `Storage.Load` then `Storage.Save` and comes out byte-identical. If it does not, the design
   falls back to option A for old saves and this record is amended. The replay passes unchanged.
3. **Restructure: one way to write.** `ChatAgent.WriteAsync(WriterInput)` with a skill folder replaces
   `RunAsync`. *Free tests:* the replay passes unchanged and the `[skill]` line is byte-identical; a
   `WriterInput` naming a copied skill folder sends that folder's `SKILL.md`.
4. **The lab: skill and temperature axes, both modes, `--score`.** *Free tests:* `Lab.RunAsync` with a
   stand-in model and the real server writes every variant and take to `sheet.md`; in blind mode each
   letter's text in `blind.md` matches its label in `key.json`, the same seed gives the same letters and
   run order, no `sheet.md` is written, and captured stderr holds no variant label, skill path or token
   count; `LabSheet.Score` gives 5 of 6 from piped guesses; the confirmation refuses at end of input and
   over the cap; a failing variant stays on the sheet with its reason; the cap mid-lab stops it and the
   sheet says where; each refusal above gives its own reason; `--score` runs with no key set. The replay
   passes unchanged. *Needs my eyes:* the confirmation and the sheet layout.
5. **Behavior: every die is cast.** *Free test:* through `McpToolSource`, seed 5 with `attitude` handed
   to `roll_character` rolls the same brief as seed 5 without it, except for attitude. It fails on
   today's code. The fixture hands in no trait, so it passes unchanged.
6. **Behavior: the five fields join the brief.** Enums, tables with the owner's rows, flags, the
   `RunOptions` change for flags with no default, the long-choice rule, the enum refusal, and
   `SavedView.Unknown` on `--load`. *Free tests:* a given tone or race is kept and a missing one is
   rolled; every brief carries `nameLetter`, `nameSyllables` and `familyLetter`; under one seed `--tone Comic` and
   `--tone Serious` roll the same brief except for tone; a flag with no default and no value stays out of
   the roll arguments and the recorded flags; the schema's syllable choices are `"1"`, `"2"`, `"3"`; a
   stand-in interviewer recording `tone` gives `tone: Comic [inferred]`; one recording `nameLetter` K on
   a quote with no K is rejected, and on "starts with K" is kept; `--tone 7` is refused by name; an old
   save loads with the unknown line and the next save leaves its JSON byte-identical, with no "unknown"
   in the file; every text table has at least six rows; the brief's properties are the same set as
   `roll_character`'s parameters. *Paid:* re-record the fixture. *Needs my eyes:* the new `--load` line,
   the interview's confirmation, and the interviewer skill's tone line.
7. **Behavior: one tone reaches the writer.** The tone files, `ChatAgent.SystemMessage`, `ToneFiles`, the
   `SKILL.md` line, and the lab's `tone` axis with its `--tone` refusal. *Free tests:* the system message
   for a Comic brief holds `Comic.md` and neither other file; every tone in the schema has a file, and
   the test fails when one is deleted; a missing tone file stops a run before the interview and a lab
   before its confirmation; the lab's tone axis gives briefs that differ only in tone. *Paid:*
   re-record. *Needs my eyes:* the three tone files, which are product.
8. **Behavior: the namer and the name in the save.** *Free tests:* each rejection has its own reason; a
   stand-in namer that gives "Tobin" for K, then "Kessa", is rejected once and then accepted; a family
   name with the wrong letter, or none, is rejected; a second
   accepted call is refused; a namer that never follows the dice stops the run at four turns and the
   writer is never called; the name reaches the writer's first message before the brief; a lab names
   once; `save_character` refuses a brief without `nameLetter`; `--load` prints a saved name.
   `ReplayTests` and `ReplaySensitivityTests` find the writer's calls by their system message and still
   fail on a dropped writer turn, shown by removing it. *Paid:* re-record, then the epic's paid checks,
   each asked for first: the blind tone check (`--lab tone --repeat 2 --mode blind`), six runs for
   names, and an elf and a dwarf under one seed. *Needs my eyes:* the namer's skill. This PR also closes
   the epic in the docs.

## Settled by the owner (2026-10-25)

1. **The race table.** Human 7, dwarf 3, elf 3, halfling 3, gnome 2, half-orc 2, orc 2, goblin 2,
   dragonborn 1, goliath 1, tiefling 1. Eleven rows, a total weight of 27, so about one character in
   four is human.
2. **The family name** has a rolled letter, `familyLetter`, checked like the first name's.
3. **Three re-records**, one in each of PRs 6, 7 and 8, so each behavior is proved on its own.
4. **The namer's extra calls** are accepted: about 2,000 to 3,000 tokens per character.
5. **Paid runs use OpenAI's `gpt-6-luna`,** the default model, which the replay fixture already uses:
   the three re-records, the lab runs and the epic's paid checks.

## Open questions for the owner

1. **The letter table,** used for both letters. Proposed: every letter but Q, U, X, Y and Z, at equal
   weight, so 21 rows. A given `--letter Q` is still allowed.
2. **The syllable weights.** Proposed: one 1, two 2, three 1. Is four wanted, for elves?
3. **Tokens, not money, at the lab's confirmation.** The epic says the lab asks "with its cost".
   Proposed: tokens only, because a price table needs hand upkeep and goes stale.
4. **A name the GM gives,** in a scene ("an innkeeper called Bram") or by a flag. Proposed: not in this
   epic. The dice apply and the namer's name wins, as the brief wins. Epic 4 can give a kept character a
   fixed name.
