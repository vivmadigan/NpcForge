# ADR-013: Saving is the GM's choice, a run can roll again, and an app-only tool lists the saves

- **Status:** accepted
- **Date:** 2026-11-08
- **Supersedes:** the "every run saves automatically" part of ADR-003. The rest of ADR-003 stands:
  a save is the brief and the text together, in one JSON file, loaded by number.

## Context

Epic 4, "Save what I like". ADR-003 saved every run "for the moment", until the console gave the
owner a way to choose. The file now holds 16 saves. Saves 9 to 16 are eight innkeepers in the same
city inn. The one character the owner liked, the goblin baker, is save 8, and the only way back to
him is to remember the 8.

Three things are missing: a way to say which characters to keep, a way to try the same scene again,
and a list of the saves. The design was checked on 2026-11-08 against `Program.cs`, `ChatAgent.cs`,
`McpToolSource.cs`, `Lab.cs`, `SpendingCap.cs`, `CharacterTools.cs`, `Storage.cs` and the tests'
`ConsoleProcess.cs`. Three runners (Opus, Sonnet and Haiku) each sketched a design. The code
showed these facts:

- The server's dice are one `static readonly Random` in `CharacterTools`, made once per server
  process from `NPCFORGE_SEED`. A second `roll_character` on the same server takes the next dice.
- `load_character` throws `McpException` for a number never saved, and `CallDirectAsync` turns that
  into an `InvalidOperationException` that `Program.cs` does not catch. `--load 99` today prints
  `Unhandled exception`, a stack trace, and exits with -532462766, not 1.
- `SpendingCapReachedException` is caught only in `Lab.cs`. In a normal run it ends the app with a
  stack trace.
- Every flag in `RunOptions` takes a value. None is a switch.
- The replay fixture is a flag run with one character in four model calls. `ReplayChatClient` stops
  the run when the app makes a call the recording does not hold.

## Options considered

Which side picks the role and the place out of a saved brief:
- **The console.** The tool would return each save's whole brief, and the console would read
  `occupation` and `setting` from it. The console already reads brief fields from JSON (`SavedView`,
  `ChatAgent.ToneOf`), but the tool would then send sixteen briefs to show two fields of each.
- **The server.** The server owns the file and already holds each brief as the `JsonElement` it was
  saved as. It reads the two fields and sends only them. All three runners chose this.

What the list shows for each save:
- Number, name, role and place, as the owner asked.
- The same, plus the players' want or the race. Checked against the 16 saves, neither field tells
  saves 1, 2 and 3 apart: all three are an unnamed innkeeper in an inn in a major city, wanting the
  name of a fence, with no race. Nothing short of the text separates them, and the list never reads
  the text.

The repeatable step:
- **A class built once per run** that holds the answers, the scene, the server and the client, with
  one method that rolls again. It has one caller.
- **Two static functions over two records.** `Program.cs` holds the loop, so the order of events
  stays readable there, as AGENTS.md asks.

The words the question accepts:
- The three words and their first letters.
- The three words only. A single letter is one key away from another answer, and an answer misread
  as leave throws away a paid character.

## Decision

1. **Saving is the GM's choice in every run**, flag runs included. A run that ends without a save
   leaves the file as it was.

2. **The question comes after each character is shown.** It goes to stderr with the rest of the
   dialogue, and the answer is read from `Console.In`, the reader the interview uses. The proposed
   wording is `Keep this character? Type save, again (new dice, same scene) or leave.`
   - `save`, `again` and `leave` are accepted, trimmed, in any case. Nothing else is, a blank line
     and a first letter included.
   - `save` calls `save_character`, prints `[app] saved as N`, and ends the run with exit code 0.
   - `leave` prints `[app] not saved` and ends the run with exit code 0.
   - Anything else prints `[app] "<answer>" is not save, again or leave.` and asks again. A typo
     never throws away a paid character.
   - End of input counts as leave: `[app] not saved: end of input`, exit code 0.
   - The answer is the user's own input, not the model's prose, so code may read it.

3. **Roll, name and write become one step the run repeats.** A restructure PR makes this change
   before the question exists, with the replay unchanged. The shape:

   ```csharp
   // What stays across a roll again: the GM's answers or the flags, and the scene.
   record Ask(Dictionary<string, object?> RollArguments, string? Scene);

   // One roll's result: the brief as the server returned it, and the name and text written from it.
   record Draft(string BriefJson, Written Written);

   static class CharacterRun
   {
       // Rolls, prints the brief line, names and writes. Throws NoNameException as WriteAsync does.
       static Task<Draft> RollAndWriteAsync(IChatClient client, ChatOptions options, McpToolSource tools, Ask ask, CancellationToken ct);

       // save_character with the draft's brief, name and text. Returns the new number.
       static Task<int> SaveAsync(McpToolSource tools, Draft draft, CancellationToken ct);
   }
   ```

   `Program.cs` builds one `Ask`, then loops: roll and write, print the text to stdout, ask. `again`
   goes round the loop with the same `Ask`. A roll again rerolls only what was rolled. Interview
   answers and flags stay, the writer is sent the same scene, and the namer names from the new dice.
   Every roll again uses the run's one `McpToolSource` and its one model client. Under `--seed`, the
   second roll takes the next dice from the server's one `Random`, so a run with roll agains gives
   the same characters every time. Each character's text goes to stdout in turn, and the saved one is
   the last shown.

4. **An app-only tool, `list_characters`, lists the saves.** It takes no arguments and joins
   `McpToolSource.AppOnly`, so the model never sees it. It returns one entry per save, in number
   order:

   ```json
   [
     { "number": 8, "name": null, "occupation": "goblin baker", "setting": "tiny bakery" },
     { "number": 9, "name": { "syllables": ["Nell"], "family": "Farrow" }, "occupation": "innkeeper", "setting": "an inn in a major city" }
   ]
   ```

   The server reads only each save's `name` and the brief's `occupation` and `setting`, never the
   text. A brief without one of those keys gives `null` for it. The name keeps the shape the save
   holds, so the console's `NamedCharacter` reads it and `Full` stays the one place that joins the
   syllables. The fields keep the brief's own names, so "role" and "place" exist only on the screen.

5. **`--list` prints the list.** It is a switch, the first flag in `RunOptions` with no value, and it
   goes with no other flag, as `--score` does. Each save is one row on stdout: the number, the name
   or `(no name)`, the occupation and the setting. An empty file prints `[app] nothing saved yet` on
   stderr and exits with 0. `McpToolSource` connects before `ModelClients.Create` runs, so `--list`
   and `--load` call no model and need no API key.

6. **`--load` of a number never saved stops with the reason and exit code 1.** `CallDirectAsync`
   throws a new `ToolRefusedException`, a subclass of `InvalidOperationException` that carries the
   server's text, when a tool returns an error. `Program.cs` catches it around the load only and
   prints `[app] not loaded: <the server's reason>`. Every other caller sees the same exception type
   as before.

7. **The spending cap stops a run cleanly.** `Program.cs` catches `SpendingCapReachedException`
   once, around the interview and the roll again loop. It prints `[app] stopped: <the cap's
   message> Nothing was saved.` and exits with code 1, with no stack trace. Every roll again draws
   on the same `SpendingCap`, so every roll again counts against it.

## Why

The owner judges a character by reading it, so the choice comes after the text is on the screen.
Code cannot make that choice, and a default would make it for the owner. Asking again on an unknown
answer is the owner's rule. A paid character costs more than one more prompt, and only end of input
means the GM has gone.

The server picks the two fields because it is the one process that reads the save file. The console
gets a short list with the brief's own names, and no brief type crosses the wire. Two static
functions keep the loop in `Program.cs`, where the order of events is meant to stay visible. A class
with one caller would hide that order behind a method name.

Roll again has to use the same server, or `--seed` would restart the dice and roll the first
character again. The server's one `Random` per process already gives this, so no seed passing is
needed.

## Consequences

- The save format does not change. A new save is still one past the highest number, and the
  16 saves load and list as they are.
- The model is sent the same messages, so `NpcForge.Tests/Fixtures/replay.jsonl` is not re-recorded.
  The tests that run the app change only to answer `save` at the end of their input. A run whose
  input ends at once now saves nothing, and so does the verify skill's replay until it answers
  `save`.
- Line 1 of a recording holds flags, and the question's answers are typed, so a recording does not
  hold them. A recording with roll agains replays only when the same answers are piped in.
- `tools/list` gains `list_characters`. The model's list does not change, because the tool is in
  `AppOnly`.
- `[app] not loaded`, `[app] stopped` and `[app] not saved` are new endings. Each exits with the
  code the decision names, and none prints a stack trace.
- Connecting before the model client changes the start of every run. A missing API key is now
  reported after the server starts, not before.
- The free tests cannot yet cover every part of a roll again. The fixture holds one character, so a
  replay that answers `again` stops at the namer's first call. A copy of the fixture with its
  recorded usage raised to the cap does cover the cap case for free. The second brief prints, and
  then the cap refuses the namer. A test that writes two characters needs a second recording, which is a
  paid run, or a fake model that the console process can start. The owner chose the paid run: one
  seeded `--record` that rolls again once and then saves, made in the PR that adds roll again, after
  that behavior settles, and asked for first with its command.
- Saves 1, 2 and 3 look the same in the list. Only `--load` tells them apart.
- Revisit if: the owner wants to delete a save (that needs the file to remember the highest number
  ever used), several characters from one scene are saved in one run, or a database replaces the
  file (epics 5 and 6).
