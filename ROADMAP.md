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
| 14 | The website makes real characters | The pages call the real core on my machine, and one command starts it all |
| 6 | Friends can use it | The front end gets logins, a database and a spending cap, deployed |
| 11 | Change a saved character | Change a saved character without starting over. For later |

Epic 14 comes first. The pages exist, on made-up data, with the look of epic 13, and
`docs/design/epic-12-api-needs.md` lists what they need from an API. Epic 14 gives them that API on
my machine, with no logins. Epic 6 then adds the logins, the database and the deploy. Both have
choices still open, listed in them. Epic 11 is a draft: sharpen it before it starts. It waits until
after the website.

## 14. The website makes real characters

**Goal.** On my machine, I describe a scene in the browser and get a real character, from the real
dice and a real model. I save it and open it again. One command starts everything.

**Constraints.**
- My machine only: no logins, no database and no deploy. Epic 6 keeps those.
- The website's API calls `NpcForge.Core` in-process (ADR-014), and the console works and prints as
  it does today.
- The model is sent what the console sends it. No skill file changes, and no tool's name,
  description or schema.
- The core gains one entry, the first of the two ADR-014 names: an interview that takes one answer
  and returns the next step. The restructure and behavior rules at the top of this file apply. A run
  reports no progress, and the flame burns for as long as the call takes.
- `src/api.ts` keeps its six signatures. A page changes only where the real API forces it.
- A second save of one draft returns the first save's number and saves nothing new.
- No test and no agent's run calls a paid model or writes to my `characters.json`.
- The first behavior PR is a thin slice: the saved page lists real saves through the API, the
  pages' proxy and Aspire, with no model call. The interview and the roll go in on top of it.
- A design check sets out the options for each open choice below, and for three more: where the
  model clients and the file store live now that two front ends need them (ADR-014), which projects
  Aspire adds, and how a browser test starts the API when neither suite starts the other (ADR-017).
  The core may not hold the model clients or the file store, and AGENTS.md allows no layer beyond
  the core and its front ends, so the check says which rule a new home would change. I choose, and
  an ADR records it before anything is built.

**Done when.**
- The replay and roll-again tests pass on the unchanged fixtures. Neither fixture interviews, so the
  interview's restructure also shows `SceneInterviewTests` passing unchanged.
- A free test walks the API on a stand-in model: a scene, a question, its answer, the confirmation,
  a roll, a save, the list and a load.
- For one seed and one scene, a test fails when what the API sends the model, every message, option
  and tool over a roll and a roll again, differs from what the core sends when called as
  `Program.cs` calls it. The writer's message holds the brief, so this also holds the dice. Show it
  failing with one message changed.
- A test sends two saves of one draft at the same moment and finds one save. Show it failing with
  the guard removed.
- If the site shows my `characters.json`: a test saves through two stores on one file at the same
  moment and finds two saves with different numbers. Show it failing with the guard removed.
- A test reaches the spending cap and gets the cap's message, with no model call after it.
- A test fails when the field names in `src/api.ts` and `Dice.Fields` differ.
- A free browser test, on the stand-in model, describes a scene, confirms it, reads the character,
  saves it and finds it on the saved page.
- A free browser test shows the failure state when the API does not answer.
- `npm --prefix NpcForge.Web test` still passes contrast on every named state at both widths.
- One command starts the API and the pages, and the Aspire dashboard shows both (*needs my eyes*).
- I make, save and reopen a character in the browser on a paid model (*needs my eyes*).
- How the failure state looks (*needs my eyes*).

**Open, mine to decide.**
- Which saves the site shows. Until I decide: my `NpcForge.Console/characters.json`, so the
  console's characters are on the saved page. The file store takes no lock today and writes the
  whole file in place, so this choice needs a guard for two writers and a test of it. A file of the
  site's own needs the guard only for two saves from the site.
- Which model the site calls, and what the cap counts. Until I decide: the console's provider, model
  and keys, and its 200,000 tokens counted for each interview.
- What the tests use as a stand-in model. Both recorded fixtures are flag runs with no interview,
  and the site always interviews. Until I decide: a scripted fake, as `SceneInterviewTests` and
  `NamerStandIn` already use, which is free. The other option is one paid recording of a scene run.
  The console refuses that today, because `--record` needs the answers as flags, so it needs a
  change to the console first.
- Whether the named states stay on made-up data (ADR-017, "Left to the owner"). Until I decide:
  they stay, because they are how every state's contrast is checked for free, and only the live
  flow calls the API.
- How far Aspire reaches. Until I decide: my machine only, an AppHost and no more. The core's traces
  on stderr show as the API's log in the dashboard.

**Tasks.**
- The design check and the ADR.
- Restructure, if the design check chooses it: the model clients and the file store move to where
  both front ends reach them.
- The thin slice: the API lists and loads saves, the saved page calls it, and Aspire starts both.
- Restructure: the interview in steps, and the console calls the same steps.
- The interview, the roll and the save through the API, tested on the stand-in model.
- The forge page calls the API, with a failure state and the browser test. Close-out.

**Not in this epic.** Logins, the database, a cap for each friend and the deploy (epic 6). Progress
on the wait screen. The sheet repeating the name and naming brief fields in its lever headings, left
over from epic 13, which needs a paid re-record.

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
- **A front end to click through** (epic 12, 2026-11-15). `NpcForge.Web` holds the website's two
  pages on made-up data, with no backend. On the forge page a GM describes a scene, confirms the
  brief, reads the character and saves it, rolls again or leaves; the saved page lists the saves and
  opens one. A page gets data only through the six functions of `src/api.ts`, which epic 6
  replaces. Free browser tests walk the whole flow and a run that leaves, read every word of every
  sheet, and check text contrast at AA on all 16 named states at phone and desktop width, with a
  screenshot of each in `docs/design/epic-12/`. The four defaults held: phones and computers,
  nothing moves, the name NpcForge, and the rolled brief beside the character. See ADR-017, lessons
  018 to 020 and `docs/design/epic-12-api-needs.md`.
- **A forge in a cavern** (epic 13, 2026-11-16). The pages look like a forge in a cavern: a flat header
  with a link to the saved characters, a large NPC FORGE title drawn as a picture over a cavern that
  falls from dark rock to lava, and panels with no text on the picture. The confirmation is the GM's
  scene and a few sentences the page writes from the fields the scene settled, with no field name,
  label or rolled field. The character page has save, roll again and leave at the top beside the
  scene, the parchment centred, the same buttons below it, and no brief. Sending a scene and rolling
  fade to black while a flame burns for as long as the seam takes, and a device that asks for less
  motion gets black at once and a still flame. Browser tests pass AA contrast on all 17 named
  states at both widths, with a screenshot of each in `docs/design/epic-13/`, and the Core, Console
  and Tests folders did not change. The two open choices held their defaults: an opened save shows
  no brief, and the console does not show the confirmation as sentences. Left out: the sheet still
  repeats the name and names brief fields in its lever headings, which needs a paid re-record. See
  ADR-018, lessons 021 and 022 and `docs/design/epic-12-api-needs.md`.
