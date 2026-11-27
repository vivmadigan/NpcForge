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
| 6 | Friends can use it | The front end gets logins, a database and a spending cap, deployed |
| 11 | Change a saved character | Change a saved character without starting over. For later |

Epic 6 comes first. The pages make real characters through `NpcForge.Api` on my machine, with no
logins (epic 14). Epic 6 adds the logins, the database and the deploy, and has choices still open,
listed in it. Epic 11 is a draft: sharpen it before it starts. It waits until after the website.

## 6. Friends can use it

**Goal.** A few friends I invite log in to the website, describe a scene, and get and keep their own
characters. It stops before it costs me more than a cap I set, with the model vendor's own limit
behind that.

**Constraints.**
- Only people I invite can sign up.
- Each person sees only their own characters.
- Spending is capped for each person and in total, on the server, with a limit at the model vendor
  as a backstop.
- Secrets stay out of the repo.
- The website's API calls `NpcForge.Core` in-process (ADR-014), and the console keeps working as it
  does today.
- The model is sent what the console sends it.
- The core gains the second entry ADR-014 names, a way for a run to report progress. Epic 14 adds
  the first. The restructure and behavior rules at the top of this file apply.
- It builds on the API and the pages as epic 14 leaves them. A page changes only where a login or a
  cap forces it.
- A design check sets out the options for each open choice below, I choose, and an ADR records it
  before anything is built.

**Done when.**
- The database store passes the same `CharacterStoreTests` as the file store.
- A test shows two saves at the same moment get different numbers (ADR-015).
- A test logs in as two people and fails if one can read, list or load the other's character. Show
  it failing with the owner filter removed.
- A test shows a run refused, with no model call, once a person's cap or the total cap is reached.
- A test shows a sign-up by someone not invited is refused.
- The replay and roll-again tests pass on the unchanged fixtures.
- A browser test against the site running locally, on a stand-in model, logs in, makes a character,
  logs out and back in, and finds it.
- On the deployed site, a test logs in and lists that person's saves, with no model call.
- A friend makes and keeps a character on their own device with a paid model (*needs my eyes*).
- The model vendor's billing page after a week of use (*needs my eyes*).

**Open, mine to decide.**
- Where it is hosted and which database.
- How friends log in, and who is invited.
- The cap for each person and in total, the period it covers, and which model friends get.
- Whether my console saves stay in the file or move to the database.
- Whether Aspire, which starts the API and the pages on my machine from epic 14, also starts the
  database and deploys the site. It bears on where the site is hosted.
- Who opens the hosting and database accounts, and who deploys. Until I decide: I open the accounts
  and hold the keys, and an agent asks before each deploy, with the command.

**Tasks.**
- ADR: the open choices, and what `docs/design/epic-12-api-needs.md` still leaves open after
  epic 14.
- The database store and logins on epic 14's API, tested locally on a stand-in model.
- Deploy that, with the invite list and both caps, before a paid model is switched on.
- The pages gain a login. The first paid run by a friend.
- Close-out: the map in `docs/architecture.md` states five things this epic changes. They are the
  cap counted once per interview, `Forge` holding interviews in memory with no expiry, the one saves
  file shared under a lock, the API finding the console's user secrets, and the held file's
  `IOException` that no front end reports as one line.

**Not in this epic.** Changing a saved character (epic 11). Sharing characters between friends.

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

One line per epic, with where its record is. What each proved is in `docs/architecture.md` "How it
was built".

- **Steps 1 to 8** (2026-09-12 to 09-22). The loop, the dice, the writing skill and saving, typed by hand.
- **Workflow reset** (2026-10-24). Agents write the code. ADR-006.
- **Safety net** (2026-10-24). A recorded run replays the whole app for free. ADR-005, lesson 007
  and `docs/runs/2026-10-24-01-replay-fixture.md`.
- **Harden the safety net** (2026-10-25). A typo, a free run or a replay cannot reach a model or the
  owner's saves. Lesson 008.
- **The writer reads the scene** (epic 7, 2026-10-25). ADR-008, lesson 009 and
  `docs/runs/2026-10-25-01` to `-03`.
- **Talk to it** (epic 2, 2026-10-25). The scene interview and the spending cap. The tavern scene
  passed on OpenAI and Claude, judged by the owner on runs of their own. ADR-007.
- **Better characters** (epic 3, 2026-10-27). Tone, race and name dice, the namer and the lab.
  ADR-009, lessons 010 and 011 and `docs/runs/2026-10-27-01-epic3-checks.md`.
- **Fill the gaps in a scene** (epic 8, 2026-11-01). ADR-010, lesson 012 and
  `docs/runs/2026-11-01-01-epic8-checks.md`.
- **Read it to the table** (epic 10, 2026-11-03). Look dice and a passage read aloud. ADR-011, lesson
  013 and `docs/runs/2026-11-03-01-epic10-checks.md`.
- **Faster tests** (epic 9, 2026-11-05). The free suite from 12 minutes to about one. ADR-012 and
  lesson 014.
- **Save what I like** (epic 4, 2026-11-08). Saving is the GM's choice; roll again, `--list` and
  `--load`. ADR-013, lessons 015 and 016 and `docs/runs/2026-11-08-01-epic4-checks.md`.
- **Ready to grow** (epic 5, 2026-11-10). One core called in-process; the MCP server retired. ADR-014
  to ADR-016 and lesson 017.
- **A front end to click through** (epic 12, 2026-11-15). The two pages on made-up data. ADR-017,
  lessons 018 to 020 and `docs/design/epic-12-api-needs.md`.
- **A forge in a cavern** (epic 13, 2026-11-16). The pages' look. ADR-018 and lessons 021 and 022.
  Left open: the sheet still repeats the name and names brief fields in its lever headings, which
  needs a paid re-record.
- **The website makes real characters** (epic 14, 2026-11-19). The pages call `NpcForge.Api`. ADR-019
  and ADR-020. Left to my eyes: the paid make, save and reopen in the browser, the Aspire dashboard
  and the failure state's look.
