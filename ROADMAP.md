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
| 10 | Read it to the table | A read-aloud opening, rolled looks and a first line |
| 9 | Faster tests | The full suite in half the time, with no test weakened |
| 4 | Keep and edit | Choose what to keep, and change it later |
| 5 | Ready to grow | A structure that can sit behind a website and a database |
| 6 | Friends can use it | A website with logins and a database, deployed |

Epics 4 to 6 are drafts: sharpen each one before it starts.

## 10. Read it to the table

**Goal.** Every character opens with something I can read straight to my players, like the boxed
text in an adventure module: where the character is in the room, what they look like and what they
are doing as the party walks in, ending with the first thing they say. For example:

> The public house has a cosy, familial feel: a few benches beside the main cask, a dozen round
> tables around an inviting hearth. Behind the bar stands a large man with a bushy beard, a bald
> head and an apron that hasn't been washed in weeks. He polishes the oak bar with fierce
> concentration. He notices you as you walk forward and says, "…"

The dice settle some of it, and the model writes the passage from everything rolled, put together.
The sheet still starts with the character's name, for me; the passage comes straight after it, then
everything the sheet had before. The character says their name in the passage only if they would.

**Constraints.**
- Simple is best. An NPC is a cardboard cut-out until the players dig in, so this adds a front to
  the cut-out and nothing more: two looks, a room, an action and a line, on top of what we have.
- How the character looks is rolled, not invented, from the tables below. As with race, my scene's
  words win: a "bald dwarf" is bald, and then nothing else is rolled for the look.
- The room comes from my scene. Where the scene says nothing about the room, it stays short and
  plain, or a gap in square brackets, never an invented layout.
- What the character is doing is written from their job, their attitude and their mannerism. If my
  scene says what they are doing, the scene wins.
- The opening line is written from their attitude, the tone and what they want.
- The namer is told the gender, so the name fits the character.
- The new dice come after the thirteen there are, so every seed's older traits stay as they are
  (ADR-009, ADR-010).
- This changes `roll_character`, so an ADR comes first.
- It changes the writer's `SKILL.md`, so the replay fixture is re-recorded once, paid, after the
  wording has settled, in the same PR (lesson 011).
- Old saves still load unchanged; a save from before this epic has no look dice and shows them as
  unknown.

**Tables** (settled with me, 2026-11-02).
- **Age and gender**, a d8: a male or female teenager, adult, middle-aged or old person, all eight
  equally likely. Cast as two dice, gender then age, so a scene that states one keeps the other
  rolled.
- **Unique look**, a d20: 1 bad posture, 2 bald, 3 beautiful, 4 big nose or ears, 5 cross-eyed,
  6 pale, 7 short, 8 tall, 9 unique armour, 10 unique clothing, 11 unique jewellery, 12 unique
  emblem or symbol, 13 unusual eye colour, 14 unusual hair colour, 15 unusual hair or beard style,
  16 scar or tattoo, 17 multiple scars or tattoos, 18 rare item or weapon, 19 paralysed part,
  20 two more looks. The two more never repeat a look, never come up 20, and never clash: short with
  tall, bald with a hair look, a scar or tattoo with multiple.
- **Unusual eye colour**, a d8: violet; amber, almost gold; pale grey, nearly colourless; one blue,
  one brown; bright green; so dark they look black; silver; red.
- **Unusual hair colour**, a d8: copper red; snow white; blue-black; a single white streak; dyed a
  bright colour; two colours, half light and half dark; golden, almost metallic; silver. An old
  character never gets snow white or silver, which would not be unusual on them.
- Every look die is cast every time, used or not, so a look handed in moves no other die.

**Done when.**
- Free tests: the brief carries gender, age and look; each one handed in replaces its die and moves
  no other die; a seed's thirteen older traits are what they were before the new dice; a 20 gives
  two looks that never repeat, never are 20 and never clash; an old character never gets snow white
  or silver hair; the interview records a look the scene states, quoted, and a gender or age the
  scene states; an old save loads and shows gender, age and look as unknown.
- The bakery scene and the control scene, three seeds each, give the name, then a read-aloud passage
  that puts the character in the scene's room, describes them from their dice, shows them at their
  work, and ends with an opening line that fits their attitude and the tone (*needs my eyes*, paid).
- The replay fixture is re-recorded with the new skill and passes.

**Tasks.**
- ADR: the look dice, their tables, where they go in the dice order, the namer, and how the writer
  is told.
- The look dice on the server; the namer reads the gender.
- The writer's new section: the read-aloud passage and the opening line.
- Re-record the replay fixture.

## 9. Faster tests

**Goal.** The full `dotnet test` takes about eight to ten minutes, which makes every change slow to
prove. I want it fast enough to run on every change without thinking twice.

**Constraints.**
- A restructure of the tests only. Nothing the app does changes, and the replay test passes
  unchanged and is never re-recorded.
- No test is deleted, skipped or weakened to save time. Every test that fails today on a broken
  guard still fails on it.
- Tests stay independent: a shared server or process never lets one test's save file, seed or
  stderr reach another, and the full suite passes in any order.
- Measure first. The epic starts by timing where the minutes go, so the fix targets the real cost,
  not a guess. The current guess is that the tests starting the console and the MCP server, about
  28 call sites in 13 files, take most of the time.

**Done when.**
- The full suite's time is measured before and after on the same machine, and the after time is
  at most half the before time.
- Every test passes, the test count is unchanged, and a break-it check on three guards picked at
  random still fails its test.
- `AGENTS.md` and the verify skill say how long the suite takes, and how to run the fast part on
  its own.

## 4. Keep and edit (draft)

**Goal.** I choose which characters are worth keeping, find them by a name I give them, and change
a kept character later without starting over, including follow-ups like "make him warier"
(moved here from "Talk to it"). A kept character may be marked recurring, and the open question
moves with it: what extra does a recurring character get? (moved here from "Better characters").
A kept character can have a fixed name, including one the GM gives in a scene ("an innkeeper called
Bram") or by a flag. Until then the name dice apply and the namer's name wins (ADR-009).

**Constraints.** Saving and loading stay app-only: the model never touches the saved file. Saves made
before this epic still load.

**Done when.** I can list my kept characters, open one by its name, change part of it, and see the
change saved. Runs I didn't keep don't clutter the list.

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
