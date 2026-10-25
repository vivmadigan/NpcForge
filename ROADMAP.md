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
| 2 | Talk to it | Describe the scene in plain words, and see where each part came from |
| 3 | Better characters | Tone, names that don't repeat, and a writers' lab |
| 4 | Keep and edit | Choose what to keep, and change it later |
| 5 | Ready to grow | A structure that can sit behind a website and a database |
| 6 | Friends can use it | A website with logins and a database, deployed |

Epics 4 to 6 are drafts: sharpen each one before it starts.

## 2. Talk to it

**Goal.** I start the app with no flags. It greets me and briefly says what it needs. I describe the
scene in one go, for example: "My players meet a tavern owner at his tavern to ask about a thieves
guild. He knows nothing about the guild, knows a few people who might, and doesn't want to get
involved." It works out the brief from my words, shows me where each part of it came from, and rolls
only after I say yes. Purpose: test like a real user, so I can improve the dialogue and the scenes
the model receives.

**Constraints.**
- The model never guesses (AGENTS.md). Each field of the brief is one of three things, labelled at
  the confirmation:
  - *My words.* A free-text field takes a span of words I typed, kept as I typed them. A quote I
    never typed is rejected.
  - *Inferred.* Only for a choice (difficulty, obstacle): the model's reading of my words, shown
    beside the words it read, such as difficulty "Wall" from "doesn't want to get involved". Kept
    and labelled. A free-text trait the model can only paraphrase is not recorded, so the dice roll
    it (ADR-007).
  - *Rolled.* I said nothing about it, so the dice decide. The model never fills a gap itself.
- What I say about the character settles that trait instead of the dice: "knows nothing about the
  guild" settles the obstacle as can't, and the dice roll the rest. `roll_character` already takes
  every trait this way; ADR-007 records the design.
- No follow-up questions. A gap is rolled, or takes its default (occupation "innkeeper", ADR-002).
- The roll happens in code after my yes, and `roll_character` stays out of the model's reach.
- Flags keep working and skip the interview, so the replay test is unchanged.
- A spending cap per session stops a conversation running up a bill.
- The matching rules decided on 2026-09-26 still apply (step 13 in `git show eddc444:ROADMAP.md`):
  one normalising function, and whole-word quotes. A quote is one sentence of at most 12 words, and
  the confirmation shows the whole sentence it came from (ADR-007).

**Done when.**
- Free tests: a quote I never typed is rejected; an inferred choice is kept and labelled;
  a free-text value that is not a span of my words is rejected; a field I
  said nothing about is rolled; a trait I described reaches the brief instead of a roll; the model
  can't reach `roll_character`, and nothing is rolled before my yes.
- The spending cap stops a session at its limit (free test, with a stand-in model).
- The replay test passes unchanged.
- The tavern scene above gives a written, saved character with nothing typed but "yes". The
  confirmation shows every field with its label and the words behind it, and the obstacle and
  attitude come from my words, not the dice (*needs my eyes*, paid, on OpenAI and Claude).

**Tasks.**
- ADR: details I give about the character settle those traits
- The scene interview: the interviewer skill, a tool that records each answer with its label and
  quote, and the confirmation
- A spending cap per session

**Not in this epic.** Follow-up changes like "make him warier" move to "Keep and edit". Ctrl+C that
stops a run without closing the app waits until a run is long enough to need it.

## 3. Better characters

**Goal.** Characters that vary more and fit the mood I ask for: a tone (serious, comic or
heartfelt), names that stop repeating, and a way to try different writing styles side by side.

**Constraints.**
- Variety comes from dice, not the model. Tone and name hints are rolled, or set by me, and never
  invented. Each trait table keeps at least six entries for every difficulty and tone, so three runs
  usually differ.
- Characters saved before this epic still load. Fields they lack show as "unknown".
- A writing-style experiment uses the same brief for each style, so only the writing changes.
- The details decided on 2026-09-26 are in step 11 of `git show eddc444:ROADMAP.md`.

**Done when.**
- Three runs at the same answers and tone give three different characters with different levers,
  and each name follows its dice (*needs my eyes*, paid).
- One run at each other tone reads as that tone (*needs my eyes*, paid).
- An old save loads with the new fields as "unknown" (free test).
- I can run two writing styles on the same brief and compare them side by side.

**Tasks.**
- Tone: set or rolled, with clashing traits left out of each tone
- Name dice: a starting letter and a number of syllables
- Throwaway or recurring characters. Open question: what extra does a recurring character get?
- Writers' lab: compare writing styles (skills) on the same brief. Temperature can be one
  experiment.

## 4. Keep and edit (draft)

**Goal.** I choose which characters are worth keeping, find them by a name I give them, and change
a kept character later without starting over, including follow-ups like "make him warier"
(moved here from "Talk to it").

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
