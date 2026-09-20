# Redirecting a run to a file in PowerShell reorders the trace against the answer

- **Status:** active
- **Date:** 2026-09-20
- **Plan version:** v01 (see docs/plans/INDEX.md)
- **Applies to:** `docs/runs/**`, `NpcForge.Console/AgentLoop.cs`, `NpcForge.Console/Program.cs` · tags: powershell, traces, runs

## Symptom

The plan for `docs/runs/` was to capture each paid run with
`dotnet run --project NpcForge.Console > docs/runs/NN.md 2>&1`. In PowerShell that produces a
file whose lines are in the wrong order. A throwaway app alternating the two streams five times
gave:

```
PowerShell  > file 2>&1        cmd  > file 2>&1
answer 0                       [trace] line 0
[trace] line 0                 answer 0
[trace] line 1                 [trace] line 1
answer 1                       answer 1
answer 2                       [trace] line 2
answer 3                       answer 2
answer 4                       ...exact alternation
[trace] line 2
```

It is also not deterministic: three identical PowerShell redirects of the same command gave two
different orderings.

## What did not work

- `dotnet run ... > file 2>&1` — the form written into the proposal. Scrambled, as above.
- `& $exe *> file` — PowerShell's merge-all operator. Worse: it moved `answer 0` ahead of every
  trace line.
- Strengthening the app's flushing was never attempted, and would not have helped: `Console.Out`
  already auto-flushes. The reordering is in PowerShell's merge of the two native streams, not in
  the app.

## What worked

Run the capture through `cmd`, which preserves the interleaving exactly:

```
cmd /c "dotnet run --project NpcForge.Console > docs\runs\NN.md 2>&1"
```

With the catch that a redirect shows nothing on screen while the run costs money, and is
unavailable under F5. So for runs the user watches, `docs/runs/README.md` says to paste the
console buffer instead: it is the real ordering, it works under F5, and it needs no tooling.

## Why it works here, specifically

This project deliberately splits the two streams — the answer on stdout, and every `[server]`,
`[app]`, `[skill]`, `[loop]` and `[tool]` trace on stderr, because on the server stdout is the MCP
transport (the "Traces" row in PLAN.md's decisions register). That design is what makes the
interleaving the information: `[loop] turn 1: 1 tool call(s)` before the answer and after it mean
different things. In a program that writes everything to one stream there would be nothing to get
wrong.

## How to tell it is happening again

A captured run file where `[app] brief` or a `[loop] turn` line appears *after* the character
text. Re-capture through `cmd`, or paste the console buffer.
