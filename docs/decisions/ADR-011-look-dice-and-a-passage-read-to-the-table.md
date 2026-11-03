# ADR-011: Look dice join the brief, and the writer opens with a passage read to the table

- **Status:** accepted
- **Date:** 2026-11-02
- **Supersedes:** two lines of earlier records, each named where it changes. ADR-009 decision 1's
  dice gain seven casts after ADR-010's thirteenth die, so the thirteenth is no longer cast last
  (Decision 1). ADR-009's namer is told the gender as well as the race and the name dice
  (Decision 3). The rest of each stands.

## Context

Epic 10, "Read it to the table" in ROADMAP.md. The owner wants every character to open with
something to read straight to the players, like the boxed text in an adventure module: where the
character is in the room, what they look like and what they are doing as the party walks in,
ending with the first thing they say. The owner settled these on 2026-11-02, before the design, and
this record does not reopen them:
- The tables, as ROADMAP.md listed them at `dd843fc` and `Tables.cs` now holds them, row for row:
  age and gender (the owner's d8, cast as two dice), a unique
  look (a d20 whose 20 gives two more looks that never repeat, never come up 20 and never clash),
  and two d8 colour tables for an unusual eye or hair colour. An old character never gets snow white
  or silver hair.
- A look the scene states wins, and then nothing else is rolled for the look.
- An NPC is a cardboard cut-out until the players dig in. Two looks and no more: no build,
  clothing or memorable-trait tables. The brief already rolls a mannerism.
- The dice settle some of it, and the model writes the passage from everything rolled, put
  together.
- The room comes from the scene. Where the scene says nothing about the room, it stays short and
  plain, or becomes a gap in square brackets.
- What the character is doing is written from their job, attitude and mannerism. A scene that says
  what they are doing wins.
- The opening line is written from their attitude, the tone and what they want. The character gives
  their name only if they would.
- The sheet still starts with the name, for the GM, and the passage comes straight after it.

Things in the code that shape the design:
- The console builds the interview, the confirmation, the roll and `--load`'s "unknown" from
  `roll_character`'s schema (`RollField.cs:5-11`, `SavedView.cs:12-18`). A trait added on the
  server reaches all four with no console change. Each parameter's `[Description]` is all the
  interviewer is told about its field (`CharacterTools.cs:23-25`).
- `RollField.ListFrom` takes only text, a string or an enum, and throws on anything else
  (`RollField.cs:38-40`). A list of looks would stop every interview. An enum without
  `JsonStringEnumConverter` is written as a number, which the same check refuses.
- Every die is cast, every time, in one order, and a handed-in value replaces its die's result
  (`CharacterTools.cs:80-84`, ADR-009). `x ?? Pick(...)` skips the die. A table that depends on a
  settled value is cast early and read late with `ReadCast` (ADR-010).
- `DiceOrderTests` holds a golden of seeds 1 to 12, captured before ADR-010. Its first test
  compares each field the golden holds, so new fields leave it passing. Its second compares whole
  briefs (`DiceOrderTests.cs:60`), so three new keys fail it for every seed.
- `BriefFieldsTests` finds every public static array on `Tables` by reflection and holds each to
  six rows or more, with two fixed tables exempt by name (`BriefFieldsTests.cs:11-26`).
- The interview may infer a choice of at most six values from any sentence the GM typed. Free text
  must be the GM's own words, quoted (`SceneDraft.cs:70`, `124-127`, ADR-007). The interviewer
  skill's free-text example is `old blacksmith`, an age folded into a role, and it tells the model
  to split a people from a role but nothing else.
- The namer is told the race and the name dice and nothing else (`Namer.cs:67`), and its skill says
  to fit the name to the race and the scene. Once the gender is rolled, a name chosen without it can
  belong to the wrong person, and the writer may not change the name.
- The writer's skill is followed by a not-given file when the want is null and always by a tone
  file. Both walk the output part by part, and `not-given/playersWant.md` numbers "the six parts".
  Lesson 010: a tone file that does not say what it changes in a part leaves that part grave.
- An edit to the writer's skill, the namer's skill or its first message changes every run, so
  `ReplayTests` and `OfflineTests` fail until the replay fixture is re-recorded, paid (ADR-005).
  Lesson 011: the PR is not ready until the re-record is in it.

## Options considered

**How the look is held in the brief.**
- **A** Three fields: `gender` and `age` as enums, and `look` as text holding the whole unique look,
  with a 20's two looks and any colour written into it.
- **B** `look` as a list. `RollField` refuses a list, and the interview records text.
- **C** A field per die: a look, two extra looks, an eye colour and a hair colour. A GM says a look
  in one phrase, and "bald and scarred" would have to be split across fields. Most of them hold
  nothing most of the time.
- **D** One `ageAndGender` field with the owner's eight rows. A scene that states one ("the old
  baker") would have to drop it or invent the other.

**How a 20 and a clash are rolled.**
- **A** Five casts every time: the d20, two extra looks, an eye colour and a hair colour. Each extra
  look is read against the rows left once 20, the looks already chosen and their clashes are taken
  out. Each colour is read against its table, the hair table without snow white and silver for an
  old character. A uniform cast read against the rows left gives each of them the chance a reroll
  would.
- **B** Reroll until the result is allowed. The same odds, but the number of casts depends on the
  seed, so any die added after the look would move.
- **C** Cast the extra and colour dice only when they are used. The same problem as B.

**A row that names a kind of look, or two looks joined by "or".** Nine of the nineteen rows name a
kind, such as "unique armour" or "paralysed part", and six join two with "or", such as "scar or
tattoo".
- **A** The writer makes the row concrete, fitted to the character's job and the scene, as it
  writes a lever's line from a want. The dice settle what kind of thing stands out. This is the
  owner's "the model writes the passage from everything rolled".
- **B** One more always-cast die per look to settle each "or". Up to three more casts, rows split in
  two, and the kind rows ("unique jewellery") would still be the writer's to make concrete.

**Where the passage goes.**
- **A** Straight after the name. "Who they are" is split: the name stays first, and its line of
  history follows the passage.
- **B** After "Who they are", so the line of history sits between the name and the passage. Not
  what the epic says.

**Flags such as `--gender`, `--age` and `--look`, as ADR-009's dice have.**
- **A** None. The interview and `roll_character` take all three, and the epic needs nothing else.
- **B** Flags now. Nothing in the epic runs one; they can come with the first run that does.

**How the writer is told.**
- **A** A new output part in `SKILL.md`, and a line for it in each file that walks the output: the
  three tone files and `not-given/playersWant.md`. No new file.
- **B** A product file added by code, like the tone files. There is nothing to switch on: every
  brief has a look.

**Whether the namer is told the gender.**
- **A** One line in its first message, after the race, and the namer's skill told to fit the name
  to it.
- **B** No change. The namer would keep choosing names blind to a gender the passage then states.

## Decision

Option A in each. In detail:

### 1. Three brief fields, and seven casts after the thirteenth die

| Field | `CharacterBrief` | `roll_character` parameter | Interview |
| --- | --- | --- | --- |
| `gender` | `Gender { Male, Female }` | `Gender? gender = null` | a choice of two, may be inferred |
| `age` | `Age { Teenager, Adult, MiddleAged, Old }` | `Age? age = null` | a choice of four, may be inferred |
| `look` | `string` | `string? look = null` | free text, the GM's words |

- Both enums carry `[JsonConverter(typeof(JsonStringEnumConverter))]`, as every enum on the brief
  does.
- The three go after `familyLetter` in the brief and among the parameters, so the brief JSON adds
  three keys at its end and no key moves.
- The parameters' descriptions, which the interviewer reads:
  - `gender`: "Whether the character is male or female: Male or Female"
  - `age`: "How old the character is: Teenager, Adult, MiddleAged or Old"
  - `look`: "What stands out about how the character looks, such as bald or a scar across one
    cheek. Not their people or their age, which are fields of their own"
- The casts after the difficulty die, in this order: gender, age, the look's d20, its two extra
  looks, eye colour, hair colour. All seven are cast in `RollCharacter`, beside the others, into
  variables, before any handed-in value is read. A value handed in replaces a result; it never
  skips a cast.
- Gender and age are plain picks of equal weight. Two dice of two and four give each of the owner's
  eight rows one chance in eight, as the d8 does.
- The look is read from its five casts and the settled age, the age handed in or rolled, by a pure
  function in its own file, `NpcForge.Server/Looks.cs`. It takes the casts as numbers and casts
  nothing. The rows stay data on `Tables`:

  ```csharp
  // Tables.cs: the owner's rows, 2026-11-02, as data.
  public enum LookColour { None, Eye, Hair }
  public sealed record LookRow(string Text, LookColour Colour = LookColour.None);
  public sealed record HairColour(string Text, bool UsualWhenOld = false);

  // All twenty rows of the d20. Row 20 is "two more looks", so the die reads twenty rows, as the
  // owner's d20 does, and the brief never holds row 20 itself.
  public static readonly LookRow[] Looks = [ /* bad posture ... paralysed part, two more looks */ ];
  public static readonly string[] EyeColours = [ /* violet ... red */ ];
  public static readonly HairColour[] HairColours = [ /* copper red ... silver */ ];

  // Looks.cs
  public sealed record LookCasts(double Look, double Second, double Third, double Eye, double Hair);
  public sealed record LookPart(LookRow Row, string? Colour);

  public static IReadOnlyList<LookPart> Read(LookCasts casts, Age age);  // one part, or two on a 20
  public static string Text(IReadOnlyList<LookPart> parts);              // what `look` holds
  ```

- No row is found by its text. A colour row says which colour table it reads. A hair colour says
  whether it is usual on an old character. The clashes name rows by reference, are held off
  `Tables`' public arrays (the six-row test would count them), and are checked in both directions.
- What `look` holds: each part's words, a colour part with its colour after a colon, as "unusual
  eye colour: violet". A 20's two parts are joined with "; ", which no row contains, as "bald;
  rare item or weapon".
- `gender` and `age` are refused by name when undefined, as the other enums are. A blank `look` is
  refused by name, as a blank `playersWant` is: it would reach the writer as a look of nothing.

### 2. Saves

`save_character` binds through `CharacterBrief`, so every new save holds all three. A save from
before this record has none of the keys and is never re-typed (ADR-009 decision 2). `--load` shows
the three as unknown through `SavedView.Unknown`, with no edit there.

### 3. The namer is told the gender

The namer's first message gains one line after the race, `Gender: female` or `Gender: male`, read
from the brief strictly, as the race is. Only a fresh roll reaches the namer: `--lab` cannot go with
`--load` (`RunOptions.cs:220`). The namer's skill says it is told the gender, and that the first
name fits the gender as well as the race and the scene. Nothing else of the brief joins the namer's
message.

### 4. The writer opens with a passage read to the table

`SKILL.md`'s output becomes: "Their name", then "Read it to the table", then "Who they are" (the
line of history, which no longer carries the name), then the rest as before. The passage's rules,
which the paid runs may reword but not drop:
- It is read aloud to the players as they walk in: second person, at most five sentences, a
  Markdown block quote.
- **The place** is the scene's, in the scene's terms. Where the scene says nothing about the room,
  one plain sentence that names the place from the setting, with no furniture, layout or other
  people. The setting always names a place, so the passage needs no gap for the room.
- **Where they are in it** may come from their job, such as behind the counter.
- **How they look** comes from their age, gender, race and look, described plainly, the age in
  plain words. The look is what stands out: one look, or a 20's two separated by `;`, each shown.
  A colour look is already settled. A look that names a kind, such as unique armour, is made
  concrete by the writer, fitted to their job and the scene; a look with "or" is one of the two,
  and "multiple" stays more than one. Their job may dress them, such as a baker's apron, and
  nothing else striking is added.
- **What they are doing** is their work, done the way their attitude and mannerism would, unless
  the scene says.
- **Their first words** end it, in quotes, as their attitude and the tone would say them. They may
  lean towards what they want and never say it. They give their name only if they would.
- **Only what the players can see and hear.** The passage holds what anyone walking in would see
  and hear in the first moments. Nothing of what is underneath, what they really want, their
  position, their weakness, what they will not discuss or what they are wrong about is said or
  hinted, and the first words do not presume what the players came for. The passage holds no gap
  in square brackets; what the scene leaves open stays a gap in the parts below.

Each tone file gains a line for the passage, as concrete as its others: the place stays plain, and
the tone shows in how they do their work and in their first words, with nothing from the parts
below. Comic adds that the look is never the joke. Each tone file's bar, "someone reading only your
text should know it is comic", now reads the passage instead of "the first two lines", whose first
is the name and second the plain place. `not-given/playersWant.md` names the parts instead of
numbering them, and says what a no-want brief changes in the passage: their first words greet,
warn off or size up strangers, and the hook and the twist stay for the parts below.

### 5. The interviewer

The scene interviewer's skill (`skills/scene-interviewer/SKILL.md`) says an age, a gender and a
look named with the role are fields of their own and stay out of the occupation, as a people does.
A look is their body, face, hair or gear, never a mood or a manner. An age is said only by a word
about age; man and woman say none. A gender is said by a word that is itself male or female, used
for this character, and is quoted with the words that show whom it means; a role such as baker says
no gender. Its free-text example loses "old". Gender and age are inferred under ADR-007's rule for a
choice: the quote must be the GM's words, and the confirmation shows what was recorded. The paid
checks include a scene with no word for the gender, to catch an interviewer that guesses one.

The writer still plays a brief entry over the scene in a clash (ADR-008). The interview, not the
writer, keeps a scene's look, gender or age, so a miss shows as `[rolled]` on the confirmation the
GM confirms.

### 6. Free tests

- **The brief carries the three**, from the real server, as strings.
- **A handed-in value moves no die.** On one server, a roll with `look` handed in followed by a roll
  with nothing handed in gives the second roll a fresh server gives after a plain first roll. The
  same for `gender` and `age`. This catches a skipped cast, which no field of the first roll shows.
- **Handing in the rolled value gives the same brief**, and handing in another changes only that
  field, and for `age` the hair colour the look reads.
- **The older traits stay.** `rolls-seeds-1-to-12.json` stays byte for byte as it is, and the first
  `DiceOrderTests` test passes against it unchanged. A second golden,
  `looks-seeds-1-to-12.json`, captured on the new code, pins `gender`, `age` and `look` for the same
  keys. The first test reads both, and the second test's whole-brief check compares against the two
  together.
- **The rows are the owner's**, in order, for the three new tables, as the race and difficulty
  tables are held.
- **The look reads as the owner's dice do**, in process on `Looks.Read` with no server: row 20 comes
  up about one time in twenty; a 20's two parts never repeat, are never row 20 and never clash, in
  either order; an old character never gets snow white or silver hair, and the same casts give them
  to an adult, so the test can fail.
- **The interview** records a look the scene states, quoted, and a gender or age from a quote.
  `SceneDraftTests.Fields` gains the three, and its test that holds it to the real schema keeps it
  honest.
- **An old save** loads and shows `gender`, `age` and `look` as unknown.
- **The namer's first message** carries the gender, and every hand-made brief in the tests gains
  one.

Existing tests that only gain the three fields: `RollFieldTests`, `SavedViewTests` (its lists, and a
save with every field shows no unknown), `StorageTests`, `NamerTests`, `WriterMessageTests`,
`McpToolSourceTests`, `SceneDraftTests`. None is weakened.

### 7. The replay fixture and the PR

The brief, the namer and the writer all change, so `ReplayTests` and `OfflineTests` fail from the
first commit of the new dice until the fixture is re-recorded. It is re-recorded once, paid, after
the skills' wording has settled, as the PR's last commit. The PR is opened only once nothing more
will be pushed to it (lesson 011).

## Why

- **Three fields.** A GM states a look in one phrase, the interview records one phrase, and
  ADR-009's brief is flat. Gender and age are two fields so a scene that states one keeps the other
  rolled.
- **Seven casts every time, in `RollCharacter`.** ADR-009's rule that a handed-in value moves no
  die, carried to dice that are read only sometimes. A cast inside a helper is one `??` from being
  skipped. Reading against the rows left keeps the owner's odds.
- **Rows as data.** A reworded row cannot silently drop a clash or the old-hair rule.
- **The writer makes a kind of look concrete.** The dice already decide what stands out, which is
  where the variety comes from; one die per "or" would cost three casts and still leave every kind
  row to the writer.
- **Straight after the name.** The epic says so, and the tone files' "first two lines" then fall on
  the passage, where the tone should show first.
- **In `SKILL.md` and the files that walk it.** Every brief has a look, so there is no case for code
  to choose a file by. A file that walks the output but leaves out a part tells the writer two
  different outputs.
- **The namer.** The name is settled before the writer starts, and the writer may not change it.

## Consequences

- Every new brief has nineteen fields, sixteen of them rolled. `--load` shows an old save's three
  as unknown.
- The confirmation gains three lines, the `[app] brief` line is longer, and `--load`'s unknown line
  for every save before this record names three more fields. These are console changes the owner
  checks.
- A baker can roll unique armour or a rare weapon. The writer makes it concrete inside the scene.
- A die added after this record goes after the hair colour cast, in `RollCharacter`.
- The passage makes every character longer, by a few sentences.
- Paid: the epic's two scenes at three seeds each, one more bakery run on a seed whose look die
  comes up 20 (seeds 1 to 12 never do), the interviewer alone on scenes that test its new rules,
  reruns after any rewording, and one re-record of the replay fixture last.
