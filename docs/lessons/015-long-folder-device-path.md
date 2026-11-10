# In a long folder the host hands the app a `\\?\` folder, and a walk up with `..` finds nothing

- **Status:** active
- **Date:** 2026-11-08
- **Applies to:** `NpcForge.Console/FileCharacterStore.cs`, `NpcForge.Tests/ConsoleProcess.cs`, `NpcForge.Tests/LongFolderTests.cs`, any code that walks up from `AppContext.BaseDirectory` · tags: paths, windows, long-paths, mcp

## Symptom

From a copy of the repo in a deep folder (a root of about 155 to 175 characters), the four tests
that run a copy of the console build (`ToneFileTests`, `NotGivenFileTests`) failed every run with
`The Debug build of NpcForge.Server is missing (\\?\C:\...\net10.0-tonetest-...\..\..\..\..\NpcForge.Server\...)`.
Before epic 9 they failed with `[server] The provided file path does not exist`. The owner's
checkout and the app's worktrees passed everything.

## What did not work

- Collapsing the `..` and keeping the `\\?\` prefix, with `Path.GetFullPath(relative, basePath)`
  or `DirectoryInfo.Parent`. Both do collapse it, and the server's file is then found, but the
  server will not start: `[server] The system cannot find the path specified.` On Windows,
  ModelContextProtocol 2.3.1's `StdioClientTransport` starts every command as `cmd.exe /c <command>`,
  and `cmd.exe` cannot run a `\\?\` path.
- The same prefixed path in the tests' `dotnet run --project`. MSBuild escapes the `?` as `%3f`
  and stops with MSB4019.

## What worked

Measuring when the prefix appears. Copies of the console whose `NpcForge.Console.dll` path was 258
or 259 characters got a plain folder; at 260 and 261 the host handed back `\\?\`. That is the old
Windows `MAX_PATH`. The one-argument `Path.GetFullPath` leaves a `\\?\` path as it is, `..` and all.
So `McpToolSource.RepoRoot` takes the prefix off `AppContext.BaseDirectory` first and then walks up,
and the tests use that same `RepoRoot`. `LongFolderTests` pads a copy's folder name until its dll
path reaches 260, so the case runs from any checkout, short or deep.

## Why it works here

The repo root is found from the program's own folder, and paths built from it go to `cmd.exe` (the
server's launcher) and to MSBuild (the tests' `dotnet run`), which take only plain paths. The .NET
file APIs take both, so `Storage` keeps the prefix and only collapses the `..`, with the two-argument
`GetFullPath`. Windows will not start an exe whose path is 260 characters or more at all ("The
filename or extension is too long", with or without the prefix, while `LongPathsEnabled` is 0), so
the server meets a `\\?\` folder only when it is run as `dotnet <dll>`.

## How to tell it is happening again

A path in an error that starts `\\?\` and still holds `..`. A `[server]` line that says `The system
cannot find the path specified`. An MSB4019 whose path starts `\\%3f\`. Tests that pass in a normal
checkout and fail only in a deep copy.
