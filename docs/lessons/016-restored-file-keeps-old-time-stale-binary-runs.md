# A file put back with its old time leaves the break in the build, and nothing says the build is stale

- **Status:** active
- **Date:** 2026-11-08
- **Applies to:** break-it checks, `NpcForge.Tests/ConsoleProcess.cs`, any C# file restored from a copy · tags: tests, build, msbuild, break-it

## Symptom

Epic 4's break-it checks edit a guard out of `NpcForge.Console`, build, show a test fail, then put
the file back. Twice in the epic, the file came back with its old time, and the next build and test
ran the broken binary. Tests failed against source that `git status` called clean, or a check
passed while the binary still held the break.

Reproduced on 2026-11-08: back up `Program.cs` with `cp -p`, change `[app] not saved` to
`[app] BROKEN not saved`, build, then `cp -p` the backup over it. `git status` was clean.
`dotnet build` reported no errors and left `NpcForge.Console.dll` at the time of the broken build.
A replay answered `leave` printed `[app] BROKEN not saved`, and
`KeepQuestionTests.Leave_saves_nothing_says_so_and_exits_0` failed.

## What did not work

- A plain `dotnet build` after the restore. MSBuild compiles only when an input is newer than the
  dll. The restored file is older than the dll the broken build wrote, so the compile is skipped.
- `ConsoleProcess`'s freshness guard (lesson 013). It stops a test when a source file is newer than
  the dll. Here every source file is older, so it passes, and the test runs the broken code.

## What worked

`dotnet build --no-incremental` after every restore. It compiles whatever the times say. The replay
then printed `[app] not saved` and the test passed. A restore that writes the file anew, such as
`git restore` or `git checkout -- <file>`, stamps it with the current time, so a plain build sees it.

## Why it works here

Break-it checks are how this repo proves a test guards something (AGENTS.md: show a new test fail
with the code it guards removed). Each check builds the break into the dll, so the dll ends up newer
than the good source. Whatever puts the source back must also put the compile back, and only a
forced compile does that whatever the restore did to the file's time.

## How to tell it is happening again

After putting a file back, a test fails or passes as if the change were still there, `git status`
and `git diff` are clean, and the dll's time is earlier than the restore. A string from the break
shows up in the run's output.
