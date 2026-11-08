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
| 5 | Ready to grow | One core the console and a website both call in-process, ready for a database |
| 6 | Friends can use it | A website with logins and a database, deployed |
| 11 | Change a saved character | Change a saved character without starting over. For later |

Epic 5 is ready to start. Epics 6 and 11 are drafts: sharpen each one before it starts. Epic 11
waits until after the website.

## 5. Ready to grow

**Goal.** A structure that's easy to keep developing. One core holds the dice, the brief, the namer
and the writer, and the rules for saving, listing and loading. The console calls it in-process now,
and a website will call it the same way in epic 6. Saving can move from the file to a database
without rewriting the rest. This comes just before epic 6, when the website and database make it pay.

Today the dice and the save file live in the MCP server, a second process the console starts, and
the namer and writer live in the console. A website would have to start and supervise that second
process, and could not reuse the writer.

**Constraints.**
- My preferred style is Clean Architecture. pstack decides the shape (`architect`), and an ADR
  records it before it's built. That ADR replaces three of AGENTS.md's rules: "two processes, no
  shared contracts project", "no Clean Architecture layering" and "no abstraction until there are
  two" where the core needs a port. It also decides whether the MCP server stays as a thin adapter
  over the core or is retired, once the console no longer starts it.
- The core references no front end, no model vendor (OpenAI, Anthropic), no MCP package and no file
  IO, and the compiler enforces it. It may use `IChatClient` from Microsoft.Extensions.AI, the one
  interface AGENTS.md already keeps.
- The console stays hand-wired, with no host or DI container. `Program.cs` still reads in the order
  things happen. Only the website gets DI, in epic 6.
- The rules that keep the model honest stay: the model never sees the roll, save, load or list,
  it's told or it rolls, and code never parses its prose. The model is sent byte-identical messages
  and tool definitions, so `lookup_archetype` reaches it exactly as today.
- Restructure first, behavior last, in separate PRs (the rule at the top of this file):
  - **Restructure PRs** move code into the core while the console still talks MCP to the server.
    Nothing visible changes. The replay test passes unchanged and is never re-recorded, and tests
    change only in `using` lines and project references.
  - **One behavior PR, last,** switches the console to call the core in-process. The `[server]`
    trace lines and the server's start-up go away. That PR is the only one where a test may change
    beyond `using` lines, and only where it checked the server process or its traces. Each such
    change is listed in the PR. If the switch changes what the model is sent, stop and ask.
- The save format does not change, and my `characters.json` loads and lists as it does today.
- `NpcForge.Console/skills/` is product (AGENTS.md). It may move only if every path to it still
  works, and the tone-file check still runs before any model call.
- Earlier thinking on layers is in steps 10 and 12 of `git show eddc444:ROADMAP.md`.

**Done when.**
- A test fails if the core project references a front end, a model vendor package, an MCP package
  or the file store. Show it failing with such a reference added.
- The file store and a second, in-memory store pass the same save, load and list tests. The tests
  are written once and run against both.
- Before the behavior PR: the replay test and every existing test pass, the fixtures are
  byte-identical, and a script shows that every test-file change is a `using` line or a project
  reference.
- After the behavior PR:
  - The replay and roll-again tests pass against the unchanged fixtures. A seeded run gives the
    same brief and character as before the switch.
  - `--list` and `--load` work on my `characters.json` (a scratch copy).
  - The verify skill passes, updated for the missing `[server]` lines.
- `AGENTS.md`, `docs/architecture.md` and README describe the new shape, and the build history has
  the epic.
- The project layout and its names (*needs my eyes*).
- How a run reads in the terminal without the `[server]` lines (*needs my eyes*).

**Tasks.**
- ADR: the core, its ports, the projects, what happens to the MCP server, and the three AGENTS.md
  rules it replaces.
- Restructure: the dice, the brief and the tables move into the core.
- Restructure: saving, listing and loading move behind a store port. The file store becomes an
  adapter, and an in-memory store with shared tests proves the port.
- Restructure: the namer, writer, scene interview and `CharacterRun` move into the core.
- Behavior: the console calls the core in-process. Close-out.

**Not in this epic.**
- The website, logins, a database and DI anywhere (epic 6).
- Any change to what the model is sent, or to how a character reads.
- Changing a saved character (epic 11).

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
- **Save what I like** (epic 4, 2026-11-08). After each character the app asks `save`, `again` or
  `leave`, in flag runs too, and only `save` saves. `again` rolls new dice for the same answers and
  scene on the same server, so a seeded run gives the same characters every time. `--list` shows
  every save by number, name, role and place from an app-only `list_characters` tool, `--load` of a
  number never saved stops with the reason, and the spending cap stops a run with `[app] stopped`
  and exit 1. The bakery scene rolled again twice and saved the third, found by `--list` and opened
  by `--load`. See ADR-013 and `docs/runs/2026-11-08-01-epic4-checks.md`.
