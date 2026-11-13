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
| 12 | A front end to click through | Two pages on made-up data, with the look of a forge. No backend yet |
| 6 | Friends can use it | The front end gets logins, a database and a spending cap, deployed |
| 11 | Change a saved character | Change a saved character without starting over. For later |

Epic 12 comes first, because its pages decide what epic 6's API has to serve. Epic 6 has choices
still open, listed in it. Epic 11 is a draft: sharpen it before it starts. It waits until after the
website.

## 12. A front end to click through

**Goal.** A simple front end that matches what the console already does, but is not connected to the
core yet. Two pages. On the first I describe a scene and the agent replies on the same page: it
confirms the brief, shows the character, and lets me save or roll again. The second lists my saved
characters and opens one. It feels like a forge in a dungeon: dark, with red.

**Constraints.**
- No backend, no database, no logins and no model call. The data is made up and lives in the front
  end, behind one seam shaped like the API epic 6 will build, so epic 6 replaces that seam and not
  the pages.
- The made-up data is copied from real runs, so the pages are judged on a full-length sheet: the
  characters from `NpcForge.Console/characters.json`, and the brief and its labels from
  `docs/runs/raw/2026-11-08-01-epic4-bakery-roll-again.md`.
- No file in `NpcForge.Core`, `NpcForge.Console` or `NpcForge.Tests` changes, and `dotnet test`
  still passes.
- The pages show what the console shows: the brief's fields marked as my words, inferred, not given
  or rolled, with the words each one came from; the character sheet; and save, again or leave.
- The sheet is the model's Markdown. The page renders it as Markdown and never cuts it up or reads
  meaning out of it (AGENTS.md, "Never parse the model's prose").
- Simple. Two pages, no more.
- The look starts from the sketch I picked, `docs/design/epic-12-sketch.html`: a pixel frame, pixel
  headings and buttons, and the character sheet in a book typeface on parchment, so it stays easy to
  read aloud. Colours and type are set once, in one place.
- ADR-014 names a React app. The rest of the toolchain, including what runs the browser test and
  where that test lives, is settled by a design check and an ADR before any page is built.

**Done when.**
- One command starts it locally (*needs my eyes* too, as it is a new way to start the app).
- A browser test walks the whole flow on the made-up data: describe a scene, confirm the brief, read
  the character, roll again, save, open the saved list, open a character. A second run ends with
  leave and saves nothing.
- A test shows a saved character's sheet on the page with every word of its text, in order.
- Each state can be opened directly on the made-up data, and a screenshot of each at phone and
  desktop width is committed in `docs/design/epic-12/`: the wait while a character is written, the
  interviewer asking for a missing place or role, the brief turned down, no name found for the dice,
  the spending cap reached, an empty saved list and a long one.
- An automated check passes for text contrast on every page, at WCAG AA.
- `docs/design/epic-12-api-needs.md` lists what each page needs from an API. It is the input to
  epic 6.
- How dark, how much red and how much pixel (*needs my eyes*).
- How each page looks and reads at phone and desktop width (*needs my eyes*).

**Open, mine to decide.** Each has a default, so the work can start.
- Phones, computers or both. Until I decide, both.
- Whether anything moves or makes a sound. Until I decide, nothing does.
- The project's name. Until I decide, NpcForge.
- Whether the page shows the rolled brief beside the character. The console prints it, and the
  confirmation does not show rolled values. Until I decide, it does.

**Not in this epic.** Logins, a real API, saving for real, deployment and spending (epic 6).

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
- The core gains the two entries ADR-014 names: an interview that takes one answer and returns the
  next question, and a way for a run to report progress. The restructure and behavior rules at the
  top of this file apply.
- It serves the pages from epic 12. A page changes only where the real API forces it.
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
- Who opens the hosting and database accounts, and who deploys. Until I decide: I open the accounts
  and hold the keys, and an agent asks before each deploy, with the command.

**Tasks.**
- ADR: the open choices, and the API the pages from epic 12 need.
- The API, the database store and logins, tested locally on a stand-in model.
- Deploy that, with the invite list and both caps, before a paid model is switched on.
- Connect the pages. The first paid run by a friend.

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
- **Save what I like** (epic 4, 2026-11-08). After each character the app asks `save`, `again` or
  `leave`, in flag runs too, and only `save` saves. `again` rolls new dice for the same answers and
  scene on the same server, so a seeded run gives the same characters every time. `--list` shows
  every save by number, name, role and place from an app-only `list_characters` tool, `--load` of a
  number never saved stops with the reason, and the spending cap stops a run with `[app] stopped`
  and exit 1. The bakery scene rolled again twice and saved the third, found by `--list` and opened
  by `--load`. See ADR-013 and `docs/runs/2026-11-08-01-epic4-checks.md`.
- **Ready to grow** (epic 5, 2026-11-10). One core, `NpcForge.Core`, holds the dice, the brief, the
  namer, the writer, the interview and the save rules, and the console calls it in-process, as the
  website will. Saves sit behind `ICharacterStore`; the file store and an in-memory store pass the
  same tests, and the compiler keeps vendors, MCP, screens and files out of the core. The MCP server
  is retired. The replay and roll-again tests pass on the unchanged fixtures, a seeded run gives the
  same brief and character, `--list` and `--load` print what they printed, and a field the dice
  cannot read stops the run with one line. See ADR-014, ADR-015, ADR-016 and lesson 017.
