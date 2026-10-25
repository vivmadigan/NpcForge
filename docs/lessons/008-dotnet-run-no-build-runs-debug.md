# A test that starts the app with `dotnet run --no-build` and no `-c` runs the Debug build

- **Status:** active
- **Date:** 2026-10-25
- **Applies to:** `NpcForge.Tests/ConsoleProcess.cs`, `NpcForge.Console/McpToolSource.cs`, `.claude/skills/verify/scripts/npcforge-verify.sh` · tags: dotnet-run, build-configuration, release

## Symptom

`dotnet test` passed. `dotnet test -c Release` failed, or passed while testing code nobody had just
built. Nothing in the failure said "wrong configuration": the messages were a missing
`NpcForge.Console.dll`, a missing server, or a replay that disagreed with the recording.

It happened three times in the "Harden the safety net" epic, each time somewhere new:
1. `ReplayTests` started the console with `dotnet run --no-build` and no `-c`.
2. The console started the server the same way, in `McpToolSource`, so a Release console drove a
   Debug server.
3. `OfflineTests` and `FlagRefusalTests` had each copied the command, and copied the missing `-c`.

## What did not work

- Fixing the one test that failed. Each fix was right and each left the next copy of the command
  untouched.
- Trusting a green `dotnet test`. The Debug build was always there from the earlier run, so the
  Debug-only run could not tell that the test was exercising the wrong build.
- Reading the code. `--no-build` reads like "use what is built", and the configuration it chooses is
  not on the line.

Each was found only by a Release-only run, with the Debug folders deleted first.

## What worked

- Pass the configuration explicitly: `-c <value>`, where the value is the
  `AssemblyConfigurationAttribute` the SDK stamped on the test assembly (and, for the server, on the
  console assembly). It is what `-c` set, not a guess from a path.
- One helper, `ConsoleProcess`, so the command exists once and a fourth test cannot forget the `-c`.
- Check freshness as well: the build in that configuration must exist and be newer than the source,
  or the test fails and says to run `dotnet build -c <configuration>`.
- Prove it the same way each time: delete `bin/Debug`, run `dotnet test -c Release`, and expect a
  pass.

## Why it works here, specifically

`dotnet run --no-build` skips the build but still has to choose a configuration, and with no `-c` it
chooses Debug. The app is two processes started by `dotnet run`, so there are two places to get it
wrong: the test starting the console, and the console starting the server. The tests cannot see the
configuration from a path, because nothing in the path says it.

## How to tell it is happening again

`dotnet test -c Release` fails or behaves differently from `dotnet test` with no code change, or a
Release run passes while `bin/Release` is older than the source. Any `dotnet run` with `--no-build`
and no `-c` in the repo: search for `--no-build` and check each hit names a configuration. The verify
script is the exception on purpose: it drives the Debug build the owner builds by hand.
