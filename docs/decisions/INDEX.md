# Decisions

The live list of every decision in force, one line each. A decision with reasoning worth keeping
also has a record (ADR) in this folder. Records are written once and never edited: when a decision
changes, a new record supersedes the old one, and only the old one's status line changes. A record
whose detail turns out wrong gets a dated amendment in its status line. Take the next number, and
copy the shape of the newest record: Status, Date, Supersedes, then Context, Options considered,
Decision, Why, Consequences.

| Decision | Choice | Why, or the record |
| --- | --- | --- |
| How work is done | Agents write the code from epics in ROADMAP.md, on their own branches, and open PRs; the owner merges and owns `master`. | [ADR-006](ADR-006-agents-write-the-code.md), superseding [ADR-001](ADR-001-crew-and-living-memory.md) |
| Model access | `IChatClient` (Microsoft.Extensions.AI), OpenAI and Anthropic behind `--provider` | The loop is written once and tested against two vendors |
| `IChatModel` from the original design | Not written. `IChatClient` is it. | It already does the job |
| `IToolSource` | Hand-written seam: `ListAsync` and `InvokeAsync` | Swaps a test's fake for the app's own `ConsoleTool` without touching the loop |
| `UseFunctionInvocation` | Off | It would run the tool loop for you, and the loop is the thing this app is built around |
| Host or DI container | None on the console: `Program.cs` wires by hand, in the order things happen. The website's API has DI. | The roll before the model's first turn has to stay visible. [ADR-014](ADR-014-one-core-called-in-process.md), [ADR-019](ADR-019-the-website-calls-the-core-through-an-api.md) |
| `AgentLoop.RunAsync` | Takes `List<ChatMessage>`; `ChatOptions` comes in through the constructor | The caller builds the history, with the skill at index 0; the loop never changes |
| Skills | `SKILL.md` files in the repo, read by the app into a system message | Works with both providers; a vendor's server-side skills would tie the app to one |
| OpenAI endpoint | Responses, not Chat Completions | Chat Completions rejects function tools on reasoning models. [Lesson 001](../lessons/001-openai-responses-endpoint.md) |
| Traces | stderr, one prefix per source: `[app]`, `[skill]`, `[loop]`, `[tool]`, `[interview]` | Stdout is the answer. [ADR-016](ADR-016-console-calls-the-core.md) |
| Structure | One core, `NpcForge.Core`, holds every shared rule; `NpcForge.Adapters` holds what the front ends share from outside; two ports, `IChatClient` and `ICharacterStore` | [ADR-014](ADR-014-one-core-called-in-process.md), as built in [ADR-016](ADR-016-console-calls-the-core.md); the adapters and the API from [ADR-019](ADR-019-the-website-calls-the-core-through-an-api.md) |
| Occupation | Supplied like the setting, default `"innkeeper"`, not rolled | [ADR-002](ADR-002-occupation-in-the-brief.md) |
| Storage | One JSON file behind `ICharacterStore`; a run saves only when the GM types `save` | [ADR-003](ADR-003-saved-character-is-brief-and-text.md), its "every run saved" superseded by [ADR-013](ADR-013-saving-is-a-choice.md); numbering in [ADR-015](ADR-015-store-numbers-and-guards.md) |
| Tests | Free: fake models, the real dice and tools, no network. The pages have their own Playwright suite, which starts the API; `dotnet test` never starts Node | Anything that costs money is not a test. [ADR-017](ADR-017-front-end-toolchain-and-seam.md) adds the pages' suite |
| Recording paid runs | One file per run in `docs/runs/`, committed first; the `[skill]` line carries a fingerprint | [docs/runs/README.md](../runs/README.md) |
| Replay | A recorded run replays the whole app for free, compared request by request; re-recorded only after a behavior change, never for a restructure | [ADR-005](ADR-005-replay-a-recorded-run.md), amended twice |
| The scene interview | The model records answers through `record_answer`; code decides each label, and only a choice may be inferred | [ADR-007](ADR-007-scene-interview-labels.md) |
| What the writer is sent | The skill, then the GM's scene word for word and the brief last; the brief wins where they disagree | [ADR-008](ADR-008-writer-reads-the-scene.md), superseding one line of ADR-007 |
| Tone, race and name dice | Five brief fields, given, inferred or rolled; one tone file joins the writer's skill; a namer names inside the dice; a lab compares writers | [ADR-009](ADR-009-tone-race-and-name-dice.md) |
| Gaps in a scene | A missing place or role is asked for, a missing want is null (`[not given]`), and a missing difficulty is rolled | [ADR-010](ADR-010-scene-gaps-asked-not-given-or-rolled.md), superseding ADR-007's default rule for interviews |
| The roll's refusals | `Dice.Roll(arguments)` refuses a missing or unreadable field by name, before any die is cast | [ADR-016](ADR-016-console-calls-the-core.md) |
| Look dice and a passage read to the table | Gender, age and look join the brief, and the writer's sheet opens with a passage read aloud | [ADR-011](ADR-011-look-dice-and-a-passage-read-to-the-table.md), amending one line each of ADR-009 and ADR-010 |
| The front end | A Vite, React and TypeScript app in `NpcForge.Web/`; pages get data only from `src/api.ts` | [ADR-017](ADR-017-front-end-toolchain-and-seam.md), amended by [ADR-019](ADR-019-the-website-calls-the-core-through-an-api.md) |
| The website's confirmation | The scene and a few sentences the page writes; no page shows the brief | [ADR-018](ADR-018-website-confirmation-in-sentences.md), amending ADR-007, ADR-010 and ADR-017 for the website only |
| The website's API | `NpcForge.Api`, one route per function of `src/api.ts`, one `Forge` holding each interview in memory, and an Aspire AppHost | [ADR-019](ADR-019-the-website-calls-the-core-through-an-api.md), amending ADR-014, ADR-017 and ADR-018; Aspire pinned to 13.6.5 by [ADR-020](ADR-020-aspire-pinned-to-13-6-5.md) |

## Records

- [ADR-001 A crew of specialist agents, with versioned decisions and curated lessons](ADR-001-crew-and-living-memory.md) — superseded by ADR-006
- [ADR-002 Occupation joins the character brief, supplied rather than rolled](ADR-002-occupation-in-the-brief.md) — accepted 2026-09-19
- [ADR-003 A saved character is the brief and the written text together, loaded by number](ADR-003-saved-character-is-brief-and-text.md) — accepted 2026-09-22; its "every run saved" superseded by ADR-013
- [ADR-005 Whole runs are regression-tested by replaying a recorded model](ADR-005-replay-a-recorded-run.md) — accepted 2026-10-01
- [ADR-006 Agents write the code; the user sets the task, the constraints and what done means](ADR-006-agents-write-the-code.md) — accepted 2026-10-24
- [ADR-007 The scene interview records the GM's own words, and only a choice may be inferred](ADR-007-scene-interview-labels.md) — accepted 2026-10-25; one line superseded by ADR-008, and its default rule for interviews by ADR-010; its confirmation amended for the website by ADR-018
- [ADR-008 The writer is sent the GM's scene, word for word, beside the brief](ADR-008-writer-reads-the-scene.md) — accepted 2026-10-25
- [ADR-009 Tone, race and name dice join the brief, a namer names the character, and a lab compares writers](ADR-009-tone-race-and-name-dice.md) — accepted 2026-10-25; its dice gain a thirteenth from ADR-010, and seven look casts and the namer's gender from ADR-011
- [ADR-010 A scene's gaps are asked for, marked not given, or rolled, as `roll_character` says](ADR-010-scene-gaps-asked-not-given-or-rolled.md) — accepted 2026-10-30; its die is no longer cast last, from ADR-011; decision 4's confirmation line amended for the website by ADR-018
- [ADR-011 Look dice join the brief, and the writer opens with a passage read to the table](ADR-011-look-dice-and-a-passage-read-to-the-table.md) — accepted 2026-11-02
- [ADR-012 The console starts the server's launcher, on an MCP client that closes stdin first](ADR-012-server-launcher-and-mcp-client-2-3.md) — accepted 2026-11-05; superseded by ADR-014 from epic 5's behavior PR
- [ADR-013 Saving is the GM's choice, a run can roll again, and an app-only tool lists the saves](ADR-013-saving-is-a-choice.md) — accepted 2026-11-08
- [ADR-014 One core, called in-process, with saves behind a port; the MCP server retires](ADR-014-one-core-called-in-process.md) — accepted 2026-11-08; supersedes ADR-012; three lines amended by ADR-015; the projects, the adapters' home, and the epic of the API, its DI and the interview's entry amended by ADR-019
- [ADR-015 A store never gives a save a number one already holds, and the guards outlive the server](ADR-015-store-numbers-and-guards.md) — accepted 2026-11-08
- [ADR-016 The console calls the core in-process: the roll refuses by name, and the shapes as built](ADR-016-console-calls-the-core.md) — accepted 2026-11-10; supersedes the INDEX lines on starting the server and on traces
- [ADR-017 The front end is a Vite and React app in `NpcForge.Web`, on made-up data behind one seam](ADR-017-front-end-toolchain-and-seam.md) — accepted 2026-11-14; amends the INDEX line on tests; two defaults, decisions 1, 3 and 6, and one open item amended for the website by ADR-018; decisions 1, 2 and 3 amended by ADR-019
- [ADR-018 The website's confirmation is the scene and a few sentences, written by the page from what the scene settled](ADR-018-website-confirmation-in-sentences.md) — accepted 2026-11-15; amends ADR-007, ADR-010 and ADR-017 for the website only; the epic that sends the entries, and what holds `BriefField` to the core, amended by ADR-019
- [ADR-019 The website calls the core through `NpcForge.Api`, with shared adapters and an Aspire AppHost](ADR-019-the-website-calls-the-core-through-an-api.md) — accepted 2026-11-16; amends ADR-014, ADR-017 and ADR-018; the Aspire version in decision 9 amended by ADR-020
- [ADR-020 Aspire is pinned to 13.6.5, not the 13.4.2 the spike ran](ADR-020-aspire-pinned-to-13-6-5.md) — accepted 2026-11-18; amends ADR-019 decision 9

ADR-004 was reserved for the server layers by a plan that no longer exists, and was never written.
Older records mention plan versions and `PLAN.md`; those are in git at commit `cd20b60`.
