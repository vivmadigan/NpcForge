# ADR-012: The console starts the server's launcher, on an MCP client that closes stdin first

- **Status:** accepted
- **Date:** 2026-11-05
- **Supersedes:** the "Starting the server" line of `INDEX.md`, which had no record: `dotnet run
  --project <server> --no-build`. The rest of that line (the path from `AppContext.BaseDirectory`,
  build order from a `ProjectReference` with `ReferenceOutputAssembly="false"`) stands.

## Context

Epic 9, "Faster tests". The free suite took 12 m 19 s. Each test that started a server paid about
7.4 s: 2.3 s for `dotnet run` to read the project and start the server, 0.1 s of work, and 5.1 s to
close it. ModelContextProtocol 2.2.0's client waited out its 5 s default `ShutdownTimeout` with the
server's stdin still open, then killed the server (lesson 014). Every run of the app paid the same
5 s at exit. The epic said "a restructure of the tests only". On 2026-11-05 the owner approved two
app changes after two independent reviewers (Opus and Sonnet) reproduced the cause and checked the
fix.

## Options considered

For the close:
1. Tests only. Splitting classes leaves `LabRunTests`, which must run alone, at about 280 s in a
   row, so the suite could not get near the owner's 3 to 4 minutes. Not waiting for a dispose in
   the tests reached about 3 minutes in a reviewer's run, but it hides the fault, and the app keeps
   its 5 s exit.
2. A shorter `ShutdownTimeout` in `McpToolSource`. Dispose took 0.55 s, but the client still killed
   a server that was never told to stop.
3. Both projects on 2.3.1. A reviewer's full suite failed 10 of 455 on a loaded machine: after a
   slow start the client's discovery probe times out, the client falls back to `initialize`, and a
   2.3 server refuses it once that session has settled on the newer protocol version. A 2.3 server
   also reorders `tools/list`, and writes its schema on a newer
   `Microsoft.Extensions.AI.Abstractions` than ADR-010 measured.
4. The console's client on 2.3.1, the server on 2.2.0. Version 2.3.0 closes the server's stdin
   before it waits (csharp-sdk pull request 1838), and a 2.2.0 server accepts the fallback
   `initialize`.

For the start:
- `dotnet run --project <server> --no-build`, as before: a median 2.1 s to the first reply and
  several CPU-seconds, which the tests pay over a hundred times.
- `dotnet <server dll>`: about 0.7 s, but the process is named `dotnet`, and the verify doctor,
  its cleanup and the debugger find the server as `NpcForge.Server`.
- The server's own launcher, `NpcForge.Server.exe`: a median 0.64 s, and the same process name.

## Decision

1. `NpcForge.Console` references ModelContextProtocol 2.3.1. `NpcForge.Server` stays on 2.2.0 until
   the client pins `McpClientOptions.ProtocolVersion`, which a server bump needs first. Each project
   file says why.
2. `McpToolSource` starts `NpcForge.Server/bin/<configuration>/net10.0/NpcForge.Server.exe`
   (`NpcForge.Server` off Windows) directly. The configuration comes from the console assembly's
   `AssemblyConfigurationAttribute` (lesson 008). The framework folder is named outright, because
   the tests run copies of the console from sibling folders of `net10.0`. A missing launcher stops
   the start with the build to run.

## Why

The fault was in the client library, and the library has fixed it, so the fix belongs there and
the server needs no change. Option 4 is the one that is both the root fix and stable under load.
The launcher is as fast as the dll and keeps what the verify skill and the owner rely on.

## Consequences

- The full suite went from 739 s and 740 s to a median 71 s (63, 71 and 88 s), with 455 of 455
  passing. A free replay run of the console's dll takes a median 1.2 s, against 8.1 s before.
- Stdout is byte-identical and the replay passes with no re-record. Every run's stderr now ends with
  8 `[server]` lines for the server's own shutdown.
- The console resolves `Microsoft.Extensions.AI.Abstractions` 10.11.0, while
  `Microsoft.Extensions.AI` and `.OpenAI` stay on 10.10.0. A reviewer got both paid clients past
  request building on it. No free test reads a real paid response on it.
- `net10.0` is now named in `McpToolSource`, `ConsoleProcess` and the verify script, so a framework
  change touches all three.
- Not caused by this change, and found by the reviewer on `2c9f05d` too: in a repo at a deep path,
  `AppContext.BaseDirectory` carries a `\\?\` prefix, `Path.GetFullPath` keeps its `..` segments,
  and the server is not found. A follow-up.
