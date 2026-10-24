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
| 1 | Harden the safety net | Close the gaps the Safety net's review found |
| 2 | Talk to it | Describe the scene in a conversation instead of flags |
| 3 | Better characters | Tone, names that don't repeat, and a writers' lab |
| 4 | Keep and edit | Choose what to keep, and change it later |
| 5 | Ready to grow | A structure that can sit behind a website and a database |
| 6 | Friends can use it | A website with logins and a database, deployed |

Epics 1 and 4 to 6 are drafts: sharpen each one before it starts.

## 1. Harden the safety net (draft)

**Goal.** The free replay stays free and honest whatever changes next. A typo can't start a paid
run. A free run can't reach a model even after "Talk to it" moves the model's first turn ahead of
the roll. Nothing free writes over my saved characters. The replay's own sensitivity is tested, so a
restructure can't quietly weaken it.

**Constraints.**
- No paid runs, and `dotnet test` stays free.
- The replay test passes unchanged, and the fixture is not re-recorded.
- Lands before "Talk to it", which moves the model call, and before "Ready to grow", which moves the
  replay code.
- Found by the Safety net's review of its own session, 2026-10-25.

**Done when.**
- A flag the app doesn't know, or a flag given without its value, stops the run before any model
  client is built (free tests).
- With the offline setting on, a run that would call OpenAI or Anthropic fails before it sends,
  shown against the real client code, not a stand-in.
- A replay or a recording run leaves the tracked `characters.json` untouched, and its save exists
  somewhere else (a test that checks both).
- `dotnet test -c Release` replays the Release build, or fails saying the build is stale.
- Free tests show the replay failing on a changed system prompt, an emptied tool list, a changed
  model option and a dropped model turn.

**Tasks.**
- One flag parser, from one table of answers, that also writes line 1 of a recording
- An offline setting that stops the paid model clients being built
- Record and replay save to a temp file unless told otherwise
- The replay test runs the configuration it was built in
- Committed tests of the replay's sensitivity

## 2. Talk to it

**Goal.** I start the app and describe the scene in plain words instead of typing flags. It asks
for whatever is missing, shows me what it understood next to the words I used, and rolls only after
I say yes. Once the character is written, I can ask for changes, like "make him warier".

**Constraints.**
- The model only gathers my answers. The roll still happens in code, after I confirm, and
  `roll_character` stays out of the model's reach.
- Every answer it records has to quote words I actually typed. An answer that adds words of its own
  is flagged at the confirmation. The detailed checks were decided on 2026-09-26: step 13 in
  `git show eddc444:ROADMAP.md`.
- Flags keep working, so the replay test is unchanged.
- A conversation can't run up a bill: a spending cap per session stops it, and Ctrl+C stops the
  run without killing the app.

**Done when.**
- With no flags, the app holds a conversation, asks for what's missing, shows each answer beside my
  words, and waits for my yes before rolling.
- An answer that quotes words I never typed is rejected, and the model can't reach the roll (free
  tests).
- A follow-up like "make him warier" rewrites the character from the same brief.
- The replay test passes unchanged.
- A few real conversations on OpenAI and Claude behave as above (*needs my eyes*, paid).

**Tasks.**
- The scene interview
- Follow-up changes after the character is written
- A spending cap and Ctrl+C
- Open question: is occupation a question in the interview, or does it stay "innkeeper" by default?

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
a kept character later without starting over.

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
