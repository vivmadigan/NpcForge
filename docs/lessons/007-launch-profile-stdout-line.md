# A launch profile puts a line on stdout above the answer

- **Status:** active
- **Date:** 2026-10-24
- **Applies to:** `**/Properties/launchSettings.json`, `NpcForge.Tests/ReplayTests.cs`, `docs/runs/**` · tags: dotnet-run, launch-settings, stdout

## Symptom

The Safety net design added `NpcForge.Console/Properties/launchSettings.json`, with a "Replay
(free)" profile for running the replay under the Visual Studio debugger. With that file present,
every `dotnet run --project NpcForge.Console` printed this as the first line of stdout:

```
Using launch settings from NpcForge.Console\Properties\launchSettings.json...
```

Stdout is the answer, so the line landed above the character in a terminal, in a pipe and in a run
capture. Visual Studio's F5 does not show it, because Visual Studio starts the app itself.

## What did not work

- `"dotnetRunMessages": false` on the default profile. Tried with the replay provider, so no model
  was called. The line still printed.

## What worked

No launch profile in the repo. The free replay command is written out in `docs/runs/README.md`
instead. Anything that starts the console with `dotnet run` and reads its stdout passes
`--no-launch-profile`, which skips the profile and the line. `ReplayTests` does.

## Why it works here, specifically

The project splits the answer (stdout) from the trace (stderr) on purpose (lesson 005). The dotnet
CLI writes this message on the app's own stdout, before the app starts, so nothing in the app can
keep it out. The server is started with `dotnet run` too, and its stdout is the MCP wire, so a
launch profile in `NpcForge.Server` would put the same line on the transport (not tried).

## How to tell it is happening again

A run's stdout, or a run file's console, starting with "Using launch settings from". Or a
`Properties/launchSettings.json` showing in `git status`: Visual Studio writes that file when debug
arguments are set in its UI. Keep it out of the commit, or pass `--no-launch-profile`.
