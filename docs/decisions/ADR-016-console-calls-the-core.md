# ADR-016: The console calls the core in-process: the roll refuses by name, and the shapes as built

- **Status:** accepted
- **Date:** 2026-11-10
- **Supersedes:** the decisions INDEX lines on starting the server and on traces, as ADR-014 planned,
  and the server's part of the lines on structure, storage, tests and replay. It records the owner's
  three decisions for epic 5's behavior PR and every shape that differs from ADR-014's sketch.
  ADR-014 and ADR-015 stand as written.

## Context

Epic 5, "Ready to grow", fourth and last PR (ADR-014, decision 9.4). Three restructure PRs had moved the
brief, the dice, the store port, the loop, the namer, the writer and the interview into `NpcForge.Core`
while the console still started `NpcForge.Server` and talked MCP to it. This PR switches the console to
call the core, deletes the server, `McpToolSource` and the `ModelContextProtocol` package, and moves the
file store and the owner's `characters.json` to the console.

PR 58 left two questions open, and the owner settled them, with a third, before this PR:

- After a malformed `lookup_archetype` call, the server answered `An error occurred invoking
  'lookup_archetype'.` and `ConsoleTool` answers with the binder's own reason. Which does the model read?
- `Dice.Roll(arguments)` threw the binder's `ArgumentException` for a missing argument or an unknown enum
  name, and ADR-014 said `RollRefusedException`. On master, `--tone grumpy` ended in an unhandled exception.
- `SavedView` read a save as raw JSON, which only the server's re-serialised `load_character` text
  needed.

Before any change, the binder was probed with 64 inputs on Microsoft.Extensions.AI.Abstractions 10.8.3
(the server's), 10.10.0 and 10.11.0, and gave the same result for every input on all three. It accepts
an enum name in any case and with spaces round it, a number that is one of the values (as text, as a
number, with a plus sign or a leading zero), a list such as `Easy, Wall` that it reads as one value, a
`JsonElement`, a null for any field, and a key the roll has no field for. It refuses an unknown name, an
empty string and a missing required field with an `ArgumentException` that names a .NET type.

## Options considered

How the roll refuses an unknown choice:
- **Wrap every `ArgumentException` from the binder.** The smallest change. It would also show a caller's
  bug, such as a number sent for the setting, as the GM's slip.
- **Match the text against the field's choices from `Dice.Fields`.** Readable, but it has to copy the
  converter's rules (case, spaces, numbers, a plus sign, a leading zero, a list), and every rule it misses
  turns an input the roll took into a refusal.
- **Read the text through the binder's own converter, with the options it binds with**, and refuse only
  what that converter cannot read. Every input the roll took still binds, by construction.

How `--load` prints the brief line:
- **`GetRawText()` of the saved brief.** The file keeps it indented and unescaped, so the line would
  change on screen.
- **The brief written compact with the default escaping.** Byte for byte what the server sent (measured
  on all 16 of the owner's saves and on a brief with an apostrophe, `Æ`, markup and an emoji).

What an unreadable save file throws:
- **A new exception type in the console.** Exact, and one more type for one caller.
- **`JsonException`, left as it is.** The console could not tell it from any other JSON fault.
- **`InvalidDataException` with the path,** .NET's own type for data in the wrong format, thrown only by
  the file store on the paths the console catches it on.

## Decision

1. **The model reads `ConsoleTool`'s error text after a malformed `lookup_archetype` call** (the owner's
   decision): `Tool failed: <the binder's reason>`. It is the one change in what the model can be sent,
   and no fixture makes such a call.

2. **`Dice.Roll(arguments)` checks the arguments against `Dice.Fields` before it binds them** (the owner's
   decision). A required field left out is refused as `<field> is missing, and the roll needs it: <what it
   takes>.` A choice in words the field does not have is refused as `<field> <value> is not one of its
   values: <the values as the schema writes them>.`, read through the binder's converter (option three).
   An enum number that is not a value uses the same words. Anything else the binder refuses stays its own
   `ArgumentException`. `RollArgumentsTests` pins every accepted spelling, and was run on the binder
   before the change.

3. **`SavedView` takes the typed `SavedCharacter`, and `SaveList.Rows` takes the store's
   `ListedCharacter` list** (the owner's decision). In `SavedViewTests` only the `Entry` helper changed.

4. **The shapes as built, where they differ from ADR-014's sketch:**
   - `Draft(CharacterBrief Brief, Written Written)` has no `BriefJson` property. No caller needs it:
     `RollAndWriteAsync` prints the brief line itself, and `SaveAsync` saves the typed brief.
   - `CharacterRun.WriterTools`, a `ConsoleTool` over `Archetypes.Lookup`, is a public property. The
     sketch chose the writer's tools inside `RollAndWriteAsync`, but the lab calls
     `ChatAgent.WriteAsync` itself, and one list for both keeps the choice in one place.
   - `Lab.RunAsync` takes the run's `Dice` where it took `McpToolSource`, and rolls a tone variant on
     the same `Dice`, as it rolled on the same server.
   - `Program.cs` builds one `Dice` after the options and before the lab and the interview, and prints
     `[app] dice: seed N` or `[app] dice: random` there. `--list`, `--load` and `--score` come earlier and
     build none. It catches `RollRefusedException` once, around the lab and the run:
     `[app] not rolled: <reason>`, exit code 1.
   - The unreadable save file throws `InvalidDataException` (option three). `--list`, `--load` and the
     save after `save` each report it as one line, `[app] not listed:`, `not loaded:` or `not saved:`,
     with exit code 1.
   - `--load` writes the brief line compact with the default escaping (option two).
   - `FileCharacterStore.DefaultPath` is `NpcForge.Console/characters.json`, found three folders up from
     the console's bin folder with the two-argument `GetFullPath` (lesson 015). `Storage` is folded in, and
     the store's serializer options leave out a null name (ADR-015, decision 5).
   - `RepoRoot`, with its long-folder rule, moved to the tests' `ConsoleProcess`, its last caller.
   - The console now resolves Microsoft.Extensions.AI.Abstractions 10.10.0, through Microsoft.Extensions.AI.
     At the base commit it resolved 10.11.0, through ModelContextProtocol 2.3.1, not the 10.10.0 ADR-014
     says. The core's pin stays at 10.8.3 as the lowest version known to work. The replay and roll-again
     tests pass against the unchanged fixtures on 10.10.0, so the model is sent the same bytes.
   - `ConsoleProcess` reads a child's stdout and stderr as UTF-8, the encoding the app writes. Read in an
     encoding taken from the console's state, `RollAgainTests` failed in 3 of 4 full runs once the suite
     stopped starting servers (lesson 017).

5. **The INDEX.** The "Starting the server" line goes. The lines on `IToolSource`, the console's wiring,
   traces, structure, storage, tests and replay lose the server.

## Why

- **The binder's converter decides what a choice is.** A second copy of its rules would drift from it,
  and the inputs it takes are exactly the ones the flags, the interview, the lab and a saved brief send
  (principle-boundary-discipline: the roll is the boundary where loose arguments become a
  `CharacterBrief`, so it is where they are checked, once).
- **Only the GM's slips are refusals.** A number sent for the setting is a bug in the caller, and
  labelling it a refusal would hide it (principle-fix-root-causes).
- **No compatibility layer.** The callers moved and the server, `McpToolSource` and their tests went in
  one commit (principle-migrate-callers-then-delete-legacy-apis). Each test of a rule that outlives the
  server was ported to the core's API with its assertions kept.
- **Less to read.** `Storage` folded into the one store that used it, `Draft` lost a property nobody
  read, and a built-in exception type replaced a new one (principle-subtract-before-you-add,
  principle-minimize-reader-load).

## Consequences

- **The terminal changes once.** The `[server]` lines are gone. `[app] dice: seed N` or
  `[app] dice: random` comes first on a run, and `lookup_archetype` gains `ConsoleTool`'s
  `[tool]   <answer>` line. `--list`, `--load` and the saved files are byte for byte what they were.
  *Needs the owner's eyes.*
- **`--tone grumpy` and `--difficulty Nonsense` end with one line and exit code 1,** with no stack trace
  and before any model call, in a run and in a lab. The verify skill's fence, `--difficulty Nonsense`
  first, still refuses before the model's first turn.
- **A null setting or occupation still binds** (measured): the binder accepts a null for any string
  parameter, and the brief is written with `"setting": null`. No caller sends one. Refusing it would
  change what the roll accepts, so it waits for its own decision.
- **The full suite takes about 25 s and starts no server.** 500 tests, 449 of them in the fast part.
- **Epic 6** adds `NpcForge.Api` beside the console; it builds its own `Dice` per request and its own
  store per friend, and calls `CharacterRun` as the console does.
