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
| 8 | Fill the gaps in a scene | Ask for where and who, and never invent a want |
| 9 | Faster tests | The full suite in half the time, with no test weakened |
| 4 | Keep and edit | Choose what to keep, and change it later |
| 5 | Ready to grow | A structure that can sit behind a website and a database |
| 6 | Friends can use it | A website with logins and a database, deployed |

Epics 4 to 6 are drafts: sharpen each one before it starts.

## 8. Fill the gaps in a scene

**Goal.** When my scene leaves out where the character is or who they are, the app asks me. When it
doesn't say what the players want, the app doesn't invent a want. It writes the character for the
situation, and when the scene suggests one, it turns what the character wants into an adventure
hook for the players.

On 2026-10-27 I typed this scene:

> The party bursts into a tiny bakery where a goblin baker is furious that someone keeps stealing his croissants. It’s a silly session, play it for laughs.

I got back save 8. The party wants the name of a fence at Wall, and a cheerfully oblivious baker
doesn't know it. The fence and the Wall are the replay fixture's test values, and "cheerfully
oblivious" overruled my "furious". In my scene the players aren't after anything. The baker wants
something from them: someone to find his croissant thief. That is the hook.

**Constraints.**
- In an interview, where the players meet the character and who the character is (`setting` and
  `occupation`) are asked for when the scene leaves them out, never defaulted. The server has no
  dice for them, and can't pick an occupation that fits a free-text setting (ADR-002). Each gets
  one short question, and my answer shows as `[my words]`. An empty answer asks again.
- What the players want (`playersWant`) is never asked for and never invented. When the scene
  doesn't say it, the brief marks it as not given, and the confirmation shows it as `[not given]`.
  Code tells the writer which case it is in, from the brief.
- With no want given, the levers are about winning the character's goodwill. On their good side
  the players get a better deal, the job or a tip. On their bad side they pay full price, get
  turned away or make an enemy. Difficulty is how hard the character is to win over.
- A hook comes from the scene. When the scene suggests one, like the croissant thief, the
  character offers it to the players. What the scene leaves open stays a gap for me, under the
  writer's rule against inventing (ADR-008). The dice give the twist, through the traits they
  already roll, such as the baker's secret. There is no table of hooks. A rolled hook in the brief
  would beat the scene's own, because the brief wins a clash.
- A difficulty the scene doesn't show is rolled. When the scene points to one, the interview can
  still infer it, as today. How often each difficulty comes up is mine to approve.
- Flag runs keep their defaults, so the replay fixture and `--setting` alone keep working.
- An ADR comes first. It replaces ADR-007's rule that a missing required field takes its flag's
  default, and it changes `roll_character`, because `playersWant` and `difficulty` stop being
  required. Three of its choices are mine:
  - Where the difficulty die goes in the dice order (ADR-009). This decides whether the epic needs
    a paid re-record.
  - Whether the obstacle (Wont, Cant, ForAPrice) still applies when no want is given.
  - Whether my answers join the scene the writer reads word for word (ADR-008), or reach the
    writer only through the brief.

**Done when.**
- Free tests:
  - A scene with no place or no role gets a question for each one it lacks. The answer shows as
    `[my words]`, and no field shows `[default]`.
  - A scene that names a place and a role gets no question, whether or not it gives a want.
  - A scene with no want shows `playersWant` as `[not given]`, and its brief and save say so.
  - The writer is told when no want was given, and is not told when one was.
  - An empty answer asks again. End of input at any question stops without rolling.
  - A scene with no difficulty rolls one and labels it `[rolled]`.
  - A flag run still uses the defaults, and the replay passes.
- The wording of the questions and of `[not given]` (*needs my eyes*).
- The bakery scene above, typed as it is, at `--seed 1` to `--seed 3`. No run asks a question. Each
  character is a furious baker played for laughs, written around winning his goodwill, with the
  croissant thief as the hook (*needs my eyes*, paid).
- Epic 7's control scene (`git show 4d1ee13:ROADMAP.md`) gives a want, and on one seed it still
  makes the death threat the first lever and leaves the town's trouble as a gap (*needs my eyes*,
  paid).

**Tasks.**
- ADR: in an interview, a missing place or role is asked for, a missing want is marked not given,
  and a missing difficulty is rolled (supersedes part of ADR-007).
- The questions for a missing place or role.
- `playersWant` and `difficulty` become optional on the server, with a difficulty die and a table
  I approve.
- The writer's skill: with no want given, write the character around their goodwill, and offer the
  scene's hook when it suggests one.
- In save 8 the interviewer didn't record "furious" as the attitude, so the dice rolled "cheerfully
  oblivious" and the writer followed the brief. If the bakery runs miss it again, find out why and
  fix it.

**Not in this epic.**
- Placeholders for a vague rolled trait, such as "[the secret he is keeping]". The recordings point
  to the writer's rule against inventing (commit `1a5f9df`), and that rule stays. Dice that make a
  vague trait concrete, such as a table of secrets, need an epic of their own.
- A table of complications that bend the scene's hook. It waits until the writers' lab shows hooks
  reading the same.

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
