# Architecture

How NpcForge fits together as it is now. Why it is shaped this way is in `README.md` and
`docs/decisions/`.

## Shape

```
NpcForge.slnx
├── NpcForge.Core      every rule the front ends share: the brief, the tables, the dice, the namer,
│                      the writer, the interview, the loop, the save rules and the skills
├── NpcForge.Console   the app: flags, the model clients, record and replay, the lab, the file store
│                      and the owner's characters.json
└── NpcForge.Tests     xUnit, free: fake models, the real dice and tools, and the whole app against a recording
```

One process. The console calls the core in-process and hands it what lives outside it: an
`IChatClient` for the model and an `ICharacterStore` for the saves, the core's two ports (ADR-014).
Inside the core, folders follow the domain: `Brief`, `Interview`, `Writing`, `Loop`, `Saves`.
`CoreReferenceTests` and the core's `BannedSymbols.txt` hold the boundary: the core references only
.NET and Microsoft.Extensions.AI.Abstractions, writes only traces to the console, and reads no file but
the bundled skills, through `SkillFiles`. Until epic 5 the dice and the saves lived in an MCP server
that the console started as a second process; ADR-016 records the switch.

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

Steps 3 to 5 are one step, `CharacterRun.RollAndWriteAsync` in the core, and `Program.cs` loops over
it (ADR-013); `CharacterRun.SaveAsync` is the save. The answers and the scene are one `Ask`, built
once, so a roll again rolls only what was rolled. Every roll uses the run's one `Dice`, so under
`--seed` the next roll takes the next dice, and a run with roll agains gives the same characters every
time. The question (`KeepQuestion`) goes to stderr and reads `Console.In`, the reader the interview
uses. It accepts `save`, `again` and `leave`, trimmed, in any case, and a UTF-8 byte order mark before
the first answer is ignored. Anything else, a blank line or a first letter included, prints
`[app] "<answer>" is not save, again or leave.` and asks again. Only the character on the screen
when the GM types `save` is saved, and a run ends with `[app] saved as N`, `[app] not saved` or
`[app] not saved: end of input`, each with exit code 0.

Step 2a runs only when no answer flag and no `--load` is given ("The scene interview"). Its
answers go to the same roll the flags fill. A yes also hands back the scene as the GM typed it, and
the writer is sent it word for word, between `<scene>` tags, before the brief (ADR-008).
`ChatAgent.FirstMessage` builds that message: the scene, then `Their name is Kessa Vale.`, then the
brief, which stays last. Code never reads the scene, and it is not saved. `[app] scene sent (<n>
chars)` on stderr shows it went. Steps 4 and 5 are both `ChatAgent.WriteAsync` ("The namer").
`--load <number>` replaces steps 2 to 6 with the store's `LoadAsync`, and `--list` with its
`ListAsync`: no dice, no roll, no model, no cost. The order at start-up is config, flags, the saves,
then `--list` or `--load` if given, then the model client, then the dice. So `--load` and `--list`
need no API key and print no dice line. A number never saved stops with `[app] not loaded: No
character saved as N.` and exit code 1. `--seed`, `--record` and `--provider replay` are under
"Replay".

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
| `--list` | off | A switch, alone like `--score`. Prints one row per save (number, name or `(no name)`, occupation, setting) from the store. An empty file says `[app] nothing saved yet` on stderr |
| `--record <file>` | none | Also writes every model call to a file. Needs `--seed` |
| `--recording <file>` | none | The file `--provider replay` answers from |
| `--lab skill=<dir>,<dir>`, `--lab temperature=<t>,<t>` or `--lab tone[=<tone>,<tone>]` | none | The writers' lab (see "The lab") |
| `--repeat <n>`, `--mode side\|blind`, `--out <dir>` | 1, `side`, a new temp folder | How many takes of each variant, blind or not, and where the sheet goes. Only with `--lab` |
| `--score <dir>` | none | Marks the guesses for a finished blind lab. No model, no dice, no key |

A flag the table does not know, a flag with no value (last, or before another flag) and a stray word
stop the run in `RunOptions.Parse`, before any model client is built. So do `--record` without
`--seed`, `--recording` without `--provider replay`, `--record` with it, and `--record` with no
answer flag (a recording replays flags, and a typed scene is not one). `RunOptions.Interview` is true
when no answer flag and no `--load` was typed; defaults do not count as typed.

## The lab

`--lab` writes one brief several ways and lays the results out side by side, so a change to the
writer is judged on a sheet instead of from memory (ADR-009). `Lab.cs` owns the run and `LabSheet.cs`
the sheets, which are pure.

- **One axis per lab.** `skill=<dir>,<dir>` gives each variant its own skill folder (a `SKILL.md`);
  `temperature=<t>,<t>` sets `ChatOptions.Temperature` on a copy per variant; `tone` (every tone in the
  schema) or `tone=Comic,Serious` writes the same character in each tone. A lab needs `--seed` and
  the answers as flags, and is refused with `--load`, `--record`,
  `--provider replay`, a repeated folder, temperature or tone, a temperature on `anthropic`, and
  `--lab tone` with `--tone`. Every refusal comes before a model client is built, except a tone name
  the roll lacks, which the lab checks against `Dice.Fields` after the client is built and before any
  model call.
- **The tone axis.** The base brief is rolled once. Each variant's brief is rolled on the run's one
  `Dice` with every field of the base brief handed back in and `tone` replaced (`BriefJson.WithField`),
  so the dice stay the only author of briefs and the briefs differ in tone alone. The sheet's brief
  leaves the tone out, because the labels give it and a blind sheet must not.
- **Tone files are checked first.** Before the question, the lab compares the schema's tones with the
  `tones/` folder of the default skill and of every `skill=` folder, and stops with the missing paths.
- **One write path, one name.** The lab rolls once and names once: the namer runs on the base
  brief after the `yes`, and every variant and take is written with that name through
  `ChatAgent.WriteAsync`, the call a normal run makes. Every sheet then shows one character. A namer
  that gives no name stops the lab with `[lab] not written: no name followed the dice (K, 2, V)`
  and no sheet, and the exit code is 1, as it is for a normal run. It is handed no store, so it
  saves nothing, and `CharactersPath` names a temp file for the run as a second stop. A brief the dice
  refuse stops the lab before its question, as it stops a run.
- **No empty folder is left behind.** A `--out` folder the lab made itself is removed again when the
  lab stops without a sheet, and when anything escapes it (a replay mismatch, a 500 from the namer).
  A folder that was already there stays, and so does one with anything in it.
- **It asks first.** Before any model call it prints the number of characters and an estimate in
  tokens against the session cap, and waits for `yes`. An estimate over the cap is refused. The
  estimate counts two calls per character, the usual case, and two for the namer, with the writer's
  message counted before the name line exists; the spending cap stops a lab that runs over, and the
  sheet says where it stopped. The `--out` folder is made and test-written before the
  question, so a bad one fails for free.
- **Side mode** writes `sheet.md`: the brief once, then each character under its label and take with
  its `[skill]` line. A variant that fails stays on the sheet with its reason.
- **Blind mode** writes `blind.md` (characters as A, B, C), `blind.json` (the same, as data) and
  `key.json` (letter to label, and any failures), and no `sheet.md`. The run order and the letters are
  shuffled from `--seed`. While the namer and the variants run, `Console.Error` goes to a null
  writer, so no trace line gives the key away. Afterwards the accepted name is printed as
  `[app] name Kessa Vale (K, 2, V)`, because every variant shares it and it is kept nowhere else, and
  then only a count of failures.
- **`--score <dir>`** reads `blind.json` and `key.json`, asks for a guess per letter, prints
  `n of m right` and writes `scored.md`. It reads no model prose as markup and runs before any client,
  store or dice.

## What the model can reach

The writer is offered one tool, `lookup_archetype`, which says how a kind of person usually behaves:
`CharacterRun.WriterTools`, a `ConsoleTool` over `Archetypes.Lookup`. The lab writes through the same
list. The namer is offered `give_name` and the interviewer `record_answer`, each a `ConsoleTool` of its
own. A `ConsoleTool` answers only the name it was offered, and every failure comes back as text starting
`Tool failed:`, so the model can read it and carry on. After a malformed `lookup_archetype` call that
text is the binder's own reason (ADR-016).

Roll, save, load and list are plain methods the app calls, never a tool in a list, so no model can see
or name them (ADR-014, decision 5):

| Method | Does |
| --- | --- |
| `Dice.Roll(arguments)` | Rolls the brief from the roll's arguments. Any rolled trait can be passed in instead. A field it cannot read is refused before any die is cast ("The brief") |
| `Dice.Fields` | The roll's fields, from its schema: name, description, choices and what a missing one gets. The interview, the tone and not-given checks, the lab and `--load` read them |
| `ICharacterStore.SaveAsync` | Keeps a brief, its name and its text under a number greater than every number held, and returns the number. `CharacterRun.SaveAsync` hands it the typed brief, so a save cannot lack a field |
| `ICharacterStore.LoadAsync` | The saved brief, name (when it has one) and text by number, or null for a number never saved |
| `ICharacterStore.ListAsync` | Every save in number order: its number, its name (or null) and the brief's `occupation` and `setting`. Never the text |

`roll_character` is still the roll's name: `Dice` builds an `AIFunction` of that name over its typed
`Roll`, for the schema and the binding, and never offers it to a model.

## The loop

`AgentLoop.RunAsync` is the whole agent. Four rules:
- **The exit is the absence of tool calls.** Not `FinishReason`, not `Text`: each provider shapes
  those differently.
- **History is the state.** The model keeps nothing between calls, so every request resends the
  list. The caller builds it; the loop only appends.
- **Every tool call gets a result appended**, failures included. Skip one and the next request is
  malformed.
- **The turn cap is not optional.** Eight turns, then it throws.

`IToolSource` is the seam to where tools come from (`ConsoleTool` in the app, `FakeToolSource` in tests).
`IChatClient` from Microsoft.Extensions.AI is the seam to the vendor, and `ModelClients.Create` is
the only code that knows which one. `UseFunctionInvocation` stays off: it would hide the loop.

## The brief

`CharacterBrief`, in the core. `Dice.Roll` is where loose arguments become one, and everything after it
trusts the type.
- **Supplied:** `setting` and `occupation`. `playersWant` is supplied too, or null for "not given"
  (ADR-010): the parameter is `string?`, required, with no default, so a caller must send the null
  and a forgotten one is refused. `CharacterBrief` always writes the null, and a blank want is
  refused by name.
- **Given, inferred or rolled (ADR-010):** `difficulty`, from `Tables.Difficulties` (Easy 1,
  SomeWork 2, Wall 1). Its die is the thirteenth, cast every time. The parameter comes after
  `occupation` because C# puts optional parameters last; the brief keeps its own order.
- **Rolled, inside what the difficulty allows:** `characterWants`, `obstacle`, `attitude`,
  `mannerism`, `weakness`, `willNotDiscuss`, `wrongAbout`.
- **Given, inferred or rolled (ADR-009):** `tone` (Serious, Comic or Heartfelt), `race` (free text,
  rolled from a weighted table), and the name dice `nameLetter`, `nameSyllables` (written "1", "2",
  "3") and `familyLetter`. Their dice are cast after the seven above, so a seed's old traits stay as
  they were. A letter field is never read from a scene: it is rolled or set by flag.
- **Given, inferred or rolled (ADR-011):** `gender` (Male or Female), `age` (Teenager, Adult,
  MiddleAged or Old) and `look` (free text). Seven casts follow the thirteenth die, every time and
  all in `Dice.Roll`: gender, age, the look's d20, two extra looks, an eye colour and a hair
  colour. `Looks.Read` is pure and reads the look from its casts and the settled age, against the
  owner's rows on `Tables`: a 20 gives two looks that never repeat or clash, written with "; "
  between them; a colour look carries its colour after ": "; an old character never gets snow white
  or silver hair. `rolls-seeds-1-to-12.json` holds the older fields and is never rewritten;
  `looks-seeds-1-to-12.json` pins the three new ones for the same seeds.

The difficulty filter is code: each want is tagged with the difficulties it fits, and `Easy` never
rolls `Wont`. The wants and obstacle dice are cast first, as `NextDouble` values, and read against
their tables only once the difficulty is settled, given or rolled (`Dice.ReadCast`). Seeded
`Random` computes `Next(n)` as `(int)(NextDouble() * n)`, so this keeps every seed's brief. Enums travel as their names, and the roll refuses a value an enum does not
define. The brief is settled once and never changes, and it is
half of the save format (ADR-003).

A trait handed in to `roll_character` is used as given. The difficulty filter shapes the dice, not a
handed-in trait: `Easy` with an obstacle of `Wont` the GM described is kept (ADR-007). Every die is
still cast, in one fixed order, and a handed-in value replaces its die's result, so the same seed
rolls the same other traits whatever is handed in (ADR-009).

`Dice.Roll(arguments)` checks the arguments against `Dice.Fields` before it binds them (ADR-016). A
required field left out is refused as `setting is missing, and the roll needs it: text.`, and a choice
the binder's own converter cannot read as `tone grumpy is not one of its values: Serious, Comic,
Heartfelt.` An enum number that is not a value, a blank want and a blank look are refused in the typed
`Roll`, in the same words, before any die is cast. Each is a `RollRefusedException`: `Program.cs`
prints `[app] not rolled: <reason>` and exits with code 1, in a run and in a lab. Every spelling the
roll took before still binds: a name in any case or with spaces round it, a number that is one of the
values, a `JsonElement`, a null want, and a key it has no field for (`RollArgumentsTests`). A value
of the wrong type stays the binder's own `ArgumentException`, a caller's bug and not the GM's slip.

## The namer

ADR-009 decision 4. `Namer.RunAsync` runs before the writer, inside `ChatAgent.WriteAsync`, unless
the `WriterInput` already carries a name (the lab's variants do). It runs `AgentLoop` with the
`namer` skill, a first message that holds the race, the gender, the name dice in words and the scene, and one
console tool, `give_name(syllables, family)`, for example `["Kes", "sa"], "Vale"`. The writer's
tool list does not change, because the namer runs on a copy of the options.

- **The check** is `NameCheck.Check`, pure, on the call's arguments, each rule with its own
  `NameRejection` and reason: no syllables, an empty syllable, a syllable with no letter in it
  ("-", "1", a zero-width space), a syllable with a space inside, the wrong first letter, the wrong
  syllable count as the namer split it, no family name, a family name with no letter or with a space
  inside, the wrong family letter. Letters ignore case as ordinal text, so a dotless `ı` is not `I`
  and a long `ſ` is not `S`. The namer's skill is sent with `\n` line endings, as the writer's is, so
  a recording made on Windows has the same fingerprint. `NameDice` reads the dice from the brief's JSON (`nameSyllables` is "1" to "3").
- **The first accepted name wins.** A later call is refused (`AlreadyNamed`). The loop ends on the
  first turn with no tool call, so a named character costs at least two calls.
- **No name, no writer.** Four turns at most (`Namer.MaxTurns`). A name accepted before the cap
  stands; with none, `NoNameException` stops the run before the writer is paid for:
  `[app] not written: no name followed the dice (K, 2, V)`, exit code 1, nothing saved.
- **What is not checked:** that the name sounds like its race, and that the writer's text uses the
  name it was told. Code cannot hear a name or read the text; only paid runs and the owner's eyes
  check those.

## The scene interview

ADR-007 is the design. `SceneInterview.RunAsync` greets the GM, reads one line (asked again while it
is empty), and runs `AgentLoop` with the `scene-interviewer` skill and one tool, `record_answer(field,
value, quote)`. The tool lives in the console (`ConsoleTool`, as `give_name` does), and any other name, `roll_character`
included, fails as `Tool failed: No tool named '...'`. The interview holds no dice and no store, so it
cannot roll, save or load. It returns `roll_character`'s arguments after a `yes`, or null after
anything else, end of input included, and `Program.cs` rolls only on the arguments. The loop gets
one turn per field plus three; out of turns (`TurnCapReachedException`) it goes on to the
confirmation with what was recorded, since every answer there already passed its checks.

- **The fields** come from the roll's schema, `Dice.Fields` (`RollField.ListFrom`): name, order,
  description, choices for an enum, and what a missing one gets (`WhenMissing`, ADR-010): not in
  `required` is *Roll*; required with a type that admits null is *NotGiven*; any other required field
  is *Ask*. Today that makes `setting` and `occupation` Ask, `playersWant` NotGiven and the rest Roll. The SDK writes the nullable
  `ObstacleKind` as type `["string","null"]` with a null among its choices; the null is dropped. A
  trait added to the roll reaches the interview with no console change.
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
  stored as the GM typed it, cut from the scene; *inferred* for a choice, stored as the roll spells
  it; *rolled* for a Roll field nobody recorded, left out of the roll; *not given* for a NotGiven
  field nobody recorded, sent to the roll as null. An interview never uses a flag default.
- **The questions** (ADR-010, decision 4): after the interviewer's loop, code asks one question for
  each Ask field nobody recorded, in schema order, built from the field's name and description. An
  empty line asks again, and end of input rolls nothing. The answer enters through
  `SceneDraft.RecordAsked`, which takes only an Ask field and a value that is not blank, and is shown
  as *my words*, "typed when asked". An answer carries its evidence: `Quoted` from `record_answer`,
  `Asked` from a question. `SceneDraft.Lines` throws while an Ask field has no answer, so a
  confirmation or a roll with a hole cannot be built. The answer reaches the writer through the
  brief only; the scene stays the line the GM typed (ADR-008).
- **The confirmation** shows every field, its label, the quote as the GM typed it and the whole
  sentence it sits in. The greeting, the confirmation and the question go to stderr, so stdout stays
  the character text. Labels and quotes stop there: the writer's first message and the save are the
  same as for a flag run (ADR-003, ADR-005).
- **Proved only by a paid run:** whether a real model quotes the GM exactly and leaves unspoken
  fields alone. That is the skill's job. The owner's tavern runs on OpenAI and Claude passed it;
  no free test can.

## Spending

`SpendingCap` wraps the model client outermost, once per session, so the interview and the writer
draw on one allowance of 200,000 tokens. It adds each response's `Usage` and refuses the next call
once the total reaches the cap, before the model is asked; one call may overshoot. It refuses
streaming, which the app never uses and which it could not count. Below the cap it passes every call
through, so the replay sees nothing new.

Every roll again draws on the same allowance. `Program.cs` catches `SpendingCapReachedException`
once, around the interview and the roll again loop, prints `[app] stopped: <the cap's message>
Nothing was saved.` and exits with code 1, with no stack trace (ADR-013, decision 7). A refused call
always comes before the question, so nothing is saved. The lab catches it on its own.

## Writing guidance

`NpcForge.Core/skills/scene-interviewer/SKILL.md` is the interviewer's: quote the GM exactly, at
most one sentence and 12 words, never offer a value, free text only as the GM's words, a choice only
when the GM's words point to it, the players' want read as what they want from this character, a people, an age or a look named
with the role kept out of `occupation`, a gender only from a word that is itself male or female, and
no questions of its own (code asks for a missing Ask field). The interview appends the field list from
the roll's schema to it.

`NpcForge.Core/skills/namer/SKILL.md` is the namer's: follow the dice, fit the name to the race,
the gender and the scene, hand it back split into syllables through `give_name`, and stop once a name is
accepted. It is sent as it is, with no appended text.

`NpcForge.Core/skills/npc-writer/SKILL.md` is copied to `bin` and read into a system message at
index 0, followed by a blank line and one tone file from `skills/npc-writer/tones/` (`Serious.md`,
`Comic.md` or `Heartfelt.md`). `ChatAgent.SystemMessage` picks the file from the `tone` in the brief's
JSON, so the model is never shown the other two. A tone file says how to write; the brief still says
what is true (ADR-009). `ToneFiles.Missing` compares the schema's tone choices with a skill folder's
`tones/`, and `Program.cs` stops with the missing paths before the interview and before any model call.

A field the brief holds as null adds `skills/npc-writer/not-given/<field>.md` between `SKILL.md` and
the tone file, so the tone still governs, and the `[skill]` line names it (ADR-010, decision 5).
Today that is `not-given/playersWant.md`: the character is written around winning the players'
goodwill, with a hook from the scene. A brief with no null is sent exactly as before.
`NotGivenFiles.Require` checks the schema's NotGiven fields on the interview path, before the
greeting; a flag run and a lab never have a null and are not checked.
`SKILL.md` sets the output order: the name, a passage read aloud to the players (ADR-011), then who
they are, where they stand, how they seem, levers, anti-levers and afterwards. The passage is a
block quote of at most five sentences: the scene's room, plain where the scene says nothing; the
character from their age, gender, race and look; their work, in their attitude and mannerism; and
their first words. It holds only what the players can see and hear, and no gap in square brackets.
Each tone file and `not-given/playersWant.md` say what they change in it. `SKILL.md` also sets
what each difficulty and obstacle oblige the writer to do, that
every lever names the brief entry or the words of the scene it comes from, that the writer uses the
name it was told ("Their name is ...") exactly and invents no other, and "show the trait,
never state it". When a scene is sent, it is settled like the brief: the traits are played through
it, the character knows what the scene hints at, and what the scene says would make them give way is
the first lever. A trouble the scene hints at but does not name is left as a gap in square brackets
for the GM, unless the scene asks for one to be made up, so a character plugs into any campaign
(epic 7, `docs/runs/2026-10-25-02-…` and `docs/runs/2026-10-25-03-the-trouble-is-the-gms.md`). Nothing
parses either file, so editing them changes the writing with no code change. The `[skill]` trace line
names the tone file and carries the length and a short SHA-256 of the combined text, so a run names the
guidance it was given: `[skill] .../npc-writer/SKILL.md + tones/Comic.md (2871 chars, 3fa09c1e)`.

## Traces

Stdout is the answer. Everything else goes to stderr with a prefix, so one console reads as one
trace.

| Prefix | From | Says |
| --- | --- | --- |
| `[app]` | `Program.cs`, `CharacterRun.cs`, `KeepQuestion.cs`, `Namer.cs` | `recording to <path>`, `dice: seed N` or `dice: random` (once per run, where the dice are built), the provider and model and the rolled brief (once per roll), `not rolled: <reason>` for a field the dice refuse, `name Kessa Vale (K, 2, V)` (the accepted name and its dice), `not written: no name followed the dice (K, 2, V)`, an answer that is not save, again or leave, `saved as N`, `not saved`, `not saved: end of input`, `not saved: <reason>` and `not listed: <reason>` for a save file that will not parse, `stopped: <the cap's message> Nothing was saved.`, `loaded N`, `not loaded: <reason>`, on a load `name Kessa Vale`, and `nothing saved yet` for an empty `--list` |
| `[skill]` | `ChatAgent.cs`, `Namer.cs`, `SceneInterview.cs` | Which `SKILL.md` and tone file, their combined length and fingerprint (the interviewer's with its field list) |
| `[loop]` | `AgentLoop.cs` | One line per model turn: tool calls, history size, tokens |
| `[tool]` | `ConsoleTool` | Each call the model made, then what it got back. The app's own calls, the roll among them, have none |
| `[interview]` | `SceneInterview.cs` | The interviewer's closing line, as a trace; nothing reads it |


## Storage

One JSON file, `NpcForge.Console/characters.json`, behind `ICharacterStore` (`FileCharacterStore`):
every character the GM chose to save, numbered from 1, the brief, the name and the text together
(ADR-003, ADR-009, ADR-013). A new save takes one past the highest number, which keeps the port's
promise of a number greater than every number held (ADR-015). An entry is `{ "brief": {...}, "name":
{ "syllables": ["Kes", "sa"], "family": "Vale" }, "text": "..." }`; a save from before the namer has
no `name`, which the file store's serializer options leave out when null so the entry keeps its
bytes, and `--load` lists it as unknown. A missing brief key is unknown too; a key saved as null is not given, and `--load` prints
`[app] not given in save N: playersWant` for it (`SavedView.NotGiven`). A field is in one line or the
other, never both. The store keeps each saved brief as the JSON it was saved as (`SavedCharacter.Brief`
is a `JsonElement`), so an old entry is read and written back without passing through `CharacterBrief`
and never gains a field added later (ADR-009). A new save is made from the typed brief
(`SavedCharacter.Of`), so it has every field. `--load` prints the brief line compact and with the
default escaping, as the run printed it, not as the file indents it. Only `FileCharacterStore` touches
the file; a database store in epic 6 implements the same port and passes the same
`CharacterStoreTests`, and `SaveFileGuardTests` holds the owner's file byte for byte. A file that will
not parse throws `InvalidDataException` with its path, and `--list`, `--load` and a save each report it
as one `[app]` line with exit code 1 (ADR-015, decision 4).

`NPCFORGE_CHARACTERS` overrides the path. Otherwise the default, `FileCharacterStore.DefaultPath`, is
found three folders up from the console's bin folder, always absolute, with the two-argument
`GetFullPath`: a build in a folder long enough gets its own folder as a `\\?\` path, where the
one-argument form leaves `..` as it is (lesson 015, `LongFolderTests`).

A replay or a recording run is not a real character, so it never uses the tracked file.
`RunOptions.CharactersPath` decides the file for every run: an `NPCFORGE_CHARACTERS` the caller set
wins; otherwise `--provider replay`, `--record` and `--lab` get a fresh `npcforge-replay-<guid>.json`,
`npcforge-record-<guid>.json` or `npcforge-lab-<guid>.json` in the OS temp folder; a normal run and `--load` keep the tracked file.
The console builds the store on the answer and ends with `[app] saved as N in <path>` whenever it
chose the file, so the temp file can be found. The temp file is left in place on purpose.

## Replay

One real run is recorded once, and the whole app replays against it for free (ADR-005).

- **The seed.** `--seed <n>` reaches the run's one `Dice` directly, never as a roll argument, whose
  arguments stay the brief fields a GM may set. The same seed rolls the same brief, and nothing but the
  flag seeds the dice. The run says which where it builds them: `[app] dice: seed N` or
  `[app] dice: random`.
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
  file against the recording: the text of the writer's last reply, the brief from the writer's first
  message, and the saved name in that message. The writer's calls are found by their system message
  (the npc-writer skill), not by index, because the namer's come first. It refuses a recording with
  fewer than two writer calls or no writer tool call,
  because a dropped model turn only shows on a later call. The child's HTTP proxy is a dead port, so
  a replay that ever lost `--provider replay` fails instead of paying. A second test runs it with
  `NPCFORGE_CHARACTERS` removed and checks the tracked `characters.json` is byte-identical afterwards
  (putting it back if not) and the save is at the path the app printed. Both answer `save`.
- **A second recording: roll again.** `NpcForge.Tests/Fixtures/roll-again.jsonl` is the replay
  fixture's flags at `--seed 1`, answered `again` and then `save`: two characters in eight calls. The
  answers are typed, not flags, so it replays only with the same answers piped in. `RollAgainTests`
  replays it and checks both briefs, both names, and that the save is the second character. A replay
  stops a run that makes one call too many, but not one that leaves calls unused, so the test also
  counts the `[loop]` lines against the recorded calls. A second test replays it twice and gets the
  same two characters, which a roll again on a new `Dice` would not.
- **Offline.** `NPCFORGE_OFFLINE=1` makes `ModelClients.Create` refuse `openai` and `anthropic` before
  it builds a client or reads a key, so a free run cannot reach a model whatever order `Program.cs` runs
  in. `replay` still works. It is a second stop, independent of the dead proxy. `OfflineTests` runs both
  paid providers against the real code, in process with a dummy key and as the whole app.
- **What forces a re-record.** Anything that changes what the model is sent: `SKILL.md`, a tool's
  description or schema, a `ChatOptions` field, the first message. Anything that changes the seeded
  brief: an entry in `Tables.cs`, the order of the picks, a .NET major version that changes
  `new Random(seed)`. And an upgrade of the AI or OpenAI libraries, because the recording is
  their JSON. Re-record after a deliberate behavior change or such an upgrade only, never for a
  restructure. A re-record with no tool call fails the test; ask the owner before paying again. Both
  recordings go stale together. The commands and the rules are in `docs/runs/README.md`.
- **By hand, free.** `docs/runs/README.md` has the replay command. Its save goes to a temp file,
  named on the `[app] saved as` line.
- **Build configuration.** A test that starts the app passes `-c <the configuration it was built in>`
  and checks the console's and the core's builds are newer than their source. The one helper that does it is
  `ConsoleProcess` (lesson 008).

## Tests

`dotnet test` is free: no network and no model. The whole suite, 500 tests, takes about 25 s. A test
that starts the console carries `[Trait("Category", "Process")]`, so
`dotnet test --filter "Category!=Process"` runs the 449 tests that start none in a few seconds. No test
starts a server since the server retired. `LabRunTests` runs alone because the lab swaps the
process-wide `Console.Error`.
- `AgentLoopTests`: the loop exits when the model sends no tool calls, throws at the cap, and runs a
  tool call through the writer's real tool. Fake models throughout.
- `RollArgumentsTests`: what `Dice.Roll(arguments)` binds, pinned on the binder before it learned to
  refuse, and what it refuses: a required field left out, a choice the field does not have, an
  undefined enum value and a blank want or look, each by name, while a value of the wrong type stays
  the binder's error.
- `SeededRollTests`: a handed-in value replaces its die without moving the others, and a handed-in
  look, gender or age moves no die on the next roll.
- `ReplayTests`: the whole app against the recorded run, as above, and the temp-file save.
- `KeepQuestionTests` and `KeepAnswerTests`: through the app, `save` saves and prints its number,
  `leave` and end of input save nothing and say so, an unknown answer asks again and a later `save`
  saves once, a blank line or a first letter is not an answer, and the words are read trimmed and in
  any case. `KeepAnswerTests` reads each answer in process, a byte order mark included.
- `RollAgainTests`: `roll-again.jsonl` replayed with `again` then `save` (two characters, the second
  saved, every recorded call made); the same recording twice giving the same two characters; and a
  copy of the replay fixture with its usage at the cap, where `again` stops with `[app] stopped`,
  exit 1, no stack trace and nothing saved.
- `ListTests` and `LoadTests`: `--list` shows every save with its number, name or `(no name)`,
  occupation and setting, and an empty or missing file says so and exits 0; `--load` of a number never
  saved says why and exits 1 with no stack trace, a load needs no model client and builds no dice, and
  its brief line reads as the run's. All run with
  `NPCFORGE_OFFLINE=1`.
- `ReplaySensitivityTests`: the replay fails on a changed system prompt, an emptied tool list, a
  changed model option and a dropped writer turn, each on the fixture and on a copy with a namer
  call in front, so the writer's calls are found by their system message.
- `NameCheckTests` and `NamerTests`: each rule with its own reason, pure; a scripted namer refused
  for "Tobin" on K and then accepted for "Kessa", a wrong or missing family name refused, a second
  accepted name refused, a name accepted before the cap standing, and what the namer is told and
  offered.
- `NamedRunTests`: the whole app as a process against a recording made in process from a stand-in
  namer and writer: the `[app] name` line before the writer, the name in the save and on `--load`,
  and a namer that never follows the dice stopping the run before the writer with nothing saved.
- `OfflineTests`: `NPCFORGE_OFFLINE=1` refuses both paid providers, in process and through the app,
  and still builds the replay client. `NamedRunTests` replays the whole app with it set.
- `RunOptionsTests` and `FlagRefusalTests`: the flag table, its refusals, when the interview runs,
  and which save file a run uses; and a typo'd flag or a bad lab next to the paid default stops at
  the parser, through the whole app, and `--tone grumpy` stops at the roll with one line and exit 1.
- `RollFieldTests`, on `Dice.Fields`: the fields in order, each one's `WhenMissing` kind, the
  choice lists, a description on each, and a flag default for every Ask and NotGiven field; the hand
  copy in `SceneDraftTests` equal to the real schema; and the three schema shapes, from hand-written schemas.
- `DiceOrderTests`: a late read picks what `Next(n)` picks; seeds 1 to 12 at every difficulty match
  a golden captured before the reorder; and a missing difficulty is rolled last and rolls what handing
  it in rolls.
- `SceneDraftTests`: every check of ADR-007 with its reason, the labels, the value and quote as the
  GM typed them, rolled and not-given fields, the GM's typed answers and their refusals, an Ask field
  with no answer that cannot be confirmed, and the roll arguments. Pure.
- `SceneInterviewTests`: a scripted stand-in interviewer. The model is offered only `record_answer`
  and naming `roll_character` fails; anything but `yes`, end of input included, returns nothing to
  roll; `yes` returns the arguments only after the confirmation was shown; the tavern scene's
  confirmation; a described trait reaches the brief from the real dice; and the app with no answer
  flag, piped a scene with a curly apostrophe and then `no`, sends the scene as typed and rolls,
  prints and saves nothing. A yes hands the scene back
  as typed, trimmed. A scene with no place and no role asks two questions in schema order, an empty
  answer asks again, end of input at a question rolls nothing, and a scene with no want shows
  `[not given]` and sends a null.
- `NotGivenFileTests`: the default skill folder has every not-given file, and the app, run on a copy
  of its build with one deleted, stops an interview before the greeting and names the file.
- `WriterMessageTests`: through the real `ChatAgent` and its skill, the writer is sent the scene word
  for word, then the name, with the brief last; with no name on the input the namer runs first and
  its name reaches the writer, and with one it is skipped; a namer that never follows the dice stops
  `WriteAsync` before the writer; and the system message holds the skill and only the tone file the
  brief names.
- `ToneFileTests`: every tone in the roll's schema has a file in the default skill folder, a deleted
  file is reported by its path, and the app, run as a process on a copy of its own build with one tone
  file deleted, stops before the interview and names the file. An intact copy reaching the interview
  is the control for this run and for `NotGivenFileTests`' (`ConsoleBuildCopy`).
- `CharacterStoreTests`: the save, load and list rules, written once and run against the file store and
  the tests' in-memory store. `SaveFileGuardTests`: on a copy of the real `characters.json`, a new
  save leaves every old entry byte for byte, with no `name` or null added.
- `UnreadableSaveFileTests`: a save file that will not parse is one `[app]` line naming it, with exit
  code 1, on `--list`, `--load` and a save. `LongFolderTests`: a console build in a folder long enough
  for a `\\?\` path still finds its default save file.
- `LabFlagTests`, `LabSheetTests`, `LabRunTests` and `LabScoreTests`: every lab refusal with its
  reason; the sheets, the key and the score, pure; the lab, in process, against the real dice and a stand-in
  model (each variant reaches the writer, the confirmation stops without a yes, blind stderr holds no
  variant, the cap stops it, a failure stays on the sheet, the tone axis gives briefs that differ only
  in tone, the lab names once and writes every variant with that name, a lab with no name writes
  nothing, a skill folder missing a tone file stops the lab before its question, and a brief the dice
  refuse leaves no folder); and `--score`
  as a process with no key.
- `SpendingCapTests`: the cap refuses the call after the limit without asking the model, and stops
  an agent loop before its turn cap.
- `ConsoleProcess`: not a test. The one place a test starts the app as a process: the build
  configuration, a freshness check, the dead proxy, a timeout, both streams read as UTF-8 (lesson
  017), and stdin always
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
- **A `string` parameter accepts a null.** Nullability is only checked by the compiler, so
  `Dice.Roll(arguments)` binds a null `setting` or `occupation` and writes it into the brief. No
  caller sends one (ADR-016).
- **A model may skip a tool it can see.** One run wrote straight from the brief with no tool call.
  That is why the roll is never a tool.
- **Output formatting differs by model:** numbered headings, bold labels, renamed sections. Never
  parse it.
- **Names converge, differently per vendor.** OpenAI models kept reaching for `Mar-` and `Mer-`
  names; Anthropic converges on backstory instead. The name dice and the namer (ADR-009) are the
  answer; the paid runs that show it are still to come.
- **Prompt caching does not engage.** The prompt is about 700 tokens, under the threshold.
- **A console paste is not byte-exact** (lesson 006), and a PowerShell redirect reorders stdout
  against stderr (lesson 005). The save file is the exact record.
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
| The writer reads the scene (epic 7) | The GM's scene reaches the writer, and the control scene gives a character who acts inside it on three seeds of three | |
| Talk to it (epic 2) | A bare run interviews the GM, every field traces to their words or the dice, nothing rolls before a yes, and a spending cap stops a session | |
| Better characters (epic 3) | Tone, race and name dice join the brief; one tone file reaches the writer; a namer names inside the dice and the save keeps it; a blind lab tells the tones apart 5 of 6 times | |
| Fill the gaps in a scene (epic 8) | A missing place or role is asked for, a missing want is not given and written for goodwill, a missing difficulty is rolled; the bakery scene passed on three seeds of three | |
| Read it to the table (epic 10) | A rolled gender, age and look join the brief with no older die moved; every sheet opens with the name and a passage to read aloud, which passed on seven runs of seven across the bakery and control scenes | |
| Faster tests (epic 9) | The free suite went from 12 m 19 s to about a minute with all 455 tests kept: a server closes at once on MCP 2.3.1, starts from its launcher, and the fast part runs on its own in 5 s | |
| Save what I like (epic 4) | Saving is the GM's choice after each character; `again` rolls new dice for the same scene on the same server, so a seeded run repeats; `--list` finds a save and `--load` opens it; the cap stops a run cleanly. The bakery scene rolled again twice and saved the third | |
| Ready to grow (epic 5) | One core, `NpcForge.Core`, called in-process by the console; saves behind `ICharacterStore`, with a file store and an in-memory store passing the same tests; the MCP server retired, with the replay and roll-again recordings passing unchanged and `--list` and `--load` printing what they printed | |
