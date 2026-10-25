# Decisions

The live list of every decision in force, one line each. A decision with reasoning worth keeping
also has a record (ADR) in this folder. Records are written once and never edited: when a decision
changes, a new record supersedes the old one, and only the old one's status line changes. A record
whose detail turns out wrong gets a dated amendment in its status line. Take the next number, and
copy the shape of the newest record: Status, Date, Supersedes, then Context, Options considered,
Decision, Why, Consequences.

| Decision | Choice | Why, or the record |
| --- | --- | --- |
| How work is done | Agents write the code. Work is described as epics in ROADMAP.md (Goal, Constraints, Done when), ceremony matches risk, pstack replaces the crew. Agents commit on their own branch and open PRs; the owner merges, tags and owns `master`. | [ADR-006](ADR-006-agents-write-the-code.md), superseding [ADR-001](ADR-001-crew-and-living-memory.md) |
| Model access | `IChatClient` (Microsoft.Extensions.AI), OpenAI and Anthropic behind `--provider` | The loop is written once and tested against two vendors |
| `IChatModel` from the original design | Not written. `IChatClient` is it. | It already does the job |
| `IToolSource` | Hand-written seam: `ListAsync` and `InvokeAsync` | Swaps a fake tool source for MCP without touching the loop |
| `UseFunctionInvocation` | Off | It would run the tool loop for you, and the loop is the thing this app is built around |
| Host or DI container on the console | None. `Program.cs` wires by hand: config, client, options, connect, roll, run, save. | The roll before the model's first turn has to stay visible. The "Ready to grow" epic revisits this with an ADR |
| `AgentLoop.RunAsync` | Takes `List<ChatMessage>`; `ChatOptions` comes in through the constructor | The caller builds the history, with the skill at index 0; the loop never changes |
| Skills | `SKILL.md` files in the repo, read by the app into a system message | Works with both providers; a vendor's server-side skills would tie the app to one |
| OpenAI endpoint | Responses, not Chat Completions | Chat Completions rejects function tools on reasoning models. [Lesson 001](../lessons/001-openai-responses-endpoint.md) |
| Traces | stderr, one prefix per source: `[app]`, `[skill]`, `[loop]`, `[tool]`, `[server]` | Stdout is the answer, and on the server it is the transport |
| Starting the server | `dotnet run --project <server> --no-build`, path from `AppContext.BaseDirectory`, build order from a `ProjectReference` with `ReferenceOutputAssembly="false"` | Same from the app and the tests, never stale, no shared types, no build output on the wire |
| Occupation | Supplied like the setting, default `"innkeeper"`. Not rolled. | [ADR-002](ADR-002-occupation-in-the-brief.md) |
| Storage | One JSON file on the server, every run saved, brief and text together, loaded by number | [ADR-003](ADR-003-saved-character-is-brief-and-text.md) |
| Tests | xUnit, free: fake models, the real server, no network | Anything that costs money is not a test |
| Recording paid runs | One file per run in `docs/runs/`, pasted from the console; commit first; the `[skill]` line carries a fingerprint | [docs/runs/README.md](../runs/README.md) |
| Replay | Seeded dice through `NPCFORGE_SEED`, set only from `--seed`; `--record`; a `replay` provider; and a free test of the whole app against `NpcForge.Tests/Fixtures/replay.jsonl`. The replay compares every message, the model's own included, every `ChatOptions` field the library serialises, and each tool. Re-recorded only after a behavior change, never for a restructure. | [ADR-005](ADR-005-replay-a-recorded-run.md), amended twice: the model's own messages, and every `ChatOptions` field rather than two. The recording command lives in docs/runs/README.md. |
| The scene interview ("Talk to it") | The model records answers with one console tool, `record_answer(field, value, quote)`; code decides each label. A free-text field takes the GM's own words; only a choice (difficulty, obstacle) may be inferred; anything unrecorded is rolled or takes its default. Labels stay out of the save and the writer. | [ADR-007](ADR-007-scene-interview-labels.md) |
| What the writer is sent | The skill, then one message: the instruction, the GM's scene word for word when there is one, and the brief last. The brief wins where they disagree. The scene is not saved. | [ADR-008](ADR-008-writer-reads-the-scene.md), superseding one line of ADR-007 |

## Records

- [ADR-001 A crew of specialist agents, with versioned decisions and curated lessons](ADR-001-crew-and-living-memory.md) — superseded by ADR-006
- [ADR-002 Occupation joins the character brief, supplied rather than rolled](ADR-002-occupation-in-the-brief.md) — accepted 2026-09-19
- [ADR-003 A saved character is the brief and the written text together, loaded by number](ADR-003-saved-character-is-brief-and-text.md) — accepted 2026-09-22
- [ADR-005 Whole runs are regression-tested by replaying a recorded model](ADR-005-replay-a-recorded-run.md) — accepted 2026-10-01
- [ADR-006 Agents write the code; the user sets the task, the constraints and what done means](ADR-006-agents-write-the-code.md) — accepted 2026-10-24
- [ADR-007 The scene interview records the GM's own words, and only a choice may be inferred](ADR-007-scene-interview-labels.md) — accepted 2026-10-25; one line superseded by ADR-008
- [ADR-008 The writer is sent the GM's scene, word for word, beside the brief](ADR-008-writer-reads-the-scene.md) — accepted 2026-10-25

ADR-004 was reserved for the server layers by a plan that no longer exists, and was never written.
Older records mention plan versions and `PLAN.md`; those are in git at commit `cd20b60`.
