# Decisions

The live list of every decision in force, one line each. A decision with reasoning worth keeping
also has a record (ADR) in this folder. Records are written once and never edited: when a decision
changes, a new record supersedes the old one, and only the old one's status line changes. A record
whose detail turns out wrong gets a dated amendment in its status line. Take the next number, and
copy the shape of the newest record: Status, Date, Supersedes, then Context, Options considered,
Decision, Why, Consequences.

| Decision | Choice | Why, or the record |
| --- | --- | --- |
| How work is done | Agents write the code. Tasks are cards (Task, Constraints, Done when), ceremony matches risk, pstack replaces the crew. | [ADR-006](ADR-006-agents-write-the-code.md), superseding [ADR-001](ADR-001-crew-and-living-memory.md) |
| Model access | `IChatClient` (Microsoft.Extensions.AI), OpenAI and Anthropic behind `--provider` | The loop is written once and tested against two vendors |
| `IChatModel` from the original design | Not written. `IChatClient` is it. | It already does the job |
| `IToolSource` | Hand-written seam: `ListAsync` and `InvokeAsync` | Swaps a fake tool source for MCP without touching the loop |
| `UseFunctionInvocation` | Off | It would run the tool loop for you, and the loop is the thing this app is built around |
| Host or DI container on the console | None. `Program.cs` wires by hand: config, client, options, connect, roll, run, save. | The roll before the model's first turn has to stay visible. Step 12 revisits this with an ADR |
| `AgentLoop.RunAsync` | Takes `List<ChatMessage>`; `ChatOptions` comes in through the constructor | The caller builds the history, with the skill at index 0; the loop never changes |
| Skills | `SKILL.md` files in the repo, read by the app into a system message | Works with both providers; a vendor's server-side skills would tie the app to one |
| OpenAI endpoint | Responses, not Chat Completions | Chat Completions rejects function tools on reasoning models. [Lesson 001](../lessons/001-openai-responses-endpoint.md) |
| Traces | stderr, one prefix per source: `[app]`, `[skill]`, `[loop]`, `[tool]`, `[server]` | Stdout is the answer, and on the server it is the transport |
| Starting the server | `dotnet run --project <server> --no-build`, path from `AppContext.BaseDirectory`, build order from a `ProjectReference` with `ReferenceOutputAssembly="false"` | Same from the app and the tests, never stale, no shared types, no build output on the wire |
| Occupation | Supplied like the setting, default `"innkeeper"`. Not rolled. | [ADR-002](ADR-002-occupation-in-the-brief.md) |
| Storage | One JSON file on the server, every run saved, brief and text together, loaded by number | [ADR-003](ADR-003-saved-character-is-brief-and-text.md) |
| Tests | xUnit, free: fake models, the real server, no network | Anything that costs money is not a test |
| Recording paid runs | One file per run in `docs/runs/`, pasted from the console; commit first; the `[skill]` line carries a fingerprint | [docs/runs/README.md](../runs/README.md) |
| Replay | Seeded dice through `NPCFORGE_SEED`, `--record`, a `replay` provider, and a free test of the whole app. Re-recorded only in feature steps. | [ADR-005](ADR-005-replay-a-recorded-run.md), amended: the replay compares the model's own messages too. The re-record command lives in ROADMAP.md step 9. |

## Records

- [ADR-001 A crew of specialist agents, with versioned decisions and curated lessons](ADR-001-crew-and-living-memory.md) — superseded by ADR-006
- [ADR-002 Occupation joins the character brief, supplied rather than rolled](ADR-002-occupation-in-the-brief.md) — accepted 2026-09-19
- [ADR-003 A saved character is the brief and the written text together, loaded by number](ADR-003-saved-character-is-brief-and-text.md) — accepted 2026-09-22
- [ADR-005 Whole runs are regression-tested by replaying a recorded model](ADR-005-replay-a-recorded-run.md) — accepted 2026-10-01
- [ADR-006 Agents write the code; the user sets the task, the constraints and what done means](ADR-006-agents-write-the-code.md) — accepted 2026-10-24

ADR-004 was reserved for the server layers by a plan that no longer exists, and was never written.
Older records mention plan versions and `PLAN.md`; those are in git at commit `cd20b60`.
