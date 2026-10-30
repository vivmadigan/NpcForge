# ADR-010: A scene's gaps are asked for, marked not given, or rolled, as `roll_character` says

- **Status:** accepted
- **Date:** 2026-10-30
- **Supersedes:** two lines of earlier records, each named where it changes. ADR-007's rule that a
  required field the scene never mentions takes its flag's default ends for interviews; flag runs
  keep their defaults (Decision 1). ADR-009 decision 1's list of dice gains a thirteenth, cast last,
  and the wants and obstacle dice are read after it (Decision 3). The rest of each stands.

## Context

Epic 8, "Fill the gaps in a scene" in ROADMAP.md. On 2026-10-27 the owner typed this scene:

> The party bursts into a tiny bakery where a goblin baker is furious that someone keeps stealing his croissants. It’s a silly session, play it for laughs.

The interview gave every required field the scene left out its flag's default (ADR-007), so save 8
got "the name of a fence" at Wall, the replay fixture's test values. The owner settled these before
the design, and this record does not reopen them:
- In an interview, a missing place or role is asked for, one short question each, and the answer is
  labelled `[my words]`. It is never defaulted.
- A missing want is never asked for and never invented. The brief marks it as not given, and the
  confirmation shows `[not given]`. Code tells the writer which case it is in, from the brief.
- With no want given, the levers turn on winning the character's goodwill, difficulty is how hard the
  character is to win over, and a hook comes from the scene. There is no table of hooks.
- A missing difficulty is rolled, from a table the owner approves. The interview can still infer one.
- Flag runs keep their four defaults, so the replay fixture and `--setting` alone keep working.

Six things in the code make the shape less obvious than it looks:
- The console knows one fact per field from `roll_character`'s schema, required or not
  (`RollField.cs:13`), and names no field (`RollField.cs:5-8`, ADR-007). It now needs three kinds.
- `CharacterBrief` is all-required, and `save_character` re-binds every new brief through it. "Not
  given" has to be a legal value. `SavedView.Unknown` already calls a missing key "unknown", for old
  saves (`SavedView.cs:13-18`).
- The wants table is filtered by difficulty and Easy drops `Wont` (`CharacterTools.cs:71`, `75`), and
  those two dice are cast first (`80-81`). ADR-009 casts every die in one fixed order with new dice
  last, and the replay and `A_seeds_old_seven_traits_are_what_they_were_before_the_new_dice` hold it.
- Every edit to the writer's `SKILL.md` changes the system message of every run, the fixture's
  included, so it forces a paid re-record (lesson 011).
- `SceneDraft.Record` accepts a value only with a quote from the scene (`SceneDraft.cs:62-129`). A
  typed answer has none.
- C# puts optional parameters after required ones (CS1737), and the schema takes its order from the
  parameters.

## Options considered

Three design runners (Opus, Sonnet, Haiku) each sketched the whole epic without seeing the others. A
cross-judge (Sonnet) scored them, and the Sonnet sketch became the base, with grafts from the other
two. All three reached the same core shape on their own. The real choices were these.

**How the console tells the three kinds apart.**
- **A** From the schema the server already sends. Required and not nullable is asked, required and
  nullable is not given, optional is rolled.
- **B** An app-only `brief_fields` tool that reads a `[WhenMissing]` attribute on each parameter. A
  second channel that says what the signature already says.
- **C** A table of field names in the console. It breaks ADR-007's rule that a new trait reaches the
  interview with no console change.

**How "not given" is written.**
- **A** A JSON null, always written.
- **B** A missing key. That is what an old save looks like.
- **C** A string such as "none". A GM could type it.

**How the writer is told.**
- **A** A product file, added by code when the brief holds a null, beside the tone file. `SKILL.md`
  is not edited.
- **B** `SKILL.md` split into want and no-want branches. A cleaner prompt, but every run's system
  message changes, so a paid re-record.
- **C** A line in the first message as well as the file. The same fact in two places.

**Where the difficulty die goes.** The owner's choice.
- **A** Cast last, thirteenth. The wants and obstacle dice keep their places in the order and are
  read after the difficulty is settled.
- **B** Its own seeded stream, cast first.
- **C** Cast first on the main stream. Every seed's brief changes, so a paid re-record.
- **D** Cast first only when no difficulty is given. A given value would then move other dice, the
  failure ADR-009 fixed.

## Decision

Option A in each. In detail:

### 1. Three kinds, read from `roll_character`'s signature

| Field | C# parameter | Schema | Kind | When the scene leaves it out |
| --- | --- | --- | --- | --- |
| `setting` | `string setting` | required, `"string"` | Ask | one question, answer `[my words]` |
| `playersWant` | `string? playersWant`, no default | required, `["string","null"]` | NotGiven | never asked, `[not given]`, null in the roll |
| `occupation` | `string occupation` | required, `"string"` | Ask | one question, answer `[my words]` |
| `difficulty` | `Difficulty? difficulty = null` | optional | Roll | `[rolled]`, left out of the roll |
| the other twelve | as today | optional | Roll | as today |

- `RollField`'s `bool Required` becomes `WhenMissing { Ask, NotGiven, Roll }`. A field not in the
  schema's `required` list is Roll; a required field whose type admits null is NotGiven; any other
  required field is Ask. No console line names a field.
- `playersWant` stays in `required` on purpose. The caller must send null to say "no want", so a
  forgotten argument is refused, never read as "not given". To the GM it is not required: a scene
  with no want rolls. A why-comment on the parameter says so. This is what the epic's "stops being
  required" means.
- The Sonnet runner measured the three schema shapes on Microsoft.Extensions.AI.Abstractions 10.8.3,
  the version the server resolves through ModelContextProtocol 2.2.0. It also measured that an
  omitted `playersWant` is refused and a null binds. Unit 3 proves all of it again through the real
  server. If a later SDK stops writing the null, that test fails, and the fallback is a server-owned
  `[McpMeta]` list of the not-given fields.
- CS1737 moves `difficulty` after `occupation` among the parameters, so the schema, the interviewer's
  field list and the confirmation show the role before the difficulty. `CharacterBrief` keeps its
  order, so the brief JSON, and the fixture, do not change.
- `SceneDraftTests.Fields` is a hand copy of the server's field list. A new test holds it equal to the
  real schema on names, choices and kinds.
- A blank `playersWant` is refused by name. "Not given" has one spelling, null.

### 2. "Not given" is a JSON null that is always written

- `CharacterBrief.PlayersWant` is `required string?` with `[JsonIgnore(Condition =
  JsonIgnoreCondition.Never)]`. Measured: without the attribute, the SDK's options drop the null and
  the key vanishes.
- A missing key is an old save, shown as unknown (`SavedView.Unknown` tests for the key,
  `SavedView.cs:16`). A null is not given. A string is given. Every save made before this record
  holds a string.
- `save_character` binds a null and refuses a brief without the key. The save file holds
  `"playersWant": null`. Storage keeps each brief as the JSON it was saved as (ADR-009 decision 2), so
  no old entry changes.
- `--load` prints the brief line as today, then, for a null, one more line from `SavedView.NotGiven`,
  read from the schema as `Unknown` is: `[app] not given in save 9: playersWant`.

### 3. The difficulty die is cast last, and the wants and obstacle dice are read after it

- A missing difficulty is rolled from `Tables.Difficulties`, a weighted table with the owner's rows:
  Easy 1, SomeWork 2, Wall 1. It is exempt from the six-row rule by name, as `SyllableCounts` is.
- The cast order is ADR-009's twelve dice, then difficulty. The wants and obstacle dice still take the
  first and second samples, as `NextDouble` values, and are read only once the difficulty is settled
  (`difficulty ?? rolled`). The filter and the Easy rule read the settled value.
- This rests on seeded `Random` computing `Next(n)` as `(int)(NextDouble() * n)`. Both are
  `(int)(Sample() * n)` in .NET's seeded implementation. The Sonnet runner measured it on 12,000
  cases, and a free test holds it.
- So a seed rolls today's brief for any given difficulty, the old-seven-traits test and the fixture
  pass unchanged, and a rolled difficulty and the same difficulty handed in roll the same brief.

### 4. Code asks for a missing place or role

- After the interviewer's loop and before the confirmation, `SceneInterview` asks one question for
  each Ask field with no answer, in schema order. The question is built from the field's description,
  so no field is named. An empty line asks again. End of input stops, and nothing is rolled.
- The answer enters the draft by its own door, `SceneDraft.RecordAsked(field, typed)`, which accepts
  only an Ask field and a value that is not blank. An answer carries its evidence: `Quoted(quote,
  sentence)` from the model's door, `Asked` from the GM's. The confirmation shows the value,
  `[my words]`, and a line saying it was typed when asked.
- `Label.Default`, `DefaultFor` and every `defaults` parameter go. `SceneDraft.Lines` throws while an
  Ask field has no answer, so a confirmation with a hole, or a roll from one, cannot be built.
  `Program.cs` stops passing `run.RollArguments()` into the interview.
- The answer reaches the writer through the brief only. `Told.Scene` stays the line the GM typed, and
  ADR-008 stands.
- The interviewer's skill gains two lines, which are product. A field it leaves out may be asked of
  the GM, left not given, or rolled. What the players want is what they want from this character, not
  the character's own trouble, so the croissant thief does not become `playersWant`.
- The greeting still invites a want ("what they want from that person"). Its wording and both
  questions are for the owner's eyes in unit 6.

### 5. The writer is told by a file chosen from the brief's nulls

- Each field that is null in the brief adds `skills/npc-writer/not-given/<field>.md` to the writer's
  system message, after `SKILL.md` and before the tone file, so the tone file still governs it. Today
  that is one file, `not-given/playersWant.md`. The `[skill]` line names it.
- A brief with a want adds nothing, so its system message is today's to the byte (`ChatAgent.cs:50`).
  `SKILL.md` and the first message are not edited.
- The file is product and follows lesson 010: it says what changes in each of the six output
  sections. Levers win the character's goodwill (a better deal, the job, a tip). Anti-levers lose it
  (full price, turned away, an enemy). Difficulty is how hard they are to win over. The obstacle still
  applies, read as goodwill: `Wont`, slow to warm to the players; `Cant`, they can offer only something
  smaller; `ForAPrice`, they warm up once paid in goodwill or coin. A hook comes from the scene: when
  the scene shows something the character wants done, they offer it to the players, and what the scene
  leaves open stays a gap (`SKILL.md:21-24`). `characterWants` is also true, and is the twist. It never
  replaces the scene's hook.
- The not-given files are checked before anything is paid for, on the interview path, for the default
  skill folder. A lab is always a flag run and never has a null, so the lab's check of its skill
  folders (`Lab.cs:179`) does not change.

```csharp
// Server
string? playersWant                                         // RollCharacter, required, no default
Difficulty? difficulty = null                               // RollCharacter, after occupation
[JsonIgnore(Condition = JsonIgnoreCondition.Never)] public required string? PlayersWant { get; init; }
public static readonly Weighted<Difficulty>[] Difficulties; // Tables: Easy 1, SomeWork 2, Wall 1

// Console
public enum WhenMissing { Ask, NotGiven, Roll }
public sealed record RollField(string Name, string Description, IReadOnlyList<string>? Choices, WhenMissing WhenMissing);
public enum Label { MyWords, Inferred, Rolled, NotGiven }
public abstract record Evidence;
public sealed record Quoted(string Quote, string Sentence) : Evidence;
public sealed record Asked : Evidence;
public sealed record Answer(string Value, Label Label, Evidence Evidence);
SceneDraft.RecordAsked(string field, string typed) -> RecordResult
SceneDraft.StillToAsk -> IReadOnlyList<RollField>
SceneDraft.Lines() -> IReadOnlyList<ConfirmationLine>       // no defaults
SceneDraft.RollArguments() -> Dictionary<string, object?>   // no defaults; a NotGiven field is sent as null
SceneInterview.RunAsync(client, options, fields, gm, screen, ct) -> Told?   // no defaults
SavedView.NotGiven(IReadOnlyList<RollField> fields, JsonElement savedEntry) -> IReadOnlyList<string>
```

## Why

- **Kinds from the signature.** The server already owns the fields and their types, and the schema
  already carries required and nullable. Reading the kind from there keeps one source of truth, and a
  field added later reaches the interview with no console change, as ADR-007 wants.
- **A null that is always written.** The save must tell an old entry from a character with no want.
  Those become two different JSON states, and the SDK's binder enforces "you must say".
- **Cast last, read after.** It is the only option that rolls a difficulty, filters by it, keeps
  ADR-009's one order with new dice last, and keeps every seed's brief. No re-record.
- **Code asks.** A question from the model would be prose, which code may not read. A typed answer
  needs no quote, because the GM typed it for that field.
- **A file chosen from the brief.** ADR-009's tone mechanism again. The fact is the brief's null, the
  guidance is a file, and runs with a want are untouched, so the epic costs no paid re-record.
- **Answers through the brief only.** A typed answer is exactly a brief field, and the scene stays the
  GM's own words, where ADR-008 drew the line.

## Consequences

- Easier: a scene with no want gets a character written for the situation, with a hook when the scene
  suggests one, and no test values. A scene with no place or role gets a question, not "an inn in a
  major city" or "innkeeper". A rolled difficulty spreads characters across the Easy and SomeWork rows
  of the wants table, not only the five Wall rows.
- No unit of the epic needs a paid re-record. The paid runs are the epic's own checks: the bakery
  scene at `--seed 1` to `3`, and epic 7's control scene at one seed.
- Harder: `playersWant` is required and nullable, an idiom that looks like a mistake, so a why-comment
  says why. The schema's order and the brief's order now differ. `not-given/playersWant.md` overrides
  lines of `SKILL.md` that assume a want; the next `SKILL.md` edit that pays for a re-record should
  fold it in.
- A GM who cannot say where or who cannot finish an interview. There are no dice for them (ADR-002).
- No flag makes a no-want run, so a no-want character cannot be recorded, replayed or compared in the
  lab. Tuning `not-given/playersWant.md` needs paid interview runs.
- Revisit if: the SDK stops writing nullable types in the schema (unit 3's test fails, and the
  `[McpMeta]` fallback applies), the lab needs no-want runs, or hooks read the same across runs (a
  table of complications, outside this epic).

## Build order

Each unit is one PR from `master` that passes `dotnet test`. Each new test is shown failing on the
code it guards, then passing. A restructure passes the replay unchanged. No unit needs a paid
re-record.

1. **This ADR.**
2. **Restructure, server: the wants and obstacle dice are cast early and read late.** *Free tests:*
   the late read equals `Next(n)` over a grid of seeds and table sizes, shown failing with the read
   off by one; a golden of seeds 1 to 12 at all three difficulties, captured on today's code in the
   PR's first commit, shown failing when two casts swap; the replay and the old-seven-traits test pass
   unchanged.
3. **Behaviour, server: a missing difficulty is rolled, and a missing want is null.**
   `Tables.Difficulties`, the thirteenth die, `string? playersWant`, the `Never` attribute, the blank
   refusal and the new parameter order. *Free tests:* the schema's `required` list is `setting`,
   `playersWant`, `occupation`, and `playersWant` admits null; a roll with no difficulty gives one
   from the table, and across seeds all three come up; with a difficulty handed in, every die after the
   wants and obstacle matches the rolled case; a rolled Easy never rolls `Wont`; a null want comes back
   as `"playersWant":null`, shown failing with the attribute removed; `save_character` keeps the null
   and refuses the same brief without the key; an omitted want is refused while a null one rolls; a
   blank want is refused by name; `SceneDraftTests.Fields` equals the real schema. The interview's
   difficulty shows `[rolled]` with no console change. *Needs my eyes:* the table.
4. **Restructure, console: `RollField`'s kind replaces `Required`, and an answer carries its
   evidence.** `Lines` still gives Ask and NotGiven fields their flag defaults, so every screen is
   unchanged. *Free tests:* the real schema gives Ask, NotGiven and Roll as Decision 1's table says,
   shown failing with `playersWant` made `string`; pure schema tests for the three shapes.
5. **Behaviour, writer: `not-given/playersWant.md` and the code that adds it.** *Free tests:* a brief
   with a null want gets the skill, the not-given file and the tone file, and the `[skill]` line names
   it, shown failing with the branch removed; a brief with a want gets today's bytes; a missing key
   adds nothing; a missing not-given file stops an interview before the greeting, and a lab is not
   stopped. *Needs my eyes:* the file.
6. **Behaviour, interview: questions for a missing place or role, and `[not given]`.** The question
   step, `RecordAsked`, `SavedView.NotGiven` and its `--load` line, and the interviewer's two lines.
   `Label.Default`, `DefaultFor` and the `defaults` parameters go. *Free tests:* no place and no role
   gives two questions in schema order, both answers show `[my words]`, and no field shows `[default]`,
   shown failing on unit 5's code; a scene that names both gets no question, with and without a want;
   an empty answer asks again; end of input at either question rolls nothing; a no-want scene shows
   `playersWant: [not given]` and sends null; the model's door still refuses an unquoted answer while
   the GM's door accepts one; an unanswered Ask field cannot be confirmed; `--load` prints the unknown
   line for an old save and the not-given line for a no-want save, never both. *Paid,* each asked for
   first: the bakery scene at `--seed 1`, `2` and `3`, and the control scene at one seed. If the bakery
   runs miss "furious" again, look first at the interviewer skill's "Leaving a field out is never a
   mistake" (`skills/scene-interviewer/SKILL.md:26`). This PR also closes the epic in the docs. *Needs
   my eyes:* the questions, the greeting, the `[not given]` and `--load` lines, the interviewer's
   lines, and the paid runs.

## Settled by the owner (2026-10-30)

1. **The difficulty die** is cast last, and the wants and obstacle dice are read after it.
2. **The obstacle** still applies when no want is given, read as goodwill.
3. **The GM's answers** reach the writer through the brief only.
4. **The difficulty table:** Easy 1, SomeWork 2, Wall 1.
