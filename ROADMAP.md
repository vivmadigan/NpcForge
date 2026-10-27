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
| 3 | Better characters | Tone, race, names that don't repeat, and a writers' lab |
| 4 | Keep and edit | Choose what to keep, and change it later |
| 5 | Ready to grow | A structure that can sit behind a website and a database |
| 6 | Friends can use it | A website with logins and a database, deployed |

Epics 4 to 6 are drafts: sharpen each one before it starts.

## 3. Better characters

**Goal.** Characters that differ more from run to run and fit the mood I ask for, through three
levers: tone, race and name dice. Plus a writers' lab, so I judge creative changes by comparing them
side by side, not from memory.

**Constraints.**
- Tone, race and the name dice are given by me (a flag, or my words in the interview), inferred
  where ADR-007 allows a choice, or rolled. The model never picks them.
- Tone (serious, comic, heartfelt) changes how the character is written, not which traits roll.
  Leaving out traits that clash with a tone waits until the lab shows a real clash.
- Race is my words when I give it ("an orc blacksmith"), and rolled from a table otherwise. The
  table and how often each race comes up are mine to approve.
- Name dice are a starting letter and a syllable count, rolled for every character. The writer is
  also told the race, so the name fits the people.
- The lab runs one brief, with the dice fixed by `--seed`, through two or more variants (skills,
  tones, temperature) and shows them side by side. A blind mode hides the labels and scores my
  guesses. Lab runs are paid, so each one asks first, with its cost.
- An ADR comes first: tone, race and name dice join the brief. This changes `roll_character` and
  the save format.
- Old saves still load, with the new fields showing as "unknown". Every table keeps at least six
  entries.

**Done when.**
- Free tests: a given tone or race is kept and a missing one is rolled; the interview infers a tone
  from my scene and labels it; every brief carries name dice; an old save loads with "unknown"; the
  replay passes against the re-recorded fixture; the lab, run with a stand-in model, produces a
  side-by-side sheet and a blind sheet whose answer key matches.
- Blind tone check: six characters from one brief, two of each tone. I name the tone and get at
  least five right (*needs my eyes*, paid).
- Names: six runs give six different first names, each following its dice (*needs my eyes*, paid,
  unless the design finds a structured check).
- Race: an elf and a dwarf from the same brief read as different peoples (*needs my eyes*, paid).

**Tasks.** In this order, because the lab is what makes the other three judgeable:
- ADR: tone, race and name dice join the brief
- The writers' lab, with its blind mode
- Tone
- Race
- Name dice. Open question: can code check that the name follows its dice without reading the
  prose, for example by the writer handing the name back as a tool argument?

**Not in this epic.** Throwaway and recurring characters move to "Keep and edit".

## 8. Fill the gaps in a scene

**Goal.** When my scene leaves something out, the app asks me or rolls it, and never fills the gap
with a test value. A goblin baker whose croissants keep vanishing should not be handed "the name
of a fence" at Wall difficulty because I didn't say what the party wants.

**Constraints.**
- What the players want is mine to say. When the scene doesn't say it, the interview asks me one
  short question, and my answer is recorded as my words. It is never inferred and never invented.
- A difficulty the scene doesn't show is rolled, like the other traits.
- The flag defaults stay for flag runs, so the replay fixture and `--setting` alone keep working.
- This changes ADR-007's rule that a missing required field takes its flag's default, so an ADR
  comes first.

**Done when.**
- Free tests: a scene with no want gets one follow-up question, and the answer shows as
  `[my words]`; end of input at that question stops without rolling; a scene with no difficulty
  rolls one and labels it `[rolled]`; a flag run still uses the defaults; the replay passes.
- The bakery scene from 2026-10-27, typed as it was, gets a question about what the party wants,
  and the character fits the answer (*needs my eyes*, paid).

**Tasks.**
- ADR: a missing want is asked for, a missing difficulty is rolled (supersedes part of ADR-007).
- The follow-up question in the interview.
- Look into the writer leaving a placeholder in the text, as in "keep [the secret he is keeping]
  buried", when a rolled trait is vague. Decide whether it is the writer's skill or the trait's
  wording.

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
