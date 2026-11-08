# Roadmap

What's next, as epics, top to bottom. Each epic says what I want and how I'll know it works.
pstack decides how to build it (`docs/pstack.md`), and `AGENTS.md` says how the work is done.

This is a living document. Add, reorder or drop epics and tasks whenever using the app shows
something new. Only finished epics stay, as one line under **Done**.

## Writing an epic

Copy this. Write it the way a user would describe the app, not the code.

```text
## <Name>

**Goal.** What I want, in a sentence or two.

**Constraints.** What must not change, and any limits: cost, rules, style.

**Done when.** Checks that pass or fail: a test, a command, something I can see.
Mark anything only I can judge as *needs my eyes*.

**Tasks.** Optional. One line each, if I already know the pieces.
```

Every epic inherits AGENTS.md's "Rules that must not break", so they aren't repeated here.

Two rules for every epic, now that the Safety net exists:
- **A restructure and a new behavior go in separate PRs.** Otherwise a bug can't be traced to one
  or the other.
- **A restructure passes the replay test unchanged and never re-records it.** A behavior change
  re-records it once, after the change has settled, in the same PR.

## The epics

| # | Epic | In one line |
| --- | --- | --- |
| 4 | Save what I like | Choose which characters to save, roll again, and list every save |
| 5 | Ready to grow | A structure that can sit behind a website and a database |
| 6 | Friends can use it | A website with logins and a database, deployed |
| 11 | Change a saved character | Change a saved character without starting over. For later |

Epic 4 is ready to start. Epics 5, 6 and 11 are drafts: sharpen each one before it starts. Epic 11
waits until after the website.

## 4. Save what I like

**Goal.** I choose which characters are worth saving. Once a run has written a character, the app
asks what to do with it: save it, roll a new one for the same scene, or leave it unsaved. A roll
again rolls every die again, keeps what I or my scene said, and has the model write a new character
from the new dice. I can also list every character I have saved, to find one I had forgotten, and
open it.

Today every run is saved and numbered, and my file holds 16. Saves 9 to 16 are eight innkeepers in
the same city inn, and the goblin baker I liked is save 8. Without a list, the only way back to him
is to remember the 8.

This replaces an earlier plan to mark saves as kept and open them by name. Changing a saved
character is epic 11.

**Constraints.**
- Saving becomes my choice in every run, flag runs included. That replaces ADR-003's "every run
  saved", so an ADR comes first.
- The question comes after the character is shown. Save saves it and ends the run. Leave ends the
  run with nothing saved. Anything else, end of input included, saves nothing, the way the
  interview's "yes" works today.
- A roll again rolls only what was rolled. What I said in the interview or by a flag stays, the
  writer is sent the scene again, and the namer names the new character from its new dice. It runs
  on the same server and the same model client as the first character.
- Each roll again calls the model, so it costs money. The session's spending cap counts every roll
  again, and when the cap stops a run, the run says so and exits with code 1. Today `Program.cs`
  does not catch the cap's exception at all; only the lab does (`Lab.cs`).
- Under `--seed`, a roll again takes the next dice from the same seed (the server makes one `Random`
  per run, in `CharacterTools.cs`), so a run with roll agains is still repeatable.
- The list shows every save, the 16 from before this epic included. For each, it shows the number,
  the name when the save has one, and enough from the brief to tell them apart. It reads only the
  save's name and brief, never the text: saves 1 to 8 have no name, and code never parses the
  model's prose. A save is opened by its number, with `--load` as today.
- Listing is a new app-only tool, like `save_character`. The model never sees it.
- This is a behavior change, but the model is sent the same messages, so the replay fixture is not
  re-recorded. The tests and the verify skill's replay close the input at once today
  (`ConsoleProcess.cs`), so they change only to answer save.
- Roll, name and write become one step the run can repeat, in a restructure PR of its own before
  the question, with the replay test unchanged (the rule at the top of this file).
- It stays in the JSON file on the server. No database and no users. Those are epics 5 and 6.

**Done when.**
- Free tests, through the console as a process, with a recorded or fake model:
  - Answering save saves the character, prints its number and ends the run. Answering leave, or
    ending the input, saves nothing and says so. Both hold in a flag run too.
  - A roll again rolls new dice, keeps every answer I gave, calls the namer and the writer again,
    and asks again about the new character. Only the character I save is saved.
  - A run under a seed rolls again: the second character's dice differ from the first, and the run
    gives the same two characters every time. This goes through the app, so a roll again that
    started a new server would fail it.
  - When the spending cap stops a roll again, the run says so, exits with code 1 without a stack
    trace, and saves nothing.
  - The list shows every save in the file with its number, its name when it has one, and its role
    and place, an old save with no name included. Its tests run with `NPCFORGE_OFFLINE=1`, so none
    can call a model.
  - `--load` with a number that was never saved stops with the reason and exit code 1.
  - The replay test passes, changed only to answer save, and the fixture is not re-recorded.
- The bakery scene, as ADR-010 quotes it: I roll again twice, save the third character, find it in
  the list and open it (*needs my eyes*, paid).
- The question's wording, the command names, and what the list shows for each character (*needs my
  eyes*).

**Tasks.**
- ADR: saving becomes a choice (supersedes part of ADR-003), the question at the end of a run, roll
  again, and the list tool.
- The list. It needs no model, so it can come first.
- A restructure: roll, name and write as one step the run can repeat. The replay passes unchanged.
- The question at the end of a run, with save and leave, and the tests answering save.
- Roll again.

**Not in this epic.**
- Deleting a save. A new save takes one past the highest number (`CharacterTools.cs`), so deleting
  the highest would hand its number to the next save, and `docs/runs/` refers to saves by number.
  Delete needs the file to remember the highest number ever used, which is a change to the save
  format.
- Saving several characters from one scene in one run.
- Changing a saved character, a name the GM gives in a scene, and recurring characters (epic 11).
- A database and logins (epics 5 and 6).

## 5. Ready to grow (draft)

**Goal.** A structure that's easy to keep developing. The same core (the dice, the writer, saving)
works behind the console now and a website later, and saving can move from a file to a database
without rewriting the rest. This comes just before epic 6, when the website and database make it pay.

**Constraints.**
- My preferred style is Clean Architecture. pstack decides the shape, and an ADR records it before
  it's built. That ADR replaces AGENTS.md's "no layering" and "no DI container" rules.
- Nothing visible changes: the replay test passes unchanged and is never re-recorded.
- Earlier thinking on layers is in steps 10 and 12 of `git show eddc444:ROADMAP.md`.

**Done when.** The replay test and every existing test pass with no test edits. The core has no
reference to the console, the model vendors or the file store, and the compiler enforces that.

## 6. Friends can use it (draft)

**Goal.** A few friends can log in to a website, describe a scene, and get and keep their own
characters.

**Constraints.** Each person sees only their own characters. Secrets stay out of the repo. Model
spending is capped, because every friend's run costs me money.

**Done when.** A friend can log in on the deployed site, make a character, log out and back in, and
find it there.

## 11. Change a saved character (draft, for later)

**Goal.** I change a saved character later without starting over, including follow-ups like "make
him warier" (moved here from "Talk to it"). A saved character can have a fixed name, including one
the GM gives in a scene ("an innkeeper called Bram") or by a flag. Until then the name dice apply
and the namer's name wins (ADR-009). A saved character may be marked recurring, and the open
question moves with it: what extra does a recurring character get? (moved here from "Better
characters").

**Constraints.** Saving and loading stay app-only: the model never touches the saved file. A change
that rewrites the character calls the model, so its checks are paid.

**Done when.** I can open a saved character, change part of it, and see the change saved.

## Done

- **Steps 1 to 8** (2026-09-12 to 2026-09-22). The agent loop, the MCP server, the dice, the
  writing skill, and saving. See `docs/architecture.md` "How it was built".
- **Workflow reset** (2026-10-24). Agents write the code, pstack replaces the crew, the docs are
  simplified. See ADR-006.
- **Safety net** (2026-10-24). `--seed` fixes the dice, `--record` records a paid run, and
  `dotnet test` replays the whole app against one recorded run for free. See
  `docs/architecture.md` "Replay" and `docs/runs/2026-10-24-01-replay-fixture.md`.
- **Harden the safety net** (2026-10-25). A mistyped flag stops the run before a model client is built,
  `NPCFORGE_OFFLINE=1` stops the paid clients, a replay or a recording saves to a temp file, a test
  replays the build configuration it was built in, and free tests prove the replay fails on a changed
  prompt, tool list, option or dropped turn. See `docs/architecture.md` "Run options" and lesson 008.
- **The writer reads the scene** (epic 7, 2026-10-25). The writer is sent the GM's scene word for
  word beside the brief, plays the traits through it, makes the scene's condition the first lever,
  and leaves a trouble the scene does not name as a gap for the GM. The control scene passed on three
  seeds of three. See ADR-008, lesson 009 and `docs/runs/2026-10-25-01` to `-03`.
- **Talk to it** (epic 2, 2026-10-25). A bare `dotnet run` greets the GM, reads the scene in one
  go, labels every field of the brief as their words, inferred or rolled, and rolls only after a
  yes. A spending cap stops a session at 200,000 tokens. The tavern scene passed on OpenAI and
  Claude, judged by the owner on runs of their own. See ADR-007 and `docs/architecture.md` "The
  scene interview".
- **Better characters** (epic 3, 2026-10-27). Every brief carries a tone, a race and name dice;
  one tone file reaches the writer; a namer names each character inside its dice before the writer
  starts, and the save keeps the name; a writers' lab compares skills, temperatures or tones side by
  side or blind. A blind tone check scored 5 of 6 on the second tone files, six runs gave six names
  that follow their dice, and an elf and a dwarf read as different peoples. See ADR-009, lessons 010
  and 011 and `docs/runs/2026-10-27-01-epic3-checks.md`.
- **Fill the gaps in a scene** (epic 8, 2026-11-01). An interview asks the GM for a missing place or
  role, marks a missing want `[not given]` and writes the character around winning the players'
  goodwill with a hook from the scene, and rolls a missing difficulty (Easy 1, SomeWork 2, Wall 1).
  Flag runs keep their defaults, and no unit needed a re-record. The Done when was judged against
  ADR-010, which replaced its "one question about the want" with `[not given]`. The bakery scene got
  no question and a baker who offers the thief-catching job, three seeds of three, and a goblin once
  the interviewer split race from role. See ADR-010, lesson 012 and
  `docs/runs/2026-11-01-01-epic8-checks.md`.
- **Read it to the table** (epic 10, 2026-11-03). Every brief rolls a gender, an age and a look
  from the owner's tables (a d20 of looks whose 20 gives two, with eye and hair colours), with no
  older die moved, and a scene's words win. The namer is told the gender. Every sheet opens with
  the name and a passage to read aloud as the party walks in: the scene's room, how they look,
  their work and their first words. Seven runs of seven passed across the bakery and control
  scenes. See ADR-011, lesson 013 and `docs/runs/2026-11-03-01-epic10-checks.md`.
- **Faster tests** (epic 9, 2026-11-05). The free suite went from 12 m 19 s to a median 71 s, all 455
  tests kept and passing, and a break-it check on three random guards failed all three. Each server
  had waited 5 s to close: the console's MCP client moved to 2.3.1, which closes the server's stdin
  first, and the server stays on 2.2.0. The console starts the server's launcher instead of
  `dotnet run`. The owner allowed both app changes, which the epic had ruled out; stdout is
  unchanged, and each run's stderr now ends with the server's own shutdown lines.
  `dotnet test --filter "Category!=Process"` runs the 331 tests that start no process in about 5 s.
  See lesson 014.
