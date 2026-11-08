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
| 4 | Keep a character | Mark the characters worth keeping, and open them by name |
| 11 | Change a kept character | Change a kept character without starting over |
| 5 | Ready to grow | A structure that can sit behind a website and a database |
| 6 | Friends can use it | A website with logins and a database, deployed |

Epic 4 is ready to start. Epics 11, 5 and 6 are drafts: sharpen each one before it starts.

## 4. Keep a character

**Goal.** I mark the characters worth keeping, see them in a list, open one by its name instead of
remembering its number, and take one off the list if I kept it by mistake. Today every run is saved
and numbered, and my file holds 16. Saves 9 to 16 are eight innkeepers in the same city inn, and the
goblin baker I liked is save 8. The only way back to him is to remember the 8.

This is the first piece of what was "Keep and edit". Changing a kept character is epic 11.

**Constraints.**
- Here "keep" means marking a save as worth keeping. Every run is still saved and numbered, as now
  (ADR-003), and keeping does not copy a save. The code says "keep" for "save" today
  (`save_character`'s description, the save comment in `Program.cs`), so the ADR rewords those.
- A kept character is found by its own name, the one the namer gave it and the write-up opens
  with. A save from before the namer (1 to 8 in my file) has no name, so I give it one when I keep
  it. A save that has a name keeps it, so a second save with a name already on the list cannot be
  kept until epic 11 adds renaming.
- Keeping takes a save's number, the one a run prints as `saved as N`. Opening and taking off the
  list take a kept name. `--load` still opens by number.
- Making a character does not change. A run prints and saves exactly what it does today, so the
  replay test passes unchanged and is never re-recorded.
- A save that is not kept stays byte for byte as it is, and every save from before this epic still
  loads with `--load`. Taking a character off the list puts its save back byte for byte, so a name
  I gave it is forgotten.
- No model call, so nothing here costs money. The new tools are app-only, like `save_character`.
- It stays in the JSON file on the server. No database and no users. Those are epics 5 and 6.
- It changes the save format and the server's tool list, so an ADR comes first.

**Done when.**
- Free tests, the ones for the new commands run with `NPCFORGE_OFFLINE=1` so none can call a model:
  - Keeping a save that has a name puts it on the list under that name. A save that is not kept
    never appears.
  - A keep changes the kept entry only by its mark, and every other entry in the file stays byte
    for byte as it was.
  - With two or more characters on the list, opening one by the name the list shows, typed in a
    different case, prints the same character that `--load` prints for its number.
  - A save with no name can be kept only when a name is given, and then opens by that name.
  - Keeping under a name already on the list, typed in a different case, is refused with the
    reason, and the list is unchanged.
  - A name that is not on the list, or a number that was never saved, stops with the reason and
    exit code 1. So does `--load` with a number that was never saved.
  - Taking a character off the list removes it from the list and puts its save back byte for byte.
    It still loads by number.
  - A new run's save holds exactly `brief`, `name` and `text`, as today.
  - The replay test passes and is not re-recorded.
- On my own file I keep the witch (save 7) and the baker (save 8) under names I give them and one
  innkeeper under its own name, list them, open each by name and take one off. I do this myself,
  free (*needs my eyes*).
- The command names, and what the list shows for each character (*needs my eyes*).

**Tasks.**
- ADR: what a kept character is in the save, the new app-only tools, how a typed name is matched to
  a kept one, and the rewording of "keep".
- Keep, take off the list, list and open by name, on the server and in the console.
- Define "keep" in AGENTS.md's Vocabulary.

**Not in this epic.** Changing a kept character, a name the GM gives in a scene, and recurring
characters (epic 11). A database and logins (epics 5 and 6).

## 11. Change a kept character (draft)

**Goal.** I change a kept character later without starting over, including follow-ups like "make
him warier" (moved here from "Talk to it"). A kept character can have a fixed name, including one
the GM gives in a scene ("an innkeeper called Bram") or by a flag. Until then the name dice apply
and the namer's name wins (ADR-009). A kept character may be marked recurring, and the open
question moves with it: what extra does a recurring character get? (moved here from "Better
characters").

**Constraints.** Saving and loading stay app-only: the model never touches the saved file. A change
that rewrites the character calls the model, so its checks are paid.

**Done when.** I can open a kept character by its name, change part of it, and see the change
saved.

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
