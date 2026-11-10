# An edit to a skill file leaves the build stale, and a plain `dotnet build` does not clear it

- **Status:** active
- **Date:** 2026-11-03
- **Applies to:** `NpcForge.Tests/ConsoleProcess.cs`, `NpcForge.Core/skills/**` · tags: tests, build, skill

## Symptom

Epic 10 edited the writer's, namer's and interviewer's `SKILL.md` files and nothing else in
`NpcForge.Console`. After `dotnet build`, six tests that start the app failed in under a second with
"The Debug build of NpcForge.Console is stale: NpcForge.Console\skills\namer\SKILL.md is newer than
the build. Run `dotnet build -c Debug`."

## What did not work

Running `dotnet build`, which the message said to run, and which had just run. The build copied the
new skill files into `bin`, so the verify doctor's "SKILL.md in bin matches the source" passed. But
no C# had changed, so MSBuild skipped the compile and `NpcForge.Console.dll` kept its old time.
`RequireFreshBuild` compares the dll's time with the newest file in the project, the skill included,
so it still saw a stale build.

## What worked

`dotnet build --no-incremental`, which rebuilds the dll and gives it a new time. The six tests then
passed. The guard's message now says to run that.

## Why it works here

The guard is right to count the skill: it is product, copied to `bin`, and a test that runs an old
copy proves nothing (AGENTS.md). Its clock is the dll, which only an actual compile moves. A copy-only
change needs the compile forced.

## How to tell it is happening again

Tests that start the app fail at once, naming a `.md` or other copied file as newer than the build,
right after a build that reported no errors.
