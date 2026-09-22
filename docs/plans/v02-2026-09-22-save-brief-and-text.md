# Plan v02: Steps 5 to 8 done — MCP, rolled briefs, a skill, and saving as brief-plus-text

- **Date:** 2026-09-22
- **Supersedes:** v01
- **Plan file at this version:** `PLAN.md`, uncommitted (working tree past commit `7058568`, "saved characters")
- **Steps done at this version:** 1 to 8

## What this version believes
Two processes, JSON over stdio, unchanged since v01. The console app now talks to a real
`NpcForge.Server` instead of a fake tool source: it rolls the character brief on the server before
the model's first turn (`roll_character` stays off the model's tool list), reads a `SKILL.md` file
into a system message that fixes the output order and the per-obstacle rule for what a `Wall`
obliges the writer to do, and hands the finished text to the model through the loop unchanged since
step 4. Occupation joined the brief, supplied rather than rolled, so who the character is stops
being the model's call. Saving now holds the written text together with the brief that produced it
— the brief alone cannot reproduce a character, because the model writes differently every run —
and every run saves automatically, numbered in order; `--load <number>` prints one back with no
roll and no model call. All eight build steps in `BUILD.md` are done; the README's success test
(three genuinely different characters, the second loaded back unchanged) has evidence in
`docs/runs/`.

## What changed since v01
- The fake tool source was replaced by a real MCP server over stdio, and the model's first turn now
  receives a brief rolled server-side instead of an empty history.
  because: BUILD.md's step 4 stub was always meant to be swapped for MCP without the loop changing,
  and the roll has to be settled before the model's first turn or the model decides the character's
  traits itself.
  driven by: steps 5 and 6 (build order), lesson 003
- Occupation joined the character brief, supplied like `Setting`, not rolled.
  because: three live runs let the model choose who the character is and one came back a patron,
  which breaks README's "three genuinely different innkeepers" test; a rolled default cannot see
  the setting, so it would put "innkeeper" into a farm's brief as settled fact.
  driven by: ADR-002
- A skill (`skills/npc-writer/SKILL.md`, read into a system message) fixed the output order and,
  after twelve paid runs, the difficulty rule became per-obstacle rather than per-difficulty.
  because: three obstacle kinds (`Wont`, `Cant`, `ForAPrice`) produced structurally different
  scenes, and a single "withhold it at a Wall" rule broke two of the three — worst on `Cant`, where
  the character never had the answer to withhold.
  because: runs 02–04, step 7
- Saving now holds the written text together with the brief, autosaved every run, loaded by number
  instead of by name.
  because: the brief alone has no name and the model writes differently every run, so reloading it
  gave the same traits but a new character — it fails "load the second one back unchanged"; a name
  also lives inside the model's free text, which step 7 found is formatted differently by model.
  driven by: ADR-003 (challenger WEAKENED verdict, 2026-09-22)
- Paid runs now have a fixed home and a fixed capture method (`docs/runs/`, one file per run,
  pasted from the console, never redirected).
  because: a PowerShell redirect reorders the trace against the answer, and runs are the only
  evidence for whether a skill edit helped.
  driven by: lesson 005

## Decisions in force
- `IChatClient` (Microsoft.Extensions.AI), both providers behind `--provider`
- `IToolSource` hand-written; MCP is one implementation, the step-4 fake was the other
- `UseFunctionInvocation` off (unrevisited since v01)
- No host or DI container; `Program.cs` wires by hand
- `AgentLoop.RunAsync` takes `List<ChatMessage>`
- Skills as `SKILL.md` files read into a system message at index 0 — built, no longer assumed
- Storage: one JSON file, `characters.json`, every run's brief and written text, numbered — ADR-003
- Occupation: supplied, default `"innkeeper"`, not rolled — ADR-002
- OpenAI on the Responses endpoint, not Chat Completions — lesson 001
- Traces on stderr: `[tool]`, `[server]`, `[app]`, `[skill]`, `[loop]`
- Starting the server: `dotnet run --project --no-build`, path from `AppContext.BaseDirectory`
- Recording paid runs: one file per run in `docs/runs/`, pasted, never redirected
- ADR-001: the crew, versioned decisions, curated lessons

## Lessons that shaped this version
- 001 OpenAI rejects function tools on Chat Completions when reasoning is on
- 003 A csproj comment that mentions a CLI flag stops the project loading
- 004 A test asserting on "Tool failed:" can pass for the wrong reason
- 005 Redirecting a run to a file in PowerShell reorders the trace against the answer
- 006 A run and its --load look different in a console paste when the save is exact

## Open questions carried forward
- Whether `UseFunctionInvocation` should replace the hand-written loop now that it is understood, or
  stay off so the loop remains visible in the debugger (open since v01).
- Ctrl+C wired to the cancellation token (open since v01, still a later nicety).
- README's "known gap": the app has no world to ask about, so local knowledge either rides on the
  "where are we" answer or the model invents a faction.
- The three ideas under `PLAN.md` "After the finish line" (a database behind `Storage.cs`, asking
  for the answers instead of flags, carrying on the conversation after the character is written) are
  talked through but not planned; any of them changes the project's direction and gets its own
  version and a challenge first.
